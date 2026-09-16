using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Ishga qabul javobidagi sinov muddati — testga kerakli qismi.</summary>
public sealed record HireProbationResponse(Guid Id, string Status, DateOnly EndDate);

/// <summary>Ishga qabul javobidagi mehnat munosabati — testga kerakli qismi.</summary>
public sealed record HireEmploymentResponse(
    Guid Id,
    Guid EmployeeId,
    string Status,
    HireProbationResponse? Probation);

/// <summary>`HireResultDto` — konvert.</summary>
public sealed record HireResultResponse(HireEmploymentResponse Employment);

/// <summary>Kartadagi shaxs — bu testda tekshiriladigan maydonlar.</summary>
public sealed record HiredPersonResponse(
    Guid Id,
    string FullName,
    string? Pinfl,
    string? Phone,
    string? Address,
    string? Speciality,
    string? TelegramUrl);

/// <summary>Xodim kartasi — bu testda tekshiriladigan qismi.</summary>
public sealed record HiredCardResponse(HiredPersonResponse Employee);

/// <summary>Sinov muddatlari ro'yxatining qatori — testga kerakli qismi.</summary>
public sealed record ProbationRowResponse(Guid Id, Guid EmployeeId, string Status);

/// <summary>
/// «/api/probations» sahifalangan javobi.
/// </summary>
/// <remarks>
/// Ro'yxat 2026-09-01 da sahifalangan: Excel tarixi ko'chirilgach u
/// 1342 qatorga (bitta javobda 926 KB) yetgan edi. Shakl xodimlar
/// ro'yxatidagi bilan bir xil.
/// </remarks>
public sealed record ProbationPageResponse(IReadOnlyList<ProbationRowResponse> Items, int Total);

/// <summary>
/// Mijoz yuboradigan shaxs bloki — <c>toHireBody</c> dagi <c>person</c>.
/// </summary>
/// <remarks>
/// Maydon nomlari QO'LDA yozilgan va bu ataylab: shartnomaning ikki tomoni
/// ikki texnologiyada yashaydi va ularni bog'laydigan yagona narsa —
/// aynan shu nomlar. Generatsiya qilingan tip bu yerda hech narsani
/// isbotlamasdi, chunki u serverning o'zidan kelardi.
/// </remarks>
public sealed record HirePersonBody(
    string LastName,
    string FirstName,
    string? MiddleName,
    DateOnly? BirthDate,
    string? Pinfl,
    string? Passport,
    string? Phone,
    string? SecondPhone,
    string? Address,
    string? Speciality,
    string? TelegramUrl,
    IReadOnlyList<Guid> DirectionIds);

/// <summary>Mijoz yuboradigan tananing AYNAN nusxasi.</summary>
public sealed record HireBody(
    Guid LegalEntityId,
    Guid? DepartmentId,
    Guid? PositionId,
    DateOnly HireDate,
    decimal Rate,
    bool IsPrimary,
    decimal? SalaryAmount,
    string SalaryCurrency,
    string? OrderNo,
    DateOnly? OrderDate,
    HirePersonBody Person,
    int? ProbationDays = null);

/// <summary>
/// <c>POST /api/employments/hire</c> — so'rov tanasining SHAKLI.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega bu test bor.</b> Endpoint uzoq vaqt hech qanday test bilan
/// qoplanmagan edi va shu bo'shliqda jimgina nuqson yashadi: interfeys
/// shaxs maydonlarini TEKIS yuborardi (<c>{ lastName, firstName, … }</c>),
/// server esa ularni ichma-ich <c>person</c> obyektida kutadi. Natijada
/// forma orqali yangi xodim HECH QACHON yaratilmagan — HR har safar
/// «xodim.malumot_yoq» xatosini ko'rgan.
/// </para>
/// <para>
/// Shu sababli bu yerdagi tana AYNAN mijoz yuboradigan tana:
/// <c>web/src/features/employees/api/employees-api.ts</c> dagi
/// <c>toHireBody</c> natijasi. Shakl bir tomonda o'zgarsa, ikkinchi tomon
/// buni testda ko'radi.
/// </para>
/// <para>
/// Ikkinchi mavzu — <b>sinovsiz qabul</b>. Foydalanuvchining qarori:
/// «hamma sinovdan o'tadi, istisnosiz». Endpoint darajasida bu shuni
/// bildiradi: har qanday muvaffaqiyatli qabul sinov muddati bilan qaytadi
/// va odam darhol <c>/api/probations</c> ro'yxatida paydo bo'ladi.
/// </para>
/// </remarks>
public sealed class HireApiTests : IClassFixture<BonviApiFactory>
{
    /// <summary>
    /// Nazorat raqami TO'G'RI JSHSHIR — 20.07.1988 da tug'ilgan erkak.
    /// </summary>
    /// <remarks>
    /// Tasodifiy raqam testni mazmunga yetib bormasdan, «nazorat raqami»
    /// xatosi bilan yiqitardi (<c>EmployeeCardApiTests</c> dagi bilan bir xil
    /// sabab).
    /// </remarks>
    private const string TogriJshshir = "32007885123450";

