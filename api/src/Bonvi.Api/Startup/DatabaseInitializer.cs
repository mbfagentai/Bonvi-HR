using Bonvi.Application.Identity.Abstractions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Bonvi.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Api.Startup;

/// <summary>Birinchi administrator uchun sozlamalar.</summary>
public sealed class BootstrapOptions
{
    public const string SectionName = "Bootstrap";

    /// <summary>Birinchi administratorning telefon raqami.</summary>
    public string? AdminLogin { get; set; }

    /// <summary>Birinchi administratorning paroli.</summary>
    public string? AdminPassword { get; set; }

    /// <summary>Birinchi administratorning ism-familiyasi.</summary>
    public string AdminFullName { get; set; } = "Bosh administrator";

    /// <summary>
    /// Ishga tushishda kutilayotgan migratsiyalarni avtomatik qo'llash.
    /// Production'da <c>false</c>: migratsiya alohida qadam bo'lishi kerak.
    /// </summary>
    public bool AutoMigrate { get; set; }

    /// <summary>
    /// HR menejeri uchun sinov hisobini yaratish.
    /// </summary>
    /// <remarks>
    /// <b>Faqat Development muhitida ishlaydi.</b> Boshqa muhitlarda bu
    /// bayroq <c>true</c> bo'lsa ham e'tiborga olinmaydi — sinov hisoblari
    /// ishlab chiqarish bazasiga tushib qolishi mumkin bo'lgan yo'l yopiq.
    /// </remarks>
    public bool CreateDevUsers { get; set; }
}

