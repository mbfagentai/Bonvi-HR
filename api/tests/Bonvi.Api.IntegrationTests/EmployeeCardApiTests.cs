using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bonvi.Domain.Catalog;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Kartadagi shaxs — test uchun faqat kerakli maydonlar.</summary>
public sealed record CardPersonResponse(Guid Id, DateOnly? BirthDate, string? Pinfl, bool NeedsReview);

/// <summary>Kartadagi mehnat munosabati — test uchun faqat kerakli maydonlar.</summary>
public sealed record CardEmploymentResponse(
    Guid Id,
    DateOnly? TerminationDate,
    Guid? TerminationReasonId,
    string? TerminationReason);

/// <summary>Xodim kartasi — test uchun.</summary>
public sealed record EmployeeCardResponse(
    CardPersonResponse Employee,
    IReadOnlyList<CardEmploymentResponse> Employments);

/// <summary>
/// Xodim kartasining uchi-uchiga testlari: tug'ilgan sanani tuzatish va
/// ishdan ketish sababining nomi.
/// </summary>
/// <remarks>
/// Ikkala mavzu ham «HR ekranda nimani ko'radi» degan savolga tegishli va
/// ikkalasi ham faqat to'liq zanjirda ko'rinadi: birinchisi buyruq ichidagi
/// TARTIBGA bog'liq, ikkinchisi esa ma'lumotnomaga birlashtirishga.
/// </remarks>
public sealed class EmployeeCardApiTests : IClassFixture<BonviApiFactory>
{
    /// <summary>
    /// Nazorat raqami TO'G'RI JSHSHIR — 20.07.1988 da tug'ilgan erkak.
    /// </summary>
    /// <remarks>
    /// Qiymat qo'lda emas, <see cref="Pinfl"/> algoritmi bo'yicha hisoblangan:
    /// tasodifiy raqam testni mazmunga yetib bormasdan, «nazorat raqami»
    /// xatosi bilan yiqitardi.
    /// </remarks>
    private const string TogriJshshir = "32007885123450";

    /// <summary>JSHSHIR ichidagi haqiqiy sana.</summary>
    private static readonly DateOnly JshshirSanasi = new(1988, 7, 20);

    /// <summary>Kartada saqlangan XATO sana — bir kun farq bilan.</summary>
    private static readonly DateOnly XatoSana = new(1988, 7, 21);

    private readonly BonviApiFactory _factory;

    public EmployeeCardApiTests(BonviApiFactory factory) => _factory = factory;

    // ────────────────────────────────────────────────────────────
    //  Tug'ilgan sana
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Tug'ilgan sana va JSHSHIR bitta so'rovda kelsa — qabul qilinadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bu ko'chirilgan yozuvni tuzatishning YAGONA yo'li. ETL «tekshirish
    /// kerak» deb belgilagan yozuvlarning katta qismida nomuvofiqlik aynan
    /// shu juftlikda, va ularning ikkalasi ham bir vaqtda o'zgaradi.
    /// </para>
    /// <para>
    /// Test avval yopiq yo'lni ko'rsatadi (faqat JSHSHIR yuborilsa — rad
    /// javob), keyin ochiq yo'lni: ikkalasi birga yuborilganda tekshiruv
    /// YANGI juftlik ustida bajariladi va tahrir o'tadi.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Tugilgan_sana_va_jshshir_birga_yuborilsa_qabul_qilinadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var client = await AdminMijozAsync();
        var xodim = await XodimYaratAsync(client, "Тузатилган", XatoSana);
        var yol = new Uri($"/api/employees/{xodim.Id}", UriKind.Relative);

