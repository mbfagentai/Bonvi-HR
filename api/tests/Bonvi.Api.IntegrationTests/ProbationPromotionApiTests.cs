using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Sinov muddati javobi (test uchun).</summary>
public sealed record ProbationResponse(
    Guid Id,
    Guid EmploymentId,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    DateOnly? ResolvedAt,
    string? Conclusion);

/// <summary>«Sinov yozuvisiz qolgan» qator (test uchun).</summary>
public sealed record MissingProbationResponse(
    Guid EmploymentId,
    Guid EmployeeId,
    string EmployeeName,
    string? PhotoKey,
    string? LegalEntity,
    string? Department,
    string? Position,
    DateOnly HireDate,
    int WorkedDays);

/// <summary>Ommaviy tasdiqlash natijasi (test uchun).</summary>
public sealed record ConfirmMissingResponse(int Confirmed, int Skipped);

/// <summary>
/// Sinovdan doimiy xodimga o'tkazish va «yo'qolgan» yozuvlarni tiklash —
/// uchi-uchiga.
/// </summary>
/// <remarks>
/// <para>
/// Bu yerda tekshiriladigan narsa mantiq emas (uni <c>ProbationPromotionTests</c>
/// mock bilan qoplaydi), balki ULANISH: so'rov tanasidagi maydonlar
/// handler'gacha yetib boradimi, ruxsatlar to'g'ri qo'yilganmi va javob
/// shakli kutilgandekmi. Aynan shu qatlamda «maydon qo'shildi, lekin
/// endpoint'ga ulanmadi» degan xato tug'iladi va u hech qanday
/// kompilyatsiya xatosi bermaydi.
/// </para>
/// <para>
/// Testlar bitta bazani baham ko'radi, shu sababli har biri o'z xodimini
/// yaratadi va faqat o'zi yaratganini tekshiradi.
/// </para>
/// </remarks>
public sealed class ProbationPromotionApiTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public ProbationPromotionApiTests(BonviApiFactory factory) => _factory = factory;

    // ────────────────────────────────────────────────────────────
    //  Sinovdan o'tkazish — bir amalda
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Bitta so'rov: sinov yopiladi, holat doimiy bo'ladi, yangi lavozim
    /// va oklad qo'yiladi.
    /// </summary>
    [Fact]
    public async Task Bir_sorov_bilan_xodim_doimiy_bolib_yangi_lavozim_va_oklad_oladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var muhit = await MuhitAsync();

        using var javob = await mijoz.PostAsJsonAsync(
            new Uri($"/api/probations/{muhit.ProbationId}/resolve", UriKind.Relative),
            new
            {
                status = "Passed",
                conclusion = "Sinovdan muvaffaqiyatli o'tdi",
                positionId = muhit.YangiLavozimId,
                departmentId = muhit.YangiBolimId,
                salaryAmount = 7_500_000m,
                rate = 1.0m,
                orderNo = "199-b",
            },
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var sinov = await javob.Content.ReadFromJsonAsync<ProbationResponse>(bekorQilish);
        sinov!.Status.ShouldBe("Passed");

        // Eng muhimi — munosabatning O'ZI o'zgardimi
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var munosabat = await kontekst.Employments
            .AsNoTracking()
            .SingleAsync(m => m.Id == muhit.EmploymentId, bekorQilish);

        munosabat.Status.ShouldBe(EmploymentStatus.Active);
        munosabat.PositionId.ShouldBe(muhit.YangiLavozimId);
        munosabat.DepartmentId.ShouldBe(muhit.YangiBolimId);
        munosabat.Salary!.Amount.ShouldBe(7_500_000m);
    }

    /// <summary>
    /// Sinovdan o'tmagan xodimga oklad tayinlash 400 bilan to'xtaydi.
    /// </summary>
    [Fact]
    public async Task Otmagan_xodimga_oklad_tayinlash_400_qaytaradi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var muhit = await MuhitAsync();

        using var javob = await mijoz.PostAsJsonAsync(
            new Uri($"/api/probations/{muhit.ProbationId}/resolve", UriKind.Relative),
            new { status = "Failed", salaryAmount = 9_000_000m },
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(javob));
        (await MatnAsync(javob)).ShouldContain("sinov.tayinlash_otmagan");
    }

    // ────────────────────────────────────────────────────────────
    //  «Sinov yozuvisiz qolganlar»
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Sinov yozuvi bo'lmagan «sinovdagi» munosabat ro'yxatda chiqadi va
    /// tasdiqlangach doimiy bo'ladi.
    /// </summary>
    /// <remarks>
    /// Bu holat Excel'dan ko'chirishda tug'iladi: sinov ma'lumoti «91.5»
    /// varag'ida yo'q bo'lsa, odam holatsiz qolib, hech qaysi ekranda
    /// ko'rinmaydi.
    /// </remarks>
    [Fact]
    public async Task Sinov_yozuvisiz_qolgan_munosabat_royxatda_chiqadi_va_tasdiqlanadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        // Sinov yozuvi ATAYLAB yaratilmaydi — ko'chirishdagi holat aynan shu
        var employmentId = await YetimMunosabatAsync();

        var royxat = await mijoz.GetFromJsonAsync<IReadOnlyList<MissingProbationResponse>>(
            new Uri("/api/probations/missing", UriKind.Relative), bekorQilish);

        var qator = royxat!.SingleOrDefault(q => q.EmploymentId == employmentId);
        qator.ShouldNotBeNull("yetim munosabat ro'yxatda bo'lishi kerak edi");
        qator.WorkedDays.ShouldBeGreaterThan(0);

        using var javob = await mijoz.PostAsJsonAsync(
            new Uri("/api/probations/missing/confirm", UriKind.Relative),
            new { employmentIds = new[] { employmentId } },
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var natija = await javob.Content.ReadFromJsonAsync<ConfirmMissingResponse>(bekorQilish);
        natija!.Confirmed.ShouldBe(1);
        natija.Skipped.ShouldBe(0);

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var munosabat = await kontekst.Employments
            .AsNoTracking()
            .SingleAsync(m => m.Id == employmentId, bekorQilish);

        munosabat.Status.ShouldBe(EmploymentStatus.Active);

        // Tarixda iz qoldi — holat sababsiz sakramadi
        var hodisalar = await kontekst.EmploymentEvents
            .AsNoTracking()
            .Where(h => h.EmploymentId == employmentId)
            .Select(h => h.Type)
            .ToListAsync(bekorQilish);

        hodisalar.ShouldContain(EmploymentEventType.ProbationResult);

        // Ikkinchi marta ro'yxatda ko'rinmaydi — ish tugadi
        var qayta = await mijoz.GetFromJsonAsync<IReadOnlyList<MissingProbationResponse>>(
            new Uri("/api/probations/missing", UriKind.Relative), bekorQilish);

        qayta!.ShouldNotContain(q => q.EmploymentId == employmentId);
    }

    /// <summary>
    /// Ro'yxatda turmagan identifikator jimgina o'tkazib yuboriladi, holati
    /// o'zgarmaydi.
    /// </summary>
    /// <remarks>
    /// Bu — himoya chegarasi. Tasdiqlash mijozdan kelgan identifikatorni
    /// tekshirmasa, u istalgan munosabatni «doimiy» qilib qo'yadigan
    /// yo'lga aylanardi.
    /// </remarks>
    [Fact]
    public async Task Begona_identifikator_holatni_ozgartirmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        // Bu munosabatning sinov YOZUVI bor, ya'ni «missing» ro'yxatida emas
        var muhit = await MuhitAsync();

        using var javob = await mijoz.PostAsJsonAsync(
            new Uri("/api/probations/missing/confirm", UriKind.Relative),
            new { employmentIds = new[] { muhit.EmploymentId } },
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var natija = await javob.Content.ReadFromJsonAsync<ConfirmMissingResponse>(bekorQilish);
        natija!.Confirmed.ShouldBe(0);
        natija.Skipped.ShouldBe(1);

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var munosabat = await kontekst.Employments
            .AsNoTracking()
            .SingleAsync(m => m.Id == muhit.EmploymentId, bekorQilish);

        munosabat.Status.ShouldBe(EmploymentStatus.Probation);
    }

    /// <summary>Bo'sh ro'yxat bilan tasdiqlash 400 qaytaradi.</summary>
    [Fact]
    public async Task Bosh_royxat_bilan_tasdiqlash_400_qaytaradi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        using var javob = await mijoz.PostAsJsonAsync(
            new Uri("/api/probations/missing/confirm", UriKind.Relative),
            new { employmentIds = Array.Empty<Guid>() },
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(javob));
    }

    // ────────────────────────────────────────────────────────────
    //  Muhit tayyorlash
    // ────────────────────────────────────────────────────────────

    private sealed record Muhit(
        Guid EmploymentId,
        Guid ProbationId,
        Guid YangiLavozimId,
        Guid YangiBolimId);

    /// <summary>Sinovda turgan xodim + unga bog'langan sinov muddati.</summary>
    private async Task<Muhit> MuhitAsync()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var belgi = Guid.NewGuid().ToString("N")[..6];

        var firma = LegalEntity.Create($"Sinov MChJ {belgi}").Value;
        var bolim = Department.Create($"Sinov bo'limi {belgi}").Value;
        var lavozim = Position.Create($"Mutaxassis {belgi}").Value;

        kontekst.LegalEntities.Add(firma);
        kontekst.Departments.Add(bolim);
        kontekst.Positions.Add(lavozim);

        var xodim = Employee.Create(FullName.Create("Сттест", $"Ном{belgi}", null).Value).Value;
        kontekst.Employees.Add(xodim);

        var boshlangan = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-60);

        var munosabat = Employment.Hire(xodim.Id, firma.Id, boshlangan).Value;
        _ = munosabat.SetSalary(Money.Create(4_000_000m).Value, boshlangan);
        kontekst.Employments.Add(munosabat);

        var sinov = Probation.Start(munosabat.Id, boshlangan, 90).Value;
        kontekst.Probations.Add(sinov);

        await kontekst.SaveChangesAsync(bekorQilish);

        return new Muhit(munosabat.Id, sinov.Id, lavozim.Id, bolim.Id);
    }

    /// <summary>
    /// «Sinovda», lekin sinov YOZUVISIZ munosabat — ko'chirishdagi holat.
    /// </summary>
    private async Task<Guid> YetimMunosabatAsync()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var belgi = Guid.NewGuid().ToString("N")[..6];

        var firma = LegalEntity.Create($"Yetim MChJ {belgi}").Value;
        kontekst.LegalEntities.Add(firma);

        var xodim = Employee.Create(FullName.Create("Ййетим", $"Ном{belgi}", null).Value).Value;
        kontekst.Employees.Add(xodim);

        var munosabat = Employment.Hire(
            xodim.Id,
            firma.Id,
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2000)).Value;

        kontekst.Employments.Add(munosabat);

        await kontekst.SaveChangesAsync(bekorQilish);

        return munosabat.Id;
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

    /// <summary>Xato javobini o'qish — tekshiruv yiqilganda sababi ko'rinishi uchun.</summary>
    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
