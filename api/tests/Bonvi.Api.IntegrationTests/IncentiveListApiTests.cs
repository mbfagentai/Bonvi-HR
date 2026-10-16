using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bonvi.Application.Identity.Abstractions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Catalog;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Umumiy ro'yxatning bitta qatori (test uchun).</summary>
/// <remarks>
/// <c>Kind</c> ATAYLAB satr: sanoq qiymatlari JSON'da nom bo'lib chiqadi
/// (<c>JsonStringEnumConverter</c>). Agar u raqamga qaytsa, bu test
/// deserializatsiyada yiqiladi va o'zgarish e'tibordan chetda qolmaydi —
/// frontend enum'ni nomi bo'yicha o'qiydi.
///
/// Summa tipi (<c>IncentiveMoneyResponse</c>) qayta e'lon qilinmaydi —
/// u <c>IncentiveApiTests</c> da allaqachon bor va ikkalasi bir xil
/// <c>MoneyDto</c> ni o'qiydi.
/// </remarks>
public sealed record IncentiveListItemResponse(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    string? PhotoKey,
    string Kind,
    Guid TypeId,
    string? TypeName,
    DateOnly Date,
    IncentiveMoneyResponse? Amount,
    decimal? Percent,
    string? OrderNo,
    DateOnly? OrderDate);

/// <summary>Sahifalangan rag'bat/jarima javobi (test uchun).</summary>
public sealed record IncentiveListPageResponse(
    IReadOnlyList<IncentiveListItemResponse> Items,
    int Page,
    int PageSize,
    int Total,
    int PageCount);

