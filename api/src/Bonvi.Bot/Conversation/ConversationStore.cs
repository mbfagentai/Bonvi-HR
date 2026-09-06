using System.Collections.Concurrent;

namespace Bonvi.Bot.Conversation;

/// <summary>
/// Suhbat qaysi qadamda.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>TARTIB — anketaning o'zi</b> (SPEC-anketa, 4-bo'lim). Qadamlar
/// aynan shu ketma-ketlikda, bittalab o'tiladi
/// (<c>BotWorker.NextStep</c>): raqamlar orasidagi bo'shliq yo'q va
/// bo'lishi ham kerak emas — yangi savol qo'shilganda u shu ro'yxatning
/// kerakli joyiga qo'yiladi.
/// </para>
/// <para>
/// <b>Uchta qadam SHARTLI</b> va ular shu yerda emas, chizish paytida
/// tekshiriladi: vakansiya (ochiq ish o'rni bo'lmasa), guvohnoma
/// toifasi (guvohnoma yo'q bo'lsa) va ommaviy oferta (havola
/// sozlanmagan bo'lsa).
/// </para>
/// <para>
/// <b>Qiymatlar XOTIRADA ishlatiladi</b>, hech qayerda saqlanmaydi
/// (<see cref="ConversationStore"/> izohi) — ya'ni raqamlarni
/// o'zgartirish hech qanday ma'lumotni buzmaydi.
/// </para>
/// </remarks>
public enum ConversationStep
{
    /// <summary>1-qadam: til tanlanmoqda.</summary>
    Language = 0,

    /// <summary>2-qadam: vakansiya tanlanmoqda.</summary>
    Vacancy = 1,

    /// <summary>3-qadam: F.I.Sh. kutilmoqda.</summary>
    Name = 2,

    /// <summary>4-qadam: tug'ilgan sana kutilmoqda.</summary>
    BirthDate = 3,

    /// <summary>5-qadam: yashash manzili kutilmoqda.</summary>
    Address = 4,

    /// <summary>6-qadam: telefon raqami kutilmoqda (kontakt tugmasi).</summary>
    Phone = 5,

    /// <summary>7-qadam: ma'lumot darajasi.</summary>
    Education = 6,

    /// <summary>8-qadam: mutaxassisligi.</summary>
    Speciality = 7,

    /// <summary>9-qadam: ish tajribasi.</summary>
    WorkExperience = 8,

    /// <summary>10-qadam: oilaviy holati.</summary>
    MaritalStatus = 9,

    /// <summary>11-qadam: rus tili darajasi.</summary>
    RussianLevel = 10,

    /// <summary>12-qadam: boshqa tillar.</summary>
    OtherLanguages = 11,

    /// <summary>13-qadam: Office dasturlari.</summary>
    OfficeSkills = 12,

    /// <summary>14-qadam: oxirgi maosh.</summary>
    LastSalary = 13,

    /// <summary>15-qadam: o'zi haqida qo'shimcha.</summary>
    ExtraNote = 14,

    /// <summary>16-qadam: haydovchilik guvohnomasi bormi.</summary>
    DriverLicense = 15,

    /// <summary>17-qadam: guvohnoma toifasi — faqat guvohnoma bo'lsa.</summary>
    DriverLicenseCategory = 16,

    /// <summary>18-qadam: surat.</summary>
    Photo = 17,

    /// <summary>19-qadam: kutilayotgan maosh.</summary>
    ExpectedSalary = 18,

    /// <summary>20-qadam: ish haqida qayerdan bilgan.</summary>
    ReferralSource = 19,

    /// <summary>21-qadam: ommaviy oferta — faqat havola sozlangan bo'lsa.</summary>
    Offer = 20,

    /// <summary>22-qadam: to'ldirilgan anketa va «Yuborish».</summary>
    Confirm = 21,
}

/// <summary>
/// Bitta nomzod bilan suhbatning holati — anketaning to'ldirilgan qismi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nomzod javob bermagan maydon <c>null</c> bo'lib qoladi</b> va
/// yakuniy xulosada UMUMAN chizilmaydi. Eski botda o'tkazib yuborilgan
/// savol xulosaga «Ўтказиб юбориш» degan javob bo'lib yozilib qolgan —
/// bu xato takrorlanmaydi (<see cref="AnswerSummary"/>).
/// </para>
/// <para>
/// Tugallangan ariza bu yerda SAQLANMAYDI: u API ga yuboriladi va
/// suhbat unutiladi — botning xotirasi HR ma'lumotining ikkinchi
/// nusxasi bo'lib qolmasligi kerak.
/// </para>
/// </remarks>
public sealed record ConversationState
{
    public ConversationStep Step { get; init; } = ConversationStep.Language;

    public BotLanguage Language { get; init; } = BotLanguage.Cyrillic;

    /// <summary>
    /// Tanlangan vakansiya — ochiq ish o'rni bo'lmasa <c>null</c>.
    /// </summary>
    /// <remarks>
    /// <c>null</c> XATO EMAS, ataylab qo'yilgan holat: bunda ariza
    /// vakansiyasiz yuboriladi va server uni o'zi «Umumiy anketa»ga
    /// qo'yadi (SPEC-anketa, 3-bo'lim). Botning o'zi hech qanday
    /// zaxira qiymat o'ylab topmaydi — «Umumiy anketa» qaysi vakansiya
    /// ekanini faqat server biladi.
    /// </remarks>
    public Guid? VacancyId { get; init; }

