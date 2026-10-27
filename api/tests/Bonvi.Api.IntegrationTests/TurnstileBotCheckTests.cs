using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Bonvi.Infrastructure.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Bot tekshiruvi — Cloudflare Turnstile mijozi (SPEC-planshet-anketa §2.3.2): so'rov shakli,
/// yopiq-sukut (rad, xato, vaqt tugashi) va jurnalga token ham, maxfiy kalit ham tushmasligi.
/// </summary>
/// <remarks>
/// Tarmoqqa chiqmaydi: <see cref="TurnstileStub"/> Cloudflare o'rnida javob beradi. Bazaga ham
/// tegmaydi — shu sababli fabrikasiz, oddiy sinf.
/// </remarks>
public sealed class TurnstileBotCheckTests
{
    private const string SiteKey = "0x4AAAAAAAsinovOchiqKalit";
    private const string Secret = "0x4AAAAAAAsinovMAXFIYkalit_hech_qachon_jurnalda_emas";
    private const string Token = "XXXX.sinov-tokeni.hech-qachon-jurnalda-emas";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sorov_shakli_POST_forma_secret_response_remoteip_idempotency_key()
    {
        var stub = new TurnstileStub((_, _) => TurnstileStub.Json("""{"success":true,"error-codes":[]}"""));
        var kalit = Guid.NewGuid();

        var natija = await Tekshiruvchi(stub).VerifyAsync(Token, "203.0.113.9", kalit, Ct);

        natija.ShouldBeTrue();

        var sorov = stub.Sorovlar.ShouldHaveSingleItem();
        sorov.Method.ShouldBe(HttpMethod.Post);
        sorov.Url.ShouldBe(new Uri("https://challenges.cloudflare.com/turnstile/v0/siteverify"));
        sorov.ContentType.ShouldBe("application/x-www-form-urlencoded");
        sorov.Forma.ShouldBe(new Dictionary<string, string>
        {
            ["secret"] = Secret,
            ["response"] = Token,
            ["idempotency_key"] = kalit.ToString(),
            ["remoteip"] = "203.0.113.9",
        });
    }

    [Fact]
    public async Task IP_bolmasa_remoteip_yuborilmaydi()
    {
        var stub = new TurnstileStub((_, _) => TurnstileStub.Json("""{"success":true}"""));

        (await Tekshiruvchi(stub).VerifyAsync(Token, null, Guid.NewGuid(), Ct)).ShouldBeTrue();

        stub.Sorovlar.ShouldHaveSingleItem().Forma.Keys.ShouldNotContain("remoteip");
    }

    [Fact]
    public async Task Rad_etilgan_token_false_va_jurnalda_token_ham_kalit_ham_yoq()
    {
        var stub = new TurnstileStub((_, _) =>
            TurnstileStub.Json("""{"success":false,"error-codes":["invalid-input-response","timeout-or-duplicate"]}"""));
        var jurnal = new YozibOluvchiLogger();

        (await Tekshiruvchi(stub, jurnal).VerifyAsync(Token, "203.0.113.9", Guid.NewGuid(), Ct)).ShouldBeFalse();

        var qator = jurnal.Qatorlar.ShouldHaveSingleItem();
        qator.ShouldContain("invalid-input-response,timeout-or-duplicate");
        jurnal.Hammasi.ShouldNotContain(Token);
        jurnal.Hammasi.ShouldNotContain(Secret);
    }

    [Fact]
    public async Task Maxfiy_kalit_rad_etilsa_xato_darajasida_jurnalga_kalitning_ozisiz()
    {
        var stub = new TurnstileStub((_, _) => TurnstileStub.Json("""{"success":false,"error-codes":["invalid-input-secret"]}"""));
        var jurnal = new YozibOluvchiLogger();

        (await Tekshiruvchi(stub, jurnal).VerifyAsync(Token, null, Guid.NewGuid(), Ct)).ShouldBeFalse();

        jurnal.Darajalar.ShouldHaveSingleItem().ShouldBe(LogLevel.Error, "sozlama xatosi — hamma anketa rad etilmoqda");
        jurnal.Hammasi.ShouldNotContain(Secret);
        jurnal.Hammasi.ShouldNotContain(Token);
    }

