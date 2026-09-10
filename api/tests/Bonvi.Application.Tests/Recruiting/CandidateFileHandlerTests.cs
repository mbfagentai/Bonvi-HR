using System.Text;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Common.Documents;
using Bonvi.Application.Recruiting.Candidates;
using Bonvi.Domain.Recruiting;
using NSubstitute;

namespace Bonvi.Application.Tests.Recruiting;

/// <summary>
/// Nomzod fayllari — ulanish qatlami: yuklash, yuklab olish, o'chirish.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega bu testlar integratsiya testidan tashqarida ham kerak.</b> Uchta
/// xatti-harakat HTTP orqali umuman KO'RINMAYDI: (1) rad etilgan yuklashdan
/// keyin diskda yetim fayl qolmaganini faqat saqlash portiga qilingan
/// chaqiruv ko'rsatadi, (2) 60 MB lik umumiy hajm chegarasini sim ustida
/// tekshirish uchtadan ortiq 20 MB lik so'rov talab qilardi, (3) «avval
/// saqlash, keyin diskdan o'chirish» tartibi javobda hech qanday iz
/// qoldirmaydi — u faqat saqlash yiqilganda ma'lum bo'ladi va o'shanda
/// kech bo'ladi.
/// </para>
/// <para>
/// Quvurning O'ZI (kichraytirish, tur aniqlash, baytma-bayt PDF)
/// <c>ScaledDocumentStoreTests</c> da haqiqiy Skia bilan tekshirilgan —
/// bu yerda u faqat sharoit.
/// </para>
/// </remarks>
public sealed class CandidateFileHandlerTests
{
    private readonly RecruitingTestContext _context = new();

    private UploadCandidateFileCommandHandler Yuklash => new(
        _context.Candidates,
        _context.Files,
        _context.Employees.CurrentUser,
        _context.UnitOfWork);

    private GetCandidateFileQueryHandler Olish => new(_context.Candidates, _context.Files);

    private DeleteCandidateFileCommandHandler Ochirish => new(
        _context.Candidates, _context.Files, _context.UnitOfWork);

    /// <summary>Haqiqiy PDF imzosi bilan boshlanadigan minimal fayl.</summary>
    /// <remarks>
    /// PDF yo'li ataylab tanlangan: u baytma-bayt saqlanadi, ya'ni diskka
    /// tushgan hajm oqimning haqiqiy uzunligiga TENG bo'lishi kerak va
    /// «e'lon qilingan hajm yozilib qolgan» xatosi darhol ko'rinadi.
    /// </remarks>
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\nsinov\n%%EOF");

    private static MemoryStream Oqim() => new(Pdf, writable: false);

    /// <summary>Diskka yozish muvaffaqiyatli bo'ladigan holat.</summary>
    private string DiskTayyor(string kalit = "2026/09/kalit.pdf")
    {
        _context.Employees.Inspector.Detect(Arg.Any<byte[]>()).Returns(DetectedFileKind.Pdf);

        _context.Employees.Storage
            .SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new StoredFile(kalit, "hujjat.pdf", "application/pdf", Pdf.Length));

