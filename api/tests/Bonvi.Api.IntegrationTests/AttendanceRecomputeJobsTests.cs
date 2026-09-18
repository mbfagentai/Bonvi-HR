using System.Globalization;
using System.Security.Cryptography;
using Bonvi.Api.Startup;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Recompute;
using Bonvi.Application.Attendance.Retention;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Bonvi.Infrastructure.Persistence.ReadModels;
using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Davomatning qayta hisoblash fon vazifalari — iflos navbat, kechikib bog'lash,
/// tungi hisoblash va parallel yozuvchi (SPEC §9, B7).
/// </summary>
/// <remarks>
/// <para>
/// Vazifalar HTTP'siz, to'g'ridan-to'g'ri chaqiriladi (SPEC §9.1: «job methods are
/// public and are tested by calling them directly») — <c>Testing</c> muhitida
/// Hangfire serveri umuman yo'q.
/// </para>
/// <para>
/// Sinf bitta bazani bo'lishadi va iflos navbat BUTUN bazani bo'shatadi, shu
/// sababli har bir test faqat o'z xodimining qatorlarini tekshiradi.
/// </para>
/// </remarks>
public sealed class AttendanceRecomputeJobsTests : IClassFixture<BonviApiFactory>
{
    private static readonly TimeSpan Tashkent = TimeSpan.FromHours(5);

    private readonly BonviApiFactory _factory;

    public AttendanceRecomputeJobsTests(BonviApiFactory factory) => _factory = factory;

    // ════════════════════════════════════════════════════════════
    //  Iflos navbat — §9.3
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Kutayotgan o'tishlar hisoblanadi va belgilanadi; sana ±1 qo'shni bilan; bir xodimning
    /// sanalari boshqa xodimga «yuqmaydi»; ikkinchi yurish hech narsaga tegmaydi.
    /// </summary>
    [Fact]
    public async Task Iflos_navbat_kutayotgan_otishlarni_hisoblaydi_belgilaydi_va_ikkinchi_yurishda_tegmaydi()
    {
        var ct = TestContext.Current.CancellationToken;
        var bugun = BugunToshkentda();
        var sana = bugun.AddDays(-5);
        var eski = bugun.AddDays(-15);

        var ikkiSanali = await XodimAsync("A", ct);
        var bittaSanali = await XodimAsync("B", ct);
        var qurilma = await QurilmaAsync(ct);

        var otishlar = new[]
        {
            Otish(qurilma, ikkiSanali, sana, 9, 5, PassDirection.In),
            Otish(qurilma, ikkiSanali, sana, 18, 2, PassDirection.Out),
            Otish(qurilma, ikkiSanali, eski, 9, 0, PassDirection.In),
            Otish(qurilma, ikkiSanali, eski, 18, 0, PassDirection.Out),
            Otish(qurilma, bittaSanali, sana, 8, 55, PassDirection.In),
        };

        await SaqlaAsync(ct, otishlar);

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            await qamrov.ServiceProvider.GetRequiredService<AttendanceDirtyRecomputeJob>().RunAsync(ct);
        }

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
            var ids = otishlar.Select(pass => pass.Id).ToList();

            (await db.AttendancePasses.AsNoTracking().Where(pass => ids.Contains(pass.Id)).ToListAsync(ct))
                .ShouldAllBe(pass => pass.ProcessedAt != null);

            var kelgan = await KunAsync(db, ikkiSanali, sana, ct);
            kelgan.ShouldNotBeNull().Status.ShouldBe(AttendanceDayStatus.Present);
            kelgan.WorkedMinutes.ShouldBe(537);
            kelgan.PassCount.ShouldBe(2);

            // Qo'shnilar ham hisoblangan (§9.3 #2) — jadval har kuni ish kuni, o'tishsiz → «Kelmadi».
            (await KunAsync(db, ikkiSanali, sana.AddDays(-1), ct)).ShouldNotBeNull().Status.ShouldBe(AttendanceDayStatus.Absent);
            (await KunAsync(db, ikkiSanali, sana.AddDays(1), ct)).ShouldNotBeNull().Status.ShouldBe(AttendanceDayStatus.Absent);
            (await KunAsync(db, ikkiSanali, eski, ct)).ShouldNotBeNull().WorkedMinutes.ShouldBe(540);

            // Navbatda bo'lmagan sana — tegilmagan.
            (await KunAsync(db, ikkiSanali, sana.AddDays(-5), ct)).ShouldBeNull();

