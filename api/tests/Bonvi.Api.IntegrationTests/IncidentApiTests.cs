using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Baxtsiz hodisa javobi (test uchun).</summary>
public sealed record IncidentResponse(
    Guid Id,
    Guid EmployeeId,
    string? EmployeeName,
    string? PhotoKey,
    DateOnly IncidentDate,
    string? Shift,
    Guid? PositionId,
    string? PositionName,
    string Location,
    string Description,
    string? Cause,
    string? CorrectiveAction,
    string Severity,
    bool MedicalAidProvided,
    string? MedicalAidNote,
    string? CommissionConclusion,
    DateOnly? ConcludedAt,
    Guid? ResponsibleEmployeeId,
    string? ResponsibleName);

/// <summary>
/// Baxtsiz hodisalar jurnali (M11) — uchi-uchiga: yozish, ro'yxat, tuzatish,
/// yakunlash, o'chirish.
/// </summary>
/// <remarks>
/// Mantiq <c>IncidentTests</c> va <c>IncidentHandlerTests</c> da qoplangan —
/// bu yerda faqat ULANISH tekshiriladi.
/// </remarks>
public sealed class IncidentApiTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public IncidentApiTests(BonviApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Hodisa_yoziladi_va_royxatda_xodim_nomi_bilan_chiqadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var xodim = await XodimAsync();

        using var yozish = await mijoz.PostAsJsonAsync(
            new Uri("/api/safety/incidents", UriKind.Relative),
            new
            {
                employeeId = xodim.Id,
                incidentDate = new DateOnly(2026, 3, 1),
                location = "Metan zavodi",
                description = "Qo'l jarohati",
                severity = "Severe",
                medicalAidProvided = true,
                medicalAidNote = "Tez tibbiy yordam chaqirildi",
            },
            bekorQilish);

        yozish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yozish));

        var natija = await yozish.Content.ReadFromJsonAsync<IncidentResponse>(bekorQilish);
        natija!.EmployeeId.ShouldBe(xodim.Id);
        natija.EmployeeName.ShouldBe(xodim.Name);
        natija.Severity.ShouldBe("Severe");
        natija.MedicalAidProvided.ShouldBeTrue();
        natija.CommissionConclusion.ShouldBeNull();

        using var royxat = await mijoz.GetAsync(new Uri("/api/safety/incidents", UriKind.Relative), bekorQilish);
        var qatorlar = await royxat.Content.ReadFromJsonAsync<IReadOnlyList<IncidentResponse>>(bekorQilish);
        qatorlar!.ShouldContain(q => q.Id == natija.Id);
    }

    [Fact]
    public async Task Tavsifsiz_yozish_400_qaytaradi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var xodim = await XodimAsync();

        using var javob = await mijoz.PostAsJsonAsync(
            new Uri("/api/safety/incidents", UriKind.Relative),
            new
            {
                employeeId = xodim.Id,
                incidentDate = new DateOnly(2026, 3, 1),
                location = "Metan zavodi",
                description = (string?)null,
                severity = "Minor",
            },
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(javob));
        (await MatnAsync(javob)).ShouldContain("baxtsiz_hodisa.tavsif_bosh");
    }

    [Fact]
    public async Task Tuzatish_yakunlash_va_ochirish_ishlaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var xodim = await XodimAsync();

        using var yozish = await mijoz.PostAsJsonAsync(
            new Uri("/api/safety/incidents", UriKind.Relative),
            new
            {
                employeeId = xodim.Id,
                incidentDate = new DateOnly(2026, 3, 1),
                location = "Sex 1",
                description = "Boshlang'ich tavsif",
                severity = "Minor",
            },
            bekorQilish);

        var yaratilgan = await yozish.Content.ReadFromJsonAsync<IncidentResponse>(bekorQilish);

        using var tuzatish = await mijoz.PutAsJsonAsync(
            new Uri($"/api/safety/incidents/{yaratilgan!.Id}", UriKind.Relative),
            new
            {
                incidentDate = new DateOnly(2026, 3, 2),
                location = "Sex 2",
                description = "Tuzatilgan tavsif",
                severity = "Fatal",
            },
            bekorQilish);

        tuzatish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(tuzatish));
        var tuzatilgan = await tuzatish.Content.ReadFromJsonAsync<IncidentResponse>(bekorQilish);
        tuzatilgan!.Location.ShouldBe("Sex 2");
        tuzatilgan.Severity.ShouldBe("Fatal");
        tuzatilgan.EmployeeId.ShouldBe(xodim.Id, "xodim tuzatishda o'zgarmaydi");

        using var xulosasiz = await mijoz.PostAsJsonAsync(
            new Uri($"/api/safety/incidents/{yaratilgan.Id}/conclude", UriKind.Relative),
            new { conclusion = (string?)null },
            bekorQilish);

        xulosasiz.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(xulosasiz));
        (await MatnAsync(xulosasiz)).ShouldContain("baxtsiz_hodisa.xulosa_bosh");

        using var yakunlash = await mijoz.PostAsJsonAsync(
            new Uri($"/api/safety/incidents/{yaratilgan.Id}/conclude", UriKind.Relative),
            new { conclusion = "Xavfsizlik qoidalari buzilgan", concludedAt = new DateOnly(2026, 3, 15) },
            bekorQilish);

        yakunlash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yakunlash));
        var yakunlangan = await yakunlash.Content.ReadFromJsonAsync<IncidentResponse>(bekorQilish);
        yakunlangan!.CommissionConclusion.ShouldBe("Xavfsizlik qoidalari buzilgan");
        yakunlangan.ConcludedAt.ShouldBe(new DateOnly(2026, 3, 15));

        using var ochirish = await mijoz.DeleteAsync(
            new Uri($"/api/safety/incidents/{yaratilgan.Id}", UriKind.Relative), bekorQilish);

        ochirish.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(ochirish));

        using var royxat = await mijoz.GetAsync(new Uri("/api/safety/incidents", UriKind.Relative), bekorQilish);
        var qatorlar = await royxat.Content.ReadFromJsonAsync<IReadOnlyList<IncidentResponse>>(bekorQilish);
        qatorlar!.ShouldNotContain(
            q => q.Id == yaratilgan.Id, "yumshoq o'chirilgan yozuv ro'yxatda chiqmasligi kerak");
    }

    // ────────────────────────────────────────────────────────────
    //  Muhit tayyorlash
    // ────────────────────────────────────────────────────────────

    private sealed record Xodim(Guid Id, string Name);

    private async Task<Xodim> XodimAsync()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var belgi = Guid.NewGuid().ToString("N")[..6];

        var xodim = Employee.Create(FullName.Create("Жабрланувчи", $"Ном{belgi}", null).Value).Value;
        kontekst.Employees.Add(xodim);

        await kontekst.SaveChangesAsync(bekorQilish);

        return new Xodim(xodim.Id, xodim.FullName.Full);
    }

    private async Task<HttpClient> AdminMijozAsync()
    {
        var client = _factory.CreateClient();

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

    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
