using System.Globalization;
using System.Security.Cryptography;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Recompute;
using Bonvi.Application.Attendance.Retention;
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
/// Saqlash muddati vazifalari — surat tozalash, o'tish tozalash va muzlatilgan kunlar
/// (SPEC §3.10, §9.2.1, §11.1 #65–#70, B7).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ Bu sinfning eng muhim testi —
/// <see cref="Otish_tozalash_eski_otishni_va_faylini_ochiradi_kun_yozuvi_aynan_qoladi"/>:
/// o'tish o'chirilgach uning kuni BAYT-BAYT o'zgarmasligi. Aks holda bir yillik
/// tabel jimgina nolga aylanadi.
/// </para>
/// <para>
/// Sozlamalar va muzlatish chegarasi butun bazaga tegishli, shu sababli har bir
/// test o'ziga kerakli qiymatni O'ZI qo'yadi va chegarani tozalaydi — testlar
/// tartibiga tayanilmaydi.
/// </para>
/// </remarks>
public sealed class AttendanceRetentionJobsTests : IClassFixture<BonviApiFactory>
{
    private static readonly TimeSpan Tashkent = TimeSpan.FromHours(5);

    private readonly BonviApiFactory _factory;

    public AttendanceRetentionJobsTests(BonviApiFactory factory) => _factory = factory;

    // ════════════════════════════════════════════════════════════
    //  Surat tozalash — §3.10
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Sozlama yozuvi YO'Q — sukut 60 kun. 61 kunlik surat: fayl o'chadi, <c>photo_key</c> va
    /// <c>raw_event</c> NULL, qator qoladi. 59 kunlik surat tegilmaydi. Ikkinchi yurish zararsiz.
    /// </summary>
    [Fact]
    public async Task Surat_tozalash_eski_faylni_ochiradi_qatorni_qoldiradi_sozlamasiz_sukut_60_kun()
    {
        var ct = TestContext.Current.CancellationToken;
        await SozlamaAsync(SystemSettingKeys.PhotoRetentionDays, null, ct);
        await SozlamaAsync(SystemSettingKeys.PassRetentionDays, null, ct);

        var qurilma = await QurilmaAsync(ct);
        var eski = await SuratliOtishAsync(qurilma, null, DateTimeOffset.UtcNow.AddDays(-61), processed: true, ct);
        var yangi = await SuratliOtishAsync(qurilma, null, DateTimeOffset.UtcNow.AddDays(-59), processed: true, ct);

        var jurnal = new YozibOluvchiJurnal();
        await VazifaAsync<AttendancePhotoPurgeJob>(jurnal, job => job.RunAsync(ct));

        var qator = await OtishAsync(eski.Id, ct);
        qator.ShouldNotBeNull("surat tozalash o'tish QATORINI o'chirmaydi");
        qator.PhotoKey.ShouldBeNull();
        qator.RawEvent.ShouldBeNull();
        qator.PhotoPurgedAt.ShouldNotBeNull();
        (await FaylBormiAsync(eski.PhotoKey!, ct)).ShouldBeFalse();

        var tegilmagan = await OtishAsync(yangi.Id, ct);
        tegilmagan.ShouldNotBeNull().PhotoKey.ShouldBe(yangi.PhotoKey);
        tegilmagan.RawEvent.ShouldNotBeNull();
        (await FaylBormiAsync(yangi.PhotoKey!, ct)).ShouldBeTrue();

        var hisobot = jurnal.Yozuvlar.OfType<PhotoPurgeReport>().ShouldHaveSingleItem();
        hisobot.RetentionDays.ShouldBe(AttendanceRules.DefaultPhotoRetentionDays);
        hisobot.PurgedPhotos.ShouldBeGreaterThanOrEqualTo(1);
        hisobot.Completed.ShouldBeTrue();

        var ikkinchi = new YozibOluvchiJurnal();
        await VazifaAsync<AttendancePhotoPurgeJob>(ikkinchi, job => job.RunAsync(ct));
        ikkinchi.Yozuvlar.OfType<PhotoPurgeReport>().ShouldHaveSingleItem().PurgedPhotos.ShouldBe(0);
        (await OtishAsync(yangi.Id, ct)).ShouldNotBeNull().PhotoKey.ShouldBe(yangi.PhotoKey);
    }