/// <summary>
/// «Rag'bat va jarima» umumiy ro'yxatining uchi-uchiga testlari.
/// </summary>
/// <remarks>
/// <para>
/// Bu ro'yxatdagi xato darhol ko'rinmaydi: HR «shu oyda ikkita jarima
/// bo'lgan» degan noto'g'ri javobni to'g'ri deb qabul qiladi va uchinchisi
/// tizimda bo'la turib hech qachon ko'rinmay qoladi. Aynan shuning uchun
/// filtr, saralash, sahifalash va ruxsat testda qadalgan.
/// </para>
/// <para>
/// Testlar bitta bazani baham ko'radi va unda boshqa testlarning yozuvlari
/// ham bor. Shu sababli har bir test O'ZIGA XOS belgi bilan xodim
/// yaratadi va ro'yxatni <c>search</c> (yoki shu urug'ga tegishli
/// bo'lim/firma) bilan cheklaydi — aks holda «total» qo'shni testning
/// yozuviga bog'liq bo'lib qolardi.
/// </para>
/// </remarks>
public sealed class IncentiveListApiTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public IncentiveListApiTests(BonviApiFactory factory) => _factory = factory;

    /// <summary>Bitta testning barcha yozuvlarini ajratib turadigan belgi.</summary>
    /// <remarks>
    /// Faqat lotin harflari va raqamlar: belgi xodimning otasining ismiga
    /// yoziladi va ism bo'yicha qidiruvda ishlatiladi — kirill «yig'ish»
    /// qoidasi (<c>қ→к</c>) unga tegmasligi kerak.
    /// </remarks>
    private static string Belgi() => "RJ" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    // ────────────────────────────────────────────────────────────
    //  Ro'yxatning o'zi
    // ────────────────────────────────────────────────────────────

    /// <summary>Filtrsiz so'rov sahifalangan shaklda va yangidan eskiga qaytadi.</summary>
    [Fact]
    public async Task Royxat_yangidan_eskiga_saralangan_holda_qaytadi()
    {
        var belgi = Belgi();
        var urug = await UrugAsync(belgi);

        using var client = await AdminMijozAsync();

        var sahifa = await SoravAsync(client, $"?search={Matn(belgi)}");

        sahifa.Page.ShouldBe(1);
        sahifa.PageSize.ShouldBe(25);
        sahifa.Total.ShouldBe(3);

        // Sukut bo'yicha tartib — sana bo'yicha KAMAYISH («oxirgi paytda
        // nima bo'ldi» degan savolga javob beradi).
        sahifa.Items.Select(qator => qator.Date).ShouldBe(
        [
            new DateOnly(2026, 8, 26),
            new DateOnly(2026, 7, 14),
            new DateOnly(2026, 7, 2),
        ]);

        // Xodim nomi va tur nomi ro'yxatning O'ZIDA bo'lishi kerak: ular
        // uchun qatordan xodim kartasiga yoki ma'lumotnomaga qayta so'rov
        // yuborilmaydi.
        sahifa.Items.ShouldAllBe(qator => !string.IsNullOrWhiteSpace(qator.EmployeeName));
        sahifa.Items.ShouldAllBe(qator => !string.IsNullOrWhiteSpace(qator.TypeName));

        sahifa.Items[0].TypeName.ShouldBe(urug.TashakkurNomi);
        sahifa.Items[0].OrderNo.ShouldBe(belgi + "-142");
        sahifa.Items[0].EmployeeId.ShouldBe(urug.AbduqodirovId);
    }

    /// <summary>
    /// Bitta xodim ro'yxatda bir necha marta ko'rinadi.
    /// </summary>
    /// <remarks>
    /// Bu ATAYLAB shunday va foydalanuvchi bilan kelishilgan: ro'yxat
    /// yig'ma ko'rsatkich («nechta rag'bat») emas, FAKTlar oqimi. Agar
    /// kimdir kelajakda xodim bo'yicha guruhlashni qo'shsa, bu test
    /// yiqiladi — o'shanda qaror qayta muhokama qilinishi kerak.
    /// </remarks>
    [Fact]
    public async Task Bitta_xodim_bir_necha_marta_chiqadi()
    {
        var belgi = Belgi();
        var urug = await UrugAsync(belgi);

        using var client = await AdminMijozAsync();

        // Qidiruv ism bo'yicha: «Абдуқодиров» kirill «қ» bilan yozilgan,
        // qidiruvda esa «к» — HR rus klaviaturasida shunday yozadi va
        // yozuv baribir topilishi kerak (`SearchFolding` izohi).
        var sahifa = await SoravAsync(client, $"?search={Matn("Абдукодиров Шерзод " + belgi)}");

        sahifa.Total.ShouldBe(2);
        sahifa.Items.ShouldAllBe(qator => qator.EmployeeId == urug.AbduqodirovId);
    }

    // ────────────────────────────────────────────────────────────
    //  Filtrlar
    // ────────────────────────────────────────────────────────────

    /// <summary>Tur bo'yicha filtr — «91.14» varag'ining o'rni va uning aksi.</summary>
    [Fact]
    public async Task Kind_boyicha_filtr_ishlaydi()
    {
        var belgi = Belgi();
        var urug = await UrugAsync(belgi);

        using var client = await AdminMijozAsync();

        var jarima = await SoravAsync(client, $"?search={Matn(belgi)}&kind=Penalty");

        jarima.Total.ShouldBe(1);
        jarima.Items[0].Kind.ShouldBe("Penalty");
        jarima.Items[0].EmployeeId.ShouldBe(urug.KarimovId);
        jarima.Items[0].TypeName.ShouldBe(urug.HayfsanNomi);

        var ragbat = await SoravAsync(client, $"?search={Matn(belgi)}&kind=Award");

        ragbat.Total.ShouldBe(2);
        ragbat.Items.ShouldAllBe(qator => qator.Kind == "Award");
    }

    /// <summary>Sana oralig'i ikki tomondan yopiq: chegaradagi kun ham kiradi.</summary>
    [Fact]
    public async Task Sana_oraligi_chegara_kunini_ham_oladi()
    {
        var belgi = Belgi();
        await UrugAsync(belgi);

        using var client = await AdminMijozAsync();

        var oraliq = await SoravAsync(client, $"?search={Matn(belgi)}&from=2026-07-14&to=2026-07-14");

        oraliq.Total.ShouldBe(1);
        oraliq.Items[0].Date.ShouldBe(new DateOnly(2026, 7, 14));
    }

    /// <summary>
    /// Bo'lim filtri xodimning munosabati orqali va ICHKI bo'limlar bilan ishlaydi.
    /// </summary>
    /// <remarks>
    /// Rag'bat yozuvida bo'lim ustuni umuman yo'q — u xodimga bog'langan.
    /// Shu sababli bu test aslida ikkita narsani tekshiradi: shart
    /// munosabatga o'tadimi va shox (ota bo'lim → ichki bo'lim) hisobga
    /// olinadimi.
    /// </remarks>
    [Fact]
    public async Task Bolim_filtri_ichki_bolimni_ham_oladi()
    {
        var belgi = Belgi();
        var urug = await UrugAsync(belgi);

        using var client = await AdminMijozAsync();

        var ichki = await SoravAsync(client, $"?search={Matn(belgi)}&departmentId={urug.IchkiBolimId}");
        ichki.Total.ShouldBe(2, "Абдуқодировнинг иккита ёзуви ички бўлимда");

        var bosh = await SoravAsync(client, $"?search={Matn(belgi)}&departmentId={urug.BoshBolimId}");
        bosh.Total.ShouldBe(2, "ота бўлим орқали ҳам топилиши керак");

        var begona = await SoravAsync(client, $"?search={Matn(belgi)}&departmentId={Guid.NewGuid()}");
        begona.Total.ShouldBe(0);
        begona.Items.ShouldBeEmpty();
    }

    /// <summary>Yuridik shaxs filtri ham munosabat orqali qo'llanadi.</summary>
    [Fact]
    public async Task Yuridik_shaxs_boyicha_filtr_ishlaydi()
    {
        var belgi = Belgi();
        var urug = await UrugAsync(belgi);

        using var client = await AdminMijozAsync();

        var birinchi = await SoravAsync(client, $"?search={Matn(belgi)}&legalEntityId={urug.FirmaId}");
        birinchi.Total.ShouldBe(2);

        var ikkinchi = await SoravAsync(client, $"?search={Matn(belgi)}&legalEntityId={urug.BoshqaFirmaId}");
        ikkinchi.Total.ShouldBe(1);
        ikkinchi.Items[0].EmployeeId.ShouldBe(urug.KarimovId);
    }

    /// <summary>
    /// Bo'lim va firma BIRGA qo'llansa — bitta munosabatga tushishi shart.
    /// </summary>
    /// <remarks>
    /// Eng nozik holat: shartlar ikki alohida ro'yxat bo'lib kesishtirilsa,
    /// «bir firmada omborchi, boshqasida sotuvchi» bo'lgan o'rindosh
    /// «VIKOR'dagi Sotuv bo'limi» so'roviga noto'g'ri tushib qolardi.
    /// Shu sababli mos kelmaydigan juftlik BO'SH natija berishi kerak.
    /// </remarks>
    [Fact]
    public async Task Bolim_va_firma_bitta_munosabatga_qollanadi()
    {
        var belgi = Belgi();
        var urug = await UrugAsync(belgi);

        using var client = await AdminMijozAsync();

        var mos = await SoravAsync(
            client,
            $"?search={Matn(belgi)}&departmentId={urug.IchkiBolimId}&legalEntityId={urug.FirmaId}");
        mos.Total.ShouldBe(2);

        // Bo'lim birinchi firmada, tanlangan firma esa ikkinchisi —
        // bunday munosabat yo'q.
        var nomos = await SoravAsync(
            client,
            $"?search={Matn(belgi)}&departmentId={urug.IchkiBolimId}&legalEntityId={urug.BoshqaFirmaId}");
        nomos.Total.ShouldBe(0);
    }

    /// <summary>Noma'lum «kind» 500 emas, 400 qaytaradi.</summary>
    [Fact]
    public async Task Nomalum_kind_400_qaytaradi()
    {
        using var client = await AdminMijozAsync();

        using var javob = await client.GetAsync(
            new Uri("/api/incentives?kind=Boshqa", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ────────────────────────────────────────────────────────────
    //  Saralash
    // ────────────────────────────────────────────────────────────

    /// <summary>Xodim nomi bo'yicha saralash ikki yo'nalishda ham ishlaydi.</summary>
    [Fact]
    public async Task Xodim_nomi_boyicha_saralash_ishlaydi()
    {
        var belgi = Belgi();
        var urug = await UrugAsync(belgi);

        using var client = await AdminMijozAsync();

        var osish = await SoravAsync(client, $"?search={Matn(belgi)}&sort=employee");
        osish.Items[0].EmployeeId.ShouldBe(urug.AbduqodirovId, "«А» «К» дан олдин");

        var kamayish = await SoravAsync(client, $"?search={Matn(belgi)}&sort=-employee");
        kamayish.Items[0].EmployeeId.ShouldBe(urug.KarimovId);
    }

    /// <summary>
    /// Noma'lum saralash ustuni xato emas, sukut bo'yicha tartib beradi.
    /// </summary>
    /// <remarks>
    /// Jadval sarlavhasini bosgan foydalanuvchi «400 Bad Request» ko'rishi
    /// kerak emas — ro'yxat ochilaveradi. Shu bilan birga <c>sort=date</c>
    /// aniq so'ralgan O'SISH tartibini beradi: sukut (kamayish) bilan
    /// adashtirilmasligi kerak.
    /// </remarks>
    [Fact]
    public async Task Nomalum_saralash_sukut_tartibiga_qaytadi()
    {
        var belgi = Belgi();
        await UrugAsync(belgi);

        using var client = await AdminMijozAsync();

        var nomalum = await SoravAsync(client, $"?search={Matn(belgi)}&sort=allakachon-yoq");
        nomalum.Items[0].Date.ShouldBe(new DateOnly(2026, 8, 26), "сукут — янгидан эскига");

        var osish = await SoravAsync(client, $"?search={Matn(belgi)}&sort=date");
        osish.Items[0].Date.ShouldBe(new DateOnly(2026, 7, 2), "«date» — аниқ сўралган ўсиш тартиби");
    }

    // ────────────────────────────────────────────────────────────
    //  Sahifalash
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Sahifalash bazada bajariladi va «total» BUTUN to'plamni sanaydi.
    /// </summary>
    /// <remarks>
    /// Eng ko'p uchraydigan xato: <c>Total</c> sahifadagi qatorlar sonini
    /// qaytaradi. Unda interfeys «1–2 / 2» deb yozadi va uchinchi yozuv
    /// HR uchun umuman mavjud bo'lmay qoladi.
    /// </remarks>
    [Fact]
    public async Task Sahifalash_total_ni_togri_qaytaradi()
    {
        var belgi = Belgi();
        await UrugAsync(belgi);

        using var client = await AdminMijozAsync();

        var birinchi = await SoravAsync(client, $"?search={Matn(belgi)}&page=1&pageSize=2");

        birinchi.Total.ShouldBe(3);
        birinchi.PageCount.ShouldBe(2);
        birinchi.Page.ShouldBe(1);
        birinchi.PageSize.ShouldBe(2);
        birinchi.Items.Count.ShouldBe(2);

        var ikkinchi = await SoravAsync(client, $"?search={Matn(belgi)}&page=2&pageSize=2");

        ikkinchi.Total.ShouldBe(3);
        ikkinchi.Items.Count.ShouldBe(1);

        // Sahifalar KESISHMASLIGI kerak: teng sanalarda ikkinchi saralash
        // mezoni bo'lmasa, bitta yozuv ikkala sahifada ham chiqib qolardi.
        var hammasi = birinchi.Items.Concat(ikkinchi.Items).Select(qator => qator.Id).ToList();
        hammasi.Distinct().Count().ShouldBe(3);
    }

    // ────────────────────────────────────────────────────────────
    //  Summa
    // ────────────────────────────────────────────────────────────

    /// <summary>Pulsiz turda summa <c>null</c>, pul turida to'ldirilgan.</summary>
    /// <remarks>
    /// Bu bir vaqtning o'zida <c>Money</c> ustunlarining (summa + valyuta)
    /// proyeksiyadan to'g'ri yig'ilishini ham tekshiradi — u faqat haqiqiy
    /// bazada ko'rinadi.
    /// </remarks>
    [Fact]
    public async Task Pulsiz_turda_summa_bosh_qaytadi()
    {
        var belgi = Belgi();
        await UrugAsync(belgi);

        using var client = await AdminMijozAsync();

        var sahifa = await SoravAsync(client, $"?search={Matn(belgi)}&sort=date");

        // Eng eski yozuv — «Пул мукофоти», summasi bilan
        var pulli = sahifa.Items[0];
        pulli.Amount.ShouldNotBeNull();
        pulli.Amount.Amount.ShouldBe(1_500_000m);
        pulli.Amount.Currency.ShouldBe("UZS");
        pulli.Amount.Formatted.ShouldNotBeNullOrWhiteSpace();

        // Qolgan ikkitasi pulsiz turlar — «Ҳайфсан» va «Ташаккурнома»
        sahifa.Items.Skip(1).ShouldAllBe(qator => qator.Amount == null);
    }

    // ────────────────────────────────────────────────────────────
    //  Ruxsatlar
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Tokensiz_royxat_401_qaytaradi()
    {
        using var client = _factory.CreateClient();

        using var javob = await client.GetAsync(
            new Uri("/api/incentives", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// <c>incentives.view</c> yo'q foydalanuvchi ro'yxatni umuman ko'rmaydi.
    /// </summary>
    /// <remarks>
    /// Aynan shu holat eng xavflisi: token haqiqiy, autentifikatsiya
    /// o'tadi — avtorizatsiya esa endpoint darajasida to'xtatishi kerak.
    /// Ro'yxatda jarima va mukofot summasi bor, ya'ni sizish jimgina
    /// bo'lardi.
    /// </remarks>
    [Fact]
    public async Task Ruxsati_yoq_foydalanuvchi_403_oladi()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await RuxsatsizTokenAsync());

        using var javob = await client.GetAsync(
            new Uri("/api/incentives", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    /// <summary>Test ma'lumotining kalitlari.</summary>
    private sealed record Urug(
        Guid FirmaId,
        Guid BoshqaFirmaId,
        Guid BoshBolimId,
        Guid IchkiBolimId,
        Guid AbduqodirovId,
        Guid KarimovId,
        string TashakkurNomi,
        string HayfsanNomi);

    /// <summary>
    /// Ikki xodim, ikki firma, ikki qavatli bo'lim daraxti va uchta yozuv.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ma'lumot foydalanuvchi tasdiqlagan jadval namunasini takrorlaydi:
    /// 26.08 Абдуқодиров / Ташаккурнома / — · 14.07 Каримов / Ҳайфсан / — ·
    /// 02.07 Абдуқодиров / Пул мукофоти / 1 500 000.
    /// </para>
    /// <para>
    /// Yozuvlar HTTP orqali emas, to'g'ridan-to'g'ri baza orqali qo'yiladi
    /// (<c>EmploymentEventsApiTests</c> dagi bilan bir xil naqsh): testga
    /// aynan kerakli sana va tur kerak, endpoint esa har bir yozuv uchun
    /// alohida so'rov va qo'shimcha tayyorgarlik talab qilardi. Domen
    /// qoidalari baribir ishlaydi — yozuvlar <c>Incentive.Grant</c> orqali
    /// yaratiladi.
    /// </para>
    /// </remarks>
    private async Task<Urug> UrugAsync(string belgi)
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var firma = LegalEntity.Create("Рағбат Тест МЧЖ " + belgi).Value;
        var boshqaFirma = LegalEntity.Create("Бошқа Тест МЧЖ " + belgi).Value;

        var bosh = Department.Create("Бош " + belgi).Value;
        var ichki = Department.Create("Ички " + belgi, bosh.Id).Value;
        var lavozim = Position.Create("Мутахассис " + belgi).Value;

        var pulMukofoti = AwardType.Create("Пул мукофоти " + belgi).Value;
        pulMukofoti.SetHasAmount(true);

        var tashakkur = AwardType.Create("Ташаккурнома " + belgi).Value;
        tashakkur.SetHasAmount(false);

        var hayfsan = PenaltyType.Create("Ҳайфсан " + belgi).Value;
        hayfsan.SetHasAmount(false);

        var abduqodirov = Employee.Create(FullName.Create("Абдуқодиров", "Шерзод", belgi).Value).Value;
        var karimov = Employee.Create(FullName.Create("Каримов", "Азиз", belgi).Value).Value;

        var birinchiMunosabat = Employment.Hire(
            abduqodirov.Id,
            firma.Id,
            new DateOnly(2024, 2, 1),
            ichki.Id,
            lavozim.Id,
            orderNo: belgi + "-1").Value;

        // Каримов boshqa firmada va bo'limsiz: yuridik shaxs filtri
        // haqiqatan ajratayotganini, bo'lim filtri esa uni tortib
        // olmayotganini ko'rsatadi.
        var ikkinchiMunosabat = Employment.Hire(
            karimov.Id,
            boshqaFirma.Id,
            new DateOnly(2024, 3, 1),
            departmentId: null,
            lavozim.Id,
            orderNo: belgi + "-2").Value;

        var mukofot = Incentive.Grant(
            abduqodirov.Id,
            IncentiveKind.Award,
            pulMukofoti.Id,
            new DateOnly(2026, 7, 2),
            typeHasAmount: true,
            amount: Money.Create(1_500_000m).Value,
            orderNo: belgi + "-101").Value;

        var tashakkurnoma = Incentive.Grant(
            abduqodirov.Id,
            IncentiveKind.Award,
            tashakkur.Id,
            new DateOnly(2026, 8, 26),
            typeHasAmount: false,
            orderNo: belgi + "-142").Value;

        var jarima = Incentive.Grant(
            karimov.Id,
            IncentiveKind.Penalty,
            hayfsan.Id,
            new DateOnly(2026, 7, 14),
            typeHasAmount: false,
            reason: "Кечикиш " + belgi).Value;

        kontekst.AddRange(firma, boshqaFirma);
        kontekst.AddRange(bosh, ichki);
        kontekst.Add(lavozim);
        kontekst.AddRange(pulMukofoti, tashakkur);
        kontekst.Add(hayfsan);
        kontekst.AddRange(abduqodirov, karimov);
        kontekst.AddRange(birinchiMunosabat, ikkinchiMunosabat);
        kontekst.AddRange(mukofot, tashakkurnoma, jarima);

        await kontekst.SaveChangesAsync(bekorQilish);

        return new Urug(
            firma.Id,
            boshqaFirma.Id,
            bosh.Id,
            ichki.Id,
            abduqodirov.Id,
            karimov.Id,
            tashakkur.Name,
            hayfsan.Name);
    }

    private static async Task<IncentiveListPageResponse> SoravAsync(HttpClient client, string soralgan)
    {
        using var javob = await client.GetAsync(
            new Uri("/api/incentives" + soralgan, UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var sahifa = await javob.Content.ReadFromJsonAsync<IncentiveListPageResponse>(
            TestContext.Current.CancellationToken);

        sahifa.ShouldNotBeNull();
        return sahifa;
    }

    /// <summary>Kirill matnini manzil satriga xavfsiz joylaydi.</summary>
    private static string Matn(string value) => Uri.EscapeDataString(value);

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

    /// <summary>
    /// Haqiqiy, lekin ruxsatlarsiz access token yasaydi.
    /// </summary>
    /// <remarks>
    /// Bazada faqat administrator bor va unda barcha ruxsatlar mavjud.
    /// Shu sababli token to'g'ridan-to'g'ri token servisidan, bo'sh ruxsat
    /// to'plami bilan olinadi — bu «ruxsati kam foydalanuvchi» holatini
    /// yangi rol yaratmasdan aniq takrorlaydi (<c>CatalogApiTests</c>
    /// dagi bilan bir xil naqsh).
    /// <para>
    /// 2026-09-26 dan token tekshiruvi ruxsatlarni BAZADAN oladi — tokenga
    /// bo'sh ro'yxat yozish endi ishlamaydi. Shu sababli ruxsatsiz rolli
    /// haqiqiy hisob yaratiladi (<see cref="TestAccounts"/>).
    /// </para>
    /// </remarks>
    private Task<string> RuxsatsizTokenAsync() => TestAccounts.TokenAsync(_factory);

    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
