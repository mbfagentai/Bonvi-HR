using Bonvi.Application.Notifications;
using Bonvi.Application.Notifications.Dtos;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Notifications;

namespace Bonvi.Application.Tests.Notifications;

/// <summary>
/// Shartlarni bildirishnomaga aylantirish qoidalari.
/// </summary>
/// <remarks>
/// Bu yerda baza ham, foydalanuvchi holati ham yo'q: faqat «qanday
/// ma'lumotdan qanday bildirishnoma chiqadi» degan savol tekshiriladi.
/// </remarks>
public sealed class NotificationCollectorTests
{
    private readonly NotificationTestContext _context = new();

    // ────────────────────── Sinov muddati: uch xil holat ──────────────────────

    [Fact]
    public async Task Bir_necha_kun_qolganda_tugayapti_deb_belgilanadi()
    {
        _context.Probation(daysLeft: 5);

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        var item = natija.ShouldHaveSingleItem();
        item.Type.ShouldBe(NotificationType.ProbationEndingSoon);
        item.Severity.ShouldBe(NotificationSeverity.Warning);
        item.Params[NotificationParams.DaysLeft].ShouldBe("5");
        item.Params.ShouldNotContainKey(NotificationParams.DaysOverdue);
    }

    /// <summary>
    /// Oxirgi kun alohida tur beradi, lekin «qolgan kun» parametri
    /// baribir keladi — nol bo'lib. Qoida barcha muddatlarda bir xil:
    /// manfiy bo'lmasa «daysLeft», bo'lsa «daysOverdue».
    /// </summary>
    [Fact]
    public async Task Oxirgi_kun_alohida_tur_beradi()
    {
        _context.Probation(daysLeft: 0);

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        var item = natija.ShouldHaveSingleItem();
        item.Type.ShouldBe(NotificationType.ProbationEndsToday);
        item.Params[NotificationParams.DaysLeft].ShouldBe("0");
        item.Params.ShouldNotContainKey(NotificationParams.DaysOverdue);
    }

    /// <summary>
    /// Muddati o'tib ketgan, lekin yakunlanmagan sinov — eng jiddiy holat:
    /// u hech qanday ro'yxatda o'z-o'zidan qizarmaydi.
    /// </summary>
    [Fact]
    public async Task Muddati_otib_ketgan_sinov_jiddiy_deb_belgilanadi()
    {
        _context.Probation(daysLeft: -3);

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        var item = natija.ShouldHaveSingleItem();
        item.Type.ShouldBe(NotificationType.ProbationOverdue);
        item.Severity.ShouldBe(NotificationSeverity.Critical);
        item.Params[NotificationParams.DaysOverdue].ShouldBe("3", "ishorani matn tashiydi, son emas");
    }

    [Fact]
    public async Task Sinov_bildirishnomasi_sinov_royxatiga_olib_boradi()
    {
        var row = _context.Probation(daysLeft: 2);

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        var item = natija.ShouldHaveSingleItem();
        item.Link.Route.ShouldBe(NotificationRoute.Probations);
        item.Link.Id.ShouldBe(row.ProbationId);
    }

    [Fact]
    public async Task Sinov_yozuvisiz_qolgan_munosabat_oz_royxatiga_olib_boradi()
    {
        var row = _context.MissingProbation();

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        var item = natija.ShouldHaveSingleItem();
        item.Type.ShouldBe(NotificationType.ProbationMissing);
        item.Link.Route.ShouldBe(NotificationRoute.MissingProbations);
        item.Link.Id.ShouldBe(row.EmploymentId);
        item.Params[NotificationParams.DaysWorked].ShouldBe("400");
    }

    // ────────────────────── Tug'ilgan kunlar ──────────────────────

    /// <summary>
    /// Foydalanuvchining ANIQ talabi: yig'ma «bugun 3 ta tug'ilgan kun»
    /// emas, har bir xodim uchun alohida yozuv. Aks holda bittasini
    /// o'qish uchalasini ham yopib qo'yardi.
    /// </summary>
    [Fact]
    public async Task Har_bir_tugilgan_kun_alohida_yozuv_boladi()
    {
        _context.Birthday(NotificationTestContext.Today, "Karimov Aziz");
        _context.Birthday(NotificationTestContext.Today, "Rahimova Dilnoza");
        _context.Birthday(NotificationTestContext.Today, "Toshmatov Olim");

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        natija.Count.ShouldBe(3);
        natija.Select(item => item.Key).Distinct(StringComparer.Ordinal).Count().ShouldBe(3);
        natija.ShouldAllBe(item => item.Type == NotificationType.BirthdayToday);
    }

    [Fact]
    public async Task Ertangi_tugilgan_kun_boshqa_tur_beradi()
    {
        _context.Birthday(NotificationTestContext.Today.AddDays(1), "Rahimova Dilnoza");

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        var item = natija.ShouldHaveSingleItem();
        item.Type.ShouldBe(NotificationType.BirthdayTomorrow);
        item.Date.ShouldBe(NotificationTestContext.Today.AddDays(1));
    }

