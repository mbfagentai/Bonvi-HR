using System.Globalization;
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

namespace Bonvi.Infrastructure.Persistence.Auditing;

/// <summary>
/// Audit klassifikatsiyasi: qaysi entity yoziladi, qaysi bo'limga tegishli, qaysi xodimga
/// tegishli, qanday nomlanadi va qaysi maydonlari yashiriladi yoki tashlab ketiladi
/// (SPEC-rollar-va-audit §7.3, §14.5).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>EF modelidagi HAR BIR entity shu yerda — yo <see cref="Audited"/> da, yo
/// <see cref="NotAudited"/> da (sababi bilan).</b> Buni <c>AuditCatalogCoverageTests</c>
/// qadaydi: yangi jadval qo'shgan odam uni ongli ravishda klassifikatsiya
/// qilmaguncha testlar yashil bo'lmaydi. Nomida <c>Hash</c>, <c>Token</c>,
/// <c>Secret</c> yoki <c>Password</c> bo'lgan har bir maydon ham yashirilgan yoki
/// tashlab ketilgan bo'lishi shart — sir jurnalga sizib tushmasin.
/// </para>
/// <para>
/// ⚠️ <b>Fon yozuvchilari jurnalni to'ldirib yubormasligi kerak.</b> Har daqiqada
/// yangilanadigan maydonlar (qurilma «oxirgi aloqa» si, hisobning oxirgi kirishi,
/// kalitning oxirgi ishlatilishi) va kursor sozlamalari TASHLAB KETILADI: tahrir
/// faqat ulargagina tegsa, o'zgarishlar ro'yxati bo'sh qoladi va qator
/// yozilmaydi. Mashina yasagan, juda ko'p va o'zi allaqachon o'zgarmas
/// jurnal bo'lgan jadvallar (o'tishlar, kun yozuvlari) umuman yozilmaydi.
/// </para>
/// <para>
/// Yashirilgan maydon qiymati <see cref="MaskedValue"/> bo'lib yoziladi, NOMI esa
/// o'zgargan maydonlar ro'yxatida qoladi — «parol almashtirildi» degan fakt
/// ko'rinadi, qiymat esa hech qachon.
/// </para>
/// <para>
/// <b>O'qish tomoni (A2) ham shu yerdan</b>: qaysi maydon shaxsiy yoki oklad
/// (<c>Personal</c>, <c>Salary</c>, <c>ExtraPayWhen</c> — qiymat bazada TO'LIQ, yashirish
/// o'qishda so'rovchining ruxsatiga qarab), qaysi qator butunligicha shaxsiy
/// (<c>PersonalRows</c>) va qaysi <c>...Id</c> qaysi jadvalga ishora qiladi (<c>Ref</c> —
/// jurnalda identifikator o'rniga nom). Qamrov testi har bir <c>Money</c> maydonni va har
/// bir <see cref="Guid"/> maydonni shu qoidalardan birida bo'lishga majburlaydi.
/// </para>
/// </remarks>
public static class AuditCatalog
{
    /// <summary>Yashirilgan maydonning qiymati o'rniga yoziladigan belgi.</summary>
    public const string MaskedValue = "***";

    /// <summary>
    /// Hamma entity uchun tashlab ketiladigan maydonlar — kim va qachonni audit qatorining o'zi saqlaydi.
    /// </summary>
    public static IReadOnlySet<string> GlobalExclusions { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        nameof(IAuditable.CreatedAt),
        nameof(IAuditable.CreatedBy),
        nameof(IAuditable.UpdatedAt),
        nameof(IAuditable.UpdatedBy),
        nameof(ISoftDeletable.DeletedAt),
        nameof(ISoftDeletable.DeletedBy),
        nameof(EmploymentEvent.RecordedAt),
    };

    /// <summary>
    /// Fon vazifalari o'zi qayta yozadigan yashirin sozlamalar — «qayergacha tekshirildi» kursorlari.
    /// </summary>
    /// <remarks>
    /// <see cref="SystemSettingKeys.AttendanceEmploymentChangesCheckedAt"/> har 15 daqiqada,
    /// <see cref="SystemSettingKeys.AttendanceFrozenThrough"/> har kecha yangilanadi —
    /// ular odam qarori emas, vazifaning xotirasi. «Umumiy anketa» va MCP o'chirgichi
    /// esa odam (yoki bir martalik urug') qarori, ular yoziladi.
    /// </remarks>
    public static IReadOnlySet<string> VolatileSettingKeys { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        SystemSettingKeys.AttendanceEmploymentChangesCheckedAt,
        SystemSettingKeys.AttendanceFrozenThrough,
    };

    /// <summary>
    /// Qurilma agenti va terminal har daqiqada yozadigan maydonlar — aloqa va telemetriya.
    /// </summary>
    private static readonly string[] DeviceTelemetry =
    [
        nameof(AttendanceDevice.LastSeenAt),
        nameof(AttendanceDevice.LastEventAt),
        nameof(AttendanceDevice.LastSeenIp),
        nameof(AttendanceDevice.LastSkewSeconds),
        nameof(AttendanceDevice.ClockOffsetSeconds),
        nameof(AttendanceDevice.ClockCheckedAt),
        nameof(AttendanceDevice.AgentSeenAt),
        nameof(AttendanceDevice.TerminalSeenAt),
        nameof(AttendanceDevice.LastRejectedAt),
        nameof(AttendanceDevice.ReconciledAt),
        nameof(AttendanceDevice.HeldRemovals),
        nameof(AttendanceDevice.ForeignPersons),
    ];