        return kalit;
    }

    private Candidate Nomzod()
    {
        var vakansiya = _context.ExistingVacancy();
        return _context.ExistingCandidate(vakansiya);
    }

    // ────────────────────── Yuklash ──────────────────────

    [Fact]
    public async Task Nomavjud_nomzodga_fayl_yuklanmaydi()
    {
        DiskTayyor();

        var natija = await Yuklash.HandleAsync(
            new UploadCandidateFileCommand(Guid.CreateVersion7(), Oqim(), "rezyume.pdf", Pdf.Length),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.topilmadi");

        // Noma'lum nomzod uchun oqim UMUMAN o'qilmaydi.
        await _context.Employees.Storage
            .DidNotReceive()
            .SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Togri_fayl_yuklanadi_va_KIM_yuklaganini_saqlaydi()
    {
        var nomzod = Nomzod();
        DiskTayyor();
        var kim = Guid.CreateVersion7();
        _context.Employees.CurrentUser.UserId.Returns(kim);

        var natija = await Yuklash.HandleAsync(
            new UploadCandidateFileCommand(nomzod.Id, Oqim(), "C:\\Users\\hr\\Паспорт.pdf", Pdf.Length),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Code : null);
        natija.Value.FileName.ShouldBe("Паспорт.pdf", "mijoz yuborgan yo'l qismi tashlanadi");
        natija.Value.ContentType.ShouldBe("application/pdf");
        natija.Value.SizeBytes.ShouldBe(Pdf.Length);

        nomzod.Files.Count.ShouldBe(1);
        nomzod.Files[0].CreatedBy.ShouldBe(kim, "«kim yukladi» — audit fakti");
        await _context.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Bazaga DISKKA TUSHGAN hajm yoziladi, mijoz e'lon qilgani emas.</summary>
    /// <remarks>
    /// ⭐ Eski rezyume kodi <c>command.SizeBytes</c> ni yozardi. Rasm
    /// serverda kichraytirilgach o'sha son YOLG'ONGA aylanadi — 4 MB lik
    /// telefon surati diskda ~300 KB bo'ladi. Bu yerda farq ataylab
    /// keskin: mijoz 4 MB deb e'lon qiladi, oqimda esa bir necha o'n bayt.
    /// </remarks>
    [Fact]
    public async Task Bazaga_ELON_QILINGAN_emas_DISKDAGI_hajm_yoziladi()
    {
        var nomzod = Nomzod();
        DiskTayyor();

        var natija = await Yuklash.HandleAsync(
            new UploadCandidateFileCommand(nomzod.Id, Oqim(), "rezyume.pdf", SizeBytes: 4L * 1024 * 1024),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Code : null);
        natija.Value.SizeBytes.ShouldBe(Pdf.Length);
        natija.Value.SizeBytes.ShouldNotBe(4L * 1024 * 1024);
        nomzod.Files[0].SizeBytes.ShouldBe(Pdf.Length);
    }

    [Fact]
    public async Task Chegaradan_katta_fayl_rad_etiladi()
    {
        var nomzod = Nomzod();
        DiskTayyor();

        var natija = await Yuklash.HandleAsync(
            new UploadCandidateFileCommand(
                nomzod.Id, Oqim(), "rezyume.pdf", DocumentLimits.MaxBytes + 1),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.rezyume_katta");
        nomzod.Files.ShouldBeEmpty();
        await _context.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Tanilmagan_mazmun_rad_etiladi()
    {
        var nomzod = Nomzod();
        _context.Employees.Inspector.Detect(Arg.Any<byte[]>()).Returns((DetectedFileKind?)null);

        var natija = await Yuklash.HandleAsync(
            new UploadCandidateFileCommand(nomzod.Id, Oqim(), "rezyume.pdf", Pdf.Length),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.rezyume_yaroqsiz");
        nomzod.Files.ShouldBeEmpty();

        await _context.Employees.Storage
            .DidNotReceive()
            .SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>O'nta fayli bor nomzodda oqim UMUMAN o'qilmaydi.</summary>
    /// <remarks>
    /// Arzon to'xtash: 4 MB lik suratni o'qib, dekodlab, saqlab, so'ng
    /// o'chirishning ma'nosi yo'q. Haqiqiy invariant esa
    /// <c>Candidate.AttachFile</c> da qoladi — u poygaga bardosh beradi.
    /// </remarks>
    [Fact]
    public async Task Ontasi_tolgan_nomzodda_fayl_OQILMAYDI()
    {
        var nomzod = Nomzod();
        DiskTayyor();

        for (var i = 1; i <= Candidate.MaxFiles; i++)
        {
            nomzod.AttachFile($"kalit-{i}", $"varaq-{i}.pdf", "application/pdf", 1024);
        }

        var natija = await Yuklash.HandleAsync(
            new UploadCandidateFileCommand(nomzod.Id, Oqim(), "varaq-11.pdf", Pdf.Length),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.rezyume_kop");
        nomzod.Files.Count.ShouldBe(Candidate.MaxFiles);

        _context.Employees.Inspector.DidNotReceive().Detect(Arg.Any<byte[]>());
        await _context.Employees.Storage
            .DidNotReceive()
            .SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Umumiy hajm oshsa, diskka yozilgan fayl ORTIDAN o'chiriladi.</summary>
    /// <remarks>
    /// ⭐ Bu yagona yo'l: hajm chegarasini faqat domen biladi va u faylni
    /// diskka yozgandan KEYIN ishlaydi. Ortidan tozalash tushib qolsa,
    /// diskda hech kim ishora qilmaydigan fayl qolardi — va buni faqat
    /// yillar keyin, disk to'lganda payqash mumkin bo'lardi.
    /// </remarks>
    [Fact]
    public async Task Umumiy_hajm_oshsa_YETIM_fayl_qoldirilmaydi()
    {
        var nomzod = Nomzod();
        var kalit = DiskTayyor();

        // Chegara to'liq band: keyingi bitta bayt ham sig'maydi.
        nomzod
            .AttachFile("kalit-1", "pasport.jpg", "image/jpeg", Candidate.MaxFilesTotalBytes)
            .IsSuccess.ShouldBeTrue();

        var natija = await Yuklash.HandleAsync(
            new UploadCandidateFileCommand(nomzod.Id, Oqim(), "diplom.pdf", Pdf.Length),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.rezyume_joy");
        nomzod.Files.Count.ShouldBe(1, "rad etilgan fayl yozuvga tushmaydi");

        await _context.Employees.Storage
            .Received(1)
            .SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        await _context.Employees.Storage.Received(1).DeleteAsync(kalit, Arg.Any<CancellationToken>());
        await _context.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ────────────────────── Yuklab olish ──────────────────────

    [Fact]
    public async Task Fayl_asl_nomi_bilan_qaytadi()
    {
        var nomzod = Nomzod();
        var fayl = nomzod.AttachFile("2026/09/kalit.pdf", "Паспорт.pdf", "application/pdf", Pdf.Length).Value;

        _context.Employees.Storage
            .OpenAsync("2026/09/kalit.pdf", Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(Pdf, writable: false));

        var natija = await Olish.HandleAsync(
            new GetCandidateFileQuery(nomzod.Id, fayl.Id), TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Code : null);
        natija.Value.FileName.ShouldBe("Паспорт.pdf");
        natija.Value.ContentType.ShouldBe("application/pdf");

        using var buffer = new MemoryStream();
        await natija.Value.Content.CopyToAsync(buffer, TestContext.Current.CancellationToken);
        buffer.ToArray().ShouldBe(Pdf);
    }

    /// <summary>BOSHQA nomzodning fayli berilmaydi.</summary>
    /// <remarks>
    /// ⭐ Fayl nomzodning AGREGATI ichidan izlanadi, ya'ni chalkash juftlik
    /// («A nomzod + B nomzodning fayli») 404 beradi. Xodim hujjatlaridan
    /// ataylab farq: u yerda hujjat faqat o'z identifikatori bo'yicha
    /// topiladi. Bu to'siq tushib qolsa, birovning pasport nusxasi
    /// istalgan nomzod manzili orqali ochilardi.
    /// </remarks>
    [Fact]
    public async Task Begona_nomzodning_fayli_BERILMAYDI()
    {
        var vakansiya = _context.ExistingVacancy();
        var birinchi = _context.ExistingCandidate(vakansiya);
        var ikkinchi = _context.ExistingCandidate(vakansiya);

        var fayl = birinchi.AttachFile("2026/09/kalit.pdf", "Паспорт.pdf", "application/pdf", 1024).Value;

        _context.Employees.Storage
            .OpenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(Pdf, writable: false));

        var natija = await Olish.HandleAsync(
            new GetCandidateFileQuery(ikkinchi.Id, fayl.Id), TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.rezyume_topilmadi");

        // Diskka murojaat ham bo'lmasligi kerak: yozuv topilmadi.
        await _context.Employees.Storage
            .DidNotReceive()
            .OpenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Yozuv bor, diskda fayl yo'q — mijozga «fayl yo'q» bo'lib ko'rinadi.</summary>
    [Fact]
    public async Task Diskda_fayl_yoq_bolsa_TOPILMADI_qaytadi()
    {
        var nomzod = Nomzod();
        var fayl = nomzod.AttachFile("2026/09/kalit.pdf", "rezyume.pdf", "application/pdf", 1024).Value;

        _context.Employees.Storage
            .OpenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Stream?)null);

        var natija = await Olish.HandleAsync(
            new GetCandidateFileQuery(nomzod.Id, fayl.Id), TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe(
            "nomzod.rezyume_topilmadi",
            "buzilgan holat mijozga 500 emas, «fayl yo'q» bo'lib ko'rinadi");
    }

    // ────────────────────── O'chirish ──────────────────────

    /// <summary>Fayl SAQLANGANDAN KEYIN diskdan o'chiriladi.</summary>
    /// <remarks>
    /// Teskarisi bo'lsa va saqlash yiqilsa, bazadagi yozuv MAVJUD
    /// BO'LMAGAN faylga ishora qilib qolardi.
    /// </remarks>
    [Fact]
    public async Task Fayl_avval_bazadan_keyin_DISKDAN_ochiriladi()
    {
        var nomzod = Nomzod();
        var fayl = nomzod.AttachFile("2026/09/kalit.pdf", "rezyume.pdf", "application/pdf", 1024).Value;

        var natija = await Ochirish.HandleAsync(
            new DeleteCandidateFileCommand(nomzod.Id, fayl.Id), TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Code : null);
        nomzod.Files.ShouldBeEmpty("yumshoq o'chirish YO'Q — yozuv butunlay ketadi");

        Received.InOrder(async () =>
        {
            await _context.UnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
            await _context.Employees.Storage.DeleteAsync("2026/09/kalit.pdf", Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Nomavjud_fayl_ochirilmaydi()
    {
        // Amal IDEMPOTENT emas (suratni o'chirishdan farqi shunda):
        // chaqiruvchi ANIQ bir qatorni nomlagan.
        var nomzod = Nomzod();
        nomzod.AttachFile("2026/09/kalit.pdf", "rezyume.pdf", "application/pdf", 1024);

        var natija = await Ochirish.HandleAsync(
            new DeleteCandidateFileCommand(nomzod.Id, Guid.CreateVersion7()),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.rezyume_topilmadi");
        nomzod.Files.Count.ShouldBe(1, "boshqa fayl tegilmaydi");

        await _context.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _context.Employees.Storage
            .DidNotReceive()
            .DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Begona_nomzodning_fayli_OCHIRILMAYDI()
    {
        var vakansiya = _context.ExistingVacancy();
        var birinchi = _context.ExistingCandidate(vakansiya);
        var ikkinchi = _context.ExistingCandidate(vakansiya);

        var fayl = birinchi.AttachFile("2026/09/kalit.pdf", "Паспорт.pdf", "application/pdf", 1024).Value;

        var natija = await Ochirish.HandleAsync(
            new DeleteCandidateFileCommand(ikkinchi.Id, fayl.Id), TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.rezyume_topilmadi");
        birinchi.Files.Count.ShouldBe(1, "begona so'rov faylni o'chirib ketmaydi");

        await _context.Employees.Storage
            .DidNotReceive()
            .DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
