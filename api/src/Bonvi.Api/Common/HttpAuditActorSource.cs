using Bonvi.Application.Abstractions;
using Bonvi.Domain.Auditing;

namespace Bonvi.Api.Common;

/// <summary>
/// <see cref="IAuditActorSource"/> — so'rovning tokenidan va endpointidan.
/// </summary>
/// <remarks>
/// <para>Tartib muhim:</para>
/// <list type="number">
///   <item>HTTP so'rovi yo'q (ishga tushish, Hangfire) → <see cref="AuditActorKind.System"/>;</item>
///   <item>qurilma endpointi (<see cref="DeviceEndpointMetadata"/>) → <see cref="AuditActorKind.Device"/> —
///         u yerda JWT umuman yo'q, himoya qurilma tokenida;</item>
///   <item>HR tokeni → <see cref="AuditActorKind.User"/>;</item>
///   <item>xodim ilovasi tokeni → <see cref="AuditActorKind.EmployeeAccount"/> (u tokenda
///         <c>NameIdentifier</c> yo'q, ya'ni HR tokeni bilan adashmaydi);</item>
///   <item>qolgani (kirish sahifasi, ochiq endpoint) → <see cref="AuditActorKind.Anonymous"/>.</item>
/// </list>
/// <para>
/// Qurilmaning QAYSI biri ekani yozilmaydi: uni qabul ishlovchisi token bo'yicha
/// topadi va bu qatlamga qaytarmaydi. O'zgargan qator (qurilma yozuvi, moslik)
/// o'zi qurilmani ko'rsatadi, amal nomi esa endpointni (<c>docs/ASSUMPTIONS.md</c>).
/// </para>
/// </remarks>
public sealed class HttpAuditActorSource : IAuditActorSource
{
    private readonly IHttpContextAccessor _accessor;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentEmployee _currentEmployee;

    public HttpAuditActorSource(
        IHttpContextAccessor accessor,
        ICurrentUser currentUser,
        ICurrentEmployee currentEmployee)
    {
        _accessor = accessor;
        _currentUser = currentUser;
        _currentEmployee = currentEmployee;
    }

    /// <inheritdoc />
    public AuditPrincipal Current
    {
        get
        {
            var http = _accessor.HttpContext;
            if (http is null)
            {
                return AuditPrincipal.System;
            }

            if (http.GetEndpoint()?.Metadata.GetMetadata<DeviceEndpointMetadata>() is not null)
            {
                return AuditPrincipal.Device;
            }

            if (_currentUser.UserId is { } userId)
            {
                return new AuditPrincipal(AuditActorKind.User, userId, _currentUser.UserName);
            }

            if (_currentEmployee.AccountId is not null)
            {
                return new AuditPrincipal(AuditActorKind.EmployeeAccount, EmployeeId: _currentEmployee.EmployeeId);
            }

            return AuditPrincipal.Anonymous;
        }
    }
}
