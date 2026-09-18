using System.Security.Cryptography;
using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Attendance.Queries;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Bonvi.Infrastructure.Persistence.ReadModels;
using Bonvi.Infrastructure.Reports;
using ClosedXML.Excel;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Davomat o'qish modeli va Excel yozuvchisi — haqiqiy PostgreSQL ustida,
/// HTTP'SIZ (SPEC §10.4, C5 nazorat nuqtasi).
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega to'g'ridan-to'g'ri, endpoint orqali emas.</b> Endpointlar keyingi
/// qismda (B6) paydo bo'ladi; C5 esa o'qish modelining O'ZINI isbotlashi
/// kerak — tabel kataklarini hosil qilish, taxta sanoqlari va Excel
/// turlari. Shu sababli <see cref="AttendanceReadModel"/> test qamrovidagi
/// <see cref="AppDbContext"/> bilan qo'lda yig'iladi va soat QOTIRILADI:
/// taxtaning «bugun» va «hozir»i test ishga tushgan vaqtga bog'liq bo'lmasin.
/// </para>
/// <para>
/// Bitta sinf bitta bazani baham ko'radi, shu sababli har bir test o'z
/// «dunyosini» noyob belgi (firma, bo'lim, ism) bilan yaratadi va o'qishni
/// shu firma bilan cheklaydi.
/// </para>
/// </remarks>
public sealed class AttendanceReadModelTests : IClassFixture<BonviApiFactory>
{
    /// <summary>Tabel sinovlari oyi: 2026-yil avgust — 31 kun, 1-avgust SHANBA.</summary>
    private const int Year = 2026;
    private const int Month = 8;

    private static readonly TimeSpan Tashkent = TimeSpan.FromHours(5);

    private readonly BonviApiFactory _factory;

    public AttendanceReadModelTests(BonviApiFactory factory) => _factory = factory;

    // ════════════════════════════════════════════════════════════
    //  Tabel — C5 ning asosiy isboti
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Har bir xodimga oyning 31 kuni uchun katak keladi: saqlangan kunlar
    /// o'z sonlari bilan, qolganlari §5.7.1 bo'yicha AYNAN kerakli holatda.
    /// </summary>
    /// <remarks>
    /// Beshta xodim §5.7.1 ning har bir tarmog'ini qamraydi: doimiy
    /// (dam olish / kelmagan), oy o'rtasida kelgan (ishlamagan → ...),
    /// oy o'rtasida ketgan (... → ishlamagan), jadvalsiz bo'lim (jadval
    /// yo'q) va lavozim jadvali bo'lim jadvalidan USTUN turgan qorovul
    /// (§5.2 — shanba ish kuni).
    /// </remarks>
    [Fact]
    public async Task Tabel_har_bir_xodimga_31_kunlik_katak_beradi_va_bosh_kunlarni_togri_hosil_qiladi()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await SeedMonthAsync(cancellationToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var readModel = ReadModel(scope.ServiceProvider, Clock(2026, 9, 16, 9, 30));

        var page = await readModel.GetTimesheetAsync(
            new TimesheetFilter(Year, Month, LegalEntityId: world.LegalEntityId, PageSize: 50),
            cancellationToken);

        page.Total.ShouldBe(5);
        page.Items.Count.ShouldBe(5);
        page.Days.Count.ShouldBe(31);
        page.Days[0].ShouldBe(new DateOnly(2026, 8, 1));
        page.Days[^1].ShouldBe(new DateOnly(2026, 8, 31));

        foreach (var row in page.Items)
        {
            // Butun massiv — har kun bir marta, tartib bilan, bo'shliqsiz.
            row.Cells.Select(cell => cell.Date).ShouldBe(page.Days);
        }

        // Familiya bo'yicha tartib: A, B, C, D, E.
        page.Items.Select(row => row.EmployeeId).ShouldBe(
            [world.Regular, world.MidMonthHire, world.Leaver, world.Unscheduled, world.SixDayGuard]);

        // ── Doimiy xodim ───────────────────────────────────────────
        var regular = Row(page, world.Regular);

        Cell(regular, 1).Status.ShouldBe(AttendanceDayStatus.DayOff);          // shanba, yozuv yo'q
        Cell(regular, 2).Status.ShouldBe(AttendanceDayStatus.DayOff);          // yakshanba

        var present = Cell(regular, 3);                                        // saqlangan
        present.Status.ShouldBe(AttendanceDayStatus.Present);
        present.WorkedMinutes.ShouldBe(480);
        present.LateMinutes.ShouldBe(5);
        present.BreakTakenMinutes.ShouldBe(45);
        present.BreakDeductionMinutes.ShouldBe(15);
        present.FirstInAt.ShouldBe(new DateTimeOffset(2026, 8, 3, 9, 15, 0, Tashkent));

        Cell(regular, 4).Status.ShouldBe(AttendanceDayStatus.Absent);          // saqlangan «kelmagan»

        var synthesizedAbsent = Cell(regular, 5);                              // yozuv yo'q, ish kuni
        synthesizedAbsent.Status.ShouldBe(AttendanceDayStatus.Absent);
        synthesizedAbsent.WorkedMinutes.ShouldBe(0);
        synthesizedAbsent.BreakTakenMinutes.ShouldBe(0);
        synthesizedAbsent.BreakDeductionMinutes.ShouldBe(0);
        synthesizedAbsent.FirstInAt.ShouldBeNull();

        Cell(regular, 8).Status.ShouldBe(AttendanceDayStatus.DayOffWorked);    // saqlangan shanba
        Cell(regular, 8).WorkedMinutes.ShouldBe(120);
        Cell(regular, 31).Status.ShouldBe(AttendanceDayStatus.Absent);         // dushanba

        // Avgustda 21 ish kuni: 1 tasi «kelgan», 20 tasi «kelmagan» (1 saqlangan + 19 hosil qilingan).
        regular.Totals.PresentDays.ShouldBe(1);
        regular.Totals.AbsentDays.ShouldBe(20);
        regular.Totals.WorkedMinutes.ShouldBe(600);
        regular.Totals.PlannedMinutes.ShouldBe(960);
        regular.Totals.LateMinutes.ShouldBe(5);

        // Bo'lim — bo'lim darajasidagi jadval OTA bo'limga biriktirilgan,
        // xodim esa ichki bo'limda: ota bo'lim bo'ylab yurish ishlagan.
        regular.Department.ShouldBe(world.ChildDepartmentName);

        // ── Oy o'rtasida ishga kirgan (15-avgust, shanba) ──────────
        var hire = Row(page, world.MidMonthHire);
        Cell(hire, 1).Status.ShouldBe(AttendanceDayStatus.NotEmployed);
        Cell(hire, 14).Status.ShouldBe(AttendanceDayStatus.NotEmployed);
        Cell(hire, 15).Status.ShouldBe(AttendanceDayStatus.DayOff);
        Cell(hire, 17).Status.ShouldBe(AttendanceDayStatus.Absent);

        // ── 10-avgust (dushanba) ishdan ketgan ─────────────────────
        var leaver = Row(page, world.Leaver);
        Cell(leaver, 10).Status.ShouldBe(AttendanceDayStatus.Absent);          // oxirgi ish kuni hali qamralgan
        Cell(leaver, 11).Status.ShouldBe(AttendanceDayStatus.NotEmployed);

        var walkedIn = Cell(leaver, 20);                                       // saqlangan NotEmployed — anomaliya
        walkedIn.Status.ShouldBe(AttendanceDayStatus.NotEmployed);
        walkedIn.FirstInAt.ShouldNotBeNull();
        Cell(leaver, 21).FirstInAt.ShouldBeNull();                             // hosil qilingan NotEmployed

        // ── Jadvalsiz bo'lim ───────────────────────────────────────
        var unscheduled = Row(page, world.Unscheduled);
        unscheduled.Cells.ShouldAllBe(cell => cell.Status == AttendanceDayStatus.NoSchedule);

        // ── Lavozim jadvali bo'lim jadvalidan ustun (6 kunlik) ─────
        var guard = Row(page, world.SixDayGuard);
        Cell(guard, 1).Status.ShouldBe(AttendanceDayStatus.Absent);            // shanba — ish kuni
        Cell(guard, 2).Status.ShouldBe(AttendanceDayStatus.DayOff);            // yakshanba

        page.LastComputedAt.ShouldBe(world.LastComputedAt);
    }

