using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Domain.Employees;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Bo'lim boshlig'i xodim kartasidan — <c>PUT /api/catalog/departments/{id}/head</c> (egasi, 2026-10-31).
/// </summary>
/// <remarks>
/// Bo'lim formasidagi qoidalarning o'zi: davomatni ko'rmaydigan odam boshliq bera olmaydi, yo'q xodim
/// boshliq bo'lmaydi; faqat boshliq o'zgaradi — nom va yuridik shaxs tegilmaydi.
/// </remarks>
public sealed class DepartmentHeadApiTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public DepartmentHeadApiTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Kartadan_boshliq_tayinlanadi_almashadi_va_olib_tashlanadi()
    {
        var (bolim, nom, birinchi, ikkinchi) = await DunyoAsync();
        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);

        using (var javob = await admin.PutAsJsonAsync(Yol($"/api/catalog/departments/{bolim}/head"), new { employeeId = birinchi }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await javob.Content.ReadAsStringAsync(Ct));
            var tana = await javob.Content.ReadFromJsonAsync<JsonElement>(Ct);
            tana.GetProperty("headEmployeeId").GetGuid().ShouldBe(birinchi);
            tana.GetProperty("name").GetString().ShouldBe(nom, "nom tegilmaydi");
        }

        // Boshqasi tayinlansa — almashadi (bo'limda bitta boshliq)
        using (var javob = await admin.PutAsJsonAsync(Yol($"/api/catalog/departments/{bolim}/head"), new { employeeId = ikkinchi }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        (await BoshliqAsync(bolim)).ShouldBe(ikkinchi);

        using (var javob = await admin.PutAsJsonAsync(Yol($"/api/catalog/departments/{bolim}/head"), new { employeeId = (Guid?)null }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        (await BoshliqAsync(bolim)).ShouldBeNull();
    }

    [Fact]
    public async Task Yoq_xodim_yoq_bolim_va_ruxsatsiz_odam_rad_etiladi()
    {
        var (bolim, _, birinchi, _) = await DunyoAsync();
        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);

        using (var javob = await admin.PutAsJsonAsync(Yol($"/api/catalog/departments/{bolim}/head"), new { employeeId = Guid.NewGuid() }, Ct))
        {
            await XatoKutAsync(javob, HttpStatusCode.BadRequest, "bolim.boshliq_topilmadi");
        }

        using (var javob = await admin.PutAsJsonAsync(Yol($"/api/catalog/departments/{Guid.NewGuid()}/head"), new { employeeId = birinchi }, Ct))
        {
            await XatoKutAsync(javob, HttpStatusCode.NotFound, "bolim.topilmadi");
        }

        // Ma'lumotnomani boshqaradi, lekin davomatni ko'rmaydi — boshliq bera olmaydi
        using var katalogchi = _factory.CreateClient();
        katalogchi.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await TestAccounts.TokenAsync(_factory, "catalog.manage", "catalog.view"));

        using (var javob = await katalogchi.PutAsJsonAsync(Yol($"/api/catalog/departments/{bolim}/head"), new { employeeId = birinchi }, Ct))
        {
            await XatoKutAsync(javob, HttpStatusCode.Forbidden, "auth.ruxsat_yoq");
        }

        // Ma'lumotnomani boshqarmaydigan odam — umuman kirmaydi
        using var kuzatuvchi = _factory.CreateClient();
        kuzatuvchi.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await TestAccounts.TokenAsync(_factory, "attendance.view", "catalog.view"));

        using (var javob = await kuzatuvchi.PutAsJsonAsync(Yol($"/api/catalog/departments/{bolim}/head"), new { employeeId = birinchi }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        (await BoshliqAsync(bolim)).ShouldBeNull();
    }

    private async Task<(Guid Bolim, string Nom, Guid Birinchi, Guid Ikkinchi)> DunyoAsync()
    {
        var belgi = "BH" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var bolim = Department.Create("Boshliqli " + belgi).Value;
        var birinchi = Employee.Create(FullName.Create("Birinchi" + belgi, "Bosh", null).Value).Value;
        var ikkinchi = Employee.Create(FullName.Create("Ikkinchi" + belgi, "Bosh", null).Value).Value;
        kontekst.AddRange(bolim, birinchi, ikkinchi);
        await kontekst.SaveChangesAsync(Ct);

        return (bolim.Id, bolim.Name, birinchi.Id, ikkinchi.Id);
    }

    private async Task<Guid?> BoshliqAsync(Guid bolim)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
        return await kontekst.Departments.AsNoTracking()
            .Where(item => item.Id == bolim)
            .Select(item => item.HeadEmployeeId)
            .SingleAsync(Ct);
    }

    private static async Task XatoKutAsync(HttpResponseMessage javob, HttpStatusCode holat, string kod)
    {
        var matn = await javob.Content.ReadAsStringAsync(Ct);
        javob.StatusCode.ShouldBe(holat, matn);
        JsonDocument.Parse(matn).RootElement.GetProperty("title").GetString().ShouldBe(kod, matn);
    }

    private static Uri Yol(string yol) => new(yol, UriKind.Relative);
}
