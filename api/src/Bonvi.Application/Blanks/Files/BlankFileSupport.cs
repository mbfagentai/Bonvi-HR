using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Bonvi.Application.Blanks.Definitions;
using Bonvi.Application.Blanks.Submissions;
using Bonvi.Application.Common.Documents;
using Bonvi.Domain.Blanks;

namespace Bonvi.Application.Blanks.Files;

/// <summary>
/// A file field address (SPEC-blankalar §3.5 <c>field_path</c>): a top-level file field
/// (<c>evidence</c>) or a file column of an existing table row (<c>handover[2].doc</c>).
/// </summary>
/// <param name="Path">The normalized path, exactly as stored in <c>blank_attachments.field_path</c>.</param>
/// <param name="RootFieldId">The top-level field — the field itself or its table; decides the section.</param>
/// <param name="Field">The file field (or file column) definition — accept / maxFiles / maxSizeMb.</param>
/// <param name="Row">Table row index; <c>null</c> for a top-level field.</param>
/// <param name="ColumnId">Table column id; <c>null</c> for a top-level field.</param>
public sealed partial record BlankFilePath(string Path, string RootFieldId, BlankField Field, int? Row, string? ColumnId)
{
    /// <summary>Resolves <paramref name="path"/> against the definition; <c>null</c> when it is not a file field.</summary>
    public static BlankFilePath? Resolve(BlankDefinition definition, string? path)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var match = PathPattern().Match(path?.Trim() ?? string.Empty);
        if (!match.Success)
        {
            return null;
        }

        var root = definition.FindField(match.Groups["field"].Value);
        if (root is null)
        {
            return null;
        }

        if (!match.Groups["row"].Success)
        {
            return root.Type == BlankFieldType.File ? new BlankFilePath(root.Id, root.Id, root, null, null) : null;
        }