    // ════════════════════════════════════════════════════════════
    //  O'tish tozalash — §9.2.1
    // ════════════════════════════════════════════════════════════

    /// <summary>#65: <c>passRetentionDays = 0</c> — o'n yillik o'tish ham, uning fayli ham qoladi.</summary>
    [Fact]
    public async Task Otish_muddati_0_bolsa_hech_narsa_ochirilmaydi()
    {
        var ct = TestContext.Current.CancellationToken;
        await SozlamaAsync(SystemSettingKeys.PhotoRetentionDays, 60, ct);
        await SozlamaAsync(SystemSettingKeys.PassRetentionDays, 0, ct);

        var qurilma = await QurilmaAsync(ct);
        var qadimgi = await SuratliOtishAsync(qurilma, null, DateTimeOffset.UtcNow.AddYears(-10), processed: true, ct);

        var jurnal = new YozibOluvchiJurnal();
        await VazifaAsync<AttendancePassPurgeJob>(jurnal, job => job.RunAsync(ct));

        (await OtishAsync(qadimgi.Id, ct)).ShouldNotBeNull();
        (await FaylBormiAsync(qadimgi.PhotoKey!, ct)).ShouldBeTrue();
        jurnal.Yozuvlar.ShouldHaveSingleItem().ShouldBe(nameof(IAttendanceJobJournal.PassPurgeDisabled));
    }

    /// <summary>
    /// #66, #67: muddat 365 — 400 kunlik o'tish VA uning fayli o'chadi, 300 kunligi qoladi;
    /// o'sha sananing <c>attendance_days</c> qatori har bir ustuni bilan AYNAN qoladi.
    /// </summary>
    [Fact]
    public async Task Otish_tozalash_eski_otishni_va_faylini_ochiradi_kun_yozuvi_aynan_qoladi()
    {
        var ct = TestContext.Current.CancellationToken;
        await SozlamaAsync(SystemSettingKeys.PhotoRetentionDays, 60, ct);
        await SozlamaAsync(SystemSettingKeys.PassRetentionDays, 365, ct);
        await MuzlatishniTozalashAsync(ct);

        var xodim = await XodimAsync(ct);
        var qurilma = await QurilmaAsync(ct);

        var lahza = DateTimeOffset.UtcNow.AddDays(-400);
        var eski = await SuratliOtishAsync(qurilma, xodim, lahza, processed: true, ct);
        var saqlanadigan = await SuratliOtishAsync(qurilma, xodim, DateTimeOffset.UtcNow.AddDays(-300), processed: true, ct);

        var kun = AttendanceDay.Compute(xodim, eski.LocalDate, KeldiFaktlari(eski.LocalDate), DateTimeOffset.UtcNow.AddDays(-399));
        await SaqlaAsync(ct, kun);
        var oldin = await KunNusxasiAsync(xodim, eski.LocalDate, ct);

        var jurnal = new YozibOluvchiJurnal();
        await VazifaAsync<AttendancePassPurgeJob>(jurnal, job => job.RunAsync(ct));

        (await OtishAsync(eski.Id, ct)).ShouldBeNull("400 kunlik o'tish o'chishi kerak");
        (await FaylBormiAsync(eski.PhotoKey!, ct)).ShouldBeFalse("fayl qatordan OLDIN o'chiriladi — yetim qolmaydi");

        (await OtishAsync(saqlanadigan.Id, ct)).ShouldNotBeNull();
        (await FaylBormiAsync(saqlanadigan.PhotoKey!, ct)).ShouldBeTrue();

        // ⚠️ Eng muhim tekshiruv: kun yozuvi BAYT-BAYT o'zgarmagan.
        var keyin = await KunNusxasiAsync(xodim, eski.LocalDate, ct);
        keyin.ShouldNotBeNull();
        keyin.ShouldBe(oldin);

        var hisobot = jurnal.Yozuvlar.OfType<PassPurgeReport>().ShouldHaveSingleItem();
        hisobot.RetentionDays.ShouldBe(365);
        hisobot.DeletedPasses.ShouldBeGreaterThanOrEqualTo(1);
        hisobot.DeletedFiles.ShouldBeGreaterThanOrEqualTo(1);
        hisobot.Completed.ShouldBeTrue();

        (await MuzlaganSanaAsync(ct)).ShouldBe(BugunToshkentda().AddDays(-365));
    }

