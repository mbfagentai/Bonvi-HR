using System.Globalization;
using Bonvi.Application.Settings;
using Bonvi.Domain.Notifications;
using Bonvi.Domain.Settings;

namespace Bonvi.Application.Tests.Settings;

/// <summary>
/// Sozlamalarni o'qish qoidalari.
/// </summary>
/// <remarks>
/// Bu yerdagi eng muhim test — <see cref="Yozilmagan_sozlama_sukut_qiymat_beradi"/>.
/// U tizimning eng kutilmagan holatini himoyalaydi: baza yangi
/// o'rnatilgan, sozlamalar ekrani hech qachon ochilmagan va jadval
/// BO'SH. Shunda ham bildirishnomalar kelishi, bot ishlashi kerak.
/// Bu qoida buzilsa, xato faqat MIJOZNING yangi serverida chiqardi —
/// bizda esa hamma joyda ishlab turgandek ko'rinardi.
/// </remarks>
public sealed class SystemSettingsReaderTests
{
    private readonly FakeSystemSettings _rows = new();

    private SystemSettingsReader Reader => new(_rows, _rows.Protector);

    [Fact]
    public async Task Yozilmagan_sozlama_sukut_qiymat_beradi()
    {
        // Jadval BO'SH — hech kim hech qachon sozlamaga tegmagan.
        var sozlamalar = await Reader.ReadAsync(TestContext.Current.CancellationToken);

        sozlamalar
            .Number(SystemSettingKeys.ProbationWarningDays)
            .ShouldBe(NotificationRules.ProbationWarningDays);

        sozlamalar
            .Number(SystemSettingKeys.NewCandidateDays)
            .ShouldBe(NotificationRules.NewCandidateDays);
    }

