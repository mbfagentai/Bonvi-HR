namespace Bonvi.Bot.Conversation;

/// <summary>
/// Bot suhbatining tili.
/// </summary>
/// <remarks>
/// <para>
/// Ikkitasi ATAYLAB: tizimdagi ma'lumotning deyarli hammasi kirillda
/// (lavozimlar, bo'limlar, mavjud xodimlarning F.I.Sh.), lekin yosh
/// nomzodlar lotinda yozadi. Til birinchi qadamda so'raladi va suhbat
/// oxirigacha o'zgarmaydi.
/// </para>
/// <para>
/// Rus tili YO'Q: bu bot ishchi o'rinlariga ariza uchun va nomzodlar
/// o'zbek tilida yozadi. Kerak bo'lsa qo'shish — bitta sanoq qiymati va
/// bitta matn to'plami.
/// </para>
/// </remarks>
public enum BotLanguage
{
    /// <summary>Ўзбекча (кирилл).</summary>
    Cyrillic = 1,

    /// <summary>O'zbekcha (lotin).</summary>
    Latin = 2,
}
