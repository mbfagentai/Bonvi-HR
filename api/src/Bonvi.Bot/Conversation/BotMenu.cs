namespace Bonvi.Bot.Conversation;

/// <summary>
/// Doimiy menyu tugmasi bosilganda qaysi bo'lim ochiladi.
/// </summary>
public enum MenuAction
{
    /// <summary>Menyu tugmasi emas — nomzodning oddiy xabari.</summary>
    None = 0,

    /// <summary>
    /// Ochiq ish o'rinlari ro'yxati.
    /// </summary>
    /// <remarks>
    /// ⚠️ Ilgari bu amal ARIZANI boshlab yuborardi (nomi ham <c>Apply</c>
    /// edi). Endi u faqat RO'YXATNI ko'rsatadi: ariza nomzod aniq bir
    /// vakansiyani tanlagandan keyin, o'sha xabar ostidagi tugma bilan
    /// boshlanadi. Sabab <c>BotTexts.VacanciesButton</c> izohida.
    /// </remarks>
    Vacancies = 1,

    /// <summary>«Kompaniya haqida» matni.</summary>
    About = 2,

    /// <summary>Kontaktlar, manzil va xarita.</summary>
    Contacts = 3,

    /// <summary>Tilni almashtirish.</summary>
    Language = 4,

    /// <summary>
    /// Anketa — VAKANSIYASIZ, «Umumiy anketa»ga.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Nega <see cref="Vacancies"/> yo'lidan alohida tugma.</b> Ochiq
    /// vakansiya bo'lmasa «Ariza topshirish» ham vakansiya qadamini
    /// o'tkazib yuboradi va natija bir xil bo'ladi. Lekin vakansiya BOR
    /// bo'lganda nomzod o'sha ro'yxatdan birini tanlashga majbur
    /// bo'lardi — holbuki uning maqsadi aniq bir o'rin emas, umuman
    /// ishga joylashish bo'lishi mumkin. Foydalanuvchi talabi shu
    /// (2026-09-07): «vakansiya bo'lsa ham shunchaki alohida anketa
    /// to'ldirish mumkin bo'lsin».
    /// </para>
    /// <para>
    /// Eski botda (@MBF_jbot) ham aynan shunday: «Бўш иш ўринлари»
    /// yonida «Ўзингиз ҳақингизда маълумот қолдиринг» alohida turadi.
    /// </para>
    /// </remarks>
    Questionnaire = 5,
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
    /// «Vakansiyalar», «Anketa to'ldirish» va til esa doim bor:
    /// birinchi ikkisi botning borlik sababi, uchinchisi sozlamaga
    /// umuman bog'liq emas.
    /// </remarks>
    /// <param name="language">Tugmalar qaysi tilda yozilishi.</param>
    /// <param name="hasAbout">«Kompaniya haqida» matni sozlanganmi.</param>
    /// <param name="hasContacts">Kontakt matni sozlanganmi.</param>
    /// <param name="hasQuestionnaire">
    /// «Umumiy anketa» ochiqmi — yopiq bo'lsa «Anketa to'ldirish» chizilmaydi.
    /// </param>
    public static List<string> Labels(
        BotLanguage language,
        bool hasAbout,
        bool hasContacts,
        bool hasQuestionnaire = true)
    {
        var labels = new List<string> { BotTexts.VacanciesButton(language) };

        /*
          «Anketa to'ldirish» faqat «Umumiy anketa» OCHIQ bo'lsa chiziladi
          (egasining qarori, 2026-10-05: yopiq vakansiyaga anketa
          topshirilmaydi — bosib bo'lmaydigan tugma ham ko'rinmasin).
          Ochiq vakansiya bo'lmaganda ham u ishlaydi — ariza «Umumiy
          anketa»ga tushadi.

          Ilgari u shartsiz chizilardi: «Umumiy anketa» yopilmasdi. Holat
          noma'lum bo'lsa (server javob bermadi) chaqiruvchi `true` beradi —
          tugma bosilganda baribir jonli tekshiruv bor va yopiq bo'lsa bot
          buni aytadi.
        */
        if (hasQuestionnaire)
        {
            labels.Add(BotTexts.QuestionnaireButton(language));
        }

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
            if (string.Equals(value, BotTexts.VacanciesButton(language), StringComparison.Ordinal))
            {
                return MenuAction.Vacancies;
            }

            if (string.Equals(value, BotTexts.QuestionnaireButton(language), StringComparison.Ordinal))
            {
                return MenuAction.Questionnaire;
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
