using System.Globalization;
using System.Security.Cryptography;
using Bonvi.Application.Attendance.Recompute;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.Settings;
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
    //  Boshlangan smenalar (#4)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Smena boshlanmaguncha qator yo'q; 09:00 dan keyingi birinchi yurishda «Kelmadi» qatori paydo
    /// bo'ladi. Bor qatorga (kechagi) va o'zi yozgan qatorga keyingi yurishlar tegmaydi.
    /// </summary>
    [Fact]
    public async Task Boshlangan_smenalar_vazifasi_reja_boshlangach_kelmadi_qatorini_yozadi()
    {
        var ct = TestContext.Current.CancellationToken;
        var sana = new DateOnly(2026, 5, 6);
        var (xodim, _) = await XodimAsync(ct);

        var kechagiLahza = new DateTimeOffset(2026, 5, 5, 16, 0, 0, TimeSpan.Zero);
        await SaqlaAsync(ct, AttendanceDay.Compute(xodim, sana.AddDays(-1), Faktlar(AttendanceDayStatus.Present, 540), kechagiLahza));

        await BoshlanganSmenalarAsync(Toshkentda(sana, 8, 0), ct);

        (await KunAsync(xodim, sana, ct)).ShouldBeNull("08:00 — 09:00 lik smena hali boshlanmagan");

        await BoshlanganSmenalarAsync(Toshkentda(sana, 9, 5), ct);

        var kun = (await KunAsync(xodim, sana, ct)).ShouldNotBeNull();
        kun.Status.ShouldBe(AttendanceDayStatus.Absent);
        var yozilgan = kun.ComputedAt;

        await BoshlanganSmenalarAsync(Toshkentda(sana, 9, 20), ct);

        (await KunAsync(xodim, sana, ct)).ShouldNotBeNull().ComputedAt.ShouldBe(yozilgan, "bor qator qayta yozilmaydi");

        var kecha = (await KunAsync(xodim, sana.AddDays(-1), ct)).ShouldNotBeNull();
        kecha.Status.ShouldBe(AttendanceDayStatus.Present, "bor qatorga tegilmaydi");
        kecha.ComputedAt.ShouldBe(kechagiLahza);
    }

    // ════════════════════════════════════════════════════════════
    //  Orqaga sanali mehnat o'zgarishlari (#7, #1)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Auditning misoli: 15-da 10-sanali ishdan bo'shatish kiritilgan — 11–13 «Kelmadi» bo'lib qolardi
    /// (tungi hisoblash faqat uch kunni qamraydi). Vazifa o'zgarishni topadi va xodimni bo'shatish
    /// sanasidan bugungacha qayta hisoblaydi; lahzani yashirin sozlamaga yozadi.
    /// </summary>
    [Fact]
    public async Task Mehnat_ozgarishi_vazifasi_orqaga_sanali_ishdan_boshatishni_tabelga_yetkazadi()
    {
        var ct = TestContext.Current.CancellationToken;
        var bugun = BugunToshkentda();
        var (xodim, munosabat) = await XodimAsync(ct);

        await QaytaHisoblaAsync([xodim], bugun.AddDays(-10), bugun.AddDays(-1), ct);
        (await KunAsync(xodim, bugun.AddDays(-5), ct)).ShouldNotBeNull().Status.ShouldBe(AttendanceDayStatus.Absent, "sinov sharti");

        await MehnatOzgarishlariAsync(ct);

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
            var yozuv = await db.Employments.Include(item => item.Events).SingleAsync(item => item.Id == munosabat, ct);
            yozuv.Terminate(bugun.AddDays(-8), reasonId: null).IsSuccess.ShouldBeTrue();
            await db.SaveChangesAsync(ct);
        }

        await MehnatOzgarishlariAsync(ct);

        (await KunAsync(xodim, bugun.AddDays(-8), ct)).ShouldNotBeNull()
            .Status.ShouldBe(AttendanceDayStatus.Absent, "ishdan ketgan kunning o'zi hali ish kuni");

        for (var sana = bugun.AddDays(-7); sana < bugun; sana = sana.AddDays(1))
        {
            (await KunAsync(xodim, sana, ct)).ShouldBeNull($"{sana:yyyy-MM-dd} — ishlamaydi, «Kelmadi» qolmaydi");
        }

        await using var tekshir = _factory.Services.CreateAsyncScope();
        var belgi = await tekshir.ServiceProvider.GetRequiredService<AppDbContext>().SystemSettings.AsNoTracking()
            .SingleAsync(setting => setting.Key == SystemSettingKeys.AttendanceEmploymentChangesCheckedAt, ct);

        DateTimeOffset.Parse(belgi.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            .ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-5));
    }

    /// <summary>
    /// Orqaga sanali ko'chirish: vazifa qayta hisoblaydi, hisoblash esa ko'chirish SANASIGA qaraydi —
    /// undan oldingi kunlar eski lavozimning jadvali va surati bilan, keyingilari yangisi bilan (#1).
    /// </summary>
    [Fact]
    public async Task Mehnat_ozgarishi_vazifasi_orqaga_sanali_kochirishni_sanasidan_qollaydi()
    {
        var ct = TestContext.Current.CancellationToken;
        var bugun = BugunToshkentda();
        var belgi = Guid.NewGuid().ToString("N")[..8];

        var firma = LegalEntity.Create("Ko'chish МЧЖ " + belgi).Value;
        var eskiLavozim = Position.Create("Omborchi " + belgi).Value;
        var yangiLavozim = Position.Create("Haydovchi " + belgi).Value;
        var xodim = Employee.Create(FullName.Create("Кўчишев", "Ходим", belgi).Value).Value;
        var munosabat = Employment.Hire(xodim.Id, firma.Id, new DateOnly(2025, 1, 1), positionId: eskiLavozim.Id).Value;

        var eskiJadval = Jadval("Eski " + belgi, 8, 17);
        var yangiJadval = Jadval("Yangi " + belgi, 9, 18);

        await SaqlaAsync(
            ct,
            firma, eskiLavozim, yangiLavozim, xodim, munosabat, eskiJadval, yangiJadval,
            ScheduleAssignment.Assign(eskiJadval.Id, ScheduleScope.Position, eskiLavozim.Id, new DateOnly(2025, 1, 1)).Value,
            ScheduleAssignment.Assign(yangiJadval.Id, ScheduleScope.Position, yangiLavozim.Id, new DateOnly(2025, 1, 1)).Value);

        await QaytaHisoblaAsync([xodim.Id], bugun.AddDays(-6), bugun.AddDays(-1), ct);
        await MehnatOzgarishlariAsync(ct);

        var kochish = bugun.AddDays(-3);

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
            var yozuv = await db.Employments.Include(item => item.Events).SingleAsync(item => item.Id == munosabat.Id, ct);
            yozuv.Transfer(kochish, null, yangiLavozim.Id).IsSuccess.ShouldBeTrue();
            await db.SaveChangesAsync(ct);
        }

        await MehnatOzgarishlariAsync(ct);

        await using var tekshir = _factory.Services.CreateAsyncScope();
        var kunlar = await tekshir.ServiceProvider.GetRequiredService<AppDbContext>().AttendanceDays.AsNoTracking()
            .Where(day => day.EmployeeId == xodim.Id && day.LocalDate >= bugun.AddDays(-6) && day.LocalDate < bugun)
            .ToDictionaryAsync(day => day.LocalDate, ct);

        for (var sana = bugun.AddDays(-6); sana < bugun; sana = sana.AddDays(1))
        {
            var kun = kunlar[sana];
            var yangi = sana >= kochish;

            kun.WorkScheduleId.ShouldBe(yangi ? yangiJadval.Id : eskiJadval.Id, sana.ToString("O", CultureInfo.InvariantCulture));
            kun.PositionId.ShouldBe(yangi ? yangiLavozim.Id : eskiLavozim.Id, sana.ToString("O", CultureInfo.InvariantCulture));
        }
    }

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

    private static DateOnly BugunToshkentda() => DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(Tashkent).DateTime);

    private static DateTimeOffset Toshkentda(DateOnly sana, int soat, int daqiqa)
        => new DateTimeOffset(sana.ToDateTime(new TimeOnly(soat, daqiqa)), Tashkent).ToUniversalTime();

    private sealed class QotirilganSoat(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;

        public DateOnly Today => utcNow.TashkentDate();
    }

    private async Task BoshlanganSmenalarAsync(DateTimeOffset hozir, CancellationToken ct)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var soat = new QotirilganSoat(hozir);
        var yozuvchi = ActivatorUtilities.CreateInstance<AttendanceDayRecomputer>(qamrov.ServiceProvider, soat);

        await ActivatorUtilities.CreateInstance<AttendanceStartedShiftsJob>(qamrov.ServiceProvider, yozuvchi, soat).RunAsync(ct);
    }

    private async Task MehnatOzgarishlariAsync(CancellationToken ct)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        await qamrov.ServiceProvider.GetRequiredService<AttendanceEmploymentChangesJob>().RunAsync(ct);
    }

    private async Task QaytaHisoblaAsync(Guid[] xodimlar, DateOnly from, DateOnly to, CancellationToken ct)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        await qamrov.ServiceProvider.GetRequiredService<AttendanceDayRecomputer>()
            .RecomputeAsync(xodimlar, AttendanceDayRecomputer.DaysBetween(from, to), ct);
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

    private static AttendanceDayFacts Faktlar(AttendanceDayStatus holat, int worked)
        => new(null, null, null, null, null, null, null, null, null, 0, worked, 0, 0, 0, 0, 0, 0, 0, holat);

    private async Task SaqlaAsync(CancellationToken ct, params object[] yozuvlar)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        db.AddRange(yozuvlar);
        await db.SaveChangesAsync(ct);
    }

    private async Task<AttendanceDay?> KunAsync(Guid xodim, DateOnly sana, CancellationToken ct)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();

        return await qamrov.ServiceProvider.GetRequiredService<AppDbContext>().AttendanceDays.AsNoTracking()
            .SingleOrDefaultAsync(day => day.EmployeeId == xodim && day.LocalDate == sana, ct);
    }
}
