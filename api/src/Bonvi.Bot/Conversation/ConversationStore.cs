using System.Collections.Concurrent;

namespace Bonvi.Bot.Conversation;

/// <summary>Suhbat qaysi qadamda.</summary>
public enum ConversationStep
{
    /// <summary>Til tanlanmoqda.</summary>
    Language = 0,

    /// <summary>Vakansiya tanlanmoqda.</summary>
    Vacancy = 1,

    /// <summary>F.I.Sh. kutilmoqda.</summary>
    Name = 2,

    /// <summary>Telefon raqami kutilmoqda.</summary>
    Phone = 3,

    /// <summary>Yashash manzili kutilmoqda.</summary>
    Address = 4,
}

/// <summary>
/// Bitta nomzod bilan suhbatning holati.
/// </summary>
/// <remarks>
/// Ariza to'rt maydondan iborat, shu sababli holat ham kichik. Tugallangan
/// ariza bu yerda SAQLANMAYDI — u API ga yuboriladi va suhbat unutiladi:
/// botning xotirasi HR ma'lumotining ikkinchi nusxasi bo'lib qolmasligi
/// kerak.
/// </remarks>
public sealed record ConversationState
{
    public ConversationStep Step { get; init; } = ConversationStep.Language;

    public BotLanguage Language { get; init; } = BotLanguage.Cyrillic;

    public Guid VacancyId { get; init; }

    public string VacancyTitle { get; init; } = string.Empty;

    public PersonName? Name { get; init; }

    /// <summary>
    /// Tugma orqali kelgan telefon raqami.
    /// </summary>
    /// <remarks>
    /// Raqam manzil so'ralishidan OLDIN keladi, ariza esa manzildan
    /// KEYIN yuboriladi — ya'ni u bir qadam kutib turishi kerak.
    /// Kontakt tugmasi ikkinchi marta bosilmaydi, shu sababli raqamni
    /// qayta so'rash imkoni yo'q.
    /// </remarks>
    public string? Phone { get; init; }

    /// <summary>Oxirgi harakat vaqti — eskirgan suhbatni tozalash uchun.</summary>
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Suhbat holatlarini XOTIRADA saqlaydi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega baza yoki Redis emas.</b> Holatning umri — bir necha daqiqa va
/// undagi yagona qimmatli narsa yarim yozilgan ariza. Bot qayta ishga
/// tushsa, nomzod <c>/start</c> bilan boshidan boshlaydi — bu uch qadamli
/// anketa uchun arzimas narxda. Buning evaziga bot hech qanday saqlash
/// tizimiga bog'lanmaydi va uning ishlashi HR bazasiga umuman ta'sir
/// qilmaydi.
/// </para>
/// <para>
/// <b>Eskirgan suhbatlar tozalanadi.</b> Nomzod ismini yozmasdan ketib
/// qolsa, uning yarim ma'lumoti xotirada abadiy turmasligi kerak —
/// bu ham xotira, ham maxfiylik masalasi.
/// </para>
/// </remarks>
public sealed class ConversationStore
{
    private readonly ConcurrentDictionary<long, ConversationState> _states = new();
    private readonly TimeSpan _lifetime;

    public ConversationStore(TimeSpan lifetime) => _lifetime = lifetime;

    /// <summary>Suhbat holati — muddati o'tgan bo'lsa <c>null</c>.</summary>
    public ConversationState? Find(long chatId)
    {
        if (!_states.TryGetValue(chatId, out var state))
        {
            return null;
        }

        if (DateTimeOffset.UtcNow - state.UpdatedAt <= _lifetime)
        {
            return state;
        }

        _ = _states.TryRemove(chatId, out _);
        return null;
    }

    /// <summary>Holatni yozadi va vaqt belgisini yangilaydi.</summary>
    public void Save(long chatId, ConversationState state)
        => _states[chatId] = state with { UpdatedAt = DateTimeOffset.UtcNow };

    /// <summary>Suhbatni unutadi — ariza yuborilgach chaqiriladi.</summary>
    public void Forget(long chatId) => _states.TryRemove(chatId, out _);

    /// <summary>
    /// Muddati o'tgan suhbatlarni tozalaydi.
    /// </summary>
    /// <remarks>
    /// <see cref="Find"/> ham tozalaydi, lekin faqat SO'RALGAN suhbatni:
    /// qaytib kelmagan nomzodning holati hech qachon so'ralmaydi va
    /// o'z-o'zidan ketmasdi. Shu sababli davriy tozalash ham kerak.
    /// </remarks>
    /// <returns>Nechta suhbat unutildi.</returns>
    public int RemoveExpired()
    {
        var deadline = DateTimeOffset.UtcNow - _lifetime;
        var removed = 0;

        foreach (var (chatId, state) in _states)
        {
            if (state.UpdatedAt < deadline && _states.TryRemove(chatId, out _))
            {
                removed++;
            }
        }

        return removed;
    }
}
