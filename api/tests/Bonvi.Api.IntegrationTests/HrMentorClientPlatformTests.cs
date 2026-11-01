using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Bonvi.Application.Blanks;
using Bonvi.Infrastructure.Services;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// <see cref="HrMentorClient"/> ning platforma yo'llari (SPEC-hujjat-manbai §2) — haqiqiy HTTP
/// javoblari bilan: qaysi 403/404 «hali bog'lanmagan» (eski ro'yxatga o'tish), qaysi biri xato,
/// va hisobot tanasining shakli.
/// </summary>
public sealed class HrMentorClientPlatformTests
{
    private static readonly HrMentorConnection Ulanish =
        HrMentorConnection.From("https://hrmentor.test", "hrm_" + new string('k', 43))!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "platforms.key_not_bound", "hrmentor.platform_not_ready")]
    [InlineData(HttpStatusCode.Forbidden, "app.permission_denied", "hrmentor.platform_not_ready")]
    [InlineData(HttpStatusCode.NotFound, "app.not_found", "hrmentor.platform_not_ready")]
    [InlineData(HttpStatusCode.NotFound, null, "hrmentor.platform_not_ready")]
    [InlineData(HttpStatusCode.Forbidden, "platforms.platform_inactive", "hrmentor.platform_off")]
    [InlineData(HttpStatusCode.Forbidden, "boshqa.kod", "hrmentor.forbidden")]
    [InlineData(HttpStatusCode.Forbidden, null, "hrmentor.forbidden")]
    [InlineData(HttpStatusCode.Unauthorized, "app.not_authenticated", "hrmentor.unauthorized")]
    [InlineData(HttpStatusCode.InternalServerError, "app.internal", "hrmentor.server_error")]
    public async Task Royxat_xatosi_kodiga_qarab_ajratiladi(HttpStatusCode holat, string? kod, string kutilgan)
    {
        var stub = new Stub(_ => Javob(holat, kod is null ? "<html>nope</html>" : Xato(kod)));
        var client = new HrMentorClient(new HttpClient(stub));

        var natija = await client.ListPlatformDocumentsAsync(Ulanish, Ct);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe(kutilgan);
    }

    [Fact]
    public async Task Royxat_platforma_va_hujjatlarni_oqiydi()
    {
        var hujjat = Guid.NewGuid();
        var versiya = Guid.NewGuid();
        var stub = new Stub(_ => Javob(HttpStatusCode.OK, new JsonObject
        {
            ["platform"] = new JsonObject { ["id"] = HrMentorFake.PlatformId, ["name"] = "hr.bonvi.uz" },
            ["documents"] = new JsonArray(Qator(hujjat, versiya)),
        }.ToJsonString()));
        var client = new HrMentorClient(new HttpClient(stub));

        var natija = await client.ListPlatformDocumentsAsync(Ulanish, Ct);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.PlatformId.ShouldBe(HrMentorFake.PlatformId);
        natija.Value.PlatformName.ShouldBe("hr.bonvi.uz");
        var item = natija.Value.Documents.ShouldHaveSingleItem();
        item.DocumentId.ShouldBe(hujjat);
        item.VersionId.ShouldBe(versiya);
        item.Code.ShouldBe("HR-F02");
        item.SizeBytes.ShouldBe(5);

        var sorov = stub.Requests.ShouldHaveSingleItem();
        sorov.Method.ShouldBe(HttpMethod.Get);
        sorov.RequestUri!.AbsolutePath.ShouldBe("/api/v1/platform/documents");
        sorov.Headers.Authorization!.Scheme.ShouldBe("Bearer");
    }

    [Fact]
    public async Task Chala_qator_yoki_platformasiz_javob_tushunarsiz()
    {
        var chala = Qator(Guid.NewGuid(), Guid.NewGuid());
        chala["sha256"] = "qisqa";
        foreach (var tana in new[]
        {
            new JsonObject
            {
                ["platform"] = new JsonObject { ["id"] = HrMentorFake.PlatformId, ["name"] = "hr.bonvi.uz" },
                ["documents"] = new JsonArray(chala),
            }.ToJsonString(),
            new JsonObject { ["documents"] = new JsonArray() }.ToJsonString(),
            "[]",
        })
        {
            var client = new HrMentorClient(new HttpClient(new Stub(_ => Javob(HttpStatusCode.OK, tana))));
            (await client.ListPlatformDocumentsAsync(Ulanish, Ct)).Error.ShouldBe(HrMentorErrors.BadResponse, tana);
        }
    }

    [Fact]
    public async Task Fayl_platforma_yolidan_olinadi()
    {
        var hujjat = Guid.NewGuid();
        var stub = new Stub(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) });
        var client = new HrMentorClient(new HttpClient(stub));

        var natija = await client.DownloadPlatformDocumentAsync(Ulanish, hujjat, 1024, Ct);

        natija.Value.ShouldBe(new byte[] { 1, 2, 3 });
        stub.Requests.ShouldHaveSingleItem().RequestUri!.AbsolutePath.ShouldBe($"/api/v1/platform/documents/{hujjat:D}/file");
    }

    [Fact]
    public async Task Hisobot_toliq_toplamni_snake_case_da_yuboradi()
    {
        string? tana = null;
        var stub = new Stub(request =>
        {
            tana = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return Javob(HttpStatusCode.OK, """{"recorded": 1, "ignored": 1}""");
        });
        var client = new HrMentorClient(new HttpClient(stub));
        var a = new HrMentorHolding(Guid.NewGuid(), Guid.NewGuid());
        var b = new HrMentorHolding(Guid.NewGuid(), Guid.NewGuid());

        var natija = await client.ReportHoldingsAsync(Ulanish, [a, b], Ct);

        natija.Value.ShouldBe(new HrMentorHoldingsReport(1, 1));
        var sorov = stub.Requests.ShouldHaveSingleItem();
        sorov.Method.ShouldBe(HttpMethod.Put);
        sorov.RequestUri!.AbsolutePath.ShouldBe("/api/v1/platform/holdings");
        var hujjatlar = JsonNode.Parse(tana!)!["documents"]!.AsArray();
        hujjatlar.Count.ShouldBe(2);
        hujjatlar[0]!["document_id"]!.GetValue<Guid>().ShouldBe(a.DocumentId);
        hujjatlar[0]!["version_id"]!.GetValue<Guid>().ShouldBe(a.VersionId);
    }

    [Fact]
    public async Task Hisobot_rad_etilsa_xato_kodi()
    {
        var client = new HrMentorClient(new HttpClient(new Stub(_ => Javob(HttpStatusCode.Forbidden, Xato("platforms.key_not_bound")))));

        (await client.ReportHoldingsAsync(Ulanish, [], Ct)).Error.ShouldBe(HrMentorErrors.Forbidden);
    }

    private static JsonObject Qator(Guid hujjat, Guid versiya) => new()
    {
        ["document_id"] = hujjat,
        ["code"] = "HR-F02",
        ["title"] = "Nomzod anketasi",
        ["doc_type"] = "form",
        ["version_id"] = versiya,
        ["version_no"] = 3,
        ["published_at"] = "2026-11-01T08:00:00Z",
        ["file_name"] = "HR-F02.pdf",
        ["content_type"] = "application/pdf",
        ["size_bytes"] = 5,
        ["sha256"] = new string('a', 64),
    };

    private static string Xato(string kod) => new JsonObject
    {
        ["error"] = new JsonObject { ["code"] = kod, ["message"] = "m", ["details"] = new JsonObject() },
    }.ToJsonString();

    private static HttpResponseMessage Javob(HttpStatusCode holat, string tana)
        => new(holat) { Content = new StringContent(tana, Encoding.UTF8, "application/json") };

    /// <summary>So'rovlarni yozib, berilgan javobni qaytaradi.</summary>
    private sealed class Stub(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(answer(request));
        }
    }
}