    public string VacancyTitle { get; init; } = string.Empty;

    public PersonName? Name { get; init; }

    /// <summary>Tug'ilgan sana — siljitilmaydi (<see cref="BotClock"/> izohi).</summary>
    public DateOnly? BirthDate { get; init; }

    /// <summary>Yashash manzili.</summary>
    public string? Address { get; init; }

    /// <summary>
    /// Tugma orqali kelgan telefon raqami.
    /// </summary>
    /// <remarks>
    /// Raqam anketaning o'rtasida keladi, ariza esa oxirida yuboriladi —
    /// ya'ni u o'n oltita qadam kutib turishi kerak. Kontakt tugmasi
    /// ikkinchi marta bosilmaydi, shu sababli raqamni qayta so'rash
    /// imkoni yo'q.
    /// </remarks>
    public string? Phone { get; init; }

    /// <summary>Ma'lumot darajasi.</summary>
    public EducationAnswer? Education { get; init; }

    /// <summary>Mutaxassisligi — o'tkazib yuborilsa <c>null</c>.</summary>
    public string? Speciality { get; init; }

    /// <summary>Ish tajribasi — o'tkazib yuborilsa <c>null</c>.</summary>
    public string? WorkExperience { get; init; }

    /// <summary>Oilaviy holati.</summary>
    public MaritalAnswer? MaritalStatus { get; init; }

    /// <summary>Rus tili darajasi.</summary>
    public RussianLevelAnswer? RussianLevel { get; init; }

    /// <summary>Boshqa tillar — o'tkazib yuborilsa <c>null</c>.</summary>
    public string? OtherLanguages { get; init; }

    /// <summary>Office dasturlari — o'tkazib yuborilsa <c>null</c>.</summary>
    public string? OfficeSkills { get; init; }

    /// <summary>Oxirgi maosh — o'tkazib yuborilsa <c>null</c>.</summary>
    public string? LastSalary { get; init; }

    /// <summary>Qo'shimcha ma'lumot — o'tkazib yuborilsa <c>null</c>.</summary>
    public string? ExtraNote { get; init; }

    /// <summary>Haydovchilik guvohnomasi bormi.</summary>
    public bool? HasDriverLicense { get; init; }

    /// <summary>Guvohnoma toifasi — faqat guvohnoma bor bo'lsa.</summary>
    public string? DriverLicenseCategory { get; init; }

    /// <summary>
    /// Telegram'dagi suratning belgisi.
    /// </summary>
    /// <remarks>
    /// Bu yerda BELGI turadi, rasmning baytlari emas: baytlar ariza
    /// YARATILGANDAN keyin, nomzodning <c>id</c> si ma'lum bo'lganda
    /// yuklab olinadi. Yarim qolgan suhbat uchun bir necha yuz kilobayt
    /// xotirada ushlab turish esa ma'nosiz — nomzod anketani tashlab
    /// ketishi mumkin.
    /// </remarks>
    public string? PhotoFileId { get; init; }

    /// <summary>Kutilayotgan maosh oralig'i.</summary>
    public SalaryAnswer? ExpectedSalary { get; init; }

    /// <summary>Ish haqida qayerdan bilgan.</summary>
    public ReferralAnswer? ReferralSource { get; init; }

    /// <summary>
    /// Ommaviy oferta bilan rozilik VAQTI.
    /// </summary>
    /// <remarks>
    /// Bayroq emas, VAQT — bu huquqiy fakt («qachon rozilik berildi»)
    /// va server ham uni shunday saqlaydi. Qiymat Toshkent siljishi
    /// bilan yoziladi, server uni o'zi UTC ga keltiradi.
    /// <para>
    /// <c>null</c> — 21-qadam chizilmagan (oferta havolasi sozlanmagan),
    /// ya'ni rozilik SO'RALMAGAN. Bu bo'shliq serverga ham SHU HOLICHA
    /// boradi: zaxira vaqt qo'yilsa, hech kim ko'rmagan ofertaga rozilik
    /// berilgandek yozilardi.
    /// </para>
    /// </remarks>
    public DateTimeOffset? OfferAcceptedAt { get; init; }

    /// <summary>Oxirgi harakat vaqti — eskirgan suhbatni tozalash uchun.</summary>
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Suhbat holatlarini XOTIRADA saqlaydi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega baza yoki Redis emas.</b> Holatning umri — bir necha daqiqa va
/// undagi yagona qimmatli narsa yarim yozilgan ariza. Buning evaziga bot
/// hech qanday saqlash tizimiga bog'lanmaydi va uning ishlashi HR
/// bazasiga umuman ta'sir qilmaydi.
/// </para>
/// <para>
/// ⚠️ <b>Narxi anketa 22 qadamga o'sgach oshdi</b> va u ochiq tan
/// olingan (SPEC-anketa, 4.2): bot qayta ishga tushsa yarim to'ldirilgan
/// anketa yo'qoladi va nomzod boshidan boshlaydi. Qaror o'zgarmadi —
/// prod'ga yangi bog'liqlik qo'shish undan qimmatroq. Yumshatilishi:
/// suhbat muddati 60 daqiqa (anketa ~5 daqiqa) va deploy ish vaqtidan
/// tashqarida qilinadi.
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
