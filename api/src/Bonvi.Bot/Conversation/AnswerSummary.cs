using System.Globalization;
using System.Text;

namespace Bonvi.Bot.Conversation;

/// <summary>Yakuniy xulosadagi qatorlar — sarlavhalari <c>BotTexts</c> da.</summary>
/// <remarks>
/// Tartib SPEC dagi anketa tartibi bilan bir xil: nomzod savollarga
/// qanday ketma-ketlikda javob bergan bo'lsa, xulosani ham shunday
/// o'qiydi. Boshqa tartib uni har bir qatorni qidirishga majburlardi.
/// </remarks>
public enum SummaryLine
{
    /// <summary>F.I.Sh.</summary>
    Name = 1,

    /// <summary>Tug'ilgan kun.</summary>
    BirthDate = 2,

    /// <summary>Yashash manzili.</summary>
    Address = 3,

    /// <summary>Telefon raqami.</summary>
    Phone = 4,

    /// <summary>Tanlangan vakansiya.</summary>
    Vacancy = 5,

    /// <summary>Ma'lumot darajasi.</summary>
    Education = 6,

    /// <summary>Mutaxassisligi.</summary>
    Speciality = 7,

    /// <summary>Ish tajribasi.</summary>
    WorkExperience = 8,

    /// <summary>Oilaviy holati.</summary>
    MaritalStatus = 9,

    /// <summary>Rus tili darajasi.</summary>
    RussianLevel = 10,

    /// <summary>Boshqa tillar.</summary>
    OtherLanguages = 11,

    /// <summary>Office dasturlari.</summary>
    OfficeSkills = 12,

    /// <summary>Oxirgi maosh.</summary>
    LastSalary = 13,

    /// <summary>Qo'shimcha ma'lumot.</summary>
    ExtraNote = 14,

    /// <summary>Haydovchilik guvohnomasi.</summary>
    DriverLicense = 15,

    /// <summary>Surat.</summary>
    Photo = 16,

    /// <summary>Kutilayotgan maosh.</summary>
    ExpectedSalary = 17,

    /// <summary>Ish haqida qayerdan bilgan.</summary>
    ReferralSource = 18,

    /// <summary>Ommaviy oferta.</summary>
    Offer = 19,
}

/// <summary>
/// To'ldirilgan anketani nomzodga qayta ko'rsatadi (22-qadam).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>BO'SH maydon UMUMAN chizilmaydi.</b> Eski botda o'tkazib
/// yuborilgan savol xulosaga «Ўтказиб юбориш» degan JAVOB bo'lib
/// yozilib qolgan — nomzod o'zining «ish tajribasi: Ўтказиб юбориш»
/// degan anketasini tasdiqlashga majbur bo'lardi. Bu yerda bunday
/// qator yo'q: javob berilmagan savol ko'rinmaydi.
/// </para>
/// <para>
/// <b>Nega alohida sinf.</b> Xulosa — bu MANTIQ (qaysi qator
/// chiziladi), matn emas. Uni <c>BotTexts</c> ichiga qo'yish tekshirib
/// bo'lmaydigan qilib qo'yardi; shu yerda esa u sof funksiya:
/// holat kiradi, satr chiqadi.
/// </para>
/// <para>
/// <b>Uzunlik chegarasi ataylab bor.</b> Telegram xabari 4096 belgi
/// bilan cheklangan, javob maydonlarining yig'indisi esa undan katta
/// bo'lishi mumkin (faqat ish tajribasi va qo'shimcha ma'lumot —
/// 4000 belgi). Chegarasiz uzun javob yozgan nomzod OXIRGI qadamda
/// «xabar yuborilmadi» degan devorga urilardi va butun anketasini
/// yo'qotardi.
/// </para>
/// </remarks>
public static class AnswerSummary
{
    /// <summary>Bitta erkin matnli javobning xulosadagi uzunligi.</summary>
    /// <remarks>
    /// Xulosa — TEKSHIRUV, matnning o'zi emas: nomzod yozganini o'zi
    /// biladi va unga javob YETIB BORGANINI ko'rsatish yetarli. To'liq
    /// matn serverga o'zgarishsiz ketadi.
    /// </remarks>
    private const int MaxValueLength = 300;

    /// <summary>Butun xulosaning chegarasi.</summary>
    /// <remarks>
    /// Telegram chegarasi 4096; 3500 unga tayanib qo'yilgan va
    /// xabarning qolgan qismi («Barcha ma'lumot to'g'rimi?») hamda HTML
    /// teglari uchun zaxira qoldiradi.
    /// </remarks>
    private const int MaxSummaryLength = 3500;

    /// <summary>Xulosadagi qatorlarning eng ko'p soni — ro'yxatning boshlang'ich hajmi.</summary>
    private const int SummaryCapacity = 19;

