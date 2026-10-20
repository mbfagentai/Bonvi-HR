using Bonvi.Application.Settings;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Settings;
using NSubstitute;

namespace Bonvi.Application.Tests.Settings;

/// <summary>
/// Sozlamalarni yozish qoidalari.
/// </summary>
/// <remarks>
/// <para>
/// Bu testlar bitta savolga javob beradi: <b>bazaga nima tushishi
/// mumkin</b>. Sozlama qiymati keyinchalik hech qanday tekshiruvsiz
/// ishlatiladi (chegara SQL so'roviga, matn Telegram xabariga tushadi),
/// ya'ni yozish payti — YAGONA to'siq.
/// </para>
/// <para>
/// Xato holatlarida har doim IKKI narsa tekshiriladi: xato kodi va
/// jadvalning tegilmagani. Faqat kodni tekshirish yetmasdi — handler
/// xato qaytarib, shu bilan birga yarim ishni qilib qo'yishi ham
/// mumkin edi.
/// </para>
/// </remarks>
public sealed class UpdateSettingsCommandHandlerTests
{
    private readonly FakeSystemSettings _rows = new();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private UpdateSettingsCommandHandler Handler =>
        new(_rows, new SystemSettingsReader(_rows, _rows.Protector), _rows.Protector, _unitOfWork);

    // ────────────────────── Rad etiladigan so'rovlar ──────────────────────

