using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bonvi.Domain.Organization;
using Bonvi.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Voronka bosqichi — testga kerakli qismi.</summary>
public sealed record StageResponse(Guid Id, string Name, bool IsActive, int SortOrder);

/// <summary>Vakansiya nomi (ma'lumotnoma yozuvi) — testga kerakli qismi.</summary>
public sealed record VacancyTitleResponse(Guid Id, string Name, bool IsActive);

/// <summary>Vakansiya — testga kerakli qismi.</summary>
/// <remarks>
/// Lavozim, bo'lim va firma maydonlari YO'Q: vakansiya faqat e'lon
/// (sababi <c>Vacancy</c> domen izohida). Shu sababli javobda ham faqat
/// e'lon nomi bor.
/// </remarks>
public sealed record VacancyResponse(
    Guid Id,
    Guid VacancyTitleId,
    string? Title,
    int Openings,
    string Status,
    // Qayta ochish uni TOZALAYDI — shu sababli testga kerak.
    DateOnly? ClosedAt,
    int CandidateCount,
    int HiredCount);

/// <summary>Nomzodning bosqich yozuvi.</summary>
public sealed record CandidateStageResponse(
    Guid Id,
    Guid StageId,
    string? Stage,
    DateOnly Date,
    string Outcome,
    string? Conclusion);

/// <summary>Ish taklifi — testga kerakli qismi.</summary>
public sealed record OfferResponse(
    Guid Id,
    Guid CandidateId,
    string Status,
    MoneyResponse Salary,
    DateOnly StartDate);

/// <summary>Nomzod kartasi — testga kerakli qismi.</summary>
public sealed record CandidateResponse(
    Guid Id,
    string FullName,
    string? Phone,
    Guid VacancyId,
    Guid? CurrentStageId,
    string? CurrentStage,
    string Status,
    string Source,
    Guid? HiredEmployeeId,
    Guid? HiredEmploymentId,
    IReadOnlyList<CandidateStageResponse> Stages,
    IReadOnlyList<OfferResponse> Offers);

/// <summary>Sahifalangan vakansiyalar ro'yxati.</summary>
public sealed record VacancyPageResponse(IReadOnlyList<VacancyResponse> Items, int Total);

/// <summary>Nomzodlar ro'yxatining qatori.</summary>
public sealed record CandidateRowResponse(
    Guid Id,
    string FullName,
    string? Phone,
    string Status,
    Guid? CurrentStageId,
    Guid? HiredEmployeeId);

/// <summary>Sahifalangan nomzodlar ro'yxati.</summary>
public sealed record CandidatePageResponse(IReadOnlyList<CandidateRowResponse> Items, int Total);

/// <summary>Ishga qabul natijasi — «accept» javobidagi qism.</summary>
public sealed record RecruitingHireResponse(HireEmploymentResponse Employment);

/// <summary>«Taklif qabul qilindi» javobining to'liq konverti.</summary>
public sealed record AcceptOfferResponse(
    OfferResponse Offer,
    CandidateResponse Candidate,
    RecruitingHireResponse Hire);