    private static readonly DateOnly JshshirSanasi = new(1988, 7, 20);

    private readonly BonviApiFactory _factory;

    public HireApiTests(BonviApiFactory factory) => _factory = factory;

    /// <summary>
    /// Interfeys yuboradigan tana bilan yangi xodim HAQIQATAN yaratiladi.
    /// </summary>
    /// <remarks>
    /// Tekshiruv uch qavatli va uchtasi ham kerak: javob (munosabat va sinov
    /// ochildi), karta (shaxs maydonlari <c>person</c> ichidan chiqib,
    /// yozuvga tushdi) va sinov ro'yxati (odam HR ko'radigan ekranda paydo
    /// bo'ldi). Faqat javobga qaralsa, <c>person</c> ichidagi maydonlar
    /// jimgina tashlab yuborilgani sezilmasdi.
    /// </remarks>
    [Fact]
    public async Task Interfeys_yuboradigan_tana_bilan_yangi_xodim_yaratiladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        var belgi = Belgi();
        var firmaId = await FirmaAsync(belgi);
        var familiya = "Синовдан " + belgi;

        using var client = await AdminMijozAsync();

        using var javob = await client.PostAsJsonAsync(
            new Uri("/api/employments/hire", UriKind.Relative),
            MijozTanasi(firmaId, familiya),
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var natija = await javob.Content.ReadFromJsonAsync<HireResultResponse>(bekorQilish);
        natija.ShouldNotBeNull();

        var munosabat = natija.Employment;

        munosabat.EmployeeId.ShouldNotBe(Guid.Empty, "shaxs «person» ichidan yaratilishi kerak");
        munosabat.Status.ShouldBe(nameof(EmploymentStatus.Probation));

        // Sinov muddati AVTOMATIK va ISTISNOSIZ ochiladi
        munosabat.Probation.ShouldNotBeNull("sinovsiz qabul bo'lishi mumkin emas");
        munosabat.Probation.Status.ShouldBe(nameof(ProbationStatus.InProgress));

        // ── Shaxs maydonlari HAQIQATAN yozildi ──
        // Tekis tanada ular server uchun umuman ko'rinmasdi va xato ham
        // chiqmasdi: noma'lum maydonlar jimgina tashlab yuboriladi.
        var karta = await KartaAsync(client, munosabat.EmployeeId);

        karta.Employee.FullName.ShouldContain(familiya);
        karta.Employee.Pinfl.ShouldBe(TogriJshshir);
        karta.Employee.Phone.ShouldBe("+998905088777");
        karta.Employee.Address.ShouldBe("Қўқон шаҳри");
        karta.Employee.Speciality.ShouldBe("Ҳисобчи");

        // ⚠️ Formada maydon nomi `telegram`, serverda `telegramUrl`. Aynan
        // shu farq tekis tanada eng oson yo'qoladigan maydon edi.
        karta.Employee.TelegramUrl.ShouldNotBeNull();
        karta.Employee.TelegramUrl.ShouldContain("bonvi_test");

        // ── Odam «Sinov muddati» ekranida paydo bo'ldi ──
        var sinovlar = await SinovlarAsync(client);

        sinovlar.ShouldContain(
            qator => qator.EmployeeId == munosabat.EmployeeId
                && qator.Status == nameof(ProbationStatus.InProgress),
            "yangi xodim /sinov ro'yxatida ko'rinishi kerak");
    }

