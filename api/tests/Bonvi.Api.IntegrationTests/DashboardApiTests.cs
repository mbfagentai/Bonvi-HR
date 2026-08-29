using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.Recruiting;
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

/// <summary>Rekruting sonlari (test uchun).</summary>
/// <remarks>
/// Butun blok <c>null</c> bo'lishi mumkin: u faqat <c>recruiting.view</c>
/// ruxsati borlarga qaytadi. Testdagi administrator bu ruxsatga ega, ya'ni
/// bu yerda blok doim to'lgan keladi; ruxsatsiz holat esa
/// <c>GetDashboardStatsQueryHandlerTests</c> da tekshiriladi — qoida
/// handler'da yashaydi va uni tekshirish uchun butun API ni ko'tarish
/// shart emas.
/// </remarks>
public sealed record DashboardRecruitingResponse(
    int OpenVacancies,
    int Openings,
    int ActiveCandidates,
    int NewCandidates);

/// <summary>Bugun tug'ilgan kuni bo'lgan bitta xodim (test uchun).</summary>
/// <remarks>
/// <c>Age</c> ATAYLAB <c>int?</c>: shubhali sanada (kelajakdagi yoki 120
/// yildan oshgan) yosh umuman qaytmaydi. Tip <c>int</c> bo'lsa, bunday
/// javob deserializatsiyada jimgina nolga aylanardi va «0 yosh» degan
/// xato test orqali o'tib ketardi.
/// </remarks>
public sealed record DashboardBirthdayResponse(
    Guid Id,
    string FullName,
    string? PhotoKey,
    string? PositionName,
    string? DepartmentName,
    DateOnly BirthDate,
    int? Age);

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
    DashboardCatalogResponse Catalog,
    DashboardRecruitingResponse? Recruiting);

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

            var birinchiFirma = LegalEntity.Create($"{belgi} Биринчи МЧЖ").Value;
            var ikkinchiFirma = LegalEntity.Create($"{belgi} Иккинчи МЧЖ").Value;

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

            var firma = LegalEntity.Create($"{belgi} Архив МЧЖ").Value;
            var ikkinchiFirma = LegalEntity.Create($"{belgi} Қолган МЧЖ").Value;

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

            var firma = LegalEntity.Create($"{belgi} Давр МЧЖ").Value;

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

            var firma = LegalEntity.Create($"{belgi} Ўтган МЧЖ").Value;

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

            var firma = LegalEntity.Create($"{belgi} Туғилган МЧЖ").Value;

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

    /// <summary>
    /// Tug'ilgan kunlar RO'YXATI bosh sahifadagi SON bilan aynan mos keladi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bu testning butun mazmuni shu: ekranda «Bugun 2 ta xodimning
    /// tug'ilgan kuni» yozuvi bosiladi va ochilgan oynada AYNAN o'sha
    /// ikki odam turishi kerak. Shart ikki joyda yozilgan bo'lsa (ilgari
    /// shunday edi), ular birinchi o'zgarishdayoq ajralib ketardi va HR
    /// son bilan ro'yxat orasidagi farqni ko'rib, ikkalasiga ham
    /// ishonmay qo'yardi.
    /// </para>
    /// <para>
    /// ⚠️ Bu yerda FARQ emas, AYNAN TENGLIK tekshiriladi (qo'shni
    /// testlardan farqi): ikkala javob ham bir vaqtda, bir xil bazadan
    /// olinadi va ular butun baza bo'yicha mos kelishi shart.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Tugilgan_kunlar_royxati_son_bilan_aynan_mos_keladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var belgi = Belgi();

        using var client = await AdminMijozAsync();

        var bugun = (await SonlarAsync(client, "?period=day")).To;

        Guid ishlayotganId;

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

            var firma = LegalEntity.Create($"{belgi} Байрам МЧЖ").Value;
            var bolim = Department.Create($"{belgi} Сотув бўлими").Value;
            var lavozim = Position.Create($"{belgi} Менежер").Value;

            // Bugun tug'ilgan va ishlayotgan — ro'yxatda BO'LISHI kerak
            var ishlayotgan = Employee.Create(
                FullName.Create("Байрамли", belgi, "Отаевич").Value,
                OttizYilOldin(bugun)).Value;
            var ishlayotganMunosabat = Employment
                .Hire(ishlayotgan.Id, firma.Id, new DateOnly(2020, 1, 10), bolim.Id, lavozim.Id)
                .Value;
            ishlayotganMunosabat.CompleteProbation(new DateOnly(2020, 1, 10)).IsSuccess.ShouldBeTrue();
            ishlayotganId = ishlayotgan.Id;

            // Bugun tug'ilgan, lekin arxivda — ro'yxatda BO'LMASLIGI kerak
            var ketgan = Employee.Create(
                FullName.Create("Кетганбайрам", belgi, null).Value,
                OttizYilOldin(bugun)).Value;
            var ketganMunosabat = Doimiy(ketgan.Id, firma.Id, new DateOnly(2019, 1, 10));
            ketganMunosabat.Terminate(new DateOnly(2022, 1, 10), null).IsSuccess.ShouldBeTrue();

            // Ertaga tug'ilgan va ishlayotgan — bugun ro'yxatda BO'LMASLIGI
            // kerak (bildirishnomalar ikkala kunni ham oladi, bosh sahifa
            // esa faqat bugunni — bir xil shart, boshqa kun)
            var ertangi = Employee.Create(
                FullName.Create("Эртангибайрам", belgi, null).Value,
                OttizYilOldin(bugun.AddDays(1))).Value;
            var ertangiMunosabat = Doimiy(ertangi.Id, firma.Id, new DateOnly(2020, 2, 10));

            kontekst.Add(firma);
            kontekst.AddRange(bolim);
            kontekst.Add(lavozim);
            kontekst.AddRange(ishlayotgan, ketgan, ertangi);
            kontekst.AddRange(ishlayotganMunosabat, ketganMunosabat, ertangiMunosabat);
            await kontekst.SaveChangesAsync(bekorQilish);
        }

        var royxat = await TugilganKunlarAsync(client);
        var sonlar = await SonlarAsync(client, "?period=day");

        // ⭐ Asosiy da'vo: son va ro'yxat bitta shartdan hosil bo'ladi
        royxat.Count.ShouldBe(sonlar.BirthdaysToday);

        var qator = royxat.SingleOrDefault(item => item.Id == ishlayotganId);
        qator.ShouldNotBeNull("ishlayotgan xodim ro'yxatda bo'lishi kerak");
        qator.FullName.ShouldBe($"Байрамли {belgi} Отаевич");
        qator.DepartmentName.ShouldBe($"{belgi} Сотув бўлими");
        qator.PositionName.ShouldBe($"{belgi} Менежер");
        qator.BirthDate.ShouldBe(OttizYilOldin(bugun));
        qator.Age.ShouldBe(bugun.Year - OttizYilOldin(bugun).Year);

        // Arxivdagi va ertangi bayram egasi ro'yxatga TUSHMAYDI
        royxat.ShouldNotContain(item => item.FullName.Contains("Кетганбайрам", StringComparison.Ordinal));
        royxat.ShouldNotContain(item => item.FullName.Contains("Эртангибайрам", StringComparison.Ordinal));
    }

    /// <summary>Tug'ilgan kunlar ro'yxati ham tokensiz ochilmaydi.</summary>
    /// <remarks>
    /// Ro'yxatda F.I.Sh., lavozim va bo'lim bor — bu bosh sahifadagi
    /// sonlardan ko'ra ko'proq ma'lumot va u ham yopiq bo'lishi shart.
    /// </remarks>
    [Fact]
    public async Task Tugilgan_kunlar_royxati_tokensiz_401_qaytaradi()
    {
        using var client = _factory.CreateClient();

        using var javob = await client.GetAsync(
            new Uri("/api/dashboard/birthdays", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
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

            var faol = Direction.Create($"{belgi} Фаол йўналиш").Value;
            var faolsiz = Direction.Create($"{belgi} Ёпилган йўналиш").Value;
            faolsiz.Deactivate("Тест учун ёпилди");

            var bolim = Department.Create($"{belgi} Фаол бўлим").Value;
            var yopilganBolim = Department.Create($"{belgi} Ёпилган бўлим").Value;
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
    //  Rekruting
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Vakansiya soni va o'rinlar yig'indisi — IKKI XIL raqam.
    /// </summary>
    /// <remarks>
    /// Bosh sahifadagi «5 vakansiya · 12 o'rin kerak» yozuvining butun
    /// mazmuni shu. Bitta e'longa bir nechta o'rin qo'yiladi (zavod sexiga
    /// 20 kishi bitta e'lon bilan qidiriladi), ya'ni bu ikki son bir-biriga
    /// teng emas. Ular almashib qolsa yoki bittasi ikkinchisidan hosil
    /// qilinsa, rahbar «5» ni ko'rib besh kishi kerak deb o'ylardi.
    ///
    /// Yopilgan vakansiya ikkala sanoqqa ham kirmaydi — u endi nomzod
    /// qabul qilmaydi va uning o'rinlari ehtiyoj emas.
    /// </remarks>
    [Fact]
    public async Task Ochiq_vakansiyalar_va_orinlar_alohida_sanaladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var belgi = Belgi();

        using var client = await AdminMijozAsync();

        var oldin = await SonlarAsync(client, "?period=month");
        oldin.Recruiting.ShouldNotBeNull("administratorda «recruiting.view» bor");

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

            var nom = VacancyTitle.Create($"{belgi} Сотувчи-консультант").Value;

            // Ikkita OCHIQ e'lon: jami 3 + 4 = 7 o'rin
            var uchOrin = Vacancy.Open(nom.Id, new DateOnly(2026, 3, 1), openings: 3).Value;
            var tortOrin = Vacancy.Open(nom.Id, new DateOnly(2026, 4, 1), openings: 4).Value;

            // Yopilgani hech qayerda ko'rinmasligi kerak — o'rinlari ko'p
            // bo'lsa ham (10), u endi kadr ehtiyoji emas.
            var yopilgan = Vacancy.Open(nom.Id, new DateOnly(2026, 2, 1), openings: 10).Value;
            yopilgan.Close(new DateOnly(2026, 5, 1), "Ўрин тўлди").IsSuccess.ShouldBeTrue();

            kontekst.Add(nom);
            kontekst.AddRange(uchOrin, tortOrin, yopilgan);
            await kontekst.SaveChangesAsync(bekorQilish);
        }

        var keyin = await SonlarAsync(client, "?period=month");
        keyin.Recruiting.ShouldNotBeNull();

        (keyin.Recruiting.OpenVacancies - oldin.Recruiting.OpenVacancies).ShouldBe(2);
        (keyin.Recruiting.Openings - oldin.Recruiting.Openings).ShouldBe(7);
    }

    /// <summary>
    /// Arizalar: asosiy son — JORIY holat, izohdagi son esa DAVRGA bog'liq.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Kartaning ikkita raqami ataylab boshqa savolga javob beradi.
    /// «Faol arizalar» — hozir voronkada kim turibdi (davr almashtirgichi
    /// unga tegmaydi), «yangi arizalar» — tanlangan oynada qancha oqim
    /// kelgani.
    /// </para>
    /// <para>
    /// RAD ETILGAN ariza faol emas, lekin KELGAN — ya'ni birinchi sonda
    /// yo'q, ikkinchisida bor. Aks holda o'tgan davrlarning raqami vaqt
    /// o'tishi bilan o'z-o'zidan kamayib borardi va taqqoslash ma'nosini
    /// yo'qotardi.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Arizalar_joriy_holat_yangilari_esa_davr_boyicha_sanaladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var belgi = Belgi();

        using var client = await AdminMijozAsync();

        var oldingiKun = await SonlarAsync(client, "?period=day");
        var oldingiOnBesh = await SonlarAsync(client, "?period=days15");
        oldingiKun.Recruiting.ShouldNotBeNull();
        oldingiOnBesh.Recruiting.ShouldNotBeNull();

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

            var nom = VacancyTitle.Create($"{belgi} Ҳисобчи").Value;
            var vakansiya = Vacancy.Open(nom.Id, new DateOnly(2026, 3, 1), openings: 5).Value;

            var birinchi = Nomzod(vakansiya.Id, "Биринчи", belgi);
            var ikkinchi = Nomzod(vakansiya.Id, "Иккинчи", belgi);

            // Bugun kelgan, keyin rad etilgan: FAOL emas, lekin KELGAN
            var radEtilgan = Nomzod(vakansiya.Id, "Радэтилган", belgi);
            radEtilgan.Reject(oldingiKun.To, "Тажрибаси етарли эмас").IsSuccess.ShouldBeTrue();

            // 10 kun oldin kelgan — «1 kun» oynasiga tushmaydi, «15 kun» ga tushadi
            var eski = Nomzod(vakansiya.Id, "Эски", belgi);

            kontekst.Add(nom);
            kontekst.Add(vakansiya);
            kontekst.AddRange(birinchi, ikkinchi, radEtilgan, eski);
            await kontekst.SaveChangesAsync(bekorQilish);

            // `CreatedAt` ni AuditableInterceptor saqlashda o'zi qo'yadi
            // (`Added` holatida doim «hozir»). Shu sababli «eski» ariza
            // ikkinchi saqlashda orqaga suriladi: `Modified` holatida
            // interceptor `CreatedAt` ga tegmaydi, faqat `UpdatedAt` ni
            // yozadi. Boshqa yo'l — soatni siljitish — butun API uchun
            // vaqtni o'zgartirardi va qo'shni testlarni buzardi.
            eski.CreatedAt = DateTimeOffset.UtcNow.AddDays(-10);
            await kontekst.SaveChangesAsync(bekorQilish);
        }

        var kun = await SonlarAsync(client, "?period=day");
        var onBesh = await SonlarAsync(client, "?period=days15");
        kun.Recruiting.ShouldNotBeNull();
        onBesh.Recruiting.ShouldNotBeNull();

        // Faol voronka: uchta (rad etilgani chiqib ketdi) — va bu son
        // DAVRGA BOG'LIQ EMAS, ikkala so'rovda ham bir xil
        (kun.Recruiting.ActiveCandidates - oldingiKun.Recruiting.ActiveCandidates).ShouldBe(3);
        (onBesh.Recruiting.ActiveCandidates - oldingiOnBesh.Recruiting.ActiveCandidates).ShouldBe(3);

        // Bugungi oyna: uchta yangi ariza (rad etilgani ham «kelgan»),
        // 10 kun oldingisi tashqarida
        (kun.Recruiting.NewCandidates - oldingiKun.Recruiting.NewCandidates).ShouldBe(3);

        // 15 kunlik oyna: eskisi ham qo'shiladi
        (onBesh.Recruiting.NewCandidates - oldingiOnBesh.Recruiting.NewCandidates).ShouldBe(4);
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    /// <summary>Voronkadagi yangi ariza (nomzod) — test uchun eng qisqa yo'l.</summary>
    private static Candidate Nomzod(Guid vakansiyaId, string familiya, string belgi)
        => Candidate.Register(FullName.Create(familiya, belgi, null).Value, vakansiyaId).Value;

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

    private static async Task<IReadOnlyList<DashboardBirthdayResponse>> TugilganKunlarAsync(HttpClient client)
    {
        using var javob = await client.GetAsync(
            new Uri("/api/dashboard/birthdays", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var royxat = await javob.Content.ReadFromJsonAsync<List<DashboardBirthdayResponse>>(
            TestContext.Current.CancellationToken);

        royxat.ShouldNotBeNull();
        return royxat;
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
