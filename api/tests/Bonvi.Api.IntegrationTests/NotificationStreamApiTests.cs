using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Bildirishnomalarning JONLI oqimi (SSE).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nima uchun bu testlar bor.</b> Foydalanuvchi ko'rsatgan nosozlik:
/// «HR ishlab o'tirganda bot orqali ariza tushsa, u buni bilmaydi —
/// faqat sahifani yangilagandan keyin ko'radi». Tuzatishning MOHIYATI
/// bitta jumlada: bazadagi o'zgarish ochiq turgan ulanishga o'zi yetib
/// borishi kerak. Aynan shu yerda tekshiriladi.
/// </para>
/// <para>
/// <b>Nega bu testlar QIMMATLI.</b> Oqim uch bo'lakdan yig'ilgan va
/// ularning har biri boshqa qatlamda: <c>SaveChanges</c> ushlagichi
/// (Infrastructure), signal (singleton) va endpoint (Api). Har biri
/// alohida to'g'ri bo'lib, zanjir baribir uzilishi mumkin — masalan
/// ushlagich ro'yxatga olinmasa yoki signal <c>Scoped</c> qilib
/// qo'yilsa. Bunday xato ekranda «jimlik» bo'lib ko'rinadi va uni
/// qo'lda topish uchun ikkita brauzer ochish kerak bo'lardi.
/// </para>
/// <para>
/// <b>Muddat har joyda qat'iy.</b> Oqim testining eng yomon nosozligi —
/// hech qachon tugamasligi. Shu sababli har bir kutish o'z muddati
/// bilan o'ralgan: xato bo'lsa test YIQILADI, osilib qolmaydi.
/// </para>
/// </remarks>
public sealed class NotificationStreamApiTests : IClassFixture<BonviApiFactory>
{
    private static readonly Uri StreamUri = new("/api/notifications/stream", UriKind.Relative);
    private static readonly Uri LoginUri = new("/api/auth/login", UriKind.Relative);

    /// <summary>Bitta hodisani kutishning eng uzun muddati.</summary>
    /// <remarks>
    /// Serverda o'zgarishdan keyin 250 ms «yig'ish» pauzasi bor
    /// (<c>NotificationEndpoints.Debounce</c>), qolgani esa hisob va
    /// tarmoq. 15 soniya — sekin mashinada ham yetadi, lekin yiqilgan
    /// test yig'ilishni to'xtatib qo'yadigan darajada uzoq emas.
    /// </remarks>
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(15);

    private readonly BonviApiFactory _factory;

