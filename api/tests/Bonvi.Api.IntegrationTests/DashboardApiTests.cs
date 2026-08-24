using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Ma'lumotnomalardagi faol yozuvlar soni (test uchun).</summary>
public sealed record DashboardCatalogResponse(
    int Departments,
    int Positions,
    int Directions,
    int LegalEntities);

/// <summary>Bosh sahifa ko'rsatkichlari (test uchun).</summary>
/// <remarks>
/// <c>Period</c> ATAYLAB satr: sanoq qiymatlari JSON'da nom bo'lib chiqadi
/// (<c>JsonStringEnumConverter</c>). U raqamga qaytsa, bu test
/// deserializatsiyada yiqiladi va o'zgarish e'tibordan chetda qolmaydi.
/// </remarks>
public sealed record DashboardStatsResponse(
    string Period,
    DateOnly From,
    DateOnly To,
    DateOnly PreviousFrom,
    DateOnly PreviousTo,
    int ActiveEmployees,
    int OnProbation,
    int FormerEmployees,
    int Hired,
    int Left,
    int HiredPrevious,
    int LeftPrevious,
    int BirthdaysToday,
    DashboardCatalogResponse Catalog);

/// <summary>
/// Bosh sahifa ko'rsatkichlarining uchi-uchiga testlari.
/// </summary>
/// <remarks>
/// <para>
/// Bu ekran tizimga kirgan HR ko'radigan BIRINCHI ekran va undagi raqamga
/// qarab qaror qabul qilinadi: «shu oyda 23 kishi ketibdi — sabab nima?».
/// Noto'g'ri raqam bu yerda jimgina xato: u xato ko'rinishida emas, ishonchli
/// son ko'rinishida chiqadi. Shu sababli har bir qoida testda qadalgan.
/// </para>
/// <para>
/// <b>Nega hamma joyda FARQ tekshiriladi, aniq son emas.</b> Endpoint butun
/// baza bo'yicha sanaydi, testlar esa bitta bazani baham ko'radi (bu sinfning
/// o'z bazasi, lekin ichidagi testlar umumiy). Aniq songa bog'lanish
/// qo'shni test yozuv qo'shishi bilan yiqilardi. Shu sababli har bir test
/// AVVAL boshlang'ich holatni oladi, keyin O'ZI yozuv qo'shadi va faqat
/// o'sishni tekshiradi.
/// </para>
/// <para>
/// Har bir test o'ziga xos belgi bilan ism va kod yozadi — unikallik
/// cheklovlariga urilmasligi uchun.
/// </para>
/// </remarks>
public sealed class DashboardApiTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public DashboardApiTests(BonviApiFactory factory) => _factory = factory;

    /// <summary>Bitta testning barcha yozuvlarini ajratib turadigan belgi.</summary>
    private static string Belgi() => "BS" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    // ────────────────────────────────────────────────────────────
    //  Ruxsat va so'rov parametri
    // ────────────────────────────────────────────────────────────

    /// <summary>Tokensiz so'rov 401 oladi — bosh sahifa ochiq emas.</summary>
    [Fact]
    public async Task Tokensiz_sorov_401_qaytaradi()
    {
        using var client = _factory.CreateClient();

        using var javob = await client.GetAsync(
            new Uri("/api/dashboard/stats", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Davr qiymati KICHIK harfda ham qabul qilinadi.
    /// </summary>
    /// <remarks>
    /// Bu test bir marta yiqilgan holatni qadaydi: so'rov satridagi sanoqni
    /// minimal API registrga sezgir bog'laydi va <c>?period=day</c> ekrandagi
    /// almashtirgichdan kelib turib 400 qaytarardi — bosh sahifa bo'sh
    /// ko'rinardi, hech qanday tushunarli xatosiz.
    /// </remarks>
    [Theory]
    [InlineData("day", "Day")]
    [InlineData("week", "Week")]
    [InlineData("days15", "Days15")]
    [InlineData("month", "Month")]
    [InlineData("Month", "Month")]
    public async Task Davr_qiymati_registrga_sezgir_emas(string sorov, string kutilgan)
    {
        using var client = await AdminMijozAsync();

        var sonlar = await SonlarAsync(client, $"?period={sorov}");

        sonlar.Period.ShouldBe(kutilgan);
    }

    /// <summary>Davr berilmasa — bir oy.</summary>
    /// <remarks>
    /// Sukut ataylab eng uzun davr: bir kunlik oynada odatda nol chiqadi
    /// va HR birinchi ekranda bo'sh raqamlarni ko'rib «tizim ishlamayapti»
    /// degan xulosaga kelardi.
    /// </remarks>
    [Fact]
    public async Task Davr_korsatilmasa_bir_oy_olinadi()
    {
        using var client = await AdminMijozAsync();

        var sonlar = await SonlarAsync(client, string.Empty);

        sonlar.Period.ShouldBe("Month");
    }

    /// <summary>Noma'lum davr 400 va tushunarli kod qaytaradi.</summary>
    /// <remarks>
    /// «99» alohida tekshiriladi: <c>Enum.TryParse</c> raqamni MUVAFFAQIYATLI
    /// o'qiydi va sanoqda mavjud bo'lmagan qiymat hosil qiladi. U sana
    /// arifmetikasiga tushsa, xato 400 ko'rinishida emas, noto'g'ri oraliq
    /// ko'rinishida chiqardi.
    /// </remarks>
    [Theory]
    [InlineData("oy")]
    [InlineData("99")]
    [InlineData("-1")]
    public async Task Notogri_davr_400_qaytaradi(string davr)
    {
        using var client = await AdminMijozAsync();

        using var javob = await client.GetAsync(
            new Uri($"/api/dashboard/stats?period={davr}", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(javob));
        (await MatnAsync(javob)).ShouldContain("dashboard.davr_notogri");
    }

    // ────────────────────────────────────────────────────────────
    //  Davr chegaralari
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Har bir davr — bugundan orqaga siljuvchi, ikki tomondan yopiq oyna;
    /// oldingi davr esa unga tegib turadi va bir xil uzunlikda bo'ladi.
    /// </summary>
    /// <remarks>
    /// «Bir kun ko'p» xatosi aynan shu yerda tug'iladi: 7 kunlik oyna uchun
    /// 6 kun orqaga qaytish kerak. Chegaralar javobda qaytgani uchun ularni
    /// mijoz emas, aynan shu test tekshiradi.
    /// </remarks>
    [Fact]
    public async Task Davr_chegaralari_javobda_toliq_qaytadi()
    {
        using var client = await AdminMijozAsync();

        // «Bugun» server soatidan olinadi (`IClock`, Toshkent vaqti).
        // Testda `DateTime.Now` ishlatilsa, yarim tunda sana ajralib ketardi.
        var kun = await SonlarAsync(client, "?period=day");
        var bugun = kun.To;

        kun.From.ShouldBe(bugun);
        kun.PreviousTo.ShouldBe(bugun.AddDays(-1));
        kun.PreviousFrom.ShouldBe(bugun.AddDays(-1));

        var hafta = await SonlarAsync(client, "?period=week");
        hafta.To.ShouldBe(bugun);
        hafta.From.ShouldBe(bugun.AddDays(-6));
        hafta.PreviousTo.ShouldBe(bugun.AddDays(-7));
        hafta.PreviousFrom.ShouldBe(bugun.AddDays(-13));

        var ounBesh = await SonlarAsync(client, "?period=days15");
        ounBesh.To.ShouldBe(bugun);
        ounBesh.From.ShouldBe(bugun.AddDays(-14));
        ounBesh.PreviousTo.ShouldBe(bugun.AddDays(-15));
        ounBesh.PreviousFrom.ShouldBe(bugun.AddDays(-29));

        // Oy kunlarda emas, kalendar bo'yicha sanaladi: fevralda 28 kun,
        // avgustda 31 — HR uchun «bir oy» aynan kalendar tushunchasi.
        var oy = await SonlarAsync(client, "?period=month");
        oy.To.ShouldBe(bugun);
        oy.From.ShouldBe(bugun.AddMonths(-1).AddDays(1));
        oy.PreviousTo.ShouldBe(bugun.AddMonths(-1));
        oy.PreviousFrom.ShouldBe(bugun.AddMonths(-2).AddDays(1));
    }

    // ────────────────────────────────────────────────────────────
    //  Xodimlar ODAM bo'yicha sanaladi
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Ikki firmada ishlaydigan o'rindosh BIR marta sanaladi.
    /// </summary>
    /// <remarks>
    /// Bu testning butun mazmuni shu: Excel'da «VIKOR + Вело унион 0,5 ставка
    /// ўриндош» kabi yozuvlar bor va munosabatlar bo'yicha sanasak, «540 xodim»
    /// «610 shartnoma» ga aylanib ketardi. Raqam kattaroq bo'lib chiqardi va
    /// hech kim buni sezmasdi.
    /// </remarks>
    [Fact]
    public async Task Ikki_firmada_ishlaydigan_xodim_bir_marta_sanaladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var belgi = Belgi();

        using var client = await AdminMijozAsync();

        var oldin = await SonlarAsync(client, "?period=month");

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

            var birinchiFirma = LegalEntity.Create($"{belgi} Биринчи МЧЖ", $"{belgi}-1").Value;
            var ikkinchiFirma = LegalEntity.Create($"{belgi} Иккинчи МЧЖ", $"{belgi}-2").Value;

            var xodim = Employee.Create(FullName.Create("Ўриндош", belgi, null).Value).Value;

            var asosiy = Doimiy(xodim.Id, birinchiFirma.Id, new DateOnly(2020, 1, 10));
            var qoshimcha = Doimiy(xodim.Id, ikkinchiFirma.Id, new DateOnly(2021, 5, 20));
            qoshimcha.SetPrimary(false);

            kontekst.AddRange(birinchiFirma, ikkinchiFirma);
            kontekst.Add(xodim);
            kontekst.AddRange(asosiy, qoshimcha);
            await kontekst.SaveChangesAsync(bekorQilish);
        }

        var keyin = await SonlarAsync(client, "?period=month");

        // Ikkita munosabat qo'shildi, lekin ODAM bitta
        (keyin.ActiveEmployees - oldin.ActiveEmployees).ShouldBe(1);
    }

    /// <summary>
    /// «Ishdan ketganlar» — endi HECH QAYERDA ishlamaydiganlar.
    /// </summary>
    /// <remarks>
    /// Bir firmadan ketib, ikkinchisida qolgan o'rindosh «ketgan» emas.
    /// Oddiy <c>Status == Terminated</c> sanog'i uni ham qo'shib yuborardi
    /// va bitta odam «xodimlar» hamda «ketganlar» kartalarida BIR VAQTDA
    /// turardi — HR bunday raqamga ishonolmaydi.
    /// </remarks>
    [Fact]
    public async Task Bir_firmadan_ketgan_orindosh_arxivga_tushmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var belgi = Belgi();

        using var client = await AdminMijozAsync();

        var oldin = await SonlarAsync(client, "?period=month");

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

            var firma = LegalEntity.Create($"{belgi} Архив МЧЖ", $"{belgi}-a").Value;
            var ikkinchiFirma = LegalEntity.Create($"{belgi} Қолган МЧЖ", $"{belgi}-q").Value;

            // 1. To'liq ketgan odam — arxivga tushishi kerak
            var ketgan = Employee.Create(FullName.Create("Кетган", belgi, null).Value).Value;
            var ketganMunosabat = Doimiy(ketgan.Id, firma.Id, new DateOnly(2019, 3, 1));
            ketganMunosabat.Terminate(new DateOnly(2023, 6, 1), null).IsSuccess.ShouldBeTrue();

            // 2. Bir firmadan ketgan, ikkinchisida qolgan o'rindosh —
            //    arxivga TUSHMASLIGI kerak
            var qolgan = Employee.Create(FullName.Create("Қолган", belgi, null).Value).Value;
            var yopilgan = Doimiy(qolgan.Id, firma.Id, new DateOnly(2019, 4, 1));
            yopilgan.Terminate(new DateOnly(2023, 7, 1), null).IsSuccess.ShouldBeTrue();
            var davomEtayotgan = Doimiy(qolgan.Id, ikkinchiFirma.Id, new DateOnly(2019, 4, 1));

            // 3. Sinovdagi odam
            var sinovda = Employee.Create(FullName.Create("Синовда", belgi, null).Value).Value;
            var sinovMunosabati = Employment
                .Hire(sinovda.Id, firma.Id, new DateOnly(2024, 2, 1))
                .Value;

            kontekst.AddRange(firma, ikkinchiFirma);
            kontekst.AddRange(ketgan, qolgan, sinovda);
            kontekst.AddRange(ketganMunosabat, yopilgan, davomEtayotgan, sinovMunosabati);
            await kontekst.SaveChangesAsync(bekorQilish);
        }

        var keyin = await SonlarAsync(client, "?period=month");

        // Arxivga faqat BITTA odam qo'shildi — «Кетган»
        (keyin.FormerEmployees - oldin.FormerEmployees).ShouldBe(1);

        // «Қолган» hali ishlayapti
        (keyin.ActiveEmployees - oldin.ActiveEmployees).ShouldBe(1);

        // Sinov muddati alohida sanaladi
        (keyin.OnProbation - oldin.OnProbation).ShouldBe(1);
    }

    // ────────────────────────────────────────────────────────────
    //  Davr sonlari
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Qabul va bo'shatish faqat TANLANGAN oynaga tushsa sanaladi,
    /// undan oldingisi esa «oldingi davr» sonida ko'rinadi.
    /// </summary>
    /// <remarks>
    /// Bu ekrandagi davr almashtirgichining butun ma'nosi. U ishlamasa,
    /// HR «1 kun» ni tanlab, aslida bir oylik raqamni ko'rib turaveradi.
    /// </remarks>
    [Fact]
    public async Task Qabul_va_boshatish_faqat_oz_davrida_sanaladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var belgi = Belgi();

        using var client = await AdminMijozAsync();

        var oldingiKun = await SonlarAsync(client, "?period=day");
        var bugun = oldingiKun.To;
        var oldingiHafta = await SonlarAsync(client, "?period=week");
        var oldingiOnBesh = await SonlarAsync(client, "?period=days15");

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

            var firma = LegalEntity.Create($"{belgi} Давр МЧЖ", $"{belgi}-d").Value;

            // Bugun kelgan
            var bugungi = Employee.Create(FullName.Create("Бугунги", belgi, null).Value).Value;
            var bugungiMunosabat = Doimiy(bugungi.Id, firma.Id, bugun);

            // 10 kun oldin kelgan — «1 hafta» ga tushmaydi, «15 kun» ga tushadi
            var onKunlik = Employee.Create(FullName.Create("Ўнкунлик", belgi, null).Value).Value;
            var onKunlikMunosabat = Doimiy(onKunlik.Id, firma.Id, bugun.AddDays(-10));

            // 100 kun oldin kelgan — bu davrlarning HECH BIRIGA tushmaydi
            var eski = Employee.Create(FullName.Create("Эски", belgi, null).Value).Value;
            var eskiMunosabat = Doimiy(eski.Id, firma.Id, bugun.AddDays(-100));

            // Bugun bo'shatilgan — «ketganlar» sonida faqat bugungi davrda
            eskiMunosabat.Terminate(bugun, null).IsSuccess.ShouldBeTrue();

            kontekst.Add(firma);
            kontekst.AddRange(bugungi, onKunlik, eski);
            kontekst.AddRange(bugungiMunosabat, onKunlikMunosabat, eskiMunosabat);
            await kontekst.SaveChangesAsync(bekorQilish);
        }

        var kun = await SonlarAsync(client, "?period=day");
        var hafta = await SonlarAsync(client, "?period=week");
        var onBesh = await SonlarAsync(client, "?period=days15");

        // Bugun: faqat bitta qabul va bitta bo'shatish
        (kun.Hired - oldingiKun.Hired).ShouldBe(1);
        (kun.Left - oldingiKun.Left).ShouldBe(1);

        // Bir hafta: 10 kun oldingisi hali ham tashqarida
        (hafta.Hired - oldingiHafta.Hired).ShouldBe(1);

        // 15 kun: bugungi va 10 kun oldingi — ikkovi
        (onBesh.Hired - oldingiOnBesh.Hired).ShouldBe(2);

        // 100 kun oldingi qabul hech qaysi oynaga tushmaydi
        (onBesh.Hired - oldingiOnBesh.Hired).ShouldBeLessThan(3);
    }

    /// <summary>
    /// Oldingi davr soni ALOHIDA qaytadi — o'sish ko'rsatkichi shundan chiziladi.
    /// </summary>
    /// <remarks>
    /// Ekrandagi «+12%» ilgari qo'lda yozib qo'yilgan edi. Endi u ikkita
    /// haqiqiy sondan hosil bo'ladi va shu ikkovining to'g'riligi shu yerda
    /// tekshiriladi: oldingi oynadagi qabul JORIY songa qo'shilib ketmasligi
    /// kerak.
    /// </remarks>
    [Fact]
    public async Task Oldingi_davr_soni_alohida_sanaladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var belgi = Belgi();

        using var client = await AdminMijozAsync();

        var oldin = await SonlarAsync(client, "?period=week");
        var bugun = oldin.To;

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

            var firma = LegalEntity.Create($"{belgi} Ўтган МЧЖ", $"{belgi}-o").Value;

            // Oldingi hafta oynasining o'rtasi: bugundan 10 kun oldin
            var otgan = Employee.Create(FullName.Create("Ўтганҳафта", belgi, null).Value).Value;
            var otganMunosabat = Doimiy(otgan.Id, firma.Id, bugun.AddDays(-10));

            kontekst.Add(firma);
            kontekst.Add(otgan);
            kontekst.Add(otganMunosabat);
            await kontekst.SaveChangesAsync(bekorQilish);
        }

        var keyin = await SonlarAsync(client, "?period=week");

        // Joriy hafta o'zgarmadi, oldingi hafta bittaga o'sdi
        (keyin.Hired - oldin.Hired).ShouldBe(0);
        (keyin.HiredPrevious - oldin.HiredPrevious).ShouldBe(1);
    }

    // ────────────────────────────────────────────────────────────
    //  Tug'ilgan kunlar
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Bugungi tug'ilgan kun — YIL e'tiborga olinmaydi, faqat kun va oy;
    /// arxivdagi odam esa sanoqqa kirmaydi.
    /// </summary>
    [Fact]
    public async Task Bugungi_tugilgan_kunlar_faqat_ishlayotganlar_boyicha_sanaladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var belgi = Belgi();

        using var client = await AdminMijozAsync();

        var oldin = await SonlarAsync(client, "?period=day");
        var bugun = oldin.To;

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

            var firma = LegalEntity.Create($"{belgi} Туғилган МЧЖ", $"{belgi}-t").Value;

            // Bugun tug'ilgan va ishlayotgan — sanalishi kerak
            var tugilgan = Employee.Create(
                FullName.Create("Туғилган", belgi, null).Value,
                OttizYilOldin(bugun)).Value;
            var tugilganMunosabat = Doimiy(tugilgan.Id, firma.Id, new DateOnly(2020, 1, 10));

            // Bugun tug'ilgan, lekin ishdan ketgan — sanalmasligi kerak
            var ketgan = Employee.Create(
                FullName.Create("Кетганбайрам", belgi, null).Value,
                OttizYilOldin(bugun)).Value;
            var ketganMunosabat = Doimiy(ketgan.Id, firma.Id, new DateOnly(2019, 1, 10));
            ketganMunosabat.Terminate(new DateOnly(2022, 1, 10), null).IsSuccess.ShouldBeTrue();

            // Ertaga tug'ilgan va ishlayotgan — bugun sanalmasligi kerak
            var ertangi = Employee.Create(
                FullName.Create("Эртанги", belgi, null).Value,
                OttizYilOldin(bugun.AddDays(1))).Value;
            var ertangiMunosabat = Doimiy(ertangi.Id, firma.Id, new DateOnly(2020, 2, 10));

            kontekst.Add(firma);
            kontekst.AddRange(tugilgan, ketgan, ertangi);
            kontekst.AddRange(tugilganMunosabat, ketganMunosabat, ertangiMunosabat);
            await kontekst.SaveChangesAsync(bekorQilish);
        }

        var keyin = await SonlarAsync(client, "?period=day");

        (keyin.BirthdaysToday - oldin.BirthdaysToday).ShouldBe(1);
    }

    // ────────────────────────────────────────────────────────────
    //  Ma'lumotnomalar
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Ma'lumotnoma sanog'iga faqat FAOL yozuvlar kiradi.
    /// </summary>
    /// <remarks>
    /// Faolsizlantirilgan yozuv o'chirilmaydi (eski buyruqlarda unga havola
    /// bor), lekin «bugun nechta bo'lim bor» degan savolga javob bermaydi.
    /// U sanoqda qolsa, ma'lumotnomani tozalash ishi ekranda umuman
    /// ko'rinmasdi: 96 raqami hech qachon kamaymasdi.
    /// </remarks>
    [Fact]
    public async Task Malumotnomada_faqat_faol_yozuvlar_sanaladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var belgi = Belgi();

        using var client = await AdminMijozAsync();

        var oldin = await SonlarAsync(client, "?period=month");

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

            var faol = Direction.Create($"{belgi} Фаол йўналиш", $"{belgi}-f").Value;
            var faolsiz = Direction.Create($"{belgi} Ёпилган йўналиш", $"{belgi}-y").Value;
            faolsiz.Deactivate("Тест учун ёпилди");

            var bolim = Department.Create($"{belgi} Фаол бўлим", code: $"{belgi}-b").Value;
            var yopilganBolim = Department.Create($"{belgi} Ёпилган бўлим", code: $"{belgi}-yb").Value;
            yopilganBolim.Deactivate();

            kontekst.AddRange(faol, faolsiz);
            kontekst.AddRange(bolim, yopilganBolim);
            await kontekst.SaveChangesAsync(bekorQilish);
        }

        var keyin = await SonlarAsync(client, "?period=month");

        (keyin.Catalog.Directions - oldin.Catalog.Directions).ShouldBe(1);
        (keyin.Catalog.Departments - oldin.Catalog.Departments).ShouldBe(1);
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    /// <summary>Sinovi yopilgan, ya'ni DOIMIY mehnat munosabati.</summary>
    /// <remarks>
    /// <c>Employment.Hire</c> munosabatni doim sinov muddatida ochadi —
    /// tizimda ishga qabul shundan boshlanadi. «Ishlayotgan xodim» sonini
    /// tekshirish uchun sinovni yopish kerak.
    /// </remarks>
    private static Employment Doimiy(Guid xodimId, Guid firmaId, DateOnly sana)
    {
        var munosabat = Employment.Hire(xodimId, firmaId, sana).Value;
        munosabat.CompleteProbation(sana).IsSuccess.ShouldBeTrue();
        return munosabat;
    }

    /// <summary>Berilgan sananing kun-oyi bilan, taxminan 30 yil oldingi sana.</summary>
    /// <remarks>
    /// 29-fevral uchun alohida qadam bor: 30 yil oldingi yil kabisa bo'lmasa,
    /// <c>new DateOnly(...)</c> umuman yaratilmaydi. Bu yiliga bir kun
    /// ishlaydigan, lekin o'sha kuni testni yiqitadigan chekka holat.
    /// </remarks>
    private static DateOnly OttizYilOldin(DateOnly kun)
    {
        var yil = kun.Year - 30;

        while (kun is { Month: 2, Day: 29 } && !DateTime.IsLeapYear(yil))
        {
            yil--;
        }

        return new DateOnly(yil, kun.Month, kun.Day);
    }

    private static async Task<string> MatnAsync(HttpResponseMessage javob) =>
        await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

    private static async Task<DashboardStatsResponse> SonlarAsync(HttpClient client, string sorov)
    {
        using var javob = await client.GetAsync(
            new Uri($"/api/dashboard/stats{sorov}", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var sonlar = await javob.Content.ReadFromJsonAsync<DashboardStatsResponse>(
            TestContext.Current.CancellationToken);

        sonlar.ShouldNotBeNull();
        return sonlar;
    }

    private async Task<HttpClient> AdminMijozAsync()
    {
        var client = _factory.CreateClient();

        using var javob = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new { login = BonviApiFactory.TestAdminLogin, password = BonviApiFactory.TestAdminPassword },
            TestContext.Current.CancellationToken);

        javob.EnsureSuccessStatusCode();

        var auth = await javob.Content.ReadFromJsonAsync<AuthResponse>(
            TestContext.Current.CancellationToken);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return client;
    }
}
