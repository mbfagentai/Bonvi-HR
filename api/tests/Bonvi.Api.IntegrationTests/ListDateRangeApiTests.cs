using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Catalog;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// HR ro'yxatlaridagi «dan–gacha» sana oralig'i (egasi, 2026-10-20) — uchi-uchiga.
/// </summary>
/// <remarks>
/// <para>
/// Har bir ro'yxat bir xil to'rt savolga javob beradi: oraliq ichidagi yozuv
/// chiqadimi, tashqaridagisi chiqmaydimi, bitta chegara bilan (ochiq oraliq)
/// ishlaydimi va teskari oraliq 400 beradimi. Chegaradagi kun ham kiradi —
/// bu ham har birida alohida qatorda.
/// </para>
/// <para>
/// ⭐ <b>Eng muhimi — QAYSI sana.</b> Bitta endpoint bir nechta ro'yxatga
/// xizmat qiladi va sana ma'nosi parametrga bog'liq: «Ishdan ketganlar»da
/// ketish sanasi, «Rad etilganlar»da voronkadan chiqqan sana. Shu sababli
/// urug'da ikkinchi sana ATAYLAB oraliqqa tushadigan qilib qo'yilgan — noto'g'ri
/// ustun tanlansa, test aynan o'sha qatorda yiqiladi.
/// </para>
/// <para>
/// Sinf bazasi umumiy: har bir ishga tushirish o'z belgisi bilan yozuv yaratadi
/// va ro'yxatni shu belgi (qidiruv, vakansiya yoki xodim) bilan cheklaydi
/// (<c>IncentiveListApiTests</c> dagi naqsh).
/// </para>
/// </remarks>
public sealed class ListDateRangeApiTests : IClassFixture<BonviApiFactory>
{
    private const string OraliqXatosi = "royxat.sana_oraligi_notogri";
    private const string DavomatDavrXatosi = "davomat.davr_notogri";

    private readonly BonviApiFactory _factory;

    public ListDateRangeApiTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Lotin harflari va raqamlar — kirill «yig'ish» qoidasi unga tegmaydi.</summary>
    private static string Belgi() => "SO" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    // ────────────────────────────────────────────────────────────
    //  Xodimlar va ishdan ketganlar — GET /api/employees
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// «Xodimlar» — ishga kirish sanasi, «Ishdan ketganlar» — ketish sanasi.
    /// </summary>
    /// <remarks>
    /// C 2025-01-05 da ishga kirgan va 2026-04-15 da ketgan: yanvar 2025
    /// oralig'ida «Ishdan ketganlar» BO'SH bo'lishi kerak — ishga kirish
    /// sanasi bo'yicha filtrlansa, aynan shu qator buzilardi.
    /// </remarks>
    [Theory]
    [InlineData("status=employed&from=2026-03-01&to=2026-03-31", "A")]
    [InlineData("status=employed&from=2026-03-10&to=2026-03-10", "A")]
    [InlineData("status=employed&from=2026-05-01", "B")]
    [InlineData("status=employed&to=2026-04-30", "A")]
    [InlineData("status=employed&from=2026-06-01", "")]
    [InlineData("status=employed", "A,B")]
    [InlineData("status=Probation&from=2026-05-20&to=2026-05-20", "B")]
    [InlineData("status=Terminated&from=2026-04-01&to=2026-04-30", "C")]
    [InlineData("status=Terminated&from=2025-01-01&to=2025-01-31", "")]
    [InlineData("status=Terminated&to=2026-04-15", "C")]
    [InlineData("status=Terminated&from=2026-04-16", "")]
    public async Task Xodimlar_royxati_holatga_mos_sana_boyicha_filtrlanadi(string sorov, string kutilgan)
    {
        var belgi = Belgi();
        var urug = await XodimlarUrugiAsync(belgi);

        using var client = await AdminMijozAsync();

        var topilgan = await IdlarAsync(client, $"/api/employees?search={belgi}&{sorov}", "id");

        topilgan.ShouldBe(Kutilgan(urug, kutilgan), ignoreOrder: true);
    }

