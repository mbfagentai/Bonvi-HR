using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// «Erkin ish jadvali» uchi-uchiga — SPEC-verifikatsiya-erkin-jadval §5, §10.3.
/// </summary>
/// <remarks>
/// <para>
/// Hisob qoidalari <c>FlexibleDayTests</c> da (bazasiz). Bu yerda — SHARTNOMA: yangi maydonlar
/// so'rov tanasidan bazagacha va bazadan har bir o'qish endpointigacha yetadimi, xato kodi va
/// ruxsatlar o'zgarmadimi. Yangi endpoint ham, yangi ruxsat ham yo'q.
/// </para>
/// <para>
/// Sinf bazasi umumiy: har bir test o'z xodimi, o'z jadvali va o'z shaxs raqami bilan ishlaydi,
/// sukut (Default) biriktirma yaratilmaydi (<c>AttendanceManagementApiTests</c> dagi qoida).
/// </para>
/// </remarks>
public sealed class FlexibleScheduleApiTests : IClassFixture<BonviApiFactory>
{
    private static readonly TimeSpan Tashkent = TimeSpan.FromHours(5);

    private readonly BonviApiFactory _factory;

    public FlexibleScheduleApiTests(BonviApiFactory factory) => _factory = factory;

    // ════════════════════════════════════════════════════════════
    //  Jadval — haftalik naqsh va yillik to'r (§5.1)
    // ════════════════════════════════════════════════════════════

    [Fact]
    public async Task Erkin_kun_jadvalda_saqlanadi_norma_sukuti_qaytadi_chegaradan_tashqarisi_400()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var belgi = Belgi();