    /// <summary>
    /// ⭐ SINOVDAGI xodim umumiy ro'yxatda ham ko'rinadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Foydalanuvchi talabi (2026-09-16): sinovdagi odam ham xodim va uni
    /// «Xodimlar» bo'limida topish kerak. Ilgari ro'yxatning standart
    /// filtri faqat DOIMIYLARNI ko'rsatardi, ya'ni yangi qabul qilingan
    /// odam ekrandan butunlay yo'qolardi — 600 dan ortiq odam shu holatda
    /// edi.
    /// </para>
    /// <para>
    /// <c>employed</c> — holat sanog'ining qiymati EMAS, ikkita holatning
    /// birlashmasi (<c>EmployeeSearchFilter.HideTerminated</c> izohi). Shu
    /// sababli test uchala so'rovni ham qadaydi: birlashma topadi, aniq
    /// holat filtri esa ajratadi.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Sinovdagi_xodim_umumiy_royxatda_korinadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        var belgi = Belgi();
        var firmaId = await FirmaAsync(belgi);
        var familiya = "Рўйхатдаги " + belgi;

        using var client = await AdminMijozAsync();

        /*
          ⚠️ JSHSHIRSIZ tana. `MijozTanasi` bitta QAT'IY raqamni yuboradi
          va server shaxsni aynan shu bo'yicha taniydi — ya'ni ikkinchi
          qabul MAVJUD odamni topib, uning ismini qayta yozardi va
          qo'shni testni yiqitardi. Bu yerda shaxsning o'zi emas,
          ro'yxat filtri tekshirilyapti: JSHSHIR umuman kerak emas.
        */
        var tana = MijozTanasi(firmaId, familiya) with
        {
            Person = MijozTanasi(firmaId, familiya).Person with
            {
                Pinfl = null,
                Phone = null,
            },
        };

        using var javob = await client.PostAsJsonAsync(
            new Uri("/api/employments/hire", UriKind.Relative),
            tana,
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var natija = await javob.Content.ReadFromJsonAsync<HireResultResponse>(bekorQilish);
        natija.ShouldNotBeNull();
        natija.Employment.Status.ShouldBe(
            nameof(EmploymentStatus.Probation),
            "yangi xodim SINOVDA boshlaydi");

        var xodimId = natija.Employment.EmployeeId;

        async Task<bool> RoyxatdaAsync(string holat)
        {
            using var royxat = await client.GetAsync(
                new Uri($"/api/employees?status={holat}&search={familiya}", UriKind.Relative),
                bekorQilish);

            royxat.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(royxat));

            using var hujjat = JsonDocument.Parse(
                await royxat.Content.ReadAsStringAsync(bekorQilish));

            return hujjat.RootElement
                .GetProperty("items")
                .EnumerateArray()
                .Any(qator => qator.GetProperty("id").GetGuid() == xodimId);
        }

        (await RoyxatdaAsync("employed")).ShouldBeTrue(
            "«ishlayotganlar» sinovdagilarni ham qamraydi — ro'yxatning standart ko'rinishi");

        (await RoyxatdaAsync("probation")).ShouldBeTrue("aniq holat filtri ham topadi");

