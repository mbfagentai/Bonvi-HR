using System.Reflection;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Attendance.Requests;
using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Audit;
using Bonvi.Application.Catalog.Usage;
using Bonvi.Application.Dashboard.Abstractions;
using Bonvi.Application.Employees.Abstractions;
using Bonvi.Application.Identity.Abstractions;
using Bonvi.Application.Identity.EmployeeAuth;
using Bonvi.Application.Me;
using Bonvi.Application.Notifications.Abstractions;
using Bonvi.Application.PublicApi.Abstractions;
using Bonvi.Application.Recruiting.Abstractions;
using Bonvi.Application.Recruiting.Forms;
using Bonvi.Application.Reports.Abstractions;
using Bonvi.Application.Reports.Export;
using Bonvi.Application.System.Queries.GetSystemInfo;
using Bonvi.Infrastructure.Identity;
using Bonvi.Infrastructure.Persistence.Auditing;
using Bonvi.Infrastructure.Persistence.ReadModels;
using Bonvi.Infrastructure.Persistence.Seed;
using Bonvi.Infrastructure.Reports;
using Bonvi.Infrastructure.Reports.Pdf;
using Bonvi.Infrastructure.Security;
using Bonvi.Infrastructure.Repositories;
using Bonvi.Domain.Abstractions;
using Bonvi.Infrastructure.Persistence;
using Bonvi.Infrastructure.Mcp;
using Bonvi.Infrastructure.Persistence.Interceptors;
using Bonvi.Infrastructure.Services;
using Bonvi.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Infrastructure;

