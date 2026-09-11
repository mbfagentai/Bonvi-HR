using Bonvi.Domain.Identity;
using Bonvi.Domain.Notifications;

namespace Bonvi.Domain.Tests.Notifications;

/// <summary>
/// Nomzod ESLATMALARINING kalit shartnomasi: aloqa (bugun / kechikkan)
/// va suhbat.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega bu uchtasi alohida faylda.</b> <c>NotificationKeyTests</c>
/// kalitning UMUMIY qoidalarini tekshiradi (barqarorlik, noyoblik,
/// tekshiruv). Bu yerda esa aniq UCHTA turning yozma shartnomasi
/// qadalgan: prefiks satri, sanoq qiymati va — eng muhimi — LAHZAning
/// kalitga qanday yozilishi.
/// </para>
/// <para>
/// ⭐ Suhbat kaliti Toshkent mintaqasida yoziladi. Bu qoida buzilsa
/// hech qanday xato chiqmaydi: kalit baribir barqaror bo'lib qolaveradi
/// va faqat kechqurungi suhbatlarda — kun chegarasidan o'tganda —
/// eski «o'qildi» belgilari boshqa kunga tegib ketardi. Shu sababli
/// kun chegarasi aniq son bilan qadalgan.
/// </para>
/// </remarks>
public sealed class CandidateNotificationKeyTests
{
    private static readonly Guid Id = Guid.Parse("0199c0de-1234-7abc-8def-0123456789ab");

    // ────────────────────── Prefikslar va qiymatlar ──────────────────────

    /// <summary>
    /// Prefikslar bazada va loglarda OCHIQ yotadi va ular
    /// bildirishnomaning «o'qildi» yozuvi bilan birga saqlanadi.
    /// O'zgartirilsa, saqlangan holatlar egasiz qolardi — ya'ni HR
    /// allaqachon o'qigan eslatmalar birdan qaytib kelardi.
    /// </summary>
    [Fact]
    public void Uch_turning_prefiksi_shartnomada_kelishilgan()
    {
        NotificationKey.Prefix(NotificationType.CandidateContactDue).ShouldBe("nomzod.aloqa_bugun");
        NotificationKey.Prefix(NotificationType.CandidateContactOverdue).ShouldBe("nomzod.aloqa_kechikkan");
        NotificationKey.Prefix(NotificationType.CandidateInterviewSoon).ShouldBe("nomzod.suhbat_yaqin");
    }

    /// <summary>
    /// Sanoq qiymatlari — shartnomaning bir qismi: yangi tur OXIRIGA
    /// qo'shiladi, o'rtaga emas. Aks holda mavjud qiymatlar jimgina
    /// siljib ketardi.
    /// </summary>
    [Fact]
    public void Uch_turning_qiymati_siljimaydi()
    {
        ((int)NotificationType.CandidateContactDue).ShouldBe(11);
        ((int)NotificationType.CandidateContactOverdue).ShouldBe(12);
        ((int)NotificationType.CandidateInterviewSoon).ShouldBe(13);
    }

    /// <summary>
    /// Uchalasi ham nomzod kartasining MAZMUNINI ko'rsatadi (ism, sana,
    /// vaqt) — demak nomzodlar ruxsati ostida. Boshqa ruxsatga
    /// qo'yilsa, ochib bo'lmaydigan sahifaga yetaklaydigan yozuv paydo
    /// bo'lardi.
    /// </summary>
    [Fact]
    public void Uch_tur_ham_rekruting_koruvchisiga_tegishli()
    {
        NotificationAccess.RequiredPermission(NotificationType.CandidateContactDue)
            .ShouldBe(Permissions.RecruitingView);

        NotificationAccess.RequiredPermission(NotificationType.CandidateContactOverdue)
            .ShouldBe(Permissions.RecruitingView);

        NotificationAccess.RequiredPermission(NotificationType.CandidateInterviewSoon)
            .ShouldBe(Permissions.RecruitingView);
    }

    // ────────────────────── Aloqa: sana kalitda ──────────────────────

    [Fact]
    public void Aloqa_kaliti_kutilgan_korinishda_yoziladi()
    {
        var sana = new DateOnly(2026, 9, 11);

        NotificationKey.For(NotificationType.CandidateContactDue, Id, sana)
            .ShouldBe("nomzod.aloqa_bugun:0199c0de-1234-7abc-8def-0123456789ab:2026-09-11");

        NotificationKey.For(NotificationType.CandidateContactOverdue, Id, sana)
            .ShouldBe("nomzod.aloqa_kechikkan:0199c0de-1234-7abc-8def-0123456789ab:2026-09-11");
    }

    /// <summary>
    /// HR aloqani boshqa kunga ko'chirsa, bu YANGI reja: eski
    /// eslatmaning «o'qildi» belgisi unga o'tmasligi kerak.
    /// </summary>
    [Fact]
    public void Aloqa_sanasi_kochirilsa_kalit_ozgaradi()
    {
        var eski = NotificationKey.For(
            NotificationType.CandidateContactDue, Id, new DateOnly(2026, 9, 11));

        var yangi = NotificationKey.For(
            NotificationType.CandidateContactDue, Id, new DateOnly(2026, 9, 12));

        yangi.ShouldNotBe(eski);
    }

