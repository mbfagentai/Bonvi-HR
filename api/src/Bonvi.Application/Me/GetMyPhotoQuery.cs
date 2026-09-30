using Bonvi.Application.Common.Photos;
using Bonvi.Application.Employees.Photos;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Me;

/// <summary>Xodimning O'Z rasmi — <c>GET /api/me/photo</c>.</summary>
/// <param name="KnownVersion">Mijozdagi nusxaning versiyasi (<c>If-None-Match</c>).</param>
public sealed record GetMyPhotoQuery(string? KnownVersion = null);

/// <summary>
/// <see cref="GetMyPhotoQuery"/> ni bajaradi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Identifikator TOKENDAN</b> (hisob qatori orqali —
/// <see cref="CurrentEmployeeResolver"/>), manzilda umuman yo'q: «boshqa
/// xodimning rasmini so'rash» degan yo'l texnik jihatdan mavjud emas —
/// <c>/api/auth/me/photo</c> dagi bilan bir xil naqsh.
/// </para>
/// <para>
/// Faylni ochish HR yo'lidagi ishlovchining O'ZIGA topshiriladi
/// (<see cref="GetEmployeePhotoQueryHandler"/>): kalitni topish, faylni
/// ochish, versiya va «rasm yo'q» javobi (<c>xodim.rasm_yoq</c>, 404) —
/// hammasi bitta joyda, ikkinchi nusxasiz.
/// </para>
/// </remarks>
public sealed class GetMyPhotoQueryHandler
{
    private readonly CurrentEmployeeResolver _resolver;
    private readonly GetEmployeePhotoQueryHandler _photos;

    public GetMyPhotoQueryHandler(CurrentEmployeeResolver resolver, GetEmployeePhotoQueryHandler photos)
    {
        _resolver = resolver;
        _photos = photos;
    }

    public async Task<Result<PhotoContent>> HandleAsync(
        GetMyPhotoQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var employeeId = await _resolver.EmployeeIdAsync(cancellationToken).ConfigureAwait(false);
        if (employeeId.IsFailure)
        {
            return employeeId.Error;
        }

        return await _photos
            .HandleAsync(new GetEmployeePhotoQuery(employeeId.Value, query.KnownVersion), cancellationToken)
            .ConfigureAwait(false);
    }
}
