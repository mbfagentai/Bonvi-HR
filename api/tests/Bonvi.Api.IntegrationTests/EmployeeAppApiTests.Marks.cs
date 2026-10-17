using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bonvi.Application.Attendance.Recompute;
using Bonvi.Domain.Attendance;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Xodim ilovasi — belgilash, belgilarning katagi, so'rovlar va HR ko'radigan dalil.
/// </summary>
/// <remarks>
/// Audit topilmalari (2026-09-27) bo'yicha regressiya testlari.
/// </remarks>
public sealed partial class EmployeeAppApiTests
{
    // ════════════════════════════════════════════════════════════
    //  Belgilash: parallel so'rovlar, ishdan ketgan, yaroqsiz o'lchov
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Bir vaqtda kelgan bir nechta bosishdan AYNAN BITTASI o'tish yozadi, qolganlari 409 — 500 emas.
    /// </summary>
    /// <remarks>
    /// Ilgari qulf tranzaksiyasiz olinib darhol bo'shardi: 1,2 soniya farq
    /// bilan kelgan ikkita bosish ikkita o'tish yozardi, bir soniya ichidagisi
    /// esa unikal indeksga urilib 500 berardi (birinchisi saqlangan bo'lsa ham).
    /// </remarks>
    [Fact]
    public async Task Bir_vaqtdagi_bosishlardan_faqat_bittasi_otish_yozadi()
    {
        var ct = TestContext.Current.CancellationToken;
        var belgi = Belgi();
        using var admin = await AdminMijozAsync();

        var xodim = await XodimAsync(belgi);
        await BelgilashniYoqAsync(admin, xodim.Id);
        var markaz = Nuqta(21);
        await ObyektAsync(admin, "Parallel " + belgi, markaz, radius: 300);

        using var ilova = await IlovaMijoziAsync(admin, xodim.Id);

        var javoblar = await Task.WhenAll(
            Enumerable.Range(0, 5).Select(i => BelgilaAsync(ilova, i % 2 == 0 ? "In" : "Out", markaz)));

        try
        {
            var holatlar = javoblar.Select(javob => javob.StatusCode).ToList();

            holatlar.Count(holat => holat == HttpStatusCode.OK).ShouldBe(1, string.Join(", ", holatlar));
            holatlar.Count(holat => holat == HttpStatusCode.Conflict).ShouldBe(4, string.Join(", ", holatlar));

            foreach (var rad in javoblar.Where(javob => javob.StatusCode == HttpStatusCode.Conflict))
            {
                var tana = await JsonAsync(rad);
                tana.GetProperty("title").GetString().ShouldBe("davomat.takroriy_belgi");
                tana.GetProperty("lastAt").GetDateTimeOffset().ShouldNotBe(default);
            }
        }
        finally
        {
            foreach (var javob in javoblar)
            {
                javob.Dispose();
            }
        }

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        (await kontekst.AttendancePasses.AsNoTracking().CountAsync(pass => pass.ResolvedEmployeeId == xodim.Id, ct))
            .ShouldBe(1);
        (await kontekst.MobileCheckIns.AsNoTracking().CountAsync(row => row.EmployeeId == xodim.Id, ct))
            .ShouldBe(1);
    }

    /// <summary>Ishdan ketgan xodim — access token hali amal qilsa ham — belgilay olmaydi.</summary>
    /// <remarks>
    /// 2026-09-27 dan rad javobini <c>/api/me/*</c> to'sig'i beradi
    /// (<c>xodim.hisob_faol_emas</c>) — ishlovchigacha yetib bormaydi. Ilgari
    /// bu yerda belgilash ishlovchisining o'z kodi (<c>davomat.mobil_ruxsat_yoq</c>)
    /// kutilardi; ishlovchidagi tekshiruv ikkinchi himoya bo'lib qoladi.
    /// </remarks>
    [Fact]
    public async Task Ishdan_ketgan_xodim_belgilay_olmaydi()
    {
        var ct = TestContext.Current.CancellationToken;
        var belgi = Belgi();
        using var admin = await AdminMijozAsync();

        var xodim = await XodimAsync(belgi);
        await BelgilashniYoqAsync(admin, xodim.Id);
        var markaz = Nuqta(22);
        await ObyektAsync(admin, "Ketgan " + belgi, markaz, radius: 300);

        using var ilova = await IlovaMijoziAsync(admin, xodim.Id);

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
            var munosabat = await kontekst.Employments.SingleAsync(row => row.EmployeeId == xodim.Id, ct);

            munosabat.Terminate(BugunToshkentda().AddDays(-1), reasonId: null).IsSuccess.ShouldBeTrue();
            await kontekst.SaveChangesAsync(ct);
        }

