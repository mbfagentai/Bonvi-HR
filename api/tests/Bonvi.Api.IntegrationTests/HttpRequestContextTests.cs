using System.Net;
using System.Security.Claims;
using Bonvi.Api.Common;
using Bonvi.Application.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Infrastructure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Audit portlarining HTTP amalga oshirilishi: amal nomi, guruh, IP, brauzer va bajaruvchi turi.
/// </summary>
/// <remarks>
/// <c>TestServer</c> so'rovida mijoz IP si yo'q — shu sababli bu qoidalar
/// <see cref="DefaultHttpContext"/> ustida tekshiriladi.
/// </remarks>
public sealed class HttpRequestContextTests
{
    [Fact]
    public void Sorov_kontekstidan_amal_nomi_IP_va_brauzer_olinadi()
    {
        var http = Sorov(endpointNomi: "TransferEmployee");
        http.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
        http.Request.Headers.UserAgent = new string('A', 400);

        var kontekst = new HttpRequestContext(new HttpContextAccessor { HttpContext = http });

        kontekst.Operation.ShouldBe("TransferEmployee");
        kontekst.IpAddress.ShouldBe("203.0.113.7");
        kontekst.UserAgent.ShouldNotBeNull().Length.ShouldBe(400, "qisqartirishni yozuvning o'zi qiladi (300)");
    }

    /// <summary>
    /// Audit IP'si — Caddy bergan haqiqiy mijoz (SPEC-planshet-anketa §2.3.3), Cloudflare chekkasi emas.
    /// </summary>
    [Fact]
    public void HR_amalida_IP_Caddy_bergan_mijoz_IPsi()
    {
        var http = Sorov(endpointNomi: "CreateVacancy");
        http.Connection.RemoteIpAddress = IPAddress.Parse("172.70.1.9");
        http.Request.Headers[ClientIp.HeaderName] = "203.0.113.21";

        new HttpRequestContext(new HttpContextAccessor { HttpContext = http }).IpAddress.ShouldBe("203.0.113.21");
    }

    [Theory]
    [InlineData("salom")]
    [InlineData("203.0.113.21, 6.6.6.6")]
    [InlineData("")]
    public void Buzuq_sarlavhada_IP_ulanish_manzili(string sarlavha)
    {
        var http = Sorov(endpointNomi: "CreateVacancy");
        http.Connection.RemoteIpAddress = IPAddress.Parse("172.70.1.9");
        http.Request.Headers[ClientIp.HeaderName] = sarlavha;

        new HttpRequestContext(new HttpContextAccessor { HttpContext = http }).IpAddress.ShouldBe("172.70.1.9");
    }

    /// <summary>Qurilma endpointida — avvalgidek ulanish manzili, sarlavha bo'lsa ham (terminallar xulqi o'zgarmaydi).</summary>
    [Fact]
    public void Qurilma_endpointida_IP_ulanish_manzili_sarlavha_bolsa_ham()
    {
        var http = Sorov(endpointNomi: "IngestAttendanceEvent", DeviceEndpointMetadata.Instance);
        http.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.5");
        http.Request.Headers[ClientIp.HeaderName] = "6.6.6.6";

        new HttpRequestContext(new HttpContextAccessor { HttpContext = http }).IpAddress.ShouldBe("198.51.100.5");
    }

    /// <summary>Bitta so'rovda guruh BITTA — kontekst nusxasi boshqa bo'lsa ham.</summary>
    [Fact]
    public void Bitta_sorovda_guruh_bir_xil()
    {
        var http = Sorov(endpointNomi: "TransferEmployee");
        var accessor = new HttpContextAccessor { HttpContext = http };

        var birinchi = new HttpRequestContext(accessor).CorrelationId;
        var ikkinchi = new HttpRequestContext(accessor).CorrelationId;

        birinchi.ShouldNotBeNull();
        ikkinchi.ShouldBe(birinchi);
    }

