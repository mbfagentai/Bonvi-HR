using System.Globalization;
using System.Text.Json.Nodes;
using Bonvi.Application.Recruiting.Forms;
using Bonvi.Application.Recruiting.Forms.Dynamic;
using Bonvi.Application.Recruiting.Forms.Texts;
using Bonvi.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;

namespace Bonvi.Infrastructure.Reports.Pdf;

/// <summary>
/// HRMentor konstruktorida yig'ilgan anketa — o'z ta'rifidan PDF (SPEC-anketa-konstruktori §8).
/// </summary>
/// <remarks>
/// <para>
/// Uslub o'rnatilgan HR-F02 PDF i bilan bir xil (<see cref="QuestionnairePdfRenderer"/>): A4, chetlar
/// 1,5 sm, to'q ko'k sarlavhalar, kulrang yorliq kataklari, ☑ / ☐ variantlar. Bo'limlar, savollar va
/// variantlar esa TA'RIFDAN — HR konstruktorda nima yig'gan bo'lsa, qog'ozda shu chiqadi.
/// </para>
/// <para>
/// Qoidalar: har bir variant chiziladi (tanlangani ☑); shart bilan yashiringan savol bo'sh chiziladi
/// (qog'oz hamma savolni ko'rsatadi); <c>pdf_hidden</c> savol chizilmaydi; takrorlanuvchi jadval
/// kamida <c>print_rows</c> qator; B qism (HR bo'limlari) bo'sh, faqat «surat» va «imzo» belgilari
/// onlayn olingan bo'lsa ☑.
/// </para>
/// <para>
/// So'zlar faqat lotin yozuvida — PDF yig'majildga tushadi va HR uni lotinda o'qiydi (o'rnatilgan
/// PDF dagidek). Umumiy so'zlar («Sahifa», «Anketa №», «Ha», «Yo'q») — o'rnatilgan PDF ning asl
/// matnlaridan (<see cref="FormTexts.Empty"/>).
/// </para>
/// </remarks>
public sealed partial class DynamicFormPdfRenderer : IDynamicFormPdfRenderer
{
    // ── Blankaning ranglari (o'rnatilgan PDF bilan bir xil) ──
    private static readonly Color Navy = new(0x1F, 0x3A, 0x5F);
    private static readonly Color LabelFill = new(0xF2, 0xF5, 0xF9);
    private static readonly Color Muted = new(0x6B, 0x76, 0x86);
    private static readonly Color Rule = new(0xAE, 0xB8, 0xC6);
    private static readonly Color Ink = new(0x1A, 0x1F, 0x2B);
    private static readonly Color Tick = new(0x1E, 0x8E, 0x3E);

    private const string Checked = "☑";
    private const string Unchecked = "☐";
    private const string Blank = "______________";

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private readonly ILogger<DynamicFormPdfRenderer> _logger;

    public DynamicFormPdfRenderer(ILogger<DynamicFormPdfRenderer> logger) => _logger = logger;

    [LoggerMessage(EventId = 4710, Level = LogLevel.Warning, Message = "Konstruktor anketasi №{Number}: {Part} PDF'ga qo'yilmadi — katak bo'sh qoldi")]
    private partial void LogImageSkipped(Exception failure, string number, string part);

