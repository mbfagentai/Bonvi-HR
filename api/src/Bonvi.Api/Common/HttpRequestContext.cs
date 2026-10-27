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
/// IP — <see cref="ClientIp"/>: Caddy bergan HAQIQIY mijoz (SPEC-planshet-anketa §2.3.3). Ilgari
/// <c>Connection.RemoteIpAddress</c> edi va prod'da u Cloudflare chekkasi — 2026-10-12 dan audit
/// IP'lari tergovga yaramasdi. Ushbu IP kirish xatolari oynasining kalitiga ham tushadi
/// (<c>AuditLogger</c>).
/// </para>
/// <para>
/// Yagona istisno — qurilma endpointlari (<see cref="DeviceEndpointMetadata"/>: terminal qabuli va
/// davomat agenti): u yerda avvalgidek ulanish manzili (terminallarning xulqi o'zgarmasin, qurilma
/// kartasidagi IP ro'yxati ham shunga qarab tekshiriladi).
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
    public string? IpAddress
    {
        get
        {
            var http = _accessor.HttpContext;

            if (http is null)
            {
                return null;
            }

            return http.GetEndpoint()?.Metadata.GetMetadata<DeviceEndpointMetadata>() is not null
                ? http.Connection.RemoteIpAddress?.ToString()
                : ClientIp.Resolve(http)?.ToString();
        }
    }

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
