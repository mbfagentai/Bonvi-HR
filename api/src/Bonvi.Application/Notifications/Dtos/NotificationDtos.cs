using Bonvi.Domain.Notifications;

namespace Bonvi.Application.Notifications.Dtos;

/// <summary>
/// Bildirishnoma qaysi ekranga olib boradi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega tayyor YO'L SATRI («/xodimlar/123») emas.</b> Marshrutlar
/// frontendda yashaydi va o'zbekcha yo'llar bilan yozilgan
/// (<c>/xodimlar</c>, <c>/sinov</c>, <c>/nomzodlar</c>). Server ularni
/// satr bo'lib yasasa, frontend marshrutini o'zgartirgan odam backend'da
/// ham qidirishi kerak bo'lardi — va unutgan kuni bildirishnoma
/// «404» ga olib borardi, hech qanday xatosiz. Sanoq esa shartnomaning
/// bir qismi: yo'l o'zgarsa, faqat frontenddagi bitta jadval o'zgaradi.
/// </para>
/// <para>
/// Bu — matnlar bilan bir xil qoida (<see cref="NotificationType"/>
/// izohi): server FAKTNI beradi, ko'rinishni frontend hal qiladi.
/// </para>
/// </remarks>
public enum NotificationRoute
{
    /// <summary>Xodim kartasi. <c>Id</c> — xodim.</summary>
    Employee = 1,

    /// <summary>Sinov muddatlari ro'yxati. <c>Id</c> — ajratib ko'rsatiladigan SINOV yozuvi.</summary>
    Probations = 2,

    /// <summary>
    /// «Sinov yozuvisiz qolganlar» ro'yxati. <c>Id</c> — MEHNAT MUNOSABATI.
    /// </summary>
    /// <remarks>
    /// <see cref="Probations"/> dan alohida: bu ro'yxat boshqa yozuvlarni
    /// (munosabatlarni, sinovlarni emas) ko'rsatadi va sahifada alohida
    /// ko'rinishda turadi. Bitta qiymat qilib qo'yilsa, frontend
    /// <c>Id</c> ning nimaga tegishli ekanini turdan taxmin qilishga
    /// majbur bo'lardi.
    /// </remarks>
    MissingProbations = 3,

    /// <summary>Nomzod kartasi. <c>Id</c> — nomzod.</summary>
    Candidate = 4,
}

/// <summary>«Kirib ko'rish» havolasi — tiplangan manzil.</summary>
/// <param name="Route">Qaysi ekran.</param>
/// <param name="Id">
/// Ekrandagi yozuv identifikatori. Ma'nosi <paramref name="Route"/> ga
/// bog'liq va har bir qiymat izohida aniq yozilgan.
/// </param>
public sealed record NotificationLinkDto(NotificationRoute Route, Guid? Id);