        using var javob = await BelgilaAsync(ilova, "In", markaz);
        await XatoKutAsync(javob, HttpStatusCode.Forbidden, "xodim.hisob_faol_emas");
        await OtishYoqAsync(xodim.Id);
    }

    /// <summary>
    /// Aniqligi yo'q, nol yoki koordinatasi yo'q/chegaradan tashqari o'lchov — «mukammal» emas, yaroqsiz.
    /// </summary>
    [Fact]
    public async Task Yaroqsiz_olchov_rad_etiladi()
    {
        var ct = TestContext.Current.CancellationToken;
        var belgi = Belgi();
        using var admin = await AdminMijozAsync();

        var xodim = await XodimAsync(belgi);
        await BelgilashniYoqAsync(admin, xodim.Id);
        var markaz = Nuqta(23);
        await ObyektAsync(admin, "Olchov " + belgi, markaz, radius: 300);

        using var ilova = await IlovaMijoziAsync(admin, xodim.Id);

        object[] tanalar =
        [
            new { direction = "In", latitude = markaz.Lat, longitude = markaz.Lon },
            new { direction = "In", latitude = markaz.Lat, longitude = markaz.Lon, accuracyMeters = 0 },
            new { direction = "In", latitude = markaz.Lat, longitude = markaz.Lon, accuracyMeters = -5 },
            new { direction = "In", longitude = markaz.Lon, accuracyMeters = 12 },
            new { direction = "In", latitude = 91m, longitude = markaz.Lon, accuracyMeters = 12 },
            new { direction = "In", latitude = markaz.Lat, longitude = 181m, accuracyMeters = 12 },
        ];

        foreach (var tana in tanalar)
        {
            using var javob = await ilova.PostAsJsonAsync(new Uri("/api/me/check-in", UriKind.Relative), tana, ct);
            await XatoKutAsync(javob, HttpStatusCode.BadRequest, "davomat.joylashuv_notogri");
        }

        await OtishYoqAsync(xodim.Id);
    }

    /// <summary>
    /// Rad javobida ilova jumlaga qo'yadigan QIYMATLAR alohida maydonlarda keladi.
    /// </summary>
    /// <remarks>
    /// Ilgari ular faqat o'zbekcha <c>detail</c> matnining ichida edi — ilova
    /// «Eng yaqin obyekt: {{site}} — {{distance}}» ni bo'sh chiqarardi.
    /// </remarks>
    [Fact]
    public async Task Rad_javobida_qiymatlar_alohida_maydonda()
    {
        var belgi = Belgi();
        using var admin = await AdminMijozAsync();

        var xodim = await XodimAsync(belgi);
        await BelgilashniYoqAsync(admin, xodim.Id);
        var markaz = Nuqta(24);
        await ObyektAsync(admin, "Qiymat " + belgi, markaz, radius: 200);

        using var ilova = await IlovaMijoziAsync(admin, xodim.Id);

        using (var uzoq = await BelgilaAsync(ilova, "In", Siljit(markaz, metr: 500)))
        {
            var tana = await JsonAsync(uzoq);
            tana.GetProperty("title").GetString().ShouldBe("davomat.obyektdan_uzoq");
            tana.GetProperty("siteName").GetString().ShouldBe("Qiymat " + belgi);
            tana.GetProperty("distanceMeters").GetInt32().ShouldBeInRange(295, 305);
        }

        using (var noaniq = await BelgilaAsync(ilova, "In", markaz, aniqlik: 350))
        {
            var tana = await JsonAsync(noaniq);
            tana.GetProperty("title").GetString().ShouldBe("davomat.joylashuv_aniq_emas");
            tana.GetProperty("accuracyMeters").GetDecimal().ShouldBe(350m);
        }

        using var qabul = await BelgilaAsync(ilova, "In", markaz);
        qabul.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qabul));
        var qabulLahza = (await JsonAsync(qabul)).GetProperty("occurredAt").GetDateTimeOffset();

        // Shartnoma: ilova API'sining lahzalari UTC da (`MobileCheckInResultDto` izohi).
        qabulLahza.Offset.ShouldBe(TimeSpan.Zero);

        using var takror = await BelgilaAsync(ilova, "Out", markaz);
        var takrorTana = await JsonAsync(takror);
        takrorTana.GetProperty("title").GetString().ShouldBe("davomat.takroriy_belgi");
        // Baza lahzani mikrosoniyagacha saqlaydi, javob esa xotiradagi (100 ns) qiymat.
        (takrorTana.GetProperty("lastAt").GetDateTimeOffset() - qabulLahza).Duration()
            .ShouldBeLessThan(TimeSpan.FromMilliseconds(1));
    }

    // ════════════════════════════════════════════════════════════
    //  So'rovlar: xodim faqat kutayotganini bekor qiladi
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// HR tasdiqlagan yoki rad etgan so'rovni xodim bekor qila OLMAYDI va HR qarori o'zgarmaydi.
    /// </summary>
    [Fact]
    public async Task Xodim_qaror_qilingan_sorovni_bekor_qila_olmaydi()
    {
        var ct = TestContext.Current.CancellationToken;
        var belgi = Belgi();
        using var admin = await AdminMijozAsync();

        var xodim = await XodimAsync(belgi);
        var tur = await YoqlikTuriAsync(admin, "Ilova turi " + belgi);
        using var ilova = await IlovaMijoziAsync(admin, xodim.Id);

        var bugun = BugunToshkentda();

        foreach (var (kun, tasdiq) in new[] { (bugun.AddDays(10), true), (bugun.AddDays(20), false) })
        {
            var sorovId = await MeningSorovimAsync(ilova, tur, kun);

            using (var qaror = await admin.PostAsJsonAsync(
                       new Uri($"/api/attendance/requests/{sorovId}/decide", UriKind.Relative),
                       new { approve = tasdiq, decisionNote = "HR qarori" },
                       ct))
            {
                qaror.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qaror));
            }

            using var bekor = await ilova.PostAsync(new Uri($"/api/me/requests/{sorovId}/cancel", UriKind.Relative), null, ct);
            await XatoKutAsync(bekor, HttpStatusCode.Conflict, "davomat.sorov_qaror_qabul_qilingan");

            await using var qamrov = _factory.Services.CreateAsyncScope();
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
            var sorov = await kontekst.AbsenceRequests.AsNoTracking().SingleAsync(row => row.Id == sorovId, ct);

            sorov.Status.ShouldBe(tasdiq ? AbsenceRequestStatus.Approved : AbsenceRequestStatus.Rejected);
            sorov.DecidedBy.ShouldNotBeNull();
            sorov.DecidedBy.ShouldNotBe(Guid.Empty, "HR ning qaror yozuvi ustidan yozilmasligi kerak");
            sorov.DecisionNote.ShouldBe("HR qarori");
            sorov.CancelledByAccountId.ShouldBeNull();
        }
    }

    /// <summary>Ilovadan yuborilgan va bekor qilingan so'rovda xodim «kim qildi» sifatida yoziladi.</summary>
    [Fact]
    public async Task Xodim_yuborgan_va_bekor_qilgan_sorovda_xodim_yoziladi()
    {
        var ct = TestContext.Current.CancellationToken;
        var belgi = Belgi();
        using var admin = await AdminMijozAsync();

        var xodim = await XodimAsync(belgi);
        var tur = await YoqlikTuriAsync(admin, "Ilova turi " + belgi);
        using var ilova = await IlovaMijoziAsync(admin, xodim.Id);

        var sorovId = await MeningSorovimAsync(ilova, tur, BugunToshkentda().AddDays(5));

        Guid hisobId;
        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
            hisobId = await kontekst.EmployeeAccounts.AsNoTracking()
                .Where(row => row.EmployeeId == xodim.Id)
                .Select(row => row.Id)
                .SingleAsync(ct);

            (await kontekst.AbsenceRequests.AsNoTracking().SingleAsync(row => row.Id == sorovId, ct))
                .SubmittedByAccountId.ShouldBe(hisobId);
        }

        using (var bekor = await ilova.PostAsync(new Uri($"/api/me/requests/{sorovId}/cancel", UriKind.Relative), null, ct))
        {
            bekor.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(bekor));
        }

        using (var takror = await ilova.PostAsync(new Uri($"/api/me/requests/{sorovId}/cancel", UriKind.Relative), null, ct))
        {
            await XatoKutAsync(takror, HttpStatusCode.Conflict, "davomat.sorov_bekor_qilingan");
        }

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
            var sorov = await kontekst.AbsenceRequests.AsNoTracking().SingleAsync(row => row.Id == sorovId, ct);

            sorov.Status.ShouldBe(AbsenceRequestStatus.Cancelled);
            sorov.CancelledByAccountId.ShouldBe(hisobId);
            sorov.DecidedBy.ShouldBeNull("HR foydalanuvchisi maydoniga xodim hisobi yoki Guid.Empty yozilmaydi");
        }

        using var royxat = await ilova.GetAsync(new Uri("/api/me/requests", UriKind.Relative), ct);
        var bitta = (await JsonAsync(royxat)).GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == sorovId);

        bitta.GetProperty("submittedByEmployee").GetBoolean().ShouldBeTrue();
        bitta.GetProperty("cancelledByEmployee").GetBoolean().ShouldBeTrue();
    }

    /// <summary>
    /// Ilova bir kunlik so'rovga vaqt oralig'ini yuboradi (SPEC-so'rovlar A3:
    /// «19.09, 08:30–11:57»); ko'p kunlik so'rovda vaqt rad etiladi.
    /// </summary>
    [Fact]
    public async Task Xodim_bir_kunlik_sorovga_vaqt_oraligini_yuboradi()
    {
        var ct = TestContext.Current.CancellationToken;
        var belgi = Belgi();
        using var admin = await AdminMijozAsync();

        var xodim = await XodimAsync(belgi);
        var tur = await YoqlikTuriAsync(admin, "Vaqtli tur " + belgi);
        using var ilova = await IlovaMijoziAsync(admin, xodim.Id);
        var kun = BugunToshkentda().AddDays(7);

        using (var javob = await ilova.PostAsJsonAsync(
            new Uri("/api/me/requests", UriKind.Relative),
            new { absenceTypeId = tur, fromDate = Iso(kun), toDate = Iso(kun), fromTime = "08:30:00", toTime = "11:57:00" },
            ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

            var sorov = await JsonAsync(javob);
            sorov.GetProperty("fromTime").GetString().ShouldStartWith("08:30");
            sorov.GetProperty("toTime").GetString().ShouldStartWith("11:57");
        }

        using (var kopKunlik = await ilova.PostAsJsonAsync(
            new Uri("/api/me/requests", UriKind.Relative),
            new { absenceTypeId = tur, fromDate = Iso(kun.AddDays(2)), toDate = Iso(kun.AddDays(3)), fromTime = "08:30:00", toTime = "11:57:00" },
            ct))
        {
            await XatoKutAsync(kopKunlik, HttpStatusCode.BadRequest, "davomat.sorov_vaqti_notogri");
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Tungi smena: belgilar o'z katagida, keyingi yo'nalish
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Tungi smenaning ertalabki chiqishi KECHAGI katakka tushadi — tabeldagidek.
    /// </summary>
    /// <remarks>
    /// Ilgari belgilar kalendar sanasi bo'yicha guruhlanardi: 22:00 dagi kirish
    /// dushanbada, 06:00 dagi chiqish seshanbada — ilova bitta smenani ikki
    /// kunga bo'lib ko'rsatardi, tabel esa ikkalasini dushanbaga qo'yardi.
    /// </remarks>
    [Fact]
    public async Task Tungi_smena_belgilari_bitta_katakka_tushadi()
    {
        var ct = TestContext.Current.CancellationToken;
        var belgi = Belgi();
        using var admin = await AdminMijozAsync();

        var xodim = await XodimAsync(belgi);
        await XodimgaTungiJadvalAsync(admin, xodim.Id, belgi);

        var kun = BugunToshkentda().AddDays(-3);
        var kirish = new DateTimeOffset(kun.ToDateTime(new TimeOnly(22, 0)), Tashkent);
        var chiqish = kirish.AddHours(8);

        await TerminalOtishlariAsync(admin, xodim.Id, belgi, (kirish, "In"), (chiqish, "Out"));

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            await qamrov.ServiceProvider.GetRequiredService<AttendanceRecomputeRangeJob>()
                .ExecuteAsync(kun.AddDays(-1), kun.AddDays(1), xodim.Id, ct);
        }

        using var ilova = await IlovaMijoziAsync(admin, xodim.Id);
        using var oy = await ilova.GetAsync(
            new Uri($"/api/me/attendance?year={kun.Year}&month={kun.Month}", UriKind.Relative),
            ct);

        oy.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(oy));
        var kunlar = (await JsonAsync(oy)).GetProperty("days").EnumerateArray().ToList();

        var smena = kunlar.Single(item => item.GetProperty("day").GetProperty("date").GetString() == Iso(kun));
        var belgilar = smena.GetProperty("marks").EnumerateArray().ToList();

        belgilar.Select(item => item.GetProperty("direction").GetString()).ShouldBe(["In", "Out"]);
        smena.GetProperty("day").GetProperty("status").GetString().ShouldBe("Present");

        var ertasi = kunlar.SingleOrDefault(item =>
            item.GetProperty("day").GetProperty("date").GetString() == Iso(kun.AddDays(1)));

        if (ertasi.ValueKind != JsonValueKind.Undefined)
        {
            ertasi.GetProperty("marks").GetArrayLength().ShouldBe(0, "ertalabki chiqish kechagi katakda");
        }
    }

    /// <summary>
    /// Kechagi smenaning kech chiqishi ilovada ham kechagi katakda — tabel bilan bir xil.
    /// </summary>
    /// <remarks>
    /// 09:00–18:00 da ishlab seshanba 00:20 da chiqish dushanbaning oynasidan
    /// (tugash + 6 soat) tashqarida. Tabel uni dushanbaning chiqishi qiladi
    /// (egasining qarori, 2026-09-27); ilova oynalarning o'zidan hisoblasa,
    /// belgi seshanbaga tushib, ikki ekran bir-biriga zid bo'lardi.
    /// </remarks>
    [Fact]
    public async Task Kech_chiqish_ilovada_ham_kechagi_katakda()
    {
        var ct = TestContext.Current.CancellationToken;
        var belgi = Belgi();
        using var admin = await AdminMijozAsync();

        var xodim = await XodimAsync(belgi);
        await XodimgaKunduzgiJadvalAsync(admin, xodim.Id, belgi);

        var kun = BugunToshkentda().AddDays(-3);
        var kirish = new DateTimeOffset(kun.ToDateTime(new TimeOnly(9, 0)), Tashkent);
        var chiqish = new DateTimeOffset(kun.AddDays(1).ToDateTime(new TimeOnly(0, 20)), Tashkent);

        await TerminalOtishlariAsync(admin, xodim.Id, belgi, (kirish, "In"), (chiqish, "Out"));

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            await qamrov.ServiceProvider.GetRequiredService<AttendanceRecomputeRangeJob>()
                .ExecuteAsync(kun.AddDays(-1), kun.AddDays(1), xodim.Id, ct);
        }

        using var ilova = await IlovaMijoziAsync(admin, xodim.Id);
        using var oy = await ilova.GetAsync(
            new Uri($"/api/me/attendance?year={kun.Year}&month={kun.Month}", UriKind.Relative),
            ct);

        oy.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(oy));
        var kunlar = (await JsonAsync(oy)).GetProperty("days").EnumerateArray().ToList();

        var smena = kunlar.Single(item => item.GetProperty("day").GetProperty("date").GetString() == Iso(kun));
        smena.GetProperty("marks").EnumerateArray()
            .Select(item => item.GetProperty("direction").GetString())
            .ShouldBe(["In", "Out"], "00:20 dagi chiqish dushanbaning katagida");

        var ertasi = kunlar.SingleOrDefault(item =>
            item.GetProperty("day").GetProperty("date").GetString() == Iso(kun.AddDays(1)));

        if (ertasi.ValueKind != JsonValueKind.Undefined)
        {
            ertasi.GetProperty("marks").GetArrayLength().ShouldBe(0, "seshanba toza");
        }
    }

    /// <summary>
    /// Keyingi yo'nalish kalendar kunidan emas, oxirgi belgilardan: kechagi kirish — «Chiqish».
    /// </summary>
    [Fact]
    public async Task Keyingi_yonalish_oxirgi_belgidan_aniqlanadi()
    {
        var ct = TestContext.Current.CancellationToken;
        var belgi = Belgi();
        using var admin = await AdminMijozAsync();

        var xodim = await XodimAsync(belgi);
        var kirish = DateTimeOffset.UtcNow.AddHours(-8);

        using var ilova = await IlovaMijoziAsync(admin, xodim.Id);

        using (var boshida = await ilova.GetAsync(new Uri("/api/me/attendance?year=2026&month=9", UriKind.Relative), ct))
        {
            var hozir = (await JsonAsync(boshida)).GetProperty("current");
            hozir.GetProperty("nextDirection").GetString().ShouldBe("In");
            hozir.GetProperty("lastMark").ValueKind.ShouldBe(JsonValueKind.Null);
        }

        await TerminalOtishlariAsync(admin, xodim.Id, belgi, (kirish, "In"));

        using var keyin = await ilova.GetAsync(new Uri("/api/me/attendance?year=2026&month=9", UriKind.Relative), ct);
        var joriy = (await JsonAsync(keyin)).GetProperty("current");

        joriy.GetProperty("nextDirection").GetString().ShouldBe("Out");
        joriy.GetProperty("lastMark").GetProperty("direction").GetString().ShouldBe("In");
        joriy.GetProperty("date").GetString()
            .ShouldBe(Iso(DateOnly.FromDateTime(kirish.ToOffset(Tashkent).DateTime)), "ochiq smena o'z katagida");
    }

    // ════════════════════════════════════════════════════════════
    //  HR: telefon belgisining dalili
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// HR jurnalida telefon belgisining obyekti, masofasi va aniqligi; tabel katagida — telefon belgisi soni.
    /// </summary>
    [Fact]
    public async Task HR_telefon_belgisining_dalilini_koradi()
    {
        var ct = TestContext.Current.CancellationToken;
        var belgi = Belgi();
        using var admin = await AdminMijozAsync();

        var xodim = await XodimAsync(belgi);
        await BelgilashniYoqAsync(admin, xodim.Id);
        var markaz = Nuqta(25);
        await ObyektAsync(admin, "Dalil " + belgi, markaz, radius: 200);

        using (var ilova = await IlovaMijoziAsync(admin, xodim.Id))
        using (var qabul = await BelgilaAsync(ilova, "In", Siljit(markaz, metr: 90), aniqlik: 17))
        {
            qabul.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qabul));
        }

        var kun = DateOnly.FromDateTime((await OtishLahzasiAsync(xodim.Id, ct)).ToOffset(Tashkent).DateTime);

        using var jurnal = await admin.GetAsync(
            new Uri($"/api/attendance/passes?employeeId={xodim.Id}&from={Iso(kun)}&to={Iso(kun)}", UriKind.Relative),
            ct);

        jurnal.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(jurnal));
        var otish = (await JsonAsync(jurnal)).GetProperty("items").EnumerateArray().ShouldHaveSingleItem();

        otish.GetProperty("method").GetString().ShouldBe("Mobile");
        var dalil = otish.GetProperty("mobile");
        dalil.GetProperty("workSiteName").GetString().ShouldBe("Dalil " + belgi);
        dalil.GetProperty("shape").GetString().ShouldBe("Circle");
        dalil.GetProperty("distanceMeters").GetDecimal().ShouldBeInRange(80m, 100m);
        dalil.GetProperty("allowanceMeters").GetInt32().ShouldBe(200);
        dalil.GetProperty("accuracyMeters").GetDecimal().ShouldBe(17m);

        // Hali qayta hisoblanmagan (hosil qilingan) katakda ham — belgi o'z sanasida.
        var katak = await KatakAsync(admin, xodim.Id, kun);
        katak.GetProperty("mobileMarks").GetInt32().ShouldBe(1);
    }

    // ════════════════════════════════════════════════════════════
    //  Yordamchilar
    // ════════════════════════════════════════════════════════════

    private static async Task<Guid> YoqlikTuriAsync(HttpClient admin, string nom)
    {
        using var javob = await admin.PostAsJsonAsync(
            new Uri("/api/catalog/absence-types", UriKind.Relative),
            new { name = nom },
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        return (await JsonAsync(javob)).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> MeningSorovimAsync(HttpClient ilova, Guid tur, DateOnly kun)
    {
        using var javob = await ilova.PostAsJsonAsync(
            new Uri("/api/me/requests", UriKind.Relative),
            new { absenceTypeId = tur, fromDate = Iso(kun), toDate = Iso(kun), note = "Ilovadan" },
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        return (await JsonAsync(javob)).GetProperty("id").GetGuid();
    }

    /// <summary>Har kuni 20:00–08:00 tungi smena — xodim pog'onasida.</summary>
    private static async Task XodimgaKunduzgiJadvalAsync(HttpClient admin, Guid xodimId, string belgi)
    {
        var ct = TestContext.Current.CancellationToken;

        using var jadval = await admin.PostAsJsonAsync(
            new Uri("/api/attendance/schedules", UriKind.Relative),
            new
            {
                name = "Kunduzgi smena " + belgi,
                graceInMinutes = 10,
                graceOutMinutes = 10,
                days = Enumerable.Range(1, 7).Select(kun => new
                {
                    dayIndex = kun,
                    isWorking = true,
                    startTime = "09:00",
                    endTime = "18:00",
                    breakMinutes = 0,
                }),
            },
            ct);

        jadval.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(jadval));
        var jadvalId = (await JsonAsync(jadval)).GetProperty("id").GetGuid();

        using var biriktirma = await admin.PostAsJsonAsync(
            new Uri("/api/attendance/schedule-assignments", UriKind.Relative),
            new { workScheduleId = jadvalId, scope = "Employee", employeeId = xodimId, effectiveFrom = "2025-01-01" },
            ct);

        biriktirma.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(biriktirma));
    }

    private static async Task XodimgaTungiJadvalAsync(HttpClient admin, Guid xodimId, string belgi)
    {
        var ct = TestContext.Current.CancellationToken;

        using var jadval = await admin.PostAsJsonAsync(
            new Uri("/api/attendance/schedules", UriKind.Relative),
            new
            {
                name = "Tungi smena " + belgi,
                graceInMinutes = 10,
                graceOutMinutes = 10,
                days = Enumerable.Range(1, 7).Select(kun => new
                {
                    dayIndex = kun,
                    isWorking = true,
                    startTime = "20:00",
                    endTime = "08:00",
                    breakMinutes = 0,
                }),
            },
            ct);

        jadval.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(jadval));
        var jadvalId = (await JsonAsync(jadval)).GetProperty("id").GetGuid();

        using var biriktirma = await admin.PostAsJsonAsync(
            new Uri("/api/attendance/schedule-assignments", UriKind.Relative),
            new { workScheduleId = jadvalId, scope = "Employee", employeeId = xodimId, effectiveFrom = "2025-01-01" },
            ct);

        biriktirma.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(biriktirma));
    }

    /// <summary>Terminal o'tishlarini ANIQ lahzalar bilan yuboradi (telefon yo'lida vaqtni server qo'yadi).</summary>
    private async Task TerminalOtishlariAsync(
        HttpClient admin,
        Guid xodimId,
        string belgi,
        params (DateTimeOffset Lahza, string Yonalish)[] otishlar)
    {
        var ct = TestContext.Current.CancellationToken;
        var shaxs = "n-" + belgi;
        var token = await QurilmaTokeniAsync(admin, "Tungi turniket " + belgi);

        using (var moslik = await admin.PostAsJsonAsync(
                   new Uri("/api/attendance/identities", UriKind.Relative),
                   new { provider = "Verifix", externalPersonId = shaxs, employeeId = xodimId },
                   ct))
        {
            moslik.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(moslik));
        }

        var tana = JsonSerializer.Serialize(new
        {
            events = otishlar.Select(otish => new
            {
                personId = shaxs,
                occurredAt = otish.Lahza.ToOffset(Tashkent)
                    .ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
                direction = otish.Yonalish,
                eventId = $"{shaxs}-{otish.Lahza.ToUnixTimeSeconds()}",
            }),
        });

        using var sorov = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/attendance/ingest", UriKind.Relative))
        {
            Content = new StringContent(tana, System.Text.Encoding.UTF8, "application/json"),
        };
        sorov.Headers.Add("X-Bonvi-Device-Token", token);

        using var mijoz = _factory.CreateClient();
        using var javob = await mijoz.SendAsync(sorov, ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
    }
}
