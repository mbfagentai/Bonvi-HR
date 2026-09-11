using Bonvi.Application.Notifications;
using Bonvi.Application.Notifications.Commands;
using Bonvi.Application.Notifications.Dtos;
using Bonvi.Application.Notifications.Queries;
using Bonvi.Domain.Notifications;

namespace Bonvi.Application.Tests.Notifications;

/// <summary>
/// «O'qildi» va «o'chirildi» holati YANGI kalitlar bilan.
/// </summary>
/// <remarks>
/// <para>
/// Kalit uchala yangi turda ham SANAGA (yoki lahzaga) bog'langan va bu
/// ikki tomonlama shartnoma: bir tomondan belgi qayta hisoblashdan omon
/// qolishi kerak, ikkinchi tomondan HR reja vaqtini o'zgartirsa, eski
/// belgi YANGI rejaga O'TMASLIGI kerak. Ikkinchisi birinchisidan muhim
/// emas: u bo'lmasa, bir marta o'qilgan HR ko'chirilgan suhbat haqida
/// boshqa hech qachon ogohlantirish olmasdi.
/// </para>
/// <para>
/// <c>NotificationHandlerTests</c> bilan bir xil naqsh — farqi faqat
/// tekshirilayotgan turlarda.
/// </para>
/// </remarks>
public sealed class CandidateNotificationStateTests
{
    private readonly NotificationTestContext _context = new();

    private ListNotificationsQueryHandler Royxat =>
        new(_context.Collector, _context.States, _context.CurrentUser);

    private MarkNotificationsReadCommandHandler Oqish => new(
        _context.Collector, _context.States, _context.CurrentUser, _context.Clock, _context.UnitOfWork);

    private DeleteNotificationsCommandHandler Ochirish => new(
        _context.Collector, _context.States, _context.CurrentUser, _context.Clock, _context.UnitOfWork);

    // ────────────────────── Belgi qayta hisoblashdan omon qoladi ──────────────────────

