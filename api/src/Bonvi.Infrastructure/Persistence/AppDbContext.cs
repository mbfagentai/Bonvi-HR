using System.Linq.Expressions;
using System.Reflection;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Catalog;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Feedback;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Mcp;
using Bonvi.Domain.Notifications;
using Bonvi.Domain.Organization;
using Bonvi.Domain.PublicApi;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.Safety;
using Bonvi.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Bonvi.Infrastructure.Repositories;

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

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// ⚠️ <b>Ijro strategiyasi ORQALI</b> — boshqacha bo'lishi mumkin emas:
    /// ulanish <c>EnableRetryOnFailure</c> bilan sozlangan va bu strategiya
    /// qo'lda ochilgan tranzaksiyani to'g'ridan-to'g'ri RAD ETADI
    /// (<c>InvalidOperationException</c>). Strategiya esa butun ishni bitta
    /// takrorlanadigan birlik sifatida oladi.
    /// </para>
    /// <para>
    /// Takrorda kuzatuv TOZALANADI: birinchi urinishda qo'shilgan (hali
    /// bazaga yetmagan) qatorlar ikkinchisida yana qo'shilib, ikki nusxa
    /// bo'lib ketmasligi uchun. Ish esa kerakli hamma narsani o'zi qayta
    /// o'qiydi (interfeys izohi).
    /// </para>
    /// <para>
    /// <c>SaveChangesAsync</c> ochiq tranzaksiya ichida o'zinikini ochmaydi va
    /// har saqlashdan oldin savepoint qo'yadi — ya'ni ish ichida tutilgan
    /// saqlash xatosi (masalan unikal indeks) tranzaksiyani «buzilgan» holda
    /// qoldirmaydi va ish davom eta oladi.
    /// </para>
    /// </remarks>
    public Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> work,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        if (Database.CurrentTransaction is not null)
        {
            return work(cancellationToken);
        }

        var attempt = 0;

        return Database.CreateExecutionStrategy().ExecuteAsync(
            async token =>
            {
                if (attempt++ > 0)
                {
                    ChangeTracker.Clear();
                }

                await using var transaction = await Database.BeginTransactionAsync(token).ConfigureAwait(false);

                var result = await work(token).ConfigureAwait(false);

                await transaction.CommitAsync(token).ConfigureAwait(false);

                return result;
            },
            cancellationToken);
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

    /// <summary>Yo'qlik turlari — davomat so'rovlari shu ro'yxatdan tanlanadi.</summary>
    public DbSet<AbsenceType> AbsenceTypes => Set<AbsenceType>();

    /// <summary>«Qayerdan bilgan» kanallari — nomzod ish haqida qayerdan bilgani (<c>referral_channels</c>).</summary>
    public DbSet<ReferralChannel> ReferralChannels => Set<ReferralChannel>();

    /// <summary>Jadval so'rovlari (SPEC-verifikatsiya-jadval-sorovi §C).</summary>
    public DbSet<ScheduleChangeRequest> ScheduleChangeRequests => Set<ScheduleChangeRequest>();

    public DbSet<ScheduleChangeDay> ScheduleChangeDays => Set<ScheduleChangeDay>();

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

    /// <summary>Ochilgan ish o'rinlari (<c>vacancies</c>).</summary>
    public DbSet<Vacancy> Vacancies => Set<Vacancy>();

    public DbSet<VacancyRequest> VacancyRequests => Set<VacancyRequest>();

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

    /// <summary>Ochiq onlayn formada to'ldirilgan anketalar — HR-F02 (<c>candidate_forms</c>).</summary>
    public DbSet<CandidateForm> CandidateForms => Set<CandidateForm>();

    // ────────────────────────────────────────────────────────────
    //  DAVOMAT / VERIFIKATSIYA (3-bosqich)
    //  Turniket va yuz terminallari to'g'ridan-to'g'ri bizga yozadi.
    //  Ikkita ajratish butun modulning asosi:
    //    • XOM O'TISH (`attendance_passes`) — qurilma nima aytgani,
    //      o'zgarmaydi;
    //    • HISOBLANGAN KUN (`attendance_days`) — biz undan nima xulosa
    //      qilganimiz, jadval yoki moslik tuzatilsa qayta yoziladi.
    //  Qo'lda tahrir YO'Q: noto'g'ri son manbada tuzatiladi
    //  (moslik, jadval, qurilma soati), keyin kun qayta hisoblanadi.
    // ────────────────────────────────────────────────────────────

    /// <summary>Terminallar reyestri (<c>attendance_devices</c>).</summary>
    public DbSet<AttendanceDevice> AttendanceDevices => Set<AttendanceDevice>();

    /// <summary>Qurilma raqami ↔ xodim mosliklari (<c>attendance_identities</c>).</summary>
    /// <remarks>
    /// <c>Employment</c> dagi bitta «Verifix ID» ustunining O'RNIGA turadi
    /// va u ataylab qurilmadi — sababi <see cref="AttendanceIdentity"/>
    /// izohida.
    /// </remarks>
    public DbSet<AttendanceIdentity> AttendanceIdentities => Set<AttendanceIdentity>();

    /// <summary>Xom o'tishlar (<c>attendance_passes</c>).</summary>
    public DbSet<AttendancePass> AttendancePasses => Set<AttendancePass>();

    /// <summary>Ish jadvallari (<c>work_schedules</c>).</summary>
    public DbSet<WorkSchedule> WorkSchedules => Set<WorkSchedule>();

    /// <summary>Jadval kunlari (<c>work_schedule_days</c>).</summary>
    /// <remarks>
    /// Yozish DOIM jadval agregati orqali ketadi (yettala kun uning
    /// invarianti). Bu to'plam o'qish modeli uchun kerak: tabel bir oyga
    /// yettita kunni bir marta o'qiydi, jadvallarni bolalari bilan
    /// yuklamasdan — <see cref="CandidateFiles"/> bilan bir xil qaror.
    /// </remarks>
    public DbSet<WorkScheduleDay> WorkScheduleDays => Set<WorkScheduleDay>();

    /// <summary>Jadvalning aniq sanaga yozilgan kunlari — yillik to'r.</summary>
    public DbSet<WorkScheduleDate> WorkScheduleDates => Set<WorkScheduleDate>();

    /// <summary>Xodimning qurilmadagi holati (agent yozgani).</summary>
    public DbSet<DeviceEnrollment> DeviceEnrollments => Set<DeviceEnrollment>();

    /// <summary>Jadval biriktirmalari (<c>schedule_assignments</c>).</summary>
    public DbSet<ScheduleAssignment> ScheduleAssignments => Set<ScheduleAssignment>();

    /// <summary>Hisoblangan kunlar — tabelning kataklari (<c>attendance_days</c>).</summary>
    public DbSet<AttendanceDay> AttendanceDays => Set<AttendanceDay>();

    /// <summary>Yo'qlik so'rovlari (SPEC-so'rovlar §B2).</summary>
    public DbSet<AbsenceRequest> AbsenceRequests => Set<AbsenceRequest>();

    // ────────────────────────────────────────────────────────────
    //  XODIM ILOVASI (PWA) — telefondan belgilash
    //  Uchta yangi jadval va bitta mavjud ustun. Ajratish davomat
    //  modulinikidek: OBYEKT — qayerda qabul qilinadi degan
    //  ma'lumotnoma, KOORDINATA esa qabul qilingan belgining dalili.
    //  Belgining o'zi oddiy `attendance_passes` qatori bo'lib yoziladi,
    //  ya'ni tabel, taxta va hisobotlarga yangi qoida qo'shilmaydi.
    // ────────────────────────────────────────────────────────────

    /// <summary>Obyektlar — geo-to'siq ma'lumotnomasi (<c>work_sites</c>).</summary>
    public DbSet<WorkSite> WorkSites => Set<WorkSite>();

    /// <summary>Telefondan kelgan belgilarning koordinatalari (<c>mobile_check_ins</c>).</summary>
    /// <remarks>
    /// Agregat ildizi EMAS: qatorga faqat o'z o'tishi orqali murojaat
    /// qilinadi. Bu to'plam yozish porti va jurnal o'qishi uchun
    /// (<c>WorkScheduleDays</c> bilan bir xil qaror).
    /// </remarks>
    public DbSet<MobileCheckIn> MobileCheckIns => Set<MobileCheckIn>();

    /// <summary>Xodimlarning ilova hisoblari (<c>employee_accounts</c>).</summary>
    /// <remarks>
    /// ⚠️ <see cref="Users"/> dan ATAYLAB alohida: bu hisoblarda rol ham,
    /// ruxsat ham yo'q va bo'lmasligi ham kerak (sababi
    /// <see cref="EmployeeAccount"/> izohida).
    /// </remarks>
    public DbSet<EmployeeAccount> EmployeeAccounts => Set<EmployeeAccount>();

    /// <summary>Ilova hisoblarining yangilash tokenlari (<c>employee_refresh_tokens</c>).</summary>
    public DbSet<EmployeeRefreshToken> EmployeeRefreshTokens => Set<EmployeeRefreshToken>();

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

    // ────────────────────────────────────────────────────────────
    //  AI ULANISH (MCP) — `docs/SPEC-mcp.md`.
    //  ⚠️ Tokenlar va kodlarning O'ZI yo'q — faqat SHA-256 xeshi.
    // ────────────────────────────────────────────────────────────

    /// <summary>Ro'yxatdan o'tgan AI mijozlari (<c>mcp_clients</c>).</summary>
    public DbSet<McpClient> McpClients => Set<McpClient>();

    /// <summary>Ulanishlar — foydalanuvchi mijozga bergan ruxsat (<c>mcp_grants</c>).</summary>
    public DbSet<McpGrant> McpGrants => Set<McpGrant>();

    /// <summary>Bir martalik avtorizatsiya kodlari (<c>mcp_auth_codes</c>).</summary>
    public DbSet<McpAuthCode> McpAuthCodes => Set<McpAuthCode>();

    /// <summary>Kirish va yangilash tokenlari (<c>mcp_tokens</c>).</summary>
    public DbSet<McpToken> McpTokens => Set<McpToken>();

    /// <summary>Har bir vosita chaqiruvining jurnali (<c>mcp_audit</c>).</summary>
    public DbSet<McpAuditEntry> McpAudit => Set<McpAuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // Har bir entity uchun konfiguratsiya fayllarini avtomatik topadi
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());


        ApplySoftDeleteFilters(modelBuilder);

        /*
          Qurilmaga yoziladigan shaxs raqamlari (SPEC-agent §D1). Ketma-ketlik
          entity'ga bog'lanmagan: raqam moslik yozuvida yashaydi va uni
          «eng kattasi + 1» bilan hisoblash ikki agent bir vaqtda ro'yxat
          so'raganda bir xil raqam berardi.
        */
        modelBuilder
            .HasSequence<long>(DevicePersonNumbers.SequenceName)
            .StartsAt(1)
            .IncrementsBy(1)
            .HasMax(99_999_999);

        // «Anketa №» (SPEC-planshet-anketa §6.1) — xuddi shu sabab: «eng kattasi + 1»
        // bir vaqtda yuborilgan ikki anketaga bir xil raqam berardi.
        modelBuilder
            .HasSequence<int>(Configurations.CandidateFormConfiguration.NumberSequence)
            .StartsAt(1)
            .IncrementsBy(1);

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
