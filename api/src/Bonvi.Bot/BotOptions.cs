using System.ComponentModel.DataAnnotations;

namespace Bonvi.Bot;

/// <summary>
/// Telegram anketa botining sozlamalari.
/// </summary>
/// <remarks>
/// Hammasi MUHITDAN keladi (<c>.env</c> → docker-compose), fayl ichida
/// sukut qiymat yo'q: token va parol — sirlar, ularning kodda yoki
/// <c>appsettings.json</c> da turishi git tarixiga tushib qolishning eng
/// keng tarqalgan yo'li.
/// </remarks>
public sealed class BotOptions
{
    public const string SectionName = "Bot";

    /// <summary>
    /// @BotFather bergan token — ZAXIRA qiymat.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Bu endi ASOSIY manba emas.</b> Token tizim sozlamalarida
    /// saqlanadi va super administrator uni ekrandan qo'yadi (sabab
    /// <c>SystemSettingKeys.BotToken</c> izohida). Bot avval o'sha
    /// qiymatni so'raydi va faqat u BO'SH bo'lsa shu maydonga tushadi.
    /// </para>
    /// <para>
    /// <b>Nega maydon umuman qoldi.</b> Ikki sabab. Birinchisi —
    /// mavjud o'rnatmalar: ular bugun shu qiymat bilan ishlab turibdi va
    /// yangilanish ularni to'xtatib qo'ymasligi kerak. Ikkinchisi —
    /// yangi o'rnatish: botni ekranga umuman kirmasdan yoqib qo'yish
    /// mumkin bo'lib qoladi.
    /// </para>
    /// <para>
    /// Bo'sh bo'lsa (va ekranda ham qo'yilmagan bo'lsa) bot ishlamaydi,
    /// lekin jarayon YIQILMAYDI va TO'XTAMAYDI ham: u sababni bir marta
    /// yozib, tokenni kutib turadi va paydo bo'lishi bilan o'zi ulanadi
    /// (<c>BotWorker.EnsureTokenAsync</c>). Ilgari bu yerda «to'xtaydi»
    /// deb yozilgandi — endi to'xtash uchun sabab yo'q, chunki token
    /// har lahzada ekranda paydo bo'lishi mumkin.
    /// </para>
    /// <para>
    /// Shu sababli bu yerda <c>[Required]</c> YO'Q: u ishga tushishdayoq
    /// istisno tashlardi.
    /// </para>
    /// </remarks>
    public string Token { get; set; } = string.Empty;

    /// <summary>Bonvi API ning ichki manzili (compose tarmog'ida).</summary>
    [Required(AllowEmptyStrings = false)]
    public string ApiBaseUrl { get; set; } = "http://api:8080";

    /// <summary>Bot xizmat hisobining logini — telefon raqami.</summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>Bu MAJBURIY va u tizim sozlamalariga hech qachon
    /// ko'chirilmaydi.</b> Bot aynan shu login va parol bilan Bonvi API
    /// ga kirib, Telegram tokenini oladi — ya'ni uni sozlamalar jadvaliga
    /// qo'yish «kalitni qulflangan xonaning ichida qoldirish» bo'lardi.
    /// </para>
    /// <para>
    /// Bo'sh bo'lsa bot ishga tushmaydi va boshqacha bo'lishi ham
    /// mumkin emas: usiz u tokenni bilib ololmaydi.
    /// </para>
    /// </remarks>
    public string Login { get; set; } = string.Empty;

    /// <summary>Bot xizmat hisobining paroli.</summary>
    /// <remarks>Majburiy — sabab <see cref="Login"/> izohida.</remarks>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Telegram'dan yangiliklarni kutish muddati (soniya).
    /// </summary>
    /// <remarks>
    /// <b>Long polling</b>: so'rov shu muddatgacha ochiq turadi va yangilik
    /// kelishi bilan javob qaytadi. Webhook ATAYLAB ishlatilmaydi — u
    /// tashqaridan kiradigan ochiq manzilni talab qiladi, ya'ni yangi port,
    /// yangi sertifikat va yangi hujum yuzasi. Bot esa faqat CHIQUVCHI
    /// ulanish bilan ishlay oladi.
    /// </remarks>
    [Range(1, 60)]
    public int PollTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Yarim qolgan suhbat qancha vaqtdan keyin unutiladi (daqiqa).
    /// </summary>
    /// <remarks>
    /// Nomzod ismini yozmasdan ketib qolsa, uning holati abadiy xotirada
    /// turmasligi kerak. Muddat o'tgach suhbat boshidan boshlanadi.
    /// </remarks>
    [Range(1, 1440)]
    public int ConversationTimeoutMinutes { get; set; } = 60;
}