    /// <summary>
    /// Bitta xodimning tug'ilgan kuni ketma-ket ikki kunda ko'rinadi
    /// («ertaga» → «bugun»). Ular ALOHIDA kalit olishi kerak: bugun
    /// o'qilgan ogohlantirish ertangi tabrikni yopib qo'ymasin.
    /// </summary>
    [Fact]
    public async Task Bugungi_va_ertangi_tugilgan_kun_kalitlari_aralashmaydi()
    {
        var ertangi = NotificationTestContext.Today.AddDays(1);
        var row = _context.Birthday(ertangi, "Rahimova Dilnoza");

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        var kalit = natija.ShouldHaveSingleItem().Key;

        kalit.ShouldBe(NotificationKey.For(NotificationType.BirthdayTomorrow, row.EmployeeId, ertangi));
        kalit.ShouldNotBe(NotificationKey.For(NotificationType.BirthdayToday, row.EmployeeId, ertangi));
    }

    [Fact]
    public async Task Yosh_hisoblanadi_va_xodim_kartasiga_havola_beriladi()
    {
        var row = _context.Birthday(NotificationTestContext.Today, "Karimov Aziz", age: 42);

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        var item = natija.ShouldHaveSingleItem();
        item.Params[NotificationParams.Age].ShouldBe("42");
        item.Link.Route.ShouldBe(NotificationRoute.Employee);
        item.Link.Id.ShouldBe(row.EmployeeId);
    }

    /// <summary>
    /// Excel'dan ko'chirilgan yozuvlarda shubhali tug'ilgan sanalar
    /// uchraydi. «136 yoshga to'ldi» degan tabrik ma'lumotga bo'lgan
    /// ishonchni yo'qotardi — yosh umuman berilmaydi.
    /// </summary>
    [Fact]
    public async Task Shubhali_sanada_yosh_berilmaydi()
    {
        _context.Birthday(NotificationTestContext.Today, "Noma'lum Shaxs", age: 150);

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        natija.ShouldHaveSingleItem().Params.ShouldNotContainKey(NotificationParams.Age);
    }

    // ────────────────────── Rekruting ──────────────────────

    [Fact]
    public async Task Yangi_nomzod_nomzod_kartasiga_olib_boradi()
    {
        var row = _context.Candidate();

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        var item = natija.ShouldHaveSingleItem();
        item.Type.ShouldBe(NotificationType.CandidateNew);
        item.Severity.ShouldBe(NotificationSeverity.Info);
        item.Link.Route.ShouldBe(NotificationRoute.Candidate);
        item.Link.Id.ShouldBe(row.CandidateId);
        item.Params[NotificationParams.Source].ShouldBe("Manual");
    }

    [Fact]
    public async Task Taklif_muddati_otib_ketsa_jiddiy_boladi()
    {
        _context.Offer(daysLeft: -1);

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        var item = natija.ShouldHaveSingleItem();
        item.Type.ShouldBe(NotificationType.JobOfferExpired);
        item.Severity.ShouldBe(NotificationSeverity.Critical);
        item.Params[NotificationParams.DaysOverdue].ShouldBe("1");
    }

    [Fact]
    public async Task Taklif_muddati_yaqinlashsa_diqqat_boladi()
    {
        _context.Offer(daysLeft: 2);

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        var item = natija.ShouldHaveSingleItem();
        item.Type.ShouldBe(NotificationType.JobOfferExpiring);
        item.Severity.ShouldBe(NotificationSeverity.Warning);
        item.Params[NotificationParams.DaysLeft].ShouldBe("2");
    }

    // ────────────────────── Ruxsat ──────────────────────

    /// <summary>
    /// Foydalanuvchining aniq talabi: <c>recruiting.view</c> yo'q odamga
    /// nomzod bildirishnomasi KO'RINMASIN.
    /// </summary>
    [Fact]
    public async Task Rekruting_ruxsatisiz_nomzod_va_taklif_korinmaydi()
    {
        _context.Candidate();
        _context.Offer(daysLeft: 1);
        _context.Birthday(NotificationTestContext.Today, "Karimov Aziz");

        _context.Deny(Permissions.RecruitingView);

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        natija.ShouldHaveSingleItem().Type.ShouldBe(NotificationType.BirthdayToday);
    }

    /// <summary>
    /// Ruxsat yo'q bo'lsa, baza SO'ROVI ham ketmasligi kerak: natijani
    /// keyin filtrlash ham ishlardi, lekin u holda ruxsatsiz odam uchun
    /// ham baza ishlab turardi.
    /// </summary>
    [Fact]
    public async Task Ruxsatsiz_manba_sorovi_umuman_yuborilmaydi()
    {
        _context.Deny(Permissions.RecruitingView);

        await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        _context.Source.CandidateCalls.ShouldBe(0);
        _context.Source.ProbationCalls.ShouldBe(1, "mehnat munosabati ruxsati joyida");
    }

