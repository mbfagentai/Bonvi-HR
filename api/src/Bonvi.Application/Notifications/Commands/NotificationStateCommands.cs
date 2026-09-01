using Bonvi.Application.Abstractions;
using Bonvi.Application.Notifications.Common;
using Bonvi.Application.Notifications.Dtos;
using Bonvi.Application.Notifications.Queries;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Notifications;

namespace Bonvi.Application.Notifications.Commands;

/// <summary>
/// Bildirishnomalarni «o'qildi» deb belgilaydi.
/// </summary>
/// <param name="Keys">Qaysi bildirishnomalar. <paramref name="All"/> berilganda e'tiborga olinmaydi.</param>
/// <param name="All">
/// Hammasini belgilash. Kalitlar ro'yxati kerak emas: server foydalanuvchi
/// AYNAN SHU PAYTDA ko'rayotgan bildirishnomalarni o'zi hisoblaydi.
/// <para>
/// Nega ro'yxat klientdan olinmaydi: ekran ochilgandan keyin yangi
/// bildirishnoma paydo bo'lgan bo'lishi mumkin (masalan yarim tunda sana
/// almashdi). Klient o'zidagi eski ro'yxatni yuborsa, o'sha yangisi
/// «o'qilmagan» bo'lib qolardi va foydalanuvchi «hammasini o'qildi» dan
/// keyin ham nuqtani ko'rib turardi.
/// </para>
/// </param>
public sealed record MarkNotificationsReadCommand(IReadOnlyList<string>? Keys = null, bool All = false);

/// <summary>
/// Bildirishnomalarni ro'yxatdan olib tashlaydi.
/// </summary>
/// <remarks>
/// <b>Nega «hammasini o'chirish» YO'Q</b> («o'qildi» da esa bor). O'chirish
/// qaytarib bo'lmaydigan amal: yozuv o'sha foydalanuvchi uchun boshqa
/// hech qachon chiqmaydi — hatto sinov muddati hamon yakunlanmagan bo'lsa
/// ham. Bunday amalni bitta tugma bilan hammasiga qo'llash — bir bosishda
/// butun ish ro'yxatini yo'qotish demak. «O'qildi» esa zararsiz: yozuv
/// joyida qoladi, faqat rangi so'nadi.
/// </remarks>
/// <param name="Keys">Qaysi bildirishnomalar.</param>
public sealed record DeleteNotificationsCommand(IReadOnlyList<string>? Keys);

/// <summary>«O'qildi» va «o'chirish» uchun umumiy yozish mantiqi.</summary>
/// <remarks>
/// <para>
/// Ikkala amal ham bitta ishni bajaradi: kalit bo'yicha holat yozuvini
/// topadi, bo'lmasa yaratadi va unga sana qo'yadi. Farq faqat QAYSI
/// sana qo'yilishida. Ikki handlerda takrorlansa, kalit tekshiruvi yoki
/// takroriy kalitlarni tashlash bittasida unutilishi mumkin edi.
/// </para>
/// <para>
/// <b>Nega mavjud yozuv qayta yozilmaydi (upsert emas, «topib-yangilash»).</b>
/// Xom <c>INSERT ... ON CONFLICT</c> tezroq bo'lardi, lekin u domen
/// qoidalarini chetlab o'tardi: <c>MarkRead</c> birinchi o'qish vaqtini
/// SAQLAB QOLADI, <c>Dismiss</c> esa o'chirish vaqtini. Bu qoidalar
/// entity ichida turadi va SQL ularni bilmaydi.
/// </para>
/// </remarks>
internal static class NotificationStateWriter
{
    /// <summary>Berilgan kalitlarga amalni qo'llaydi va nechtasi o'zgarganini qaytaradi.</summary>
    public static async Task<int> ApplyAsync(
        Guid userId,
        IReadOnlyCollection<string> keys,
        Action<NotificationState> action,
        INotificationStateRepository states,
        CancellationToken cancellationToken)
    {
        if (keys.Count == 0)
        {
            return 0;
        }

        var saved = await states.GetAsync(userId, keys, cancellationToken).ConfigureAwait(false);
        var byKey = saved.ToDictionary(state => state.Key, StringComparer.Ordinal);

        var affected = 0;
        foreach (var key in keys)
        {
            if (byKey.TryGetValue(key, out var existing))
            {
                action(existing);
                affected++;
                continue;
            }

            // Kalit ALLAQACHON tekshirilgan (`Normalize`), shu sababli bu
            // yerda xato bo'lishi mumkin emas. Shunga qaramay natija
            // e'tibordan qoldirilmaydi: `Result` ni tashlab yuborish —
            // aynan shu loyihada rad etilgan naqsh.
            var created = NotificationState.For(userId, key);
            if (created.IsFailure)
            {
                continue;
            }

            action(created.Value);
            states.Add(created.Value);
            affected++;
        }

        return affected;
    }