    /// <summary>Bo'lim filtri ichki bo'limlarni ham oladi, sahifalash XODIM bo'yicha.</summary>
    [Fact]
    public async Task Tabel_bolim_filtri_ichki_bolimlarni_oladi_va_xodim_boyicha_sahifalanadi()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await SeedMonthAsync(cancellationToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var readModel = ReadModel(scope.ServiceProvider, Clock(2026, 9, 16, 9, 30));

        var byParent = await readModel.GetTimesheetAsync(
            new TimesheetFilter(Year, Month, DepartmentId: world.ParentDepartmentId, PageSize: 50),
            cancellationToken);

        // Ota bo'lim so'raldi — ichki bo'limdagi to'rttasi keladi, boshqa
        // bo'limdagi «jadvalsiz» xodim kelmaydi.
        byParent.Items.Select(row => row.EmployeeId).ShouldBe(
            [world.Regular, world.MidMonthHire, world.Leaver, world.SixDayGuard]);

        var secondPage = await readModel.GetTimesheetAsync(
            new TimesheetFilter(Year, Month, LegalEntityId: world.LegalEntityId, Page: 2, PageSize: 2),
            cancellationToken);

        secondPage.Total.ShouldBe(5);
        secondPage.Items.Select(row => row.EmployeeId).ShouldBe([world.Leaver, world.Unscheduled]);
        secondPage.Items.ShouldAllBe(row => row.Cells.Count == 31);
    }

    // ════════════════════════════════════════════════════════════
    //  Jonli taxta va ishdan ketganlar ogohlantirishi
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Taxta sanoqlari SPEC §6.5 ta'riflari bo'yicha; ishdan ketgan odamning
    /// o'tishi qayta hisoblashsiz, filtrdan qat'i nazar ogohlantirishga tushadi.
    /// </summary>
    [Fact]
    public async Task Taxta_sanoqlari_togri_va_ishdan_ketgan_ogohlantirishi_filtrga_boysunmaydi()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tag = Tag("BD");

        // Hozir: 2026-09-16 (chorshanba), Toshkent 09:30. Smena 09:00, yon berish 10 daqiqa.
        var now = new DateTimeOffset(2026, 9, 16, 9, 30, 0, Tashkent);

        Guid firm, onTime, late, notYet, cameAndLeft, unknownDoor, formerEmployee, deviceId;
        string departmentName, deviceName = $"{tag} kirish";

        await using (var seedScope = _factory.Services.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();

            var legalEntity = LegalEntity.Create($"{tag} MChJ").Value;
            var department = Department.Create($"{tag} Ofis").Value;
            departmentName = department.Name;
            var schedule = WorkSchedule.Create($"{tag} 9-18", WeekDays(saturdayWorking: false)).Value;
            var assignment = ScheduleAssignment.Assign(
                schedule.Id, ScheduleScope.Department, department.Id, new DateOnly(2020, 1, 1)).Value;

            var device = Device(deviceName);
            device.NoteEvent(now.AddHours(-1).ToUniversalTime(), "10.0.0.7", skewSeconds: 600);
            deviceId = device.Id;

            db.AddRange(legalEntity, department, schedule, assignment, device);

            var employees = new List<Employee>();
            Employee Hired(string suffix, DateOnly? terminatedOn = null)
            {
                var employee = Person(tag, suffix);
                var employment = Employment.Hire(employee.Id, legalEntity.Id, new DateOnly(2024, 1, 1), department.Id).Value;
                if (terminatedOn is { } date)
                {
                    employment.Terminate(date, reasonId: null).IsSuccess.ShouldBeTrue();
                }

                employees.Add(employee);
                db.AddRange(employee, employment);
                return employee;
            }

            onTime = Hired("A").Id;
            late = Hired("B").Id;
            notYet = Hired("C").Id;
            cameAndLeft = Hired("D").Id;
            unknownDoor = Hired("E").Id;
            formerEmployee = Hired("F", terminatedOn: new DateOnly(2026, 8, 31)).Id;
            firm = legalEntity.Id;

            AttendancePass Pass(Guid employee, int hour, int minute, PassDirection direction, int day = 16)
            {
                var occurred = new DateTimeOffset(2026, 9, day, hour, minute, 0, Tashkent);
                var pass = AttendancePass.Record(
                    device.Id,
                    AttendanceIdentityProvider.Verifix,
                    $"{tag}-{employee:N}",
                    occurred,
                    occurred.ToUniversalTime(),
                    Guid.NewGuid().ToString("N"),
                    direction,
                    DirectionSource.Device,
                    PassMethod.Face).Value;
                pass.Resolve(employee, occurred.ToUniversalTime());
                return pass;
            }

            db.AddRange(
                Pass(onTime, 8, 55, PassDirection.In),
                Pass(late, 9, 20, PassDirection.In),
                Pass(cameAndLeft, 8, 0, PassDirection.In),
                Pass(cameAndLeft, 8, 30, PassDirection.Out),
                Pass(unknownDoor, 9, 5, PassDirection.Unknown),
                Pass(formerEmployee, 8, 0, PassDirection.In),
                Pass(formerEmployee, 9, 15, PassDirection.In),
                // 30 soat oldin — ogohlantirish oynasidan (24 soat) tashqarida.
                Pass(formerEmployee, 3, 30, PassDirection.In, day: 15));

            await db.SaveChangesAsync(cancellationToken);
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var readModel = ReadModel(scope.ServiceProvider, new FixedClock(now.ToUniversalTime()));

        var board = await readModel.GetBoardAsync(new BoardFilter(LegalEntityId: firm), cancellationToken);

        board.ExpectedCount.ShouldBe(5);
        board.AbsentCount.ShouldBe(1);                    // hali kelmagan
        board.LateCount.ShouldBe(2);                      // 09:20 da kelgan + hali kelmagan (09:30 > 09:10)
        board.InsideCount.ShouldBe(2);                    // noma'lum eshik va chiqib ketgan — ichkarida emas
        board.Items.Select(item => item.EmployeeId).ShouldBe([onTime, late]);
        board.ClockWarningDeviceCount.ShouldBeGreaterThanOrEqualTo(1);

        var first = board.Items[0];
        first.SinceAt.ShouldBe(new DateTimeOffset(2026, 9, 16, 8, 55, 0, Tashkent));
        first.Department.ShouldBe(departmentName);
        first.LastDeviceName.ShouldBe(deviceName);

        // ── Ogohlantirish: firma filtri bilan HAM, qidiruv filtri bilan HAM ──
        var alert = board.FormerEmployeeAlerts.ShouldHaveSingleItem();
        alert.EmployeeId.ShouldBe(formerEmployee);
        alert.OccurredAt.ShouldBe(new DateTimeOffset(2026, 9, 16, 9, 15, 0, Tashkent));
        alert.PassCount.ShouldBe(2);
        alert.TerminationDate.ShouldBe(new DateOnly(2026, 8, 31));
        alert.LegalEntityName.ShouldBe($"{tag} MChJ");
        alert.DeviceId.ShouldBe(deviceId);
        alert.DeviceName.ShouldBe(deviceName);
        alert.FullName.ShouldStartWith($"{tag}F");

        var searched = await readModel.GetBoardAsync(
            new BoardFilter(LegalEntityId: firm, Search: "hech-kimga-mos-kelmaydi"),
            cancellationToken);

        searched.ExpectedCount.ShouldBe(0);
        searched.Items.ShouldBeEmpty();
        searched.FormerEmployeeAlerts.Select(item => item.PassId).ShouldBe([alert.PassId]);

        // Nishon uchun alohida so'rov — taxtadagi bilan AYNAN bir xil qatorlar.
        var badge = await readModel.GetFormerEmployeeAlertsAsync(cancellationToken);
        badge.ShouldBe(board.FormerEmployeeAlerts);

        // 30 soatdan keyin eski o'tish oynadan chiqadi — ogohlantirish o'zi yo'qoladi.
        var later = ReadModel(scope.ServiceProvider, new FixedClock(now.AddHours(30).ToUniversalTime()));
        (await later.GetFormerEmployeeAlertsAsync(cancellationToken)).ShouldBeEmpty();
    }

