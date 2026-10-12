using Bonvi.Domain.Common;

namespace Bonvi.Domain.Auditing;

/// <summary>Kuzatiladigan amal turi.</summary>
/// <remarks>
/// Bazada SON bo'lib yotadi — yangi qiymat faqat OXIRIGA qo'shiladi. 10 va 11
/// WebAuthn bosqichi uchun band (<c>StepUp</c>, <c>StepUpFailed</c>,
/// SPEC-rollar-va-audit §10) — ularni boshqa ma'noda ishlatmang.
/// </remarks>
public enum AuditAction
{
    Create = 1,
    Update = 2,
    Delete = 3,
    Login = 4,
    LoginFailed = 5,
    Logout = 6,
    Export = 7,
    PermissionDenied = 8,

    /// <summary>
    /// Bekor qilingan yangilash tokeni qayta ishlatildi — o'g'irlik belgisi,
    /// hisobning barcha sessiyalari yopildi.
    /// </summary>
    TokenReuse = 9,
}

/// <summary>O'zgarishni KIM qildi — odam, tizim, xodim ilovasi yoki qurilma.</summary>
/// <remarks>
/// <para>
/// Bazada SON bo'lib yotadi (<c>actor_kind</c>, sukut <c>1</c>). Kengaytirishdan
/// oldingi barcha yozuvlar <see cref="User"/> — ularni faqat foydalanuvchi
/// boshqaruvi yozgan.
/// </para>
/// <para>
/// <see cref="EmployeeAccount"/> va <see cref="Device"/> — faqat «kim» degan
/// savolga javob (SPEC §14.6). Xodim hisoblari rol tizimiga KIRMAYDI.
/// </para>
/// </remarks>
public enum AuditActorKind
{
    /// <summary>Tizim foydalanuvchisi (HR, rahbariyat, bot hisobi).</summary>
    User = 1,

    /// <summary>Tizimning o'zi: ishga tushish, fon vazifasi, ETL.</summary>
    System = 2,

    /// <summary>Kirmagan so'rov — masalan noma'lum login bilan kirish urinishi.</summary>
    Anonymous = 3,

    /// <summary>Xodim ilovasi (PWA) — xodimning o'z hisobi.</summary>
    EmployeeAccount = 4,

    /// <summary>Davomat terminali yoki uning agenti (qurilma tokeni bilan).</summary>
    Device = 5,
}

/// <summary>
/// Audit yozuvi: kim, qachon, nimani o'zgartirdi.
/// </summary>
/// <remarks>
/// <para>
/// HR tizimida bu majburiy. «Kim bu xodimni ishdan bo'shatdi?»,
/// «Okladni kim o'zgartirdi?» — bu savollar albatta paydo bo'ladi.
/// </para>
/// <para>
/// Yozuv <b>o'zgartirilmaydi va o'chirilmaydi</b>: konstruktordan boshqa
/// yo'l bilan qiymat bermaydi, o'zgartiruvchi metodlari yo'q. Ilova
/// darajasida buni <c>AuditTrailInterceptor</c>, baza darajasida
/// <c>audit_logs_append_only</c> triggeri qo'riqlaydi.
/// </para>
/// <para>
/// <b>Nomlar — o'sha lahzadagi NUSXA</b> (<see cref="ActorName"/>,
/// <see cref="ActorRole"/>, <see cref="EntityLabel"/>). Rol nomini egasi
/// istalgan payt o'zgartiradi; o'qishda qayta hisoblansa, tarix ham
/// o'zgarib qolardi.
/// </para>
/// </remarks>
public sealed class AuditLog : AggregateRoot
{
    /// <summary><see cref="EntityLabel"/> ning eng katta uzunligi.</summary>
    public const int MaxEntityLabelLength = 200;

    /// <summary><see cref="UserAgent"/> ning eng katta uzunligi.</summary>
    public const int MaxUserAgentLength = 300;

    private List<string>? _changedFields;

    private AuditLog(Guid id, DateTimeOffset occurredAt, AuditAction action, string entityName)
        : base(id)
    {
        OccurredAt = occurredAt;
        Action = action;
        EntityName = entityName;
    }

    /// <summary>EF Core uchun.</summary>
    private AuditLog() => EntityName = string.Empty;

    public DateTimeOffset OccurredAt { get; private set; }

    public AuditAction Action { get; private set; }

    /// <summary>Qaysi entity: <c>Employee</c>, <c>User</c>, …</summary>
    public string EntityName { get; private set; }

    public string? EntityId { get; private set; }

    public Guid? UserId { get; private set; }

    /// <summary>
    /// Login nusxasi. Foydalanuvchi keyin o'chirilsa ham
    /// audit yozuvida kim ekani ko'rinib turadi.
    /// </summary>
    public string? UserLogin { get; private set; }

    /// <summary>O'zgarishdan oldingi qiymatlar (JSON).</summary>
    public string? OldValues { get; private set; }

    /// <summary>O'zgarishdan keyingi qiymatlar (JSON).</summary>
    public string? NewValues { get; private set; }

    public string? IpAddress { get; private set; }

    public string? UserAgent { get; private set; }

    /// <summary>Bajaruvchining turi.</summary>
    public AuditActorKind ActorKind { get; private set; }

