using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Bonvi.Application.Blanks.Definitions;
using Bonvi.Application.Blanks.Pdf;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Blanks;
using Bonvi.Infrastructure.Reports.Pdf;
using Microsoft.Extensions.Logging;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using SkiaSharp;

namespace Bonvi.Infrastructure.Blanks.Pdf;

/// <summary>
/// Generic blank PDF (SPEC-blankalar §10): the definition drives the layout, no per-template code.
/// </summary>
/// <remarks>
/// <para>
/// Visual language is HR-F02's (<see cref="QuestionnairePdfRenderer"/>): A4, 1.5 cm margins, navy
/// <c>#1F3A5F</c> titles, <c>#F2F5F9</c> label cells, <c>#AEB8C6</c> rules, the same bundled fonts and
/// logo. Fonts come from <see cref="BundledFontResolver.EnsureInstalled"/> — the process-wide resolver is
/// shared with the questionnaire PDF and installed at most once (PDFsharp allows only one).
/// </para>
/// <para>
/// Each visible section becomes a navy title plus a two-column label/value table; a <c>table</c> field
/// breaks out into its own full-width grid (repeating header, fixed rows printed, three empty rows when the
/// table is empty so it can be filled by hand). Hidden fields print «—», hidden sections are skipped (§4.4).
/// Long free text that could outgrow a table row is printed as a flowing paragraph instead — MigraDoc
/// never splits a single row across pages.
/// </para>
/// </remarks>
public sealed partial class BlankPdfRenderer : IBlankPdfRenderer
{
    private static readonly Color Navy = new(0x1F, 0x3A, 0x5F);
    private static readonly Color LabelFill = new(0xF2, 0xF5, 0xF9);
    private static readonly Color Muted = new(0x6B, 0x76, 0x86);
    private static readonly Color Rule = new(0xAE, 0xB8, 0xC6);
    private static readonly Color Ink = new(0x1A, 0x1F, 0x2B);
    private static readonly Color Tick = new(0x1E, 0x8E, 0x3E);
    private static readonly Color Warning = new(0xB4, 0x23, 0x18);

    /// <summary>Usable width of an A4 page with 1.5 cm margins.</summary>
    private const double PageWidth = 18.0;

    /// <summary>Signature images larger than this are refused before decoding (decompression bomb guard).</summary>
    private const int MaxSignatureEdge = 4096;

    /// <summary>
    /// At most two blank PDFs render at once — same budget as the questionnaire renderer
    /// (2-core / 1.9 GB server); a third request waits, it is not refused.
    /// </summary>
    private static readonly SemaphoreSlim RenderGate = new(2, 2);

    private readonly ILogger<BlankPdfRenderer> _logger;

    public BlankPdfRenderer(ILogger<BlankPdfRenderer> logger) => _logger = logger;

    [LoggerMessage(EventId = 4720, Level = LogLevel.Warning, Message = "Blank {Code} №{Number}: signature of step {Step} left out of the PDF")]
    private partial void LogSignatureSkipped(Exception failure, string code, int? number, string step);

    public async Task<byte[]> RenderAsync(BlankPdfModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        await RenderGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return Render(model);
        }
        finally
        {
            RenderGate.Release();
        }
    }

    private byte[] Render(BlankPdfModel model)
    {
        BundledFontResolver.EnsureInstalled();

        var logo = Base64(BundledFontResolver.ReadResource("Assets.mbf-logo.png"));
        var signatures = model.Steps.Select(step => PrepareSignature(model, step)).ToList();
        var document = new Builder(model, logo, signatures).Build();

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();

        using var output = new MemoryStream();
        renderer.PdfDocument.Save(output, closeStream: false);

        return output.ToArray();
    }

    /// <summary>Flattens a signature PNG onto white; a broken image leaves the cell empty instead of failing the PDF.</summary>
    private string? PrepareSignature(BlankPdfModel model, BlankPdfStep step)
    {
        if (step.SignaturePng is not { Length: > 0 } source)
        {
            return null;
        }

        try
        {
            using var data = SKData.CreateCopy(source);
            using var codec = SKCodec.Create(data) ?? throw new InvalidDataException("signature is not an image");

            if (codec.Info.Width <= 0 || codec.Info.Height <= 0 || codec.Info.Width > MaxSignatureEdge || codec.Info.Height > MaxSignatureEdge)
            {
                throw new InvalidDataException("signature dimensions out of range");
            }

            using var bitmap = SKBitmap.Decode(source) ?? throw new InvalidDataException("signature not decoded");
            using var surface = SKSurface.Create(new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Rgb888x, SKAlphaType.Opaque));
            surface.Canvas.Clear(SKColors.White);
            surface.Canvas.DrawBitmap(bitmap, 0, 0);

            using var image = surface.Snapshot();
            using var png = image.Encode(SKEncodedImageFormat.Png, 100) ?? throw new InvalidDataException("signature not encoded");

            return Base64(png.ToArray());
        }
