using Bonvi.Bot.Bonvi;
using Bonvi.Bot.Conversation;

namespace Bonvi.Bot.Tests;

/// <summary>
/// Takroriy arizaning ikkita to'sig'i: jarayondagi ariza va qayta ariza
/// muddati.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega test kerak.</b> Muammo haqiqiy edi: bitta raqam bilan ikki
/// kun ichida ikkita anketa tushdi (docs/SPEC-qayta-ariza.md). To'siq endi
/// SERVERDA va bot faqat javobni o'qiydi — ya'ni buzilish jimgina
/// bo'ladi. Javobni o'qiy olmagan bot <c>null</c> oladi va nomzodni
/// O'TKAZIB YUBORADI: hech qanday xato, hech qanday jurnal yozuvi,
/// faqat o'sha odamning navbatdagi anketasi.
/// </para>
/// <para>
/// Shu sababli tekshiruv ikki qavatli: matnlar alohida, butun oqim
/// esa haqiqiy JSON javob bilan.
/// </para>
/// </remarks>
public sealed class TakroriyArizaTests
{
    private static readonly DateTimeOffset AppliedAt = new(2026, 9, 5, 20, 30, 0, TimeSpan.Zero);
    private static readonly DateOnly Available = new(2027, 3, 6);

    // ═══════════════════════════════════════════════════════════
    //  MATNLAR
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void Muddat_matni_uchala_tilda_boshqacha()
    {
        /*
          `BotTexts` dagi asosiy tuzoq: `_ =>` tarmog'i lotinni
          bildiradi va ruscha uchun alohida tarmoq yozilmasa, rus
          nomzod jimgina lotincha matn olardi. Kompilyator bunday
          xatoni ushlamaydi — faqat shu test ushlaydi.
        */
        var texts = Enum.GetValues<BotLanguage>()
            .Select(language => BotTexts.ReapplyLater(language, AppliedAt, Available))
            .ToList();

        texts.Distinct(StringComparer.Ordinal).Count().ShouldBe(texts.Count);
    }

    [Fact]
    public void Tayanch_matn_uchala_tilda_boshqacha()
    {
        var texts = Enum.GetValues<BotLanguage>()
            .Select(BotTexts.ApplicationNotAccepted)
            .ToList();

        texts.Distinct(StringComparer.Ordinal).Count().ShouldBe(texts.Count);
    }

    [Theory]
    [InlineData(BotLanguage.Cyrillic)]
    [InlineData(BotLanguage.Latin)]
    [InlineData(BotLanguage.Russian)]
    public void Muddat_matnida_qayta_topshirish_sanasi_bor(BotLanguage language)
    {
        var text = BotTexts.ReapplyLater(language, AppliedAt, Available);

        // Sanasiz xabar nomzodni boshi berk ko'chaga olib borardi: u
        // ertasiga yana urinib, yana o'sha javobni olardi.
        text.ShouldContain("06.03.2027");

        // Ariza qachon topshirilgani ham aytiladi — nomzod gap qaysi
        // murojaat haqida ketayotganini tanishi kerak. Sana TOSHKENT
        // vaqtida: UTC bo'yicha 05.09 soat 20:30 — Toshkentda 06.09.
        text.ShouldContain("06.09.2026");
    }

    [Theory]
    [InlineData(BotLanguage.Cyrillic, "Ҳозирги ҳолат")]
    [InlineData(BotLanguage.Latin, "Hozirgi holat")]
    [InlineData(BotLanguage.Russian, "Текущий статус")]
    public void Muddat_matnida_bosqich_aytilmaydi(BotLanguage language, string stageLabel)
    {
        /*
          Ariza ALLAQACHON yopilgan. «Ҳозирги ҳолат: Анкета» degan
          qator nomzodga jarayon davom etyapti degan yolg'on umid
          berardi — aynan shuning uchun bu yerda `AlreadyApplied`
          ishlatilmagan. Tekshiruv bosqichning NOMI bo'yicha emas,
          qatorning YORLIG'I bo'yicha: bosqich nomi HR ekranidan
          keladi va istalgan so'z bo'lishi mumkin.
        */
        BotTexts.ReapplyLater(language, AppliedAt, Available).ShouldNotContain(stageLabel);
    }

    [Theory]
    [InlineData(BotLanguage.Cyrillic)]
    [InlineData(BotLanguage.Latin)]
    [InlineData(BotLanguage.Russian)]
    public void Tayanch_matnda_sana_ham_muddat_ham_yoq(BotLanguage language)
    {
        /*
          ⭐ Bu matn server TAFSILOT bermaganda ishlatiladi. Aynan
          shuning uchun unda hech qanday sana bo'lishi mumkin emas:
          bot bilmagan qiymatni taxmin qilib yozsa, nomzodga yolg'on
          muddat ketardi va u o'sha kuni kelib yana to'silardi.
        */
        var text = BotTexts.ApplicationNotAccepted(language);

        // Raqamning O'ZI yo'q: sanani ham, muddatni ham raqamsiz
        // yozib bo'lmaydi, ya'ni bitta shart ikkalasini ham qamraydi.
        text.Any(char.IsDigit).ShouldBeFalse();

        text.ShouldNotContain("Kassir");
    }

