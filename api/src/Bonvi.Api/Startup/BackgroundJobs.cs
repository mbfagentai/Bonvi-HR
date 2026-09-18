using Bonvi.Application.Attendance.Recompute;
using Bonvi.Application.Attendance.Retention;
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

    /// <summary>Surat va xom hodisani tozalash (SPEC §3.10).</summary>
    public const string PurgePhotosJobId = "attendance:purge-photos";

    /// <summary>O'tish qatorlarini tozalash (SPEC §9.2.1).</summary>
    public const string PurgePassesJobId = "attendance:purge-passes";

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

        return app;
    }
}
