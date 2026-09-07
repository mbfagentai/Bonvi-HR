using Bonvi.Domain.Identity;
using Bonvi.Domain.Notifications;

namespace Bonvi.Domain.Tests.Notifications;

/// <summary>
/// Bildirishnoma kalitining BARQARORLIGI.
/// </summary>
/// <remarks>
/// Bu testlar butun bo'limning poydevorini himoyalaydi. Bildirishnomalar
/// bazada saqlanmaydi — ular har so'rovda qaytadan hisoblanadi, va
/// «bu o'sha bildirishnoma» degan tushuncha FAQAT kalit orqali yashaydi.
/// Kalit barqaror bo'lmasa, «o'qildi» belgisi jimgina yo'qolardi va
/// foydalanuvchi bir xil narsani qayta-qayta ko'rardi — hech qanday xato
/// chiqmagan holda.
/// </remarks>
public sealed class NotificationKeyTests
{
    private static readonly Guid Id = Guid.Parse("0199c0de-1234-7abc-8def-0123456789ab");
    private static readonly DateOnly Sana = new(2026, 8, 30);

    // ────────────────────── Barqarorlik ──────────────────────

    [Fact]
    public void Bir_xil_malumotdan_bir_xil_kalit_chiqadi()
    {
        var birinchi = NotificationKey.For(NotificationType.CandidateNew, Id);
        var ikkinchi = NotificationKey.For(NotificationType.CandidateNew, Id);

        ikkinchi.ShouldBe(birinchi, "kalit faqat ma'lumotdan hosil bo'lishi kerak");
    }

    [Fact]
    public void Sanali_kalit_ham_takrorlanadi()
    {
        var birinchi = NotificationKey.For(NotificationType.ProbationEndingSoon, Id, Sana);
        var ikkinchi = NotificationKey.For(NotificationType.ProbationEndingSoon, Id, Sana);

        ikkinchi.ShouldBe(birinchi);
    }

    [Fact]
    public void Kalit_kutilgan_korinishda_yoziladi()
    {
        NotificationKey.For(NotificationType.CandidateNew, Id)
            .ShouldBe("nomzod.yangi:0199c0de-1234-7abc-8def-0123456789ab");

        NotificationKey.For(NotificationType.BirthdayToday, Id, Sana)
            .ShouldBe("tugilgan_kun.bugun:0199c0de-1234-7abc-8def-0123456789ab:2026-08-30");
    }

