using Bonvi.Domain.Abstractions;
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
/// <b>Ba'zi yozuvlar SIR.</b> Ilgari bu jadvalda bitta ham sir yo'q edi
/// va shu izohda «kelajakda sir kerak bo'lsa, u ALOHIDA mexanizm
/// bo'ladi» deb yozilgandi. Telegram tokeni ekranga chiqarilgach
/// (foydalanuvchi qarori, sabab <see cref="SystemSettingKeys.BotToken"/>
/// izohida) shu va'da bajarildi: sir bu jadvalning oddiy qatori EMAS,
/// u <see cref="SystemSettingKind.Secret"/> turi bilan belgilanadi va
/// boshqacha muomala qiladi — <see cref="ISecretProtector"/> orqali
/// shifrlanib yoziladi, API javobida hech qachon qaytmaydi va o'chirish
/// deganda qatorning O'ZI olib tashlanadi.
/// </para>
/// <para>
/// <b>Nima baribir <c>.env</c> da qoldi:</b> baza paroli, JWT kaliti va
/// bot hisobining logini/paroli. Birinchi ikkitasi bazaga umuman
/// kirishdan OLDIN kerak, uchinchisi esa — bot aynan shu bilan tizimga
/// kirib tokenni oladi. Ularni bu jadvalga ko'chirish «kalitni
/// qulflangan xonaning ichida qoldirish» bo'lardi.
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
    /// <para>
    /// Turni <see cref="SystemSettingKeys"/> biladi; bu yerda esa faqat
    /// tekshirilgan va tozalangan satr yotadi (sabab
    /// <see cref="SystemSettingKind"/> izohida).
    /// </para>
    /// <para>
    /// <see cref="SystemSettingKind.Secret"/> turida bu maydonda SHIFRLANGAN
    /// matn yotadi (<c>v1.…</c>). Uni ochish faqat o'qish paytida va faqat
    /// <see cref="ISecretProtector"/> orqali bo'ladi — ya'ni agregatdan
    /// olingan qiymat hech qachon ochiq sir emas.
    /// </para>
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
    public static Result<SystemSetting> Create(string? key, string? value, ISecretProtector protector)
    {
        ArgumentNullException.ThrowIfNull(protector);

        var trimmed = key?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return SystemSettingErrors.EmptyKey;
        }

        if (SystemSettingKeys.Find(trimmed) is not { } definition)
        {
            return SystemSettingErrors.UnknownKey;
        }

        var stored = Prepare(definition, value, protector);
        if (stored.IsFailure)
        {
            return stored.Error;
        }

        return new SystemSetting(NewId(), definition.Key, stored.Value);
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
    public Result SetValue(string? value, ISecretProtector protector)
    {
        ArgumentNullException.ThrowIfNull(protector);

        if (SystemSettingKeys.Find(Key) is not { } definition)
        {
            return SystemSettingErrors.UnknownKey;
        }

        var stored = Prepare(definition, value, protector);
        if (stored.IsFailure)
        {
            return stored.Error;
        }

        Value = stored.Value;
        return Result.Success();
    }

    /// <summary>
    /// SERVER yozadigan sozlama uchun yangi yozuv ochadi.
    /// </summary>
    /// <remarks>
    /// <see cref="Create"/> ning juftligi: sabab
    /// <see cref="SetServerValue"/> izohida. Yozuv bo'sh qiymat bilan
    /// ochiladi va darhol to'ldiriladi — shunda uzunlik tekshiruvi
    /// bitta joyda qoladi.
    /// </remarks>
    /// <param name="key">Sozlama kaliti.</param>
    /// <param name="value">Serverdan kelgan qiymat.</param>
    public static Result<SystemSetting> CreateServerValue(string? key, string? value)
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

        var setting = new SystemSetting(NewId(), definition.Key, string.Empty);

        var applied = setting.SetServerValue(value);

        return applied.IsFailure ? applied.Error : Result.Success(setting);
    }

    /// <summary>
    /// SERVER yozadigan qiymat — rasm kaliti yoki ichki identifikator.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega alohida yo'l.</b> <see cref="SystemSettingKind.Photo"/> va
    /// <see cref="SystemSettingKind.Hidden"/> turidagi sozlamani
    /// <see cref="Create"/> ham, <see cref="SetValue"/> ham RAD ETADI
    /// (<see cref="SystemSettingDefinition.Normalize"/>) — bu ataylab:
    /// umumiy «sozlamalarni saqlash» so'rovi bilan fayl kalitini yoki
    /// vakansiya identifikatorini yozib qo'yish, ularni mavjud
    /// bo'lmagan faylga va mavjud bo'lmagan qatorga qaratib qo'yardi.
    /// </para>
    /// <para>
    /// Bu metod esa qiymatni ALLAQACHON hosil qilgan chaqiruvchi uchun:
    /// rasm kaliti saqlash natijasidan, «Umumiy anketa» vakansiyasining
    /// identifikatori esa urug'lantirishdan keladi. Shifrlash yo'q —
    /// ikkalasi ham sir emas.
    /// </para>
    /// <para>
    /// <b>Nega ikkala tur bitta metodda.</b> Ular uchun qoida AYNAN bir
    /// xil: qiymatni server beradi va tekshiriladigan yagona narsa —
    /// uzunlik. Ikkita nusxa yozilsa, biri o'zgarganda ikkinchisi
    /// eskirib qolardi.
    /// </para>
    /// </remarks>
    /// <param name="value">
    /// Serverdan kelgan qiymat. Bo'sh — rasm olib tashlandi (yashirin
    /// qiymatda esa «hali yozilmagan» degani).
    /// </param>
    public Result SetServerValue(string? value)
    {
        if (SystemSettingKeys.Find(Key) is not { } definition)
        {
            return SystemSettingErrors.UnknownKey;
        }

        if (definition.Kind is not (SystemSettingKind.Photo or SystemSettingKind.Hidden))
        {
            // Bu yo'l faqat SERVER yozadigan turlar uchun. Matn yoki
            // sonni bu yerdan yozishga ruxsat berish naqsh, chegara va
            // shifrlash tekshiruvlarini butunlay chetlab o'tish
            // imkonini berardi.
            return SystemSettingErrors.NotEditable;
        }

        var trimmed = value?.Trim() ?? string.Empty;

        if (definition.MaxLength is { } limit && trimmed.Length > limit)
        {
            return SystemSettingErrors.TextTooLong(limit);
        }

        Value = trimmed;
        return Result.Success();
    }

    /// <summary>
    /// Kelgan qiymatni tekshiradi va SAQLANADIGAN ko'rinishga keltiradi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ikki qadam va tartibi MUHIM: avval tekshiruv (ochiq matn ustida),
    /// keyin shifrlash. Teskarisi bo'lsa naqsh shifrlangan satrni
    /// tekshirib, har doim rad etardi.
    /// </para>
    /// <para>
    /// <b>Nega shifrlash aynan agregatda.</b> Uni chaqiruvchiga
    /// qoldirish «shifrlashni unutib yozish» yo'lini ochib qo'yardi va
    /// bu xato jimgina o'tardi: qator bazaga tushardi, ekran ishlardi,
    /// faqat token ochiq matn bo'lib yotardi. Bu yerda esa
    /// <see cref="Value"/> ga boshqa hech qanday yo'l yo'q.
    /// </para>
    /// <para>
    /// BO'SH sirni shifrlash ham ma'nosiz: bo'sh sir «yo'q» degani va
    /// bunday holatda yozuvning o'zi o'chiriladi (use-case shuni
    /// qiladi). Bu yerda u shunchaki bo'sh satr bo'lib qaytadi.
    /// </para>
    /// </remarks>
    private static Result<string> Prepare(
        SystemSettingDefinition definition,
        string? value,
        ISecretProtector protector)
    {
        var normalized = definition.Normalize(value);
        if (normalized.IsFailure)
        {
            return normalized.Error;
        }

        if (definition.Kind != SystemSettingKind.Secret || normalized.Value.Length == 0)
        {
            return normalized;
        }

        return Result.Success(protector.Protect(normalized.Value));
    }
}
