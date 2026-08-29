using Bonvi.Application.Abstractions;
using Bonvi.Application.Notifications.Common;
using Bonvi.Application.Notifications.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Notifications.Queries;

/// <summary>
/// Foydalanuvchining bildirishnomalari.
/// </summary>
/// <remarks>
/// <b>O'chirilganlar hech qanday parametr bilan qaytmaydi.</b> «Savatcha»
/// yoki «o'chirilganlarni ko'rsatish» rejimi ataylab yo'q: foydalanuvchi
/// o'chirganda «bu menga endi kerak emas» deydi, «keyin qarayman»
/// demaydi. Kerak bo'lganda ma'lumotning o'zi joyida turibdi —
/// sinov ro'yxatida, nomzod kartasida.
/// </remarks>
/// <param name="OnlyUnread">
/// Faqat o'qilmaganlar. <c>false</c> (sukut) — o'qilganlari ham qaytadi:
/// qo'ng'iroq ochilganda ro'yxat bo'shab qolmasligi kerak, aks holda
/// «hozirgina nima yozilgan edi?» degan savolga javob topib bo'lmasdi.
/// </param>
/// <param name="Limit">
/// Nechta yozuv qaytsin. Bo'sh — <see cref="ListNotificationsQueryHandler.DefaultLimit"/>.
/// <para>
/// Sahifalash (offset/cursor) ATAYLAB yo'q. Bildirishnomalar bazadan
/// o'qilmaydi, HISOBLANADI — ya'ni ikkinchi sahifani berish uchun
/// hammasini qaytadan hisoblash kerak bo'lardi va shu oraliqda ro'yxat
/// o'zgargan bo'lsa, yozuvlar sahifalar orasida sakrab yurardi. Amaliy
/// hajm esa kichik: 540 xodimda eng ko'p bo'lgan tur — tug'ilgan kunlar,
/// ular kuniga o'rtacha uch-to'rtta. Chegara faqat kutilmagan holat uchun.
/// </para>
/// </param>
public sealed record ListNotificationsQuery(bool OnlyUnread = false, int? Limit = null);

public sealed class ListNotificationsQueryHandler
{
    /// <summary>Sukut bo'yicha nechta yozuv qaytadi.</summary>
    public const int DefaultLimit = 50;

    /// <summary>Bir so'rovda qaytishi mumkin bo'lgan eng ko'p yozuv.</summary>
    public const int MaxLimit = 200;

    private readonly NotificationCollector _collector;
    private readonly INotificationStateRepository _states;
    private readonly ICurrentUser _user;

    public ListNotificationsQueryHandler(
        NotificationCollector collector,
        INotificationStateRepository states,
        ICurrentUser user)
    {
        _collector = collector;
        _states = states;
        _user = user;
    }

    public async Task<Result<NotificationListDto>> HandleAsync(
        ListNotificationsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (_user.UserId is not { } userId)
        {
            return NotificationErrors.NotAuthenticated;
        }

        var computed = await _collector.CollectAsync(cancellationToken).ConfigureAwait(false);

        var visible = await ApplyUserStateAsync(computed, userId, _states, cancellationToken)
            .ConfigureAwait(false);

        var unread = visible.Count(item => !item.IsRead);

        var limit = Math.Clamp(query.Limit ?? DefaultLimit, 1, MaxLimit);

        var items = query.OnlyUnread
            ? visible.Where(item => !item.IsRead)
            : visible;

        return Result.Success(new NotificationListDto(
            [.. items.Take(limit)],
            visible.Count,
            unread));
    }