    /// <summary>Umumiy son va sahifalar soni ham filtrdan keyin sanaladi.</summary>
    [Fact]
    public async Task Xodimlar_sanogi_va_sahifalari_oraliqqa_mos()
    {
        var belgi = Belgi();
        await XodimlarUrugiAsync(belgi);

        using var client = await AdminMijozAsync();

        var sahifa = await JsonAsync(client, $"/api/employees?search={belgi}&status=employed&from=2026-01-01&pageSize=1");

        sahifa.GetProperty("total").GetInt32().ShouldBe(2);
        sahifa.GetProperty("pageCount").GetInt32().ShouldBe(2);
        sahifa.GetProperty("items").GetArrayLength().ShouldBe(1);

        var tor = await JsonAsync(client, $"/api/employees?search={belgi}&status=employed&from=2026-05-01&pageSize=1");

        tor.GetProperty("total").GetInt32().ShouldBe(1);
        tor.GetProperty("pageCount").GetInt32().ShouldBe(1);
    }

    /// <summary>
    /// Sana ro'yxatda KO'RINADIGAN (asosiy) munosabatniki.
    /// </summary>
    /// <remarks>
    /// D 2015-yildan asosiy ishida, 2026-04-01 dan o'rindosh. «Aprelda ishga
    /// kirganlar» oralig'ida u chiqmasligi kerak: qatorda 2015-yil turadi va
    /// HR oraliqdan tashqaridagi sanani ko'rib, filtr buzilgan deb o'ylardi.
    /// </remarks>
    [Fact]
    public async Task Orindoshlikdagi_yangi_munosabat_eski_xodimni_oraliqqa_olib_kirmaydi()
    {
        var belgi = Belgi();
        Guid xodimId;

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

            var firma = LegalEntity.Create("Сана МЧЖ " + belgi).Value;
            var boshqaFirma = LegalEntity.Create("Сана Бошқа МЧЖ " + belgi).Value;
            var xodim = Employee.Create(FullName.Create("Санаов", "Дилшод", belgi).Value).Value;

            var asosiy = Employment.Hire(xodim.Id, firma.Id, new DateOnly(2015, 2, 1)).Value;
            asosiy.CompleteProbation(new DateOnly(2015, 5, 1)).IsSuccess.ShouldBeTrue();

            var orindosh = Employment.Hire(xodim.Id, boshqaFirma.Id, new DateOnly(2026, 4, 1)).Value;
            orindosh.SetPrimary(false);

            kontekst.AddRange(firma, boshqaFirma, xodim);
            kontekst.AddRange(asosiy, orindosh);
            await kontekst.SaveChangesAsync(Ct);

            xodimId = xodim.Id;
        }

        using var client = await AdminMijozAsync();

        (await IdlarAsync(client, $"/api/employees?search={belgi}&status=employed&from=2026-04-01&to=2026-04-30", "id"))
            .ShouldBeEmpty();

        var eski = await JsonAsync(client, $"/api/employees?search={belgi}&status=employed&from=2015-02-01&to=2015-02-01");
        var qator = eski.GetProperty("items").EnumerateArray().ShouldHaveSingleItem();