            // Bitta sanali xodim eski sanada hisoblanmagan: guruhlar kesishmaydi.
            (await KunAsync(db, bittaSanali, sana, ct)).ShouldNotBeNull().Status.ShouldBe(AttendanceDayStatus.MissingExit);
            (await KunAsync(db, bittaSanali, eski, ct)).ShouldBeNull();
        }

        var oldin = await HisoblanganVaqtlarAsync(ikkiSanali, ct);

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            await qamrov.ServiceProvider.GetRequiredService<AttendanceDirtyRecomputeJob>().RunAsync(ct);
        }

        (await HisoblanganVaqtlarAsync(ikkiSanali, ct)).ShouldBe(oldin, "navbat bo'sh — ikkinchi yurish hech bir kunni qayta yozmaydi");
    }

    /// <summary>
    /// Qabul moslik saqlanishidan OLDIN o'qigan o'tish bog'lanmay qoladi; davriy vazifa uni
    /// to'ldirish kodi bilan bog'laydi, kunini hisoblaydi va belgilaydi.
    /// </summary>
    [Fact]
    public async Task Mosligi_bor_lekin_boglanmagan_otish_davriy_vazifada_boglanadi_va_hisoblanadi()
    {
        var ct = TestContext.Current.CancellationToken;
        var sana = BugunToshkentda().AddDays(-2);
        var xodim = await XodimAsync("L", ct);
        var qurilma = await QurilmaAsync(ct);
        var shaxs = "late-" + Guid.NewGuid().ToString("N")[..8];

        var kirish = Otish(qurilma, null, sana, 9, 0, PassDirection.In, shaxs);
        var chiqish = Otish(qurilma, null, sana, 18, 0, PassDirection.Out, shaxs);
        var moslik = AttendanceIdentity.Link(AttendanceIdentityProvider.Verifix, shaxs, xodim).Value;

        // To'ldirish vazifasi CHAQIRILMAYDI — aynan o'sha tor oyna.
        await SaqlaAsync(ct, kirish, chiqish, moslik);

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            await qamrov.ServiceProvider.GetRequiredService<AttendanceDirtyRecomputeJob>().RunAsync(ct);
        }

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

            var otishlar = await db.AttendancePasses.AsNoTracking().Where(pass => pass.RawPersonId == shaxs).ToListAsync(ct);
            otishlar.Count.ShouldBe(2);
            otishlar.ShouldAllBe(pass => pass.ResolvedEmployeeId == xodim && pass.ProcessedAt != null);

            var kun = await KunAsync(db, xodim, sana, ct);
            kun.ShouldNotBeNull().PassCount.ShouldBe(2);
            kun.WorkedMinutes.ShouldBe(540);
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Parallel yozuvchi — B6 topgan poyga
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Ikki qayta hisoblash bir (xodim, sana) ni BIRINCHI marta birga yozadi: ikkinchisi
    /// <c>ux_attendance_days_employee_date</c> ga uriladi, lekin yiqilmaydi — qayta o'qib yangilaydi.
    /// </summary>
    [Fact]
    public async Task Ikki_qayta_hisoblash_bir_kunni_birinchi_marta_birga_yozsa_ikkinchisi_yiqilmaydi()
    {
        var ct = TestContext.Current.CancellationToken;
        var sana = BugunToshkentda().AddDays(-3);
        var xodim = await XodimAsync("R", ct);
        var qurilma = await QurilmaAsync(ct);

        var kirish = Otish(qurilma, xodim, sana, 9, 0, PassDirection.In);
        var chiqish = Otish(qurilma, xodim, sana, 17, 0, PassDirection.Out);
        await SaqlaAsync(ct, kirish, chiqish);

        await using var birinchi = _factory.Services.CreateAsyncScope();
        await using var ikkinchi = _factory.Services.CreateAsyncScope();

        var gOlib = birinchi.ServiceProvider.GetRequiredService<AttendanceDayRecomputer>();

        // Ikkinchi yozuvchi qatorni «yo'q» deb o'qib, qo'shib bo'lgach — saqlashidan
        // aynan OLDIN birinchisi to'liq yozib ulguradi.
        var oraliq = new OraliqdaYozuvchi(
            ikkinchi.ServiceProvider.GetRequiredService<AppDbContext>(),
            () => gOlib.RecomputeAsync([xodim], [sana], ct));

        var kechikkan = Yozuvchi(ikkinchi.ServiceProvider, oraliq);

        var natija = await kechikkan.RecomputeAsync([xodim], [sana], ct);

        var fault = ikkinchi.ServiceProvider.GetRequiredService<IDatabaseFault>();
        var xato = oraliq.Xatolar.ShouldHaveSingleItem();
        fault.IsConcurrentDayWrite(xato).ShouldBeTrue(xato.ToString());
        oraliq.Chaqiruvlar.ShouldBe(2, "birinchi saqlash to'qnashdi, ikkinchisi qayta o'qib yozdi");
        natija.StoredDays.ShouldBe(1);

        await using var tekshir = _factory.Services.CreateAsyncScope();
        var db = tekshir.ServiceProvider.GetRequiredService<AppDbContext>();
        var qatorlar = await db.AttendanceDays.AsNoTracking()
            .Where(day => day.EmployeeId == xodim && day.LocalDate == sana)
            .ToListAsync(ct);

        qatorlar.ShouldHaveSingleItem().WorkedMinutes.ShouldBe(480);
        qatorlar[0].Status.ShouldBe(AttendanceDayStatus.Present);
    }

    /// <summary>
    /// Ikki qayta hisoblash bitta ESKIRGAN qatorni birga o'chiradi: ikkinchisining DELETE i
    /// nol qatorga tegadi, lekin yiqilmaydi.
    /// </summary>
    [Fact]
    public async Task Ikki_qayta_hisoblash_bitta_eskirgan_qatorni_birga_ochirsa_ikkinchisi_yiqilmaydi()
    {
        var ct = TestContext.Current.CancellationToken;
        var sana = BugunToshkentda().AddDays(-4);

        // Munosabatsiz, o'tishsiz odam — bu kun uchun qator bo'lmasligi kerak (§5.4).
        var odam = Employee.Create(FullName.Create("Eskirgan", "Qator", Guid.NewGuid().ToString("N")[..6]).Value).Value;
        var eskirgan = AttendanceDay.Compute(odam.Id, sana, Faktlar(AttendanceDayStatus.Absent, worked: 0), DateTimeOffset.UtcNow);
        await SaqlaAsync(ct, odam, eskirgan);

        await using var birinchi = _factory.Services.CreateAsyncScope();
        await using var ikkinchi = _factory.Services.CreateAsyncScope();

        var gOlib = birinchi.ServiceProvider.GetRequiredService<AttendanceDayRecomputer>();
        var oraliq = new OraliqdaYozuvchi(
            ikkinchi.ServiceProvider.GetRequiredService<AppDbContext>(),
            () => gOlib.RecomputeAsync([odam.Id], [sana], ct));

        var natija = await Yozuvchi(ikkinchi.ServiceProvider, oraliq).RecomputeAsync([odam.Id], [sana], ct);

        var xato = oraliq.Xatolar.ShouldHaveSingleItem();
        ikkinchi.ServiceProvider.GetRequiredService<IDatabaseFault>().IsConcurrentDayWrite(xato).ShouldBeTrue(xato.ToString());
        natija.RemovedDays.ShouldBe(0, "qatorni g'olib o'chirgan — qayta urinish uni topmaydi");

        await using var tekshir = _factory.Services.CreateAsyncScope();
        (await KunAsync(tekshir.ServiceProvider.GetRequiredService<AppDbContext>(), odam.Id, sana, ct)).ShouldBeNull();
    }

    // ════════════════════════════════════════════════════════════
    //  Tungi hisoblash va ulanish
    // ════════════════════════════════════════════════════════════

    /// <summary>Tungi vazifa bugun − 2 … bugun ni yozadi va o'tishlarning «hisobga olindi» belgisiga tegmaydi.</summary>
    [Fact]
    public async Task Tungi_hisoblash_oxirgi_uch_kunni_yozadi_va_navbat_belgisiga_tegmaydi()
    {
        var ct = TestContext.Current.CancellationToken;
        var bugun = BugunToshkentda();
        var xodim = await XodimAsync("N", ct);
        var qurilma = await QurilmaAsync(ct);

        var kirish = Otish(qurilma, xodim, bugun.AddDays(-1), 9, 0, PassDirection.In);
        await SaqlaAsync(ct, kirish);

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            await qamrov.ServiceProvider.GetRequiredService<AttendanceNightlyRecomputeJob>().RunAsync(ct);
        }

        await using var tekshir = _factory.Services.CreateAsyncScope();
        var db = tekshir.ServiceProvider.GetRequiredService<AppDbContext>();

        (await KunAsync(db, xodim, bugun.AddDays(-2), ct)).ShouldNotBeNull().Status.ShouldBe(AttendanceDayStatus.Absent);
        (await KunAsync(db, xodim, bugun.AddDays(-1), ct)).ShouldNotBeNull().Status.ShouldBe(AttendanceDayStatus.MissingExit);
        (await KunAsync(db, xodim, bugun, ct)).ShouldNotBeNull();
        (await KunAsync(db, xodim, bugun.AddDays(-3), ct)).ShouldBeNull();

        (await db.AttendancePasses.AsNoTracking().SingleAsync(pass => pass.Id == kirish.Id, ct))
            .ProcessedAt.ShouldBeNull("tungi hisoblash navbatdan ishni yashirmaydi (qo'lda qayta hisoblash bilan bir qoida)");
    }

    /// <summary>
    /// <c>Testing</c> — sinxron navbat, Hangfire yo'q; boshqa HAR QANDAY muhit — Hangfire navbati va serveri.
    /// Sinxron navbat ishlab chiqarishga tushib qolsa, import tasdig'i bitta so'rovda minglab so'rov bo'lardi.
    /// </summary>
    [Fact]
    public void Navbat_faqat_Testing_da_sinxron_qolgan_hamma_muhitda_Hangfire()
    {
        _factory.Services.GetRequiredService<IBackgroundJobQueue>().GetType().Name.ShouldBe("InlineBackgroundJobQueue");

        foreach (var muhit in new[] { Environments.Production, Environments.Development, Environments.Staging })
        {
            var quruvchi = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = muhit });
            quruvchi.Configuration["ConnectionStrings:Default"] = "Host=mavjud-emas;Database=bonvi";
            var oldingiXizmatlar = quruvchi.Services.Count(service => service.ServiceType == typeof(IHostedService));

            quruvchi.AddBonviBackgroundJobs();

            quruvchi.Services.Single(service => service.ServiceType == typeof(IBackgroundJobQueue))
                .ImplementationType.ShouldNotBeNull().Name.ShouldBe("HangfireBackgroundJobQueue", muhit);
            quruvchi.Services.ShouldContain(service => service.ServiceType == typeof(IBackgroundJobClient), muhit);
            quruvchi.Services.Count(service => service.ServiceType == typeof(IHostedService))
                .ShouldBeGreaterThan(oldingiXizmatlar, $"{muhit}: Hangfire serveri ulanmagan");
        }

        var sinov = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = BackgroundJobs.TestingEnvironment });
        sinov.AddBonviBackgroundJobs();

        sinov.Services.Single(service => service.ServiceType == typeof(IBackgroundJobQueue))
            .ImplementationType.ShouldNotBeNull().Name.ShouldBe("InlineBackgroundJobQueue");
        sinov.Services.ShouldNotContain(service => service.ServiceType == typeof(IBackgroundJobClient));
    }

    // ════════════════════════════════════════════════════════════
    //  Bazadagi natija — §11.1 #50, #64a (B4 + B6 ning ochiq bandi, bittadan)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// #64a. Tanaffus oynali jadvalda qayta hisoblash: #51, #52, #53, #58, #58a, #58c holatlarining
    /// <c>break_taken_minutes</c>, <c>break_deduction_minutes</c> va <c>worked_minutes</c> i BAZAGA aynan
    /// SPEC dagi sonlar bilan yoziladi va tabelga o'zgarmay chiqadi; hosil qilingan katak (§5.7.1) ikkalasida 0.
    /// </summary>
    /// <remarks>
    /// Hisoblashning o'zi B4 birlik testlarida; bu yerda — natija qatorga YETIB BORISHI: ustun
    /// xaritasi almashib qolsa (taken ↔ deduction) yoki yozuvchi bittasini unutsa, birlik testlari
    /// yashil qoladi, bu test esa yiqiladi. Soat qotirilgan — «hosil qilingan» katak test kuniga bog'liq emas.
    /// </remarks>
    [Fact]
    public async Task Tanaffus_natijasi_qatorga_aynan_yoziladi_va_hosil_qilingan_katakda_nol()
    {
        var ct = TestContext.Current.CancellationToken;
        var (xodim, _) = await TanaffusliXodimAsync(ct);
        var soat = new QotirilganSoat(new DateTimeOffset(2026, 9, 16, 7, 0, 0, TimeSpan.Zero));

        var natija = await QaytaHisoblaAsync(soat, Dushanba, Dushanba.AddDays(6), [xodim], ct);
        natija.StoredDays.ShouldBe(6, "yakshanba — o'tishsiz dam olish kuni, qator yo'q");
        natija.RemovedDays.ShouldBe(0);

        // (sana, holat SPEC raqami, olingan tanaffus, ushlanma, ishlangan)
        var kutilgan = new (DateOnly Sana, string Holat, int Olingan, int Ushlanma, int Ishlangan)[]
        {
            (Dushanba, "#51 binodan chiqmagan", 0, 60, 480),
            (Dushanba.AddDays(1), "#52 aynan oyna bo'yicha tushlik", 60, 0, 480),
            (Dushanba.AddDays(2), "#53 yarim tanaffus", 30, 30, 480),
            (Dushanba.AddDays(3), "#58 oynadan oldin ketgan", 0, 0, 235),
            (Dushanba.AddDays(4), "#58a oyna o'rtasida kelgan", 0, 30, 240),
            (Dushanba.AddDays(5), "#58c tanaffus oynadan qisqa", 0, 30, 510),
        };

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        foreach (var (sana, holat, olingan, ushlanma, ishlangan) in kutilgan)
        {
            var kun = (await KunAsync(db, xodim, sana, ct)).ShouldNotBeNull(holat);
            kun.BreakTakenMinutes.ShouldBe(olingan, holat);
            kun.BreakDeductionMinutes.ShouldBe(ushlanma, holat);
            kun.WorkedMinutes.ShouldBe(ishlangan, holat);
            kun.PlannedMinutes.ShouldBe(sana == Dushanba.AddDays(5) ? 510 : 480, holat);
            kun.ComputedAt.ShouldBe(soat.UtcNow, holat);
        }

        (await KunAsync(db, xodim, Dushanba.AddDays(6), ct)).ShouldBeNull();

        var tabel = await new AttendanceReadModel(db, qamrov.ServiceProvider.GetRequiredService<IDepartmentRepository>(), soat)
            .GetTimesheetAsync(new TimesheetFilter(2026, 6, EmployeeId: xodim, PageSize: 10), ct);
        var kataklar = tabel.Items.ShouldHaveSingleItem().Cells.ToDictionary(katak => katak.Date);

        foreach (var (sana, holat, olingan, ushlanma, ishlangan) in kutilgan)
        {
            kataklar[sana].BreakTakenMinutes.ShouldBe(olingan, holat);
            kataklar[sana].BreakDeductionMinutes.ShouldBe(ushlanma, holat);
            kataklar[sana].WorkedMinutes.ShouldBe(ishlangan, holat);
        }

        // Hosil qilingan kataklar: yakshanba (dam olish) va qayta hisoblanmagan keyingi dushanba (ish kuni).
        foreach (var sana in new[] { Dushanba.AddDays(6), Dushanba.AddDays(7) })
        {
            (await KunAsync(db, xodim, sana, ct)).ShouldBeNull("sinov sharti: katak saqlanmagan, hosil qilingan");
            kataklar[sana].FirstInAt.ShouldBeNull();
            kataklar[sana].BreakTakenMinutes.ShouldBe(0, sana.ToString("O", CultureInfo.InvariantCulture));
            kataklar[sana].BreakDeductionMinutes.ShouldBe(0, sana.ToString("O", CultureInfo.InvariantCulture));
        }

        kataklar[Dushanba.AddDays(6)].Status.ShouldBe(AttendanceDayStatus.DayOff);
        kataklar[Dushanba.AddDays(7)].Status.ShouldBe(AttendanceDayStatus.Absent);
    }

    /// <summary>
    /// #50. Qayta hisoblash idempotent — BAZADA: bir hafta, ikki xodim (tanaffus oynali kunduzgi va
    /// tungi smena: yarim tundan o'tgan kun, sakrash, noma'lum yo'nalish, chiqishsiz kun, kelmagan kunlar)
    /// ikki marta hisoblanadi va HAR BIR qatorning HAR BIR ustuni (identifikator ham) teng —
    /// <c>computed_at</c> dan tashqari, u esa ikkinchi yurishning soatiga surilgan (ya'ni qatorlar
    /// haqiqatan qayta yozilgan, «tegilmagani uchun teng» emas).
    /// </summary>
    [Fact]
    public async Task Ikki_marta_qayta_hisoblash_bazada_har_bir_ustunni_bir_xil_qoldiradi()
    {
        var ct = TestContext.Current.CancellationToken;
        var (kunduzgi, qurilma) = await TanaffusliXodimAsync(ct);
        var tungi = await TungiXodimAsync(qurilma, ct);
        Guid[] xodimlar = [kunduzgi, tungi];

        var birinchiSoat = new QotirilganSoat(new DateTimeOffset(2026, 9, 16, 7, 0, 0, TimeSpan.Zero));
        var ikkinchiSoat = new QotirilganSoat(new DateTimeOffset(2026, 9, 16, 7, 45, 0, TimeSpan.Zero));

        var birinchi = await QaytaHisoblaAsync(birinchiSoat, Dushanba, Dushanba.AddDays(6), xodimlar, ct);
        var oldin = await KunlarJsonAsync(xodimlar, ct);

        var ikkinchi = await QaytaHisoblaAsync(ikkinchiSoat, Dushanba, Dushanba.AddDays(6), xodimlar, ct);
        var keyin = await KunlarJsonAsync(xodimlar, ct);

        // Sinov sharti — ma'lumot bo'sh emas va holatlar xilma-xil.
        birinchi.ShouldBe(new AttendanceRecomputeOutcome(2, 13, 0));
        oldin.Count.ShouldBe(13);

        ikkinchi.ShouldBe(birinchi);
        keyin.ShouldBe(oldin);

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        (await db.AttendanceDays.AsNoTracking().Where(day => xodimlar.Contains(day.EmployeeId)).Select(day => day.ComputedAt).ToListAsync(ct))
            .ShouldAllBe(lahza => lahza == ikkinchiSoat.UtcNow);

        // Tungi smena qatorlari haqiqatan murakkab holatlar: reja tugashi ERTASIGA, sakrash sanalgan.
        var dushanbaTuni = (await KunAsync(db, tungi, Dushanba, ct)).ShouldNotBeNull();
        dushanbaTuni.WorkedMinutes.ShouldBe(735);
        dushanbaTuni.PlanEndAt.ShouldBe(new DateTimeOffset(Dushanba.AddDays(1).ToDateTime(new TimeOnly(8, 0)), Tashkent));

        var seshanbaTuni = (await KunAsync(db, tungi, Dushanba.AddDays(1), ct)).ShouldNotBeNull();
        seshanbaTuni.Status.ShouldBe(AttendanceDayStatus.MissingExit);
        seshanbaTuni.IgnoredPassCount.ShouldBe(1);

        (await KunAsync(db, tungi, Dushanba.AddDays(2), ct)).ShouldNotBeNull().PassCount.ShouldBe(2);
    }

    // ════════════════════════════════════════════════════════════
    //  Yordamchilar
    // ════════════════════════════════════════════════════════════

    /// <summary>2026-06-08 — dushanba (ISO 1); #50 va #64a haftasi.</summary>
    private static readonly DateOnly Dushanba = new(2026, 6, 8);

    private static DateOnly BugunToshkentda() => DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(Tashkent).DateTime);

    /// <summary>Qotirilgan soat — <c>computed_at</c> va «bugun» test vaqtiga bog'liq bo'lmasin.</summary>
    private sealed class QotirilganSoat(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;

        public DateOnly Today => utcNow.TashkentDate();
    }

    /// <summary>
    /// Qo'lda qayta hisoblash (<c>POST /recompute</c> navbatga qo'yadigan vazifaning o'zi) — qotirilgan soat bilan.
    /// </summary>
    /// <remarks>
    /// Endpoint orqali EMAS: <c>RateLimitPolicies.Export</c> (daqiqada 5) <c>/recompute</c> ga ham qo'llanadi
    /// va Testing'da ham yoqiq. Vazifa — o'sha yagona yozuvchi, faqat soati qotirilgan.
    /// </remarks>
    private async Task<AttendanceRecomputeOutcome> QaytaHisoblaAsync(
        IClock soat,
        DateOnly from,
        DateOnly to,
        IReadOnlyCollection<Guid> xodimlar,
        CancellationToken ct)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var yozuvchi = ActivatorUtilities.CreateInstance<AttendanceDayRecomputer>(qamrov.ServiceProvider, soat);

        return await yozuvchi.RecomputeAsync(xodimlar, AttendanceDayRecomputer.DaysBetween(from, to), ct);
    }

    /// <summary>
    /// Dushanba–juma 09:00–18:00 (tanaffus 60, oyna 13:00–14:00), shanba — xuddi shunday, lekin tanaffus 30;
    /// yakshanba dam. Bo'lim va lavozim bilan — surat ustunlari ham yoziladi. #51…#58c o'tishlari bilan.
    /// </summary>
    private async Task<(Guid Xodim, Guid Qurilma)> TanaffusliXodimAsync(CancellationToken ct)
    {
        var belgi = Guid.NewGuid().ToString("N")[..8];

        var firma = LegalEntity.Create("Tanaffus МЧЖ " + belgi).Value;
        var bolim = Department.Create("Tanaffus bo'limi " + belgi).Value;
        var lavozim = Position.Create("Tanaffus lavozimi " + belgi).Value;
        var xodim = Employee.Create(FullName.Create("Танаффус" + belgi, "Ходим", null).Value).Value;
        var munosabat = Employment.Hire(xodim.Id, firma.Id, new DateOnly(2025, 1, 1), bolim.Id, lavozim.Id).Value;

        WorkScheduleDayInput Kun(int indeks, int tanaffus)
            => new(indeks, true, new TimeOnly(9, 0), new TimeOnly(18, 0), tanaffus, new TimeOnly(13, 0), new TimeOnly(14, 0));

        var jadval = WorkSchedule.Create(
            "Tanaffus jadval " + belgi,
            [Kun(1, 60), Kun(2, 60), Kun(3, 60), Kun(4, 60), Kun(5, 60), Kun(6, 30), new WorkScheduleDayInput(7, IsWorking: false)]).Value;
        var biriktirma = ScheduleAssignment.Assign(jadval.Id, ScheduleScope.Employee, xodim.Id, new DateOnly(2026, 1, 1)).Value;
        var qurilma = await QurilmaAsync(ct);

        AttendancePass[] Juftlar(DateOnly sana, params (int Soat, int Daqiqa)[] vaqtlar)
            => [.. vaqtlar.Select((vaqt, i) => Hisoblangan(Otish(qurilma, xodim.Id, sana, vaqt.Soat, vaqt.Daqiqa, i % 2 == 0 ? PassDirection.In : PassDirection.Out)))];

        await SaqlaAsync(
            ct,
            [
                firma, bolim, lavozim, xodim, munosabat, jadval, biriktirma,
                .. Juftlar(Dushanba, (9, 0), (18, 0)),
                .. Juftlar(Dushanba.AddDays(1), (9, 0), (13, 0), (14, 0), (18, 0)),
                .. Juftlar(Dushanba.AddDays(2), (9, 0), (13, 0), (13, 30), (18, 0)),
                .. Juftlar(Dushanba.AddDays(3), (9, 0), (12, 55)),
                .. Juftlar(Dushanba.AddDays(4), (13, 30), (18, 0)),
                .. Juftlar(Dushanba.AddDays(5), (9, 0), (18, 0)),
            ]);

        return (xodim.Id, qurilma);
    }

    /// <summary>
    /// Har kuni 20:00–08:00 (oynasiz). Dushanba 19:52 → seshanba 08:07 (#39); seshanba 20:05 va 20:05:20
    /// (sakrash), chiqishsiz; chorshanba — yo'nalishi noma'lum ikki o'tish (19:58, payshanba 08:03).
    /// </summary>
    private async Task<Guid> TungiXodimAsync(Guid qurilma, CancellationToken ct)
    {
        var belgi = Guid.NewGuid().ToString("N")[..8];

        var firma = LegalEntity.Create("Tungi МЧЖ " + belgi).Value;
        var xodim = Employee.Create(FullName.Create("Тунги" + belgi, "Ходим", null).Value).Value;
        var munosabat = Employment.Hire(xodim.Id, firma.Id, new DateOnly(2025, 1, 1)).Value;
        var jadval = WorkSchedule.Create(
            "Tungi jadval " + belgi,
            [.. Enumerable.Range(1, 7).Select(kun => new WorkScheduleDayInput(kun, true, new TimeOnly(20, 0), new TimeOnly(8, 0)))]).Value;
        var biriktirma = ScheduleAssignment.Assign(jadval.Id, ScheduleScope.Employee, xodim.Id, new DateOnly(2026, 1, 1)).Value;

        var sakrash = Otish(qurilma, xodim.Id, Dushanba.AddDays(1), 20, 5, PassDirection.In);

        await SaqlaAsync(
            ct,
            firma, xodim, munosabat, jadval, biriktirma,
            Hisoblangan(Otish(qurilma, xodim.Id, Dushanba, 19, 52, PassDirection.In)),
            Hisoblangan(Otish(qurilma, xodim.Id, Dushanba.AddDays(1), 8, 7, PassDirection.Out)),
            Hisoblangan(sakrash),
            Hisoblangan(OtishLahzada(qurilma, xodim.Id, sakrash.OccurredAt.AddSeconds(20), PassDirection.In)),
            Hisoblangan(Otish(qurilma, xodim.Id, Dushanba.AddDays(2), 19, 58, PassDirection.Unknown)),
            Hisoblangan(Otish(qurilma, xodim.Id, Dushanba.AddDays(3), 8, 3, PassDirection.Unknown)));

        return xodim.Id;
    }

    /// <summary>Soniyagacha aniq lahzadagi bog'langan o'tish.</summary>
    private static AttendancePass OtishLahzada(Guid qurilma, Guid xodim, DateTimeOffset lahza, PassDirection yonalish)
    {
        var otish = AttendancePass.Record(
            qurilma,
            AttendanceIdentityProvider.Verifix,
            "p-" + Guid.NewGuid().ToString("N")[..10],
            lahza,
            lahza.ToUniversalTime().AddSeconds(3),
            Guid.NewGuid().ToString("N"),
            yonalish,
            yonalish == PassDirection.Unknown ? DirectionSource.Unknown : DirectionSource.Device,
            PassMethod.Face).Value;

        otish.Resolve(xodim, otish.ReceivedAt);
        return otish;
    }

    /// <summary>
    /// «Hisobga olingan» deb belgilaydi — iflos navbat testi butun bazani bo'shatadi va bu o'tishlarni
    /// o'z soati bilan qayta hisoblab yurmasin.
    /// </summary>
    private static AttendancePass Hisoblangan(AttendancePass otish)
    {
        otish.MarkProcessed(otish.ReceivedAt.AddMinutes(1));
        return otish;
    }

    /// <summary>
    /// Xodimlarning kun qatorlari JSON ko'rinishida — <c>computed_at</c> dan boshqa HAR BIR ustun (identifikator
    /// ham; keyin qo'shilgan ustun ham avtomatik kiradi), tartiblangan.
    /// </summary>
    private async Task<List<string>> KunlarJsonAsync(Guid[] xodimlar, CancellationToken ct)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();

        var qatorlar = await qamrov.ServiceProvider.GetRequiredService<AppDbContext>().Database
            .SqlQuery<string>($"SELECT (to_jsonb(d) - 'computed_at')::text AS \"Value\" FROM attendance_days AS d WHERE d.employee_id = ANY({xodimlar})")
            .ToListAsync(ct);

        return [.. qatorlar.Order(StringComparer.Ordinal)];
    }

    /// <summary>Munosabati va har kuni 09:00–18:00 (tanaffussiz) jadvali bor xodim.</summary>
    private async Task<Guid> XodimAsync(string harf, CancellationToken ct)
    {
        var belgi = harf + Guid.NewGuid().ToString("N")[..8];

        var firma = LegalEntity.Create("Fon МЧЖ " + belgi).Value;
        var xodim = Employee.Create(FullName.Create("Фон" + belgi, "Ходим", null).Value).Value;
        var munosabat = Employment.Hire(xodim.Id, firma.Id, new DateOnly(2025, 1, 1)).Value;
        var jadval = WorkSchedule.Create(
            "Fon jadval " + belgi,
            [.. Enumerable.Range(1, 7).Select(kun => new WorkScheduleDayInput(kun, true, new TimeOnly(9, 0), new TimeOnly(18, 0)))]).Value;
        var biriktirma = ScheduleAssignment.Assign(jadval.Id, ScheduleScope.Employee, xodim.Id, new DateOnly(2025, 1, 1)).Value;

        await SaqlaAsync(ct, firma, xodim, munosabat, jadval, biriktirma);

        return xodim.Id;
    }

    private async Task<Guid> QurilmaAsync(CancellationToken ct)
    {
        var qurilma = AttendanceDevice.Register(
            "Fon turniket " + Guid.NewGuid().ToString("N")[..8],
            RandomNumberGenerator.GetBytes(32),
            "tst00000",
            DateTimeOffset.UtcNow).Value;

        await SaqlaAsync(ct, qurilma);

        return qurilma.Id;
    }

    /// <summary>Qabul qilingan va hali hisoblanmagan o'tish; <paramref name="xodim"/> <c>null</c> — bog'lanmagan.</summary>
    private static AttendancePass Otish(
        Guid qurilma,
        Guid? xodim,
        DateOnly sana,
        int soat,
        int daqiqa,
        PassDirection yonalish,
        string? shaxs = null)
    {
        // Qabul va bog'lash lahzasi UTC da — qabul yo'lidagi soat kabi (timestamptz faqat UTC ni yozadi).
        var lahza = new DateTimeOffset(sana.ToDateTime(new TimeOnly(soat, daqiqa)), Tashkent);
        var qabul = lahza.ToUniversalTime().AddSeconds(3);

        var otish = AttendancePass.Record(
            qurilma,
            AttendanceIdentityProvider.Verifix,
            shaxs ?? "p-" + Guid.NewGuid().ToString("N")[..10],
            lahza,
            qabul,
            Guid.NewGuid().ToString("N"),
            yonalish,
            DirectionSource.Device,
            PassMethod.Face).Value;

        if (xodim is { } bogliq)
        {
            otish.Resolve(bogliq, qabul);
        }

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

    private static Task<AttendanceDay?> KunAsync(AppDbContext db, Guid xodim, DateOnly sana, CancellationToken ct)
        => db.AttendanceDays.AsNoTracking().SingleOrDefaultAsync(day => day.EmployeeId == xodim && day.LocalDate == sana, ct);

    private async Task<List<DateTimeOffset>> HisoblanganVaqtlarAsync(Guid xodim, CancellationToken ct)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();

        return await qamrov.ServiceProvider.GetRequiredService<AppDbContext>().AttendanceDays.AsNoTracking()
            .Where(day => day.EmployeeId == xodim)
            .OrderBy(day => day.LocalDate)
            .Select(day => day.ComputedAt)
            .ToListAsync(ct);
    }

    /// <summary>Qayta hisoblovchi — berilgan saqlash chegarasi bilan, qolgani qamrovdan.</summary>
    private static AttendanceDayRecomputer Yozuvchi(IServiceProvider qamrov, IUnitOfWork saqlash)
        => new(
            qamrov.GetRequiredService<IAttendanceComputationSource>(),
            qamrov.GetRequiredService<IAttendanceDayRepository>(),
            saqlash,
            qamrov.GetRequiredService<IDatabaseFault>(),
            qamrov.GetRequiredService<AttendanceRetention>(),
            qamrov.GetRequiredService<IClock>());

    /// <summary>
    /// Birinchi saqlashdan AYNAN oldin boshqa yozuvchini to'liq yurgizadi — poygani
    /// uxlab-kutishsiz, har safar bir xil takrorlaydi.
    /// </summary>
    private sealed class OraliqdaYozuvchi(IUnitOfWork asl, Func<Task> birinchiSaqlashdanOldin) : IUnitOfWork
    {
        public int Chaqiruvlar { get; private set; }

        public List<Exception> Xatolar { get; } = [];

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            Chaqiruvlar++;

            if (Chaqiruvlar == 1)
            {
                await birinchiSaqlashdanOldin();
            }

            try
            {
                return await asl.SaveChangesAsync(cancellationToken);
            }
            catch (Exception xato)
            {
                Xatolar.Add(xato);
                throw;
            }
        }
    }
}