    /// <summary>
    /// Hisoblangan bildirishnomalarga foydalanuvchining holatini qo'shadi:
    /// o'chirilganlarini olib tashlaydi, o'qilganlarini belgilaydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega faqat SHU SAFAR hisoblangan kalitlar so'raladi.</b>
    /// Foydalanuvchining butun tarixi yillar davomida o'sadi (har
    /// «hammasini o'qildi» bosilganda o'nlab qator), joriy ro'yxat esa
    /// har doim kichik. Hammasini yuklab, keyin xotirada kesish bir kun
    /// jimgina sekinlashardi — bu loyihada ro'yxat ekranlari uchun
    /// ataylab rad etilgan naqsh (<c>IEmployeeReadModel</c> izohi).
    /// </para>
    /// <para>
    /// Statik metod va bog'liqliklari parametrda: shu mantiq
    /// <c>GetUnreadCountQuery</c> da ham, «hammasini o'qildi» amalida ham
    /// kerak. Uchala joyda takrorlansa, bittasida o'chirilganlarni
    /// filtrlash unutilib, o'chirilgan bildirishnoma sanoqda qolib
    /// ketardi — foydalanuvchi esa ro'yxatda ko'rinmaydigan «1» ni
    /// yopa olmasdi.
    /// </para>
    /// </remarks>
    internal static async Task<IReadOnlyList<NotificationDto>> ApplyUserStateAsync(
        IReadOnlyList<NotificationDto> computed,
        Guid userId,
        INotificationStateRepository states,
        CancellationToken cancellationToken)
    {
        if (computed.Count == 0)
        {
            return [];
        }

        var keys = computed.Select(item => item.Key).ToArray();

        var saved = await states.GetAsync(userId, keys, cancellationToken).ConfigureAwait(false);

        var byKey = saved.ToDictionary(state => state.Key, StringComparer.Ordinal);

        var result = new List<NotificationDto>(computed.Count);
        foreach (var item in computed)
        {
            if (!byKey.TryGetValue(item.Key, out var state))
            {
                result.Add(item);
                continue;
            }

            if (state.IsDismissed)
            {
                // Sharti hamon rost bo'lsa ham qaytmaydi — foydalanuvchining
                // aniq talabi (`NotificationState.Dismiss` izohi).
                continue;
            }

            result.Add(item with { IsRead = state.IsRead, ReadAt = state.ReadAt });
        }

        return result;
    }
}

/// <summary>
/// O'qilmaganlar soni — qo'ng'iroq ustidagi nuqta uchun.
/// </summary>
/// <remarks>
/// <b>Nega alohida endpoint, garchi sanoq ro'yxat javobida ham
/// bo'lsa.</b> Ikkalasi ikki xil holatga xizmat qiladi: ro'yxat
/// foydalanuvchi qo'ng'iroqni BOSGANDA so'raladi, sanoq esa u boshqa
/// ish qilib turganda vaqti-vaqti bilan yangilanadi. Sanoq uchun butun
/// ro'yxatni tarmoqdan olib o'tish (o'nlab yozuv, har birida
/// parametrlar) ma'nosiz bo'lardi.
/// </remarks>
public sealed record GetUnreadNotificationCountQuery;

public sealed class GetUnreadNotificationCountQueryHandler
{
    private readonly NotificationCollector _collector;
    private readonly INotificationStateRepository _states;
    private readonly ICurrentUser _user;

    public GetUnreadNotificationCountQueryHandler(
        NotificationCollector collector,
        INotificationStateRepository states,
        ICurrentUser user)
    {
        _collector = collector;
        _states = states;
        _user = user;
    }

    public async Task<Result<NotificationCountDto>> HandleAsync(
        GetUnreadNotificationCountQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (_user.UserId is not { } userId)
        {
            return NotificationErrors.NotAuthenticated;
        }

        // Sanoq ham AYNAN shu yo'ldan o'tadi: hisoblash → holat → filtr.
        // Ikkinchi, «tezroq» yo'l yozilsa (masalan faqat sonlarni sanash),
        // u ro'yxat bilan bir kun ajralib ketardi va nuqta ochilmaydigan
        // ro'yxatga ishora qilib turardi.
        var computed = await _collector.CollectAsync(cancellationToken).ConfigureAwait(false);

        var visible = await ListNotificationsQueryHandler
            .ApplyUserStateAsync(computed, userId, _states, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new NotificationCountDto(visible.Count(item => !item.IsRead)));
    }
}
