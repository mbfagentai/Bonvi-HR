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

/// <summary>Qayta aloqa (exit interview) javobi (test uchun).</summary>
public sealed record ExitInterviewResponse(
    Guid Id,
    Guid EmploymentId,
    Guid? EmployeeId,
    string? EmployeeName,
    string? PhotoKey,
    Guid? LegalEntityId,
    string? LegalEntityName,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? PositionId,
    string? PositionName,
    DateOnly? TerminationDate,
    string? TerminationReasonName,
    DateOnly InterviewDate,
    string Reason,
    string? WouldRecommend,
    string? Impressions,
    string? ConditionsToReturn);

/// <summary>
/// Qayta aloqa (exit interview, M10) — uchi-uchiga: yozish, ro'yxat, tuzatish, o'chirish.
/// </summary>
/// <remarks>
/// Mantiq (sababsiz yozilmasligi, «faqat yakunlangan munosabat», «bittadan
/// ortiq bo'lmaydi») <c>ExitInterviewTests</c> va <c>ExitInterviewHandlerTests</c>
/// da qoplangan — bu yerda faqat ULANISH tekshiriladi.
/// </remarks>
public sealed class ExitInterviewApiTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public ExitInterviewApiTests(BonviApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Yakunlangan_munosabatga_yoziladi_va_xodim_nomi_bilan_chiqadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var muhit = await TugatilganMunosabatAsync();

        using var yozish = await mijoz.PostAsJsonAsync(
            new Uri("/api/exit-interviews", UriKind.Relative),
            new
            {
                employmentId = muhit.EmploymentId,
                interviewDate = new DateOnly(2026, 3, 1),
                reason = "Rahbariyat bilan ziddiyat",
                wouldRecommend = "Yes",
                impressions = "Ajoyib jamoa",
            },
            bekorQilish);

        yozish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yozish));

        var natija = await yozish.Content.ReadFromJsonAsync<ExitInterviewResponse>(bekorQilish);
        natija!.EmploymentId.ShouldBe(muhit.EmploymentId);
        natija.EmployeeName.ShouldBe(muhit.EmployeeName);
        natija.Reason.ShouldBe("Rahbariyat bilan ziddiyat");
        natija.WouldRecommend.ShouldBe("Yes");

        using var royxat = await mijoz.GetAsync(new Uri("/api/exit-interviews", UriKind.Relative), bekorQilish);
        var qatorlar = await royxat.Content.ReadFromJsonAsync<IReadOnlyList<ExitInterviewResponse>>(bekorQilish);
        qatorlar!.ShouldContain(q => q.Id == natija.Id);
    }

    [Fact]
    public async Task Tugamagan_munosabatga_yozish_409_qaytaradi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var employmentId = await FaolMunosabatAsync();

        using var javob = await mijoz.PostAsJsonAsync(
            new Uri("/api/exit-interviews", UriKind.Relative),
            new { employmentId, interviewDate = new DateOnly(2026, 3, 1), reason = "Sabab" },
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.Conflict, await MatnAsync(javob));
        (await MatnAsync(javob)).ShouldContain("qayta_aloqa.mehnat_tugamagan");
    }

    [Fact]
    public async Task Takroriy_yozuv_409_qaytaradi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var muhit = await TugatilganMunosabatAsync();

        using var birinchi = await mijoz.PostAsJsonAsync(
            new Uri("/api/exit-interviews", UriKind.Relative),
            new { employmentId = muhit.EmploymentId, interviewDate = new DateOnly(2026, 3, 1), reason = "Sabab" },
            bekorQilish);

        birinchi.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(birinchi));

        using var ikkinchi = await mijoz.PostAsJsonAsync(
            new Uri("/api/exit-interviews", UriKind.Relative),
            new { employmentId = muhit.EmploymentId, interviewDate = new DateOnly(2026, 3, 2), reason = "Boshqa sabab" },
            bekorQilish);

        ikkinchi.StatusCode.ShouldBe(HttpStatusCode.Conflict, await MatnAsync(ikkinchi));
        (await MatnAsync(ikkinchi)).ShouldContain("qayta_aloqa.allaqachon_bor");
    }

    [Fact]
    public async Task Tuzatish_va_ochirish_ishlaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var muhit = await TugatilganMunosabatAsync();

        using var yozish = await mijoz.PostAsJsonAsync(
            new Uri("/api/exit-interviews", UriKind.Relative),
            new { employmentId = muhit.EmploymentId, interviewDate = new DateOnly(2026, 3, 1), reason = "Eski sabab" },
            bekorQilish);

        var yaratilgan = await yozish.Content.ReadFromJsonAsync<ExitInterviewResponse>(bekorQilish);

        using var tuzatish = await mijoz.PutAsJsonAsync(
            new Uri($"/api/exit-interviews/{yaratilgan!.Id}", UriKind.Relative),
            new { interviewDate = new DateOnly(2026, 3, 5), reason = "Yangi sabab", wouldRecommend = "No" },
            bekorQilish);

        tuzatish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(tuzatish));
        var tuzatilgan = await tuzatish.Content.ReadFromJsonAsync<ExitInterviewResponse>(bekorQilish);
        tuzatilgan!.Reason.ShouldBe("Yangi sabab");
        tuzatilgan.WouldRecommend.ShouldBe("No");
        tuzatilgan.EmploymentId.ShouldBe(muhit.EmploymentId, "mehnat munosabati tuzatishda o'zgarmaydi");

        using var ochirish = await mijoz.DeleteAsync(
            new Uri($"/api/exit-interviews/{yaratilgan.Id}", UriKind.Relative), bekorQilish);

        ochirish.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(ochirish));

        using var royxat = await mijoz.GetAsync(new Uri("/api/exit-interviews", UriKind.Relative), bekorQilish);
        var qatorlar = await royxat.Content.ReadFromJsonAsync<IReadOnlyList<ExitInterviewResponse>>(bekorQilish);
        qatorlar!.ShouldNotContain(
            q => q.Id == yaratilgan.Id, "yumshoq o'chirilgan yozuv ro'yxatda chiqmasligi kerak");

        // Yumshoq o'chirilgandan keyin XUDDI SHU munosabatga yangi yozuv
        // yozib bo'lmasligi kerak edi — filtrsiz unikal indeks aynan shu
        // holatni bloklardi.
        using var qaytaYozish = await mijoz.PostAsJsonAsync(
            new Uri("/api/exit-interviews", UriKind.Relative),
            new { employmentId = muhit.EmploymentId, interviewDate = new DateOnly(2026, 3, 10), reason = "Uchinchi sabab" },
            bekorQilish);

        qaytaYozish.StatusCode.ShouldBe(
            HttpStatusCode.OK, "o'chirilgan yozuv o'rniga yangisini yozish mumkin bo'lishi kerak: " + await MatnAsync(qaytaYozish));
    }

    [Fact]
    public async Task Yozuv_yoq_bolganda_by_employment_404_qaytaradi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var muhit = await TugatilganMunosabatAsync();

        using var javob = await mijoz.GetAsync(
            new Uri($"/api/exit-interviews/by-employment/{muhit.EmploymentId}", UriKind.Relative), bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.NotFound, await MatnAsync(javob));
        (await MatnAsync(javob)).ShouldContain("qayta_aloqa.topilmadi");
    }

    // ────────────────────────────────────────────────────────────
    //  Muhit tayyorlash
    // ────────────────────────────────────────────────────────────

    private sealed record Muhit(Guid EmploymentId, string EmployeeName);

    /// <summary>Ishdan bo'shatilgan (Terminated) mehnat munosabati.</summary>
    private async Task<Muhit> TugatilganMunosabatAsync()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var belgi = Guid.NewGuid().ToString("N")[..6];

        var firma = LegalEntity.Create($"Qayta aloqa MChJ {belgi}").Value;
        kontekst.LegalEntities.Add(firma);

        var xodim = Employee.Create(FullName.Create("Кетувчи", $"Ном{belgi}", null).Value).Value;
        kontekst.Employees.Add(xodim);

        var boshlangan = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-200);
        var munosabat = Employment.Hire(xodim.Id, firma.Id, boshlangan).Value;
        _ = munosabat.Terminate(DateOnly.FromDateTime(DateTime.UtcNow), null);
        kontekst.Employments.Add(munosabat);

        await kontekst.SaveChangesAsync(bekorQilish);

        return new Muhit(munosabat.Id, xodim.FullName.Full);
    }

    /// <summary>Hali ishlab turgan (Probation) mehnat munosabati.</summary>
    private async Task<Guid> FaolMunosabatAsync()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var belgi = Guid.NewGuid().ToString("N")[..6];

        var firma = LegalEntity.Create($"Faol MChJ {belgi}").Value;
        kontekst.LegalEntities.Add(firma);

        var xodim = Employee.Create(FullName.Create("Ишловчи", $"Ном{belgi}", null).Value).Value;
        kontekst.Employees.Add(xodim);

        var munosabat = Employment
            .Hire(xodim.Id, firma.Id, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-10))
            .Value;

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

    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