    [Fact]
    public async Task Mehnat_ruxsatisiz_sinov_bildirishnomalari_korinmaydi()
    {
        _context.Probation(daysLeft: 1);
        _context.MissingProbation();
        _context.Birthday(NotificationTestContext.Today, "Karimov Aziz");

        _context.Deny(Permissions.EmploymentView);

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        natija.ShouldHaveSingleItem().Type.ShouldBe(NotificationType.BirthdayToday);
    }

    [Fact]
    public async Task Xodimlar_ruxsatisiz_tugilgan_kun_korinmaydi()
    {
        _context.Birthday(NotificationTestContext.Today, "Karimov Aziz");
        _context.Probation(daysLeft: 1);

        _context.Deny(Permissions.EmployeesView);

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        natija.ShouldHaveSingleItem().Type.ShouldBe(NotificationType.ProbationEndingSoon);
    }

    /// <summary>
    /// Oklad ALOHIDA ruxsat ostida: rekrutingni ko'rish huquqi taklif
    /// summasini ko'rish huquqini bermaydi.
    /// </summary>
    [Fact]
    public async Task Oklad_ruxsatisiz_taklif_summasi_berilmaydi()
    {
        _context.Offer(daysLeft: 1, salary: 7_000_000m);
        _context.Deny(Permissions.SalaryView);

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        var item = natija.ShouldHaveSingleItem();
        item.Params.ShouldNotContainKey(NotificationParams.SalaryAmount);
        item.Params.ShouldNotContainKey(NotificationParams.SalaryCurrency);
        item.Type.ShouldBe(NotificationType.JobOfferExpiring, "bildirishnomaning o'zi ko'rinadi");
    }

    [Fact]
    public async Task Oklad_ruxsati_bilan_summa_beriladi()
    {
        _context.Offer(daysLeft: 1, salary: 7_000_000m);

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        var item = natija.ShouldHaveSingleItem();
        item.Params[NotificationParams.SalaryAmount].ShouldBe("7000000");
        item.Params[NotificationParams.SalaryCurrency].ShouldBe("UZS");
    }

    // ────────────────────── Kalit barqarorligi va tartib ──────────────────────

    /// <summary>
    /// ⭐ Butun bo'limning poydevori: ikki marta hisoblansa, kalitlar
    /// AYNAN bir xil bo'lishi kerak. Aks holda «o'qildi» belgisi har
    /// safar yo'qolardi.
    /// </summary>
    [Fact]
    public async Task Ikki_marta_hisoblansa_kalitlar_bir_xil_boladi()
    {
        _context.Probation(daysLeft: 3);
        _context.Birthday(NotificationTestContext.Today, "Karimov Aziz");
        _context.Candidate();
        _context.Offer(daysLeft: 1);
        _context.MissingProbation();

        var birinchi = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);
        var ikkinchi = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        birinchi.Count.ShouldBe(5);
        ikkinchi.Select(item => item.Key).ShouldBe(birinchi.Select(item => item.Key));
    }

    /// <summary>
    /// Avval SODIR BO'LGANI, keyin KELAYOTGANI.
    /// </summary>
    /// <remarks>
    /// Ilgari birinchi mezon MUHIMLIK edi, endi VAQT (foydalanuvchi
    /// qarori). Sana ikki xil ma'no tashiydi — «bugun sodir bo'ldi» va
    /// «falon kuni tugaydi» — shu sababli ular ikki guruhga bo'linadi:
    /// bo'lmasa kelajakdagi muddat bugungi hodisadan tepaga chiqib
    /// ketardi (sabab `NotificationCollector` dagi tartib izohida).
    ///
    /// Muhimlik yo'qolmadi: u endi RANG bilan aytiladi
    /// (`notification-presentation.ts` dagi `unreadRow`).
    /// </remarks>
    [Fact]
    public async Task Sodir_bolgani_kelayotganidan_tepada()
    {
        _context.Birthday(NotificationTestContext.Today, "Karimov Aziz");
        _context.Probation(daysLeft: 4, name: "Kelayotgan Muddat");
        _context.Probation(daysLeft: -10, name: "Otib Ketgan");

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        // Bugun (tug'ilgan kun) va o'tib ketgan muddat — SODIR bo'lgan,
        // yangisi tepada. Kelajakdagi muddat esa oxirida.
        natija.Select(item => item.Type).ShouldBe(
        [
            NotificationType.BirthdayToday,
            NotificationType.ProbationOverdue,
            NotificationType.ProbationEndingSoon,
        ]);
    }

    /// <summary>Teng muhimlikda eng yaqin muddat tepada — «kimga bugun ulgurish kerak».</summary>
    [Fact]
    public async Task Teng_muhimlikda_yaqin_muddat_tepada()
    {
        _context.Probation(daysLeft: 6, name: "Uzoq Muddat");
        _context.Probation(daysLeft: 1, name: "Yaqin Muddat");

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        natija[0].Params[NotificationParams.EmployeeName].ShouldBe("Yaqin Muddat");
    }
}