    // ════════════════════════════════════════════════════════════
    //  Asosiy sahifa paneli
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Oltita holat — har biri bitta odamda; sanoqlar yig'indisi «jami» ga teng, munosabati
    /// sanani qamramaydigan odamlar (ketgan, hali kelmagan) o'tishi bo'lsa ham panelga tushmaydi.
    /// </summary>
    [Fact]
    public async Task Panel_olti_holatni_ajratadi_va_munosabatsizlarni_sanamaydi()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await SeedPanelAsync(cancellationToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var readModel = ReadModel(scope.ServiceProvider, Clock(2026, 9, 16, 10, 0));

        var panel = await readModel.GetDashboardAsync(
            new DashboardFilter(PanelDay, LegalEntityId: world.Firm),
            cancellationToken);

        panel.Date.ShouldBe(PanelDay);
        panel.IsToday.ShouldBeTrue();
        panel.AsOf.ShouldBe(new DateTimeOffset(2026, 9, 16, 10, 0, 0, Tashkent));

        panel.Total.ShouldBe(6, "ketgan (15-sentabr) va ertaga keladigan odam sanalmaydi");
        panel.Counts.ShouldBe(new AttendanceDashboardCountsDto(OnTime: 1, Late: 1, Absent: 1, NotStarted: 1, DayOff: 1, NoSchedule: 1));
        panel.TotalItems.ShouldBe(6);
        panel.Items.Select(item => item.EmployeeId).ShouldBe(
            [world.OnTime, world.Late, world.Absent, world.NotStarted, world.DayOff, world.NoSchedule]);

        var onTime = Item(panel, world.OnTime);
        onTime.Status.ShouldBe(AttendanceDashboardStatus.OnTime);
        onTime.ArrivedAt.ShouldBe(PanelAt(16, 8, 55));
        onTime.LeftAt.ShouldBeNull();
        onTime.LateMinutes.ShouldBeNull();
        onTime.FullName.ShouldStartWith(world.Tag + "A");
        onTime.DepartmentName.ShouldBe(world.ChildDepartmentName);
        onTime.PositionName.ShouldBe(world.OperatorName);

        // SPEC namunasi: 09:00 smena, 10 daqiqa yon berish, 09:33 — reja boshlanishidan 33.
        var late = Item(panel, world.Late);
        late.Status.ShouldBe(AttendanceDashboardStatus.Late);
        late.LateMinutes.ShouldBe(33);
        late.ArrivedAt.ShouldBe(PanelAt(16, 9, 33));
        late.LeftAt.ShouldBe(PanelAt(16, 9, 50));

        var absent = Item(panel, world.Absent);
        absent.Status.ShouldBe(AttendanceDashboardStatus.Absent);
        absent.ArrivedAt.ShouldBeNull();
        absent.LateMinutes.ShouldBeNull();

        Item(panel, world.NotStarted).Status.ShouldBe(AttendanceDashboardStatus.NotStarted);

        // Dam olish kunida kelgan — baribir DayOff, lekin kelgan vaqti ko'rinadi.
        var dayOff = Item(panel, world.DayOff);
        dayOff.Status.ShouldBe(AttendanceDashboardStatus.DayOff);
        dayOff.ArrivedAt.ShouldBe(PanelAt(16, 9, 40));
        dayOff.LateMinutes.ShouldBeNull();

        var noSchedule = Item(panel, world.NoSchedule);
        noSchedule.Status.ShouldBe(AttendanceDashboardStatus.NoSchedule);
        noSchedule.ArrivedAt.ShouldBe(PanelAt(16, 8, 0));
        noSchedule.DepartmentName.ShouldBe(world.UnscheduledDepartmentName);

        panel.Items.ShouldNotContain(item => item.EmployeeId == world.Terminated || item.EmployeeId == world.HiredTomorrow);
    }

    /// <summary>
    /// «Jami» va sanoqlar qidiruv va holat filtriga bo'ysunmaydi, jadval bo'ysunadi; ism
    /// bo'yicha ikki tomonga saralash, sahifalash va org-filtr tabeldagidek (ichki bo'lim, lavozim).
    /// </summary>
    [Fact]
    public async Task Panel_sanoqlari_qidiruv_va_holatga_boysunmaydi_jadval_esa_boysunadi()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await SeedPanelAsync(cancellationToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var readModel = ReadModel(scope.ServiceProvider, Clock(2026, 9, 16, 10, 0));

        var all = new DashboardFilter(PanelDay, LegalEntityId: world.Firm);
        var unfiltered = await readModel.GetDashboardAsync(all, cancellationToken);

        // ── Qidiruv ─────────────────────────────────────────────────
        var searched = await readModel.GetDashboardAsync(all with { Search = world.Tag + "B" }, cancellationToken);
        searched.Total.ShouldBe(6);
        searched.Counts.ShouldBe(unfiltered.Counts);
        searched.TotalItems.ShouldBe(1);
        searched.Items.ShouldHaveSingleItem().EmployeeId.ShouldBe(world.Late);

        // ── Holat ───────────────────────────────────────────────────
        var absentOnly = await readModel.GetDashboardAsync(all with { Status = AttendanceDashboardStatus.Absent }, cancellationToken);
        absentOnly.Total.ShouldBe(6);
        absentOnly.Counts.ShouldBe(unfiltered.Counts);
        absentOnly.TotalItems.ShouldBe(1);
        absentOnly.Items.ShouldHaveSingleItem().EmployeeId.ShouldBe(world.Absent);

        var nobody = await readModel.GetDashboardAsync(
            all with { Search = world.Tag + "B", Status = AttendanceDashboardStatus.OnTime },
            cancellationToken);
        nobody.Total.ShouldBe(6);
        nobody.Counts.ShouldBe(unfiltered.Counts);
        nobody.TotalItems.ShouldBe(0);
        nobody.Items.ShouldBeEmpty();

        // ── Saralash va sahifalash ──────────────────────────────────
        var descending = await readModel.GetDashboardAsync(all with { Descending = true }, cancellationToken);
        descending.Items.Select(item => item.EmployeeId).ShouldBe(
            [world.NoSchedule, world.DayOff, world.NotStarted, world.Absent, world.Late, world.OnTime]);

        var secondPage = await readModel.GetDashboardAsync(all with { Page = 2, PageSize = 4 }, cancellationToken);
        secondPage.TotalItems.ShouldBe(6);
        secondPage.Items.Select(item => item.EmployeeId).ShouldBe([world.DayOff, world.NoSchedule]);

        var beyond = await readModel.GetDashboardAsync(all with { Page = 3, PageSize = 4 }, cancellationToken);
        beyond.Items.ShouldBeEmpty();
        beyond.Total.ShouldBe(6);

        // ── Org-filtr: ota bo'lim ichki bo'limni ham oladi, lavozim ────
        var byParent = await readModel.GetDashboardAsync(
            new DashboardFilter(PanelDay, DepartmentId: world.ParentDepartment),
            cancellationToken);
        byParent.Total.ShouldBe(5, "jadvalsiz bo'limdagi odam ota bo'limga kirmaydi");
        byParent.Counts.NoSchedule.ShouldBe(0);
        byParent.Items.Select(item => item.EmployeeId).ShouldBe(
            [world.OnTime, world.Late, world.Absent, world.NotStarted, world.DayOff]);

        var byPosition = await readModel.GetDashboardAsync(
            new DashboardFilter(PanelDay, PositionId: world.GuardPosition),
            cancellationToken);
        byPosition.Total.ShouldBe(1);
        byPosition.Items.ShouldHaveSingleItem().EmployeeId.ShouldBe(world.NotStarted);
    }

