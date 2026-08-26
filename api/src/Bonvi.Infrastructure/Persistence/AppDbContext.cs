using System.Linq.Expressions;
using System.Reflection;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Catalog;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Organization;
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

    /// <summary>Rag'bat va jarima (<c>incentives</c>).</summary>
    public DbSet<Incentive> Incentives => Set<Incentive>();

    /// <summary>Xodimga biriktirilgan fayllar: shartnoma, buyruq, anketa, skan (<c>employee_documents</c>).</summary>
    public DbSet<EmployeeDocument> EmployeeDocuments => Set<EmployeeDocument>();

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
