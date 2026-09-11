using System.Globalization;
using Bonvi.Application.Notifications;
using Bonvi.Application.Notifications.Dtos;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Notifications;

namespace Bonvi.Application.Tests.Notifications;

/// <summary>
/// «Bugun kimga qo'ng'iroq qilishim kerak» — keyingi aloqa sanasidan
/// bildirishnoma yasash qoidalari.
/// </summary>
/// <remarks>
/// <para>
/// Bu bo'lak nomzodlar ro'yxatidan OLIB TASHLANGAN «Keyingi aloqa»
/// ustunining o'rnini bosadi (mijoz talabi): sana yozilishda davom
/// etadi, ko'rsatadigan joy esa endi qo'ng'iroq.
/// </para>
/// <para>
/// Bu yerda faqat QATORDAN BILDIRISHNOMA yasash tekshiriladi. Kimning
/// qatori umuman kelishi (voronkadan chiqqanlar, rezerv) — bazadagi
/// so'rovning ishi va u <c>NotificationApiTests</c> dagi ikkita testda haqiqiy
/// PostgreSQL ustida tekshiriladi
/// (<c>Aloqa_eslatmasi_FAQAT_jarayondagi_va_rezervdagiga_chiqadi</c>,
/// <c>Rezervdagiga_SUHBAT_eslatmasi_chiqmaydi</c>).
/// </para>
/// </remarks>
public sealed class CandidateContactNotificationTests
{
    private readonly NotificationTestContext _context = new();

    // ────────────────────── Ikki tur, ikki daraja ──────────────────────

    /// <summary>
    /// Bugungi reja — «diqqat»: bu bugun bajariladigan ish, o'tkazib
    /// yuborilgan muddat emas.
    /// </summary>
    [Fact]
    public async Task Bugungi_aloqa_diqqat_darajasida_keladi()
    {
        var qator = _context.Contact(daysUntil: 0, name: "Yusupova Nilufar");

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        var item = natija.ShouldHaveSingleItem();
        item.Type.ShouldBe(NotificationType.CandidateContactDue);
        item.Severity.ShouldBe(NotificationSeverity.Warning);
        item.Date.ShouldBe(NotificationTestContext.Today);
        item.Params[NotificationParams.CandidateName].ShouldBe("Yusupova Nilufar");
        item.Link.Route.ShouldBe(NotificationRoute.Candidate);
        item.Link.Id.ShouldBe(qator.CandidateId);
        item.Key.ShouldBe(Kalit("nomzod.aloqa_bugun", qator.CandidateId, "2026-08-29"));
    }

    /// <summary>
    /// Kechikkan reja — «jiddiy». Bu HR ning O'ZI belgilagan va'da va
    /// uning o'tib ketishi tizimdagi eng oson yo'qoladigan ish: ustun
    /// olib tashlangandan keyin u hech qayerda qizarmaydi.
    /// </summary>
    [Fact]
    public async Task Kechikkan_aloqa_jiddiy_darajada_keladi()
    {
        var qator = _context.Contact(daysUntil: -3, name: "Ergashev Jasur");

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        var item = natija.ShouldHaveSingleItem();
        item.Type.ShouldBe(NotificationType.CandidateContactOverdue);
        item.Severity.ShouldBe(NotificationSeverity.Critical);
        item.Date.ShouldBe(NotificationTestContext.Today.AddDays(-3), "sana — REJALASHTIRILGAN kun, bugun emas");
        item.Params[NotificationParams.CandidateName].ShouldBe("Ergashev Jasur");
        item.Key.ShouldBe(Kalit("nomzod.aloqa_kechikkan", qator.CandidateId, "2026-08-26"));
    }

    /// <summary>
    /// ⭐ Ikkala tur O'ZARO INKOR: bitta nomzoddan bitta qator, ikkita
    /// yozuv emas. Sana yo bugun, yo o'tmishda — uchinchi holat bu yerga
    /// yetib kelmaydi.
    /// </summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, true)]
    [InlineData(-45, true)]
    public async Task Bitta_nomzod_ikkita_yozuv_bermaydi(int kunFarqi, bool kechikkan)
    {
        _context.Contact(daysUntil: kunFarqi);

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        var kutilgan = kechikkan
            ? NotificationType.CandidateContactOverdue
            : NotificationType.CandidateContactDue;

        var boshqasi = kechikkan
            ? NotificationType.CandidateContactDue
            : NotificationType.CandidateContactOverdue;

        natija.ShouldHaveSingleItem().Type.ShouldBe(kutilgan);
        natija.ShouldNotContain(item => item.Type == boshqasi);
    }

