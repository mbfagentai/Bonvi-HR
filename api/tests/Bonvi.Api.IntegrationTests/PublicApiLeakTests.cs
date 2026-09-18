using System.Globalization;
using System.Net;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// ⭐ <b>Modulning ASOSIY testi: maydon sizib chiqishi.</b>
/// </summary>
/// <remarks>
/// <para>
/// To'liq to'ldirilgan xodim — oklad, JSHSHIR, pasport, manzil,
/// sudlanganlik, ko'rib chiqish izohi, mehnat.uz havolasi — ommaviy
/// endpoint orqali seriyalanadi va <b>XOM JSON</b> ichida o'sha
/// qiymatlar ham, maydon NOMLARI ham yo'qligi tekshiriladi.
/// </para>
/// <para>
/// ⚠️ <b>Nega DTO shakli emas, xom matn.</b> DTO'ni tekshiruv «bugungi
/// tip» ni ko'radi: kimdir ertaga <c>PublicEmployeeDto</c> ga maydon
/// qo'shsa, tip bo'yicha yozilgan tekshiruv u haqda hech narsa demasdi.
/// Javob matni esa shartnomaning O'ZI — u yerda paydo bo'lgan har qanday
/// ortiqcha so'z darhol ko'rinadi. Chetlab o'tish yo'li yo'q.
/// </para>
/// <para>
/// Bu test <b>birinchi</b> yozilgan va modulning mavjud bo'lish sababi
/// shu (SPEC §1, §9). U yiqilsa — xodimlarning shaxsiy ma'lumoti
/// BonviHR nazorat qilmaydigan bazalarga ko'chib o'tgan bo'ladi.
/// </para>
/// </remarks>
public sealed class PublicApiLeakTests : IClassFixture<BonviApiFactory>
{
    /// <summary>
    /// Javobda HECH QACHON uchramasligi kerak bo'lgan MAYDON NOMLARI.
    /// </summary>
    /// <remarks>
    /// Ro'yxat SPEC §5 ning oxirgi bandidan so'zma-so'z olingan. Taqqoslash
    /// registrga sezgir EMAS: JSON'da maydonlar <c>camelCase</c> da keladi,
    /// lekin seriyalash siyosati o'zgarsa ham test ishlashda davom etishi
    /// kerak.
    /// </remarks>
    private static readonly string[] TaqiqlanganNomlar =
    [
        "pinfl",
        "passport",
        "address",
        "salary",
        "speciality",
        "criminalRecord",
        "hasCriminalRecord",
        "criminalRecordNote",
        "mehnatUz",
        "needsReview",
        "reviewNote",
        "secondPhone",
        "flags",
    ];

    private readonly BonviApiFactory _factory;

    public PublicApiLeakTests(BonviApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Ommaviy_javobda_shaxsiy_maydonlar_YOQ()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        var sirlar = await ToliqXodimYaratAsync().ConfigureAwait(true);

        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        var kalit = await PublicApiHarness.KalitYaratAsync(
            admin,
            PublicApiHarness.NoyobNom("Sizish"));

        using var mijoz = PublicApiHarness.KalitMijozi(_factory, kalit.Key);

        // ── 1. BITTA xodim ────────────────────────────────────────
        using (var bitta = await mijoz.GetAsync(
            new Uri($"{PublicApiHarness.Prefix}/employees/{sirlar.EmployeeId}", UriKind.Relative),
            bekorQilish))
        {
            bitta.StatusCode.ShouldBe(HttpStatusCode.OK, await PublicApiHarness.MatnAsync(bitta));

            var json = await PublicApiHarness.MatnAsync(bitta);

            // Javob HAQIQATAN shu xodim ekanini avval tasdiqlaymiz: aks
            // holda bo'sh yoki boshqa javob ham «sizish yo'q» deb
            // o'tib ketardi.
            json.ShouldContain(sirlar.LastName);

            Tekshir(json, sirlar);
        }

        // ── 2. RO'YXAT — ikkinchi yo'l, aynan shu xarita ───────────
        using (var royxat = await mijoz.GetAsync(
            new Uri($"{PublicApiHarness.Prefix}/employees?search={sirlar.LastName}", UriKind.Relative),
            bekorQilish))
        {
            royxat.StatusCode.ShouldBe(HttpStatusCode.OK, await PublicApiHarness.MatnAsync(royxat));

            var json = await PublicApiHarness.MatnAsync(royxat);

            Bor(json, sirlar.LastName, "qidiruv xodimni topishi kerak");

            Tekshir(json, sirlar);
        }
    }