    private static readonly AuditEntityRule[] Rules =
    [
        // ---------- Tizim: hisoblar, rollar, sozlamalar ----------
        Audit<User>(AccessModuleKeys.Users)
            .Label(user => user.FullName.Full)
            .Mask(nameof(User.PasswordHash))
            .Exclude(nameof(User.LastLoginAt), nameof(User.FailedLoginAttempts), nameof(User.LockedUntil))
            .Ref(AuditRefTarget.Employee, nameof(User.EmployeeId))
            .Ref(AuditRefTarget.Role, nameof(User.RoleIds)),

        Audit<Role>(AccessModuleKeys.Roles)
            .Label(role => role.Name),

        Audit<SystemSetting>(AccessModuleKeys.Settings)
            .Label(setting => setting.Key)
            .MaskWhen((setting, field) =>
                field == nameof(SystemSetting.Value)
                && SystemSettingKeys.Find(setting.Key)?.Kind == SystemSettingKind.Secret)
            .SkipWhen(setting => VolatileSettingKeys.Contains(setting.Key)),

        Audit<ApiKey>(AccessModuleKeys.Settings)
            .Label(key => key.Name)
            .Mask(nameof(ApiKey.KeyHash))
            .Exclude(nameof(ApiKey.LastUsedAt), nameof(ApiKey.LastUsedIp))
            .Ref(AuditRefTarget.User, nameof(ApiKey.RevokedBy))
            .Ref(AuditRefTarget.Department, nameof(ApiKey.DepartmentId)),

        // ---------- Ma'lumotnomalar ----------
        Audit<LegalEntity>(AccessModuleKeys.Catalog).Label(row => row.Name),
        Audit<Direction>(AccessModuleKeys.Catalog).Label(row => row.Name),
        Audit<TerminationReason>(AccessModuleKeys.Catalog).Label(row => row.Name),
        Audit<AwardType>(AccessModuleKeys.Catalog).Label(row => row.Name),
        Audit<PenaltyType>(AccessModuleKeys.Catalog).Label(row => row.Name),
        Audit<DocumentType>(AccessModuleKeys.Catalog).Label(row => row.Name),
        Audit<AbsenceType>(AccessModuleKeys.Catalog).Label(row => row.Name),
        Audit<ReferralChannel>(AccessModuleKeys.Catalog).Label(row => row.Name),

        // `Level` va `Path` — daraxtdan HISOBLANADI (xom SQL bilan qayta quriladi).
        Audit<Department>(AccessModuleKeys.Catalog)
            .Label(row => row.Name)
            .Exclude(nameof(Department.Level), nameof(Department.Path))
            .Ref(AuditRefTarget.Department, nameof(Department.ParentId))
            .Ref(AuditRefTarget.LegalEntity, nameof(Department.LegalEntityId))
            .Ref(AuditRefTarget.Employee, nameof(Department.HeadEmployeeId)),

        Audit<Position>(AccessModuleKeys.Catalog)
            .Label(row => row.Name)
            .Ref(AuditRefTarget.Department, nameof(Position.DepartmentId)),

        Audit<PositionDuty>(AccessModuleKeys.Catalog)
            .LabelFrom(AuditLookup.PositionName, nameof(PositionDuty.PositionId))
            .Ref(AuditRefTarget.Position, nameof(PositionDuty.PositionId)),

        // ---------- Kadrlar ----------

        // Shaxsiy maydonlar ro'yxati — SPEC §3.10 dagi xodim kartasi bilan AYNAN bir xil
        // (P1 kartada ham shularni yashiradi). Telefon, ism, rasm, jins va mutaxassislik
        // ataylab ochiq: egasining qarori — davomat xodimi kelmagan odamga qo'ng'iroq qila olsin.
        Audit<Employee>(AccessModuleKeys.Employees)
            .SubjectSelf()
            .Label(employee => employee.FullName.Full)
            .Personal(
                nameof(Employee.BirthDate),
                nameof(Employee.Pinfl),
                nameof(Employee.Passport),
                nameof(Employee.SecondPhone),
                nameof(Employee.Address),
                nameof(Employee.HasCriminalRecord),
                nameof(Employee.CriminalRecordNote),
                nameof(Employee.MehnatUzUrl))
            .Ref(AuditRefTarget.Direction, nameof(Employee.DirectionIds)),

        Audit<Employment>(AccessModuleKeys.Employment)
            .Subject(nameof(Employment.EmployeeId))
            .LabelFromSubject()
            .Salary(nameof(Employment.Salary))
            .Ref(AuditRefTarget.Employee, nameof(Employment.EmployeeId), nameof(Employment.ResponsibleEmployeeId))
            .Ref(AuditRefTarget.LegalEntity, nameof(Employment.LegalEntityId))
            .Ref(AuditRefTarget.Department, nameof(Employment.DepartmentId))
            .Ref(AuditRefTarget.Position, nameof(Employment.PositionId))
            .Ref(AuditRefTarget.TerminationReason, nameof(Employment.TerminationReasonId)),

        // Qo'shimcha vazifada `ToSalary` — oklad EMAS, vazifa haqi: uni `salary.extra.view`
        // qo'riqlaydi (`EmployeeMapper` dagi bilan bir xil qoida, 2026-09-27).
        Audit<EmploymentEvent>(AccessModuleKeys.Employment)
            .SubjectVia(AuditLookup.EmploymentEmployee, nameof(EmploymentEvent.EmploymentId))
            .LabelFromSubject()
            .Salary(nameof(EmploymentEvent.FromSalary), nameof(EmploymentEvent.ToSalary))
            .ExtraPayWhen(nameof(EmploymentEvent.ToSalary), nameof(EmploymentEvent.Type), nameof(EmploymentEventType.AdditionalDuty))
            .Ref(AuditRefTarget.Employment, nameof(EmploymentEvent.EmploymentId))
            .Ref(AuditRefTarget.Department, nameof(EmploymentEvent.FromDepartmentId), nameof(EmploymentEvent.ToDepartmentId))
            .Ref(AuditRefTarget.Position, nameof(EmploymentEvent.FromPositionId), nameof(EmploymentEvent.ToPositionId))
            .Ref(AuditRefTarget.Candidate, nameof(EmploymentEvent.CandidateId))
            .Ref(AuditRefTarget.User, nameof(EmploymentEvent.RecordedBy)),

        Audit<Probation>(AccessModuleKeys.Employment)
            .SubjectVia(AuditLookup.EmploymentEmployee, nameof(Probation.EmploymentId))
            .LabelFromSubject()
            .Ref(AuditRefTarget.Employment, nameof(Probation.EmploymentId))
            .Ref(AuditRefTarget.Employee, nameof(Probation.ReviewerEmployeeId)),

        Audit<EmployeeFlag>(AccessModuleKeys.Lists)
            .Subject(nameof(EmployeeFlag.EmployeeId))
            .LabelFromSubject()
            .Ref(AuditRefTarget.Employee, nameof(EmployeeFlag.EmployeeId)),

        // Qobiliyatlar, hujjatlar va tekshiruv materiallari — QATORI bilan shaxsiy
        // (SPEC §7.10): sudlanganlik va qarzdorlik ma'lumotnomalari shu yerda.
        Audit<EmployeeCompetency>(AccessModuleKeys.Employees)
            .Subject(nameof(EmployeeCompetency.EmployeeId))
            .LabelFromSubject()
            .PersonalRows()
            .Ref(AuditRefTarget.Employee, nameof(EmployeeCompetency.EmployeeId)),

        Audit<EmployeeCompetencyLanguage>(AccessModuleKeys.Employees)
            .SubjectVia(AuditLookup.CompetencyEmployee, nameof(EmployeeCompetencyLanguage.EmployeeCompetencyId))
            .LabelFromSubject()
            .PersonalRows()
            .Ref(AuditRefTarget.EmployeeCompetency, nameof(EmployeeCompetencyLanguage.EmployeeCompetencyId)),

        Audit<EmployeeDocument>(AccessModuleKeys.Employees)
            .Subject(nameof(EmployeeDocument.EmployeeId))
            .LabelFromSubject()
            .PersonalRows()
            .Ref(AuditRefTarget.Employee, nameof(EmployeeDocument.EmployeeId))
            .Ref(AuditRefTarget.DocumentType, nameof(EmployeeDocument.DocumentTypeId)),

        Audit<EmployeeCheckFile>(AccessModuleKeys.Employees)
            .Subject(nameof(EmployeeCheckFile.EmployeeId))
            .LabelFromSubject()
            .PersonalRows()
            .Ref(AuditRefTarget.Employee, nameof(EmployeeCheckFile.EmployeeId)),

        // Rag'bat/jarima summasi — oklad EMAS: ilovada u `incentives.view` bilan
        // ochiq ko'rinadi (`EmployeeMapper`), jurnal undan ko'p yashirmasin.
        Audit<Incentive>(AccessModuleKeys.Incentives)
            .Subject(nameof(Incentive.EmployeeId))
            .LabelFromSubject()
            .OpenMoney(nameof(Incentive.Amount))
            .Ref(AuditRefTarget.Employee, nameof(Incentive.EmployeeId))
            .Ref(AuditRefTarget.IncentiveType, nameof(Incentive.TypeId)),

        Audit<ExitInterview>(AccessModuleKeys.Feedback)
            .SubjectVia(AuditLookup.EmploymentEmployee, nameof(ExitInterview.EmploymentId))
            .LabelFromSubject()
            .Ref(AuditRefTarget.Employment, nameof(ExitInterview.EmploymentId)),

        Audit<Incident>(AccessModuleKeys.Safety)
            .Subject(nameof(Incident.EmployeeId))
            .LabelFromSubject()
            .Ref(AuditRefTarget.Employee, nameof(Incident.EmployeeId), nameof(Incident.ResponsibleEmployeeId)),

        // ---------- Rekruting ----------
        Audit<PipelineStage>(AccessModuleKeys.Recruiting).Label(row => row.Name),
        Audit<Vacancy>(AccessModuleKeys.Recruiting).Label(row => row.Title),

        Audit<Candidate>(AccessModuleKeys.Recruiting)
            .Label(row => row.FullName.Full)
            .Ref(AuditRefTarget.Vacancy, nameof(Candidate.VacancyId))
            .Ref(AuditRefTarget.PipelineStage, nameof(Candidate.CurrentStageId))
            .Ref(AuditRefTarget.ReferralChannel, nameof(Candidate.ReferralChannelId)),

        Audit<CandidateStageEntry>(AccessModuleKeys.Recruiting)
            .LabelFrom(AuditLookup.CandidateName, nameof(CandidateStageEntry.CandidateId))
            .Ref(AuditRefTarget.Candidate, nameof(CandidateStageEntry.CandidateId))
            .Ref(AuditRefTarget.PipelineStage, nameof(CandidateStageEntry.StageId))
            .Ref(AuditRefTarget.User, nameof(CandidateStageEntry.RecordedBy)),
        Audit<CandidateNote>(AccessModuleKeys.Recruiting)
            .LabelFrom(AuditLookup.CandidateName, nameof(CandidateNote.CandidateId))
            .Ref(AuditRefTarget.Candidate, nameof(CandidateNote.CandidateId)),
        Audit<CandidateFile>(AccessModuleKeys.Recruiting)
            .LabelFrom(AuditLookup.CandidateName, nameof(CandidateFile.CandidateId))
            .Ref(AuditRefTarget.Candidate, nameof(CandidateFile.CandidateId)),
        Audit<CandidateQuestionnaire>(AccessModuleKeys.Recruiting)
            .LabelFrom(AuditLookup.CandidateName, nameof(CandidateQuestionnaire.CandidateId))
            .Ref(AuditRefTarget.Candidate, nameof(CandidateQuestionnaire.CandidateId)),

        // Onlayn anketa (SPEC-planshet-anketa): yozuv «qachon, qaysi IP'dan, №» bilan jurnalga
        // tushadi (bajaruvchi — «Onlayn anketa», Anonymous), JAVOBLAR esa yo'q — ularda
        // sudlanganlik, sog'liq va kredit haqidagi ma'lumot bor, jurnal esa boshqa ruxsat
        // (audit.view) bilan o'qiladi. Hujjat o'zgarmaydi — faqat «yaratildi» qatori bo'ladi.
        Audit<CandidateForm>(AccessModuleKeys.Recruiting)
            .LabelFrom(AuditLookup.CandidateName, nameof(CandidateForm.CandidateId))
            .Exclude(
                nameof(CandidateForm.Answers),
                nameof(CandidateForm.PhotoKey),
                nameof(CandidateForm.SignatureKey),

                // Texnik identifikatorlar: fayl nomi jurnalda bor (CandidateFile qatori)
                nameof(CandidateForm.ClientSubmissionId),
                nameof(CandidateForm.FileId))
            .Ref(AuditRefTarget.Candidate, nameof(CandidateForm.CandidateId)),

        // ---------- Davomat (SPEC §14.5) ----------
        Audit<AbsenceRequest>(AccessModuleKeys.Attendance)
            .Subject(nameof(AbsenceRequest.EmployeeId))
            .LabelFromSubject()
            .Ref(AuditRefTarget.Employee, nameof(AbsenceRequest.EmployeeId))
            .Ref(AuditRefTarget.AbsenceType, nameof(AbsenceRequest.AbsenceTypeId))
            .Ref(AuditRefTarget.User, nameof(AbsenceRequest.DecidedBy))
            .Ref(
                AuditRefTarget.EmployeeAccount,
                nameof(AbsenceRequest.SubmittedByAccountId),
                nameof(AbsenceRequest.CancelledByAccountId),
                nameof(AbsenceRequest.EditedByAccountId))
            // Hisoblagich va tahrir lahzasi — texnik: audit yozuvining o'zi lahzani va muallifni
            // saqlaydi (SPEC-sorov-tahriri §3.5); ularni ko'rsatish har qarorda shovqin bo'lardi.
            .Exclude(nameof(AbsenceRequest.Version), nameof(AbsenceRequest.EditedAt)),

        // Jadval so'rovi (SPEC-verifikatsiya-jadval-sorovi §C) — yo'qlik so'rovining egizagi.
        Audit<ScheduleChangeRequest>(AccessModuleKeys.Attendance)
            .Subject(nameof(ScheduleChangeRequest.EmployeeId))
            .LabelFromSubject()
            .Ref(AuditRefTarget.Employee, nameof(ScheduleChangeRequest.EmployeeId))
            .Ref(AuditRefTarget.User, nameof(ScheduleChangeRequest.DecidedBy))
            .Ref(
                AuditRefTarget.EmployeeAccount,
                nameof(ScheduleChangeRequest.SubmittedByAccountId),
                nameof(ScheduleChangeRequest.CancelledByAccountId),
                nameof(ScheduleChangeRequest.EditedByAccountId))
            .Exclude(nameof(ScheduleChangeRequest.Version), nameof(ScheduleChangeRequest.EditedAt)),

        // So'rovning kuni — yorlig'i sana: so'rov tahririda qaysi kun o'zgargani shu bilan ko'rinadi.
        // Xodimi ota so'rovdan: faqat kunlari o'zgargan tahrirda so'rov qatorining o'zi yozilmaydi,
        // va kunlar xodim tarixida ko'rinmay qolardi. Havola maydon sifatida yozilmaydi — so'rov
        // uchun havola turi yo'q, xodim esa subyekt bo'lib turibdi.
        Audit<ScheduleChangeDay>(AccessModuleKeys.Attendance)
            .SubjectVia(AuditLookup.ScheduleChangeRequestEmployee, nameof(ScheduleChangeDay.RequestId))
            .Label(row => row.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture))
            .Exclude(nameof(ScheduleChangeDay.RequestId)),

