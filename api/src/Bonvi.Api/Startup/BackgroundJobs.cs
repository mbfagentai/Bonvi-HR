using Bonvi.Application.Attendance.Recompute;
using Bonvi.Application.Attendance.Retention;
using Bonvi.Application.Blanks;
using Bonvi.Application.Employees.Probations;
using Bonvi.Application.Identity.EmployeeAccounts;
using Bonvi.Application.Processes.Engine;
using Bonvi.Infrastructure;
using Bonvi.Infrastructure.Services;
using Hangfire;

namespace Bonvi.Api.Startup;

/// <summary>
/// Fon vazifalari: Hangfire serveri, navbat porti va takroriy jadval (SPEC §9).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Bitta shart ikkalasini hal qiladi.</b> <c>Testing</c> muhitida Hangfire
/// UMUMAN ulanmaydi va navbat sinxron (<c>InlineBackgroundJobQueue</c>) —
/// integratsiya testida fon oqimi bazani tekshiruv o'rtasida o'zgartirmasligi,
/// «moslik yaratdim → katak paydo bo'ldi» esa kutishsiz aniq bo'lishi uchun.
/// Qolgan HAMMA muhitda (Development ham) — Hangfire serveri va Hangfire
/// navbati. Navbat va server bitta <c>if</c> da: biri bor-u ikkinchisi yo'q
/// holat tug'ilmaydi.
/// </para>
/// <para>
/// ⚠️ <b>Dashboard marshruti YO'Q</b> (SPEC §1.2 #12). U sukut bo'yicha
/// himoyasiz admin sahifa, vazifa argumentlarida esa qurilmadagi shaxs raqami
/// bor. Vazifalar holati jurnalda (<c>AttendanceJobJournal</c>, EventId 5300+).
/// </para>
/// <para>
/// <b><c>WorkerCount = 2</c>.</b> Sukut ishchilar soni protsessor soniga qarab
/// o'sadi (20 tagacha), production VPS esa 2 GB xotirali. Bu yukka ikki ishchi
/// yetarli: takroriy vazifalar kechasi, fon ishi esa kun bo'yi kam.
/// </para>
/// </remarks>
public static class BackgroundJobs
{
    /// <summary>Hangfire ulanmaydigan muhit nomi (<c>BonviApiFactory</c> shuni qo'yadi).</summary>
    public const string TestingEnvironment = "Testing";

    /// <summary>Iflos navbat (SPEC §9.3).</summary>
    public const string RecomputeDirtyJobId = "attendance:recompute-dirty";

    /// <summary>Tungi to'liq qayta hisoblash — oxirgi uch kun.</summary>
    public const string NightlyRecomputeJobId = "attendance:nightly-recompute";

    /// <summary>Smenasi boshlangan, lekin qatori yo'q ish kunlari — «Kelmadi».</summary>
    public const string StartedShiftsJobId = "attendance:started-shifts";

    /// <summary>Orqaga sanali mehnat o'zgarishlari (ishdan bo'shatish, qabul, ko'chirish).</summary>
    public const string EmploymentChangesJobId = "attendance:employment-changes";

    /// <summary>Surat va xom hodisani tozalash (SPEC §3.10).</summary>
    public const string PurgePhotosJobId = "attendance:purge-photos";

    /// <summary>O'tish qatorlarini tozalash (SPEC §9.2.1).</summary>
    public const string PurgePassesJobId = "attendance:purge-passes";

    /// <summary>Hisobi yo'q xodimlarga ilova hisobini avtomatik ochish.</summary>
    public const string ProvisionEmployeeAccountsJobId = "employee-app:provision-accounts";

    /// <summary>
    /// Muddati tugagan sinovni avtomatik «o'tdi» qilish (<c>ProbationAutoPassJob</c>) — 2026-10-30 dan
    /// REJADA YO'Q; identifikator eski yozuvni o'chirish uchun qoldi.
    /// </summary>
    public const string ProbationAutoPassJobId = "probations:auto-pass";

