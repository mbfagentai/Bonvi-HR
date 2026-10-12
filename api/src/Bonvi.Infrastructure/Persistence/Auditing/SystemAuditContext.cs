using Bonvi.Application.Abstractions;
using Bonvi.Application.Common.Auditing;

namespace Bonvi.Infrastructure.Persistence.Auditing;

/// <summary>
/// HTTP'siz xost uchun audit konteksti: bajaruvchi doim <c>System</c>, amal nomi va guruh —
/// <see cref="Use"/> dan, IP va brauzer yo'q.
/// </summary>
/// <remarks>
/// <para>
/// ETL (<c>EtlDatabase</c>) shuni ishlatadi: u o'z kontekstini DI'siz yasaydi va
/// har bir qadamni <c>etl:&lt;qadam&gt;</c> nomi bilan o'raydi.
/// </para>
/// <para>
/// Qamrov ochilmagan bo'lsa ham qatorlar BITTA guruhga tushadi (nusxa yashagan
/// davomda) — «bu yugurish nimalarni yozdi» savoliga javob qolsin.
/// </para>
/// </remarks>
public sealed class SystemAuditContext : IRequestContext, IAuditScope, IAuditActorSource
{
    private readonly AuditScopeTracker _scopes = new();
    private readonly Guid _instanceCorrelation = Guid.CreateVersion7();

    /// <inheritdoc />
    public string? Operation => _scopes.Operation;

    /// <inheritdoc />
    public Guid? CorrelationId => _scopes.CorrelationId ?? _instanceCorrelation;

    /// <inheritdoc />
    public string? IpAddress => null;

    /// <inheritdoc />
    public string? UserAgent => null;

    /// <inheritdoc />
    public AuditPrincipal Current => AuditPrincipal.System;

    /// <inheritdoc />
    public IDisposable Use(string operation) => _scopes.Push(operation);
}
