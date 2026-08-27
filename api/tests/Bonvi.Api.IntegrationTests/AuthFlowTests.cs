using System.Net;
using System.Net.Http.Json;
using Bonvi.Domain.Identity;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Kirish javobi (test uchun).</summary>
public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string RefreshToken,
    UserResponse User);

/// <summary>Foydalanuvchi ma'lumoti (test uchun).</summary>
public sealed record UserResponse(
    Guid Id,
    string Login,
    string FullName,
    string ShortName,
    Guid? EmployeeId,
    string[] Roles,
    string[] Permissions);

/// <summary>
/// Autentifikatsiya oqimining uchi-uchiga testlari.
/// </summary>
/// <remarks>
/// <para>
/// Haqiqiy PostgreSQL ustida ishlaydi: migratsiya qo'llanadi, rollar
/// urug'lantiriladi, birinchi administrator yaratiladi — hammasi
/// <c>DatabaseInitializer</c> orqali, xuddi haqiqiy ishga tushishdagidek.
/// </para>
/// <para>
/// Nega mock emas: bu yerda tekshirilayotgan narsalarning ko'pi
/// (unikallik cheklovi, token xeshini topish, tranzaksiya) faqat
/// haqiqiy bazada ko'rinadi.
/// </para>
/// </remarks>
public sealed class AuthFlowTests : IClassFixture<BonviApiFactory>
{
    private const string AdminLogin = BonviApiFactory.TestAdminLogin;
    private const string AdminPassword = BonviApiFactory.TestAdminPassword;

    private readonly BonviApiFactory _factory;

    public AuthFlowTests(BonviApiFactory factory) => _factory = factory;

    private HttpClient Client => _factory.CreateClient();

    private static JsonContent LoginBody(string login, string password)
        => JsonContent.Create(new { login, password });

    // ────────── Muvaffaqiyatli oqim ──────────

    [Fact]
    public async Task Togri_malumot_bilan_kirish_token_qaytaradi()
    {
        using var client = Client;

        using var javob = await client.PostAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            LoginBody(AdminLogin, AdminPassword),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK);

        var natija = await javob.Content.ReadFromJsonAsync<AuthResponse>(
            TestContext.Current.CancellationToken);

        natija.ShouldNotBeNull();
        natija.AccessToken.ShouldNotBeNullOrWhiteSpace();
        natija.RefreshToken.ShouldNotBeNullOrWhiteSpace();
        natija.User.Login.ShouldBe("+998905088888");
        // Boshlang'ich hisob SUPER ADMINISTRATOR: eski ikki rolli modeldagi
        // «admin» endi shu kod bilan yuradi (`IdentitySeeder` ko'chiradi).
        natija.User.Roles.ShouldContain(SystemRoles.SuperAdmin);

