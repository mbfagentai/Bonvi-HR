using Bonvi.Application.Notifications.Commands;
using Bonvi.Application.Notifications.Queries;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Notifications;
using Bonvi.Domain.Settings;
using NSubstitute;

namespace Bonvi.Application.Tests.Notifications;

/// <summary>
/// «O'qildi» va «o'chirildi» holatining hisoblashdan omon qolishi.
/// </summary>
/// <remarks>
/// Bu testlarning hammasi bitta savolga javob beradi: bildirishnomalar
/// har so'rovda QAYTADAN hisoblanadi — foydalanuvchining qarori shundan
/// keyin ham joyida turadimi? Kalit barqaror bo'lmasa, bularning
/// hammasi yiqiladi.
/// </remarks>
public sealed class NotificationHandlerTests
{
    private readonly NotificationTestContext _context = new();

    private ListNotificationsQueryHandler Royxat =>
        new(_context.Collector, _context.States, _context.CurrentUser);

    private GetUnreadNotificationCountQueryHandler Sanoq =>
        new(_context.Collector, _context.States, _context.CurrentUser);

    private MarkNotificationsReadCommandHandler Oqish => new(
        _context.Collector, _context.States, _context.CurrentUser, _context.Clock, _context.UnitOfWork);

    private DeleteNotificationsCommandHandler Ochirish => new(
        _context.Collector, _context.States, _context.CurrentUser, _context.Clock, _context.UnitOfWork);

    // ────────────────────── Ro'yxat va sanoq ──────────────────────

