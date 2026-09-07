using Bonvi.Application.Tests.Notifications;
using Bonvi.Domain.Notifications;
using Bonvi.Domain.Settings;

namespace Bonvi.Application.Tests.Settings;

/// <summary>
/// Bildirishnoma chegaralari sozlamadan olinishi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega bu test kerak.</b> Chegara yig'uvchidan bazadagi so'rovga
/// uzatiladi va u yerda <c>WHERE end_date &lt;= @until</c> bo'lib
/// ishlatiladi. Ya'ni sozlama e'tiborga olinmasa, HECH QANDAY xato
/// chiqmaydi — ro'yxat shunchaki eski chegara bilan yig'iladi va
/// foydalanuvchi «sozlama ishlamayapti» deb qoladi. Bunday nosozlikni
/// faqat shu yerda — uzatilayotgan SANANI tekshirib — tutish mumkin.
/// </para>
/// <para>
/// <b>Nega sukut qiymat ham tekshiriladi.</b> Sozlamalar ekrani hech
/// qachon ochilmagan tizimda jadval bo'sh bo'ladi va bildirishnomalar
/// baribir avvalgidek kelishi kerak. Bu — o'tish (migratsiya)
/// kafolati: sozlamalar qo'shilgani mavjud xatti-harakatni
/// o'zgartirmaydi.
/// </para>
/// </remarks>
public sealed class SettingsInNotificationsTests
{
    private readonly NotificationTestContext _context = new();

    [Fact]
    public async Task Sozlama_yozilmagan_bolsa_sukut_chegara_ishlaydi()
    {
        await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        _context.Source.LastProbationUntil.ShouldBe(
            NotificationTestContext.Today.AddDays(NotificationRules.ProbationWarningDays));

        _context.Source.LastCandidateSince.ShouldBe(
            NotificationTestContext.Today.AddDays(-NotificationRules.NewCandidateDays));
    }

    [Fact]
    public async Task Sinov_chegarasi_sozlamadan_olinadi()
    {
        _context.Settings.Seed(SystemSettingKeys.ProbationWarningDays, "30");

        await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        _context.Source.LastProbationUntil.ShouldBe(NotificationTestContext.Today.AddDays(30));
    }

    [Fact]
    public async Task Yangi_nomzod_chegarasi_sozlamadan_olinadi()
    {
        _context.Settings.Seed(SystemSettingKeys.NewCandidateDays, "3");

        await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        // Nomzodlarda chegara ORQAGA hisoblanadi: «shu sanadan keyin
        // qo'shilganlar yangi».
        _context.Source.LastCandidateSince.ShouldBe(NotificationTestContext.Today.AddDays(-3));
    }

    /// <summary>
    /// Bitta chegarani o'zgartirish qolganlariga tegmaydi. Sozlamalar
    /// bitta jadvalda yotgani uchun bu ravshan emas: noto'g'ri yozilgan
    /// o'quvchi hamma kalitga bitta qiymat berib qo'yishi mumkin edi.
    /// </summary>
    [Fact]
    public async Task Bitta_sozlama_qolganlariga_tegmaydi()
    {
        _context.Settings.Seed(SystemSettingKeys.ProbationWarningDays, "30");

        await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        _context.Source.LastCandidateSince.ShouldBe(
            NotificationTestContext.Today.AddDays(-NotificationRules.NewCandidateDays));
    }
}
