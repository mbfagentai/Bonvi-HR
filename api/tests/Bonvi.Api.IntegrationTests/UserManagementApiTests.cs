using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using Bonvi.Domain.Auditing;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Foydalanuvchi (test uchun).</summary>
public sealed record UserSummaryResponse(
    Guid Id,
    string Login,
    string FullName,
    string ShortName,
    bool IsActive,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset? LockedUntil,
    string[] RoleCodes,
    string[] RoleNames);

/// <summary>
/// Foydalanuvchilarni boshqarish HTTP qatlamining uchi-uchiga testlari.
/// </summary>
/// <remarks>
/// Haqiqiy PostgreSQL ustida ishlaydi. Testlar bitta bazani baham ko'radi,
/// shu sababli har bir test o'ziga xos telefon raqami ishlatadi.
/// </remarks>
public sealed class UserManagementApiTests : IClassFixture<BonviApiFactory>
{
    private static readonly string[] HrManagerRoleOnly = ["hr_manager"];
    private static readonly string[] AdminRoleOnly = ["admin"];

    private static int _telefonSonagichi;

    private readonly BonviApiFactory _factory;

    public UserManagementApiTests(BonviApiFactory factory) => _factory = factory;

    // ────────────────────────────────────────────────────────────
    //  Ruxsatlar
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Tokensiz_royxat_401_qaytaradi()
    {
        using var client = _factory.CreateClient();

        using var javob = await client.GetAsync(
            new Uri("/api/users", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Users_manage_ruxsati_yoqligi_403_beradi()
    {
        using var adminClient = await AdminMijozAsync();
        using var hrClient = await HrManagerMijozAsync(adminClient);

        using var royxat = await hrClient.GetAsync(
            new Uri("/api/users", UriKind.Relative),
            TestContext.Current.CancellationToken);

        royxat.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var yaratish = await hrClient.PostAsJsonAsync(
            new Uri("/api/users", UriKind.Relative),
            new
            {
                login = NoyobTelefon(),
                lastName = "Familiya",
                firstName = "Ism",
                password = "Parol_12345",
                roleCodes = HrManagerRoleOnly,
            },
            TestContext.Current.CancellationToken);

        // HR menejer boshqa hech kimning hisobini yarata olmaydi — foydalanuvchi
        // boshqaruvi butunlay administratorga tegishli
        yaratish.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ────────────────────────────────────────────────────────────
    //  To'liq hayot sikli
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Foydalanuvchining_toliq_hayot_sikli_ishlaydi()
    {
        using var client = await AdminMijozAsync();
        var telefon = NoyobTelefon();
        const string boshlangichParol = "Boshlangich_Parol1";

        // 1. Yaratish
        using var yaratish = await client.PostAsJsonAsync(
            new Uri("/api/users", UriKind.Relative),
            new
            {
                login = telefon,
                lastName = "Каримов",
                firstName = "Жасур",
                password = boshlangichParol,
                roleCodes = HrManagerRoleOnly,
            },
            TestContext.Current.CancellationToken);

        yaratish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yaratish));

        var yaratilgan = await yaratish.Content.ReadFromJsonAsync<UserSummaryResponse>(
            TestContext.Current.CancellationToken);

        yaratilgan.ShouldNotBeNull();
        yaratilgan.IsActive.ShouldBeTrue();
        yaratilgan.RoleCodes.ShouldContain("hr_manager");
        // Parol javobda hech qachon qaytmasligi kerak
        (await MatnAsync(yaratish)).ShouldNotContain(boshlangichParol);

        // 2. Ro'yxatda ko'rinishi
        using var royxat1 = await client.GetAsync(
            new Uri("/api/users", UriKind.Relative),
            TestContext.Current.CancellationToken);

        var hammasi = await royxat1.Content.ReadFromJsonAsync<List<UserSummaryResponse>>(
            TestContext.Current.CancellationToken);

        hammasi.ShouldNotBeNull();
        hammasi.ShouldContain(u => u.Id == yaratilgan.Id);

        // 3. Yangi hisob bilan haqiqatan kirish mumkin
        using var yangiHisobKirish = await _factory.CreateClient().PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new { login = telefon, password = boshlangichParol },
            TestContext.Current.CancellationToken);

        yangiHisobKirish.StatusCode.ShouldBe(HttpStatusCode.OK);
        var eskiSessiya = await yangiHisobKirish.Content.ReadFromJsonAsync<AuthResponse>(
            TestContext.Current.CancellationToken);
        eskiSessiya.ShouldNotBeNull();

        // 4. Tahrirlash — ism va rol o'zgaradi
        using var tahrir = await client.PutAsJsonAsync(
            new Uri($"/api/users/{yaratilgan.Id}", UriKind.Relative),
            new
            {
                login = telefon,
                lastName = "Каримов",
                firstName = "Жасур",
                middleName = "Baxtiyorovich",
                roleCodes = HrManagerRoleOnly,
            },
            TestContext.Current.CancellationToken);

        tahrir.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(tahrir));
        var tahrirlangan = await tahrir.Content.ReadFromJsonAsync<UserSummaryResponse>(
            TestContext.Current.CancellationToken);
        tahrirlangan.ShouldNotBeNull();
        tahrirlangan.FullName.ShouldContain("Baxtiyorovich");

        // 5. Parolni tiklash — eski sessiya bekor bo'lishi kerak
        const string yangiParol = "Yangilangan_Parol1";

        using var tiklash = await client.PostAsJsonAsync(
            new Uri($"/api/users/{yaratilgan.Id}/reset-password", UriKind.Relative),
            new { newPassword = yangiParol },
            TestContext.Current.CancellationToken);

        tiklash.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(tiklash));

