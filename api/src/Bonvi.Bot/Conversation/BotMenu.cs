namespace Bonvi.Bot.Conversation;

/// <summary>
/// Doimiy menyu tugmasi bosilganda qaysi bo'lim ochiladi.
/// </summary>
public enum MenuAction
{
    /// <summary>Menyu tugmasi emas — nomzodning oddiy xabari.</summary>
    None = 0,

    /// <summary>Vakansiyalar ro'yxati va ariza.</summary>
    Apply = 1,

    /// <summary>«Kompaniya haqida» matni.</summary>
    About = 2,

    /// <summary>Kontaktlar, manzil va xarita.</summary>
    Contacts = 3,

    /// <summary>Tilni almashtirish.</summary>
    Language = 4,
}

/// <summary>
/// Ekran ostidagi doimiy menyu: tugmalari va ularni tanib olish.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega doimiy menyu (reply keyboard), xabar ostidagi tugmalar
/// emas.</b> Xabar ostidagi tugma o'sha xabarga bog'lanib qoladi:
/// suhbat uzayganda u yuqoriga surilib ketadi va nomzod uni topolmaydi.
/// Doimiy menyu esa ekranning ostida turadi — nomzod istalgan paytda,
/// hatto ariza yarmida ham bo'limni almashtira oladi va <c>/start</c>
/// buyrug'ini eslab qolishi shart emas.
/// </para>
/// <para>
/// <b>Buning narxi:</b> tugma bosilganda Telegram uning MATNINI oddiy
/// xabar qilib yuboradi — ya'ni «bu tugmami yoki nomzod shunday
/// yozdimi?» degan savolga bot matnni taqqoslab javob beradi.
/// <see cref="Resolve"/> aynan shu ish uchun.
/// </para>
/// </remarks>
public static class BotMenu
{
    /// <summary>Barcha tillar — tugmani tanib olish uchun.</summary>
    /// <remarks>
    /// Taqqoslash UCHALA tilda ham qilinadi: suhbat unutilgan bo'lsa
    /// bot tugmani qaysi tilda chizganini bilmaydi, tugma esa nomzod
    /// ekranida turaveradi. Bitta tilda tekshirish bunday tugmani
    /// «tushunarsiz xabar» deb rad etardi.
    /// </remarks>
    private static readonly BotLanguage[] Languages =
        [BotLanguage.Cyrillic, BotLanguage.Latin, BotLanguage.Russian];

    /// <summary>
    /// Menyu tugmalari — sozlanmagan bo'lim tugmasi CHIZILMAYDI.
    /// </summary>
    /// <remarks>
    /// Bo'sh bo'limga olib boradigan tugma botni buzuq ko'rsatadi.
    /// «Ariza topshirish» va til esa doim bor: birinchisi botning
    /// borlik sababi, ikkinchisi sozlamaga umuman bog'liq emas.
    /// </remarks>
    /// <param name="language">Tugmalar qaysi tilda yozilishi.</param>
    /// <param name="hasAbout">«Kompaniya haqida» matni sozlanganmi.</param>
    /// <param name="hasContacts">Kontakt matni sozlanganmi.</param>
    public static List<string> Labels(BotLanguage language, bool hasAbout, bool hasContacts)
    {
        var labels = new List<string> { BotTexts.ApplyButton(language) };

        if (hasAbout)
        {
            labels.Add(BotTexts.AboutButton(language));
        }

        if (hasContacts)
        {
            labels.Add(BotTexts.ContactsButton(language));
        }

        labels.Add(BotTexts.LanguageButton);

        return labels;
    }

    /// <summary>
    /// Kelgan matn menyu tugmasimi — uchala tilda ham qaraladi.
    /// </summary>
    /// <remarks>
    /// Taqqoslash AYNAN teng bo'lishi bo'yicha: «ariza» deb yozgan
    /// odam menyuni ochib yubormasligi kerak. Emoji va bo'shliqlar
    /// tugmani boshqa matndan yetarlicha ajratib turadi, ya'ni
    /// tasodifan mos kelish amalda bo'lmaydi.
    /// </remarks>
    public static MenuAction Resolve(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return MenuAction.None;
        }

        var value = text.Trim();

        if (string.Equals(value, BotTexts.LanguageButton, StringComparison.Ordinal))
        {
            return MenuAction.Language;
        }

        foreach (var language in Languages)
        {
            if (string.Equals(value, BotTexts.ApplyButton(language), StringComparison.Ordinal))
            {
                return MenuAction.Apply;
            }

            if (string.Equals(value, BotTexts.AboutButton(language), StringComparison.Ordinal))
            {
                return MenuAction.About;
            }

            if (string.Equals(value, BotTexts.ContactsButton(language), StringComparison.Ordinal))
            {
                return MenuAction.Contacts;
            }
        }

        return MenuAction.None;
    }
}