    [Fact]
    public async Task Cloudflare_xatosi_ulanish_uzilishi_va_buzuq_javob_hammasi_false()
    {
        var holatlar = new Func<TurnstileStubRequest, CancellationToken, Task<HttpResponseMessage>>[]
        {
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)),
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)),
            (_, _) => throw new HttpRequestException("ulanib bo'lmadi", null, HttpStatusCode.BadGateway),
            (_, _) => TurnstileStub.Json("<html>Cloudflare</html>"),
            (_, _) => TurnstileStub.Json("null"),
        };

        foreach (var holat in holatlar)
        {
            var jurnal = new YozibOluvchiLogger();

            (await Tekshiruvchi(new TurnstileStub(holat), jurnal).VerifyAsync(Token, "203.0.113.9", Guid.NewGuid(), Ct))
                .ShouldBeFalse("yopiq-sukut");

            jurnal.Hammasi.ShouldNotContain(Token);
            jurnal.Hammasi.ShouldNotContain(Secret);
        }
    }

    [Fact]
    public async Task Vaqt_tugasa_false()
    {
        var stub = new TurnstileStub(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var jurnal = new YozibOluvchiLogger();

        // Haqiqiy muddat 5 s (DI'da — KioskQuestionnaireApiTests.Bot_tekshiruvi_mijozining_muddati_5_soniya);
        // bu yerda qisqa, test kutib o'tirmasin
        var natija = await Tekshiruvchi(stub, jurnal, TimeSpan.FromMilliseconds(200)).VerifyAsync(Token, null, Guid.NewGuid(), Ct);

        natija.ShouldBeFalse();
        jurnal.Qatorlar.ShouldHaveSingleItem().ShouldContain("vaqt tugadi");
    }

    [Fact]
    public async Task Sorovchi_bekor_qilsa_istisno_yutilmaydi()
    {
        var stub = new TurnstileStub(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var bekor = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Mijoz uzildi — bu «Cloudflare javob bermadi» emas: natija hech kimga kerak emas
        var istisno = await Record.ExceptionAsync(() => Tekshiruvchi(stub).VerifyAsync(Token, null, Guid.NewGuid(), bekor.Token));

        istisno.ShouldBeAssignableTo<OperationCanceledException>();
    }

    [Fact]
    public async Task Token_yoq_yoki_juda_uzun_Cloudflarega_yuborilmaydi()
    {
        var stub = new TurnstileStub((_, _) => TurnstileStub.Json("""{"success":true}"""));
        var tekshiruvchi = Tekshiruvchi(stub);

        (await tekshiruvchi.VerifyAsync(null, null, Guid.NewGuid(), Ct)).ShouldBeFalse();
        (await tekshiruvchi.VerifyAsync("   ", null, Guid.NewGuid(), Ct)).ShouldBeFalse();
        (await tekshiruvchi.VerifyAsync(new string('x', TurnstileBotCheck.MaxTokenLength + 1), null, Guid.NewGuid(), Ct)).ShouldBeFalse();

        stub.Sorovlar.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("", "")]
    [InlineData(SiteKey, "")]
    [InlineData("", Secret)]
    [InlineData("  ", "  ")]
    public async Task Kalitlardan_biri_bolmasa_tekshiruv_yoqilmaydi_va_Cloudflarega_sorov_yoq(string siteKey, string secret)
    {
        var stub = new TurnstileStub((_, _) => TurnstileStub.Json("""{"success":true}"""));
        var tekshiruvchi = Tekshiruvchi(stub, options: new TurnstileOptions { SiteKey = siteKey, SecretKey = secret });

        tekshiruvchi.SiteKey.ShouldBeNull();
        (await tekshiruvchi.VerifyAsync(Token, null, Guid.NewGuid(), Ct)).ShouldBeFalse("o'chiq tekshiruv eshikni ochmaydi");
        stub.Sorovlar.ShouldBeEmpty();
    }

    [Fact]
    public void Yarim_sozlama_ogohlantirish_matnini_beradi_kalitning_ozisiz()
    {
        new TurnstileOptions { SiteKey = SiteKey }.Problem.ShouldBe("Turnstile__SecretKey berilmagan");
        new TurnstileOptions { SecretKey = Secret }.Problem.ShouldBe("Turnstile__SiteKey berilmagan");
        new TurnstileOptions().Problem.ShouldBeNull("ikkalasi bo'sh — o'chiq, bu xato emas");
        new TurnstileOptions { SiteKey = SiteKey, SecretKey = Secret }.Problem.ShouldBeNull();
    }

    [Fact]
    public void Ikkala_kalit_bolsa_ochiq_kalit_beriladi_bosh_joysiz()
        => Tekshiruvchi(new TurnstileStub((_, _) => TurnstileStub.Json("{}")), options: new TurnstileOptions { SiteKey = " " + SiteKey + "\n", SecretKey = Secret })
            .SiteKey.ShouldBe(SiteKey);

    private static TurnstileBotCheck Tekshiruvchi(
        TurnstileStub stub,
        YozibOluvchiLogger? jurnal = null,
        TimeSpan? timeout = null,
        TurnstileOptions? options = null)
    {
        var http = new HttpClient(stub) { Timeout = timeout ?? TurnstileBotCheck.Timeout };

        return new TurnstileBotCheck(
            http,
            Options.Create(options ?? new TurnstileOptions { SiteKey = SiteKey, SecretKey = Secret }),
            jurnal ?? new YozibOluvchiLogger());
    }

    /// <summary>Jurnal qatorlarini yig'adi — ichida token yoki kalit yo'qligini tekshirish uchun.</summary>
    private sealed class YozibOluvchiLogger : ILogger<TurnstileBotCheck>
    {
        private readonly ConcurrentQueue<(LogLevel Level, string Text)> _qatorlar = new();

        public IReadOnlyList<string> Qatorlar => [.. _qatorlar.Select(qator => qator.Text)];

        public IReadOnlyList<LogLevel> Darajalar => [.. _qatorlar.Select(qator => qator.Level)];

        public string Hammasi => string.Join('\n', Qatorlar);

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            // Matn ham, xom qiymatlar ham (strukturali jurnal ularni alohida yozadi)
            var qiymatlar = state is IEnumerable<KeyValuePair<string, object?>> juftlar
                ? string.Join(' ', juftlar.Select(juft => juft.Key + "=" + juft.Value))
                : string.Empty;

            _qatorlar.Enqueue((logLevel, formatter(state, exception) + " | " + qiymatlar + " | " + exception));
        }
    }
}

