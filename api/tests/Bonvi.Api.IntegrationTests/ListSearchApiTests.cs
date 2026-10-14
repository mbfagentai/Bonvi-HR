using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bonvi.Domain.Catalog;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Feedback;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Organization;
using Bonvi.Domain.Safety;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Xodim bo'yicha qidiruv BARCHA ro'yxatlarda bir xil ishlashi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega bu testlar bor.</b> Ilgari har bir ro'yxat qidiruvni o'zicha
/// tushunardi: xodimlar ro'yxatida F.I.Sh. + JSHSHIR + pasport + telefon,
/// buyruqlarda faqat buyruq raqami, rag'batda faqat F.I.Sh., qolgan
/// yettitasida esa qidiruv umuman yo'q edi. Natijada xodimlar ro'yxatida
/// JSHSHIR bilan odamni topgan HR o'sha matnni boshqa ekranga
/// ko'chirganda BO'SH natija olardi — va hech qanday xato chiqmasdi.
/// Topilmagan qidiruv esa bo'sh ekran emas: HR odamni «bazada yo'q» deb
/// hisoblaydi va yangi karta ochadi (loyihaning eng katta muammosi aynan
/// shundan boshlangan).
/// </para>
/// <para>
/// Shu sababli bu yerda har bir ro'yxat AYNAN BIR XIL uchta kalit bilan
/// so'raladi: familiya, JSHSHIR va formatlangan telefon. Uchtasi ham
/// ishlashi shart — bittasi ishlab, ikkinchisi ishlamasa, foydalanuvchi
/// qaysi ekranda nima ishlashini eslab qolishga majbur bo'ladi.
/// </para>
/// <para>
/// Tekshiruv ATAYLAB haqiqiy PostgreSQL ustida: ism yig'ish
/// (<c>translate</c>) va telefon ajratgichlarini tashlash
/// (<c>regexp_replace</c>) bazada bajariladi va ularni mock bilan ko'rib
/// bo'lmaydi (<c>SearchFoldingTests</c> dagi bilan bir xil sabab).
/// </para>
/// <para>
/// Bu test sinfi o'z bazasiga ega (<see cref="BonviApiFactory"/> har bir
/// sinf uchun yangi baza yaratadi), shu sababli urug' BIR MARTA qo'yiladi
/// va keyingi chaqiruvlarda takrorlanmaydi.
/// </para>
/// </remarks>
public sealed class ListSearchApiTests : IClassFixture<BonviApiFactory>
{
    /// <summary>
    /// Izlanadigan xodimning familiyasi — to'rttala «qiyin» harf bilan.
    /// </summary>
    /// <remarks>
    /// қ, ғ, ҳ va ў rus klaviaturasida yo'q. Bu yerda ular ataylab:
    /// qidiruv shartining nusxasi emas, AYNAN o'sha yig'ish qoidasi
    /// ishlayotganini ko'rsatadi (agar biror ro'yxatda qoida qayta
    /// yozilgan bo'lsa, bu test o'sha ro'yxatda yiqiladi).
    /// </remarks>
    private const string Familiya = "Ғаниқўҳов";

    /// <summary>Nazorat raqami to'g'ri JSHSHIR (1990-03-15, erkak).</summary>
    private const string Jshshir = "31503904234561";

    /// <summary>Pasport LOTIN harflari bilan — ETL shunday normallashtiradi.</summary>
    private const string Pasport = "AB7412589";

    /// <summary>Bazadagi kanonik telefon (ajratgichsiz).</summary>
    private const string Telefon = "905087412";

    /// <summary>HR ekranda KO'RADIGAN va ko'chirib qo'yadigan shakl.</summary>
    private const string FormatlanganTelefon = "+998 90 508 74 12";

    /// <summary>Izlanadigan xodimning buyruq raqami.</summary>
    private const string BuyruqRaqami = "QID-185";

    /// <summary>«Shovqin» xodim — qidiruv haqiqatan toraytirayotganini ko'rsatadi.</summary>
    private const string ShovqinFamiliya = "Каримов";

    private readonly BonviApiFactory _factory;

    public ListSearchApiTests(BonviApiFactory factory) => _factory = factory;