    /// <summary>
    /// Sana ISO ko'rinishida bo'lishi shart: server mintaqa sozlamasi
    /// o'zgarganda kalit o'zgarsa, hamma «o'qildi» belgisi yo'qolardi.
    /// </summary>
    [Fact]
    public void Sana_madaniyatga_boglanmaydi()
    {
        var oldingi = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("ru-RU");

            NotificationKey.For(NotificationType.BirthdayToday, Id, Sana)
                .ShouldEndWith("2026-08-30");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = oldingi;
        }
    }

    // ────────────────────── Turlar bir-biriga aralashmaydi ──────────────────────

    /// <summary>
    /// Bir xil xodim va bir xil sana — lekin «bugun» va «ertaga» BOSHQA
    /// bildirishnoma. Ertangi tug'ilgan kunni bugun o'qigan HR ertaga
    /// kelib, xuddi shu odam haqidagi «bugun» xabarini YANGI holida
    /// ko'rishi kerak.
    /// </summary>
    [Fact]
    public void Bugungi_va_ertangi_tugilgan_kun_kalitlari_farq_qiladi()
    {
        var bugun = NotificationKey.For(NotificationType.BirthdayToday, Id, Sana);
        var ertaga = NotificationKey.For(NotificationType.BirthdayTomorrow, Id, Sana);

        ertaga.ShouldNotBe(bugun);
    }

    /// <summary>Har bir turning prefiksi noyob — aks holda ikki tur bitta yozuvga tushardi.</summary>
    [Fact]
    public void Har_bir_turning_prefiksi_noyob()
    {
        var turlar = Enum.GetValues<NotificationType>();

        var prefikslar = turlar.Select(NotificationKey.Prefix).ToList();

        prefikslar.Distinct(StringComparer.Ordinal).Count().ShouldBe(turlar.Length);
    }

    /// <summary>
    /// Tanish prefiks bilan BOSHLANADIGAN begona kalit tur bermaydi.
    /// </summary>
    /// <remarks>
    /// Tur aniqlash <c>StartsWith</c> bilan yozilsa, «nomzod.yangi_arxiv»
    /// degan begona kalit jimgina «yangi nomzod» bo'lib chiqardi. Ilgari
    /// bunday juftlik tizimda HAQIQATAN bor edi («taklif.muddati» va
    /// «taklif.muddati_otdi»); ish taklifi o'chirilgach u qolmadi, lekin
    /// qoida o'zgarmaydi — kalit AVVAL ajratgich bo'yicha bo'linadi va
    /// birinchi bo'lak TO'LIQ solishtiriladi.
    /// </remarks>
    [Fact]
    public void Tanish_prefiks_bilan_boshlanadigan_begona_kalit_qabul_qilinmaydi()
    {
        var prefiks = NotificationKey.Prefix(NotificationType.CandidateNew);

        NotificationKey.TypeOf($"{prefiks}_arxiv{NotificationKey.Separator}{Id:D}")
            .ShouldBeNull("prefiks TO'LIQ solishtirilishi kerak");

        NotificationKey.TypeOf($"{prefiks}{NotificationKey.Separator}{Id:D}")
            .ShouldBe(NotificationType.CandidateNew);
    }

    [Fact]
    public void Har_bir_turdagi_kalit_oz_turiga_qaytadi()
    {
        foreach (var tur in Enum.GetValues<NotificationType>())
        {
            NotificationKey.TypeOf(NotificationKey.For(tur, Id, Sana)).ShouldBe(tur);
            NotificationKey.TypeOf(NotificationKey.For(tur, Id)).ShouldBe(tur);
        }
    }

    // ────────────────────── Tekshiruv ──────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ajratgichsiz")]
    [InlineData(":boshida_ajratgich")]
    [InlineData("nomalum.tur:0199c0de-1234-7abc-8def-0123456789ab")]
    public void Notogri_kalit_rad_etiladi(string? kalit)
    {
        NotificationKey.IsValid(kalit).ShouldBeFalse();
    }

    [Fact]
    public void Juda_uzun_kalit_rad_etiladi()
    {
        var uzun = "nomzod.yangi:" + new string('x', NotificationKey.MaxLength);

        NotificationKey.IsValid(uzun).ShouldBeFalse("uzun satr bazaga tushmasligi kerak");
    }

    // ────────────────────── Ruxsat jadvali ──────────────────────

    /// <summary>
    /// Har bir tur uchun ruxsat belgilangan bo'lishi SHART. Yangi tur
    /// qo'shib, ruxsatini yozmagan odam shu testda to'xtaydi — aks holda
    /// yozuv ishlab turgan tizimda istisno bo'lib chiqardi.
    /// </summary>
    [Fact]
    public void Har_bir_tur_uchun_ruxsat_belgilangan()
    {
        foreach (var tur in Enum.GetValues<NotificationType>())
        {
            var ruxsat = NotificationAccess.RequiredPermission(tur);

            Permissions.Exists(ruxsat).ShouldBeTrue($"«{ruxsat}» tizimda mavjud ruxsat bo'lishi kerak");
        }
    }

    [Fact]
    public void Nomzod_bildirishnomalari_rekruting_ruxsati_ostida()
    {
        NotificationAccess.RequiredPermission(NotificationType.CandidateNew)
            .ShouldBe(Permissions.RecruitingView);

        NotificationAccess.RequiredPermission(NotificationType.CandidateFromBot)
            .ShouldBe(Permissions.RecruitingView);
    }
}
