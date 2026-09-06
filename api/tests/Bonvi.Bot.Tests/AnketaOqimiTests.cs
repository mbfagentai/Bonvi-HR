using System.Text.Json;
using Bonvi.Bot.Bonvi;
using Bonvi.Bot.Conversation;

namespace Bonvi.Bot.Tests;

/// <summary>
/// Anketaning 22 qadami: tartib, o'tkazib yuborish va yuborish.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nima qadab turiladi.</b> Nomzod ko'radigan yagona narsa — savollar
/// KETMA-KETLIGI. Bitta qadam tushib qolsa yoki noto'g'ri joyda chizilsa,
/// bu faqat prod'da, haqiqiy nomzodda ko'rinardi va o'sha nomzodning
/// anketasi yo'qolardi.
/// </para>
/// <para>
/// Testlar botga xuddi Telegram yuborgandek yangilik beradi
/// (<see cref="BotHarness"/>) va u qaysi savolni chizganiga qaraydi.
/// </para>
/// </remarks>
public sealed class AnketaOqimiTests
{
    private static readonly Guid VacancyId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private const BotLanguage Uz = BotLanguage.Latin;

    [Fact]
    public async Task Qadamlar_SPEC_dagi_tartib_bilan_boradi()
    {
        using var bot = New(withVacancy: true);

        await bot.ChooseLanguageAsync();
        await bot.ApplyAsync();

        // 2-qadam: vakansiya.
        bot.Last.Text.ShouldBe(BotTexts.ChooseVacancy(Uz));
        bot.HasButton("v:" + VacancyId.ToString("N")).ShouldBeTrue();

        await bot.PressAsync("v:" + VacancyId.ToString("N"));
        bot.Last.Text.ShouldBe(BotTexts.AskName(Uz));

        await bot.TextAsync("Karimov Aziz Akmal o‘g‘li");
        bot.Last.Text.ShouldBe(BotTexts.AskBirthDate(Uz));

        await bot.TextAsync("10.10.1990");
        bot.Last.Text.ShouldBe(BotTexts.AskAddress(Uz));

        await bot.TextAsync("Rishton tumani, Rabot MFY");
        bot.Last.Text.ShouldBe(BotTexts.AskPhone(Uz));

        // Telefon FAQAT kontakt tugmasi orqali — qoida saqlanadi.
        bot.Last.RequestsContact.ShouldBeTrue();

        await bot.ContactAsync();
        bot.Last.Text.ShouldBe(BotTexts.AskEducation(Uz));

        await bot.PressAsync("edu:HigherComplete");
        bot.Last.Text.ShouldBe(BotTexts.AskSpeciality(Uz));

        await bot.TextAsync("Buxgalter");
        bot.Last.Text.ShouldBe(BotTexts.AskWorkExperience(Uz));

        await bot.TextAsync("Dream Job MChJ, kassir, 2015–2018");
        bot.Last.Text.ShouldBe(BotTexts.AskMaritalStatus(Uz));

        await bot.PressAsync("mar:Married");
        bot.Last.Text.ShouldBe(BotTexts.AskRussianLevel(Uz));

        await bot.PressAsync("rus:Advanced");
        bot.Last.Text.ShouldBe(BotTexts.AskOtherLanguages(Uz));

        await bot.TextAsync("ingliz — o‘rta");
        bot.Last.Text.ShouldBe(BotTexts.AskOfficeSkills(Uz));

        await bot.TextAsync("Word 80%, Excel 60%");
        bot.Last.Text.ShouldBe(BotTexts.AskLastSalary(Uz));

        await bot.TextAsync("3 mln");
        bot.Last.Text.ShouldBe(BotTexts.AskExtraNote(Uz));

        await bot.TextAsync("Tez o‘rganaman");
        bot.Last.Text.ShouldBe(BotTexts.AskDriverLicense(Uz));

        await bot.PressAsync("lic:yes");
        bot.Last.Text.ShouldBe(BotTexts.AskDriverLicenseCategory(Uz));

        await bot.PressAsync("cat:BC");
        bot.Last.Text.ShouldBe(BotTexts.AskPhoto(Uz));

        await bot.PhotoAsync();
        bot.Last.Text.ShouldBe(BotTexts.AskExpectedSalary(Uz));

        await bot.PressAsync("sal:From2To3Million");
        bot.Last.Text.ShouldBe(BotTexts.AskReferralSource(Uz));

        await bot.PressAsync("src:Telegram");
        bot.Last.Text.ShouldBe(BotTexts.Offer(Uz, "https://mbf.uz/oferta"));

        await bot.PressAsync("offer");
        bot.Last.Text.ShouldContain(BotTexts.ConfirmQuestion(Uz));

        await bot.PressAsync("submit");
        bot.Last.Text.ShouldBe(BotTexts.Accepted(Uz, "Kassir"));

        // Tugagan ariza botning xotirasida QOLMAYDI.
        bot.State.ShouldBeNull();
    }