        qator.GetProperty("id").GetGuid().ShouldBe(xodimId);
        qator.GetProperty("hireDate").GetString().ShouldBe("2015-02-01");
    }

    /// <summary>Excel eksporti ekrandagi oraliq bilan aynan bir xil qatorlarni beradi.</summary>
    [Fact]
    public async Task Xodimlar_eksporti_oraliqqa_amal_qiladi()
    {
        var belgi = Belgi();
        var urug = await XodimlarUrugiAsync(belgi);

        using var client = await AdminMijozAsync();

        using var javob = await client.GetAsync(
            Yol($"/api/reports/employees/export?search={belgi}&status=Terminated&from=2026-04-01&to=2026-04-30"),
            Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var baytlar = await javob.Content.ReadAsByteArrayAsync(Ct);
        using var kitob = new XLWorkbook(new MemoryStream(baytlar));
        var varaq = kitob.Worksheet("Xodimlar");

        List<string> ismlar = [];
        for (var qator = 2; !varaq.Cell(qator, 1).IsEmpty(); qator++)
        {
            ismlar.Add(varaq.Cell(qator, 1).GetString());
        }

        ismlar.ShouldBe([urug.Ismlar["C"]]);
    }

    [Theory]
    [InlineData("/api/employees?from=2026-05-02&to=2026-05-01")]
    [InlineData("/api/employees?status=Terminated&from=2026-05-02&to=2026-05-01")]
    [InlineData("/api/reports/employees/export?from=2026-05-02&to=2026-05-01")]
    [InlineData("/api/recruiting/candidates?from=2026-05-02&to=2026-05-01")]
    [InlineData("/api/recruiting/candidates?rejected=true&from=2026-05-02&to=2026-05-01")]
    [InlineData("/api/probations?from=2026-05-02&to=2026-05-01")]
    [InlineData("/api/employees/flagged?type=Reserve&from=2026-05-02&to=2026-05-01")]
    public async Task Teskari_oraliq_400_va_royxat_kodini_beradi(string manzil)
    {
        using var client = await AdminMijozAsync();

        using var javob = await client.GetAsync(Yol(manzil), Ct);

        await XatoKutAsync(javob, HttpStatusCode.BadRequest, OraliqXatosi);
    }

    /// <summary>
    /// Davomat so'rovlari o'z mavjud kodini saqlaydi — web uni allaqachon biladi.
    /// </summary>
    [Theory]
    [InlineData("/api/attendance/requests?from=2026-05-02&to=2026-05-01")]
    [InlineData("/api/attendance/schedule-requests?from=2026-05-02&to=2026-05-01")]
    public async Task Sorovlarda_teskari_oraliq_davomat_kodini_beradi(string manzil)
    {
        using var client = await AdminMijozAsync();

        using var javob = await client.GetAsync(Yol(manzil), Ct);

        await XatoKutAsync(javob, HttpStatusCode.BadRequest, DavomatDavrXatosi);
    }

    /// <summary>Bir kunlik oraliq ham, chegarasiz oraliq ham — xato emas.</summary>
    [Theory]
    [InlineData("/api/employees?from=2026-05-01&to=2026-05-01")]
    [InlineData("/api/employees?from=0001-01-01&to=9999-12-31")]
    [InlineData("/api/recruiting/candidates?from=0001-01-01&to=9999-12-31")]
    [InlineData("/api/probations?from=0001-01-01&to=9999-12-31")]
    [InlineData("/api/employees/flagged?type=Reserve&from=0001-01-01&to=9999-12-31")]
    public async Task Chekka_sanalar_500_bermaydi(string manzil)
    {
        using var client = await AdminMijozAsync();

        using var javob = await client.GetAsync(Yol(manzil), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
    }

    // ────────────────────────────────────────────────────────────
    //  Nomzodlar va rad etilganlar — GET /api/recruiting/candidates
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// «Nomzodlar» — ariza lahzasi TOSHKENT kuni bo'yicha; «Rad etilganlar» — voronkadan chiqqan sana.
    /// </summary>
    /// <remarks>
    /// <para>
    /// N1 UTC bo'yicha 9-oktabr 19:30 da yaratilgan — Toshkentda bu 10-oktabr
    /// 00:30, ya'ni 10-oktabr filtrida chiqishi, 9-oktabrda chiqmasligi
    /// kerak. N2 esa aksincha (Toshkentda 9-oktabr 23:30).
    /// </para>
    /// <para>
    /// R1 10-oktabrda ariza bergan, lekin 15-sentyabrda rad etilgan:
    /// «Rad etilganlar»ning 10-oktabr oralig'i BO'SH bo'lishi kerak.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("from=2026-10-10&to=2026-10-10", "N1")]
    [InlineData("from=2026-10-09&to=2026-10-09", "N2")]
    [InlineData("from=2026-10-10", "N1,N3")]
    [InlineData("to=2026-10-09", "N2")]
    [InlineData("from=2026-10-20&to=2026-10-20", "N3")]
    [InlineData("from=2026-10-21", "")]
    [InlineData("", "N1,N2,N3")]
    [InlineData("rejected=true&from=2026-09-01&to=2026-09-30", "R1")]
    [InlineData("rejected=true&from=2026-09-15&to=2026-09-15", "R1")]
    [InlineData("rejected=true&from=2026-10-10&to=2026-10-10", "")]
    [InlineData("rejected=true&from=2026-10-01", "R2")]
    [InlineData("rejected=true&to=2026-09-30", "R1")]
    [InlineData("rejected=true", "R1,R2")]
    public async Task Nomzodlar_royxati_bolimga_mos_sana_boyicha_filtrlanadi(string sorov, string kutilgan)
    {
        var urug = await NomzodlarUrugiAsync(Belgi());

        using var client = await AdminMijozAsync();

        var topilgan = await IdlarAsync(client, $"/api/recruiting/candidates?vacancyId={urug.VakansiyaId}&{sorov}", "id");

        topilgan.ShouldBe(Kutilgan(urug.Idlar, kutilgan), ignoreOrder: true);
    }

    /// <summary>
    /// «Rezerv» / «Qora ro'yxat» (`closedDate=true`) — ro'yxatga TUSHGAN sana bo'yicha (egasi, 2026-10-21).
    /// </summary>
    /// <remarks>
    /// Ikkalasi 10-oktabrda ariza bergan, lekin Z1 20-sentyabrda rezervga, Q1 25-sentyabrda qora
    /// ro'yxatga tushgan: `closedDate=true` bilan 10-oktabr oralig'i BO'SH, sentyabr — ular.
    /// </remarks>
    [Theory]
    [InlineData("status=Reserve&closedDate=true&from=2026-09-01&to=2026-09-30", "Z1")]
    [InlineData("status=Reserve&closedDate=true&from=2026-10-10&to=2026-10-10", "")]
    [InlineData("status=Reserve&from=2026-10-10&to=2026-10-10", "Z1")]
    [InlineData("status=Blacklisted&closedDate=true&from=2026-09-25&to=2026-09-25", "Q1")]
    [InlineData("status=Blacklisted&closedDate=true&to=2026-09-24", "")]
    public async Task Rezerv_va_qora_royxat_tushgan_sana_boyicha(string sorov, string kutilgan)
    {
        var belgi = Belgi();

        Guid vakansiyaId, z1Id, q1Id;
        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
            var vakansiya = Vacancy.Open("Рўйхат санаси " + belgi, new DateOnly(2026, 8, 1), openings: 5).Value;
            var z1 = Candidate.Register(FullName.Create("Номзодов", "Зафар", belgi).Value, vakansiya.Id).Value;
            z1.MoveToReserve(new DateOnly(2026, 9, 20)).IsSuccess.ShouldBeTrue();
            var q1 = Candidate.Register(FullName.Create("Номзодов", "Қосим", belgi).Value, vakansiya.Id).Value;
            q1.Blacklist(new DateOnly(2026, 9, 25), "sinov").IsSuccess.ShouldBeTrue();

            kontekst.Add(vakansiya);
            kontekst.AddRange(z1, q1);
            await kontekst.SaveChangesAsync(Ct);
            await YaratilganAsync(kontekst, z1.Id, new DateTimeOffset(2026, 10, 10, 5, 0, 0, TimeSpan.Zero));
            await YaratilganAsync(kontekst, q1.Id, new DateTimeOffset(2026, 10, 10, 5, 0, 0, TimeSpan.Zero));
            (vakansiyaId, z1Id, q1Id) = (vakansiya.Id, z1.Id, q1.Id);
        }

        using var client = await AdminMijozAsync();

        var topilgan = await IdlarAsync(client, $"/api/recruiting/candidates?vacancyId={vakansiyaId}&includeArchive=true&{sorov}", "id");

        topilgan.ShouldBe(Kutilgan(new Dictionary<string, Guid> { ["Z1"] = z1Id, ["Q1"] = q1Id }, kutilgan), ignoreOrder: true);
    }

    /// <summary>Nomzodlar sanog'i ham oraliqdan keyin.</summary>
    [Fact]
    public async Task Nomzodlar_sanogi_oraliqqa_mos()
    {
        var urug = await NomzodlarUrugiAsync(Belgi());

        using var client = await AdminMijozAsync();

        var sahifa = await JsonAsync(
            client,
            $"/api/recruiting/candidates?vacancyId={urug.VakansiyaId}&from=2026-10-09&to=2026-10-10&pageSize=1");

        sahifa.GetProperty("total").GetInt32().ShouldBe(2);
        sahifa.GetProperty("pageCount").GetInt32().ShouldBe(2);
    }

    // ────────────────────────────────────────────────────────────
    //  Sinov muddati — GET /api/probations
    // ────────────────────────────────────────────────────────────

    /// <summary>Sinov TUGASH sanasi bo'yicha (boshlanish va ishga kirish emas).</summary>
    [Theory]
    [InlineData("from=2026-11-01&to=2026-11-30", "P1,P2")]
    [InlineData("from=2026-11-30&to=2026-11-30", "P2")]
    [InlineData("from=2026-12-01", "P3")]
    [InlineData("to=2026-11-05", "P1")]
    [InlineData("from=2026-08-01&to=2026-08-31", "")]
    [InlineData("from=2027-01-01", "")]
    public async Task Sinov_royxati_tugash_sanasi_boyicha_filtrlanadi(string sorov, string kutilgan)
    {
        var belgi = Belgi();
        var urug = await SinovUrugiAsync(belgi);

        using var client = await AdminMijozAsync();

        var topilgan = await IdlarAsync(client, $"/api/probations?search={belgi}&{sorov}", "employeeId");

        topilgan.ShouldBe(Kutilgan(urug, kutilgan), ignoreOrder: true);

        var sahifa = await JsonAsync(client, $"/api/probations?search={belgi}&{sorov}");
        sahifa.GetProperty("total").GetInt32().ShouldBe(topilgan.Count);
    }

    // ────────────────────────────────────────────────────────────
    //  Belgilar — GET /api/employees/flagged
    // ────────────────────────────────────────────────────────────

    /// <summary>Belgi BOSHLANGAN sana bo'yicha; «onlyActive» bilan birga ishlaydi.</summary>
    [Theory]
    [InlineData("onlyActive=false&from=2026-09-01&to=2026-09-30", "F2,F3")]
    [InlineData("onlyActive=false&from=2026-09-30&to=2026-09-30", "F3")]
    [InlineData("onlyActive=false&from=2026-09-06", "F3")]
    [InlineData("onlyActive=false&to=2026-08-31", "F1")]
    [InlineData("onlyActive=false&from=2026-10-01", "")]
    [InlineData("onlyActive=false", "F1,F2,F3")]
    [InlineData("onlyActive=true&from=2026-08-01&to=2026-08-31", "")]
    public async Task Belgilar_royxati_boshlanish_sanasi_boyicha_filtrlanadi(string sorov, string kutilgan)
    {
        var belgi = Belgi();
        var urug = await BelgilarUrugiAsync(belgi);

        using var client = await AdminMijozAsync();

        var topilgan = await IdlarAsync(client, $"/api/employees/flagged?type=Reserve&search={belgi}&{sorov}", "employeeId");

        topilgan.ShouldBe(Kutilgan(urug, kutilgan), ignoreOrder: true);
    }

    // ────────────────────────────────────────────────────────────
    //  Verifikatsiya → So'rovlar
    // ────────────────────────────────────────────────────────────

    /// <summary>Yo'qlik so'rovi DAVRI oraliqqa tegsa (qisman ham) chiqadi.</summary>
    [Theory]
    [InlineData("from=2026-10-03&to=2026-10-10", "Y1,Y2")]
    [InlineData("from=2026-10-04&to=2026-10-09", "")]
    [InlineData("from=2026-10-22&to=2026-10-22", "Y3")]
    [InlineData("from=2026-10-11", "Y3")]
    [InlineData("to=2026-10-02", "Y1")]
    [InlineData("from=2026-11-01", "")]
    public async Task Yoqlik_sorovlari_davr_kesishuvi_boyicha_filtrlanadi(string sorov, string kutilgan)
    {
        var urug = await YoqlikUrugiAsync(Belgi());

        using var client = await AdminMijozAsync();

        var topilgan = await IdlarAsync(client, $"/api/attendance/requests?employeeId={urug.XodimId}&{sorov}", "id");

        topilgan.ShouldBe(Kutilgan(urug.Idlar, kutilgan), ignoreOrder: true);
    }

    /// <summary>
    /// Jadval so'rovi — birorta KUNI oraliq ichida bo'lsa chiqadi.
    /// </summary>
    /// <remarks>
    /// J1 ning kunlari 1- va 30-oktabr: 10–20-oktabr oralig'ida u chiqMAsligi
    /// kerak (ilgari ikki alohida shart uni chiqarardi). J3 — almashtirish
    /// (5 ↔ 25-oktabr): juftining ikkinchi sanasi ham hisobga olinadi.
    /// </remarks>
    [Theory]
    [InlineData("from=2026-10-10&to=2026-10-20", "J2")]
    [InlineData("from=2026-10-30&to=2026-10-30", "J1")]
    [InlineData("from=2026-10-24&to=2026-10-26", "J3")]
    [InlineData("from=2026-10-26", "J1")]
    [InlineData("to=2026-10-04", "J1")]
    [InlineData("from=2026-10-02&to=2026-10-04", "")]
    [InlineData("from=2026-11-01", "")]
    [InlineData("", "J1,J2,J3")]
    public async Task Jadval_sorovlari_kunlari_boyicha_filtrlanadi(string sorov, string kutilgan)
    {
        var urug = await JadvalSorovUrugiAsync(Belgi());

        using var client = await AdminMijozAsync();

        var topilgan = await IdlarAsync(client, $"/api/attendance/schedule-requests?employeeId={urug.XodimId}&{sorov}", "id");

        topilgan.ShouldBe(Kutilgan(urug.Idlar, kutilgan), ignoreOrder: true);
    }

    // ────────────────────────────────────────────────────────────
    //  Urug'lar
    // ────────────────────────────────────────────────────────────

    /// <summary>Xodimlar urug'i: identifikatorlar va to'liq ismlar (eksportni o'qish uchun).</summary>
    private sealed record XodimlarUrug(IReadOnlyDictionary<string, Guid> Idlar, IReadOnlyDictionary<string, string> Ismlar);

    /// <summary>
    /// A — doimiy, 2026-03-10 da kirgan; B — sinovda, 2026-05-20 da kirgan;
    /// C — 2025-01-05 da kirib, 2026-04-15 da ketgan.
    /// </summary>
    private async Task<XodimlarUrug> XodimlarUrugiAsync(string belgi)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var firma = LegalEntity.Create("Сана МЧЖ " + belgi).Value;

        var a = Employee.Create(FullName.Create("Санаов", "Анвар", belgi).Value).Value;
        var b = Employee.Create(FullName.Create("Санаов", "Ботир", belgi).Value).Value;
        var c = Employee.Create(FullName.Create("Санаов", "Жасур", belgi).Value).Value;

        var ma = Employment.Hire(a.Id, firma.Id, new DateOnly(2026, 3, 10)).Value;
        ma.CompleteProbation(new DateOnly(2026, 4, 10)).IsSuccess.ShouldBeTrue();

        var mb = Employment.Hire(b.Id, firma.Id, new DateOnly(2026, 5, 20)).Value;

        var mc = Employment.Hire(c.Id, firma.Id, new DateOnly(2025, 1, 5)).Value;
        mc.Terminate(new DateOnly(2026, 4, 15), reasonId: null).IsSuccess.ShouldBeTrue();

        kontekst.Add(firma);
        kontekst.AddRange(a, b, c);
        kontekst.AddRange(ma, mb, mc);
        await kontekst.SaveChangesAsync(Ct);

        return new XodimlarUrug(
            new Dictionary<string, Guid> { ["A"] = a.Id, ["B"] = b.Id, ["C"] = c.Id },
            new Dictionary<string, string> { ["A"] = a.FullName.Full, ["B"] = b.FullName.Full, ["C"] = c.FullName.Full });
    }

    private static IReadOnlyList<Guid> Kutilgan(XodimlarUrug urug, string harflar) => Kutilgan(urug.Idlar, harflar);

    /// <summary>Nomzodlar urug'i — o'z vakansiyasi bilan, ro'yxat shu vakansiya bilan cheklanadi.</summary>
    private sealed record NomzodlarUrug(Guid VakansiyaId, IReadOnlyDictionary<string, Guid> Idlar);

    /// <summary>
    /// N1/N2/N3 — jarayonda, yaratilgan lahzasi Toshkent kuni chegarasi atrofida;
    /// R1 — rad etilgan (15-sentyabr), R2 — o'zi voz kechgan (12-oktabr).
    /// </summary>
    /// <remarks>
    /// <c>created_at</c> ni interseptor saqlashda «hozir» qilib qo'yadi, shu sababli
    /// u saqlangandan keyin to'g'ridan-to'g'ri <c>UPDATE</c> bilan o'rnatiladi.
    /// </remarks>
    private async Task<NomzodlarUrug> NomzodlarUrugiAsync(string belgi)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var vakansiya = Vacancy.Open("Сана вакансияси " + belgi, new DateOnly(2026, 8, 1), openings: 5).Value;

        Candidate Nomzod(string ism) => Candidate.Register(FullName.Create("Номзодов", ism, belgi).Value, vakansiya.Id).Value;

        var n1 = Nomzod("Анвар");
        var n2 = Nomzod("Ботир");
        var n3 = Nomzod("Жасур");

        var r1 = Nomzod("Дилшод");
        r1.Reject(new DateOnly(2026, 9, 15)).IsSuccess.ShouldBeTrue();

        var r2 = Nomzod("Эркин");
        r2.Withdraw(new DateOnly(2026, 10, 12)).IsSuccess.ShouldBeTrue();

        kontekst.Add(vakansiya);
        kontekst.AddRange(n1, n2, n3, r1, r2);
        await kontekst.SaveChangesAsync(Ct);

        await YaratilganAsync(kontekst, n1.Id, new DateTimeOffset(2026, 10, 9, 19, 30, 0, TimeSpan.Zero));
        await YaratilganAsync(kontekst, n2.Id, new DateTimeOffset(2026, 10, 9, 18, 30, 0, TimeSpan.Zero));
        await YaratilganAsync(kontekst, n3.Id, new DateTimeOffset(2026, 10, 20, 18, 59, 0, TimeSpan.Zero));
        await YaratilganAsync(kontekst, r1.Id, new DateTimeOffset(2026, 10, 10, 5, 0, 0, TimeSpan.Zero));
        await YaratilganAsync(kontekst, r2.Id, new DateTimeOffset(2026, 8, 1, 5, 0, 0, TimeSpan.Zero));

        return new NomzodlarUrug(
            vakansiya.Id,
            new Dictionary<string, Guid>
            {
                ["N1"] = n1.Id,
                ["N2"] = n2.Id,
                ["N3"] = n3.Id,
                ["R1"] = r1.Id,
                ["R2"] = r2.Id,
            });
    }

    private static Task<int> YaratilganAsync(AppDbContext kontekst, Guid nomzodId, DateTimeOffset lahza)
        => kontekst.Candidates
            .Where(nomzod => nomzod.Id == nomzodId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(nomzod => nomzod.CreatedAt, lahza), Ct);

    /// <summary>
    /// Uch xodim, har birida sinov: P1 tugashi 2026-11-05, P2 — 2026-11-30, P3 — 2026-12-15.
    /// </summary>
    /// <remarks>
    /// Boshlanish va ishga kirish sanalari ATAYLAB avgustda — boshqa ustun
    /// bo'yicha filtrlansa, avgust oralig'i bo'sh bo'lmay qolardi.
    /// </remarks>
    private async Task<XodimlarUrug> SinovUrugiAsync(string belgi)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var firma = LegalEntity.Create("Синов МЧЖ " + belgi).Value;
        kontekst.Add(firma);

        var idlar = new Dictionary<string, Guid>();
        var ismlar = new Dictionary<string, string>();

        foreach (var (kalit, ism, tugash) in new[]
                 {
                     ("P1", "Анвар", new DateOnly(2026, 11, 5)),
                     ("P2", "Ботир", new DateOnly(2026, 11, 30)),
                     ("P3", "Жасур", new DateOnly(2026, 12, 15)),
                 })
        {
            var xodim = Employee.Create(FullName.Create("Синовов", ism, belgi).Value).Value;
            var munosabat = Employment.Hire(xodim.Id, firma.Id, new DateOnly(2026, 8, 3)).Value;
            var sinov = Probation.Import(munosabat.Id, new DateOnly(2026, 8, 3), tugash).Value;

            kontekst.AddRange(xodim, munosabat, sinov);

            idlar[kalit] = xodim.Id;
            ismlar[kalit] = xodim.FullName.Full;
        }

        await kontekst.SaveChangesAsync(Ct);

        return new XodimlarUrug(idlar, ismlar);
    }

    /// <summary>
    /// Uch xodim, har birida rezerv belgisi: F1 2026-08-10 dan, F2 2026-09-05 dan,
    /// F3 2026-09-30 dan. F1 ning belgisi 2026-08-31 da yopilgan — faqat tarixda.
    /// </summary>
    private async Task<XodimlarUrug> BelgilarUrugiAsync(string belgi)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var idlar = new Dictionary<string, Guid>();
        var ismlar = new Dictionary<string, string>();

        foreach (var (kalit, ism, boshlanish, tugash) in new (string, string, DateOnly, DateOnly?)[]
                 {
                     ("F1", "Анвар", new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 31)),
                     ("F2", "Ботир", new DateOnly(2026, 9, 5), null),
                     ("F3", "Жасур", new DateOnly(2026, 9, 30), null),
                 })
        {
            var xodim = Employee.Create(FullName.Create("Белгиев", ism, belgi).Value).Value;
            var flag = EmployeeFlag.Raise(xodim.Id, EmployeeFlagType.Reserve, boshlanish, endDate: tugash).Value;

            kontekst.AddRange(xodim, flag);

            idlar[kalit] = xodim.Id;
            ismlar[kalit] = xodim.FullName.Full;
        }

        await kontekst.SaveChangesAsync(Ct);

        return new XodimlarUrug(idlar, ismlar);
    }

    /// <summary>Bitta xodimning so'rovlari — ro'yxat <c>employeeId</c> bilan cheklanadi.</summary>
    private sealed record SorovlarUrug(Guid XodimId, IReadOnlyDictionary<string, Guid> Idlar);

    /// <summary>Y1 — 1–3-oktabr, Y2 — 10-oktabr, Y3 — 20–25-oktabr.</summary>
    private async Task<SorovlarUrug> YoqlikUrugiAsync(string belgi)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var xodim = Employee.Create(FullName.Create("Сўровов", "Анвар", belgi).Value).Value;
        var tur = AbsenceType.Create("Сана тури " + belgi).Value;

        var y1 = AbsenceRequest.Create(xodim.Id, tur.Id, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 3)).Value;
        var y2 = AbsenceRequest.Create(xodim.Id, tur.Id, new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 10)).Value;
        var y3 = AbsenceRequest.Create(xodim.Id, tur.Id, new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 25)).Value;

        kontekst.AddRange(xodim, tur);
        kontekst.AddRange(y1, y2, y3);
        await kontekst.SaveChangesAsync(Ct);

        return new SorovlarUrug(xodim.Id, new Dictionary<string, Guid> { ["Y1"] = y1.Id, ["Y2"] = y2.Id, ["Y3"] = y3.Id });
    }

    /// <summary>
    /// J1 — «Kunni o'zgartirish» 1- va 30-oktabrga; J2 — 15-oktabrga;
    /// J3 — «Kun almashtirish» 5 ↔ 25-oktabr.
    /// </summary>
    private async Task<SorovlarUrug> JadvalSorovUrugiAsync(string belgi)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var xodim = Employee.Create(FullName.Create("Жадвалов", "Анвар", belgi).Value).Value;

        static ScheduleChangeDayInput DamOlish(DateOnly sana, DateOnly? juft = null)
            => new(sana, IsWorking: false, SwapWith: juft);

        var j1 = ScheduleChangeRequest.Create(
            xodim.Id,
            ScheduleChangeKind.Change,
            [DamOlish(new DateOnly(2026, 10, 1)), DamOlish(new DateOnly(2026, 10, 30))]).Value;

        var j2 = ScheduleChangeRequest.Create(
            xodim.Id,
            ScheduleChangeKind.Change,
            [DamOlish(new DateOnly(2026, 10, 15))]).Value;

        var j3 = ScheduleChangeRequest.Create(
            xodim.Id,
            ScheduleChangeKind.Swap,
            [
                DamOlish(new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 25)),
                DamOlish(new DateOnly(2026, 10, 25), new DateOnly(2026, 10, 5)),
            ]).Value;

        kontekst.Add(xodim);
        kontekst.AddRange(j1, j2, j3);
        await kontekst.SaveChangesAsync(Ct);

        return new SorovlarUrug(xodim.Id, new Dictionary<string, Guid> { ["J1"] = j1.Id, ["J2"] = j2.Id, ["J3"] = j3.Id });
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    /// <summary>«A,B» → urug'dagi identifikatorlar; bo'sh satr — bo'sh ro'yxat.</summary>
    private static IReadOnlyList<Guid> Kutilgan(IReadOnlyDictionary<string, Guid> idlar, string kalitlar)
        => string.IsNullOrEmpty(kalitlar)
            ? []
            : [.. kalitlar.Split(',').Select(kalit => idlar[kalit])];

    private static Uri Yol(string manzil) => new(manzil, UriKind.Relative);

    /// <summary>
    /// Javobdagi qatorlarning identifikatorlari — sahifalangan (<c>{items:[…]}</c>)
    /// va sahifalanmagan (massiv) javobning ikkalasidan ham.
    /// </summary>
    private static async Task<IReadOnlyList<Guid>> IdlarAsync(HttpClient client, string manzil, string maydon)
    {
        var ildiz = await JsonAsync(client, manzil);

        var qatorlar = ildiz.ValueKind == JsonValueKind.Object ? ildiz.GetProperty("items") : ildiz;

        return [.. qatorlar.EnumerateArray().Select(qator => qator.GetProperty(maydon).GetGuid())];
    }

    private static async Task<JsonElement> JsonAsync(HttpClient client, string manzil)
    {
        using var javob = await client.GetAsync(Yol(manzil), Ct);

        var matn = await MatnAsync(javob);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, matn);

        using var hujjat = JsonDocument.Parse(matn);
        return hujjat.RootElement.Clone();
    }

    private static async Task XatoKutAsync(HttpResponseMessage javob, HttpStatusCode holat, string kod)
    {
        var matn = await MatnAsync(javob);
        javob.StatusCode.ShouldBe(holat, matn);
        JsonDocument.Parse(matn).RootElement.GetProperty("title").GetString().ShouldBe(kod, matn);
    }

    private async Task<HttpClient> AdminMijozAsync()
    {
        var client = _factory.CreateClient();

        using var javob = await client.PostAsJsonAsync(
            Yol("/api/auth/login"),
            new { login = BonviApiFactory.TestAdminLogin, password = BonviApiFactory.TestAdminPassword },
            Ct);

        javob.EnsureSuccessStatusCode();

        var auth = await javob.Content.ReadFromJsonAsync<AuthResponse>(Ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return client;
    }

    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(Ct);
}
