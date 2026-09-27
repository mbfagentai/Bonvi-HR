using System.Linq.Expressions;
using System.Reflection;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Catalog;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Feedback;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Notifications;
using Bonvi.Domain.Organization;
using Bonvi.Domain.PublicApi;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.Safety;
using Bonvi.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Bonvi.Infrastructure.Persistence;

/// <summary>
/// Ma'lumotlar bazasi konteksti.
/// </summary>
/// <remarks>
/// <para>
/// Bu yerda <c>DbSet</c> lar entity qo'shilgan sari e'lon qilinadi.
/// Jadval konfiguratsiyasi <c>Persistence/Configurations</c> papkasida —
/// har bir entity uchun alohida fayl (<c>IEntityTypeConfiguration</c>).
/// </para>
/// <para>
/// Ikkita global qoida avtomatik qo'llanadi:
/// </para>
/// <list type="number">
///   <item><b>snake_case</b> — PostgreSQL'da jadval va ustun nomlari
///         <c>employee_document</c> ko'rinishida bo'ladi, <c>EmployeeDocument</c> emas.
///         Sabab: PostgreSQL katta harfli nomlarni qo'shtirnoqsiz tanimaydi.</item>
///   <item><b>Yumshoq o'chirish filtri</b> — <c>ISoftDeletable</c> entity'lar
///         so'rovlarda avtomatik yashiriladi. O'chirilgan yozuvni ko'rish uchun
///         ataylab <c>IgnoreQueryFilters()</c> chaqirish kerak.</item>
/// </list>
/// </remarks>
public class AppDbContext : DbContext, IUnitOfWork
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
        ChangeTracker.Tracked += OnEntityTracked;
    }

    /// <summary>
    /// Domen bergan identifikator tufayli yangi bola yozuvlar «mavjud» deb
    /// hisoblanib qolishining oldini oladi.
    /// </summary>
    /// <remarks>
    /// <para><b>Muammo.</b> Bizda identifikatorni domen beradi
    /// (<c>Entity.NewId()</c> → <c>Guid.CreateVersion7()</c>). Ya'ni yangi
    /// yaratilgan obyektda kalit ALLAQACHON to'ldirilgan bo'ladi.</para>
    ///
    /// <para>
    /// EF Core kuzatilayotgan otaning navigatsiyasidan yangi bola topganda
    /// uning holatini <c>IsKeySet</c> bo'yicha taxmin qiladi: kalit bor
    /// bo'lsa — «bu bazada mavjud yozuv» deb hisoblaydi va INSERT o'rniga
    /// UPDATE yuboradi. Natija:
    /// <c>«expected to affect 1 row(s), but actually affected 0»</c>.
    /// </para>
    ///
    /// <para>
    /// Bu faqat MAVJUD agregatga bola qo'shilganda yuzaga chiqadi. Yangi
    /// agregat yaratilganda butun graf <c>Added</c> bo'lgani uchun muammo
    /// ko'rinmaydi — shu sababli u yaratish va urug'lantirish testlaridan
    /// o'tib ketadi va faqat ishlab turgan tizimda 500 xato bo'lib chiqadi.
    /// </para>
    ///
    /// <para><b>Yechim.</b> <c>FromQuery</c> — ishonchli belgi:
    /// bazadan o'qilgan yozuvda u <c>true</c>, endigina yaratilganda esa
    /// <c>false</c>. Demak «so'rovdan kelmagan va <c>Modified</c> deb
    /// belgilangan» yozuv — bu har doim yangi yozuv.
    /// </para>
    ///
    /// <para>
    /// Bu tuzatish bitta joyda turadi va <b>barcha</b> agregatlarga
    /// tegishli: lavozim vazifalari, mehnat hodisalari, hujjatlar,
    /// rag'bat va jarimalar — kelajakda qo'shiladiganlari ham.
    /// </para>
    ///
    /// <para>
    /// Cheklov: agar biror joyda mavjud yozuvni <c>Attach</c> qilib,
    /// keyin uni yangilamoqchi bo'lsak, bu ishlovchi xalaqit beradi.
    /// Loyihada bunday yo'l yo'q — barcha o'zgartirishlar repozitoriy
    /// orqali «yukla → o'zgartir → saqla» tartibida bajariladi.
    /// </para>
    /// </remarks>
    private static void OnEntityTracked(object? sender, EntityTrackedEventArgs e)
    {
        if (!e.FromQuery && e.Entry.State == EntityState.Modified)
        {
            e.Entry.State = EntityState.Added;
        }
    }

    /// <summary>Tizim foydalanuvchilari (<c>users</c>).</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>Rollar va ularning ruxsatlari (<c>roles</c>).</summary>
    public DbSet<Role> Roles => Set<Role>();

    /// <summary>Yangilash tokenlari (<c>refresh_tokens</c>).</summary>
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>O'zgarishlar jurnali (<c>audit_logs</c>).</summary>
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // ────────────────────────────────────────────────────────────
    //  MA'LUMOTNOMALAR (S2)
    //  Jadval nomlari konfiguratsiyalarda ATAYLAB aniq yozilgan
    //  (`ConfigureReference("…")`), chunki `lower(name)` bo'yicha
    //  unikal indekslar migratsiyada xom SQL bilan yaratiladi.
    // ────────────────────────────────────────────────────────────

    /// <summary>Yuridik shaxslar (<c>legal_entities</c>).</summary>
    public DbSet<LegalEntity> LegalEntities => Set<LegalEntity>();

    /// <summary>Biznes yo'nalishlari (<c>directions</c>).</summary>
    public DbSet<Direction> Directions => Set<Direction>();

    /// <summary>Bo'limlar daraxti (<c>departments</c>).</summary>
    public DbSet<Department> Departments => Set<Department>();

    /// <summary>Lavozimlar (<c>positions</c>).</summary>
    public DbSet<Position> Positions => Set<Position>();

    /// <summary>Lavozim vazifalari (<c>position_duties</c>).</summary>
    public DbSet<PositionDuty> PositionDuties => Set<PositionDuty>();

    /// <summary>Ishdan ketish sabablari (<c>termination_reasons</c>).</summary>
    public DbSet<TerminationReason> TerminationReasons => Set<TerminationReason>();

    /// <summary>Rag'bat turlari (<c>award_types</c>).</summary>
    public DbSet<AwardType> AwardTypes => Set<AwardType>();

    /// <summary>Jarima turlari (<c>penalty_types</c>).</summary>
    public DbSet<PenaltyType> PenaltyTypes => Set<PenaltyType>();

    /// <summary>Hujjat turlari (<c>document_types</c>).</summary>
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();

    // ────────────────────────────────────────────────────────────
    //  XODIMLAR VA MEHNAT MUNOSABATLARI (S4/S5)
    //  Xodim — SHAXS, mehnat munosabati — uning yuridik shaxs bilan
    //  bog'lanishi. Ular ataylab ajratilgan: bitta odam bir vaqtda ikki
    //  firmada ishlashi va ketib qaytib kelishi mumkin.
    // ────────────────────────────────────────────────────────────

    /// <summary>Xodimlar (<c>employees</c>).</summary>
    public DbSet<Employee> Employees => Set<Employee>();

    /// <summary>Mehnat munosabatlari (<c>employments</c>).</summary>
    public DbSet<Employment> Employments => Set<Employment>();

    /// <summary>Mehnat yo'lidagi hodisalar (<c>employment_events</c>).</summary>
    public DbSet<EmploymentEvent> EmploymentEvents => Set<EmploymentEvent>();

    /// <summary>Sinov muddatlari (<c>probations</c>).</summary>
    public DbSet<Probation> Probations => Set<Probation>();

    /// <summary>Xodim belgilari: rezerv, qora ro'yxat, nazorat (<c>employee_flags</c>).</summary>
    public DbSet<EmployeeFlag> EmployeeFlags => Set<EmployeeFlag>();

    /// <summary>Qobiliyatlar profili: til, kompyuter, ta'lim (<c>employee_competencies</c>).</summary>
    public DbSet<EmployeeCompetency> EmployeeCompetencies => Set<EmployeeCompetency>();

    /// <summary>Profildagi tillar: til + daraja (<c>employee_competency_languages</c>).</summary>
    public DbSet<EmployeeCompetencyLanguage> EmployeeCompetencyLanguages => Set<EmployeeCompetencyLanguage>();

    /// <summary>Rag'bat va jarima (<c>incentives</c>).</summary>
    public DbSet<Incentive> Incentives => Set<Incentive>();

    /// <summary>Xodimga biriktirilgan fayllar: shartnoma, buyruq, anketa, skan (<c>employee_documents</c>).</summary>
    public DbSet<EmployeeDocument> EmployeeDocuments => Set<EmployeeDocument>();
    public DbSet<EmployeeCheckFile> EmployeeCheckFiles => Set<EmployeeCheckFile>();

    // ────────────────────────────────────────────────────────────
    //  QAYTA ALOQA VA XAVFSIZLIK (S9 — M10/M11)
    // ────────────────────────────────────────────────────────────

    /// <summary>Ishdan ketuvchi xodim bilan suhbatlar (<c>exit_interviews</c>).</summary>
    public DbSet<ExitInterview> ExitInterviews => Set<ExitInterview>();

    /// <summary>Baxtsiz hodisalar jurnali (<c>incidents</c>).</summary>
    public DbSet<Incident> Incidents => Set<Incident>();

    // ────────────────────────────────────────────────────────────
    //  REKRUTING (2-bosqich)
    //  Voronka: vakansiya → nomzod → bosqichlar → ishga olish
    //  (1-bosqichning ishga qabul oqimi). Bosqichlar ro'yxati KODDA emas,
    //  ma'lumotnomada — foydalanuvchi qarori (sababi `PipelineStage` da).
    // ────────────────────────────────────────────────────────────

    /// <summary>Voronka bosqichlari — ma'lumotnoma (<c>pipeline_stages</c>).</summary>
    public DbSet<PipelineStage> PipelineStages => Set<PipelineStage>();

    /// <summary>
    /// Vakansiya nomlari — ma'lumotnoma (<c>vacancy_titles</c>).
    /// </summary>
    /// <remarks>
    /// E'londa turadigan qisqa nomlar. Bu <b>lavozimlar ma'lumotnomasi
    /// emas</b> va u bilan hech qanday bog'lanishi ham yo'q — nega ataylab
    /// shunday qilingani <see cref="VacancyTitle"/> izohida.
    /// </remarks>
    public DbSet<VacancyTitle> VacancyTitles => Set<VacancyTitle>();

    /// <summary>Ochilgan ish o'rinlari (<c>vacancies</c>).</summary>
    public DbSet<Vacancy> Vacancies => Set<Vacancy>();

    /// <summary>Nomzodlar (<c>candidates</c>).</summary>
    public DbSet<Candidate> Candidates => Set<Candidate>();

    /// <summary>Nomzodning bosqichlardan o'tishi (<c>candidate_stage_entries</c>).</summary>
    public DbSet<CandidateStageEntry> CandidateStageEntries => Set<CandidateStageEntry>();

    /// <summary>Nomzodga biriktirilgan fayllar (<c>candidate_files</c>).</summary>
    /// <remarks>
    /// Yozish DOIM nomzod agregati orqali ketadi (fayllar soni va hajmi —
    /// uning invarianti, sababi <see cref="CandidateFile"/> izohida). Bu
    /// to'plam o'qish modeli uchun kerak: nomzodlar RO'YXATIDA «fayli
    /// bormi» belgisi shu jadvalga bitta <c>EXISTS</c> so'rovi bilan
    /// hisoblanadi, nomzodni bolalari bilan yuklamasdan.
    /// </remarks>
    public DbSet<CandidateFile> CandidateFiles => Set<CandidateFile>();

    /// <summary>Amallar izohlari (<c>candidate_notes</c>).</summary>
    public DbSet<CandidateNote> CandidateNotes => Set<CandidateNote>();

    /// <summary>Botda to'ldirilgan anketalar (<c>candidate_questionnaires</c>).</summary>
    public DbSet<CandidateQuestionnaire> CandidateQuestionnaires => Set<CandidateQuestionnaire>();

    // ────────────────────────────────────────────────────────────
    //  BILDIRISHNOMALAR
    //  ⚠️ Bildirishnomalarning O'ZI bu yerda YO'Q va bo'lmaydi ham:
    //  ular saqlanmaydi, so'rov kelganda joriy ma'lumotdan HISOBLANADI
    //  (sababi `INotificationReadModel` izohida). Bazada faqat
    //  foydalanuvchining qarori qoladi: nimani o'qidi, nimani o'chirdi.
    // ────────────────────────────────────────────────────────────

    /// <summary>Kim nimani o'qigan/o'chirgan (<c>notification_states</c>).</summary>
    public DbSet<NotificationState> NotificationStates => Set<NotificationState>();

    // ────────────────────────────────────────────────────────────
    //  TIZIM SOZLAMALARI
    //  Kalit-qiymat jadvali: bot matni va bildirishnoma chegaralari.
    //  ⚠️ Bu yerda faqat O'ZGARTIRILGAN sozlamalar yotadi — jadval bo'sh
    //  bo'lishi NORMAL holat va tizim shunda ham to'liq ishlaydi
    //  (sukut qiymatlar `SystemSettingKeys` da, sababi o'sha yerda).
    // ────────────────────────────────────────────────────────────

    /// <summary>Tizim sozlamalari (<c>system_settings</c>).</summary>
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    // ────────────────────────────────────────────────────────────
    //  TASHQI API
    //  Holdingning boshqa loyihalariga berilgan FAQAT O'QISH kalitlari.
    //  ⚠️ Kalitning O'ZI bu yerda YO'Q — faqat prefiksi va xeshi
    //  (sababi `ApiKey` izohida).
    // ────────────────────────────────────────────────────────────

    /// <summary>Tashqi API kalitlari (<c>api_keys</c>).</summary>
    /// <remarks>
    /// Yozuvlar HECH QACHON o'chirilmaydi va jadval <c>ISoftDeletable</c>
    /// ham emas: bekor qilingan kalit tarix bo'lib qoladi.
    /// </remarks>
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // Har bir entity uchun konfiguratsiya fayllarini avtomatik topadi
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        ApplySoftDeleteFilters(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// <see cref="ISoftDeletable"/> ni amalga oshirgan barcha entity'larga
    /// <c>WHERE deleted_at IS NULL</c> filtrini qo'shadi.
    /// </summary>
    private static void ApplySoftDeleteFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var property = Expression.Property(parameter, nameof(ISoftDeletable.DeletedAt));
            var condition = Expression.Equal(property, Expression.Constant(null, typeof(DateTimeOffset?)));
            var lambda = Expression.Lambda(condition, parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }
    }
}