        using var eskiTokenBilanYangilash = await _factory.CreateClient().PostAsJsonAsync(
            new Uri("/api/auth/refresh", UriKind.Relative),
            new { refreshToken = eskiSessiya.RefreshToken },
            TestContext.Current.CancellationToken);

        eskiTokenBilanYangilash.StatusCode.ShouldBe(
            HttpStatusCode.Forbidden, "parol tiklanganda eski sessiya bekor qilinishi kerak");

        // Yangi parol bilan kirish ishlaydi
        using var yangiParolBilanKirish = await _factory.CreateClient().PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new { login = telefon, password = yangiParol },
            TestContext.Current.CancellationToken);

        yangiParolBilanKirish.StatusCode.ShouldBe(HttpStatusCode.OK);

        // 6. Faolsizlantirish — kirish yopiladi
        using var faolsizlantirish = await client.PostAsync(
            new Uri($"/api/users/{yaratilgan.Id}/deactivate", UriKind.Relative),
            content: null,
            TestContext.Current.CancellationToken);

        faolsizlantirish.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(faolsizlantirish));

        using var faolsizKirish = await _factory.CreateClient().PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new { login = telefon, password = yangiParol },
            TestContext.Current.CancellationToken);

        faolsizKirish.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // 7. Qayta faollashtirish
        using var faollashtirish = await client.PostAsync(
            new Uri($"/api/users/{yaratilgan.Id}/activate", UriKind.Relative),
            content: null,
            TestContext.Current.CancellationToken);

        faollashtirish.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var qaytaKirish = await _factory.CreateClient().PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new { login = telefon, password = yangiParol },
            TestContext.Current.CancellationToken);

        qaytaKirish.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Band_telefon_raqami_bilan_yaratish_409_qaytaradi()
    {
        using var client = await AdminMijozAsync();
        var telefon = NoyobTelefon();

        using var birinchi = await client.PostAsJsonAsync(
            new Uri("/api/users", UriKind.Relative),
            new
            {
                login = telefon,
                lastName = "Familiya",
                firstName = "Ism",
                password = "Parol_12345",
                roleCodes = HrManagerRoleOnly,
            },
            TestContext.Current.CancellationToken);

        birinchi.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(birinchi));

        using var ikkinchi = await client.PostAsJsonAsync(
            new Uri("/api/users", UriKind.Relative),
            new
            {
                login = telefon,
                lastName = "Boshqa",
                firstName = "Odam",
                password = "Parol_12345",
                roleCodes = HrManagerRoleOnly,
            },
            TestContext.Current.CancellationToken);

        ikkinchi.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await MatnAsync(ikkinchi)).ShouldContain("foydalanuvchi.login_band");
    }

    [Fact]
    public async Task Qisqa_parol_bilan_yaratish_400_qaytaradi()
    {
        using var client = await AdminMijozAsync();

        using var javob = await client.PostAsJsonAsync(
            new Uri("/api/users", UriKind.Relative),
            new
            {
                login = NoyobTelefon(),
                lastName = "Familiya",
                firstName = "Ism",
                password = "qisqa",
                roleCodes = HrManagerRoleOnly,
            },
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await MatnAsync(javob)).ShouldContain("foydalanuvchi.parol_qisqa");
    }

    // ────────────────────────────────────────────────────────────
    //  O'zini o'zi bloklashdan va oxirgi administratordan himoya
    // ────────────────────────────────────────────────────────────

    /// <remarks>
    /// Bu test ATAYLAB o'zining ALOHIDA bazasida ishlaydi (umumiy
    /// <see cref="_factory"/> emas): boshqa testlar shu sinfda qo'shimcha
    /// administrator yaratishi mumkin, "tizimda bor-yo'g'i bitta admin bor"
    /// holatini esa faqat yangi, hech kim tegmagan bazada kafolatlash mumkin.
    /// </remarks>
    [Fact]
    public async Task Yagona_administrator_ozini_faolsizlantira_olmaydi()
    {
        await using var yakkaFactory = new BonviApiFactory();
        await yakkaFactory.InitializeAsync();

        using var client = yakkaFactory.CreateClient();

        using var kirish = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new { login = BonviApiFactory.TestAdminLogin, password = BonviApiFactory.TestAdminPassword },
            TestContext.Current.CancellationToken);

        kirish.EnsureSuccessStatusCode();
        var auth = await kirish.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        using var men = await client.GetAsync(new Uri("/api/auth/me", UriKind.Relative), TestContext.Current.CancellationToken);
        var joriy = await men.Content.ReadFromJsonAsync<UserResponse>(TestContext.Current.CancellationToken);
        joriy.ShouldNotBeNull();

        using var javob = await client.PostAsync(
            new Uri($"/api/users/{joriy.Id}/deactivate", UriKind.Relative),
            content: null,
            TestContext.Current.CancellationToken);

        // Bir vaqtda ham "o'zini o'zi", ham "oxirgi admin" holati — tizimda
        // boshqa faol admin yo'qligi birinchi tekshiriladi
        javob.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await MatnAsync(javob)).ShouldContain("foydalanuvchi.oxirgi_administrator");
    }

    [Fact]
    public async Task Administrator_boshqa_admin_bolsa_ham_ozini_faolsizlantira_olmaydi()
    {
        using var client = await AdminMijozAsync();

        // Ikkinchi administrator yaratamiz — endi tizimda ikkita admin bor
        var telefon = NoyobTelefon();
        using var yaratish = await client.PostAsJsonAsync(
            new Uri("/api/users", UriKind.Relative),
            new
            {
                login = telefon,
                lastName = "Иккинчи",
                firstName = "Админ",
                password = "Ikkinchi_Admin1",
                roleCodes = AdminRoleOnly,
            },
            TestContext.Current.CancellationToken);

        yaratish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yaratish));

        using var men = await client.GetAsync(new Uri("/api/auth/me", UriKind.Relative), TestContext.Current.CancellationToken);
        var joriy = await men.Content.ReadFromJsonAsync<UserResponse>(TestContext.Current.CancellationToken);
        joriy.ShouldNotBeNull();

        using var javob = await client.PostAsync(
            new Uri($"/api/users/{joriy.Id}/deactivate", UriKind.Relative),
            content: null,
            TestContext.Current.CancellationToken);

        // Endi boshqa faol admin bor — shu sababli aynan "o'zini o'zi
        // bloklash" qoidasi ishga tushadi
        javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await MatnAsync(javob)).ShouldContain("foydalanuvchi.ozini_blokash");
    }

    // ────────────────────────────────────────────────────────────
    //  Audit
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Foydalanuvchi_yaratish_audit_jurnaliga_yoziladi()
    {
        using var client = await AdminMijozAsync();
        var telefon = NoyobTelefon();

        using var yaratish = await client.PostAsJsonAsync(
            new Uri("/api/users", UriKind.Relative),
            new
            {
                login = telefon,
                lastName = "Familiya",
                firstName = "Ism",
                password = "Parol_12345",
                roleCodes = HrManagerRoleOnly,
            },
            TestContext.Current.CancellationToken);

        yaratish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yaratish));
        var yaratilgan = await yaratish.Content.ReadFromJsonAsync<UserSummaryResponse>(
            TestContext.Current.CancellationToken);
        yaratilgan.ShouldNotBeNull();

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var yozuv = await kontekst.AuditLogs
            .Where(log => log.EntityName == "User" && log.EntityId == yaratilgan.Id.ToString())
            .OrderByDescending(log => log.OccurredAt)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        yozuv.ShouldNotBeNull();
        yozuv.Action.ShouldBe(AuditAction.Create);
        yozuv.UserLogin.ShouldBe("+998" + BonviApiFactory.TestAdminLogin);
        yozuv.NewValues.ShouldNotBeNull();
        // Parol audit jurnaliga hech qachon yozilmaydi
        yozuv.NewValues.ShouldNotContain("Parol_12345");
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    private static string NoyobTelefon()
    {
        var raqam = 1000000 + Interlocked.Increment(ref _telefonSonagichi);
        return "90" + raqam.ToString(CultureInfo.InvariantCulture);
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

    /// <summary>
    /// Haqiqiy HR menejer hisobi bilan kirgan mijoz — administrator uchun
    /// mo'ljallangan endpoint'larga ruxsati yo'qligini ko'rsatish uchun.
    /// </summary>
    private async Task<HttpClient> HrManagerMijozAsync(HttpClient adminClient)
    {
        var telefon = NoyobTelefon();
        const string parol = "Hr_Sinov_Paroli1";

        using var yaratish = await adminClient.PostAsJsonAsync(
            new Uri("/api/users", UriKind.Relative),
            new
            {
                login = telefon,
                lastName = "HR",
                firstName = "Sinov",
                password = parol,
                roleCodes = HrManagerRoleOnly,
            },
            TestContext.Current.CancellationToken);

        yaratish.EnsureSuccessStatusCode();

        var client = _factory.CreateClient();

        using var kirish = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new { login = telefon, password = parol },
            TestContext.Current.CancellationToken);

        kirish.EnsureSuccessStatusCode();

        var auth = await kirish.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return client;
    }

    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
