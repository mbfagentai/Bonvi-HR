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

    private static UpdateSettingsCommand Command(params (string? Key, string? Value)[] items)
        => new([.. items.Select(item => new SystemSettingUpdate(item.Key, item.Value))]);
}