    public async Task<byte[]> RenderAsync(DynamicFormPdfModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        // O'rnatilgan PDF bilan BITTA navbat — jarayon bo'yicha ko'pi bilan ikkita chizish
        await QuestionnairePdfRenderer.RenderGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return Render(model);
        }
        finally
        {
            QuestionnairePdfRenderer.RenderGate.Release();
        }
    }

    private byte[] Render(DynamicFormPdfModel model)
    {
        BundledFontResolver.EnsureInstalled();

        var document = new Builder(model, PrepareImages(model)).Build();

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();

        using var output = new MemoryStream();
        renderer.PdfDocument.Save(output, closeStream: false);

        return output.ToArray();
    }

    /// <summary>Surat 3×4 ga qirqiladi, imzo oq fonga yotqiziladi; buzuq rasm — katak bo'sh qoladi.</summary>
    private PdfImages PrepareImages(DynamicFormPdfModel model)
    {
        var number = model.Number?.ToString(Invariant) ?? "namuna";
        string? photo = null;
        string? signature = null;

        try
        {
            photo = model.Photo is { Length: > 0 } bytes
                ? QuestionnairePdfRenderer.Base64(QuestionnairePdfRenderer.PortraitJpeg(bytes))
                : null;
        }
#pragma warning disable CA1031 // Buzuq surat PDF'ni yiqitmasin — katak bo'sh qoladi
        catch (Exception failure)
#pragma warning restore CA1031
        {
            LogImageSkipped(failure, number, "surat");
        }

        try
        {
            signature = model.Signature is { Length: > 0 } bytes
                ? QuestionnairePdfRenderer.Base64(QuestionnairePdfRenderer.FlatPng(bytes))
                : null;
        }
#pragma warning disable CA1031 // Buzuq imzo PDF'ni yiqitmasin — chiziq bo'sh qoladi
        catch (Exception failure)
#pragma warning restore CA1031
        {
            LogImageSkipped(failure, number, "imzo");
        }

        return new PdfImages(
            QuestionnairePdfRenderer.Base64(BundledFontResolver.ReadResource("Assets.mbf-logo.png")),
            photo,
            signature);
    }

    private sealed record PdfImages(string Logo, string? Photo, string? Signature);

    /// <summary>Savol qatordagi joyi: yarim qator (juft), to'liq qator yoki alohida blok (jadval, tilxat).</summary>
    private enum Shape
    {
        Narrow,
        Wide,
        Block,
    }

    /// <summary>Bitta hujjatni yig'uvchi — ta'rif tartibida, bo'limma-bo'lim.</summary>
    private sealed class Builder(DynamicFormPdfModel model, PdfImages images)
    {
        /// <summary>Sahifaning yozuv eni (A4 − 2 × 1,5 sm).</summary>
        private const double PageWidth = 18.0;

        /// <summary>«№» ustuni.</summary>
        private const double NumberWidth = 0.79;

        /// <summary>Yarim qatordagi javob katagi (~5,2 sm) va to'liq qatordagi (~14,2 sm) — belgilar chegarasi.</summary>
        private const int NarrowBudget = 28;

        /// <inheritdoc cref="NarrowBudget"/>
        private const int WideBudget = 78;

        /// <summary>Tilxatdagi «F.I.Sh.» o'rni.</summary>
        private const string FullNameToken = "{{fullName}}";

        /// <summary>O'rnatilgan PDF ning asl so'zlari (Sahifa, Anketa №, Ha, Yo'q…).</summary>
        private static readonly FormTexts Words = FormTexts.Empty;

        private readonly FormDefinition _definition = model.Answers.Definition;
        private readonly JsonObject _values = model.Answers.Values;
        private readonly Document _document = new();
        private Section _section = null!;

        private int _sectionNumber;
        private bool _originPrinted;

        // Joriy bo'limning juft-juft savollar jadvali
        private Table? _grid;
        private FormField? _pending;
        private bool _sectionHasContent;

        public Document Build()
        {
            Styles();
            Page();

            TitleBlock();
            InfoTable();

            if (_definition.Pdf.Instruction is { } instruction)
            {
                Instruction(instruction);
            }

            if (_definition.Pdf.CandidatePart is { } candidatePart)
            {
                Bar(candidatePart);
            }

            foreach (var section in _definition.Sections.Where(section => !section.Office))
            {
                CandidateSection(section);
            }

            if (!_originPrinted)
            {
                Origin();
            }

            var office = _definition.Sections.Where(section => section.Office && Printed(section).Count > 0).ToList();
            if (office.Count > 0)
            {
                _section.AddPageBreak();
                Bar(_definition.Pdf.OfficePart ?? $"{Words.Pdf("pdf.hr.part")}  {Words.Pdf("pdf.hr.partText")}");

                foreach (var section in office)
                {
                    OfficeSection(section);
                }
            }

            if (_definition.Pdf.Footer is { } footer)
            {
                Footer(footer);
            }

            return _document;
        }

        // ════════════════════════════════════════════════ sahifa va uslub

        private void Styles()
        {
            var number = model.Number is { } value ? "№" + value.ToString(Invariant) : "namuna";
            _document.Info.Title = $"{_definition.Title.Latin} {_definition.Pdf.PrintedCode} {number}";
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
            header.AddColumn(Unit.FromCentimeter(9));
            header.AddColumn(Unit.FromCentimeter(9));
            header.Format.Font.Size = 8;
            header.Format.Font.Color = Muted;

            var headerRow = header.AddRow();
            headerRow.Cells[0].AddParagraph(Words.Pdf("pdf.head.company"));
            var right = headerRow.Cells[1].AddParagraph();
            right.Format.Alignment = ParagraphAlignment.Right;
            right.AddText(_definition.Pdf.PrintedCode);
            Dot(right);
            right.AddText(_definition.Title.Latin);
            Dot(right);
            right.AddText("v" + _definition.Label);

            var footer = _section.Footers.Primary.AddParagraph();
            footer.Format.Alignment = ParagraphAlignment.Center;
            footer.Format.Font.Size = 8;
            footer.Format.Font.Color = Muted;
            footer.AddText(Words.Pdf("pdf.head.page") + " ");
            footer.AddPageField();
            footer.AddText(" / ");
            footer.AddNumPagesField();
        }

        // ════════════════════════════════════════════════ sarlavha

        private void TitleBlock()
        {
            var table = _section.AddTable();
            table.Borders.Visible = false;
            table.AddColumn(Unit.FromCentimeter(4.76));
            table.AddColumn(Unit.FromCentimeter(10.24));
            table.AddColumn(Unit.FromCentimeter(3.0));

            var row = table.AddRow();
            row.Height = Unit.FromCentimeter(4.0);
            row.HeightRule = RowHeightRule.Exactly;
            row.VerticalAlignment = VerticalAlignment.Center;

            var logo = row.Cells[0].AddParagraph().AddImage(images.Logo);
            logo.Width = Unit.FromCentimeter(3.7);
            logo.LockAspectRatio = true;

            var title = row.Cells[1].AddParagraph();
            title.Format.Alignment = ParagraphAlignment.Center;
            var titleText = title.AddFormattedText(_definition.Pdf.Title, TextFormat.Bold);
            titleText.Font.Size = 17;
            titleText.Font.Color = Navy;

            if (_definition.Pdf.Subtitle is { } subtitle)
            {
                Centered(row.Cells[1], subtitle, 9, Muted).Format.SpaceBefore = 3;
            }

            var form = Centered(row.Cells[1], "Blanka " + _definition.Pdf.PrintedCode, 8, Muted);
            form.Format.SpaceBefore = 2;
            Dot(form);
            form.AddText(Words.Pdf("pdf.head.version") + " " + _definition.Label);

            var photo = row.Cells[2];
            photo.Borders.Visible = true;
            photo.Borders.Color = Rule;
            photo.Borders.Width = 0.75;
            photo.Format.Alignment = ParagraphAlignment.Center;

            if (images.Photo is { } source)
            {
                var image = photo.AddParagraph().AddImage(source);
                image.Width = Unit.FromCentimeter(2.85);
                image.Height = Unit.FromCentimeter(3.8);
            }
            else
            {
                var label = photo.AddParagraph();
                label.AddFormattedText(Words.Pdf("pdf.head.photo"), TextFormat.Bold).Font.Color = Muted;
                Centered(photo, Words.Pdf("pdf.head.photoSize"), 8, Muted);
            }

            Spacer(6);
        }

        /// <summary>
        /// Anketa № | Sana | So'rov № — Lavozim | Bo'lim (o'rnatilgan PDF va qog'oz blanka sarlavhasi).
        /// Namunada raqam o'rnida «Namuna»; so'rov raqami va bo'lim HR qo'lda yozadi; «Bo'lim» katagi
        /// nomini HR ta'rifda o'zgartiradi (<c>pdf.head_department</c>).
        /// </summary>
        private void InfoTable()
        {
            var table = Grid(2.29, 3.17, 1.59, 5.11, 1.94, 3.89);

            var row = Row(table);
            Label(row.Cells[0], Words.Pdf("pdf.head.number"));

            if (model.Number is { } number)
            {
                Value(row.Cells[1], number.ToString(Invariant)).Format.Font.Bold = true;
            }
            else
            {
                var sample = Value(row.Cells[1], "Namuna");
                sample.Format.Font.Italic = true;
                sample.Format.Font.Color = Muted;
            }

            Label(row.Cells[2], Words.Pdf("pdf.head.date"));
            Value(row.Cells[3], Day(model.SubmittedAt));
            Label(row.Cells[4], Words.Pdf("pdf.head.request"));
            Value(row.Cells[5], "(HR-F01)").Format.Font.Color = Muted;

            var second = Row(table);
            Label(second.Cells[0], Words.Pdf("pdf.head.vacancy"));
            Value(second.Cells[1], model.VacancyTitle);
            second.Cells[1].MergeRight = 2;
            Label(second.Cells[4], _definition.Pdf.HeadDepartment ?? Words.Pdf("pdf.head.department"));
            Value(second.Cells[5], null);
        }

        private void Instruction(string text)
        {
            var paragraph = _section.AddParagraph(text);
            paragraph.Format.SpaceBefore = 5;
            paragraph.Format.Font.Size = 8;
            paragraph.Format.Font.Italic = true;
            paragraph.Format.Font.Color = Muted;
        }

        // ════════════════════════════════════════════════ nomzod bo'limlari

        /// <summary>
        /// Bo'lim: qisqa savollar ikkitadan bir qatorga (o'rnatilgan PDF dagi «yorliq | javob | yorliq | javob»),
        /// uzunlari to'liq qatorga, jadvallar va tilxat — alohida blok.
        /// </summary>
        private void CandidateSection(FormSection section)
        {
            var fields = Printed(section);
            if (fields.Count == 0)
            {
                return;
            }

            SectionTitle(section);

            foreach (var field in fields)
            {
                switch (ShapeOf(field))
                {
                    case Shape.Narrow when _pending is null:
                        _pending = field;
                        break;

                    case Shape.Narrow:
                        PairRow(_pending!, field);
                        _pending = null;
                        break;

                    case Shape.Wide:
                        FlushPending();
                        WideRow(field);
                        break;

                    default:
                        CloseGrid();
                        Block(section, fields.Count, field);
                        break;
                }
            }

            CloseGrid();
        }

        private static Shape ShapeOf(FormField field) => field.Kind switch
        {
            FieldKind.Repeat or FieldKind.Levels or FieldKind.Info or FieldKind.Consent
                or FieldKind.Signature or FieldKind.Checklist => Shape.Block,
            FieldKind.LongText or FieldKind.MultiChoice or FieldKind.Referral or FieldKind.Vacancy => Shape.Wide,
            FieldKind.Choice => field.Options.Sum(option => option.Printed.Length + 4) <= NarrowBudget * 2 ? Shape.Narrow : Shape.Wide,
            _ => Shape.Narrow,
        };

        private Table CurrentGrid()
        {
            if (_grid is null)
            {
                Gap();
                _grid = Grid(3.79, 5.21, 3.79, 5.21);
            }

            return _grid;
        }

        private void PairRow(FormField left, FormField right)
        {
            var row = Row(CurrentGrid());
            Label(row.Cells[0], left.Printed);
            Answer(row.Cells[1], left, NarrowBudget);
            Label(row.Cells[2], right.Printed);
            Answer(row.Cells[3], right, NarrowBudget);
        }

        private void WideRow(FormField field)
        {
            var row = Row(CurrentGrid());
            Label(row.Cells[0], field.Printed);
            Answer(row.Cells[1], field, WideBudget);
            row.Cells[1].MergeRight = 2;
        }

        /// <summary>Juftsiz qolgan qisqa savol — to'liq qatorga.</summary>
        private void FlushPending()
        {
            if (_pending is { } field)
            {
                _pending = null;
                WideRow(field);
            }
        }

        private void CloseGrid()
        {
            FlushPending();
            _grid = null;
        }

        /// <summary>Bo'lim ichidagi ikki jadval orasidagi oraliq (o'rnatilgan PDF dagi 5 pt).</summary>
        private void Gap()
        {
            if (_sectionHasContent)
            {
                Spacer(5);
            }

            _sectionHasContent = true;
        }

        private void Answer(Cell cell, FormField field, int budget)
        {
            var value = _values[field.Id];

            switch (field.Kind)
            {
                case FieldKind.YesNo:
                    var flag = DynamicFormAnswers.Flag(value);
                    Choices(cell, Lines([(YesWord(field), flag == true), (NoWord(field), flag == false)], budget));
                    break;

                case FieldKind.Choice:
                    var chosen = DynamicFormAnswers.Text(value);
                    Choices(cell, Lines(field.Options.Select(option => (option.Printed, option.Value == chosen)), budget));
                    break;

                case FieldKind.MultiChoice:
                    var picked = value is JsonArray items
                        ? items.Select(DynamicFormAnswers.Text).OfType<string>().ToHashSet(StringComparer.Ordinal)
                        : [];
                    Choices(cell, Lines(field.Options.Select(option => (option.Printed, picked.Contains(option.Value))), budget));
                    break;

                case FieldKind.Referral:
                    var detail = DynamicFormAnswers.Text((value as JsonObject)?["detail"]);
                    Choices(cell, Lines(
                        model.ReferralChoices.Select(choice => (
                            choice.Chosen && !string.IsNullOrWhiteSpace(detail)
                                ? Words.Pdf("pdf.conditions.referralDetail", ("label", choice.Label), ("detail", detail.Trim()))
                                : choice.Label,
                            choice.Chosen)),
                        budget));
                    break;

                case FieldKind.Vacancy:
                    Value(cell, model.VacancyTitle);
                    break;

                case FieldKind.Phone:
                    Value(cell, Phone(DynamicFormAnswers.Text(value)));
                    break;

                case FieldKind.Telegram:
                    Value(cell, DynamicFormAnswers.Text(value) is { } handle ? "@" + handle : null);
                    break;

                default:
                    Value(cell, DynamicFormMapping.Display(field, value));
                    break;
            }
        }

        private void Block(FormSection section, int fieldCount, FormField field)
        {
            switch (field.Kind)
            {
                case FieldKind.Repeat:
                    RepeatTable(section, fieldCount, field);
                    break;

                case FieldKind.Levels:
                    LevelsTable(section, fieldCount, field);
                    break;

                case FieldKind.Info:
                    Gap();
                    Hint(field.Text?.Latin ?? field.Printed);
                    break;

                case FieldKind.Consent:
                    ConsentItem(field);
                    break;

                case FieldKind.Signature:
                    SignatureBlock(field);
                    break;

                case FieldKind.Checklist:
                    ChecklistTable(field);
                    break;
            }
        }

        /// <summary>
        /// Jadval nomi — bo'limda boshqa savollar ham bo'lsa (bitta jadvalli bo'limda sarlavhaning o'zi
        /// yetarli); izohi kursivda.
        /// </summary>
        private void TableCaption(FormSection section, int fieldCount, FormField field)
        {
            var caption = field.Printed;

            if (fieldCount > 1
                && caption.Length > 0
                && !string.Equals(caption, section.Title.Latin, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(caption, section.PdfTitle, StringComparison.OrdinalIgnoreCase))
            {
                var paragraph = _section.AddParagraph();
                paragraph.Format.SpaceAfter = 2;
                paragraph.Format.KeepWithNext = true;
                paragraph.AddFormattedText(caption, TextFormat.Bold);
            }

            if (field.Hint?.Latin is { Length: > 0 } hint)
            {
                Hint(hint);
            }
        }

        // ════════════════════════════════════════════════ takrorlanuvchi jadval

        private void RepeatTable(FormSection section, int fieldCount, FormField field)
        {
            var columns = field.Columns.Where(column => !column.PdfHidden).ToList();
            if (columns.Count == 0)
            {
                return;
            }

            Gap();
            TableCaption(section, fieldCount, field);

            var widths = Distribute(PageWidth - NumberWidth, [.. columns.Select(column => Weight(column.Kind))]);
            var table = Grid([NumberWidth, .. widths]);
            var header = Header(table, ["№", .. columns.Select(column => column.Printed)]);

            var rows = _values[field.Id] is JsonArray answered ? answered.OfType<JsonObject>().ToList() : [];
            var count = Math.Max(Math.Max(field.PrintRowCount, rows.Count), 1);

            // Kichik jadval sahifa chegarasida bo'linmasin — sarlavhasi bilan birga
            if (count <= 8)
            {
                header.KeepWith = count;
            }

            for (var index = 0; index < count; index++)
            {
                var answer = index < rows.Count ? rows[index] : null;
                var row = Row(table, 0.85);
                Number(row.Cells[0], index + 1);

                for (var column = 0; column < columns.Count; column++)
                {
                    Value(row.Cells[column + 1], answer is null ? null : CellText(columns[column], answer[columns[column].Id]));
                }
            }
        }

        /// <summary>Ustunning nisbiy eni — o'rnatilgan PDF jadvallaridan chiqarilgan.</summary>
        private static double Weight(FieldKind kind) => kind switch
        {
            FieldKind.LongText => 2.2,
            FieldKind.ShortText => 1.6,
            FieldKind.Choice or FieldKind.Period or FieldKind.Phone => 1.3,
            FieldKind.Money => 1.1,
            FieldKind.YearRange or FieldKind.Date => 1.0,
            FieldKind.Month => 0.9,
            _ => 0.8,
        };

        private static double[] Distribute(double total, double[] weights)
        {
            var sum = weights.Sum();
            return [.. weights.Select(weight => Math.Round(total * weight / sum, 2))];
        }

        private static string? CellText(FormField column, JsonNode? value) => column.Kind switch
        {
            FieldKind.Phone => Phone(DynamicFormAnswers.Text(value)),
            FieldKind.Period => value is JsonObject period ? Period(period) : null,
            FieldKind.YearRange => value is JsonObject range ? Years(range) : null,
            _ => DynamicFormMapping.Display(column, value),
        };

        // ════════════════════════════════════════════════ darajalar jadvali

        /// <summary>
        /// Qatorlar × jihatlar. Bitta jihatli va 2–3 guruhli ro'yxat (kompyuter) — o'rnatilgan PDF dagidek
        /// guruhlar yonma-yon ustunlarda; qolgani — guruh sarlavhali qatorlar.
        /// </summary>
        private void LevelsTable(FormSection section, int fieldCount, FormField field)
        {
            Gap();
            TableCaption(section, fieldCount, field);

            var answer = _values[field.Id] as JsonObject;
            var rows = answer?["rows"] as JsonObject;
            var printed = field.Options
                .Where(option => field.PrintedRows.Count == 0
                    || field.PrintedRows.Contains(option.Value)
                    || rows?[option.Value] is not null)
                .ToList();

            var groupIds = field.Groups.Select(group => group.Id).ToHashSet(StringComparer.Ordinal);
            var sideBySide = field.Aspects.Count == 1
                && field.Groups.Count is >= 2 and <= 3
                && field.Other is null
                && printed.All(option => option.Group is { } group && groupIds.Contains(group));

            if (sideBySide)
            {
                GroupColumns(field, printed, rows);
            }
            else
            {
                LevelRows(field, printed, rows, answer?["other"] as JsonArray);
            }
        }

        private void GroupColumns(FormField field, List<FormOption> printed, JsonObject? rows)
        {
            const double levelWidth = 1.06;
            var groupWidth = PageWidth / field.Groups.Count;

            var widths = new List<double>();
            for (var group = 0; group < field.Groups.Count; group++)
            {
                widths.Add(groupWidth - levelWidth);
                widths.Add(levelWidth);
            }

            var table = Grid([.. widths]);
            var columns = field.Groups
                .Select(group => printed.Where(option => option.Group == group.Id).ToList())
                .ToList();

            var header = table.AddRow();
            header.HeadingFormat = true;
            header.Height = Unit.FromCentimeter(0.55);
            header.VerticalAlignment = VerticalAlignment.Center;

            for (var column = 0; column < field.Groups.Count; column++)
            {
                var cell = header.Cells[column * 2];
                HeaderCell(cell, field.Groups[column].Label.Latin);
                header.Cells[(column * 2) + 1].Shading.Color = Navy;
                cell.MergeRight = 1;
            }

            var count = Math.Max(1, columns.Max(items => items.Count));
            header.KeepWith = count;

            for (var index = 0; index < count; index++)
            {
                var row = Row(table, 0.55);

                for (var column = 0; column < columns.Count; column++)
                {
                    if (index < columns[column].Count)
                    {
                        var option = columns[column][index];
                        Small(row.Cells[column * 2], option.Printed);
                        Level(row.Cells[(column * 2) + 1], LevelAt(rows?[option.Value], 0));
                    }
                }
            }
        }

        private void LevelRows(FormField field, List<FormOption> printed, JsonObject? rows, JsonArray? others)
        {
            var aspects = field.Aspects.Count;
            var aspectWidth = Math.Min(3.88, (PageWidth - NumberWidth - 5.0) / aspects);
            var nameWidth = PageWidth - NumberWidth - (aspectWidth * aspects);

            var table = Grid([NumberWidth, nameWidth, .. Enumerable.Repeat(aspectWidth, aspects)]);
            Header(table, ["№", field.Printed, .. field.Aspects.Select(aspect => aspect.Label.Latin)]);

            var number = 0;

            // Guruhsiz qatorlar birinchi, keyin har guruh o'z sarlavhasi ostida
            var groupIds = field.Groups.Select(group => group.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var option in printed.Where(option => option.Group is null || !groupIds.Contains(option.Group)))
            {
                LevelRow(table, ++number, option.Printed, rows?[option.Value] as JsonArray, aspects);
            }

            foreach (var group in field.Groups)
            {
                var members = printed.Where(option => option.Group == group.Id).ToList();
                if (members.Count == 0)
                {
                    continue;
                }

                var title = Row(table, 0.55);
                Label(title.Cells[0], group.Label.Latin);
                title.Cells[0].MergeRight = aspects + 1;

                foreach (var option in members)
                {
                    LevelRow(table, ++number, option.Printed, rows?[option.Value] as JsonArray, aspects);
                }
            }

            if (field.Other is null)
            {
                return;
            }

            var other = others?.OfType<JsonObject>().ToList() ?? [];
            foreach (var entry in other)
            {
                var name = DynamicFormAnswers.Text(entry["name"]) ?? string.Empty;
                LevelRow(table, ++number, Words.Pdf("pdf.languages.other", ("name", name)), entry["levels"] as JsonArray, aspects);
            }

            if (other.Count == 0)
            {
                LevelRow(table, ++number, Words.Pdf("pdf.languages.other", ("name", Blank)), null, aspects);
            }
        }

        private static void LevelRow(Table table, int number, string name, JsonArray? levels, int aspects)
        {
            var row = Row(table, 0.71);
            Number(row.Cells[0], number);
            Value(row.Cells[1], name);

            for (var aspect = 0; aspect < aspects; aspect++)
            {
                Level(row.Cells[aspect + 2], LevelAt(levels, aspect));
            }
        }

        private static int? LevelAt(JsonNode? levels, int aspect)
            => levels is JsonArray array && aspect < array.Count && DynamicFormAnswers.Number(array[aspect]) is { } level
                ? (int)level
                : null;

        // ════════════════════════════════════════════════ tilxat va imzo

        /// <summary>
        /// Tilxat bandi: matnning oxirgi qatori ☑ bilan; undan oldingi qatorlari (kirish — «Men, … (F.I.Sh.):»)
        /// oddiy matn. <c>{{fullName}}</c> o'rniga nomzodning to'liq ismi (qalin, tagiga chizilgan).
        /// </summary>
        private void ConsentItem(FormField field)
        {
            _sectionHasContent = true;

            var lines = (field.Text?.Latin ?? field.Printed)
                .Replace("\r", string.Empty, StringComparison.Ordinal)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length == 0)
            {
                return;
            }

            foreach (var intro in lines[..^1])
            {
                var paragraph = _section.AddParagraph();
                paragraph.Format.KeepWithNext = true;
                paragraph.Format.SpaceBefore = 2;
                WithName(paragraph, intro);
            }

            var item = _section.AddParagraph();
            item.Format.KeepWithNext = true;
            item.Format.SpaceBefore = 2;
            item.Format.LeftIndent = Unit.FromCentimeter(0.55);
            item.Format.FirstLineIndent = Unit.FromCentimeter(-0.55);
            item.Format.TabStops.AddTabStop(Unit.FromCentimeter(0.55));
            Box(item, DynamicFormAnswers.Flag(_values[field.Id]) == true);
            item.AddTab();
            WithName(item, lines[^1]);
        }

        private void WithName(Paragraph paragraph, string text)
        {
            var parts = text.Split(FullNameToken);

            for (var index = 0; index < parts.Length; index++)
            {
                if (index > 0)
                {
                    var name = paragraph.AddFormattedText(FullName() ?? Blank, TextFormat.Bold | TextFormat.Underline);
                    name.Font.Color = Ink;
                }

                paragraph.AddText(parts[index]);
            }
        }

        private string? FullName()
        {
            var parts = new[] { FormProfile.LastName, FormProfile.FirstName, FormProfile.MiddleName }
                .Select(model.Answers.BoundText)
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .ToList();

            return parts.Count == 0 ? null : string.Join(' ', parts);
        }

        /// <summary>Sana | Imzo (rasm) | imzo yonidagi ism — keyin «Onlayn to'ldirildi» qatori.</summary>
        private void SignatureBlock(FormField field)
        {
            _sectionHasContent = true;
            Spacer(10, keepWithNext: true);

            var signatures = _section.AddTable();
            signatures.Borders.Visible = false;
            signatures.AddColumn(Unit.FromCentimeter(6.7));
            signatures.AddColumn(Unit.FromCentimeter(5.29));
            signatures.AddColumn(Unit.FromCentimeter(6.01));
            signatures.KeepTogether = true;

            var row = signatures.AddRow();
            row.Height = Unit.FromCentimeter(1.6);
            row.VerticalAlignment = VerticalAlignment.Bottom;

            var date = row.Cells[0].AddParagraph();
            date.AddText(Words.Pdf("pdf.consent.date") + " ");
            date.AddFormattedText(Day(model.SubmittedAt), TextFormat.Underline);

            var sign = row.Cells[1].AddParagraph();
            sign.AddText(Words.Pdf("pdf.consent.signature") + " ");

            if (images.Signature is { } signature)
            {
                var image = sign.AddImage(signature);
                image.Height = Unit.FromCentimeter(1.3);
                image.LockAspectRatio = true;
            }
            else
            {
                sign.AddText("____________________");
            }

            var name = row.Cells[2].AddParagraph();
            if (DynamicFormAnswers.Text((_values[field.Id] as JsonObject)?["name"]) is { } signed)
            {
                name.AddFormattedText(signed, TextFormat.Underline);
            }
            else
            {
                name.AddText("____________________");
            }

            Origin();
        }

        private void Origin()
        {
            _originPrinted = true;

            var origin = _section.AddParagraph();
            origin.Format.SpaceBefore = 6;
            origin.Format.Font.Size = 7.5;
            origin.Format.Font.Color = Muted;
            origin.AddText(Words.Pdf(
                "pdf.consent.origin",
                ("date", model.SubmittedAt.ToString("dd.MM.yyyy HH:mm", Invariant)),
                ("language", model.Language)));
        }

        // ════════════════════════════════════════════════ B qism

        /// <summary>HR bo'limi — bo'sh: «yorliq | izoh» qatorlari, variantlar ☐, hujjatlar ro'yxati.</summary>
        private void OfficeSection(FormSection section)
        {
            SectionTitle(section);

            Table? table = null;

            foreach (var field in Printed(section))
            {
                if (field.Kind is FieldKind.Checklist or FieldKind.Repeat or FieldKind.Levels or FieldKind.Info)
                {
                    table = null;
                    Block(section, int.MaxValue, field);
                    continue;
                }

                if (table is null)
                {
                    Gap();
                    table = Grid(6.0, 12.0);
                }

                var row = Row(table);
                Label(row.Cells[0], field.Printed);

                if (field.Kind is FieldKind.Choice or FieldKind.MultiChoice or FieldKind.YesNo)
                {
                    Answer(row.Cells[1], field, WideBudget);
                }
                else if (field.Hint?.Latin is { Length: > 0 } hint)
                {
                    Value(row.Cells[1], hint).Format.Font.Color = Muted;
                }
            }
        }

        /// <summary>Hujjatlar ro'yxati: <c>auto</c> bandi (surat, imzo) onlayn olingan bo'lsa ☑ va izohi bilan.</summary>
        private void ChecklistTable(FormField field)
        {
            Gap();

            var table = Grid(NumberWidth, 8.82, 2.82, 5.57);
            Header(table, ["№", field.Printed, Words.Pdf("pdf.hr.submitted"), Words.Pdf("pdf.hr.note")]);

            for (var index = 0; index < field.Items.Count; index++)
            {
                var item = field.Items[index];
                var done = item.Auto switch
                {
                    "photo" => images.Photo is not null,
                    "signature" => images.Signature is not null,
                    _ => false,
                };

                var row = Row(table);
                Number(row.Cells[0], index + 1);
                Value(row.Cells[1], item.Label);
                var tick = row.Cells[2].AddParagraph();
                tick.Format.Alignment = ParagraphAlignment.Center;
                Box(tick, done);
                Value(row.Cells[3], done ? item.Note : null).Format.Font.Color = Muted;
            }
        }

        private void Footer(string text)
        {
            var paragraph = _section.AddParagraph(text);
            paragraph.Format.SpaceBefore = 8;
            paragraph.Format.Font.Size = 7.5;
            paragraph.Format.Font.Italic = true;
            paragraph.Format.Font.Color = Muted;
        }

        // ════════════════════════════════════════════════ ta'rif

        /// <summary>Bo'limning PDF ga chiqadigan savollari (<c>pdf_hidden</c> va surat — yo'q; surat sarlavhada).</summary>
        private static List<FormField> Printed(FormSection section)
            => [.. section.Fields.Where(field => !field.PdfHidden && field.Kind != FieldKind.Photo)];

        /// <summary>«1. SHAXSIY MA'LUMOTLAR» — raqam ketma-ket (B qism davom ettiradi).</summary>
        private void SectionTitle(FormSection section)
        {
            _sectionHasContent = false;
            _grid = null;
            _pending = null;

            var name = section.PdfTitle ?? section.Title.Latin;
            Title($"{++_sectionNumber}. {name.ToUpperInvariant()}");
        }

        private static string YesWord(FormField field) => field.Yes?.Latin ?? Words.Pdf("pdf.common.yes");

        private static string NoWord(FormField field) => field.No?.Latin ?? Words.Pdf("pdf.common.no");

        // ════════════════════════════════════════════════ qurilish bloklari

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

        private static Row Row(Table table, double height = 0.74)
        {
            var row = table.AddRow();
            row.Height = Unit.FromCentimeter(height);
            row.HeightRule = RowHeightRule.AtLeast;
            row.VerticalAlignment = VerticalAlignment.Center;

            return row;
        }

        private static Row Header(Table table, string[] titles)
        {
            var row = table.AddRow();
            row.HeadingFormat = true;
            row.Height = Unit.FromCentimeter(0.55);
            row.HeightRule = RowHeightRule.AtLeast;
            row.VerticalAlignment = VerticalAlignment.Center;

            for (var index = 0; index < titles.Length; index++)
            {
                HeaderCell(row.Cells[index], titles[index]);
            }

            return row;
        }

        private static void HeaderCell(Cell cell, string title)
        {
            cell.Shading.Color = Navy;
            var paragraph = cell.AddParagraph();
            paragraph.Format.Alignment = ParagraphAlignment.Center;
            var text = paragraph.AddFormattedText(title, TextFormat.Bold);
            text.Font.Color = Colors.White;
            text.Font.Size = 8.5;
        }

        private static void Label(Cell cell, string text)
        {
            cell.Shading.Color = LabelFill;
            cell.AddParagraph().AddFormattedText(text, TextFormat.Bold);
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

        private static void Small(Cell cell, string text)
        {
            var paragraph = cell.AddParagraph(text);
            paragraph.Format.Font.Size = 8;
        }

        private static void Number(Cell cell, int number)
        {
            var paragraph = cell.AddParagraph(number.ToString(Invariant));
            paragraph.Format.Alignment = ParagraphAlignment.Center;
            paragraph.Format.Font.Color = Muted;
        }

        private static void Level(Cell cell, int? level)
        {
            var paragraph = cell.AddParagraph(level?.ToString(Invariant) ?? string.Empty);
            paragraph.Format.Alignment = ParagraphAlignment.Center;
            paragraph.Format.Font.Bold = true;
        }

        /// <summary>
        /// Variantlarni katak eniga qarab qatorlarga teradi: qisqalari yonma-yon, uzuni yangi qatorda
        /// (belgi, bo'shliq va oraliq — har variantga ~4 belgi).
        /// </summary>
        private static (string Label, bool Chosen)[][] Lines(IEnumerable<(string Label, bool Chosen)> choices, int budget)
        {
            var lines = new List<(string Label, bool Chosen)[]>();
            var line = new List<(string Label, bool Chosen)>();
            var used = 0;

            foreach (var choice in choices)
            {
                var width = choice.Label.Length + 4;

                if (line.Count > 0 && used + width > budget)
                {
                    lines.Add([.. line]);
                    line.Clear();
                    used = 0;
                }

                line.Add(choice);
                used += width;
            }

            if (line.Count > 0)
            {
                lines.Add([.. line]);
            }

            return [.. lines];
        }

        /// <summary>Variantlar: har ichki ro'yxat — bitta qator, tanlangani ☑ va qalin.</summary>
        private static void Choices(Cell cell, (string Label, bool Chosen)[][] lines)
        {
            var paragraph = cell.AddParagraph();

            for (var line = 0; line < lines.Length; line++)
            {
                if (line > 0)
                {
                    paragraph.AddLineBreak();
                }

                for (var index = 0; index < lines[line].Length; index++)
                {
                    var (label, chosen) = lines[line][index];

                    if (index > 0)
                    {
                        paragraph.AddSpace(5);
                    }

                    Box(paragraph, chosen);
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
        }

        private static void Box(Paragraph paragraph, bool chosen)
        {
            var box = paragraph.AddFormattedText(chosen ? Checked : Unchecked);
            box.Font.Name = BundledFontResolver.Symbols;
            box.Font.Bold = false;

            if (chosen)
            {
                box.Font.Color = Tick;
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

        /// <summary>To'q ko'k qism chizig'i («A QISM  NOMZOD TOMONIDAN TO'LDIRILADI»).</summary>
        private void Bar(string text)
        {
            Spacer(8);

            var table = _section.AddTable();
            table.Borders.Visible = false;
            table.LeftPadding = Unit.FromCentimeter(0.15);
            table.AddColumn(Unit.FromCentimeter(PageWidth));

            var row = table.AddRow();
            row.Height = Unit.FromCentimeter(0.74);
            row.HeightRule = RowHeightRule.AtLeast;
            row.VerticalAlignment = VerticalAlignment.Center;
            row.Cells[0].Shading.Color = Navy;

            var title = row.Cells[0].AddParagraph();
            title.Format.Font.Bold = true;
            title.Format.Font.Color = Colors.White;
            title.Format.Font.Size = 10.5;
            title.AddText(text);

            Spacer(2);
        }

        private void Spacer(double points, bool keepWithNext = false)
        {
            var paragraph = _section.AddParagraph();
            paragraph.Format.KeepWithNext = keepWithNext;
            paragraph.Format.SpaceBefore = 0;
            paragraph.Format.LineSpacingRule = LineSpacingRule.Exactly;
            paragraph.Format.LineSpacing = Unit.FromPoint(points);
        }

        private static Paragraph Centered(Cell cell, string text, double size, Color color)
        {
            var paragraph = cell.AddParagraph(text);
            paragraph.Format.Alignment = ParagraphAlignment.Center;
            paragraph.Format.Font.Size = size;
            paragraph.Format.Font.Color = color;

            return paragraph;
        }

        private static void Dot(Paragraph paragraph)
        {
            paragraph.AddSpace(2);
            paragraph.AddText("·");
            paragraph.AddSpace(2);
        }

        // ════════════════════════════════════════════════ formatlash

        private static string Day(DateTimeOffset moment) => moment.ToString("dd.MM.yyyy", Invariant);

        /// <summary>«03.2017 – 05.2020», «03.2017 – hozir»; yo'q qism — «____» (o'rnatilgan PDF dagidek).</summary>
        private static string? Period(JsonObject period)
        {
            var from = DynamicFormAnswers.Text(period["from"]);
            var to = DynamicFormAnswers.Text(period["to"]);
            var current = DynamicFormAnswers.Flag(period["current"]) == true;

            if (from is null && to is null && !current)
            {
                return null;
            }

            var start = from is null ? "____" : QuestionnaireFormMapping.Month(from);
            var end = current ? Words.Pdf("pdf.work.present") : to is null ? "____" : QuestionnaireFormMapping.Month(to);

            return $"{start} – {end}";
        }

        /// <summary>«2010 – 2014», «2010 –», «2014».</summary>
        private static string? Years(JsonObject range)
        {
            var from = DynamicFormAnswers.Number(range["from"]);
            var to = DynamicFormAnswers.Number(range["to"]);

            return (from, to) switch
            {
                (null, null) => null,
                (null, { } end) => end.ToString(Invariant),
                ({ } start, null) => $"{start.ToString(Invariant)} –",
                ({ } start, { } end) => $"{start.ToString(Invariant)} – {end.ToString(Invariant)}",
            };
        }

        /// <summary>Kanonik O'zbekiston raqami — ekrandagidek; boshqasi o'zicha.</summary>
        private static string? Phone(string? value)
            => value is { Length: 13 } && value.StartsWith("+998", StringComparison.Ordinal)
                ? PhoneNumber.FromTrusted(value).Formatted
                : value;
    }
}