        (await RoyxatdaAsync("active")).ShouldBeFalse(
            "«doimiy» filtri sinovdagini ko'rsatmaydi — ikkisi boshqa holat");
    }

    /// <summary>
    /// Shaxs TEKIS yuborilsa — so'rov rad etiladi.
    /// </summary>
    /// <remarks>
    /// Bu nuqsonning o'zini qaytarib keladigan test: mijoz bir kun
    /// <c>toHireBody</c> ni chetlab o'tsa yoki shaxs maydonlarini yana
    /// yuqori qavatga chiqarsa, xato jimgina emas, aynan shu yerda
    /// ko'rinadi. Serverning javobi ham aniq: «xodim.malumot_yoq».
    /// </remarks>
    [Fact]
    public async Task Shaxs_tekis_yuborilsa_sorov_rad_etiladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        var belgi = Belgi();
        var firmaId = await FirmaAsync(belgi);

        using var client = await AdminMijozAsync();

        using var javob = await client.PostAsJsonAsync(
            new Uri("/api/employments/hire", UriKind.Relative),
            new
            {
                legalEntityId = firmaId,
                hireDate = new DateOnly(2026, 3, 2),
                rate = 1m,
                isPrimary = true,

                // ⚠️ ATAYLAB noto'g'ri: shaxs `person` ichida bo'lishi kerak
                lastName = "Текис " + belgi,
                firstName = "Ходим",
            },
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(javob));
        (await XatoKodiAsync(javob)).ShouldBe("xodim.malumot_yoq");
    }

    /// <summary>
    /// Sinovsiz ishga qabul — endpoint darajasida ham yopiq.
    /// </summary>
    /// <remarks>
    /// Ilgari <c>probationDays: 0</c> alohida yo'l edi: sinov yozuvi
    /// ochilmasdi va munosabat darhol «faol» bo'lardi. Odam esa
    /// <c>/sinov</c> ro'yxatiga umuman tushmasdi va uni hech kim
    /// baholamasdi.
    /// </remarks>
    [Fact]
    public async Task Sinovsiz_qabul_endpointda_ham_rad_etiladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        var belgi = Belgi();
        var firmaId = await FirmaAsync(belgi);

        using var client = await AdminMijozAsync();

        using var javob = await client.PostAsJsonAsync(
            new Uri("/api/employments/hire", UriKind.Relative),
            MijozTanasi(firmaId, "Синовсиз " + belgi, sinovKunlari: 0),
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(javob));
        (await XatoKodiAsync(javob)).ShouldBe("sinov.majburiy");
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    /// <summary>Manba: <c>toHireBody</c> (<c>employees-api.ts</c>).</summary>
    private static HireBody MijozTanasi(Guid firmaId, string familiya, int? sinovKunlari = null)
        => new(
            LegalEntityId: firmaId,
            DepartmentId: null,
            PositionId: null,
            HireDate: new DateOnly(2026, 3, 2),
            Rate: 1m,
            IsPrimary: true,
            SalaryAmount: null,
            SalaryCurrency: "UZS",
            OrderNo: "91-b",
            OrderDate: new DateOnly(2026, 3, 1),
            Person: new HirePersonBody(
                LastName: familiya,
                FirstName: "Ходим",
                MiddleName: null,
                BirthDate: JshshirSanasi,
                Pinfl: TogriJshshir,
                Passport: null,
                Phone: "905088777",
                SecondPhone: null,
                Address: "Қўқон шаҳри",
                Speciality: "Ҳисобчи",
                TelegramUrl: "bonvi_test",
                DirectionIds: []),
            ProbationDays: sinovKunlari);

    private static string Belgi() => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    /// <summary>
    /// Yuridik shaxsni to'g'ridan-to'g'ri bazaga qo'yadi.
    /// </summary>
    /// <remarks>
    /// Ma'lumotnoma endpoint'i orqali emas: bu testda tekshiriladigan narsa
    /// ishga qabul shartnomasi, firma esa faqat sharoit.
    /// </remarks>
    private async Task<Guid> FirmaAsync(string belgi)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var firma = LegalEntity.Create("Қабул Тест МЧЖ " + belgi).Value;
        kontekst.Add(firma);

        await kontekst.SaveChangesAsync(TestContext.Current.CancellationToken);

        return firma.Id;
    }

    private static async Task<HiredCardResponse> KartaAsync(HttpClient client, Guid xodimId)
    {
        using var javob = await client.GetAsync(
            new Uri($"/api/employees/{xodimId}", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var karta = await javob.Content.ReadFromJsonAsync<HiredCardResponse>(
            TestContext.Current.CancellationToken);

        karta.ShouldNotBeNull();
        return karta;
    }

    private static async Task<IReadOnlyList<ProbationRowResponse>> SinovlarAsync(HttpClient client)
    {
        using var javob = await client.GetAsync(
            new Uri("/api/probations?status=InProgress", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var sahifa = await javob.Content.ReadFromJsonAsync<ProbationPageResponse>(
            TestContext.Current.CancellationToken);

        sahifa.ShouldNotBeNull();
        return sahifa.Items;
    }

    private async Task<HttpClient> AdminMijozAsync()
    {
        var client = _factory.CreateClient();

        using var javob = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new { login = BonviApiFactory.TestAdminLogin, password = BonviApiFactory.TestAdminPassword },
            TestContext.Current.CancellationToken);

        javob.EnsureSuccessStatusCode();

        var auth = await javob.Content.ReadFromJsonAsync<AuthResponse>(
            TestContext.Current.CancellationToken);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return client;
    }

    private static async Task<string?> XatoKodiAsync(HttpResponseMessage javob)
    {
        var muammo = await javob.Content.ReadFromJsonAsync<ProblemResponse>(
            TestContext.Current.CancellationToken);

        return muammo?.Title;
    }

    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