    /// <summary>
    /// Muzlatish: tozalashdan keyin qo'lda qayta hisoblash ham, chegara yonidagi o'tishning ±1
    /// qo'shnisi ham o'chirilgan kunni NOLGA qayta yozmaydi.
    /// </summary>
    [Fact]
    public async Task Tozalangan_kun_qolda_ham_iflos_navbatning_qoshnisi_sifatida_ham_qayta_hisoblanmaydi()
    {
        var ct = TestContext.Current.CancellationToken;
        await SozlamaAsync(SystemSettingKeys.PhotoRetentionDays, 60, ct);
        await SozlamaAsync(SystemSettingKeys.PassRetentionDays, 365, ct);
        await MuzlatishniTozalashAsync(ct);

        var xodim = await XodimAsync(ct);
        var qurilma = await QurilmaAsync(ct);

        var chegara = BugunToshkentda().AddDays(-365);
        var ochadigan = await SuratliOtishAsync(qurilma, xodim, Tush(chegara.AddDays(-1)), processed: true, ct);

        // Chegara kunining yozuvi — oldingi kungi (o'chadigan) tungi o'tishlardan hisoblangan deb faraz.
        var chegaraKuni = AttendanceDay.Compute(xodim, chegara, KeldiFaktlari(chegara), DateTimeOffset.UtcNow.AddDays(-360));
        await SaqlaAsync(ct, chegaraKuni);
        var oldin = await KunNusxasiAsync(xodim, chegara, ct);

        await VazifaAsync<AttendancePassPurgeJob>(new YozibOluvchiJurnal(), job => job.RunAsync(ct));
        (await OtishAsync(ochadigan.Id, ct)).ShouldBeNull();
        (await MuzlaganSanaAsync(ct)).ShouldBe(chegara);

        // (1) Qo'lda qayta hisoblash (§5.8) — muzlagan sana hisoblanmaydi.
        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var natija = await qamrov.ServiceProvider.GetRequiredService<AttendanceRecomputeRangeJob>()
                .ExecuteAsync(chegara.AddDays(-2), chegara, xodim, ct);

            natija.FrozenDates.ShouldBe(3);
            natija.StoredDays.ShouldBe(0);
        }

        (await KunNusxasiAsync(xodim, chegara, ct)).ShouldBe(oldin);