    /// <summary>
    /// Yon berish chegarasi lahza bo'yicha: roppa-rosa 09:10 — vaqtida, 09:11 — 11 daqiqa
    /// kechikkan; faqat chiqish o'qigan kun — birinchi o'tish «kelgan» deb olinadi.
    /// </summary>
    [Fact]
    public async Task Panel_yon_berish_chegarasi_va_faqat_chiqish_oqigan_kun()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = new PanelWorld("DG");

        var atGrace = world.Hire("A", world.Child.Id);
        var afterGrace = world.Hire("B", world.Child.Id);
        var exitsOnly = world.Hire("C", world.Child.Id);

        world.Pass(atGrace, PanelAt(16, 9, 10), PassDirection.In);
        world.Pass(afterGrace, PanelAt(16, 9, 11), PassDirection.In);
        world.Pass(exitsOnly, PanelAt(16, 9, 5), PassDirection.Out);
        world.Pass(exitsOnly, PanelAt(16, 12, 0), PassDirection.Out);

        await world.SaveAsync(_factory.Services, cancellationToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var readModel = ReadModel(scope.ServiceProvider, Clock(2026, 9, 16, 13, 0));

        var panel = await readModel.GetDashboardAsync(
            new DashboardFilter(PanelDay, LegalEntityId: world.Firm.Id),
            cancellationToken);

        var boundary = Item(panel, atGrace);
        boundary.Status.ShouldBe(AttendanceDashboardStatus.OnTime);
        boundary.LateMinutes.ShouldBeNull();

        var oneMinuteLater = Item(panel, afterGrace);
        oneMinuteLater.Status.ShouldBe(AttendanceDashboardStatus.Late);
        oneMinuteLater.LateMinutes.ShouldBe(11, "yon berish (10) + 1 — reja boshlanishidan sanaladi");

        var exits = Item(panel, exitsOnly);
        exits.Status.ShouldBe(AttendanceDashboardStatus.OnTime);
        exits.ArrivedAt.ShouldBe(PanelAt(16, 9, 5));
        exits.LeftAt.ShouldBe(PanelAt(16, 12, 0));

        panel.Counts.ShouldBe(new AttendanceDashboardCountsDto(OnTime: 2, Late: 1, Absent: 0, NotStarted: 0, DayOff: 0, NoSchedule: 0));
    }

    /// <summary>
    /// O'tishsiz ish kuni: bugun reja boshlanishidan oldin — «boshlanmagan», roppa-rosa
    /// boshlanishda — «kelmagan»; o'tgan sana hech qachon «boshlanmagan» emas, kelajak sana — doim.
    /// </summary>
    [Fact]
    public async Task Panel_boshlanmagan_va_kelmagan_sana_va_soatga_qarab_ajraladi()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = new PanelWorld("DN");
        var person = world.Hire("A", world.Child.Id);
        await world.SaveAsync(_factory.Services, cancellationToken);

        await using var scope = _factory.Services.CreateAsyncScope();

        async Task<(AttendanceDashboardStatus Status, bool IsToday)> StatusAsync(DateOnly date, FixedClock clock)
        {
            var panel = await ReadModel(scope.ServiceProvider, clock).GetDashboardAsync(
                new DashboardFilter(date, LegalEntityId: world.Firm.Id),
                cancellationToken);

            return (Item(panel, person).Status, panel.IsToday);
        }

        (await StatusAsync(PanelDay, Clock(2026, 9, 16, 8, 59)))
            .ShouldBe((AttendanceDashboardStatus.NotStarted, true));
        (await StatusAsync(PanelDay, Clock(2026, 9, 16, 9, 0)))
            .ShouldBe((AttendanceDashboardStatus.Absent, true));

        // Kecha, soat hali 08:00 bo'lsa ham — kechagi smena allaqachon o'tgan.
        (await StatusAsync(PanelDay.AddDays(-1), Clock(2026, 9, 16, 8, 0)))
            .ShouldBe((AttendanceDashboardStatus.Absent, false));

