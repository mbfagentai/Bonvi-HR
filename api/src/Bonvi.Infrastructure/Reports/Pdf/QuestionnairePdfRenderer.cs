using System.Globalization;
using Bonvi.Application.Recruiting.Forms;
using Bonvi.Application.Recruiting.Forms.Texts;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using SkiaSharp;

namespace Bonvi.Infrastructure.Reports.Pdf;

/// <summary>
/// HR-F02 «Nomzod anketasi» — qog'oz blanka ko'rinishidagi PDF (SPEC §6.3).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>O'lchamlar blankaning o'zidan</b> (<c>HR-F02_Nomzod_anketasi_v2.docx</c>):
/// A4, chetlar 1,5 sm, sarlavha va pastki qator 0,74 sm, jadval ustunlari
/// docx'dagi <c>tblGrid</c> qiymatlari (sm), ranglar — to'q ko'k <c>#1F3A5F</c>,
/// yorliq katagi <c>#F2F5F9</c>, kulrang matn <c>#6B7686</c>, chegara
/// <c>#AEB8C6</c>. Maqsad — HR qog'ozni va PDF'ni yonma-yon qo'yganda farqni
/// ko'rmasin: ikkalasi ham bitta yig'majildga tushadi.
/// </para>
/// <para>
/// Barcha variantlar chiziladi: tanlangani ☑, qolgani ☐ — qog'ozda nomzod
/// shunday belgilaydi. Jadvallar kamida qog'ozdagi qatorlar soni bilan
/// chiziladi va javob ko'p bo'lsa o'sadi.
/// </para>
/// <para>
/// B qism (12–14) bo'sh chiziladi — uni hozircha HR qo'lda to'ldiradi. Faqat
/// ikki belgi tayyor: surat va imzolangan A qism onlayn olingan.
/// </para>
/// <para>
/// <b>Matnlar HRMentor dan</b> (SPEC-blankalar-hrmentor §5.3): har bir o'zbekcha matn
/// <see cref="FormTexts.Pdf(string)"/> orqali — HR o'zgartirgan bo'lsa uniki, aks holda
/// <see cref="QuestionnairePdfTexts"/> dagi asl matn (PDF avvalgidek, bayt-baytigacha).
/// Variantlar qatori (uy turi, ma'lumot, …) HR ro'yxati bo'yicha: tartib va nom HR niki,
/// yashirilgan variant faqat tanlangan bo'lsa chiqadi.
/// </para>
/// </remarks>
public sealed partial class QuestionnairePdfRenderer : IQuestionnairePdfRenderer
{
    // ── Blankaning ranglari ──
    private static readonly Color Navy = new(0x1F, 0x3A, 0x5F);
    private static readonly Color LabelFill = new(0xF2, 0xF5, 0xF9);
    private static readonly Color Muted = new(0x6B, 0x76, 0x86);
    private static readonly Color Rule = new(0xAE, 0xB8, 0xC6);
    private static readonly Color Ink = new(0x1A, 0x1F, 0x2B);
    private static readonly Color Tick = new(0x1E, 0x8E, 0x3E);

    private const string Checked = "☑";
    private const string Unchecked = "☐";
    private const string Blank = "__________";

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>Saqlangan surat — 512×512 kvadrat; chegara faqat eski yoki qo'lda qo'yilgan fayllar uchun.</summary>
    private const int MaxPhotoEdge = 4096;

    /// <summary>Imzo chegarasi — qabul qilishdagi bilan bir xil (<c>SubmitQuestionnaireFormCommandHandler</c>).</summary>
    private const int MaxSignatureWidth = SubmitQuestionnaireFormCommandHandler.MaxSignatureWidth;

    /// <inheritdoc cref="MaxSignatureWidth"/>
    private const int MaxSignatureHeight = SubmitQuestionnaireFormCommandHandler.MaxSignatureHeight;

    /// <summary>
    /// Bir vaqtda ko'pi bilan IKKITA PDF chiziladi — jarayon bo'yicha (onlayn anketa yuborishi va HR
    /// «PDF» tugmasi birga).
    /// </summary>
    /// <remarks>
    /// Bitta chizish o'nlab MB xotira va bir soniyagacha protsessor oladi; server 1.9 GB RAM va
    /// 2 yadroli. Cheklovsiz bir lahzada kelgan o'nta so'rov HR ishini sekinlatardi. Uchinchisi
    /// navbatda kutadi — rad etilmaydi.
    /// </remarks>
    internal static readonly SemaphoreSlim RenderGate = new(2, 2);

    private readonly ILogger<QuestionnairePdfRenderer> _logger;

    public QuestionnairePdfRenderer(ILogger<QuestionnairePdfRenderer> logger) => _logger = logger;

    [LoggerMessage(EventId = 4700, Level = LogLevel.Warning, Message = "Anketa №{Number}: {Part} PDF'ga qo'yilmadi — katak bo'sh qoldi")]
    private partial void LogImageSkipped(Exception failure, int number, string part);

    public async Task<byte[]> RenderAsync(QuestionnairePdfModel model, CancellationToken cancellationToken = default)
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

    private byte[] Render(QuestionnairePdfModel model)
    {
        BundledFontResolver.EnsureInstalled();

        var document = new Builder(model, PrepareImages(model)).Build();

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();

        using var output = new MemoryStream();
        renderer.PdfDocument.Save(output, closeStream: false);

        return output.ToArray();
    }

    /// <summary>
    /// Rasmlarni PDF uchun tayyorlaydi: surat 3×4 ga qirqiladi, imzo oq fonga
    /// yotqiziladi (shaffof PNG). Buzuq rasm — anketa baribir chiziladi, katak bo'sh.
    /// </summary>
    private PdfImages PrepareImages(QuestionnairePdfModel model)
    {
        string? photo = null;
        string? signature = null;

        try
        {
            photo = model.Photo is { Length: > 0 } bytes ? Base64(PortraitJpeg(bytes)) : null;
        }
#pragma warning disable CA1031 // Buzuq surat PDF'ni yiqitmasin — katak bo'sh qoladi
        catch (Exception failure)
#pragma warning restore CA1031
        {
            LogImageSkipped(failure, model.Number, "surat");
        }

        try
        {
            signature = model.Signature is { Length: > 0 } bytes ? Base64(FlatPng(bytes)) : null;
        }
#pragma warning disable CA1031 // Buzuq imzo PDF'ni yiqitmasin — chiziq bo'sh qoladi
        catch (Exception failure)
#pragma warning restore CA1031
        {
            LogImageSkipped(failure, model.Number, "imzo");
        }

        return new PdfImages(
            Base64(BundledFontResolver.ReadResource("Assets.mbf-logo.png")),
            photo,
            signature);
    }