    /// <summary>
    /// Matnli sozlamada sukut qiymat — BO'SH SATR, <c>null</c> emas.
    /// Bot bo'sh matnda tugmani umuman chizmaydi, ya'ni bo'shlik uning
    /// uchun ma'noli qiymat; <c>null</c> esa chaqiruv joyida tekshiruv
    /// talab qilardi.
    /// </summary>
    [Fact]
    public async Task Yozilmagan_matn_bosh_satr_beradi()
    {
        var sozlamalar = await Reader.ReadAsync(TestContext.Current.CancellationToken);

        sozlamalar.Text(SystemSettingKeys.BotAboutCyrillic).ShouldBe(string.Empty);
        sozlamalar.Text(SystemSettingKeys.BotAboutLatin).ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Yozilgan_qiymat_sukut_qiymatdan_ustun()
    {
        _rows.Seed(SystemSettingKeys.ProbationWarningDays, "14");
        _rows.Seed(SystemSettingKeys.BotAboutLatin, "Bonvi — 540 xodim");

        var sozlamalar = await Reader.ReadAsync(TestContext.Current.CancellationToken);

        sozlamalar.Number(SystemSettingKeys.ProbationWarningDays).ShouldBe(14);
        sozlamalar.Text(SystemSettingKeys.BotAboutLatin).ShouldBe("Bonvi — 540 xodim");

        // Qolganlari tegilmagan — bittasini yozish boshqalarini
        // sukut qiymatdan uzib qo'ymaydi.
        sozlamalar
            .Number(SystemSettingKeys.NewCandidateDays)
            .ShouldBe(NotificationRules.NewCandidateDays);
    }

    /// <summary>
    /// Ro'yxatda BARCHA sozlama bor — bazada yozuvi bo'lmagani ham.
    /// Aks holda yangi o'rnatilgan tizimda sozlamalar sahifasi bo'sh
    /// chiqardi va foydalanuvchi nima sozlash mumkinligini bilmasdi.
    /// </summary>
    [Fact]
    public async Task Royxatda_yozilmagan_sozlamalar_ham_bor()
    {
        _rows.Seed(SystemSettingKeys.ProbationWarningDays, "5");

        var sozlamalar = await Reader.ReadAsync(TestContext.Current.CancellationToken);

        var royxat = sozlamalar.Describe();

        // YASHIRIN sozlamalar bu ro'yxatda YO'Q — shu sababli `All` emas,
        // `Visible` bilan solishtiriladi (sabab `SystemSettingKeys.Visible` da).
        royxat.Count.ShouldBe(SystemSettingKeys.Visible.Count);

        var sinov = royxat.Single(item => item.Key == SystemSettingKeys.ProbationWarningDays);
        sinov.Value.ShouldBe("5");
        sinov.DefaultValue.ShouldBe(
            NotificationRules.ProbationWarningDays.ToString(CultureInfo.InvariantCulture));

        // Yozilmagan sozlamada joriy qiymat sukut qiymatga TENG bo'ladi:
        // mijozga har doim tizim ISHLATAYOTGAN qiymat ko'rsatiladi.
        var candidate = royxat.Single(item => item.Key == SystemSettingKeys.NewCandidateDays);
        candidate.Value.ShouldBe(candidate.DefaultValue);
    }

    /// <summary>
    /// Ta'rif ham javobga tushadi: ekran maydonni shu ma'lumot bo'yicha
    /// chizadi va «qaysi sozlama qanday» degan bilim frontendda
    /// takrorlanmaydi.
    /// </summary>
    [Fact]
    public async Task Royxatda_tur_va_chegaralar_ham_boladi()
    {
        var royxat = (await Reader.ReadAsync(TestContext.Current.CancellationToken)).Describe();

        var timeout = royxat.Single(item => item.Key == SystemSettingKeys.BotConversationTimeoutMinutes);
        timeout.Kind.ShouldBe(SystemSettingKind.Number);
        timeout.Min.ShouldBe(1);
        timeout.Max.ShouldBe(1440);
        timeout.MaxLength.ShouldBeNull();

        var about = royxat.Single(item => item.Key == SystemSettingKeys.BotAboutCyrillic);
        about.Kind.ShouldBe(SystemSettingKind.Text);
        about.MaxLength.ShouldBe(SystemSettingKeys.MaxTextLength);
        about.Min.ShouldBeNull();
        about.Max.ShouldBeNull();
    }

    /// <summary>
    /// Bitta so'rovda bazaga BIR MARTA boriladi. Bildirishnoma yig'ilishi
    /// uchta chegarani ketma-ket so'raydi — har biri alohida so'rov
    /// bo'lsa, bitta ekran ochilishi uchta bir xil SELECT yuborardi.
    /// </summary>
    [Fact]
    public async Task Sozlamalar_sorov_davomida_bir_marta_oqiladi()
    {
        var reader = Reader;

        await reader.ReadAsync(TestContext.Current.CancellationToken);
        await reader.ReadAsync(TestContext.Current.CancellationToken);
        await reader.ReadAsync(TestContext.Current.CancellationToken);

        _rows.ReadCalls.ShouldBe(1);
    }

    /// <summary>
    /// Yozgandan keyin kesh tozalanadi: «saqlandi» javobida foydalanuvchi
    /// o'zi endigina o'zgartirgan ESKI qiymatni ko'rmasligi kerak.
    /// </summary>
    [Fact]
    public async Task Forget_keshni_tashlaydi()
    {
        var reader = Reader;

        await reader.ReadAsync(TestContext.Current.CancellationToken);

        reader.Forget();
        await reader.ReadAsync(TestContext.Current.CancellationToken);

        _rows.ReadCalls.ShouldBe(2);
    }

    /// <summary>
    /// Noma'lum kalitda ISTISNO tashlanadi, sukut qiymat qaytmaydi.
    /// Bu — dasturchi xatosi (kalitlar kodda konstanta) va u jimgina
    /// ishlab ketmasligi kerak: aks holda kalitdagi imlo xatosi hech
    /// qachon sezilmasdi.
    /// </summary>
    [Fact]
    public async Task Nomalum_kalit_oqilganda_istisno_tashlanadi()
    {
        var sozlamalar = await Reader.ReadAsync(TestContext.Current.CancellationToken);

        // Blokli lambda ataylab: shunda u aniq `Action` bo'ladi va
        // qaytgan qiymat bo'yicha chalkashlik qolmaydi.
        Should.Throw<ArgumentException>(() => { _ = sozlamalar.Text("bot.aboutLatin"); });
        Should.Throw<ArgumentException>(() => { _ = sozlamalar.Number("notifications.yoq"); });
    }

    /// <summary>
    /// Matnli sozlamani son deb o'qish — kodda yo'l qo'yilgan chalkashlik.
    /// U jimgina sukut qiymat qaytarsa, xato faqat ekranda g'alati son
    /// bo'lib ko'rinardi.
    /// </summary>
    [Fact]
    public async Task Tur_mos_kelmasa_istisno_tashlanadi()
    {
        var sozlamalar = await Reader.ReadAsync(TestContext.Current.CancellationToken);

        Should.Throw<InvalidOperationException>(
            () => { _ = sozlamalar.Number(SystemSettingKeys.BotAboutCyrillic); });

        Should.Throw<InvalidOperationException>(
            () => { _ = sozlamalar.Text(SystemSettingKeys.ProbationWarningDays); });
    }
}