    [Fact]
    public async Task Bosh_royxat_nol_sanoq_beradi()
    {
        var natija = await Royxat.HandleAsync(
            new ListNotificationsQuery(), TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.Items.ShouldBeEmpty();
        natija.Value.Total.ShouldBe(0);
        natija.Value.UnreadCount.ShouldBe(0);
    }

    [Fact]
    public async Task Yangi_bildirishnomalar_oqilmagan_boladi()
    {
        _context.Probation(daysLeft: 3);
        _context.Birthday(NotificationTestContext.Today, "Karimov Aziz");

        var natija = await Royxat.HandleAsync(
            new ListNotificationsQuery(), TestContext.Current.CancellationToken);

        natija.Value.Total.ShouldBe(2);
        natija.Value.UnreadCount.ShouldBe(2);
        natija.Value.Items.ShouldAllBe(item => !item.IsRead);
    }

    /// <summary>
    /// ⭐ Asosiy qoida: o'qilgan bildirishnoma QAYTA HISOBLASHDAN keyin
    /// ham o'qilgan qoladi.
    /// </summary>
    [Fact]
    public async Task Oqilgan_bildirishnoma_qayta_hisoblashdan_song_ham_oqilgan_qoladi()
    {
        _context.Probation(daysLeft: 3);

        var kalit = await BirinchiKalitAsync();

        var amal = await Oqish.HandleAsync(
            new MarkNotificationsReadCommand([kalit]), TestContext.Current.CancellationToken);

        amal.IsSuccess.ShouldBeTrue();
        amal.Value.Affected.ShouldBe(1);
        amal.Value.UnreadCount.ShouldBe(0);

        // Ro'yxat butunlay YANGIDAN hisoblanadi — kalit barqaror bo'lgani
        // uchun holat topiladi.
        var keyingi = await Royxat.HandleAsync(
            new ListNotificationsQuery(), TestContext.Current.CancellationToken);

        keyingi.Value.Total.ShouldBe(1, "yozuv ro'yxatda qoladi, faqat rangi so'nadi");
        keyingi.Value.UnreadCount.ShouldBe(0);
        keyingi.Value.Items.ShouldHaveSingleItem().IsRead.ShouldBeTrue();
    }

    /// <summary>
    /// ⭐ Ikkinchi asosiy qoida: o'chirilgani sharti hamon rost bo'lsa ham
    /// qaytmaydi.
    /// </summary>
    [Fact]
    public async Task Ochirilgan_bildirishnoma_shart_rost_bolsa_ham_qaytmaydi()
    {
        _context.Probation(daysLeft: -5);

        var kalit = await BirinchiKalitAsync();

        var amal = await Ochirish.HandleAsync(
            new DeleteNotificationsCommand([kalit]), TestContext.Current.CancellationToken);

        amal.IsSuccess.ShouldBeTrue();
        amal.Value.UnreadCount.ShouldBe(0);

        var keyingi = await Royxat.HandleAsync(
            new ListNotificationsQuery(), TestContext.Current.CancellationToken);

        keyingi.Value.Items.ShouldBeEmpty();
        keyingi.Value.Total.ShouldBe(0);
    }

    [Fact]
    public async Task Ochirilgan_bildirishnoma_sanoqqa_ham_tushmaydi()
    {
        _context.Probation(daysLeft: 2);
        _context.Birthday(NotificationTestContext.Today, "Karimov Aziz");

        var kalit = await BirinchiKalitAsync();

        await Ochirish.HandleAsync(
            new DeleteNotificationsCommand([kalit]), TestContext.Current.CancellationToken);

        var sanoq = await Sanoq.HandleAsync(
            new GetUnreadNotificationCountQuery(), TestContext.Current.CancellationToken);

        sanoq.Value.UnreadCount.ShouldBe(1);
    }

    // ────────────────────── «Hammasini o'qildi» ──────────────────────

    [Fact]
    public async Task Hammasini_oqildi_barcha_yozuvlarni_qamraydi()
    {
        _context.Probation(daysLeft: 3);
        _context.Birthday(NotificationTestContext.Today, "Karimov Aziz");
        _context.Candidate();

        var amal = await Oqish.HandleAsync(
            new MarkNotificationsReadCommand(All: true), TestContext.Current.CancellationToken);

        amal.IsSuccess.ShouldBeTrue();
        amal.Value.Affected.ShouldBe(3);
        amal.Value.UnreadCount.ShouldBe(0);

        var keyingi = await Royxat.HandleAsync(
            new ListNotificationsQuery(), TestContext.Current.CancellationToken);

        keyingi.Value.Items.ShouldAllBe(item => item.IsRead);
    }

    /// <summary>
    /// «Hammasini o'qildi» ruxsat filtridan O'TGAN ro'yxatga qo'llanadi:
    /// ko'rinmaydigan bildirishnomaga holat yozilmaydi.
    /// </summary>
    [Fact]
    public async Task Hammasini_oqildi_faqat_korinadigan_yozuvlarga_tegadi()
    {
        _context.Candidate();
        _context.Deny(Permissions.RecruitingView);
        _context.Birthday(NotificationTestContext.Today, "Karimov Aziz");

        var amal = await Oqish.HandleAsync(
            new MarkNotificationsReadCommand(All: true), TestContext.Current.CancellationToken);

        amal.Value.Affected.ShouldBe(1);
        _context.States.Rows.ShouldHaveSingleItem()
            .Key.ShouldStartWith(NotificationKey.Prefix(NotificationType.BirthdayToday));
    }

    [Fact]
    public async Task Takroriy_oqish_birinchi_vaqtni_saqlaydi()
    {
        _context.Probation(daysLeft: 3);
        var kalit = await BirinchiKalitAsync();

        await Oqish.HandleAsync(
            new MarkNotificationsReadCommand([kalit]), TestContext.Current.CancellationToken);

        var birinchiVaqt = _context.States.Rows.ShouldHaveSingleItem().ReadAt;

        _context.Clock.UtcNow.Returns(NotificationTestContext.Now.AddDays(2));

        await Oqish.HandleAsync(
            new MarkNotificationsReadCommand([kalit]), TestContext.Current.CancellationToken);

        _context.States.Rows.ShouldHaveSingleItem().ReadAt.ShouldBe(birinchiVaqt);
    }

    [Fact]
    public async Task Takrorlangan_kalit_bir_marta_sanaladi()
    {
        _context.Probation(daysLeft: 3);
        var kalit = await BirinchiKalitAsync();

        var amal = await Oqish.HandleAsync(
            new MarkNotificationsReadCommand([kalit, kalit, kalit]),
            TestContext.Current.CancellationToken);

        amal.Value.Affected.ShouldBe(1);
        _context.States.Rows.Count.ShouldBe(1);
    }

    // ────────────────────── Foydalanuvchilar bir-biriga aralashmaydi ──────────────────────

    /// <summary>
    /// Foydalanuvchining aniq qarori: bittasi o'qisa, boshqalarda
    /// YANGILIGICHA qoladi.
    /// </summary>
    [Fact]
    public async Task Bir_foydalanuvchining_oqishi_boshqasiga_tasir_qilmaydi()
    {
        _context.Probation(daysLeft: 3);
        var kalit = await BirinchiKalitAsync();

        await Oqish.HandleAsync(
            new MarkNotificationsReadCommand([kalit]), TestContext.Current.CancellationToken);

        // Ikkinchi foydalanuvchi — bir xil bildirishnoma, boshqa hisob.
        _context.CurrentUser.UserId.Returns(Guid.CreateVersion7());

        var boshqasi = await Royxat.HandleAsync(
            new ListNotificationsQuery(), TestContext.Current.CancellationToken);

        boshqasi.Value.UnreadCount.ShouldBe(1);
        boshqasi.Value.Items.ShouldHaveSingleItem().IsRead.ShouldBeFalse();
    }

    // ────────────────────── Kalit tekshiruvi ──────────────────────

    [Fact]
    public async Task Notogri_kalit_rad_etiladi()
    {
        var natija = await Oqish.HandleAsync(
            new MarkNotificationsReadCommand(["shunchaki_matn"]),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("bildirishnoma.kalit_notogri");
        _context.States.Rows.ShouldBeEmpty("noto'g'ri kalit bazaga tushmasligi kerak");
    }

    [Fact]
    public async Task Bosh_royxat_bilan_amal_bajarilmaydi()
    {
        var natija = await Ochirish.HandleAsync(
            new DeleteNotificationsCommand([]), TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("bildirishnoma.kalit_yoq");
    }

    [Fact]
    public async Task Juda_kop_kalit_rad_etiladi()
    {
        var kalitlar = Enumerable
            .Range(0, NotificationRules.MaxKeysPerRequest + 1)
            .Select(_ => NotificationKey.For(NotificationType.CandidateNew, Guid.CreateVersion7()))
            .ToArray();

        var natija = await Oqish.HandleAsync(
            new MarkNotificationsReadCommand(kalitlar), TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("bildirishnoma.kalit_kop");
    }

    /// <summary>
    /// Shart o'zgargan bo'lishi mumkin (HR sinovni yakunladi) — bunday
    /// kalitni «o'qildi» qilish xato EMAS, u shunchaki hech qachon
    /// ishlatilmaydi.
    /// </summary>
    [Fact]
    public async Task Mavjud_bolmagan_bildirishnoma_kaliti_xato_bermaydi()
    {
        var kalit = NotificationKey.For(NotificationType.CandidateNew, Guid.CreateVersion7());

        var natija = await Oqish.HandleAsync(
            new MarkNotificationsReadCommand([kalit]), TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.Affected.ShouldBe(1);
    }

    // ────────────────────── Chegara ──────────────────────

    [Fact]
    public async Task Limit_royxatni_qisqartiradi_lekin_sanoqni_emas()
    {
        _context.Birthday(NotificationTestContext.Today, "Birinchi Xodim");
        _context.Birthday(NotificationTestContext.Today, "Ikkinchi Xodim");
        _context.Birthday(NotificationTestContext.Today, "Uchinchi Xodim");

        var natija = await Royxat.HandleAsync(
            new ListNotificationsQuery(Limit: 2), TestContext.Current.CancellationToken);

        natija.Value.Items.Count.ShouldBe(2);
        natija.Value.Total.ShouldBe(3, "sanoq butun ro'yxat bo'yicha");
        natija.Value.UnreadCount.ShouldBe(3);
    }

    [Fact]
    public async Task Faqat_oqilmaganlar_soralishi_mumkin()
    {
        _context.Probation(daysLeft: 3);
        _context.Birthday(NotificationTestContext.Today, "Karimov Aziz");

        var kalit = await BirinchiKalitAsync();
        await Oqish.HandleAsync(
            new MarkNotificationsReadCommand([kalit]), TestContext.Current.CancellationToken);

        var natija = await Royxat.HandleAsync(
            new ListNotificationsQuery(OnlyUnread: true), TestContext.Current.CancellationToken);

        natija.Value.Items.ShouldHaveSingleItem().IsRead.ShouldBeFalse();
        natija.Value.Total.ShouldBe(2, "«total» — ko'rinadigan hammasi, filtrga bog'liq emas");
    }

    // ────────────────────── Foydalanuvchi aniqlanmagan ──────────────────────

    [Fact]
    public async Task Foydalanuvchisiz_royxat_berilmaydi()
    {
        _context.CurrentUser.UserId.Returns((Guid?)null);

        var natija = await Royxat.HandleAsync(
            new ListNotificationsQuery(), TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("bildirishnoma.foydalanuvchi_aniqlanmadi");
    }

    /// <summary>
    /// ⭐ Uchinchi asosiy qoida: o'qilgan bildirishnoma bir haftadan
    /// keyin ro'yxatdan O'ZI chiqadi.
    /// </summary>
    /// <remarks>
    /// Foydalanuvchining talabi: «o'qilganlari bir haftadan keyin
    /// avtomatik o'chirib yuborilsin, qo'lda har safar tozalab
    /// turgandan shu yaxshiroq». Bildirishnoma HISOBLANADI, ya'ni
    /// sharti rost bo'lib turgan ekan («sinov yozuvi yo'q») u yillab
    /// qaytaverardi.
    /// </remarks>
    [Fact]
    public async Task Oqilgan_bildirishnoma_bir_haftadan_keyin_royxatdan_chiqadi()
    {
        _context.Probation(daysLeft: 3);

        var kalit = await BirinchiKalitAsync();

        var amal = await Oqish.HandleAsync(
            new MarkNotificationsReadCommand([kalit]), TestContext.Current.CancellationToken);

        amal.IsSuccess.ShouldBeTrue();

        // Bir hafta o'tdi: soat oldinga suriladi, holat yozuvidagi
        // `ReadAt` esa eski lahzada qoladi.
        _context.Clock.UtcNow.Returns(NotificationTestContext.Now.AddDays(8));

        var keyingi = await Royxat.HandleAsync(
            new ListNotificationsQuery(), TestContext.Current.CancellationToken);

        keyingi.Value.Total.ShouldBe(0, "o'qilganiga bir haftadan oshgan yozuv ko'rinmaydi");
        keyingi.Value.UnreadCount.ShouldBe(0);
    }

    /// <summary>
    /// O'qilmagan bildirishnoma qancha yotsa ham ro'yxatda QOLADI.
    /// </summary>
    /// <remarks>
    /// Muddat aynan «o'qilgan» holatga bog'langan. Usiz eslatma HR uni
    /// ko'rmasdan turib o'chib ketardi — bu esa butun bildirishnoma
    /// tizimining ma'nosini yo'q qilardi.
    /// </remarks>
    [Fact]
    public async Task Oqilmagan_bildirishnoma_muddatdan_keyin_ham_qoladi()
    {
        _context.Probation(daysLeft: 3);

        _context.Clock.UtcNow.Returns(NotificationTestContext.Now.AddDays(30));

        var natija = await Royxat.HandleAsync(
            new ListNotificationsQuery(), TestContext.Current.CancellationToken);

        natija.Value.Total.ShouldBe(1);
        natija.Value.UnreadCount.ShouldBe(1);
    }

    /// <summary>
    /// Muddat SOZLAMADAN o'qiladi, kodga qadab qo'yilmagan.
    /// </summary>
    [Fact]
    public async Task Muddat_sozlamadan_oqiladi()
    {
        _ = _context.Settings.Seed(SystemSettingKeys.ReadRetentionDays, "30");
        _context.Probation(daysLeft: 3);

        var kalit = await BirinchiKalitAsync();

        _ = await Oqish.HandleAsync(
            new MarkNotificationsReadCommand([kalit]), TestContext.Current.CancellationToken);

        // Sukut bo'yicha 7 kun bo'lardi va yozuv chiqib ketardi —
        // sozlama 30 kunga qo'yilgani uchun qoladi.
        _context.Clock.UtcNow.Returns(NotificationTestContext.Now.AddDays(10));

        var natija = await Royxat.HandleAsync(
            new ListNotificationsQuery(), TestContext.Current.CancellationToken);

        natija.Value.Total.ShouldBe(1, "sozlamada 30 kun turibdi");
    }

    /// <summary>Ro'yxatdagi birinchi bildirishnomaning kaliti.</summary>
    private async Task<string> BirinchiKalitAsync()
    {
        var natija = await Royxat.HandleAsync(
            new ListNotificationsQuery(), TestContext.Current.CancellationToken);

        return natija.Value.Items[0].Key;
    }
}