        var row = int.Parse(match.Groups["row"].Value, CultureInfo.InvariantCulture);
        var column = root.Type == BlankFieldType.Table ? root.FindColumn(match.Groups["column"].Value) : null;
        return column is { Type: BlankFieldType.File } && row < root.EffectiveMaxRows
            ? new BlankFilePath($"{root.Id}[{row}].{column.Id}", root.Id, column, row, column.Id)
            : null;
    }

    /// <summary>
    /// The upload limit of this field in bytes: its <c>maxSizeMb</c> (default 20), never above
    /// <see cref="DocumentLimits.MaxBytes"/>.
    /// </summary>
    public long MaxBytes => Math.Min((long)(Field.MaxSizeMb ?? 20) * 1024 * 1024, DocumentLimits.MaxBytes);

    /// <summary>The field's <c>accept</c> as flags (the definition validator requires at least one value).</summary>
    public BlankFileAccept Accept => Field.Accept.Aggregate(BlankFileAccept.None, (flags, item) => flags | item switch
        {
            "pdf" => BlankFileAccept.Pdf,
            "image" => BlankFileAccept.Image,
            "office" => BlankFileAccept.Office,
            "any" => BlankFileAccept.Any,
            _ => BlankFileAccept.None,
        });

    /// <summary>
    /// Appends the attachment id to the field's answer. A table cell needs its row to exist already
    /// (the row is saved with the form first) — returns <c>false</c> otherwise.
    /// </summary>
    public bool Link(JsonObject answers, Guid attachmentId)
    {
        ArgumentNullException.ThrowIfNull(answers);

        var cell = CellOf(answers, create: true);
        if (cell is null)
        {
            return false;
        }

        cell.Add(attachmentId.ToString());
        return true;
    }

    /// <summary>Removes the attachment id from the field's answer, if present; an emptied field is dropped.</summary>
    public void Unlink(JsonObject answers, Guid attachmentId)
    {
        ArgumentNullException.ThrowIfNull(answers);

        if (CellOf(answers, create: false) is not { } cell)
        {
            return;
        }

        var id = attachmentId.ToString();
        foreach (var item in cell.Where(item => item is JsonValue value
                     && value.TryGetValue<string>(out var text)
                     && string.Equals(text, id, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            cell.Remove(item);
        }

        if (cell.Count == 0)
        {
            Owner(answers)?.Remove(ColumnId ?? RootFieldId);
        }
    }

    private JsonObject? Owner(JsonObject answers)
        => Row is not { } row
            ? answers
            : answers[RootFieldId] is JsonArray rows && row < rows.Count ? rows[row] as JsonObject : null;

    private JsonArray? CellOf(JsonObject answers, bool create)
    {
        var owner = Owner(answers);
        var key = ColumnId ?? RootFieldId;
        if (owner is null)
        {
            return null;
        }

        if (owner[key] is JsonArray existing)
        {
            return existing;
        }

        if (!create)
        {
            return null;
        }

        var created = new JsonArray();
        owner[key] = created;
        return created;
    }

    [GeneratedRegex(@"^(?<field>[a-z][A-Za-z0-9]{0,47})(?:\[(?<row>\d{1,3})\]\.(?<column>[a-z][A-Za-z0-9]{0,47}))?$", RegexOptions.CultureInvariant)]
    private static partial Regex PathPattern();
}

/// <summary>Shared rules of the attachment use cases: who may change a field now, file names, error mapping.</summary>
public sealed class BlankFileAccess
{
    private readonly BlankActorResolver _actors;
    private readonly BlankSubmissionDetails _details;

    public BlankFileAccess(BlankActorResolver actors, BlankSubmissionDetails details)
    {
        _actors = actors;
        _details = details;
    }

    /// <summary>
    /// The sections the caller may change now (SPEC §5) — the same rule as saving answers: Draft →
    /// draft and <c>open</c> sections for <c>blanks.fill</c>; InApproval → sections of Open steps the
    /// caller may act on, plus <c>open</c> sections for <c>blanks.fill</c>.
    /// </summary>
    public async Task<IReadOnlySet<string>> EditableSectionsAsync(
        BlankSubmission submission,
        BlankDefinition definition,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(submission);

        var caller = await _actors.CallerAsync(cancellationToken).ConfigureAwait(false);
        var actionable = submission.Status == BlankSubmissionStatus.InApproval
            ? await _details.ActionableAsync(
                    caller,
                    submission.Steps.Where(step => step.State == BlankStepState.Open)
                        .Select(step => (step.StepCode, step.ActorRole, step.Mode)),
                    submission.EmployeeId,
                    submission.DepartmentId,
                    BlankSubmissionRules.ParseObject(submission.Answers),
                    cancellationToken)
                .ConfigureAwait(false)
            : [];

        return BlankSubmissionRules.EditableSections(
            definition,
            submission.Status,
            caller.CanFill,
            actionable.Select(step => step.StepCode));
    }

    /// <summary>The caller's user id (the audit «uploaded_by» / «removed_by»).</summary>
    public async Task<Guid?> UserIdAsync(CancellationToken cancellationToken)
        => (await _actors.CallerAsync(cancellationToken).ConfigureAwait(false)).UserId;

    /// <summary>
    /// Recomputes completeness after the file answer changed. Answers are not re-validated: only the
    /// file id list moved, and re-running date bounds such as <c>today</c> on old answers would fail an
    /// upload for an unrelated field. Computed values do not depend on files and are kept.
    /// </summary>
    public static BlankAnswersState StateAfterFileChange(BlankSubmission submission, BlankDefinition definition, JsonObject answers)
    {
        ArgumentNullException.ThrowIfNull(submission);

        return BlankSubmissionRules.BuildState(
            definition,
            new BlankAnswersOutcome(answers, BlankSubmissionRules.ParseObject(submission.Computed), []),
            submission.Steps.Select(step => new BlankStepSnapshot(step.StepCode, step.OrderNo, step.State, step.DueDate)).ToList());
    }

    /// <summary>
    /// The stored display name: the client's base name without directories, control, format (bidi,
    /// zero-width) or reserved characters, with the extension of the DETECTED kind.
    /// </summary>
    public static string SafeFileName(string? clientName, BlankFileKind kind)
    {
        var extension = BlankFileKinds.ExtensionFor(kind);
        var lastSegment = (clientName ?? string.Empty).Replace('\\', '/').Split('/')[^1];
        var baseName = global::System.IO.Path.GetFileNameWithoutExtension(lastSegment);

        var clean = new string(baseName.Where(IsSafe).ToArray()).Trim().Trim('.').Trim();
        if (clean.Length == 0)
        {
            clean = "fayl";
        }

        var room = BlankAttachment.MaxFileNameLength - extension.Length;
        return (clean.Length > room ? clean[..room] : clean) + extension;
    }

    /// <summary>Inspector rejection → error code (SPEC §7 error table).</summary>
    public static Domain.Common.Error ErrorFor(BlankFileRejection rejection) => rejection switch
    {
        BlankFileRejection.MacroEnabled => BlankErrors.FileMacro,
        BlankFileRejection.LegacyOrEncrypted => BlankErrors.FileLegacyFormat,
        BlankFileRejection.ArchiveTooLarge => BlankErrors.FileTooLarge,
        _ => BlankErrors.FileKind,
    };

    private static bool IsSafe(char symbol)
        => !char.IsControl(symbol)
           && CharUnicodeInfo.GetUnicodeCategory(symbol) is not (UnicodeCategory.Format or UnicodeCategory.Surrogate
               or UnicodeCategory.PrivateUse or UnicodeCategory.OtherNotAssigned)
           && symbol is not ('"' or '<' or '>' or ':' or '|' or '?' or '*' or '/' or '\\');
}
