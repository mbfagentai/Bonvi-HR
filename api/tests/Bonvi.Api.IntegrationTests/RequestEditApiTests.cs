using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bonvi.Application.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Kutayotgan so'rovni tahrirlash va parallellik — SPEC-verifikatsiya-sorov-tahriri §3, §8.4.
/// </summary>
/// <remarks>
/// <para>
/// Xodim faqat O'Z va faqat «Kutilmoqda» so'rovini o'zgartiradi, yaratishdagi qoidalarning o'zi
/// bilan. Har bir holat o'zgarishi <c>version</c> ni oshiradi: eskirgan versiya ham, parallel yozuv
/// ham 409 <c>davomat.sorov_ozgargan</c> — hech qachon 500.
/// </para>
/// <para>
/// Sinf bazasi umumiy: har bir test o'z xodimi bilan ishlaydi.
/// </para>
/// </remarks>
public sealed class RequestEditApiTests : IClassFixture<BonviApiFactory>
{
    private static readonly TimeSpan Tashkent = TimeSpan.FromHours(5);

    private readonly BonviApiFactory _factory;

    public RequestEditApiTests(BonviApiFactory factory) => _factory = factory;

    // ════════════════════════════════════════════════════════════
    //  Yo'qlik so'rovi — xodimning tahriri
    // ════════════════════════════════════════════════════════════

    [Fact]
    public async Task Xodim_kutayotgan_sorovini_ozgartiradi_versiya_oshadi_belgi_va_audit_yoziladi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var belgi = Belgi();
        var tur = await YoqlikTuriAsync(admin, "Tahrir turi " + belgi);
        var xodim = await XodimAsync(belgi);
        using var ilova = await IlovaMijoziAsync(admin, xodim.Id);
        var sana = BugunToshkentda().AddDays(5);

        var sorov = await IlovadanSorovAsync(ilova, tur, sana, sana);
        sorov.GetProperty("version").GetInt32().ShouldBe(0);
        sorov.GetProperty("editedAt").ValueKind.ShouldBe(JsonValueKind.Null);
        sorov.GetProperty("editedByEmployee").GetBoolean().ShouldBeFalse();
        var id = sorov.GetProperty("id").GetGuid();