    /// <summary>
    /// Mijozdan kelgan kalitlarni tozalaydi va tekshiradi.
    /// </summary>
    /// <remarks>
    /// Takrorlangan kalitlar tashlanadi (<c>HashSet</c>): bitta so'rovda
    /// bir xil kalit ikki marta kelsa, u ikki marta sanalib, javobdagi
    /// «nechta o'zgardi» soni yolg'on chiqardi.
    /// </remarks>
    public static Result<IReadOnlyCollection<string>> Normalize(IReadOnlyList<string>? keys)
    {
        if (keys is null || keys.Count == 0)
        {
            return NotificationErrors.NothingToDo;
        }

        if (keys.Count > NotificationRules.MaxKeysPerRequest)
        {
            return NotificationErrors.TooManyKeys;
        }

        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            var trimmed = key?.Trim();
            if (!NotificationKey.IsValid(trimmed))
            {
                return NotificationErrors.InvalidKey;
            }

            unique.Add(trimmed!);
        }

        return Result.Success<IReadOnlyCollection<string>>(unique);
    }
}

public sealed class MarkNotificationsReadCommandHandler
{
    private readonly NotificationCollector _collector;
    private readonly INotificationStateRepository _states;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public MarkNotificationsReadCommandHandler(
        NotificationCollector collector,
        INotificationStateRepository states,
        ICurrentUser user,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _collector = collector;
        _states = states;
        _user = user;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<NotificationActionResultDto>> HandleAsync(
        MarkNotificationsReadCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (_user.UserId is not { } userId)
        {
            return NotificationErrors.NotAuthenticated;
        }

        var now = _clock.UtcNow;

        if (command.All)
        {
            // Hisoblash BIR MARTA: shu ro'yxatdan ham belgilanadigan
            // kalitlar olinadi, ham amaldan keyingi sanoq kelib chiqadi —
            // hammasi o'qilgach o'qilmaganlar soni ta'rifi bo'yicha nol.
            var visible = await VisibleAsync(userId, cancellationToken).ConfigureAwait(false);

            var unreadKeys = visible
                .Where(item => !item.IsRead)
                .Select(item => item.Key)
                .ToArray();

            var marked = await NotificationStateWriter
                .ApplyAsync(userId, unreadKeys, state => state.MarkRead(now), _states, cancellationToken)
                .ConfigureAwait(false);

            if (marked > 0)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            return Result.Success(new NotificationActionResultDto(marked, 0));
        }

        var normalized = NotificationStateWriter.Normalize(command.Keys);
        if (normalized.IsFailure)
        {
            return normalized.Error;
        }

        var affected = await NotificationStateWriter
            .ApplyAsync(userId, normalized.Value, state => state.MarkRead(now), _states, cancellationToken)
            .ConfigureAwait(false);

        if (affected > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        // Sanoq SAQLAGANDAN KEYIN qayta hisoblanadi: klient qo'ng'iroq
        // ustidagi nuqtani darhol yangilaydi va buning uchun ikkinchi
        // so'rov yuborishga majbur bo'lmaydi.
        var after = await VisibleAsync(userId, cancellationToken).ConfigureAwait(false);

        return Result.Success(new NotificationActionResultDto(
            affected,
            after.Count(item => !item.IsRead)));
    }

    private async Task<IReadOnlyList<NotificationDto>> VisibleAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var computed = await _collector.CollectAsync(cancellationToken).ConfigureAwait(false);

        var cutoff = await _collector.ReadRetentionCutoffAsync(cancellationToken).ConfigureAwait(false);

        return await ListNotificationsQueryHandler
            .ApplyUserStateAsync(computed, userId, _states, cutoff, cancellationToken)
            .ConfigureAwait(false);
    }
}

public sealed class DeleteNotificationsCommandHandler
{
    private readonly NotificationCollector _collector;
    private readonly INotificationStateRepository _states;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteNotificationsCommandHandler(
        NotificationCollector collector,
        INotificationStateRepository states,
        ICurrentUser user,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _collector = collector;
        _states = states;
        _user = user;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<NotificationActionResultDto>> HandleAsync(
        DeleteNotificationsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (_user.UserId is not { } userId)
        {
            return NotificationErrors.NotAuthenticated;
        }

        var normalized = NotificationStateWriter.Normalize(command.Keys);
        if (normalized.IsFailure)
        {
            return normalized.Error;
        }

        var now = _clock.UtcNow;

        // O'chirish «o'qildi» ni ham qo'yadi. Nega: o'chirilgan yozuv
        // ro'yxatdan chiqib ketadi va uni boshqa hech qachon o'qib
        // bo'lmaydi — «o'qilmagan» bo'lib qolgan yozuv esa sanoqda
        // ko'rinmasa ham, bazada abadiy shunday yotardi va kelajakda
        // biror hisobot uni noto'g'ri sanardi.
        var affected = await NotificationStateWriter
            .ApplyAsync(
                userId,
                normalized.Value,
                state =>
                {
                    state.MarkRead(now);
                    state.Dismiss(now);
                },
                _states,
                cancellationToken)
            .ConfigureAwait(false);

        if (affected > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var computed = await _collector.CollectAsync(cancellationToken).ConfigureAwait(false);

        var cutoff = await _collector.ReadRetentionCutoffAsync(cancellationToken).ConfigureAwait(false);
        var after = await ListNotificationsQueryHandler
            .ApplyUserStateAsync(computed, userId, _states, cutoff, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new NotificationActionResultDto(
            affected,
            after.Count(item => !item.IsRead)));
    }
}