/// <summary>Cloudflare o'rnida — so'rovni yozib oladi va berilgan javobni qaytaradi.</summary>
internal sealed class TurnstileStub : HttpMessageHandler
{
    private readonly Func<TurnstileStubRequest, CancellationToken, Task<HttpResponseMessage>> _javob;

    /// <param name="javob">Javob — ko'rilgan so'rovga qarab (masalan tokenga).</param>
    public TurnstileStub(Func<TurnstileStubRequest, CancellationToken, Task<HttpResponseMessage>> javob) => _javob = javob;

    public ConcurrentQueue<TurnstileStubRequest> Sorovlar { get; } = new();

    public static Task<HttpResponseMessage> Json(string body) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    });

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var matn = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);

        var forma = matn
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(juft => juft.Split('=', 2))
            .ToDictionary(juft => WebUtility.UrlDecode(juft[0]), juft => WebUtility.UrlDecode(juft.Length > 1 ? juft[1] : string.Empty));

        var sorov = new TurnstileStubRequest(request.Method, request.RequestUri, request.Content?.Headers.ContentType?.MediaType, forma);
        Sorovlar.Enqueue(sorov);

        return await _javob(sorov, cancellationToken);
    }
}

/// <summary>Stub ko'rgan so'rov.</summary>
internal sealed record TurnstileStubRequest(HttpMethod Method, Uri? Url, string? ContentType, Dictionary<string, string> Forma);
