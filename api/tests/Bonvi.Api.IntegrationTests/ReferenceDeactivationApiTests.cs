using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Ma'lumotnomani faol emas qilish: ishlayotgan xodim qolsa — yo'q, ular oynaning o'zida boshqasiga
/// o'tkaziladi (docs/SPEC-malumotnoma-faolsizlantirish.md; egasi, 2026-10-31).
/// </summary>
public sealed class ReferenceDeactivationApiTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public ReferenceDeactivationApiTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Bolim_xodimlari_otkazilgach_quyi_bolimlari_bilan_birga_faol_emas_boladi()
    {
        var belgi = Belgi();
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var firma = LegalEntity.Create("Yopish MCHJ " + belgi).Value;
        var ota = Department.Create("Yopiladigan " + belgi).Value;
        var bola = Department.Create("Ichidagi " + belgi, ota.Id).Value;
        var yangi = Department.Create("Yangi joy " + belgi).Value;
        var aziz = Employee.Create(FullName.Create("Azizov" + belgi, "Aziz", null).Value).Value;
        var bobur = Employee.Create(FullName.Create("Boburov" + belgi, "Bobur", null).Value).Value;
        var ketgan = Employee.Create(FullName.Create("Ketganov" + belgi, "Kamol", null).Value).Value;
        var azizIshi = Employment.Hire(aziz.Id, firma.Id, new DateOnly(2025, 1, 1), ota.Id).Value;
        var boburIshi = Employment.Hire(bobur.Id, firma.Id, new DateOnly(2025, 1, 1), bola.Id).Value;
        var ketganIshi = Employment.Hire(ketgan.Id, firma.Id, new DateOnly(2024, 1, 1), bola.Id).Value;
        ketganIshi.Terminate(new DateOnly(2024, 6, 1), null).IsSuccess.ShouldBeTrue();
        kontekst.AddRange(firma, ota, bola, yangi, aziz, bobur, ketgan, azizIshi, boburIshi, ketganIshi);
        await kontekst.SaveChangesAsync(Ct);

        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);

        // Ishdan ketgan sanalmaydi; quyi bo'lim xodimi ham sanaladi
        using (var javob = await admin.GetAsync(Yol($"/api/catalog/departments/{ota.Id}/usage"), Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await javob.Content.ReadAsStringAsync(Ct));
            var tana = await javob.Content.ReadFromJsonAsync<JsonElement>(Ct);
            tana.GetProperty("activeEmployees").GetInt32().ShouldBe(2);
            tana.GetProperty("activeSubdepartments").GetInt32().ShouldBe(1);
            tana.GetProperty("canReassign").GetBoolean().ShouldBeTrue();
            tana.GetProperty("employeesHidden").GetBoolean().ShouldBeFalse();
            tana.GetProperty("employees").EnumerateArray()
                .Select(item => item.GetProperty("employeeId").GetGuid())
                .ShouldBe([aziz.Id, bobur.Id], ignoreOrder: true);
        }

        using (var javob = await admin.PostAsync(Yol($"/api/catalog/departments/{ota.Id}/deactivate"), null, Ct))
        {
            await XatoKutAsync(javob, HttpStatusCode.Conflict, "malumotnoma.faol_xodim_bor");
        }

        // O'zining ichiga o'tkazib bo'lmaydi
        using (var javob = await admin.PostAsJsonAsync(Yol($"/api/catalog/departments/{ota.Id}/reassign"), new { targetId = bola.Id }, Ct))
        {
            await XatoKutAsync(javob, HttpStatusCode.BadRequest, "malumotnoma.otkazish_joyi_notogri");
        }

        using (var javob = await admin.PostAsJsonAsync(
                   Yol($"/api/catalog/departments/{ota.Id}/reassign"),
                   new { targetId = yangi.Id, effectiveDate = "2026-10-31", orderNo = "B-" + belgi },
                   Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.NoContent, await javob.Content.ReadAsStringAsync(Ct));
        }

        await using (var tekshiruv = _factory.Services.CreateAsyncScope())
        {
            var baza = tekshiruv.ServiceProvider.GetRequiredService<AppDbContext>();

            var ishlar = await baza.Employments.AsNoTracking()
                .Include(item => item.Events)
                .Where(item => item.Id == azizIshi.Id || item.Id == boburIshi.Id || item.Id == ketganIshi.Id)
                .ToListAsync(Ct);

            ishlar.Single(item => item.Id == azizIshi.Id).DepartmentId.ShouldBe(yangi.Id);
            ishlar.Single(item => item.Id == boburIshi.Id).DepartmentId.ShouldBe(yangi.Id);
            ishlar.Single(item => item.Id == ketganIshi.Id).DepartmentId.ShouldBe(bola.Id, "ketganning tarixi tegilmaydi");

            var otkazish = ishlar.Single(item => item.Id == boburIshi.Id).Events
                .Single(item => item.Type == EmploymentEventType.Transfer);
            otkazish.OrderNo.ShouldBe("B-" + belgi);
            otkazish.FromDepartmentId.ShouldBe(bola.Id);
            otkazish.EffectiveDate.ShouldBe(new DateOnly(2026, 10, 31));

            var holat = await baza.Departments.AsNoTracking()
                .Where(item => item.Id == ota.Id || item.Id == bola.Id || item.Id == yangi.Id)
                .ToDictionaryAsync(item => item.Id, item => item.IsActive, Ct);
            holat[ota.Id].ShouldBeFalse();
            holat[bola.Id].ShouldBeFalse("quyi bo'lim birga yopiladi");
            holat[yangi.Id].ShouldBeTrue();
        }

        // Faol emas bo'limga o'tkazib bo'lmaydi
        using (var javob = await admin.PostAsJsonAsync(Yol($"/api/catalog/departments/{yangi.Id}/reassign"), new { targetId = ota.Id }, Ct))
        {
            await XatoKutAsync(javob, HttpStatusCode.BadRequest, "malumotnoma.otkazish_joyi_notogri");
        }

        // Qayta yoqish: avval ota, keyin quyi bo'lim
        using (var javob = await admin.PostAsync(Yol($"/api/catalog/departments/{bola.Id}/activate"), null, Ct))
        {
            await XatoKutAsync(javob, HttpStatusCode.Conflict, "bolim.ota_faol_emas");
        }

        using (var javob = await admin.PostAsync(Yol($"/api/catalog/departments/{ota.Id}/activate"), null, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        using (var javob = await admin.PostAsync(Yol($"/api/catalog/departments/{bola.Id}/activate"), null, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        // Endi bo'sh — oddiy faol emas qilish o'tadi va quyi bo'limni ham yopadi
        using (var javob = await admin.PostAsync(Yol($"/api/catalog/departments/{ota.Id}/deactivate"), null, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.NoContent, await javob.Content.ReadAsStringAsync(Ct));
        }

        (await FaolmiAsync<Department>(bola.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task Lavozim_va_yonalish_otkaziladi_yuridik_shaxs_faqat_toxtatadi()
    {
        var belgi = Belgi();
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var firma = LegalEntity.Create("Ishlayotgan MCHJ " + belgi).Value;
        var bolim = Department.Create("Lavozimli " + belgi).Value;
        var eski = Position.Create("Eski lavozim " + belgi).Value;
        var yangi = Position.Create("Yangi lavozim " + belgi).Value;
        var bosh = Position.Create("Bo'sh lavozim " + belgi).Value;
        var eskiYonalish = Direction.Create("Eski yo'nalish " + belgi).Value;
        var yangiYonalish = Direction.Create("Yangi yo'nalish " + belgi).Value;
        var xodim = Employee.Create(FullName.Create("Lavozimov" + belgi, "Laziz", null).Value).Value;
        xodim.AssignDirection(eskiYonalish.Id);
        var ish = Employment.Hire(xodim.Id, firma.Id, new DateOnly(2025, 1, 1), bolim.Id, eski.Id).Value;
        kontekst.AddRange(firma, bolim, eski, yangi, bosh, eskiYonalish, yangiYonalish, xodim, ish);
        await kontekst.SaveChangesAsync(Ct);

        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);

        // Lavozim
        using (var javob = await admin.PostAsync(Yol($"/api/catalog/positions/{eski.Id}/deactivate"), null, Ct))
        {
            await XatoKutAsync(javob, HttpStatusCode.Conflict, "malumotnoma.faol_xodim_bor");
        }

        using (var javob = await admin.PostAsJsonAsync(Yol($"/api/catalog/positions/{eski.Id}/reassign"), new { targetId = yangi.Id }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.NoContent, await javob.Content.ReadAsStringAsync(Ct));
        }

        (await FaolmiAsync<Position>(eski.Id)).ShouldBeFalse();

        // Xodimi yo'q lavozim — avvalgidek darhol
        using (var javob = await admin.PostAsync(Yol($"/api/catalog/positions/{bosh.Id}/deactivate"), null, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        // Yo'nalish — xodimdagi belgi almashadi
        using (var javob = await admin.PostAsync(Yol($"/api/catalog/directions/{eskiYonalish.Id}/deactivate"), null, Ct))
        {
            await XatoKutAsync(javob, HttpStatusCode.Conflict, "malumotnoma.faol_xodim_bor");
        }

        using (var javob = await admin.PostAsJsonAsync(Yol($"/api/catalog/directions/{eskiYonalish.Id}/reassign"), new { targetId = yangiYonalish.Id }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.NoContent, await javob.Content.ReadAsStringAsync(Ct));
        }

        // Yuridik shaxs — o'tkazib bo'lmaydi (yangi shartnoma), faqat to'xtatadi
        using (var javob = await admin.GetAsync(Yol($"/api/catalog/legal-entities/{firma.Id}/usage"), Ct))
        {
            var tana = await javob.Content.ReadFromJsonAsync<JsonElement>(Ct);
            tana.GetProperty("activeEmployees").GetInt32().ShouldBe(1);
            tana.GetProperty("canReassign").GetBoolean().ShouldBeFalse();
        }

        using (var javob = await admin.PostAsync(Yol($"/api/catalog/legal-entities/{firma.Id}/deactivate"), null, Ct))
        {
            await XatoKutAsync(javob, HttpStatusCode.Conflict, "malumotnoma.faol_xodim_bor");
        }

        await using var tekshiruv = _factory.Services.CreateAsyncScope();
        var baza = tekshiruv.ServiceProvider.GetRequiredService<AppDbContext>();

        var saqlangan = await baza.Employments.AsNoTracking().Include(item => item.Events).SingleAsync(item => item.Id == ish.Id, Ct);
        saqlangan.PositionId.ShouldBe(yangi.Id);
        saqlangan.Events.ShouldContain(item => item.Type == EmploymentEventType.Promotion && item.FromPositionId == eski.Id);

        var odam = await baza.Employees.AsNoTracking().SingleAsync(item => item.Id == xodim.Id, Ct);
        odam.DirectionIds.ShouldBe([yangiYonalish.Id]);
        (await FaolmiAsync<Direction>(eskiYonalish.Id)).ShouldBeFalse();
        (await FaolmiAsync<LegalEntity>(firma.Id)).ShouldBeTrue();
    }

    [Fact]
    public async Task Otkazish_huquqisiz_otkazib_bolmaydi_xodim_huquqisiz_ism_korinmaydi()
    {
        var belgi = Belgi();
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var firma = LegalEntity.Create("Huquq MCHJ " + belgi).Value;
        var bolim = Department.Create("Huquqli " + belgi).Value;
        var boshqa = Department.Create("Boshqa " + belgi).Value;
        var xodim = Employee.Create(FullName.Create("Huquqov" + belgi, "Hasan", null).Value).Value;
        kontekst.AddRange(firma, bolim, boshqa, xodim, Employment.Hire(xodim.Id, firma.Id, new DateOnly(2025, 1, 1), bolim.Id).Value);
        await kontekst.SaveChangesAsync(Ct);

        using var katalogchi = _factory.CreateClient();
        katalogchi.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await TestAccounts.TokenAsync(_factory, "catalog.manage", "catalog.view"));

        using (var javob = await katalogchi.GetAsync(Yol($"/api/catalog/departments/{bolim.Id}/usage"), Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK);
            var tana = await javob.Content.ReadFromJsonAsync<JsonElement>(Ct);
            tana.GetProperty("activeEmployees").GetInt32().ShouldBe(1);
            tana.GetProperty("employeesHidden").GetBoolean().ShouldBeTrue();
            tana.GetProperty("employees").GetArrayLength().ShouldBe(0);
        }

        using (var javob = await katalogchi.PostAsJsonAsync(Yol($"/api/catalog/departments/{bolim.Id}/reassign"), new { targetId = boshqa.Id }, Ct))
        {
            await XatoKutAsync(javob, HttpStatusCode.Forbidden, "auth.ruxsat_yoq");
        }

        (await FaolmiAsync<Department>(bolim.Id)).ShouldBeTrue();
    }

    private async Task<bool> FaolmiAsync<TEntity>(Guid id)
        where TEntity : Bonvi.Domain.Common.ReferenceEntity
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
        return await kontekst.Set<TEntity>().AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => item.IsActive)
            .SingleAsync(Ct);
    }

    private static string Belgi() => "RD" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private static async Task XatoKutAsync(HttpResponseMessage javob, HttpStatusCode holat, string kod)
    {
        var matn = await javob.Content.ReadAsStringAsync(Ct);
        javob.StatusCode.ShouldBe(holat, matn);
        JsonDocument.Parse(matn).RootElement.GetProperty("title").GetString().ShouldBe(kod, matn);
    }

    private static Uri Yol(string yol) => new(yol, UriKind.Relative);
}