        // Super administrator barcha ruxsatlarga ega bo'lishi kerak
        natija.User.Permissions.ShouldContain("employees.view");
        natija.User.Permissions.ShouldContain("salary.view");
        natija.User.Permissions.ShouldContain("users.manage");
    }

    [Fact]
    public async Task Token_bilan_me_endpointi_ochiladi()
    {
        using var client = Client;
        var auth = await KirishAsync(client);

        using var sorov = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        sorov.Headers.Authorization = new("Bearer", auth.AccessToken);

        using var javob = await client.SendAsync(sorov, TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK);

        var user = await javob.Content.ReadFromJsonAsync<UserResponse>(
            TestContext.Current.CancellationToken);

        user.ShouldNotBeNull();
        user.Login.ShouldBe("+998905088888");
    }

    [Fact]
    public async Task Sessiya_yangilanadi_va_eski_token_bekor_qilinadi()
    {
        using var client = Client;
        var auth = await KirishAsync(client);

        using var birinchi = await client.PostAsJsonAsync(
            new Uri("/api/auth/refresh", UriKind.Relative),
            new { refreshToken = auth.RefreshToken },
            TestContext.Current.CancellationToken);

        birinchi.StatusCode.ShouldBe(HttpStatusCode.OK);

        var yangi = await birinchi.Content.ReadFromJsonAsync<AuthResponse>(
            TestContext.Current.CancellationToken);

        yangi.ShouldNotBeNull();
        yangi.RefreshToken.ShouldNotBe(auth.RefreshToken, "har bir yangilashda yangi token beriladi");
    }

    /// <summary>
    /// Eng muhim xavfsizlik testi: o'g'irlangan tokenni aniqlash.
    /// </summary>
    [Fact]
    public async Task Bekor_qilingan_token_qayta_ishlatilsa_barcha_sessiyalar_yopiladi()
    {
        using var client = Client;
        var auth = await KirishAsync(client);

        // 1. Tokenni yangilaymiz — eskisi bekor bo'ladi
        using var yangilash = await client.PostAsJsonAsync(
            new Uri("/api/auth/refresh", UriKind.Relative),
            new { refreshToken = auth.RefreshToken },
            TestContext.Current.CancellationToken);

        var yangi = await yangilash.Content.ReadFromJsonAsync<AuthResponse>(
            TestContext.Current.CancellationToken);
        yangi.ShouldNotBeNull();

        // 2. ESKI tokenni qayta ishlatishga urinamiz — bu o'g'irlik belgisi
        using var qaytaUrinish = await client.PostAsJsonAsync(
            new Uri("/api/auth/refresh", UriKind.Relative),
            new { refreshToken = auth.RefreshToken },
            TestContext.Current.CancellationToken);

        qaytaUrinish.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // 3. Endi YANGI token ham ishlamasligi kerak — hammasi bekor qilingan
        using var yangiBilan = await client.PostAsJsonAsync(
            new Uri("/api/auth/refresh", UriKind.Relative),
            new { refreshToken = yangi.RefreshToken },
            TestContext.Current.CancellationToken);

        yangiBilan.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ────────── Xato holatlar ──────────

    [Fact]
    public async Task Notogri_parol_400_qaytaradi()
    {
        using var client = Client;

        using var javob = await client.PostAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            LoginBody(AdminLogin, "butunlay_notogri_parol"),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var matn = await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        matn.ShouldContain("auth.notogri_malumot");
    }

    [Fact]
    public async Task Mavjud_bolmagan_login_AYNAN_SHU_xatoni_beradi()
    {
        // Ma'lumot oshkor qilmaslik: javob noto'g'ri paroldagi bilan bir xil
        using var client = Client;

        using var javob = await client.PostAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            LoginBody("901112233", "parol"),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var matn = await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        matn.ShouldContain("auth.notogri_malumot");
    }

    [Fact]
    public async Task Tokensiz_me_endpointi_401_qaytaradi()
    {
        using var client = Client;

        using var javob = await client.GetAsync(
            new Uri("/api/auth/me", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Soxta_token_bilan_me_endpointi_401_qaytaradi()
    {
        using var client = Client;

        using var sorov = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        sorov.Headers.Authorization = new("Bearer", "soxta.token.qiymati");

        using var javob = await client.SendAsync(sorov, TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Chiqishdan_keyin_token_ishlamaydi()
    {
        using var client = Client;
        var auth = await KirishAsync(client);

        using var chiqish = await client.PostAsJsonAsync(
            new Uri("/api/auth/logout", UriKind.Relative),
            new { refreshToken = auth.RefreshToken, allSessions = false },
            TestContext.Current.CancellationToken);

        chiqish.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var yangilash = await client.PostAsJsonAsync(
            new Uri("/api/auth/refresh", UriKind.Relative),
            new { refreshToken = auth.RefreshToken },
            TestContext.Current.CancellationToken);

        yangilash.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Tokensiz_chiqish_ham_muvaffaqiyat_qaytaradi()
    {
        // Chiqish hech qachon xato bermasligi kerak — foydalanuvchi baribir chiqdi
        using var client = Client;

        using var javob = await client.PostAsJsonAsync(
            new Uri("/api/auth/logout", UriKind.Relative),
            new { refreshToken = (string?)null, allSessions = false },
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private static async Task<AuthResponse> KirishAsync(HttpClient client)
    {
        using var javob = await client.PostAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            LoginBody(AdminLogin, AdminPassword),
            TestContext.Current.CancellationToken);

        javob.EnsureSuccessStatusCode();

        var natija = await javob.Content.ReadFromJsonAsync<AuthResponse>(
            TestContext.Current.CancellationToken);

        return natija!;
    }
}
