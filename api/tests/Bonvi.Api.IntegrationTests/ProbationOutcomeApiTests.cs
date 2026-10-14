using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Sinov natijasi xodimning HOLATIGA to'g'ri o'tishi — u qaysi menyuda
/// turishini aynan shu holat belgilaydi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega bu testlar bor (2026-10-14).</b> HR sinovni muddatidan oldin
/// yakunlardi, natija o'zgarardi, lekin xodim «Sinov» menyusida qolib
/// ketardi. Uch sabab topildi va egasi uchalasini ham hal qildi:
/// </para>
/// <list type="number">
///   <item>«O'tmadi» / «o'zi ketdi» munosabatni yopmasdi — xodim «sinovda»
///   holatida qolardi. Endi bo'shatish shu amalning o'zida.</item>
///   <item>«Bekor qilindi» xodimni «sinovda» holatida, sinovsiz qoldirardi
///   (prod'da takror qabulni bekor qilishga urinish). Endi u qabul
///   qilinmaydi.</item>
///   <item>Eski sana bilan kiritilgan qabulda muddat kiritilgandayoq tugagan
///   bo'lardi, xodim esa yillab «sinovda» turardi. Endi u darhol
///   «o'tdi».</item>
/// </list>
/// </remarks>
public sealed class ProbationOutcomeApiTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public ProbationOutcomeApiTests(BonviApiFactory factory) => _factory = factory;

    /// <summary>
    /// API ning «bugun»i — Toshkent sanasi. UTC sanasi kechqurun bir kun
    /// orqada qoladi va chegara testi (muddat AYNAN bugun tugaydi) soat
    /// 19:00 dan keyin yiqilardi.
    /// </summary>
    private DateOnly Bugun => _factory.Services.GetRequiredService<IClock>().Today;

    // ────────────────────────────────────────────────────────────
    //  «O'tmadi» / «o'zi ketdi» — shu amalning o'zida bo'shatish
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Muddatidan OLDIN «o'tmadi» — xodim shu so'rovda bo'shatiladi va
    /// «sinovda» holatida qolmaydi.
    /// </summary>
    [Theory]
    [InlineData("Failed")]
    [InlineData("LeftEarly")]
    public async Task Otmagan_xodim_shu_sorovda_ishdan_boshatiladi(string natija)
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var muhit = await SinovdagiAsync();
        var ketish = Bugun.AddDays(-1);

        using var javob = await mijoz.PostAsJsonAsync(
            new Uri($"/api/probations/{muhit.ProbationId}/resolve", UriKind.Relative),
            new
            {
                status = natija,
                conclusion = "Talabga javob bermadi",
                termination = new { terminationDate = ketish, orderNo = "77-k" },
            },
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var (munosabat, sinov) = await HolatAsync(muhit);

        munosabat.Status.ShouldBe(EmploymentStatus.Terminated, "xodim «sinovda» holatida qolib ketmasligi kerak");
        munosabat.TerminationDate.ShouldBe(ketish);
        sinov.Status.ShouldBe(Enum.Parse<ProbationStatus>(natija), "bo'shatish natijani «o'zi ketdi» bilan ustidan yozmasligi kerak");
        sinov.ResolvedAt.ShouldBe(ketish, "natija va bo'shatish — bitta voqea");
    }

    /// <summary>
    /// Bo'shatish ma'lumotisiz «o'tmadi» — 400, va HECH NARSA saqlanmaydi.
    /// </summary>
    /// <remarks>
    /// Aynan eski xatoning o'zi: natija yozilib, xodim «sinovda» qolardi.
    /// </remarks>
    [Fact]
    public async Task Boshatishsiz_otmadi_400_va_hech_narsa_saqlanmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var muhit = await SinovdagiAsync();

        using var javob = await mijoz.PostAsJsonAsync(
            new Uri($"/api/probations/{muhit.ProbationId}/resolve", UriKind.Relative),
            new { status = "Failed" },
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(javob));
        (await MatnAsync(javob)).ShouldContain("sinov.bosatish_kerak");

        var (munosabat, sinov) = await HolatAsync(muhit);

        munosabat.Status.ShouldBe(EmploymentStatus.Probation);
        sinov.Status.ShouldBe(ProbationStatus.InProgress, "natija yolg'iz saqlanmasligi kerak");
    }

    /// <summary>
    /// Qora ro'yxatdagi xodim: tasdiqsiz bo'shatish to'xtaydi va sinov ham
    /// OCHIQ qoladi; tasdiq bilan — ikkalasi birga o'tadi.
    /// </summary>
    /// <remarks>
    /// Tranzaksiya chegarasining eng nozik joyi: sinov xotirada yopilgan,
    /// bo'shatish esa rad etilgan. Bitta <c>SaveChanges</c> bo'lmasa, HR
    /// «o'tmadi, lekin ishda» degan holatni olardi.
    /// </remarks>
    [Fact]
    public async Task Qora_royxatdagi_xodim_tasdiqsiz_boshatilmaydi_va_sinov_ochiq_qoladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var muhit = await SinovdagiAsync(qoraRoyxat: true);
        var tana = new
        {
            status = "Failed",
            termination = new { terminationDate = Bugun },
        };

        using (var rad = await mijoz.PostAsJsonAsync(
                   new Uri($"/api/probations/{muhit.ProbationId}/resolve", UriKind.Relative),
                   tana,
                   bekorQilish))
        {
            rad.StatusCode.ShouldNotBe(HttpStatusCode.OK, await MatnAsync(rad));

            var (munosabat, sinov) = await HolatAsync(muhit);
            munosabat.Status.ShouldBe(EmploymentStatus.Probation);
            sinov.Status.ShouldBe(ProbationStatus.InProgress);
        }

        using var javob = await mijoz.PostAsJsonAsync(
            new Uri($"/api/probations/{muhit.ProbationId}/resolve?confirm=true", UriKind.Relative),
            tana,
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var (keyin, sinovKeyin) = await HolatAsync(muhit);
        keyin.Status.ShouldBe(EmploymentStatus.Terminated);
        sinovKeyin.Status.ShouldBe(ProbationStatus.Failed);
    }

    /// <summary>
    /// «O'tdi» bilan bo'shatish ma'lumoti — kiritish xatosi, 400.
    /// </summary>
    [Fact]
    public async Task Otdi_bilan_boshatish_400()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var muhit = await SinovdagiAsync();

        using var javob = await mijoz.PostAsJsonAsync(
            new Uri($"/api/probations/{muhit.ProbationId}/resolve", UriKind.Relative),
            new { status = "Passed", termination = new { terminationDate = Bugun } },
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(javob));
        (await MatnAsync(javob)).ShouldContain("sinov.bosatish_otganga_yoq");

        var (munosabat, _) = await HolatAsync(muhit);
        munosabat.Status.ShouldBe(EmploymentStatus.Probation);
    }

    /// <summary>
    /// Muddatidan OLDIN «o'tdi» — xodim darhol doimiy va «Sinovda» ro'yxatidan
    /// chiqadi.
    /// </summary>
    [Fact]
    public async Task Muddatidan_oldin_otdi_xodimni_sinovdagilar_royxatidan_chiqaradi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var muhit = await SinovdagiAsync();

        using var javob = await mijoz.PostAsJsonAsync(
            new Uri($"/api/probations/{muhit.ProbationId}/resolve", UriKind.Relative),
            new { status = "Passed" },
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var (munosabat, _) = await HolatAsync(muhit);
        munosabat.Status.ShouldBe(EmploymentStatus.Active);

        // «Sinov» sahifasi sukut bo'yicha AYNAN shu ro'yxatni so'raydi
        var sinovdagilar = await mijoz.GetFromJsonAsync<ProbationPage>(
            new Uri($"/api/probations?status=InProgress&search={Uri.EscapeDataString(muhit.Familiya)}", UriKind.Relative),
            bekorQilish);

        sinovdagilar!.Items.ShouldNotContain(qator => qator.Id == muhit.ProbationId);
    }

    // ────────────────────────────────────────────────────────────
    //  «Bekor qilindi» — qabul qilinmaydi
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Bekor_qilindi_qabul_qilinmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var muhit = await SinovdagiAsync();

        using var javob = await mijoz.PostAsJsonAsync(
            new Uri($"/api/probations/{muhit.ProbationId}/resolve", UriKind.Relative),
            new { status = "Cancelled" },
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(javob));
        (await MatnAsync(javob)).ShouldContain("sinov.bekor_qilinmaydi");

        var (munosabat, sinov) = await HolatAsync(muhit);
        munosabat.Status.ShouldBe(EmploymentStatus.Probation);
        sinov.Status.ShouldBe(ProbationStatus.InProgress);
    }

    // ────────────────────────────────────────────────────────────
    //  Eski sana bilan kiritilgan qabul
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Muddat kiritilgandayoq tugagan — xodim darhol doimiy, sinov «o'tdi»
    /// MUDDAT TUGAGAN sana bilan.
    /// </summary>
    /// <remarks>
    /// Prod'dagi holat: Мамажонов 2015-07-01 bilan kiritildi va yillab
    /// «sinovda» turardi; yana 18 kishi ham shunday edi.
    /// </remarks>
    [Fact]
    public async Task Muddati_otib_ketgan_qabul_darhol_doimiy_boladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var qabul = Bugun.AddDays(-200);
        var natija = await QabulAsync(mijoz, qabul, sinovKunlari: 90);

        natija.Employment.Status.ShouldBe(nameof(EmploymentStatus.Active));
        natija.Employment.Probation.ShouldNotBeNull("sinov yozuvi baribir bo'ladi — tarix uchun");
        natija.Employment.Probation.Status.ShouldBe(nameof(ProbationStatus.Passed));

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var sinov = await kontekst.Probations
            .AsNoTracking()
            .SingleAsync(p => p.EmploymentId == natija.Employment.Id, bekorQilish);

        sinov.ResolvedAt.ShouldBe(sinov.EndDate, "natija bugun bilan emas, muddat tugagan kun bilan");
        sinov.EndDate.ShouldBeLessThan(Bugun);
    }

    /// <summary>
    /// Chegara: muddat AYNAN bugun tugaydi — bugun hali sinov kuni, xodim
    /// «sinovda» qoladi. Yaqinda boshlangan qabul ham — o'zgarishsiz.
    /// </summary>
    [Theory]
    [InlineData(-90)]   // tugash kuni = bugun
    [InlineData(-10)]   // yangi qabul
    public async Task Muddati_tugamagan_qabul_sinovda_qoladi(int kunOldin)
    {
        using var mijoz = await AdminMijozAsync();

        var natija = await QabulAsync(mijoz, Bugun.AddDays(kunOldin), sinovKunlari: 90);

        natija.Employment.Status.ShouldBe(nameof(EmploymentStatus.Probation));
        natija.Employment.Probation!.Status.ShouldBe(nameof(ProbationStatus.InProgress));
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    private sealed record ProbationRowResponse(Guid Id);

    private sealed record ProbationPage(IReadOnlyList<ProbationRowResponse> Items);

    private sealed record Muhit(Guid EmploymentId, Guid ProbationId, string Familiya);

    /// <summary>Muddati hali tugamagan sinovdagi xodim.</summary>
    private async Task<Muhit> SinovdagiAsync(bool qoraRoyxat = false)
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var belgi = Guid.NewGuid().ToString("N")[..6];
        var familiya = $"Натижа{belgi}";

        var firma = LegalEntity.Create($"Natija MChJ {belgi}").Value;
        kontekst.LegalEntities.Add(firma);

        var xodim = Employee.Create(FullName.Create(familiya, "Синов", null).Value).Value;
        kontekst.Employees.Add(xodim);

        var boshlangan = Bugun.AddDays(-30);

        var munosabat = Employment.Hire(xodim.Id, firma.Id, boshlangan).Value;
        kontekst.Employments.Add(munosabat);

        var sinov = Probation.Start(munosabat.Id, boshlangan, 90).Value;
        kontekst.Probations.Add(sinov);

        if (qoraRoyxat)
        {
            kontekst.Add(EmployeeFlag.Raise(
                xodim.Id,
                EmployeeFlagType.Blacklist,
                boshlangan,
                reason: "Тест").Value);
        }

        await kontekst.SaveChangesAsync(bekorQilish);

        return new Muhit(munosabat.Id, sinov.Id, familiya);
    }

    /// <summary>Bazadagi holat — kuzatuvsiz, yangi kontekstdan.</summary>
    private async Task<(Employment Munosabat, Probation Sinov)> HolatAsync(Muhit muhit)
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var munosabat = await kontekst.Employments
            .AsNoTracking()
            .SingleAsync(m => m.Id == muhit.EmploymentId, bekorQilish);

        var sinov = await kontekst.Probations
            .AsNoTracking()
            .SingleAsync(p => p.Id == muhit.ProbationId, bekorQilish);

        return (munosabat, sinov);
    }

    /// <summary>Interfeys yuboradigan tana bilan ishga qabul.</summary>
    private async Task<HireResultResponse> QabulAsync(HttpClient mijoz, DateOnly qabul, int sinovKunlari)
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var belgi = Guid.NewGuid().ToString("N")[..6];
        var firma = LegalEntity.Create($"Qabul MChJ {belgi}").Value;
        kontekst.LegalEntities.Add(firma);
        await kontekst.SaveChangesAsync(bekorQilish);

        using var javob = await mijoz.PostAsJsonAsync(
            new Uri("/api/employments/hire", UriKind.Relative),
            new HireBody(
                LegalEntityId: firma.Id,
                DepartmentId: null,
                PositionId: null,
                HireDate: qabul,
                Rate: 1m,
                IsPrimary: true,
                SalaryAmount: null,
                SalaryCurrency: "UZS",
                OrderNo: null,
                OrderDate: null,
                Person: new HirePersonBody(
                    LastName: $"Эски{belgi}",
                    FirstName: "Сана",
                    MiddleName: null,
                    BirthDate: null,
                    Pinfl: null,
                    Passport: null,
                    Phone: null,
                    SecondPhone: null,
                    Address: null,
                    Speciality: null,
                    TelegramUrl: null,
                    DirectionIds: []),
                ProbationDays: sinovKunlari),
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        return (await javob.Content.ReadFromJsonAsync<HireResultResponse>(bekorQilish))!;
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

    /// <summary>Xato javobini o'qish — tekshiruv yiqilganda sababi ko'rinishi uchun.</summary>
    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