    [Fact]
    public async Task Toldirilgan_anketa_serverga_yuboriladi()
    {
        using var bot = New(withVacancy: true);

        await WalkAsync(bot, chooseVacancy: true, withPhoto: false);
        await bot.PressAsync("submit");

        var body = bot.RegisterBody.ShouldNotBeNull().RootElement;

        body.GetProperty("vacancyId").GetGuid().ShouldBe(VacancyId);
        body.GetProperty("lastName").GetString().ShouldBe("Karimov");
        body.GetProperty("firstName").GetString().ShouldBe("Aziz");
        body.GetProperty("birthDate").GetString().ShouldBe("1990-10-10");
        body.GetProperty("phone").GetString().ShouldBe("+998901234567");
        body.GetProperty("address").GetString().ShouldBe("Rishton tumani, Rabot MFY");
        body.GetProperty("speciality").GetString().ShouldBe("Buxgalter");
        body.GetProperty("source").GetString().ShouldBe("TelegramBot");
        body.GetProperty("telegramChatId").GetInt64().ShouldBe(BotHarness.ChatId);
        body.GetProperty("telegramLanguage").GetString().ShouldBe("uz-Latn");

        var form = body.GetProperty("questionnaire");

        // Sanoqlar SATR bo'lib ketadi — server ularni nom bo'yicha o'qiydi.
        form.GetProperty("education").GetString().ShouldBe("HigherComplete");
        form.GetProperty("maritalStatus").GetString().ShouldBe("Married");
        form.GetProperty("russianLevel").GetString().ShouldBe("Advanced");
        form.GetProperty("expectedSalary").GetString().ShouldBe("From2To3Million");
        form.GetProperty("referralSource").GetString().ShouldBe("Telegram");
        form.GetProperty("hasDriverLicense").GetBoolean().ShouldBeTrue();
        form.GetProperty("driverLicenseCategories").GetString().ShouldBe("BC");
        form.GetProperty("offerAcceptedAt").GetDateTimeOffset().ShouldBeGreaterThan(default);
    }

    [Fact]
    public async Task Ochiq_vakansiya_bolmasa_qadam_otkazib_yuboriladi()
    {
        using var bot = New(withVacancy: false);

        await bot.ChooseLanguageAsync();
        await bot.ApplyAsync();

        // Vakansiya SO'RALMAYDI — nomzod to'g'ridan-to'g'ri F.I.Sh. ga
        // tushadi va anketa to'xtamaydi.
        bot.Last.Text.ShouldBe(BotTexts.AskName(Uz));
        bot.State.ShouldNotBeNull().Step.ShouldBe(ConversationStep.Name);
    }

    [Fact]
    public async Task Vakansiyasiz_ariza_vacancyId_siz_yuboriladi()
    {
        using var bot = New(withVacancy: false);

        await WalkAsync(bot, chooseVacancy: false, withPhoto: false);
        await bot.PressAsync("submit");

        var body = bot.RegisterBody.ShouldNotBeNull().RootElement;

        // Maydon UMUMAN yo'q: server arizani o'zi «Umumiy anketa»ga qo'yadi.
        body.TryGetProperty("vacancyId", out _).ShouldBeFalse();

        // Yakuniy xabar ham boshqacha — «ochiq vakansiya yo'q» degani
        // nomzodga rad javobi bo'lib tuyulmasligi kerak.
        bot.Last.Text.ShouldBe(BotTexts.AcceptedToGeneralPool(Uz));
    }