    /// <summary>
    /// Noma'lum kalit — bazaga tushishdan oldingi oxirgi to'siq. U o'tib
    /// ketsa, jadvalda hech qachon o'qilmaydigan qator qolardi va uni
    /// yozgan odam sozlama «ishlamayapti» deb o'ylardi.
    /// </summary>
    [Fact]
    public async Task Nomalum_kalit_rad_etiladi()
    {
        var natija = await Handler.HandleAsync(
            Command(("bot.aboutLatin", "Bonvi")),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("sozlama.notogri_kalit");
        natija.Error.Type.ShouldBe(ErrorType.Validation);

        _rows.Rows.ShouldBeEmpty();
    }

    /// <summary>
    /// Registr AHAMIYATLI: «Bot.About.Latin» boshqa kalit hisoblanadi.
    /// Aks holda bazada bir xil sozlamaning ikki xil yozuvi paydo
    /// bo'lishi mumkin edi.
    /// </summary>
    [Fact]
    public async Task Kalit_registri_ahamiyatli()
    {
        var natija = await Handler.HandleAsync(
            Command(("Bot.About.Latin", "Bonvi")),
            TestContext.Current.CancellationToken);

        natija.Error.Code.ShouldBe("sozlama.notogri_kalit");
    }

    [Fact]
    public async Task Kalit_korsatilmasa_alohida_xato_beriladi()
    {
        var natija = await Handler.HandleAsync(
            Command(("   ", "7")),
            TestContext.Current.CancellationToken);

        // Bo'sh kalit va noma'lum kalit — ikki xil vaziyat: birinchisi
        // so'rov noto'g'ri yig'ilganini bildiradi, ikkinchisi mijoz
        // tizimda yo'q sozlamani bilishini.
        natija.Error.Code.ShouldBe("sozlama.kalit_bosh");
    }

    /// <summary>
    /// Chegaradan tashqari son. Chegarasiz qo'yilganda sozlama tizimni
    /// ishdan chiqarishi mumkin edi: «0 kun» bildirishnomalarni
    /// butunlay o'chirardi, «100000 kun» esa ro'yxatni hech qachon
    /// bo'shatmasdi.
    /// </summary>
    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("91")]
    public async Task Chegaradan_tashqari_son_rad_etiladi(string qiymat)
    {
        var natija = await Handler.HandleAsync(
            Command((SystemSettingKeys.ProbationWarningDays, qiymat)),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("sozlama.chegara");

        // Xabarda chegaralar ko'rsatiladi — foydalanuvchi nimadan chiqib
        // ketganini bilishi kerak.
        natija.Error.Message.ShouldContain("90");

        _rows.Rows.ShouldBeEmpty();
    }

    /// <summary>
    /// Suhbat muddatining chegarasi boshqa: 1 daqiqadan bir sutkagacha.
    /// Chegara har bir kalitda O'ZINIKI ekani shu test bilan qulflanadi.
    /// </summary>
    [Fact]
    public async Task Har_bir_kalitning_chegarasi_ozinikidir()
    {
        var yaroqli = await Handler.HandleAsync(
            Command((SystemSettingKeys.BotConversationTimeoutMinutes, "1440")),
            TestContext.Current.CancellationToken);

        yaroqli.IsSuccess.ShouldBeTrue("bir sutka — ruxsat etilgan eng katta qiymat");

        var yaroqsiz = await Handler.HandleAsync(
            Command((SystemSettingKeys.BotConversationTimeoutMinutes, "1441")),
            TestContext.Current.CancellationToken);

        yaroqsiz.Error.Code.ShouldBe("sozlama.chegara");
    }

    [Theory]
    [InlineData("etti")]
    [InlineData("7.5")]
    [InlineData("")]
    public async Task Son_ornida_boshqa_narsa_rad_etiladi(string qiymat)
    {
        var natija = await Handler.HandleAsync(
            Command((SystemSettingKeys.NewCandidateDays, qiymat)),
            TestContext.Current.CancellationToken);

        natija.Error.Code.ShouldBe("sozlama.notogri_son");
        _rows.Rows.ShouldBeEmpty();
    }

    [Fact]
    public async Task Juda_uzun_matn_rad_etiladi()
    {
        var uzun = new string('a', SystemSettingKeys.MaxTextLength + 1);

        var natija = await Handler.HandleAsync(
            Command((SystemSettingKeys.BotAboutCyrillic, uzun)),
            TestContext.Current.CancellationToken);

        natija.Error.Code.ShouldBe("sozlama.matn_uzun");
        _rows.Rows.ShouldBeEmpty();
    }

    [Fact]
    public async Task Bosh_royxat_rad_etiladi()
    {
        var natija = await Handler.HandleAsync(
            new UpdateSettingsCommand([]),
            TestContext.Current.CancellationToken);

        // Bo'sh so'rov jimgina «muvaffaqiyat» qaytarsa, klientdagi xato
        // sezilmasdan qolardi: foydalanuvchi «saqlandi» xabarini ko'rib,
        // hech narsa saqlanmagan bo'lardi.
        natija.Error.Code.ShouldBe("sozlama.royxat_bosh");
    }

    [Fact]
    public async Task Takroriy_kalit_rad_etiladi()
    {
        var natija = await Handler.HandleAsync(
            Command(
                (SystemSettingKeys.ProbationWarningDays, "3"),
                (SystemSettingKeys.ProbationWarningDays, "5")),
            TestContext.Current.CancellationToken);

        // «Oxirgisi g'olib» qoidasi yashirin xato bo'lardi: qaysi qiymat
        // yozilgani so'rovdagi TARTIBGA bog'liq bo'lib qolardi.
        natija.Error.Code.ShouldBe("sozlama.takror_kalit");
        _rows.Rows.ShouldBeEmpty();
    }

    /// <summary>
    /// ⭐ Hammasi yoki hech narsa. Bitta yozuv xato bo'lsa, undan OLDINGI
    /// to'g'ri yozuvlar ham saqlanmaydi — aks holda foydalanuvchi xato
    /// xabarini ko'rib, aslida nima saqlanganini bilmasdi.
    /// </summary>
    [Fact]
    public async Task Bitta_xato_yozuv_butun_sorovni_bekor_qiladi()
    {
        var natija = await Handler.HandleAsync(
            Command(
                (SystemSettingKeys.BotAboutLatin, "Bonvi — 540 xodim"),
                (SystemSettingKeys.ProbationWarningDays, "1000")),
            TestContext.Current.CancellationToken);

        natija.Error.Code.ShouldBe("sozlama.chegara");

        _rows.Rows.ShouldBeEmpty("to'g'ri kelgan birinchi yozuv ham saqlanmasligi kerak");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ────────────────────── Muvaffaqiyatli yozish ──────────────────────

    [Fact]
    public async Task Yangi_qiymat_saqlanadi()
    {
        var natija = await Handler.HandleAsync(
            Command((SystemSettingKeys.ProbationWarningDays, "14")),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();

        var saqlangan = _rows.Rows.ShouldHaveSingleItem();
        saqlangan.Key.ShouldBe(SystemSettingKeys.ProbationWarningDays);
        saqlangan.Value.ShouldBe("14");

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Javobda TO'LIQ ro'yxat qaytadi va u YANGI qiymatni ko'rsatadi:
    /// ekran shu javob bilan yangilanadi va ikkinchi so'rov yubormaydi.
    /// Kesh tozalanmasa, foydalanuvchi «saqlandi» javobida eski qiymatni
    /// ko'rardi.
    /// </summary>
    [Fact]
    public async Task Javobda_yangilangan_toliq_royxat_qaytadi()
    {
        var natija = await Handler.HandleAsync(
            Command((SystemSettingKeys.ProbationWarningDays, "21")),
            TestContext.Current.CancellationToken);

        natija.Value.Count.ShouldBe(SystemSettingKeys.Visible.Count);

        natija.Value
            .Single(item => item.Key == SystemSettingKeys.ProbationWarningDays)
            .Value
            .ShouldBe("21");
    }

    /// <summary>
    /// Ikkinchi yozish YANGI qator ochmaydi. Ochilsa, bitta sozlamada
    /// ikkita qiymat bo'lib qolardi va qaysi biri o'qilishi so'rov
    /// tartibiga qolardi.
    /// </summary>
    [Fact]
    public async Task Ikkinchi_yozish_yangi_qator_ochmaydi()
    {
        _rows.Seed(SystemSettingKeys.ProbationWarningDays, "3");

        await Handler.HandleAsync(
            Command((SystemSettingKeys.ProbationWarningDays, "5")),
            TestContext.Current.CancellationToken);

        var saqlangan = _rows.Rows.ShouldHaveSingleItem();
        saqlangan.Value.ShouldBe("5");
    }

    /// <summary>
    /// Matnli sozlamani BO'SHATISH mumkin — bu o'chirish emas, ma'noli
    /// qiymat: bo'sh matnda bot «Kompaniya haqida» tugmasini umuman
    /// chizmaydi.
    /// </summary>
    [Fact]
    public async Task Matnli_sozlamani_boshatish_mumkin()
    {
        _rows.Seed(SystemSettingKeys.BotAboutLatin, "Eski matn");

        var natija = await Handler.HandleAsync(
            Command((SystemSettingKeys.BotAboutLatin, "   ")),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        _rows.Rows.ShouldHaveSingleItem().Value.ShouldBe(string.Empty);
    }

    /// <summary>
    /// Matn chetidagi bo'shliqlar olib tashlanadi: HR matnni Word'dan
    /// nusxa qilganda oxirida ko'rinmas yangi qator qolib ketadi va bot
    /// uni xabar oxirida bo'sh qator qilib chizardi.
    /// </summary>
    [Fact]
    public async Task Matn_chetidagi_boshliqlar_olib_tashlanadi()
    {
        await Handler.HandleAsync(
            Command((SystemSettingKeys.BotAboutCyrillic, "  Бонви  \n")),
            TestContext.Current.CancellationToken);

        _rows.Rows.ShouldHaveSingleItem().Value.ShouldBe("Бонви");
    }

    [Fact]
    public async Task Bir_nechta_sozlama_birdan_yoziladi()
    {
        var natija = await Handler.HandleAsync(
            Command(
                (SystemSettingKeys.ProbationWarningDays, "10"),
                (SystemSettingKeys.NewCandidateDays, "14"),
                (SystemSettingKeys.BotConversationTimeoutMinutes, "30")),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        _rows.Rows.Count.ShouldBe(3);

        // Bitta tranzaksiya: uchala qiymat birga saqlanadi yoki umuman
        // saqlanmaydi.
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ────────────────────── Davomat: o'tish saqlash muddati, «0 — hech qachon» ──────────────────────

    /// <summary>
    /// <c>attendance.passRetentionDays</c> ning chegaralari (B2): 0 ✓, 30 ✓, 3650 ✓.
    /// </summary>
    /// <remarks>
    /// Surat muddati SHU so'rovda eng kichik qiymatga (7) qo'yiladi: aks holda sukut 60
    /// bilan «30 &lt; 60» juftlik qoidasi (<c>davomat.saqlash_muddati_nomuvofiq</c>)
    /// ishga tushib, 30 ning chegara sifatida qabul qilinishini tekshirib bo'lmasdi.
    /// </remarks>
    [Theory]
    [InlineData("0")]
    [InlineData("30")]
    [InlineData("3650")]
    public async Task Otish_saqlash_muddatida_nol_va_chegara_ichidagi_qiymat_qabul_qilinadi(string qiymat)
    {
        var natija = await Handler.HandleAsync(
            Command(
                (SystemSettingKeys.PhotoRetentionDays, "7"),
                (SystemSettingKeys.PassRetentionDays, qiymat)),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Code : null);
        _rows.Rows.Single(row => row.Key == SystemSettingKeys.PassRetentionDays).Value.ShouldBe(qiymat);
    }

    /// <summary>
    /// 0 bilan quyi chegara orasidagi son — «hech qachon» emas, TERISH XATOSI (29 ✗, 1 ✗);
    /// yuqori chegaradan keyingisi ham (3651 ✗). Hech narsa yozilmaydi.
    /// </summary>
    [Theory]
    [InlineData("1")]
    [InlineData("29")]
    [InlineData("3651")]
    [InlineData("-1")]
    public async Task Otish_saqlash_muddatida_noldan_boshqa_chegaradan_tashqari_qiymat_rad_etiladi(string qiymat)
    {
        var natija = await Handler.HandleAsync(
            Command(
                (SystemSettingKeys.PhotoRetentionDays, "7"),
                (SystemSettingKeys.PassRetentionDays, qiymat)),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("sozlama.chegara");
        natija.Error.Message.ShouldContain("3650");

        _rows.Rows.ShouldBeEmpty("surat muddati ham saqlanmasligi kerak — hammasi yoki hech narsa");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// #69 aynan: bazada surat muddati 60 turibdi, so'rovda FAQAT o'tish muddati. 30 — surat
    /// muddatidan qisqa, <c>davomat.saqlash_muddati_nomuvofiq</c>; 0 — «hech qachon», u kichik son
    /// EMAS va qabul qilinadi; 60 — teng, qabul.
    /// </summary>
    [Theory]
    [InlineData("30", "davomat.saqlash_muddati_nomuvofiq")]
    [InlineData("0", null)]
    [InlineData("60", null)]
    public async Task Otish_muddati_bazadagi_surat_muddati_bilan_solishtiriladi_nol_istisno(string otish, string? kutilganXato)
    {
        _rows.Seed(SystemSettingKeys.PhotoRetentionDays, "60");

        var natija = await Handler.HandleAsync(
            Command((SystemSettingKeys.PassRetentionDays, otish)),
            TestContext.Current.CancellationToken);

        if (kutilganXato is null)
        {
            natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Code : null);
            _rows.Rows.Single(row => row.Key == SystemSettingKeys.PassRetentionDays).Value.ShouldBe(otish);
        }
        else
        {
            natija.Error.Code.ShouldBe(kutilganXato);
            natija.Error.Type.ShouldBe(ErrorType.Validation);
            _rows.Rows.ShouldNotContain(row => row.Key == SystemSettingKeys.PassRetentionDays);
        }
    }

    /// <summary>
    /// «0 — hech qachon» faqat o'tish muddatiga tegishli: surat muddatida 0 — oddiy chegara
    /// xatosi. <c>AllowZero</c> boshqa kalitga «yuqib» qolsa, bugungi suratlar tungi
    /// tozalashda o'chib ketardi.
    /// </summary>
    [Fact]
    public async Task Surat_saqlash_muddatida_nol_rad_etiladi()
    {
        var natija = await Handler.HandleAsync(
            Command((SystemSettingKeys.PhotoRetentionDays, "0")),
            TestContext.Current.CancellationToken);

        natija.Error.Code.ShouldBe("sozlama.chegara");
        _rows.Rows.ShouldBeEmpty();
    }

    // ────────────────────── Jadval so'rovidagi tanaffus qoidasi (SPEC-sorov-tahriri §2.2, §8.3) ──────────────────────

    /// <summary>To'rt son birga: boshi &lt; oxiri, uzunlik ≤ jumadagi, jumadagi ≤ oraliq.</summary>
    [Theory]
    [InlineData("900", "720", "60", "120")] // boshi oxiridan keyin
    [InlineData("720", "720", "60", "120")] // boshi = oxiri
    [InlineData("720", "900", "90", "60")] // uzunlik jumadagidan katta
    [InlineData("720", "780", "60", "120")] // jumadagi oraliqqa sig'maydi
    public async Task Tanaffus_qoidasining_zid_sonlari_rad_etiladi(string from, string to, string minutes, string fridayMax)
    {
        var natija = await Handler.HandleAsync(
            Command(
                (SystemSettingKeys.RequestBreakFrom, from),
                (SystemSettingKeys.RequestBreakTo, to),
                (SystemSettingKeys.RequestBreakMinutes, minutes),
                (SystemSettingKeys.RequestBreakFridayMaxMinutes, fridayMax)),
            TestContext.Current.CancellationToken);

        natija.Error.Code.ShouldBe("davomat.tanaffus_qoidasi_nomuvofiq");
        natija.Error.Type.ShouldBe(ErrorType.Validation);
        _rows.Rows.ShouldBeEmpty();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Tanaffus_qoidasi_mos_bolsa_saqlanadi()
    {
        var natija = await Handler.HandleAsync(
            Command(
                (SystemSettingKeys.RequestBreakFrom, "780"),
                (SystemSettingKeys.RequestBreakTo, "840"),
                (SystemSettingKeys.RequestBreakMinutes, "30"),
                (SystemSettingKeys.RequestBreakFridayMaxMinutes, "45")),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Code : null);
        _rows.Rows.Single(row => row.Key == SystemSettingKeys.RequestBreakMinutes).Value.ShouldBe("30");
    }

    /// <summary>
    /// Qisman yangilash JORIY qiymatlar bilan solishtiriladi: bazada 12:00–13:00 / 60 / 60 turibdi,
    /// jumadagini 120 qilish oraliqqa sig'maydi; sukut qoida ustida esa 120 — joyida.
    /// </summary>
    [Fact]
    public async Task Tanaffus_qoidasining_bitta_soni_joriy_qiymatlar_bilan_tekshiriladi()
    {
        _rows.Seed(SystemSettingKeys.RequestBreakTo, "780");
        _rows.Seed(SystemSettingKeys.RequestBreakFridayMaxMinutes, "60");

        var rad = await Handler.HandleAsync(
            Command((SystemSettingKeys.RequestBreakFridayMaxMinutes, "120")),
            TestContext.Current.CancellationToken);

        rad.Error.Code.ShouldBe("davomat.tanaffus_qoidasi_nomuvofiq");

        var boshqa = new FakeSystemSettings();
        var qabul = await new UpdateSettingsCommandHandler(
                boshqa,
                new SystemSettingsReader(boshqa, boshqa.Protector),
                boshqa.Protector,
                _unitOfWork)
            .HandleAsync(Command((SystemSettingKeys.RequestBreakFridayMaxMinutes, "120")), TestContext.Current.CancellationToken);

        qabul.IsSuccess.ShouldBeTrue(qabul.IsFailure ? qabul.Error.Code : null);
    }

    [Fact]
    public async Task Tanaffus_kalitlari_bolmasa_bazaga_murojaat_qilinmaydi()
    {
        var natija = await Handler.HandleAsync(
            Command((SystemSettingKeys.ProbationWarningDays, "10")),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();

        // Bitta o'qish — saqlangandan keyingi javob; tekshiruv uchun alohida o'qish yo'q.
        _rows.ReadCalls.ShouldBe(1);
    }

    private static UpdateSettingsCommand Command(params (string? Key, string? Value)[] items)
        => new([.. items.Select(item => new SystemSettingUpdate(item.Key, item.Value))]);
}
