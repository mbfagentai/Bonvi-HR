using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bonvi.Application.Recruiting.Forms;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Bonvi.Infrastructure.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Onlayn anketa — bot tekshiruvi (Cloudflare Turnstile, SPEC-planshet-anketa §2.3.2): seans ochiq
/// kalitni beradi, tartib (tuzoq → takroriy yuborish → token → qolgani), yopiq-sukut, yoqilmagan
/// holatda token so'ralmaydi.
/// </summary>
/// <remarks>
/// Ko'p testda — soxta tekshiruvchi (<see cref="SoxtaBotTekshiruvi"/>, port almashtiriladi): handler
/// qoidalari Cloudflare'siz. Bittasida — HAQIQIY <see cref="TurnstileBotCheck"/>, tarmoq o'rnida
/// <see cref="TurnstileStub"/>: ro'yxatga olish, IP uzatish va yopiq-sukut uchi-uchiga.
/// </remarks>
public sealed partial class KioskQuestionnaireApiTests
{
    [Fact]
    public async Task Bot_tekshiruvi_yoqilmagan_bolsa_seansda_kalit_null_va_token_kerak_emas()
    {
        using var mehmon = Mehmon();

        using (var seans = await mehmon.GetAsync(new Uri(SessionPath, UriKind.Relative), Ct))
        {
            seans.StatusCode.ShouldBe(HttpStatusCode.OK);

            using var hujjat = JsonDocument.Parse(await MatnAsync(seans));
            hujjat.RootElement.GetProperty("turnstileSiteKey").ValueKind.ShouldBe(JsonValueKind.Null, "kalitlar berilmagan");
        }

        using var javob = await YuborAsync(mehmon, QuestionnaireSamples.Minimal(Bugun, Telefon()), Guid.NewGuid());
        javob.StatusCode.ShouldBe(HttpStatusCode.Created, await MatnAsync(javob));
    }