    /// <summary>Qamrov amal nomini va guruhni almashtiradi; yopilganda — tiklaydi.</summary>
    [Fact]
    public void Qamrov_nomni_va_guruhni_almashtiradi_va_tiklaydi()
    {
        var kontekst = new HttpRequestContext(new HttpContextAccessor());

        kontekst.Operation.ShouldBeNull();
        var tashqi = kontekst.CorrelationId;
        tashqi.ShouldNotBeNull("HTTP'siz qamrovda ham guruh bor");

        using (kontekst.Use("startup:identity-seed"))
        {
            kontekst.Operation.ShouldBe("startup:identity-seed");
            var ichki = kontekst.CorrelationId;
            ichki.ShouldNotBe(tashqi);

            using (kontekst.Use("startup:catalog-seed"))
            {
                kontekst.Operation.ShouldBe("startup:catalog-seed");
                kontekst.CorrelationId.ShouldNotBe(ichki);
            }

            kontekst.Operation.ShouldBe("startup:identity-seed");
            kontekst.CorrelationId.ShouldBe(ichki);
        }

        kontekst.Operation.ShouldBeNull();
        kontekst.CorrelationId.ShouldBe(tashqi);
    }

    [Fact]
    public void HTTP_sorovi_bolmasa_bajaruvchi_tizim()
        => Manba(http: null).Current.ShouldBe(AuditPrincipal.System);

    [Fact]
    public void Qurilma_endpointida_bajaruvchi_qurilma()
    {
        var http = Sorov(endpointNomi: "GetAttendanceAgentRoster", DeviceEndpointMetadata.Instance);

        Manba(http).Current.Kind.ShouldBe(AuditActorKind.Device);
    }

    [Fact]
    public void HR_tokeni_bilan_bajaruvchi_foydalanuvchi()
    {
        var id = Guid.CreateVersion7();
        var http = Sorov(endpointNomi: "ListUsers");
        http.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Name, "+998905088888")],
            authenticationType: "Bearer"));

        var bajaruvchi = Manba(http).Current;

        bajaruvchi.Kind.ShouldBe(AuditActorKind.User);
        bajaruvchi.UserId.ShouldBe(id);
        bajaruvchi.Login.ShouldBe("+998905088888");
    }

    [Fact]
    public void Xodim_ilovasi_tokeni_bilan_bajaruvchi_xodim_hisobi()
    {
        var hisob = Guid.CreateVersion7();
        var xodim = Guid.CreateVersion7();
        var http = Sorov(endpointNomi: "SubmitAbsenceRequest");
        http.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(JwtRegisteredClaimNames.Sub, hisob.ToString()), new Claim(EmployeeJwtTokenService.EmployeeClaim, xodim.ToString())],
            authenticationType: "EmployeeBearer"));

        var bajaruvchi = Manba(http).Current;

        bajaruvchi.Kind.ShouldBe(AuditActorKind.EmployeeAccount);
        bajaruvchi.UserId.ShouldBeNull("xodim hisobi tizim foydalanuvchisi emas");
        bajaruvchi.EmployeeId.ShouldBe(xodim);
    }

    [Fact]
    public void Tokensiz_sorovda_bajaruvchi_anonim()
        => Manba(Sorov(endpointNomi: "Login")).Current.ShouldBe(AuditPrincipal.Anonymous);

    private static HttpAuditActorSource Manba(HttpContext? http)
    {
        var accessor = new HttpContextAccessor { HttpContext = http };
        return new HttpAuditActorSource(accessor, new CurrentUser(accessor), new CurrentEmployee(accessor));
    }

    private static DefaultHttpContext Sorov(string endpointNomi, params object[] qoshimcha)
    {
        var http = new DefaultHttpContext();
        http.SetEndpoint(new Endpoint(
            requestDelegate: null,
            new EndpointMetadataCollection([new EndpointNameMetadata(endpointNomi), .. qoshimcha]),
            endpointNomi));

        return http;
    }
}