        Audit<WorkSchedule>(AccessModuleKeys.Attendance).Label(row => row.Name),

        Audit<WorkScheduleDay>(AccessModuleKeys.Attendance)
            .LabelFrom(AuditLookup.WorkScheduleName, nameof(WorkScheduleDay.WorkScheduleId))
            .Ref(AuditRefTarget.WorkSchedule, nameof(WorkScheduleDay.WorkScheduleId)),

        Audit<WorkScheduleDate>(AccessModuleKeys.Attendance)
            .LabelFrom(AuditLookup.WorkScheduleName, nameof(WorkScheduleDate.WorkScheduleId))
            .Ref(AuditRefTarget.WorkSchedule, nameof(WorkScheduleDate.WorkScheduleId)),

        // Biriktirma bo'limga yoki lavozimga ham bo'lishi mumkin — xodim faqat
        // shaxsiy biriktirmada bor; nom esa doim jadvalniki.
        Audit<ScheduleAssignment>(AccessModuleKeys.Attendance)
            .Subject(nameof(ScheduleAssignment.EmployeeId))
            .LabelFrom(AuditLookup.WorkScheduleName, nameof(ScheduleAssignment.WorkScheduleId))
            .Ref(AuditRefTarget.WorkSchedule, nameof(ScheduleAssignment.WorkScheduleId))
            .Ref(AuditRefTarget.Employee, nameof(ScheduleAssignment.EmployeeId))
            .Ref(AuditRefTarget.Position, nameof(ScheduleAssignment.PositionId))
            .Ref(AuditRefTarget.Department, nameof(ScheduleAssignment.DepartmentId)),