    // ────────────────────── Suhbat: LAHZA kalitda ──────────────────────

    /// <summary>
    /// ⭐ Kalitdagi vaqt TOSHKENT mintaqasida. Kechqurungi UTC lahzasi
    /// keyingi kunga o'tadi va kalitdagi SANA ham shu bilan birga
    /// siljiydi.
    /// </summary>
    [Fact]
    public void Suhbat_kaliti_Toshkent_kunini_oladi()
    {
        // UTC 19:30 → Toshkentda ERTASI kun soat 00:30.
        var kechqurun = new DateTimeOffset(2026, 9, 11, 19, 30, 0, TimeSpan.Zero);

        NotificationKey.For(NotificationType.CandidateInterviewSoon, Id, kechqurun)
            .ShouldBe("nomzod.suhbat_yaqin:0199c0de-1234-7abc-8def-0123456789ab:2026-09-12T00:30");
    }

    /// <summary>UTC yarim tuni — Toshkentda o'sha kunning ertalabki 05:00 i.</summary>
    [Fact]
    public void Yarim_tundagi_UTC_lahzasi_ertalabki_soatga_tushadi()
    {
        var yarimTun = new DateTimeOffset(2026, 9, 11, 0, 0, 0, TimeSpan.Zero);

        NotificationKey.For(NotificationType.CandidateInterviewSoon, Id, yarimTun)
            .ShouldBe("nomzod.suhbat_yaqin:0199c0de-1234-7abc-8def-0123456789ab:2026-09-11T05:00");
    }

    /// <summary>
    /// Bir XIL lahza turli siljishlarda berilsa ham kalit bitta bo'ladi:
    /// bazadan UTC keladi, use-case'da esa qiymat boshqa siljishda
    /// yasalishi mumkin.
    /// </summary>
    [Fact]
    public void Siljish_kalitga_tasir_qilmaydi()
    {
        var utc = new DateTimeOffset(2026, 9, 11, 9, 15, 0, TimeSpan.Zero);
        var toshkent = new DateTimeOffset(2026, 9, 11, 14, 15, 0, TimeSpan.FromHours(5));

        NotificationKey.For(NotificationType.CandidateInterviewSoon, Id, toshkent)
            .ShouldBe(NotificationKey.For(NotificationType.CandidateInterviewSoon, Id, utc));
    }

    /// <summary>
    /// Suhbat O'SHA KUN ichida ko'chirilsa ham kalit yangilanadi: nomzod
    /// yangi vaqt haqida xabar oladi, HR esa ertalab «o'qildi» qilgan
    /// yozuvi tufayli ogohlantirishsiz qolmasligi kerak.
    /// </summary>
    [Fact]
    public void Suhbat_vaqti_kochirilsa_kalit_ozgaradi()
    {
        var ertalab = NotificationKey.For(
            NotificationType.CandidateInterviewSoon,
            Id,
            new DateTimeOffset(2026, 9, 11, 5, 0, 0, TimeSpan.Zero));

        var kunduzi = NotificationKey.For(
            NotificationType.CandidateInterviewSoon,
            Id,
            new DateTimeOffset(2026, 9, 11, 10, 0, 0, TimeSpan.Zero));

        kunduzi.ShouldNotBe(ertalab);
    }

    /// <summary>
    /// Aniqlik — DAQIQA: sekundlar kesiladi. Aks holda kalit ekranda
    /// umuman ko'rinmaydigan qismga bog'lanib qolardi.
    /// </summary>
    [Fact]
    public void Sekundlar_kalitga_tushmaydi()
    {
        var sekundsiz = new DateTimeOffset(2026, 9, 11, 10, 0, 0, TimeSpan.Zero);
        var sekundli = new DateTimeOffset(2026, 9, 11, 10, 0, 47, TimeSpan.Zero);

        NotificationKey.For(NotificationType.CandidateInterviewSoon, Id, sekundli)
            .ShouldBe(NotificationKey.For(NotificationType.CandidateInterviewSoon, Id, sekundsiz));
    }

    /// <summary>
    /// Kalit o'z turiga qaytishi kerak — mijoz uni «o'qildi» so'rovida
    /// qaytarib yuboradi va u yerda AVVAL turga ajratiladi.
    /// </summary>
    [Fact]
    public void Uch_turning_kaliti_oz_turiga_qaytadi()
    {
        var sana = new DateOnly(2026, 9, 11);
        var lahza = new DateTimeOffset(2026, 9, 11, 10, 0, 0, TimeSpan.Zero);

        NotificationKey.TypeOf(NotificationKey.For(NotificationType.CandidateContactDue, Id, sana))
            .ShouldBe(NotificationType.CandidateContactDue);

        NotificationKey.TypeOf(NotificationKey.For(NotificationType.CandidateContactOverdue, Id, sana))
            .ShouldBe(NotificationType.CandidateContactOverdue);

        NotificationKey.TypeOf(NotificationKey.For(NotificationType.CandidateInterviewSoon, Id, lahza))
            .ShouldBe(NotificationType.CandidateInterviewSoon);
    }
}