    [Fact]
    public void Muddat_sanasi_siljitilmaydi()
    {
        /*
          `availableFrom` — `DateOnly`, ya'ni KUN qiymati. Uni
          Toshkent siljishi bilan «tuzatish» sanani bir kunga surib
          yuborardi va nomzod muddat tugagan kuni yana to'silardi.
          Chegara qiymati: yil boshi.
        */
        var text = BotTexts.ReapplyLater(BotLanguage.Latin, AppliedAt, new DateOnly(2027, 1, 1));

        text.ShouldContain("01.01.2027");
        text.ShouldNotContain("31.12.2026");
    }

    // ═══════════════════════════════════════════════════════════
    //  OQIM — haqiqiy JSON javob bilan
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task Tafsilotsiz_tosiq_jimgina_ochilmaydi()
    {
        /*
          ⭐ Server «yo'q» dedi, lekin sana yubormadi (javob shakli
          o'zgardi yoki nazariy nosozlik). Bunday javobda xabarni
          chizib bo'lmaydi — lekin to'siqni OCHIB YUBORISH ham mumkin
          emas: nomzod anketani to'ldirib chiqardi va HR ikkinchi
          yozuvni ko'rardi. Bot tayanch matnga tushishi shart.
        */
        using var bot = new BotHarness
        {
            Block = ApplicationBlockReason.Cooldown,
            WithDetails = false,
        };

        await ReachPhoneAsync(bot);

        bot.Last.Text.ShouldBe(BotTexts.ApplicationNotAccepted(BotLanguage.Latin));

        // Suhbat unutiladi: nomzod qolgan savollarga javob berib
        // o'tirmaydi va menyuga qaytadi.
        bot.State.ShouldBeNull();

        // Ariza YARATILMAYDI — to'siqning butun ma'nosi shu.
        bot.ApiCalls.ShouldNotContain("POST /api/recruiting/candidates");
    }

    [Fact]
    public async Task Qora_royxatdagi_odamga_umumiy_rad_javobi_chiqadi_va_sabab_aytilmaydi()
    {
        // Server «Blacklisted» degan (2026-10-07). Nomzodga sabab ham, sana
        // ham aytilmaydi — tafsilotsiz to'siq bilan bir xil muloyim javob,
        // anketa esa to'xtaydi.
        using var bot = new BotHarness { Block = ApplicationBlockReason.Blacklisted };

        await ReachPhoneAsync(bot);

        bot.Last.Text.ShouldBe(BotTexts.ApplicationNotAccepted(BotLanguage.Latin));
        bot.State.ShouldBeNull();
        bot.ApiCalls.ShouldNotContain("POST /api/recruiting/candidates");
    }

    [Fact]
    public async Task Muddat_ichidagi_odamga_qaytish_sanasi_aytiladi()
    {
        using var bot = new BotHarness
        {
            Block = ApplicationBlockReason.Cooldown,
            AvailableFrom = new DateOnly(2027, 3, 6),
        };

        await ReachPhoneAsync(bot);

        bot.Last.Text.ShouldContain("06.03.2027");
        bot.State.ShouldBeNull();
    }

    [Fact]
    public async Task Server_javob_bermasa_anketa_davom_etadi()
    {
        /*
          ⭐ Ataylab shunday. Server yiqilganida odamni «siz allaqachon
          ariza topshirgansiz» degan YOLG'ON xabar bilan qaytarish eng
          yomon natija bo'lardi: nomzod hech qachon ariza topshira
          olmasdi va buni hech kim bilmasdi.
        */
        using var bot = new BotHarness { CheckSucceeds = false };

        await ReachPhoneAsync(bot);

        // Anketa 7-qadamga (ma'lumot darajasi) o'tdi va suhbat tirik.
        bot.State.ShouldNotBeNull().Phone.ShouldBe("+998901234567");
    }

    [Fact]
    public async Task Tosiq_yoq_bolsa_anketa_davom_etadi()
    {
        using var bot = new BotHarness();

        await ReachPhoneAsync(bot);

        bot.State.ShouldNotBeNull().Phone.ShouldBe("+998901234567");
    }

    /// <summary>Anketani 6-qadamgacha — telefon raqamigacha — olib boradi.</summary>
    /// <remarks>
    /// Tekshiruv AYNAN shu qadamda bo'lishi kerak: uni oxirga
    /// qoldirish nomzodni o'n oltita savolga javob berishga majburlab,
    /// keyin rad etardi.
    /// </remarks>
    private static async Task ReachPhoneAsync(BotHarness bot)
    {
        await bot.ChooseLanguageAsync();
        await bot.QuestionnaireAsync();
        await bot.TextAsync("Karimov Aziz");
        await bot.TextAsync("10.10.1990");
        await bot.TextAsync("Rishton tumani");
        await bot.ContactAsync();
    }
}
