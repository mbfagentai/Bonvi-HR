using Bonvi.Domain.Employees;

namespace Bonvi.Domain.Notifications;

/// <summary>
/// Bildirishnomalar qachon paydo bo'lishini belgilaydigan chegaralar.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega konstanta, sozlama emas.</b> Bu qiymatlarni interfeysda
/// o'zgartirish imkoniyati hech kimdan so'ralmagan, ular esa
/// bildirishnoma MATNIGA ham tushadi («7 kun qoldi»). Sozlamaga
/// chiqarilganda tarjima matni bilan haqiqiy chegara ajralib ketishi
/// mumkin edi. Kerak bo'lganda o'zgartirish — bitta qator.
/// </para>
/// <para>
/// <b>Nega hammasi bitta joyda.</b> Chegaralar ikki tomondan
/// ishlatiladi: o'qish modeli SQL'da shu chegarani qo'yadi, yig'uvchi esa
/// shu chegara bo'yicha turni tanlaydi. Ular ajralib qolsa, bazadan
/// kelgan qator hech qanday turga tushmay, jimgina yo'qolardi.
/// </para>
/// </remarks>
public static class NotificationRules
{
    /// <summary>
    /// Sinov muddati tugashiga necha kun qolganda ogohlantiriladi.
    /// </summary>
    /// <remarks>
    /// <see cref="Probation.WarningDays"/> ning O'ZI — bu yerda yangi son
    /// yozilmaydi. HR aynan «7 kun» ni so'ragan va u allaqachon
    /// <c>/sinov</c> sahifasidagi «diqqat» belgisini boshqaradi. Ikkinchi
    /// nusxa bo'lsa, ro'yxatda qizarib turgan, lekin bildirishnomasi
    /// kelmagan (yoki aksincha) xodim paydo bo'lardi.
    /// </remarks>
    public const int ProbationWarningDays = Probation.WarningDays;

    /// <summary>
    /// Nomzod necha kun davomida «yangi» hisoblanadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// «Yangi nomzod» — HODISA emas, HOLAT: fon vazifasi yo'q, demak
    /// «qo'shilgan LAHZA» ni ushlab qoladigan joy ham yo'q. Uning o'rniga
    /// shart hisoblanadi: «yaqinda qo'shilgan va hamon voronkada».
    /// </para>
    /// <para>
    /// Nega aynan 7 kun: HR bo'limida uch kishi ishlaydi va ular har kuni
    /// tizimga kirmaydi (dam olish kunlari, ta'til). Uch kun qilib
    /// qo'yilsa, dushanba kuni kelgan odam juma kuni tushgan anketani
    /// umuman ko'rmasdi.
    /// </para>
    /// </remarks>
    public const int NewCandidateDays = 7;

    /// <summary>Ish taklifi javob muddati tugashiga necha kun qolganda ogohlantiriladi.</summary>
    /// <remarks>
    /// Sinovnikidan (7) qisqaroq, chunki taklif muddati odatda bir hafta
    /// atrofida bo'ladi — 7 kunlik ogohlantirish taklif yuborilgan kuniyoq
    /// ishga tushib, ma'nosini yo'qotardi.
    /// </remarks>
    public const int OfferWarningDays = 3;

    /// <summary>Bitta so'rovda nechta kalit yuborilishi mumkin.</summary>
    /// <remarks>
    /// «Hammasini o'qildi» amali server tomonda bajariladi va kalit
    /// ro'yxatini talab qilmaydi, ya'ni bu chegara oddiy ishda hech qachon
    /// urilmaydi. U faqat so'rov tanasi cheksiz o'smasligi uchun bor.
    /// </remarks>
    public const int MaxKeysPerRequest = 200;
}