        // 1. Faqat JSHSHIR: u kartadagi eski sana bilan solishtiriladi va
        //    to'g'ri raqam bo'lsa ham rad etiladi — bu kutilgan holat.
        using (var faqatJshshir = await client.PutAsJsonAsync(
            yol,
            new { lastName = "Тузатилган", firstName = "Ходим", pinfl = TogriJshshir },
            bekorQilish))
        {
            faqatJshshir.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(faqatJshshir));
            (await XatoKodiAsync(faqatJshshir)).ShouldBe("xodim.jshshir_sana_mos_emas");
        }

        // 2. Sana va JSHSHIR BIRGA — tekshiruv yangi juftlik ustida
        using (var birga = await client.PutAsJsonAsync(
            yol,
            new
            {
                lastName = "Тузатилган",
                firstName = "Ходим",
                birthDate = JshshirSanasi,
                pinfl = TogriJshshir,
            },
            bekorQilish))
        {
            birga.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(birga));
        }

        var karta = await KartaAsync(client, xodim.Id);

        karta.Employee.BirthDate.ShouldBe(JshshirSanasi, "sana HAQIQATAN o'zgarishi kerak");
        karta.Employee.Pinfl.ShouldBe(TogriJshshir);
    }

    /// <summary>Sana JSHSHIRsiz ham tuzatiladi.</summary>
    /// <remarks>
    /// Excel'dan kelgan yozuvlarning bir qismida JSHSHIR umuman yo'q, sana
    /// esa xato. Bu holat ham tuzatiladigan bo'lishi kerak.
    /// </remarks>
    [Fact]
    public async Task Tugilgan_sana_jshshirsiz_ham_tahrirlanadi()
    {
        using var client = await AdminMijozAsync();
        var xodim = await XodimYaratAsync(client, "Сана", new DateOnly(1990, 5, 5));

        using (var tahrir = await client.PutAsJsonAsync(
            new Uri($"/api/employees/{xodim.Id}", UriKind.Relative),
            new { lastName = "Сана", firstName = "Ходим", birthDate = new DateOnly(1990, 6, 6) },
            TestContext.Current.CancellationToken))
        {
            tahrir.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(tahrir));
        }

        var karta = await KartaAsync(client, xodim.Id);

        karta.Employee.BirthDate.ShouldBe(new DateOnly(1990, 6, 6));
    }

    /// <summary>Sana yuborilmasa — o'chirilmaydi.</summary>
    /// <remarks>
    /// Qism-qism yangilashda bo'sh maydon «tozala» degani emas: aks holda
    /// faqat telefonni tuzatgan HR tug'ilgan sanani ham yo'qotardi.
    /// </remarks>
    [Fact]
    public async Task Sana_yuborilmasa_kartada_qoladi()
    {
        using var client = await AdminMijozAsync();
        var xodim = await XodimYaratAsync(client, "Тегилмаган", new DateOnly(1985, 2, 3));

        using (var tahrir = await client.PutAsJsonAsync(
            new Uri($"/api/employees/{xodim.Id}", UriKind.Relative),
            new { lastName = "Тегилмаган", firstName = "Ходим", phone = "905088888" },
            TestContext.Current.CancellationToken))
        {
            tahrir.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(tahrir));
        }

        var karta = await KartaAsync(client, xodim.Id);

        karta.Employee.BirthDate.ShouldBe(new DateOnly(1985, 2, 3));
    }

    // ────────────────────────────────────────────────────────────
    //  Ishdan ketish sababi
    // ────────────────────────────────────────────────────────────

    /// <summary>Kartada ishdan ketish sababi NOMI bilan qaytadi.</summary>
    /// <remarks>
    /// Identifikatorning o'zi ekranda hech narsa bermaydi — mijozda uni
    /// nomga aylantiradigan ma'lumot yo'q va «Ishdan ketish sababi» qatori
    /// doim bo'sh chiqardi. HR esa arxivdagi kartani aynan shu qator uchun
    /// ochadi.
    /// </remarks>
    [Fact]
    public async Task Ishdan_ketish_sababi_nomi_bilan_qaytadi()
    {
        var belgi = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var kutilganNom = "O'z xohishiga ko'ra " + belgi;

        var xodimId = await BoshatilganXodimAsync(belgi, kutilganNom);

        using var client = await AdminMijozAsync();
        var karta = await KartaAsync(client, xodimId);

        karta.Employments.Count.ShouldBe(1);

        var munosabat = karta.Employments[0];

        munosabat.TerminationReasonId.ShouldNotBeNull();
        munosabat.TerminationReason.ShouldBe(kutilganNom);
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    /// <summary>Ishdan bo'shatilgan xodimni to'g'ridan-to'g'ri bazaga qo'yadi.</summary>
    /// <remarks>
    /// Bo'shatish HTTP orqali emas: testga aynan ma'lumotnomaga bog'langan
    /// sabab kerak, ishga qabul va bo'shatish formalarining o'z qoidalari
    /// esa bu yerda tekshirilmaydi.
    /// </remarks>
    private async Task<Guid> BoshatilganXodimAsync(string belgi, string sababNomi)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var firma = LegalEntity.Create("Сабаб Тест МЧЖ " + belgi).Value;
        var sabab = TerminationReason.Create(sababNomi).Value;
        var xodim = Employee.Create(FullName.Create("Кетган", "Ходим", belgi).Value).Value;

        var munosabat = Employment.Hire(
            xodim.Id,
            firma.Id,
            new DateOnly(2023, 2, 1),
            orderNo: belgi + "-1").Value;

        munosabat
            .ImportTermination(new DateOnly(2024, 8, 30), sabab.Id, belgi + "-2")
            .IsSuccess.ShouldBeTrue();

        kontekst.AddRange(firma, sabab, xodim);
        kontekst.Add(munosabat);

        await kontekst.SaveChangesAsync(TestContext.Current.CancellationToken);

        return xodim.Id;
    }

    private static async Task<EmployeeCardResponse> KartaAsync(HttpClient client, Guid xodimId)
    {
        using var javob = await client.GetAsync(
            new Uri($"/api/employees/{xodimId}", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var karta = await javob.Content.ReadFromJsonAsync<EmployeeCardResponse>(
            TestContext.Current.CancellationToken);

        karta.ShouldNotBeNull();
        return karta;
    }

    private static async Task<EmployeeResponse> XodimYaratAsync(
        HttpClient client,
        string familiya,
        DateOnly tugilganSana)
    {
        using var javob = await client.PostAsJsonAsync(
            new Uri("/api/employees", UriKind.Relative),
            new
            {
                lastName = familiya + " " + Guid.NewGuid().ToString("N")[..6],
                firstName = "Ходим",
                birthDate = tugilganSana,
            },
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var xodim = await javob.Content.ReadFromJsonAsync<EmployeeResponse>(
            TestContext.Current.CancellationToken);

        xodim.ShouldNotBeNull();
        return xodim;
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

    /// <summary>Xato javobidan kodni oladi — tekshiruv aniq bo'lishi uchun.</summary>
    private static async Task<string?> XatoKodiAsync(HttpResponseMessage javob)
    {
        var muammo = await javob.Content.ReadFromJsonAsync<ProblemResponse>(
            TestContext.Current.CancellationToken);

        return muammo?.Title;
    }

    /// <summary>Javob tanasi — tekshiruv yiqilganda sababi ko'rinishi uchun.</summary>
    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
