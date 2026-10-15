using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Organization;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Global qidiruv (<c>GET /api/search</c>) — barcha bo'limlar, barcha holatlar.
/// </summary>
/// <remarks>
/// <para>
/// Asosiy talab (egasi, 2026-10-15): yuqori paneldagi qidiruv HAMMA joydan
/// topishi kerak — ishlayotgan va ishdan ketgan xodimlar, jarayondagi, rad
/// etilgan, o'zi voz kechgan, rezerv, qora ro'yxatdagi va ishga olingan
/// nomzodlar, Excel arxivi va vakansiyalar. Bo'limlardagi ro'yxatlar
/// ularning ko'pini sukut bo'yicha YASHIRADI, ya'ni bu testlar «yashirish
/// qoidasi global qidiruvga sizib kirmadimi» degan savolni tekshiradi.
/// </para>
/// <para>
/// Urug' bir marta qo'yiladi va idempotent — sinf o'z bazasiga ega
/// (<see cref="BonviApiFactory"/>), testlar esa uni baham ko'radi.
/// </para>
/// </remarks>
public sealed class GlobalSearchApiTests : IClassFixture<BonviApiFactory>
{
    /// <summary>Barcha urug' yozuvlarining umumiy familiyasi.</summary>
    /// <remarks>
    /// Maxsus harfsiz — lotin yozuvi («Globalov») qidiruv kaliti bo'yicha
    /// AYNAN shu kirill familiyaga mos keladi («Каримов»/«Karimov» kabi).
    /// </remarks>
    private const string Familiya = "Глобалов";

    /// <summary>«Shovqin» — qidiruv haqiqatan toraytirayotganini ko'rsatadi.</summary>
    private const string ShovqinFamiliya = "Шовқинов";

    private const string Jshshir = "31503904234561";

    private const string Vakansiya = "Глобал синов вакансияси";

    private readonly BonviApiFactory _factory;

    public GlobalSearchApiTests(BonviApiFactory factory) => _factory = factory;

    // ────────────────────────────────────────────────────────────
    //  Xodimlar — barcha holatlar
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Xodimlar_barcha_holatda_topiladi_ishlayotganlar_birinchi()
    {
        await TayyorlaAsync();
        using var client = await AdminMijozAsync();

        var natija = await QidirAsync(client, Familiya);

        natija.Employees.ShouldNotBeNull();
        natija.Employees.Total.ShouldBe(4);

        // Ishlayotganlar (doimiy «Ишлайди» va sinovdagi «Синов») ism
        // bo'yicha, keyin ishdan ketgan, oxirida munosabati yo'q.
        natija.Employees.Items
            .Select(x => x.FullName.Split(' ')[1])
            .ShouldBe(["Ишлайди", "Синов", "Кетган", "Муносабатсиз"]);

        natija.Employees.Items.Select(x => x.Status)
            .ShouldBe(["Active", "Probation", "Terminated", null]);
    }

    [Fact]
    public async Task Xodim_jshshir_va_lotin_yozuvi_bilan_ham_topiladi()
    {
        var urug = await TayyorlaAsync();
        using var client = await AdminMijozAsync();

        var jshshirBilan = await QidirAsync(client, Jshshir);
        jshshirBilan.Employees!.Items.Select(x => x.Id).ShouldBe([urug.IshlayotganId]);

        var lotinda = await QidirAsync(client, "Globalov");
        lotinda.Employees!.Total.ShouldBe(4);
    }

    [Fact]
    public async Task Begona_xodim_topilmaydi_va_belgilar_qaytadi()
    {
        var urug = await TayyorlaAsync();
        using var client = await AdminMijozAsync();

        var natija = await QidirAsync(client, Familiya);

        natija.Employees!.Items.ShouldNotContain(x => x.Id == urug.ShovqinId);

        // Qora ro'yxat belgisi qatorda ko'rinadi — HR uni darhol ko'rishi kerak.
        natija.Employees.Items.Single(x => x.Id == urug.KetganId).Flags.ShouldBe(["Blacklist"]);

        var shovqin = await QidirAsync(client, ShovqinFamiliya);
        shovqin.Employees!.Items.Select(x => x.Id).ShouldBe([urug.ShovqinId]);
    }

    // ────────────────────────────────────────────────────────────
    //  Nomzodlar — barcha holatlar va Excel arxivi
    // ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Жараён", "InProgress")]
    [InlineData("Рад", "Rejected")]
    [InlineData("Возкечган", "Withdrawn")]
    [InlineData("Резерв", "Reserve")]
    [InlineData("Қора", "Blacklisted")]
    [InlineData("Олинган", "Hired")]
    [InlineData("Архив", "InProgress")]
    public async Task Nomzod_har_qanday_holatda_topiladi(string ism, string holat)
    {
        await TayyorlaAsync();
        using var client = await AdminMijozAsync();

        var natija = await QidirAsync(client, $"{Familiya} {ism}");

        natija.Candidates.ShouldNotBeNull();
        natija.Candidates.Total.ShouldBe(1, $"«{ism}» nomzodi topilmadi");
        natija.Candidates.Items.Single().Status.ShouldBe(holat);
        natija.Candidates.Items.Single().Vacancy.ShouldBe(Vakansiya);
    }