    /// <summary>
    /// To'ldirilgan javoblardan xulosa yig'adi.
    /// </summary>
    /// <param name="state">Suhbatning holati — javoblar shu yerda.</param>
    /// <returns>HTML bilan tayyor matn (tugmalar va savol chaqiruvchida).</returns>
    public static string Build(ConversationState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var language = state.Language;
        var lines = new List<string>(SummaryCapacity);

        if (state.Name is { } name)
        {
            var full = string.Join(' ', new[] { name.LastName, name.FirstName, name.MiddleName }
                .Where(part => !string.IsNullOrWhiteSpace(part)));

            Add(lines, language, SummaryLine.Name, full);
        }

        if (state.BirthDate is { } birthDate)
        {
            // Sana SILJITILMAYDI: `DateOnly` — kun qiymati (`BotClock` izohi).
            Add(lines, language, SummaryLine.BirthDate, birthDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));
        }

        Add(lines, language, SummaryLine.Address, state.Address);
        Add(lines, language, SummaryLine.Phone, state.Phone);

        // Vakansiya tanlanmagan bo'lishi mumkin (ochiq ish o'rni yo'q) —
        // unda bu qator ham chizilmaydi, «Umumiy anketa» degan ichki nom
        // esa nomzodga umuman ko'rsatilmaydi.
        Add(lines, language, SummaryLine.Vacancy, state.VacancyTitle);

        if (state.Education is { } education)
        {
            Add(lines, language, SummaryLine.Education, BotTexts.EducationLabel(language, education));
        }

        Add(lines, language, SummaryLine.Speciality, state.Speciality);
        Add(lines, language, SummaryLine.WorkExperience, state.WorkExperience);

        if (state.MaritalStatus is { } marital)
        {
            Add(lines, language, SummaryLine.MaritalStatus, BotTexts.MaritalLabel(language, marital));
        }

        if (state.RussianLevel is { } russian)
        {
            Add(lines, language, SummaryLine.RussianLevel, BotTexts.RussianLevelLabel(language, russian));
        }

        Add(lines, language, SummaryLine.OtherLanguages, state.OtherLanguages);
        Add(lines, language, SummaryLine.OfficeSkills, state.OfficeSkills);
        Add(lines, language, SummaryLine.LastSalary, state.LastSalary);
        Add(lines, language, SummaryLine.ExtraNote, state.ExtraNote);

        if (state.HasDriverLicense is { } hasLicense)
        {
            /*
              Toifa javobning DAVOMI, alohida qator emas: «гувоҳнома:
              ҳа» va «тоифа: BC» ikkita qator bo'lganida ular bir-biridan
              uzilib, o'qishda «ҳа» ma'nosiz qolardi.
            */
            var value = hasLicense ? BotTexts.YesButton(language) : BotTexts.NoButton(language);

            if (hasLicense && !string.IsNullOrWhiteSpace(state.DriverLicenseCategory))
            {
                value += ", " + state.DriverLicenseCategory;
            }

            Add(lines, language, SummaryLine.DriverLicense, value);
        }

        if (state.PhotoFileId is not null)
        {
            Add(lines, language, SummaryLine.Photo, BotTexts.PhotoSent(language));
        }

        if (state.ExpectedSalary is { } salary)
        {
            Add(lines, language, SummaryLine.ExpectedSalary, BotTexts.SalaryLabel(language, salary));
        }

        if (state.ReferralSource is { } referral)
        {
            Add(lines, language, SummaryLine.ReferralSource, BotTexts.ReferralLabel(language, referral));
        }

        if (state.OfferAcceptedAt is not null)
        {
            Add(lines, language, SummaryLine.Offer, BotTexts.OfferAccepted(language));
        }

        return Join(lines);
    }

    /// <summary>
    /// Qatorni qo'shadi — qiymat bo'sh bo'lsa HECH NARSA qilmaydi.
    /// </summary>
    /// <remarks>
    /// Bo'sh javobni bu yerda to'sish qoidani BITTA joyga yig'adi:
    /// chaqiruv joylarida <c>if</c> takrorlanganida ulardan biri bir kun
    /// unutilardi va xulosaga bo'sh qator chiqib qolardi.
    /// </remarks>
    private static void Add(List<string> lines, BotLanguage language, SummaryLine line, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var text = value.Trim();

        if (text.Length > MaxValueLength)
        {
            text = text[..MaxValueLength] + "…";
        }

        lines.Add($"{BotTexts.SummaryLabel(language, line)}: {BotTexts.Escape(text)}");
    }

    /// <summary>Qatorlarni birlashtiradi va umumiy chegaraga sig'diradi.</summary>
    private static string Join(List<string> lines)
    {
        var builder = new StringBuilder();

        foreach (var line in lines)
        {
            if (builder.Length + line.Length + 1 > MaxSummaryLength)
            {
                // Qolgan qatorlar xulosaga sig'madi va uch nuqta buni
                // ochiq ko'rsatadi. Javoblarning O'ZI to'liq yuboriladi —
                // qisqarish faqat SHU ekranda.
                _ = builder.Append('…');
                break;
            }

            if (builder.Length > 0)
            {
                _ = builder.Append('\n');
            }

            _ = builder.Append(line);
        }

        return builder.ToString();
    }
}
