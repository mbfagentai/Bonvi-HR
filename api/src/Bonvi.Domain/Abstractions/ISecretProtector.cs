namespace Bonvi.Domain.Abstractions;

/// <summary>
/// Sirni bazaga yozishdan oldin shifrlaydi va o'qiganda ochadi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega bu umuman kerak bo'lib qoldi.</b> Ilgari tizim sozlamalari
/// jadvalida ATAYLAB bitta ham sir yo'q edi va <c>SystemSetting</c>
/// izohida shunday deb yozilgan ham edi. Foydalanuvchi Telegram tokenini
/// ekrandan qo'yishni talab qildi — bu uning qarori va u asosli: token
/// almashganda («BotFather» dan yangisi olinganda) hozirgi tartibda
/// serverga kirib, <c>.env</c> ni tahrirlab, konteynerni qayta ishga
/// tushirish kerak edi. Endi buning hammasi bitta maydonga aylandi.
/// </para>
/// <para>
/// <b>Nega shifrlash, hash emas.</b> Hash bir tomonlama, token esa
/// Telegram'ga YUBORILISHI kerak — ya'ni uni qaytarib o'qish shart.
/// Parol bilan farqi shunda: parolni tekshirish kifoya, tokenni
/// ishlatish kerak.
/// </para>
/// <para>
/// <b>Bu nimadan himoya qiladi va nimadan yo'q.</b> Kalit baribir
/// muhitda (<c>.env</c>) qoladi, ya'ni serverga to'liq kirgan odamdan
/// himoya YO'Q — bunday himoyani hech qanday sxema bermaydi. Himoya
/// aniq bir narsadan: BAZANING NUSXASIDAN. Zaxira nusxa har kecha
/// olinadi (<c>BACKUP_CRON_SCHEDULE</c>), u fayl bo'lib yotadi,
/// ko'chiriladi va tahlil uchun boshqa mashinaga tushirilishi mumkin.
/// Shifrlanmagan token bunday nusxaning har biriga ochiq matn bo'lib
/// tushardi.
/// </para>
/// <para>
/// <b>Nega Domain qatlamida.</b> Sozlamaning SIR ekani — uning
/// ta'rifidagi xossa (<c>SystemSettingKind.Secret</c>), ya'ni domen
/// qoidasi. Agregat qiymatni yozayotganda uni himoyalangan holga
/// keltirishi kerak; buni Application qatlamiga chiqarish «agregatni
/// chetlab o'tib yozish» yo'lini ochib qo'yardi.
/// </para>
/// </remarks>
public interface ISecretProtector
{
    /// <summary>Ochiq matnni saqlashga yaroqli holga keltiradi.</summary>
    /// <remarks>
    /// Natija HAR SAFAR boshqacha bo'ladi (tasodifiy nonce), hatto bir
    /// xil matn uchun ham. Shu sababli shifrlangan qiymatlarni
    /// solishtirib «o'zgardimi?» deb bo'lmaydi — o'zgarish ochiq matn
    /// darajasida aniqlanadi.
    /// </remarks>
    string Protect(string plainText);

    /// <summary>
    /// Saqlangan qiymatni ochadi. Ocha olmasa — <c>null</c>.
    /// </summary>
    /// <remarks>
    /// <b>Nega istisno emas.</b> <c>null</c> real va kutilgan holat:
    /// kalit almashtirilgan (masalan <c>JWT__SIGNINGKEY</c> yangilangan)
    /// yoki baza boshqa muhitdan ko'chirilgan. Bunda tizim yiqilmasligi
    /// kerak — u sozlamani «kiritilmagan» deb ko'rsatadi va super
    /// administrator tokenni qayta qo'yadi.
    /// </remarks>
    string? Unprotect(string protectedText);
}
