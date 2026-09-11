using System.Globalization;
using Bonvi.Application.Notifications;
using Bonvi.Application.Notifications.Dtos;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Notifications;

namespace Bonvi.Application.Tests.Notifications;

/// <summary>
/// «Bu odam hozir keladi» — yaqinlashib qolgan suhbat eslatmasi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Bu faylning eng qimmatli qismi — MINTAQA.</b> Bazada vaqt UTC,
/// ekranda Toshkent (+5). O'girish unutilsa yoki teskari qo'yilsa,
/// tizim hech qanday xato bermaydi: suhbat vaqti ekranda BESH SOAT
/// oldin ko'rinadi va buni faqat kelmagan nomzod ko'rsatadi. Shu sababla
/// kun chegarasidagi ikkita holat aniq son bilan qadalgan.
/// </para>
/// <para>
/// Oynaning O'ZI (kim ro'yxatga tushadi) bazada kesiladi va u <c>NotificationApiTests</c> dagi ikkita testda haqiqiy
/// PostgreSQL ustida tekshiriladi
/// (<c>Aloqa_eslatmasi_FAQAT_jarayondagi_va_rezervdagiga_chiqadi</c>,
/// <c>Rezervdagiga_SUHBAT_eslatmasi_chiqmaydi</c>);
/// bu yerda UZATILGAN chegaralar va qatordan yasalgan yozuv ko'riladi.
/// </para>
/// </remarks>
public sealed class CandidateInterviewNotificationTests
{
    private readonly NotificationTestContext _context = new();

    // ────────────────────── ⭐ Mintaqa: UTC → Toshkent ──────────────────────

    /// <summary>
    /// ⭐ Kechqurungi UTC lahzasi Toshkentda KEYINGI kunga o'tadi:
    /// 19:30 UTC — bu ertasi kun soat 00:30.
    /// </summary>
    /// <remarks>
    /// Sana ham shu bilan birga siljiydi: UTC kuni olinsa, bunday yozuv
    /// ro'yxatda «kechagi» bo'lib turardi va ro'yxatning tartibi ham
    /// buzilardi.
    /// </remarks>
    [Fact]
    public async Task Kechqurungi_suhbat_Toshkent_kunida_ertasiga_otadi()
    {
        _context.Interview(new DateTimeOffset(2026, 8, 29, 19, 30, 0, TimeSpan.Zero));

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        var item = natija.ShouldHaveSingleItem();
        item.Params[NotificationParams.InterviewTime].ShouldBe("00:30", "UTC 19:30 — Toshkentda 00:30");
        item.Date.ShouldBe(new DateOnly(2026, 8, 30), "kun ham Toshkent kalendarida — ERTASI kun");
    }

    /// <summary>⭐ UTC yarim tuni — Toshkentda o'sha kunning ertalabki 05:00 i.</summary>
    [Fact]
    public async Task Yarim_tundagi_UTC_suhbati_ertalabki_beshga_tushadi()
    {
        _context.Interview(new DateTimeOffset(2026, 8, 29, 0, 0, 0, TimeSpan.Zero));

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        var item = natija.ShouldHaveSingleItem();
        item.Params[NotificationParams.InterviewTime].ShouldBe("05:00");
        item.Date.ShouldBe(new DateOnly(2026, 8, 29), "kun o'zgarmaydi — siljish faqat soatni surdi");
    }

    /// <summary>
    /// Siljishi Toshkent bo'lgan lahza ham AYNAN o'sha vaqtni beradi:
    /// o'girish LAHZA bilan ishlaydi, yozilish shakli bilan emas.
    /// </summary>
    [Fact]
    public async Task Toshkent_siljishidagi_lahza_ozgarmaydi()
    {
        _context.Interview(new DateTimeOffset(2026, 8, 29, 14, 30, 0, TimeSpan.FromHours(5)));

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        natija.ShouldHaveSingleItem().Params[NotificationParams.InterviewTime].ShouldBe("14:30");
    }