    [Fact]
    public async Task Nomzodlar_soni_toliq_qatorlar_esa_cheklangan()
    {
        await TayyorlaAsync();
        using var client = await AdminMijozAsync();

        var natija = await QidirAsync(client, Familiya);

        natija.Candidates!.Total.ShouldBe(7);
        natija.Candidates.Items.Count.ShouldBe(6);
    }

    [Fact]
    public async Task Vakansiya_nomi_bilan_topiladi()
    {
        var urug = await TayyorlaAsync();
        using var client = await AdminMijozAsync();

        var natija = await QidirAsync(client, "Глобал синов");

        natija.Vacancies.ShouldNotBeNull();
        natija.Vacancies.Items.Select(x => x.Id).ShouldBe([urug.VakansiyaId]);
    }

    // ────────────────────────────────────────────────────────────
    //  Ruxsatlar (RBAC)
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Ruxsatsiz_foydalanuvchiga_403()
    {
        using var client = await RuxsatliMijozAsync();

        using var javob = await client.GetAsync(Manzil(Familiya), TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Tokensiz_sorovga_401()
    {
        using var client = _factory.CreateClient();

        using var javob = await client.GetAsync(Manzil(Familiya), TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Faqat_xodimlar_ruxsati_nomzod_va_vakansiyani_kormaydi()
    {
        await TayyorlaAsync();
        using var client = await RuxsatliMijozAsync(Permissions.EmployeesView);

        var natija = await QidirAsync(client, Familiya);

        natija.Employees.ShouldNotBeNull();
        natija.Employees.Total.ShouldBe(4);
        natija.Candidates.ShouldBeNull();
        natija.Vacancies.ShouldBeNull();
    }

    [Fact]
    public async Task Faqat_rekruting_ruxsati_xodimlarni_kormaydi()
    {
        await TayyorlaAsync();
        using var client = await RuxsatliMijozAsync(Permissions.RecruitingView);

        var natija = await QidirAsync(client, Familiya);

        natija.Employees.ShouldBeNull();
        natija.Candidates.ShouldNotBeNull();
        natija.Candidates.Total.ShouldBe(7);
        natija.Vacancies.ShouldNotBeNull();
    }

    [Fact]
    public async Task Bir_belgili_matn_qidirilmaydi()
    {
        await TayyorlaAsync();
        using var client = await AdminMijozAsync();

        var natija = await QidirAsync(client, "Г");

        natija.Employees!.Total.ShouldBe(0);
        natija.Employees.Items.ShouldBeEmpty();
        natija.Candidates!.Total.ShouldBe(0);
        natija.Vacancies!.Total.ShouldBe(0);
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    private sealed record SearchResponse(
        string Query,
        EmployeesGroup? Employees,
        CandidatesGroup? Candidates,
        VacanciesGroup? Vacancies);

    private sealed record EmployeesGroup(int Total, List<EmployeeHit> Items);

    private sealed record EmployeeHit(Guid Id, string FullName, string? Status, List<string> Flags);

    private sealed record CandidatesGroup(int Total, List<CandidateHit> Items);

    private sealed record CandidateHit(Guid Id, string FullName, string Status, string? Vacancy);

    private sealed record VacanciesGroup(int Total, List<VacancyHit> Items);

    private sealed record VacancyHit(Guid Id, string Title, string Status);

    private sealed record Urug(Guid IshlayotganId, Guid KetganId, Guid ShovqinId, Guid VakansiyaId);

    private static Uri Manzil(string matn)
        => new($"/api/search?q={Uri.EscapeDataString(matn)}", UriKind.Relative);

    private static async Task<SearchResponse> QidirAsync(HttpClient client, string matn)
    {
        using var javob = await client.GetAsync(Manzil(matn), TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var natija = await javob.Content.ReadFromJsonAsync<SearchResponse>(TestContext.Current.CancellationToken);

        natija.ShouldNotBeNull();
        return natija;
    }

    /// <summary>
    /// To'rt xodim (doimiy, sinovdagi, ishdan ketgan, munosabatsiz), bitta
    /// «shovqin» xodim, bitta vakansiya va har bir holatdagi nomzod.
    /// </summary>
    /// <remarks>
    /// Yozuvlar domen fabrikalari orqali, to'g'ridan-to'g'ri bazaga
    /// (<c>ListSearchApiTests</c> dagi bilan bir xil sabab).
    /// </remarks>
    private async Task<Urug> TayyorlaAsync()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var pinfl = Pinfl.Create(Jshshir).Value;

        var mavjud = await kontekst.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Pinfl == pinfl, bekorQilish);

        if (mavjud is not null)
        {
            var ketganMavjud = await kontekst.Employees.AsNoTracking()
                .FirstAsync(x => x.FullName.FirstName == "Кетган", bekorQilish);
            var shovqinMavjud = await kontekst.Employees.AsNoTracking()
                .FirstAsync(x => x.FullName.LastName == ShovqinFamiliya, bekorQilish);
            var vakansiyaMavjud = await kontekst.Vacancies.AsNoTracking()
                .FirstAsync(x => x.Title == Vakansiya, bekorQilish);

            return new Urug(mavjud.Id, ketganMavjud.Id, shovqinMavjud.Id, vakansiyaMavjud.Id);
        }

        var firma = LegalEntity.Create("Глобал Қидирув МЧЖ").Value;
        var bolim = Department.Create("Глобал бўлими").Value;
        var lavozim = Position.Create("Глобал мутахассиси").Value;

        var ishlayotgan = Xodim("Ишлайди");
        _ = ishlayotgan.SetPinfl(pinfl);
        var sinovdagi = Xodim("Синов");
        var ketgan = Xodim("Кетган");
        var munosabatsiz = Xodim("Муносабатсиз");
        var shovqin = Employee.Create(FullName.Create(ShovqinFamiliya, "Азиз", null).Value).Value;

        var doimiy = Employment.Hire(ishlayotgan.Id, firma.Id, new DateOnly(2024, 1, 10), bolim.Id, lavozim.Id).Value;
        doimiy.CompleteProbation(new DateOnly(2024, 4, 10)).IsSuccess.ShouldBeTrue();

        var sinov = Employment.Hire(sinovdagi.Id, firma.Id, new DateOnly(2026, 9, 1), bolim.Id, lavozim.Id).Value;

        var ketganMunosabat = Employment.Hire(ketgan.Id, firma.Id, new DateOnly(2020, 1, 10), bolim.Id, lavozim.Id).Value;
        ketganMunosabat.CompleteProbation(new DateOnly(2020, 4, 10)).IsSuccess.ShouldBeTrue();
        ketganMunosabat.Terminate(new DateOnly(2023, 6, 1), null).IsSuccess.ShouldBeTrue();

        var shovqinMunosabat = Employment.Hire(shovqin.Id, firma.Id, new DateOnly(2024, 2, 1), bolim.Id, lavozim.Id).Value;

        var qoraBelgi = EmployeeFlag.Raise(
            ketgan.Id,
            EmployeeFlagType.Blacklist,
            new DateOnly(2023, 6, 2),
            reason: "Тест").Value;

        var vakansiya = Vacancy.Open(Vakansiya, new DateOnly(2026, 9, 1), openings: 2).Value;
        var sana = new DateOnly(2026, 10, 1);

        var jarayonda = Nomzod("Жараён", vakansiya.Id);

        var rad = Nomzod("Рад", vakansiya.Id);
        rad.Reject(sana).IsSuccess.ShouldBeTrue();

        var vozKechgan = Nomzod("Возкечган", vakansiya.Id);
        vozKechgan.Withdraw(sana).IsSuccess.ShouldBeTrue();

        var rezerv = Nomzod("Резерв", vakansiya.Id);
        rezerv.MoveToReserve(sana).IsSuccess.ShouldBeTrue();

        var qora = Nomzod("Қора", vakansiya.Id);
        qora.Blacklist(sana, "Тест").IsSuccess.ShouldBeTrue();

        var olingan = Nomzod("Олинган", vakansiya.Id);
        olingan.MarkHired(sana).IsSuccess.ShouldBeTrue();

        // Jobster/Excel arxivi — ro'yxatda sukut bo'yicha yashirin.
        var arxiv = Candidate.Register(
            FullName.Create(Familiya, "Архив", null).Value,
            vakansiya.Id,
            CandidateSource.Excel).Value;

        kontekst.AddRange(firma, bolim, lavozim);
        kontekst.AddRange(ishlayotgan, sinovdagi, ketgan, munosabatsiz, shovqin);
        kontekst.AddRange(doimiy, sinov, ketganMunosabat, shovqinMunosabat);
        kontekst.Add(qoraBelgi);
        kontekst.Add(vakansiya);
        kontekst.AddRange(jarayonda, rad, vozKechgan, rezerv, qora, olingan, arxiv);

        await kontekst.SaveChangesAsync(bekorQilish);

        return new Urug(ishlayotgan.Id, ketgan.Id, shovqin.Id, vakansiya.Id);
    }

    private static Employee Xodim(string ism)
        => Employee.Create(FullName.Create(Familiya, ism, null).Value).Value;

    private static Candidate Nomzod(string ism, Guid vakansiyaId)
        => Candidate.Register(FullName.Create(Familiya, ism, null).Value, vakansiyaId).Value;

    private async Task<HttpClient> RuxsatliMijozAsync(params string[] ruxsatlar)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAccounts.TokenAsync(_factory, ruxsatlar));

        return client;
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
}