    /// <summary>
    /// JSHSHIR va pasport bo'yicha tashqaridan QIDIRIB BO'LMAYDI.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ Bu testning mavzusi — qiymat emas, MOSLIK fakti. Ichki qidiruv
    /// JSHSHIR va pasportni ham qamraydi; tashqarida esa qo'lida raqam
    /// bo'lgan kalit egasi «bu odam shu yerda ishlaydimi?» degan savolga
    /// javob olardi va ustiga to'liq ismni ko'rardi — qiymatning o'zi
    /// javobga tushmasa ham. Egasining qarori, 2026-09-18: tashqarida
    /// faqat ism va telefon.
    /// </para>
    /// <para>
    /// Ism bo'yicha qidiruv ISHLASHI ham shu yerda tekshiriladi: aks holda
    /// «qidiruvni butunlay o'chirib qo'yish» ham testni yashil qoldirardi.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Jshshir_va_pasport_boyicha_tashqaridan_qidirib_bolmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        var sirlar = await ToliqXodimYaratAsync().ConfigureAwait(true);

        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        var kalit = await PublicApiHarness.KalitYaratAsync(
            admin,
            PublicApiHarness.NoyobNom("Qidiruv"));

        using var mijoz = PublicApiHarness.KalitMijozi(_factory, kalit.Key);

        foreach (var (nima, qiymat) in new[] { ("JSHSHIR", sirlar.Pinfl), ("pasport", sirlar.Passport) })
        {
            using var javob = await mijoz.GetAsync(
                new Uri($"{PublicApiHarness.Prefix}/employees?search={qiymat}", UriKind.Relative),
                bekorQilish);

            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await PublicApiHarness.MatnAsync(javob));

            var json = await PublicApiHarness.MatnAsync(javob);

