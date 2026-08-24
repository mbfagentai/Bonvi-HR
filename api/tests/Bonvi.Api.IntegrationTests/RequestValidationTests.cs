using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Noto'g'ri so'rov <b>400</b> qaytarishi — <b>500</b> emas.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega bu test bor.</b> Barcha istisnolar bitta joyda ushlanadi
/// (<c>GlobalExceptionHandler</c>) va u dastlab hamma narsani
/// <c>500 Ichki xato</c> ga aylantirardi. Natijada mijozning imlo xatosi
/// («<c>?type=</c> yozilmagan») server nosozligi bo'lib ko'rinardi.
/// </para>
/// <para>
/// Ikkala oqibat ham qimmat: mijoz nimani noto'g'ri yozganini bilmaydi, va
/// haqiqiy nosozliklarni kuzatishda mijoz xatolari shovqin bo'lib yotadi.
/// Bu qoida majburiy parametrli <b>har bir</b> endpoint'ga tegishli, shu
/// sababli u testda qadalgan.
/// </para>
/// </remarks>
public sealed class RequestValidationTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public RequestValidationTests(BonviApiFactory factory) => _factory = factory;

    /// <summary>Majburiy parametr umuman berilmagan.</summary>
    [Fact]
    public async Task Majburiy_parametr_berilmasa_400_qaytadi()
    {
        using var client = _factory.CreateClient();
        await AvtorizatsiyaAsync(client);

        using var javob = await client.GetAsync(
            new Uri("/api/employees/flagged", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>Parametr berilgan, lekin qiymati enum'da yo'q.</summary>
    [Fact]
    public async Task Nomalum_qiymatli_parametr_400_qaytadi()
    {
        using var client = _factory.CreateClient();
        await AvtorizatsiyaAsync(client);

        using var javob = await client.GetAsync(
            new Uri("/api/employees/flagged?type=Boshqa", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>Xato javobi ham loyihaning yagona ko'rinishida bo'ladi.</summary>
    /// <remarks>
    /// <c>traceId</c> muhim: HR «xato chiqdi» deganda logdagi aynan shu
    /// so'rovni topish uchun yagona ip shu.
    /// </remarks>
    [Fact]
    public async Task Xato_javobi_ProblemDetails_korinishida_boladi()
    {
        using var client = _factory.CreateClient();
        await AvtorizatsiyaAsync(client);

        using var javob = await client.GetAsync(
            new Uri("/api/employees/flagged", UriKind.Relative),
            TestContext.Current.CancellationToken);

        var muammo = await javob.Content.ReadFromJsonAsync<ProblemBody>(
            TestContext.Current.CancellationToken);

        muammo.ShouldNotBeNull();
        muammo.Status.ShouldBe(400);
        muammo.Title.ShouldNotBeNullOrWhiteSpace();
        muammo.TraceId.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>To'g'ri so'rov avvalgidek ishlaydi — tuzatish hech narsani buzmadi.</summary>
    [Fact]
    public async Task Togri_parametr_bilan_sorov_ishlayveradi()
    {
        using var client = _factory.CreateClient();
        await AvtorizatsiyaAsync(client);

        using var javob = await client.GetAsync(
            new Uri("/api/employees/flagged?type=Blacklist", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task AvtorizatsiyaAsync(HttpClient client)
    {
        using var javob = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new { login = BonviApiFactory.TestAdminLogin, password = BonviApiFactory.TestAdminPassword },
            TestContext.Current.CancellationToken);

        javob.EnsureSuccessStatusCode();

        var auth = await javob.Content.ReadFromJsonAsync<AuthResponse>(
            TestContext.Current.CancellationToken);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
    }

    private sealed record ProblemBody(string? Title, int? Status, string? Detail, string? TraceId);
}