        // Token va HMAC siri — yashiriladi; token kim/qachon berilgani — audit
        // qatorining o'zida. Terminal seriyasi (birinchi ulanishdagi bog'lanish)
        // va HR ning «o'chirishlarga ruxsat» i yoziladi — ular kamdan-kam va ma'noli.
        Audit<AttendanceDevice>(AccessModuleKeys.Attendance)
            .Label(device => device.Name)
            .Mask(nameof(AttendanceDevice.TokenHash), nameof(AttendanceDevice.TokenPrefix), nameof(AttendanceDevice.HmacSecret))
            .Exclude(nameof(AttendanceDevice.TokenIssuedAt), nameof(AttendanceDevice.TokenIssuedBy))
            .Exclude(DeviceTelemetry)
            .Ref(AuditRefTarget.LegalEntity, nameof(AttendanceDevice.LegalEntityId))
            .Ref(AuditRefTarget.Department, nameof(AttendanceDevice.DepartmentId)),

        Audit<AttendanceIdentity>(AccessModuleKeys.Attendance)
            .Subject(nameof(AttendanceIdentity.EmployeeId))
            .LabelFromSubject()
            .Ref(AuditRefTarget.Employee, nameof(AttendanceIdentity.EmployeeId)),

        Audit<WorkSite>(AccessModuleKeys.Attendance).Label(row => row.Name),