    [Fact]
    public async Task Otkazib_yuborilgan_savol_xulosada_chizilmaydi()
    {
        using var bot = New(withVacancy: false);

        await bot.ChooseLanguageAsync();
        await bot.ApplyAsync();
        await bot.TextAsync("Karimov Aziz");
        await bot.TextAsync("10.10.1990");
        await bot.TextAsync("Rishton tumani");
        await bot.ContactAsync();
        await bot.PressAsync("edu:SecondarySchool");

        // 8-qadamdan 15-qadamgacha — hammasi o'tkazib yuboriladi.
        await bot.PressAsync("skip");
        await bot.PressAsync("skip");
        await bot.PressAsync("mar:Unmarried");
        await bot.PressAsync("rus:Elementary");
        await bot.PressAsync("skip");
        await bot.PressAsync("skip");
        await bot.PressAsync("skip");
        await bot.PressAsync("skip");
        await bot.PressAsync("lic:no");
        await bot.PressAsync("skip");
        await bot.PressAsync("sal:From1To2Million");
        await bot.PressAsync("src:Olx");
        await bot.PressAsync("offer");

        var summary = bot.Last.Text;

        // Eski botda bu qatorlar «Ўтказиб юбориш» degan JAVOB bilan
        // chizilardi — bu xato takrorlanmasligi kerak.
        summary.ShouldNotContain(BotTexts.SummaryLabel(Uz, SummaryLine.Speciality));
        summary.ShouldNotContain(BotTexts.SummaryLabel(Uz, SummaryLine.WorkExperience));
        summary.ShouldNotContain(BotTexts.SummaryLabel(Uz, SummaryLine.OtherLanguages));
        summary.ShouldNotContain(BotTexts.SummaryLabel(Uz, SummaryLine.Photo));
        summary.ShouldNotContain(BotTexts.SkipButton(Uz));

        // Vakansiya tanlanmagan — bu qator ham chizilmaydi.
        summary.ShouldNotContain(BotTexts.SummaryLabel(Uz, SummaryLine.Vacancy));

        // Berilgan javoblar esa joyida.
        summary.ShouldContain("Karimov Aziz");
        summary.ShouldContain(BotTexts.EducationLabel(Uz, EducationAnswer.SecondarySchool));
    }

    [Fact]
    public async Task Menyu_tugmasi_anketa_ortasida_javob_bolib_yozilmaydi()
    {
        using var bot = New(withVacancy: false);

        await bot.ChooseLanguageAsync();
        await bot.ApplyAsync();
        await bot.TextAsync("Karimov Aziz");
        await bot.TextAsync("10.10.1990");
        await bot.TextAsync("Rishton tumani");
        await bot.ContactAsync();
        await bot.PressAsync("edu:HigherComplete");

        // Mutaxassislik kutilyapti — nomzod esa menyu tugmasini bosdi.
        bot.Last.Text.ShouldBe(BotTexts.AskSpeciality(Uz));

        await bot.TextAsync(BotTexts.ContactsButton(BotLanguage.Cyrillic));

        var state = bot.State.ShouldNotBeNull();

        // Tugmaning MATNI mutaxassislik javobiga aylanmadi.
        state.Speciality.ShouldBeNull();

        // Menyu amali yarim qolgan anketani bekor qiladi.
        state.Step.ShouldBe(ConversationStep.Vacancy);
    }

    [Fact]
    public async Task Guvohnoma_yoq_bolsa_toifa_soralmaydi()
    {
        using var bot = New(withVacancy: false);

        await ReachDriverLicenseAsync(bot);

        await bot.PressAsync("lic:no");

        // 17-qadam O'TKAZIB YUBORILDI — keyingisi surat.
        bot.Last.Text.ShouldBe(BotTexts.AskPhoto(Uz));
        bot.State.ShouldNotBeNull().Step.ShouldBe(ConversationStep.Photo);
    }