    [Fact]
    public async Task Oqilgan_aloqa_eslatmasi_oqilgan_qoladi()
    {
        _context.Contact(daysUntil: 0, name: "Yusupova Nilufar");

        var oldin = await BirinchiAsync();
        oldin.Type.ShouldBe(NotificationType.CandidateContactDue);
        oldin.IsRead.ShouldBeFalse();

        var amal = await Oqish.HandleAsync(
            new MarkNotificationsReadCommand([oldin.Key]), TestContext.Current.CancellationToken);

        amal.IsSuccess.ShouldBeTrue();
        amal.Value.Affected.ShouldBe(1);
        amal.Value.UnreadCount.ShouldBe(0);

        var keyin = await BirinchiAsync();
        keyin.Key.ShouldBe(oldin.Key, "kalit qayta hisoblashda O'ZGARMASLIGI kerak");
        keyin.IsRead.ShouldBeTrue();
        keyin.ReadAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Oqilgan_suhbat_eslatmasi_oqilgan_qoladi()
    {
        _context.Interview(new DateTimeOffset(2026, 8, 29, 5, 0, 0, TimeSpan.Zero), "Xolmatov Sardor");

        var oldin = await BirinchiAsync();
        oldin.Type.ShouldBe(NotificationType.CandidateInterviewSoon);

        await Oqish.HandleAsync(
            new MarkNotificationsReadCommand([oldin.Key]), TestContext.Current.CancellationToken);

        var keyin = await BirinchiAsync();
        keyin.Key.ShouldBe(oldin.Key);
        keyin.IsRead.ShouldBeTrue();
        keyin.Params[NotificationParams.InterviewTime].ShouldBe("10:00", "o'qilgan yozuvning mazmuni o'zgarmaydi");
    }

    // ────────────────────── Reja ko'chirilsa — YANGI eslatma ──────────────────────

    /// <summary>
    /// ⭐ HR kechikkan aloqani bugunga ko'chirdi — bu YANGI reja: eski
    /// «o'qildi» belgisi unga o'tmaydi.
    /// </summary>
    [Fact]
    public async Task Aloqa_boshqa_kunga_kochirilsa_eslatma_qaytadan_korinadi()
    {
        var nomzod = Guid.CreateVersion7();

        _context.Contact(daysUntil: -4, name: "Ergashev Jasur", candidateId: nomzod);

        var kechikkan = await BirinchiAsync();
        kechikkan.Type.ShouldBe(NotificationType.CandidateContactOverdue);

        await Oqish.HandleAsync(
            new MarkNotificationsReadCommand([kechikkan.Key]), TestContext.Current.CancellationToken);

        // HR sanani BUGUNGA ko'chirdi: manbada o'sha nomzodning yangi
        // rejasi turadi.
        _context.Source.Contacts.Clear();
        _context.Contact(daysUntil: 0, name: "Ergashev Jasur", candidateId: nomzod);

        var yangi = await BirinchiAsync();

        yangi.Key.ShouldNotBe(kechikkan.Key, "sana kalitning bir qismi");
        yangi.Type.ShouldBe(NotificationType.CandidateContactDue);
        yangi.IsRead.ShouldBeFalse("yangi reja haqida HR qaytadan ogohlantirilishi kerak");
    }

    /// <summary>
    /// ⭐ Suhbat O'SHA KUN ichida ko'chirildi (10:00 → 15:00). Kun
    /// o'zgarmagan, lekin bu boshqa holat: nomzod yangi vaqt haqida xabar
    /// oladi, HR ham olishi kerak.
    /// </summary>
    [Fact]
    public async Task Suhbat_vaqti_kochirilsa_eslatma_qaytadan_korinadi()
    {
        var nomzod = Guid.CreateVersion7();

        _context.Interview(
            new DateTimeOffset(2026, 8, 29, 5, 0, 0, TimeSpan.Zero),
            "Xolmatov Sardor",
            candidateId: nomzod);

        var ertalabki = await BirinchiAsync();
        ertalabki.Params[NotificationParams.InterviewTime].ShouldBe("10:00");

        await Oqish.HandleAsync(
            new MarkNotificationsReadCommand([ertalabki.Key]), TestContext.Current.CancellationToken);

        _context.Source.Interviews.Clear();
        _context.Interview(
            new DateTimeOffset(2026, 8, 29, 10, 0, 0, TimeSpan.Zero),
            "Xolmatov Sardor",
            candidateId: nomzod);

        var kunduzgi = await BirinchiAsync();

        kunduzgi.Params[NotificationParams.InterviewTime].ShouldBe("15:00");
        kunduzgi.Key.ShouldNotBe(ertalabki.Key, "kalitda LAHZA turadi, kun emas");
        kunduzgi.IsRead.ShouldBeFalse();
    }

    // ────────────────────── O'chirish ──────────────────────

    /// <summary>
    /// Kechikkan aloqaning sharti har kuni rost bo'lib turadi (sana
    /// o'tmishda va o'zgarmaydi) — ya'ni faqat o'chirish uni ro'yxatdan
    /// olib tashlaydi.
    /// </summary>
    [Fact]
    public async Task Ochirilgan_aloqa_eslatmasi_shart_rost_bolsa_ham_qaytmaydi()
    {
        _context.Contact(daysUntil: -30, name: "Ergashev Jasur");

        var yozuv = await BirinchiAsync();

        var amal = await Ochirish.HandleAsync(
            new DeleteNotificationsCommand([yozuv.Key]), TestContext.Current.CancellationToken);

        amal.IsSuccess.ShouldBeTrue();
        amal.Value.UnreadCount.ShouldBe(0);

        var keyingi = await Royxat.HandleAsync(
            new ListNotificationsQuery(), TestContext.Current.CancellationToken);

        keyingi.Value.Items.ShouldBeEmpty();
        keyingi.Value.Total.ShouldBe(0);
    }

    /// <summary>Ro'yxatdagi YAGONA yozuv.</summary>
    private async Task<NotificationDto> BirinchiAsync()
    {
        var natija = await Royxat.HandleAsync(
            new ListNotificationsQuery(), TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        return natija.Value.Items.ShouldHaveSingleItem();
    }
}