    [Fact]
    public async Task Bot_tekshiruvi_yoqiq_seans_kalitni_beradi_yaroqli_token_201_takroriy_tokensiz_200()
    {
        var tekshiruv = new SoxtaBotTekshiruvi();
        await using var fabrika = BotTekshiruvli(tekshiruv);

        const string ip = "203.0.113.61";
        using var mehmon = Mehmon(fabrika, ip);

        using (var seans = await mehmon.GetAsync(new Uri(SessionPath, UriKind.Relative), Ct))
        {
            seans.StatusCode.ShouldBe(HttpStatusCode.OK);

            using var hujjat = JsonDocument.Parse(await MatnAsync(seans));
            hujjat.RootElement.GetProperty("turnstileSiteKey").GetString().ShouldBe(SoxtaBotTekshiruvi.Kalit);
        }

        var yuborish = Guid.NewGuid();
        var javoblar = QuestionnaireSamples.Minimal(Bugun, Telefon());

        KioskSubmittedResponse? birinchi;
        using (var javob = await YuborAsync(mehmon, javoblar, yuborish, turnstileToken: SoxtaBotTekshiruvi.Yaroqli))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Created, await MatnAsync(javob));
            birinchi = await javob.Content.ReadFromJsonAsync<KioskSubmittedResponse>(Ct);
        }

        var chaqiruv = tekshiruv.Chaqiruvlar.ShouldHaveSingleItem();
        chaqiruv.Token.ShouldBe(SoxtaBotTekshiruvi.Yaroqli);
        chaqiruv.ClientIp.ShouldBe(ip, "Caddy bergan X-Bonvi-Client-Ip — Cloudflare'ga shu");
        chaqiruv.IdempotencyKey.ShouldBe(yuborish);

        // Javob yo'qolgan, forma qayta yuboradi — tokeni endi yo'q (yoki eskirgan): 200, o'sha raqam
        using var takror = await YuborAsync(mehmon, javoblar, yuborish);
        takror.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(takror));
        (await takror.Content.ReadFromJsonAsync<KioskSubmittedResponse>(Ct)).ShouldBe(birinchi);

        tekshiruv.Chaqiruvlar.Count.ShouldBe(1, "takroriy yuborishda token tekshirilmaydi");
    }

    [Fact]
    public async Task Bot_tekshiruvi_yoqiq_token_yoq_yoki_yaroqsiz_400_va_hech_narsa_yozilmaydi()
    {
        var tekshiruv = new SoxtaBotTekshiruvi();
        await using var fabrika = BotTekshiruvli(tekshiruv);
        using var mehmon = Mehmon(fabrika, NoyobIp());

        var telefon = Telefon();
        var javoblar = QuestionnaireSamples.Minimal(Bugun, telefon);
        var yuborishlar = new List<Guid>();

        foreach (var token in new[] { null, "", "soxta-token" })
        {
            var yuborish = Guid.NewGuid();
            yuborishlar.Add(yuborish);

            using var javob = await YuborAsync(mehmon, javoblar, yuborish, turnstileToken: token);
            javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest, $"token: «{token}»");

            using var xato = JsonDocument.Parse(await MatnAsync(javob));
            xato.RootElement.GetProperty("title").GetString().ShouldBe("anketa.bot_tekshiruvi");
            xato.RootElement.GetProperty("detail").GetString().ShouldBe("turnstileToken");
            xato.RootElement.GetProperty("field").GetString().ShouldBe("turnstileToken");
        }

        // Tuzoq maydon — tokendan OLDIN: o'z kodi, Cloudflare'ga so'rov ham ketmaydi
        var oldingi = tekshiruv.Chaqiruvlar.Count;

        using (var bot = await YuborAsync(mehmon, javoblar, Guid.NewGuid(), website: "https://spam.example"))
        {
            bot.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

            using var xato = JsonDocument.Parse(await MatnAsync(bot));
            xato.RootElement.GetProperty("title").GetString().ShouldBe("anketa.belgi");
        }

        tekshiruv.Chaqiruvlar.Count.ShouldBe(oldingi, "tuzoq to'lgan so'rov tokenga yetib bormaydi");

        await using var qamrov = fabrika.Services.CreateAsyncScope();
        var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        (await db.CandidateForms.CountAsync(form => yuborishlar.Contains(form.ClientSubmissionId), Ct)).ShouldBe(0);
        (await db.Candidates.CountAsync(candidate => candidate.Phone == PhoneNumber.FromTrusted(telefon), Ct)).ShouldBe(0);
    }

    /// <summary>
    /// HAQIQIY tekshiruvchi: Cloudflare'ga ulanib bo'lmadi yoki javob kelmadi — 400, hech narsa
    /// yozilmaydi (yopiq-sukut); haqiqiy token mijoz IP'si bilan — 201.
    /// </summary>
    [Fact]
    public async Task Cloudflare_ulanmasa_yoki_vaqt_tugasa_400_yopiq_sukut_haqiqiy_tekshiruvchi_bilan()
    {
        const string ip = "203.0.113.62";

        await using var fabrika = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Turnstile:SiteKey", "0x4AAAAAAAsinovOchiq");
            builder.UseSetting("Turnstile:SecretKey", "0x4AAAAAAAsinovMaxfiy");

            builder.ConfigureTestServices(services => services
                .AddHttpClient(TurnstileBotCheck.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new TurnstileStub(Cloudflare))

                // Haqiqiy muddat 5 s — test uni kutmasin (2 s: yuklama ostida ham «yaroqli» ulguradi)
                .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(2)));
        });

        using var mehmon = Mehmon(fabrika, ip);

        var telefon = Telefon();
        var javoblar = QuestionnaireSamples.Minimal(Bugun, telefon);
        var rad = new List<Guid>();

        foreach (var token in new[] { "uzilish", "kutish" })
        {
            var yuborish = Guid.NewGuid();
            rad.Add(yuborish);

            using var javob = await YuborAsync(mehmon, javoblar, yuborish, turnstileToken: token);
            javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest, token);
            (await MatnAsync(javob)).ShouldContain("anketa.bot_tekshiruvi");
        }

        await using (var qamrov = fabrika.Services.CreateAsyncScope())
        {
            var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

            (await db.CandidateForms.CountAsync(form => rad.Contains(form.ClientSubmissionId), Ct)).ShouldBe(0);
            (await db.Candidates.CountAsync(candidate => candidate.Phone == PhoneNumber.FromTrusted(telefon), Ct)).ShouldBe(0);
        }

        // Cloudflare tasdiqladi (stub faqat mijozning haqiqiy IP'si kelganda «ha» deydi)
        using var togri = await YuborAsync(mehmon, javoblar, Guid.NewGuid(), turnstileToken: "yaroqli");
        togri.StatusCode.ShouldBe(HttpStatusCode.Created, await MatnAsync(togri));

        static async Task<HttpResponseMessage> Cloudflare(TurnstileStubRequest sorov, CancellationToken cancellationToken)
        {
            switch (sorov.Forma.GetValueOrDefault("response"))
            {
                case "uzilish":
                    throw new HttpRequestException("Cloudflare'ga ulanib bo'lmadi");

                case "kutish":
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                    break;

                case "yaroqli" when sorov.Forma.GetValueOrDefault("remoteip") == ip
                                    && sorov.Forma.GetValueOrDefault("secret") == "0x4AAAAAAAsinovMaxfiy":
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("""{"success":true}""", Encoding.UTF8, "application/json"),
                    };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"success":false,"error-codes":["invalid-input-response"]}""", Encoding.UTF8, "application/json"),
            };
        }
    }

    [Fact]
    public void Bot_tekshiruvi_mijozining_muddati_5_soniya()
    {
        using var mijoz = _factory.Services.GetRequiredService<IHttpClientFactory>().CreateClient(TurnstileBotCheck.HttpClientName);

        mijoz.Timeout.ShouldBe(TimeSpan.FromSeconds(5), "SPEC §2.3.2: 5 s, keyin yopiq-sukut");
    }

    /// <summary>Soxta tekshiruvchi bilan alohida xost — tekshiruv yoqiq.</summary>
    private WebApplicationFactory<Program> BotTekshiruvli(SoxtaBotTekshiruvi tekshiruv)
        => _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IQuestionnaireBotCheck>(tekshiruv)));

    /// <summary>Cloudflare o'rnida: <see cref="Yaroqli"/> — o'tadi, qolgani — yo'q. Chaqiruvlarni yozib oladi.</summary>
    private sealed class SoxtaBotTekshiruvi : IQuestionnaireBotCheck
    {
        /// <summary>Cloudflare'ning ochiq SINOV kaliti (hujjatdagi «doim o'tadi» kaliti) — haqiqiy emas.</summary>
        public const string Kalit = "1x00000000000000000000AA";

        public const string Yaroqli = "yaroqli-token";

        public ConcurrentQueue<(string? Token, string? ClientIp, Guid IdempotencyKey)> Chaqiruvlar { get; } = new();

        public string? SiteKey => Kalit;

        public Task<bool> VerifyAsync(string? token, string? clientIp, Guid idempotencyKey, CancellationToken cancellationToken)
        {
            Chaqiruvlar.Enqueue((token, clientIp, idempotencyKey));

            return Task.FromResult(token == Yaroqli);
        }
    }
}
