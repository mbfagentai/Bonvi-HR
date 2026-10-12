using Bonvi.Application.Abstractions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Infrastructure.Persistence.Auditing;

namespace Bonvi.Infrastructure.Services;

/// <summary>
/// <see cref="IAuditLogger"/> — hodisani shu qamrovning navbatiga qo'yadi; qatorni saqlash
/// paytida <c>AuditTrailInterceptor</c> yasaydi.
/// </summary>
/// <remarks>
/// <para>
/// Bu yerda bazaga murojaat YO'Q va bo'lmasligi kerak: bajaruvchining ismi va rol
/// nomi bazadan o'qiladi, <c>Record</c> esa sinxron. Interceptor buni saqlash
/// paytida (asinxron yo'lda — asinxron) entity o'zgarishlari bilan bitta to'plamda
/// qiladi — natijada hodisa va o'zgarish qatorlari bir xil bajaruvchi, amal nomi,
/// guruh va IP bilan yoziladi (SPEC §7.4: «qo'lda yo'l ham o'sha portlardan»).
/// </para>
/// <para>
/// <see cref="AuditLog"/> uchun alohida repozitoriy interfeysi ataylab yo'q:
/// unga bu qatlamda o'qish so'rovi bo'lmaydi (faqat qo'shiladi).
/// </para>
/// </remarks>
public sealed class AuditLogger : IAuditLogger
{
    private readonly PendingAuditEvents _pending;
    private readonly IClock _clock;
    private readonly IRequestContext _request;
    private readonly LoginFailureWindow _loginFailures;

    public AuditLogger(PendingAuditEvents pending, IClock clock, IRequestContext request, LoginFailureWindow loginFailures)
    {
        _pending = pending;
        _clock = clock;
        _request = request;
        _loginFailures = loginFailures;
    }

    public void Record(
        AuditAction action,
        string entityName,
        string? entityId,
        string? oldValues = null,
        string? newValues = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);

        _pending.Add(new PendingAuditEvent(_clock.UtcNow, action, entityName, entityId, oldValues, newValues, Actor: null));
    }

    public void RecordSecurityEvent(AuditAction action, Guid? userId, string? login, string? detailJson)
    {
        // Bir xil xato urinish (IP + login + sabab) 10 daqiqada bir marta — sababi
        // `LoginFailureWindow` izohida. Muvaffaqiyatli kirish va boshqa hodisalar har doim yoziladi.
        if (action == AuditAction.LoginFailed
            && !_loginFailures.FirstInWindow($"{_request.IpAddress}|{login}|{detailJson}"))
        {
            return;
        }

        var actor = userId is { } id
            ? new AuditPrincipal(AuditActorKind.User, id, login)
            : AuditPrincipal.Anonymous with { Login = login };

        _pending.Add(new PendingAuditEvent(
            _clock.UtcNow,
            action,
            "User",
            userId?.ToString(),
            OldValues: null,
            NewValues: detailJson,
            actor));
    }
}