    /// <summary>
    /// Xodim bo'yicha qidiruvi bo'lgan barcha ro'yxatlar.
    /// </summary>
    /// <remarks>
    /// <c>{0}</c> — qidiruv matni (manzil satriga xavfsiz joylanadi).
    /// Yangi ro'yxat qo'shilganda uni SHU YERGA qo'shish kifoya: qolgan
    /// tekshiruvlar avtomatik qo'llanadi va yangi ekran birinchi kunidan
    /// qolganlar bilan bir xil ishlaydi.
    /// </remarks>
    private static readonly string[] Royxatlar =
    [
        // Xodimlar ro'yxatining O'ZI ham shu yerda: u qolganlarga
        // qidiruv qoidasini beradigan manba (`EmployeeSearchQuery`) va
        // aynan shu sababli u ham xuddi shu tekshiruvdan o'tishi kerak.
        // Aks holda manba buzilib, qolganlar bilan birga jimgina
        // buzilardi — testlar esa faqat «hammasi bir xil noto'g'ri»
        // ekanini tasdiqlardi.
        "/api/employees?search={0}",
        "/api/employments/events?search={0}",
        "/api/incentives?search={0}",
        "/api/probations?search={0}",
        "/api/employees/flagged?type=Blacklist&onlyActive=false&search={0}",
        "/api/exit-interviews?search={0}",
        "/api/safety/incidents?search={0}",
    ];

    /// <summary>
    /// Har bir ro'yxat × har bir qidiruv kaliti.
    /// </summary>
    /// <remarks>
    /// Uchta kalit ataylab uch xil yo'lni bosadi: familiya — ism ustuni
    /// va yig'ish; JSHSHIR — kod ustuni; formatlangan telefon — raqamli
    /// ustun va ajratgichlarni tashlash. Ular bitta ro'yxatda ishlab,
    /// boshqasida ishlamasligi mumkin, shu sababli har biri har bir
    /// ro'yxatga alohida qo'llanadi.
    /// </remarks>
    public static TheoryData<string, string> RoyxatVaKalit()
    {
        // Pasport ham shu yerda: u KOD ustuni bo'yicha alohida yo'lni
        // bosadi (`SearchFolding.ForCode` — kirill gomogliflari) va
        // ilgari faqat bitta testda, aylanma tarzda tekshirilardi.
        string[] kalitlar = [Familiya, Jshshir, Pasport, FormatlanganTelefon];

        var data = new TheoryData<string, string>();

        foreach (var royxat in Royxatlar)
        {
            foreach (var kalit in kalitlar)
            {
                data.Add(royxat, kalit);
            }
        }

        return data;
    }

    // ────────────────────────────────────────────────────────────
    //  Asosiy talab: har bir ro'yxat uchala kalit bilan ham topadi
    // ────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(RoyxatVaKalit))]
    public async Task Har_bir_royxat_fio_jshshir_va_telefon_bilan_topadi(string royxat, string kalit)
    {
        var urug = await TayyorlaAsync();

        using var client = await AdminMijozAsync();

        var topilgan = await XodimlarAsync(client, Manzil(royxat, kalit));

        topilgan.ShouldNotBeEmpty($"«{kalit}» bo'yicha «{royxat}» hech narsa topmadi");
        topilgan.ShouldAllBe(
            xodimId => xodimId == urug.XodimId,
            $"«{kalit}» bo'yicha «{royxat}» begona xodimni olib keldi");
    }

    /// <summary>
    /// Qidiruvsiz so'rovda ikkala xodim ham ko'rinadi.
    /// </summary>
    /// <remarks>
    /// Bu yuqoridagi testning teskari yarmi va usiz u ma'nosiz bo'lardi:
    /// ro'yxat umuman bo'sh qaytsa ham «begona xodim yo'q» sharti
    /// bajarilardi. Bu yerda esa qidiruvsiz holatda IKKALA xodim ham
    /// borligi tasdiqlanadi — demak qidiruvni aynan qidiruv toraytirgan.
    /// </remarks>
    [Theory]
    [InlineData("/api/employees")]
    [InlineData("/api/employments/events")]
    [InlineData("/api/incentives")]
    [InlineData("/api/probations")]
    [InlineData("/api/employees/flagged?type=Blacklist&onlyActive=false")]
    [InlineData("/api/exit-interviews")]
    [InlineData("/api/safety/incidents")]
    public async Task Qidiruvsiz_royxatda_ikkala_xodim_ham_bor(string royxat)
    {
        var urug = await TayyorlaAsync();

        using var client = await AdminMijozAsync();

        var topilgan = await XodimlarAsync(client, royxat);

        topilgan.ShouldContain(urug.XodimId);
        topilgan.ShouldContain(urug.ShovqinXodimId);
    }