        // Shifrlangan parol ham (HR ko'ra oladigan) — yashiriladi; parol
        // qachon qo'yilgani ham shu guruhda: «almashtirildi» fakti ko'rinadi.
        Audit<EmployeeAccount>(AccessModuleKeys.Attendance)
            .Subject(nameof(EmployeeAccount.EmployeeId))
            .LabelFromSubject()
            .Mask(nameof(EmployeeAccount.PasswordHash), nameof(EmployeeAccount.PasswordProtected), nameof(EmployeeAccount.PasswordSetAt), nameof(EmployeeAccount.PasswordHandedOutAt))
            .Exclude(nameof(EmployeeAccount.LastLoginAt))
            .Ref(AuditRefTarget.Employee, nameof(EmployeeAccount.EmployeeId)),
    ];

    /// <summary>Yozilmaydigan entity'lar — har biri sababi bilan.</summary>
    public static IReadOnlyDictionary<Type, string> NotAudited { get; } = new Dictionary<Type, string>
    {
        [typeof(AuditLog)] = "jurnalning o'zi — o'zgarmas, faqat qo'shiladi",
        [typeof(RefreshToken)] = "sessiya tokeni — har kirish va yangilashda yoziladi; kirish/chiqish xavfsizlik hodisasi bo'lib yoziladi",
        [typeof(EmployeeRefreshToken)] = "xodim ilovasining sessiya tokeni — har kirish va yangilashda",
        [typeof(NotificationState)] = "foydalanuvchining «o'qildi/yopildi» belgisi — interfeys holati, ma'lumot emas",
        [typeof(AttendancePass)] = "terminal o'tishi — mashina yasaydi, kuniga minglab, o'zi o'zgarmas hodisa jurnali",
        [typeof(AttendanceDay)] = "kun yozuvi — o'tishlardan har 15 daqiqada va har kecha QAYTA HISOBLANADI",
        [typeof(DeviceEnrollment)] = "terminalga yozish holati — agent har daqiqada hisobot beradi",
        [typeof(MobileCheckIn)] = "telefondan belgilash — o'tish bilan bir xil, o'zgarmas hodisa",
        [typeof(McpClient)] = "MCP jadvallari alohida, interceptorsiz kontekst orqali yoziladi (McpDatabase.CreateStoreContext)",
        [typeof(McpGrant)] = "MCP jadvallari alohida, interceptorsiz kontekst orqali yoziladi; ulanish izi mcp_audit da",
        [typeof(McpAuthCode)] = "bir martalik OAuth kodi — sir, bir necha daqiqa yashaydi",
        [typeof(McpToken)] = "OAuth tokeni — sir, har yangilashda almashadi",
        [typeof(McpAuditEntry)] = "AI chaqiruvlarining o'z jurnali (mcp_audit)",
    };

    /// <summary>Yoziladigan entity'lar va ularning qoidalari.</summary>
    public static IReadOnlyDictionary<Type, AuditEntityRule> Audited { get; } =
        Rules.ToDictionary(rule => rule.EntityType);

    /// <summary>Entity turining qoidasi; yozilmaydigan yoki notanish tur bo'lsa <c>null</c>.</summary>
    /// <remarks>
    /// Notanish tur ham <c>null</c> — ishlab chiqarishda jimgina o'tkazib yuboriladi,
    /// yiqitilmaydi. Uni ushlash testning ishi (<c>AuditCatalogCoverageTests</c>),
    /// HR ning saqlash tugmasi emas.
    /// </remarks>
    /// <param name="entityType">Entity'ning CLR turi.</param>
    public static AuditEntityRule? Find(Type entityType)
        => Audited.GetValueOrDefault(entityType);

    /// <summary>
    /// Entity NOMI bo'yicha qoida — o'qish tomoni uchun: jurnal qatorida tur emas, nom yotadi.
    /// </summary>
    /// <remarks>
    /// Registrsiz: so'rovdagi <c>entity=employmentevent</c> ham topiladi (javobdagi nom esa
    /// doim kanonik — <c>EmploymentEvent</c>). Notanish nom — <c>null</c>.
    /// </remarks>
    /// <param name="entityName">Entity nomi (<c>Employee</c>, <c>EmploymentEvent</c>).</param>
    public static AuditEntityRule? FindByName(string? entityName)
        => entityName is null ? null : ByName.GetValueOrDefault(entityName);

    /// <summary>Bo'limning yoziladigan entity nomlari — jurnalni bo'lim bo'yicha filtrlash uchun.</summary>
    /// <param name="module">Bo'lim kaliti (<c>AccessModuleKeys</c>).</param>
    public static IEnumerable<string> EntityNamesOf(string module)
        => Rules.Where(rule => rule.Module == module).Select(rule => rule.EntityType.Name);

    private static readonly Dictionary<string, AuditEntityRule> ByName =
        Rules.ToDictionary(rule => rule.EntityType.Name, StringComparer.OrdinalIgnoreCase);

    private static AuditEntityRule<T> Audit<T>(string module)
        where T : class
        => new(module);
}

/// <summary>Bog'liq jadvaldan o'qiladigan qiymat — yorliq yoki xodim.</summary>
public enum AuditLookup
{
    /// <summary><c>employees.id</c> → F.I.Sh.</summary>
    EmployeeName = 1,

    /// <summary><c>candidates.id</c> → F.I.Sh.</summary>
    CandidateName = 2,

    /// <summary><c>positions.id</c> → nom.</summary>
    PositionName = 3,