/// <summary>
/// Infrastructure qatlamini DI konteyneriga ulash.
/// </summary>
/// <remarks>
/// Bu yagona joyda "qaysi interfeys qaysi amalga oshirish bilan bog'lanadi"
/// hal qilinadi. Domain va Application bu haqda hech narsa bilmaydi.
/// </remarks>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddPersistence(configuration);
        services.AddCaching(configuration);
        services.AddStorage(configuration);

        services.AddSingleton<IClock, SystemClock>();

        // Audit jurnalining HODISA yo'li (kirish, eksport, ruxsat rad etilishi) —
        // barcha bo'limlar uchun umumiy, shu sababli shu yerda. Entity
        // o'zgarishlarini `AuditTrailInterceptor` o'zi yozadi (`AddPersistence`).
        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddSingleton<LoginFailureWindow>();

        // Jurnalni O'QISH (A2) — har bir bo'limning tarixi, so'rovchining ruxsatiga qarab.
        services.AddScoped<IAuditReadModel, AuditReadModel>();

        services.AddIdentityServices(configuration);
        services.AddCatalogServices();
        services.AddEmployeeServices();
        services.AddFeedbackServices();
        services.AddSafetyServices();
        services.AddAttendanceServices();
        services.AddRecruitingServices();
        services.AddNotificationServices();
        services.AddSettingsServices(configuration);
        services.AddPublicApiServices();

        // Bosh sahifa sanoqlari — bironta bo'limga tegishli emas (xodimlar
        // ham, ma'lumotnomalar ham), shu sababli shu yerda turadi.
        services.AddScoped<IDashboardReadModel, DashboardReadModel>();

        // Hisobotlar (kadrlar oqimi, keyinchalik boshqalari) — xuddi
        // bosh sahifa kabi bironta bo'limga tegishli emas.
        services.AddScoped<IReportsReadModel, ReportsReadModel>();

        // Excel eksporti — holatsiz (bitta chaqiruv, bitta fayl), shu
        // sababli bitta nusxa yetarli (xuddi `IImageProcessor` kabi).
        services.AddSingleton<IEmployeeExcelWriter, EmployeeExcelWriter>();
        services.AddSingleton<IIncentiveExcelWriter, IncentiveExcelWriter>();
        // Qolgan ro'yxatlarning eksporti — bitta umumiy jadval yozuvchisi (SPEC-excel-eksport)
        services.AddSingleton<IExcelTableWriter, ExcelTableWriter>();

        // Planshet anketasining PDF'i (SPEC-planshet-anketa §6.3) — holatsiz,
        // shriftlar jarayonga bir marta o'rnatiladi.
        services.AddSingleton<IQuestionnairePdfRenderer, QuestionnairePdfRenderer>();
        services.AddSingleton<IQuestionnaireFormJournal, QuestionnaireFormJournal>();
        services.AddQuestionnaireBotCheck(configuration);

        return services;
    }

    /// <summary>
    /// Onlayn anketaning bot tekshiruvi — Cloudflare Turnstile (SPEC-planshet-anketa §2.3.2).
    /// </summary>
    /// <remarks>
    /// Kalitlar berilmasa tekshiruv o'chiq (<see cref="TurnstileOptions"/>), lekin ro'yxatga olish
    /// HAR DOIM bir xil — seans javobi ochiq kalitni shu port orqali o'qiydi. Muddat 5 soniya
    /// (<see cref="TurnstileBotCheck.Timeout"/>); <c>HttpClient</c> ning o'z so'rov jurnali o'chiq
    /// (sababi <see cref="TurnstileBotCheck"/> izohida).
    /// </remarks>
    private static IServiceCollection AddQuestionnaireBotCheck(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<TurnstileOptions>()
            .Bind(configuration.GetSection(TurnstileOptions.SectionName));

        services
            .AddHttpClient<IQuestionnaireBotCheck, TurnstileBotCheck>(
                TurnstileBotCheck.HttpClientName,
                client => client.Timeout = TurnstileBotCheck.Timeout)
            .RemoveAllLoggers();

        return services;
    }

    /// <summary>Xodimlar va mehnat munosabatlari bilan ishlaydigan portlar.</summary>
    /// <remarks>
    /// <para>
    /// Ochiq generik ro'yxatga olish bu yerda ISHLAMAYDI: har bir port o'z
    /// so'rovlarini qo'shadi (JSHSHIR bo'yicha topish, stavkalar yig'indisi,
    /// muddati tugayotgan sinovlar) va ularning umumiy asosi yo'q. Shu sababli
    /// har biri aniq ko'rsatiladi.
    /// </para>
    /// <para>
    /// <c>IEmployeeReadModel</c> — repozitoriy EMAS: u agregat qaytarmaydi,
    /// balki ro'yxat ekranlari uchun tayyor qatorlar beradi. Ro'yxatga olish
    /// bir xil bo'lgani uchun shu yerda turadi, amalga oshirilishi esa
    /// <c>Persistence/ReadModels</c> da — repozitoriylar bilan aralashmasin.
    /// </para>
    /// </remarks>
    private static IServiceCollection AddEmployeeServices(this IServiceCollection services)
    {
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IEmploymentRepository, EmploymentRepository>();
        services.AddScoped<IProbationRepository, ProbationRepository>();
        services.AddScoped<IEmployeeFlagRepository, EmployeeFlagRepository>();
        services.AddScoped<IEmployeeCompetencyRepository, EmployeeCompetencyRepository>();
        services.AddScoped<IIncentiveRepository, IncentiveRepository>();
        services.AddScoped<IEmployeeDocumentRepository, EmployeeDocumentRepository>();
        services.AddScoped<IEmployeeCheckFileRepository, EmployeeCheckFileRepository>();

        services.AddScoped<IEmployeeReadModel, EmployeeReadModel>();

        // Rag'bat/jarima ro'yxati — ALOHIDA o'qish modeli, `IEmployeeReadModel`
        // ga qo'shilmagan: sababi `IIncentiveReadModel` izohida (boshqa
        // ruxsat chegarasi — `incentives.view`).
        services.AddScoped<IIncentiveReadModel, IncentiveReadModel>();

        return services;
    }

    /// <summary>Tashqi API (ommaviy o'qish) bilan ishlaydigan portlar.</summary>
    /// <remarks>
    /// <para>
    /// <c>IApiKeyUsageTracker</c> — YAGONA <c>Singleton</c> bu ro'yxatda va
    /// bu ataylab: uning ichida «qaysi kalitga qachon yozildi» degan
    /// xotira jadvali bor va u so'rovlar orasida yashashi kerak. Scoped
    /// bo'lsa, chegara har so'rovda nolga tushib, «daqiqada bitta yozuv»
    /// qoidasi umuman ishlamasdi (batafsil — <c>ApiKeyUsageTracker</c>
    /// izohida).
    /// </para>
    /// <para>
    /// <c>IPublicApiReadModel</c> — repozitoriy EMAS: u agregat qaytarmaydi
    /// va uning butun mazmuni CHEGARA (qaysi ustunlar tashqariga chiqadi).
    /// Shu sababli amalga oshirilishi <c>Persistence/ReadModels</c> da.
    /// </para>
    /// </remarks>
    private static IServiceCollection AddPublicApiServices(this IServiceCollection services)
    {
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
        services.AddScoped<IPublicApiReadModel, PublicApiReadModel>();
        services.AddScoped<IPublicTeamReadModel, PublicTeamReadModel>();
        services.AddSingleton<IApiKeyUsageTracker, ApiKeyUsageTracker>();

        return services;
    }

    /// <summary>«Qayta aloqa» (exit interview, S9/M10) bilan ishlaydigan portlar.</summary>
    private static IServiceCollection AddFeedbackServices(this IServiceCollection services)
    {
        services.AddScoped<IExitInterviewRepository, ExitInterviewRepository>();

        return services;
    }

    /// <summary>Baxtsiz hodisalar jurnali (S9/M11) bilan ishlaydigan portlar.</summary>
    private static IServiceCollection AddSafetyServices(this IServiceCollection services)
    {
        services.AddScoped<IIncidentRepository, IncidentRepository>();

        return services;
    }

    /// <summary>Davomat (3-bosqich) bilan ishlaydigan portlar.</summary>
    /// <remarks>
    /// <para>
    /// Oltita repozitoriyning HAMMASI bir joyda, garchi bugun ulardan faqat
    /// uchtasini qabul yo'li ishlatsa ham: ular bitta sxema bilan birga
    /// kelgan (<c>DavomatModuli</c> migratsiyasi) va qismini ro'yxatga olish
    /// keyingi qism muallifini «qolgani qayerda?» deb qidirishga majbur
    /// qilardi.
    /// </para>
    /// <para>
    /// O'qish tomoni ham shu yerda: <c>IAttendanceReadModel</c> (jurnal, taxta,
    /// tabel, mosliklar) va <c>IAttendanceComputationSource</c> (qayta hisoblash
    /// yuklovchisi) — ikkalasi bir xil qoida bilan o'qiydi (o'chirilgan jadval
    /// va bo'limlar ham), shu sababli yonma-yon turadi.
    /// </para>
    /// <para>
    /// ⚠️ <b>Fon navbati bu yerda YO'Q.</b> Qaysi biri ulanishini
    /// (<see cref="AddHangfireBackgroundJobQueue"/> yoki
    /// <see cref="AddInlineBackgroundJobQueue"/>) Hangfire serverini ishga
    /// tushiradigan AYNAN o'sha shart hal qiladi — Api dagi
    /// <c>BackgroundJobs.AddBonviBackgroundJobs</c> (SPEC §9.1.1). Ikkalasi bitta
    /// <c>if</c> da bo'lgani uchun «server yo'q, lekin navbat Hangfire» yoki
    /// «server bor, lekin ish so'rov ichida» holati tug'ilmaydi.
    /// </para>
    /// </remarks>
    private static IServiceCollection AddAttendanceServices(this IServiceCollection services)
    {
        services.AddScoped<IAttendanceDeviceRepository, AttendanceDeviceRepository>();
        services.AddScoped<IAbsenceRequestRepository, AbsenceRequestRepository>();
        services.AddScoped<IScheduleChangeRequestRepository, ScheduleChangeRequestRepository>();
        services.AddScoped<IWorkScheduleDateRepository, WorkScheduleDateRepository>();
        services.AddScoped<IAbsenceTypeLookup, AbsenceTypeLookup>();
        services.AddScoped<IAttendanceIdentityRepository, AttendanceIdentityRepository>();
        services.AddScoped<IAttendancePassRepository, AttendancePassRepository>();
        services.AddScoped<IAttendanceManualPassRepository, AttendanceManualPassRepository>();
        services.AddScoped<IWorkScheduleRepository, WorkScheduleRepository>();
        services.AddScoped<IScheduleAssignmentRepository, ScheduleAssignmentRepository>();
        services.AddScoped<IAttendanceDayRepository, AttendanceDayRepository>();
        services.AddScoped<IDeviceEnrollmentRepository, DeviceEnrollmentRepository>();
        services.AddScoped<IDevicePersonNumbers, DevicePersonNumbers>();

        services.AddScoped<IAttendanceReadModel, AttendanceReadModel>();
        services.AddScoped<IScheduleChangeRequestReadModel, AttendanceReadModel>();
        services.AddScoped<IAttendanceComputationSource, AttendanceComputationSource>();
        services.AddScoped<IEnrollmentReadModel, EnrollmentReadModel>();

        /*
          Telefondan belgilash (SPEC-xodim-ilovasi §E) — bitta yozish porti.

          ⚠️ OBYEKTLARNING repozitoriysi bu yerda YO'Q va kerak ham emas:
          `WorkSite` — ma'lumotnoma, ya'ni uni ochiq generik
          `IReferenceRepository<>` ro'yxati (`AddCatalogServices`) allaqachon
          qoplaydi. Bu yerga qo'shilsa, ikkita ro'yxatga olish bir-birini
          bosib, qaysi biri ishlayotganini aytib bo'lmas edi.
        */
        services.AddScoped<IMobileCheckInRepository, MobileCheckInRepository>();

        // Excel yozuvchi va o'quvchi — holatsiz, bitta nusxa yetarli
        // (`EmployeeExcelWriter` bilan bir xil).
        services.AddSingleton<IAttendanceExcelWriter, AttendanceExcelWriter>();
        services.AddSingleton<IAttendanceImportReader, AttendanceImportReader>();

        // Fon vazifalari jurnali — holatsiz, faqat logger ushlaydi.
        services.AddSingleton<IAttendanceJobJournal, AttendanceJobJournal>();

        return services;
    }

    /// <summary>
    /// Fon navbati — Hangfire. <c>Testing</c> dan boshqa hamma muhit uchun.
    /// </summary>
    /// <remarks>
    /// <c>IBackgroundJobClient</c> ning o'zini Api dagi <c>AddHangfire</c> ro'yxatga
    /// oladi; bu metod faqat Application portini unga ulaydi. Singleton: adapter
    /// holatsiz, mijoz ham singleton.
    /// </remarks>
    /// <param name="services">DI konteyneri.</param>
    public static IServiceCollection AddHangfireBackgroundJobQueue(this IServiceCollection services)
    {
        services.AddSingleton<IBackgroundJobQueue, HangfireBackgroundJobQueue>();

        return services;
    }

    /// <summary>
    /// Fon navbati — SINXRON, ish shu so'rovning o'zida. FAQAT <c>Testing</c> muhiti uchun.
    /// </summary>
    /// <remarks>
    /// ⚠️ Ishlab chiqarishda bu navbat qolmasligi SHART: import tasdig'i bitta
    /// so'rov ichida ~540 × 8 so'rov bo'lardi (sabab <c>HangfireBackgroundJobQueue</c>
    /// izohida). Scoped: vazifa chaqiruvchining qamrovidan olinadi.
    /// </remarks>
    /// <param name="services">DI konteyneri.</param>
    public static IServiceCollection AddInlineBackgroundJobQueue(this IServiceCollection services)
    {
        services.AddScoped<IBackgroundJobQueue, InlineBackgroundJobQueue>();

        return services;
    }

    /// <summary>Rekruting (2-bosqich) bilan ishlaydigan portlar.</summary>
    /// <remarks>
    /// <para>
    /// <c>IPipelineStageRepository</c> ALOHIDA ro'yxatga olinadi, garchi
    /// bosqich ma'lumotnoma bo'lsa ham: ochiq generik
    /// <c>IReferenceRepository&lt;&gt;</c> uni qoplamaydi, chunki
    /// so'ralayotgan interfeys boshqa (voronka tartibi va «eng katta
    /// tartib» qo'shilgan). Bo'lim va lavozim bilan aynan bir xil holat.
    /// </para>
    /// <para>
    /// <c>IRecruitingReadModel</c> — repozitoriy EMAS: u agregat
    /// qaytarmaydi, ro'yxat ekranlari uchun tayyor qatorlar beradi. Shu
    /// sababli amalga oshirilishi <c>Persistence/ReadModels</c> da turadi.
    /// </para>
    /// </remarks>
    private static IServiceCollection AddRecruitingServices(this IServiceCollection services)
    {
        services.AddScoped<IPipelineStageRepository, PipelineStageRepository>();
        services.AddScoped<IVacancyRepository, VacancyRepository>();
        services.AddScoped<IVacancyRequestRepository, VacancyRequestRepository>();
        services.AddScoped<ICandidateRepository, CandidateRepository>();
        services.AddScoped<ICandidateFormRepository, CandidateFormRepository>();

        services.AddScoped<IRecruitingReadModel, RecruitingReadModel>();
        services.AddScoped<IVacancyRequestReadModel, VacancyRequestReadModel>();
        services.AddScoped<IReferenceUsageReadModel, ReferenceUsageReadModel>();

        return services;
    }

    /// <summary>Bildirishnomalar bilan ishlaydigan portlar.</summary>
    /// <remarks>
    /// <para>
    /// Ikkita port, ikki xil vazifa: <c>INotificationReadModel</c>
    /// SHARTLARNI o'qiydi (agregat qaytarmaydi — shu sababli
    /// <c>Persistence/ReadModels</c> da), <c>INotificationStateRepository</c>
    /// esa foydalanuvchining «o'qildi»/«o'chirildi» qarorini saqlaydi.
    /// Ular BIRGA turadi, chunki ikkalasi ham faqat shu bo'limga xizmat
    /// qiladi.
    /// </para>
    /// <para>
    /// ⚠️ Bu yerda hech qanday FON VAZIFASI ro'yxatga olinmaydi va bu
    /// ataylab: bildirishnomalar o'qish paytida hisoblanadi
    /// (sababi <c>INotificationReadModel</c> izohida).
    /// </para>
    /// </remarks>
    private static IServiceCollection AddNotificationServices(this IServiceCollection services)
    {
        services.AddScoped<INotificationStateRepository, NotificationStateRepository>();
        services.AddScoped<INotificationReadModel, NotificationReadModel>();

        return services;
    }

    /// <summary>Tizim sozlamalari bilan ishlaydigan portlar.</summary>
    /// <remarks>
    /// <para>
    /// Atigi BITTA port va u ham juda kichik: sozlamalar soni kodda
    /// belgilangan (<c>SystemSettingKeys.All</c>), ya'ni «ro'yxat
    /// ekranlari» ham, o'qish modeli ham kerak emas.
    /// </para>
    /// <para>
    /// ⚠️ Bu yerda KESH yo'q va bu ataylab. Sozlama har so'rovda bazadan
    /// o'qiladi (bir marta — <c>SystemSettingsReader</c> so'rov davomida
    /// eslab qoladi). Redis keshi qo'shilsa, bosh rahbar o'zgartirgan
    /// chegara boshqa jarayonlarda (bot, ikkinchi konteyner) eskiligicha
    /// qolib ketardi va buni faqat qayta ishga tushirish tuzatardi —
    /// oltita qatorli jadval uchun bunday narx o'rinsiz.
    /// </para>
    /// </remarks>
    private static IServiceCollection AddSettingsServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<ISystemSettingRepository, SystemSettingRepository>();

        /*
          Sirlarni shifrlash. `Singleton`, chunki kalit ishga tushishda
          BIR MARTA chiqariladi (HKDF) va keyin o'zgarmaydi — uni har
          so'rovda qayta hisoblash ortiqcha ish bo'lardi. Sinf holatsiz
          va oqimga xavfsiz: `AesGcm` har chaqiruvda yangisi yasaladi.

          Sozlama bo'limi ISHGA TUSHISHDA tekshirilmaydi (`ValidateOnStart`
          yo'q): unda majburiy maydon yo'q — kalit berilmasa
          `Jwt:SigningKey` dan chiqariladi (sabab
          `SecretProtectionOptions` da).
        */
        services.AddOptions<SecretProtectionOptions>()
            .Bind(configuration.GetSection(SecretProtectionOptions.SectionName));

        services.AddSingleton<ISecretProtector, AesGcmSecretProtector>();

        return services;
    }

    /// <summary>Ma'lumotnomalar bilan ishlaydigan repozitoriylar va urug'lantiruvchi.</summary>
    /// <remarks>
    /// <para>
    /// Ochiq generik ro'yxatga olish: <c>IReferenceRepository&lt;AwardType&gt;</c>,
    /// <c>IReferenceRepository&lt;DocumentType&gt;</c> va boshqalarning HAMMASI
    /// bitta satr bilan qoplanadi. Yangi ma'lumotnoma qo'shilganda bu yerni
    /// tahrirlash kerak emas — unutish imkoniyati yo'q qilinadi.
    /// </para>
    /// <para>
    /// Bo'lim va lavozim uchun ALOHIDA ro'yxatga olish bor: ular
    /// <c>IReferenceRepository&lt;T&gt;</c> ga qo'shimcha imkoniyat qo'shadi
    /// (daraxt, vazifalar). Ochiq generik ularni qoplamaydi, chunki so'ralayotgan
    /// interfeys boshqa.
    /// </para>
    /// </remarks>
    private static IServiceCollection AddCatalogServices(this IServiceCollection services)
    {
        services.AddScoped(typeof(IReferenceRepository<>), typeof(ReferenceRepository<>));

        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IPositionRepository, PositionRepository>();

        services.AddScoped<CatalogSeeder>();

        return services;
    }

    private static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");

        // DIQQAT: bo'sh satrni ham rad etamiz, faqat null'ni emas.
        // appsettings.json'dagi "Default": "" qiymati null EMAS — agar uni
        // o'tkazib yuborsak, dastur muammosiz ishga tushadi va faqat birinchi
        // so'rovda "503" bo'lib yiqiladi. Xatoni shu yerda, darhol ko'rsatamiz.
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "«ConnectionStrings:Default» sozlamasi bo'sh yoki topilmadi. " +
                ".env faylini va docker-compose muhit o'zgaruvchilarini tekshiring.");
        }

        services.AddScoped<AuditableInterceptor>();

        /*
          O'zgarishlar jurnali (SPEC-rollar-va-audit §7). Interceptor `Scoped` —
          bajaruvchi keshi so'rov bilan yashaydi. Navbat (`PendingAuditEvents`)
          `IAuditLogger` va interceptor o'rtasida UMUMIY bo'lishi shart: ikkalasi
          bitta qamrovdan olinadi, ya'ni kirish yoki eksport hodisasi o'sha so'rovning
          saqlashida yoziladi.

          ⚠️ `IRequestContext`, `IAuditScope` va `IAuditActorSource` bu yerda
          RO'YXATGA OLINMAYDI — ular xostga bog'liq (HTTP so'rovi yoki tizim) va
          `ICurrentUser` kabi Api'da ulanadi (`Program.cs`).
        */
        services.AddScoped<PendingAuditEvents>();
        services.AddScoped<AuditTrailInterceptor>();

        /*
          «Ma'lumot o'zgardi» signali — SINGLETON va boshqacha bo'lishi
          mumkin emas: uni bir tomondan so'rovlar uyg'otadi, ikkinchi
          tomondan soatlab ochiq turgan `stream` kutadi. Har so'rovda
          yangi nusxa yasalsa, ular BOSHQA-BOSHQA obyektni kutib
          o'tirardi va signal hech qachon yetib bormasdi.

          Interceptor esa `Scoped` (qolganlari bilan bir xil), lekin
          ichidagi signal — o'sha yagona nusxa.
        */
        services.AddSingleton<IDataChangeSignal, DataChangeSignal>();
        services.AddScoped<DataChangeInterceptor>();

        // Rol yoki hisob huquqi o'zgarganda ruxsatlar keshini eskiradi —
        // xuddi yuqoridagi signal kabi: interceptor `Scoped`, versiya esa
        // yagona nusxa (`AddIdentityServices` da).
        services.AddScoped<AccessChangeInterceptor>();

        services.AddDbContext<AppDbContext>((provider, options) =>
        {
            options
                .UseNpgsql(connectionString, npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                    npgsql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), null);
                })
                // PostgreSQL uchun snake_case: employee_document, created_at
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(
                    provider.GetRequiredService<AuditableInterceptor>(),

                    // `AuditableInterceptor` dan KEYIN: yumshoq o'chirish allaqachon
                    // `DeletedAt` ga aylangan va `UpdatedAt` qo'yilgan bo'lishi kerak.
                    provider.GetRequiredService<AuditTrailInterceptor>(),
                    provider.GetRequiredService<DataChangeInterceptor>(),

                    // Oxirida: yumshoq o'chirishni `AuditableInterceptor`
                    // allaqachon `DeletedAt` ga aylantirgan bo'lishi kerak.
                    provider.GetRequiredService<AccessChangeInterceptor>());
        });

        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<IDatabaseProbe, EfDatabaseProbe>();

        // AI ulanish (MCP) — o'qish roli va `mcp_*` yozuvlari uchun alohida
        // ulanishlar (`docs/SPEC-mcp.md` §3). Singleton: ichida faqat
        // ulanish satrlari va bir marta quriladigan kontekst opsiyalari bor.
        services.AddSingleton<McpDatabase>();
        services.AddScoped<Bonvi.Application.Mcp.IMcpGrantRevoker, McpGrantRevoker>();

        return services;
    }

    /// <summary>Autentifikatsiya bilan bog'liq servislar va repozitoriylar.</summary>
    private static IServiceCollection AddIdentityServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Sozlamalar ishga tushishda tekshiriladi: imzolash kaliti qisqa bo'lsa
        // yoki umuman berilmagan bo'lsa, dastur darhol to'xtaydi.
        // Aks holda xato faqat birinchi kirish urinishida chiqadi.
        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        /*
          XODIM ILOVASINING tokenlari — ALOHIDA sozlama bo'limi, ALOHIDA
          auditoriya, lekin O'SHA imzolash kaliti (sababi
          `EmployeeJwtOptions` izohida).

          `ValidateOnStart` bor: auditoriya bo'sh qolsa yoki HR
          auditoriyasi bilan bir xil bo'lib qolsa, tizim ishga tushishda
          to'xtashi kerak — aks holda xodim tokeni jimgina HR
          endpointlariga kira boshlardi. Tenglik tekshiruvi Api
          qatlamida (`AddBonviAuthentication`), chunki u ikkala
          sozlamani ham ko'radi.
        */
        services
            .AddOptions<EmployeeJwtOptions>()
            .Bind(configuration.GetSection(EmployeeJwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IPasswordHasher, BonviPasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        // Xodim tokenlari — parolni xeshlagich HR bilan BITTA
        // (`IPasswordHasher`): ikkinchi amalga oshirish — ikkinchi
        // xato qilish imkoniyati.
        services.AddSingleton<IEmployeeTokenService, EmployeeJwtTokenService>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        /*
          Har so'rovda tokendagi ruxsatlarni bazadagi JORIY holat bilan
          almashtirish (egasining qarori: rol tahriri keyingi so'rovdayoq
          kuchga kirsin). Natija jarayon xotirasida ≤ 30 soniya turadi.

          ⚠️ Bu yerda `IDistributedCache` (Redis) EMAS, `IMemoryCache` va
          bu ataylab: eskirish `AccessVersion` orqali — u shu jarayonning
          xotirasida. Redis'da saqlangan natijani versiya oshgani bilan
          boshqa nusxa bilmasdi, har so'rovda tarmoq aylanishi esa bitta
          birlamchi kalit bo'yicha so'rovdan qimmatroq.
        */
        services.AddMemoryCache();
        services.AddSingleton<AccessVersion>();
        services.AddScoped<IUserAccessReader, UserAccessReader>();

        // «Kim kiritdi» ismlari (buyruq muallifi) — o'chirilgan hisoblar ham.
        services.AddScoped<IUserNameLookup, UserNameLookup>();

        // Xodim ilovasining hisoblari va tokenlari
        services.AddScoped<IEmployeeAccountRepository, EmployeeAccountRepository>();
        services.AddScoped<IEmployeeRefreshTokenRepository, EmployeeRefreshTokenRepository>();
        services.AddScoped<IEmployeeAppReadModel, EmployeeAppReadModel>();

        // Kirish jurnali — holatsiz, faqat logger ushlaydi
        // (`IAttendanceJobJournal` bilan bir xil).
        services.AddSingleton<IEmployeeAuthJournal, EmployeeAuthJournal>();

        // «Jamoam» (SPEC-jamoam §C) — panel qatoriga telefon va ko'rish auditining jurnali.
        services.AddScoped<ITeamReadModel, TeamReadModel>();
        services.AddSingleton<ITeamViewJournal, TeamViewJournal>();

        services.AddScoped<IdentitySeeder>();

        return services;
    }

    private static IServiceCollection AddCaching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var redis = configuration.GetConnectionString("Redis");

        if (string.IsNullOrWhiteSpace(redis))
        {
            // Redis mavjud bo'lmasa tizim ishlashda davom etadi (xotiradagi kesh)
            services.AddDistributedMemoryCache();
            return services;
        }

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redis;
            options.InstanceName = "bonvi:";
        });

        return services;
    }

    private static IServiceCollection AddStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        // Baza nosozligini ma'noli holatga o'girish — drayver haqidagi bilim
        // shu qatlamda qoladi (sabab `IDatabaseFault` izohida).
        services.AddSingleton<IDatabaseFault, PostgresFault>();

        // Rasm qayta ishlash — holatsiz, shu sababli bitta nusxa yetarli.
        // Sozlama olmaydi: o'lcham va sifat use-case tomonidan beriladi.
        services.AddSingleton<IImageProcessor, SkiaImageProcessor>();

        // Hujjat mazmunini tekshirish (S8/M9) — holatsiz, xuddi
        // `IImageProcessor` kabi bitta nusxa yetarli.
        services.AddSingleton<IFileContentInspector, FileContentInspector>();

        return services;
    }

    /// <summary>Arxitektura testlari uchun qatlam yig'malasi havolasi.</summary>
    public static Assembly Assembly => typeof(DependencyInjection).Assembly;
}
