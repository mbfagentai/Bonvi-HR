using Bonvi.Bot.Conversation;

namespace Bonvi.Bot.Tests;

/// <summary>
/// Anketa uzilishlarga CHIDAMLIMI: bot va API qayta ishga tushishi, yopilgan
/// vakansiya, chet el raqami.
/// </summary>
/// <remarks>
/// <para>
/// Egasining talabi (2026-09-29): «server o'chib-yonsa ham anketa davom
/// etsin; deploy anketa topshirishga ta'sir qilmasin». Prod jurnali esa
/// «ariza yuborilmadi» xabarlarining haqiqiy sabablarini ko'rsatdi —
/// yopilgan «Umumiy anketa» va +7 raqamlar. Har bir sabab shu yerda
/// alohida qadab qo'yiladi.
/// </para>
/// </remarks>
public sealed class AnketaChidamliligiTests
{
    private const BotLanguage Uz = BotLanguage.Latin;

    // ────────────────────── Bot qayta ishga tushadi ──────────────────────

    [Fact]
    public async Task Bot_qayta_ishga_tushsa_anketa_davom_etadi()
    {
        var nusxa = new XotiradagiNusxa();

        using (var eski = AnketaOqimiTests.New(withVacancy: false, nusxa))
        {
            await eski.ChooseLanguageAsync();
            await eski.QuestionnaireAsync();
            await eski.TextAsync("Karimov Aziz");
            await eski.TextAsync("10.10.1990");
        }

        // Yangi jarayon — xotira bo'sh, faqat nusxa bor
        using var yangi = AnketaOqimiTests.New(withVacancy: false, nusxa);

        var holat = yangi.State.ShouldNotBeNull("nusxadan tiklanishi kerak");
        holat.Step.ShouldBe(ConversationStep.Address);
        holat.Name.ShouldNotBeNull().LastName.ShouldBe("Karimov");

        await yangi.TextAsync("Rishton tumani");

        yangi.State.ShouldNotBeNull().Step.ShouldBe(ConversationStep.Phone, "nomzod to'xtagan joyidan davom etadi");
    }

    [Fact]
    public async Task Qayta_yetkazilgan_Yuborish_nomzodni_ikki_marta_yozmaydi()
    {
        var nusxa = new XotiradagiNusxa();

        // Birinchi jarayon: server nomzodni YOZDI, lekin javob yetib
        // kelmadi (bot shu paytda to'xtatildi) — suhbat «tasdiq» da qoldi
        using (var eski = AnketaOqimiTests.New(withVacancy: false, nusxa))
        {
            await AnketaOqimiTests.WalkAsync(eski, chooseVacancy: false, withPhoto: false);
            eski.RegisterSucceeds = false;
            await eski.PressAsync("submit");
        }

        // Qayta ishga tushgan bot: Telegram «Yuborish» ni qayta yetkazdi,
        // server esa bu telefonda jarayondagi ariza borligini aytadi
        using var yangi = AnketaOqimiTests.New(withVacancy: false, nusxa);
        yangi.Block = Bonvi.ApplicationBlockReason.Active;

        await yangi.PressAsync("submit");

        yangi.RegisterVacancyIds.ShouldBeEmpty("ariza QAYTA yozilmaydi");
        yangi.Last.Text.ShouldBe(BotTexts.AcceptedToGeneralPool(Uz), "nomzodga ariza qabul qilingani aytiladi");
        yangi.State.ShouldBeNull();
    }