#pragma warning disable CA1031 // A broken signature must not block the document — the cell stays empty
        catch (Exception failure)
#pragma warning restore CA1031
        {
            LogSignatureSkipped(failure, model.Definition.Code, model.Number, step.StepId);
            return null;
        }
    }

    private static string Base64(byte[] bytes) => "base64:" + Convert.ToBase64String(bytes);

    /// <summary>Builds one MigraDoc document; holds the per-render state.</summary>
    private sealed partial class Builder(BlankPdfModel model, string logo, IReadOnlyList<string?> signatures)
    {
        private const string Checked = "☑";
        private const string Unchecked = "☐";
        private const string Dash = "—";
        private const string ComputedMark = " (hisoblangan)";

        /// <summary>Longer free text leaves the label/value grid and flows as a paragraph (can break pages).</summary>
        private const int InlineTextLimit = 600;

        /// <summary>Rough character budget for putting all options of a choice on one line (~12 cm cell).</summary>
        private const int ChoiceLineBudget = 70;

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        /// <summary>«3 000 000», «3,57» — non-breaking group separator so a number never wraps.</summary>
        private static readonly NumberFormatInfo Numbers = new()
        {
            NumberGroupSeparator = " ",
            NumberDecimalSeparator = ",",
            NumberGroupSizes = [3],
        };

        private readonly BlankDefinition _definition = model.Definition;
        private readonly Document _document = new();
        private Section _section = null!;

        /// <summary>Label/value grid of the current section; <c>null</c> after a block that broke out of it.</summary>
        private Table? _grid;

        public Document Build()
        {
            Styles();
            Page();
            TitleBlock();
            InfoRow();

            foreach (var section in _definition.Sections)
            {
                if (IsVisible(section.When))
                {
                    RenderSection(section);
                }
            }

            Signatures();
            Provenance();

            return _document;
        }

        // ════════════════════════════════════════════════ page and styles

        private void Styles()
        {
            _document.Info.Title = $"{_definition.Code} {_definition.Title}" + (model.Number is { } number ? $" №{number}" : string.Empty);
            _document.Info.Author = "Bonvi HR";

            var normal = _document.Styles[StyleNames.Normal]!;
            normal.Font.Name = BundledFontResolver.Sans;
            normal.Font.Size = 9;
            normal.Font.Color = Ink;
            normal.ParagraphFormat.SpaceBefore = 0;
            normal.ParagraphFormat.SpaceAfter = 0;
        }

        private void Page()
        {
            _section = _document.AddSection();

            var setup = _section.PageSetup;
            setup.PageFormat = PageFormat.A4;
            setup.Orientation = Orientation.Portrait;
            setup.TopMargin = Unit.FromCentimeter(1.5);
            setup.BottomMargin = Unit.FromCentimeter(1.5);
            setup.LeftMargin = Unit.FromCentimeter(1.5);
            setup.RightMargin = Unit.FromCentimeter(1.5);
            setup.HeaderDistance = Unit.FromCentimeter(0.74);
            setup.FooterDistance = Unit.FromCentimeter(0.74);

            var header = _section.Headers.Primary.AddTable();
            header.Borders.Visible = false;
            header.AddColumn(Unit.FromCentimeter(PageWidth / 2));
            header.AddColumn(Unit.FromCentimeter(PageWidth / 2));
            header.Format.Font.Size = 8;
            header.Format.Font.Color = Muted;

            var row = header.AddRow();
            var left = row.Cells[0].AddParagraph();

            if (IsDraftLegalStatus)
            {
                var draft = left.AddFormattedText("LOYIHA", TextFormat.Bold);
                draft.Font.Color = Warning;
                left.AddText(" · ");
            }

            left.AddText(_definition.Organization ?? "MBF Company — HR hujjatlar paketi");

            var right = row.Cells[1].AddParagraph($"{_definition.Code} · {_definition.Title} · v{_definition.Version}");
            right.Format.Alignment = ParagraphAlignment.Right;

            var footer = _section.Footers.Primary.AddParagraph();
            footer.Format.Alignment = ParagraphAlignment.Center;
            footer.Format.Font.Size = 8;
            footer.Format.Font.Color = Muted;
            footer.AddText("Sahifa ");
            footer.AddPageField();
            footer.AddText(" / ");
            footer.AddNumPagesField();
        }

        private bool IsDraftLegalStatus => string.Equals(_definition.LegalStatus, "draft", StringComparison.Ordinal);

        // ════════════════════════════════════════════════ title and info row

        private void TitleBlock()
        {
            var table = _section.AddTable();
            table.Borders.Visible = false;
            table.AddColumn(Unit.FromCentimeter(4.0));
            table.AddColumn(Unit.FromCentimeter(PageWidth - 4.0));

            var row = table.AddRow();
            row.VerticalAlignment = VerticalAlignment.Center;

            var image = row.Cells[0].AddParagraph().AddImage(logo);
            image.Width = Unit.FromCentimeter(3.2);
            image.LockAspectRatio = true;

            var cell = row.Cells[1];
            var title = cell.AddParagraph();
            var text = title.AddFormattedText(_definition.Title, TextFormat.Bold);
            text.Font.Size = 16;
            text.Font.Color = Navy;

            var form = cell.AddParagraph($"Blanka {_definition.Code} · Versiya {_definition.Version}");
            form.Format.SpaceBefore = 3;
            form.Format.Font.Size = 8.5;
            form.Format.Font.Color = Muted;

            if (IsDraftLegalStatus)
            {
                var draft = cell.AddParagraph();
                draft.Format.SpaceBefore = 3;
                var line = draft.AddFormattedText("Loyiha — yurist tasdig'idan o'tmagan", TextFormat.Bold);
                line.Font.Size = 9.5;
                line.Font.Color = Warning;
            }

            Spacer(8);
        }

        private void InfoRow()
        {
            var table = Grid(1.0, 2.2, 1.2, 2.4, 1.9, 4.6, 1.6, 3.1);
            var row = Row(table);

            Label(row.Cells[0], "№");
            Value(row.Cells[1], model.Number?.ToString(Invariant)).Format.Font.Bold = true;
            Label(row.Cells[2], "Sana");
            Value(row.Cells[3], model.DocumentDate is { } date ? FormatDate(date) : null);

            var subject = _definition.Subject switch
            {
                "employee" => "Xodim",
                "candidate" => "Nomzod",
                _ => null,
            };

            Label(row.Cells[4], subject ?? "Sub'ekt");
            Value(row.Cells[5], subject is null ? Dash : model.SubjectName);
            Label(row.Cells[6], "Bo'lim");
            Value(row.Cells[7], model.DepartmentName);

            _grid = null;
        }

        // ════════════════════════════════════════════════ sections and fields

        private void RenderSection(BlankSection section)
        {
            Title(section.Title);

            if (!string.IsNullOrWhiteSpace(section.Hint))
            {
                Hint(section.Hint);
            }

            _grid = null;

            foreach (var field in section.Fields)
            {
                var visible = IsVisible(field.When);

                switch (field.Type)
                {
                    case BlankFieldType.Table when visible:
                        RenderTable(field);
                        break;
                    case BlankFieldType.Static:
                        if (visible)
                        {
                            StaticRow(field);
                        }

                        break;
                    case BlankFieldType.LongText when visible && Str(Answer(field.Id)) is { Length: > InlineTextLimit } longText:
                        TextBlock(field, longText);
                        break;
                    default:
                        FieldRow(field, visible);
                        break;
                }
            }

            _grid = null;
        }

        private Table CurrentGrid() => _grid ??= Grid(5.4, PageWidth - 5.4);

        private void FieldRow(BlankField field, bool visible)
        {
            var row = Row(CurrentGrid());
            LabelWithHint(row.Cells[0], field);
            var cell = row.Cells[1];

            if (!visible)
            {
                Value(cell, Dash).Format.Font.Color = Muted;
                return;
            }

            var answer = Answer(field.Id);

            switch (field.Type)
            {
                case BlankFieldType.Choice or BlankFieldType.Multi:
                    Options(cell, field.Options, Selected(answer));
                    break;
                case BlankFieldType.YesNo:
                    var yes = Bool(answer);
                    Boxes(cell, [("Ha", yes == true), ("Yo'q", yes == false)]);
                    break;
                case BlankFieldType.Check:
                    Boxes(cell, [(string.Empty, Bool(answer) == true)]);
                    break;
                case BlankFieldType.Computed:
                    ComputedValue(cell, field, model.Computed[field.Id]);
                    break;
                case BlankFieldType.LongText:
                    MultiLine(cell, Str(answer));
                    break;
                case BlankFieldType.File:
                    MultiLine(cell, Files(field.Id, answer));
                    break;
                case BlankFieldType.Table:
                    // Hidden table — prints like any hidden field (visible tables never get here)
                    Value(cell, Dash).Format.Font.Color = Muted;
                    break;
                default:
                    Value(cell, Format(field, answer));
                    break;
            }
        }

        /// <summary>Static text across the full width, <c>{{fieldId}}</c> placeholders filled in.</summary>
        private void StaticRow(BlankField field)
        {
            var row = Row(CurrentGrid());
            row.Cells[0].MergeRight = 1;

            if (!string.IsNullOrWhiteSpace(field.Label))
            {
                row.Cells[0].AddParagraph().AddFormattedText(field.Label, TextFormat.Bold);
            }

            var text = Placeholder().Replace(field.Text ?? string.Empty, match =>
                _definition.FindField(match.Groups[1].Value) is { } target && Format(target, Answer(target.Id)) is { } value
                    ? value
                    : "________");

            var paragraph = MultiLine(row.Cells[0], text);
            paragraph.Format.Font.Italic = true;
        }

        /// <summary>Long free text as a flowing paragraph outside the grid — it may span pages.</summary>
        private void TextBlock(BlankField field, string text)
        {
            _grid = null;

            var label = _section.AddParagraph();
            label.Format.SpaceBefore = 4;
            label.Format.SpaceAfter = 2;
            label.Format.KeepWithNext = true;
            label.AddFormattedText(field.Label ?? field.Id, TextFormat.Bold);

            var body = _section.AddParagraph();
            body.Format.Borders.Color = Rule;
            body.Format.Borders.Width = 0.5;
            body.Format.Borders.Distance = Unit.FromCentimeter(0.15);
            body.Format.LeftIndent = Unit.FromCentimeter(0.15);
            body.Format.RightIndent = Unit.FromCentimeter(0.15);
            body.Format.SpaceAfter = 4;
            AddLines(body, text);
        }

        // ════════════════════════════════════════════════ tables

        private void RenderTable(BlankField table)
        {
            _grid = null;
            var fixedRows = table.FixedRows.Count > 0;

            // A fixed-row table prints its label as the header of the row-label column instead
            if (!fixedRows && !string.IsNullOrWhiteSpace(table.Label))
            {
                var caption = _section.AddParagraph();
                caption.Format.SpaceBefore = 4;
                caption.Format.SpaceAfter = 2;
                caption.Format.KeepWithNext = true;
                caption.AddFormattedText(table.Label, TextFormat.Bold);
            }

            var shares = new List<double> { fixedRows ? 2.5 : 0 };
            shares.AddRange(table.Columns.Select(column => (double)(column.PdfWidth ?? DefaultShare(column.Type))));

            // The «№» column of a free table has a fixed narrow width; the rest share the remaining width
            const double NumberWidth = 0.8;
            var free = fixedRows ? PageWidth : PageWidth - NumberWidth;
            var total = shares.Sum();
            var widths = shares.Select((share, index) => index == 0 && !fixedRows ? NumberWidth : free * share / total).ToArray();

            var grid = Grid(widths);
            grid.Format.Font.Size = 8.5;

            var header = grid.AddRow();
            header.HeadingFormat = true;
            header.VerticalAlignment = VerticalAlignment.Center;
            HeaderCell(header.Cells[0], fixedRows ? table.Label ?? string.Empty : "№");

            for (var index = 0; index < table.Columns.Count; index++)
            {
                HeaderCell(header.Cells[index + 1], table.Columns[index].Label ?? table.Columns[index].Id);
            }

            var rows = Answer(table.Id) as JsonArray;

            if (fixedRows)
            {
                for (var index = 0; index < table.FixedRows.Count; index++)
                {
                    var fixedRow = table.FixedRows[index];
                    var row = Row(grid, 0.6);
                    Label(row.Cells[0], fixedRow.Label);
                    TableCells(table, row, index, FixedRowAnswer(rows, fixedRow.Id, index), fixedRow);
                }

                return;
            }

            var count = rows?.Count ?? 0;

            for (var index = 0; index < count; index++)
            {
                var row = Row(grid, 0.6);
                var number = row.Cells[0].AddParagraph((index + 1).ToString(Invariant));
                number.Format.Alignment = ParagraphAlignment.Center;
                number.Format.Font.Color = Muted;
                TableCells(table, row, index, rows![index] as JsonObject, null);
            }

            // An empty table still offers room to fill by hand, like the paper form
            if (count == 0)
            {
                for (var index = 0; index < 3; index++)
                {
                    Row(grid, 0.6);
                }
            }
        }

        private void TableCells(BlankField table, Row row, int index, JsonObject? cells, BlankFixedRow? fixedRow)
        {
            for (var column = 0; column < table.Columns.Count; column++)
            {
                var field = table.Columns[column];
                var cell = row.Cells[column + 1];

                if (fixedRow is not null && fixedRow.Cells.TryGetValue(field.Id, out var printed) && printed.Text is not null)
                {
                    Value(cell, printed.Text);
                    continue;
                }

                var visible = field.When is null || BlankConditions.Evaluate(field.When, id => cells?[id] ?? Lookup(id));

                if (!visible)
                {
                    Value(cell, Dash).Format.Font.Color = Muted;
                }
                else if (field.Type == BlankFieldType.Computed)
                {
                    ComputedValue(cell, field, model.Computed[$"{table.Id}[{index}].{field.Id}"], mark: false);
                }
                else if (field.Type == BlankFieldType.File)
                {
                    MultiLine(cell, Files($"{table.Id}[{index}].{field.Id}", cells?[field.Id]));
                }
                else
                {
                    MultiLine(cell, Format(field, cells?[field.Id]));
                }
            }
        }

        /// <summary>The stored row of a fixed row: matched by <c>_row</c>, else by position.</summary>
        private static JsonObject? FixedRowAnswer(JsonArray? rows, string id, int index)
        {
            if (rows is null)
            {
                return null;
            }

            foreach (var item in rows)
            {
                if (item is JsonObject row && Str(row["_row"]) == id)
                {
                    return row;
                }
            }

            return index < rows.Count && rows[index] is JsonObject positional && positional["_row"] is null ? positional : null;
        }

        private static decimal DefaultShare(BlankFieldType type) => type switch
        {
            BlankFieldType.LongText => 3m,
            BlankFieldType.Text or BlankFieldType.Ref or BlankFieldType.Choice or BlankFieldType.Multi
                or BlankFieldType.File or BlankFieldType.DocRef or BlankFieldType.Order => 2m,
            BlankFieldType.Date or BlankFieldType.DateTime or BlankFieldType.DateRange or BlankFieldType.Money => 1.4m,
            _ => 1m,
        };

        // ════════════════════════════════════════════════ signatures and provenance

        private void Signatures()
        {
            var rows = model.Steps.Count > 0
                ? model.Steps.Select((step, index) => (Step: step, Signature: signatures[index])).ToList()
                : [.. _definition.Steps
                    .Where(step => IsVisible(step.When))
                    .Select(step => (Step: new BlankPdfStep(step.Id, step.Title, step.Actor, BlankStepState.Waiting), Signature: (string?)null))];

            if (rows.Count == 0)
            {
                return;
            }

            Title("Imzolar");
            var grid = Grid(3.6, 2.6, 2.8, 3.2, 2.2, 3.6);
            grid.Format.Font.Size = 8.5;

            var header = grid.AddRow();
            header.HeadingFormat = true;
            string[] titles = ["Bosqich", "Mas'ul", "Qaror", "F.I.Sh.", "Sana, vaqt", "Imzo"];

            for (var index = 0; index < titles.Length; index++)
            {
                HeaderCell(header.Cells[index], titles[index]);
            }

            foreach (var (step, signature) in rows)
            {
                var row = Row(grid, 1.1);
                row.Cells[0].AddParagraph().AddFormattedText(step.Title, TextFormat.Bold);
                Value(row.Cells[1], step.ActorLabel ?? ActorLabel(step.ActorRole));
                MultiLine(row.Cells[2], Decision(step));
                Value(row.Cells[3], step.SignerName);
                Value(row.Cells[4], step.ActedAt is { } at ? FormatDateTime(at) : null);
                SignatureCell(row.Cells[5], step, signature);
            }
        }

        private string? Decision(BlankPdfStep step)
        {
            var label = step.Decision is { } value
                ? _definition.FindStep(step.StepId)?.Decision?.Options.FirstOrDefault(option => option.Value == value)?.Label ?? value
                : null;

            return (label, step.Note) switch
            {
                (null, null) => null,
                ({ } only, null) => only,
                (null, { } note) => note,
                ({ } both, { } note) => $"{both}: {note}",
            };
        }

        private static void SignatureCell(Cell cell, BlankPdfStep step, string? signature)
        {
            cell.Format.Alignment = ParagraphAlignment.Center;

            switch (step.State)
            {
                case BlankStepState.Skipped:
                    Value(cell, "O'tkazib yuborildi").Format.Font.Color = Muted;
                    return;
                case BlankStepState.Rejected:
                    Value(cell, "Rad etildi").Format.Font.Color = Warning;
                    return;
                case BlankStepState.Signed:
                    break;
                default:
                    return;
            }

            if (signature is not null)
            {
                var image = cell.AddParagraph().AddImage(signature);
                image.Height = Unit.FromCentimeter(0.9);
                image.LockAspectRatio = true;
                return;
            }

            var text = step.Method == BlankSignMethod.InPerson ? "Qo'lda imzolandi" : "Elektron tasdiqlandi";
            Value(cell, text).Format.Font.Color = Tick;
        }

        private string ActorLabel(string role)
        {
            if (role.StartsWith(BlankActors.FieldPrefix, StringComparison.Ordinal))
            {
                var fieldId = role[BlankActors.FieldPrefix.Length..];
                return _definition.FindField(fieldId)?.Label ?? fieldId;
            }

            return role switch
            {
                BlankActorRoles.Hr1 => "HR-1",
                BlankActorRoles.Hr2 => "HR-2",
                BlankActorRoles.Hr3 => "HR-3",
                BlankActorRoles.Hr4 => "HR-4",
                BlankActorRoles.Hr5 => "HR-5",
                BlankActorRoles.Hr8 => "HR-8",
                BlankActorRoles.PayrollAccountant => "Hisobchi (ish haqi)",
                BlankActorRoles.ChiefAccountant => "Bosh hisobchi",
                BlankActorRoles.Director => "Direktor",
                BlankActorRoles.Lawyer => "Yurist",
                BlankActorRoles.SubjectEmployee => "Xodim",
                BlankActorRoles.DepartmentHead => "Bo'lim rahbari",
                BlankActorRoles.Witness => "Guvoh",
                BlankActorRoles.Candidate => "Nomzod",
                _ => role,
            };
        }

        private void Provenance()
        {
            Spacer(8);

            var number = model.Number is { } n ? $" №{n}" : string.Empty;
            var line = _section.AddParagraph(
                $"Bonvi HR · {_definition.Code}{number} · {StatusLabel(model.Status)} · chop etildi {FormatDateTime(model.GeneratedAt)}");
            line.Format.Font.Size = 7.5;
            line.Format.Font.Color = Muted;

            if (!string.IsNullOrWhiteSpace(_definition.Retention))
            {
                var retention = _section.AddParagraph(_definition.Retention);
                retention.Format.SpaceBefore = 2;
                retention.Format.Font.Size = 7.5;
                retention.Format.Font.Color = Muted;
            }
        }

        private static string StatusLabel(BlankSubmissionStatus status) => status switch
        {
            BlankSubmissionStatus.Draft => "Qoralama",
            BlankSubmissionStatus.InApproval => "Tasdiqlashda",
            BlankSubmissionStatus.Approved => "Tasdiqlangan",
            BlankSubmissionStatus.Rejected => "Rad etilgan",
            BlankSubmissionStatus.Cancelled => "Bekor qilingan",
            _ => status.ToString(),
        };

        // ════════════════════════════════════════════════ values

        private JsonNode? Answer(string id) => model.Answers[id];

        /// <summary>Condition lookup: stored answers first, then stored computed values.</summary>
        private JsonNode? Lookup(string id) => model.Answers[id] ?? model.Computed[id];

        private bool IsVisible(BlankCondition? condition) => condition is null || BlankConditions.Evaluate(condition, Lookup);

        /// <summary>One-line text of a stored value; <c>null</c> when empty.</summary>
        private string? Format(BlankField field, JsonNode? node)
        {
            if (node is null)
            {
                return null;
            }

            return field.Type switch
            {
                BlankFieldType.Text or BlankFieldType.LongText => Str(node),
                BlankFieldType.WholeNumber or BlankFieldType.Year or BlankFieldType.Score => FormatNumber(Number(node)),
                BlankFieldType.Percent => Number(node) is { } percent ? FormatNumber(percent) + "%" : null,
                BlankFieldType.Money => Number(node) is { } money ? FormatNumber(money) + " so'm" : null,
                BlankFieldType.Date => FormatIsoDate(Str(node)),
                BlankFieldType.DateTime => DateTimeOffset.TryParse(Str(node), Invariant, DateTimeStyles.None, out var at) ? FormatDateTime(at) : Str(node),
                BlankFieldType.Month => Str(node) is { Length: >= 7 } month ? $"{month[5..7]}.{month[..4]}" : Str(node),
                BlankFieldType.DateRange => Join(" — ", FormatIsoDate(Str(Prop(node, "from"))), FormatIsoDate(Str(Prop(node, "to")))),
                BlankFieldType.YesNo or BlankFieldType.Check => Bool(node) switch { true => "Ha", false => "Yo'q", null => null },
                BlankFieldType.Choice or BlankFieldType.Multi => ChosenText(field, node),
                BlankFieldType.Ref => Str(Prop(node, "label")) ?? Str(Prop(node, "id")),
                BlankFieldType.DocRef => Join(" ", Str(Prop(node, "templateCode")), Prop(node, "number") is { } number ? "№" + number : null),
                BlankFieldType.Order => Join(", ", Str(Prop(node, "number")) is { } order ? "№ " + order : null, FormatIsoDate(Str(Prop(node, "date")))),
                BlankFieldType.File => Files(field.Id, node),
                _ => Str(node),
            };
        }

        /// <summary>Chosen options only — the compact form used inside tables and placeholders.</summary>
        private string? ChosenText(BlankField field, JsonNode node)
        {
            var parts = Selected(node)
                .Select(pick => field.Options.FirstOrDefault(option => option.Value == pick.Value) is { } option
                    ? WithExtra(option.Label, option, pick.Extra)
                    : pick.Value)
                .ToList();

            return parts.Count == 0 ? null : string.Join("; ", parts);
        }

        private string WithExtra(string label, BlankOption option, JsonNode? extra)
            => option.Extra is { } definition && Format(new BlankField { Id = option.Value, Type = definition.Type }, extra) is { } text
                ? $"{label}: {text}"
                : label;

        private static List<(string Value, JsonNode? Extra)> Selected(JsonNode? node) => node switch
        {
            JsonArray items => [.. items.SelectMany(item => Selected(item))],
            JsonObject pick when Str(pick["value"]) is { } value => [(value, pick["extra"])],
            JsonValue scalar when Str(scalar) is { } value => [(value, null)],
            _ => [],
        };

        private string? Files(string fieldPath, JsonNode? node)
        {
            var ids = node is JsonArray array ? array.Select(Str).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase) : [];
            var files = model.Attachments
                .Where(file => ids.Contains(file.Id.ToString()) || (ids.Count == 0 && file.FieldPath == fieldPath))
                .Select(file => $"Ilova: {file.FileName} ({KindLabel(file.Kind)}, {FormatSize(file.SizeBytes)})")
                .ToList();

            return files.Count == 0 ? null : string.Join("\n", files);
        }

        private static string KindLabel(BlankAttachmentKind kind) => kind switch
        {
            BlankAttachmentKind.Pdf => "PDF",
            BlankAttachmentKind.Jpeg => "JPEG",
            BlankAttachmentKind.Png => "PNG",
            BlankAttachmentKind.Webp => "WEBP",
            BlankAttachmentKind.Docx => "Word",
            BlankAttachmentKind.Xlsx => "Excel",
            BlankAttachmentKind.Pptx => "PowerPoint",
            _ => kind.ToString(),
        };

        private static string FormatSize(long bytes) => bytes >= 1024 * 1024
            ? (bytes / (1024m * 1024m)).ToString("0.0", Numbers) + " MB"
            : Math.Max(1, (bytes + 1023) / 1024).ToString(Invariant) + " KB";

        private static void ComputedValue(Cell cell, BlankField field, JsonNode? node, bool mark = true)
        {
            var text = node is null
                ? null
                : field.ValueType switch
                {
                    "number" => FormatNumber(Number(node)),
                    "bool" => Bool(node) switch { true => "Ha", false => "Yo'q", null => null },
                    "date" => FormatIsoDate(Str(node)),
                    _ => Str(node),
                };

            var paragraph = Value(cell, text);

            if (mark && text is not null)
            {
                var suffix = paragraph.AddFormattedText(ComputedMark);
                suffix.Font.Color = Muted;
                suffix.Font.Size = 7.5;
            }
        }

        private static JsonNode? Prop(JsonNode? node, string name) => node is JsonObject obj ? obj[name] : null;

        private static string? Str(JsonNode? node) => node is JsonValue value && value.GetValueKind() == JsonValueKind.String
            && value.GetValue<string>() is { } text && !string.IsNullOrWhiteSpace(text)
                ? text
                : null;

        private static decimal? Number(JsonNode? node) => node is JsonValue value && value.GetValueKind() == JsonValueKind.Number
            && decimal.TryParse(value.ToJsonString(), NumberStyles.Float, Invariant, out var number)
                ? number
                : null;

        private static bool? Bool(JsonNode? node) => node is JsonValue value
            ? value.GetValueKind() switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => null }
            : null;

        private static string? FormatNumber(decimal? number) => number?.ToString("#,0.##", Numbers);

        private static string FormatDate(DateOnly date) => date.ToString("dd.MM.yyyy", Invariant);

        private static string? FormatIsoDate(string? iso)
            => iso is { Length: >= 10 } && DateOnly.TryParseExact(iso[..10], "yyyy-MM-dd", Invariant, DateTimeStyles.None, out var date)
                ? FormatDate(date)
                : iso;

        private static string FormatDateTime(DateTimeOffset at) => at.ToTashkent().ToString("dd.MM.yyyy HH:mm", Invariant);

        private static string? Join(string separator, params string?[] parts)
        {
            var present = parts.Where(part => !string.IsNullOrEmpty(part)).ToList();
            return present.Count == 0 ? null : string.Join(separator, present);
        }

        // ════════════════════════════════════════════════ building blocks

        private Table Grid(params double[] widths)
        {
            var table = _section.AddTable();
            table.Borders.Color = Rule;
            table.Borders.Width = 0.5;
            table.LeftPadding = Unit.FromCentimeter(0.15);
            table.RightPadding = Unit.FromCentimeter(0.15);
            table.TopPadding = Unit.FromCentimeter(0.08);
            table.BottomPadding = Unit.FromCentimeter(0.08);

            foreach (var width in widths)
            {
                table.AddColumn(Unit.FromCentimeter(width));
            }

            return table;
        }

        private static Row Row(Table table, double height = 0.7)
        {
            var row = table.AddRow();
            row.Height = Unit.FromCentimeter(height);
            row.HeightRule = RowHeightRule.AtLeast;
            row.VerticalAlignment = VerticalAlignment.Center;

            return row;
        }

        private static void HeaderCell(Cell cell, string title)
        {
            cell.Shading.Color = Navy;
            var paragraph = cell.AddParagraph();
            paragraph.Format.Alignment = ParagraphAlignment.Center;
            var text = paragraph.AddFormattedText(title, TextFormat.Bold);
            text.Font.Color = Colors.White;
            text.Font.Size = 8;
        }

        private static void Label(Cell cell, string text)
        {
            cell.Shading.Color = LabelFill;
            cell.AddParagraph().AddFormattedText(text, TextFormat.Bold);
        }

        private static void LabelWithHint(Cell cell, BlankField field)
        {
            Label(cell, field.Label ?? field.Id);

            if (!string.IsNullOrWhiteSpace(field.Hint))
            {
                var hint = cell.AddParagraph(field.Hint);
                hint.Format.Font.Size = 7;
                hint.Format.Font.Color = Muted;
            }
        }

        private static Paragraph Value(Cell cell, string? text)
        {
            var paragraph = cell.AddParagraph();

            if (!string.IsNullOrEmpty(text))
            {
                paragraph.AddText(text);
            }

            return paragraph;
        }

        private static Paragraph MultiLine(Cell cell, string? text)
        {
            var paragraph = cell.AddParagraph();
            AddLines(paragraph, text);

            return paragraph;
        }

        private static void AddLines(Paragraph paragraph, string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

            for (var index = 0; index < lines.Length; index++)
            {
                if (index > 0)
                {
                    paragraph.AddLineBreak();
                }

                if (lines[index].Length > 0)
                {
                    paragraph.AddText(lines[index]);
                }
            }
        }

        /// <summary>Every option ☑/☐; the chosen one bold with its extra inline.</summary>
        private void Options(Cell cell, IReadOnlyList<BlankOption> options, List<(string Value, JsonNode? Extra)> selected)
        {
            var items = options
                .Select(option =>
                {
                    var pick = selected.FirstOrDefault(item => item.Value == option.Value);
                    var chosen = pick.Value is not null;
                    var label = chosen ? WithExtra(option.Label, option, pick.Extra) : option.Label;

                    return (label, chosen);
                })
                .ToList();

            Boxes(cell, items);
        }

        private static void Boxes(Cell cell, List<(string Label, bool Chosen)> items)
        {
            var paragraph = cell.AddParagraph();
            var inline = items.Sum(item => item.Label.Length + 4) <= ChoiceLineBudget;

            for (var index = 0; index < items.Count; index++)
            {
                var (label, chosen) = items[index];

                if (index > 0)
                {
                    if (inline)
                    {
                        paragraph.AddSpace(5);
                    }
                    else
                    {
                        paragraph.AddLineBreak();
                    }
                }

                var box = paragraph.AddFormattedText(chosen ? Checked : Unchecked);
                box.Font.Name = BundledFontResolver.Symbols;
                box.Font.Bold = false;

                if (chosen)
                {
                    box.Font.Color = Tick;
                }

                if (label.Length == 0)
                {
                    continue;
                }

                paragraph.AddText(" ");

                if (chosen)
                {
                    paragraph.AddFormattedText(label, TextFormat.Bold);
                }
                else
                {
                    paragraph.AddText(label);
                }
            }
        }

        private void Title(string text)
        {
            var paragraph = _section.AddParagraph();
            paragraph.Format.SpaceBefore = 9;
            paragraph.Format.SpaceAfter = 3;
            paragraph.Format.KeepWithNext = true;
            var title = paragraph.AddFormattedText(text, TextFormat.Bold);
            title.Font.Size = 10.5;
            title.Font.Color = Navy;
        }

        private void Hint(string text)
        {
            var paragraph = _section.AddParagraph(text);
            paragraph.Format.SpaceAfter = 2;
            paragraph.Format.KeepWithNext = true;
            paragraph.Format.Font.Size = 7.5;
            paragraph.Format.Font.Italic = true;
            paragraph.Format.Font.Color = Muted;
        }

        private void Spacer(double points)
        {
            var paragraph = _section.AddParagraph();
            paragraph.Format.LineSpacingRule = LineSpacingRule.Exactly;
            paragraph.Format.LineSpacing = Unit.FromPoint(points);
        }

        [GeneratedRegex(@"\{\{([A-Za-z0-9]+)\}\}", RegexOptions.CultureInvariant)]
        private static partial Regex Placeholder();
    }
}
