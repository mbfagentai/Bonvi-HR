using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bonvi.Domain.Blanks;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Processes.Facts;

/// <summary>A blank submission as the facts see it — header, raw answers and its steps.</summary>
internal sealed record BlankFactRow(
    Guid Id,
    string TemplateCode,
    BlankSubmissionStatus Status,
    Guid? EmployeeId,
    Guid? EmploymentId,
    Guid? CandidateId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? CompletedAt,
    string Answers,
    string Computed,
    List<BlankFactStep> Steps)
{
    private JsonObject? _answers;
    private JsonObject? _computed;

    public JsonObject AnswersJson => _answers ??= BlankJson.Parse(Answers);

    public JsonObject ComputedJson => _computed ??= BlankJson.Parse(Computed);

    public BlankFactStep? Step(string stepCode) => Steps.Find(step => step.StepCode == stepCode);
}

internal sealed record BlankFactStep(string StepCode, BlankStepState State, string? Decision, DateTimeOffset? ActedAt);

/// <summary>
/// The blank queries of the fact source. They read only <c>blank_submissions</c> and
/// <c>blank_submission_steps</c> (one SQL statement each: the steps are a projected collection).
/// </summary>
internal static class BlankFactRows
{
    /// <summary>Draft, InApproval, Approved. Cancelled and Rejected blanks prove nothing.</summary>
    public static readonly BlankSubmissionStatus[] LiveStatuses =
    [
        BlankSubmissionStatus.Draft,
        BlankSubmissionStatus.InApproval,
        BlankSubmissionStatus.Approved,
    ];

    /// <summary>Live employee-subject blanks of the given templates for the given employees.</summary>
    public static async Task<List<BlankFactRow>> ForEmployeesAsync(
        AppDbContext context,
        string[] templateCodes,
        Guid[] employeeIds,
        CancellationToken cancellationToken)
    {
        if (employeeIds.Length == 0)
        {
            return [];
        }

        return await Project(context.BlankSubmissions.AsNoTracking()
                .Where(submission => templateCodes.Contains(submission.TemplateCode)
                    && LiveStatuses.Contains(submission.Status)
                    && submission.EmployeeId != null
                    && employeeIds.Contains(submission.EmployeeId.Value)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Live candidate-subject blanks of the given templates for the given candidates.</summary>
    public static async Task<List<BlankFactRow>> ForCandidatesAsync(
        AppDbContext context,
        string[] templateCodes,
        Guid[] candidateIds,
        CancellationToken cancellationToken)
    {
        if (candidateIds.Length == 0)
        {
            return [];
        }

        return await Project(context.BlankSubmissions.AsNoTracking()
                .Where(submission => templateCodes.Contains(submission.TemplateCode)
                    && LiveStatuses.Contains(submission.Status)
                    && submission.CandidateId != null
                    && candidateIds.Contains(submission.CandidateId.Value)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Live subject-less blanks of one template created on/after <paramref name="since"/> (HR-F04:
    /// the candidate is inside its answers, so the subject columns cannot narrow the scan — the time
    /// window does; councils are weekly, so this is tens of rows).
    /// </summary>
    public static async Task<List<BlankFactRow>> SubjectlessSinceAsync(
        AppDbContext context,
        string templateCode,
        DateTimeOffset since,
        CancellationToken cancellationToken)
    {
        var sinceUtc = since.ToUniversalTime();

        return await Project(context.BlankSubmissions.AsNoTracking()
                .Where(submission => submission.TemplateCode == templateCode
                    && LiveStatuses.Contains(submission.Status)
                    && submission.CreatedAt >= sinceUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static IQueryable<BlankFactRow> Project(IQueryable<BlankSubmission> submissions)
        => submissions.Select(submission => new BlankFactRow(
            submission.Id,
            submission.TemplateCode,
            submission.Status,
            submission.EmployeeId,
            submission.EmploymentId,
            submission.CandidateId,
            submission.CreatedAt,
            submission.SubmittedAt,
            submission.CompletedAt,
            submission.Answers,
            submission.Computed,
            submission.Steps
                .Select(step => new BlankFactStep(step.StepCode, step.State, step.Decision, step.ActedAt))
                .ToList()));
}

/// <summary>
/// Readers for the blank answers JSON (formats of <c>BlankAnswersValidator</c>): answers are flat by
/// field id; choice = <c>{"value": …}</c>; ref = <c>{"id": "&lt;uuid&gt;", "label"?}</c>; date =
/// <c>"yyyy-MM-dd"</c>; table = array of row objects keyed by column id; computed = plain values.
/// </summary>
internal static class BlankJson
{
    /// <summary>Parses an answers/computed column; anything but a JSON object reads as empty.</summary>
    public static JsonObject Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(json) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }

    /// <summary>A choice value — <c>{"value": "x"}</c>, or a bare string for tolerance.</summary>
    public static string? Choice(JsonNode? node) => node switch
    {
        JsonObject choice => Text(choice["value"]),
        _ => Text(node),
    };

    /// <summary>A ref field's id.</summary>
    public static Guid? RefId(JsonNode? node)
        => node is JsonObject reference && Guid.TryParse(Text(reference["id"]), out var id) ? id : null;

    /// <summary>A date field normalised to <c>yyyy-MM-dd</c>, or <c>null</c>.</summary>
    public static string? Date(JsonNode? node)
        => DateOnly.TryParseExact(Text(node), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;

    /// <summary>A computed boolean as <c>true</c>/<c>false</c>, or <c>null</c> when not computed.</summary>
    public static string? Flag(JsonNode? node)
        => node is JsonValue value && value.TryGetValue<bool>(out var flag) ? (flag ? "true" : "false") : null;

    /// <summary>The rows of a table field.</summary>
    public static IEnumerable<JsonObject> Rows(JsonNode? node)
        => node is JsonArray rows ? rows.OfType<JsonObject>() : [];

    private static string? Text(JsonNode? node)
        => node is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)
            ? text
            : null;
}
