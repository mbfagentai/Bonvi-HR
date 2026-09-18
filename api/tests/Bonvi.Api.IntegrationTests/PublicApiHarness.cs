using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bonvi.Api.Endpoints;
using Bonvi.Api.Extensions;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Yaratilgan API kaliti — testlar uchun.</summary>
public sealed record ApiKeyCreatedResponse(Guid Id, string Name, string Prefix, string Key);

/// <summary>Ro'yxatdagi API kaliti — testlar uchun.</summary>
/// <remarks>
/// ⚠️ Bu yozuvda <c>Key</c> maydoni ATAYLAB yo'q va bo'lmasligi ham kerak:
/// ro'yxat javobida kalit hech qachon qaytmaydi. Uni bu yerga qo'shish
/// «kalit qaytmaydi» testini jimgina ma'nosiz qilib qo'yardi.
/// </remarks>
public sealed record ApiKeyListResponse(
    Guid Id,
    string Name,
    string Prefix,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    string? LastUsedIp,
    DateTimeOffset? RevokedAt,
    bool IsActive);

/// <summary>
/// Tashqi API testlarining umumiy yordamchilari.
/// </summary>
/// <remarks>
/// Bitta joyda, chunki uchta test sinfi ham AYNAN bir xil ikki ishni
/// qiladi: administrator bo'lib kirish va kalit yasab, u bilan mijoz
/// ochish. Har sinfda takrorlansa, kalit yaratish yo'li o'zgarganda
/// uchtasidan biri eskirib qolardi.
/// </remarks>
internal static class PublicApiHarness
{
    /// <summary>
    /// Ommaviy yuzaning yo'l prefiksi — testlar shu yerdan yuradi.
    /// </summary>
    /// <remarks>
    /// Qiymat KODDAN olinadi, qo'lda yozilmaydi: prefiks o'zgarsa testlar
    /// eski manzilga urinib, «marshrut yo'q» degan 404 ni «himoya ishladi»
    /// deb o'qib yuborishi mumkin edi.
    /// </remarks>
    public const string Prefix = PublicApiEndpoints.Prefix;

    /// <summary>JWT bilan kirgan administrator mijozi.</summary>
    public static async Task<HttpClient> AdminMijozAsync(BonviApiFactory factory)
    {
        var client = factory.CreateClient();

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

    /// <summary>Yangi kalit yaratadi — ICHKI endpoint orqali, haqiqiy yo'ldan.</summary>
    public static async Task<ApiKeyCreatedResponse> KalitYaratAsync(HttpClient admin, string nom)
    {
        using var javob = await admin.PostAsJsonAsync(
            new Uri("/api/api-keys", UriKind.Relative),
            new { name = nom },
            TestContext.Current.CancellationToken);

        javob.EnsureSuccessStatusCode();

        var kalit = await javob.Content.ReadFromJsonAsync<ApiKeyCreatedResponse>(
            TestContext.Current.CancellationToken);

        kalit.ShouldNotBeNull();
        return kalit;
    }

    /// <summary>Nomni noyob qiladi — testlar parallel ishlaydi.</summary>
    public static string NoyobNom(string asos)
        => $"{asos}-{Guid.NewGuid().ToString("N")[..8]}";

    /// <summary>Kalit sarlavhasi bilan mijoz.</summary>
    public static HttpClient KalitMijozi(BonviApiFactory factory, string kalit)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyAuthenticationHandler.HeaderName, kalit);

        return client;
    }

    /// <summary>Javob tanasi — tekshiruv yiqilganda sababi ko'rinishi uchun.</summary>
    public static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