/// <summary>
/// Rekruting (2-bosqich) — uchi-uchiga: vakansiya → nomzod → voronka →
/// ish taklifi → 1-bosqichning ishga qabul oqimi.
/// </summary>
/// <remarks>
/// <para>
/// Mantiq domen va use-case testlarida qoplangan — bu yerda ULANISH
/// tekshiriladi: marshrutlar, ruxsatlar, urug' fayli, migratsiya va eng
/// muhimi — nomzoddan xodimga o'tish HAQIQIY bazada ishlaydimi.
/// </para>
/// <para>
/// <b>Nega bu eng muhim test.</b> Butun 2-bosqichning qiymati bitta
/// jumlada: «nomzod ishga olinganda xodim kartasi O'ZI paydo bo'ladi va u
/// sinov ro'yxatiga tushadi». Agar shu yo'l uzilsa, HR yana ikkita tizimda
/// ikki marta yozardi — ya'ni Excel'dagi holat qaytadi.
/// </para>
/// </remarks>
public sealed class RecruitingApiTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public RecruitingApiTests(BonviApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Urugh_fayli_voronkani_TARTIB_bilan_quyadi()
    {
        using var mijoz = await AdminMijozAsync();

        var bosqichlar = await BosqichlarAsync(mijoz);

        /*
          VORONKA UCHTA BOSQICH (2026-08-31, foydalanuvchi qarori).

          Ilgari urug' faylida oltita bosqich bor edi — Jobster'ning
          voronkasidan olingan. MBF ning haqiqiy jarayoni esa qisqaroq:
          ariza tushadi, kengash ko'radi, rahbar suhbatlashadi va
          shundan keyin ishga olinadi yoki rad etiladi. «Телефон
          суҳбат», «Суҳбат 1» va «Job Offer» ular o'tmaydigan
          bosqichlar edi va voronka taxtasida doim bo'sh turardi.

          Bosqichlar ekrandan boshqariladi, ya'ni bu son QAT'IY EMAS —
          test aynan uchtani emas, VORONKANING SHAKLINI qadaydi:
          anketadan boshlanadi, kengash bor va tartib buzilmagan.
        */
        bosqichlar.Count.ShouldBeGreaterThanOrEqualTo(3, "urug' fayli uchta bosqich beradi");
        bosqichlar[0].Name.ShouldBe("Анкета", "voronka anketadan boshlanadi");
        bosqichlar.Select(b => b.SortOrder).ShouldBe(bosqichlar.Select(b => b.SortOrder).Order());
        bosqichlar.ShouldContain(b => b.Name == "Кенгаш", "kengash — oddiy bosqich, alohida mexanizm emas");
        bosqichlar.ShouldContain(b => b.Name == "Раҳбар суҳбати", "yakuniy qaror shu yerda");
    }

    [Fact]
    public async Task Toliq_voronka_nomzodni_XODIMGA_aylantiradi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var (firmaId, lavozimId) = await MalumotnomaAsync();
        var bosqichlar = await BosqichlarAsync(mijoz);
        var anketa = bosqichlar[0];
        var kengash = bosqichlar.First(b => b.Name == "Кенгаш");

        // ── 1. Vakansiya ochiladi — YANGI ma'lumotnomadagi nom bilan ──
        //    ⚠️ E'lon nomi kompaniya lavozimi EMAS: yuqoridagi «lavozimId»
        //    bu yerda umuman ishlatilmaydi, u faqat 5-qadamda — ishga
        //    qabulda kerak bo'ladi.
        var nom = await VakansiyaNomiAsync(mijoz);
        var vakansiya = await VakansiyaAsync(mijoz, nom.Id);
        vakansiya.Status.ShouldBe("Open");
        vakansiya.CandidateCount.ShouldBe(0);
        vakansiya.VacancyTitleId.ShouldBe(nom.Id);
        vakansiya.Title.ShouldBe(nom.Name, "nom qatorda tayyor keladi");

        // ── 2. Nomzod kiritiladi — darhol birinchi bosqichga tushadi ──
        var nomzod = await NomzodAsync(mijoz, vakansiya.Id);
        nomzod.Status.ShouldBe("InProgress");
        nomzod.Source.ShouldBe("Manual");
        nomzod.CurrentStageId.ShouldBe(anketa.Id);
        nomzod.Stages.Count.ShouldBe(1);
        nomzod.Stages[0].Outcome.ShouldBe("Pending");

        // ── 3. Kengash xulosasi — ODDIY bosqich yozuvi ──
        using var kengashJavobi = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{nomzod.Id}/stages", UriKind.Relative),
            new
            {
                stageId = kengash.Id,
                outcome = "Passed",
                date = new DateOnly(2026, 4, 1),
                conclusion = "Кенгаш хулосаси: ишга олинсин",
            },
            bekorQilish);

        kengashJavobi.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(kengashJavobi));
        var kengashdan = await kengashJavobi.Content.ReadFromJsonAsync<CandidateResponse>(bekorQilish);
        kengashdan!.CurrentStageId.ShouldBe(kengash.Id);
        kengashdan.Stages.Count.ShouldBe(2);
        kengashdan.Stages[^1].Conclusion.ShouldBe("Кенгаш хулосаси: ишга олинсин");
        kengashdan.Stages[^1].Stage.ShouldBe("Кенгаш", "bosqich NOMI qatorda tayyor keladi");

        // ── 4. Ish taklifi ──
        var taklif = await TaklifAsync(mijoz, nomzod.Id);
        taklif.Status.ShouldBe("Sent");
        taklif.Salary.Amount.ShouldBe(6_000_000m);

        // ── 5. Taklif qabul qilinadi → XODIM yaratiladi ──
        // ⭐ Firma va LAVOZIM aynan shu yerda tanlanadi — vakansiyadan
        //    emas. Bu e'lon bilan shtat jadvali uchrashadigan yagona nuqta.
        using var qabul = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/offers/{taklif.Id}/accept", UriKind.Relative),
            new
            {
                legalEntityId = firmaId,
                positionId = lavozimId,
                orderNo = "185-b",
                orderDate = new DateOnly(2026, 4, 10),
                pinfl = (string?)null,
            },
            bekorQilish);

        qabul.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qabul));
        var natija = await qabul.Content.ReadFromJsonAsync<AcceptOfferResponse>(bekorQilish);

        natija!.Offer.Status.ShouldBe("Accepted");
        natija.Candidate.Status.ShouldBe("Hired");
        natija.Candidate.HiredEmployeeId.ShouldNotBeNull("nomzod qaysi xodimga aylangani ko'rinishi kerak");

        var mehnat = natija.Hire.Employment;
        mehnat.Status.ShouldBe("Probation", "sinov muddati MAJBURIY va istisnosiz");
        mehnat.Probation.ShouldNotBeNull();
        natija.Candidate.HiredEmployeeId.ShouldBe(mehnat.EmployeeId);
        natija.Candidate.HiredEmploymentId.ShouldBe(mehnat.Id);

        // ── 6. Yangi xodim SINOV ro'yxatida ──
        using var sinovlar = await mijoz.GetAsync(
            new Uri("/api/probations?status=InProgress", UriKind.Relative), bekorQilish);

        var sahifa = await sinovlar.Content.ReadFromJsonAsync<ProbationPageResponse>(bekorQilish);
        sahifa!.Items.ShouldContain(
            q => q.EmployeeId == mehnat.EmployeeId,
            "ishga olingan nomzod /sinov ro'yxatida");

        // ── 7. Nomzod voronkada FAOL bo'lib qolmaydi ──
        var faollar = await NomzodlarAsync(mijoz, $"?vacancyId={vakansiya.Id}&status=InProgress");
        faollar.Items.ShouldNotContain(q => q.Id == nomzod.Id);

        var ishgaOlinganlar = await NomzodlarAsync(mijoz, $"?vacancyId={vakansiya.Id}&status=Hired");
        ishgaOlinganlar.Items.ShouldContain(q => q.Id == nomzod.Id && q.HiredEmployeeId == mehnat.EmployeeId);

        // ── 8. Vakansiya sanog'i yangilandi ──
        using var vakansiyaJavobi = await mijoz.GetAsync(
            new Uri($"/api/recruiting/vacancies/{vakansiya.Id}", UriKind.Relative), bekorQilish);

        var yangilangan = await vakansiyaJavobi.Content.ReadFromJsonAsync<VacancyResponse>(bekorQilish);
        yangilangan!.CandidateCount.ShouldBe(1);
        yangilangan.HiredCount.ShouldBe(1);
        yangilangan.Status.ShouldBe("Open", "o'rin to'lganda vakansiya O'ZI yopilmaydi — bu HR qarori");

        // ── 9. Ikkinchi marta qabul qilib bo'lmaydi ──
        using var takror = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/offers/{taklif.Id}/accept", UriKind.Relative),
            new { legalEntityId = firmaId, positionId = lavozimId },
            bekorQilish);

        takror.StatusCode.ShouldBe(HttpStatusCode.Conflict, await MatnAsync(takror));
    }

    [Fact]
    public async Task Ikkinchi_faol_taklif_yuborilmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var vakansiya = await VakansiyaAsync(mijoz, (await VakansiyaNomiAsync(mijoz)).Id);
        var nomzod = await NomzodAsync(mijoz, vakansiya.Id);

        await TaklifAsync(mijoz, nomzod.Id);

        using var ikkinchi = await mijoz.PostAsJsonAsync(
            new Uri("/api/recruiting/offers", UriKind.Relative),
            new { candidateId = nomzod.Id, salaryAmount = 7_000_000m, startDate = new DateOnly(2026, 5, 1) },
            bekorQilish);

        ikkinchi.StatusCode.ShouldBe(HttpStatusCode.Conflict, await MatnAsync(ikkinchi));
        (await MatnAsync(ikkinchi)).ShouldContain("taklif.faol_mavjud");
    }

    [Fact]
    public async Task Ochirilgan_bosqichga_otkazilmaydi_lekin_unda_turgan_nomzod_yoqolmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var vakansiya = await VakansiyaAsync(mijoz, (await VakansiyaNomiAsync(mijoz)).Id);

        // Sinov uchun ALOHIDA bosqich: urug' fayldagilarni o'chirsak,
        // boshqa testlar buziladi.
        using var yaratish = await mijoz.PostAsJsonAsync(
            new Uri("/api/recruiting/stages", UriKind.Relative),
            new { name = "Синов босқичи " + Belgi() },
            bekorQilish);

        yaratish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yaratish));
        var bosqich = await yaratish.Content.ReadFromJsonAsync<StageResponse>(bekorQilish);

        // Nomzod aynan shu bosqichda turadi
        var nomzod = await NomzodAsync(mijoz, vakansiya.Id, bosqich!.Id);
        nomzod.CurrentStageId.ShouldBe(bosqich.Id);

        using var ochirish = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/stages/{bosqich.Id}/deactivate", UriKind.Relative),
            new { reason = "Босқич бекор қилинди" },
            bekorQilish);

        ochirish.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            "bosqichda nomzod turgani faolsizlantirishni TO'SMAYDI");

        // 1. Unda TURGAN nomzod yo'qolmaydi va bosqich nomi ko'rinib turadi
        using var karta = await mijoz.GetAsync(
            new Uri($"/api/recruiting/candidates/{nomzod.Id}", UriKind.Relative), bekorQilish);

        var kartaJavobi = await karta.Content.ReadFromJsonAsync<CandidateResponse>(bekorQilish);
        kartaJavobi!.CurrentStageId.ShouldBe(bosqich.Id);
        kartaJavobi.CurrentStage.ShouldBe(bosqich.Name, "faolsizlantirilgan bosqichning NOMI ham qaytadi");

        // 2. YANGI nomzodni unga o'tkazib bo'lmaydi
        var boshqa = await NomzodAsync(mijoz, vakansiya.Id);

        using var otkazish = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{boshqa.Id}/stages", UriKind.Relative),
            new { stageId = bosqich.Id },
            bekorQilish);

        otkazish.StatusCode.ShouldBe(HttpStatusCode.Conflict, await MatnAsync(otkazish));
        (await MatnAsync(otkazish)).ShouldContain("bosqich.faol_emas");
    }

    [Fact]
    public async Task Yopilgan_vakansiyaga_nomzod_kiritilmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var vakansiya = await VakansiyaAsync(mijoz, (await VakansiyaNomiAsync(mijoz)).Id);

        using var yopish = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/vacancies/{vakansiya.Id}/close", UriKind.Relative),
            new { reason = "Ўрин тўлди" },
            bekorQilish);

        yopish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yopish));

        using var kiritish = await mijoz.PostAsJsonAsync(
            new Uri("/api/recruiting/candidates", UriKind.Relative),
            new { vacancyId = vakansiya.Id, lastName = "Каримов", firstName = "Азиз" },
            bekorQilish);

        kiritish.StatusCode.ShouldBe(HttpStatusCode.Conflict, await MatnAsync(kiritish));
        (await MatnAsync(kiritish)).ShouldContain("vakansiya.qabul_qilmaydi");

        // ── Yopish QAYTARILADI ──
        //
        // Yopish oynasida ilgari «qayta ochish mumkin emas» degan yozuv
        // turardi va u YOLG'ON edi: domen hech qachon bunday shart
        // qo'ymagan. Endi buni test ham ushlab turadi.
        using var qaytaOchish = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/vacancies/{vakansiya.Id}/reopen", UriKind.Relative),
            new { },
            bekorQilish);

        qaytaOchish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qaytaOchish));

        var ochilgan = await qaytaOchish.Content.ReadFromJsonAsync<VacancyResponse>(bekorQilish);
        ochilgan!.Status.ShouldBe("Open");
        ochilgan.ClosedAt.ShouldBeNull("qayta ochilgan vakansiyada yopilish sanasi bo'lmaydi");
    }

    /// <summary>
    /// O'chirish — FAQAT nomzodi yo'q vakansiyada.
    /// </summary>
    /// <remarks>
    /// Nomzodi bor vakansiya — TARIX: uni o'chirish nomzodlarning
    /// yozuvini yetim qoldirardi («bu odam qayerga ariza bergan edi?»
    /// degan savol javobsiz qolardi). Frontend bunday qatorda o'chirish
    /// tugmasini umuman chizmaydi, lekin himoya SERVER tomonda:
    /// so'rovni qo'lda ham, eskirgan ro'yxatdan ham yuborish mumkin.
    /// </remarks>
    [Fact]
    public async Task Nomzodi_bor_vakansiya_ochirilmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var nom = await VakansiyaNomiAsync(mijoz);

        // ── 1. Nomzodi YO'Q vakansiya o'chiriladi ──
        var bosh = await VakansiyaAsync(mijoz, nom.Id);

        using var boshniOchirish = await mijoz.DeleteAsync(
            new Uri($"/api/recruiting/vacancies/{bosh.Id}", UriKind.Relative), bekorQilish);

        boshniOchirish.StatusCode.ShouldBe(
            HttpStatusCode.NoContent,
            "xato kiritilgan yozuv o'chiriladi: " + await MatnAsync(boshniOchirish));

        // ── 2. Nomzodi BOR vakansiya o'chirilmaydi ──
        var tolgan = await VakansiyaAsync(mijoz, nom.Id);
        await NomzodAsync(mijoz, tolgan.Id);

        using var ochirish = await mijoz.DeleteAsync(
            new Uri($"/api/recruiting/vacancies/{tolgan.Id}", UriKind.Relative), bekorQilish);

        ochirish.StatusCode.ShouldBe(HttpStatusCode.Conflict, await MatnAsync(ochirish));
        (await MatnAsync(ochirish)).ShouldContain("vakansiya.nomzodlari_bor");

        // ── 3. U hamon ro'yxatda va yopilishi mumkin ──
        using var yopish = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/vacancies/{tolgan.Id}/close", UriKind.Relative),
            new { reason = "Хато очилган" },
            bekorQilish);

        yopish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yopish));
    }

    [Fact]
    public async Task Qabulda_LAVOZIM_majburiy()
    {
        // ⭐ Foydalanuvchining qarori: «ишга олингандан кейин
        // компаниянинг бўлимлари билан ҳам танланиб АНИҚ ЛАВОЗИМ
        // танланади». Vakansiyada lavozim yo'q, ya'ni uni bu yerda
        // bo'sh qoldirish xodimni lavozimsiz yaratardi.
        //
        // ⚠️ Bu majburiylik FAQAT rekruting qatlamida:
        // `POST /api/employments/hire` lavozimsiz ishlashda davom etadi
        // (HR formasi va Excel'dan ko'chirish unga tayanadi).
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var (firmaId, _) = await MalumotnomaAsync();
        var vakansiya = await VakansiyaAsync(mijoz, (await VakansiyaNomiAsync(mijoz)).Id);
        var nomzod = await NomzodAsync(mijoz, vakansiya.Id);
        var taklif = await TaklifAsync(mijoz, nomzod.Id);

        using var lavozimsiz = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/offers/{taklif.Id}/accept", UriKind.Relative),
            new { legalEntityId = firmaId },
            bekorQilish);

        lavozimsiz.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(lavozimsiz));
        (await MatnAsync(lavozimsiz)).ShouldContain("taklif.lavozim_yoq");

        // Firma ham majburiy — u `HireEmployeeCommand` da nullable emas.
        using var firmasiz = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/offers/{taklif.Id}/accept", UriKind.Relative),
            new { },
            bekorQilish);

        firmasiz.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(firmasiz));
        (await MatnAsync(firmasiz)).ShouldContain("taklif.yuridik_shaxs_yoq");

        // Nomzod voronkada QOLDI: rad etilgan qabul hech narsani
        // o'zgartirmaydi.
        using var karta = await mijoz.GetAsync(
            new Uri($"/api/recruiting/candidates/{nomzod.Id}", UriKind.Relative), bekorQilish);

        var kartaJavobi = await karta.Content.ReadFromJsonAsync<CandidateResponse>(bekorQilish);
        kartaJavobi!.Status.ShouldBe("InProgress");
    }

    [Fact]
    public async Task Vakansiya_nomi_MAJBURIY_va_malumotnomadan_olinadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        // 1. Nomsiz vakansiya ochilmaydi
        using var nomsiz = await mijoz.PostAsJsonAsync(
            new Uri("/api/recruiting/vacancies", UriKind.Relative),
            new { openings = 1 },
            bekorQilish);

        nomsiz.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(nomsiz));
        (await MatnAsync(nomsiz)).ShouldContain("vakansiya.nom_yoq");

        // 2. Ma'lumotnomada yo'q nom bilan ham ochilmaydi
        using var notanish = await mijoz.PostAsJsonAsync(
            new Uri("/api/recruiting/vacancies", UriKind.Relative),
            new { vacancyTitleId = Guid.CreateVersion7(), openings = 1 },
            bekorQilish);

        notanish.StatusCode.ShouldBe(HttpStatusCode.NotFound, await MatnAsync(notanish));
        (await MatnAsync(notanish)).ShouldContain("vakansiya.nom_topilmadi");

        // 3. LAVOZIM identifikatori ham ish bermaydi — bu boshqa
        //    ma'lumotnoma va ular ATAYLAB aralashmaydi.
        var (_, lavozimId) = await MalumotnomaAsync();

        using var lavozimBilan = await mijoz.PostAsJsonAsync(
            new Uri("/api/recruiting/vacancies", UriKind.Relative),
            new { vacancyTitleId = lavozimId, openings = 1 },
            bekorQilish);

        lavozimBilan.StatusCode.ShouldBe(HttpStatusCode.NotFound, await MatnAsync(lavozimBilan));
        (await MatnAsync(lavozimBilan)).ShouldContain("vakansiya.nom_topilmadi");

        // 4. To'g'ri nom bilan — ochiladi va nom bo'yicha filtrlanadi
        var nom = await VakansiyaNomiAsync(mijoz);
        var vakansiya = await VakansiyaAsync(mijoz, nom.Id);

        using var royxat = await mijoz.GetAsync(
            new Uri($"/api/recruiting/vacancies?vacancyTitleId={nom.Id}", UriKind.Relative),
            bekorQilish);

        royxat.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(royxat));
        var sahifa = await royxat.Content.ReadFromJsonAsync<VacancyPageResponse>(bekorQilish);
        sahifa!.Items.ShouldContain(q => q.Id == vakansiya.Id);
        sahifa.Items.ShouldAllBe(q => q.VacancyTitleId == nom.Id);
    }

    [Fact]
    public async Task Nomzod_TELEFON_va_ism_boyicha_topiladi()
    {
        using var mijoz = await AdminMijozAsync();

        var vakansiya = await VakansiyaAsync(mijoz, (await VakansiyaNomiAsync(mijoz)).Id);
        var nomzod = await NomzodAsync(mijoz, vakansiya.Id, familiya: "Абдуқодиров");

        // 1. Telefon — formatlangan holda ham topilishi kerak
        var telefonBoyicha = await NomzodlarAsync(mijoz, "?search=" + Uri.EscapeDataString("90 508 88 88"));
        telefonBoyicha.Items.ShouldContain(q => q.Id == nomzod.Id, "ajratgichli raqam ham topilishi kerak");

        // 2. Ism — rus klaviaturasidagi imlo bilan ham («қ» → «к»)
        var ismBoyicha = await NomzodlarAsync(mijoz, "?search=" + Uri.EscapeDataString("Абдукодиров"));
        ismBoyicha.Items.ShouldContain(
            q => q.Id == nomzod.Id,
            "kirill imlo variantlari xodimlar ro'yxatidagi kabi yig'iladi");
    }

    [Fact]
    public async Task Voronka_qayta_tartiblanadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        // BARCHA bosqichlar: qayta tartiblash to'liq ro'yxatni talab qiladi.
        var asl = await BosqichlarAsync(mijoz, onlyActive: false);
        var teskari = asl.Select(b => b.Id).Reverse().ToList();

        using var javob = await mijoz.PostAsJsonAsync(
            new Uri("/api/recruiting/stages/reorder", UriKind.Relative),
            new { orderedIds = teskari },
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        var yangi = await javob.Content.ReadFromJsonAsync<List<StageResponse>>(bekorQilish);
        yangi.ShouldNotBeNull();
        yangi.Select(b => b.Id).ShouldBe(teskari);
        yangi.Select(b => b.SortOrder).ShouldBe(Enumerable.Range(0, yangi.Count), "0 dan qayta raqamlanadi");

        // Yarim ro'yxat rad etiladi
        using var yarim = await mijoz.PostAsJsonAsync(
            new Uri("/api/recruiting/stages/reorder", UriKind.Relative),
            new { orderedIds = new[] { teskari[0] } },
            bekorQilish);

        yarim.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(yarim));
        (await MatnAsync(yarim)).ShouldContain("bosqich.tartib_royxati");

        // Boshqa testlarga xalaqit bermaslik uchun tartib TIKLANADI
        using var tiklash = await mijoz.PostAsJsonAsync(
            new Uri("/api/recruiting/stages/reorder", UriKind.Relative),
            new { orderedIds = asl.Select(b => b.Id).ToList() },
            bekorQilish);

        tiklash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(tiklash));
    }

    // ────────────────────────────────────────────────────────────
    //  Muhit tayyorlash
    // ────────────────────────────────────────────────────────────

    private static string Belgi() => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    /// <summary>
    /// Yuridik shaxs va lavozimni to'g'ridan-to'g'ri bazaga qo'yadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ma'lumotnoma endpoint'i orqali emas (<c>HireApiTests</c> dagi bilan
    /// bir xil sabab): bu testda tekshiriladigan narsa rekruting
    /// shartnomasi, ma'lumotnoma esa faqat sharoit.
    /// </para>
    /// <para>
    /// ⚠️ Bu ikkovi endi VAKANSIYA uchun emas, TAKLIFNI QABUL QILISH
    /// uchun kerak: vakansiyada lavozim ham, firma ham yo'q (sababi
    /// <c>Vacancy</c> domen izohida). Shu sababli uni faqat to'liq
    /// voronka testi chaqiradi — qolganlari vakansiya ochish uchun
    /// yolg'iz e'lon NOMIDAN foydalanadi.
    /// </para>
    /// </remarks>
    private async Task<(Guid LegalEntityId, Guid PositionId)> MalumotnomaAsync()
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var belgi = Belgi();

        var firma = LegalEntity.Create("Рекрутинг Тест МЧЖ " + belgi).Value;
        var lavozim = Position.Create("Сотувчи " + belgi).Value;

        kontekst.Add(firma);
        kontekst.Add(lavozim);

        await kontekst.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (firma.Id, lavozim.Id);
    }

    /// <summary>Voronka bosqichlari.</summary>
    /// <remarks>
    /// <paramref name="onlyActive"/> ATAYLAB parametr: qayta tartiblash
    /// BARCHA bosqichlarni talab qiladi (faolsizlantirilganlari ham), qolgan
    /// testlar esa faqat faol voronkani ko'radi. Bu farq bir marta shu
    /// testlarni yiqitgan — boshqa test o'z bosqichini faolsizlantirgach,
    /// «faqat faollar» ro'yxati to'liq bo'lmay qolgan.
    /// </remarks>
    private static async Task<IReadOnlyList<StageResponse>> BosqichlarAsync(
        HttpClient mijoz,
        bool onlyActive = true)
    {
        using var javob = await mijoz.GetAsync(
            new Uri($"/api/recruiting/stages?onlyActive={onlyActive.ToString().ToLowerInvariant()}", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var bosqichlar = await javob.Content.ReadFromJsonAsync<List<StageResponse>>(
            TestContext.Current.CancellationToken);

        bosqichlar.ShouldNotBeNull();
        return bosqichlar;
    }

    /// <summary>
    /// Vakansiya nomini MA'LUMOTNOMA ENDPOINT'i orqali yaratadi.
    /// </summary>
    /// <remarks>
    /// Yuridik shaxs va lavozimdan farqli, bu yozuv bazaga to'g'ridan-to'g'ri
    /// QO'YILMAYDI: aynan shu marshrut («/api/catalog/vacancy-titles»)
    /// bu topshiriqda YANGI qo'shilgan va uning ishlashi ham
    /// tekshirilishi kerak — ma'lumotnoma umumiy quvurdan to'g'ri
    /// o'tayotganini boshqa hech narsa ko'rsatmaydi.
    /// </remarks>
    private static async Task<VacancyTitleResponse> VakansiyaNomiAsync(HttpClient mijoz)
    {
        using var javob = await mijoz.PostAsJsonAsync(
            new Uri("/api/catalog/vacancy-titles", UriKind.Relative),
            new { name = "Сотувчи-консультант " + Belgi() },
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var nom = await javob.Content.ReadFromJsonAsync<VacancyTitleResponse>(
            TestContext.Current.CancellationToken);

        nom.ShouldNotBeNull();
        nom.IsActive.ShouldBeTrue();
        return nom;
    }

    private static async Task<VacancyResponse> VakansiyaAsync(HttpClient mijoz, Guid nomId)
    {
        using var javob = await mijoz.PostAsJsonAsync(
            new Uri("/api/recruiting/vacancies", UriKind.Relative),
            new
            {
                vacancyTitleId = nomId,
                openings = 2,
                openedAt = new DateOnly(2026, 3, 1),
                note = "Шошилинч",
            },
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var vakansiya = await javob.Content.ReadFromJsonAsync<VacancyResponse>(
            TestContext.Current.CancellationToken);

        vakansiya.ShouldNotBeNull();
        return vakansiya;
    }

    private static async Task<CandidateResponse> NomzodAsync(
        HttpClient mijoz,
        Guid vakansiyaId,
        Guid? bosqichId = null,
        string familiya = "Каримов")
    {
        using var javob = await mijoz.PostAsJsonAsync(
            new Uri("/api/recruiting/candidates", UriKind.Relative),
            new
            {
                vacancyId = vakansiyaId,
                lastName = familiya,
                firstName = "Азиз",
                phone = "905088888",
                speciality = "Иқтисодчи",
                birthDate = new DateOnly(1995, 5, 20),
                stageId = bosqichId,
            },
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var nomzod = await javob.Content.ReadFromJsonAsync<CandidateResponse>(
            TestContext.Current.CancellationToken);

        nomzod.ShouldNotBeNull();
        return nomzod;
    }

    private static async Task<OfferResponse> TaklifAsync(HttpClient mijoz, Guid nomzodId)
    {
        using var javob = await mijoz.PostAsJsonAsync(
            new Uri("/api/recruiting/offers", UriKind.Relative),
            new
            {
                candidateId = nomzodId,
                salaryAmount = 6_000_000m,
                startDate = new DateOnly(2026, 4, 15),
                sentAt = new DateOnly(2026, 4, 5),
                expiresAt = new DateOnly(2026, 4, 12),
            },
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var taklif = await javob.Content.ReadFromJsonAsync<OfferResponse>(
            TestContext.Current.CancellationToken);

        taklif.ShouldNotBeNull();
        return taklif;
    }

    private static async Task<CandidatePageResponse> NomzodlarAsync(HttpClient mijoz, string sorov)
    {
        using var javob = await mijoz.GetAsync(
            new Uri("/api/recruiting/candidates" + sorov, UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var sahifa = await javob.Content.ReadFromJsonAsync<CandidatePageResponse>(
            TestContext.Current.CancellationToken);

        sahifa.ShouldNotBeNull();
        return sahifa;
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