    /// <summary><c>work_schedules.id</c> → nom.</summary>
    WorkScheduleName = 4,

    /// <summary><c>employments.id</c> → xodim.</summary>
    EmploymentEmployee = 5,

    /// <summary><c>employee_competencies.id</c> → xodim.</summary>
    CompetencyEmployee = 6,

    /// <summary><c>schedule_change_requests.id</c> → xodim.</summary>
    ScheduleChangeRequestEmployee = 7,
}

/// <summary>
/// Maydonning O'QISHDAGI ko'rinish sinfi — qator ko'rinsa ham, qiymatini kim ko'radi
/// (SPEC-rollar-va-audit §7.3 «Field rules», §7.10).
/// </summary>
/// <remarks>
/// Yozish tomoniga ta'sir qilmaydi: qiymat jurnalda TO'LIQ saqlanadi (faqat sirlar
/// <see cref="AuditCatalog.MaskedValue"/> bo'lib), yashirish har bir o'qishda so'rovchining
/// ruxsatiga qarab qilinadi. Ruxsat keyin berilsa, eski tarix ham ochiladi.
/// </remarks>
public enum AuditFieldClass
{
    /// <summary>Qatorni ko'rgan har kim ko'radi.</summary>
    Open = 0,

    /// <summary>Shaxsiy ma'lumot — <c>employees.personal.view</c> (yoki <c>employees.edit</c>, SPEC §3.4).</summary>
    Personal = 1,

    /// <summary>Oklad — <c>salary.view</c>.</summary>
    Salary = 2,

    /// <summary>Qo'shimcha vazifa haqi — <c>salary.view</c> VA <c>salary.extra.view</c>.</summary>
    ExtraPay = 3,
}

/// <summary>
/// Havola maydoni (<c>DepartmentId</c>, <c>RoleIds</c>) qaysi jadvalga ishora qiladi — o'qishda
/// identifikator o'rniga NOM ko'rsatiladi.
/// </summary>
/// <remarks>
/// Nom O'QISH paytida, jadval bo'yicha bitta so'rov bilan va o'chirilganlari bilan birga
/// olinadi (SPEC §8.3). Yozish paytidagi nusxa emas: bo'lim qayta nomlansa, eski qatorda
/// ham yangi nomi chiqadi — bu ataylab, havola «qaysi bo'lim» degan savolga javob beradi.
/// </remarks>
public enum AuditRefTarget
{
    /// <summary><c>employees</c> → F.I.Sh.</summary>
    Employee = 1,

    /// <summary><c>users</c> → F.I.Sh.</summary>
    User = 2,

    /// <summary><c>roles</c> → nom.</summary>
    Role = 3,

    /// <summary><c>departments</c> → nom.</summary>
    Department = 4,

    /// <summary><c>positions</c> → nom.</summary>
    Position = 5,

    /// <summary><c>legal_entities</c> → nom.</summary>
    LegalEntity = 6,

    /// <summary><c>directions</c> → nom.</summary>
    Direction = 7,

    /// <summary><c>termination_reasons</c> → nom.</summary>
    TerminationReason = 8,

    /// <summary><c>document_types</c> → nom.</summary>
    DocumentType = 9,

    /// <summary>
    /// Rag'bat YOKI jarima turi — <c>Incentive.TypeId</c> bitta ustun, lekin turiga qarab ikki
    /// jadvaldan biriga ishora qiladi; ikkalasidan qidiriladi (identifikatorlar takrorlanmaydi).
    /// </summary>
    IncentiveType = 10,

    /// <summary><c>pipeline_stages</c> → nom.</summary>
    PipelineStage = 11,

    /// <summary><c>vacancies</c> → sarlavha.</summary>
    Vacancy = 12,

    /// <summary><c>candidates</c> → F.I.Sh.</summary>
    Candidate = 13,

    /// <summary><c>work_schedules</c> → nom.</summary>
    WorkSchedule = 14,

    /// <summary><c>absence_types</c> → nom.</summary>
    AbsenceType = 15,

    /// <summary><c>employments</c> → xodimning F.I.Sh.</summary>
    Employment = 16,

    /// <summary><c>employee_competencies</c> → xodimning F.I.Sh.</summary>
    EmployeeCompetency = 17,

    /// <summary><c>employee_accounts</c> → xodimning F.I.Sh.</summary>
    EmployeeAccount = 18,

    /// <summary><c>referral_channels</c> → nom.</summary>
    ReferralChannel = 19,
}

/// <summary>
/// Maydon yozuvning TURIGA qarab qo'shimcha haq bo'lib qoladi — masalan buyruqning
/// <c>ToSalary</c> si qo'shimcha vazifada.
/// </summary>
/// <param name="Field">Maydon (murakkab xossa nomi, nuqtasiz).</param>
/// <param name="Discriminator">Turni bildiruvchi maydon (<c>Type</c>).</param>
/// <param name="Value">Shu qiymatda maydon — qo'shimcha haq (sanoq a'zosi NOMI).</param>
public sealed record AuditExtraPayRule(string Field, string Discriminator, string Value);

/// <summary>O'zgarish qaysi xodimga tegishli ekani qayerdan olinadi.</summary>
public enum AuditSubjectSource
{
    /// <summary>Xodimga tegishli emas (rol, sozlama, ma'lumotnoma).</summary>
    None = 0,

    /// <summary>Yozuvning o'zi xodim — o'z identifikatori.</summary>
    Self = 1,

    /// <summary>Yozuvdagi xodim maydoni (<c>EmployeeId</c>).</summary>
    Property = 2,

    /// <summary>Bog'liq jadval orqali (<see cref="AuditLookup.EmploymentEmployee"/> va h.k.).</summary>
    Lookup = 3,
}