    [Fact]
    public async Task Guvohnoma_bor_bolsa_toifa_soraladi()
    {
        using var bot = New(withVacancy: false);

        await ReachDriverLicenseAsync(bot);

        await bot.PressAsync("lic:yes");

        bot.Last.Text.ShouldBe(BotTexts.AskDriverLicenseCategory(Uz));
        bot.HasButton("cat:BCED").ShouldBeTrue();
    }

    [Fact]
    public async Task Oferta_havolasi_bosh_bolsa_qadam_chizilmaydi()
    {
        using var bot = New(withVacancy: false);

        bot.OfferUrl = string.Empty;

        await ReachReferralAsync(bot);
        await bot.PressAsync("src:Acquaintance");

        // 21-qadam yo'q — to'g'ridan-to'g'ri yakuniy tasdiq.
        bot.Last.Text.ShouldContain(BotTexts.ConfirmQuestion(Uz));
        bot.HasButton("submit").ShouldBeTrue();
        bot.State.ShouldNotBeNull().Step.ShouldBe(ConversationStep.Confirm);

        // Rozilik qatori ham xulosada yo'q — u berilmagan.
        bot.Last.Text.ShouldNotContain(BotTexts.SummaryLabel(Uz, SummaryLine.Offer));

        /*
          ⭐ ENG MUHIM TEKSHIRUV — serverga NIMA ketishi.

          Ilgari bot bu holatda yuborish lahzasini qo'yardi
          (`?? BotClock.Now`), chunki ustun majburiy edi. Natijada
          bazaga «nomzod ofertaga rozilik berdi» degan huquqiy fakt
          tushardi — holbuki nomzod hech qanday oferta KO'RMAGAN.

          Qadam chizilmasligini tekshirishning o'zi bu xatoni ushlamaydi:
          o'shanda ham qadam chizilmasdi, faqat tanaga soxta vaqt
          yozilardi. Shu sababli tekshiruv aynan TANADA.
        */
        await bot.PressAsync("submit");

        var body = bot.RegisterBody.ShouldNotBeNull().RootElement;
        var form = body.GetProperty("questionnaire");

        // Maydon yo yo'q, yo `null` — ikkalasi ham serverda «rozilik
        // so'ralmagan» degani va ustun bo'sh qoladi.
        if (form.TryGetProperty("offerAcceptedAt", out var oferta))
        {
            oferta.ValueKind.ShouldBe(JsonValueKind.Null, "rozilik so'ralmagan — soxta vaqt yozilmasin");
        }
    }

    [Fact]
    public async Task Oferta_havolasi_borligida_rozilik_vaqti_yoziladi()
    {
        using var bot = New(withVacancy: false);

        await ReachReferralAsync(bot);
        await bot.PressAsync("src:Acquaintance");

        bot.Last.Text.ShouldBe(BotTexts.Offer(Uz, "https://mbf.uz/oferta"));

        await bot.PressAsync("offer");

        var accepted = bot.State.ShouldNotBeNull().OfferAcceptedAt.ShouldNotBeNull();

        // Vaqt TOSHKENT siljishi bilan yoziladi (server uni UTC ga
        // keltiradi) — bu huquqiy fakt, bayroq emas.
        accepted.Offset.ShouldBe(BotClock.TashkentOffset);
    }

    [Fact]
    public async Task Takroriy_ariza_telefon_qadamida_toxtatiladi()
    {
        using var bot = New(withVacancy: false);

        bot.HasExistingApplication = true;

        await bot.ChooseLanguageAsync();
        await bot.ApplyAsync();
        await bot.TextAsync("Karimov Aziz");
        await bot.TextAsync("10.10.1990");
        await bot.TextAsync("Rishton tumani");
        await bot.ContactAsync();

        // Nomzod o'n oltita savolga javob berib bo'lib emas, AYNAN
        // telefon qadamida to'xtatiladi.
        bot.Last.Text.ShouldContain("Kassir");
        bot.State.ShouldBeNull();

        // Sana Toshkent vaqtida: UTC bo'yicha 05.09 soat 20:30 —
        // Toshkentda allaqachon 06.09.
        bot.Last.Text.ShouldContain("06.09.2026");
    }

