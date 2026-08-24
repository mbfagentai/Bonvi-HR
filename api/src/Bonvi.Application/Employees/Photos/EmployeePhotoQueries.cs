using Bonvi.Application.Common.Photos;
using Bonvi.Application.Employees.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Employees.Photos;

/// <summary>Xodim rasmini o'qish so'rovi.</summary>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="KnownVersion">
/// Mijozdagi nusxaning versiya belgisi. Serverdagi bilan mos kelsa, rasm
/// qayta yuborilmaydi (HTTP darajasida bu <c>If-None-Match</c>).
/// </param>
public sealed record GetEmployeePhotoQuery(Guid EmployeeId, string? KnownVersion = null);

/// <summary>
/// Xodim rasmini qaytaradi.
/// </summary>
/// <remarks>
/// Kalitni topish — shu handler ishi, faylni ochish va versiyani solishtirish
/// esa <see cref="SquarePhotoStore"/> niki. Fayl yo'qolgan holat (kalit bazada
/// bor, fayl esa yo'q) ham «rasm yo'q» deb qaraladi: interfeys uchun natija
/// bir xil, xato ko'rsatishning ma'nosi yo'q.
/// </remarks>
public sealed class GetEmployeePhotoQueryHandler
{
    private readonly IEmployeeRepository _employees;
    private readonly SquarePhotoStore _photos;

    public GetEmployeePhotoQueryHandler(IEmployeeRepository employees, SquarePhotoStore photos)
    {
        _employees = employees;
        _photos = photos;
    }

    public async Task<Result<PhotoContent>> HandleAsync(
        GetEmployeePhotoQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var employee = await _employees.GetByIdAsync(query.EmployeeId, cancellationToken).ConfigureAwait(false);
        if (employee is null)
        {
            return EmployeeErrors.NotFound;
        }

        if (employee.PhotoKey is not { } key)
        {
            return EmployeeErrors.PhotoNotFound;
        }

        var content = await _photos.OpenAsync(key, query.KnownVersion, cancellationToken).ConfigureAwait(false);

        if (content is null)
        {
            return EmployeeErrors.PhotoNotFound;
        }

        return Result.Success(content);
    }
}