        Guid jadvalId;
        using (var javob = await admin.PostAsJsonAsync(Yol("/schedules"), JadvalTanasi("Erkin dushanba " + belgi, erkinKun: 1), ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
            var jadval = await JsonAsync(javob);
            jadvalId = jadval.GetProperty("id").GetGuid();

            var dushanba = Kun(jadval, 1);
            dushanba.GetProperty("isFlexible").GetBoolean().ShouldBeTrue();
            dushanba.GetProperty("requiredMinutes").GetInt32().ShouldBe(450, "570 − 120 — server sukuti");

            var seshanba = Kun(jadval, 2);
            seshanba.GetProperty("isFlexible").GetBoolean().ShouldBeFalse();
            seshanba.GetProperty("requiredMinutes").ValueKind.ShouldBe(JsonValueKind.Null);
        }

        var oqish = await JsonAsync(await admin.GetAsync(Yol($"/schedules/{jadvalId}"), ct));
        Kun(oqish, 1).GetProperty("requiredMinutes").GetInt32().ShouldBe(450);

        using (var kop = await admin.PostAsJsonAsync(Yol("/schedules"), JadvalTanasi("Erkin xato " + belgi, erkinKun: 1, norma: 451), ct))
        {
            await XatoKutAsync(kop, HttpStatusCode.BadRequest, "davomat.jadval_erkin_vaqti");
        }

        using (var nol = await admin.PutAsJsonAsync(Yol($"/schedules/{jadvalId}"), JadvalTanasi("Erkin dushanba " + belgi, erkinKun: 1, norma: 0), ct))
        {
            await XatoKutAsync(nol, HttpStatusCode.BadRequest, "davomat.jadval_erkin_vaqti");
        }

        using (var tahrir = await admin.PutAsJsonAsync(Yol($"/schedules/{jadvalId}"), JadvalTanasi("Erkin dushanba " + belgi, erkinKun: 1, norma: 420), ct))
        {
            tahrir.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(tahrir));
            Kun(await JsonAsync(tahrir), 1).GetProperty("requiredMinutes").GetInt32().ShouldBe(420);
        }
    }

    /// <summary>Yillik to'r katagi: yozish → to'rda ko'rinadi → «nega shu jadval» ko'rsatadi; to'ldirish naqshdan nusxalaydi.</summary>
    [Fact]
    public async Task Erkin_katak_torda_va_resolveda_korinadi_toldirish_erkinlikni_nusxalaydi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var belgi = Belgi();
        var xodim = await XodimAsync(belgi);
        var jadvalId = await XodimgaJadvalAsync(admin, xodim.Id, JadvalTanasi("Qat'iy " + belgi));
        var sana = BugunToshkentda().AddDays(-5);

        using (var yozish = await admin.PutAsJsonAsync(
                   Yol($"/schedules/{jadvalId}/dates"),
                   new
                   {
                       dates = new[] { Iso(sana) },
                       isWorking = true,
                       startTime = "08:30",
                       endTime = "18:00",
                       breakMinutes = 120,
                       breakStart = "12:00",
                       breakEnd = "14:00",
                       isFlexible = true,
                       requiredMinutes = 420,
                   },
                   ct))
        {
            yozish.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(yozish));
        }

        var tor = await JsonAsync(await admin.GetAsync(Yol($"/schedules/{jadvalId}/year?year={sana.Year}"), ct));
        var kunlar = tor.GetProperty("days").EnumerateArray().ToList();

        var katak = kunlar.Single(day => day.GetProperty("date").GetString() == Iso(sana));
        katak.GetProperty("source").GetString().ShouldBe("Date");
        katak.GetProperty("isFlexible").GetBoolean().ShouldBeTrue();
        katak.GetProperty("requiredMinutes").GetInt32().ShouldBe(420);
        katak.GetProperty("plannedMinutes").GetInt32().ShouldBe(420, "erkin katakda reja — norma");

        var qoshni = kunlar.First(day => day.GetProperty("date").GetString() != Iso(sana));
        qoshni.GetProperty("isFlexible").GetBoolean().ShouldBeFalse();
        qoshni.GetProperty("requiredMinutes").ValueKind.ShouldBe(JsonValueKind.Null);

        var manba = await JsonAsync(await admin.GetAsync(Yol($"/schedules/resolve?employeeId={xodim.Id}&date={Iso(sana)}"), ct));
        manba.GetProperty("daySource").GetString().ShouldBe("Date");
        manba.GetProperty("day").GetProperty("isFlexible").GetBoolean().ShouldBeTrue();
        manba.GetProperty("day").GetProperty("requiredMinutes").GetInt32().ShouldBe(420);

        // Chegaradan tashqari katak — o'sha kod.
        using (var xato = await admin.PutAsJsonAsync(
                   Yol($"/schedules/{jadvalId}/dates"),
                   new { dates = new[] { Iso(sana) }, isWorking = true, startTime = "08:30", endTime = "18:00", breakMinutes = 120, isFlexible = true, requiredMinutes = 451 },
                   ct))
        {
            await XatoKutAsync(xato, HttpStatusCode.BadRequest, "davomat.jadval_erkin_vaqti");
        }

        // ── Yilni naqshdan to'ldirish erkinlikni ham nusxalaydi ──
        Guid erkinJadval;
        using (var javob = await admin.PostAsJsonAsync(Yol("/schedules"), JadvalTanasi("Hammasi erkin " + belgi, erkinKun: 0), ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
            erkinJadval = (await JsonAsync(javob)).GetProperty("id").GetGuid();
        }

        using (var toldirish = await admin.PostAsJsonAsync(Yol($"/schedules/{erkinJadval}/year/fill"), new { year = 2031 }, ct))
        {
            toldirish.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(toldirish));
        }

        var toldirilgan = (await JsonAsync(await admin.GetAsync(Yol($"/schedules/{erkinJadval}/year?year=2031"), ct)))
            .GetProperty("days").EnumerateArray().ToList();

        toldirilgan.Count.ShouldBe(365);
        toldirilgan.ShouldAllBe(day => day.GetProperty("source").GetString() == "Date");
        toldirilgan.ShouldAllBe(day => day.GetProperty("isFlexible").GetBoolean());
        toldirilgan.ShouldAllBe(day => day.GetProperty("requiredMinutes").GetInt32() == 450);
    }

    // ════════════════════════════════════════════════════════════
    //  Jadval so'rovi va hisob natijasi (§5.2, §5.3)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Qat'iy 09:00–18:00 xodim 10:00–18:00 da ishladi (kechikish 50). HR shu sanaga erkin kun so'rovini
    /// kiritadi va tasdiqlaydi — kun qayta hisoblanadi: kechikish 0, ishlangan 360, kam ishlangan 90.
    /// Tabel, hisobotlar va Excel shu sonlarni ko'rsatadi.
    /// </summary>
    [Fact]
    public async Task Tasdiqlangan_erkin_sorov_kunni_kechikishsiz_va_kam_ishlangan_bilan_hisoblaydi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var belgi = Belgi();
        var xodim = await XodimAsync(belgi);
        await XodimgaJadvalAsync(admin, xodim.Id, JadvalTanasi("Qat'iy " + belgi));
        var sana = BugunToshkentda().AddDays(-3);

        await OtishlarAsync(admin, xodim.Id, "ek-" + belgi, (Lahza(sana, 10, 0), "In"), (Lahza(sana, 18, 0), "Out"));

        var oldin = await KatakAsync(admin, xodim.Id, sana, ct);
        oldin.GetProperty("lateMinutes").GetInt32().ShouldBe(50, "sinov sharti: qat'iy kun");
        oldin.GetProperty("isFlexible").GetBoolean().ShouldBeFalse();
        oldin.GetProperty("shortfallMinutes").GetInt32().ShouldBe(0);

        Guid sorovId;
        using (var yaratish = await admin.PostAsJsonAsync(
                   Yol("/schedule-requests"),
                   new
                   {
                       employeeId = xodim.Id,
                       kind = "Change",
                       days = new[]
                       {
                           new { date = Iso(sana), isWorking = true, startTime = "08:30", endTime = "18:00", breakStart = "12:00", breakEnd = "14:00", isFlexible = true },
                       },
                   },
                   ct))
        {
            yaratish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yaratish));
            var yozuv = await JsonAsync(yaratish);
            sorovId = yozuv.GetProperty("id").GetGuid();

            var kun = yozuv.GetProperty("days").EnumerateArray().ShouldHaveSingleItem();
            kun.GetProperty("isFlexible").GetBoolean().ShouldBeTrue();
            kun.GetProperty("requiredMinutes").GetInt32().ShouldBe(450);
            kun.GetProperty("breakMinutes").GetInt32().ShouldBe(120);
        }

        var royxat = await JsonAsync(await admin.GetAsync(Yol($"/schedule-requests/{sorovId}"), ct));
        royxat.GetProperty("days")[0].GetProperty("requiredMinutes").GetInt32().ShouldBe(450);

        using (var qaror = await admin.PostAsJsonAsync(Yol($"/schedule-requests/{sorovId}/decide"), new { approve = true }, ct))
        {
            qaror.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qaror));
        }

        // ── Saqlangan kun ──
        var saqlangan = await SaqlanganKunAsync(xodim.Id, sana, ct);
        saqlangan.IsFlexible.ShouldBeTrue();
        saqlangan.LateMinutes.ShouldBe(0);
        saqlangan.WorkedMinutes.ShouldBe(360);
        saqlangan.ShortfallMinutes.ShouldBe(90);

        // ── Tabel katagi va jami ──
        var tabel = await JsonAsync(await admin.GetAsync(
            Yol($"/timesheet?year={sana.Year}&month={sana.Month}&employeeId={xodim.Id}"), ct));
        var qator = tabel.GetProperty("items").EnumerateArray().ShouldHaveSingleItem();
        var katak = qator.GetProperty("cells").EnumerateArray().Single(cell => cell.GetProperty("date").GetString() == Iso(sana));

        katak.GetProperty("isFlexible").GetBoolean().ShouldBeTrue();
        katak.GetProperty("plannedMinutes").GetInt32().ShouldBe(450);
        katak.GetProperty("shortfallMinutes").GetInt32().ShouldBe(90);
        katak.GetProperty("lateMinutes").GetInt32().ShouldBe(0);
        katak.GetProperty("workedMinutes").GetInt32().ShouldBe(360);
        qator.GetProperty("totals").GetProperty("shortfallMinutes").GetInt32().ShouldBe(90);

        var manba = await JsonAsync(await admin.GetAsync(Yol($"/schedules/resolve?employeeId={xodim.Id}&date={Iso(sana)}"), ct));
        manba.GetProperty("daySource").GetString().ShouldBe("Request");
        manba.GetProperty("day").GetProperty("isFlexible").GetBoolean().ShouldBeTrue();
        manba.GetProperty("day").GetProperty("requiredMinutes").GetInt32().ShouldBe(450);

        // ── Hisobotlar ──
        var davr = $"from={Iso(sana)}&to={Iso(sana)}";

        var kam = await JsonAsync(await admin.GetAsync(Yol($"/reports/exceptions?kind=Shortfall&{davr}&employeeId={xodim.Id}"), ct));
        var kamQator = kam.GetProperty("items").EnumerateArray().ShouldHaveSingleItem();
        kamQator.GetProperty("shortfallMinutes").GetInt32().ShouldBe(90);
        kamQator.GetProperty("lateMinutes").GetInt32().ShouldBe(0);

        (await JsonAsync(await admin.GetAsync(Yol($"/reports/exceptions?kind=Shortfall&{davr}&employeeId={xodim.Id}&minMinutes=91"), ct)))
            .GetProperty("total").GetInt32().ShouldBe(0, "«kamida» filtri kam ishlanganga ham ishlaydi");
        (await JsonAsync(await admin.GetAsync(Yol($"/reports/exceptions?kind=Late&{davr}&employeeId={xodim.Id}"), ct)))
            .GetProperty("total").GetInt32().ShouldBe(0, "erkin kunda kechikish yo'q");

        var firma = await FirmaIdAsync(xodim.Id, ct);
        var xulosa = await JsonAsync(await admin.GetAsync(Yol($"/reports/summary?{davr}&legalEntityId={firma}"), ct));
        xulosa.GetProperty("items").EnumerateArray().ShouldHaveSingleItem().GetProperty("shortfallMinutes").GetInt32().ShouldBe(90);

        // ── Excel: yangi ustunlar OXIRIDA, mavjudlari joyida ──
        using (var fayl = await ExcelAsync(admin, $"/export/timesheet?year={sana.Year}&month={sana.Month}&employeeId={xodim.Id}", ct))
        {
            var kunlarVaraq = fayl.Worksheet("Kunlar");
            kunlarVaraq.Cell(1, 8).GetString().ShouldBe("Ishlangan (daq.)");
            kunlarVaraq.Cell(1, 15).GetString().ShouldBe("Tanaffus ayirilgan (daq.)");
            kunlarVaraq.Cell(1, 16).GetString().ShouldBe("Erkin jadval");
            kunlarVaraq.Cell(1, 17).GetString().ShouldBe("Kam ishlangan (daq.)");
            kunlarVaraq.Cell(1, 18).GetString().ShouldBe("Kam ishlangan (soat:daq)");

            var qatorRaqami = 1 + sana.Day;   // bitta xodim: 2-qatordan oyning 1-kuni
            kunlarVaraq.Cell(qatorRaqami, 4).GetDateTime().ShouldBe(sana.ToDateTime(TimeOnly.MinValue));
            kunlarVaraq.Cell(qatorRaqami, 16).GetString().ShouldBe("Ha");
            kunlarVaraq.Cell(qatorRaqami, 17).GetDouble().ShouldBe(90);
            kunlarVaraq.Cell(qatorRaqami, 18).GetString().ShouldBe("1:30");

            var tabelVaraq = fayl.Worksheet("Tabel");
            var jami = 4 + DateTime.DaysInMonth(sana.Year, sana.Month);
            tabelVaraq.Cell(1, jami).GetString().ShouldBe("Ishlangan (daq.)");
            tabelVaraq.Cell(1, jami + 9).GetString().ShouldBe("Erta ketish (soat:daq)");
            tabelVaraq.Cell(1, jami + 10).GetString().ShouldBe("Kam ishlangan (daq.)");
            tabelVaraq.Cell(1, jami + 11).GetString().ShouldBe("Kam ishlangan (soat:daq)");
            tabelVaraq.Cell(2, jami + 10).GetDouble().ShouldBe(90);
        }

        using (var fayl = await ExcelAsync(admin, $"/export/exceptions?kind=Shortfall&{davr}&employeeId={xodim.Id}", ct))
        {
            var varaq = fayl.Worksheet("Kam ishlaganlar");
            varaq.Cell(1, 15).GetString().ShouldBe("Holat");
            varaq.Cell(1, 16).GetString().ShouldBe("Kam ishlangan (daq.)");
            varaq.Cell(1, 17).GetString().ShouldBe("Kam ishlangan (soat:daq)");
            varaq.Cell(2, 16).GetDouble().ShouldBe(90);
        }

        using (var fayl = await ExcelAsync(admin, $"/export/summary?{davr}&legalEntityId={firma}", ct))
        {
            var varaq = fayl.Worksheet("Xulosa");
            varaq.Cell(1, 18).GetString().ShouldBe("Ortiqcha ish (soat:daq)");
            varaq.Cell(1, 19).GetString().ShouldBe("Kam ishlangan (daq.)");
            varaq.Cell(1, 20).GetString().ShouldBe("Kam ishlangan (soat:daq)");
            varaq.Cell(2, 19).GetDouble().ShouldBe(90);
        }
    }

    /// <summary>Kun almashtirishda erkin sananing kuni (norma bilan) juftiga o'tadi — server surati.</summary>
    [Fact]
    public async Task Kun_almashtirishda_erkin_kun_normasi_bilan_kochadi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var belgi = Belgi();
        var xodim = await XodimAsync(belgi);
        var jadvalId = await XodimgaJadvalAsync(admin, xodim.Id, JadvalTanasi("Qat'iy " + belgi));

        var erkin = BugunToshkentda().AddDays(10);
        var qatiy = erkin.AddDays(1);

        using (var katak = await admin.PutAsJsonAsync(
                   Yol($"/schedules/{jadvalId}/dates"),
                   new { dates = new[] { Iso(erkin) }, isWorking = true, startTime = "08:30", endTime = "18:00", breakMinutes = 120, breakStart = "12:00", breakEnd = "14:00", isFlexible = true, requiredMinutes = 400 },
                   ct))
        {
            katak.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(katak));
        }

        using var yaratish = await admin.PostAsJsonAsync(
            Yol("/schedule-requests"),
            new { employeeId = xodim.Id, kind = "Swap", swaps = new[] { new { date = Iso(erkin), withDate = Iso(qatiy) } } },
            ct);
        yaratish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yaratish));

        var kunlar = (await JsonAsync(yaratish)).GetProperty("days").EnumerateArray().ToList();

        var olgan = kunlar.Single(kun => kun.GetProperty("date").GetString() == Iso(qatiy));
        olgan.GetProperty("isFlexible").GetBoolean().ShouldBeTrue("erkin sananing kuni juftiga o'tdi");
        olgan.GetProperty("requiredMinutes").GetInt32().ShouldBe(400);
        olgan.GetProperty("startTime").GetString().ShouldStartWith("08:30");

        var bergan = kunlar.Single(kun => kun.GetProperty("date").GetString() == Iso(erkin));
        bergan.GetProperty("isFlexible").GetBoolean().ShouldBeFalse();
        bergan.GetProperty("requiredMinutes").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    // ════════════════════════════════════════════════════════════
    //  Xodim ilovasi (§5.2, §5.3)
    // ════════════════════════════════════════════════════════════

    [Fact]
    public async Task Xodim_ilovasida_erkin_reja_sorov_va_oylik_davomat()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var belgi = Belgi();
        var xodim = await XodimAsync(belgi);
        await XodimgaJadvalAsync(admin, xodim.Id, JadvalTanasi("Hammasi erkin " + belgi, erkinKun: 0));
        using var ilova = await IlovaMijoziAsync(admin, xodim.Id);

        var keyin = BugunToshkentda().AddDays(3);

        var reja = await JsonAsync(await ilova.GetAsync(new Uri($"/api/me/plan?date={Iso(keyin)}", UriKind.Relative), ct));
        reja.GetProperty("hasSchedule").GetBoolean().ShouldBeTrue();
        reja.GetProperty("isFlexible").GetBoolean().ShouldBeTrue();
        reja.GetProperty("requiredMinutes").GetInt32().ShouldBe(450);
        reja.GetProperty("breakMinutes").GetInt32().ShouldBe(120);

        using (var yuborish = await ilova.PostAsJsonAsync(
                   new Uri("/api/me/schedule-requests", UriKind.Relative),
                   new
                   {
                       kind = "Change",
                       days = new[]
                       {
                           new { date = Iso(keyin), isWorking = true, startTime = "08:30:00", endTime = "18:00:00", breakStart = "12:00:00", breakEnd = "14:00:00", isFlexible = true, requiredMinutes = (int?)420 },
                       },
                   },
                   ct))
        {
            yuborish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yuborish));
            var kun = (await JsonAsync(yuborish)).GetProperty("days").EnumerateArray().ShouldHaveSingleItem();
            kun.GetProperty("isFlexible").GetBoolean().ShouldBeTrue();
            kun.GetProperty("requiredMinutes").GetInt32().ShouldBe(420);
        }

        using (var xato = await ilova.PostAsJsonAsync(
                   new Uri("/api/me/schedule-requests", UriKind.Relative),
                   new
                   {
                       kind = "Change",
                       days = new[] { new { date = Iso(keyin.AddDays(1)), isWorking = true, startTime = "08:30:00", endTime = "18:00:00", breakMinutes = 120, isFlexible = true, requiredMinutes = 451 } },
                   },
                   ct))
        {
            await XatoKutAsync(xato, HttpStatusCode.BadRequest, "davomat.jadval_erkin_vaqti");
        }

        // ── Oylik davomat: erkin kun katagi va jami ──
        var sana = BugunToshkentda().AddDays(-3);
        await OtishlarAsync(admin, xodim.Id, "ei-" + belgi, (Lahza(sana, 10, 0), "In"), (Lahza(sana, 18, 0), "Out"));

        var oy = await JsonAsync(await ilova.GetAsync(
            new Uri($"/api/me/attendance?year={sana.Year}&month={sana.Month}", UriKind.Relative), ct));
        var katak = oy.GetProperty("days").EnumerateArray()
            .Select(kun => kun.GetProperty("day"))
            .Single(kun => kun.GetProperty("date").GetString() == Iso(sana));

        katak.GetProperty("status").GetString().ShouldBe("Present");
        katak.GetProperty("isFlexible").GetBoolean().ShouldBeTrue();
        katak.GetProperty("plannedMinutes").GetInt32().ShouldBe(450);
        katak.GetProperty("lateMinutes").GetInt32().ShouldBe(0);
        katak.GetProperty("shortfallMinutes").GetInt32().ShouldBe(90);
        oy.GetProperty("totals").GetProperty("shortfallMinutes").GetInt32().ShouldBe(90);
    }

    // ════════════════════════════════════════════════════════════
    //  RBAC — o'zgargan tanalar avvalgi ruxsatlar ostida
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Faqat <c>attendance.view</c>: erkin tanali yozuv endpointlarida 403, «Kam ishlaganlar» hisobotida 200;
    /// tokensiz — 401; HR tokeni xodim endpointida — 401 (auditoriya boshqa).
    /// </summary>
    [Fact]
    public async Task Erkin_tanali_endpointlar_avvalgi_ruxsatlar_ostida()
    {
        var ct = TestContext.Current.CancellationToken;
        var birorta = Guid.CreateVersion7();
        var jadval = JadvalTanasi("RBAC " + Belgi(), erkinKun: 1);
        var sanalar = new { dates = new[] { "2026-10-21" }, isWorking = true, startTime = "08:30", endTime = "18:00", isFlexible = true };
        var sorov = new
        {
            employeeId = birorta,
            kind = "Change",
            days = new[] { new { date = "2026-10-21", isWorking = true, startTime = "08:30", endTime = "18:00", isFlexible = true } },
        };

        using var korish = _factory.CreateClient();
        korish.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await TestAccounts.TokenAsync(_factory, Permissions.AttendanceView));

        (await korish.PostAsJsonAsync(Yol("/schedules"), jadval, ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await korish.PutAsJsonAsync(Yol($"/schedules/{birorta}"), jadval, ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await korish.PutAsJsonAsync(Yol($"/schedules/{birorta}/dates"), sanalar, ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await korish.PostAsJsonAsync(Yol("/schedule-requests"), sorov, ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await korish.PutAsJsonAsync(Yol($"/schedule-requests/{birorta}"), sorov, ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await korish.GetAsync(Yol("/reports/exceptions?kind=Shortfall"), ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await korish.GetAsync(Yol("/export/exceptions?kind=Shortfall"), ct)).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden, "eksport — reports.export ham kerak");

        using var anonim = _factory.CreateClient();
        (await anonim.GetAsync(Yol("/reports/exceptions?kind=Shortfall"), ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonim.GetAsync(Yol("/export/exceptions?kind=Shortfall"), ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var admin = await AdminMijozAsync();
        using var xodimYoli = await admin.PostAsJsonAsync(
            new Uri("/api/me/schedule-requests", UriKind.Relative),
            new { kind = "Change", days = sorov.days },
            ct);
        xodimYoli.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "HR tokeni xodim siyosatidan o'tmaydi");
    }

    // ────────────────────────────────────────────────────────────
    //  Muhit tayyorlash
    // ────────────────────────────────────────────────────────────

    private sealed record Xodim(Guid Id, string Telefon);

    private sealed record SaqlanganKun(bool IsFlexible, int LateMinutes, int WorkedMinutes, int ShortfallMinutes);

    /// <summary>
    /// Haftalik jadval tanasi: qat'iy kunlar 09:00–18:00; <paramref name="erkinKun"/> — erkin 08:30–18:00,
    /// tanaffus 120 (12:00–14:00), norma <paramref name="norma"/> (bo'sh — sukut). 0 — hamma kun erkin.
    /// </summary>
    private static object JadvalTanasi(string nom, int? erkinKun = null, int? norma = null) => new
    {
        name = nom,
        graceInMinutes = 10,
        graceOutMinutes = 10,
        note = (string?)null,
        days = Enumerable.Range(1, 7).Select(kun => kun == erkinKun || erkinKun == 0
            ? (object)new
            {
                dayIndex = kun,
                isWorking = true,
                startTime = "08:30",
                endTime = "18:00",
                breakMinutes = 120,
                breakStart = "12:00",
                breakEnd = "14:00",
                isFlexible = true,
                requiredMinutes = norma,
            }
            : new { dayIndex = kun, isWorking = true, startTime = "09:00", endTime = "18:00", breakMinutes = 0 }),
    };

    private static JsonElement Kun(JsonElement jadval, int kun)
        => jadval.GetProperty("days").EnumerateArray().Single(day => day.GetProperty("dayIndex").GetInt32() == kun);

    /// <summary>Jadval yaratadi va uni XODIM pog'onasida biriktiradi.</summary>
    private static async Task<Guid> XodimgaJadvalAsync(HttpClient admin, Guid xodimId, object tana)
    {
        var ct = TestContext.Current.CancellationToken;

        using var jadval = await admin.PostAsJsonAsync(Yol("/schedules"), tana, ct);
        jadval.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(jadval));
        var jadvalId = (await JsonAsync(jadval)).GetProperty("id").GetGuid();

        using var biriktirma = await admin.PostAsJsonAsync(
            Yol("/schedule-assignments"),
            new { workScheduleId = jadvalId, scope = "Employee", employeeId = xodimId, effectiveFrom = "2025-01-01" },
            ct);
        biriktirma.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(biriktirma));

        return jadvalId;
    }

    /// <summary>
    /// Terminal o'tishlari: qurilma → hodisalar → moslik. Moslik eski o'tishlarni bog'laydi va kunni
    /// qayta hisoblaydi (testda navbat sinxron) — C6 dagi yo'l.
    /// </summary>
    private async Task OtishlarAsync(HttpClient admin, Guid xodimId, string shaxs, params (DateTimeOffset Lahza, string Yonalish)[] otishlar)
    {
        var ct = TestContext.Current.CancellationToken;

        using var qurilma = await admin.PostAsJsonAsync(Yol("/devices"), new { name = "Erkin turniket " + shaxs, provider = "Generic" }, ct);
        qurilma.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qurilma));
        var token = (await JsonAsync(qurilma)).GetProperty("token").GetString()!;

        var tana = JsonSerializer.Serialize(new
        {
            events = otishlar.Select((otish, i) => new
            {
                personId = shaxs,
                occurredAt = otish.Lahza.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
                direction = otish.Yonalish,
                eventId = $"{shaxs}-{otish.Lahza.ToUnixTimeSeconds()}-{i}",
            }),
        });

        using (var mijoz = _factory.CreateClient())
        using (var sorov = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/attendance/ingest", UriKind.Relative)))
        {
            sorov.Content = new StringContent(tana, Encoding.UTF8, "application/json");
            sorov.Headers.Add("X-Bonvi-Device-Token", token);

            using var qabul = await mijoz.SendAsync(sorov, ct);
            qabul.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qabul));
        }

        using var moslik = await admin.PostAsJsonAsync(Yol("/identities"), new { externalPersonId = shaxs, employeeId = xodimId }, ct);
        moslik.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(moslik));
    }

    /// <summary>Xodim — o'z firmasi, mehnat munosabati va takrorlanmas telefoni (ilova logini) bilan.</summary>
    private async Task<Xodim> XodimAsync(string belgi)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var firma = LegalEntity.Create("Erkin MCHJ " + belgi).Value;
        var xodim = Employee.Create(FullName.Create("Erkinov", "Xodim", belgi).Value).Value;
        var telefon = "9" + Random.Shared.Next(10_000_000, 99_999_999).ToString(CultureInfo.InvariantCulture);
        xodim.SetPhones(PhoneNumber.Create(telefon).Value);

        kontekst.AddRange(firma, xodim);
        kontekst.Add(Employment.Hire(xodim.Id, firma.Id, new DateOnly(2025, 1, 1)).Value);
        await kontekst.SaveChangesAsync(TestContext.Current.CancellationToken);

        return new Xodim(xodim.Id, telefon);
    }

    private async Task<Guid> FirmaIdAsync(Guid xodimId, CancellationToken ct)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();

        return await qamrov.ServiceProvider.GetRequiredService<AppDbContext>().Employments.AsNoTracking()
            .Where(munosabat => munosabat.EmployeeId == xodimId)
            .Select(munosabat => munosabat.LegalEntityId)
            .FirstAsync(ct);
    }

    private async Task<SaqlanganKun> SaqlanganKunAsync(Guid xodimId, DateOnly sana, CancellationToken ct)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();

        return await qamrov.ServiceProvider.GetRequiredService<AppDbContext>().AttendanceDays.AsNoTracking()
            .Where(day => day.EmployeeId == xodimId && day.LocalDate == sana)
            .Select(day => new SaqlanganKun(day.IsFlexible, day.LateMinutes, day.WorkedMinutes, day.ShortfallMinutes))
            .SingleAsync(ct);
    }

    /// <summary>Tabeldagi bitta xodimning bitta katagi.</summary>
    private static async Task<JsonElement> KatakAsync(HttpClient admin, Guid xodimId, DateOnly sana, CancellationToken ct)
    {
        var tabel = await JsonAsync(await admin.GetAsync(
            Yol($"/timesheet?year={sana.Year}&month={sana.Month}&employeeId={xodimId}"), ct));

        return tabel.GetProperty("items")[0].GetProperty("cells").EnumerateArray()
            .Single(cell => cell.GetProperty("date").GetString() == Iso(sana));
    }

    private static async Task<XLWorkbook> ExcelAsync(HttpClient admin, string yol, CancellationToken ct)
    {
        using var javob = await admin.GetAsync(Yol(yol), ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        return new XLWorkbook(new MemoryStream(await javob.Content.ReadAsByteArrayAsync(ct)));
    }

    /// <summary>HR «Ilova» bo'limini ochadi, parolni ko'radi va xodim sifatida kiradi.</summary>
    private async Task<HttpClient> IlovaMijoziAsync(HttpClient admin, Guid xodimId)
    {
        var ct = TestContext.Current.CancellationToken;

        using var bolim = await admin.GetAsync(new Uri($"/api/employees/{xodimId}/app-access", UriKind.Relative), ct);
        bolim.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(bolim));
        var login = (await JsonAsync(bolim)).GetProperty("login").GetString()!;

        using var parolJavobi = await admin.GetAsync(new Uri($"/api/employees/{xodimId}/app-access/password", UriKind.Relative), ct);
        parolJavobi.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(parolJavobi));
        var parol = (await JsonAsync(parolJavobi)).GetProperty("password").GetString()!;

        using var kirish = await _factory.CreateClient().PostAsJsonAsync(
            new Uri("/api/employee-auth/login", UriKind.Relative),
            new { login, password = parol },
            ct);
        kirish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(kirish));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            (await JsonAsync(kirish)).GetProperty("accessToken").GetString()!);

        return client;
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

    private static Uri Yol(string yol) => new("/api/attendance" + yol, UriKind.Relative);

    private static string Belgi() => Guid.NewGuid().ToString("N")[..8];

    private static string Iso(DateOnly sana) => sana.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateOnly BugunToshkentda() => DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(Tashkent).DateTime);

    private static DateTimeOffset Lahza(DateOnly sana, int soat, int daqiqa)
        => new(sana.ToDateTime(new TimeOnly(soat, daqiqa)), Tashkent);

    private static async Task XatoKutAsync(HttpResponseMessage javob, HttpStatusCode holat, string kod)
    {
        var matn = await MatnAsync(javob);
        javob.StatusCode.ShouldBe(holat, matn);
        JsonDocument.Parse(matn).RootElement.GetProperty("title").GetString().ShouldBe(kod, matn);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage javob)
        => JsonDocument.Parse(await MatnAsync(javob)).RootElement.Clone();

    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