    /// <summary>Bajaruvchining F.I.Sh. — o'sha lahzadagi nusxa.</summary>
    public string? ActorName { get; private set; }

    /// <summary>Bajaruvchining roli NOMI — o'sha lahzada qanday yozilgan bo'lsa.</summary>
    public string? ActorRole { get; private set; }

    /// <summary>
    /// Amal nomi: endpoint (<c>TransferEmployee</c>) yoki tizim amali
    /// (<c>startup:identity-seed</c>, <c>etl:employees</c>).
    /// </summary>
    public string? Operation { get; private set; }

    /// <summary>
    /// Bitta HTTP so'rov (yoki bitta tizim amali) davomida yozilgan barcha
    /// qatorlarda bir xil — «bitta harakat» guruhi.
    /// </summary>
    public Guid? CorrelationId { get; private set; }

    /// <summary>O'zgarish qaysi xodimga tegishli — xodim kartasidagi tarix shu bo'yicha.</summary>
    public Guid? SubjectEmployeeId { get; private set; }

    /// <summary>Yozuvning odamga tushunarli nomi (xodim, nomzod, rol…) — o'sha lahzadagi nusxa.</summary>
    public string? EntityLabel { get; private set; }

    /// <summary>
    /// O'zgargan maydon nomlari: yaratishda — yozilgan bo'sh bo'lmaganlari,
    /// tahrirda — o'zgarganlari, o'chirishda — <c>null</c>.
    /// </summary>
    /// <remarks>
    /// Yashirilgan maydon (masalan parol xeshi) NOMI shu yerda qoladi —
    /// «parol almashtirildi» degan fakt ko'rinadi, qiymati esa hech qachon.
    /// </remarks>
    public IReadOnlyList<string>? ChangedFields => _changedFields?.AsReadOnly();

    /// <summary>
    /// Qisqa shakl — kengaytirishdan oldingi chaqiruvlar uchun.
    /// Bajaruvchi turi <see cref="AuditActorKind.User"/> bo'ladi.
    /// </summary>
    public static AuditLog Record(
        DateTimeOffset occurredAt,
        AuditAction action,
        string entityName,
        string? entityId = null,
        Guid? userId = null,
        string? userLogin = null,
        string? oldValues = null,
        string? newValues = null,
        string? ipAddress = null,
        string? userAgent = null)
        => RecordChange(new AuditLogEntry(occurredAt, action, entityName)
        {
            EntityId = entityId,
            ActorKind = AuditActorKind.User,
            UserId = userId,
            UserLogin = userLogin,
            OldValues = oldValues,
            NewValues = newValues,
            IpAddress = ipAddress,
            UserAgent = userAgent,
        });

    /// <summary>To'liq yozuv — bajaruvchi, amal, guruh va xodim bilan.</summary>
    /// <remarks>
    /// Parametrlar bitta yozuv turida (<see cref="AuditLogEntry"/>), uzun
    /// ro'yxatda emas: o'n sakkiz ketma-ket satr/<c>Guid?</c> parametrda ikkitasini
    /// almashtirib qo'yish kompilyatsiyadan jimgina o'tib ketardi.
    /// </remarks>
    public static AuditLog RecordChange(AuditLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.EntityName);

        return new AuditLog(NewId(), entry.OccurredAt, entry.Action, entry.EntityName)
        {
            EntityId = entry.EntityId,
            ActorKind = entry.ActorKind,
            UserId = entry.UserId,
            UserLogin = entry.UserLogin,
            ActorName = entry.ActorName,
            ActorRole = entry.ActorRole,
            Operation = entry.Operation,
            CorrelationId = entry.CorrelationId,
            SubjectEmployeeId = entry.SubjectEmployeeId,
            EntityLabel = Truncate(entry.EntityLabel, MaxEntityLabelLength),
            _changedFields = entry.ChangedFields is null ? null : [.. entry.ChangedFields],
            OldValues = entry.OldValues,
            NewValues = entry.NewValues,
            IpAddress = entry.IpAddress,
            UserAgent = Truncate(entry.UserAgent, MaxUserAgentLength),
        };
    }

    private static string? Truncate(string? value, int maxLength)
        => value is null || value.Length <= maxLength ? value : value[..maxLength];
}

/// <summary><see cref="AuditLog.RecordChange"/> ning kirish ma'lumoti.</summary>
/// <param name="OccurredAt">Qachon (UTC).</param>
/// <param name="Action">Amal turi.</param>
/// <param name="EntityName">Entity nomi (<c>Employee</c>) yoki hodisa obyekti (<c>Endpoint</c>).</param>
public sealed record AuditLogEntry(DateTimeOffset OccurredAt, AuditAction Action, string EntityName)
{
    public string? EntityId { get; init; }

    public AuditActorKind ActorKind { get; init; } = AuditActorKind.User;

    public Guid? UserId { get; init; }

    public string? UserLogin { get; init; }

    public string? ActorName { get; init; }

    public string? ActorRole { get; init; }

    public string? Operation { get; init; }

    public Guid? CorrelationId { get; init; }

    public Guid? SubjectEmployeeId { get; init; }

    public string? EntityLabel { get; init; }

    public IReadOnlyList<string>? ChangedFields { get; init; }

    public string? OldValues { get; init; }

    public string? NewValues { get; init; }

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }
}
