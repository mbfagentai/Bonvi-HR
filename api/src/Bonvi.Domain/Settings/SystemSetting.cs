using Bonvi.Domain.Common;

namespace Bonvi.Domain.Settings;

/// <summary>
/// Bitta tizim sozlamasi: kalit va uning qiymati.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega har bir sozlama uchun ustun emas, kalit-qiymat.</b> Ustunli
/// jadval («settings» degan bitta qatorli jadval, har sozlamaga bitta
/// ustun) o'qishni tiplashgan qilardi, lekin har bir yangi sozlama
/// MIGRATSIYA talab qilardi. Bu yerda esa yangi sozlama —
/// <see cref="SystemSettingKeys"/> ga bitta qator; bazaga tegilmaydi.
/// Tiplilik yo'qolmadi: u ta'riflar ro'yxatida (tur, chegara, sukut
/// qiymat) va o'qish shu ro'yxat orqali ketadi.
/// </para>
/// <para>
/// <b>Bu yozuv SIR EMAS.</b> Shifrlash yo'q va kerak ham emas: bu yerda
/// bot matni va bildirishnoma chegaralari turadi. Sirlar (token, parol,
/// JWT kaliti) bu jadvalga TUSHMAYDI — ular muhit o'zgaruvchilaridan
/// keladi va git tarixiga hech qachon yaqinlashmaydi
/// (<c>BotOptions</c> izohi). Agar kelajakda sir saqlash kerak bo'lsa,
/// u ALOHIDA mexanizm bo'ladi, bu jadvalning kengaytmasi emas.
/// </para>
/// <para>
/// <b>Yozuv YO'Q bo'lishi — normal holat.</b> Sozlamalar ekrani hech
/// qachon ochilmagan tizimda jadval butunlay bo'sh bo'ladi va hamma
/// narsa sukut qiymatlar bilan ishlaydi. Shu sababli urug'lantirish
/// (seed) ham yo'q: qator faqat foydalanuvchi ATAYLAB qiymat yozganda
/// paydo bo'ladi va aynan shu «yozilgan qiymat» ni anglatadi.
/// </para>
/// <para>
/// <b>Nega <c>ISoftDeletable</c> emas.</b> Sozlama — tarixiy hujjat
/// emas, joriy holat. O'chirilgan sozlama «sukut qiymatga qaytarish»
/// degani va bunda yozuvni saqlab qo'yishning ma'nosi yo'q. Kim qachon
/// o'zgartirgani esa <see cref="IAuditable"/> orqali baribir qoladi.
/// </para>
/// </remarks>
public sealed class SystemSetting : AggregateRoot, IAuditable
{
    /// <summary>Kalitning eng katta uzunligi — bazadagi ustun uchun.</summary>
    /// <remarks>
    /// Haqiqiy kalitlar 40 belgidan qisqa (<c>notifications.probationWarningDays</c>
    /// — 36). 100 zaxira bilan olingan: ustun kengligi hech narsaga
    /// turmaydi, lekin cheklovsiz <c>text</c> ustunida unikal indeks
    /// PostgreSQL'da kalit uzunligi chegarasiga urilib qolishi mumkin.
    /// </remarks>
    public const int MaxKeyLength = 100;

    private SystemSetting(Guid id, string key, string value)
        : base(id)
    {
        Key = key;
        Value = value;
    }

    /// <summary>EF Core uchun.</summary>
    private SystemSetting()
    {
        Key = string.Empty;
        Value = string.Empty;
    }

    /// <summary>
    /// Sozlamaning barqaror kaliti — <see cref="SystemSettingKeys"/> dagi qiymat.
    /// </summary>
    /// <remarks>
    /// Bazada unikal: bitta sozlamada ikkita qator bo'lsa, qaysi biri
    /// «haqiqiy» ekani tasodifga qolardi (so'rov tartibiga). O'zgarmaydi
    /// ham — kalitni almashtirish boshqa sozlamaga aylanish demak.
    /// </remarks>
    public string Key { get; private set; }

    /// <summary>
    /// Qiymat — HAR DOIM matn, hatto son bo'lsa ham.
    /// </summary>
    /// <remarks>
    /// Turni <see cref="SystemSettingKeys"/> biladi; bu yerda esa faqat
    /// tekshirilgan va tozalangan satr yotadi (sabab
    /// <see cref="SystemSettingKind"/> izohida).
    /// </remarks>
    public string Value { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    /// <summary>
    /// Yangi sozlama yozuvini ochadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Kalit ikki bosqichda tekshiriladi: avval BO'SH emasligi, keyin
    /// ro'yxatda BORLIGI. Ikkita alohida xato ataylab — «kalit
    /// yuborilmadi» va «bunday sozlama yo'q» butunlay boshqa vaziyatlar
    /// va ularni frontend ham boshqacha ko'rsatadi.
    /// </para>
    /// <para>
    /// Qiymat esa ta'rifning O'ZI tomonidan tekshiriladi
    /// (<see cref="SystemSettingDefinition.Normalize"/>): chegara, tur va
    /// uzunlik — hammasi bitta joyda.
    /// </para>
    /// </remarks>
    public static Result<SystemSetting> Create(string? key, string? value)
    {
        var trimmed = key?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return SystemSettingErrors.EmptyKey;
        }

        if (SystemSettingKeys.Find(trimmed) is not { } definition)
        {
            return SystemSettingErrors.UnknownKey;
        }

        var normalized = definition.Normalize(value);
        if (normalized.IsFailure)
        {
            return normalized.Error;
        }

        return new SystemSetting(NewId(), definition.Key, normalized.Value);
    }

    /// <summary>
    /// Qiymatni almashtiradi.
    /// </summary>
    /// <remarks>
    /// Kalit bu yerda QAYTA tekshirilmaydi: yozuv allaqachon mavjud,
    /// demak u <see cref="Create"/> dan o'tgan. Ta'rif esa baribir
    /// izlanadi — kalit ro'yxatdan OLIB TASHLANGAN bo'lishi mumkin
    /// (sozlama bekor qilindi, lekin bazadagi qator qoldi). Bunday
    /// yozuvni yangilashga ruxsat berish ma'nosiz bo'lardi: uni hech kim
    /// o'qimaydi.
    /// </remarks>
    public Result SetValue(string? value)
    {
        if (SystemSettingKeys.Find(Key) is not { } definition)
        {
            return SystemSettingErrors.UnknownKey;
        }

        var normalized = definition.Normalize(value);
        if (normalized.IsFailure)
        {
            return normalized.Error;
        }

        Value = normalized.Value;
        return Result.Success();
    }
}