    /// <summary>
    /// Pasport bo'yicha qidiruv ham ishlaydi — kirill klaviaturada ham.
    /// </summary>
    /// <remarks>
    /// Excel'da 607 ta pasportda kirill «А» (U+0410) turgan edi, lotin «A»
    /// (U+0041) emas. ETL ularni lotinga o'girdi, HR esa baribir kirill
    /// klaviaturada yozadi — ikkalasi ekranda AYNAN bir xil ko'rinadi.
    /// Alohida test, chunki bu kalit boshqa qoidaga (kod gomogliflari)
    /// tayanadi.
    /// </remarks>
    [Theory]
    [InlineData(Pasport)]      // lotin — saqlangani
    [InlineData("АВ7412589")]  // kirill «А» va «В»
    public async Task Pasport_bilan_qidiruv_barcha_royxatlarda_ishlaydi(string kalit)
    {
        var urug = await TayyorlaAsync();

        using var client = await AdminMijozAsync();

        foreach (var royxat in Royxatlar)
        {
            var topilgan = await XodimlarAsync(client, Manzil(royxat, kalit));

            topilgan.ShouldNotBeEmpty($"«{kalit}» bo'yicha «{royxat}» hech narsa topmadi");
            topilgan.ShouldAllBe(xodimId => xodimId == urug.XodimId);
        }
    }

    /// <summary>
    /// Rus klaviaturasida yozilgan familiya ham topiladi.
    /// </summary>
    /// <remarks>
    /// «Ғаниқўҳов» → «Ганикухов»: to'rttala harf ham almashgan. Bu qoida
    /// ilgari FAQAT xodimlar ro'yxatida bor edi; bu test uni endi hamma
    /// joyda talab qiladi.
    /// </remarks>
    [Theory]
    [InlineData("Ганикухов")]
    [InlineData("ГАНИКУХОВ")]
    [InlineData("никух")]
    public async Task Ism_qiyin_harflarsiz_yozilganda_ham_barcha_royxatlarda_topiladi(string kalit)
    {
        var urug = await TayyorlaAsync();

        using var client = await AdminMijozAsync();

        foreach (var royxat in Royxatlar)
        {
            var topilgan = await XodimlarAsync(client, Manzil(royxat, kalit));

            topilgan.ShouldNotBeEmpty($"«{kalit}» bo'yicha «{royxat}» hech narsa topmadi");
            topilgan.ShouldAllBe(xodimId => xodimId == urug.XodimId);
        }
    }

    /// <summary>
    /// Kirillda saqlangan familiya LOTINDA yozilganda ham topiladi.
    /// </summary>
    /// <remarks>
    /// Ismlar bazada aralash (№91 kirillda, bot orqali kelganlar lotinda) va
    /// HR qaysi yozuvda yozishini bilmaydi (egasining 2026-10-14 dagi
    /// talabi). Qoida bitta joyda — bazadagi <c>search_key()</c> — va bu test
    /// uni HAR BIR ro'yxatda talab qiladi: biror ro'yxat o'z shartini
    /// yozib olgan bo'lsa, aynan shu yerda yiqiladi.
    /// </remarks>
    [Theory]
    [InlineData("Ganiqo'hov")]   // rasmiy lotin
    [InlineData("Ganiqohov")]    // apostrofsiz
    [InlineData("Ganikuxov")]    // ruscha lotin uslubi
    [InlineData("Ganiqo'hov Sherzod")] // familiya + ism
    public async Task Ism_lotinda_yozilganda_ham_barcha_royxatlarda_topiladi(string kalit)
    {
        var urug = await TayyorlaAsync();

        using var client = await AdminMijozAsync();

        foreach (var royxat in Royxatlar)
        {
            var topilgan = await XodimlarAsync(client, Manzil(royxat, kalit));

            topilgan.ShouldNotBeEmpty($"«{kalit}» bo'yicha «{royxat}» hech narsa topmadi");
            topilgan.ShouldAllBe(xodimId => xodimId == urug.XodimId);
        }
    }

