using System.Net;
using System.Text;
using Bonvi.Application.Recruiting.Council;
using Bonvi.Infrastructure.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Bonvi.Api.IntegrationTests;

/// <summary>The real client against a stubbed network: the address Telegram gets and how its answers read.</summary>
public sealed class TelegramGroupMembershipTests
{
    private const string Token = "8997126064:AAabcdefghijklmnopqrstuvwxyz012345678";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class Stub(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<Uri> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Asked.Add(request.RequestUri!);

            return Task.FromResult(answer(request));
        }
    }

    private sealed class LogSink : ILogger<TelegramGroupMembership>
    {
        public List<string> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Lines.Add(formatter(state, exception) + exception);
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string body)
        => new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static (TelegramGroupMembership Client, LogSink Sink) Client(HttpMessageHandler handler)
    {
        var log = new LogSink();
#pragma warning disable CA2000 // The client owns nothing here; the test's process ends soon after.
        var http = new HttpClient(handler);
        var cache = new MemoryCache(new MemoryCacheOptions());
#pragma warning restore CA2000

        return (new TelegramGroupMembership(http, cache, log), log);
    }

    [Fact]
    public async Task Manzil_togri_va_token_scheme_bolib_qolmaydi()
    {
        using var stub = new Stub(_ => Json(HttpStatusCode.OK, """{"ok":true,"result":{"status":"member"}}"""));
        var (client, _) = Client(stub);

        (await client.CheckAsync(Token, -883839539, 77, Ct)).ShouldBe(TelegramGroupStatus.Member);

        stub.Asked.ShouldHaveSingleItem().AbsoluteUri
            .ShouldBe($"https://api.telegram.org/bot{Token}/getChatMember?chat_id=-883839539&user_id=77");
    }

    [Theory]
    [InlineData("creator", TelegramGroupStatus.Member)]
    [InlineData("administrator", TelegramGroupStatus.Member)]
    [InlineData("member", TelegramGroupStatus.Member)]
    [InlineData("left", TelegramGroupStatus.NotMember)]
    [InlineData("kicked", TelegramGroupStatus.NotMember)]
    public async Task Holat_oqiladi(string status, TelegramGroupStatus expected)
    {
        using var stub = new Stub(_ => Json(HttpStatusCode.OK, "{\"ok\":true,\"result\":{\"status\":\"" + status + "\"}}"));

        (await Client(stub).Client.CheckAsync(Token, -1, 77, Ct)).ShouldBe(expected);
    }

    [Theory]
    [InlineData(true, TelegramGroupStatus.Member)]
    [InlineData(false, TelegramGroupStatus.NotMember)]
    public async Task Cheklangan_azo_is_member_boyicha(bool isMember, TelegramGroupStatus expected)
    {
        using var stub = new Stub(_ => Json(HttpStatusCode.OK, "{\"ok\":true,\"result\":{\"status\":\"restricted\",\"is_member\":" + (isMember ? "true" : "false") + "}}"));

        (await Client(stub).Client.CheckAsync(Token, -1, 77, Ct)).ShouldBe(expected);
    }

    [Fact]
    public async Task Telegram_400_azo_emas_403_va_500_nomalum()
    {
        using var yoq = new Stub(_ => Json(HttpStatusCode.BadRequest, """{"ok":false,"description":"Bad Request: user not found"}"""));
        (await Client(yoq).Client.CheckAsync(Token, -1, 77, Ct)).ShouldBe(TelegramGroupStatus.NotMember);

        using var chiqarilgan = new Stub(_ => Json(HttpStatusCode.Forbidden, """{"ok":false,"description":"Forbidden: bot was kicked"}"""));
        (await Client(chiqarilgan).Client.CheckAsync(Token, -1, 77, Ct)).ShouldBe(TelegramGroupStatus.Unavailable);

        using var buzuq = new Stub(_ => Json(HttpStatusCode.InternalServerError, "oops"));
        (await Client(buzuq).Client.CheckAsync(Token, -1, 77, Ct)).ShouldBe(TelegramGroupStatus.Unavailable);
    }

    [Fact]
    public async Task Tarmoq_xatosi_ekranni_buzmaydi_tokenni_yozmaydi_keshlanmaydi()
    {
        using var stub = new Stub(_ => throw new HttpRequestException($"failed https://api.telegram.org/bot{Token}/x"));
        var (client, log) = Client(stub);

        (await client.CheckAsync(Token, -1, 77, Ct)).ShouldBe(TelegramGroupStatus.Unavailable);
        (await client.CheckAsync(Token, -1, 77, Ct)).ShouldBe(TelegramGroupStatus.Unavailable);

        stub.Asked.Count.ShouldBe(2);
        log.Lines.ShouldNotBeEmpty();
        log.Lines.ShouldAllBe(line => !line.Contains("AAabcdef", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Javob_bir_daqiqa_eslab_qolinadi()
    {
        using var stub = new Stub(_ => Json(HttpStatusCode.OK, """{"ok":true,"result":{"status":"member"}}"""));
        var (client, _) = Client(stub);

        await client.CheckAsync(Token, -1, 77, Ct);
        await client.CheckAsync(Token, -1, 77, Ct);
        await client.CheckAsync(Token, -1, 78, Ct);

        stub.Asked.Count.ShouldBe(2);
    }
}
