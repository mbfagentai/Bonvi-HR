using Bonvi.Application.Abstractions;
using Bonvi.Domain.Auditing;

namespace Bonvi.Infrastructure.Persistence.Auditing;

/// <summary>
/// <c>IAuditLogger</c> navbatga qo'ygan, hali saqlanmagan HODISALAR — qamrov (so'rov) davomida.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega to'g'ridan-to'g'ri <c>AuditLog</c> qo'shilmaydi.</b> Bajaruvchining
/// ismi va roli bazadan o'qiladi; <c>Record</c> esa sinxron va har chaqiruvda
/// bazaga borishi sinxron kiritish-chiqarish bo'lardi. Navbatdagi hodisani
/// <c>AuditTrailInterceptor</c> saqlash paytida (asinxron yo'lda — asinxron)
/// to'liq qatorga aylantiradi: entity o'zgarishlari bilan BIR XIL bajaruvchi,
/// amal nomi, guruh va IP oladi.
/// </para>
/// <para>
/// Saqlash yiqilsa hodisalar ham ketadi — bo'lmagan amalning izi qolmasin
/// (interceptor qo'shgan qatorlarni kuzatuvdan chiqaradi).
/// </para>
/// </remarks>
public sealed class PendingAuditEvents
{
    private readonly List<PendingAuditEvent> _events = [];

    /// <summary>Navbatga qo'yadi.</summary>
    /// <param name="auditEvent">Hodisa.</param>
    public void Add(PendingAuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        _events.Add(auditEvent);
    }

    /// <summary>Hammasini oladi va navbatni bo'shatadi.</summary>
    public IReadOnlyList<PendingAuditEvent> Drain()
    {
        if (_events.Count == 0)
        {
            return [];
        }

        PendingAuditEvent[] drained = [.. _events];
        _events.Clear();
        return drained;
    }
}

/// <summary>Navbatdagi bitta hodisa.</summary>
/// <param name="OccurredAt">Qachon (UTC).</param>
/// <param name="Action">Amal turi.</param>
/// <param name="EntityName">Obyekt nomi.</param>
/// <param name="EntityId">Obyekt identifikatori.</param>
/// <param name="OldValues">Oldingi qiymatlar (JSON).</param>
/// <param name="NewValues">Tafsilot (JSON).</param>
/// <param name="Actor">
/// Aniq bajaruvchi (kirish hodisalari — so'rovda tasdiqlangan foydalanuvchi yo'q);
/// <c>null</c> — joriy bajaruvchi (<see cref="IAuditActorSource"/>).
/// </param>
public sealed record PendingAuditEvent(
    DateTimeOffset OccurredAt,
    AuditAction Action,
    string EntityName,
    string? EntityId,
    string? OldValues,
    string? NewValues,
    AuditPrincipal? Actor);
