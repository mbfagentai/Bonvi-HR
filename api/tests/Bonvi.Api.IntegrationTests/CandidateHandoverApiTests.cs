using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Bonvi.Application.Recruiting.Candidates;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.Recruiting;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Xodim — ko'chirish testiga kerakli qismi.</summary>
public sealed record HandoverPersonResponse(Guid Id, string? PhotoKey);

/// <summary>Xodim KARTASI — javob konverti («employee» ichma-ich turadi).</summary>
public sealed record HandoverCardResponse(HandoverPersonResponse Employee);

/// <summary>Profildagi til qatori.</summary>
public sealed record HandoverLanguageResponse(string Language, string Level);

/// <summary>Qobiliyatlar profili — ko'chirish testiga kerakli qismi.</summary>
public sealed record HandoverCompetencyResponse(
    string? Languages,
    IReadOnlyList<HandoverLanguageResponse> LanguageSkills,
    int? ComputerLiteracyPercent,
    string? OtherAbilities,
    string? CandidateNote);

/// <summary>
/// ⭐ Nomzoddan xodimga KO'CHIRISH — uchi-uchiga.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nima uchun bu test bor.</b> Foydalanuvchi aytgan holat
/// (2026-09-19): nomzodning anketa suratlari fayllar bo'limiga
/// yuklangan, odam sinov muddatiga ishga olingan va xodim kartasidagi
/// «Hujjatlar» bo'p-bo'sh chiqqan — HR hammasini qaytadan yuklashga
/// majbur bo'lgan. Tekshirganda surat ham ko'chmasligi ma'lum bo'ldi.
/// </para>
/// <para>
/// Bu yerda tekshiriladigan narsa bitta: ishga qabuldan KEYIN fayl ham,
/// surat ham xodim kartasida bo'ladimi va nomzodda TAKROR qolmaydimi.
/// </para>
/// </remarks>
public sealed class CandidateHandoverApiTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public CandidateHandoverApiTests(BonviApiFactory factory) => _factory = factory;

    /// <summary>
    /// Fayl xodim hujjatiga aylanadi, nomzodda qolmaydi va AYNAN o'sha
    /// baytlar qaytadi.
    /// </summary>
    /// <remarks>
    /// ⭐ Eng muhim shart — oxirgisi: yozuv ko'chganda diskdagi fayl
    /// TEGILMASLIGI kerak. Ko'chirish faylni o'chirib yuborsa, hujjat
    /// birinchi ochilishdayoq «fayl yo'q» bo'lib chiqardi va buni
    /// faqat HR sezardi.
    /// </remarks>
    [Fact]
    public async Task Nomzod_fayli_xodim_hujjatiga_aylanadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var nomzodId = await NomzodAsync(mijoz);
        var fayl = await FaylYuklaAsync(mijoz, nomzodId, "Анкета.pdf");

        var xodimId = await IshgaOlAsync(mijoz, nomzodId);

        // ── 1. Hujjat xodim kartasida, TO'G'RI tur bilan ──
        var hujjatlar = await HujjatlarAsync(mijoz, xodimId);

        hujjatlar.Count.ShouldBe(1, "nomzodning fayli xodim hujjatiga aylanishi kerak");
        hujjatlar[0].FileName.ShouldBe("Анкета.pdf");
        hujjatlar[0].SizeBytes.ShouldBe(fayl.SizeBytes);
        hujjatlar[0].DocumentTypeName.ShouldBe(
            CandidateHandover.DocumentTypeName,
            "fayllar «qabulxona» turiga tushadi — HR keyin aniqlashtiradi");
        hujjatlar[0].Note.ShouldNotBeNullOrWhiteSpace("izohda qayerdan kelgani yozilib qoladi");

        // ── 2. Nomzodda TAKROR qolmaydi ──
        var karta = await KartaAsync(mijoz, nomzodId);
        karta.Files.Count.ShouldBe(0, "fayl KO'CHADI, nusxa olinmaydi");

        // ── 3. Baytlar joyida — fayl diskdan o'chirilmagan ──
        using var yuklash = await mijoz.GetAsync(
            new Uri($"/api/employees/{xodimId}/documents/{hujjatlar[0].Id}/file", UriKind.Relative),
            bekorQilish);

        yuklash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yuklash));
        (await yuklash.Content.ReadAsByteArrayAsync(bekorQilish)).ShouldBe(Pdf());
    }

    /// <summary>Nomzodning surati xodim kartasiga NUSXALANADI.</summary>
    /// <remarks>
    /// Ikkala kartada ham surat qolishi SHART: nomzod suratini
    /// o'chirish diskdagi baytni ham o'chiradi, ya'ni bitta kalitni
    /// bo'lishib olish xodimning suratini jimgina yo'qotardi
    /// (<c>SquarePhotoStore.CopyAsync</c> izohi).
    /// </remarks>
    [Fact]
    public async Task Nomzod_surati_xodimga_kochadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var nomzodId = await NomzodAsync(mijoz);
        await SuratYuklaAsync(mijoz, nomzodId);

        // Avval NOMZODDA surat borligiga ishonch hosil qilamiz — aks
        // holda keyingi tekshiruv «ko'chmadi» deb yolg'on gapirardi.
        using (var nomzodSuratiOldin = await mijoz.GetAsync(
            new Uri($"/api/recruiting/candidates/{nomzodId}/photo", UriKind.Relative), bekorQilish))
        {
            nomzodSuratiOldin.StatusCode.ShouldBe(
                HttpStatusCode.OK, "surat yuklanmagan bo'lsa test ma'nosiz");
        }

        var xodimId = await IshgaOlAsync(mijoz, nomzodId);

        using var xodimJavobi = await mijoz.GetAsync(
            new Uri($"/api/employees/{xodimId}", UriKind.Relative), bekorQilish);

        xodimJavobi.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(xodimJavobi));

        var karta = await xodimJavobi.Content.ReadFromJsonAsync<HandoverCardResponse>(bekorQilish);

        karta!.Employee.PhotoKey.ShouldNotBeNullOrWhiteSpace(
            "yangi xodim kartasida yuz turishi kerak");

        // ⚠️ Kalit NOMZODNIKIDAN boshqa bo'lishi SHART: nusxa olinadi,
        // bo'lishib olinmaydi (sababi `SquarePhotoStore.CopyAsync` izohida).
        await using (var tekshir = _factory.Services.CreateAsyncScope())
        {
            var db = tekshir.ServiceProvider.GetRequiredService<AppDbContext>();

            var nomzodKalit = await db.Candidates
                .Where(item => item.Id == nomzodId)
                .Select(item => item.PhotoKey)
                .FirstAsync(bekorQilish);

            karta.Employee.PhotoKey.ShouldNotBe(
                nomzodKalit,
                "bitta kalitni bo'lishib olish nomzod suratini o'chirganda xodimnikini ham yo'qotardi");
        }

        // Surat HAQIQATAN ochiladi — kalit bo'sh joyga ishora qilmaydi.
        using var surat = await mijoz.GetAsync(
            new Uri($"/api/employees/{xodimId}/photo", UriKind.Relative), bekorQilish);

        surat.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(surat));
        (await surat.Content.ReadAsByteArrayAsync(bekorQilish)).Length.ShouldBeGreaterThan(0);

        // Nomzodda ham qoladi: nusxa olindi, ko'chirilmadi.
        using var nomzodSurati = await mijoz.GetAsync(
            new Uri($"/api/recruiting/candidates/{nomzodId}/photo", UriKind.Relative), bekorQilish);

        nomzodSurati.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            "nomzod kartasi ham suratsiz qolmasligi kerak");
    }

    /// <summary>
    /// Anketa «Qobiliyatlar» profiliga tushadi — mosi maydonga, qolgani izohga.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ Ikkala tomoni ham qadaladi. Rus tili darajasi TUZILGAN qiymat
    /// bo'lib tushadi (ekranda daraja bilan ko'rinadi), xodim tomonida
    /// o'rni yo'q javoblar esa tayyor matnga yig'iladi — ta'lim
    /// darajasi, ish tajribasi, oilaviy holat, oylik kutilmasi va ishni
    /// qayerdan eshitgani (u endi nomzodning o'z maydonidan — kanal nomi va aniqlik).
    /// </para>
    /// <para>
    /// Matnning O'ZI ham tekshiriladi: yorliqsiz yoki tarjimasiz qator
    /// chiqib qolsa, HR ekranda «Married» degan so'zni ko'rardi.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Anketa_qobiliyatlar_profiliga_kochadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var nomzodId = await NomzodAsync(mijoz);
        await AnketaBiriktirAsync(nomzodId);

        var xodimId = await IshgaOlAsync(mijoz, nomzodId);

        using var javob = await mijoz.GetAsync(
            new Uri($"/api/employees/{xodimId}/competency", UriKind.Relative), bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var profil = await javob.Content.ReadFromJsonAsync<HandoverCompetencyResponse>(bekorQilish);
        profil.ShouldNotBeNull();

        // ── Mos tushadigani — TUZILGAN qiymat ──
        profil.LanguageSkills.Count.ShouldBe(1, "rus tili darajasi ro'yxatga qator bo'lib tushadi");
        profil.LanguageSkills[0].Language.ShouldBe("Russian");
        profil.LanguageSkills[0].Level.ShouldBe("Advanced");
        profil.Languages.ShouldBe("Ingliz tili — o'rta");
        profil.OtherAbilities.ShouldNotBeNull();
        profil.OtherAbilities.ShouldContain("Word, Excel");
        profil.OtherAbilities.ShouldContain("B, C");

        // ── Qolgani — tayyor MATN ──
        profil.CandidateNote.ShouldNotBeNullOrWhiteSpace();
        profil.CandidateNote.ShouldContain("Oliy (bakalavr)", customMessage: "ta'lim darajasi tarjimalangan bo'lishi kerak");
        profil.CandidateNote.ShouldContain("Oilali");
        profil.CandidateNote.ShouldContain("3 yil savdoda");
        profil.CandidateNote.ShouldContain("2-3 mln so'm");
        profil.CandidateNote.ShouldContain(
            "Ish haqida qayerdan bilgan: Tanishlar orqali — Рустамов Баҳодир",
            customMessage: "kanal nomi va aniqlik — nomzodning o'z maydonidan, anketadan emas");
        profil.CandidateNote.ShouldNotContain("Married", customMessage: "enum nomi ekranga chiqmasligi kerak");

        // ⚠️ Kompyuter FOIZI bo'sh qoladi: anketada bu matn, bu yerda esa
        // 0-100 foiz — matnni songa aylantirish taxmin bo'lardi.
        profil.ComputerLiteracyPercent.ShouldBeNull();
    }

    /// <summary>
    /// ⭐ HR izohlari ham xodimga o'tadi — anketa javoblari ORTIDAN.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Foydalanuvchi qarori (2026-09-24). Izoh uch joyda yashaydi va
    /// uchalasi ham tekshiriladi: kartadagi izoh, bosqich xulosasi va
    /// izohlar lentasi. Lenta yorlig'i tarjimalangan bo'lishi kerak —
    /// aks holda HR «InterviewRescheduled» degan so'zni ko'rardi.
    /// </para>
    /// <para>
    /// Nomzodda esa hammasi QOLADI: bu nusxa, ko'chirish emas.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task HR_izohlari_xodimga_otadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var nomzodId = await NomzodAsync(mijoz);
        await AnketaBiriktirAsync(nomzodId);
        await IzohlarQoyAsync(nomzodId);

        var xodimId = await IshgaOlAsync(mijoz, nomzodId);

        using var javob = await mijoz.GetAsync(
            new Uri($"/api/employees/{xodimId}/competency", UriKind.Relative), bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var profil = await javob.Content.ReadFromJsonAsync<HandoverCompetencyResponse>(bekorQilish);
        var matn = profil!.CandidateNote;

        matn.ShouldNotBeNull();
        matn.ShouldContain("HR izohi: Tavsiya bilan keldi");
        matn.ShouldContain("Suhbatda o'zini yaxshi ko'rsatdi", customMessage: "bosqich xulosasi ham o'tishi kerak");
        matn.ShouldContain(
            "Suhbat ko'chirildi: Nomzod iltimosi bilan",
            customMessage: "lenta yorlig'i tarjimalangan bo'lishi kerak");
        matn.ShouldNotContain("InterviewRescheduled");
        matn.IndexOf("Oilali", StringComparison.Ordinal).ShouldBeLessThan(
            matn.IndexOf("HR izohi", StringComparison.Ordinal),
            "anketa javoblari birinchi, izohlar ortidan");

        // Nomzodda QOLADI — tarix o'chmaydi.
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        (await kontekst.CandidateNotes.CountAsync(note => note.CandidateId == nomzodId, bekorQilish))
            .ShouldBe(1, "lenta nomzod kartasida qolishi kerak");
    }

    /// <summary>
    /// ⭐ Ko'chirib bo'lmagan fayl NOMZODDA qoladi — yo'qolmaydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Regressiya testi. Ilgari ko'chirish butun ro'yxatni BIRDAN
    /// bo'shatardi va faqat keyin hujjat yasardi: yasash yiqilsa, fayl
    /// ikkala joydan ham yo'qolardi — nomzoddan o'chgan, xodimga
    /// qo'shilmagan, diskda esa hech kim ishora qilmaydigan bayt
    /// qolgan. Buni hech kim sezmasdi.
    /// </para>
    /// <para>
    /// Yiqilish sababi xayoliy emas: <c>EmployeeDocument.Attach</c>
    /// hajmi <c>0</c> bo'lgan faylni rad etadi, nomzod faylida esa
    /// <c>0</c> — «hajmi noma'lum» va eski ko'chirilgan rezyumelarda
    /// uchraydi. Shu sababli test bunday qatorni ATAYLAB to'g'ridan-
    /// to'g'ri bazaga qo'yadi: domen orqali uni yasab bo'lmaydi.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Kochirib_bolmagan_fayl_nomzodda_qoladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var nomzodId = await NomzodAsync(mijoz);

        // Yaroqli fayl — u KO'CHISHI kerak.
        await FaylYuklaAsync(mijoz, nomzodId, "Diplom.pdf");

        // Hajmi noma'lum fayl — u QOLISHI kerak.
        await HajmsizFaylQoyAsync(nomzodId);

        var xodimId = await IshgaOlAsync(mijoz, nomzodId);

        var hujjatlar = await HujjatlarAsync(mijoz, xodimId);
        hujjatlar.Count.ShouldBe(1, "faqat yaroqli fayl hujjatga aylanadi");
        hujjatlar[0].FileName.ShouldBe("Diplom.pdf");

        var karta = await KartaAsync(mijoz, nomzodId);
        karta.Files.Count.ShouldBe(
            1,
            "ko'chirib bo'lmagan fayl nomzod kartasida QOLADI — jimgina yo'qolmaydi");

        karta.Files[0].SizeBytes.ShouldBe(0);
    }

    /// <summary>
    /// Ko'chiradigan narsasi yo'q nomzod ishga qabulni YIQITMAYDI.
    /// </summary>
    /// <remarks>
    /// Excel'dan kelgan va bot orqali o'tmagan nomzodlarda na surat, na
    /// fayl, na anketa bo'ladi — ular ko'pchilikni tashkil qiladi.
    /// Ko'chirish ishga qabulning SHARTI emas, qo'shimchasi.
    /// </remarks>
    [Fact]
    public async Task Bosh_nomzod_ham_muammosiz_ishga_olinadi()
    {
        using var mijoz = await AdminMijozAsync();

        var nomzodId = await NomzodAsync(mijoz);
        var xodimId = await IshgaOlAsync(mijoz, nomzodId);

        (await HujjatlarAsync(mijoz, xodimId)).Count.ShouldBe(0);
    }

    /// <summary>
    /// Hujjat turi ma'lumotnomada URUG'LANGAN bo'lishi kerak.
    /// </summary>
    /// <remarks>
    /// Tur topilmasa ko'chirish jimgina to'xtaydi (fayl nomzodda
    /// qoladi) — ya'ni urug' faylidagi qator tushib qolsa, butun
    /// funksiya ishlamay qo'yadi va buni hech bir test aytmasdi.
    /// </remarks>
    [Fact]
    public async Task Nomzodlik_hujjatlari_turi_urugda_bor()
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var bor = await kontekst.DocumentTypes
            .AnyAsync(
                type => type.Name == CandidateHandover.DocumentTypeName,
                TestContext.Current.CancellationToken);

        bor.ShouldBeTrue($"«{CandidateHandover.DocumentTypeName}» ma'lumotnomada bo'lishi SHART");
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    private static byte[] Pdf()
        => Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\n%%EOF");

    private static string Belgi() => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private static async Task<IReadOnlyList<EmployeeDocumentResponse>> HujjatlarAsync(
        HttpClient mijoz,
        Guid xodimId)
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var javob = await mijoz.GetAsync(
            new Uri($"/api/employees/{xodimId}/documents", UriKind.Relative), bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var royxat = await javob.Content
            .ReadFromJsonAsync<IReadOnlyList<EmployeeDocumentResponse>>(bekorQilish);

        royxat.ShouldNotBeNull();
        return royxat;
    }

    private static async Task<CandidateFileCardResponse> KartaAsync(HttpClient mijoz, Guid nomzodId)
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var javob = await mijoz.GetAsync(
            new Uri($"/api/recruiting/candidates/{nomzodId}", UriKind.Relative), bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var karta = await javob.Content.ReadFromJsonAsync<CandidateFileCardResponse>(bekorQilish);
        karta.ShouldNotBeNull();
        return karta;
    }

    /// <summary>Vakansiya va nomzod — bitta qadamda.</summary>
    private static async Task<Guid> NomzodAsync(HttpClient mijoz)
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        Guid vakansiyaId;

        using (var vakansiya = await mijoz.PostAsJsonAsync(
            new Uri("/api/recruiting/vacancies", UriKind.Relative),
            new { title = "Оператор " + Belgi(), openings = 1, openedAt = new DateOnly(2026, 3, 1) },
            bekorQilish))
        {
            vakansiya.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(vakansiya));
            vakansiyaId = (await vakansiya.Content.ReadFromJsonAsync<VacancyResponse>(bekorQilish))!.Id;
        }

        using var javob = await mijoz.PostAsJsonAsync(
            new Uri("/api/recruiting/candidates", UriKind.Relative),
            new
            {
                vacancyId = vakansiyaId,
                lastName = "Кўчувчи " + Belgi(),
                firstName = "Азиз",
                phone = "905066666",

                // «Qayerdan bilgan» — nomzodning o'zida va majburiy (SPEC-qayerdan-bilgan §5)
                referralChannelId = ReferralSeeds.Acquaintance,
                referralDetail = "Рустамов Баҳодир",
            },
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var nomzod = await javob.Content.ReadFromJsonAsync<CandidateFileCardResponse>(bekorQilish);
        nomzod.ShouldNotBeNull();
        return nomzod.Id;
    }

    /// <summary>
    /// Hajmi NOMA'LUM (<c>0</c>) faylni to'g'ridan-to'g'ri bazaga qo'yadi.
    /// </summary>
    /// <remarks>
    /// Domen bunday faylni yasashga ruxsat bermaydi
    /// (<c>CandidateFile.Create</c> hajmni talab qiladi) — u faqat eski
    /// rezyumelar ko'chirilganda paydo bo'lgan. Testga esa AYNAN shunday
    /// qator kerak.
    /// </remarks>
    private async Task HajmsizFaylQoyAsync(Guid nomzodId)
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        await kontekst.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO candidate_files (id, candidate_id, file_key, file_name, content_type, size_bytes, created_at)
            VALUES ({Guid.CreateVersion7()}, {nomzodId}, '2026/09/hajmsiz.pdf', 'Eski rezyume.pdf', 'application/pdf', 0, now())
            """,
            bekorQilish);
    }

    /// <summary>
    /// Nomzodga uch xil izoh qo'yadi — TO'G'RIDAN-TO'G'RI bazaga.
    /// </summary>
    /// <remarks>
    /// Test ko'chirishni tekshiradi, izoh yozish oqimlarini emas —
    /// ularning o'z testlari bor. Bosqich qatori nomzod yaratilganda
    /// paydo bo'ladi, bu yerda unga faqat xulosa yoziladi.
    /// </remarks>
    private async Task IzohlarQoyAsync(Guid nomzodId)
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        await kontekst.Database.ExecuteSqlAsync(
            $"UPDATE candidates SET note = 'Tavsiya bilan keldi' WHERE id = {nomzodId}",
            bekorQilish);

        await kontekst.Database.ExecuteSqlAsync(
            $"UPDATE candidate_stage_entries SET conclusion = 'Suhbatda o''zini yaxshi ko''rsatdi' WHERE candidate_id = {nomzodId}",
            bekorQilish);

        await kontekst.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO candidate_notes (id, candidate_id, kind, text, created_at)
            VALUES ({Guid.CreateVersion7()}, {nomzodId}, {(int)CandidateNoteKind.InterviewRescheduled}, 'Nomzod iltimosi bilan', now())
            """,
            bekorQilish);
    }

    /// <summary>
    /// Nomzodga anketa biriktiradi — TO'G'RIDAN-TO'G'RI bazaga.
    /// </summary>
    /// <remarks>
    /// Anketa odatda botdan keladi va uni HTTP orqali yaratadigan yo'l
    /// yo'q. Testga kerak bo'lgani esa faqat NATIJA: anketasi bor
    /// nomzod ishga olinganda profil to'ladimi.
    /// </remarks>
    private async Task AnketaBiriktirAsync(Guid nomzodId)
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var anketa = CandidateQuestionnaire.Create(
            nomzodId,
            EducationLevel.HigherComplete,
            MaritalStatus.Married,
            LanguageLevel.Advanced,
            SalaryExpectation.From2To3Million,
            offerAcceptedAt: null,
            workExperience: "3 yil savdoda",
            otherLanguages: "Ingliz tili — o'rta",
            officeSkills: "Word, Excel",
            lastSalary: "4 000 000",
            extraNote: "Kechki smenaga tayyor",
            hasDriverLicense: true,
            driverLicenseCategories: "B, C").Value;

        kontekst.Add(anketa);

        await kontekst.SaveChangesAsync(bekorQilish);
    }

    private static async Task<CandidateFileResponse> FaylYuklaAsync(
        HttpClient mijoz,
        Guid nomzodId,
        string fileName)
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var forma = new MultipartFormDataContent();
        using var qism = new ByteArrayContent(Pdf());
        qism.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        forma.Add(qism, "file", fileName);

        using var javob = await mijoz.PostAsync(
            new Uri($"/api/recruiting/candidates/{nomzodId}/files", UriKind.Relative),
            forma,
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var fayl = await javob.Content.ReadFromJsonAsync<CandidateFileResponse>(bekorQilish);
        fayl.ShouldNotBeNull();
        return fayl;
    }

    private static async Task SuratYuklaAsync(HttpClient mijoz, Guid nomzodId)
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var forma = new MultipartFormDataContent();
        using var qism = new ByteArrayContent(TestImages.JpegYasa(600, 600, sifat: 80));
        qism.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        forma.Add(qism, "file", "surat.jpg");

        using var javob = await mijoz.PostAsync(
            new Uri($"/api/recruiting/candidates/{nomzodId}/photo", UriKind.Relative),
            forma,
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
    }

    /// <summary>Nomzodni xodimga aylantiradi va XODIM identifikatorini qaytaradi.</summary>
    private async Task<Guid> IshgaOlAsync(HttpClient mijoz, Guid nomzodId)
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var (firmaId, lavozimId) = await MalumotnomaAsync();

        using var javob = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{nomzodId}/hire", UriKind.Relative),
            new { legalEntityId = firmaId, positionId = lavozimId },
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var natija = await javob.Content.ReadFromJsonAsync<DirectHireResponse>(bekorQilish);
        natija.ShouldNotBeNull();
        return natija.Hire.Employment.EmployeeId;
    }

    private async Task<(Guid LegalEntityId, Guid PositionId)> MalumotnomaAsync()
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var belgi = Belgi();

        var firma = LegalEntity.Create("Кўчириш Тест МЧЖ " + belgi).Value;
        var lavozim = Position.Create("Оператор " + belgi).Value;

        kontekst.Add(firma);
        kontekst.Add(lavozim);

        await kontekst.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (firma.Id, lavozim.Id);
    }

    private async Task<HttpClient> AdminMijozAsync()
    {
        var client = _factory.CreateClient();

        using var javob = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new { login = BonviApiFactory.TestAdminLogin, password = BonviApiFactory.TestAdminPassword },
            TestContext.Current.CancellationToken);

        javob.EnsureSuccessStatusCode();

        var auth = await javob.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return client;
    }

    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