    [Fact]
    public async Task Surat_ariza_yaratilgandan_KEYIN_yuklanadi()
    {
        using var bot = New(withVacancy: false);

        await WalkAsync(bot, chooseVacancy: false, withPhoto: true);
        await bot.PressAsync("submit");

        var register = bot.ApiCalls.IndexOf("POST /api/recruiting/candidates");
        var photo = bot.ApiCalls.IndexOf($"POST /api/recruiting/candidates/{bot.CreatedCandidateId:D}/photo");

        register.ShouldBeGreaterThanOrEqualTo(0);
        photo.ShouldBeGreaterThan(register, "rasm nomzodning «id» si ma'lum bo'lgach yuboriladi");
    }

    [Fact]
    public async Task Surat_yuklanmasa_ham_ariza_qabul_qilinadi()
    {
        using var bot = New(withVacancy: false);

        bot.PhotoUploadSucceeds = false;

        await WalkAsync(bot, chooseVacancy: false, withPhoto: true);
        await bot.PressAsync("submit");

        // Rasm arizadan muhimroq emas: nomzod muvaffaqiyat xabarini
        // oladi va suhbat yopiladi.
        bot.Last.Text.ShouldBe(BotTexts.AcceptedToGeneralPool(Uz));
        bot.State.ShouldBeNull();
    }

    [Fact]
    public async Task Ariza_yuborilmasa_suhbat_saqlanadi()
    {
        using var bot = New(withVacancy: false);

        bot.RegisterSucceeds = false;

        await WalkAsync(bot, chooseVacancy: false, withPhoto: false);
        await bot.PressAsync("submit");

        bot.Last.Text.ShouldBe(BotTexts.Failed(Uz));

        // Nomzod «Yuborish»ni qayta bosishi kifoya — javoblar joyida.
        bot.State.ShouldNotBeNull().Step.ShouldBe(ConversationStep.Confirm);
    }

    [Fact]
    public async Task Rasm_orniga_matn_kelsa_savol_qaytariladi()
    {
        using var bot = New(withVacancy: false);

        await ReachDriverLicenseAsync(bot);
        await bot.PressAsync("lic:no");

        await bot.TextAsync("rasm yubordim");

        bot.Last.Text.ShouldBe(BotTexts.PhotoNotUnderstood(Uz));
        bot.State.ShouldNotBeNull().Step.ShouldBe(ConversationStep.Photo);
    }

    [Fact]
    public async Task Tushunarsiz_sana_qabul_qilinmaydi()
    {
        using var bot = New(withVacancy: false);

        await bot.ChooseLanguageAsync();
        await bot.ApplyAsync();
        await bot.TextAsync("Karimov Aziz");

        await bot.TextAsync("kecha");

        bot.Last.Text.ShouldBe(BotTexts.BirthDateNotUnderstood(Uz));
        bot.State.ShouldNotBeNull().Step.ShouldBe(ConversationStep.BirthDate);
    }

    [Fact]
    public async Task Boshqa_qadamning_tugmasi_etiborsiz_qoldiriladi()
    {
        using var bot = New(withVacancy: false);

        await bot.ChooseLanguageAsync();
        await bot.ApplyAsync();
        await bot.TextAsync("Karimov Aziz");

        var before = bot.Sent.Count;

        // Eski xabardagi tugma: nomzod suhbatni yuqoriga surib bosdi.
        await bot.PressAsync("edu:HigherComplete");

        bot.Sent.Count.ShouldBe(before, "qadamga tegishli bo'lmagan tugma javobsiz qoladi");
        bot.State.ShouldNotBeNull().Step.ShouldBe(ConversationStep.BirthDate);
        bot.State.ShouldNotBeNull().Education.ShouldBeNull();
    }

