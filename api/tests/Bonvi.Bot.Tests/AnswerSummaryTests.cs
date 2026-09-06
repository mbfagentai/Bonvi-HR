using Bonvi.Bot.Conversation;

namespace Bonvi.Bot.Tests;

/// <summary>
/// Yakuniy xulosa (22-qadam).
/// </summary>
/// <remarks>
/// Eski botda (@MBF_jbot) o'tkazib yuborilgan savol xulosaga «Ўтказиб
/// юбориш» degan JAVOB bo'lib yozilib qolgan va nomzod o'zining
/// «иш тажрибаси: Ўтказиб юбориш» degan anketasini tasdiqlashga majbur
/// bo'lardi. Bu testlar aynan shu xatoni qadab turadi.
/// </remarks>
public sealed class AnswerSummaryTests
{
    private const BotLanguage Uz = BotLanguage.Latin;

    [Fact]
    public void Bosh_maydon_umuman_chizilmaydi()
    {
        var state = new ConversationState
        {
            Language = Uz,
            Name = new PersonName("Karimov", "Aziz", null),
            Phone = "+998901234567",
            Education = EducationAnswer.HigherComplete,
        };

        var summary = AnswerSummary.Build(state);

        summary.ShouldContain(BotTexts.SummaryLabel(Uz, SummaryLine.Name));
        summary.ShouldNotContain(BotTexts.SummaryLabel(Uz, SummaryLine.WorkExperience));
        summary.ShouldNotContain(BotTexts.SummaryLabel(Uz, SummaryLine.Speciality));
        summary.ShouldNotContain(BotTexts.SummaryLabel(Uz, SummaryLine.BirthDate));
        summary.ShouldNotContain(BotTexts.SummaryLabel(Uz, SummaryLine.Offer));
    }

    [Fact]
    public void Tugilgan_sana_siljitilmaydi()
    {
        var state = new ConversationState
        {
            Language = Uz,
            BirthDate = new DateOnly(1990, 10, 10),
        };

        // Vaqt mintaqasi KUN qiymatiga tegishli emas: siljitilsa
        // nomzodning tug'ilgan kuni bir kunga surilib ketardi.
        AnswerSummary.Build(state).ShouldContain("10.10.1990");
    }

    [Fact]
    public void Guvohnoma_toifasi_javobning_davomi_bolib_chiziladi()
    {
        var state = new ConversationState
        {
            Language = Uz,
            HasDriverLicense = true,
            DriverLicenseCategory = "BCE",
        };

        AnswerSummary.Build(state)
            .ShouldContain($"{BotTexts.SummaryLabel(Uz, SummaryLine.DriverLicense)}: "
                + $"{BotTexts.YesButton(Uz)}, BCE");
    }

    [Fact]
    public void Guvohnoma_yoq_javobi_ham_chiziladi()
    {
        var state = new ConversationState { Language = Uz, HasDriverLicense = false };

        AnswerSummary.Build(state).ShouldContain(BotTexts.NoButton(Uz));
    }

    [Fact]
    public void Uzun_javob_qisqartiriladi()
    {
        var state = new ConversationState
        {
            Language = Uz,
            WorkExperience = new string('a', Answers.MaxWorkExperienceLength),
            ExtraNote = new string('b', Answers.MaxExtraNoteLength),
            OfficeSkills = new string('c', Answers.MaxOfficeSkillsLength),
        };

        // Telegram xabari 4096 belgi bilan cheklangan: chegarasiz uzun
        // javob yozgan nomzod OXIRGI qadamda «xabar yuborilmadi» degan
        // devorga urilardi.
        AnswerSummary.Build(state).Length.ShouldBeLessThan(4000);
    }

    [Fact]
    public void HTML_belgilari_xavfsiz_qilinadi()
    {
        var state = new ConversationState
        {
            Language = Uz,
            Speciality = "<b>dasturchi</b> & muhandis",
        };

        // Tozalanmagan «<» yoki «&» Telegram'da BUTUN xabarni rad
        // etardi va nomzod hech qanday javob olmasdi.
        var summary = AnswerSummary.Build(state);

        summary.ShouldContain("&lt;b&gt;");
        summary.ShouldContain("&amp;");
    }

    [Theory]
    [InlineData(BotLanguage.Cyrillic)]
    [InlineData(BotLanguage.Latin)]
    [InlineData(BotLanguage.Russian)]
    public void Har_qaysi_tilda_sarlavha_bor(BotLanguage language)
    {
        // Bitta tilda unutilgan tarmoq kompilyatorga ko'rinmaydi —
        // nomzod esa bo'sh sarlavhali qator ko'rardi.
        foreach (var line in Enum.GetValues<SummaryLine>())
        {
            BotTexts.SummaryLabel(language, line).ShouldNotBeNullOrWhiteSpace();
        }
    }
}
