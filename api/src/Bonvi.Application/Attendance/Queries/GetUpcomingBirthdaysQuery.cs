using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Queries;

/// <summary>
/// Asosiy sahifa — yaqinlashib kelayotgan tug'ilgan kunlar
/// (<c>GET /dashboard/birthdays</c>).
/// </summary>
/// <remarks>
/// <para>
/// Sana parametri ATAYLAB yo'q: «bugun» qachonligini mijoz aytmaydi, u
/// serverda <see cref="IClock"/> orqali, Toshkent vaqti bo'yicha olinadi —
/// bosh sahifadagi ro'yxat bilan AYNAN bir manbadan.
/// </para>
/// <para>
/// Davr ham yo'q: panel «tanlangan oraliqda kim» degan savolga emas,
/// «keyingi navbatda kim» degan savolga javob beradi (§E5). Org-filtr esa
/// BOR — u ekranning qolgan qismi bilan bitta to'plamni ko'rsatadi (§E2).
/// </para>
/// </remarks>
/// <param name="LegalEntityId">Firma.</param>
/// <param name="DepartmentId">Bo'lim (ichki bo'limlari bilan).</param>
/// <param name="PositionId">Lavozim.</param>
/// <param name="Limit">Nechta odam — berilmasa <see cref="GetUpcomingBirthdaysQueryHandler.DefaultLimit"/>.</param>
public sealed record GetUpcomingBirthdaysQuery(
    Guid? LegalEntityId = null,
    Guid? DepartmentId = null,
    Guid? PositionId = null,
    int? Limit = null);

/// <summary><see cref="GetUpcomingBirthdaysQuery"/> ni bajaradi.</summary>
/// <remarks>
/// <para>
/// <b>Uzunlik 400 bilan rad ETILMAYDI, qisiladi.</b> Hisobotlardagi davr
/// tekshiruvidan farqi shu: u yerda noto'g'ri oraliq javobning MA'NOSINI
/// buzadi va HR uni tuzatishi kerak; bu yerda esa «nechta qator ko'rsatilsin»
/// — ko'rinish tafsiloti, va uning uchun xato ekrani chiqarish panelni
/// sababsiz bo'shatardi.
/// </para>
/// <para>
/// Ruxsat bu yerda tekshirilmaydi: endpoint <c>attendance.view</c> bilan
/// yopilgan va javobda maskalanadigan maydon yo'q — ism, rasm, lavozim va
/// bo'lim panelning jadvalida allaqachon ochiq.
/// </para>
/// </remarks>
public sealed class GetUpcomingBirthdaysQueryHandler
{
    /// <summary>Sukut bo'yicha uzunlik — egasining qarori (§E1).</summary>
    public const int DefaultLimit = 10;

    /// <summary>Eng ko'pi — panel ro'yxat emas, yon ustundagi karta.</summary>
    public const int MaxLimit = 50;

    private readonly IAttendanceReadModel _readModel;
    private readonly IClock _clock;

    public GetUpcomingBirthdaysQueryHandler(IAttendanceReadModel readModel, IClock clock)
    {
        _readModel = readModel;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<AttendanceBirthdayDto>>> HandleAsync(
        GetUpcomingBirthdaysQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var limit = Math.Clamp(query.Limit ?? DefaultLimit, 1, MaxLimit);

        var items = await _readModel
            .GetUpcomingBirthdaysAsync(
                new BirthdayFilter(
                    _clock.Today,
                    query.LegalEntityId,
                    query.DepartmentId,
                    query.PositionId,
                    limit),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(items);
    }
}
