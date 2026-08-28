using System.Reflection;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Dashboard.Abstractions;
using Bonvi.Application.Employees.Abstractions;
using Bonvi.Application.Identity.Abstractions;
using Bonvi.Application.Recruiting.Abstractions;
using Bonvi.Application.Reports.Abstractions;
using Bonvi.Application.System.Queries.GetSystemInfo;
using Bonvi.Infrastructure.Identity;
using Bonvi.Infrastructure.Persistence.ReadModels;
using Bonvi.Infrastructure.Persistence.Seed;
using Bonvi.Infrastructure.Reports;
using Bonvi.Infrastructure.Repositories;
using Bonvi.Domain.Abstractions;
using Bonvi.Infrastructure.Persistence;
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

        // Audit jurnali — barcha bo'limlar uchun umumiy, shu sababli
        // bironta modulga tegishli emas, shu yerda ro'yxatga olinadi.
        services.AddScoped<IAuditLogger, AuditLogger>();

        services.AddIdentityServices(configuration);
        services.AddCatalogServices();
        services.AddEmployeeServices();
        services.AddFeedbackServices();
        services.AddSafetyServices();
        services.AddRecruitingServices();

        // Bosh sahifa sanoqlari — bironta bo'limga tegishli emas (xodimlar
        // ham, ma'lumotnomalar ham), shu sababli shu yerda turadi.
        services.AddScoped<IDashboardReadModel, DashboardReadModel>();

        // Hisobotlar (kadrlar oqimi, keyinchalik boshqalari) — xuddi
        // bosh sahifa kabi bironta bo'limga tegishli emas.
        services.AddScoped<IReportsReadModel, ReportsReadModel>();

        // Excel eksporti — holatsiz (bitta chaqiruv, bitta fayl), shu
        // sababli bitta nusxa yetarli (xuddi `IImageProcessor` kabi).
        services.AddSingleton<IEmployeeExcelWriter, EmployeeExcelWriter>();

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

        services.AddScoped<IEmployeeReadModel, EmployeeReadModel>();

        // Rag'bat/jarima ro'yxati — ALOHIDA o'qish modeli, `IEmployeeReadModel`
        // ga qo'shilmagan: sababi `IIncentiveReadModel` izohida (boshqa
        // ruxsat chegarasi — `incentives.view`).
        services.AddScoped<IIncentiveReadModel, IncentiveReadModel>();

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
        services.AddScoped<ICandidateRepository, CandidateRepository>();
        services.AddScoped<IJobOfferRepository, JobOfferRepository>();

        services.AddScoped<IRecruitingReadModel, RecruitingReadModel>();

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
                .AddInterceptors(provider.GetRequiredService<AuditableInterceptor>());
        });

        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<IDatabaseProbe, EfDatabaseProbe>();

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

        services.AddSingleton<IPasswordHasher, BonviPasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

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