        // (2) Chegaradan KEYINGI kungi kutayotgan o'tish — iflos navbat ±1 ga kengaytiradi,
        // ya'ni chegara kunining o'zini ham so'raydi.
        var ertasi = await SuratliOtishAsync(qurilma, xodim, Tush(chegara.AddDays(1)), processed: false, ct);

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            await qamrov.ServiceProvider.GetRequiredService<AttendanceDirtyRecomputeJob>().RunAsync(ct);
        }

        (await KunNusxasiAsync(xodim, chegara, ct)).ShouldBe(oldin, "muzlagan qo'shni qayta yozilmasligi kerak");
        (await OtishAsync(ertasi.Id, ct)).ShouldNotBeNull().ProcessedAt.ShouldNotBeNull();

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.AttendanceDays.AsNoTracking().SingleOrDefaultAsync(day => day.EmployeeId == xodim && day.LocalDate == chegara.AddDays(1), ct))
                .ShouldNotBeNull("muzlamagan kun odatdagidek hisoblanadi")
                .PassCount.ShouldBe(1);
        }
    }

    /// <summary>
    /// #68 (AMENDED 2026-09-17 — qoida o'zgargan, tekshiruv yumshatilmagan): 400 kunlik, xodimga
    /// BOG'LANGAN, lekin hali hisobga olinmagan o'tish o'chirilmaydi va ogohlantirish yoziladi;
    /// BOG'LANMAGAN 400 kunlik o'tish esa surat fayli bilan birga O'CHIRILADI — sanasi muzlagan,
    /// uni hech qanday moslik endi tabelga qo'sha olmaydi.
    /// </summary>
    [Fact]
    public async Task Boglangan_hisobga_olinmagan_eski_otish_qoladi_boglanmagani_fayli_bilan_ochiriladi()
    {
        var ct = TestContext.Current.CancellationToken;
        await SozlamaAsync(SystemSettingKeys.PhotoRetentionDays, 60, ct);
        await SozlamaAsync(SystemSettingKeys.PassRetentionDays, 365, ct);

        var xodim = await XodimAsync(ct);
        var qurilma = await QurilmaAsync(ct);

        var kutayotgan = await SuratliOtishAsync(qurilma, xodim, DateTimeOffset.UtcNow.AddDays(-400), processed: false, ct);
        var boglanmagan = await SuratliOtishAsync(qurilma, null, DateTimeOffset.UtcNow.AddDays(-400), processed: false, ct);
        var hisoblangan = await SuratliOtishAsync(qurilma, xodim, DateTimeOffset.UtcNow.AddDays(-400), processed: true, ct);

        var jurnal = new YozibOluvchiJurnal();
        await VazifaAsync<AttendancePassPurgeJob>(jurnal, job => job.RunAsync(ct));

        // Buzilgan navbat belgisi — qoladi, fayli bilan.
        (await OtishAsync(kutayotgan.Id, ct)).ShouldNotBeNull();
        (await FaylBormiAsync(kutayotgan.PhotoKey!, ct)).ShouldBeTrue();

        // Moslanmagan eski o'tish — qator ham, fayl ham yo'q.
        (await OtishAsync(boglanmagan.Id, ct)).ShouldBeNull();
        (await FaylBormiAsync(boglanmagan.PhotoKey!, ct)).ShouldBeFalse();

        (await OtishAsync(hisoblangan.Id, ct)).ShouldBeNull();
        (await FaylBormiAsync(hisoblangan.PhotoKey!, ct)).ShouldBeFalse();

        jurnal.Yozuvlar.OfType<KutayotganQoldi>().ShouldHaveSingleItem().Soni.ShouldBeGreaterThanOrEqualTo(1);
        jurnal.Yozuvlar.OfType<PassPurgeReport>().ShouldHaveSingleItem().Completed.ShouldBeTrue();
    }

    /// <summary>
    /// #70: juftlik qo'lda teskari yozilgan (o'tish 30 &lt; surat 60) — vazifa <c>max</c> ni oladi:
    /// 45 kunlik o'tish surati bilan qoladi, 70 kunligi fayli bilan o'chadi.
    /// </summary>
    [Fact]
    public async Task Teskari_yozilgan_muddatlarda_katta_muddat_ishlatiladi_va_fayl_yetim_qolmaydi()
    {
        var ct = TestContext.Current.CancellationToken;
        await SozlamaAsync(SystemSettingKeys.PhotoRetentionDays, 60, ct);
        await SozlamaAsync(SystemSettingKeys.PassRetentionDays, 30, ct);

        var qurilma = await QurilmaAsync(ct);
        var qirqBesh = await SuratliOtishAsync(qurilma, null, Tush(BugunToshkentda().AddDays(-45)), processed: true, ct);
        var yetmish = await SuratliOtishAsync(qurilma, null, Tush(BugunToshkentda().AddDays(-70)), processed: true, ct);

        var jurnal = new YozibOluvchiJurnal();
        await VazifaAsync<AttendancePassPurgeJob>(jurnal, job => job.RunAsync(ct));

        (await OtishAsync(qirqBesh.Id, ct)).ShouldNotBeNull();
        (await FaylBormiAsync(qirqBesh.PhotoKey!, ct)).ShouldBeTrue();
        (await OtishAsync(yetmish.Id, ct)).ShouldBeNull();
        (await FaylBormiAsync(yetmish.PhotoKey!, ct)).ShouldBeFalse();

        jurnal.Yozuvlar.OfType<PassPurgeReport>().ShouldHaveSingleItem().RetentionDays.ShouldBe(60);

        await MuzlatishniTozalashAsync(ct);
    }

    /// <summary>
    /// To'plamlar: bir yurishda <see cref="AttendancePassPurgeJob.BatchSize"/> dan ko'p qator o'chadi;
    /// fayli o'chmagan o'tish QOLADI (fayl yetim bo'lmasin) va aylanib yurishga sabab bo'lmaydi.
    /// </summary>
    [Fact]
    public async Task Otish_tozalash_toplamlab_ishlaydi_va_fayli_ochmagan_qatorni_qoldiradi()
    {
        var ct = TestContext.Current.CancellationToken;
        await SozlamaAsync(SystemSettingKeys.PhotoRetentionDays, 60, ct);
        await SozlamaAsync(SystemSettingKeys.PassRetentionDays, 365, ct);

        var qurilma = await QurilmaAsync(ct);
        var sana = BugunToshkentda().AddDays(-500);
        var soni = AttendancePassPurgeJob.BatchSize + 50;

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AddRange(Enumerable.Range(0, soni).Select(i => Otish(qurilma, null, Tush(sana).AddSeconds(i), processed: true)));
            await db.SaveChangesAsync(ct);
        }

        var ochmaydigan = await SuratliOtishAsync(qurilma, null, Tush(sana.AddDays(-1)), processed: true, ct);

        var jurnal = new YozibOluvchiJurnal();
        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var saqlash = new BuzuqFayl(qamrov.ServiceProvider.GetRequiredService<IFileStorage>(), ochmaydigan.PhotoKey!);
            var vazifa = ActivatorUtilities.CreateInstance<AttendancePassPurgeJob>(qamrov.ServiceProvider, jurnal, saqlash);
            await vazifa.RunAsync(ct);
        }

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.AttendancePasses.AsNoTracking().CountAsync(pass => pass.DeviceId == qurilma, ct)).ShouldBe(1);
        }

        (await OtishAsync(ochmaydigan.Id, ct)).ShouldNotBeNull().PhotoKey.ShouldBe(ochmaydigan.PhotoKey);
        (await FaylBormiAsync(ochmaydigan.PhotoKey!, ct)).ShouldBeTrue();

        var hisobot = jurnal.Yozuvlar.OfType<PassPurgeReport>().ShouldHaveSingleItem();
        hisobot.DeletedPasses.ShouldBeGreaterThanOrEqualTo(soni);
        hisobot.FailedFiles.ShouldBe(1);
    }

    // ════════════════════════════════════════════════════════════
    //  Yordamchilar
    // ════════════════════════════════════════════════════════════

    private static DateOnly BugunToshkentda() => DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(Tashkent).DateTime);

    /// <summary>Toshkent vaqti bilan shu kunning tushi.</summary>
    private static DateTimeOffset Tush(DateOnly sana) => new(sana.ToDateTime(new TimeOnly(12, 0)), Tashkent);

    /// <summary>Kun yozuvining barcha saqlangan ustunlari — «aynan qoldi» tekshiruvi uchun.</summary>
    private sealed record KunNusxasi(
        Guid Id,
        AttendanceDayStatus Status,
        int PlannedMinutes,
        int WorkedMinutes,
        int LateMinutes,
        int EarlyLeaveMinutes,
        int OvertimeMinutes,
        int BreakTakenMinutes,
        int BreakDeductionMinutes,
        int PassCount,
        int IgnoredPassCount,
        DateTimeOffset? FirstInAt,
        DateTimeOffset? LastOutAt,
        DateTimeOffset ComputedAt);

    private async Task<KunNusxasi?> KunNusxasiAsync(Guid xodim, DateOnly sana, CancellationToken ct)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();

        return await qamrov.ServiceProvider.GetRequiredService<AppDbContext>().AttendanceDays.AsNoTracking()
            .Where(day => day.EmployeeId == xodim && day.LocalDate == sana)
            .Select(day => new KunNusxasi(
                day.Id,
                day.Status,
                day.PlannedMinutes,
                day.WorkedMinutes,
                day.LateMinutes,
                day.EarlyLeaveMinutes,
                day.OvertimeMinutes,
                day.BreakTakenMinutes,
                day.BreakDeductionMinutes,
                day.PassCount,
                day.IgnoredPassCount,
                day.FirstInAt,
                day.LastOutAt,
                day.ComputedAt))
            .SingleOrDefaultAsync(ct);
    }

    /// <summary>To'liq kelgan kun: 09:07 – 18:20, 7 daqiqa kechikkan, 13 daqiqa ortiqcha.</summary>
    private static AttendanceDayFacts KeldiFaktlari(DateOnly sana)
    {
        var kirdi = new DateTimeOffset(sana.ToDateTime(new TimeOnly(9, 7)), Tashkent);
        var chiqdi = new DateTimeOffset(sana.ToDateTime(new TimeOnly(18, 20)), Tashkent);

        return new AttendanceDayFacts(
            EmploymentId: null,
            LegalEntityId: null,
            DepartmentId: null,
            PositionId: null,
            WorkScheduleId: null,
            PlanStartAt: null,
            PlanEndAt: null,
            FirstInAt: kirdi.ToUniversalTime(),
            LastOutAt: chiqdi.ToUniversalTime(),
            PlannedMinutes: 540,
            WorkedMinutes: 553,
            LateMinutes: 7,
            EarlyLeaveMinutes: 0,
            OvertimeMinutes: 13,
            BreakTakenMinutes: 0,
            BreakDeductionMinutes: 0,
            PassCount: 2,
            IgnoredPassCount: 0,
            Status: AttendanceDayStatus.Present);
    }

    /// <summary>Munosabati va har kuni 09:00–18:00 jadvali bor xodim — qayta hisoblansa, kun «Kelmadi» bo'lardi.</summary>
    private async Task<Guid> XodimAsync(CancellationToken ct)
    {
        var belgi = Guid.NewGuid().ToString("N")[..8];

        var firma = LegalEntity.Create("Saqlash МЧЖ " + belgi).Value;
        var xodim = Employee.Create(FullName.Create("Сақлаш" + belgi, "Ходим", null).Value).Value;
        var munosabat = Employment.Hire(xodim.Id, firma.Id, new DateOnly(2020, 1, 1)).Value;
        var jadval = WorkSchedule.Create(
            "Saqlash jadval " + belgi,
            [.. Enumerable.Range(1, 7).Select(kun => new WorkScheduleDayInput(kun, true, new TimeOnly(9, 0), new TimeOnly(18, 0)))]).Value;
        var biriktirma = ScheduleAssignment.Assign(jadval.Id, ScheduleScope.Employee, xodim.Id, new DateOnly(2020, 1, 1)).Value;

        await SaqlaAsync(ct, firma, xodim, munosabat, jadval, biriktirma);

        return xodim.Id;
    }

    private async Task<Guid> QurilmaAsync(CancellationToken ct)
    {
        var qurilma = AttendanceDevice.Register(
            "Saqlash turniket " + Guid.NewGuid().ToString("N")[..8],
            RandomNumberGenerator.GetBytes(32),
            "tst00000",
            DateTimeOffset.UtcNow).Value;

        await SaqlaAsync(ct, qurilma);

        return qurilma.Id;
    }

    /// <summary>Suratsiz o'tish — qabul va o'tish lahzasi bir xil (soat og'ishi yo'q).</summary>
    private static AttendancePass Otish(Guid qurilma, Guid? xodim, DateTimeOffset lahza, bool processed, string? suratKaliti = null)
    {
        // timestamptz faqat UTC ni yozadi — qabul yo'lidagi soat ham UTC beradi.
        lahza = lahza.ToUniversalTime();

        var otish = AttendancePass.Record(
            qurilma,
            AttendanceIdentityProvider.Verifix,
            "r-" + Guid.NewGuid().ToString("N")[..10],
            lahza,
            lahza,
            Guid.NewGuid().ToString("N"),
            PassDirection.In,
            DirectionSource.Device,
            PassMethod.Face,
            photoKey: suratKaliti,
            rawEvent: suratKaliti is null ? null : """{"name":"Sinov Odam"}""").Value;

        if (xodim is { } bogliq)
        {
            otish.Resolve(bogliq, lahza);
        }

        if (processed)
        {
            otish.MarkProcessed(lahza.AddMinutes(10));
        }

        return otish;
    }

    /// <summary>Diskda haqiqiy fayli bor o'tish.</summary>
    private async Task<AttendancePass> SuratliOtishAsync(
        Guid qurilma,
        Guid? xodim,
        DateTimeOffset lahza,
        bool processed,
        CancellationToken ct)
    {
        string kalit;

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            await using var baytlar = new MemoryStream(TestImages.JpegYasa(16, 16, 80));
            kalit = (await qamrov.ServiceProvider.GetRequiredService<IFileStorage>().SaveAsync(baytlar, "otish.jpg", "image/jpeg", ct)).Key;
        }

        var otish = Otish(qurilma, xodim, lahza, processed, kalit);
        await SaqlaAsync(ct, otish);

        return otish;
    }

    private async Task SaqlaAsync(CancellationToken ct, params object[] yozuvlar)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        db.AddRange(yozuvlar);
        await db.SaveChangesAsync(ct);
    }

    private async Task<AttendancePass?> OtishAsync(Guid id, CancellationToken ct)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();

        return await qamrov.ServiceProvider.GetRequiredService<AppDbContext>().AttendancePasses.AsNoTracking()
            .SingleOrDefaultAsync(pass => pass.Id == id, ct);
    }

    private async Task<bool> FaylBormiAsync(string kalit, CancellationToken ct)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        await using var oqim = await qamrov.ServiceProvider.GetRequiredService<IFileStorage>().OpenAsync(kalit, ct);

        return oqim is not null;
    }

    /// <summary>Sonli sozlamani yozadi; <c>null</c> — yozuvni o'chiradi (sukut qiymat ishlaydi).</summary>
    private async Task SozlamaAsync(string kalit, int? qiymat, CancellationToken ct)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
        var himoya = qamrov.ServiceProvider.GetRequiredService<ISecretProtector>();

        var qator = await db.SystemSettings.SingleOrDefaultAsync(setting => setting.Key == kalit, ct);
        var matn = qiymat?.ToString(CultureInfo.InvariantCulture);

        if (matn is null)
        {
            if (qator is not null)
            {
                db.SystemSettings.Remove(qator);
            }
        }
        else if (qator is null)
        {
            db.SystemSettings.Add(SystemSetting.Create(kalit, matn, himoya).Value);
        }
        else
        {
            qator.SetValue(matn, himoya).IsSuccess.ShouldBeTrue();
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task MuzlatishniTozalashAsync(CancellationToken ct)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.SystemSettings
            .Where(setting => setting.Key == SystemSettingKeys.AttendanceFrozenThrough)
            .ExecuteDeleteAsync(ct);
    }

    private async Task<DateOnly?> MuzlaganSanaAsync(CancellationToken ct)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();

        return await qamrov.ServiceProvider.GetRequiredService<AttendanceRetention>().FrozenThroughAsync(ct);
    }

    /// <summary>Vazifani o'z qamrovida, yozib oluvchi jurnal bilan yurgizadi.</summary>
    private async Task VazifaAsync<TVazifa>(YozibOluvchiJurnal jurnal, Func<TVazifa, Task> yurgiz)
        where TVazifa : class
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();

        await yurgiz(ActivatorUtilities.CreateInstance<TVazifa>(qamrov.ServiceProvider, jurnal));
    }

    /// <summary>Jurnalga «bog'langan-u hisobga olinmagan o'tishlar qoldirildi» yozuvi.</summary>
    private sealed record KutayotganQoldi(int Soni);

    /// <summary>Jurnal yozuvlarini tekshirish uchun saqlaydi.</summary>
    private sealed class YozibOluvchiJurnal : IAttendanceJobJournal
    {
        public List<object> Yozuvlar { get; } = [];

        public void DirtyRecomputeFinished(DirtyRecomputeReport report) => Yozuvlar.Add(report);

        public void NightlyRecomputeFinished(DateOnly from, DateOnly to, AttendanceRecomputeOutcome outcome) => Yozuvlar.Add(outcome);

        public void PhotoPurgeFinished(PhotoPurgeReport report) => Yozuvlar.Add(report);

        public void PassPurgeDisabled() => Yozuvlar.Add(nameof(PassPurgeDisabled));

        public void ExpiredPendingPassesKept(DateOnly localDateBefore, int count) => Yozuvlar.Add(new KutayotganQoldi(count));

        public void PassPurgeFinished(PassPurgeReport report) => Yozuvlar.Add(report);
    }

    /// <summary>Bitta kalitni o'chira olmaydigan saqlash — ruxsat xatosi bilan tiklangan faylga o'xshab.</summary>
    private sealed class BuzuqFayl(IFileStorage asl, string buzuqKalit) : IFileStorage
    {
        public Task<StoredFile> SaveAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken = default)
            => asl.SaveAsync(content, fileName, contentType, cancellationToken);

        public Task<Stream?> OpenAsync(string key, CancellationToken cancellationToken = default)
            => asl.OpenAsync(key, cancellationToken);

        public Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
            => key == buzuqKalit
                ? throw new UnauthorizedAccessException("sinov: ruxsat yo'q")
                : asl.DeleteAsync(key, cancellationToken);
    }
}