/// <summary>
/// Ishga tushishda bazani tayyorlaydi: migratsiya, rollar, birinchi administrator.
/// </summary>
/// <remarks>
/// <para>
/// <b>Birinchi administrator faqat bazada bironta ham foydalanuvchi bo'lmaganda</b>
/// yaratiladi. Aks holda har bir qayta ishga tushishda parol tiklanib ketardi —
/// bu esa administrator parolini o'zgartirishning ma'nosini yo'qotadi.
/// </para>
/// <para>
/// Parol sozlamalarda bo'lmasa, hisob umuman yaratilmaydi va ogohlantirish
/// yoziladi. Standart parol qo'yish (masalan «admin») xavfli: uni o'zgartirishni
/// unutish oson, topish esa juda oson.
/// </para>
/// </remarks>
public sealed partial class DatabaseInitializer : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        IServiceProvider services,
        IHostEnvironment environment,
        ILogger<DatabaseInitializer> logger)
    {
        _services = services;
        _environment = environment;
        _logger = logger;
    }

    [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "Migratsiyalar qo'llanmoqda: {Count} ta")]
    private partial void LogApplyingMigrations(int count);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Standart rollar tekshirildi")]
    private partial void LogRolesSeeded();

    [LoggerMessage(
        EventId = 2006,
        Level = LogLevel.Information,
        Message = "Ma'lumotnomalar tekshirildi: {Created} qo'shildi, {Skipped} mavjud edi, {MissingFiles} fayl topilmadi")]
    private partial void LogCatalogSeeded(int created, int skipped, int missingFiles);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Birinchi administrator yaratildi: {Login}")]
    private partial void LogAdminCreated(string login);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Warning,
        Message = "Bazada foydalanuvchi yo'q, lekin «Bootstrap:AdminLogin» yoki «Bootstrap:AdminPassword» berilmagan — administrator yaratilmadi")]
    private partial void LogAdminSkipped();

    [LoggerMessage(EventId = 2004, Level = LogLevel.Error, Message = "Birinchi administrator yaratilmadi: {Reason}")]
    private partial void LogAdminFailed(string reason);

    [LoggerMessage(
        EventId = 2005,
        Level = LogLevel.Warning,
        Message = "SINOV hisobi yaratildi (faqat development): {Login} — {Role}")]
    private partial void LogDevUserCreated(string login, string role);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var provider = scope.ServiceProvider;

        var context = provider.GetRequiredService<AppDbContext>();
        var options = provider.GetRequiredService<
            Microsoft.Extensions.Options.IOptions<BootstrapOptions>>().Value;

        // ---------- Migratsiya ----------
        if (options.AutoMigrate || _environment.IsDevelopment())
        {
            var pending = (await context.Database
                .GetPendingMigrationsAsync(cancellationToken)
                .ConfigureAwait(false)).ToList();

            if (pending.Count > 0)
            {
                LogApplyingMigrations(pending.Count);
                await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        // ---------- Rollar ----------
        var seeder = provider.GetRequiredService<IdentitySeeder>();
        await seeder.SeedAsync(cancellationToken).ConfigureAwait(false);
        LogRolesSeeded();

        // ---------- Ma'lumotnomalar ----------
        // Excel'dan tozalab olingan ro'yxatlar: yuridik shaxslar, yo'nalishlar,
        // bo'limlar daraxti, lavozimlar, ishdan ketish sabablari va boshqalar.
        // Amal idempotent — mavjud yozuvlarga tegilmaydi, faqat yetishmayotgani
        // qo'shiladi. Shu sababli har ishga tushishda chaqirish xavfsiz.
        var catalog = await provider.GetRequiredService<CatalogSeeder>()
            .SeedAsync(cancellationToken)
            .ConfigureAwait(false);

        LogCatalogSeeded(catalog.Created, catalog.Skipped, catalog.MissingFiles);

        // ---------- Birinchi administrator ----------
        await CreateFirstAdminAsync(provider, options, cancellationToken).ConfigureAwait(false);

        // ---------- Sinov hisoblari (faqat Development) ----------
        if (options.CreateDevUsers && _environment.IsDevelopment())
        {
            await CreateDevUsersAsync(provider, options, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Har bir rol uchun bitta sinov hisobi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nega kerak: HR menejeri nimani ko'rmasligini (masalan foydalanuvchi
    /// boshqaruvi) tekshirish uchun o'sha rol ostida kirib ko'rish kerak.
    /// Har safar qo'lda foydalanuvchi yaratish — ishni sekinlashtiradi.
    /// </para>
    /// <para>
    /// Parol administrator paroli bilan bir xil — ishlab chiqishda
    /// har bir hisob uchun alohida parol eslab qolish keraksiz yuk.
    /// </para>
    /// <para>
    /// Amal idempotent: mavjud hisoblarga tegilmaydi.
    /// </para>
    /// </remarks>
    private async Task CreateDevUsersAsync(
        IServiceProvider provider,
        BootstrapOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.AdminPassword))
        {
            return;
        }

        (string Login, string Name, string RoleCode)[] devUsers =
        [
            ("905088877", "Мамажонов Дилшод Рустамович", SystemRoles.HrManager),
        ];

        var context = provider.GetRequiredService<AppDbContext>();
        var hasher = provider.GetRequiredService<IPasswordHasher>();
        var created = 0;

        foreach (var (login, name, roleCode) in devUsers)
        {
            var phone = PhoneNumber.Create(login);
            var fullName = FullName.Parse(name);

            if (phone.IsFailure || fullName.IsFailure)
            {
                continue;
            }

            var loginValue = phone.Value.Value;
            var exists = await context.Users
                .IgnoreQueryFilters()
                .AnyAsync(u => u.Login == phone.Value, cancellationToken)
                .ConfigureAwait(false);

            if (exists)
            {
                continue;
            }

            var role = await context.Roles
                .FirstOrDefaultAsync(r => r.Code == roleCode, cancellationToken)
                .ConfigureAwait(false);

            if (role is null)
            {
                continue;
            }

            var userResult = User.Create(phone.Value, hasher.Hash(options.AdminPassword), fullName.Value);
            if (userResult.IsFailure)
            {
                continue;
            }

            userResult.Value.AssignRole(role.Id);
            context.Users.Add(userResult.Value);
            created++;

            LogDevUserCreated(loginValue, roleCode);
        }

        if (created > 0)
        {
            await provider.GetRequiredService<IUnitOfWork>()
                .SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task CreateFirstAdminAsync(
        IServiceProvider provider,
        BootstrapOptions options,
        CancellationToken cancellationToken)
    {
        var context = provider.GetRequiredService<AppDbContext>();

        // Bironta foydalanuvchi bo'lsa — hech narsa qilmaymiz.
        // O'chirilganlarni ham hisobga olamiz: aks holda administratorni
        // o'chirib, qayta ishga tushirish orqali parolni tiklab olish mumkin bo'lardi.
        var anyUser = await context.Users
            .IgnoreQueryFilters()
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);

        if (anyUser)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.AdminLogin) ||
            string.IsNullOrWhiteSpace(options.AdminPassword))
        {
            LogAdminSkipped();
            return;
        }

        var loginResult = PhoneNumber.Create(options.AdminLogin);
        if (loginResult.IsFailure)
        {
            LogAdminFailed(loginResult.Error.Message);
            return;
        }

        var nameResult = FullName.Parse(options.AdminFullName);
        if (nameResult.IsFailure)
        {
            LogAdminFailed(nameResult.Error.Message);
            return;
        }

        var adminRole = await context.Roles
            .FirstOrDefaultAsync(r => r.Code == SystemRoles.Admin, cancellationToken)
            .ConfigureAwait(false);

        if (adminRole is null)
        {
            LogAdminFailed("«admin» roli topilmadi");
            return;
        }

        var hasher = provider.GetRequiredService<IPasswordHasher>();

        var userResult = User.Create(
            loginResult.Value,
            hasher.Hash(options.AdminPassword),
            nameResult.Value);

        if (userResult.IsFailure)
        {
            LogAdminFailed(userResult.Error.Message);
            return;
        }

        var user = userResult.Value;
        user.AssignRole(adminRole.Id);

        context.Users.Add(user);

        await provider.GetRequiredService<IUnitOfWork>()
            .SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        LogAdminCreated(loginResult.Value.Value);
    }
}