    /// <remarks>Konstruktor anketasi (<see cref="DynamicFormPdfRenderer"/>) ham shundan foydalanadi.</remarks>
    internal static string Base64(byte[] bytes) => "base64:" + Convert.ToBase64String(bytes);

    /// <summary>Kvadrat suratni markazdan 3:4 (eni:bo'yi) ga qirqadi.</summary>
    internal static byte[] PortraitJpeg(byte[] source)
    {
        EnsureWithin(source, MaxPhotoEdge, MaxPhotoEdge, "surat");

        using var bitmap = SKBitmap.Decode(source) ?? throw new InvalidDataException("surat o'qilmadi");

        var width = Math.Min(bitmap.Width, bitmap.Height * 3 / 4);
        var height = Math.Min(bitmap.Height, bitmap.Width * 4 / 3);
        var left = (bitmap.Width - width) / 2;
        var top = (bitmap.Height - height) / 2;

        // Rgba8888 — `SkiaImageProcessor` bilan bir xil: JPEG kodlovchisi Rgb888x ni qabul qilmaydi
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.White);
        surface.Canvas.DrawBitmap(bitmap, new SKRect(left, top, left + width, top + height), new SKRect(0, 0, width, height));

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90)
            ?? throw new InvalidDataException("surat JPEG'ga kodlanmadi");

        return data.ToArray();
    }

    /// <summary>Imzoni oq fonga yotqizadi — PDF'da shaffoflik chegarasi qolmasin.</summary>
    internal static byte[] FlatPng(byte[] source)
    {
        EnsureWithin(source, MaxSignatureWidth, MaxSignatureHeight, "imzo");

        using var bitmap = SKBitmap.Decode(source) ?? throw new InvalidDataException("imzo o'qilmadi");

        using var surface = SKSurface.Create(new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Rgb888x, SKAlphaType.Opaque));
        surface.Canvas.Clear(SKColors.White);
        surface.Canvas.DrawBitmap(bitmap, 0, 0);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidDataException("imzo PNG'ga kodlanmadi");

        return data.ToArray();
    }

    /// <summary>
    /// O'lchamni SARLAVHADAN tekshiradi — chegaradan katta rasm umuman dekodlanmaydi.
    /// </summary>
    /// <remarks>
    /// Saqlangan fayllar qabul qilishda allaqachon tekshirilgan; bu ikkinchi qatlam diskka
    /// boshqa yo'l bilan tushgan fayl uchun: PDF har safar HR «PDF» ni bosganda chiziladi va
    /// bitta «bomba» fayl API jarayonini har safar o'ldirardi. Istisno <see cref="PrepareImages"/>
    /// da ushlanadi — PDF rasmsiz chiziladi.
    /// </remarks>
    private static void EnsureWithin(byte[] source, int maxWidth, int maxHeight, string part)
    {
        using var data = SKData.CreateCopy(source);
        using var codec = SKCodec.Create(data) ?? throw new InvalidDataException(part + " o'qilmadi");

        if (codec.Info.Width <= 0 || codec.Info.Height <= 0 || codec.Info.Width > maxWidth || codec.Info.Height > maxHeight)
        {
            throw new InvalidDataException(part + " o'lchami chegaradan katta");
        }
    }

    private sealed record PdfImages(string Logo, string? Photo, string? Signature);

    /// <summary>Bitta hujjatni yig'uvchi — har bir bo'lim alohida metod, blanka tartibida.</summary>
    private sealed class Builder(QuestionnairePdfModel model, PdfImages images)
    {
        private readonly QuestionnaireFormAnswers _a = model.Answers;
        private readonly FormTexts _t = model.Texts ?? FormTexts.Empty;
        private readonly Document _document = new();
        private Section _section = null!;

        public Document Build()
        {
            Styles();
            Page();

            TitleBlock();
            InfoTable();
            Instruction();

            Bar(T("pdf.head.partA"), T("pdf.head.partAText"));
            Hint(T("pdf.head.partAHint"));

            Personal();
            Education();
            Languages();
            Computer();
            Work();
            References();
            Family();
            Conditions();
            Health();
            AboutYou();
            Consent();

            _section.AddPageBreak();
            HrPart();

            return _document;
        }

        // ════════════════════════════════════════════════ sahifa va uslub

        private void Styles()
        {
            _document.Info.Title = $"Nomzod anketasi HR-F02 №{model.Number}";
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

            // Ikki ustunli jadval, tabulyator emas: o'ng tomonga tekislangan tabulyator
            // matnni birinchi bo'shliqda sindirib, ikkinchi qatorga tushirardi.
            var header = _section.Headers.Primary.AddTable();
            header.Borders.Visible = false;
            header.AddColumn(Unit.FromCentimeter(9));
            header.AddColumn(Unit.FromCentimeter(9));
            header.Format.Font.Size = 8;
            header.Format.Font.Color = Muted;

            var headerRow = header.AddRow();
            headerRow.Cells[0].AddParagraph(T("pdf.head.company"));
            var right = headerRow.Cells[1].AddParagraph();
            right.Format.Alignment = ParagraphAlignment.Right;
            right.AddText("HR-F02");
            right.AddSpace(2);
            right.AddText("·");
            right.AddSpace(2);
            right.AddText(T("pdf.head.formName"));
            right.AddSpace(2);
            right.AddText("·");
            right.AddSpace(2);
            right.AddText("v" + model.FormVersion);

            var footer = _section.Footers.Primary.AddParagraph();
            footer.Format.Alignment = ParagraphAlignment.Center;
            footer.Format.Font.Size = 8;
            footer.Format.Font.Color = Muted;
            footer.AddText(T("pdf.head.page") + " ");
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
            var titleText = title.AddFormattedText(T("pdf.head.title"), TextFormat.Bold);
            titleText.Font.Size = 17;
            titleText.Font.Color = Navy;

            Centered(row.Cells[1], T("pdf.head.subtitle"), 9, Muted).Format.SpaceBefore = 3;
            var form = Centered(row.Cells[1], T("pdf.head.blank"), 8, Muted);
            form.Format.SpaceBefore = 2;
            form.AddSpace(2);
            form.AddText("·");
            form.AddSpace(2);
            form.AddText(T("pdf.head.version") + " " + model.FormVersion);

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
                label.AddFormattedText(T("pdf.head.photo"), TextFormat.Bold).Font.Color = Muted;
                Centered(photo, T("pdf.head.photoSize"), 8, Muted);
            }

            Spacer(6);
        }

        private void InfoTable()
        {
            var table = Grid(2.29, 3.17, 1.59, 5.11, 1.94, 3.89);

            var first = Row(table, 0.74);
            Label(first.Cells[0], T("pdf.head.number"));
            Value(first.Cells[1], model.Number.ToString(Invariant)).Format.Font.Bold = true;
            Label(first.Cells[2], T("pdf.head.date"));
            Value(first.Cells[3], Date(DateOnly.FromDateTime(model.SubmittedAt.DateTime)));
            Label(first.Cells[4], T("pdf.head.request"));
            Value(first.Cells[5], "(HR-F01)").Format.Font.Color = Muted;

            var second = Row(table, 0.74);
            Label(second.Cells[0], T("pdf.head.vacancy"));
            Value(second.Cells[1], model.VacancyTitle);
            second.Cells[1].MergeRight = 2;
            Label(second.Cells[4], T("pdf.head.department"));
            Value(second.Cells[5], null);
        }

        private void Instruction()
        {
            var text = _section.AddParagraph();
            text.Format.SpaceBefore = 5;
            text.Format.Font.Size = 8;
            text.Format.Font.Italic = true;
            text.Format.Font.Color = Muted;
            var (before, after) = _t.PdfAround("pdf.head.instruction", "box");
            text.AddText(before);
            Box(text, true);
            text.AddText(after);
        }

        // ════════════════════════════════════════════════ 1. shaxsiy

        private void Personal()
        {
            Title(T("pdf.personal.title"));

            var table = Grid(3.79, 5.21, 3.79, 5.21);

            Pair(table, T("pdf.personal.lastName"), _a.LastName, T("pdf.personal.firstName"), _a.FirstName);
            Pair(table, T("pdf.personal.middleName"), _a.MiddleName, T("pdf.personal.birthDate"), _a.BirthDate is { } birth ? Date(birth) : null);

            // Blankadagi «Jinsi / JSHSHIR» va «Pasport / Kim tomonidan va qachon berilgan» qatorlari
            // ATAYLAB yo'q (egasi, 2026-10-26): pasport kengash suhbatidan keyin olinadi. B qismdagi
            // «Pasport nusxasi» belgisi esa HR'ning o'z ro'yxati — u qoladi.
            Wide(table, T("pdf.personal.registeredAddress"), _a.RegisteredAddress);
            Wide(table, T("pdf.personal.actualAddress"), _a.ActualAddress);

            var housing = Row(table);
            Label(housing.Cells[0], T("pdf.personal.housing"));
            var housingType = _a.HousingType?.ToString();
            Choices(housing.Cells[1], Options(
                HrF02TextCatalog.HousingList,
                [[(nameof(FormHousingType.Apartment), "Ko‘p qavatli uy"), (nameof(FormHousingType.House), "Hovli")]],
                value => value == housingType));
            Label(housing.Cells[2], T("pdf.personal.mahalla"));
            Value(housing.Cells[3], _a.Mahalla);

            Pair(table, T("pdf.personal.phone"), Phone(_a.Phone) ?? "+998", T("pdf.personal.phoneExtra"), Phone(_a.PhoneExtra) ?? "+998");
            Pair(table, T("pdf.personal.telegram"), "@" + _a.Telegram, T("pdf.personal.email"), _a.Email);

            var marital = Row(table);
            Label(marital.Cells[0], T("pdf.personal.marital"));
            var maritalStatus = _a.MaritalStatus?.ToString();
            Choices(marital.Cells[1], Options(
                HrF02TextCatalog.MaritalList,
                [
                    [(nameof(MaritalStatus.Married), "Oilali"), (nameof(MaritalStatus.Unmarried), "Bo‘ydoq / turmush qurmagan")],
                    [(nameof(MaritalStatus.Divorced), "Ajrashgan"), (nameof(MaritalStatus.Widowed), "Beva")],
                ],
                value => value == maritalStatus));
            Label(marital.Cells[2], T("pdf.personal.children"));
            Value(marital.Cells[3], _a.ChildrenCount?.ToString(Invariant));

            var military = Row(table);
            Label(military.Cells[0], T("pdf.personal.military"));
            var served = _a.MilitaryService == FormMilitaryService.Served;
            var months = served && _a.MilitaryServiceMonths is { } m ? m.ToString(Invariant) : "____";
            var militaryService = _a.MilitaryService?.ToString();
            Choices(military.Cells[1], Options(
                HrF02TextCatalog.MilitaryList,
                [[
                    (nameof(FormMilitaryService.Served), "O‘tagan"),
                    (nameof(FormMilitaryService.Reserve), "Zaxirada"),
                    (nameof(FormMilitaryService.Deferred), "Kechiktirilgan"),
                    (nameof(FormMilitaryService.NotServed), "O‘tamagan"),
                ]],
                value => value == militaryService,
                (value, label) => value == nameof(FormMilitaryService.Served)
                    ? _t.Pdf("pdf.personal.militaryServed", ("label", label), ("months", months))
                    : label));
            military.Cells[1].MergeRight = 2;

            var license = Row(table);
            Label(license.Cells[0], T("pdf.personal.license"));
            var categories = _a.DriverLicenseCategories is { Count: > 0 } chosen ? string.Join(", ", chosen) : "________";
            Choices(license.Cells[1], [[(T("pdf.common.no"), _a.HasDriverLicense == false), (_t.Pdf("pdf.personal.licenseYes", ("categories", categories)), _a.HasDriverLicense == true)]]);
            Label(license.Cells[2], T("pdf.personal.car"));
            var car = _a.HasCar == true && _a.CarModel is { } carModel ? carModel : "_____";
            Choices(license.Cells[3], [[(T("pdf.common.no"), _a.HasCar == false), (_t.Pdf("pdf.personal.carYes", ("car", car)), _a.HasCar == true)]]);
        }

        // ════════════════════════════════════════════════ 2. ma'lumoti

        private void Education()
        {
            Title(T("pdf.education.title"));

            var level = Grid(3.79, 5.21, 3.79, 5.21);
            var row = Row(level);
            Label(row.Cells[0], T("pdf.education.level"));
            var chosen = _a.EducationLevel?.ToString();
            Choices(row.Cells[1], Options(
                HrF02TextCatalog.EducationList,
                [
                    [
                        (nameof(EducationLevel.SecondarySchool), "O‘rta (maktab)"),
                        (nameof(EducationLevel.VocationalSchool), "O‘rta maxsus (kollej, texnikum, litsey)"),
                        (nameof(EducationLevel.HigherIncomplete), "Oliy (tugallanmagan)"),
                    ],
                    [
                        (nameof(EducationLevel.HigherComplete), "Oliy (bakalavr)"),
                        (nameof(EducationLevel.HigherMaster), "Oliy (magistr)"),
                    ],
                ],
                value => value == chosen));
            row.Cells[1].MergeRight = 2;

            Spacer(5);

            var schools = Grid(0.79, 5.82, 5.03, 2.91, 3.45);
            Header(schools, "№", T("pdf.education.institution"), T("pdf.education.faculty"), T("pdf.education.years"), T("pdf.education.diploma"));

            var educations = _a.Educations ?? [];
            for (var index = 0; index < Math.Max(3, educations.Count); index++)
            {
                var school = index < educations.Count ? educations[index] : null;
                var line = Row(schools);
                Number(line.Cells[0], index + 1);
                Value(line.Cells[1], school?.Institution);
                Value(line.Cells[2], school?.Faculty);
                Value(line.Cells[3], Years(school?.StartYear, school?.EndYear));
                Value(line.Cells[4], school?.DiplomaNumber);
            }

            Spacer(5);

            var extra = Grid(3.79, 5.21, 3.79, 5.21);
            Pair(extra, T("pdf.education.school"), _a.SchoolName, T("pdf.education.subjects"), _a.StrongSubjects);
            Wide(extra, T("pdf.education.courses"), _a.Courses);
        }

        // ════════════════════════════════════════════════ 3. tillar

        private void Languages()
        {
            Title(T("pdf.languages.title"));
            Hint(T("pdf.languages.scale"));

            var table = Grid(0.79, 5.57, 3.88, 3.88, 3.88);
            Header(table, "№", T("pdf.languages.language"), T("pdf.languages.speaking"), T("pdf.languages.reading"), T("pdf.languages.writing"));

            var rows = _a.Languages ?? [];
            var lines = new List<(string Name, FormLanguageSkill? Skill)>();

            foreach (var (language, label) in FixedLanguageRows())
            {
                lines.Add((label, rows.FirstOrDefault(row => row.Language == language)));
            }

            var others = rows.Where(row => row.Language == FormLanguage.Other).ToList();
            foreach (var other in others)
            {
                lines.Add((_t.Pdf("pdf.languages.other", ("name", other.OtherName ?? string.Empty)), other));
            }

            if (others.Count == 0)
            {
                lines.Add((_t.Pdf("pdf.languages.other", ("name", "______________")), null));
            }

            for (var index = 0; index < lines.Count; index++)
            {
                var (name, skill) = lines[index];
                var row = Row(table, 0.71);
                Number(row.Cells[0], index + 1);
                Value(row.Cells[1], name);
                Level(row.Cells[2], skill?.Speaking);
                Level(row.Cells[3], skill?.Reading);
                Level(row.Cells[4], skill?.Writing);
            }
        }

        // ════════════════════════════════════════════════ 4. kompyuter

        /// <remarks>
        /// Qisqartirilgan jadval (egasi, 2026-10-26): 3 ustun — operatsion tizim, ofis, maxsus
        /// dasturlar — har biri «dastur | daraja» juftligi, kamida 5 qator. Blankadagi har ustun
        /// ostidagi «Boshqa: ____» katagi va «Dasturlash» ustuni o'rniga jadval ostida bitta
        /// «Boshqa dasturlar va dasturlash tillari» qatori — u doim chiziladi, bo'sh bo'lsa
        /// qog'ozdagidek chiziqcha bilan.
        /// </remarks>
        private void Computer()
        {
            Title(T("pdf.computer.title"));
            Hint(T("pdf.computer.scale"));

            var table = Grid(4.94, 1.06, 4.94, 1.06, 4.94, 1.06);
            var skills = _a.ComputerSkills ?? new Dictionary<string, int>();
            var groups = ComputerColumns(skills);

            var header = table.AddRow();
            header.HeadingFormat = true;
            header.Height = Unit.FromCentimeter(0.55);
            header.VerticalAlignment = VerticalAlignment.Center;

            for (var column = 0; column < groups.Count; column++)
            {
                var cell = header.Cells[column * 2];
                HeaderCell(cell, groups[column].Title);
                header.Cells[(column * 2) + 1].Shading.Color = Navy;
                cell.MergeRight = 1;
            }

            var rows = Math.Max(5, groups.Max(group => group.Items.Count));

            // Kichik jadval sahifa chegarasida bo'linmasin — sarlavha qatorlari bilan birga
            header.KeepWith = rows;

            for (var index = 0; index < rows; index++)
            {
                var row = Row(table, 0.55);

                for (var column = 0; column < groups.Count; column++)
                {
                    var items = groups[column].Items;

                    if (index < items.Count)
                    {
                        Small(row.Cells[column * 2], items[index].Label);
                        Level(row.Cells[(column * 2) + 1], skills.TryGetValue(items[index].Key, out var level) ? level : null);
                    }
                }
            }

            var other = _section.AddParagraph();
            other.Format.SpaceBefore = 4;
            other.AddText(T("pdf.computer.other") + " ");

            if (_a.ComputerSkillsOther is { } text)
            {
                other.AddFormattedText(text, TextFormat.Underline);
            }
            else
            {
                other.AddText("________________________________________________");
            }
        }

        // ════════════════════════════════════════════════ 5. mehnat

        /// <remarks>
        /// 3.1: bo'lim boshida «Avval ishlaganmisiz?» tanlovi. «Yo'q» bo'lsa jadval baribir
        /// qog'ozdagidek bo'sh qatorlar bilan chiziladi. Blankadagi «Umumiy ish tajribangiz»
        /// qatori ATAYLAB yo'q (HR rahbari, 2026-10-27) — eski 3.0 javobidagi yil/oy ham chiqmaydi.
        /// </remarks>
        private void Work()
        {
            Title(T("pdf.work.title"));

            var experience = Grid(3.79, 5.21, 3.79, 5.21);
            var question = Row(experience);
            Label(question.Cells[0], T("pdf.work.experience"));
            YesNo(question.Cells[1], _a.HasWorkExperience);
            question.Cells[2].Borders.Visible = false;
            question.Cells[3].Borders.Visible = false;

            Spacer(5);
            Hint(T("pdf.work.hint"));

            var table = Grid(0.79, 4.76, 3.35, 3.0, 6.1);
            Header(table, "№", T("pdf.work.organization"), T("pdf.work.position"), T("pdf.work.period"), T("pdf.work.leaveReason"));

            var jobs = _a.Jobs ?? [];
            for (var index = 0; index < Math.Max(4, jobs.Count); index++)
            {
                var job = index < jobs.Count ? jobs[index] : null;
                var row = Row(table, 0.85);
                Number(row.Cells[0], index + 1);
                Value(row.Cells[1], job?.Organization);
                Value(row.Cells[2], job?.Position);
                Value(row.Cells[3], job is null ? null : Period(job));
                Value(row.Cells[4], job?.LeaveReason);
            }
        }

        // ════════════════════════════════════════════════ 6. tavsiya

        private void References()
        {
            Title(T("pdf.references.title"));
            Hint(T("pdf.references.hint"));

            var table = Grid(0.79, 5.64, 6.1, 5.47);
            Header(table, "№", T("pdf.references.fullName"), T("pdf.references.organizationPosition"), T("pdf.references.phone"));

            var references = _a.References ?? [];
            for (var index = 0; index < Math.Max(2, references.Count); index++)
            {
                var reference = index < references.Count ? references[index] : null;
                var row = Row(table, 0.85);
                Number(row.Cells[0], index + 1);
                Value(row.Cells[1], reference?.FullName);
                Value(row.Cells[2], reference?.OrganizationPosition);
                Value(row.Cells[3], Phone(reference?.Phone));
            }
        }

        // ════════════════════════════════════════════════ 7. oila

        private void Family()
        {
            Title(T("pdf.family.title"));
            Hint(T("pdf.family.hint"));

            var table = Grid(0.71, 2.56, 3.53, 1.94, 3.62, 2.38, 3.27);
            Header(
                table,
                "№",
                T("pdf.family.relation"),
                T("pdf.family.fullName"),
                T("pdf.family.birthYear"),
                T("pdf.family.workplace"),
                T("pdf.family.phone"),
                T("pdf.family.address"));

            var family = _a.FamilyMembers ?? [];
            for (var index = 0; index < Math.Max(5, family.Count); index++)
            {
                var member = index < family.Count ? family[index] : null;
                var row = Row(table, 0.85);
                Number(row.Cells[0], index + 1);
                Value(row.Cells[1], member?.Relation is { } relation ? RelationName(relation) : null);
                Value(row.Cells[2], member?.FullName);
                Value(row.Cells[3], member?.BirthYear?.ToString(Invariant));
                Value(row.Cells[4], member?.Workplace);
                Value(row.Cells[5], Phone(member?.Phone));
                Value(row.Cells[6], member?.Address);
            }
        }

        // ════════════════════════════════════════════════ 8. sharoitlar

        private void Conditions()
        {
            Title(T("pdf.conditions.title"));

            var table = Grid(0.79, 9.17, 8.04);
            Header(table, "№", T("pdf.common.question"), T("pdf.common.answer"));

            Question(table, 1, T("pdf.conditions.referral"), cell =>
                Choices(cell, ReferralLines(model.ReferralChoices, _a.ReferralDetail)));

            Question(table, 2, T("pdf.conditions.acquaintances"), cell =>
                YesNoDetail(cell, _a.HasAcquaintances, _a.AcquaintancesDetail, T("pdf.common.yes")));

            Question(table, 3, T("pdf.conditions.lastSalary"), cell =>
                Text(cell, _a.LastSalary is { } last ? QuestionnaireFormMapping.Money(last) : T("pdf.conditions.salaryBlank")));

            Question(table, 4, T("pdf.conditions.expectedSalary"), cell =>
                Text(cell, _a.ExpectedSalary is { } expected ? QuestionnaireFormMapping.Money(expected) : T("pdf.conditions.salaryBlank")));

            Question(table, 5, T("pdf.conditions.startAfter"), cell => StartAfter(cell));

            Question(table, 6, T("pdf.conditions.shifts"), cell => YesNo(cell, _a.CanWorkShifts));
            Question(table, 7, T("pdf.conditions.overtime"), cell => YesNo(cell, _a.CanWorkOvertime));
            Question(table, 8, T("pdf.conditions.travel"), cell => YesNo(cell, _a.CanTravel));

            Question(table, 9, T("pdf.conditions.otherJob"), cell =>
                YesNoDetail(cell, _a.HasOtherJob, _a.OtherJobDetail, T("pdf.common.yes")));

            Question(table, 10, T("pdf.conditions.loan"), cell =>
            {
                var payment = _a.HasLoan == true && _a.LoanMonthlyPayment is { } amount
                    ? QuestionnaireFormMapping.Money(amount)
                    : T("pdf.conditions.loanBlank");

                Choices(cell, [[(T("pdf.common.no"), _a.HasLoan == false), (_t.Pdf("pdf.conditions.loanYes", ("payment", payment)), _a.HasLoan == true)]]);
            });

            Question(table, 11, T("pdf.conditions.trial"), cell => YesNo(cell, _a.AgreesToTrial));
            Question(table, 12, T("pdf.conditions.parents"), cell => YesNo(cell, _a.AgreesParentsInvite));
            Question(table, 13, T("pdf.conditions.startConditions"), cell => Text(cell, _a.StartConditions));
        }

        /// <summary>
        /// «Ishga qachondan chiqa olasiz?» — 3.1 dan to'rt variant, har biri alohida qatorda
        /// (javob katagi ikkitasini yonma-yon sig'dirmaydi).
        /// </summary>
        /// <remarks>
        /// Bazadagi 3.0 anketasida tanlov yo'q, aniq sana bor — o'shanda sana avvalgidek chiqadi.
        /// </remarks>
        private void StartAfter(Cell cell)
        {
            if (_a.StartAfter is null && _a.AvailableFrom is { } legacy)
            {
                Text(cell, Date(legacy));
                return;
            }

            var startAfter = _a.StartAfter?.ToString();
            Choices(cell, Options(
                HrF02TextCatalog.StartAfterList,
                [
                    [(nameof(FormStartAfter.OnHireDay), "Qabul qilingan kundan")],
                    [(nameof(FormStartAfter.OneWeek), "Qabul qilingandan 1 hafta keyin")],
                    [(nameof(FormStartAfter.TenDays), "Qabul qilingandan 10 kun keyin")],
                    [(nameof(FormStartAfter.FifteenDays), "Qabul qilingandan 15 kun keyin")],
                ],
                value => value == startAfter));
        }

        // ════════════════════════════════════════════════ 9. sog'liq

        private void Health()
        {
            Title(T("pdf.health.title"));

            var table = Grid(0.79, 9.17, 8.04);
            Header(table, "№", T("pdf.common.question"), T("pdf.common.answer"));

            Question(table, 1, T("pdf.health.restriction"), cell =>
            {
                YesNoDetail(cell, _a.HasHealthRestriction, _a.HealthRestrictionDetail, T("pdf.common.has"));
                var hint = cell.AddParagraph(T("pdf.health.restrictionHint"));
                hint.Format.Font.Size = 7.5;
                hint.Format.Font.Italic = true;
                hint.Format.Font.Color = Muted;
            });

            Question(table, 2, T("pdf.health.patient"), cell =>
                Choices(cell, [[(T("pdf.common.no"), _a.HasFamilyPatient == false), (T("pdf.common.has"), _a.HasFamilyPatient == true)]]));

            Question(table, 3, T("pdf.health.conviction"), cell => YesNoDetail(cell, _a.HasConviction, _a.ConvictionDetail, T("pdf.common.yes")));

            Question(table, 4, T("pdf.health.relativesConviction"), cell =>
                YesNoDetail(cell, _a.RelativesConviction, _a.RelativesConvictionDetail, T("pdf.common.yes")));

            Question(table, 5, T("pdf.health.abroad"), cell => Text(cell, _a.AbroadTrips));
        }

        // ════════════════════════════════════════════════ 10. o'zingiz haqingizda

        private void AboutYou()
        {
            Title(T("pdf.about.title"));
            Hint(T("pdf.about.hint"));

            var table = Grid(0.79, 7.58, 9.62);
            Header(table, "№", T("pdf.common.question"), T("pdf.common.answer"));

            (string Question, string? Answer)[] questions =
            [
                (T("pdf.about.motivation"), _a.Motivation),
                (T("pdf.about.whyUs"), _a.WhyUs),
                (T("pdf.about.strengths"), _a.Strengths),
                (T("pdf.about.weaknesses"), _a.Weaknesses),
                (T("pdf.about.motivators"), _a.Motivators),
                (T("pdf.about.successDefinition"), _a.SuccessDefinition),
                (T("pdf.about.lastJobAchievement"), _a.LastJobAchievement),
                (T("pdf.about.bestIdea"), _a.BestIdea),
                (T("pdf.about.biggestChallenge"), _a.BiggestChallenge),
                (T("pdf.about.stressHandling"), _a.StressHandling),
                (T("pdf.about.unfairnessToYou"), _a.UnfairnessToYou),
                (T("pdf.about.unfairnessToCompany"), _a.UnfairnessToCompany),
                (T("pdf.about.teamwork"), _a.Teamwork),
                (T("pdf.about.previousTeamOpinion"), _a.PreviousTeamOpinion),
                (T("pdf.about.studentWorkExperience"), _a.StudentWorkExperience),
                (T("pdf.about.fiveYearPlan"), _a.FiveYearPlan),
                (T("pdf.about.extraNote"), _a.ExtraNote),
            ];

            for (var index = 0; index < questions.Length; index++)
            {
                var (question, answer) = questions[index];
                var row = Row(table, 1.13);
                Number(row.Cells[0], index + 1);
                Value(row.Cells[1], question);
                Value(row.Cells[2], answer);
            }
        }

        // ════════════════════════════════════════════════ 11. tilxat

        private void Consent()
        {
            Title(T("pdf.consent.title"));

            var fullName = string.Join(' ', new[] { _a.LastName, _a.FirstName, _a.MiddleName }.Where(part => !string.IsNullOrEmpty(part)));

            // Tilxat bo'linmasin: sarlavha, matn va imzo bitta sahifada (qog'ozdagidek)
            var intro = _section.AddParagraph();
            intro.Format.KeepWithNext = true;
            var (before, after) = _t.PdfAround("pdf.consent.intro", "name");
            intro.AddText(before);
            var name = intro.AddFormattedText(fullName, TextFormat.Bold | TextFormat.Underline);
            name.Font.Color = Ink;
            intro.AddText(after);

            string[] items =
            [
                T("pdf.consent.accuracy"),
                T("pdf.consent.personalData"),
                T("pdf.consent.referenceCheck"),
                T("pdf.consent.notifyChanges"),
                T("pdf.consent.confidentiality"),
            ];

            for (var index = 0; index < items.Length; index++)
            {
                var item = _section.AddParagraph();
                item.Format.KeepWithNext = true;
                item.Format.SpaceBefore = 2;
                item.Format.LeftIndent = Unit.FromCentimeter(0.55);
                item.Format.FirstLineIndent = Unit.FromCentimeter(-0.45);
                item.Format.TabStops.AddTabStop(Unit.FromCentimeter(0.55));
                item.AddText($"{index + 1}.");
                item.AddTab();
                item.AddText(items[index]);
            }

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
            date.AddText(T("pdf.consent.date") + " ");
            date.AddFormattedText(Date(DateOnly.FromDateTime(model.SubmittedAt.DateTime)), TextFormat.Underline);

            var sign = row.Cells[1].AddParagraph();
            sign.AddText(T("pdf.consent.signature") + " ");

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

            var origin = _section.AddParagraph();
            origin.Format.SpaceBefore = 6;
            origin.Format.Font.Size = 7.5;
            origin.Format.Font.Color = Muted;
            origin.AddText(_t.Pdf(
                "pdf.consent.origin",
                ("date", model.SubmittedAt.ToString("dd.MM.yyyy HH:mm", Invariant)),
                ("language", model.Language)));
        }

        // ════════════════════════════════════════════════ B qism

        private void HrPart()
        {
            Bar(T("pdf.hr.part"), T("pdf.hr.partText"));
            Hint(T("pdf.hr.hint"));

            Title(T("pdf.hr.documentsTitle"));
            var documents = Grid(0.79, 8.82, 2.82, 5.57);
            Header(documents, "№", T("pdf.hr.document"), T("pdf.hr.submitted"), T("pdf.hr.note"));

            (string Name, bool Done, string? Note)[] papers =
            [
                (T("pdf.hr.photo"), true, T("pdf.hr.photoNote")),
                (T("pdf.hr.passport"), false, null),
                (T("pdf.hr.diploma"), false, null),
                (T("pdf.hr.license"), false, null),
                (T("pdf.hr.noConviction"), false, null),
                (T("pdf.hr.military"), false, null),
                (T("pdf.hr.signedForm"), true, T("pdf.hr.signedNote")),
            ];

            for (var index = 0; index < papers.Length; index++)
            {
                var (name, done, note) = papers[index];
                var row = Row(documents);
                Number(row.Cells[0], index + 1);
                Value(row.Cells[1], name);
                var tick = row.Cells[2].AddParagraph();
                tick.Format.Alignment = ParagraphAlignment.Center;
                Box(tick, done);
                Value(row.Cells[3], note).Format.Font.Color = Muted;
            }

            Title(T("pdf.hr.testsTitle"));
            var tests = Grid(0.79, 6.35, 4.59, 6.27);
            Header(tests, "№", T("pdf.hr.test"), T("pdf.hr.result"), T("pdf.hr.note"));

            TestRow(tests, 1, T("pdf.hr.professional"), cell => Text(cell, T("pdf.hr.points")));
            TestRow(tests, 2, T("pdf.hr.disc"), cell => Choices(cell, [[("D", false), ("I", false), ("S", false), ("C", false)]]));
            TestRow(tests, 3, T("pdf.hr.productivity"), cell => Text(cell, T("pdf.hr.points")));
            TestRow(tests, 4, T("pdf.hr.russian"), cell => Text(cell, T("pdf.hr.russianLevel")));
            TestRow(tests, 5, T("pdf.hr.excel"), cell => Text(cell, T("pdf.hr.excelLevel")));
            TestRow(tests, 6, T("pdf.hr.practical"), _ => { });

            Title(T("pdf.hr.interviewTitle"));
            var interview = Grid(0.79, 6.61, 2.47, 8.12);
            Header(interview, "№", T("pdf.hr.criterion"), T("pdf.hr.score"), T("pdf.hr.note"));

            string[] criteria =
            [
                T("pdf.hr.fit"),
                T("pdf.hr.experience"),
                T("pdf.hr.communication"),
                T("pdf.hr.practicalResult"),
            ];

            for (var index = 0; index < criteria.Length; index++)
            {
                var row = Row(interview);
                Number(row.Cells[0], index + 1);
                Value(row.Cells[1], criteria[index]);
            }

            var decision = Row(interview, 1.0);
            Label(decision.Cells[0], T("pdf.hr.decision"));
            decision.Cells[0].MergeRight = 1;
            Choices(decision.Cells[2], [[(T("pdf.hr.council"), false), (T("pdf.hr.reserve"), false), (T("pdf.hr.rejected"), false)]]);
            decision.Cells[2].AddParagraph(T("pdf.hr.rejectReason"));
            decision.Cells[2].MergeRight = 1;

            Spacer(24);

            var signatures = _section.AddTable();
            signatures.Borders.Visible = false;
            signatures.AddColumn(Unit.FromCentimeter(7.41));
            signatures.AddColumn(Unit.FromCentimeter(4.94));
            signatures.AddColumn(Unit.FromCentimeter(5.65));

            var line = signatures.AddRow();
            line.Cells[0].AddParagraph(T("pdf.hr.specialist"));
            line.Cells[1].AddParagraph(T("pdf.hr.sign"));
            line.Cells[2].AddParagraph(T("pdf.hr.date"));

            var storage = _section.AddParagraph();
            storage.Format.SpaceBefore = 8;
            storage.Format.Font.Size = 7.5;
            storage.Format.Font.Italic = true;
            storage.Format.Font.Color = Muted;
            storage.AddText(T("pdf.hr.storage"));
        }

        // ════════════════════════════════════════════════ HR matnlari va ro'yxatlari

        /// <summary>PDF matni: HR niki ?? asl matn.</summary>
        private string T(string key) => _t.Pdf(key);

        /// <summary>
        /// Variantlar qatori HR ro'yxati bo'yicha (SPEC-blankalar-hrmentor §5.3).
        /// </summary>
        /// <param name="listId">Ro'yxat.</param>
        /// <param name="layout">Qog'ozdagi tartib: (qiymat, asl nom), qatorlar bo'yicha.</param>
        /// <param name="chosen">Qiymat tanlanganmi.</param>
        /// <param name="decorate">Nomga qo'shimcha («O'tagan» → «O'tagan, muddati: 12 oy»).</param>
        /// <remarks>
        /// HR ro'yxatni o'zgartirmagan bo'lsa — qog'ozdagi tartib va nomlar AYNAN (PDF avvalgidek).
        /// O'zgartirgan bo'lsa — HR tartibi; nom HR o'zgartirgani, aks holda qog'ozdagi
        /// (<see cref="FormTexts.ChangedLatin"/>); yashirilgani faqat tanlangan bo'lsa; qatorlarga
        /// qog'ozdagi qator uzunliklari bo'yicha bo'linadi (oxirgisi takrorlanadi).
        /// </remarks>
        private (string Label, bool Chosen)[][] Options(
            string listId,
            (string Value, string Label)[][] layout,
            Func<string, bool> chosen,
            Func<string, string, string>? decorate = null)
        {
            string Decorate(string value, string label) => decorate is null ? label : decorate(value, label);

            if (!_t.HasList(listId))
            {
                return [.. layout.Select(line => line.Select(option => (Decorate(option.Value, option.Label), chosen(option.Value))).ToArray())];
            }

            var paper = layout.SelectMany(line => line).ToDictionary(option => option.Value, option => option.Label, StringComparer.Ordinal);
            var options = _t.Options(listId)
                .Where(option => !option.Hidden || chosen(option.Value))
                .Select(option => (
                    Decorate(
                        option.Value,
                        FormTexts.ChangedLatin(listId, option) ?? paper.GetValueOrDefault(option.Value) ?? option.Label[FormTexts.Latin]),
                    chosen(option.Value)))
                .ToList();

            var lines = new List<(string Label, bool Chosen)[]>();
            var index = 0;
            while (index < options.Count)
            {
                var size = layout[Math.Min(lines.Count, layout.Length - 1)].Length;
                lines.Add([.. options.Skip(index).Take(size)]);
                index += size;
            }

            return [.. lines];
        }

        /// <summary>Til jadvalining qat'iy qatorlari (o'zbek, rus, ingliz, xitoy) — HR tartibi va nomi bilan.</summary>
        private IEnumerable<(FormLanguage Language, string Label)> FixedLanguageRows()
        {
            if (!_t.HasList(HrF02TextCatalog.LanguageList))
            {
                return QuestionnaireFormCatalog.FixedLanguages.Select(language => (language, QuestionnaireFormCatalog.LanguageLabel(language)));
            }

            var ordered = _t.Options(HrF02TextCatalog.LanguageList)
                .Select(option => (Parsed: Enum.TryParse<FormLanguage>(option.Value, out var language) ? language : (FormLanguage?)null, option))
                .Where(row => row.Parsed is { } language && QuestionnaireFormCatalog.FixedLanguages.Contains(language))
                .Select(row => (
                    row.Parsed!.Value,
                    FormTexts.ChangedLatin(HrF02TextCatalog.LanguageList, row.option) ?? QuestionnaireFormCatalog.LanguageLabel(row.Parsed!.Value)))
                .ToList();

            // Yopiq ro'yxat — har til bor (Shape). Shunga qaramay biror til tushib qolsa — asl nomi bilan oxirida.
            ordered.AddRange(QuestionnaireFormCatalog.FixedLanguages
                .Where(language => ordered.All(row => row.Item1 != language))
                .Select(language => (language, QuestionnaireFormCatalog.LanguageLabel(language))));

            return ordered;
        }

        /// <summary>Qarindoshlik nomi: HR o'zgartirgani ?? qog'ozdagi.</summary>
        private string RelationName(FormFamilyRelation relation)
            => (_t.HasList(HrF02TextCatalog.RelationList)
                    && _t.Options(HrF02TextCatalog.RelationList).FirstOrDefault(option => option.Value == relation.ToString()) is { } option
                    ? FormTexts.ChangedLatin(HrF02TextCatalog.RelationList, option)
                    : null)
                ?? QuestionnaireFormCatalog.RelationLabel(relation);

        /// <summary>Kompyuter jadvalining uch ustuni — <see cref="FormTexts.ComputerGroups"/>.</summary>
        private IReadOnlyList<ComputerSkillGroup> ComputerColumns(IReadOnlyDictionary<string, int> skills)
            => _t.ComputerGroups(skills);

        private static void TestRow(Table table, int number, string name, Action<Cell> result)
        {
            var row = Row(table);
            Number(row.Cells[0], number);
            Value(row.Cells[1], name);
            result(row.Cells[2]);
        }

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

        private static void Header(Table table, params string[] titles)
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

        private static void Text(Cell cell, string? text) => Value(cell, text);

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

        private static void Pair(Table table, string leftLabel, string? leftValue, string rightLabel, string? rightValue)
        {
            var row = Row(table);
            Label(row.Cells[0], leftLabel);
            Value(row.Cells[1], leftValue);
            Label(row.Cells[2], rightLabel);
            Value(row.Cells[3], rightValue);
        }

        private static void Wide(Table table, string label, string? value)
        {
            var row = Row(table);
            Label(row.Cells[0], label);
            Value(row.Cells[1], value);
            row.Cells[1].MergeRight = 2;
        }

        private static void Question(Table table, int number, string question, Action<Cell> answer)
        {
            var row = Row(table, 0.85);
            Number(row.Cells[0], number);
            Value(row.Cells[1], question);
            answer(row.Cells[2]);
        }

        /// <summary>Variantlar: har ichki ro'yxat — bitta qator, tanlangan ☑.</summary>
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
                        // Bir nechta bo'shliq matnda bittaga yig'iladi — ochiq probel belgilari
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

        /// <summary>Bir qatordagi variantlar matnining taxminiy chegarasi (belgi) — javob katagi ~8 sm.</summary>
        private const int ChoiceLineBudget = 44;

        /// <summary>
        /// «Qayerdan bildingiz?» variantlari — «Qayerdan bilgan» ma'lumotnomasidan
        /// (SPEC-qayerdan-bilgan §5): hammasi ☐/☑, tanlanganidan keyin «, kim/qaysi: …».
        /// </summary>
        /// <remarks>
        /// Ro'yxat qog'ozdagidek qat'iy emas (HR kanal qo'shadi), shu sababli qatorlarga
        /// uzunlik bo'yicha teriladi: qisqa nomlar yonma-yon, uzunlari yangi qatorda.
        /// </remarks>
        private (string Label, bool Chosen)[][] ReferralLines(
            IReadOnlyList<QuestionnairePdfChoice> choices,
            string? detail)
        {
            var lines = new List<(string Label, bool Chosen)[]>();
            var line = new List<(string Label, bool Chosen)>();
            var used = 0;

            foreach (var choice in choices)
            {
                var label = choice.Chosen && !string.IsNullOrWhiteSpace(detail)
                    ? _t.Pdf("pdf.conditions.referralDetail", ("label", choice.Label), ("detail", detail.Trim()))
                    : choice.Label;

                // Belgi, bo'shliq va oraliq — har variantga ~4 belgi
                var width = label.Length + 4;

                if (line.Count > 0 && used + width > ChoiceLineBudget)
                {
                    lines.Add([.. line]);
                    line.Clear();
                    used = 0;
                }

                line.Add((label, choice.Chosen));
                used += width;
            }

            if (line.Count > 0)
            {
                lines.Add([.. line]);
            }

            return [.. lines];
        }

        private void YesNo(Cell cell, bool? value)
            => Choices(cell, [[(T("pdf.common.yes"), value == true), (T("pdf.common.no"), value == false)]]);

        private void YesNoDetail(Cell cell, bool? value, string? detail, string yes)
            => Choices(cell, [[(T("pdf.common.no"), value == false), ($"{yes}: {(value == true && detail is not null ? detail : Blank)}", value == true)]]);

        private static void Box(Paragraph paragraph, bool chosen)
        {
            var box = paragraph.AddFormattedText(chosen ? Checked : Unchecked);
            box.Font.Name = BundledFontResolver.Symbols;
            box.Font.Bold = false;

            // Tanlangani yashil ☑ (egasi, 2026-10-29: ☑ «g'alati ko'rinadi», ✅ ga o'xshasin)
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

        private void Bar(string part, string text)
        {
            Spacer(8);

            var table = _section.AddTable();
            table.Borders.Visible = false;
            table.LeftPadding = Unit.FromCentimeter(0.15);
            table.AddColumn(Unit.FromCentimeter(18));

            var row = table.AddRow();
            row.Height = Unit.FromCentimeter(0.74);
            row.HeightRule = RowHeightRule.AtLeast;
            row.VerticalAlignment = VerticalAlignment.Center;
            row.Cells[0].Shading.Color = Navy;

            var title = row.Cells[0].AddParagraph();
            title.Format.Font.Bold = true;
            title.Format.Font.Color = Colors.White;
            title.Format.Font.Size = 10.5;
            title.AddText(part);
            title.AddSpace(3);
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

        // ════════════════════════════════════════════════ formatlash

        private static string Date(DateOnly date) => date.ToString("dd.MM.yyyy", Invariant);

        private static string? Years(int? from, int? to) => (from, to) switch
        {
            (null, null) => null,
            (null, { } end) => end.ToString(Invariant),
            ({ } start, null) => $"{start.ToString(Invariant)} –",
            ({ } start, { } end) => $"{start.ToString(Invariant)} – {end.ToString(Invariant)}",
        };

        private string Period(FormJob job)
        {
            var from = job.From is null ? "____" : QuestionnaireFormMapping.Month(job.From);
            var to = job.Current ? T("pdf.work.present") : job.To is null ? "____" : QuestionnaireFormMapping.Month(job.To);

            return $"{from} – {to}";
        }

        /// <summary>Kanonik O'zbekiston raqami — ekrandagidek; boshqasi o'zicha.</summary>
        private static string? Phone(string? value)
            => value is { Length: 13 } && value.StartsWith("+998", StringComparison.Ordinal)
                ? PhoneNumber.FromTrusted(value).Formatted
                : value;
    }
}