    /// <summary>
    /// <c>LIKE</c> jokerlari ekranlanadi — «%» butun ro'yxatni olib
    /// kelmaydi.
    /// </summary>
    /// <remarks>
    /// Eng xavfli xato — teskarisi: qidiruv BEGONA yozuvlarni chiqarib
    /// yuborishi. U holda HR ro'yxatdan noto'g'ri odamni tanlab, boshqa
    /// birovning kartasiga amal bajarardi.
    /// </remarks>
    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    public async Task Joker_belgilar_royxatni_ochib_yubormaydi(string kalit)
    {
        _ = await TayyorlaAsync();

        using var client = await AdminMijozAsync();

        foreach (var royxat in Royxatlar)
        {
            var topilgan = await XodimlarAsync(client, Manzil(royxat, kalit));

            topilgan.ShouldBeEmpty($"«{kalit}» «{royxat}» ro'yxatini ochib yubordi");
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Buyruqlar: eski xatti-harakat buzilmadi
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Buyruq raqami bo'yicha qidiruv AVVALGIDEK ishlaydi.
    /// </summary>
    /// <remarks>
    /// Buyruqlar ro'yxatiga xodim sharti «YOKI» bilan qo'shildi. Aynan
    /// shu joyda eski yo'lni jimgina buzish oson bo'lardi: shart o'rni
    /// o'zgardi (endi u xodim bilan birlashtirilgandan KEYIN turadi) va
    /// bu test o'sha ko'chirish natijani o'zgartirmaganini qadaydi.
    /// HR qo'lida qog'oz buyruq bilan keladigan holat — eng ko'p
    /// uchraydiganlaridan biri.
    /// </remarks>
    [Fact]
    public async Task Buyruq_raqami_bilan_qidiruv_buzilmadi()
    {
        var urug = await TayyorlaAsync();

        using var client = await AdminMijozAsync();

        var topilgan = await XodimlarAsync(
            client,
            Manzil("/api/employments/events?search={0}", BuyruqRaqami));

        topilgan.ShouldNotBeEmpty("buyruq raqami bo'yicha qidiruv ishlamay qoldi");
        topilgan.ShouldAllBe(xodimId => xodimId == urug.XodimId);
    }

    /// <summary>
    /// Buyruq raqami va xodim sharti BIR-BIRINI ALMASHTIRMAYDI.
    /// </summary>
    /// <remarks>
    /// «Shovqin» xodimning buyruq raqami bo'yicha qidiruv aynan uni
    /// topishi kerak — ya'ni ikkita shart «yoki» bilan bog'langan va
    /// biri ikkinchisini yutib yubormaydi.
    /// </remarks>
    [Fact]
    public async Task Buyruq_raqami_va_xodim_sharti_bir_birini_almashtirmaydi()
    {
        var urug = await TayyorlaAsync();

        using var client = await AdminMijozAsync();

        var buyruqBoyicha = await XodimlarAsync(
            client,
            Manzil("/api/employments/events?search={0}", urug.ShovqinBuyruqRaqami));

        buyruqBoyicha.ShouldNotBeEmpty();
        buyruqBoyicha.ShouldAllBe(xodimId => xodimId == urug.ShovqinXodimId);

        var ismBoyicha = await XodimlarAsync(
            client,
            Manzil("/api/employments/events?search={0}", ShovqinFamiliya));

        ismBoyicha.ShouldNotBeEmpty();
        ismBoyicha.ShouldAllBe(xodimId => xodimId == urug.ShovqinXodimId);
    }

    // ────────────────────────────────────────────────────────────
    //  Foydalanuvchilar — boshqacha ro'yxat
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Foydalanuvchilar F.I.Sh. va LOGIN (telefon) bo'yicha topiladi.
    /// </summary>
    /// <remarks>
    /// Bu ro'yxat qolganlaridan ATAYLAB farq qiladi: hisobda JSHSHIR ham,
    /// pasport ham yo'q. Telefon qoidasi esa bir xil — administrator
    /// ekranda ko'rgan formatlangan raqamni ko'chirib qo'yishi mumkin.
    /// </remarks>
    [Theory]
    [InlineData(Familiya)]              // aynan saqlangani
    [InlineData("Ганикухов")]           // rus klaviaturasi
    [InlineData("Ganiqo'hov")]          // lotinda
    [InlineData(Telefon)]               // login, ajratgichsiz
    [InlineData(FormatlanganTelefon)]   // login, ekrandagi ko'rinishda
    public async Task Foydalanuvchilar_fio_va_login_boyicha_topiladi(string kalit)
    {
        var urug = await TayyorlaAsync();

        using var client = await AdminMijozAsync();

        var topilgan = await FoydalanuvchilarAsync(client, Manzil("/api/users?search={0}", kalit));

        topilgan.ShouldNotBeEmpty($"«{kalit}» bo'yicha foydalanuvchi topilmadi");
        topilgan.ShouldAllBe(id => id == urug.FoydalanuvchiId);
    }

    /// <summary>
    /// Qidiruvsiz so'rov barcha hisoblarni beradi — administrator ham.
    /// </summary>
    /// <remarks>
    /// Bu ro'yxatning eng muhim xususiyati: faolsiz va begona hisoblar
    /// ham ko'rinishi kerak. Qidiruv qo'shilganda uni jimgina buzib
    /// qo'yish oson (masalan shart doim qo'llanib qolsa), shu sababli
    /// tekshiruv alohida turadi.
    /// </remarks>
    [Fact]
    public async Task Qidiruvsiz_foydalanuvchilar_royxati_toliq_qaytadi()
    {
        var urug = await TayyorlaAsync();

        using var client = await AdminMijozAsync();

        var topilgan = await FoydalanuvchilarAsync(client, "/api/users");

        topilgan.ShouldContain(urug.FoydalanuvchiId);
        topilgan.Count.ShouldBeGreaterThan(1, "administrator hisobi ham ro'yxatda bo'lishi kerak");
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    /// <summary>Test ma'lumotining kalitlari.</summary>
    private sealed record Urug(
        Guid XodimId,
        Guid ShovqinXodimId,
        Guid FoydalanuvchiId,
        string ShovqinBuyruqRaqami);

    /// <summary>Manzil satriga qidiruv matnini xavfsiz joylaydi.</summary>
    private static string Manzil(string qolip, string kalit)
        => string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            qolip,
            Uri.EscapeDataString(kalit));

    /// <summary>
    /// Ikki xodim, ikki hisob va HAR BIR ro'yxatda ikkitadan yozuv.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Idempotent: sinf bitta bazani baham ko'radi va urug' allaqachon
    /// bo'lsa faqat kalitlari qaytariladi. JSHSHIR unikal, ikkinchi marta
    /// qo'shish cheklovga urilardi (<c>SearchFoldingTests</c> dagi bilan
    /// bir xil naqsh).
    /// </para>
    /// <para>
    /// Yozuvlar HTTP orqali emas, to'g'ridan-to'g'ri baza orqali qo'yiladi
    /// (<c>EmploymentEventsApiTests</c> dagi bilan bir xil sabab): testga
    /// aynan kerakli sana, tur va buyruq raqami kerak, endpoint esa har
    /// bir yozuv uchun alohida so'rov va qo'shimcha tayyorgarlik talab
    /// qilardi. Domen qoidalari baribir ishlaydi — hamma narsa domen
    /// fabrikalari orqali yaratiladi.
    /// </para>
    /// <para>
    /// «Shovqin» xodim MAJBURIY: usiz «qidiruv begona yozuvni olib
    /// kelmadi» degan tekshiruv bo'sh bazada ham o'tib ketardi.
    /// </para>
    /// </remarks>
    private async Task<Urug> TayyorlaAsync()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var pinfl = Pinfl.Create(Jshshir).Value;

        var mavjud = await kontekst.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Pinfl == pinfl, bekorQilish)
            .ConfigureAwait(false);

        if (mavjud is not null)
        {
            var shovqin = await kontekst.Employees
                .AsNoTracking()
                .FirstAsync(x => x.FullName.LastName == ShovqinFamiliya, bekorQilish)
                .ConfigureAwait(false);

            var hisob = await kontekst.Users
                .AsNoTracking()
                .FirstAsync(x => x.FullName.LastName == Familiya, bekorQilish)
                .ConfigureAwait(false);

            return new Urug(mavjud.Id, shovqin.Id, hisob.Id, ShovqinFamiliya + "-777");
        }

        var firma = LegalEntity.Create("Қидирув Тест МЧЖ").Value;
        var bolim = Department.Create("Қидирув бўлими").Value;
        var lavozim = Position.Create("Қидирув мутахассиси").Value;
        var mukofot = AwardType.Create("Қидирув ташаккурномаси").Value;
        mukofot.SetHasAmount(false);

        var xodim = Employee.Create(
            FullName.Create(Familiya, "Шерзод", "Қидирув").Value,
            new DateOnly(1990, 3, 15)).Value;

        _ = xodim.SetPinfl(pinfl);
        xodim.SetPassport(PassportSeries.Create(Pasport).Value);
        xodim.SetPhones(PhoneNumber.Create(Telefon).Value);

        // Shovqin xodimda JSHSHIR, pasport va telefon ATAYLAB yo'q: ular
        // unikal cheklovga ega va testga ikkinchi to'plam kerak emas —
        // uning vazifasi «qidiruv toraytiryaptimi» degan savolga javob
        // berish, ya'ni u ro'yxatda BO'LISHI va qidiruvda TUSHIB QOLISHI
        // kifoya.
        var shovqinXodim = Employee.Create(FullName.Create(ShovqinFamiliya, "Азиз", "Шовқин").Value).Value;

        var munosabat = Employment.Hire(
            xodim.Id,
            firma.Id,
            new DateOnly(2024, 1, 15),
            bolim.Id,
            lavozim.Id,
            orderNo: BuyruqRaqami).Value;

        var shovqinBuyruq = ShovqinFamiliya + "-777";

        var shovqinMunosabat = Employment.Hire(
            shovqinXodim.Id,
            firma.Id,
            new DateOnly(2024, 2, 20),
            bolim.Id,
            lavozim.Id,
            orderNo: shovqinBuyruq).Value;

        // Sinov muddatlari
        var sinov = Probation.Start(munosabat.Id, new DateOnly(2024, 1, 15)).Value;
        var shovqinSinov = Probation.Start(shovqinMunosabat.Id, new DateOnly(2024, 2, 20)).Value;

        // Belgilar (qora ro'yxat — sabab MAJBURIY, domen shunday talab qiladi)
        var belgi = EmployeeFlag.Raise(
            xodim.Id,
            EmployeeFlagType.Blacklist,
            new DateOnly(2024, 3, 1),
            reason: "Тест").Value;

        var shovqinBelgi = EmployeeFlag.Raise(
            shovqinXodim.Id,
            EmployeeFlagType.Blacklist,
            new DateOnly(2024, 3, 2),
            reason: "Тест").Value;

        // Rag'bat
        var ragbat = Incentive.Grant(
            xodim.Id,
            IncentiveKind.Award,
            mukofot.Id,
            new DateOnly(2024, 4, 1),
            typeHasAmount: false).Value;

        var shovqinRagbat = Incentive.Grant(
            shovqinXodim.Id,
            IncentiveKind.Award,
            mukofot.Id,
            new DateOnly(2024, 4, 2),
            typeHasAmount: false).Value;

        // Qayta aloqa
        var suhbat = ExitInterview.Record(munosabat.Id, new DateOnly(2024, 5, 1), "Тест").Value;
        var shovqinSuhbat = ExitInterview.Record(shovqinMunosabat.Id, new DateOnly(2024, 5, 2), "Тест").Value;

        // Baxtsiz hodisalar
        var hodisa = Incident.Record(
            xodim.Id,
            new DateOnly(2024, 6, 1),
            "Цех",
            "Тест",
            IncidentSeverity.Minor).Value;

        var shovqinHodisa = Incident.Record(
            shovqinXodim.Id,
            new DateOnly(2024, 6, 2),
            "Цех",
            "Тест",
            IncidentSeverity.Minor).Value;

        // Foydalanuvchi hisoblari. Parol xeshi ixtiyoriy satr — bu hisoblar
        // bilan tizimga KIRILMAYDI, ular faqat ro'yxatda qidiriladi.
        var foydalanuvchi = User.Create(
            PhoneNumber.Create(Telefon).Value,
            "test-hash",
            FullName.Create(Familiya, "Шерзод", null).Value).Value;

        var shovqinFoydalanuvchi = User.Create(
            PhoneNumber.Create("905081111").Value,
            "test-hash",
            FullName.Create(ShovqinFamiliya, "Азиз", null).Value).Value;

        kontekst.AddRange(firma, bolim, lavozim, mukofot);
        kontekst.AddRange(xodim, shovqinXodim);
        kontekst.AddRange(munosabat, shovqinMunosabat);
        kontekst.AddRange(sinov, shovqinSinov);
        kontekst.AddRange(belgi, shovqinBelgi);
        kontekst.AddRange(ragbat, shovqinRagbat);
        kontekst.AddRange(suhbat, shovqinSuhbat);
        kontekst.AddRange(hodisa, shovqinHodisa);
        kontekst.AddRange(foydalanuvchi, shovqinFoydalanuvchi);

        await kontekst.SaveChangesAsync(bekorQilish).ConfigureAwait(false);

        return new Urug(xodim.Id, shovqinXodim.Id, foydalanuvchi.Id, shovqinBuyruq);
    }

