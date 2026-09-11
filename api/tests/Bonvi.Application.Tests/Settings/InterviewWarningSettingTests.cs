using Bonvi.Application.Settings;
using Bonvi.Application.Tests.Notifications;
using Bonvi.Domain.Notifications;
using Bonvi.Domain.Settings;
using NSubstitute;

namespace Bonvi.Application.Tests.Settings;

/// <summary>
/// «Suhbatgacha necha soat qolganda ogohlantirilsin» sozlamasi —
/// <c>notifications.interviewWarningHours</c>.
/// </summary>
/// <remarks>
/// <para>
/// Bu sozlama bildirishnoma chegaralari orasida YAGONA soatli qiymat va
/// shu sababli uning chegarasi ham boshqa: <c>MaxDays</c> emas,
/// <see cref="SystemSettingKeys.MaxWarningHours"/>. 90 kun bu yerda
/// «90 soat» degani bo'lardi va u ma'nosiz katta.
/// </para>
/// <para>
/// <b>Nega yozish va o'qish BITTA faylda.</b> Sozlamaning ma'nosi
/// ikkita bo'lakdan yig'iladi: qiymat bazaga TUSHISHI mumkin bo'lishi va
/// tushgach oynani HAQIQATAN o'zgartirishi. Ular ajratilsa, «qiymat
/// saqlandi, lekin hech narsa o'zgarmadi» degan nosozlik ikkita
/// yashil test orasidan o'tib ketardi.
/// </para>
/// <para>
/// Migratsiya YO'Q: jadvalda qator bo'lmasa, o'qishda sukut qiymat
/// ishlatiladi (<c>SystemSettingsReader</c>) — buni
/// <c>CandidateInterviewNotificationTests</c> qadaydi.
/// </para>
/// </remarks>
public sealed class InterviewWarningSettingTests
{
    private readonly NotificationTestContext _context = new();

    private UpdateSettingsCommandHandler Yozish => new(
        _context.Settings,
        new SystemSettingsReader(_context.Settings, _context.Settings.Protector),
        _context.Settings.Protector,
        _context.UnitOfWork);

    // ────────────────────── Rad etiladigan qiymatlar ──────────────────────

    /// <summary>
    /// Chegaradan tashqari soat rad etiladi. «25» — shartnomadagi aniq
    /// misol: eng katta qiymat 24 soat, chunki undan uzoq
    /// ogohlantirishda «bu odam hozir keladi» degan ma'no yo'qoladi.
    /// «0» esa bildirishnomani butunlay o'chirib qo'yardi.
    /// </summary>
    [Theory]
    [InlineData("25")]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("100")]
    public async Task Chegaradan_tashqari_soat_rad_etiladi(string qiymat)
    {
        var natija = await Yozish.HandleAsync(
            Sorov(qiymat), TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("sozlama.chegara");

        // Xabarda chegaralar ko'rsatiladi: foydalanuvchi nimadan chiqib
        // ketganini bilishi kerak.
        natija.Error.Message.ShouldContain("24");

        _context.Settings.Rows.ShouldBeEmpty("rad etilgan qiymat bazaga tushmasligi kerak");
        await _context.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("bir")]
    [InlineData("1.5")]
    [InlineData("")]
    public async Task Son_ornida_boshqa_narsa_rad_etiladi(string qiymat)
    {
        var natija = await Yozish.HandleAsync(
            Sorov(qiymat), TestContext.Current.CancellationToken);

        natija.Error.Code.ShouldBe("sozlama.notogri_son");
        _context.Settings.Rows.ShouldBeEmpty();
    }

    // ────────────────────── Qabul qilinadigan qiymatlar ──────────────────────

    /// <summary>Ikkala chet ham ruxsat etilgan: 1 soatdan 24 soatgacha.</summary>
    [Theory]
    [InlineData("1")]
    [InlineData("24")]
    public async Task Chegaradagi_qiymat_qabul_qilinadi(string qiymat)
    {
        var natija = await Yozish.HandleAsync(
            Sorov(qiymat), TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();

        var saqlangan = _context.Settings.Rows.ShouldHaveSingleItem();
        saqlangan.Key.ShouldBe(SystemSettingKeys.InterviewWarningHours);
        saqlangan.Value.ShouldBe(qiymat);
    }

    // ────────────────────── Qiymat oynani HAQIQATAN o'zgartiradi ──────────────────────

    /// <summary>
    /// ⭐ Saqlangan qiymat suhbat oynasini o'zgartiradi. Oyna bazada
    /// qo'llanadi, shu sababli bu yerda manbaga UZATILGAN chegara
    /// tekshiriladi — sozlama e'tiborga olinmasa, hech qanday xato
    /// chiqmaydi va HR «sozlama ishlamayapti» deb qolardi.
    /// </summary>
    [Fact]
    public async Task Saqlangan_qiymat_oynani_ozgartiradi()
    {
        var yozildi = await Yozish.HandleAsync(
            Sorov("6"), TestContext.Current.CancellationToken);

        yozildi.IsSuccess.ShouldBeTrue();

        await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        _context.Source.LastInterviewUntil.ShouldBe(
            NotificationTestContext.Now.AddHours(6),
            "oyna sozlamadagi soatga cho'ziladi");

        _context.Source.LastInterviewFrom.ShouldBe(
            NotificationTestContext.Now,
            "quyi chegara sozlamaga bog'liq emas — u har doim «hozir»");
    }

    /// <summary>
    /// Qiymat SOATDA o'lchanadi, kunda emas. Bu qo'shni sozlamalar
    /// kunda hisoblangani uchun aniq qadalgan: birlik almashsa, «24»
    /// deb yozilgan qiymat 24 KUNLIK oyna berardi.
    /// </summary>
    [Fact]
    public async Task Qiymat_soatda_olchanadi()
    {
        _context.Settings.Seed(SystemSettingKeys.InterviewWarningHours, "24");

        await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        _context.Source.LastInterviewUntil.ShouldBe(NotificationTestContext.Now.AddHours(24));
        _context.Source.LastInterviewUntil.ShouldNotBe(NotificationTestContext.Now.AddDays(24));
    }

    /// <summary>
    /// Suhbat sozlamasi qolgan chegaralarga TEGMAYDI. Sozlamalar bitta
    /// jadvalda yotgani uchun bu ravshan emas: noto'g'ri yozilgan
    /// o'quvchi hamma kalitga bitta qiymat berib qo'yishi mumkin edi.
    /// </summary>
    [Fact]
    public async Task Suhbat_sozlamasi_qolgan_chegaralarga_tegmaydi()
    {
        _context.Settings.Seed(SystemSettingKeys.InterviewWarningHours, "6");

        await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        _context.Source.LastProbationUntil.ShouldBe(
            NotificationTestContext.Today.AddDays(NotificationRules.ProbationWarningDays));

        _context.Source.LastCandidateSince.ShouldBe(
            NotificationTestContext.Today.AddDays(-NotificationRules.NewCandidateDays));

        // Keyingi aloqada sozlama UMUMAN yo'q: sana HR ning o'zi
        // tanlagan kuni, chegara emas.
        _context.Source.LastContactUntil.ShouldBe(NotificationTestContext.Today);
    }

    private static UpdateSettingsCommand Sorov(string? qiymat)
        => new([new SystemSettingUpdate(SystemSettingKeys.InterviewWarningHours, qiymat)]);
}
