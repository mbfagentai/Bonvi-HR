using Bonvi.Application.Employees.Probations;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Muddati tugagan sinov — avtomatik «o'tdi» (<see cref="ProbationAutoPassJob"/>,
/// egasi 2026-10-22: «sinov muddat tugasa avtomatik active xodimga aylansin»).
/// </summary>
/// <remarks>
/// Har bir test o'z sinovlarini yaratadi; bazada boshqa testlarning muddati o'tgan
/// sinovlari ham bo'lishi mumkin — shu sababli natija SONI emas, aynan shu testning
/// yozuvlari tekshiriladi.
/// </remarks>
public sealed class ProbationAutoPassJobTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public ProbationAutoPassJobTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>API ning «bugun»i — Toshkent sanasi (<c>ProbationOutcomeApiTests</c> izohi).</summary>
    private DateOnly Bugun => _factory.Services.GetRequiredService<IClock>().Today;

    /// <summary>
    /// Asosiy holat: oxirgi kuni kecha bo'lgan sinov «o'tdi», xodim «faol», natija sanasi —
    /// tugash kuni, mehnat tarixida «sinov natijasi» hodisasi, audit — «tizim, shu vazifa».
    /// </summary>
    [Fact]
    public async Task Muddati_tugagan_sinov_otdi_va_xodim_faol_boladi()
    {
        var tugadi = Bugun.AddDays(-1);
        var muhit = await SinovdagiAsync(tugadi);

        await VazifaAsync();

        var (munosabat, sinov) = await HolatAsync(muhit);

        sinov.Status.ShouldBe(ProbationStatus.Passed);
        sinov.ResolvedAt.ShouldBe(tugadi, "natija sanasi — vazifa ishlagan kun emas, sinovning oxirgi kuni");
        sinov.Conclusion.ShouldBe(ProbationAutoPassJob.Conclusion);
        munosabat.Status.ShouldBe(EmploymentStatus.Active, "xodim doimiy xodimga o'tishi kerak");

        var hodisa = await BazadaAsync(db => db.EmploymentEvents.AsNoTracking()
            .Where(row => row.EmploymentId == muhit.EmploymentId && row.Type == EmploymentEventType.ProbationResult)
            .SingleAsync(Ct));
        hodisa.EffectiveDate.ShouldBe(tugadi);

        // Tayyorlovdagi «yaratildi» qatori ham bor — vazifaniki «o'zgartirildi»
        var qator = await BazadaAsync(db => db.AuditLogs.AsNoTracking()
            .Where(row => row.EntityId == muhit.ProbationId.ToString() && row.Action == AuditAction.Update)
            .SingleAsync(Ct));
        qator.ActorKind.ShouldBe(AuditActorKind.System);
        qator.Operation.ShouldBe(ProbationAutoPassJob.AuditOperation);
    }

    /// <summary>Oxirgi kuni BUGUN — sinov hali tugamagan, tegilmaydi.</summary>
    [Fact]
    public async Task Bugun_tugaydigan_sinovga_tegilmaydi()
    {
        var muhit = await SinovdagiAsync(Bugun);

        await VazifaAsync();

        var (munosabat, sinov) = await HolatAsync(muhit);

        sinov.Status.ShouldBe(ProbationStatus.InProgress);
        munosabat.Status.ShouldBe(EmploymentStatus.Probation);
    }

    /// <summary>
    /// Bo'shatilgan, lekin sinovi ochiq qolib ketgan xodim — sinov yopiladi, munosabat
    /// «faol» ga QAYTMAYDI (ketgan odam tirilmasin).
    /// </summary>
    [Fact]
    public async Task Boshatilgan_xodimning_ochiq_sinovi_yopiladi_lekin_u_qaytmaydi()
    {
        var muhit = await SinovdagiAsync(Bugun.AddDays(-5), boshatilgan: true);

        await VazifaAsync();

        var (munosabat, sinov) = await HolatAsync(muhit);

        sinov.Status.ShouldBe(ProbationStatus.Passed);
        munosabat.Status.ShouldBe(EmploymentStatus.Terminated);
    }

    /// <summary>
    /// Ikkinchi yurish hech narsa o'zgartirmaydi; HR allaqachon yakunlagan sinov
    /// («o'tmadi») natijasi ustidan yozilmaydi.
    /// </summary>
    [Fact]
    public async Task Takroriy_yurish_va_HR_natijasi_ozgarmaydi()
    {
        var muhit = await SinovdagiAsync(Bugun.AddDays(-2));
        var hrQarori = await SinovdagiAsync(Bugun.AddDays(-2));

        await BazadaAsync(async db =>
        {
            var sinov = await db.Probations.SingleAsync(row => row.Id == hrQarori.ProbationId, Ct);
            sinov.Resolve(ProbationStatus.Failed, Bugun.AddDays(-3), "HR qarori").IsSuccess.ShouldBeTrue();
            return await db.SaveChangesAsync(Ct);
        });

        await VazifaAsync();
        var birinchi = await HolatAsync(muhit);

        (await VazifaAsync()).ShouldBe(0, "o'tkazilganlar ikkinchi marta olinmaydi");

        var ikkinchi = await HolatAsync(muhit);
        ikkinchi.Sinov.ResolvedAt.ShouldBe(birinchi.Sinov.ResolvedAt);

        var (_, hrSinov) = await HolatAsync(hrQarori);
        hrSinov.Status.ShouldBe(ProbationStatus.Failed, "HR natijasi avtomatik «o'tdi» bilan almashmasligi kerak");
        hrSinov.Conclusion.ShouldBe("HR qarori");
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    private sealed record Muhit(Guid EmploymentId, Guid ProbationId);

    private async Task<int> VazifaAsync()
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        return await qamrov.ServiceProvider.GetRequiredService<ProbationAutoPassJob>().RunAsync(Ct);
    }

    /// <summary>Sinovdagi xodim: sinov oxirgi kuni <paramref name="tugash"/>.</summary>
    private async Task<Muhit> SinovdagiAsync(DateOnly tugash, bool boshatilgan = false)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var belgi = Guid.NewGuid().ToString("N")[..6];

        var firma = LegalEntity.Create($"Avto MChJ {belgi}").Value;
        kontekst.LegalEntities.Add(firma);

        var xodim = Employee.Create(FullName.Create($"Автосинов{belgi}", "Тест", null).Value).Value;
        kontekst.Employees.Add(xodim);

        var boshlangan = tugash.AddDays(-60);

        var munosabat = Employment.Hire(xodim.Id, firma.Id, boshlangan).Value;
        kontekst.Employments.Add(munosabat);

        if (boshatilgan)
        {
            munosabat.Terminate(tugash.AddDays(-10), reasonId: null).IsSuccess.ShouldBeTrue();
        }

        var sinov = Probation.Import(munosabat.Id, boshlangan, tugash).Value;
        kontekst.Probations.Add(sinov);

        await kontekst.SaveChangesAsync(Ct);

        return new Muhit(munosabat.Id, sinov.Id);
    }

    private async Task<(Employment Munosabat, Probation Sinov)> HolatAsync(Muhit muhit)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var munosabat = await kontekst.Employments.AsNoTracking().SingleAsync(row => row.Id == muhit.EmploymentId, Ct);
        var sinov = await kontekst.Probations.AsNoTracking().SingleAsync(row => row.Id == muhit.ProbationId, Ct);

        return (munosabat, sinov);
    }

    private async Task<T> BazadaAsync<T>(Func<AppDbContext, Task<T>> ish)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        return await ish(qamrov.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