    /// <summary>
    /// Vaqt 24 soatlik va INVARIANT formatda: «PM» degan tushuncha yo'q,
    /// ajratgich esa serverning til sozlamasidan olinmaydi.
    /// </summary>
    /// <remarks>
    /// Ba'zi madaniyatlarda soat ajratgichi nuqta («13.05»). Format
    /// satridagi «:» aynan shu ajratgichga almashadi — ya'ni
    /// <c>InvariantCulture</c> olib tashlansa, vaqt server mintaqasiga
    /// qarab boshqacha yozilardi va frontend uni tanimasdi.
    /// </remarks>
    [Fact]
    public async Task Vaqt_server_tiliga_boglanmaydi()
    {
        _context.Interview(new DateTimeOffset(2026, 8, 29, 8, 5, 0, TimeSpan.Zero));

        var oldingi = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("fi-FI");

            var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

            natija.ShouldHaveSingleItem().Params[NotificationParams.InterviewTime].ShouldBe("13:05");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = oldingi;
        }
    }

    // ────────────────────── Yozuvning o'zi ──────────────────────

    /// <summary>
    /// Daraja «diqqat», «jiddiy» emas: suhbat hali BO'LMAGAN — bu bugun
    /// bajariladigan ish, o'tkazib yuborilgan muddat emas.
    /// </summary>
    [Fact]
    public async Task Suhbat_eslatmasi_diqqat_darajasida_keladi()
    {
        var qator = _context.Interview(
            new DateTimeOffset(2026, 8, 29, 5, 0, 0, TimeSpan.Zero),
            name: "Xolmatov Sardor");

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        var item = natija.ShouldHaveSingleItem();
        item.Type.ShouldBe(NotificationType.CandidateInterviewSoon);
        item.Severity.ShouldBe(NotificationSeverity.Warning);
        item.Params[NotificationParams.CandidateName].ShouldBe("Xolmatov Sardor");
        item.Link.Route.ShouldBe(NotificationRoute.Candidate);
        item.Link.Id.ShouldBe(qator.CandidateId);
    }

    /// <summary>
    /// Parametrlar — ism va VAQT. Vaqt sanaviy turlarda umuman yo'q va
    /// aynan shu bilan bu tur ulardan farq qiladi.
    /// </summary>
    [Fact]
    public async Task Suhbat_parametrlari_ism_va_vaqtdan_iborat()
    {
        _context.Interview(new DateTimeOffset(2026, 8, 29, 5, 0, 0, TimeSpan.Zero));

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        var item = natija.ShouldHaveSingleItem();
        item.Params.Count.ShouldBe(2);
        item.Params.Keys.ShouldBe(
            [NotificationParams.CandidateName, NotificationParams.InterviewTime],
            ignoreOrder: true);
    }

    /// <summary>
    /// Kalitda LAHZA turadi, sana emas: suhbat o'sha kun ichida
    /// ko'chirilsa ham bu YANGI holat va ogohlantirish qaytadan
    /// ko'rinishi kerak. Lahza kalitda Toshkent vaqtida yoziladi.
    /// </summary>
    [Fact]
    public async Task Suhbat_kaliti_lahzani_daqiqagacha_tashiydi()
    {
        var qator = _context.Interview(new DateTimeOffset(2026, 8, 29, 19, 30, 0, TimeSpan.Zero));

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        natija.ShouldHaveSingleItem().Key.ShouldBe(
            "nomzod.suhbat_yaqin:"
            + qator.CandidateId.ToString("D", CultureInfo.InvariantCulture)
            + ":2026-08-30T00:30");
    }

    [Fact]
    public async Task Har_bir_suhbat_alohida_yozuv_boladi()
    {
        _context.Interview(new DateTimeOffset(2026, 8, 29, 5, 0, 0, TimeSpan.Zero), "Birinchi Nomzod");
        _context.Interview(new DateTimeOffset(2026, 8, 29, 5, 30, 0, TimeSpan.Zero), "Ikkinchi Nomzod");

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        natija.Count.ShouldBe(2);
        natija.Select(item => item.Key).Distinct(StringComparer.Ordinal).Count().ShouldBe(2);
        natija.ShouldAllBe(item => item.Type == NotificationType.CandidateInterviewSoon);
    }

    // ────────────────────── Oynaning chegaralari ──────────────────────

    /// <summary>
    /// Oyna HOZIRDAN boshlanadi: o'tib ketgan suhbat «yaqin» emas va
    /// u haqidagi ogohlantirish faqat halaqit berardi.
    /// </summary>
    /// <remarks>
    /// Quyi chegara soatdan olinadi (<c>IClock.UtcNow</c>), kunning
    /// boshidan emas — bu bo'lim tizimdagi yagona SOATLI bo'lak.
    /// </remarks>
    [Fact]
    public async Task Oyna_hozirdan_boshlanadi()
    {
        await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        _context.Source.LastInterviewFrom.ShouldBe(NotificationTestContext.Now);
    }

    /// <summary>
    /// Sozlama yozilmagan tizimda oyna — BIR SOAT (mijoz talabi):
    /// «bu odam hozir keladi» degan xabar uzoq oldin kelsa, o'z ma'nosini
    /// yo'qotadi.
    /// </summary>
    [Fact]
    public async Task Sozlamasiz_oyna_bir_soat_boladi()
    {
        await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        NotificationRules.InterviewWarningHours.ShouldBe(1, "sukut qiymat — mijoz kelishuvi");

        _context.Source.LastInterviewUntil.ShouldBe(NotificationTestContext.Now.AddHours(1));
    }

    // ────────────────────── Ruxsat ──────────────────────

    [Fact]
    public async Task Rekruting_ruxsatisiz_suhbat_eslatmasi_kelmaydi()
    {
        _context.Interview(new DateTimeOffset(2026, 8, 29, 5, 0, 0, TimeSpan.Zero));
        _context.Birthday(NotificationTestContext.Today, "Karimov Aziz");

        _context.Deny(Permissions.RecruitingView);

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        natija.ShouldHaveSingleItem().Type.ShouldBe(NotificationType.BirthdayToday);
        natija.ShouldNotContain(item => item.Params.ContainsKey(NotificationParams.InterviewTime));
    }

    /// <summary>
    /// Ruxsat yo'q bo'lsa, suhbat SO'ROVI ham ketmasligi kerak: natijani
    /// keyin filtrlash ham ishlardi, lekin u holda ruxsatsiz odam uchun
    /// ham baza ishlab turardi.
    /// </summary>
    [Fact]
    public async Task Ruxsatsiz_suhbat_sorovi_yuborilmaydi()
    {
        _context.Deny(Permissions.RecruitingView);

        await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        _context.Source.LastInterviewFrom.ShouldBeNull();
        _context.Source.LastInterviewUntil.ShouldBeNull();
        _context.Source.LastContactUntil.ShouldBeNull();
    }
}