    [Fact]
    public async Task Birinchi_yuborishda_takror_tekshiruvi_qilinmaydi()
    {
        using var bot = AnketaOqimiTests.New(withVacancy: false);

        await AnketaOqimiTests.WalkAsync(bot, chooseVacancy: false, withPhoto: false);

        var tekshiruvlar = bot.ApiCalls.Count(chaqiruv => chaqiruv.EndsWith("/application-check", StringComparison.Ordinal));

        await bot.PressAsync("submit");

        bot.ApiCalls.Count(chaqiruv => chaqiruv.EndsWith("/application-check", StringComparison.Ordinal))
            .ShouldBe(tekshiruvlar, "odatiy yo'lda qo'shimcha so'rov yo'q");
        bot.RegisterVacancyIds.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Suhbat_suratdan_OLDIN_unutiladi()
    {
        // Surat bir-ikki soniya oladi: shu orada bot to'xtatilsa, «tasdiq»
        // holati nusxada qolmasligi kerak — aks holda ariza ikki marta ketardi
        var nusxa = new XotiradagiNusxa();
        using var bot = AnketaOqimiTests.New(withVacancy: false, nusxa);

        await AnketaOqimiTests.WalkAsync(bot, chooseVacancy: false, withPhoto: true);

        var suratChaqiruvi = $"POST /api/recruiting/candidates/{bot.CreatedCandidateId:D}/photo";
        bool? suratYuborilganmidi = null;
        nusxa.Ochirilganda = () => suratYuborilganmidi = bot.ApiCalls.Contains(suratChaqiruvi);

        await bot.PressAsync("submit");

        suratYuborilganmidi.ShouldBe(false);
        bot.ApiCalls.ShouldContain(suratChaqiruvi);
    }

    [Fact]
    public async Task Yakunlangan_ariza_nusxadan_ham_ochadi()
    {
        var nusxa = new XotiradagiNusxa();

        using (var bot = AnketaOqimiTests.New(withVacancy: false, nusxa))
        {
            await AnketaOqimiTests.WalkAsync(bot, chooseVacancy: false, withPhoto: false);
            await bot.PressAsync("submit");
        }

        nusxa.Count.ShouldBe(0, "topshirilgan anketa hech qayerda qolmaydi");
    }

    // ────────────────────── API vaqtincha javob bermaydi ──────────────────────

    [Fact]
    public async Task API_bir_necha_soniya_javob_bermasa_ariza_baribir_qabul_qilinadi()
    {
        using var bot = AnketaOqimiTests.New(withVacancy: false);

        await AnketaOqimiTests.WalkAsync(bot, chooseVacancy: false, withPhoto: false);

        // Deploy: API ikki marta ulanishni rad etadi, keyin ko'tariladi
        bot.RegisterPlan.Enqueue(BotHarness.Unreachable);
        bot.RegisterPlan.Enqueue(BotHarness.Unreachable);

        await bot.PressAsync("submit");

        bot.RegisterVacancyIds.Count.ShouldBe(3, "ikki muvaffaqiyatsiz + bitta muvaffaqiyatli urinish");
        bot.Sent.Count(xabar => xabar.Text == BotTexts.Submitting(Uz))
            .ShouldBe(1, "«yuborilmoqda» bir marta aytiladi");
        bot.Last.Text.ShouldBe(BotTexts.AcceptedToGeneralPool(Uz));
        bot.State.ShouldBeNull();
    }

    [Fact]
    public async Task API_uzoq_javob_bermasa_suhbat_saqlanadi()
    {
        using var bot = AnketaOqimiTests.New(withVacancy: false);

        await AnketaOqimiTests.WalkAsync(bot, chooseVacancy: false, withPhoto: false);

        // Stenddagi uchta qayta urinishning hammasi ham yiqiladi
        for (var i = 0; i < 4; i++)
        {
            bot.RegisterPlan.Enqueue(BotHarness.Unreachable);
        }

        await bot.PressAsync("submit");

        bot.Last.Text.ShouldBe(BotTexts.Failed(Uz));
        bot.State.ShouldNotBeNull().Step.ShouldBe(ConversationStep.Confirm, "«Yuborish» ni qayta bosish kifoya");
    }

    [Fact]
    public async Task API_ishlamay_qolgan_bolsa_keyingi_yuborish_kutmaydi()
    {
        using var bot = AnketaOqimiTests.New(withVacancy: false);

        await AnketaOqimiTests.WalkAsync(bot, chooseVacancy: false, withPhoto: false);

        for (var i = 0; i < 10; i++)
        {
            bot.RegisterPlan.Enqueue(BotHarness.Unreachable);
        }

        await bot.PressAsync("submit");
        bot.RegisterVacancyIds.Count.ShouldBe(4, "bitta urinish + uchta kutib qayta urinish");

        // Bot yangiliklarni birma-bir ishlaydi: har bir bosish boshqalarni
        // yana bir daqiqaga to'xtatmasligi kerak
        await bot.PressAsync("submit");
        bot.RegisterVacancyIds.Count.ShouldBe(5, "API yaqinda javob bermagan — kutilmaydi");
        bot.Last.Text.ShouldBe(BotTexts.Failed(Uz));
    }

    [Fact]
    public async Task Server_rad_etsa_qayta_urinilmaydi()
    {
        using var bot = AnketaOqimiTests.New(withVacancy: false);

        await AnketaOqimiTests.WalkAsync(bot, chooseVacancy: false, withPhoto: false);

        // Rad javobi — «vaqtincha» emas: qayta yuborish natijani o'zgartirmaydi
        bot.RegisterPlan.Enqueue("nomzod.umumiy_anketa_yoq");

        await bot.PressAsync("submit");

        bot.RegisterVacancyIds.Count.ShouldBe(1);
        bot.Last.Text.ShouldBe(BotTexts.Failed(Uz));
    }

    // ────────────────────── Vakansiya yopilgan ──────────────────────

    [Theory]
    [InlineData("vakansiya.qabul_qilmaydi")]
    [InlineData("vakansiya.topilmadi")]
    public async Task Tanlangan_vakansiya_yopilsa_ariza_umumiy_anketaga_tushadi(string kod)
    {
        using var bot = AnketaOqimiTests.New(withVacancy: true);

        await AnketaOqimiTests.WalkAsync(bot, chooseVacancy: true, withPhoto: false);

        // HR vakansiyani nomzod anketa to'ldirayotgan paytda yopdi
        bot.RegisterPlan.Enqueue(kod);

        await bot.PressAsync("submit");

        bot.RegisterVacancyIds.ShouldBe([AnketaOqimiTests.VacancyId, null]);
        bot.Last.Text.ShouldBe(BotTexts.AcceptedVacancyClosed(Uz, "Kassir"));
        bot.State.ShouldBeNull();
    }

    // ────────────────────── Chet el raqami ──────────────────────

    [Fact]
    public async Task Chet_el_raqami_telefon_qadamidayoq_aytiladi_va_ozbek_raqami_yoziladi()
    {
        using var bot = AnketaOqimiTests.New(withVacancy: false);

        await bot.ChooseLanguageAsync();
        await bot.QuestionnaireAsync();
        await bot.TextAsync("Karimov Aziz");
        await bot.TextAsync("10.10.1990");
        await bot.TextAsync("Rishton tumani");
        await bot.ContactAsync("+79161234567");

        bot.Last.Text.ShouldBe(BotTexts.ForeignPhone(Uz));
        bot.State.ShouldNotBeNull().Step.ShouldBe(ConversationStep.Phone);

        // Noto'g'ri yozilgan raqam qabul qilinmaydi
        await bot.TextAsync("12345");
        bot.Last.Text.ShouldBe(BotTexts.PhoneFormat(Uz));

        await bot.TextAsync("+998 90 123 45 67");

        var holat = bot.State.ShouldNotBeNull();
        holat.Phone.ShouldBe("+998901234567");
        holat.Step.ShouldBe(ConversationStep.Education, "anketa keyingi savolga o'tadi");
        holat.PhoneTypingAllowed.ShouldBeFalse();
    }

    [Fact]
    public async Task Ozbek_raqamini_qolda_yozish_odatda_qabul_qilinmaydi()
    {
        using var bot = AnketaOqimiTests.New(withVacancy: false);

        await bot.ChooseLanguageAsync();
        await bot.QuestionnaireAsync();
        await bot.TextAsync("Karimov Aziz");
        await bot.TextAsync("10.10.1990");
        await bot.TextAsync("Rishton tumani");

        // Terish xatosidan himoya o'z joyida: istisno faqat chet el raqamida
        await bot.TextAsync("+998 90 123 45 67");

        bot.Last.Text.ShouldBe(BotTexts.PhoneButtonOnly(Uz));
        bot.State.ShouldNotBeNull().Phone.ShouldBeNull();
    }

    // ────────────────────── Muddat sozlamadan ──────────────────────

    [Fact]
    public async Task Suhbat_muddati_tizim_sozlamasidan_olinadi()
    {
        using var bot = AnketaOqimiTests.New(withVacancy: false);

        bot.ConversationTimeoutMinutes = 1440;

        await bot.ChooseLanguageAsync();
        await bot.QuestionnaireAsync();

        bot.Lifetime.ShouldBe(TimeSpan.FromHours(24), "ekrandagi «Suhbat muddati» ishga tushishdagi qiymatdan ustun");
    }

    /// <summary>Redis o'rnidagi nusxa — ikki stend orasida baham ko'riladi.</summary>
    private sealed class XotiradagiNusxa : IConversationBackup
    {
        private readonly Dictionary<long, ConversationState> _holatlar = [];

        public int Count => _holatlar.Count;

        /// <summary>O'chirish paytida chaqiriladi — tartibni tekshirish uchun.</summary>
        public Action? Ochirilganda { get; set; }

        public ConversationState? Load(long chatId) => _holatlar.GetValueOrDefault(chatId);

        public void Store(long chatId, ConversationState state, TimeSpan lifetime) => _holatlar[chatId] = state;

        public void Remove(long chatId)
        {
            Ochirilganda?.Invoke();
            _holatlar.Remove(chatId);
        }
    }
}