/// <summary>Bitta entity turining audit qoidasi.</summary>
/// <remarks>
/// Qoida faqat <see cref="AuditCatalog"/> ichida, statik ishga tushishda yig'iladi —
/// keyin o'zgarmaydi (o'zgartiruvchilar <c>internal</c> va zanjirli).
/// </remarks>
public abstract class AuditEntityRule
{
    private readonly HashSet<string> _excluded = new(StringComparer.Ordinal);
    private readonly HashSet<string> _masked = new(StringComparer.Ordinal);

    // O'qish tomonining qoidalari registrsiz: kengaytirishdan oldingi qatorlarda maydon
    // nomlari kichik harf bilan boshlanadi (`permissions`, `fullName`).
    private readonly Dictionary<string, AuditFieldClass> _fieldClasses = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AuditRefTarget> _refs = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _openMoney = new(StringComparer.OrdinalIgnoreCase);

    protected AuditEntityRule(Type entityType, string module)
    {
        EntityType = entityType;
        Module = module;
    }

    /// <summary>Entity'ning CLR turi.</summary>
    public Type EntityType { get; }

    /// <summary>Bo'lim kaliti (<c>AccessModuleKeys</c>) — jurnalda qatorni kim ko'rishini hal qiladi.</summary>
    public string Module { get; }

    /// <summary>Shu turda tashlab ketiladigan maydonlar (umumiylaridan tashqari).</summary>
    public IReadOnlySet<string> ExcludedFields => _excluded;

    /// <summary>Doim yashiriladigan maydonlar.</summary>
    public IReadOnlySet<string> MaskedFields => _masked;

    /// <summary>Xodim qayerdan olinadi.</summary>
    public AuditSubjectSource SubjectSource { get; private protected set; }

    /// <summary>Xodim maydoni yoki bog'liq jadval kaliti.</summary>
    public string? SubjectProperty { get; private protected set; }

    /// <summary><see cref="AuditSubjectSource.Lookup"/> da — qaysi jadval.</summary>
    public AuditLookup? SubjectLookup { get; private protected set; }

    /// <summary>Yorliq bog'liq jadvaldan olinsa — qaysi jadval.</summary>
    public AuditLookup? LabelLookup { get; private protected set; }

    /// <summary>Yorliq jadvalining kaliti joylashgan maydon.</summary>
    public string? LabelKeyProperty { get; private protected set; }

    /// <summary>Maydon tashlab ketiladimi (umumiy va shu turdagi ro'yxat).</summary>
    /// <param name="field">Maydon nomi (murakkab xossada nuqtali: <c>Salary.Amount</c>).</param>
    public bool IsExcluded(string field)
        => AuditCatalog.GlobalExclusions.Contains(field) || _excluded.Contains(field);

    /// <summary>Shu yozuvning maydoni yashiriladimi.</summary>
    /// <param name="entity">Entity nusxasi — qiymatga bog'liq yashirish uchun (masalan maxfiy sozlama).</param>
    /// <param name="field">Maydon nomi.</param>
    public bool IsMasked(object entity, string field)
        => _masked.Contains(field) || MaskedFor(entity, field);

    /// <summary>Butun qator yozilmaydimi (masalan fon vazifasining kursor sozlamasi).</summary>
    /// <param name="entity">Entity nusxasi.</param>
    public abstract bool Skips(object entity);

    /// <summary>Entity'ning o'z yorlig'i; bog'liq jadvaldan olinadigan bo'lsa <c>null</c>.</summary>
    /// <param name="entity">Entity nusxasi.</param>
    public abstract string? OwnLabel(object entity);

    /// <summary>Yorliq entity'ning o'zidan olinadimi (qamrov testi uchun).</summary>
    public abstract bool HasOwnLabel { get; }

    /// <summary>Yorliq xodim nomidan olinadimi.</summary>
    public bool UsesSubjectLabel { get; private protected set; }

    /// <summary>Shaxsiy/oklad sinfidagi maydonlar (nuqtasiz — murakkab xossa butunligicha).</summary>
    public IReadOnlyDictionary<string, AuditFieldClass> FieldClasses => _fieldClasses;

    /// <summary>Havola maydonlari va ular ishora qiladigan jadval.</summary>
    public IReadOnlyDictionary<string, AuditRefTarget> Refs => _refs;

    /// <summary>
    /// Pul, lekin oklad EMAS — ilovaning o'zida ham ochiq ko'rinadigan summa (rag'bat/jarima).
    /// </summary>
    /// <remarks>
    /// Qamrov testi har bir <c>Money</c> maydonni yo oklad, yo shu ro'yxatda bo'lishini talab
    /// qiladi: yangi pul maydoni «o'z-o'zidan ochiq» bo'lib qolmasin.
    /// </remarks>
    public IReadOnlySet<string> OpenMoneyFields => _openMoney;

    /// <summary>
    /// Butun QATOR shaxsiy — <c>employees.personal.view</c> siz ko'rinmaydi (qobiliyatlar,
    /// hujjatlar, tekshiruv materiallari; SPEC §7.10).
    /// </summary>
    public bool RequiresPersonalView { get; private protected set; }

    /// <summary>Turga qarab qo'shimcha haq bo'ladigan maydon (bo'lmasa <c>null</c>).</summary>
    public AuditExtraPayRule? ExtraPay { get; private protected set; }

    /// <summary>Maydonning ko'rinish sinfi.</summary>
    /// <remarks>
    /// <para>
    /// Murakkab xossaning qismlari butunining sinfini oladi: <c>Salary.Amount</c> →
    /// <c>Salary</c> → <see cref="AuditFieldClass.Salary"/>.
    /// </para>
    /// <para>
    /// <paramref name="discriminator"/> NOMA'LUM bo'lsa (<c>null</c>) va maydon turga qarab
    /// qo'shimcha haq bo'lishi mumkin bo'lsa — qo'shimcha haq deb olinadi: shubhada
    /// yashirgan ma'qul, ochib qo'ygandan ko'ra.
    /// </para>
    /// </remarks>
    /// <param name="field">Maydon nomi (nuqtali bo'lishi mumkin).</param>
    /// <param name="discriminator">Yozuv turining qiymati (<see cref="ExtraPay"/> uchun); noma'lum — <c>null</c>.</param>
    public AuditFieldClass ClassOf(string field, string? discriminator)
    {
        ArgumentNullException.ThrowIfNull(field);

        var whole = TopLevel(field);

        if (ExtraPay is { } extra
            && string.Equals(extra.Field, whole, StringComparison.OrdinalIgnoreCase)
            && (discriminator is null || string.Equals(discriminator, extra.Value, StringComparison.Ordinal)))
        {
            return AuditFieldClass.ExtraPay;
        }

        return _fieldClasses.GetValueOrDefault(whole, AuditFieldClass.Open);
    }