    /// <summary>
    /// Ro'yxat javobidagi <c>employeeId</c> qiymatlari.
    /// </summary>
    /// <remarks>
    /// Javob ikki shaklda keladi: sahifalangan ro'yxatlar <c>{items:[…]}</c>
    /// obyektini, sahifalanmaganlari esa massivning o'zini qaytaradi.
    /// Ikkalasi ham shu yerda o'qiladi — har bir ro'yxat uchun alohida
    /// javob tipi e'lon qilish testni yettita bir xil recordga
    /// to'ldirardi, holbuki tekshiruvga faqat bitta maydon kerak.
    /// </remarks>
    private static async Task<IReadOnlyList<Guid>> XodimlarAsync(HttpClient client, string manzil)
    {
        using var javob = await client.GetAsync(new Uri(manzil, UriKind.Relative), TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var matn = await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var hujjat = JsonDocument.Parse(matn);

        var qatorlar = hujjat.RootElement.ValueKind == JsonValueKind.Object
            ? hujjat.RootElement.GetProperty("items")
            : hujjat.RootElement;

        return
        [
            .. qatorlar
                .EnumerateArray()
                .Select(XodimId),
        ];
    }

    /// <summary>
    /// Qatordan xodim identifikatorini oladi.
    /// </summary>
    /// <remarks>
    /// Ikki shakl bor va ikkalasi ham to'g'ri: xodimga TEGISHLI yozuv
    /// (sinov, rag'bat, belgi) uni <c>employeeId</c> bilan ko'rsatadi,
    /// xodimning O'ZI esa oddiy <c>id</c> bilan keladi. Ular bir xil
    /// bo'lishi shart emas — bu ikki xil obyekt.
    /// </remarks>
    private static Guid XodimId(JsonElement qator)
        => qator.TryGetProperty("employeeId", out var xodim)
            ? xodim.GetGuid()
            : qator.GetProperty("id").GetGuid();

    /// <summary>Foydalanuvchilar ro'yxatidagi identifikatorlar.</summary>
    private static async Task<IReadOnlyList<Guid>> FoydalanuvchilarAsync(HttpClient client, string manzil)
    {
        using var javob = await client.GetAsync(new Uri(manzil, UriKind.Relative), TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var matn = await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var hujjat = JsonDocument.Parse(matn);

        return [.. hujjat.RootElement.EnumerateArray().Select(qator => qator.GetProperty("id").GetGuid())];
    }

    private async Task<HttpClient> AdminMijozAsync()
    {
        var client = _factory.CreateClient();

        using var javob = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new { login = BonviApiFactory.TestAdminLogin, password = BonviApiFactory.TestAdminPassword },
            TestContext.Current.CancellationToken);

        javob.EnsureSuccessStatusCode();

        var auth = await javob.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return client;
    }

    /// <summary>Xato javobini o'qish — tekshiruv yiqilganda sababi ko'rinishi uchun.</summary>
    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
