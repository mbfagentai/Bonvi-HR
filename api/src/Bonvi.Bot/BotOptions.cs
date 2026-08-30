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
    /// Yarim qolgan suhbat qancha vaqtdan keyin unutiladi (daqiqa).
    /// </summary>
    /// <remarks>
    /// Nomzod ismini yozmasdan ketib qolsa, uning holati abadiy xotirada
    /// turmasligi kerak. Muddat o'tgach suhbat boshidan boshlanadi.
    /// </remarks>
    [Range(1, 1440)]
    public int ConversationTimeoutMinutes { get; set; } = 60;
}
