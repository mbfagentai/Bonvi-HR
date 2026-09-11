using Bonvi.Domain.Employees;

namespace Bonvi.Domain.Notifications;

/// <summary>
/// Bildirishnomalar qachon paydo bo'lishini belgilaydigan chegaralar —
/// SUKUT qiymatlar.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Bu qiymatlar endi TO'G'RIDAN-TO'G'RI ishlatilmaydi.</b> Yig'uvchi
/// (<c>NotificationCollector</c>) ularni tizim sozlamalaridan o'qiydi
/// (<c>SystemSettingKeys</c>), bu yerdagi konstantalar esa o'sha
/// sozlamalarning SUKUT qiymati bo'lib turadi. Ya'ni sozlamalar ekrani
/// hech qachon ochilmagan tizimda aynan shu sonlar ishlaydi.
/// </para>
/// <para>
/// <b>Nega konstanta baribir qoldi</b> (sozlama ta'rifiga ko'chirib
/// yuborilmadi). Sukut qiymat — bu «HR so'ragan qiymat» va uning
/// kelib chiqishi shu yerda izohlangan: <c>ProbationWarningDays</c>
/// <see cref="Probation.WarningDays"/> ning O'ZI bo'lishi shart, aks
/// holda ro'yxatda qizarib turgan, lekin bildirishnomasi kelmagan
/// xodim paydo bo'lardi. Sozlamalar ro'yxatiga ko'chirilsa, bu
/// bog'liqlik ko'zdan yo'qolardi.
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

    /// <summary>
    /// Suhbatgacha necha SOAT qolganda ogohlantiriladi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nega aynan 1 soat (mijoz talabi, 2026-09-10): xabar «bu odam
    /// hozir keladi» degan ma'noni berishi kerak — stolni bo'shatish,
    /// xonani tayyorlash, anketani ochib qo'yish uchun yetadigan vaqt.
    /// Ertalabki rejalashtirish uchun boshqa xabar bor
    /// (<see cref="NotificationType.CandidateContactDue"/>), ya'ni bu
    /// chegara uzoq bo'lishi shart emas.
    /// </para>
    /// <para>
    /// <b>Nega SOAT, kun emas.</b> Qolgan uchta chegara kunda va ular
    /// kalendar faktlariga tegishli («muddat falon kuni tugaydi»).
    /// Suhbat esa LAHZA: kun bilan o'lchansa, ertalab soat 9 da
    /// kelgan xabar kechqurun 18:00 dagi suhbat haqida bo'lardi va
    /// «hozir keladi» degan ma'no butunlay yo'qolardi.
    /// </para>
    /// <para>
    /// Sukut qiymat — sozlama hech qachon ochilmagan tizim uchun
    /// (<c>SystemSettingKeys.InterviewWarningHours</c>), qolganlari
    /// bilan bir xil qoida.
    /// </para>
    /// </remarks>
    public const int InterviewWarningHours = 1;

    /// <summary>
    /// O'qilgan bildirishnoma ro'yxatda necha kun qoladi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega kerak.</b> Bildirishnoma HISOBLANADI: sharti rost bo'lib
    /// turgan ekan («480 xodimda sinov yozuvi yo'q») u ro'yxatda YILLAB
    /// turaveradi — o'qilgan bo'lsa ham. HR har safar qo'lda belgilab
    /// o'chirishga majbur bo'lardi va bu ish hech qachon tugamasdi.
    /// </para>
    /// <para>
    /// <b>Nega 7 kun.</b> HR bo'limida uch kishi ishlaydi va ular har
    /// kuni tizimga kirmaydi (dam olish, ta'til) —
    /// <see cref="NewCandidateDays"/> dagi bilan AYNAN bir xil mulohaza.
    /// Bir hafta ichida hamma o'qigan xabarni ko'rib ulguradi.
    /// </para>
    /// <para>
    /// <b>Qanday ishlaydi.</b> Yozuv O'CHIRILMAYDI: bildirishnoma
    /// ro'yxatdan chiqariladi, holat yozuvi esa bazada qoladi. Farqi
    /// muhim — holat o'chirilsa, sharti hamon rost bildirishnoma
    /// ertaga YANA O'QILMAGAN bo'lib qaytardi.
    /// </para>
    /// </remarks>
    public const int ReadRetentionDays = 7;

    /// <summary>Bitta so'rovda nechta kalit yuborilishi mumkin.</summary>
    /// <remarks>
    /// «Hammasini o'qildi» amali server tomonda bajariladi va kalit
    /// ro'yxatini talab qilmaydi, ya'ni bu chegara oddiy ishda hech qachon
    /// urilmaydi. U faqat so'rov tanasi cheksiz o'smasligi uchun bor.
    /// </remarks>
    public const int MaxKeysPerRequest = 200;
}
