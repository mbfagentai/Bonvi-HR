using Bonvi.Domain.Common;

namespace Bonvi.Domain.Auditing;

/// <summary>Kuzatiladigan amal turi.</summary>
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
/// yo'l bilan qiymat bermaydi, o'zgartiruvchi metodlari yo'q.
/// </para>
/// </remarks>
public sealed class AuditLog : AggregateRoot
{
    private AuditLog(
        Guid id,
        DateTimeOffset occurredAt,
        AuditAction action,
        string entityName,
        string? entityId,
        Guid? userId,
        string? userLogin,
        string? oldValues,
        string? newValues,
        string? ipAddress,
        string? userAgent)
        : base(id)
    {
        OccurredAt = occurredAt;
        Action = action;
        EntityName = entityName;
        EntityId = entityId;
        UserId = userId;
        UserLogin = userLogin;
        OldValues = oldValues;
        NewValues = newValues;
        IpAddress = ipAddress;
        UserAgent = userAgent;
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
        => new(
            NewId(), occurredAt, action, entityName, entityId,
            userId, userLogin, oldValues, newValues, ipAddress, userAgent);
}