    [Fact]
    public async Task Tugma_kutilayotgan_qadamda_yozilgan_matn_javob_bolmaydi()
    {
        using var bot = New(withVacancy: false);

        await bot.ChooseLanguageAsync();
        await bot.ApplyAsync();
        await bot.TextAsync("Karimov Aziz");
        await bot.TextAsync("10.10.1990");
        await bot.TextAsync("Rishton tumani");
        await bot.ContactAsync();

        // Ma'lumot darajasi TUGMA bilan beriladi.
        bot.Last.Text.ShouldBe(BotTexts.AskEducation(Uz));

        await bot.TextAsync("oliy");

        // Anketa o'rtasida «menyudan bo'lim tanlang» deyish nomzodni
        // yarim to'ldirilgan anketadan chiqishga undardi.
        bot.Last.Text.ShouldBe(BotTexts.UseButtons(Uz));
        bot.State.ShouldNotBeNull().Step.ShouldBe(ConversationStep.Education);
        bot.State.ShouldNotBeNull().Education.ShouldBeNull();
    }

    /// <summary>Sinov stendi — ochiq vakansiya bilan yoki usiz.</summary>
    private static BotHarness New(bool withVacancy)
    {
        var bot = new BotHarness { OfferUrl = "https://mbf.uz/oferta" };

        if (withVacancy)
        {
            bot.Vacancies.Add(new OpenVacancy(VacancyId, "Kassir"));
        }

        return bot;
    }

    /// <summary>16-qadamgacha (haydovchilik guvohnomasi) olib boradi.</summary>
    private static async Task ReachDriverLicenseAsync(BotHarness bot)
    {
        await bot.ChooseLanguageAsync();
        await bot.ApplyAsync();
        await bot.TextAsync("Karimov Aziz");
        await bot.TextAsync("10.10.1990");
        await bot.TextAsync("Rishton tumani");
        await bot.ContactAsync();
        await bot.PressAsync("edu:HigherComplete");
        await bot.PressAsync("skip");
        await bot.PressAsync("skip");
        await bot.PressAsync("mar:Married");
        await bot.PressAsync("rus:Intermediate");
        await bot.PressAsync("skip");
        await bot.PressAsync("skip");
        await bot.PressAsync("skip");
        await bot.PressAsync("skip");
    }

    /// <summary>20-qadamgacha (manba) olib boradi.</summary>
    private static async Task ReachReferralAsync(BotHarness bot)
    {
        await ReachDriverLicenseAsync(bot);
        await bot.PressAsync("lic:no");
        await bot.PressAsync("skip");
        await bot.PressAsync("sal:From3To4Million");
    }

    /// <summary>Anketani to'liq to'ldiradi va yakuniy tasdiqda to'xtaydi.</summary>
    private static async Task WalkAsync(BotHarness bot, bool chooseVacancy, bool withPhoto)
    {
        await bot.ChooseLanguageAsync();
        await bot.ApplyAsync();

        if (chooseVacancy)
        {
            await bot.PressAsync("v:" + VacancyId.ToString("N"));
        }

        await bot.TextAsync("Karimov Aziz Akmal o‘g‘li");
        await bot.TextAsync("10.10.1990");
        await bot.TextAsync("Rishton tumani, Rabot MFY");
        await bot.ContactAsync();
        await bot.PressAsync("edu:HigherComplete");
        await bot.TextAsync("Buxgalter");
        await bot.TextAsync("Dream Job MChJ, kassir, 2015–2018");
        await bot.PressAsync("mar:Married");
        await bot.PressAsync("rus:Advanced");
        await bot.PressAsync("skip");
        await bot.PressAsync("skip");
        await bot.PressAsync("skip");
        await bot.PressAsync("skip");
        await bot.PressAsync("lic:yes");
        await bot.PressAsync("cat:BC");

        if (withPhoto)
        {
            await bot.PhotoAsync();
        }
        else
        {
            await bot.PressAsync("skip");
        }

        await bot.PressAsync("sal:From2To3Million");
        await bot.PressAsync("src:Telegram");
        await bot.PressAsync("offer");
    }
}
