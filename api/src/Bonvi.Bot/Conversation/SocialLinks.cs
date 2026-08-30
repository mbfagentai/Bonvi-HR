using Bonvi.Bot.Telegram;

namespace Bonvi.Bot.Conversation;

/// <summary>
/// «Kompaniya haqida» xabari ostidagi ijtimoiy tarmoq tugmalari.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega havolali tugma, matndagi havola emas.</b> Matnga qo'yilgan
/// havola xabar ichida yo'qoladi — ayniqsa matn uzun bo'lsa. Tugma
/// esa xabarning ostida alohida turadi va bosish maydoni katta:
/// telefonda bu ikkalasi orasidagi farqni sezilarli qiladi.
/// </para>
/// <para>
/// Bo'sh havolaning tugmasi CHIZILMAYDI. Telegram bo'sh <c>url</c> li
/// tugmani qabul qilmaydi va BUTUN xabarni rad etardi — ya'ni bitta
/// to'ldirilmagan sozlama «Kompaniya haqida» ni butunlay ishlamaydigan
/// qilib qo'yardi.
/// </para>
/// </remarks>
public static class SocialLinks
{
    /// <summary>Bitta qatorda nechta tugma turadi.</summary>
    /// <remarks>
    /// Nomlar qisqa («Instagram», «Telegram») va telefon ekranida
    /// ikkitasi bemalol sig'adi. Bittalab qilinsa, to'rtta havola
    /// xabarni bekorga cho'zib yuborardi.
    /// </remarks>
    private const int PerRow = 2;

    /// <summary>
    /// To'ldirilgan havolalardan tugmalar qatorlarini yig'adi.
    /// </summary>
    /// <returns>Bironta havola yo'q bo'lsa <c>null</c>.</returns>
    public static IReadOnlyList<IReadOnlyList<InlineButton>>? Buttons(
        BotLanguage language,
        string? instagram,
        string? telegram,
        string? facebook,
        string? website)
    {
        var links = new List<InlineButton>();

        Add(BotTexts.InstagramButton, instagram);
        Add(BotTexts.TelegramButton, telegram);
        Add(BotTexts.FacebookButton, facebook);
        Add(BotTexts.WebsiteButton(language), website);

        if (links.Count == 0)
        {
            return null;
        }

        return [.. links
            .Select((button, index) => (button, index))
            .GroupBy(pair => pair.index / PerRow)
            .Select(row => (IReadOnlyList<InlineButton>)[.. row.Select(pair => pair.button)])];

        void Add(string label, string? url)
        {
            if (!string.IsNullOrWhiteSpace(url))
            {
                links.Add(InlineButton.Link(label, url.Trim()));
            }
        }
    }
}
