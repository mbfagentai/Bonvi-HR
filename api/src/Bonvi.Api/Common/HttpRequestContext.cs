using Bonvi.Application.Abstractions;
using Bonvi.Application.Common.Auditing;

namespace Bonvi.Api.Common;

/// <summary>
/// <see cref="IRequestContext"/> va <see cref="IAuditScope"/> — HTTP so'rovidan.
/// </summary>
/// <remarks>
/// <para>
/// <b>Amal nomi</b> — moslangan endpointning nomi (<c>.WithName("TransferEmployee")</c>).
/// Har bir endpointda nom bor, shu sababli jurnalda «qaysi tugma bosilgan» aniq turadi.
/// </para>
/// <para>
/// <b>Guruh</b> — so'rov boshida BIR MARTA yasalgan Guid v7, <c>HttpContext.Items</c> da
/// saqlanadi: so'rov ichida nechta saqlash bo'lsa ham, hammasining qatorlari bitta
/// guruhda («ko'chirish» = mehnat munosabati + buyruq).
/// </para>
/// <para>
/// <b>HTTP'siz qamrov</b> (ishga tushish, Hangfire vazifasi) — <see cref="Use"/> bergan nom
/// va guruh; hech biri berilmasa nom bo'sh, guruh esa shu qamrov uchun bitta.
/// </para>
/// <para>
/// IP — <c>Connection.RemoteIpAddress</c>: proksi sarlavhalari konveyer boshida
/// qo'llangan (<c>UseBonviSecurity</c>), ya'ni bu Caddy emas, mijozning manzili.
/// </para>
/// </remarks>
public sealed class HttpRequestContext : IRequestContext, IAuditScope
{
    private const string CorrelationItemKey = "bonvi.audit.correlation-id";

    private readonly IHttpContextAccessor _accessor;
    private readonly AuditScopeTracker _scopes = new();
    private Guid? _detachedCorrelation;

    public HttpRequestContext(IHttpContextAccessor accessor) => _accessor = accessor;

    /// <inheritdoc />
    public string? Operation => _scopes.IsActive
        ? _scopes.Operation
        : _accessor.HttpContext?.GetEndpoint()?.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName;

    /// <inheritdoc />
    public Guid? CorrelationId => _scopes.CorrelationId ?? RequestCorrelation() ?? (_detachedCorrelation ??= Guid.CreateVersion7());

    /// <inheritdoc />
    public string? IpAddress => _accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    /// <inheritdoc />
    public string? UserAgent
    {
        get
        {
            var value = _accessor.HttpContext?.Request.Headers.UserAgent.ToString();

            return string.IsNullOrEmpty(value) ? null : value;
        }
    }

    /// <inheritdoc />
    public IDisposable Use(string operation) => _scopes.Push(operation);

    private Guid? RequestCorrelation()
    {
        var http = _accessor.HttpContext;
        if (http is null)
        {
            return null;
        }

        if (http.Items.TryGetValue(CorrelationItemKey, out var stored) && stored is Guid known)
        {
            return known;
        }

        var created = Guid.CreateVersion7();
        http.Items[CorrelationItemKey] = created;
        return created;
    }
}
