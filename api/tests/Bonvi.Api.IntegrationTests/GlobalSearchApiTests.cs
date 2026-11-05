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

    /// <summary>Surname of the «one person» seed, kept apart from <see cref="Familiya"/>.</summary>
    private const string YakkaFamiliya = "Яккаев";

    /// <summary>Digits of the phone shared by a hired candidate and an employee.</summary>
    private const string YakkaTelefon = "901110022";

    private const int YakkaXodimlarSoni = 14;

    /// <summary>Surname of the former-employee and blacklist seed, with its own count.</summary>
    private const string SobiqFamiliya = "Собиқов";

    private const int SobiqXodimlarSoni = 8;

    private static readonly string[] SobiqKorinadiganNomzodlar =
        ["Собиқтел", "Собиқсана", "Муносабатсиз", "Ўчганиш", "Қораисм", "Бўшрад"];

    private static readonly string[] YakkaKorinadiganNomzodlar =
        ["Ёлғиз", "Эгизак", "Санасиз", "Ўчирилган", "Ўчганбоғ", "Бўштелефон"];

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
    //  One person comes up in one place (owner, 2026-11-05)
    // ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Боғлиқ", "hired, linked by the hire event")]
    [InlineData("Телефонномзод", "hired, same phone")]
    [InlineData("Радтел", "rejected, same phone")]
    [InlineData("Форматномзод", "rejected, same phone written differently")]
    [InlineData("Радисм", "rejected, same name")]
    [InlineData("Возисм", "withdrawn, same name")]
    [InlineData("Резервисм", "reserve, same name")]
    [InlineData("Жараёнтел", "in progress, same phone")]
    [InlineData("Жараёнсана", "in progress, same name and birth date")]
    [InlineData("Жараёнбоғ", "in progress, linked by the hire event")]
    public async Task Xodim_bolgan_nomzod_qidiruvda_chiqmaydi(string ism, string sabab)
    {
        await BirOdamTayyorlaAsync();
        using var client = await AdminMijozAsync();

        var natija = await QidirAsync(client, $"{YakkaFamiliya} {ism}");

        natija.Candidates.ShouldNotBeNull();
        natija.Candidates.Total.ShouldBe(0, sabab);
        natija.Candidates.Items.ShouldBeEmpty(sabab);
    }

    [Fact]
    public async Task Lotinda_yozilgan_ismdosh_ham_bitta_odam()
    {
        await BirOdamTayyorlaAsync();
        using var client = await AdminMijozAsync();

        // The candidate is «Yakkayev Ismdosh», the employee «Яккаев Исмдош».
        var natija = await QidirAsync(client, $"{YakkaFamiliya} Исмдош");

        natija.Employees!.Items.Select(x => x.FullName).ShouldBe([$"{YakkaFamiliya} Исмдош"]);
        natija.Candidates!.Total.ShouldBe(0);
        natija.Candidates.Items.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Ёлғиз", "Hired", "hired, no such employee")]
    [InlineData("Эгизак", "InProgress", "in progress, name twin with another birth date and phone")]
    [InlineData("Санасиз", "InProgress", "in progress, name twin and no birth date on either side")]
    [InlineData("Ўчирилган", "Hired", "the same name and phone belong to a deleted employee")]
    [InlineData("Ўчганбоғ", "Hired", "the hire event points to a deleted employee")]
    [InlineData("Бўштелефон", "InProgress", "an empty phone must not match an empty phone")]
    public async Task Xodim_bolmagan_nomzod_qidiruvda_qoladi(string ism, string holat, string sabab)
    {
        await BirOdamTayyorlaAsync();
        using var client = await AdminMijozAsync();

        var natija = await QidirAsync(client, $"{YakkaFamiliya} {ism}");

        natija.Candidates.ShouldNotBeNull();
        natija.Candidates.Total.ShouldBe(1, sabab);
        natija.Candidates.Items.Single().Status.ShouldBe(holat, sabab);
    }

    [Fact]
    public async Task Sanoq_qatorlarga_teng_ikkinchi_guruhda_ham()
    {
        await BirOdamTayyorlaAsync();
        using var client = await AdminMijozAsync();

        var natija = await QidirAsync(client, SobiqFamiliya);

        natija.Candidates!.Total.ShouldBe(SobiqKorinadiganNomzodlar.Length);
        natija.Candidates.Items.Count.ShouldBe(natija.Candidates.Total);
        natija.Candidates.Items
            .Select(x => x.FullName.Split(' ')[1])
            .ShouldBe(SobiqKorinadiganNomzodlar, ignoreOrder: true);

        natija.Employees!.Total.ShouldBe(SobiqXodimlarSoni);
    }

    [Theory]
    [InlineData("Собиқтел", "InProgress", "in progress, same phone as a former employee")]
    [InlineData("Собиқсана", "InProgress", "in progress, same name and birth date as a former employee")]
    [InlineData("Муносабатсиз", "InProgress", "in progress, same phone as an employee with no employment")]
    [InlineData("Ўчганиш", "InProgress", "in progress, same phone as an employee whose only employment is deleted")]
    [InlineData("Қораисм", "Blacklisted", "blacklisted, same phone and name as a current employee")]
    [InlineData("Бўшрад", "Rejected", "closed status, an empty phone must not match an empty phone")]
    public async Task Tirik_ariza_va_qora_royxat_yashirilmaydi(string ism, string holat, string sabab)
    {
        await BirOdamTayyorlaAsync();
        using var client = await AdminMijozAsync();

        var natija = await QidirAsync(client, $"{SobiqFamiliya} {ism}");

        natija.Candidates.ShouldNotBeNull();
        natija.Candidates.Total.ShouldBe(1, sabab);
        natija.Candidates.Items.Single().Status.ShouldBe(holat, sabab);
    }

    [Theory]
    [InlineData("Собиқрад", "rejected, same phone as a former employee")]
    [InlineData("Жўнаётган", "in progress, the employee leaves on a future date, still current")]
    [InlineData("Қораборғ", "blacklisted, linked by the hire event")]
    public async Task Yopiq_holat_va_boglangan_qora_royxat_yashiriladi(string ism, string sabab)
    {
        await BirOdamTayyorlaAsync();
        using var client = await AdminMijozAsync();

        var natija = await QidirAsync(client, $"{SobiqFamiliya} {ism}");

        natija.Candidates.ShouldNotBeNull();
        natija.Candidates.Total.ShouldBe(0, sabab);
        natija.Candidates.Items.ShouldBeEmpty(sabab);
    }

    [Fact]
    public async Task Sanoq_qatorlarga_teng_va_xodimlar_ozgarmaydi()
    {
        await BirOdamTayyorlaAsync();
        using var client = await AdminMijozAsync();

        var natija = await QidirAsync(client, YakkaFamiliya);

        // The count is what proves the filter runs in SQL: hidden rows must not be counted.
        natija.Candidates!.Total.ShouldBe(YakkaKorinadiganNomzodlar.Length);
        natija.Candidates.Items.Count.ShouldBe(natija.Candidates.Total);
        natija.Candidates.Items
            .Select(x => x.FullName.Split(' ')[1])
            .ShouldBe(YakkaKorinadiganNomzodlar, ignoreOrder: true);

        // Employees are not filtered at all; only the soft-deleted ones are missing.
        natija.Employees!.Total.ShouldBe(YakkaXodimlarSoni);
    }

    [Fact]
    public async Task Telefon_bilan_qidirganda_ham_bitta_odam()
    {
        await BirOdamTayyorlaAsync();
        using var client = await AdminMijozAsync();

        var natija = await QidirAsync(client, YakkaTelefon);

        natija.Employees!.Items.Select(x => x.FullName).ShouldBe([$"{YakkaFamiliya} Телефонходим"]);
        natija.Candidates!.Total.ShouldBe(0);
        natija.Candidates.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Xodimlarni_kormaydigan_foydalanuvchiga_nomzod_yashirilmaydi()
    {
        await BirOdamTayyorlaAsync();
        using var client = await RuxsatliMijozAsync(Permissions.RecruitingView);

        // No employee group here, so hiding the candidate would leave no row at all.
        var natija = await QidirAsync(client, $"{YakkaFamiliya} Телефонномзод");

        natija.Employees.ShouldBeNull();
        natija.Candidates!.Total.ShouldBe(1);
        natija.Candidates.Items.Single().Status.ShouldBe("Hired");
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

    /// <summary>
    /// Pairs of one employee and one candidate, each pair matching for exactly one reason, so a
    /// test failure names the rule that broke. First names differ inside a pair unless the name
    /// is the reason.
    /// </summary>
    private async Task BirOdamTayyorlaAsync()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        if (await kontekst.Employees.AnyAsync(x => x.FullName.LastName == YakkaFamiliya, bekorQilish))
        {
            return;
        }

        var firma = LegalEntity.Create("Якка МЧЖ").Value;
        var vakansiya = Vacancy.Open("Якка вакансияси", new DateOnly(2026, 9, 1), openings: 2).Value;
        var sana = new DateOnly(2026, 10, 1);
        var tugilgan = new DateOnly(1992, 2, 2);

        var telefon = PhoneNumber.Create($"+998{YakkaTelefon}").Value;
        var radTelefon = PhoneNumber.Create("+998901110023").Value;
        var formatTelefon = PhoneNumber.Create("+998901110024").Value;
        var jarayonTelefon = PhoneNumber.Create("+998901110025").Value;
        var egizakXodimTelefon = PhoneNumber.Create("+998901110026").Value;
        var egizakNomzodTelefon = PhoneNumber.Create("+998901110027").Value;
        var ochirilganTelefon = PhoneNumber.Create("+998901110028").Value;

        var boglangan = YakkaXodim("Боғланган");
        var jarayonBoglangan = YakkaXodim("Боғланганжараён");
        var ochirilganBoglangan = YakkaXodim("Ўчирилганбоғланган");
        var ochirilgan = YakkaXodim("Ўчирилган", ochirilganTelefon);
        ochirilgan.DeletedAt = DateTimeOffset.UtcNow;
        ochirilganBoglangan.DeletedAt = DateTimeOffset.UtcNow;
        var boshXodim = YakkaXodim("Бўшходим");
        var egizakXodim = YakkaXodim("Эгизак", egizakXodimTelefon, new DateOnly(1990, 1, 1));
        var sanasizXodim = YakkaXodim("Санасиз");
        var jarayonXodim = YakkaXodim("Жараёнходим", jarayonTelefon);
        var jarayonSanaXodim = YakkaXodim("Жараёнсана", tugilgan: tugilgan);
        var formatXodim = YakkaXodim("Форматходим", formatTelefon);

        var xodimlar = new[]
        {
            boglangan,
            jarayonBoglangan,
            boshXodim,
            formatXodim,
            egizakXodim,
            sanasizXodim,
            jarayonXodim,
            jarayonSanaXodim,
            YakkaXodim("Телефонходим", telefon),
            YakkaXodim("Исмдош"),
            YakkaXodim("Радходим", radTelefon),
            YakkaXodim("Радисм"),
            YakkaXodim("Возисм"),
            YakkaXodim("Резервисм"),
        };
        xodimlar.Length.ShouldBe(YakkaXodimlarSoni);

        var boglanganNomzod = YakkaNomzod("Боғлиқ", vakansiya.Id);
        boglanganNomzod.MarkHired(sana).IsSuccess.ShouldBeTrue();

        var telefonli = YakkaNomzod("Телефонномзод", vakansiya.Id, telefon);
        telefonli.MarkHired(sana).IsSuccess.ShouldBeTrue();

        var lotincha = Candidate.Register(FullName.Create("Yakkayev", "Ismdosh", null).Value, vakansiya.Id).Value;
        lotincha.MarkHired(sana).IsSuccess.ShouldBeTrue();

        var yolgiz = YakkaNomzod("Ёлғиз", vakansiya.Id);
        yolgiz.MarkHired(sana).IsSuccess.ShouldBeTrue();

        var radTel = YakkaNomzod("Радтел", vakansiya.Id, radTelefon);
        radTel.Reject(sana).IsSuccess.ShouldBeTrue();

        var formatNomzod = YakkaNomzod("Форматномзод", vakansiya.Id, formatTelefon);
        formatNomzod.Reject(sana).IsSuccess.ShouldBeTrue();

        var radIsm = YakkaNomzod("Радисм", vakansiya.Id);
        radIsm.Reject(sana).IsSuccess.ShouldBeTrue();

        var vozIsm = YakkaNomzod("Возисм", vakansiya.Id);
        vozIsm.Withdraw(sana).IsSuccess.ShouldBeTrue();

        var rezervIsm = YakkaNomzod("Резервисм", vakansiya.Id);
        rezervIsm.MoveToReserve(sana).IsSuccess.ShouldBeTrue();

        var egizak = YakkaNomzod("Эгизак", vakansiya.Id, egizakNomzodTelefon, new DateOnly(1995, 5, 5));
        var sanasiz = YakkaNomzod("Санасиз", vakansiya.Id);
        var jarayonTel = YakkaNomzod("Жараёнтел", vakansiya.Id, jarayonTelefon);
        var jarayonSana = YakkaNomzod("Жараёнсана", vakansiya.Id, tugilgan: tugilgan);
        var jarayonBog = YakkaNomzod("Жараёнбоғ", vakansiya.Id);

        var ochirilganNomzod = YakkaNomzod("Ўчирилган", vakansiya.Id, ochirilganTelefon);
        ochirilganNomzod.MarkHired(sana).IsSuccess.ShouldBeTrue();

        var ochirilganBog = YakkaNomzod("Ўчганбоғ", vakansiya.Id);
        ochirilganBog.MarkHired(sana).IsSuccess.ShouldBeTrue();

        var boshNomzod = YakkaNomzod("Бўштелефон", vakansiya.Id);

        // Second group: who counts as a CURRENT employee, and the blacklist.
        var sobiqTelefon = PhoneNumber.Create("+998901110031").Value;
        var sobiqRadTelefon = PhoneNumber.Create("+998901110032").Value;
        var munosabatsizTelefon = PhoneNumber.Create("+998901110033").Value;
        var ketayotganTelefon = PhoneNumber.Create("+998901110034").Value;
        var qoraTelefon = PhoneNumber.Create("+998901110035").Value;
        var ochganIshTelefon = PhoneNumber.Create("+998901110036").Value;
        var sobiqTugilgan = new DateOnly(1985, 3, 3);

        var sobiqXodim = SobiqXodim("Собиқходим", sobiqTelefon);
        var sobiqSanaXodim = SobiqXodim("Собиқсана", tugilgan: sobiqTugilgan);
        var sobiqRadXodim = SobiqXodim("Собиқрадходим", sobiqRadTelefon);
        var munosabatsizXodim = SobiqXodim("Ходимсиз", munosabatsizTelefon);
        var ketayotgan = SobiqXodim("Кетаётган", ketayotganTelefon);
        var qoraXodim = SobiqXodim("Қораисм", qoraTelefon);
        var qoraBogXodim = SobiqXodim("Қораходим");
        var ochganIshXodim = SobiqXodim("Ўчганмеҳнат", ochganIshTelefon);

        var sobiqTel = SobiqNomzod("Собиқтел", vakansiya.Id, sobiqTelefon);
        var sobiqSana = SobiqNomzod("Собиқсана", vakansiya.Id, tugilgan: sobiqTugilgan);
        var munosabatsizNomzod = SobiqNomzod("Муносабатсиз", vakansiya.Id, munosabatsizTelefon);
        var ochganIsh = SobiqNomzod("Ўчганиш", vakansiya.Id, ochganIshTelefon);
        var jonayotgan = SobiqNomzod("Жўнаётган", vakansiya.Id, ketayotganTelefon);

        var sobiqRad = SobiqNomzod("Собиқрад", vakansiya.Id, sobiqRadTelefon);
        sobiqRad.Reject(sana).IsSuccess.ShouldBeTrue();

        var boshRad = SobiqNomzod("Бўшрад", vakansiya.Id);
        boshRad.Reject(sana).IsSuccess.ShouldBeTrue();

        var qoraNomzod = SobiqNomzod("Қораисм", vakansiya.Id, qoraTelefon);
        qoraNomzod.Blacklist(sana, "Тест").IsSuccess.ShouldBeTrue();

        var qoraBog = SobiqNomzod("Қораборғ", vakansiya.Id);
        qoraBog.Blacklist(sana, "Тест").IsSuccess.ShouldBeTrue();

        Employment Ishlaydi(Employee xodim)
            => Employment.Hire(xodim.Id, firma.Id, new DateOnly(2026, 1, 10)).Value;

        Employment Ketgan(Employee xodim)
        {
            var munosabat = Employment.Hire(xodim.Id, firma.Id, new DateOnly(2020, 1, 10)).Value;
            munosabat.CompleteProbation(new DateOnly(2020, 4, 10)).IsSuccess.ShouldBeTrue();
            munosabat.Terminate(new DateOnly(2023, 6, 1), null).IsSuccess.ShouldBeTrue();

            return munosabat;
        }

        var ketayotganIsh = Ishlaydi(ketayotgan);
        var ochganMunosabat = Ishlaydi(ochganIshXodim);
        ochganMunosabat.DeletedAt = DateTimeOffset.UtcNow;

        Employment Qabul(Employee xodim, Candidate nomzod)
            => Employment.Hire(xodim.Id, firma.Id, new DateOnly(2026, 10, 2), candidateId: nomzod.Id).Value;

        kontekst.Add(firma);
        kontekst.Add(vakansiya);
        kontekst.AddRange(xodimlar);
        kontekst.AddRange(ochirilgan, ochirilganBoglangan);
        kontekst.AddRange(
            boglanganNomzod, telefonli, lotincha, yolgiz, radTel, formatNomzod, radIsm, vozIsm, rezervIsm,
            egizak, sanasiz, jarayonTel, jarayonSana, jarayonBog, ochirilganNomzod,
            ochirilganBog, boshNomzod);
        kontekst.AddRange(
            Qabul(boglangan, boglanganNomzod),
            Qabul(jarayonBoglangan, jarayonBog),
            Qabul(ochirilganBoglangan, ochirilganBog));
        kontekst.AddRange(
            Ishlaydi(boshXodim),
            Ishlaydi(egizakXodim),
            Ishlaydi(sanasizXodim),
            Ishlaydi(jarayonXodim),
            Ishlaydi(jarayonSanaXodim));
        kontekst.AddRange(
            sobiqXodim, sobiqSanaXodim, sobiqRadXodim, munosabatsizXodim, ketayotgan, qoraXodim,
            qoraBogXodim, ochganIshXodim);
        kontekst.AddRange(
            sobiqTel, sobiqSana, munosabatsizNomzod, ochganIsh, jonayotgan, sobiqRad, boshRad,
            qoraNomzod, qoraBog);
        kontekst.AddRange(
            Ketgan(sobiqXodim),
            Ketgan(sobiqSanaXodim),
            Ketgan(sobiqRadXodim),
            ketayotganIsh,
            ochganMunosabat,
            Ishlaydi(qoraXodim),
            Qabul(qoraBogXodim, qoraBog));

        await kontekst.SaveChangesAsync(bekorQilish);

        // The domain cannot produce these values; older rows and direct imports can.
        await kontekst.Database.ExecuteSqlAsync(
            $"UPDATE employees SET phone = '' WHERE id = {boshXodim.Id}",
            bekorQilish);
        await kontekst.Database.ExecuteSqlAsync(
            $"UPDATE candidates SET phone = '' WHERE id = {boshNomzod.Id}",
            bekorQilish);
        await kontekst.Database.ExecuteSqlAsync(
            $"UPDATE candidates SET phone = '' WHERE id = {boshRad.Id}",
            bekorQilish);
        await kontekst.Database.ExecuteSqlAsync(
            $"UPDATE employments SET termination_date = {new DateOnly(2099, 1, 1)} WHERE id = {ketayotganIsh.Id}",
            bekorQilish);
        await kontekst.Database.ExecuteSqlAsync(
            $"UPDATE employees SET phone = '+998 90 111 00 24' WHERE id = {formatXodim.Id}",
            bekorQilish);
    }

    private static Employee YakkaXodim(string ism, PhoneNumber? telefon = null, DateOnly? tugilgan = null)
        => FamiliyaliXodim(YakkaFamiliya, ism, telefon, tugilgan);

    private static Employee SobiqXodim(string ism, PhoneNumber? telefon = null, DateOnly? tugilgan = null)
        => FamiliyaliXodim(SobiqFamiliya, ism, telefon, tugilgan);

    private static Employee FamiliyaliXodim(string familiya, string ism, PhoneNumber? telefon, DateOnly? tugilgan)
    {
        var xodim = Employee.Create(FullName.Create(familiya, ism, null).Value, tugilgan).Value;
        xodim.SetPhones(telefon);

        return xodim;
    }

    private static Candidate SobiqNomzod(
        string ism,
        Guid vakansiyaId,
        PhoneNumber? telefon = null,
        DateOnly? tugilgan = null)
        => Candidate.Register(
            FullName.Create(SobiqFamiliya, ism, null).Value,
            vakansiyaId,
            birthDate: tugilgan,
            phone: telefon).Value;

    private static Candidate YakkaNomzod(
        string ism,
        Guid vakansiyaId,
        PhoneNumber? telefon = null,
        DateOnly? tugilgan = null)
        => Candidate.Register(
            FullName.Create(YakkaFamiliya, ism, null).Value,
            vakansiyaId,
            birthDate: tugilgan,
            phone: telefon).Value;

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
