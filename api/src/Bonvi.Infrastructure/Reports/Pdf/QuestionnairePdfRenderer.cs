using System.Globalization;
using Bonvi.Application.Recruiting.Forms;
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
    private static readonly SemaphoreSlim RenderGate = new(2, 2);

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

    private static string Base64(byte[] bytes) => "base64:" + Convert.ToBase64String(bytes);

    /// <summary>Kvadrat suratni markazdan 3:4 (eni:bo'yi) ga qirqadi.</summary>
    private static byte[] PortraitJpeg(byte[] source)
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
    private static byte[] FlatPng(byte[] source)
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
        private readonly Document _document = new();
        private Section _section = null!;

        public Document Build()
        {
            Styles();
            Page();

            TitleBlock();
            InfoTable();
            Instruction();

            Bar("A QISM", "NOMZOD TOMONIDAN TO‘LDIRILADI");
            Hint("Barcha savollarga o‘zingiz javob bering.");

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
            headerRow.Cells[0].AddParagraph("MBF Company — HR hujjatlar paketi");
            var right = headerRow.Cells[1].AddParagraph();
            right.Format.Alignment = ParagraphAlignment.Right;
            right.AddText("HR-F02");
            right.AddSpace(2);
            right.AddText("·");
            right.AddSpace(2);
            right.AddText("Nomzod anketasi");
            right.AddSpace(2);
            right.AddText("·");
            right.AddSpace(2);
            right.AddText("v" + model.FormVersion);

            var footer = _section.Footers.Primary.AddParagraph();
            footer.Format.Alignment = ParagraphAlignment.Center;
            footer.Format.Font.Size = 8;
            footer.Format.Font.Color = Muted;
            footer.AddText("Sahifa ");
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
            var titleText = title.AddFormattedText("NOMZOD ANKETASI", TextFormat.Bold);
            titleText.Font.Size = 17;
            titleText.Font.Color = Navy;

            Centered(row.Cells[1], "Ishga qabul qilish uchun", 9, Muted).Format.SpaceBefore = 3;
            var form = Centered(row.Cells[1], "Blanka HR-F02", 8, Muted);
            form.Format.SpaceBefore = 2;
            form.AddSpace(2);
            form.AddText("·");
            form.AddSpace(2);
            form.AddText("Versiya " + model.FormVersion);

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
                label.AddFormattedText("Rasm", TextFormat.Bold).Font.Color = Muted;
                Centered(photo, "3 × 4 sm", 8, Muted);
            }

            Spacer(6);
        }

        private void InfoTable()
        {
            var table = Grid(2.29, 3.17, 1.59, 5.11, 1.94, 3.89);

            var first = Row(table, 0.74);
            Label(first.Cells[0], "Anketa №");
            Value(first.Cells[1], model.Number.ToString(Invariant)).Format.Font.Bold = true;
            Label(first.Cells[2], "Sana");
            Value(first.Cells[3], Date(DateOnly.FromDateTime(model.SubmittedAt.DateTime)));
            Label(first.Cells[4], "So‘rov №");
            Value(first.Cells[5], "(HR-F01)").Format.Font.Color = Muted;

            var second = Row(table, 0.74);
            Label(second.Cells[0], "Lavozim");
            Value(second.Cells[1], model.VacancyTitle);
            second.Cells[1].MergeRight = 2;
            Label(second.Cells[4], "Bo‘lim");
            Value(second.Cells[5], null);
        }

        private void Instruction()
        {
            var text = _section.AddParagraph();
            text.Format.SpaceBefore = 5;
            text.Format.Font.Size = 8;
            text.Format.Font.Italic = true;
            text.Format.Font.Color = Muted;
            text.AddText("Anketani aniq va to‘liq to‘ldiring. Javob variantlaridan keraklisini belgilang (");
            Box(text, true);
            text.AddText("). Sizga tegishli bo‘lmagan savolga «yo‘q» yoki «—» deb yozing. A qismni nomzod, B qismni HR bo‘limi to‘ldiradi.");
        }

        // ════════════════════════════════════════════════ 1. shaxsiy

        private void Personal()
        {
            Title("1. SHAXSIY MA’LUMOTLAR");

            var table = Grid(3.79, 5.21, 3.79, 5.21);

            Pair(table, "Familiyasi", _a.LastName, "Ismi", _a.FirstName);
            Pair(table, "Otasining ismi", _a.MiddleName, "Tug‘ilgan sanasi", _a.BirthDate is { } birth ? Date(birth) : null);

            // Blankadagi «Jinsi / JSHSHIR» va «Pasport / Kim tomonidan va qachon berilgan» qatorlari
            // ATAYLAB yo'q (egasi, 2026-10-26): pasport kengash suhbatidan keyin olinadi. B qismdagi
            // «Pasport nusxasi» belgisi esa HR'ning o'z ro'yxati — u qoladi.
            Wide(table, "Doimiy ro‘yxatdagi manzil (propiska)", _a.RegisteredAddress);
            Wide(table, "Haqiqiy yashash manzili", _a.ActualAddress);

            var housing = Row(table);
            Label(housing.Cells[0], "Uy turi");
            Choices(housing.Cells[1], [[("Ko‘p qavatli uy", _a.HousingType == FormHousingType.Apartment), ("Hovli", _a.HousingType == FormHousingType.House)]]);
            Label(housing.Cells[2], "Mahalla (MFY) nomi");
            Value(housing.Cells[3], _a.Mahalla);

            Pair(table, "Telefon raqami", Phone(_a.Phone) ?? "+998", "Qo‘shimcha telefon (yaqiningiz)", Phone(_a.PhoneExtra) ?? "+998");
            Pair(table, "Telegram", "@" + _a.Telegram, "E-mail", _a.Email);

            var marital = Row(table);
            Label(marital.Cells[0], "Oilaviy holati");
            Choices(marital.Cells[1],
            [
                [("Oilali", _a.MaritalStatus == MaritalStatus.Married), ("Bo‘ydoq / turmush qurmagan", _a.MaritalStatus == MaritalStatus.Unmarried)],
                [("Ajrashgan", _a.MaritalStatus == MaritalStatus.Divorced), ("Beva", _a.MaritalStatus == MaritalStatus.Widowed)],
            ]);
            Label(marital.Cells[2], "Farzandlar soni");
            Value(marital.Cells[3], _a.ChildrenCount?.ToString(Invariant));

            var military = Row(table);
            Label(military.Cells[0], "Harbiy xizmat (erkaklar uchun)");
            var served = _a.MilitaryService == FormMilitaryService.Served;
            var months = served && _a.MilitaryServiceMonths is { } m ? m.ToString(Invariant) : "____";
            Choices(military.Cells[1],
            [[
                ($"O‘tagan, muddati: {months} oy", served),
                ("Zaxirada", _a.MilitaryService == FormMilitaryService.Reserve),
                ("Kechiktirilgan", _a.MilitaryService == FormMilitaryService.Deferred),
                ("O‘tamagan", _a.MilitaryService == FormMilitaryService.NotServed),
            ]]);
            military.Cells[1].MergeRight = 2;

            var license = Row(table);
            Label(license.Cells[0], "Haydovchilik guvohnomasi");
            var categories = _a.DriverLicenseCategories is { Count: > 0 } chosen ? string.Join(", ", chosen) : "________";
            Choices(license.Cells[1], [[("Yo‘q", _a.HasDriverLicense == false), ($"Bor, toifasi: {categories}", _a.HasDriverLicense == true)]]);
            Label(license.Cells[2], "Shaxsiy avtomobil");
            var car = _a.HasCar == true && _a.CarModel is { } carModel ? carModel : "_____";
            Choices(license.Cells[3], [[("Yo‘q", _a.HasCar == false), ($"Bor, rusumi: {car}", _a.HasCar == true)]]);
        }

        // ════════════════════════════════════════════════ 2. ma'lumoti

        private void Education()
        {
            Title("2. MA’LUMOTI");

            var level = Grid(3.79, 5.21, 3.79, 5.21);
            var row = Row(level);
            Label(row.Cells[0], "Ma’lumot darajasi");
            var chosen = _a.EducationLevel;
            Choices(row.Cells[1],
            [
                [
                    ("O‘rta (maktab)", chosen == EducationLevel.SecondarySchool),
                    ("O‘rta maxsus (kollej, texnikum, litsey)", chosen == EducationLevel.VocationalSchool),
                    ("Oliy (tugallanmagan)", chosen == EducationLevel.HigherIncomplete),
                ],
                [
                    ("Oliy (bakalavr)", chosen == EducationLevel.HigherComplete),
                    ("Oliy (magistr)", chosen == EducationLevel.HigherMaster),
                ],
            ]);
            row.Cells[1].MergeRight = 2;

            Spacer(5);

            var schools = Grid(0.79, 5.82, 5.03, 2.91, 3.45);
            Header(schools, "№", "O‘quv yurti nomi", "Fakultet / mutaxassislik", "O‘qigan yillari (dan – gacha)", "Diplom / sertifikat №");

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
            Pair(extra, "Tamomlagan maktabingiz (№, tuman)", _a.SchoolName, "Qaysi fanlardan yaxshi o‘qigansiz", _a.StrongSubjects);
            Wide(extra, "Kurslar, sertifikatlar, malaka razryadi (payvandchi, stanokchi, elektrik va h.k.)", _a.Courses);
        }

        // ════════════════════════════════════════════════ 3. tillar

        private void Languages()
        {
            Title("3. TIL BILISH DARAJASI");
            Hint("Darajani raqam bilan yozing:  0 – bilmayman,  1 – boshlang‘ich,  2 – o‘rta,  3 – o‘rtadan yuqori,  4 – yuqori (erkin),  5 – ona tili.");

            var table = Grid(0.79, 5.57, 3.88, 3.88, 3.88);
            Header(table, "№", "Til", "So‘zlashuv", "O‘qish", "Yozish");

            var rows = _a.Languages ?? [];
            var lines = new List<(string Name, FormLanguageSkill? Skill)>();

            foreach (var language in QuestionnaireFormCatalog.FixedLanguages)
            {
                lines.Add((QuestionnaireFormCatalog.LanguageLabel(language), rows.FirstOrDefault(row => row.Language == language)));
            }

            var others = rows.Where(row => row.Language == FormLanguage.Other).ToList();
            foreach (var other in others)
            {
                lines.Add(($"Boshqa: {other.OtherName}", other));
            }

            if (others.Count == 0)
            {
                lines.Add(("Boshqa: ______________", null));
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
            Title("4. KOMPYUTER SAVODXONLIGI");
            Hint("Darajani raqam bilan yozing:  1 – boshlang‘ich,  2 – o‘rta,  3 – yaxshi,  4 – mukammal.  Bilmagan dasturingizni bo‘sh qoldiring.");

            var table = Grid(4.94, 1.06, 4.94, 1.06, 4.94, 1.06);
            var groups = QuestionnaireFormCatalog.ComputerGroups;

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

            var skills = _a.ComputerSkills ?? new Dictionary<string, int>();
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
            other.AddText("Boshqa dasturlar va dasturlash tillari: ");

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
            Title("5. MEHNAT FAOLIYATI");

            var experience = Grid(3.79, 5.21, 3.79, 5.21);
            var question = Row(experience);
            Label(question.Cells[0], "Avval ishlaganmisiz?");
            YesNo(question.Cells[1], _a.HasWorkExperience);
            question.Cells[2].Borders.Visible = false;
            question.Cells[3].Borders.Visible = false;

            Spacer(5);
            Hint("Oxirgi ish joyingizdan boshlab yozing.");

            var table = Grid(0.79, 4.76, 3.35, 3.0, 6.1);
            Header(table, "№", "Tashkilot nomi", "Lavozimi", "Davri (oy.yil – oy.yil)", "Ishdan ketish sababi");

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
            Title("6. TAVSIYA BERUVCHILAR (AVVALGI ISH JOYIDAN SURISHTIRUV UCHUN)");
            Hint("Sizni avvalgi ish joyingizdan yaxshi taniydigan rahbar yoki hamkasbingiz. HR bo‘limi ular bilan bog‘lanishi mumkin.");

            var table = Grid(0.79, 5.64, 6.1, 5.47);
            Header(table, "№", "F.I.Sh.", "Tashkilot, lavozimi", "Telefon raqami");

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
            Title("7. OILA A’ZOLARI HAQIDA MA’LUMOT");
            Hint("Ota-onangiz, turmush o‘rtog‘ingiz, aka-uka va opa-singillaringiz.");

            var table = Grid(0.71, 2.56, 3.53, 1.94, 3.62, 2.38, 3.27);
            Header(table, "№", "Qarindoshligi", "F.I.Sh.", "Tug‘ilgan yili", "Ish joyi, lavozimi", "Telefon raqami", "Yashash manzili");

            var family = _a.FamilyMembers ?? [];
            for (var index = 0; index < Math.Max(5, family.Count); index++)
            {
                var member = index < family.Count ? family[index] : null;
                var row = Row(table, 0.85);
                Number(row.Cells[0], index + 1);
                Value(row.Cells[1], member?.Relation is { } relation ? QuestionnaireFormCatalog.RelationLabel(relation) : null);
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
            Title("8. ISH SHAROITLARI VA KUTILMALAR");

            var table = Grid(0.79, 9.17, 8.04);
            Header(table, "№", "Savol", "Javob");

            Question(table, 1, "Bu vakansiya haqida qayerdan bildingiz?", cell =>
                Choices(cell, ReferralLines(model.ReferralChoices, _a.ReferralDetail)));

            Question(table, 2, "Korxonamizda tanishlaringiz yoki qarindoshlaringiz ishlaydimi? (F.I.Sh., bo‘limi)", cell =>
                YesNoDetail(cell, _a.HasAcquaintances, _a.AcquaintancesDetail, "Ha"));

            Question(table, 3, "Oxirgi ish joyingizda oylik maoshingiz qancha bo‘lgan?", cell =>
                Text(cell, _a.LastSalary is { } last ? QuestionnaireFormMapping.Money(last) : "______________ so‘m"));

            Question(table, 4, "Qancha oylik maoshga ishlamoqchisiz?", cell =>
                Text(cell, _a.ExpectedSalary is { } expected ? QuestionnaireFormMapping.Money(expected) : "______________ so‘m"));

            Question(table, 5, "Ishga qachondan chiqa olasiz?", cell => StartAfter(cell));

            Question(table, 6, "Smenali yoki 12 soatlik ish rejimida ishlay olasizmi?", cell => YesNo(cell, _a.CanWorkShifts));
            Question(table, 7, "Zarurat bo‘lsa, ish vaqtidan keyin qolib ishlay olasizmi?", cell => YesNo(cell, _a.CanWorkOvertime));
            Question(table, 8, "Xizmat safarlariga (komandirovka) chiqishga rozimisiz?", cell => YesNo(cell, _a.CanTravel));

            Question(table, 9, "Boshqa joyda yarim stavka ishingiz yoki shaxsiy biznesingiz bormi?", cell =>
                YesNoDetail(cell, _a.HasOtherJob, _a.OtherJobDetail, "Ha"));

            Question(table, 10, "Hozirda kredit to‘lovlaringiz bormi?", cell =>
            {
                var payment = _a.HasLoan == true && _a.LoanMonthlyPayment is { } amount
                    ? QuestionnaireFormMapping.Money(amount)
                    : "________ so‘m";

                Choices(cell, [[("Yo‘q", _a.HasLoan == false), ($"Ha, oylik to‘lov: {payment}", _a.HasLoan == true)]]);
            });

            Question(table, 11, "1 haftalik amaliy sinov (stajirovka) davrini o‘tashga rozimisiz?", cell => YesNo(cell, _a.AgreesToTrial));
            Question(table, 12, "Zarurat bo‘lsa, ota-onangizni korxonaga taklif qilishimizga rozimisiz?", cell => YesNo(cell, _a.AgreesParentsInvite));
            Question(table, 13, "Ishni boshlash uchun qanday shartlaringiz bor?", cell => Text(cell, _a.StartConditions));
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

            Choices(cell,
            [
                [("Qabul qilingan kundan", _a.StartAfter == FormStartAfter.OnHireDay)],
                [("Qabul qilingandan 1 hafta keyin", _a.StartAfter == FormStartAfter.OneWeek)],
                [("Qabul qilingandan 10 kun keyin", _a.StartAfter == FormStartAfter.TenDays)],
                [("Qabul qilingandan 15 kun keyin", _a.StartAfter == FormStartAfter.FifteenDays)],
            ]);
        }

        // ════════════════════════════════════════════════ 9. sog'liq

        private void Health()
        {
            Title("9. SOG‘LIQ VA QO‘SHIMCHA MA’LUMOTLAR");

            var table = Grid(0.79, 9.17, 8.04);
            Header(table, "№", "Savol", "Javob");

            Question(table, 1, "Ushbu lavozimda ishlashingizga sog‘lig‘ingiz bo‘yicha cheklov bormi? (jumladan, xavfli ishlab chiqarish: gaz ballonlari bilan ishlash)", cell =>
            {
                YesNoDetail(cell, _a.HasHealthRestriction, _a.HealthRestrictionDetail, "Bor");
                var hint = cell.AddParagraph("Yakuniy xulosa tibbiy ko‘rikda beriladi.");
                hint.Format.Font.Size = 7.5;
                hint.Format.Font.Italic = true;
                hint.Format.Font.Color = Muted;
            });

            Question(table, 2, "Oilangizda doimiy parvarishga muhtoj bemor bormi?", cell =>
                Choices(cell, [[("Yo‘q", _a.HasFamilyPatient == false), ("Bor", _a.HasFamilyPatient == true)]]));

            Question(table, 3, "Sudlanganmisiz?", cell => YesNoDetail(cell, _a.HasConviction, _a.ConvictionDetail, "Ha"));

            Question(table, 4, "Yaqin qarindoshlaringiz orasida sudlanganlar bormi?", cell =>
                YesNoDetail(cell, _a.RelativesConviction, _a.RelativesConvictionDetail, "Ha"));

            Question(table, 5, "Chet elda bo‘lganmisiz? (qachon, qaysi davlatda, qanday maqsadda)", cell => Text(cell, _a.AbroadTrips));
        }

        // ════════════════════════════════════════════════ 10. o'zingiz haqingizda

        private void AboutYou()
        {
            Title("10. O‘ZINGIZ HAQINGIZDA");
            Hint("Qisqa va samimiy javob bering. To‘g‘ri yoki noto‘g‘ri javob yo‘q.");

            var table = Grid(0.79, 7.58, 9.62);
            Header(table, "№", "Savol", "Javob");

            (string Question, string? Answer)[] questions =
            [
                ("Nima uchun ishlaysiz? Sizni ishlashga nima undaydi?", _a.Motivation),
                ("Nima uchun aynan bizning korxonani tanladingiz?", _a.WhyUs),
                ("Eng kuchli tomonlaringiz nimada?", _a.Strengths),
                ("Zaif tomonlaringiz nimada deb o‘ylaysiz?", _a.Weaknesses),
                ("Sizni ko‘proq nima rag‘batlantiradi?", _a.Motivators),
                ("Muvaffaqiyat deganda nimani tushunasiz?", _a.SuccessDefinition),
                ("Oxirgi ish joyingizdagi eng katta muvaffaqiyatingiz nima bo‘lgan? (avval ishlaganlar uchun)", _a.LastJobAchievement),
                ("Avvalgi ish joyingizga qanday foydali g‘oya bergansiz va u qanday natija bergan? (avval ishlaganlar uchun)", _a.BestIdea),
                ("Ishda duch kelgan eng katta qiyinchiligingiz nima bo‘lgan va uni qanday hal qilgansiz?", _a.BiggestChallenge),
                ("Ishdagi stressga qanday munosabatdasiz, u bilan qanday kurashasiz?", _a.StressHandling),
                ("Ish joyingizda sizga nisbatan nohaqlik qilinsa, qanday yo‘l tutasiz?", _a.UnfairnessToYou),
                ("Korxonaga nisbatan nohaqlik qilinayotganiga guvoh bo‘lsangiz, nima qilasiz?", _a.UnfairnessToCompany),
                ("Katta jamoada ishlay olasizmi? Jamoaviy ish deganda nimani tushunasiz?", _a.Teamwork),
                ("Avvalgi ish joyingizdagi jamoa va rahbariyat haqida fikringiz.", _a.PreviousTeamOpinion),
                ("O‘qish davrida biror joyda shogird bo‘lganmisiz yoki ishlaganmisiz?", _a.StudentWorkExperience),
                ("5 yildan keyin o‘zingizni qayerda ko‘rasiz?", _a.FiveYearPlan),
                ("Qo‘shimcha ma’lumot: o‘zingiz haqingizda yana nimani bilishimizni istaysiz?", _a.ExtraNote),
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
            Title("11. TILXAT (ROZILIK VA TASDIQ)");

            var fullName = string.Join(' ', new[] { _a.LastName, _a.FirstName, _a.MiddleName }.Where(part => !string.IsNullOrEmpty(part)));

            // Tilxat bo'linmasin: sarlavha, matn va imzo bitta sahifada (qog'ozdagidek)
            var intro = _section.AddParagraph();
            intro.Format.KeepWithNext = true;
            intro.AddText("Men, ");
            var name = intro.AddFormattedText(fullName, TextFormat.Bold | TextFormat.Underline);
            name.Font.Color = Ink;
            intro.AddText(" (F.I.Sh.):");

            string[] items =
            [
                "Ushbu anketada keltirgan barcha ma’lumotlarim to‘liq va to‘g‘ri ekanini tasdiqlayman. Ma’lumotlar noto‘g‘ri ekani aniqlansa, bu mehnat shartnomasini bekor qilish uchun asos bo‘lishini bilaman.",
                "Shaxsga doir ma’lumotlarim ishga qabul qilish va mehnat munosabatlari maqsadida O‘zbekiston Respublikasining «Shaxsga doir ma’lumotlar to‘g‘risida»gi Qonuniga muvofiq ishlanishiga roziman.",
                "Avvalgi ish joylarim va men ko‘rsatgan tavsiya beruvchilar orqali surishtiruv o‘tkazilishiga roziman.",
                "Ishga qabul qilinsam, anketadagi ma’lumotlarim o‘zgarganda bu haqda HR bo‘limini o‘z vaqtida xabardor qilaman.",
                "Ishdan bo‘shaganimdan keyin 2 yil davomida korxona sirlarini oshkor qilmaslikka majburiyat olaman. Bu majburiyat buzilgan taqdirda menga nisbatan qonuniy choralar ko‘rilishiga roziman.",
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
            date.AddText("Sana: ");
            date.AddFormattedText(Date(DateOnly.FromDateTime(model.SubmittedAt.DateTime)), TextFormat.Underline);

            var sign = row.Cells[1].AddParagraph();
            sign.AddText("Imzo: ");

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
            origin.AddText(
                $"Onlayn to‘ldirildi: {model.SubmittedAt.ToString("dd.MM.yyyy HH:mm", Invariant)}  ·  Til: {model.Language}");
        }

        // ════════════════════════════════════════════════ B qism

        private void HrPart()
        {
            Bar("B QISM", "HR BO‘LIMI TOMONIDAN TO‘LDIRILADI");
            Hint("Nomzod bu qismni to‘ldirmaydi.");

            Title("12. HUJJATLAR NAZORATI");
            var documents = Grid(0.79, 8.82, 2.82, 5.57);
            Header(documents, "№", "Hujjat", "Topshirildi", "Izoh");

            (string Name, bool Done, string? Note)[] papers =
            [
                ("Rasm (3 × 4)", true, "Onlayn olingan"),
                ("Pasport / ID-karta nusxasi", false, null),
                ("Diplom / sertifikat nusxasi", false, null),
                ("Haydovchilik guvohnomasi nusxasi (bo‘lsa)", false, null),
                ("Sudlanmaganlik haqida ma’lumotnoma", false, null),
                ("Harbiy hisob hujjati (erkaklar)", false, null),
                ("Imzolangan anketa (A qism)", true, "Onlayn imzolangan"),
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

            Title("13. TEST VA SINOVLAR");
            var tests = Grid(0.79, 6.35, 4.59, 6.27);
            Header(tests, "№", "Sinov", "Natija", "Izoh");

            TestRow(tests, 1, "Professional test", cell => Text(cell, "________ ball"));
            TestRow(tests, 2, "DISC test", cell => Choices(cell, [[("D", false), ("I", false), ("S", false), ("C", false)]]));
            TestRow(tests, 3, "Unumdorlik (produktivlik) testi", cell => Text(cell, "________ ball"));
            TestRow(tests, 4, "Rus tili (og‘zaki tekshiruv)", cell => Text(cell, "Daraja: ____ (0–5)"));
            TestRow(tests, 5, "Excel (amaliy tekshiruv)", cell => Text(cell, "Daraja: ____ (1–4)"));
            TestRow(tests, 6, "Amaliy sinov (usta bahosi, sana)", _ => { });

            Title("14. DASTLABKI SUHBAT XULOSASI");
            var interview = Grid(0.79, 6.61, 2.47, 8.12);
            Header(interview, "№", "Mezon", "Baho (1–5)", "Izoh");

            string[] criteria =
            [
                "Talablarga mosligi (HR-F01, 2-bo‘lim)",
                "Tajriba va malaka",
                "Muloqot va motivatsiya",
                "Amaliy sinov natijasi",
            ];

            for (var index = 0; index < criteria.Length; index++)
            {
                var row = Row(interview);
                Number(row.Cells[0], index + 1);
                Value(row.Cells[1], criteria[index]);
            }

            var decision = Row(interview, 1.0);
            Label(decision.Cells[0], "Qaror");
            decision.Cells[0].MergeRight = 1;
            Choices(decision.Cells[2], [[("Kengashga (HR-F04)", false), ("Zaxira bazaga", false), ("Rad etildi", false)]]);
            decision.Cells[2].AddParagraph("Rad etish sababi: ______________________________");
            decision.Cells[2].MergeRight = 1;

            Spacer(24);

            var signatures = _section.AddTable();
            signatures.Borders.Visible = false;
            signatures.AddColumn(Unit.FromCentimeter(7.41));
            signatures.AddColumn(Unit.FromCentimeter(4.94));
            signatures.AddColumn(Unit.FromCentimeter(5.65));

            var line = signatures.AddRow();
            line.Cells[0].AddParagraph("HR mutaxassisi: ______________________");
            line.Cells[1].AddParagraph("Imzo: ______________");
            line.Cells[2].AddParagraph("Sana: ______________");

            var storage = _section.AddParagraph();
            storage.Format.SpaceBefore = 8;
            storage.Format.Font.Size = 7.5;
            storage.Format.Font.Italic = true;
            storage.Format.Font.Color = Muted;
            storage.AddText(
                "Saqlash: ishga qabul qilinsa, xodimning shaxsiy ish yig‘majildida; qabul qilinmasa, 6 oy. " +
                "Reglament: HR-R02 «Ishga qabul». Keyingi blankalar: HR-F03 «Surishtiruv», HR-F04 «Kengash».");
        }

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
        private static (string Label, bool Chosen)[][] ReferralLines(
            IReadOnlyList<QuestionnairePdfChoice> choices,
            string? detail)
        {
            var lines = new List<(string Label, bool Chosen)[]>();
            var line = new List<(string Label, bool Chosen)>();
            var used = 0;

            foreach (var choice in choices)
            {
                var label = choice.Chosen && !string.IsNullOrWhiteSpace(detail)
                    ? $"{choice.Label}, kim/qaysi: {detail.Trim()}"
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

        private static void YesNo(Cell cell, bool? value)
            => Choices(cell, [[("Ha", value == true), ("Yo‘q", value == false)]]);

        private static void YesNoDetail(Cell cell, bool? value, string? detail, string yes)
            => Choices(cell, [[("Yo‘q", value == false), ($"{yes}: {(value == true && detail is not null ? detail : Blank)}", value == true)]]);

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

        private static string Period(FormJob job)
        {
            var from = job.From is null ? "____" : QuestionnaireFormMapping.Month(job.From);
            var to = job.Current ? "hozir" : job.To is null ? "____" : QuestionnaireFormMapping.Month(job.To);

            return $"{from} – {to}";
        }

        /// <summary>Kanonik O'zbekiston raqami — ekrandagidek; boshqasi o'zicha.</summary>
        private static string? Phone(string? value)
            => value is { Length: 13 } && value.StartsWith("+998", StringComparison.Ordinal)
                ? PhoneNumber.FromTrusted(value).Formatted
                : value;
    }
}