        using (var tahrir = await ilova.PutAsJsonAsync(
                   Men($"/requests/{id}"),
                   new { version = 0, absenceTypeId = tur, fromDate = Iso(sana), toDate = Iso(sana), fromTime = "14:00:00", toTime = "16:00:00", note = "Shifokorga" },
                   ct))
        {
            tahrir.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(tahrir));
            var yozuv = await JsonAsync(tahrir);
            yozuv.GetProperty("version").GetInt32().ShouldBe(1);
            yozuv.GetProperty("editedByEmployee").GetBoolean().ShouldBeTrue();
            yozuv.GetProperty("editedAt").ValueKind.ShouldBe(JsonValueKind.String);
            yozuv.GetProperty("status").GetString().ShouldBe("Pending");
            yozuv.GetProperty("submittedByEmployee").GetBoolean().ShouldBeTrue("muallif o'zgarmaydi");
            yozuv.GetProperty("fromTime").GetString().ShouldBe("14:00:00");
            yozuv.GetProperty("note").GetString().ShouldBe("Shifokorga");
        }

        // HR navbati ham o'sha belgini ko'radi.
        var navbat = await JsonAsync(await admin.GetAsync(Yol($"/requests?employeeId={xodim.Id}"), ct));
        var qator = navbat.GetProperty("items").EnumerateArray().ShouldHaveSingleItem();
        qator.GetProperty("version").GetInt32().ShouldBe(1);
        qator.GetProperty("editedByEmployee").GetBoolean().ShouldBeTrue();

        // Audit — avtomatik, xodim hisobi nomidan.
        var audit = await AuditAsync("AbsenceRequest", id, ct);
        audit.ShouldContain(log => log.Action == AuditAction.Update && log.ActorKind == AuditActorKind.EmployeeAccount);

        // HR tahriri — lahza yangilanadi, «ilovada» belgisi tushadi.
        using (var hr = await admin.PutAsJsonAsync(
                   Yol($"/requests/{id}"),
                   new { employeeId = xodim.Id, absenceTypeId = tur, fromDate = Iso(sana), toDate = Iso(sana), note = "HR", version = 1 },
                   ct))
        {
            hr.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(hr));
            var yozuv = await JsonAsync(hr);
            yozuv.GetProperty("version").GetInt32().ShouldBe(2);
            yozuv.GetProperty("editedByEmployee").GetBoolean().ShouldBeFalse();
            yozuv.GetProperty("editedAt").ValueKind.ShouldBe(JsonValueKind.String);
        }
    }

    [Fact]
    public async Task Begona_sorov_404_HR_tokeni_va_anonim_401()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var belgi = Belgi();
        var tur = await YoqlikTuriAsync(admin, "Begona turi " + belgi);
        var sana = BugunToshkentda().AddDays(6);

        var egasi = await XodimAsync(belgi);
        using var egasiIlova = await IlovaMijoziAsync(admin, egasi.Id);
        var yoqlik = (await IlovadanSorovAsync(egasiIlova, tur, sana, sana)).GetProperty("id").GetGuid();
        var jadval = (await IlovadanJadvalSorovAsync(egasiIlova, sana, null)).GetProperty("id").GetGuid();

        var begona = await XodimAsync(Belgi());
        using var begonaIlova = await IlovaMijoziAsync(admin, begona.Id);

        var yoqlikTanasi = new { version = 0, absenceTypeId = tur, fromDate = Iso(sana), toDate = Iso(sana) };
        var jadvalTanasi = new { version = 0, kind = "Change", days = new[] { new { date = Iso(sana), isWorking = false } } };

        using (var javob = await begonaIlova.PutAsJsonAsync(Men($"/requests/{yoqlik}"), yoqlikTanasi, ct))
        {
            await XatoKutAsync(javob, HttpStatusCode.NotFound, "davomat.sorov_topilmadi");
        }

        using (var javob = await begonaIlova.PutAsJsonAsync(Men($"/schedule-requests/{jadval}"), jadvalTanasi, ct))
        {
            await XatoKutAsync(javob, HttpStatusCode.NotFound, "davomat.sorov_topilmadi");
        }

        // HR tokeni — hatto administratorniki ham — xodim sxemasidan o'tmaydi.
        (await admin.PutAsJsonAsync(Men($"/requests/{yoqlik}"), yoqlikTanasi, ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await admin.PutAsJsonAsync(Men($"/schedule-requests/{jadval}"), jadvalTanasi, ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var anonim = _factory.CreateClient();
        (await anonim.PutAsJsonAsync(Men($"/requests/{yoqlik}"), yoqlikTanasi, ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonim.PutAsJsonAsync(Men($"/schedule-requests/{jadval}"), jadvalTanasi, ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Begona urinish hech narsani o'zgartirmadi.
        var royxat = await JsonAsync(await egasiIlova.GetAsync(Men("/requests"), ct));
        royxat.GetProperty("items")[0].GetProperty("version").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Qaror_qabul_qilingan_va_bekor_qilingan_sorov_ozgarmaydi_eskirgan_versiya_409()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var belgi = Belgi();
        var tur = await YoqlikTuriAsync(admin, "Holat turi " + belgi);
        var xodim = await XodimAsync(belgi);
        using var ilova = await IlovaMijoziAsync(admin, xodim.Id);
        var bugun = BugunToshkentda();

        var tasdiqlangan = (await IlovadanSorovAsync(ilova, tur, bugun.AddDays(10), bugun.AddDays(10))).GetProperty("id").GetGuid();
        var radEtilgan = (await IlovadanSorovAsync(ilova, tur, bugun.AddDays(11), bugun.AddDays(11))).GetProperty("id").GetGuid();
        var bekorQilingan = (await IlovadanSorovAsync(ilova, tur, bugun.AddDays(12), bugun.AddDays(12))).GetProperty("id").GetGuid();
        var eskirgan = (await IlovadanSorovAsync(ilova, tur, bugun.AddDays(13), bugun.AddDays(13))).GetProperty("id").GetGuid();

        (await admin.PostAsJsonAsync(Yol($"/requests/{tasdiqlangan}/decide"), new { approve = true }, ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await admin.PostAsJsonAsync(Yol($"/requests/{radEtilgan}/decide"), new { approve = false }, ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ilova.PostAsync(Men($"/requests/{bekorQilingan}/cancel"), null, ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        foreach (var (id, kun) in new[] { (tasdiqlangan, 10), (radEtilgan, 11), (bekorQilingan, 12) })
        {
            // Versiya «to'g'ri» bo'lsa ham — holat birinchi aytiladi (§3.3 ustuvorlik).
            using var javob = await ilova.PutAsJsonAsync(
                Men($"/requests/{id}"),
                new { version = 1, absenceTypeId = tur, fromDate = Iso(bugun.AddDays(kun)), toDate = Iso(bugun.AddDays(kun)) },
                ct);
            await XatoKutAsync(javob, HttpStatusCode.Conflict, "davomat.sorov_qaror_qabul_qilingan");
        }

        using (var birinchi = await ilova.PutAsJsonAsync(
                   Men($"/requests/{eskirgan}"),
                   new { version = 0, absenceTypeId = tur, fromDate = Iso(bugun.AddDays(13)), toDate = Iso(bugun.AddDays(13)), note = "1" },
                   ct))
        {
            birinchi.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(birinchi));
        }

        using (var ikkinchi = await ilova.PutAsJsonAsync(
                   Men($"/requests/{eskirgan}"),
                   new { version = 0, absenceTypeId = tur, fromDate = Iso(bugun.AddDays(13)), toDate = Iso(bugun.AddDays(13)), note = "2" },
                   ct))
        {
            await XatoKutAsync(ikkinchi, HttpStatusCode.Conflict, "davomat.sorov_ozgargan");
        }
    }

    /// <summary>Tahrir yaratishdagi qoidalarning o'zi bilan tekshiriladi (§3.1).</summary>
    [Fact]
    public async Task Tahrir_yaratishdagi_qoidalar_bilan_tekshiriladi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var belgi = Belgi();
        var tur = await YoqlikTuriAsync(admin, "Qoida turi " + belgi);
        var faqatHr = await YoqlikTuriAsync(admin, "Faqat HR " + belgi, employeeCanRequest: false);
        var faolsiz = await YoqlikTuriAsync(admin, "Faolsiz " + belgi);
        using (var ochir = await admin.PostAsync(
                   new Uri($"/api/catalog/absence-types/{faolsiz}/deactivate?reason=sinov", UriKind.Relative), null, ct))
        {
            ochir.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(ochir));
        }

        var xodim = await XodimAsync(belgi);
        using var ilova = await IlovaMijoziAsync(admin, xodim.Id);
        var sana = BugunToshkentda().AddDays(20);

        var id = (await IlovadanSorovAsync(ilova, tur, sana, sana)).GetProperty("id").GetGuid();
        _ = await IlovadanSorovAsync(ilova, tur, sana.AddDays(2), sana.AddDays(3));

        async Task Xato(object tana, HttpStatusCode holat, string kod)
        {
            using var javob = await ilova.PutAsJsonAsync(Men($"/requests/{id}"), tana, ct);
            await XatoKutAsync(javob, holat, kod);
        }

        await Xato(new { version = 0, absenceTypeId = tur, fromDate = Iso(sana), toDate = Iso(sana.AddDays(-1)) }, HttpStatusCode.BadRequest, "davomat.sorov_davri_notogri");
        await Xato(new { version = 0, absenceTypeId = faqatHr, fromDate = Iso(sana), toDate = Iso(sana) }, HttpStatusCode.BadRequest, "davomat.yoqlik_turi_xodimga_yopiq");
        await Xato(new { version = 0, absenceTypeId = faolsiz, fromDate = Iso(sana), toDate = Iso(sana) }, HttpStatusCode.BadRequest, "davomat.yoqlik_turi_faolsiz");
        await Xato(new { version = 0, absenceTypeId = Guid.NewGuid(), fromDate = Iso(sana), toDate = Iso(sana) }, HttpStatusCode.NotFound, "davomat.yoqlik_turi_topilmadi");
        await Xato(new { version = 0, absenceTypeId = tur, fromDate = Iso(sana), toDate = Iso(sana.AddDays(2)) }, HttpStatusCode.Conflict, "davomat.sorov_takroriy");
        await Xato(
            new { version = 0, absenceTypeId = tur, fromDate = Iso(sana), toDate = Iso(sana), note = new string('a', 2001) },
            HttpStatusCode.BadRequest,
            "davomat.izoh_uzun");

        // O'z sanalari bilan kesishmaydi — so'rovning o'zi hisobga olinmaydi.
        using (var ozi = await ilova.PutAsJsonAsync(
                   Men($"/requests/{id}"),
                   new { version = 0, absenceTypeId = tur, fromDate = Iso(sana), toDate = Iso(sana), note = new string('a', 2000) },
                   ct))
        {
            ozi.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(ozi));
            (await JsonAsync(ozi)).GetProperty("version").GetInt32().ShouldBe(1, "rad etilgan urinishlar versiyani oshirmagan");
        }

        // Yaratishda ham uzun izoh — 400, 500 emas.
        using (var yangi = await ilova.PostAsJsonAsync(
                   Men("/requests"),
                   new { absenceTypeId = tur, fromDate = Iso(sana.AddDays(9)), toDate = Iso(sana.AddDays(9)), note = new string('a', 2001) },
                   ct))
        {
            await XatoKutAsync(yangi, HttpStatusCode.BadRequest, "davomat.izoh_uzun");
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Jadval so'rovi — xodimning tahriri
    // ════════════════════════════════════════════════════════════

    [Fact]
    public async Task Xodim_jadval_sorovini_ozgartiradi_turi_almashadi_oyna_va_tanaffus_qoidasi_ishlaydi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var belgi = Belgi();
        var xodim = await XodimAsync(belgi);
        await XodimgaJadvalAsync(admin, xodim.Id, "Tahrir jadvali " + belgi);
        using var ilova = await IlovaMijoziAsync(admin, xodim.Id);

        var kun = JumaEmas(BugunToshkentda().AddDays(3));
        var juft = JumaEmas(kun.AddDays(1));

        var sorov = await IlovadanJadvalSorovAsync(ilova, kun, null);
        var id = sorov.GetProperty("id").GetGuid();

        // ── Change → Change: yangi soatlar, qoidaga mos tanaffus ──
        using (var tahrir = await ilova.PutAsJsonAsync(
                   Men($"/schedule-requests/{id}"),
                   new
                   {
                       version = 0,
                       kind = "Change",
                       days = new[] { new { date = Iso(kun), isWorking = true, startTime = "10:00:00", endTime = "19:00:00", breakStart = "12:00:00", breakEnd = "13:00:00" } },
                       note = "Kech kelaman",
                   },
                   ct))
        {
            tahrir.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(tahrir));
            var yozuv = await JsonAsync(tahrir);
            yozuv.GetProperty("version").GetInt32().ShouldBe(1);
            yozuv.GetProperty("editedByEmployee").GetBoolean().ShouldBeTrue();
            var kunYozuvi = yozuv.GetProperty("days").EnumerateArray().ShouldHaveSingleItem();
            kunYozuvi.GetProperty("startTime").GetString().ShouldStartWith("10:00");
            kunYozuvi.GetProperty("breakMinutes").GetInt32().ShouldBe(60);
        }

        // ── Tanaffus qoidasi: dushanba–payshanba 120 daqiqa va oraliqdan tashqari ──
        using (var uzun = await ilova.PutAsJsonAsync(
                   Men($"/schedule-requests/{id}"),
                   new { version = 1, kind = "Change", days = new[] { new { date = Iso(kun), isWorking = true, startTime = "09:00:00", endTime = "18:00:00", breakStart = "12:00:00", breakEnd = "14:00:00" } } },
                   ct))
        {
            await XatoKutAsync(uzun, HttpStatusCode.BadRequest, "davomat.jadval_sorovi_tanaffus_davomiyligi");
        }

        using (var tashqari = await ilova.PutAsJsonAsync(
                   Men($"/schedule-requests/{id}"),
                   new { version = 1, kind = "Change", days = new[] { new { date = Iso(kun), isWorking = true, startTime = "09:00:00", endTime = "18:00:00", breakStart = "11:00:00", breakEnd = "12:00:00" } } },
                   ct))
        {
            await XatoKutAsync(tashqari, HttpStatusCode.BadRequest, "davomat.jadval_sorovi_tanaffus_oraligi");
        }

        // ── Sana oynasi: bugundan 32 kun oldin ──
        using (var eski = await ilova.PutAsJsonAsync(
                   Men($"/schedule-requests/{id}"),
                   new { version = 1, kind = "Change", days = new[] { new { date = Iso(BugunToshkentda().AddDays(-32)), isWorking = false } } },
                   ct))
        {
            await XatoKutAsync(eski, HttpStatusCode.BadRequest, "davomat.jadval_sorovi_sana_oynasi");
        }

        // ── Change → Swap: kunlar joriy rejalardan qayta suratga olinadi ──
        using (var almashtirish = await ilova.PutAsJsonAsync(
                   Men($"/schedule-requests/{id}"),
                   new { version = 1, kind = "Swap", swaps = new[] { new { date = Iso(kun), withDate = Iso(juft) } } },
                   ct))
        {
            almashtirish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(almashtirish));
            var yozuv = await JsonAsync(almashtirish);
            yozuv.GetProperty("kind").GetString().ShouldBe("Swap");
            yozuv.GetProperty("version").GetInt32().ShouldBe(2);

            var kunlar = yozuv.GetProperty("days").EnumerateArray().ToList();
            kunlar.Count.ShouldBe(2);
            kunlar.ShouldAllBe(item => item.GetProperty("startTime").GetString()!.StartsWith("09:00", StringComparison.Ordinal));
            kunlar.Single(item => item.GetProperty("date").GetString() == Iso(kun))
                .GetProperty("swapWith").GetString().ShouldBe(Iso(juft));
        }

        // ── Eskirgan versiya ──
        using (var eskirgan = await ilova.PutAsJsonAsync(
                   Men($"/schedule-requests/{id}"),
                   new { version = 1, kind = "Change", days = new[] { new { date = Iso(kun), isWorking = false } } },
                   ct))
        {
            await XatoKutAsync(eskirgan, HttpStatusCode.Conflict, "davomat.sorov_ozgargan");
        }

        var audit = await AuditAsync("ScheduleChangeRequest", id, ct);
        audit.ShouldContain(log => log.Action == AuditAction.Update && log.ActorKind == AuditActorKind.EmployeeAccount);
    }

    // ════════════════════════════════════════════════════════════
    //  HR — versiya qatlami (§3.3, 2-qatlam)
    // ════════════════════════════════════════════════════════════

    [Fact]
    public async Task HR_tahriri_va_qarori_eskirgan_versiyada_409_joriy_va_versiyasiz_200()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var belgi = Belgi();
        var tur = await YoqlikTuriAsync(admin, "HR versiya " + belgi);
        var xodim = await XodimAsync(belgi);
        using var ilova = await IlovaMijoziAsync(admin, xodim.Id);
        var bugun = BugunToshkentda();

        // ── Yo'qlik so'rovi ──
        var sana = bugun.AddDays(30);
        var yoqlik = (await IlovadanSorovAsync(ilova, tur, sana, sana)).GetProperty("id").GetGuid();
        object Tana(int? versiya) => new { employeeId = xodim.Id, absenceTypeId = tur, fromDate = Iso(sana), toDate = Iso(sana), version = versiya };

        await XatoKutAsync(await admin.PutAsJsonAsync(Yol($"/requests/{yoqlik}"), Tana(5), ct), HttpStatusCode.Conflict, "davomat.sorov_ozgargan");
        (await admin.PutAsJsonAsync(Yol($"/requests/{yoqlik}"), Tana(0), ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await admin.PutAsJsonAsync(Yol($"/requests/{yoqlik}"), Tana(null), ct)).StatusCode.ShouldBe(HttpStatusCode.OK, "eski mijoz");

        await XatoKutAsync(
            await admin.PostAsJsonAsync(Yol($"/requests/{yoqlik}/decide"), new { approve = true, version = 1 }, ct),
            HttpStatusCode.Conflict,
            "davomat.sorov_ozgargan");
        await XatoKutAsync(
            await admin.PostAsJsonAsync(Yol($"/requests/{yoqlik}/decide"), new { approve = true, decisionNote = new string('a', 2001), version = 2 }, ct),
            HttpStatusCode.BadRequest,
            "davomat.izoh_uzun");

        using (var qaror = await admin.PostAsJsonAsync(Yol($"/requests/{yoqlik}/decide"), new { approve = true, version = 2 }, ct))
        {
            qaror.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qaror));
            (await JsonAsync(qaror)).GetProperty("version").GetInt32().ShouldBe(3);
        }

        var ikkinchi = (await IlovadanSorovAsync(ilova, tur, sana.AddDays(1), sana.AddDays(1))).GetProperty("id").GetGuid();
        (await admin.PostAsJsonAsync(Yol($"/requests/{ikkinchi}/decide"), new { approve = false }, ct)).StatusCode
            .ShouldBe(HttpStatusCode.OK, "versiyasiz qaror — eski mijoz");

        // ── Jadval so'rovi ──
        var kun = bugun.AddDays(31);
        var jadval = (await IlovadanJadvalSorovAsync(ilova, kun, null)).GetProperty("id").GetGuid();
        object JadvalTana(int? versiya)
            => new { employeeId = xodim.Id, kind = "Change", days = new[] { new { date = Iso(kun), isWorking = false } }, version = versiya };

        await XatoKutAsync(await admin.PutAsJsonAsync(Yol($"/schedule-requests/{jadval}"), JadvalTana(3), ct), HttpStatusCode.Conflict, "davomat.sorov_ozgargan");
        (await admin.PutAsJsonAsync(Yol($"/schedule-requests/{jadval}"), JadvalTana(0), ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await admin.PutAsJsonAsync(Yol($"/schedule-requests/{jadval}"), JadvalTana(null), ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        await XatoKutAsync(
            await admin.PostAsJsonAsync(Yol($"/schedule-requests/{jadval}/decide"), new { approve = false, version = 0 }, ct),
            HttpStatusCode.Conflict,
            "davomat.sorov_ozgargan");

        using (var qaror = await admin.PostAsJsonAsync(Yol($"/schedule-requests/{jadval}/decide"), new { approve = false, version = 2 }, ct))
        {
            qaror.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qaror));
            var yozuv = await JsonAsync(qaror);
            yozuv.GetProperty("version").GetInt32().ShouldBe(3);
            yozuv.GetProperty("editedAt").ValueKind.ShouldBe(JsonValueKind.String, "HR tahriri ham lahzani yozadi");
            yozuv.GetProperty("editedByEmployee").GetBoolean().ShouldBeFalse();
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Poyga — parallel yozuv hech qachon 500 emas (§3.3, 1-qatlam)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Xodim tahriri va HR qarori (jadval so'rovida — HR tahriri ham) BIR VAQTDA: aynan bittasi o'tadi,
    /// ikkinchisi 409 — qaysi kod bilan ekani tartibga bog'liq (avval o'tgani holatni yoki versiyani
    /// o'zgartirgan, yoki bazada yutgan).
    /// </summary>
    [Fact]
    public async Task Parallel_tahrir_va_qaror_bittasi_otadi_ikkinchisi_409()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var belgi = Belgi();
        var tur = await YoqlikTuriAsync(admin, "Poyga turi " + belgi);
        var xodim = await XodimAsync(belgi);
        using var ilova = await IlovaMijoziAsync(admin, xodim.Id);
        var bugun = BugunToshkentda();

        for (var i = 0; i < 4; i++)
        {
            var sana = bugun.AddDays(40 + i);
            var yoqlik = (await IlovadanSorovAsync(ilova, tur, sana, sana)).GetProperty("id").GetGuid();

            await BittasiOtadiAsync(
                ilova.PutAsJsonAsync(Men($"/requests/{yoqlik}"), new { version = 0, absenceTypeId = tur, fromDate = Iso(sana), toDate = Iso(sana), note = "poyga" }, ct),
                admin.PostAsJsonAsync(Yol($"/requests/{yoqlik}/decide"), new { approve = true, version = 0 }, ct));

            var kun = bugun.AddDays(50 + i);
            var jadval = (await IlovadanJadvalSorovAsync(ilova, kun, null)).GetProperty("id").GetGuid();

            await BittasiOtadiAsync(
                ilova.PutAsJsonAsync(
                    Men($"/schedule-requests/{jadval}"),
                    new { version = 0, kind = "Change", days = new[] { new { date = Iso(kun), isWorking = true, startTime = "09:00:00", endTime = "18:00:00" } } },
                    ct),
                admin.PostAsJsonAsync(Yol($"/schedule-requests/{jadval}/decide"), new { approve = true, version = 0 }, ct));

            // Ikki tahrir — ikkalasi bir sanali kunni almashtiradi (bazadagi (so'rov, sana) unikalligi).
            var ikkiTahrir = (await IlovadanJadvalSorovAsync(ilova, kun.AddDays(20), null)).GetProperty("id").GetGuid();
            var yangiKun = new[] { new { date = Iso(kun.AddDays(20)), isWorking = true, startTime = "10:00:00", endTime = "19:00:00" } };

            await BittasiOtadiAsync(
                ilova.PutAsJsonAsync(Men($"/schedule-requests/{ikkiTahrir}"), new { version = 0, kind = "Change", days = yangiKun }, ct),
                admin.PutAsJsonAsync(
                    Yol($"/schedule-requests/{ikkiTahrir}"),
                    new { employeeId = xodim.Id, kind = "Change", days = yangiKun, version = 0 },
                    ct));
        }
    }

    [Fact]
    public async Task Parallel_bekor_qilish_va_qaror_bittasi_otadi_ikkinchisi_409()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var belgi = Belgi();
        var tur = await YoqlikTuriAsync(admin, "Bekor poyga " + belgi);
        var xodim = await XodimAsync(belgi);
        using var ilova = await IlovaMijoziAsync(admin, xodim.Id);
        var bugun = BugunToshkentda();

        for (var i = 0; i < 4; i++)
        {
            var sana = bugun.AddDays(60 + i);
            var yoqlik = (await IlovadanSorovAsync(ilova, tur, sana, sana)).GetProperty("id").GetGuid();

            await BittasiOtadiAsync(
                ilova.PostAsync(Men($"/requests/{yoqlik}/cancel"), null, ct),
                admin.PostAsJsonAsync(Yol($"/requests/{yoqlik}/decide"), new { approve = false, version = 0 }, ct));

            var kun = bugun.AddDays(70 + i);
            var jadval = (await IlovadanJadvalSorovAsync(ilova, kun, null)).GetProperty("id").GetGuid();

            await BittasiOtadiAsync(
                ilova.PostAsync(Men($"/schedule-requests/{jadval}/cancel"), null, ct),
                admin.PostAsJsonAsync(Yol($"/schedule-requests/{jadval}/decide"), new { approve = true, version = 0 }, ct));
        }
    }

    /// <summary>
    /// Poyganing o'zi, tasodifsiz: ikki yozuvchi so'rovni bir versiyada o'qiydi, birinchisi saqlaydi —
    /// ikkinchisining saqlashi bazada ushlanadi va «eskirgan yozuv» deb taniladi (handler uni 409 qiladi).
    /// Jadval so'rovida ikkala tahrir ham kunlarni (bir xil sana bilan) almashtiradi.
    /// </summary>
    [Fact]
    public async Task Kechikkan_yozuv_bazada_ushlanadi_va_eskirgan_deb_taniladi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var belgi = Belgi();
        var tur = await YoqlikTuriAsync(admin, "Baza poygasi " + belgi);
        var xodim = await XodimAsync(belgi);
        using var ilova = await IlovaMijoziAsync(admin, xodim.Id);
        var sana = BugunToshkentda().AddDays(80);
        var kun = sana.AddDays(1);

        var yoqlik = (await IlovadanSorovAsync(ilova, tur, sana, sana)).GetProperty("id").GetGuid();
        var jadval = (await IlovadanJadvalSorovAsync(ilova, kun, "09:00:00")).GetProperty("id").GetGuid();

        await using var birinchi = _factory.Services.CreateAsyncScope();
        await using var ikkinchi = _factory.Services.CreateAsyncScope();
        var golib = birinchi.ServiceProvider.GetRequiredService<AppDbContext>();
        var kechikkan = ikkinchi.ServiceProvider.GetRequiredService<AppDbContext>();
        var fault = ikkinchi.ServiceProvider.GetRequiredService<IDatabaseFault>();

        // ── Yo'qlik so'rovi: qaror va tahrir ──
        var a = await golib.AbsenceRequests.SingleAsync(request => request.Id == yoqlik, ct);
        var b = await kechikkan.AbsenceRequests.SingleAsync(request => request.Id == yoqlik, ct);

        a.Approve(Guid.NewGuid(), DateTimeOffset.UtcNow).IsSuccess.ShouldBeTrue();
        await golib.SaveChangesAsync(ct);

        b.Update(tur, sana, sana, null, null, "kech", DateTimeOffset.UtcNow, Guid.NewGuid()).IsSuccess.ShouldBeTrue();
        var xato = await Should.ThrowAsync<Exception>(() => kechikkan.SaveChangesAsync(ct));
        fault.IsStaleRequestWrite(xato).ShouldBeTrue(xato.ToString());
        fault.IsConcurrentDayWrite(xato).ShouldBeFalse();

        // ── Jadval so'rovi: ikki tahrir, ikkalasi kunni almashtiradi ──
        var c = await golib.ScheduleChangeRequests.Include(request => request.Days).SingleAsync(request => request.Id == jadval, ct);
        var d = await kechikkan.ScheduleChangeRequests.Include(request => request.Days).SingleAsync(request => request.Id == jadval, ct);

        c.Update(ScheduleChangeKind.Change, [new ScheduleChangeDayInput(kun, false)], "birinchi", at: DateTimeOffset.UtcNow).IsSuccess.ShouldBeTrue();
        await golib.SaveChangesAsync(ct);

        d.Update(
                ScheduleChangeKind.Change,
                [new ScheduleChangeDayInput(kun, true, new TimeOnly(10, 0), new TimeOnly(19, 0))],
                "ikkinchi",
                at: DateTimeOffset.UtcNow)
            .IsSuccess.ShouldBeTrue();
        var xato2 = await Should.ThrowAsync<Exception>(() => kechikkan.SaveChangesAsync(ct));
        fault.IsStaleRequestWrite(xato2).ShouldBeTrue(xato2.ToString());

        // G'olibning yozuvi joyida.
        var yakun = await JsonAsync(await admin.GetAsync(Yol($"/schedule-requests/{jadval}"), ct));
        yakun.GetProperty("note").GetString().ShouldBe("birinchi");
        yakun.GetProperty("version").GetInt32().ShouldBe(1);
    }

    private static async Task BittasiOtadiAsync(Task<HttpResponseMessage> birinchi, Task<HttpResponseMessage> ikkinchi)
    {
        var javoblar = await Task.WhenAll(birinchi, ikkinchi);

        try
        {
            var holatlar = javoblar.Select(javob => javob.StatusCode).ToList();
            var matn = string.Join(" | ", await Task.WhenAll(javoblar.Select(MatnAsync)));

            holatlar.ShouldNotContain(HttpStatusCode.InternalServerError, matn);
            holatlar.Count(holat => (int)holat is >= 200 and < 300).ShouldBe(1, matn);
            holatlar.Count(holat => holat == HttpStatusCode.Conflict).ShouldBe(1, matn);

            var rad = javoblar.Single(javob => javob.StatusCode == HttpStatusCode.Conflict);
            var kod = JsonDocument.Parse(await MatnAsync(rad)).RootElement.GetProperty("title").GetString();
            kod.ShouldBeOneOf("davomat.sorov_ozgargan", "davomat.sorov_qaror_qabul_qilingan");
        }
        finally
        {
            foreach (var javob in javoblar)
            {
                javob.Dispose();
            }
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Muhit tayyorlash
    // ────────────────────────────────────────────────────────────

    private sealed record Xodim(Guid Id);

    private sealed record AuditQatori(AuditAction Action, AuditActorKind ActorKind);

    private static async Task<JsonElement> IlovadanSorovAsync(HttpClient ilova, Guid tur, DateOnly dan, DateOnly gacha)
    {
        using var javob = await ilova.PostAsJsonAsync(
            Men("/requests"),
            new { absenceTypeId = tur, fromDate = Iso(dan), toDate = Iso(gacha) },
            TestContext.Current.CancellationToken);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        return await JsonAsync(javob);
    }

    /// <summary>Ilovadan «Kunni o'zgartirish» — dam olish kuni (tanaffus qoidasiga tegmaydi) yoki ish kuni.</summary>
    private static async Task<JsonElement> IlovadanJadvalSorovAsync(HttpClient ilova, DateOnly sana, string? boshlanish)
    {
        object kun = boshlanish is null
            ? new { date = Iso(sana), isWorking = false }
            : new { date = Iso(sana), isWorking = true, startTime = boshlanish, endTime = "18:00:00" };

        using var javob = await ilova.PostAsJsonAsync(
            Men("/schedule-requests"),
            new { kind = "Change", days = new[] { kun } },
            TestContext.Current.CancellationToken);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        return await JsonAsync(javob);
    }

    private async Task<List<AuditQatori>> AuditAsync(string entity, Guid id, CancellationToken ct)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kalit = id.ToString();

        return await qamrov.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs.AsNoTracking()
            .Where(log => log.EntityName == entity && log.EntityId == kalit)
            .Select(log => new AuditQatori(log.Action, log.ActorKind))
            .ToListAsync(ct);
    }

    /// <summary>Yo'qlik turi — xodim so'ray oladigan yoki faqat HR kiritadigan.</summary>
    private static async Task<Guid> YoqlikTuriAsync(HttpClient admin, string nom, bool employeeCanRequest = true)
    {
        using var javob = await admin.PostAsJsonAsync(
            new Uri("/api/catalog/absence-types", UriKind.Relative),
            new { name = nom, employeeCanRequest },
            TestContext.Current.CancellationToken);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        return (await JsonAsync(javob)).GetProperty("id").GetGuid();
    }

    /// <summary>Hamma kun 09:00–18:00, tanaffus 60 daqiqa 12:00–15:00 — XODIM pog'onasida biriktiriladi.</summary>
    private static async Task XodimgaJadvalAsync(HttpClient admin, Guid xodimId, string nom)
    {
        var ct = TestContext.Current.CancellationToken;

        using var jadval = await admin.PostAsJsonAsync(
            Yol("/schedules"),
            new
            {
                name = nom,
                graceInMinutes = 10,
                graceOutMinutes = 10,
                note = (string?)null,
                days = Enumerable.Range(1, 7).Select(kun => new
                {
                    dayIndex = kun,
                    isWorking = true,
                    startTime = "09:00",
                    endTime = "18:00",
                    breakMinutes = 60,
                    breakStart = "12:00",
                    breakEnd = "15:00",
                }),
            },
            ct);
        jadval.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(jadval));
        var jadvalId = (await JsonAsync(jadval)).GetProperty("id").GetGuid();

        using var biriktirma = await admin.PostAsJsonAsync(
            Yol("/schedule-assignments"),
            new { workScheduleId = jadvalId, scope = "Employee", employeeId = xodimId, effectiveFrom = "2025-01-01" },
            ct);
        biriktirma.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(biriktirma));
    }

    /// <summary>Xodim — o'z firmasi, mehnat munosabati va takrorlanmas telefoni (ilova logini) bilan.</summary>
    private async Task<Xodim> XodimAsync(string belgi)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var firma = LegalEntity.Create("Tahrir MCHJ " + belgi).Value;
        var xodim = Employee.Create(FullName.Create("Tahrirov", "Xodim", belgi).Value).Value;
        var telefon = "9" + Random.Shared.Next(10_000_000, 99_999_999).ToString(CultureInfo.InvariantCulture);
        xodim.SetPhones(PhoneNumber.Create(telefon).Value);

        kontekst.AddRange(firma, xodim);
        kontekst.Add(Employment.Hire(xodim.Id, firma.Id, new DateOnly(2025, 1, 1)).Value);
        await kontekst.SaveChangesAsync(TestContext.Current.CancellationToken);

        return new Xodim(xodim.Id);
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

    private static Uri Men(string yol) => new("/api/me" + yol, UriKind.Relative);

    private static string Belgi() => Guid.NewGuid().ToString("N")[..8];

    private static string Iso(DateOnly sana) => sana.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateOnly BugunToshkentda() => DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(Tashkent).DateTime);

    /// <summary>Juma bo'lmagan eng yaqin kun — 120 daqiqalik tanaffus u yerda qoidaga zid.</summary>
    private static DateOnly JumaEmas(DateOnly sana) => sana.DayOfWeek == DayOfWeek.Friday ? sana.AddDays(1) : sana;

    private static async Task XatoKutAsync(HttpResponseMessage javob, HttpStatusCode holat, string kod)
    {
        using (javob)
        {
            var matn = await MatnAsync(javob);
            javob.StatusCode.ShouldBe(holat, matn);
            JsonDocument.Parse(matn).RootElement.GetProperty("title").GetString().ShouldBe(kod, matn);
        }
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage javob)
        => JsonDocument.Parse(await MatnAsync(javob)).RootElement.Clone();

    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