        // Ertaga, soat 10:00 bo'lsa ham — ertangi smena hali boshlanmagan.
        (await StatusAsync(PanelDay.AddDays(1), Clock(2026, 9, 16, 10, 0)))
            .ShouldBe((AttendanceDashboardStatus.NotStarted, false));
    }

    /// <summary>
    /// Tungi smena (20:00–08:00): ertalabki chiqish OLDINGI sananing qatoriga tushadi —
    /// bugungi qator uni «kelgan» deb o'qimaydi.
    /// </summary>
    [Fact]
    public async Task Panel_tungi_smenaning_ertalabki_chiqishi_oldingi_sanaga_tegishli()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = new PanelWorld("DT");
        var guard = world.Hire("A", world.Child.Id);

        world.OwnSchedule(
            guard,
            [.. Enumerable.Range(1, 7).Select(index => new WorkScheduleDayInput(index, IsWorking: true, new TimeOnly(20, 0), new TimeOnly(8, 0)))]);

        world.Pass(guard, PanelAt(15, 19, 55), PassDirection.In);
        world.Pass(guard, PanelAt(16, 8, 5), PassDirection.Out);

        await world.SaveAsync(_factory.Services, cancellationToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var readModel = ReadModel(scope.ServiceProvider, Clock(2026, 9, 16, 10, 0));

        var yesterday = await readModel.GetDashboardAsync(
            new DashboardFilter(PanelDay.AddDays(-1), LegalEntityId: world.Firm.Id),
            cancellationToken);

        var shift = Item(yesterday, guard);
        shift.Status.ShouldBe(AttendanceDashboardStatus.OnTime);
        shift.ArrivedAt.ShouldBe(PanelAt(15, 19, 55));
        shift.LeftAt.ShouldBe(PanelAt(16, 8, 5), "chiqish 16-sanada, lekin 15-sanadagi smenaniki");

        var today = await readModel.GetDashboardAsync(
            new DashboardFilter(PanelDay, LegalEntityId: world.Firm.Id),
            cancellationToken);

        var tonight = Item(today, guard);
        tonight.Status.ShouldBe(AttendanceDashboardStatus.NotStarted, "bugungi smena 20:00 da boshlanadi");
        tonight.ArrivedAt.ShouldBeNull();
        tonight.LeftAt.ShouldBeNull();
    }

    // ════════════════════════════════════════════════════════════
    //  O'tishlar jurnali
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Jurnal filtrlari, bitta o'tish bir xil shaklda, moslanmagan va ketgan
    /// xodimning o'tishi.
    /// </summary>
    [Fact]
    public async Task Otishlar_jurnali_filtrlaydi_va_bitta_otish_royxatdagi_shaklda_qaytadi()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tag = Tag("PS");
        var day = new DateOnly(2026, 7, 10);

        Guid employed, departed, departmentId, employedPass, unmatchedPass;
        string departmentName;

        await using (var seedScope = _factory.Services.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();

            var legalEntity = LegalEntity.Create($"{tag} MChJ").Value;
            var department = Department.Create($"{tag} Ombor").Value;
            departmentId = department.Id;
            departmentName = department.Name;
            var device = Device($"{tag} darvoza");

            var working = Person(tag, "A");
            working.SetPhoto($"employees/{tag}.jpg");
            var job = Employment.Hire(working.Id, legalEntity.Id, new DateOnly(2025, 1, 1), department.Id).Value;

            var gone = Person(tag, "B");
            var goneJob = Employment.Hire(gone.Id, legalEntity.Id, new DateOnly(2024, 1, 1), department.Id).Value;
            goneJob.Terminate(new DateOnly(2026, 6, 30), reasonId: null).IsSuccess.ShouldBeTrue();

            employed = working.Id;
            departed = gone.Id;

            var at8 = new DateTimeOffset(2026, 7, 10, 8, 0, 0, Tashkent);
            var skewed = AttendancePass.Record(
                device.Id, AttendanceIdentityProvider.Verifix, $"{tag}-1", at8,
                at8.AddMinutes(10).ToUniversalTime(), "k1", PassDirection.In, DirectionSource.Device, PassMethod.Face,
                photoKey: $"attendance/{tag}-1.jpg").Value;
            skewed.Resolve(working.Id, at8.ToUniversalTime());
            employedPass = skewed.Id;

            var at9 = new DateTimeOffset(2026, 7, 10, 9, 0, 0, Tashkent);
            var unknownPerson = AttendancePass.Record(
                device.Id, AttendanceIdentityProvider.Verifix, $"{tag}-777", at9,
                at9.ToUniversalTime(), "k2", PassDirection.In, DirectionSource.Device, PassMethod.Card).Value;
            unmatchedPass = unknownPerson.Id;

            var at10 = new DateTimeOffset(2026, 7, 10, 10, 0, 0, Tashkent);
            var leaverPass = AttendancePass.Record(
                device.Id, AttendanceIdentityProvider.Verifix, $"{tag}-2", at10,
                at10.ToUniversalTime(), "k3", PassDirection.In, DirectionSource.Device, PassMethod.Face).Value;
            leaverPass.Resolve(gone.Id, at10.ToUniversalTime());

            db.AddRange(legalEntity, department, device, working, job, gone, goneJob, skewed, unknownPerson, leaverPass);
            await db.SaveChangesAsync(cancellationToken);
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var readModel = ReadModel(scope.ServiceProvider, Clock(2026, 9, 16, 9, 30));

        // Qidiruv: ism bo'yicha ikkitasi, shaxs raqami bo'yicha moslanmagan uchinchisi.
        var all = await readModel.SearchPassesAsync(new PassSearchFilter(day, day, Search: tag), cancellationToken);
        all.Total.ShouldBe(3);
        all.Items.Select(item => item.EmployeeId).ShouldBe([departed, null, employed]);   // eng yangisi birinchi

        var unmatched = await readModel.SearchPassesAsync(
            new PassSearchFilter(day, day, Matched: false, Search: tag),
            cancellationToken);
        unmatched.Items.ShouldHaveSingleItem().Id.ShouldBe(unmatchedPass);
        unmatched.Items[0].EmployeeName.ShouldBeNull();
        unmatched.Items[0].RawPersonId.ShouldBe($"{tag}-777");

        var one = await readModel.SearchPassesAsync(
            new PassSearchFilter(day, day, EmployeeId: employed),
            cancellationToken);
        var row = one.Items.ShouldHaveSingleItem();
        row.ClockSkewSeconds.ShouldBe(600);
        row.SkewWarning.ShouldBeTrue();
        row.PhotoKey.ShouldBe($"attendance/{tag}-1.jpg");            // turniket surati
        row.EmployeePhotoKey.ShouldBe($"employees/{tag}.jpg");       // xodim kartasi
        row.Department.ShouldBe(departmentName);

        // Ketgan xodimning o'tishida bo'lim yo'q — o'tish sanasida munosabat yo'q.
        all.Items[0].Department.ShouldBeNull();
        all.Items[0].EmployeeName.ShouldNotBeNull();

        // Bitta o'tish — ro'yxatdagi bilan AYNAN bir xil yozuv.
        (await readModel.FindPassAsync(employedPass, cancellationToken)).ShouldBe(row);
        (await readModel.FindPassAsync(Guid.NewGuid(), cancellationToken)).ShouldBeNull();

        var byDepartment = await readModel.SearchPassesAsync(
            new PassSearchFilter(day, day, DepartmentId: departmentId, Search: tag),
            cancellationToken);
        // Ketgan xodim o'sha kuni bo'limda ISHLAMAGAN — bo'lim filtri uni olmaydi.
        byDepartment.Items.Select(item => item.EmployeeId).ShouldBe([employed]);

        var ascending = await readModel.SearchPassesAsync(
            new PassSearchFilter(day, day, Search: tag, Descending: false, PageSize: 1),
            cancellationToken);
        ascending.Total.ShouldBe(3);
        ascending.Items.ShouldHaveSingleItem().Id.ShouldBe(employedPass);

        // Topilmagan o'tish — use-case darajasida 404 kodi.
        var handler = new GetPassQueryHandler(readModel);
        var missing = await handler.HandleAsync(new GetPassQuery(Guid.NewGuid()), cancellationToken);
        missing.IsFailure.ShouldBeTrue();
        missing.Error.Code.ShouldBe("davomat.otish_topilmadi");
    }

    // ════════════════════════════════════════════════════════════
    //  Hisobotlar
    // ════════════════════════════════════════════════════════════

    /// <summary>Chetlanishlar va xulosa faqat saqlangan yozuvlardan, surat bo'yicha.</summary>
    [Fact]
    public async Task Chetlanishlar_va_xulosa_saqlangan_kunlardan_yigiladi()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await SeedMonthAsync(cancellationToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var readModel = ReadModel(scope.ServiceProvider, Clock(2026, 9, 16, 9, 30));

        var from = new DateOnly(2026, 8, 1);
        var to = new DateOnly(2026, 8, 31);

        var absent = await readModel.GetExceptionsAsync(
            new ExceptionFilter(ExceptionKind.Absent, from, to, LegalEntityId: world.LegalEntityId),
            cancellationToken);
        var absentRow = absent.Items.ShouldHaveSingleItem();   // hosil qilingan kataklar hisobotga KIRMAYDI
        absentRow.Date.ShouldBe(new DateOnly(2026, 8, 4));
        absentRow.Department.ShouldBe(world.ChildDepartmentName);

        var lateRows = await readModel.GetExceptionsAsync(
            new ExceptionFilter(ExceptionKind.Late, from, to, LegalEntityId: world.LegalEntityId),
            cancellationToken);
        lateRows.Items.ShouldHaveSingleItem().LateMinutes.ShouldBe(5);

        var lateOverTen = await readModel.GetExceptionsAsync(
            new ExceptionFilter(ExceptionKind.Late, from, to, LegalEntityId: world.LegalEntityId, MinMinutes: 10),
            cancellationToken);
        lateOverTen.Total.ShouldBe(0);

        var summary = await readModel.GetSummaryAsync(
            new SummaryFilter(from, to, LegalEntityId: world.LegalEntityId),
            cancellationToken);

        var regular = summary.Items.ShouldHaveSingleItem();    // NotEmployed yozuvida firma surati yo'q
        regular.EmployeeId.ShouldBe(world.Regular);
        regular.PresentDays.ShouldBe(1);
        regular.AbsentDays.ShouldBe(1);
        regular.DayOffWorkedDays.ShouldBe(1);
        regular.PlannedDays.ShouldBe(2);
        regular.WorkedMinutes.ShouldBe(600);
        regular.PlannedMinutes.ShouldBe(960);
        regular.Department.ShouldBe(world.ChildDepartmentName);
    }

    // ════════════════════════════════════════════════════════════
    //  Excel — faylni QAYTA O'QIB tekshiriladi
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Tabel fayli: sana kataklari — sana, davomiylik ustunlari — son va
    /// ular Excel'ning o'zida yig'iladi.
    /// </summary>
    [Fact]
    public async Task Tabel_Excel_sanalari_sana_davomiyliklari_son_va_yigiladi()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await SeedMonthAsync(cancellationToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var readModel = ReadModel(scope.ServiceProvider, Clock(2026, 9, 16, 9, 30));

        var export = await new ExportTimesheetQueryHandler(readModel, new AttendanceExcelWriter())
            .HandleAsync(new GetTimesheetQuery(Year, Month, LegalEntityId: world.LegalEntityId), cancellationToken);

        export.IsSuccess.ShouldBeTrue();
        export.Value.FileName.ShouldBe("Bonvi_davomat_tabel_2026-08.xlsx");

        using var workbook = new XLWorkbook(new MemoryStream(export.Value.Content));

        // ── «Tabel» varag'i ─────────────────────────────────────────
        var grid = workbook.Worksheet("Tabel");

        var firstDay = grid.Cell(1, 4);
        firstDay.DataType.ShouldBe(XLDataType.DateTime);
        firstDay.GetDateTime().ShouldBe(new DateTime(2026, 8, 1));

        // 2-qator — doimiy xodim (familiya bo'yicha birinchi). 3-avgust = 6-ustun.
        grid.Cell(2, 1).GetString().ShouldStartWith(world.RegularLastName);
        grid.Cell(2, 6).DataType.ShouldBe(XLDataType.Number);
        grid.Cell(2, 6).GetDouble().ShouldBe(8.0);
        grid.Cell(2, 7).GetString().ShouldBe("K");                             // 4-avgust — kelmagan

        var workedColumn = 4 + 31;                                             // «Ishlangan (daq.)»
        grid.Cell(1, workedColumn).GetString().ShouldBe("Ishlangan (daq.)");
        grid.Cell(2, workedColumn).DataType.ShouldBe(XLDataType.Number);
        grid.Cell(2, workedColumn).GetDouble().ShouldBe(600);
        grid.Cell(2, workedColumn + 1).GetString().ShouldBe("10:00");

        // ── «Kunlar» varag'i ────────────────────────────────────────
        var days = workbook.Worksheet("Kunlar");
        days.Cell(1, 4).GetString().ShouldBe("Sana");
        days.Cell(1, 8).GetString().ShouldBe("Ishlangan (daq.)");
        days.Cell(1, 9).GetString().ShouldBe("Ishlangan (soat:daq)");

        // Doimiy xodimning 31 qatori: 2..32. 3-avgust — 4-qator.
        days.Cell(4, 4).DataType.ShouldBe(XLDataType.DateTime);
        days.Cell(4, 4).GetDateTime().ShouldBe(new DateTime(2026, 8, 3));
        days.Cell(4, 6).DataType.ShouldBe(XLDataType.DateTime);
        days.Cell(4, 6).GetDateTime().ShouldBe(new DateTime(2026, 8, 3, 9, 15, 0));   // Toshkent vaqti, UTC emas
        days.Cell(4, 8).DataType.ShouldBe(XLDataType.Number);
        days.Cell(4, 9).GetString().ShouldBe("8:00");

        // «Yig'iladi» — gap emas, Excel formulasi bilan isbot.
        days.Cell(40, 20).FormulaA1 = "SUM(H2:H32)";
        days.Cell(40, 20).GetDouble().ShouldBe(600);
    }

    /// <summary>O'tishlar, chetlanishlar va xulosa fayllari ham xuddi shu ikki qoidaga bo'ysunadi.</summary>
    [Fact]
    public void Otish_chetlanish_va_xulosa_Excel_turlari_togri()
    {
        var writer = new AttendanceExcelWriter();
        var occurred = new DateTimeOffset(2026, 8, 3, 3, 15, 0, TimeSpan.Zero);   // Toshkent 08:15

        var passes = writer.WritePasses(
        [
            new AttendancePassDto(
                Guid.NewGuid(), occurred, new DateOnly(2026, 8, 3), occurred, 0, false, false,
                PassDirection.In, DirectionSource.Device, PassMethod.Face, Guid.NewGuid(), "Kirish",
                AttendanceIdentityProvider.Verifix, "00731", Guid.NewGuid(), "Karimov Anvar", null,
                "Sex", "Operator", null, null, null),
        ]);

        using (var workbook = new XLWorkbook(new MemoryStream(passes)))
        {
            var sheet = workbook.Worksheet("O'tishlar");
            sheet.Cell(2, 1).DataType.ShouldBe(XLDataType.DateTime);
            sheet.Cell(2, 2).DataType.ShouldBe(XLDataType.DateTime);
            sheet.Cell(2, 2).GetDateTime().ShouldBe(new DateTime(2026, 8, 3, 8, 15, 0));
            sheet.Cell(2, 10).DataType.ShouldBe(XLDataType.Number);
        }

        var exceptions = writer.WriteExceptions(
            ExceptionKind.Late,
        [
            new AttendanceExceptionDto(
                new DateOnly(2026, 8, 3), Guid.NewGuid(), "Karimov Anvar", null, "Sex", "Operator",
                occurred, occurred.AddHours(9), occurred, occurred.AddHours(9), 75, 0, 465,
                AttendanceDayStatus.Present),
        ]);

        using (var workbook = new XLWorkbook(new MemoryStream(exceptions)))
        {
            var sheet = workbook.Worksheet("Kechikkanlar");
            sheet.Cell(2, 1).DataType.ShouldBe(XLDataType.DateTime);
            sheet.Cell(2, 5).DataType.ShouldBe(XLDataType.DateTime);
            sheet.Cell(2, 9).DataType.ShouldBe(XLDataType.Number);
            sheet.Cell(2, 9).GetDouble().ShouldBe(75);
            sheet.Cell(2, 10).GetString().ShouldBe("1:15");
            sheet.Cell(2, 13).GetDouble().ShouldBe(465);
            sheet.Cell(2, 14).GetString().ShouldBe("7:45");
        }

        var summary = writer.WriteSummary(
        [
            new AttendanceSummaryDto(
                Guid.NewGuid(), "Karimov Anvar", null, "Sex", "Operator",
                22, 20, 1, 0, 10_590, 10_560, 75, 0, 30, 1),
        ]);

        using (var workbook = new XLWorkbook(new MemoryStream(summary)))
        {
            var sheet = workbook.Worksheet("Xulosa");
            sheet.Cell(2, 4).DataType.ShouldBe(XLDataType.Number);
            sheet.Cell(2, 9).DataType.ShouldBe(XLDataType.Number);
            sheet.Cell(2, 9).GetDouble().ShouldBe(10_590);

            // 24 soatdan oshgan yig'indi aylanmaydi: 176:30, «8:30» emas.
            sheet.Cell(2, 10).GetString().ShouldBe("176:30");
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Urug' va yordamchilar
    // ════════════════════════════════════════════════════════════

    /// <summary>Avgust tabelining «dunyosi» — beshta xodim va ularning to'rtta saqlangan kuni.</summary>
    private sealed record MonthWorld(
        Guid LegalEntityId,
        Guid ParentDepartmentId,
        string ChildDepartmentName,
        Guid Regular,
        string RegularLastName,
        Guid MidMonthHire,
        Guid Leaver,
        Guid Unscheduled,
        Guid SixDayGuard,
        DateTimeOffset LastComputedAt);

    private async Task<MonthWorld> SeedMonthAsync(CancellationToken cancellationToken)
    {
        var tag = Tag("TS");

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var legalEntity = LegalEntity.Create($"{tag} MChJ").Value;
        var parent = Department.Create($"{tag} Departament").Value;
        var child = Department.Create($"{tag} Sex", parent.Id).Value;
        var other = Department.Create($"{tag} Boshqa").Value;
        var operatorPosition = Position.Create($"{tag} Operator").Value;
        var guardPosition = Position.Create($"{tag} Qorovul").Value;

        var office = WorkSchedule.Create($"{tag} Ofis 9-18", WeekDays(saturdayWorking: false)).Value;
        var sixDay = WorkSchedule.Create($"{tag} Olti kunlik", WeekDays(saturdayWorking: true)).Value;

        // Jadval OTA bo'limga — xodimlar ichki bo'limda (§5.2, eng yaqin ota).
        var byDepartment = ScheduleAssignment.Assign(
            office.Id, ScheduleScope.Department, parent.Id, new DateOnly(2020, 1, 1)).Value;

        // Lavozim pog'onasi bo'lim pog'onasidan ustun.
        var byPosition = ScheduleAssignment.Assign(
            sixDay.Id, ScheduleScope.Position, guardPosition.Id, new DateOnly(2020, 1, 1)).Value;

        db.AddRange(legalEntity, parent, child, other, operatorPosition, guardPosition, office, sixDay, byDepartment, byPosition);

        Employee Hire(string suffix, DateOnly hireDate, Guid departmentId, Guid positionId, DateOnly? terminatedOn = null)
        {
            var employee = Person(tag, suffix);
            var employment = Employment.Hire(employee.Id, legalEntity.Id, hireDate, departmentId, positionId).Value;

            if (terminatedOn is { } date)
            {
                employment.Terminate(date, reasonId: null).IsSuccess.ShouldBeTrue();
            }

            db.AddRange(employee, employment);
            return employee;
        }

        var regular = Hire("A", new DateOnly(2025, 1, 1), child.Id, operatorPosition.Id);
        var midMonth = Hire("B", new DateOnly(2026, 8, 15), child.Id, operatorPosition.Id);
        var leaver = Hire("C", new DateOnly(2024, 1, 1), child.Id, operatorPosition.Id, terminatedOn: new DateOnly(2026, 8, 10));
        var unscheduled = Hire("D", new DateOnly(2025, 1, 1), other.Id, operatorPosition.Id);
        var guard = Hire("E", new DateOnly(2025, 1, 1), child.Id, guardPosition.Id);

        var computedAt = new DateTimeOffset(2026, 9, 1, 3, 0, 0, TimeSpan.Zero);

        AttendanceDay Day(
            Guid employeeId,
            int dayOfMonth,
            AttendanceDayStatus status,
            DateTimeOffset at,
            int worked = 0,
            int planned = 0,
            int lateMinutes = 0,
            int breakTaken = 0,
            int breakDeduction = 0,
            DateTimeOffset? firstIn = null,
            bool snapshot = true)
            => AttendanceDay.Compute(
                employeeId,
                new DateOnly(Year, Month, dayOfMonth),
                new AttendanceDayFacts(
                    EmploymentId: null,
                    LegalEntityId: snapshot ? legalEntity.Id : null,
                    DepartmentId: snapshot ? child.Id : null,
                    PositionId: snapshot ? operatorPosition.Id : null,
                    WorkScheduleId: snapshot ? office.Id : null,
                    PlanStartAt: null,
                    PlanEndAt: null,
                    FirstInAt: firstIn?.ToUniversalTime(),
                    LastOutAt: null,
                    PlannedMinutes: planned,
                    WorkedMinutes: worked,
                    LateMinutes: lateMinutes,
                    EarlyLeaveMinutes: 0,
                    OvertimeMinutes: 0,
                    BreakTakenMinutes: breakTaken,
                    BreakDeductionMinutes: breakDeduction,
                    PassCount: firstIn is null ? 0 : 2,
                    IgnoredPassCount: 0,
                    Status: status),
                at);

        db.AddRange(
            Day(regular.Id, 3, AttendanceDayStatus.Present, computedAt, worked: 480, planned: 480, lateMinutes: 5,
                breakTaken: 45, breakDeduction: 15, firstIn: new DateTimeOffset(2026, 8, 3, 9, 15, 0, Tashkent)),
            Day(regular.Id, 4, AttendanceDayStatus.Absent, computedAt.AddMinutes(-5), planned: 480),
            Day(regular.Id, 8, AttendanceDayStatus.DayOffWorked, computedAt.AddMinutes(-10), worked: 120),
            Day(leaver.Id, 20, AttendanceDayStatus.NotEmployed, computedAt.AddMinutes(-15),
                firstIn: new DateTimeOffset(2026, 8, 20, 10, 0, 0, Tashkent), snapshot: false));

        await db.SaveChangesAsync(cancellationToken);

        return new MonthWorld(
            legalEntity.Id,
            parent.Id,
            child.Name,
            regular.Id,
            regular.FullName.LastName,
            midMonth.Id,
            leaver.Id,
            unscheduled.Id,
            guard.Id,
            computedAt);
    }

    private static AttendanceReadModel ReadModel(IServiceProvider services, IClock clock)
        => new(
            services.GetRequiredService<AppDbContext>(),
            services.GetRequiredService<IDepartmentRepository>(),
            clock);

    private static FixedClock Clock(int year, int month, int day, int hour, int minute)
        => new(new DateTimeOffset(year, month, day, hour, minute, 0, Tashkent).ToUniversalTime());

    private static string Tag(string prefix)
        => prefix + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    /// <summary>Familiya belgi + harf bilan boshlanadi — tabel tartibi testda oldindan ma'lum bo'lsin.</summary>
    private static Employee Person(string tag, string suffix)
        => Employee.Create(FullName.Create($"{tag}{suffix}", "Test", null).Value).Value;

    private static AttendanceDevice Device(string name)
        => AttendanceDevice.Register(
            name,
            RandomNumberGenerator.GetBytes(32),
            "tst00000",
            DateTimeOffset.UtcNow).Value;

    /// <summary>Dushanba–juma 09:00–18:00, tanaffus 13:00–14:00; shanba — ixtiyoriy.</summary>
    private static WorkScheduleDayInput[] WeekDays(bool saturdayWorking)
        =>
        [
            .. Enumerable.Range(1, 7).Select(index => index <= 5 || (index == 6 && saturdayWorking)
                ? new WorkScheduleDayInput(
                    index,
                    IsWorking: true,
                    new TimeOnly(9, 0),
                    new TimeOnly(18, 0),
                    BreakMinutes: 60,
                    new TimeOnly(13, 0),
                    new TimeOnly(14, 0))
                : new WorkScheduleDayInput(index, IsWorking: false)),
        ];

    /// <summary>Panel sinovlari kuni — 2026-09-16, chorshanba.</summary>
    private static readonly DateOnly PanelDay = new(2026, 9, 16);

    /// <summary>Sentabr 2026 dagi Toshkent lahzasi.</summary>
    private static DateTimeOffset PanelAt(int day, int hour, int minute)
        => new(2026, 9, day, hour, minute, 0, Tashkent);

    private static AttendanceDashboardItemDto Item(DashboardPage page, Guid employeeId)
        => page.Items.Single(item => item.EmployeeId == employeeId);

    /// <summary>Oltita holat panelining «dunyosi» — har holatda bitta odam, familiya harfi tartibida.</summary>
    private sealed record PanelSeed(
        string Tag,
        Guid Firm,
        Guid ParentDepartment,
        Guid GuardPosition,
        string ChildDepartmentName,
        string UnscheduledDepartmentName,
        string OperatorName,
        Guid OnTime,
        Guid Late,
        Guid Absent,
        Guid NotStarted,
        Guid DayOff,
        Guid NoSchedule,
        Guid Terminated,
        Guid HiredTomorrow);

    /// <summary>
    /// 2026-09-16 soat 10:00 uchun: A 08:55 da keldi, B 09:33 da (09:50 da chiqdi), C yo'q,
    /// D ning smenasi 14:00 da, E ning chorshanbasi dam olish kuni (09:40 da keldi),
    /// F jadvalsiz bo'limda (08:00). Ketgan va ertaga keladigan odamning o'tishi bor, lekin
    /// ular panelda bo'lmasligi kerak.
    /// </summary>
    private async Task<PanelSeed> SeedPanelAsync(CancellationToken cancellationToken)
    {
        var world = new PanelWorld("DB");
        var guard = Position.Create($"{world.Tag} Qorovul").Value;
        world.Add(guard);

        var onTime = world.Hire("A", world.Child.Id);
        var late = world.Hire("B", world.Child.Id);
        var absent = world.Hire("C", world.Child.Id);
        var notStarted = world.Hire("D", world.Parent.Id, positionId: guard.Id);
        var dayOff = world.Hire("E", world.Parent.Id);
        var noSchedule = world.Hire("F", world.Unscheduled.Id);
        var terminated = world.Hire("G", world.Child.Id, terminatedOn: PanelDay.AddDays(-1));
        var hiredTomorrow = world.Hire("H", world.Child.Id, hired: PanelDay.AddDays(1));

        world.OwnSchedule(
            notStarted,
            [.. Enumerable.Range(1, 7).Select(index => new WorkScheduleDayInput(index, IsWorking: true, new TimeOnly(14, 0), new TimeOnly(22, 0)))]);

        // Chorshanba (ISO 3) — dam olish kuni.
        world.OwnSchedule(
            dayOff,
            [.. WeekDays(saturdayWorking: false).Select(day => day.DayIndex == 3 ? new WorkScheduleDayInput(3, IsWorking: false) : day)]);

        world.Pass(onTime, PanelAt(16, 8, 55), PassDirection.In);
        world.Pass(late, PanelAt(16, 9, 33), PassDirection.In);
        world.Pass(late, PanelAt(16, 9, 50), PassDirection.Out);
        world.Pass(dayOff, PanelAt(16, 9, 40), PassDirection.In);
        world.Pass(noSchedule, PanelAt(16, 8, 0), PassDirection.In);
        world.Pass(terminated, PanelAt(16, 9, 0), PassDirection.In);
        world.Pass(hiredTomorrow, PanelAt(16, 9, 0), PassDirection.In);

        await world.SaveAsync(_factory.Services, cancellationToken);

        return new PanelSeed(
            world.Tag,
            world.Firm.Id,
            world.Parent.Id,
            guard.Id,
            world.Child.Name,
            world.Unscheduled.Name,
            world.Operator.Name,
            onTime,
            late,
            absent,
            notStarted,
            dayOff,
            noSchedule,
            terminated,
            hiredTomorrow);
    }

    /// <summary>
    /// Panel sinovi uchun bitta firma: ota bo'lim (09:00–18:00 jadval, yon berish 10), uning
    /// ichki bo'limi, jadvalsiz bo'lim, lavozim va turniket. Xodimlar va o'tishlar test ichida.
    /// </summary>
    /// <remarks>
    /// Sinf bazasi umumiy — jadval faqat SHU dunyoning bo'limiga yoki xodimiga biriktiriladi
    /// (sukut pog'onasi boshqa testlarning xodimlariga ham tushardi).
    /// </remarks>
    private sealed class PanelWorld
    {
        private readonly List<object> _pending = [];

        public PanelWorld(string prefix)
        {
            Tag = AttendanceReadModelTests.Tag(prefix);
            Firm = LegalEntity.Create($"{Tag} MChJ").Value;
            Parent = Department.Create($"{Tag} Ofis").Value;
            Child = Department.Create($"{Tag} Sex", Parent.Id).Value;
            Unscheduled = Department.Create($"{Tag} Jadvalsiz").Value;
            Operator = Position.Create($"{Tag} Operator").Value;
            Turnstile = Device($"{Tag} turniket");

            var office = WorkSchedule.Create($"{Tag} Ofis 9-18", WeekDays(saturdayWorking: false)).Value;
            var byDepartment = ScheduleAssignment.Assign(
                office.Id, ScheduleScope.Department, Parent.Id, new DateOnly(2020, 1, 1)).Value;

            _pending.AddRange([Firm, Parent, Child, Unscheduled, Operator, Turnstile, office, byDepartment]);
        }

        public string Tag { get; }

        public LegalEntity Firm { get; }

        public Department Parent { get; }

        public Department Child { get; }

        public Department Unscheduled { get; }

        public Position Operator { get; }

        public AttendanceDevice Turnstile { get; }

        public void Add(object entity) => _pending.Add(entity);

        /// <summary>Familiya — belgi + harf, ya'ni ism bo'yicha tartib harf tartibi.</summary>
        public Guid Hire(
            string suffix,
            Guid departmentId,
            DateOnly? hired = null,
            DateOnly? terminatedOn = null,
            Guid? positionId = null)
        {
            var employee = Person(Tag, suffix);
            var employment = Employment.Hire(
                employee.Id,
                Firm.Id,
                hired ?? new DateOnly(2024, 1, 1),
                departmentId,
                positionId ?? Operator.Id).Value;

            if (terminatedOn is { } date)
            {
                employment.Terminate(date, reasonId: null).IsSuccess.ShouldBeTrue();
            }

            _pending.AddRange([employee, employment]);
            return employee.Id;
        }

        /// <summary>Xodim pog'onasidagi jadval — bo'lim jadvalidan ustun.</summary>
        public void OwnSchedule(Guid employeeId, WorkScheduleDayInput[] days)
        {
            var schedule = WorkSchedule.Create($"{Tag} {employeeId:N}", days).Value;
            var assignment = ScheduleAssignment.Assign(
                schedule.Id, ScheduleScope.Employee, employeeId, new DateOnly(2020, 1, 1)).Value;

            _pending.AddRange([schedule, assignment]);
        }

        /// <summary>Xodimga bog'langan o'tish, qurilma aytgan yo'nalish bilan.</summary>
        public void Pass(Guid employeeId, DateTimeOffset at, PassDirection direction)
        {
            var pass = AttendancePass.Record(
                Turnstile.Id,
                AttendanceIdentityProvider.Verifix,
                $"{Tag}-{employeeId:N}",
                at,
                at.ToUniversalTime(),
                Guid.NewGuid().ToString("N"),
                direction,
                DirectionSource.Device,
                PassMethod.Face).Value;

            pass.Resolve(employeeId, at.ToUniversalTime());
            _pending.Add(pass);
        }

        public async Task SaveAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.AddRange(_pending);
            await db.SaveChangesAsync(cancellationToken);
            _pending.Clear();
        }
    }

    private static TimesheetRowDto Row(TimesheetPage page, Guid employeeId)
        => page.Items.Single(row => row.EmployeeId == employeeId);

    private static TimesheetCellDto Cell(TimesheetRowDto row, int dayOfMonth)
        => row.Cells.Single(cell => cell.Date == new DateOnly(Year, Month, dayOfMonth));

    /// <summary>Qotirilgan soat — taxtaning «hozir»i test vaqtiga bog'liq bo'lmasin.</summary>
    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;

        public DateOnly Today => utcNow.TashkentDate();
    }
}