    public NotificationStreamApiTests(BonviApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Tokensiz_oqim_401_qaytaradi()
    {
        using var client = _factory.CreateClient();

        using var javob = await client.GetAsync(StreamUri, TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Ulanish ochilishi bilan BOSHLANG'ICH holat keladi.
    /// </summary>
    /// <remarks>
    /// Birinchi xabar shart: usiz brauzer ulanib, birinchi o'zgarishgacha
    /// (soatlab bo'lishi mumkin) eski sanoq bilan turaverardi.
    /// </remarks>
    [Fact]
    public async Task Ulanishda_boshlangich_holat_keladi()
    {
        using var cts = new CancellationTokenSource(EventTimeout);
        using var client = await AdminMijozAsync();

        using var response = await OpenStreamAsync(client, cts.Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/event-stream");

        await using var body = await response.Content.ReadAsStreamAsync(cts.Token);
        using var reader = new StreamReader(body);

        var first = await ReadEventAsync(reader, cts.Token);

        first.ShouldNotBeNull();
        first.ShouldContain("\"unreadCount\"");
    }

    /// <summary>
    /// ⭐ Bazadagi o'zgarish OCHIQ ulanishga o'zi yetib boradi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Butun tuzatishning mohiyati shu testda. Yozuv <c>AppDbContext</c>
    /// orqali, ya'ni HAQIQIY yo'ldan ketadi (<c>SaveChanges</c>) —
    /// signalni test qo'lda uyg'otmaydi, aks holda u ushlagichning
    /// ro'yxatga olinganini umuman tekshirmasdi.
    /// </para>
    /// <para>
    /// Yangi xodim ismiga NOYOB belgi qo'shiladi: baza test sinflari
    /// orasida bo'lishiladi va boshqa testning yozuvini «o'zimizniki»
    /// deb o'qib yuborish mumkin emas.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Ozgarish_ochiq_oqimga_yetib_boradi()
    {
        using var cts = new CancellationTokenSource(EventTimeout * 2);
        using var client = await AdminMijozAsync();

        using var response = await OpenStreamAsync(client, cts.Token);
        await using var body = await response.Content.ReadAsStreamAsync(cts.Token);
        using var reader = new StreamReader(body);

        // Boshlang'ich holat — undan keyingina o'zgarish yasaladi.
        (await ReadEventAsync(reader, cts.Token)).ShouldNotBeNull();

        var belgi = "Oqim" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..6];

        await SinovYaratAsync(belgi, cts.Token);

        /*
          Ikkinchi hodisa. Sabr bilan o'qiladi: oqimda oradan boshqa
          testning o'zgarishi ham o'tishi mumkin (baza umumiy), shu
          sababli BIZNING belgimiz chiqquncha o'qiladi.
        */
        string? payload = null;

        while (!cts.IsCancellationRequested)
        {
            var next = await ReadEventAsync(reader, cts.Token);
            if (next is null) break;

            if (next.Contains(belgi, StringComparison.Ordinal))
            {
                payload = next;
                break;
            }
        }

        payload.ShouldNotBeNull("o'zgarish ochiq turgan oqimga yetib bormadi");
        payload.ShouldContain("ProbationEndingSoon");
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Oqimni ochadi — javob TANASINI kutmasdan.
    /// </summary>
    /// <remarks>
    /// <c>ResponseHeadersRead</c> MAJBURIY: standart holatda
    /// <c>HttpClient</c> butun javobni o'qib bo'lguncha kutadi, oqim esa
    /// hech qachon tugamaydi — test o'sha yerda osilib qolardi.
    /// </remarks>
    private static async Task<HttpResponseMessage> OpenStreamAsync(
        HttpClient client,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, StreamUri);

        return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    /// <summary>
    /// Keyingi <c>notifications</c> hodisasining tanasini o'qiydi.
    /// </summary>
    /// <remarks>
    /// Izoh qatorlari (<c>: ping</c>) o'tkazib yuboriladi — ular
    /// «tirikman» belgisi va ma'lumot tashimaydi.
    /// </remarks>
    private static async Task<string?> ReadEventAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var isNotification = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);

            if (line is null) return null;

            if (line.StartsWith("event:", StringComparison.Ordinal))
            {
                isNotification = line["event:".Length..].Trim() == "notifications";
                continue;
            }

            if (isNotification && line.StartsWith("data:", StringComparison.Ordinal))
            {
                return line["data:".Length..].Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// Bugundan uch kun keyin tugaydigan sinov muddatini yozadi.
    /// </summary>
    /// <remarks>
    /// Yozuv <c>AppDbContext</c> orqali — ya'ni <c>SaveChanges</c> dan
    /// o'tadi va signal HAQIQIY yo'l bilan uyg'onadi. Uch kun tanlandi:
    /// sukut chegara yetti kun, ya'ni bildirishnoma albatta chiqadi
    /// (<c>NotificationRules.ProbationWarningDays</c>).
    /// </remarks>
    private async Task SinovYaratAsync(string belgi, CancellationToken cancellationToken)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var bugun = DateOnly.FromDateTime(DateTime.UtcNow);

        var firma = LegalEntity.Create($"{belgi} Оқим МЧЖ").Value;
        var xodim = Employee.Create(FullName.Create("Оқимдаги", belgi, null).Value).Value;
        var munosabat = Employment.Hire(xodim.Id, firma.Id, bugun.AddDays(-60)).Value;
        var sinov = Probation.Import(munosabat.Id, bugun.AddDays(-60), bugun.AddDays(3)).Value;

        kontekst.Add(firma);
        kontekst.Add(xodim);
        kontekst.Add(munosabat);
        kontekst.Add(sinov);

        await kontekst.SaveChangesAsync(cancellationToken);
    }

    private async Task<HttpClient> AdminMijozAsync()
    {
        var client = _factory.CreateClient();

        using var javob = await client.PostAsJsonAsync(
            LoginUri,
            new { login = BonviApiFactory.TestAdminLogin, password = BonviApiFactory.TestAdminPassword },
            TestContext.Current.CancellationToken);

        javob.EnsureSuccessStatusCode();

        var auth = await javob.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return client;
    }
}
