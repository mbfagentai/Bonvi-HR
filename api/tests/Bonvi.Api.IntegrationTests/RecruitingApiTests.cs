using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bonvi.Application.Identity.Abstractions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Organization;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.Settings;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Voronka bosqichi — testga kerakli qismi.</summary>
public sealed record StageResponse(Guid Id, string Name, bool IsActive, int SortOrder);

/// <summary>Vakansiya — testga kerakli qismi.</summary>
/// <remarks>
/// Lavozim, bo'lim va firma maydonlari YO'Q: vakansiya faqat e'lon
/// (sababi <c>Vacancy</c> domen izohida). Shu sababli javobda ham faqat
/// e'lon nomi bor.
/// </remarks>
public sealed record VacancyResponse(
    Guid Id,
    string Title,
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
    string? Conclusion,
    DateTimeOffset RecordedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>Nomzodning amal izohi (bosqichga bog'lanmagan qayd).</summary>
/// <remarks>
/// <c>Kind</c> ATAYLAB satr, sanoq emas: interfeys qatorning yorlig'ini
/// aynan shu nom bo'yicha to'rt tilda chizadi. Test sim ustidagi
/// QIYMATNI qadaydi, C# turini emas.
/// </remarks>
public sealed record CandidateNoteResponse(
    Guid Id,
    string Kind,
    string Text,
    Guid? CreatedBy,
    DateTimeOffset CreatedAt);

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

    // Rezerv testiga kerak: rezervga o'tkazish ularni to'ldiradi,
    // voronkaga qaytarish esa TOZALAYDI.
    DateOnly? ClosedAt,
    string? ClosingNote,

    // Suhbat vaqti — «faqat ko'rish» ruxsati testiga kerak: rad etilgan
    // taklif kartada iz qoldirmasligi kerak.
    DateTimeOffset? InterviewAt,

    // Keyingi aloqa sanasi va amal izohlari — yangi maydonlar.
    DateOnly? NextContactDate,
    IReadOnlyList<CandidateNoteResponse> Notes);

/// <summary>Sahifalangan vakansiyalar ro'yxati.</summary>
public sealed record VacancyPageResponse(IReadOnlyList<VacancyResponse> Items, int Total);

/// <summary>Nomzodlar ro'yxatining qatori.</summary>
/// <remarks>
/// <paramref name="HasResume"/> va <paramref name="NextContactDate"/> —
/// yangi ustunlar. Birinchisi endi «kamida BITTA fayl biriktirilganmi»
/// degan ma'noda (nomi ataylab o'zgarmadi: uni o'zgartirish TypeScript
/// o'quvchisini va anketa testlarini buzardi).
/// </remarks>
public sealed record CandidateRowResponse(
    Guid Id,
    string FullName,
    string? Phone,
    string Status,
    Guid? CurrentStageId,
    Guid? HiredEmployeeId,
    bool HasResume,
    DateOnly? NextContactDate);

/// <summary>«Bu odam yangi anketa topshira oladimi» javobi.</summary>
/// <remarks>
/// <c>Reason</c> ATAYLAB satr, sanoq emas: bot javobni aynan shu nomlar
/// bilan o'qiydi va nom o'zgarsa u to'siqni tanimay qolardi — ya'ni
/// jimgina hammani o'tkazib yuborardi. Test sim ustidagi QIYMATNI
/// qadaydi, C# turini emas.
/// </remarks>
public sealed record ApplicationCheckResponse(
    bool Allowed,
    string Reason,
    DateTimeOffset? AppliedAt,
    string? VacancyTitle,
    string? StageName,
    DateOnly? AvailableFrom);

/// <summary>Sahifalangan nomzodlar ro'yxati.</summary>
public sealed record CandidatePageResponse(IReadOnlyList<CandidateRowResponse> Items, int Total);

/// <summary>Ishga qabul natijasi — «accept» javobidagi qism.</summary>
public sealed record RecruitingHireResponse(HireEmploymentResponse Employment);

/// <summary>«Nomzod ishga olindi» javobining to'liq konverti.</summary>
public sealed record DirectHireResponse(
    CandidateResponse Candidate,
    RecruitingHireResponse Hire);

/// <summary>
/// Rekruting (2-bosqich) — uchi-uchiga: vakansiya → nomzod → voronka →
/// 1-bosqichning ishga qabul oqimi.
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

    /// <summary>
    /// Ishga OLINMAGAN nomzodda «xodim kartasi» havolasi <c>null</c>.
    /// </summary>
    /// <remarks>
    /// ⚠️ Ro'yxat bu maydonni lug'atdan oladi va <c>GetValueOrDefault</c>
    /// topilmaganda <c>Guid.Empty</c> qaytarardi — javobda
    /// «00000000-…» chiqib turardi. Bugun ro'yxat maydonni CHIZMAYDI,
    /// ya'ni xato ko'rinmaydi; unga «xodim kartasi» havolasi qo'shilgan
    /// kuni esa u rad etilgan nomzodda ham paydo bo'lib, mavjud
    /// bo'lmagan xodimga olib borardi.
    /// </remarks>
    [Fact]
    public async Task Ishga_olinmagan_nomzodda_xodim_havolasi_bosh()
    {
        using var mijoz = await AdminMijozAsync();

        var nom = VakansiyaNomi();
        var vakansiya = await VakansiyaAsync(mijoz, nom);
        var nomzod = await NomzodAsync(mijoz, vakansiya.Id);

        var royxat = await NomzodlarAsync(mijoz, $"?vacancyId={vakansiya.Id}&status=InProgress");

        royxat.Items
            .First(qator => qator.Id == nomzod.Id)
            .HiredEmployeeId
            .ShouldBeNull("ishga olinmagan nomzodda xodim havolasi BO'SH bo'lishi kerak");
    }

    /// <summary>
    /// ⭐ Bosqich tarixi — HAR BIR ALOQA alohida qator.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Foydalanuvchi talabi (2026-09-16): «besh marta gaplashilsa, beshta
    /// holat bo'lishi mumkin». Ilgari server ayni bosqich qayta
    /// yozilganda mavjud qatorni TO'LDIRARDI va u bilan birga oldingi
    /// izohni ham o'chirardi — HR yozgan gap ogohlantirishsiz yo'qolardi.
    /// </para>
    /// <para>
    /// Test aynan shu yo'lni qaytaradi: xulosa bilan yozish, keyin
    /// XULOSASIZ qayta yozish (voronkadagi sudrash aynan shunday so'rov
    /// yuboradi) va eski qatorni tuzatish.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Ayni_bosqich_qayta_yozilsa_tarix_TOPLANADI()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var bosqichlar = await BosqichlarAsync(mijoz);
        var kengash = bosqichlar.First(b => b.Name == "Кенгаш");
        var vakansiya = await VakansiyaAsync(mijoz, VakansiyaNomi());
        var nomzod = await NomzodAsync(mijoz, vakansiya.Id, familiya: "Такрорий");

        async Task<CandidateResponse> YozAsync(string? xulosa)
        {
            using var javob = await mijoz.PostAsJsonAsync(
                new Uri($"/api/recruiting/candidates/{nomzod.Id}/stages", UriKind.Relative),
                new { stageId = kengash.Id, conclusion = xulosa },
                bekorQilish);

            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
            var karta = await javob.Content.ReadFromJsonAsync<CandidateResponse>(bekorQilish);
            karta.ShouldNotBeNull();
            return karta;
        }

        await YozAsync("Биринчи қўнғироқ: эртага келаман деди");

        // ⚠️ Eng muhim qadam: xulosasiz qayta yozish. Ilgari aynan shu
        // so'rov yuqoridagi izohni NULL bilan almashtirardi.
        await YozAsync(null);

        var uchinchi = await YozAsync("Учинчи қўнғироқ: кенгашга чақирдим");

        uchinchi.Stages.Count.ShouldBe(4, "анкета + учта қўнғироқ — ҳар бири алоҳида қатор");
        uchinchi.Stages[1].Conclusion.ShouldBe(
            "Биринчи қўнғироқ: эртага келаман деди",
            "izohsiz qayta yozish eski izohga TEGMAYDI");
        uchinchi.Stages[2].Conclusion.ShouldBeNull();
        uchinchi.Stages[3].Conclusion.ShouldBe("Учинчи қўнғироқ: кенгашга чақирдим");
        uchinchi.CurrentStageId.ShouldBe(kengash.Id, "joriy holat — OXIRGI yozuvdan");

        // ── Eskiroq qator tuzatiladi: AYNI qator, yangisi qo'shilmaydi ──
        var eski = uchinchi.Stages[1];

        using var tuzatish = await mijoz.PutAsJsonAsync(
            new Uri(
                $"/api/recruiting/candidates/{nomzod.Id}/stages/{eski.Id}",
                UriKind.Relative),
            new
            {
                date = eski.Date,
                conclusion = "Биринчи қўнғироқ: гаплашилди, келишилди",
            },
            bekorQilish);

        tuzatish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(tuzatish));
        var tuzatilgan = await tuzatish.Content.ReadFromJsonAsync<CandidateResponse>(bekorQilish);

        tuzatilgan!.Stages.Count.ShouldBe(4, "tuzatish YANGI qator qo'shmaydi");
        tuzatilgan.Stages[1].Id.ShouldBe(eski.Id, "tuzatilgan qator O'Z O'RNIDA qoladi");
        tuzatilgan.Stages[1].Conclusion.ShouldBe("Биринчи қўнғироқ: гаплашилди, келишилди");
        tuzatilgan.Stages[1].RecordedAt.ShouldBe(
            eski.RecordedAt,
            "kiritilgan vaqt o'zgarmaydi — aks holda qator lentaning tepasiga sakrardi");
        tuzatilgan.Stages[1].UpdatedAt.ShouldNotBeNull("tuzatilgani ko'rinib turadi");
        tuzatilgan.Stages[3].Conclusion.ShouldBe(
            "Учинчи қўнғироқ: кенгашга чақирдим",
            "qolgan qatorlarga tegilmaydi");
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

        // ── 1. Vakansiya ochiladi — nomi o'zida yoziladi ──
        //    ⚠️ E'lon nomi kompaniya lavozimi EMAS: yuqoridagi «lavozimId»
        //    bu yerda umuman ishlatilmaydi, u faqat 5-qadamda — ishga
        //    qabulda kerak bo'ladi.
        var nom = VakansiyaNomi();
        var vakansiya = await VakansiyaAsync(mijoz, nom);
        vakansiya.Status.ShouldBe("Open");
        vakansiya.CandidateCount.ShouldBe(0);
        vakansiya.Title.ShouldBe(nom, "nom qatorda tayyor keladi");

        // ── 2. Nomzod kiritiladi — darhol birinchi bosqichga tushadi ──
        var nomzod = await NomzodAsync(mijoz, vakansiya.Id);
        nomzod.Status.ShouldBe("InProgress");
        nomzod.Source.ShouldBe("Manual");
        nomzod.CurrentStageId.ShouldBe(anketa.Id);
        nomzod.Stages.Count.ShouldBe(1);

        // ── 3. Kengash xulosasi — ODDIY bosqich yozuvi ──
        using var kengashJavobi = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{nomzod.Id}/stages", UriKind.Relative),
            new
            {
                stageId = kengash.Id,
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

        // ── 4. Nomzod ishga olinadi → XODIM yaratiladi ──
        // ⭐ Firma va LAVOZIM aynan shu yerda tanlanadi — vakansiyadan
        //    emas. Bu e'lon bilan shtat jadvali uchrashadigan yagona nuqta.
        using var qabul = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{nomzod.Id}/hire", UriKind.Relative),
            new
            {
                legalEntityId = firmaId,
                positionId = lavozimId,
                orderNo = "185-b",
                orderDate = new DateOnly(2026, 4, 10),
                salaryAmount = 6_000_000m,
                pinfl = (string?)null,
            },
            bekorQilish);

        qabul.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qabul));
        var natija = await qabul.Content.ReadFromJsonAsync<DirectHireResponse>(bekorQilish);

        natija!.Candidate.Status.ShouldBe("Hired");
        natija.Candidate.HiredEmployeeId.ShouldNotBeNull("nomzod qaysi xodimga aylangani ko'rinishi kerak");

        var mehnat = natija.Hire.Employment;
        mehnat.Status.ShouldBe("Probation", "sinov muddati MAJBURIY va istisnosiz");
        mehnat.Probation.ShouldNotBeNull();
        natija.Candidate.HiredEmployeeId.ShouldBe(mehnat.EmployeeId);
        natija.Candidate.HiredEmploymentId.ShouldBe(mehnat.Id);

        // ── 5. Yangi xodim SINOV ro'yxatida ──
        using var sinovlar = await mijoz.GetAsync(
            new Uri("/api/probations?status=InProgress", UriKind.Relative), bekorQilish);

        var sahifa = await sinovlar.Content.ReadFromJsonAsync<ProbationPageResponse>(bekorQilish);
        sahifa!.Items.ShouldContain(
            q => q.EmployeeId == mehnat.EmployeeId,
            "ishga olingan nomzod /sinov ro'yxatida");

        // ── 6. Nomzod voronkada FAOL bo'lib qolmaydi ──
        var faollar = await NomzodlarAsync(mijoz, $"?vacancyId={vakansiya.Id}&status=InProgress");
        faollar.Items.ShouldNotContain(q => q.Id == nomzod.Id);

        var ishgaOlinganlar = await NomzodlarAsync(mijoz, $"?vacancyId={vakansiya.Id}&status=Hired");
        ishgaOlinganlar.Items.ShouldContain(q => q.Id == nomzod.Id && q.HiredEmployeeId == mehnat.EmployeeId);


        // ── 7. Vakansiya sanog'i yangilandi ──
        using var vakansiyaJavobi = await mijoz.GetAsync(
            new Uri($"/api/recruiting/vacancies/{vakansiya.Id}", UriKind.Relative), bekorQilish);

        var yangilangan = await vakansiyaJavobi.Content.ReadFromJsonAsync<VacancyResponse>(bekorQilish);
        yangilangan!.CandidateCount.ShouldBe(1);
        yangilangan.HiredCount.ShouldBe(1);
        yangilangan.Status.ShouldBe("Open", "o'rin to'lganda vakansiya O'ZI yopilmaydi — bu HR qarori");

        // ── 8. Ikkinchi marta qabul qilib bo'lmaydi ──
        using var takror = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{nomzod.Id}/hire", UriKind.Relative),
            new { legalEntityId = firmaId, positionId = lavozimId },
            bekorQilish);

        takror.StatusCode.ShouldBe(HttpStatusCode.Conflict, await MatnAsync(takror));
    }

    [Fact]
    public async Task Nomzod_qoshimcha_maydonlar_bilan_xodimga_aylanadi()
    {
        // Yuqoridagi to'liq voronka testidan farqi: nomzod darhol
        // kengash bosqichida yaratiladi va so'rovda oklad, ishga qabul
        // sanasi ham beriladi — ya'ni formaning to'liq shakli.
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var (firmaId, lavozimId) = await MalumotnomaAsync();
        var bosqichlar = await BosqichlarAsync(mijoz);
        var kengash = bosqichlar.First(b => b.Name == "Кенгаш");

        var vakansiya = await VakansiyaAsync(mijoz, VakansiyaNomi());
        var nomzod = await NomzodAsync(mijoz, vakansiya.Id, kengash.Id);

        using var qabul = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{nomzod.Id}/hire", UriKind.Relative),
            new
            {
                legalEntityId = firmaId,
                positionId = lavozimId,
                hireDate = new DateOnly(2026, 4, 15),
                orderNo = "186-b",
                orderDate = new DateOnly(2026, 4, 10),
                salaryAmount = 6_000_000m,
            },
            bekorQilish);

        qabul.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qabul));
        var natija = await qabul.Content.ReadFromJsonAsync<DirectHireResponse>(bekorQilish);

        // 1. Nomzod voronkadan chiqdi va xodimga bog'landi
        natija!.Candidate.Status.ShouldBe("Hired");
        natija.Candidate.HiredEmployeeId.ShouldNotBeNull("nomzod qaysi xodimga aylangani ko'rinishi kerak");

        // 2. Xodim, mehnat munosabati va MAJBURIY sinov muddati yaratildi
        var mehnat = natija.Hire.Employment;
        mehnat.Status.ShouldBe("Probation", "sinov muddati MAJBURIY va istisnosiz");
        mehnat.Probation.ShouldNotBeNull();
        natija.Candidate.HiredEmployeeId.ShouldBe(mehnat.EmployeeId);
        natija.Candidate.HiredEmploymentId.ShouldBe(mehnat.Id);

        // 3. Yangi xodim SINOV ro'yxatida — 1-bosqichning oqimi chaqirilgani
        //    aynan shu bilan ko'rinadi.
        using var sinovlar = await mijoz.GetAsync(
            new Uri("/api/probations?status=InProgress", UriKind.Relative), bekorQilish);

        var sahifa = await sinovlar.Content.ReadFromJsonAsync<ProbationPageResponse>(bekorQilish);
        sahifa!.Items.ShouldContain(
            q => q.EmployeeId == mehnat.EmployeeId,
            "ishga olingan xodim /sinov ro'yxatiga tushadi");

        // 4. Voronkada faol qolmaydi, vakansiya sanog'i yangilanadi
        var faollar = await NomzodlarAsync(mijoz, $"?vacancyId={vakansiya.Id}&status=InProgress");
        faollar.Items.ShouldNotContain(q => q.Id == nomzod.Id);

        using var vakansiyaJavobi = await mijoz.GetAsync(
            new Uri($"/api/recruiting/vacancies/{vakansiya.Id}", UriKind.Relative), bekorQilish);

        var yangilangan = await vakansiyaJavobi.Content.ReadFromJsonAsync<VacancyResponse>(bekorQilish);
        yangilangan!.HiredCount.ShouldBe(1);

        // 5. Ikkinchi marta olib bo'lmaydi
        using var takror = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{nomzod.Id}/hire", UriKind.Relative),
            new { legalEntityId = firmaId, positionId = lavozimId },
            bekorQilish);

        takror.StatusCode.ShouldBe(HttpStatusCode.Conflict, await MatnAsync(takror));
        (await MatnAsync(takror)).ShouldContain("nomzod.allaqachon_ishga_olingan");
    }

    [Fact]
    public async Task Ishga_olishda_FIRMA_va_LAVOZIM_majburiy()
    {
        // Xato kodlari `nomzod.*` prefiksida: ular ish taklifi bilan
        // birga o'chirilgan `taklif.*` kodlarining o'rnini bosadi.
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var (firmaId, _) = await MalumotnomaAsync();
        var vakansiya = await VakansiyaAsync(mijoz, VakansiyaNomi());
        var nomzod = await NomzodAsync(mijoz, vakansiya.Id);
        var yol = new Uri($"/api/recruiting/candidates/{nomzod.Id}/hire", UriKind.Relative);

        using var firmasiz = await mijoz.PostAsJsonAsync(yol, new { }, bekorQilish);

        firmasiz.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(firmasiz));
        (await MatnAsync(firmasiz)).ShouldContain("nomzod.yuridik_shaxs_yoq");

        using var lavozimsiz = await mijoz.PostAsJsonAsync(
            yol, new { legalEntityId = firmaId }, bekorQilish);

        lavozimsiz.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(lavozimsiz));
        (await MatnAsync(lavozimsiz)).ShouldContain("nomzod.lavozim_yoq");

        // Nomzod voronkada QOLDI: rad etilgan qabul hech narsani
        // o'zgartirmaydi.
        var karta = await KartaAsync(mijoz, nomzod.Id);
        karta.Status.ShouldBe("InProgress");
    }

    [Fact]
    public async Task Rezerv_nomzodni_SAQLAYDI_va_voronkaga_qaytaradi()
    {
        // ⭐ Rezervning butun ma'nosi qaytishda: «ҳозир олинмади, лекин
        //    керак бўлса чақирамиз». Rad etilganlarga qo'shib yuborilsa,
        //    bu ro'yxat yo'qolardi.
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var bosqichlar = await BosqichlarAsync(mijoz);
        var kengash = bosqichlar.First(b => b.Name == "Кенгаш");

        var vakansiya = await VakansiyaAsync(mijoz, VakansiyaNomi());
        var nomzod = await NomzodAsync(mijoz, vakansiya.Id, kengash.Id);

        // ── 1. Rezervga ──
        using var rezerv = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{nomzod.Id}/reserve", UriKind.Relative),
            new { at = new DateOnly(2026, 4, 2), reason = "Ҳозир ўрин йўқ" },
            bekorQilish);

        rezerv.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(rezerv));
        var rezervda = await rezerv.Content.ReadFromJsonAsync<CandidateResponse>(bekorQilish);

        rezervda!.Status.ShouldBe("Reserve");
        rezervda.ClosedAt.ShouldBe(new DateOnly(2026, 4, 2));
        rezervda.ClosingNote.ShouldBe("Ҳозир ўрин йўқ");
        rezervda.CurrentStageId.ShouldBe(kengash.Id, "bosqich saqlanadi");

        // ── 2. Voronka taxtasida yo'q, lekin FILTR bilan topiladi ──
        var faollar = await NomzodlarAsync(mijoz, $"?vacancyId={vakansiya.Id}&status=InProgress");
        faollar.Items.ShouldNotContain(q => q.Id == nomzod.Id);

        var rezervdagilar = await NomzodlarAsync(mijoz, $"?vacancyId={vakansiya.Id}&status=Reserve");
        rezervdagilar.Items.ShouldContain(
            q => q.Id == nomzod.Id,
            "rezervdagi nomzod aynan shu filtr uchun saqlanadi");

        // ── 3. Voronkaga qaytariladi ──
        using var qaytarish = await mijoz.PostAsync(
            new Uri($"/api/recruiting/candidates/{nomzod.Id}/return", UriKind.Relative),
            content: null,
            bekorQilish);

        qaytarish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qaytarish));
        var qaytgan = await qaytarish.Content.ReadFromJsonAsync<CandidateResponse>(bekorQilish);

        qaytgan!.Status.ShouldBe("InProgress");
        qaytgan.ClosedAt.ShouldBeNull("nomzod endi voronkadan chiqqan emas");
        qaytgan.ClosingNote.ShouldBeNull();
        qaytgan.CurrentStageId.ShouldBe(kengash.Id, "qayerda to'xtagan bo'lsa, o'sha yerdan davom etadi");
        qaytgan.Stages.Count.ShouldBe(nomzod.Stages.Count, "bosqichlar tarixi saqlanadi");

        var yanaFaollar = await NomzodlarAsync(mijoz, $"?vacancyId={vakansiya.Id}&status=InProgress");
        yanaFaollar.Items.ShouldContain(q => q.Id == nomzod.Id);

        // ── 4. Ikkinchi marta qaytarib bo'lmaydi: u allaqachon voronkada ──
        using var takror = await mijoz.PostAsync(
            new Uri($"/api/recruiting/candidates/{nomzod.Id}/return", UriKind.Relative),
            content: null,
            bekorQilish);

        takror.StatusCode.ShouldBe(HttpStatusCode.Conflict, await MatnAsync(takror));
        (await MatnAsync(takror)).ShouldContain("nomzod.qaytarilmaydi");
    }

    /// <summary>
    /// ⭐ RAD ETILGAN nomzod sabab bilan voronkaga qaytariladi.
    /// </summary>
    /// <remarks>
    /// Foydalanuvchi talabi (2026-09-16): rad etilgan odam ko'pincha o'zi
    /// qayta aloqaga chiqadi. Ilgari yagona yo'l uni NOLDAN kiritish edi —
    /// o'shanda bosqichlar tarixi, anketa va fayllar yangi yozuvda
    /// bo'lmasdi.
    /// </remarks>
    [Fact]
    public async Task Rad_etilgan_nomzod_SABAB_bilan_voronkaga_qaytadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var bosqichlar = await BosqichlarAsync(mijoz);
        var kengash = bosqichlar.First(b => b.Name == "Кенгаш");
        var vakansiya = await VakansiyaAsync(mijoz, VakansiyaNomi());
        var nomzod = await NomzodAsync(mijoz, vakansiya.Id, familiya: "Қайтган");

        using (var bosqich = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{nomzod.Id}/stages", UriKind.Relative),
            new { stageId = kengash.Id, conclusion = "Кенгаш маъқулламади" },
            bekorQilish))
        {
            bosqich.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(bosqich));
        }

        using (var rad = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{nomzod.Id}/reject", UriKind.Relative),
            new { reason = "Тажрибаси етарли эмас" },
            bekorQilish))
        {
            rad.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(rad));
        }

        // ── Sababsiz qaytarish RAD ETILADI ──
        using (var sababsiz = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{nomzod.Id}/return", UriKind.Relative),
            new { note = (string?)null },
            bekorQilish))
        {
            sababsiz.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(sababsiz));
            (await MatnAsync(sababsiz)).ShouldContain("nomzod.qaytarish_sababi_yoq");
        }

        // ── Sabab bilan — qaytadi ──
        using (var qaytarish = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{nomzod.Id}/return", UriKind.Relative),
            new { note = "Ўзи қайта мурожаат қилди, тажриба орттирган" },
            bekorQilish))
        {
            qaytarish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qaytarish));
        }

        var karta = await KartaAsync(mijoz, nomzod.Id);

        karta.Status.ShouldBe("InProgress");
        karta.ClosedAt.ShouldBeNull();
        karta.ClosingNote.ShouldBeNull("chiqish sababi endi o'rinsiz");
        karta.CurrentStageId.ShouldBe(kengash.Id, "qayerda to'xtagan bo'lsa, o'sha yerdan");
        karta.Stages.Count.ShouldBe(2, "bosqichlar tarixi butunligicha saqlanadi");
        karta.Stages[^1].Conclusion.ShouldBe("Кенгаш маъқулламади");

        /*
          ⭐ IKKALA sabab ham lentada: rad etishniki va qaytarishniki.
          `ClosingNote` tozalangani uchun «nega rad etilgan edi» degan
          savolning javobi FAQAT shu yerda qoladi.
        */
        karta.Notes.Count.ShouldBe(2);
        karta.Notes[0].Kind.ShouldBe("Rejected");
        karta.Notes[0].Text.ShouldBe("Тажрибаси етарли эмас");
        karta.Notes[1].Kind.ShouldBe("ReturnToFunnel");
        karta.Notes[1].Text.ShouldBe("Ўзи қайта мурожаат қилди, тажриба орттирган");

        // Ro'yxatda ham faol bo'lib ko'rinadi.
        var faollar = await NomzodlarAsync(mijoz, $"?vacancyId={vakansiya.Id}&status=InProgress");
        faollar.Items.ShouldContain(q => q.Id == nomzod.Id);
    }

    [Fact]
    public async Task Voronkadan_chiqqan_nomzod_rezervga_otkazilmaydi()
    {
        // Rezerv ham YAKUN: rad etilgan nomzodni «aslida rezerv edi» deb
        // o'zgartirish qarorni keyinchalik qayta yozish bo'lardi.
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var vakansiya = await VakansiyaAsync(mijoz, VakansiyaNomi());
        var nomzod = await NomzodAsync(mijoz, vakansiya.Id);

        using var rad = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{nomzod.Id}/reject", UriKind.Relative),
            new { reason = "Тажрибаси етарли эмас" },
            bekorQilish);

        rad.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(rad));

        using var rezerv = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{nomzod.Id}/reserve", UriKind.Relative),
            new { reason = "Ҳозир ўрин йўқ" },
            bekorQilish);

        rezerv.StatusCode.ShouldBe(HttpStatusCode.Conflict, await MatnAsync(rezerv));
        (await MatnAsync(rezerv)).ShouldContain("nomzod.jarayonda_emas");

        var karta = await KartaAsync(mijoz, nomzod.Id);
        karta.Status.ShouldBe("Rejected", "holat o'zgarmaydi");
        karta.ClosingNote.ShouldBe("Тажрибаси етарли эмас", "rad etish izohi tegilmaydi");
    }

    [Fact]
    public async Task Rad_etilgan_nomzod_SUKUT_royxatda_KORINMAYDI()
    {
        /*
          ⭐ Foydalanuvchi qarori (2026-09-10) MAVJUD qoidani KENGAYTIRDI:
             endi «o'zi voz kechgan» (`Withdrawn`) ham kundalik ro'yxatdan
             CHIQADI. Ilgari u ro'yxatda qolardi va sababi «kam uchraydi»
             deb yozilgan edi — amalda bu to'g'ri chiqmadi.

          ⚠️ Ikkala holat AYRIM bo'lib qoladi: belgisi ham, voronka
             hisoboti ham alohida. Faqat RO'YXATDA ko'rinishi umumiy va u
             `?rejected=true` BO'LIMI orqali ochiladi (`?status=…` ham
             avvalgidek ishlaydi).

          ⚠️ Yozuv O'CHIRILMAYDI: u kelasi izlanishning bazasi.
        */
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var vakansiya = await VakansiyaAsync(mijoz, VakansiyaNomi());

        var jarayonda = await NomzodAsync(mijoz, vakansiya.Id, familiya: "Жараёнда");
        var vozKechgan = await NomzodAsync(mijoz, vakansiya.Id, familiya: "Возкечган");
        var radEtilgan = await NomzodAsync(mijoz, vakansiya.Id, familiya: "Радэтилган");

        // Excel'dan ko'chirilgan kengash arxivi — pastdagi 5-bo'lim uchun.
        var arxivRad = await NomzodAsync(
            mijoz, vakansiya.Id, familiya: "Арxиврад", manba: "Excel");
        var arxivVoz = await NomzodAsync(
            mijoz, vakansiya.Id, familiya: "Арxиввоз", manba: "Excel");

        foreach (var id in new[] { vozKechgan.Id, arxivVoz.Id })
        {
            using var vozKechish = await mijoz.PostAsJsonAsync(
                new Uri($"/api/recruiting/candidates/{id}/withdraw", UriKind.Relative),
                new { reason = "Бошқа жойга ишга кирди" },
                bekorQilish);

            vozKechish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(vozKechish));
        }

        foreach (var id in new[] { radEtilgan.Id, arxivRad.Id })
        {
            using var rad = await mijoz.PostAsJsonAsync(
                new Uri($"/api/recruiting/candidates/{id}/reject", UriKind.Relative),
                new { reason = "Тажрибаси етарли эмас" },
                bekorQilish);

            rad.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(rad));
        }

        // ── 1. SUKUT ro'yxat: IKKALASI ham yo'q ──
        var sukut = await NomzodlarAsync(mijoz, $"?vacancyId={vakansiya.Id}");

        sukut.Items.ShouldContain(q => q.Id == jarayonda.Id);
        sukut.Items.ShouldNotContain(q => q.Id == radEtilgan.Id);
        sukut.Items.ShouldNotContain(
            q => q.Id == vozKechgan.Id,
            "voz kechgan odam «hozir kim ish ustida?» degan savolga javob emas");

        // Sanoq qatorlarga MOS: ikkalasi bitta so'rovdan olinadi.
        sukut.Total.ShouldBe(sukut.Items.Count, "sanoq va qatorlar bir-biriga zid bo'lmasligi kerak");
        sukut.Total.ShouldBe(1, "arxiv qatorlari ham sukut ro'yxatga tushmaydi");

        // ── 2. «Rad etilganlar» BO'LIMI: ikkalasi birga ──
        var bolim = await NomzodlarAsync(mijoz, $"?vacancyId={vakansiya.Id}&rejected=true");

        bolim.Items.ShouldContain(q => q.Id == radEtilgan.Id);
        bolim.Items.ShouldContain(q => q.Id == vozKechgan.Id, "bo'lim ikkala holatni BIRGA ko'rsatadi");
        bolim.Items.ShouldNotContain(q => q.Id == jarayonda.Id, "jarayondagi nomzod bo'limda turmaydi");
        bolim.Total.ShouldBe(bolim.Items.Count);
        bolim.Total.ShouldBe(
            2,
            "bo'lim Excel arxivini O'ZI tortmaydi — arxivning kirish yo'li «source=Excel»");

        // Holatlar bo'limda ham AYRIM ko'rinadi — belgilar shunga qarab chiziladi.
        bolim.Items.First(q => q.Id == radEtilgan.Id).Status.ShouldBe("Rejected");
        bolim.Items.First(q => q.Id == vozKechgan.Id).Status.ShouldBe("Withdrawn");

        // ── 3. Bo'lim + holat tanlovi: faqat voz kechganlar ──
        var faqatVoz = await NomzodlarAsync(
            mijoz, $"?vacancyId={vakansiya.Id}&rejected=true&status=Withdrawn");

        faqatVoz.Items.ShouldContain(q => q.Id == vozKechgan.Id);
        faqatVoz.Items.ShouldNotContain(q => q.Id == radEtilgan.Id);
        faqatVoz.Total.ShouldBe(1);

        // ── 4. Holat yolg'iz ham avvalgidek ishlaydi ──
        var holat = await NomzodlarAsync(mijoz, $"?vacancyId={vakansiya.Id}&status=Withdrawn");

        holat.Items.ShouldContain(q => q.Id == vozKechgan.Id);
        holat.Items.ShouldNotContain(q => q.Id == radEtilgan.Id);
        holat.Total.ShouldBe(1, "arxiv «status» tanlovidan ham o'z-o'zidan ochilmaydi");

        // ── 5. Excel arxivi: IKKALA holat ham qaytadi (ARXIV IMTIYOZI) ──
        //
        //    Kengash arxividagi yozuvlarning deyarli hammasi yopilgan. Yangi
        //    «voz kechganlarni yashirish» qoidasi arxivga ham tegsa, «Excel
        //    arxivi» ni tanlagan HR bo'm-bo'sh ro'yxat olardi.
        var arxiv = await NomzodlarAsync(mijoz, $"?vacancyId={vakansiya.Id}&source=Excel");

        arxiv.Items.ShouldContain(q => q.Id == arxivRad.Id);
        arxiv.Items.ShouldContain(q => q.Id == arxivVoz.Id, "imtiyoz IKKALA holatni ham qamraydi");
        arxiv.Items.ShouldNotContain(q => q.Id == jarayonda.Id, "manba filtri qo'llanadi");
        arxiv.Total.ShouldBe(2);

        // ── 6. Yozuv O'CHIRILMAGAN: karta ochilaveradi ──
        var karta = await KartaAsync(mijoz, radEtilgan.Id);
        karta.Status.ShouldBe("Rejected");
        karta.ClosingNote.ShouldBe("Тажрибаси етарли эмас", "izoh ICHKI — nomzodga bormaydi");

        var vozKarta = await KartaAsync(mijoz, vozKechgan.Id);
        vozKarta.Status.ShouldBe("Withdrawn", "holat RAD ETISHGA aylanib qolmaydi");
    }

    [Fact]
    public async Task Aloqa_sanasi_KELGAN_nomzodlar_filtr_bilan_qaytadi()
    {
        // «Bugun kim bilan gaplashish kerak» — bu maydonning YAGONA ekrandagi
        // ma'nosi (bildirishnoma ham, fon vazifasi ham ataylab yo'q).
        using var mijoz = await AdminMijozAsync();

        var bosqichlar = await BosqichlarAsync(mijoz);
        var anketa = bosqichlar[0];
        var vakansiya = await VakansiyaAsync(mijoz, VakansiyaNomi());

        var otgan = await NomzodAsync(mijoz, vakansiya.Id, anketa.Id, familiya: "Ўтган");
        var kelasi = await NomzodAsync(mijoz, vakansiya.Id, anketa.Id, familiya: "Келаси");
        var sanasiz = await NomzodAsync(mijoz, vakansiya.Id, anketa.Id, familiya: "Санасиз");

        // Chegara sanadan 30 kun narida: server «bugun» ni Toshkent
        // kalendarida hisoblaydi, test esa UTC da — bir kunlik farq
        // javobga ta'sir qilmasligi kerak.
        await AloqaSanasiniQoyAsync(mijoz, otgan.Id, anketa.Id, Bugun().AddDays(-30));
        await AloqaSanasiniQoyAsync(mijoz, kelasi.Id, anketa.Id, Bugun().AddDays(30));

        // ── 1. Filtr: faqat muddati kelganlar ──
        var kelganlar = await NomzodlarAsync(mijoz, $"?vacancyId={vakansiya.Id}&contactDue=true");

        kelganlar.Items.ShouldContain(q => q.Id == otgan.Id);
        kelganlar.Items.ShouldNotContain(q => q.Id == kelasi.Id, "kelasi sana hali kelmagan");
        kelganlar.Items.ShouldNotContain(q => q.Id == sanasiz.Id, "sanasi yo'q nomzod bu ro'yxatda emas");
        kelganlar.Total.ShouldBe(kelganlar.Items.Count);
        kelganlar.Total.ShouldBe(1);
        kelganlar.Items[0].NextContactDate.ShouldBe(Bugun().AddDays(-30), "ustun qatorda ham qaytadi");

        // ── 2. Bayroqsiz (va «false» bilan) — filtr YO'Q ──
        var hammasi = await NomzodlarAsync(mijoz, $"?vacancyId={vakansiya.Id}");
        hammasi.Total.ShouldBe(3);

        var ochiq = await NomzodlarAsync(mijoz, $"?vacancyId={vakansiya.Id}&contactDue=false");
        ochiq.Total.ShouldBe(3, "«false» — filtr yo'q, bo'sh ro'yxat emas");

        var karta = await KartaAsync(mijoz, kelasi.Id);
        karta.NextContactDate.ShouldBe(Bugun().AddDays(30), "kartada ham shu sana turadi");
    }

    [Fact]
    public async Task Aloqa_sanasi_boyicha_saralashda_SANASIZ_qatorlar_OXIRIDA()
    {
        /*
          ⭐ PostgreSQL NULL'ni o'sish tartibida oxiriga, KAMAYISH tartibida
             esa BOSHIGA qo'yadi. Ya'ni «keyingi aloqa bo'yicha, yangidan
             eskiga» so'rovi bir ekran chiziqcha bilan ochilardi va HR
             ro'yxatning boshida hech narsa ko'rmasdi. Shu sababli tartib
             ikkala yo'nalishda ham ATAYLAB «sanasi yo'qlar oxirida».
        */
        using var mijoz = await AdminMijozAsync();

        var bosqichlar = await BosqichlarAsync(mijoz);
        var anketa = bosqichlar[0];
        var vakansiya = await VakansiyaAsync(mijoz, VakansiyaNomi());

        var erta = await NomzodAsync(mijoz, vakansiya.Id, anketa.Id, familiya: "Эрта");
        var kech = await NomzodAsync(mijoz, vakansiya.Id, anketa.Id, familiya: "Кеч");
        var sanasiz = await NomzodAsync(mijoz, vakansiya.Id, anketa.Id, familiya: "Санасиз");

        await AloqaSanasiniQoyAsync(mijoz, erta.Id, anketa.Id, Bugun().AddDays(-20));
        await AloqaSanasiniQoyAsync(mijoz, kech.Id, anketa.Id, Bugun().AddDays(-10));

        var osish = await NomzodlarAsync(mijoz, $"?vacancyId={vakansiya.Id}&sort=nextContact");

        osish.Items.Count.ShouldBe(3);
        osish.Items[0].Id.ShouldBe(erta.Id);
        osish.Items[1].Id.ShouldBe(kech.Id);
        osish.Items[2].Id.ShouldBe(sanasiz.Id, "sanasi yo'q qator OXIRIDA");

        var kamayish = await NomzodlarAsync(mijoz, $"?vacancyId={vakansiya.Id}&sort=-nextContact");

        kamayish.Items.Count.ShouldBe(3);
        kamayish.Items[0].Id.ShouldBe(kech.Id);
        kamayish.Items[1].Id.ShouldBe(erta.Id);
        kamayish.Items[2].Id.ShouldBe(sanasiz.Id, "teskari tartibda HAM oxirida");
    }

    /// <summary>Voronkada kartani sudrab o'tkazish aloqa sanasini o'chirmasligi kerak.</summary>
    /// <remarks>
    /// ⭐ Sudrash oynasiz ishlaydi va so'rovda faqat bosqich bo'ladi. «null —
    /// tozalash» shartnomasida u HR besh daqiqa oldin qo'ygan qo'ng'iroq
    /// rejasini JIMGINA o'chirib tashlardi. Shu sababli shartnomada ikkita
    /// alohida maydon bor va bu test uchala holatni sim ustida qadaydi.
    /// </remarks>
    [Fact]
    public async Task Sudrab_otkazish_aloqa_sanasiga_TEGMAYDI()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var bosqichlar = await BosqichlarAsync(mijoz);
        var anketa = bosqichlar[0];
        var kengash = bosqichlar.First(b => b.Name == "Кенгаш");
        var vakansiya = await VakansiyaAsync(mijoz, VakansiyaNomi());
        var nomzod = await NomzodAsync(mijoz, vakansiya.Id, anketa.Id);

        // ── 1. Sana qo'yildi ──
        await AloqaSanasiniQoyAsync(mijoz, nomzod.Id, anketa.Id, Bugun().AddDays(7));
        (await KartaAsync(mijoz, nomzod.Id)).NextContactDate.ShouldBe(Bugun().AddDays(7));

        // ── 2. SUDRASH: so'rovda faqat bosqich — sana TEGILMAYDI ──
        using (var sudrash = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{nomzod.Id}/stages", UriKind.Relative),
            new { stageId = kengash.Id },
            bekorQilish))
        {
            sudrash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(sudrash));

            var keyin = await sudrash.Content.ReadFromJsonAsync<CandidateResponse>(bekorQilish);
            keyin!.CurrentStageId.ShouldBe(kengash.Id, "bosqich baribir o'zgaradi");
            keyin.NextContactDate.ShouldBe(Bugun().AddDays(7), "sana JOYIDA qoladi");
        }

        // ── 3. Bayroq ATAYLAB yuborilsa — tozalanadi ──
        using (var tozalash = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{nomzod.Id}/stages", UriKind.Relative),
            new { stageId = kengash.Id, clearNextContact = true },
            bekorQilish))
        {
            tozalash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(tozalash));

            var keyin = await tozalash.Content.ReadFromJsonAsync<CandidateResponse>(bekorQilish);
            keyin!.NextContactDate.ShouldBeNull();
        }
    }

    [Fact]
    public async Task Kartada_izohlar_YARATILISH_tartibida_qaytadi()
    {
        // Izoh HECH QACHON mustaqil emas: u amaldan tug'iladi. Shu sababli
        // test uni amallar orqali yozadi va tarixdagi tartibini qadaydi.
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var bosqichlar = await BosqichlarAsync(mijoz);
        var anketa = bosqichlar[0];
        var vakansiya = await VakansiyaAsync(mijoz, VakansiyaNomi());
        var boshqa = await VakansiyaAsync(mijoz, VakansiyaNomi());
        var nomzod = await NomzodAsync(mijoz, vakansiya.Id, anketa.Id);

        // ── 1. Boshqa vakansiyaga o'tkazish ──
        using (var kochirish = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{nomzod.Id}/reassign", UriKind.Relative),
            new { vacancyId = boshqa.Id, note = "Сотувчига мос эмас, кассирга кўчирилди" },
            bekorQilish))
        {
            kochirish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(kochirish));
        }

        // ── 2. Rezerv va rezervdan qaytarish ──
        using (var rezerv = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{nomzod.Id}/reserve", UriKind.Relative),
            new { reason = "Ҳозир ўрин йўқ" },
            bekorQilish))
        {
            rezerv.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(rezerv));
        }

        using (var qaytarish = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{nomzod.Id}/return", UriKind.Relative),
            new { note = "Ўрин бўшади, қайта чақирдим" },
            bekorQilish))
        {
            qaytarish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qaytarish));
        }

        var karta = await KartaAsync(mijoz, nomzod.Id);

        /*
          ⭐ Uchta qator: ko'chirish, REZERVGA o'tkazish va qaytarish.
          O'rtadagisi 2026-09-16 da qo'shildi — chiqish sababi
          `ClosingNote` da turardi va qaytarish uni tozalardi, ya'ni
          «nega rezervga o'tgan edi» degan fakt o'sha lahzada
          yo'qolardi.
        */
        karta.Notes.Count.ShouldBe(3);
        karta.Notes[0].Kind.ShouldBe("Reassign");
        karta.Notes[0].Text.ShouldBe("Сотувчига мос эмас, кассирга кўчирилди");
        karta.Notes[0].CreatedBy.ShouldNotBeNull("«kim yozdi» — audit fakti");
        karta.Notes[1].Kind.ShouldBe("Reserved");
        karta.Notes[1].Text.ShouldBe("Ҳозир ўрин йўқ");
        karta.Notes[2].Kind.ShouldBe("ReturnToFunnel");
        karta.Notes[2].Text.ShouldBe("Ўрин бўшади, қайта чақирдим");
        karta.ClosingNote.ShouldBeNull("qaytarish chiqish izohini TOZALAYDI — izoh esa saqlanib qoldi");
    }

    [Fact]
    public async Task BOSH_izoh_kartada_yozuv_qoldirmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var vakansiya = await VakansiyaAsync(mijoz, VakansiyaNomi());
        var boshqa = await VakansiyaAsync(mijoz, VakansiyaNomi());
        var nomzod = await NomzodAsync(mijoz, vakansiya.Id);

        // Domen bo'sh matnda ISTISNO tashlaydi — tekshiruv tushib qolsa,
        // bu so'rov 500 bilan qaytardi.
        using var kochirish = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{nomzod.Id}/reassign", UriKind.Relative),
            new { vacancyId = boshqa.Id, note = "   " },
            bekorQilish);

        kochirish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(kochirish));

        var karta = await KartaAsync(mijoz, nomzod.Id);
        karta.VacancyId.ShouldBe(boshqa.Id);
        karta.Notes.ShouldBeEmpty("«izoh yozilmadi» — yozuvsiz ko'rinadi");
    }

    [Fact]
    public async Task Ishga_olingan_nomzod_SUKUT_royxatda_KORINMAYDI()
    {
        // ⭐ Foydalanuvchi talabi (2026-09-07): ishga olingan nomzod endi
        //    XODIM va uning o'rni «Xodimlar» bo'limida. Nomzodlar ro'yxati
        //    esa «hozir kim ish ustida?» degan savolga javob beradi.
        //
        //    ⚠️ Yozuv O'CHIRILMAYDI — pastda vakansiya sanog'i buni
        //    ko'rsatadi.
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var (firmaId, lavozimId) = await MalumotnomaAsync();
        var vakansiya = await VakansiyaAsync(mijoz, VakansiyaNomi());

        var jarayonda = await NomzodAsync(mijoz, vakansiya.Id, familiya: "Жараёнда");
        var rezervda = await NomzodAsync(mijoz, vakansiya.Id, familiya: "Резервда");
        var ishgaOlingan = await NomzodAsync(mijoz, vakansiya.Id, familiya: "Ишгаолинган");

        using (var rezerv = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{rezervda.Id}/reserve", UriKind.Relative),
            new { reason = "Ҳозир ўрин йўқ" },
            bekorQilish))
        {
            rezerv.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(rezerv));
        }

        using (var qabul = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{ishgaOlingan.Id}/hire", UriKind.Relative),
            new { legalEntityId = firmaId, positionId = lavozimId },
            bekorQilish))
        {
            qabul.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qabul));
        }

        // ── 1. SUKUT ro'yxat: ishga olingan ham, rezervdagi ham yo'q ──
        var sukut = await NomzodlarAsync(mijoz, $"?vacancyId={vakansiya.Id}");

        sukut.Items.ShouldContain(q => q.Id == jarayonda.Id);
        sukut.Items.ShouldNotContain(
            q => q.Id == rezervda.Id,
            "foydalanuvchi qarori (2026-09-26): rezervdagilar faqat «Rezerv» bo'limida");
        sukut.Items.ShouldNotContain(q => q.Id == ishgaOlingan.Id);

        // ── 2. Sanoq qatorlarga MOS: ikkalasi bitta so'rovdan olinadi ──
        sukut.Total.ShouldBe(
            sukut.Items.Count,
            "sanoq va qatorlar bir-biriga zid bo'lmasligi kerak");
        sukut.Total.ShouldBe(1);

        // «Rezerv» bo'limi aynan shu so'rov bilan ochiladi
        var rezervBolimi = await NomzodlarAsync(mijoz, $"?vacancyId={vakansiya.Id}&status=Reserve");
        rezervBolimi.Items.ShouldContain(q => q.Id == rezervda.Id);

        // ── 3. Filtr ataylab tanlansa — qaytadi ──
        var ishgaOlinganlar = await NomzodlarAsync(mijoz, $"?vacancyId={vakansiya.Id}&status=Hired");

        ishgaOlinganlar.Items.ShouldContain(q => q.Id == ishgaOlingan.Id);
        ishgaOlinganlar.Total.ShouldBe(1);
        ishgaOlinganlar.Items[0].HiredEmployeeId.ShouldNotBeNull("havola joyida qoladi");

        // ── 4. Yozuv O'CHIRILMAGAN: vakansiya sanog'i buni ko'rsatadi ──
        using var vakansiyaJavobi = await mijoz.GetAsync(
            new Uri($"/api/recruiting/vacancies/{vakansiya.Id}", UriKind.Relative), bekorQilish);

        var yangilangan = await vakansiyaJavobi.Content.ReadFromJsonAsync<VacancyResponse>(bekorQilish);
        yangilangan!.CandidateCount.ShouldBe(3, "uchala yozuv ham bazada turibdi");
        yangilangan.HiredCount.ShouldBe(1, "«2/3 to'ldirildi» hisobi shu yozuvga tayanadi");

        // ── 5. Karta ham ochilaveradi — faqat RO'YXATDAN chiqarilgan ──
        var karta = await KartaAsync(mijoz, ishgaOlingan.Id);
        karta.Status.ShouldBe("Hired");
    }

    /// <summary>Yangi uchala amal ham RUXSAT talab qiladi.</summary>
    /// <remarks>
    /// Token HAQIQIY va imzosi to'g'ri — faqat ichida «recruiting.*» yo'q.
    /// Aynan shu holat eng xavflisi: autentifikatsiya o'tadi, avtorizatsiya
    /// esa endpoint darajasida to'xtatishi kerak. «Ishga olish» uchun bu
    /// ayniqsa muhim: u rekrutingdan tashqariga, XODIMLAR bo'limiga yozadi.
    /// </remarks>
    [Fact]
    public async Task Ruxsatsiz_foydalanuvchi_rezerv_va_ishga_olishda_403_oladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        // Nomzod haqiqiy bo'lsin: 403 «yo'q» degani emas, «ruxsat yo'q» degani.
        using var admin = await AdminMijozAsync();
        var (firmaId, lavozimId) = await MalumotnomaAsync();
        var vakansiya = await VakansiyaAsync(admin, VakansiyaNomi());
        var nomzod = await NomzodAsync(admin, vakansiya.Id, familiya: "Ҳимояланган");

        using var mijoz = _factory.CreateClient();
        mijoz.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await RuxsatsizTokenAsync());

        using (var rezerv = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{nomzod.Id}/reserve", UriKind.Relative),
            new { reason = "Ҳозир ўрин йўқ" },
            bekorQilish))
        {
            rezerv.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using (var qaytarish = await mijoz.PostAsync(
            new Uri($"/api/recruiting/candidates/{nomzod.Id}/return", UriKind.Relative),
            content: null,
            bekorQilish))
        {
            qaytarish.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using (var ishgaOlish = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{nomzod.Id}/hire", UriKind.Relative),
            new { legalEntityId = firmaId, positionId = lavozimId },
            bekorQilish))
        {
            ishgaOlish.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        // Ruxsatsiz urinishlar hech narsani o'zgartirmadi.
        var karta = await KartaAsync(admin, nomzod.Id);
        karta.Status.ShouldBe("InProgress");
    }

    /// <summary>O'zgargan yozish marshrutlari FAQAT ko'rish ruxsatiga ochilmaydi.</summary>
    /// <remarks>
    /// <para>
    /// Uchala marshrut ham shu o'zgarishda kengaydi (izoh va keyingi aloqa
    /// sanasi maydonlari). Ruxsat tekshiruvi endpoint darajasida turadi,
    /// ya'ni yangi maydon qo'shilganda uni tushirib qoldirish oson —
    /// va o'shanda ro'yxatni ko'ra oladigan har kim nomzodni voronka
    /// bo'ylab yurgiza olardi.
    /// </para>
    /// <para>
    /// Oxirgi qadam MAJBURIY: o'qish YO'LI ochiq qolishi ham tekshiriladi,
    /// aks holda test shunchaki «hamma yopiq» degan holatdan xursand bo'lib
    /// o'tib ketardi.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Ozgargan_yozish_marshrutlari_FAQAT_korish_ruxsatiga_ochilmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var admin = await AdminMijozAsync();
        var bosqichlar = await BosqichlarAsync(admin);
        var kengash = bosqichlar.First(b => b.Name == "Кенгаш");
        var vakansiya = await VakansiyaAsync(admin, VakansiyaNomi());
        var boshqa = await VakansiyaAsync(admin, VakansiyaNomi());
        var nomzod = await NomzodAsync(admin, vakansiya.Id, familiya: "Ҳимояланган");

        using var koruvchi = _factory.CreateClient();
        koruvchi.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await KoruvchiTokenAsync());

        using (var bosqich = await koruvchi.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{nomzod.Id}/stages", UriKind.Relative),
            new { stageId = kengash.Id, nextContactDate = Bugun().AddDays(3) },
            bekorQilish))
        {
            bosqich.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        // Tarixni TUZATISH ham yozish amali: ko'ruvchi tarixga tegmaydi.
        using (var tuzatish = await koruvchi.PutAsJsonAsync(
            new Uri(
                $"/api/recruiting/candidates/{nomzod.Id}/stages/{nomzod.Stages[0].Id}",
                UriKind.Relative),
            new { conclusion = "Ўзгартириб қўяман" },
            bekorQilish))
        {
            tuzatish.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using (var kochirish = await koruvchi.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{nomzod.Id}/reassign", UriKind.Relative),
            new { vacancyId = boshqa.Id, note = "Кўчириб қўяман" },
            bekorQilish))
        {
            kochirish.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using (var taklif = await koruvchi.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{nomzod.Id}/interview-invite", UriKind.Relative),
            new { interviewAt = DateTimeOffset.UtcNow.AddDays(3), conclusion = "Чақирдим" },
            bekorQilish))
        {
            taklif.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using (var qaytarish = await koruvchi.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{nomzod.Id}/return", UriKind.Relative),
            new { note = "Қайтараман" },
            bekorQilish))
        {
            qaytarish.StatusCode.ShouldBe(
                HttpStatusCode.Forbidden,
                "«rezervda emas» (409) EMAS: ruxsat tekshiruvi qoidadan OLDIN turadi");
        }

        // Hech narsa o'zgarmagan.
        var karta = await KartaAsync(admin, nomzod.Id);
        karta.VacancyId.ShouldBe(vakansiya.Id);
        karta.NextContactDate.ShouldBeNull();
        karta.InterviewAt.ShouldBeNull();
        karta.Notes.ShouldBeEmpty();

        // O'qish yo'li esa AYNAN shu token bilan ochiq.
        using (var royxat = await koruvchi.GetAsync(
            new Uri($"/api/recruiting/candidates?vacancyId={vakansiya.Id}", UriKind.Relative),
            bekorQilish))
        {
            royxat.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(royxat));

            var sahifa = await royxat.Content.ReadFromJsonAsync<CandidatePageResponse>(bekorQilish);
            sahifa!.Items.ShouldContain(q => q.Id == nomzod.Id);
        }
    }

    [Fact]
    public async Task Ochirilgan_bosqichga_otkazilmaydi_lekin_unda_turgan_nomzod_yoqolmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var vakansiya = await VakansiyaAsync(mijoz, VakansiyaNomi());

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

        var vakansiya = await VakansiyaAsync(mijoz, VakansiyaNomi());

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

        var nom = VakansiyaNomi();

        // ── 1. Nomzodi YO'Q vakansiya o'chiriladi ──
        var bosh = await VakansiyaAsync(mijoz, nom);

        using var boshniOchirish = await mijoz.DeleteAsync(
            new Uri($"/api/recruiting/vacancies/{bosh.Id}", UriKind.Relative), bekorQilish);

        boshniOchirish.StatusCode.ShouldBe(
            HttpStatusCode.NoContent,
            "xato kiritilgan yozuv o'chiriladi: " + await MatnAsync(boshniOchirish));

        // ── 2. Nomzodi BOR vakansiya o'chirilmaydi ──
        //    Nom AYNAN o'sha: o'chirilgan vakansiya nomni band qilmaydi —
        //    aks holda xato kiritilgan yozuv nomni abadiy «egallab» olardi.
        var tolgan = await VakansiyaAsync(mijoz, nom);
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

    /// <summary>
    /// ⭐ Vakansiya nomi — MAJBURIY va TAKRORLANMAYDI.
    /// </summary>
    /// <remarks>
    /// 2026-09-29 dan nom vakansiyaning o'zida yoziladi — «Vakansiya
    /// nomlari» ma'lumotnomasi olib tashlandi (sababi <c>Vacancy</c>
    /// domen izohida). Ma'lumotnoma himoya qilgan narsa — bitta e'lonning
    /// «Сотувчи» / «сотувчи » nusxalari — endi shu test bilan qo'riqlanadi.
    /// </remarks>
    [Fact]
    public async Task Vakansiya_nomi_MAJBURIY_va_takrorlanmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        // 1. Nomsiz — va faqat bo'shliqdan iborat nom bilan — ochilmaydi
        foreach (var tana in new object[] { new { openings = 1 }, new { title = "  \t ", openings = 1 } })
        {
            using var nomsiz = await mijoz.PostAsJsonAsync(
                new Uri("/api/recruiting/vacancies", UriKind.Relative), tana, bekorQilish);

            nomsiz.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(nomsiz));
            (await MatnAsync(nomsiz)).ShouldContain("vakansiya.nom_yoq");
        }

        // 2. Bir xil nom — registr va bo'shliqlar boshqacha bo'lsa ham — 409
        var nom = VakansiyaNomi();
        var vakansiya = await VakansiyaAsync(mijoz, nom);
        vakansiya.Title.ShouldBe(nom);

        using (var takror = await mijoz.PostAsJsonAsync(
            new Uri("/api/recruiting/vacancies", UriKind.Relative),
            new { title = "  " + nom.ToUpperInvariant().Replace(" ", "   ", StringComparison.Ordinal) + " " },
            bekorQilish))
        {
            takror.StatusCode.ShouldBe(HttpStatusCode.Conflict, await MatnAsync(takror));
            (await MatnAsync(takror)).ShouldContain("vakansiya.nom_band");
        }

        // 3. YOPILGAN vakansiya ham nomni band qiladi: HR yangisini emas,
        //    o'shani qayta ochishi kerak (nomzodlar tarixi bitta joyda).
        using (var yopish = await mijoz.PostAsync(
            new Uri($"/api/recruiting/vacancies/{vakansiya.Id}/close", UriKind.Relative), null, bekorQilish))
        {
            yopish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yopish));
        }

        using (var yopiqQaytadan = await mijoz.PostAsJsonAsync(
            new Uri("/api/recruiting/vacancies", UriKind.Relative), new { title = nom }, bekorQilish))
        {
            yopiqQaytadan.StatusCode.ShouldBe(HttpStatusCode.Conflict, await MatnAsync(yopiqQaytadan));
        }

        using (var qaytaOchish = await mijoz.PostAsync(
            new Uri($"/api/recruiting/vacancies/{vakansiya.Id}/reopen", UriKind.Relative), null, bekorQilish))
        {
            qaytaOchish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qaytaOchish));
        }

        // 4. Tahrirlashda boshqa vakansiyaning nomini olib bo'lmaydi,
        //    o'z nomini qayta saqlash esa xato emas.
        var boshqa = await VakansiyaAsync(mijoz, VakansiyaNomi());

        using (var nomOlish = await mijoz.PutAsJsonAsync(
            new Uri($"/api/recruiting/vacancies/{boshqa.Id}", UriKind.Relative),
            new { title = nom.ToLowerInvariant(), openings = 1 },
            bekorQilish))
        {
            nomOlish.StatusCode.ShouldBe(HttpStatusCode.Conflict, await MatnAsync(nomOlish));
            (await MatnAsync(nomOlish)).ShouldContain("vakansiya.nom_band");
        }

        using (var ozNomi = await mijoz.PutAsJsonAsync(
            new Uri($"/api/recruiting/vacancies/{vakansiya.Id}", UriKind.Relative),
            new { title = nom, openings = 3 },
            bekorQilish))
        {
            ozNomi.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(ozNomi));
        }
    }

    /// <summary>Vakansiyalar ro'yxati NOM bo'yicha qidiriladi.</summary>
    /// <remarks>
    /// Qidiruv qoidasi boshqa ro'yxatlardagi bilan bir xil: qismiy moslik,
    /// registr va «қ/к» farqi hisobga olinmaydi (<c>SearchFolding</c>).
    /// </remarks>
    [Fact]
    public async Task Vakansiyalar_nom_boyicha_qidiriladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var belgi = Belgi();
        var topiladigan = await VakansiyaAsync(mijoz, $"Қоровул {belgi}");
        var begona = await VakansiyaAsync(mijoz, VakansiyaNomi());

        // «қ» o'rniga «к», kichik harflar, nomning faqat bir qismi
        using var royxat = await mijoz.GetAsync(
            new Uri(
                "/api/recruiting/vacancies?pageSize=200&search="
                    + Uri.EscapeDataString($"коровул {belgi.ToLowerInvariant()}"),
                UriKind.Relative),
            bekorQilish);

        royxat.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(royxat));
        var sahifa = await royxat.Content.ReadFromJsonAsync<VacancyPageResponse>(bekorQilish);
        sahifa!.Items.ShouldContain(q => q.Id == topiladigan.Id);
        sahifa.Items.ShouldNotContain(q => q.Id == begona.Id);
        sahifa.Total.ShouldBe(sahifa.Items.Count, "sanoq ham filtrlangan to'plamdan olinadi");
    }

    [Fact]
    public async Task Nomzod_TELEFON_va_ism_boyicha_topiladi()
    {
        using var mijoz = await AdminMijozAsync();

        var vakansiya = await VakansiyaAsync(mijoz, VakansiyaNomi());
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

    /// <summary>
    /// ⭐ Qo'lda kiritishdagi ogohlantirish: shu raqamli nomzodlar HAMMA
    /// holatda topiladi — ro'yxat yashiradigan rezervdagilar ham.
    /// </summary>
    /// <remarks>
    /// 2026-09-26 dan rezervdagilar nomzodlar ro'yxatida (va qidiruvida)
    /// ko'rinmaydi. HR «topilmadi» ni ko'rib, o'sha odamni ikkinchi marta
    /// kiritishi mumkin edi — shu endpoint forma uchun «bu raqam bizda bor»
    /// degan javobni beradi. Kiritish TO'SILMAYDI.
    /// </remarks>
    [Fact]
    public async Task Telefon_boyicha_mavjud_nomzodlar_HAMMA_holatda_topiladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var vakansiya = await VakansiyaAsync(mijoz, VakansiyaNomi());
        var raqam = Raqam();

        var rezervdagi = await NomzodAsync(mijoz, vakansiya.Id, familiya: "Резервдаги", telefon: raqam);
        using (var javob = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{rezervdagi.Id}/reserve", UriKind.Relative),
            new { at = new DateOnly(2026, 4, 2), reason = "Ҳозир ўрин йўқ" },
            bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        }

        var jarayondagi = await NomzodAsync(mijoz, vakansiya.Id, familiya: "Жараёндаги", telefon: raqam);
        var begona = await NomzodAsync(mijoz, vakansiya.Id, familiya: "Бегона", telefon: Raqam());

        // Ro'yxat qidiruvi rezervdagini ko'rsatmaydi — egasining qarori
        var qidiruv = await NomzodlarAsync(mijoz, "?search=" + raqam);
        qidiruv.Items.ShouldNotContain(q => q.Id == rezervdagi.Id);

        // Formatlangan raqam bilan ham — jarayondagi birinchi
        var topilgan = await MosliklarAsync(
            mijoz, "?phone=" + Uri.EscapeDataString($"+998 {raqam[..2]} {raqam[2..5]} {raqam[5..7]} {raqam[7..]}"));

        topilgan.Select(q => q.Id).ShouldBe([jarayondagi.Id, rezervdagi.Id]);
        topilgan[1].Status.ShouldBe("Reserve");
        topilgan[1].FullName.ShouldStartWith("Резервдаги");
        topilgan.ShouldNotContain(q => q.Id == begona.Id);

        // Tahrirlanayotgan nomzodning o'zi tushmaydi
        var ozidan = await MosliklarAsync(mijoz, $"?phone={raqam}&exceptId={jarayondagi.Id}");
        ozidan.Select(q => q.Id).ShouldBe([rezervdagi.Id]);

        // Chala raqam — xato emas, bo'sh ro'yxat
        (await MosliklarAsync(mijoz, "?phone=" + raqam[..5])).ShouldBeEmpty();
        (await MosliklarAsync(mijoz, string.Empty)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Telefon_mosliklari_rekruting_korish_ruxsatini_talab_qiladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var yol = new Uri("/api/recruiting/candidates/phone-matches?phone=" + Raqam(), UriKind.Relative);

        using (var ruxsatsiz = await RuxsatliMijozAsync(Permissions.EmployeesView))
        using (var javob = await ruxsatsiz.GetAsync(yol, bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await MatnAsync(javob));
        }

        using (var korish = await RuxsatliMijozAsync(Permissions.RecruitingView))
        using (var javob = await korish.GetAsync(yol, bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        }
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
    //  Takroriy ariza to'sig'i
    //  (2026-09-08 — docs/SPEC-qayta-ariza.md)
    // ────────────────────────────────────────────────────────────

    /// <summary>To'siq TELEGRAM hisobi bo'yicha ham ishlaydi.</summary>
    /// <remarks>
    /// ⭐ Faqat telefon bo'yicha tekshirish YETMAYDI: bir odamda ikkita
    /// SIM bo'lishi mumkin va u o'sha Telegram hisobidan yangi raqam
    /// bilan qaytib kelardi. Ekranda ko'rilgan holat aynan shunga
    /// yaqin — bir odam, ikki kun, ikkita anketa.
    /// </remarks>
    [Fact]
    public async Task Tekshiruv_TELEGRAM_hisobi_boyicha_ham_tosadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        using var bot = await BotMijozAsync();

        var suhbat = SuhbatRaqami();
        var begonaSuhbat = SuhbatRaqami();
        var eskiRaqam = Raqam();
        var yangiRaqam = Raqam();

        var vakansiya = await VakansiyaAsync(admin, VakansiyaNomi());
        var nomzod = await NomzodAsync(
            admin, vakansiya.Id, familiya: "Иккинчиуриниш", telefon: eskiRaqam, suhbatId: suhbat);

        // Ariza YOPILADI — muddat aynan shu sanadan hisoblanadi.
        using (var rad = await admin.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{nomzod.Id}/reject", UriKind.Relative),
            new { reason = "Тажрибаси етарли эмас" },
            bekorQilish))
        {
            rad.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(rad));
        }

        // 1. YANGI raqam, ESKI hisob — to'siq qoladi.
        var yangiRaqamBilan = await TekshiruvAsync(bot, Sorov(yangiRaqam, suhbat));

        yangiRaqamBilan.Allowed.ShouldBeFalse("raqamni almashtirish yordam bermaydi");
        yangiRaqamBilan.Reason.ShouldBe("Cooldown");

        // 2. ESKI raqam, BEGONA hisob — to'siq baribir qoladi.
        var boshqaHisobdan = await TekshiruvAsync(bot, Sorov(eskiRaqam, begonaSuhbat));

        boshqaHisobdan.Allowed.ShouldBeFalse("hisobni almashtirish ham yordam bermaydi");
        boshqaHisobdan.Reason.ShouldBe("Cooldown");

        // 3. Nazorat o'lchovi: ikkala kalit ham begona bo'lsa — yo'l ochiq.
        //    Busiz test «hammani to'sadigan» xatoni ham o'tkazib yuborardi.
        var begona = await TekshiruvAsync(bot, Sorov(yangiRaqam, begonaSuhbat));

        begona.Allowed.ShouldBeTrue("to'siq ODAMGA qo'yilgan, hammaga emas");
        begona.Reason.ShouldBe("None");
    }

    /// <summary>Jarayondagi ariza muddatdan QAT'I NAZAR to'sadi.</summary>
    /// <remarks>
    /// Bu — botdagi eng eski qoida va u saqlanishi kerak: ikkinchi yozuv
    /// ochilsa, HR bir odamni voronkada ikki marta ko'rardi (ekranda
    /// ko'rilgan aynan shu holat — bir raqam, ikkita anketa).
    /// </remarks>
    [Fact]
    public async Task Tekshiruv_jarayondagi_arizani_tosadi()
    {
        using var admin = await AdminMijozAsync();
        using var bot = await BotMijozAsync();

        var raqam = Raqam();
        var nom = VakansiyaNomi();
        var vakansiya = await VakansiyaAsync(admin, nom);
        var kengash = (await BosqichlarAsync(admin)).First(b => b.Name == "Кенгаш");

        await NomzodAsync(admin, vakansiya.Id, kengash.Id, "Жараёнда", raqam);

        var javob = await TekshiruvAsync(bot, Sorov(raqam));

        javob.Allowed.ShouldBeFalse();
        javob.Reason.ShouldBe("Active");

        // Bot xabarni aynan shu uchta maydondan yozadi: «falon kuni
        // falon vakansiyaga ariza bergansiz, hozir falon bosqichda».
        javob.AppliedAt.ShouldNotBeNull();
        javob.VacancyTitle.ShouldBe(nom);
        javob.StageName.ShouldBe(kengash.Name);
        javob.AvailableFrom.ShouldBeNull("jarayondagi arizada «qachondan keyin» degan javob yo'q");
    }

    /// <summary>Muddat ichidagi qayta ariza to'siladi va SANA aytiladi.</summary>
    /// <remarks>
    /// Hisob YOPILGAN sanadan ketadi, ariza topshirilgan sanadan emas:
    /// HR qarorni bir oy keyin yozishi mumkin va muddat o'shanda
    /// boshlanishi kerak (SPEC, 2.1-bo'lim).
    /// </remarks>
    [Fact]
    public async Task Tekshiruv_muddat_ichidagi_arizani_tosadi_va_SANANI_aytadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        using var bot = await BotMijozAsync();

        var raqam = Raqam();
        var nom = VakansiyaNomi();
        var vakansiya = await VakansiyaAsync(admin, nom);
        var kengash = (await BosqichlarAsync(admin)).First(b => b.Name == "Кенгаш");
        var nomzod = await NomzodAsync(admin, vakansiya.Id, kengash.Id, "Муддатичида", raqam);

        // Bir oy oldin yopilgan: sukut muddat olti oy, ya'ni yo'l hali yopiq.
        var yopilgan = Bugun().AddMonths(-1);

        using (var rad = await admin.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{nomzod.Id}/reject", UriKind.Relative),
            new { at = yopilgan, reason = "Тажрибаси етарли эмас" },
            bekorQilish))
        {
            rad.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(rad));
        }

        var javob = await TekshiruvAsync(bot, Sorov(raqam));

        javob.Allowed.ShouldBeFalse();
        javob.Reason.ShouldBe("Cooldown");
        javob.AvailableFrom.ShouldBe(
            yopilgan.AddMonths(RecruitingRules.ReapplyMonths),
            "hisob YOPILGAN sanadan + sozlamadagi oy");
        javob.AppliedAt.ShouldNotBeNull();
        javob.VacancyTitle.ShouldBe(nom);
        javob.StageName.ShouldBe(kengash.Name);
    }

    /// <summary>Muddat o'tgach yo'l OCHILADI.</summary>
    /// <remarks>
    /// ⭐ Ikkinchi vazifasi ham bor: nomzod yozuvi BUGUN yaratilgan,
    /// yopilish sanasi esa uch yil oldin. Hisob <c>ClosedAt</c> dan
    /// emas, kiritilgan sanadan ketganida bu odam to'silib qolardi va
    /// xatoni faqat prod'da payqash mumkin bo'lardi.
    /// </remarks>
    [Fact]
    public async Task Tekshiruv_muddat_otgach_arizani_OTKAZADI()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        using var bot = await BotMijozAsync();

        var raqam = Raqam();
        var vakansiya = await VakansiyaAsync(admin, VakansiyaNomi());
        var nomzod = await NomzodAsync(admin, vakansiya.Id, familiya: "Муддатўтган", telefon: raqam);

        using (var rad = await admin.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{nomzod.Id}/reject", UriKind.Relative),
            new { at = Bugun().AddYears(-3), reason = "Тажрибаси етарли эмас" },
            bekorQilish))
        {
            rad.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(rad));
        }

        var javob = await TekshiruvAsync(bot, Sorov(raqam));

        javob.Allowed.ShouldBeTrue("muddat mutlaq taqiq emas — u o'zi tugaydi");
        javob.Reason.ShouldBe("None");
        javob.AvailableFrom.ShouldBeNull();
        javob.AppliedAt.ShouldBeNull("to'siq yo'q ekan, botga tafsilot ham kerak emas");
    }

    /// <summary>Muddat SOZLAMADAN olinadi, kodda qadab qo'yilmagan.</summary>
    /// <remarks>
    /// ⭐ Endpoint umuman shu uchun bor: bot qoidani o'zi yozganida HR
    /// ekranda muddatni o'zgartirgach, bot eski qoida bilan ishlab
    /// qolardi. Test ikkala yo'nalishni ham tekshiradi — sozlama
    /// ko'tarilganda to'siq PAYDO bo'ladi, qaytarilganda esa KETADI.
    /// </remarks>
    [Fact]
    public async Task Tekshiruv_muddati_SOZLAMADAN_olinadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        using var bot = await BotMijozAsync();

        var raqam = Raqam();
        var vakansiya = await VakansiyaAsync(admin, VakansiyaNomi());
        var nomzod = await NomzodAsync(admin, vakansiya.Id, familiya: "Созламабўйича", telefon: raqam);
        var yopilgan = Bugun().AddYears(-3);

        using (var rad = await admin.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{nomzod.Id}/reject", UriKind.Relative),
            new { at = yopilgan, reason = "Тажрибаси етарли эмас" },
            bekorQilish))
        {
            rad.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(rad));
        }

        try
        {
            (await TekshiruvAsync(bot, Sorov(raqam)))
                .Allowed.ShouldBeTrue("sukut olti oy — uch yil oldingi ariza yo'lni to'smaydi");

            // Yuqori chegara — amalda «umuman bo'lmaydi» degani.
            await MuddatniYozAsync(admin, 120);

            var yopiq = await TekshiruvAsync(bot, Sorov(raqam));

            yopiq.Allowed.ShouldBeFalse("sozlama ko'tarilgach o'sha ariza endi to'sadi");
            yopiq.Reason.ShouldBe("Cooldown");
            yopiq.AvailableFrom.ShouldBe(yopilgan.AddMonths(120), "hisob YANGI qiymat bilan ketadi");
        }
        finally
        {
            // Sozlama BUTUN bazaga tegishli — keyingi testlar uni eski
            // holida ko'rishi kerak.
            await MuddatniYozAsync(admin, RecruitingRules.ReapplyMonths);
        }

        (await TekshiruvAsync(bot, Sorov(raqam)))
            .Allowed.ShouldBeTrue("qiymat qaytarilgach to'siq ham ketadi");
    }

    /// <summary>Muddat sozlamasi CHEGARADAN tashqariga chiqmaydi.</summary>
    /// <remarks>
    /// 0 oy — qoidaning o'zini bekor qilardi (ertasiga qayta ariza,
    /// ya'ni ekranda ko'rilgan aynan o'sha holat), 120 dan yuqorisi esa
    /// «umuman bo'lmaydi» chegarasidan oshadi. Ikkalasini ham SERVER
    /// to'xtatadi: ekrandagi min/max faqat qulaylik.
    /// </remarks>
    [Fact]
    public async Task Qayta_ariza_muddati_CHEGARADAN_tashqariga_chiqmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();

        foreach (var qiymat in new[] { "0", "121" })
        {
            using var javob = await admin.PutAsJsonAsync(
                new Uri("/api/settings", UriKind.Relative),
                new { items = new[] { new { key = SystemSettingKeys.ReapplyMonths, value = qiymat } } },
                bekorQilish);

            javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(javob));
            (await MatnAsync(javob)).ShouldContain("sozlama.chegara");
        }

        // Sozlama ro'yxatda KO'RINADI va chegaralari e'lon qilingan —
        // ekran maydonni aynan shular bo'yicha chizadi.
        using var royxat = await admin.GetAsync(new Uri("/api/settings", UriKind.Relative), bekorQilish);
        royxat.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(royxat));

        var sozlamalar = await royxat.Content.ReadFromJsonAsync<List<SettingResponse>>(bekorQilish);
        var muddat = sozlamalar.ShouldNotBeNull().Single(s => s.Key == SystemSettingKeys.ReapplyMonths);

        muddat.Kind.ShouldBe("Number");
        muddat.Min.ShouldBe(1);
        muddat.Max.ShouldBe(120);
        muddat.DefaultValue.ShouldBe("6", "sukut qiymat — yarim yil");
        muddat.Value.ShouldBe("6", "rad etilgan so'rovlar qiymatga TEGMAGAN");
    }

    /// <summary>Begona raqamga yo'l ochiq.</summary>
    /// <remarks>
    /// Nazorat o'lchovi. Busiz yuqoridagi to'rt test «hammani to'sadigan»
    /// xatoni ham xursandchilik bilan o'tkazib yuborardi — va o'shanda
    /// bot bironta ham yangi anketa qabul qilmasdi.
    /// </remarks>
    [Fact]
    public async Task Tekshiruv_begona_raqamni_OTKAZADI()
    {
        using var bot = await BotMijozAsync();

        var javob = await TekshiruvAsync(bot, Sorov(Raqam(), SuhbatRaqami()));

        javob.Allowed.ShouldBeTrue();
        javob.Reason.ShouldBe("None");
        javob.AppliedAt.ShouldBeNull();
        javob.VacancyTitle.ShouldBeNull();
        javob.StageName.ShouldBeNull();
        javob.AvailableFrom.ShouldBeNull();
    }

    /// <summary>Tekshiruv FAQAT bot xizmat hisobiga ochiq.</summary>
    /// <remarks>
    /// <para>
    /// Testning eng muhim yarmi — SUPER ADMINISTRATORGA rad javobi. Bu
    /// endpoint odam uchun emas, JARAYON uchun ochilgan va u
    /// <c>SettingsApiTests.Tokenni_faqat_bot_hisobi_oqiy_oladi</c> bilan
    /// bir xil qoidaga bo'ysunadi. Kimdir bir kun uni «qulaylik uchun»
    /// <c>recruiting.view</c> ga o'tkazsa, bu test yiqiladi.
    /// </para>
    /// <para>
    /// Sabab shunchaki tartib emas: javob boshqa odamning arizasi
    /// haqidagi ma'lumotni istalgan telefon raqami bo'yicha beradi —
    /// qachon topshirgani, qaysi vakansiyaga, qaysi bosqichda.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Tekshiruv_FAQAT_bot_hisobiga_ochiq()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var yol = new Uri(
            "/api/recruiting/candidates/application-check" + Sorov(Raqam()), UriKind.Relative);

        using var admin = await AdminMijozAsync();

        using (var javob = await admin.GetAsync(yol, bekorQilish))
        {
            javob.StatusCode.ShouldBe(
                HttpStatusCode.Forbidden,
                "super administrator ham bu yo'lni ocha olmaydi");
        }

        using var ruxsatsiz = _factory.CreateClient();
        ruxsatsiz.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await RuxsatsizTokenAsync());

        using (var javob = await ruxsatsiz.GetAsync(yol, bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using var tokensiz = _factory.CreateClient();

        using (var javob = await tokensiz.GetAsync(yol, bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "«kim ekaningni bilmayman» — boshqa javob");
        }

        // Bot esa o'tadi — aks holda test yopiq eshikni tekshirib,
        // eshikning umuman ochilmasligini payqamasdi.
        using var bot = await BotMijozAsync();

        using (var javob = await bot.GetAsync(yol, bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        }
    }

    /// <summary>Botga ketadigan ikkala javob ham AYNAN o'z maydonlarini beradi.</summary>
    /// <remarks>
    /// <para>
    /// ⭐ Bu — «ichki maydon nomzodga bormaydi» qoidasining MEXANIK
    /// qorovuli. Nomzodning telefoni, manzili, ichki izohlari va endi
    /// keyingi aloqa sanasi ham botga TUSHMASLIGI kerak: bot bu obyektlarni
    /// nomzodga yoziladigan xabarga aylantiradi.
    /// </para>
    /// <para>
    /// Test JSON maydon NOMLARINI qadaydi, C# turini emas. Shu sababli
    /// DTO ga yangi maydon qo'shilsa — hatto zararsizi ham — bu test
    /// yiqiladi va o'sha qo'shimcha ATAYLAB ko'rib chiqiladi.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Botga_ketadigan_javoblarda_ORTIQCHA_maydon_yoq()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var admin = await AdminMijozAsync();
        using var bot = await BotMijozAsync();

        var bosqichlar = await BosqichlarAsync(admin);
        var kengash = bosqichlar.First(b => b.Name == "Кенгаш");
        var vakansiya = await VakansiyaAsync(admin, VakansiyaNomi());

        var radEtilgan = await NomzodAsync(
            admin, vakansiya.Id, familiya: "Ботрад", telefon: Raqam(), suhbatId: SuhbatRaqami());

        var chaqirilgan = await NomzodAsync(
            admin, vakansiya.Id, familiya: "Ботсуҳбат", telefon: Raqam(), suhbatId: SuhbatRaqami());

        using (var rad = await admin.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{radEtilgan.Id}/reject", UriKind.Relative),
            new { reason = "Тажрибаси етарли эмас" },
            bekorQilish))
        {
            rad.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(rad));
        }

        using (var taklif = await admin.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{chaqirilgan.Id}/interview-invite", UriKind.Relative),
            new
            {
                interviewAt = DateTimeOffset.UtcNow.AddDays(3),
                stageId = kengash.Id,
                nextContactDate = Bugun().AddDays(1),
            },
            bekorQilish))
        {
            taklif.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(taklif));
        }

        (await BotMaydonlariAsync(bot, "rejection-notices", radEtilgan.Id)).ShouldBe(
            ["candidateId", "chatId", "fullName", "language", "vacancyTitle"],
            "rad etish xabari uchun boshqa hech narsa kerak emas");

        (await BotMaydonlariAsync(bot, "interview-notices", chaqirilgan.Id)).ShouldBe(
            ["candidateId", "chatId", "fullName", "interviewAt", "language", "vacancyTitle"],
            "suhbat taklifi uchun qo'shimcha faqat VAQT");
    }

    // ────────────────────────────────────────────────────────────
    //  Muhit tayyorlash
    // ────────────────────────────────────────────────────────────

    private static string Belgi() => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    /// <summary>Botga ketgan javobdagi bitta yozuvning maydon NOMLARI.</summary>
    /// <remarks>
    /// Javob <c>JsonDocument</c> bilan o'qiladi, C# rekordi bilan EMAS:
    /// rekord ortiqcha maydonni jimgina tashlab yuborardi va test aynan
    /// shu ortiqchani qidiryapti.
    /// </remarks>
    private static async Task<string[]> BotMaydonlariAsync(HttpClient bot, string yol, Guid nomzodId)
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var javob = await bot.GetAsync(
            new Uri($"/api/recruiting/candidates/{yol}?limit=50", UriKind.Relative), bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        using var hujjat = JsonDocument.Parse(await javob.Content.ReadAsStringAsync(bekorQilish));

        var qator = hujjat.RootElement
            .EnumerateArray()
            .FirstOrDefault(yozuv => yozuv.GetProperty("candidateId").GetGuid() == nomzodId);

        qator.ValueKind.ShouldBe(JsonValueKind.Object, $"«{yol}» navbatida nomzod topilmadi");

        return [.. qator.EnumerateObject()
            .Select(maydon => maydon.Name)
            .OrderBy(nom => nom, StringComparer.Ordinal)];
    }

    /// <summary>Telefon raqami hisoblagichi — izohi <see cref="Raqam"/> da.</summary>
    private static int _raqamHisoblagichi;

    /// <summary>Telegram suhbati hisoblagichi.</summary>
    private static int _suhbatHisoblagichi;

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

    /// <summary>Takrorlanmaydigan vakansiya nomi.</summary>
    /// <remarks>
    /// Nom takrorlanmasligi SHART: bir xil nomli ikkinchi vakansiya
    /// «vakansiya.nom_band» bilan rad etiladi, testlar esa bitta bazani
    /// baham ko'radi.
    /// </remarks>
    private static string VakansiyaNomi() => "Сотувчи-консультант " + Belgi();

    private static async Task<VacancyResponse> VakansiyaAsync(HttpClient mijoz, string nom)
    {
        using var javob = await mijoz.PostAsJsonAsync(
            new Uri("/api/recruiting/vacancies", UriKind.Relative),
            new
            {
                title = nom,
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

    /// <summary>Nomzod kiritadi.</summary>
    /// <remarks>
    /// <paramref name="telefon"/> va <paramref name="suhbatId"/> —
    /// takroriy ariza tekshiruvi uchun qo'shilgan. Sukut qiymatlar
    /// ATAYLAB o'zgarmagan: mavjud testlar ularga tayanadi va ular
    /// tekshiruvga umuman tegmaydi.
    /// <para>
    /// <paramref name="manba"/> — «Excel» berilsa nomzod KENGASH ARXIVI
    /// qatoriga aylanadi (sukut ro'yxatda ko'rinmaydi, «source=Excel»
    /// bilan esa yashirish qoidalari ochiladi). Sukut qiymat serverning
    /// o'z sukutiga teng, ya'ni mavjud chaqiruvlar uchun hech narsa
    /// o'zgarmaydi.
    /// </para>
    /// </remarks>
    private static async Task<CandidateResponse> NomzodAsync(
        HttpClient mijoz,
        Guid vakansiyaId,
        Guid? bosqichId = null,
        string familiya = "Каримов",
        string telefon = "905088888",
        long? suhbatId = null,
        string manba = "Manual")
    {
        using var javob = await mijoz.PostAsJsonAsync(
            new Uri("/api/recruiting/candidates", UriKind.Relative),
            new
            {
                vacancyId = vakansiyaId,
                lastName = familiya,
                firstName = "Азиз",
                phone = telefon,
                speciality = "Иқтисодчи",
                birthDate = new DateOnly(1995, 5, 20),
                stageId = bosqichId,
                telegramChatId = suhbatId,
                source = manba,
            },
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var nomzod = await javob.Content.ReadFromJsonAsync<CandidateResponse>(
            TestContext.Current.CancellationToken);

        nomzod.ShouldNotBeNull();
        return nomzod;
    }

    private sealed record PhoneMatchResponse(
        Guid Id,
        string FullName,
        string Status,
        string? VacancyTitle,
        DateTimeOffset CreatedAt);

    private static async Task<List<PhoneMatchResponse>> MosliklarAsync(HttpClient mijoz, string sorov)
    {
        using var javob = await mijoz.GetAsync(
            new Uri("/api/recruiting/candidates/phone-matches" + sorov, UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var natija = await javob.Content.ReadFromJsonAsync<List<PhoneMatchResponse>>(
            TestContext.Current.CancellationToken);

        natija.ShouldNotBeNull();
        return natija;
    }

    private async Task<HttpClient> RuxsatliMijozAsync(params string[] ruxsatlar)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAccounts.TokenAsync(_factory, ruxsatlar));

        return client;
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

    /// <summary>Bosqich yozuvi orqali keyingi aloqa sanasini qo'yadi.</summary>
    /// <remarks>
    /// Ekrandagi yo'l bilan: yangi oyna aynan shu so'rovni yuboradi
    /// (bosqich + xulosa + aloqa sanasi bitta tranzaksiyada). Bazaga
    /// to'g'ridan-to'g'ri yozish shartnomani umuman tekshirmasdi.
    /// </remarks>
    private static async Task AloqaSanasiniQoyAsync(
        HttpClient mijoz,
        Guid nomzodId,
        Guid bosqichId,
        DateOnly sana)
    {
        using var javob = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{nomzodId}/stages", UriKind.Relative),
            new { stageId = bosqichId, nextContactDate = sana },
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
    }

    /// <summary>Nomzod kartasini o'qiydi.</summary>
    private static async Task<CandidateResponse> KartaAsync(HttpClient mijoz, Guid nomzodId)
    {
        using var javob = await mijoz.GetAsync(
            new Uri($"/api/recruiting/candidates/{nomzodId}", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var karta = await javob.Content.ReadFromJsonAsync<CandidateResponse>(
            TestContext.Current.CancellationToken);

        karta.ShouldNotBeNull();
        return karta;
    }

    /// <summary>
    /// Haqiqiy, lekin RUXSATSIZ token.
    /// </summary>
    /// <remarks>
    /// Foydalanuvchi va imzo haqiqiy — faqat uning rolida bironta ruxsat
    /// yo'q. Tekshiriladigan narsa bitta — endpoint'dagi
    /// <c>RequireAuthorization</c>. (Ilgari administrator tokeniga bo'sh
    /// ro'yxat yozilardi; 2026-09-26 dan ruxsatlar bazadan olinadi, shu
    /// sababli hisob haqiqatan yaratiladi — <see cref="TestAccounts"/>.)
    /// </remarks>
    private Task<string> RuxsatsizTokenAsync() => TestAccounts.TokenAsync(_factory);

    /// <summary>
    /// Haqiqiy token, ichida FAQAT <c>recruiting.view</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ Bu hisobni login orqali olish MUMKIN emas: urug'lantirilgan
    /// rollarning hammasida ikkala rekruting ruxsati BIRGA turadi
    /// (<c>Permissions.cs</c>), ya'ni «faqat ko'ruvchi» degan foydalanuvchi
    /// bazada yo'q. Ruxsat esa endpoint darajasida tekshiriladi, shu sababli
    /// token'ni to'g'ridan-to'g'ri yasash aynan shu farqni ko'rsatadi:
    /// yuklab olish ochiq, yuklash va o'chirish esa 403.
    /// </para>
    /// <para>
    /// <see cref="RuxsatsizTokenAsync"/> ning egizagi — farqi bittagina
    /// ruxsatda.
    /// </para>
    /// </remarks>
    private Task<string> KoruvchiTokenAsync() => TestAccounts.TokenAsync(_factory, Permissions.RecruitingView);

    /// <summary>Har chaqiruvda BOSHQA telefon raqami.</summary>
    /// <remarks>
    /// Takroriy ariza tekshiruvi vakansiya bo'yicha AJRATMAYDI — to'siq
    /// odamga qo'yiladi. Ya'ni bitta raqamni ikkita test ishlatsa, biri
    /// ikkinchisining to'sig'iga urilardi va yiqilish tasodifiy
    /// ko'rinardi. Tasodif yetarli emas: oxirgi ikki xona —
    /// hisoblagich, ya'ni raqamlar bir yig'ma ichida takrorlanmaydi.
    /// </remarks>
    private static string Raqam()
        => "9"
            + Random.Shared.Next(100_000, 999_999).ToString(CultureInfo.InvariantCulture)
            + (Interlocked.Increment(ref _raqamHisoblagichi) % 100).ToString("D2", CultureInfo.InvariantCulture);

    /// <summary>Har chaqiruvda BOSHQA Telegram suhbati.</summary>
    private static long SuhbatRaqami()
        => 700_000_000L + Interlocked.Increment(ref _suhbatHisoblagichi);

    /// <summary>Bugungi kun.</summary>
    /// <remarks>
    /// Server sanani TOSHKENT mintaqasida hisoblaydi (<c>SystemClock</c>),
    /// bu yerda esa UTC olinadi va ular kun chegarasida besh soatga farq
    /// qilishi mumkin. Shu sababli testlar sanani hech qachon bugungi
    /// kunga TENG qilib qo'ymaydi: chegara oylar va yillar bilan
    /// olinadi, ya'ni bir kunlik farq javobga ta'sir qilmaydi.
    /// </remarks>
    private static DateOnly Bugun() => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>Tekshiruv so'rovining satri.</summary>
    /// <remarks>
    /// Raqam «+998…» ko'rinishida ketadi — Telegram kontaktni aynan
    /// shunday beradi. «+» so'rov satrida BO'SH JOYGA aylanadi va shu
    /// sababli qochiriladi: qochirilmasa server boshqa raqamni ko'rib,
    /// hech kimni topmasdi va testlar yolg'on «yo'l ochiq» javobidan
    /// xursand bo'lib o'tib ketardi.
    /// </remarks>
    private static string Sorov(string raqam, long? suhbatId = null)
    {
        var sorov = "?phone=" + Uri.EscapeDataString("+998" + raqam);

        return suhbatId is { } id
            ? sorov + "&telegramChatId=" + id.ToString(CultureInfo.InvariantCulture)
            : sorov;
    }

    /// <summary>Botning savoli: «bu odam anketa topshira oladimi?».</summary>
    private static async Task<ApplicationCheckResponse> TekshiruvAsync(HttpClient bot, string sorov)
    {
        using var javob = await bot.GetAsync(
            new Uri("/api/recruiting/candidates/application-check" + sorov, UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var natija = await javob.Content.ReadFromJsonAsync<ApplicationCheckResponse>(
            TestContext.Current.CancellationToken);

        natija.ShouldNotBeNull();
        return natija;
    }

    /// <summary>«Qayta ariza muddati» sozlamasini yozadi.</summary>
    /// <remarks>
    /// Ekrandagi yo'l bilan — <c>PUT /api/settings</c> orqali, bazaga
    /// to'g'ridan-to'g'ri emas: tekshirilayotgan narsa aynan
    /// «HR qiymatni o'zgartirdi, bot yangi qoida bilan ishlaydi»
    /// degan zanjir.
    /// </remarks>
    private static async Task MuddatniYozAsync(HttpClient admin, int oy)
    {
        using var javob = await admin.PutAsJsonAsync(
            new Uri("/api/settings", UriKind.Relative),
            new
            {
                items = new[]
                {
                    new
                    {
                        key = SystemSettingKeys.ReapplyMonths,
                        value = oy.ToString(CultureInfo.InvariantCulture),
                    },
                },
            },
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
    }

    private async Task<HttpClient> AdminMijozAsync()
        => await KirishAsync(BonviApiFactory.TestAdminLogin, BonviApiFactory.TestAdminPassword);

    /// <summary>Telegram botning XIZMAT hisobi.</summary>
    /// <remarks>
    /// «/application-check» ROL bo'yicha himoyalangan
    /// (<c>AuthenticationExtensions.BotServicePolicy</c>) va unga ruxsat
    /// bilan yetib bo'lmaydi — hatto super administrator ham. Shu
    /// sababli tekshiruv testlariga alohida hisob kerak;
    /// <c>SettingsApiTests</c> dagi bilan bir xil naqsh.
    /// </remarks>
    private async Task<HttpClient> BotMijozAsync()
        => await KirishAsync(BonviApiFactory.TestBotLogin, BonviApiFactory.TestBotPassword);

    private async Task<HttpClient> KirishAsync(string login, string parol)
    {
        var client = _factory.CreateClient();

        using var javob = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new { login, password = parol },
            TestContext.Current.CancellationToken);

        javob.EnsureSuccessStatusCode();

        var auth = await javob.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return client;
    }

    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
