using System.Security.Cryptography;
using Bonvi.Application.Attendance.Recompute;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Iflos navbat KO'RMAYDIGAN o'zgarishlar: smenaning boshlanishi, orqaga sanali mehnat o'zgarishlari
/// va chorakdan uzun qayta hisoblash (audit topilmalari #1, #3, #4, #7).
/// </summary>
/// <remarks>
/// <para>
/// Vazifalar HTTP'siz, to'g'ridan-to'g'ri chaqiriladi — <c>Testing</c> da Hangfire yo'q (SPEC §9.1).
/// </para>
/// <para>
/// ⚠️ Alohida sinf — alohida baza: mehnat o'zgarishlari vazifasining birinchi yurishi oxirgi chorakda
/// o'zgargan HAMMA xodimni qayta hisoblaydi va boshqa sinfning «bu sana tegilmagan» degan
/// tekshiruvlarini buzardi. Bu yerda ham har bir test faqat o'z xodimini tekshiradi.
/// </para>
/// </remarks>
public sealed class AttendanceCatchUpJobsTests : IClassFixture<BonviApiFactory>
{
    private static readonly TimeSpan Tashkent = TimeSpan.FromHours(5);

    private readonly BonviApiFactory _factory;

    public AttendanceCatchUpJobsTests(BonviApiFactory factory) => _factory = factory;

    // ════════════════════════════════════════════════════════════
    //  Chorakdan uzun qayta hisoblash (#3)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Yarim yillik qayta hisoblash oynalarga bo'linadi (ikki chorak oynasi) va natija to'liq: har kun
    /// qatori bor, oyna chegarasidan narigi o'tishli kun ham to'g'ri. Saqlangan qatorlar kuzatuvda
    /// to'planib qolmaydi.
    /// </summary>
    [Fact]
    public async Task Chorakdan_uzun_qayta_hisoblash_oynalarga_bolinadi_va_kuzatuvda_toplanmaydi()
    {
        var ct = TestContext.Current.CancellationToken;
        var (xodim, _) = await XodimAsync(ct);
        var qurilma = await QurilmaAsync(ct);
        var boshi = new DateOnly(2026, 1, 5);
        var oxiri = new DateOnly(2026, 6, 30);
        var otishliKun = new DateOnly(2026, 6, 15);

        await SaqlaAsync(
            ct,
            Otish(qurilma, xodim, otishliKun, 9, 0),
            Otish(qurilma, xodim, otishliKun, 18, 0));

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var soat = new QotirilganSoat(new DateTimeOffset(2026, 7, 1, 7, 0, 0, TimeSpan.Zero));
        var yozuvchi = ActivatorUtilities.CreateInstance<AttendanceDayRecomputer>(qamrov.ServiceProvider, soat);

        var natija = await yozuvchi.RecomputeAsync([xodim], AttendanceDayRecomputer.DaysBetween(boshi, oxiri), ct);

        natija.StoredDays.ShouldBe(oxiri.DayNumber - boshi.DayNumber + 1);

        var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ChangeTracker.Entries<AttendanceDay>().Count().ShouldBe(0, "saqlangan to'plam kuzatuvdan unutiladi");

        var kun = await db.AttendanceDays.AsNoTracking().SingleAsync(day => day.EmployeeId == xodim && day.LocalDate == otishliKun, ct);
        kun.Status.ShouldBe(AttendanceDayStatus.Present);
        kun.WorkedMinutes.ShouldBe(540);
    }

    // ════════════════════════════════════════════════════════════
    //  Yordamchilar
    // ════════════════════════════════════════════════════════════

    private sealed class QotirilganSoat(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;

        public DateOnly Today => utcNow.TashkentDate();
    }

    private static WorkSchedule Jadval(string nom, int boshlanish, int tugash)
        => WorkSchedule.Create(
            nom,
            [.. Enumerable.Range(1, 7).Select(kun => new WorkScheduleDayInput(kun, true, new TimeOnly(boshlanish, 0), new TimeOnly(tugash, 0)))]).Value;

    /// <summary>Munosabati (2025-yildan) va har kuni 09:00–18:00 shaxsiy jadvali bor xodim.</summary>
    private async Task<(Guid Xodim, Guid Munosabat)> XodimAsync(CancellationToken ct)
    {
        var belgi = Guid.NewGuid().ToString("N")[..8];

        var firma = LegalEntity.Create("Kuzatuv МЧЖ " + belgi).Value;
        var xodim = Employee.Create(FullName.Create("Кузатув" + belgi, "Ходим", null).Value).Value;
        var munosabat = Employment.Hire(xodim.Id, firma.Id, new DateOnly(2025, 1, 1)).Value;
        var jadval = Jadval("Kuzatuv jadval " + belgi, 9, 18);
        var biriktirma = ScheduleAssignment.Assign(jadval.Id, ScheduleScope.Employee, xodim.Id, new DateOnly(2025, 1, 1)).Value;

        await SaqlaAsync(ct, firma, xodim, munosabat, jadval, biriktirma);

        return (xodim.Id, munosabat.Id);
    }

    private async Task<Guid> QurilmaAsync(CancellationToken ct)
    {
        var qurilma = AttendanceDevice.Register(
            "Kuzatuv turniketi " + Guid.NewGuid().ToString("N")[..8],
            RandomNumberGenerator.GetBytes(32),
            "tst00000",
            DateTimeOffset.UtcNow).Value;

        await SaqlaAsync(ct, qurilma);

        return qurilma.Id;
    }

    /// <summary>Bog'langan va «hisobga olingan» o'tish, yo'nalishi noma'lum (agent shunday yuboradi).</summary>
    private static AttendancePass Otish(Guid qurilma, Guid xodim, DateOnly sana, int soat, int daqiqa)
    {
        var lahza = new DateTimeOffset(sana.ToDateTime(new TimeOnly(soat, daqiqa)), Tashkent);
        var qabul = lahza.ToUniversalTime().AddSeconds(3);

        var otish = AttendancePass.Record(
            qurilma,
            AttendanceIdentityProvider.Verifix,
            "p-" + Guid.NewGuid().ToString("N")[..10],
            lahza,
            qabul,
            Guid.NewGuid().ToString("N")).Value;

        otish.Resolve(xodim, qabul);
        otish.MarkProcessed(qabul.AddMinutes(1));

        return otish;
    }

    private async Task SaqlaAsync(CancellationToken ct, params object[] yozuvlar)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        db.AddRange(yozuvlar);
        await db.SaveChangesAsync(ct);
    }
}