    /// <summary>
    /// Parametrda FAQAT ism. Sana alohida parametr bo'lib ketmaydi —
    /// u bildirishnomaning o'z sanasi (<c>Date</c>) va ikkinchi nusxa
    /// faqat ikkalasi ajralib ketishi uchun imkoniyat yaratardi.
    /// </summary>
    [Fact]
    public async Task Aloqa_parametrida_faqat_ism_boladi()
    {
        _context.Contact(daysUntil: 0, name: "Yusupova Nilufar");

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        var item = natija.ShouldHaveSingleItem();
        item.Params.Count.ShouldBe(1);
        item.Params.Keys.ShouldContain(NotificationParams.CandidateName);
        item.Params.ShouldNotContainKey(NotificationParams.InterviewTime);
    }

    /// <summary>Har bir nomzod — alohida yozuv, yig'ma «bugun 3 ta qo'ng'iroq» emas.</summary>
    [Fact]
    public async Task Har_bir_nomzod_alohida_yozuv_boladi()
    {
        _context.Contact(daysUntil: 0, name: "Birinchi Nomzod");
        _context.Contact(daysUntil: 0, name: "Ikkinchi Nomzod");
        _context.Contact(daysUntil: -2, name: "Uchinchi Nomzod");

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        natija.Count.ShouldBe(3);
        natija.Select(item => item.Key).Distinct(StringComparer.Ordinal).Count().ShouldBe(3);
        natija.Select(item => item.Params[NotificationParams.CandidateName]).ShouldBe(
            ["Birinchi Nomzod", "Ikkinchi Nomzod", "Uchinchi Nomzod"],
            ignoreOrder: true);
    }

    // ────────────────────── So'rovning chegarasi ──────────────────────

    /// <summary>
    /// Yuqori chegara — BUGUN: ertangi rejalar qo'ng'iroqqa tushmaydi.
    /// Chegara bazada qo'llanadi, shu sababli bu yerda UZATILGAN qiymat
    /// tekshiriladi — boshqa yo'l bilan uni ko'rsatib bo'lmaydi.
    /// </summary>
    [Fact]
    public async Task Aloqa_sorovi_bugungacha_soraladi()
    {
        await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        _context.Source.LastContactUntil.ShouldBe(NotificationTestContext.Today);
    }

    // ────────────────────── Ruxsat ──────────────────────

    /// <summary>
    /// <c>recruiting.view</c> yo'q odam nomzod ismini qo'ng'iroq ostidan
    /// o'qiy olmasligi kerak — foydalanuvchining aniq talabi.
    /// </summary>
    [Fact]
    public async Task Rekruting_ruxsatisiz_aloqa_eslatmasi_kelmaydi()
    {
        _context.Contact(daysUntil: 0, name: "Yusupova Nilufar");
        _context.Contact(daysUntil: -5, name: "Ergashev Jasur");
        _context.Birthday(NotificationTestContext.Today, "Karimov Aziz");

        _context.Deny(Permissions.RecruitingView);

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        natija.ShouldHaveSingleItem().Type.ShouldBe(NotificationType.BirthdayToday);
        natija.ShouldNotContain(item => item.Params.ContainsKey(NotificationParams.CandidateName));
    }

    // ────────────────────── Tartib va barqarorlik ──────────────────────

    /// <summary>
    /// Ikkalasi ham SODIR bo'lgan guruhda va u yerda yangisi tepada:
    /// «bugun qo'ng'iroq qilish kerak» eski qarzdan yuqorida turadi.
    /// </summary>
    [Fact]
    public async Task Bugungi_aloqa_kechikkanidan_tepada()
    {
        _context.Contact(daysUntil: -10, name: "Eski Qarz");
        _context.Contact(daysUntil: 0, name: "Bugungi Reja");

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        natija.Select(item => item.Params[NotificationParams.CandidateName])
            .ShouldBe(["Bugungi Reja", "Eski Qarz"]);
    }

    /// <summary>
    /// Kalit ikki hisoblashda bir xil chiqishi shart — «o'qildi» belgisi
    /// faqat shu orqali yashaydi.
    /// </summary>
    [Fact]
    public async Task Aloqa_kaliti_qayta_hisoblashda_ozgarmaydi()
    {
        _context.Contact(daysUntil: 0);
        _context.Contact(daysUntil: -4);

        var birinchi = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);
        var ikkinchi = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        birinchi.Count.ShouldBe(2);
        ikkinchi.Select(item => item.Key).ShouldBe(birinchi.Select(item => item.Key));
    }

    /// <summary>Kalit satri — prefiks, nomzod, sana.</summary>
    private static string Kalit(string prefiks, Guid id, string sana)
        => string.Concat(prefiks, ":", id.ToString("D", CultureInfo.InvariantCulture), ":", sana);
}
