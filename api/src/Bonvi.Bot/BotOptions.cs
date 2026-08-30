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
    /// @BotFather bergan token.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bo'sh bo'lsa bot ishlamaydi, lekin jarayon YIQILMAYDI: u
    /// tushunarli xabar yozib, kutib turadi
    /// (<c>BotWorker.ExecuteAsync</c>). Istisno tashlash konteynerni
    /// cheksiz qayta ishga tushish halqasiga solib qo'yardi va haqiqiy
    /// sabab minglab satr orasida yo'qolardi.
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
    /// <remarks>Bo'sh bo'lsa bot ishlamaydi (<see cref="Token"/> izohi).</remarks>
    public string Login { get; set; } = string.Empty;

    /// <summary>Bot xizmat hisobining paroli.</summary>
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
    /// «Kompaniya haqida» tugmasi ostidagi matn — kirill alifbosida.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega sozlamada, kodda emas.</b> Bu MARKETING matni: u
    /// o'zgaradi, uni HR yozadi va u dasturchini kutmasligi kerak.
    /// Tizimda esa bunday matnni saqlaydigan joy yo'q —
    /// «yuridik shaxslar» ma'lumotnomasi faqat nomlarni tutadi.
    /// </para>
    /// <para>
    /// <b>Bo'sh bo'lsa tugma UMUMAN chizilmaydi.</b> Bo'sh bo'lim
    /// ko'rsatib, «ma'lumot yo'q» deb yozish botni tugallanmagandek
    /// ko'rsatardi.
    /// </para>
    /// <para>
    /// Yangi qatorni <c>\n</c> bilan yozing. Matnda <c>&lt;b&gt;</c> va
    /// <c>&lt;i&gt;</c> teglari ishlaydi.
    /// </para>
    /// </remarks>
    public string AboutCyrillic { get; set; } = string.Empty;

    /// <summary>«Kompaniya haqida» matni — lotin alifbosida.</summary>
    /// <remarks>
    /// Bo'sh bo'lsa <see cref="AboutCyrillic"/> ishlatiladi: bitta matn
    /// yozib qo'yish ikkitasini majburlashdan ko'ra foydaliroq — nomzod
    /// hech bo'lmasa biror narsa o'qiydi.
    /// </remarks>
    public string AboutLatin { get; set; } = string.Empty;

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
