using System.Diagnostics.CodeAnalysis;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Identity.Abstractions;
using Bonvi.Application.Recruiting.Candidates;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.Settings;
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

    /// <summary>Telegram anketa boti hisobining telefon raqami.</summary>
    /// <remarks>
    /// <para>
    /// Bu ODAM emas, xizmat hisobi: bot uning nomidan API ga kirib nomzod
    /// yozadi. Login telefon raqami bo'lishi shart, chunki tizimda
    /// identifikator shu (<c>User.Login</c>) — kompaniyaning ishlatilmayotgan
    /// raqamini bering.
    /// </para>
    /// <para>
    /// Ikkalasi (<c>BotLogin</c> va <c>BotPassword</c>) berilmasa hisob
    /// UMUMAN yaratilmaydi va bot ishga tushmaydi. Bu ataylab: sukut bo'yicha
    /// parol qo'yish — eng oson unutiladigan xavf.
    /// </para>
    /// </remarks>
    public string? BotLogin { get; set; }

    /// <summary>Telegram anketa boti hisobining paroli.</summary>
    public string? BotPassword { get; set; }

    /// <summary>Bot hisobining ekranda ko'rinadigan nomi.</summary>
    public string BotFullName { get; set; } = "Telegram bot";

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
    /// <summary>
    /// «Tushadigan joy» vakansiyasining nomi (<c>vacancies.title</c>).
    /// </summary>
    /// <remarks>
    /// Nom nomzodga KO'RINMAYDI (bot bu vakansiyani ro'yxatda
    /// chizmaydi), lekin HR ekranlarida — voronkada, nomzod kartasida —
    /// aynan shu matn turadi. Shu sababli u tushunarli bo'lishi kerak.
    /// </remarks>
    public const string GeneralPoolTitle = "Umumiy anketa";

    /// <summary>Vakansiya kartasidagi izoh — nima uchun kerakligini tushuntiradi.</summary>
    /// <remarks>
    /// Izoh HR uchun va u OGOHLANTIRISH ham: bu vakansiya qo'lda
    /// ochilmagan, uni yopish esa vakansiyasiz kelgan anketalarni
    /// butunlay to'xtatib qo'yadi (urug' yopilganini qayta ochmaydi —
    /// sabab <see cref="SeedGeneralPoolVacancyAsync"/> izohida).
    /// </remarks>
    private const string GeneralPoolNote =
        "Tizim vakansiyasi: ochiq ish o'rni bo'lmaganda nomzod anketasi shu yerga tushadi. "
        + "Yopilsa, bunday anketalar qabul qilinmaydi.";

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

    [LoggerMessage(
        EventId = 2010,
        Level = LogLevel.Information,
        Message = "Nomzodlik narsalari ko'chirildi: {Employees} xodim, {Files} fayl, {Photos} surat, {Profiles} profil")]
    private partial void LogHandoverDone(int employees, int files, int photos, int profiles);

    [LoggerMessage(
        EventId = 2011,
        Level = LogLevel.Warning,
        Message = "Nomzodlik narsalarini ko'chirib bo'lmadi: {CandidateId}")]
    private partial void LogHandoverFailed(Exception error, Guid candidateId);

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

    [LoggerMessage(
        EventId = 2006,
        Level = LogLevel.Information,
        Message = "Telegram bot hisobi yaratildi: {Login}")]
    private partial void LogBotUserCreated(string login);

    [LoggerMessage(
        EventId = 2007,
        Level = LogLevel.Warning,
        Message = "Telegram bot hisobi yaratilmadi: {Reason}")]
    private partial void LogBotUserSkipped(string reason);

    [LoggerMessage(
        EventId = 2008,
        Level = LogLevel.Information,
        Message = "«{Title}» vakansiyasi tayyor: {VacancyId}")]
    private partial void LogGeneralPoolSeeded(string title, Guid vacancyId);

    [LoggerMessage(
        EventId = 2009,
        Level = LogLevel.Warning,
        Message = "«{Title}» vakansiyasi tayyorlanmadi: {Reason}")]
    private partial void LogGeneralPoolSkipped(string title, string reason);

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

        /*
          Har bir urug' o'z audit amali bilan o'raladi (SPEC-rollar-va-audit §7.4):
          yozgan qatorlari «tizim, startup:<nom>» bo'lib jurnalga tushadi.
          Odatiy (ikkinchi va keyingi) ishga tushishda urug'lar hech narsa
          yozmaydi — ya'ni jurnalga ham hech narsa qo'shilmaydi.
        */
        var auditScope = provider.GetRequiredService<IAuditScope>();

        // ---------- Rollar ----------
        using (auditScope.Use("startup:identity-seed"))
        {
            await provider.GetRequiredService<IdentitySeeder>().SeedAsync(cancellationToken).ConfigureAwait(false);
        }

        LogRolesSeeded();

        // ---------- Ma'lumotnomalar ----------
        // Excel'dan tozalab olingan ro'yxatlar: yuridik shaxslar, yo'nalishlar,
        // bo'limlar daraxti, lavozimlar, ishdan ketish sabablari va boshqalar.
        // Har bir ma'lumotnoma FAQAT bazada butunlay BO'SH bo'lsa quyiladi —
        // birorta yozuvi bo'lsa, butun JSON fayli o'tkazib yuboriladi (sababi
        // `CatalogSeeder` ichida: HR tuzatgan nomni seeder qayta yaratmasligi
        // kerak). Shu sababli har ishga tushishda chaqirish xavfsiz, lekin
        // «yetishmayotgani qo'shiladi» degani emas — bo'lim ro'yxatiga keyin
        // qo'lda bitta qator qo'shilsa ham, seeder uni endi to'ldirmaydi.
        CatalogSeedResult catalog;

        using (auditScope.Use("startup:catalog-seed"))
        {
            catalog = await provider.GetRequiredService<CatalogSeeder>()
                .SeedAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        LogCatalogSeeded(catalog.Created, catalog.Skipped, catalog.MissingFiles);

        // ---------- «Umumiy anketa» vakansiyasi ----------
        using (auditScope.Use("startup:general-pool"))
        {
            await SeedGeneralPoolVacancyAsync(provider, cancellationToken).ConfigureAwait(false);
        }

        // Ma'lumotnomalardan KEYIN: ko'chirish «Номзодлик ҳужжатлари»
        // turini nomi bo'yicha qidiradi va u yuqorida urug'lanadi.
        using (auditScope.Use("startup:candidate-handover"))
        {
            await HandOverCandidateBelongingsAsync(provider, cancellationToken).ConfigureAwait(false);
        }

        // ---------- Birinchi administrator ----------
        using (auditScope.Use("startup:first-admin"))
        {
            await CreateFirstAdminAsync(provider, options, cancellationToken).ConfigureAwait(false);
        }

        // ---------- Telegram bot hisobi ----------
        using (auditScope.Use("startup:bot-user"))
        {
            await CreateBotUserAsync(provider, options, cancellationToken).ConfigureAwait(false);
        }

        // ---------- Sinov hisoblari (faqat Development) ----------
        if (options.CreateDevUsers && _environment.IsDevelopment())
        {
            using (auditScope.Use("startup:dev-users"))
            {
                await CreateDevUsersAsync(provider, options, cancellationToken).ConfigureAwait(false);
            }
        }

        /*
          AI ulanish (MCP) — o'qish rolini MIGRATSIYADAN KEYIN tayyorlash:
          ruxsat ro'yxati funksiyasini migratsiya yaratadi va har ishga
          tushishda uni qayta qo'llash yangi jadvallarni ham hisobga oladi.
          Natija (tayyor / nosoz + sabab) `McpRuntime` da turadi; MCP
          sozlanmagan bo'lsa bazaga tegilmaydi. Xato ilovani YIQITMAYDI —
          MCP yopiq qoladi, qolgan tizim ishlayveradi (`docs/SPEC-mcp.md` §2.2).
        */
        await _services.GetRequiredService<Mcp.McpRuntime>().CheckAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// ⭐ Allaqachon ishga olinganlarning nomzodlik narsalarini
    /// xodim kartasiga ko'chiradi — BIR MARTALIK tuzatish.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega kerak.</b> Ko'chirish ishga qabul oqimiga 2026-09-20 da
    /// qo'shildi; undan OLDIN ishga olinganlarning surati, fayllari va
    /// anketasi nomzod kartasida qolib ketgan. Muammoning o'zi ham aynan
    /// shundan boshlangan: HR anketa suratlarini qaytadan yuklashga
    /// majbur bo'lgan. Kod tuzatilgani bilan eski yozuvlar o'z-o'zidan
    /// tuzalmaydi.
    /// </para>
    /// <para>
    /// <b>Nega migratsiyada emas.</b> Ko'chirish hujjat turini NOMI
    /// bo'yicha qidiradi, tur esa ma'lumotnoma urug'ida yaratiladi —
    /// migratsiya paytida u hali mavjud emas. Ustiga-ustak amal diskdagi
    /// faylga tegadi (surat nusxasi), bu esa SQL ning ishi emas.
    /// </para>
    /// <para>
    /// <b>Amal IDEMPOTENT va O'ZI TUGAYDI.</b> So'rov faqat haqiqatan
    /// ko'chiriladigan narsasi borlarni tanlaydi: fayli bor, yoki surati
    /// bor-u xodimda surat yo'q, yoki anketasi (yoki HR izohi) bor-u xodimda qobiliyatlar
    /// profili yo'q. Birinchi yurishdan keyin ro'yxat bo'shab qoladi va
    /// keyingi ishga tushishlar hech narsa qilmaydi.
    /// </para>
    /// <para>
    /// <b>Bitta yozuv yiqilsa, qolganlari ketaveradi.</b> Har xodim
    /// alohida saqlanadi: eski ma'lumotda kutilmagan holat bo'lishi
    /// mumkin (masalan diskda yo'q fayl) va u butun yurishni
    /// to'xtatmasligi kerak. Ishga tushish esa umuman to'xtamaydi — bu
    /// tuzatish, tizimning ishlashi uchun shart emas.
    /// </para>
    /// </remarks>
    private async Task HandOverCandidateBelongingsAsync(
        IServiceProvider provider,
        CancellationToken cancellationToken)
    {
        var context = provider.GetRequiredService<AppDbContext>();

        var pairs = await (
                from item in context.EmploymentEvents
                join employment in context.Employments on item.EmploymentId equals employment.Id
                join candidate in context.Candidates on item.CandidateId!.Value equals candidate.Id
                where item.CandidateId != null
                      && (context.CandidateFiles.Any(file => file.CandidateId == candidate.Id)
                          || (candidate.PhotoKey != null
                              && context.Employees.Any(
                                  person => person.Id == employment.EmployeeId && person.PhotoKey == null))
                          || ((context.CandidateQuestionnaires.Any(form => form.CandidateId == candidate.Id)
                                  || candidate.Note != null
                                  || context.CandidateNotes.Any(note => note.CandidateId == candidate.Id)
                                  || context.CandidateStageEntries.Any(
                                      entry => entry.CandidateId == candidate.Id && entry.Conclusion != null))
                              && !context.EmployeeCompetencies.Any(
                                  profile => profile.EmployeeId == employment.EmployeeId)))
                select new { CandidateId = candidate.Id, employment.EmployeeId })
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (pairs.Count == 0)
        {
            return;
        }

        var candidates = provider.GetRequiredService<ICandidateRepository>();
        var employees = provider.GetRequiredService<IEmployeeRepository>();
        var handover = provider.GetRequiredService<CandidateHandover>();
        var unitOfWork = provider.GetRequiredService<IUnitOfWork>();

        var touched = 0;
        var files = 0;
        var photos = 0;
        var profiles = 0;

        foreach (var pair in pairs)
        {
            try
            {
                var candidate = await candidates
                    .GetWithStagesAsync(pair.CandidateId, cancellationToken)
                    .ConfigureAwait(false);

                var employee = await employees
                    .GetByIdAsync(pair.EmployeeId, cancellationToken)
                    .ConfigureAwait(false);

                if (candidate is null || employee is null)
                {
                    continue;
                }

                var moved = await handover
                    .ApplyAsync(candidate, employee, cancellationToken)
                    .ConfigureAwait(false);

                if (!moved.Photo && moved.Files == 0 && !moved.Competency)
                {
                    continue;
                }

                await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                touched++;
                files += moved.Files;
                photos += moved.Photo ? 1 : 0;
                profiles += moved.Competency ? 1 : 0;
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                LogHandoverFailed(error, pair.CandidateId);
            }
        }

        if (touched > 0)
        {
            LogHandoverDone(touched, files, photos, profiles);
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

            userResult.Value.SetRole(role.Id);
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

    /// <summary>
    /// «Umumiy anketa» vakansiyasini tayyorlaydi va uni sozlamaga bog'laydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega bunday vakansiya umuman kerak.</b> Nomzod OCHIQ VAKANSIYA
    /// BO'LMASA HAM anketa to'ldira olishi kerak, <c>Candidate.VacancyId</c>
    /// esa majburiy. Ustunni nullable qilish o'rniga doimiy «tushadigan
    /// joy» vakansiyasi tanlandi: bunda voronka, bosqichlar va rad etish
    /// bir zumda ishlab ketadi va mavjud ma'lumotga umuman tegilmaydi.
    /// </para>
    /// <para>
    /// <b>IDEMPOTENT.</b> Odatiy (ikkinchi va undan keyingi) ishga
    /// tushishda bu yerda ikkita <c>SELECT</c> bo'ladi va boshqa hech
    /// narsa: sozlama OCHIQ vakansiyani ko'rsatib turgan bo'lsa, yo'l shu
    /// yerda tugaydi.
    /// </para>
    /// <para>
    /// ⚠️ <b>Yopiq bo'lsa — QAYTA OCHILADI (2026-09-29).</b> Ilgari
    /// yozuvga tegilmasdi, chunki HR uni ataylab yopgan bo'lishi mumkin
    /// edi. Amalda prod'da u 2026-09-11 da yopilgan va 18 kun davomida
    /// botning «Anketa to'ldirish» orqali kelgan har bir arizasi rad
    /// etilgan. Endi uni yopib bo'lmaydi (<c>GeneralPoolVacancy</c>
    /// izohi), ya'ni yopiq holat — ruxsat etilmagan holat va u shu
    /// yerda tuzatiladi. Jurnalga ogohlantirish yoziladi.
    /// </para>
    /// <para>
    /// <b>Nega vakansiya YUMSHOQ O'CHIRILGANLARDAN izlanmaydi</b>
    /// (<c>CreateBotUserAsync</c> dagi <c>IgnoreQueryFilters</c> dan
    /// farqli). U yerda maqsad — unikal indeksga urilmaslik; bu yerda
    /// esa vakansiya ISHLASHI kerak. O'chirilgan yozuvni «bor» deb
    /// hisoblash tizimni tuzatib bo'lmaydigan holatda qoldirardi: bot
    /// nomzodni ko'rinmaydigan vakansiyaga yozishga urinardi.
    /// Nom esa registrga sezgir EMAS holda izlanadi — vakansiya nomining
    /// bandlik qoidasi bilan AYNAN bir xil
    /// (<c>IVacancyRepository.TitleTakenAsync</c>): HR «umumiy anketa»
    /// vakansiyasini qo'lda ochib qo'ygan bo'lsa, registri boshqa deb
    /// ikkinchisini ochish ro'yxatda ikkita bir xil qator hosil qilardi.
    /// </para>
    /// <para>
    /// Domen tekshiruvi rad etsa, urug' O'TKAZIB YUBORILADI va
    /// ogohlantirish yoziladi — ilova baribir ko'tariladi. Bu urug'
    /// tizimning ishlashi uchun shart emas: usiz faqat vakansiyasiz
    /// anketa qabul qilinmaydi, qolgan hamma narsa avvalgidek.
    /// </para>
    /// </remarks>
    [SuppressMessage(
        "Globalization",
        "CA1304:Specify CultureInfo",
        Justification = "Nom taqqoslanishi SQL'ga tarjima qilinadi (ReferenceRepository.NameEquals izohi).")]
    [SuppressMessage(
        "Globalization",
        "CA1311:Specify a culture or use an invariant version",
        Justification = "Nom taqqoslanishi SQL'ga tarjima qilinadi (ReferenceRepository.NameEquals izohi).")]
    [SuppressMessage(
        "Performance",
        "CA1862:Use the 'StringComparison' method overloads to perform case-insensitive string comparisons",
        Justification = "EF Core StringComparison variantini SQL'ga tarjima qila olmaydi.")]
    private async Task SeedGeneralPoolVacancyAsync(
        IServiceProvider provider,
        CancellationToken cancellationToken)
    {
        var context = provider.GetRequiredService<AppDbContext>();

        var setting = await context.SystemSettings
            .FirstOrDefaultAsync(row => row.Key == SystemSettingKeys.GeneralPoolVacancyId, cancellationToken)
            .ConfigureAwait(false);

        var linked = setting is not null && Guid.TryParse(setting.Value, out var known)
            ? await context.Vacancies
                .FirstOrDefaultAsync(vacancy => vacancy.Id == known, cancellationToken)
                .ConfigureAwait(false)
            : null;

        if (linked is not null)
        {
            /*
              Bog'langan yozuv bor — HOLATIGA tegilmaydi. Yopiq bo'lsa ham
              yopiq qoladi: «Umumiy anketa» ni yopish egasining ongli qarori
              (2026-10-05) va u «botda anketa topshirish ham yopiq» degani.
              Ilgari (2026-09-29) bu yerda yopiq yozuv har ishga tushishda
              QAYTA OCHILARDI — ya'ni har deploy HR yopgan anketani jimgina
              ochib yuborardi.
            */
            return;
        }

        // ---------- Vakansiyaning o'zi ----------
        // FAQAT ochig'i: yopilgan vakansiya nomzod qabul qilmaydi
        // (`Vacancy.AcceptsCandidates`), ya'ni unga bog'lanish botni
        // ishlamaydigan holatda qoldirardi. Tartib esa barqarorlik
        // uchun: bir xil bazada har doim bir xil yozuv tanlanadi.
        var normalizedTitle = GeneralPoolTitle.ToLowerInvariant();

        var vacancy = await context.Vacancies
            .Where(row => row.Title.ToLower() == normalizedTitle && row.Status == VacancyStatus.Open)
            .OrderBy(row => row.CreatedAt)
            .ThenBy(row => row.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        Vacancy? newVacancy = null;

        if (vacancy is null)
        {
            var opened = Vacancy.Open(
                GeneralPoolTitle,
                provider.GetRequiredService<IClock>().Today,
                note: GeneralPoolNote);

            if (opened.IsFailure)
            {
                LogGeneralPoolSkipped(GeneralPoolTitle, opened.Error.Message);
                return;
            }

            newVacancy = opened.Value;
        }

        var vacancyId = (vacancy ?? newVacancy!).Id;

        // ---------- Sozlama ----------
        /*
          Qiymatni `SetValue` YOZA OLMAYDI: kalit
          `SystemSettingKind.Hidden` turida va uni umumiy «sozlamalarni
          saqlash» so'rovi ham, bu yer ham bir xil eshikdan — SERVER
          yo'lidan — o'tkazadi (`SystemSetting.SetServerValue`).
        */
        var stored = vacancyId.ToString();

        if (setting is null)
        {
            var created = SystemSetting.CreateServerValue(SystemSettingKeys.GeneralPoolVacancyId, stored);
            if (created.IsFailure)
            {
                LogGeneralPoolSkipped(GeneralPoolTitle, created.Error.Message);
                return;
            }

            context.SystemSettings.Add(created.Value);
        }
        else
        {
            var applied = setting.SetServerValue(stored);
            if (applied.IsFailure)
            {
                LogGeneralPoolSkipped(GeneralPoolTitle, applied.Error.Message);
                return;
            }
        }

        // Yozuvlar SHU YERDA qo'shiladi — barcha tekshiruvlardan keyin.
        // Yuqorida qo'shilganda, tekshiruv yiqilgan yo'lda kontekstda
        // saqlanmagan yozuv qolib ketardi va uni keyingi urug'ning
        // `SaveChanges` i yozib yuborardi.
        if (newVacancy is not null)
        {
            context.Vacancies.Add(newVacancy);
        }

        await provider.GetRequiredService<IUnitOfWork>()
            .SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        LogGeneralPoolSeeded(GeneralPoolTitle, vacancyId);
    }

    /// <summary>
    /// Telegram anketa boti uchun xizmat hisobini yaratadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega alohida hisob, administratornikini ishlatmasdan.</b> Bot
    /// tokeni Telegram serverlariga chiqadi va sizib ketishi mumkin
    /// bo'lgan yagona sir. Uning ortidagi hisob eng kam huquqli bo'lishi
    /// kerak: <c>recruiting_bot</c> roli faqat ochiq vakansiyalarni o'qiy
    /// oladi va nomzod yoza oladi. Eng yomon holatda soxta nomzod paydo
    /// bo'ladi, xodimlar bazasi esa tegilmaydi.
    /// </para>
    /// <para>
    /// <b>Mavjud hisobga TEGILMAYDI.</b> Birinchi administrator bilan bir
    /// xil qoida: aks holda har qayta ishga tushishda parol tiklanib,
    /// uni o'zgartirishning ma'nosi qolmasdi.
    /// </para>
    /// <para>
    /// Sozlama bo'lmasa jimgina o'tkazib yuborilmaydi — ogohlantirish
    /// yoziladi, chunki «bot ishlamayapti» degan savolning javobi aynan
    /// shu qatordan boshlanadi.
    /// </para>
    /// </remarks>
    private async Task CreateBotUserAsync(
        IServiceProvider provider,
        BootstrapOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.BotLogin) || string.IsNullOrWhiteSpace(options.BotPassword))
        {
            LogBotUserSkipped("«Bootstrap:BotLogin» yoki «Bootstrap:BotPassword» berilmagan");
            return;
        }

        var phone = PhoneNumber.Create(options.BotLogin);
        if (phone.IsFailure)
        {
            LogBotUserSkipped($"telefon raqami noto'g'ri: {options.BotLogin}");
            return;
        }

        var context = provider.GetRequiredService<AppDbContext>();

        // `IgnoreQueryFilters` — o'chirilgan hisob ham hisobga olinadi:
        // aks holda uni qayta yaratishga urinib, unikal indeksga urilardik.
        var exists = await context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Login == phone.Value, cancellationToken)
            .ConfigureAwait(false);

        if (exists)
        {
            return;
        }

        var role = await context.Roles
            .FirstOrDefaultAsync(r => r.Code == SystemRoles.RecruitingBot, cancellationToken)
            .ConfigureAwait(false);

        if (role is null)
        {
            LogBotUserSkipped($"«{SystemRoles.RecruitingBot}» roli topilmadi");
            return;
        }

        var fullName = FullName.Parse(options.BotFullName);
        if (fullName.IsFailure)
        {
            LogBotUserSkipped($"nom noto'g'ri: {options.BotFullName}");
            return;
        }

        var hasher = provider.GetRequiredService<IPasswordHasher>();
        var user = User.Create(phone.Value, hasher.Hash(options.BotPassword), fullName.Value);

        if (user.IsFailure)
        {
            LogBotUserSkipped(user.Error.Message);
            return;
        }

        user.Value.SetRole(role.Id);
        context.Users.Add(user.Value);

        await provider.GetRequiredService<IUnitOfWork>()
            .SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        LogBotUserCreated(phone.Value.Value);
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

        // Boshlang'ich hisob SUPER ADMINISTRATOR bo'ladi — eski `admin`
        // emas. Ikki rolli modeldan qolgan bazalarda o'sha rol allaqachon
        // `super_admin` ga ko'chirilgan (`IdentitySeeder.Kochir`) va u
        // seeder bu yerdan OLDIN ishlaydi, ya'ni kod bu yerda doim yangi.
        var adminRole = await context.Roles
            .FirstOrDefaultAsync(r => r.Code == SystemRoles.SuperAdmin, cancellationToken)
            .ConfigureAwait(false);

        if (adminRole is null)
        {
            LogAdminFailed("«super_admin» roli topilmadi");
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
        user.SetRole(adminRole.Id);

        context.Users.Add(user);

        await provider.GetRequiredService<IUnitOfWork>()
            .SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        LogAdminCreated(loginResult.Value.Value);
    }
}