    /// <summary>HRMentor: katalog, anketa matnlari va blankalar (SPEC-blankalar-hrmentor §3).</summary>
    public const string HrMentorSyncJobId = "hrmentor:sync";

    /// <summary>The process engine's pass (docs/SPEC-mening-ishlarim.md §7.4, hook H15).</summary>
    public const string ProcessReconcileJobId = "processes:reconcile";

    /// <summary>Hangfire serverini va navbat portini ulaydi — yoki <c>Testing</c> da sinxron navbatni.</summary>
    /// <param name="builder">Ilova quruvchisi.</param>
    public static WebApplicationBuilder AddBonviBackgroundJobs(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.Environment.IsEnvironment(TestingEnvironment))
        {
            builder.Services.AddInlineBackgroundJobQueue();
            return builder;
        }

        var configuration = builder.Configuration;

        // Sozlash lambdasi birinchi murojaatgacha KECHIKADI — `dotnet ef`
        // xostni qurganda Hangfire bazaga ulanmaydi (izohi `HangfireJobStorage` da).
        builder.Services.AddHangfire(hangfire => hangfire
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseBonviPostgreSqlStorage(configuration));

        builder.Services.AddHangfireServer(options =>
        {
            options.WorkerCount = 2;
            options.Queues = ["default"];
        });

        builder.Services.AddHangfireBackgroundJobQueue();

        // HRMentor dagi o'zgarishni kutib turadi (SPEC-hujjat-manbai §7): HR saqlashi bilan
        // sinxronlash darhol. Besh daqiqalik `hrmentor:sync` xavfsizlik to'ri bo'lib qoladi.
        // `Testing` da yo'q — testlar qadamni (`HrMentorChangeWatch`) o'zi chaqiradi.
        builder.Services.AddHostedService<HrMentorChangeWatcher>();