            Yoq(json, sirlar.LastName, $"{nima} bo'yicha qidiruv xodimni TOPMASLIGI kerak");
            Yoq(json, qiymat, nima);
        }

        // Ism bo'yicha esa topilishi kerak — qidiruvning o'zi tirik.
        using var ismBilan = await mijoz.GetAsync(
            new Uri($"{PublicApiHarness.Prefix}/employees?search={sirlar.LastName}", UriKind.Relative),
            bekorQilish);

        ismBilan.StatusCode.ShouldBe(HttpStatusCode.OK, await PublicApiHarness.MatnAsync(ismBilan));

        Bor(
            await PublicApiHarness.MatnAsync(ismBilan),
            sirlar.LastName,
            "ism bo'yicha qidiruv ishlashi kerak");
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    /// <summary>Tekshiruvda ishlatiladigan sirlar — bittasi ham javobga tushmasligi kerak.</summary>
    private sealed record Sirlar(
        Guid EmployeeId,
        string LastName,
        string Pinfl,
        string Passport,
        string Address,
        string Speciality,
        string CriminalNote,
        string ReviewNote,
        string MehnatUzUrl,
        string SalaryAmount,
        string SecondPhone);

    /// <summary>
    /// Xom JSON ichida taqiqlangan qiymat yoki nom bor-yo'qligini tekshiradi.
    /// </summary>
    /// <remarks>
    /// Taqqoslash <c>OrdinalIgnoreCase</c> bilan QO'LDA yozilgan, Shouldly'ning
    /// <c>ShouldNotContain</c> si bilan emas: bu yerda registrga sezgirlik
    /// tekshiruvning MAZMUNI (seriyalash siyosati o'zgarsa ham test
    /// ishlashda davom etishi kerak) va u chaqiruvda ochiq ko'rinib
    /// turishi kerak.
    /// </remarks>
    private static void Tekshir(string json, Sirlar sirlar)
    {
        // ── Qiymatlar ─────────────────────────────────────────────
        Yoq(json, sirlar.Pinfl, "JSHSHIR");
        Yoq(json, sirlar.Passport, "pasport");
        Yoq(json, sirlar.Address, "manzil");
        Yoq(json, sirlar.Speciality, "mutaxassislik");
        Yoq(json, sirlar.CriminalNote, "sudlanganlik izohi");
        Yoq(json, sirlar.ReviewNote, "ko'rib chiqish izohi");
        Yoq(json, sirlar.MehnatUzUrl, "mehnat.uz havolasi");
        Yoq(json, sirlar.SalaryAmount, "oklad summasi");
        Yoq(json, sirlar.SecondPhone, "ikkinchi telefon");

        // ── Maydon nomlari ────────────────────────────────────────
        // Nomning O'ZI ham chiqmasligi kerak: bo'sh yoki `null` qiymatli
        // maydon ham «bu ma'lumot bor» degan ishora beradi va SPEC §5
        // aynan shuni taqiqlaydi («absent from the JSON, not null-valued»).
        foreach (var nom in TaqiqlanganNomlar)
        {
            Yoq(json, nom, $"maydon nomi «{nom}»");
        }
    }

    private static void Bor(string json, string kutilgan, string nima)
        => json.Contains(kutilgan, StringComparison.Ordinal)
            .ShouldBeTrue($"javobda {nima}: «{kutilgan}»");

    private static void Yoq(string json, string taqiqlangan, string nima)
        => json.Contains(taqiqlangan, StringComparison.OrdinalIgnoreCase)
            .ShouldBeFalse($"ommaviy javobda {nima} bo'lmasligi kerak: «{taqiqlangan}»");

    /// <summary>
    /// Barcha nozik maydonlari to'ldirilgan xodim yaratadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Yozuv API orqali emas, TO'G'RIDAN-TO'G'RI baza orqali qo'yiladi:
    /// ichki endpoint'lar barcha maydonni bir yo'la yozmaydi (oklad
    /// munosabatda, «tuzatish kerak» belgisi esa muvaffaqiyatli tahrirdan
    /// keyin O'CHADI). Testga esa aynan eng yomon holat kerak — hammasi
    /// to'ldirilgan yozuv.
    /// </para>
    /// <para>
    /// <c>Pinfl.FromTrusted</c> ataylab: bu yerda nazorat raqamining
    /// to'g'riligi ahamiyatsiz, ahamiyatlisi — bazada shu RAQAM turishi va
    /// uni javobda izlab ko'rish mumkinligi.
    /// </para>
    /// </remarks>
    private async Task<Sirlar> ToliqXodimYaratAsync()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var belgi = Guid.NewGuid().ToString("N")[..8];

        var sirlar = new Sirlar(
            EmployeeId: Guid.Empty,
            LastName: "Сизмаган" + belgi,
            // JSHSHIR ning boshlang'ich yetti belgisi TASODIFIY BO'LMASLIGI
            // kerak: ular sana bo'lib o'qiladi («3» — 1900-yillar, erkak;
            // keyin dd, mm, yy) va tasodifiy raqam «32-oy» kabi mavjud
            // bo'lmagan sana yasab, `SetPinfl` ni istisno bilan yiqitardi.
            // Sana kartadagi `BirthDate` bilan ataylab MOS: testning
            // mavzusi sana tekshiruvi emas, maydonning sizib chiqishi.
            Pinfl: "3110294" + DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture)[^7..],
            Passport: "AA" + DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture)[^7..],
            Address: "Тошкент ш., Чилонзор " + belgi,
            Speciality: "Мухандис-механик " + belgi,
            CriminalNote: "2019 й. 168-модда " + belgi,
            ReviewNote: "ЖШШИР санаси мос эмас " + belgi,
            MehnatUzUrl: "https://mehnat.uz/profil/" + belgi,
            SalaryAmount: "12345678",
            SecondPhone: "+998911234567");

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var xodim = Employee.Create(
                FullName.Create(sirlar.LastName, "Алишер", "Ботирович").Value,
                new DateOnly(1994, 2, 11))
            .Value;

        // `SetPinfl` natijasi ATAYLAB tekshirilmaydi: JSHSHIR ichidagi sana
        // kartadagi sana bilan mos kelmasligi mumkin va u holda metod
        // ogohlantirish qaytaradi — lekin qiymatni BARIBIR o'rnatadi,
        // ya'ni test uchun kerakli holat hosil bo'ladi.
        xodim.SetPinfl(Pinfl.FromTrusted(sirlar.Pinfl));
        xodim.SetPassport(PassportSeries.FromTrusted(sirlar.Passport));
        xodim.SetPhones(
            PhoneNumber.FromTrusted("+998901234567"),
            PhoneNumber.FromTrusted(sirlar.SecondPhone));
        xodim.SetAddress(sirlar.Address);
        xodim.SetSpeciality(sirlar.Speciality);
        xodim.SetHasCriminalRecord(true);
        xodim.SetCriminalRecordNote(sirlar.CriminalNote);
        xodim.SetMehnatUzUrl(sirlar.MehnatUzUrl);
        xodim.SetTelegram("@karimov_" + belgi);
        xodim.FlagForReview(sirlar.ReviewNote);

        var firma = LegalEntity.Create("Сизиш текшируви " + belgi).Value;
        var bolim = Department.Create("Сизиш бўлими " + belgi).Value;
        var lavozim = Position.Create("Сизиш лавозими " + belgi).Value;
        var yonalish = Direction.Create("Сизиш йўналиши " + belgi).Value;

        xodim.AssignDirection(yonalish.Id);

        var munosabat = Employment
            .Hire(xodim.Id, firma.Id, new DateOnly(2021, 3, 1), bolim.Id, lavozim.Id)
            .Value;

        munosabat.SetSalary(
            Money.Create(decimal.Parse(sirlar.SalaryAmount, CultureInfo.InvariantCulture)).Value,
            new DateOnly(2021, 3, 1));

        context.LegalEntities.Add(firma);
        context.Departments.Add(bolim);
        context.Positions.Add(lavozim);
        context.Directions.Add(yonalish);
        context.Employees.Add(xodim);
        context.Employments.Add(munosabat);

        await context.SaveChangesAsync(bekorQilish);

        return sirlar with { EmployeeId = xodim.Id };
    }
}