    /// <summary>Havola maydonining jadvali; havola bo'lmasa <c>null</c>.</summary>
    /// <param name="field">Maydon nomi.</param>
    public AuditRefTarget? RefOf(string field)
        => _refs.TryGetValue(field, out var target) ? target : null;

    /// <summary><c>Salary.Amount</c> → <c>Salary</c>.</summary>
    private static string TopLevel(string field)
    {
        var dot = field.IndexOf('.', StringComparison.Ordinal);
        return dot < 0 ? field : field[..dot];
    }

    private protected abstract bool MaskedFor(object entity, string field);

    private protected void AddExcluded(IEnumerable<string> fields) => _excluded.UnionWith(fields);

    private protected void AddMasked(IEnumerable<string> fields) => _masked.UnionWith(fields);

    private protected void AddClass(AuditFieldClass fieldClass, IEnumerable<string> fields)
    {
        foreach (var field in fields)
        {
            _fieldClasses[field] = fieldClass;
        }
    }

    private protected void AddRefs(AuditRefTarget target, IEnumerable<string> fields)
    {
        foreach (var field in fields)
        {
            _refs[field] = target;
        }
    }

    private protected void AddOpenMoney(IEnumerable<string> fields) => _openMoney.UnionWith(fields);
}

/// <summary><see cref="AuditEntityRule"/> ning turli ko'rinishi — katalogni o'qilarli yozish uchun.</summary>
/// <typeparam name="T">Entity turi.</typeparam>
public sealed class AuditEntityRule<T> : AuditEntityRule
    where T : class
{
    private Func<T, string?>? _label;
    private Func<T, bool>? _skip;
    private Func<T, string, bool>? _maskWhen;

    internal AuditEntityRule(string module)
        : base(typeof(T), module)
    {
    }

    /// <inheritdoc />
    public override bool Skips(object entity) => _skip is not null && _skip((T)entity);

    /// <inheritdoc />
    public override string? OwnLabel(object entity) => _label?.Invoke((T)entity);

    /// <inheritdoc />
    public override bool HasOwnLabel => _label is not null;

    internal AuditEntityRule<T> Label(Func<T, string?> label)
    {
        _label = label;
        return this;
    }

    internal AuditEntityRule<T> LabelFrom(AuditLookup lookup, string keyProperty)
    {
        LabelLookup = lookup;
        LabelKeyProperty = keyProperty;
        return this;
    }

    internal AuditEntityRule<T> LabelFromSubject()
    {
        UsesSubjectLabel = true;
        return this;
    }

    internal AuditEntityRule<T> SubjectSelf()
    {
        SubjectSource = AuditSubjectSource.Self;
        return this;
    }

    internal AuditEntityRule<T> Subject(string employeeIdProperty)
    {
        SubjectSource = AuditSubjectSource.Property;
        SubjectProperty = employeeIdProperty;
        return this;
    }

    internal AuditEntityRule<T> SubjectVia(AuditLookup lookup, string keyProperty)
    {
        SubjectSource = AuditSubjectSource.Lookup;
        SubjectLookup = lookup;
        SubjectProperty = keyProperty;
        return this;
    }

    internal AuditEntityRule<T> Exclude(params string[] fields)
    {
        AddExcluded(fields);
        return this;
    }

    internal AuditEntityRule<T> Mask(params string[] fields)
    {
        AddMasked(fields);
        return this;
    }

    internal AuditEntityRule<T> MaskWhen(Func<T, string, bool> predicate)
    {
        _maskWhen = predicate;
        return this;
    }

    internal AuditEntityRule<T> SkipWhen(Func<T, bool> predicate)
    {
        _skip = predicate;
        return this;
    }

    /// <summary>Shaxsiy maydonlar — <see cref="AuditFieldClass.Personal"/>.</summary>
    internal AuditEntityRule<T> Personal(params string[] fields)
    {
        AddClass(AuditFieldClass.Personal, fields);
        return this;
    }

    /// <summary>Oklad maydonlari — <see cref="AuditFieldClass.Salary"/> (murakkab xossa nomi, nuqtasiz).</summary>
    internal AuditEntityRule<T> Salary(params string[] fields)
    {
        AddClass(AuditFieldClass.Salary, fields);
        return this;
    }

    /// <summary>Maydon <paramref name="discriminator"/> = <paramref name="value"/> bo'lganda qo'shimcha haq.</summary>
    internal AuditEntityRule<T> ExtraPayWhen(string field, string discriminator, string value)
    {
        ExtraPay = new AuditExtraPayRule(field, discriminator, value);
        return this;
    }

    /// <summary>Oklad bo'lmagan, ilovada ochiq ko'rinadigan summa.</summary>
    internal AuditEntityRule<T> OpenMoney(params string[] fields)
    {
        AddOpenMoney(fields);
        return this;
    }

    /// <summary>Havola maydonlari — o'qishda nomi ko'rsatiladi.</summary>
    internal AuditEntityRule<T> Ref(AuditRefTarget target, params string[] fields)
    {
        AddRefs(target, fields);
        return this;
    }

    /// <summary>Butun qator shaxsiy ma'lumot.</summary>
    internal AuditEntityRule<T> PersonalRows()
    {
        RequiresPersonalView = true;
        return this;
    }

    private protected override bool MaskedFor(object entity, string field)
        => _maskWhen is not null && _maskWhen((T)entity, field);
}