/// <summary>
/// Bitta bildirishnoma.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tayyor MATN yo'q va bu ataylab.</b> Javobda tur va parametrlar
/// keladi, jumlani frontend yig'adi — sabab <see cref="NotificationType"/>
/// izohida (tizim to'rt tilli).
/// </para>
/// <para>
/// <b>Nega parametrlar lug'at (<c>Dictionary</c>), qat'iy maydonlar
/// emas.</b> Turlarning parametrlari bir-biriga o'xshamaydi:
/// tug'ilgan kunda yosh bor, nomzodda manba, sinovda qolgan
/// kunlar. Ularning hammasini bitta DTO'ga qo'shsak, har bir
/// bildirishnomada o'nlab <c>null</c> maydon yurardi va frontend
/// «bu turda qaysi maydon to'ldirilgan» ni baribir bilishi kerak
/// bo'lardi. Lug'at bilan shartnoma ochiq: tur → parametr nomlari, va
/// yangi tur qo'shilganda mavjud tiplar buzilmaydi.
/// </para>
/// <para>
/// Qiymatlar SATR: sana ISO ko'rinishida, son invariant formatda.
/// Ularni foydalanuvchi tiliga o'girish — frontendning ishi
/// (<c>Intl.DateTimeFormat</c>, <c>Intl.NumberFormat</c>).
/// </para>
/// </remarks>
/// <param name="Key">
/// Barqaror identifikator. «O'qildi» va «o'chirish» so'rovlarida AYNAN shu
/// qaytariladi — sababi <see cref="NotificationKey"/> izohida.
/// </param>
/// <param name="Type">Tur — matn shu bo'yicha tanlanadi.</param>
/// <param name="Severity">Muhimlik darajasi — rang va tartib shu bo'yicha.</param>
/// <param name="Date">
/// Bildirishnoma ISHORA QILAYOTGAN sana: sinov tugash sanasi, tug'ilgan
/// kun, ariza tushgan kun. Bu «xabar kelgan vaqt» EMAS — bunday vaqt umuman
/// yo'q, chunki bildirishnoma saqlanmaydi, hisoblanadi.
/// </param>
/// <param name="Params">Matnni yig'ish uchun qiymatlar. Kalitlari turga bog'liq.</param>
/// <param name="Link">Qaysi ekranga o'tish.</param>
/// <param name="IsRead">Shu foydalanuvchi o'qiganmi.</param>
/// <param name="ReadAt">Qachon o'qigan. <c>null</c> — o'qilmagan.</param>
public sealed record NotificationDto(
    string Key,
    NotificationType Type,
    NotificationSeverity Severity,
    DateOnly Date,
    IReadOnlyDictionary<string, string> Params,
    NotificationLinkDto Link,
    bool IsRead = false,
    DateTimeOffset? ReadAt = null);

/// <summary>
/// Bildirishnomalar ro'yxati va o'qilmaganlar soni.
/// </summary>
/// <remarks>
/// <b>Nega sanoq ham SHU javobda.</b> Qo'ng'iroq ustidagi nuqta va
/// ro'yxatning o'zi bitta ekranda yonma-yon turadi. Sanoq alohida
/// so'ralsa, ro'yxat ochilgan har safar ikkita so'rov ketardi va ular
/// bir-biriga mos kelmasligi mumkin edi (orada boshqa birov o'zgartirsa).
/// Alohida <c>/unread-count</c> endpoint'i ham bor, lekin u boshqa
/// vazifa uchun: ro'yxat OCHILMAGANDA ham nuqtani yangilab turish.
/// </remarks>
/// <param name="Items">Bildirishnomalar — muhimligi va sanasi bo'yicha tartiblangan.</param>
/// <param name="Total">Foydalanuvchi ko'ra oladigan (o'chirilmagan) bildirishnomalarning UMUMIY soni.</param>
/// <param name="UnreadCount">Shulardan o'qilmaganlari.</param>
public sealed record NotificationListDto(
    IReadOnlyList<NotificationDto> Items,
    int Total,
    int UnreadCount);

/// <summary>O'qilmagan bildirishnomalar soni — qo'ng'iroq ustidagi nuqta uchun.</summary>
/// <param name="UnreadCount">O'qilmaganlar soni. Nol bo'lsa — nuqta chizilmaydi.</param>
public sealed record NotificationCountDto(int UnreadCount);

/// <summary>
/// «O'qildi» yoki «o'chirildi» amalining natijasi.
/// </summary>
/// <remarks>
/// <b>Nega javob bo'sh emas (204).</b> Frontend qo'ng'iroq ustidagi
/// nuqtani darhol yangilashi kerak. Sanoq javobda kelmasa, u har
/// amaldan keyin qo'shimcha so'rov yuborishga majbur bo'lardi — va shu
/// oraliqda ekranda eski son turardi.
/// </remarks>
/// <param name="Affected">Nechta bildirishnoma holati o'zgardi (yoki yangidan yozildi).</param>
/// <param name="UnreadCount">Amaldan KEYINGI o'qilmaganlar soni.</param>
public sealed record NotificationActionResultDto(int Affected, int UnreadCount);