        return builder;
    }

    /// <summary>Takroriy vazifalarni ro'yxatga oladi (yoki yangilaydi). <c>Testing</c> da hech narsa qilmaydi.</summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>Cron UTC da</b> (<c>TimeZone = Utc</c>): konteyner UTC da ishlaydi va
    /// mahalliy vaqt deb o'qilgan cron jimgina besh soat siljiydi. Toshkent
    /// vaqti har bir satr yonida yozilgan.
    /// </para>
    /// <para>
    /// <b>Tartib ataylab:</b> tungi hisoblash (03:10) tozalashlardan OLDIN;
    /// surat tozalash (03:30) o'tish tozalashdan OLDIN — odatdagi o'tishning
    /// surati allaqachon ketgan bo'ladi va 03:45 dagi yurish fayl bilan ishlamaydi.
    /// </para>
    /// <para>
    /// <c>AddOrUpdate</c> — idempotent: har ishga tushishda jadval koddagiga
    /// tenglashtiriladi. Omborga birinchi murojaat shu yerda, ya'ni ishga tushishda
    /// baza yo'q bo'lsa ilova yiqiladi va konteyner qayta ko'tariladi
    /// (<c>restart: unless-stopped</c>).
    /// </para>
    /// </remarks>
    /// <param name="app">Qurilgan ilova.</param>
    public static WebApplication UseBonviRecurringJobs(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (app.Environment.IsEnvironment(TestingEnvironment))
        {
            return app;
        }

        var jobs = app.Services.GetRequiredService<IRecurringJobManager>();
        var utc = new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc };

        // Har 15 daqiqada — Toshkentda ham har 15 daqiqada.
        jobs.AddOrUpdate<AttendanceDirtyRecomputeJob>(
            RecomputeDirtyJobId,
            job => job.RunAsync(CancellationToken.None),
            "*/15 * * * *",
            utc);

        // Har 15 daqiqada, iflos navbatdan 5 daqiqa keyin (…:05, :20, :35, :50) —
        // ikki ishchi bir vaqtda uchta vazifaga talashmasin.
        jobs.AddOrUpdate<AttendanceStartedShiftsJob>(
            StartedShiftsJobId,
            job => job.RunAsync(CancellationToken.None),
            "5-59/15 * * * *",
            utc);

        // Har 15 daqiqada, iflos navbatdan 12 daqiqa keyin (…:12, :27, :42, :57) —
        // 03:10 dagi tungi hisoblash bilan bir daqiqaga tushmasin.
        jobs.AddOrUpdate<AttendanceEmploymentChangesJob>(
            EmploymentChangesJobId,
            job => job.RunAsync(CancellationToken.None),
            "12-59/15 * * * *",
            utc);

        // 22:10 UTC = 03:10 Toshkent.
        jobs.AddOrUpdate<AttendanceNightlyRecomputeJob>(
            NightlyRecomputeJobId,
            job => job.RunAsync(CancellationToken.None),
            "10 22 * * *",
            utc);

        // 22:30 UTC = 03:30 Toshkent.
        jobs.AddOrUpdate<AttendancePhotoPurgeJob>(
            PurgePhotosJobId,
            job => job.RunAsync(CancellationToken.None),
            "30 22 * * *",
            utc);

        // 22:45 UTC = 03:45 Toshkent.
        jobs.AddOrUpdate<AttendancePassPurgeJob>(
            PurgePassesJobId,
            job => job.RunAsync(CancellationToken.None),
            "45 22 * * *",
            utc);

        // Har 15 daqiqada (…:08, :23, :38, :53) — davomat vazifalari bilan bir
        // daqiqaga tushmasin. Yangi qabul qilingan odamning hisobi ko'pi bilan
        // 15 daqiqada paydo bo'ladi; HR «Ilova» bo'limini ochsa — darhol.
        jobs.AddOrUpdate<EmployeeAccountProvisioningJob>(
            ProvisionEmployeeAccountsJobId,
            job => job.RunAsync(CancellationToken.None),
            "8-59/15 * * * *",
            utc);

        // ⛔ Sinovni avtomatik «o'tdi» qilish O'CHIRILDI (egasi, 2026-10-30): muddat tugasa
        // xodim «sinovda» qoladi va «Sinov muddati o'tib ketgan» ogohlantirishi chiqib turadi —
        // natijani HR o'zi kiritadi. Prod Hangfire bazasidagi eski jadval yozuvi ham o'chiriladi,
        // aks holda u yangi kod bilan ham har kecha ishlayverardi.
        jobs.RemoveIfExists(ProbationAutoPassJobId);

        // Har 5 daqiqada (…:01, :06, :11, …) — 15 daqiqalik davomat vazifalari bilan bir
        // daqiqaga tushmasin. HRMentor ulanmagan bo'lsa yurish hech narsa qilmaydi. HR o'zgarishini
        // odatda `HrMentorChangeWatcher` soniyalarda olib keladi (SPEC-hujjat-manbai §7); bu yurish —
        // xavfsizlik to'ri: halqa ishlamasa ham o'zgarish ko'pi bilan 5 daqiqada keladi.
        jobs.AddOrUpdate<HrMentorSyncJob>(
            HrMentorSyncJobId,
            job => job.RunAsync(CancellationToken.None),
            "1-59/5 * * * *",
            utc);

        // Every 5 minutes (:02, :07, :12, …), a minute after `hrmentor:sync`, which holds the SPEC's `1-59/5`.
        // Shares :12, :27, :42, :57 with `attendance:employment-changes`; the server has two workers for that.
        // Dormant until `processes.startedOn` is written: a run then reads one setting and ends.
        jobs.AddOrUpdate<ProcessReconcileJob>(
            ProcessReconcileJobId,
            job => job.RunAsync(CancellationToken.None),
            "2-59/5 * * * *",
            utc);

        // ⭐ Ishga tushishda ham — NAVBATGA qo'yiladi, shu yerda bajarilmaydi:
        // birinchi yurish ~540 ta parolni xeshlaydi (bir necha o'n soniya) va
        // u ilovaning ko'tarilishini to'sib qo'ymasligi kerak. Vazifani Hangfire
        // serveri oladi, u esa bazani tayyorlagichdan (migratsiyalar) KEYIN
        // ishga tushadi.
        jobs.Trigger(ProvisionEmployeeAccountsJobId);

        return app;
    }
}
