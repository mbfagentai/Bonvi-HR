using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bonvi.Application.Attendance.Retention;
using Bonvi.Application.Identity.Abstractions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Organization;
using Bonvi.Domain.Settings;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Davomatni boshqarish endpointlari — uchi-uchiga (SPEC §6, B6, C6 nazorat nuqtasi).
/// </summary>
/// <remarks>
/// <para>
/// Hisoblash mantig'i B4 ning birlik testlarida; bu yerda — ULANISH: navbat
/// sinxron ishlaydimi, qayta hisoblash bazaga to'g'ri yetib boradimi, kodlar
/// va HTTP holatlari SPEC §6.9 dagidekmi, har bir endpoint to'g'ri ruxsat
/// ostidami.
/// </para>
/// <para>
/// Sinf ichidagi testlar bitta bazani bo'lishadi. Shu sababli hech bir test
/// sukut (Default) biriktirma yaratmaydi — u boshqa testning xodimiga ham
/// tushardi; har bir test o'z xodimi va o'z shaxs raqami bilan ishlaydi.
/// </para>
/// </remarks>
public sealed class AttendanceManagementApiTests : IClassFixture<BonviApiFactory>
{
    private static readonly TimeSpan Tashkent = TimeSpan.FromHours(5);

    private readonly BonviApiFactory _factory;

    public AttendanceManagementApiTests(BonviApiFactory factory) => _factory = factory;

    // ════════════════════════════════════════════════════════════
    //  C6 — moslik → eski o'tish bog'lanadi → tabel katagi
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// C6: qurilma → moslanmagan o'tish → moslik → o'tish bog'lanadi VA tabel katagi paydo
    /// bo'ladi, hech kim qayta hisoblashni chaqirmasdan.
    /// </summary>
    [Fact]
    public async Task C6_moslik_yaratilganda_eski_otish_boglanadi_va_tabel_katagi_qayta_hisoblashsiz_paydo_boladi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var belgi = Belgi();
        var xodim = await XodimAsync(belgi, new DateOnly(2025, 1, 1));

        await XodimgaJadvalAsync(admin, xodim.Id, belgi);

        using var qurilma = await admin.PostAsJsonAsync(
            Yol("/devices"),
            new { name = "C6 turniket " + belgi, provider = "Generic" },
            ct);
        qurilma.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qurilma));
        var yaratilgan = await JsonAsync(qurilma);
        var token = yaratilgan.GetProperty("token").GetString()!;

        var sana = BugunToshkentda().AddDays(-3);
        var shaxs = "c6-" + belgi;

        using (var mijoz = _factory.CreateClient())
        {
            using var qabul = await mijoz.SendAsync(
                Hodisalar(
                    token,
                    (shaxs, Lahza(sana, 9, 5), "In"),
                    (shaxs, Lahza(sana, 18, 2), "Out")),
                ct);
            qabul.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qabul));
        }

        var moslanmagan = await JsonAsync(await admin.GetAsync(Yol("/identities/unmatched?pageSize=200"), ct));
        var guruh = moslanmagan.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("rawPersonId").GetString() == shaxs);
        guruh.GetProperty("passCount").GetInt32().ShouldBe(2);

        using var moslik = await admin.PostAsJsonAsync(
            Yol("/identities"),
            new { provider = "Verifix", externalPersonId = shaxs, employeeId = xodim.Id, displayNameInDevice = "C6 Sinov" },
            ct);
        moslik.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(moslik));
        (await JsonAsync(moslik)).GetProperty("employeeName").GetString().ShouldBe(xodim.Name);

        // O'tishlar bog'langan va «hisobga olingan».
        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
            var otishlar = await kontekst.AttendancePasses.AsNoTracking()
                .Where(pass => pass.RawPersonId == shaxs)
                .ToListAsync(ct);

            otishlar.Count.ShouldBe(2);
            otishlar.ShouldAllBe(pass => pass.ResolvedEmployeeId == xodim.Id);
            otishlar.ShouldAllBe(pass => pass.ProcessedAt != null);
        }

        // Tabel katagi — SAQLANGAN qatordan (hosil qilingan emas: worked > 0).
        using var tabel = await admin.GetAsync(
            Yol($"/timesheet?year={sana.Year}&month={sana.Month}&employeeId={xodim.Id}"),
            ct);
        tabel.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(tabel));

        var qator = (await JsonAsync(tabel)).GetProperty("items").EnumerateArray().ShouldHaveSingleItem();
        var katak = qator.GetProperty("cells").EnumerateArray()
            .Single(cell => cell.GetProperty("date").GetString() == Iso(sana));

        katak.GetProperty("status").GetString().ShouldBe("Present");
        katak.GetProperty("workedMinutes").GetInt32().ShouldBe(537);
        katak.GetProperty("lateMinutes").GetInt32().ShouldBe(0, "09:05 — 10 daqiqalik yon berish ichida");
        katak.GetProperty("firstInAt").GetDateTimeOffset().ShouldBe(Lahza(sana, 9, 5));

        var keyin = await JsonAsync(await admin.GetAsync(Yol("/identities/unmatched?pageSize=200"), ct));
        keyin.GetProperty("items").EnumerateArray()
            .ShouldNotContain(item => item.GetProperty("rawPersonId").GetString() == shaxs);
    }

    /// <summary>
    /// Jurnalda kechikish FAQAT kunning birinchi kirishida: saqlangan kun yozuvidan va
    /// bugungi hali hisoblanmagan kun uchun taxtadagi hisoblashdan.
    /// </summary>
    [Fact]
    public async Task Jurnalda_kechikish_faqat_kunning_birinchi_kirishida_korinadi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var belgi = Belgi();
        var xodim = await XodimAsync(belgi, new DateOnly(2025, 1, 1));
        await XodimgaJadvalAsync(admin, xodim.Id, belgi);

        using var qurilma = await admin.PostAsJsonAsync(Yol("/devices"), new { name = "Kech " + belgi, provider = "Generic" }, ct);
        var token = (await JsonAsync(qurilma)).GetProperty("token").GetString()!;

        var otgan = BugunToshkentda().AddDays(-2);
        var bugun = BugunToshkentda();
        var shaxs = "k-" + belgi;

        using var mijoz = _factory.CreateClient();

        // 1) Moslikdan OLDIN — to'ldirish kunni saqlaydi (09:25 → 15 daqiqa kechikish).
        using (var qabul = await mijoz.SendAsync(
                   Hodisalar(token, (shaxs, Lahza(otgan, 9, 25), "In"), (shaxs, Lahza(otgan, 12, 0), "Out"), (shaxs, Lahza(otgan, 13, 0), "In")),
                   ct))
        {
            qabul.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qabul));
        }

        using (var moslik = await admin.PostAsJsonAsync(Yol("/identities"), new { externalPersonId = shaxs, employeeId = xodim.Id }, ct))
        {
            moslik.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(moslik));
        }

        (await KunAsync(xodim.Id, otgan, ct)).ShouldNotBeNull();

        var otganlar = (await JsonAsync(await admin.GetAsync(Yol($"/passes?from={Iso(otgan)}&to={Iso(otgan)}&employeeId={xodim.Id}"), ct)))
            .GetProperty("items").EnumerateArray().ToList();
        otganlar.Count.ShouldBe(3);
        otganlar.Single(item => item.GetProperty("occurredAt").GetDateTimeOffset() == Lahza(otgan, 9, 25))
            .GetProperty("lateMinutes").GetInt32().ShouldBe(15);
        otganlar.Where(item => item.GetProperty("occurredAt").GetDateTimeOffset() != Lahza(otgan, 9, 25))
            .ShouldAllBe(item => item.GetProperty("lateMinutes").ValueKind == JsonValueKind.Null);

        // 2) Moslikdan KEYIN, bugun — qayta hisoblanmagan, qatori yo'q: taxtadagi hisoblash.
        using (var qabul = await mijoz.SendAsync(Hodisalar(token, (shaxs, Lahza(bugun, 9, 40), "In")), ct))
        {
            qabul.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qabul));
        }

        (await KunAsync(xodim.Id, bugun, ct)).ShouldBeNull("bugungi o'tish hali qayta hisoblanmagan");

        var bugungi = (await JsonAsync(await admin.GetAsync(Yol($"/passes?employeeId={xodim.Id}"), ct)))
            .GetProperty("items").EnumerateArray().ShouldHaveSingleItem();
        bugungi.GetProperty("lateMinutes").GetInt32().ShouldBe(30);
    }

    // ════════════════════════════════════════════════════════════
    //  Qayta hisoblash — eskirgan qator o'chiriladi (koordinator qarori)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Ish kuni dam olish kuniga aylangach qayta hisoblash eski «Kelmadi» qatorini O'CHIRADI:
    /// natija noldan hisoblash bilan bir xil.
    /// </summary>
    [Fact]
    public async Task Qayta_hisoblash_dam_olish_kuniga_aylangan_kunning_eski_qatorini_ochiradi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var belgi = Belgi();
        var xodim = await XodimAsync(belgi, new DateOnly(2025, 1, 1));
        var jadvalId = await XodimgaJadvalAsync(admin, xodim.Id, belgi);

        // 2026-06-10 — chorshanba (ISO 3).
        var sana = new DateOnly(2026, 6, 10);

        using (var birinchi = await admin.PostAsJsonAsync(
                   Yol("/recompute"), new { from = Iso(sana), to = Iso(sana), employeeId = xodim.Id }, ct))
        {
            birinchi.StatusCode.ShouldBe(HttpStatusCode.Accepted, await MatnAsync(birinchi));
            var javob = await JsonAsync(birinchi);
            javob.GetProperty("employeeCount").GetInt32().ShouldBe(1);
            javob.GetProperty("dayCount").GetInt32().ShouldBe(1);
            javob.GetProperty("jobId").GetString().ShouldNotBeNullOrWhiteSpace();
        }

        (await KunAsync(xodim.Id, sana, ct)).ShouldNotBeNull().Status.ShouldBe(AttendanceDayStatus.Absent);

        using (var tahrir = await admin.PutAsJsonAsync(
                   Yol($"/schedules/{jadvalId}"),
                   JadvalTanasi("Jadval " + belgi, dayOff: 3),
                   ct))
        {
            tahrir.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(tahrir));
        }

        using (var ikkinchi = await admin.PostAsJsonAsync(
                   Yol("/recompute"), new { from = Iso(sana), to = Iso(sana), employeeId = xodim.Id }, ct))
        {
            ikkinchi.StatusCode.ShouldBe(HttpStatusCode.Accepted, await MatnAsync(ikkinchi));
        }

        (await KunAsync(xodim.Id, sana, ct)).ShouldBeNull("dam olish kuni o'tishsiz — qator bo'lmasligi kerak (§5.4)");

        var tabel = await JsonAsync(await admin.GetAsync(Yol($"/timesheet?year=2026&month=6&employeeId={xodim.Id}"), ct));
        tabel.GetProperty("items")[0].GetProperty("cells").EnumerateArray()
            .Single(cell => cell.GetProperty("date").GetString() == Iso(sana))
            .GetProperty("status").GetString().ShouldBe("DayOff");
    }

    /// <summary>92 kundan uzun davr va teskari davr — hisobotlardagi kodlar bilan.</summary>
    [Fact]
    public async Task Qayta_hisoblash_davri_tekshiriladi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();

        using var uzun = await admin.PostAsJsonAsync(Yol("/recompute"), new { from = "2026-01-01", to = "2026-06-01" }, ct);
        await XatoKutAsync(uzun, HttpStatusCode.BadRequest, "davomat.davr_uzun");

        using var teskari = await admin.PostAsJsonAsync(Yol("/recompute"), new { from = "2026-06-02", to = "2026-06-01" }, ct);
        await XatoKutAsync(teskari, HttpStatusCode.BadRequest, "davomat.davr_notogri");
    }

    // ════════════════════════════════════════════════════════════
    //  Jadvallar — biriktirilgan jadval (koordinator qarori)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Biriktirmasi bor jadval faolsizlantirilmaydi va o'chirilmaydi; faolsiz jadval
    /// biriktirilmaydi; yumshoq o'chirilgan jadvalning kunlari bazada qoladi.
    /// </summary>
    [Fact]
    public async Task Biriktirilgan_jadval_faolsizlantirilmaydi_va_ochirilgan_jadval_kunlarini_yoqotmaydi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var belgi = Belgi();
        var xodim = await XodimAsync(belgi, new DateOnly(2025, 1, 1));
        var jadvalId = await XodimgaJadvalAsync(admin, xodim.Id, belgi);

        using (var faolsiz = await admin.PostAsync(Yol($"/schedules/{jadvalId}/deactivate"), null, ct))
        {
            await XatoKutAsync(faolsiz, HttpStatusCode.Conflict, "davomat.jadval_biriktirilgan");
        }

        using (var ochir = await admin.DeleteAsync(Yol($"/schedules/{jadvalId}"), ct))
        {
            await XatoKutAsync(ochir, HttpStatusCode.Conflict, "davomat.jadval_biriktirilgan");
        }

        var biriktirmalar = await JsonAsync(await admin.GetAsync(Yol($"/schedule-assignments?employeeId={xodim.Id}"), ct));
        var biriktirma = biriktirmalar.EnumerateArray().ShouldHaveSingleItem();
        biriktirma.GetProperty("employeeName").GetString().ShouldBe(xodim.Name);
        biriktirma.GetProperty("workScheduleName").GetString().ShouldBe("Jadval " + belgi);

        using (var olib = await admin.DeleteAsync(Yol($"/schedule-assignments/{biriktirma.GetProperty("id").GetGuid()}"), ct))
        {
            olib.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(olib));
        }

        using (var faolsiz = await admin.PostAsync(Yol($"/schedules/{jadvalId}/deactivate"), null, ct))
        {
            faolsiz.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(faolsiz));
            (await JsonAsync(faolsiz)).GetProperty("isActive").GetBoolean().ShouldBeFalse();
        }

        using (var yangi = await admin.PostAsJsonAsync(
                   Yol("/schedule-assignments"),
                   new { workScheduleId = jadvalId, scope = "Employee", employeeId = xodim.Id, effectiveFrom = "2025-01-01" },
                   ct))
        {
            await XatoKutAsync(yangi, HttpStatusCode.Conflict, "davomat.jadval_faolsiz");
        }

        using (var ochir = await admin.DeleteAsync(Yol($"/schedules/{jadvalId}"), ct))
        {
            ochir.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(ochir));
        }

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var jadval = await kontekst.WorkSchedules.IgnoreQueryFilters().AsNoTracking()
            .Include(schedule => schedule.Days)
            .SingleAsync(schedule => schedule.Id == jadvalId, ct);

        jadval.DeletedAt.ShouldNotBeNull();
        jadval.Days.Count.ShouldBe(7, "yumshoq o'chirish kunlarni bazadan o'chirmasligi kerak");
    }

    /// <summary>Bo'lim pog'onasida yutgan biriktirma OTA bo'limda bo'lsa, javob uni ko'rsatadi.</summary>
    [Fact]
    public async Task Resolve_ota_bolim_biriktirmasini_va_ishlamagan_sanani_korsatadi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var belgi = Belgi();

        Guid otaId;
        Guid ichkiId;
        Guid xodimId;

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
            var firma = LegalEntity.Create("Жадвал МЧЖ " + belgi).Value;
            var ota = Department.Create("Завод " + belgi).Value;
            var ichki = Department.Create("Цех " + belgi, ota.Id).Value;
            var xodim = Employee.Create(FullName.Create("Бўлимов", "Ходим", belgi).Value).Value;
            var munosabat = Employment.Hire(xodim.Id, firma.Id, new DateOnly(2026, 1, 1), ichki.Id).Value;

            kontekst.AddRange(firma, ota, ichki, xodim);
            kontekst.Add(munosabat);
            await kontekst.SaveChangesAsync(ct);

            otaId = ota.Id;
            ichkiId = ichki.Id;
            xodimId = xodim.Id;
        }

        var jadvalId = await JadvalYaratAsync(admin, "Zavod " + belgi);

        using (var biriktir = await admin.PostAsJsonAsync(
                   Yol("/schedule-assignments"),
                   new { workScheduleId = jadvalId, scope = "Department", departmentId = otaId, effectiveFrom = "2026-01-01" },
                   ct))
        {
            biriktir.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(biriktir));
        }

        var javob = await JsonAsync(await admin.GetAsync(Yol($"/schedules/resolve?employeeId={xodimId}&date=2026-06-10"), ct));
        javob.GetProperty("scope").GetString().ShouldBe("Department");
        javob.GetProperty("departmentId").GetGuid().ShouldBe(ichkiId);
        javob.GetProperty("matchedDepartmentId").GetGuid().ShouldBe(otaId);
        javob.GetProperty("workScheduleId").GetGuid().ShouldBe(jadvalId);
        javob.GetProperty("day").GetProperty("dayIndex").GetInt32().ShouldBe(3);

        var oldin = await JsonAsync(await admin.GetAsync(Yol($"/schedules/resolve?employeeId={xodimId}&date=2025-06-10"), ct));
        oldin.GetProperty("employmentId").ValueKind.ShouldBe(JsonValueKind.Null);
        oldin.GetProperty("scope").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    // ════════════════════════════════════════════════════════════
    //  Xato kodlari: *_yoq = 400, *_topilmadi = 404 (SPEC §6.9.1)
    // ════════════════════════════════════════════════════════════

    /// <summary>Bo'sh identifikator — 400 «_yoq», mavjud bo'lmagani — 404 «_topilmadi»; takror — 409.</summary>
    [Fact]
    public async Task Bosh_va_mavjud_bolmagan_identifikator_ikki_xil_kod_va_holat_beradi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var belgi = Belgi();
        var xodim = await XodimAsync(belgi, new DateOnly(2025, 1, 1));

        using (var bosh = await admin.PostAsJsonAsync(
                   Yol("/identities"), new { externalPersonId = "e-" + belgi, employeeId = Guid.Empty }, ct))
        {
            await XatoKutAsync(bosh, HttpStatusCode.BadRequest, "davomat.xodim_yoq");
        }

        using (var yoq = await admin.PostAsJsonAsync(
                   Yol("/identities"), new { externalPersonId = "e-" + belgi, employeeId = Guid.NewGuid() }, ct))
        {
            await XatoKutAsync(yoq, HttpStatusCode.NotFound, "davomat.xodim_topilmadi");
        }

        using (var uzun = await admin.PostAsJsonAsync(
                   Yol("/identities"), new { externalPersonId = new string('9', 65), employeeId = xodim.Id }, ct))
        {
            await XatoKutAsync(uzun, HttpStatusCode.BadRequest, "davomat.shaxs_raqami_uzun");
        }

        using (var birinchi = await admin.PostAsJsonAsync(
                   Yol("/identities"), new { externalPersonId = "e-" + belgi, employeeId = xodim.Id }, ct))
        {
            birinchi.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(birinchi));
        }

        using (var takror = await admin.PostAsJsonAsync(
                   Yol("/identities"), new { externalPersonId = "e-" + belgi, employeeId = xodim.Id }, ct))
        {
            await XatoKutAsync(takror, HttpStatusCode.Conflict, "davomat.moslik_takror");
        }

        using (var jadvalBosh = await admin.PostAsJsonAsync(
                   Yol("/schedule-assignments"),
                   new { workScheduleId = Guid.Empty, scope = "Employee", employeeId = xodim.Id, effectiveFrom = "2026-01-01" },
                   ct))
        {
            await XatoKutAsync(jadvalBosh, HttpStatusCode.BadRequest, "davomat.jadval_yoq");
        }

        using (var jadvalYoq = await admin.PostAsJsonAsync(
                   Yol("/schedule-assignments"),
                   new { workScheduleId = Guid.NewGuid(), scope = "Employee", employeeId = xodim.Id, effectiveFrom = "2026-01-01" },
                   ct))
        {
            await XatoKutAsync(jadvalYoq, HttpStatusCode.NotFound, "davomat.jadval_topilmadi");
        }

        using (var moslikYoq = await admin.DeleteAsync(Yol($"/identities/{Guid.NewGuid()}"), ct))
        {
            await XatoKutAsync(moslikYoq, HttpStatusCode.NotFound, "davomat.moslik_topilmadi");
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Qurilmalar
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Token faqat yaratishda qaytadi; almashtirilgach eskisi o'ladi; o'tishi bor qurilma o'chmaydi;
    /// IP ro'yxati va imzo siri saqlashda tekshiriladi.
    /// </summary>
    [Fact]
    public async Task Qurilma_tokeni_bir_marta_qaytadi_almashtirilgach_eskisi_oladi_va_otishi_bor_qurilma_ochmaydi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var belgi = Belgi();
        var nom = "Kirish " + belgi;

        using var yarat = await admin.PostAsJsonAsync(Yol("/devices"), new { name = nom, provider = "Generic" }, ct);
        yarat.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yarat));
        var yaratilgan = await JsonAsync(yarat);
        var eskiToken = yaratilgan.GetProperty("token").GetString()!;
        var qurilmaId = yaratilgan.GetProperty("device").GetProperty("id").GetGuid();
        yaratilgan.GetProperty("device").GetProperty("tokenPrefix").GetString().ShouldBe(eskiToken[..8]);

        using (var bitta = await admin.GetAsync(Yol($"/devices/{qurilmaId}"), ct))
        {
            var matn = await MatnAsync(bitta);
            bitta.StatusCode.ShouldBe(HttpStatusCode.OK, matn);
            matn.ShouldNotContain(eskiToken);
        }

        using (var takror = await admin.PostAsJsonAsync(Yol("/devices"), new { name = nom.ToUpperInvariant() }, ct))
        {
            await XatoKutAsync(takror, HttpStatusCode.Conflict, "davomat.qurilma_nom_takror");
        }

        using (var ip = await admin.PostAsJsonAsync(Yol("/devices"), new { name = "IP " + belgi, allowedIps = "10.0.0.0/33" }, ct))
        {
            await XatoKutAsync(ip, HttpStatusCode.BadRequest, "davomat.ip_royxati_notogri");
        }

        using (var imzosiz = await admin.PostAsJsonAsync(Yol("/devices"), new { name = "HMAC " + belgi, authMode = "TokenAndHmac" }, ct))
        {
            await XatoKutAsync(imzosiz, HttpStatusCode.BadRequest, "davomat.imzo_siri_yoq");
        }

        // Tahrirda `hmacSecret: null` — sir joyida qoladi, `""` — o'chadi.
        using (var imzoli = await admin.PutAsJsonAsync(
                   Yol($"/devices/{qurilmaId}"),
                   new { name = nom, provider = "Generic", authMode = "TokenAndHmac", hmacSecret = "sir-" + belgi },
                   ct))
        {
            imzoli.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(imzoli));
            (await JsonAsync(imzoli)).GetProperty("hasHmacSecret").GetBoolean().ShouldBeTrue();
        }

        using (var saqla = await admin.PutAsJsonAsync(
                   Yol($"/devices/{qurilmaId}"),
                   new { name = nom, provider = "Generic", authMode = "TokenAndHmac", hmacSecret = (string?)null },
                   ct))
        {
            saqla.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(saqla));
            (await JsonAsync(saqla)).GetProperty("hasHmacSecret").GetBoolean().ShouldBeTrue();
        }

        using (var tozala = await admin.PutAsJsonAsync(
                   Yol($"/devices/{qurilmaId}"),
                   new { name = nom, provider = "Generic", authMode = "Token", hmacSecret = "" },
                   ct))
        {
            tozala.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(tozala));
            (await JsonAsync(tozala)).GetProperty("hasHmacSecret").GetBoolean().ShouldBeFalse();
        }

        using var mijoz = _factory.CreateClient();
        var vaqt = DateTimeOffset.UtcNow.ToOffset(Tashkent).AddMinutes(-1);

        using (var qabul = await mijoz.SendAsync(Hodisalar(eskiToken, ("d-" + belgi, vaqt, "In")), ct))
        {
            qabul.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qabul));
        }

        using var almashtir = await admin.PostAsync(Yol($"/devices/{qurilmaId}/rotate-token"), null, ct);
        almashtir.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(almashtir));
        var yangiToken = (await JsonAsync(almashtir)).GetProperty("token").GetString()!;
        yangiToken.ShouldNotBe(eskiToken);

        using (var eski = await mijoz.SendAsync(Hodisalar(eskiToken, ("d-" + belgi, vaqt.AddSeconds(5), "Out")), ct))
        {
            eski.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using (var yangi = await mijoz.SendAsync(Hodisalar(yangiToken, ("d-" + belgi, vaqt.AddSeconds(5), "Out")), ct))
        {
            yangi.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yangi));
        }

        // #79: ro'yxat va bitta qurilma javobida token HECH QACHON yo'q — na eskisi, na almashtirilgani.
        foreach (var yol in new[] { "/devices?includeInactive=true", $"/devices/{qurilmaId}" })
        {
            using var javob = await admin.GetAsync(Yol(yol), ct);
            var matn = await MatnAsync(javob);
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, matn);
            matn.ShouldNotContain(eskiToken);
            matn.ShouldNotContain(yangiToken);
        }

        var royxat = await JsonAsync(await admin.GetAsync(Yol("/devices"), ct));
        royxat.EnumerateArray().Single(device => device.GetProperty("id").GetGuid() == qurilmaId)
            .GetProperty("passCount24h").GetInt32().ShouldBe(2);

        using (var ochir = await admin.DeleteAsync(Yol($"/devices/{qurilmaId}"), ct))
        {
            await XatoKutAsync(ochir, HttpStatusCode.Conflict, "davomat.qurilma_otishlari_bor");
        }

        using (var faolsiz = await admin.PostAsync(Yol($"/devices/{qurilmaId}/deactivate"), null, ct))
        {
            faolsiz.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(faolsiz));
            (await JsonAsync(faolsiz)).GetProperty("isActive").GetBoolean().ShouldBeFalse();
        }

        (await JsonAsync(await admin.GetAsync(Yol("/devices"), ct))).EnumerateArray()
            .ShouldNotContain(device => device.GetProperty("id").GetGuid() == qurilmaId);
        (await JsonAsync(await admin.GetAsync(Yol("/devices?includeInactive=true"), ct))).EnumerateArray()
            .ShouldContain(device => device.GetProperty("id").GetGuid() == qurilmaId);
    }

    // ════════════════════════════════════════════════════════════
    //  O'tish surati
    // ════════════════════════════════════════════════════════════

    /// <summary>Surat baytlari, ETag bilan 304, va ikki xil 404.</summary>
    [Fact]
    public async Task Otish_surati_beriladi_va_ikki_xil_404_ajraladi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var belgi = Belgi();

        using var yarat = await admin.PostAsJsonAsync(Yol("/devices"), new { name = "Surat " + belgi, provider = "Generic" }, ct);
        var token = (await JsonAsync(yarat)).GetProperty("token").GetString()!;

        // Butun soniyaga: hodisa vaqti ISO satrda soniyasigacha yuboriladi.
        var hozir = DateTimeOffset.UtcNow.ToOffset(Tashkent);
        var vaqt = hozir.AddTicks(-(hozir.Ticks % TimeSpan.TicksPerSecond)).AddMinutes(-2);
        var surat = Convert.ToBase64String(TestImages.JpegYasa(48, 48, 80));

        using (var mijoz = _factory.CreateClient())
        {
            var tana = $$"""
                { "events": [
                    { "personId": "s-{{belgi}}", "occurredAt": "{{IsoLahza(vaqt)}}", "direction": "In", "eventId": "1", "photoBase64": "{{surat}}" },
                    { "personId": "s-{{belgi}}", "occurredAt": "{{IsoLahza(vaqt.AddMinutes(1))}}", "direction": "Out", "eventId": "2" }
                ] }
                """;
            using var qabul = await mijoz.SendAsync(JsonSorov(token, tana), ct);
            qabul.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qabul));
        }

        Guid suratli;
        Guid suratsiz;

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
            var otishlar = await kontekst.AttendancePasses.AsNoTracking()
                .Where(pass => pass.RawPersonId == "s-" + belgi)
                .ToListAsync(ct);

            suratli = otishlar.Single(pass => pass.PhotoKey != null).Id;
            suratsiz = otishlar.Single(pass => pass.PhotoKey == null).Id;
        }

        // Moslanmagan guruhning namunasi — surati BOR o'tish, garchi undan yangiroq suratsiz o'tish bo'lsa ham.
        var guruh = (await JsonAsync(await admin.GetAsync(Yol("/identities/unmatched?pageSize=200"), ct)))
            .GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("rawPersonId").GetString() == "s-" + belgi);
        guruh.GetProperty("samplePassId").GetGuid().ShouldBe(suratli);
        guruh.GetProperty("lastDeviceName").GetString().ShouldBe("Surat " + belgi);
        guruh.GetProperty("lastSeenAt").GetDateTimeOffset().ShouldBe(vaqt.AddMinutes(1));

        using var rasm = await admin.GetAsync(Yol($"/passes/{suratli}/photo"), ct);
        rasm.StatusCode.ShouldBe(HttpStatusCode.OK);
        rasm.Content.Headers.ContentType!.MediaType.ShouldBe("image/jpeg");
        var etag = rasm.Headers.ETag.ShouldNotBeNull();

        using (var sorov = new HttpRequestMessage(HttpMethod.Get, Yol($"/passes/{suratli}/photo")))
        {
            sorov.Headers.IfNoneMatch.Add(etag);
            using var kesh = await admin.SendAsync(sorov, ct);
            kesh.StatusCode.ShouldBe(HttpStatusCode.NotModified);
        }

        using (var yoq = await admin.GetAsync(Yol($"/passes/{suratsiz}/photo"), ct))
        {
            await XatoKutAsync(yoq, HttpStatusCode.NotFound, "davomat.surat_yoq");
        }

        using (var otishYoq = await admin.GetAsync(Yol($"/passes/{Guid.NewGuid()}/photo"), ct))
        {
            await XatoKutAsync(otishYoq, HttpStatusCode.NotFound, "davomat.otish_topilmadi");
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Verifix importi
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// CSV: sarlavha ikkinchi qatorda, nuqta-vergul bilan; ExactName / AlreadyMapped / None;
    /// tasdiqda yaratilgan, o'tkazib yuborilgan va xato qatorlar.
    /// </summary>
    [Fact]
    public async Task Import_csv_oldindan_korsatadi_va_tasdiqda_mosliklar_yaratadi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var belgi = Belgi();
        var xodim = await XodimAsync(belgi, new DateOnly(2025, 1, 1));
        var boglangan = await XodimAsync(belgi + "b", new DateOnly(2025, 1, 1));

        using (var moslik = await admin.PostAsJsonAsync(
                   Yol("/identities"), new { externalPersonId = "i2-" + belgi, employeeId = boglangan.Id }, ct))
        {
            moslik.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(moslik));
        }

        // Fayldagi ism «қ» siz — yig'ish qoidasi uni topishi kerak.
        var csv = string.Join(
            "\r\n",
            "Verifix xodimlar ro'yxati;;",
            "Табельный номер;ФИО;Izoh",
            $"i1-{belgi};{xodim.Name.Replace('Қ', 'К')};",
            $"i2-{belgi};Kimdir Boshqa;",
            $"i3-{belgi};\"Ҳеч Кимов; Ҳеч\";",
            ";Jami: 3;",
            string.Empty);

        using var preview = await Yuklash(admin, Encoding.UTF8.GetBytes(csv), "verifix.csv", ct);
        preview.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(preview));

        var natija = await JsonAsync(preview);
        natija.GetProperty("provider").GetString().ShouldBe("Verifix");
        natija.GetProperty("totalRows").GetInt32().ShouldBe(3, "shaxs raqamisiz «Jami» qatori tashlanadi");

        var qatorlar = natija.GetProperty("rows").EnumerateArray().ToList();
        qatorlar[0].GetProperty("rowNumber").GetInt32().ShouldBe(3);
        qatorlar[0].GetProperty("matchKind").GetString().ShouldBe("ExactName");
        qatorlar[0].GetProperty("suggestedEmployeeId").GetGuid().ShouldBe(xodim.Id);
        qatorlar[1].GetProperty("matchKind").GetString().ShouldBe("AlreadyMapped");
        qatorlar[1].GetProperty("suggestedEmployeeId").GetGuid().ShouldBe(boglangan.Id);
        qatorlar[2].GetProperty("matchKind").GetString().ShouldBe("None");
        qatorlar[2].GetProperty("fullNameInFile").GetString().ShouldBe("Ҳеч Кимов; Ҳеч");

        using var tasdiq = await admin.PostAsJsonAsync(
            Yol("/identities/import/confirm"),
            new
            {
                provider = "Verifix",
                rows = new object[]
                {
                    new { rawPersonId = "i1-" + belgi, employeeId = xodim.Id, displayNameInDevice = "Faylda" },
                    new { rawPersonId = "i2-" + belgi, employeeId = xodim.Id },
                    new { rawPersonId = "i3-" + belgi, employeeId = Guid.NewGuid() },
                    new { rawPersonId = "i1-" + belgi, employeeId = xodim.Id },
                },
            },
            ct);
        tasdiq.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(tasdiq));

        var tasdiqNatija = await JsonAsync(tasdiq);
        tasdiqNatija.GetProperty("created").GetInt32().ShouldBe(1);
        tasdiqNatija.GetProperty("skipped").GetInt32().ShouldBe(2);
        var xato = tasdiqNatija.GetProperty("failed").EnumerateArray().ShouldHaveSingleItem();
        xato.GetProperty("rawPersonId").GetString().ShouldBe("i3-" + belgi);
        xato.GetProperty("code").GetString().ShouldBe("davomat.xodim_topilmadi");

        var mosliklar = await JsonAsync(await admin.GetAsync(Yol($"/identities?search=i1-{belgi}"), ct));
        var yangi = mosliklar.GetProperty("items").EnumerateArray().ShouldHaveSingleItem();
        yangi.GetProperty("source").GetString().ShouldBe("Import");
        yangi.GetProperty("displayNameInDevice").GetString().ShouldBe("Faylda");
    }

    /// <summary>XLSX: son bo'lib saqlangan raqam va JSHSHIR matnga aylanadi, JSHSHIR ism o'rniga yutadi.</summary>
    [Fact]
    public async Task Import_xlsx_sonli_kataklarni_oqiydi_va_fayl_xatolarini_kod_bilan_qaytaradi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var belgi = Belgi();
        // JSHSHIR: 1 raqam (asr/jins) + tug'ilgan sana DDMMYY + 7 raqam. Oxirgi yettitasi
        // tasodifiy — sinf ichidagi boshqa xodimlar bilan unikallik indeksiga urilmasin.
        var pinfl = "3150588" + Random.Shared.Next(0, 10_000_000).ToString("D7", CultureInfo.InvariantCulture);
        var xodim = await XodimAsync(belgi, new DateOnly(2025, 1, 1), pinfl);
        var raqam = 900_000 + Random.Shared.Next(99_999);

        byte[] fayl;
        using (var kitob = new XLWorkbook())
        {
            var varaq = kitob.AddWorksheet("Xodimlar");
            varaq.Cell(1, 1).Value = "Employee No";
            varaq.Cell(1, 2).Value = "Name";
            varaq.Cell(1, 3).Value = "PINFL";
            varaq.Cell(2, 1).Value = raqam;
            varaq.Cell(2, 2).Value = "Boshqa Ism Umuman";
            varaq.Cell(2, 3).Value = double.Parse(pinfl, CultureInfo.InvariantCulture);

            using var oqim = new MemoryStream();
            kitob.SaveAs(oqim);
            fayl = oqim.ToArray();
        }

        using var preview = await Yuklash(admin, fayl, "Verifix.XLSX", ct);
        preview.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(preview));

        var qator = (await JsonAsync(preview)).GetProperty("rows").EnumerateArray().ShouldHaveSingleItem();
        qator.GetProperty("rawPersonId").GetString().ShouldBe(raqam.ToString(CultureInfo.InvariantCulture));
        qator.GetProperty("pinflInFile").GetString().ShouldBe(pinfl);
        qator.GetProperty("matchKind").GetString().ShouldBe("ExactPinfl");
        qator.GetProperty("suggestedEmployeeId").GetGuid().ShouldBe(xodim.Id);

        using (var turi = await Yuklash(admin, "salom"u8.ToArray(), "royxat.txt", ct))
        {
            await XatoKutAsync(turi, HttpStatusCode.BadRequest, "davomat.import_fayl_turi");
        }

        using (var buzuq = await Yuklash(admin, "salom"u8.ToArray(), "royxat.xlsx", ct))
        {
            await XatoKutAsync(buzuq, HttpStatusCode.BadRequest, "davomat.import_fayl_turi");
        }

        using (var ustunsiz = await Yuklash(admin, "Ism;Familiya\r\nAli;Valiyev\r\n"u8.ToArray(), "royxat.csv", ct))
        {
            await XatoKutAsync(ustunsiz, HttpStatusCode.BadRequest, "davomat.import_ustun_topilmadi");
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Saqlash muddatlari (SPEC §2.9)
    // ════════════════════════════════════════════════════════════

    /// <summary>O'tish yozuvi suratdan oldin o'chmasin; 0 — «hech qachon», har doim mos.</summary>
    [Fact]
    public async Task Otish_saqlash_muddati_surat_muddatidan_qisqa_bolmaydi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();

        using (var qisqa = await admin.PutAsJsonAsync(
                   new Uri("/api/settings", UriKind.Relative),
                   new { items = new[] { new { key = SystemSettingKeys.PassRetentionDays, value = "30" } } },
                   ct))
        {
            await XatoKutAsync(qisqa, HttpStatusCode.BadRequest, "davomat.saqlash_muddati_nomuvofiq");
        }

        using (var ikkalasi = await admin.PutAsJsonAsync(
                   new Uri("/api/settings", UriKind.Relative),
                   new
                   {
                       items = new[]
                       {
                           new { key = SystemSettingKeys.PhotoRetentionDays, value = "30" },
                           new { key = SystemSettingKeys.PassRetentionDays, value = "30" },
                       },
                   },
                   ct))
        {
            ikkalasi.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(ikkalasi));
        }

        using (var hechQachon = await admin.PutAsJsonAsync(
                   new Uri("/api/settings", UriKind.Relative),
                   new
                   {
                       items = new[]
                       {
                           new { key = SystemSettingKeys.PhotoRetentionDays, value = "365" },
                           new { key = SystemSettingKeys.PassRetentionDays, value = "0" },
                       },
                   },
                   ct))
        {
            hechQachon.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(hechQachon));
        }
    }

    // ════════════════════════════════════════════════════════════
    //  RBAC matritsasi (SPEC §7.3)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Faqat <c>attendance.view</c> bo'lgan foydalanuvchi har bir <c>manage</c> endpointida va
    /// har bir eksportda (<c>reports.export</c> yo'q) 403 oladi, o'qish endpointlarida esa o'tadi.
    /// </summary>
    [Fact]
    public async Task Faqat_korish_ruxsati_bor_foydalanuvchi_boshqaruv_va_eksportda_403_oladi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var mijoz = _factory.CreateClient();
        mijoz.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await TokenAsync(Permissions.AttendanceView));

        foreach (var (usul, yol) in BoshqaruvEndpointlari.Concat(EksportEndpointlari))
        {
            using var javob = await YuborAsync(mijoz, usul, yol, ct);
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden, $"{usul} {yol}");
        }

        foreach (var (usul, yol) in KorishEndpointlari)
        {
            using var javob = await YuborAsync(mijoz, usul, yol, ct);
            javob.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden, $"{usul} {yol}");
            javob.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized, $"{usul} {yol}");
        }
    }

    /// <summary>Ikkala ruxsati ham yo'q foydalanuvchi HAMMA joyda 403; tokensiz — 401.</summary>
    [Fact]
    public async Task Ruxsatsiz_foydalanuvchi_hamma_davomat_endpointida_403_tokensiz_401_oladi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var mijoz = _factory.CreateClient();
        mijoz.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await TokenAsync(Permissions.ReportsExport));

        foreach (var (usul, yol) in BoshqaruvEndpointlari.Concat(EksportEndpointlari).Concat(KorishEndpointlari))
        {
            using var javob = await YuborAsync(mijoz, usul, yol, ct);
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden, $"{usul} {yol}");
        }

        using var anonim = _factory.CreateClient();
        foreach (var (usul, yol) in KorishEndpointlari.Take(3).Concat(BoshqaruvEndpointlari.Take(3)))
        {
            using var javob = await YuborAsync(anonim, usul, yol, ct);
            javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, $"{usul} {yol}");
        }
    }

    /// <summary>
    /// Asosiy sahifa paneli HTTP orqali: tokensiz 401, <c>attendance.view</c> siz 403, u bilan 200 — va
    /// javob shartnomadagi shaklda: sana sukut bo'yicha bugun (Toshkent), holat enum NOMI bilan, sahifa
    /// sukut 50 va 200 bilan qisiladi, «total»/«counts» holat filtriga bo'ysunmaydi, noma'lum holat — 400.
    /// </summary>
    /// <remarks>
    /// Holat qoidalarining o'zi <c>AttendanceReadModelTests</c> da, qotirilgan soat bilan; bu yerda soat
    /// haqiqiy, shu sababli faqat vaqtga bog'liq bo'lmagan narsa tekshiriladi — o'tgan sanadagi o'tishsiz
    /// ish kuni har doim «Absent».
    /// </remarks>
    [Fact]
    public async Task Panel_401_403_200_va_javob_shartnomadagi_shaklda()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var belgi = Belgi();
        var xodim = await XodimAsync(belgi, new DateOnly(2025, 1, 1));
        await XodimgaJadvalAsync(admin, xodim.Id, belgi);

        using (var anonim = _factory.CreateClient())
        using (var javob = await anonim.GetAsync(Yol("/dashboard"), ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using (var ruxsatsiz = _factory.CreateClient())
        {
            ruxsatsiz.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", await TokenAsync(Permissions.ReportsExport, Permissions.AttendanceManage));

            using var javob = await ruxsatsiz.GetAsync(Yol("/dashboard"), ct);
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "attendance.manage ham o'qish ruxsatini bermaydi");
        }

        using var korish = _factory.CreateClient();
        korish.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await TokenAsync(Permissions.AttendanceView));

        // ── Sukutlar: bugun, 1-sahifa, 50 ta ──────────────────────────
        using var bugungiJavob = await korish.GetAsync(Yol("/dashboard"), ct);
        bugungiJavob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(bugungiJavob));
        var bugungi = await JsonAsync(bugungiJavob);

        bugungi.GetProperty("date").GetString().ShouldBe(Iso(BugunToshkentda()));
        bugungi.GetProperty("isToday").GetBoolean().ShouldBeTrue();
        bugungi.GetProperty("asOf").GetDateTimeOffset().ShouldBeInRange(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(1));
        bugungi.GetProperty("page").GetInt32().ShouldBe(1);
        bugungi.GetProperty("pageSize").GetInt32().ShouldBe(50);

        string[] holatlar = ["onTime", "late", "absent", "notStarted", "dayOff", "noSchedule"];
        var sanoq = bugungi.GetProperty("counts");
        holatlar.Sum(holat => sanoq.GetProperty(holat).GetInt32()).ShouldBe(bugungi.GetProperty("total").GetInt32());

        // ── O'tgan sana, bitta xodim: o'tishsiz ish kuni ──────────────
        var otgan = Iso(BugunToshkentda().AddDays(-3));

        using var bittaJavob = await korish.GetAsync(Yol($"/dashboard?date={otgan}&search={belgi}&pageSize=500"), ct);
        bittaJavob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(bittaJavob));
        var bitta = await JsonAsync(bittaJavob);

        bitta.GetProperty("date").GetString().ShouldBe(otgan);
        bitta.GetProperty("isToday").GetBoolean().ShouldBeFalse();
        bitta.GetProperty("pageSize").GetInt32().ShouldBe(200, "eng ko'pi 200 — boshqa ro'yxatlardagi bilan bir xil");
        bitta.GetProperty("totalItems").GetInt32().ShouldBe(1);
        bitta.GetProperty("pageCount").GetInt32().ShouldBe(1);
        bitta.GetProperty("total").GetInt32().ShouldBeGreaterThanOrEqualTo(1);

        var qator = bitta.GetProperty("items").EnumerateArray().ShouldHaveSingleItem();
        qator.GetProperty("employeeId").GetGuid().ShouldBe(xodim.Id);
        qator.GetProperty("fullName").GetString().ShouldBe(xodim.Name);
        qator.GetProperty("status").GetString().ShouldBe("Absent");

        foreach (var bosh in new[] { "photoKey", "positionName", "departmentName", "arrivedAt", "leftAt", "lateMinutes" })
        {
            qator.GetProperty(bosh).ValueKind.ShouldBe(JsonValueKind.Null, bosh);
        }

        // ── Holat filtri faqat jadvalni toraytiradi ───────────────────
        using var kechikkanJavob = await korish.GetAsync(Yol($"/dashboard?date={otgan}&search={belgi}&status=Late&sort=-name"), ct);
        kechikkanJavob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(kechikkanJavob));
        var kechikkan = await JsonAsync(kechikkanJavob);

        kechikkan.GetProperty("items").GetArrayLength().ShouldBe(0);
        kechikkan.GetProperty("totalItems").GetInt32().ShouldBe(0);
        kechikkan.GetProperty("pageCount").GetInt32().ShouldBe(0);
        kechikkan.GetProperty("total").GetInt32().ShouldBe(bitta.GetProperty("total").GetInt32());
        kechikkan.GetProperty("counts").GetRawText().ShouldBe(bitta.GetProperty("counts").GetRawText());

        // ── Noma'lum holat nomi va noto'g'ri sana — yangi xato kodisiz 400 ──
        using (var notogriHolat = await korish.GetAsync(Yol("/dashboard?status=Kechikdi"), ct))
        {
            notogriHolat.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        using (var notogriSana = await korish.GetAsync(Yol("/dashboard?date=17.09.2026"), ct))
        {
            notogriSana.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }
    }

    /// <summary>
    /// #77. <c>attendance.view</c> + <c>attendance.manage</c>, lekin <c>reports.export</c> YO'Q — to'rttala
    /// eksportda 403. Qarshi tekshiruv: <c>view</c> + <c>reports.export</c> (manage'siz) — to'rttalasi 200
    /// va Excel; ya'ni 403 aynan eksport ruxsati yo'qligidan, eksport ortiqcha ruxsat so'ragani uchun emas.
    /// </summary>
    /// <remarks>
    /// ⚠️ Qarshi tekshiruv BOT hisobi nomidan: eksport siyosati (daqiqada 5) foydalanuvchi bo'yicha va
    /// admin nomidan shu sinfda <c>/recompute</c> ham chaqiriladi — bitta bo'linmada 429 olinardi.
    /// 403 javoblari cheklovga tushmaydi (avtorizatsiya konveyerda cheklovdan oldin).
    /// </remarks>
    [Fact]
    public async Task Boshqaruv_ruxsati_bor_eksport_ruxsati_yoq_foydalanuvchi_eksportda_403_oladi()
    {
        var ct = TestContext.Current.CancellationToken;

        using var boshqaruvchi = _factory.CreateClient();
        boshqaruvchi.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await TokenAsync(Permissions.AttendanceView, Permissions.AttendanceManage));

        foreach (var (usul, yol) in EksportEndpointlari)
        {
            using var javob = await YuborAsync(boshqaruvchi, usul, yol, ct);
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden, $"{usul} {yol}");
        }

        using (var nazorat = await YuborAsync(boshqaruvchi, HttpMethod.Get, "/devices", ct))
        {
            nazorat.StatusCode.ShouldBe(HttpStatusCode.OK, "token boshqaruvda ishlashi kerak — 403 tokendan emas");
        }

        using var eksportchi = _factory.CreateClient();
        eksportchi.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await HisobTokeniAsync(BonviApiFactory.TestBotLogin, Permissions.AttendanceView, Permissions.ReportsExport));

        foreach (var (usul, yol) in EksportEndpointlari)
        {
            using var javob = await YuborAsync(eksportchi, usul, yol, ct);
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, $"{usul} {yol}: {await MatnAsync(javob)}");
            javob.Content.Headers.ContentType!.MediaType
                .ShouldBe("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{usul} {yol}");
        }
    }

    /// <summary>
    /// #80. Saqlash muddatlari — faqat <c>settings.manage</c>: HR menejerining standart ruxsatlari (ikkala
    /// davomat ruxsati bilan) sozlamalarni o'zgartira olmaydi — 403 va bazadagi qiymat o'zgarmaydi. O'sha
    /// token davomat qurilmalarini ochadi — 403 ruxsat bo'yicha, yaroqsiz token emas.
    /// </summary>
    [Fact]
    public async Task HR_menejeri_davomat_ruxsatlari_bilan_ham_saqlash_muddatini_ozgartira_olmaydi()
    {
        var ct = TestContext.Current.CancellationToken;

        var (_, hrRuxsatlari) = SystemRoles.Defaults[SystemRoles.HrManager];
        using var hr = _factory.CreateClient();
        hr.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync(hrRuxsatlari));

        using (var qurilmalar = await hr.GetAsync(Yol("/devices"), ct))
        {
            qurilmalar.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qurilmalar));
        }

        var oldin = await SozlamaQiymatlariAsync(ct);

        using (var ozgartir = await hr.PutAsJsonAsync(
                   new Uri("/api/settings", UriKind.Relative),
                   new
                   {
                       items = new[]
                       {
                           new { key = SystemSettingKeys.PhotoRetentionDays, value = "7" },
                           new { key = SystemSettingKeys.PassRetentionDays, value = "0" },
                       },
                   },
                   ct))
        {
            ozgartir.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await MatnAsync(ozgartir));
        }

        (await SozlamaQiymatlariAsync(ct)).ShouldBe(oldin);
    }

    // ════════════════════════════════════════════════════════════
    //  Mosliklar — o'chirish tarixni o'zgartirmaydi (§4.3, §11.1 #27, #28)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// #27, #28. Moslik o'chirilsa o'tgan o'tishlar va ularning kuni XODIMDA qoladi; o'sha raqam
    /// boshqa xodimga qayta bog'lansa ham tarix ko'chmaydi; bitta xodimga IKKINCHI raqam — ruxsat.
    /// </summary>
    [Fact]
    public async Task Moslik_ochirilsa_otgan_otishlar_xodimda_qoladi_va_bitta_xodimga_ikkinchi_raqam_ruxsat()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var belgi = Belgi();
        var xodim = await XodimAsync(belgi, new DateOnly(2025, 1, 1));
        var boshqa = await XodimAsync(belgi + "x", new DateOnly(2025, 1, 1));
        await XodimgaJadvalAsync(admin, xodim.Id, belgi);

        var shaxs = "m27-" + belgi;
        var sana = new DateOnly(2026, 6, 10);
        var otishlar = await MoslanmaganOtishlarAsync(shaxs, (Lahza(sana, 9, 0), PassDirection.In), (Lahza(sana, 18, 0), PassDirection.Out));

        Guid moslikId;
        using (var moslik = await admin.PostAsJsonAsync(Yol("/identities"), new { externalPersonId = shaxs, employeeId = xodim.Id }, ct))
        {
            moslik.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(moslik));
            moslikId = (await JsonAsync(moslik)).GetProperty("id").GetGuid();
        }

        (await OtishEgalariAsync(otishlar, ct)).ShouldAllBe(egasi => egasi == xodim.Id);
        var kun = (await KunAsync(xodim.Id, sana, ct)).ShouldNotBeNull();
        kun.ShouldBe(new KunQatori(AttendanceDayStatus.Present, 540));

        using (var ochir = await admin.DeleteAsync(Yol($"/identities/{moslikId}"), ct))
        {
            ochir.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(ochir));
        }

        (await OtishEgalariAsync(otishlar, ct)).ShouldAllBe(egasi => egasi == xodim.Id);
        (await KunAsync(xodim.Id, sana, ct)).ShouldBe(kun, "o'chirish tabelni jimgina qayta yozmasligi kerak");
        (await JsonAsync(await admin.GetAsync(Yol("/identities/unmatched?pageSize=200"), ct))).GetProperty("items").EnumerateArray()
            .ShouldNotContain(item => item.GetProperty("rawPersonId").GetString() == shaxs);

        // Xato moslik tuzatiladi: o'sha raqam boshqa xodimga. Yumshoq o'chirilgan yozuv to'smaydi,
        // o'tgan o'tishlar esa baribir birinchi xodimda — qayta hisoblash alohida, sanali amal (§4.3).
        using (var qayta = await admin.PostAsJsonAsync(Yol("/identities"), new { externalPersonId = shaxs, employeeId = boshqa.Id }, ct))
        {
            qayta.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qayta));
            (await JsonAsync(qayta)).GetProperty("employeeId").GetGuid().ShouldBe(boshqa.Id);
        }

        (await OtishEgalariAsync(otishlar, ct)).ShouldAllBe(egasi => egasi == xodim.Id);
        (await KunAsync(boshqa.Id, sana, ct)).ShouldBeNull();

        // #28, ikkinchi yarmi: bitta xodimga boshqa raqam — ruxsat (ikkinchi karta, yuz + karta).
        using (var ikkinchi = await admin.PostAsJsonAsync(Yol("/identities"), new { externalPersonId = "m28-" + belgi, employeeId = xodim.Id }, ct))
        {
            ikkinchi.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(ikkinchi));
        }

        // Bazadagi TIRIK mosliklar (ro'yxat endpointi bu yerda ishlatilmaydi — o'chirilganlarni ko'rsatish
        // xatosi alohida testda: `Ochirilgan_moslik_royxatda_korinmaydi_va_sanoq_bilan_mos`).
        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var tirik = await qamrov.ServiceProvider.GetRequiredService<AppDbContext>().AttendanceIdentities.AsNoTracking()
                .Where(identity => identity.ExternalPersonId.EndsWith(belgi))
                .Select(identity => new { identity.ExternalPersonId, identity.EmployeeId })
                .ToListAsync(ct);

            tirik.ShouldBe(
                [new { ExternalPersonId = shaxs, EmployeeId = boshqa.Id }, new { ExternalPersonId = "m28-" + belgi, EmployeeId = xodim.Id }],
                ignoreOrder: true);
        }
    }

    /// <summary>
    /// O'chirilgan (yumshoq) moslik <c>GET /identities</c> ro'yxatida KO'RINMAYDI — na qidiruvda, na xodim
    /// filtrida — va <c>items</c> soni <c>total</c> bilan mos. Aks holda HR o'chirgan moslikni ekranda ko'rib,
    /// uni qayta o'chirmoqchi bo'ladi va 404 <c>davomat.moslik_topilmadi</c> oladi.
    /// </summary>
    [Fact]
    public async Task Ochirilgan_moslik_royxatda_korinmaydi_va_sanoq_bilan_mos()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var belgi = Belgi();
        var xodim = await XodimAsync(belgi, new DateOnly(2025, 1, 1));
        var shaxs = "del-" + belgi;

        Guid moslikId;
        using (var moslik = await admin.PostAsJsonAsync(Yol("/identities"), new { externalPersonId = shaxs, employeeId = xodim.Id }, ct))
        {
            moslik.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(moslik));
            moslikId = (await JsonAsync(moslik)).GetProperty("id").GetGuid();
        }

        var oldin = await JsonAsync(await admin.GetAsync(Yol($"/identities?search={shaxs}"), ct));
        oldin.GetProperty("items").GetArrayLength().ShouldBe(1, "sinov sharti: moslik o'chirishdan oldin ro'yxatda");

        using (var ochir = await admin.DeleteAsync(Yol($"/identities/{moslikId}"), ct))
        {
            ochir.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(ochir));
        }

        foreach (var yol in new[] { $"/identities?search={shaxs}", $"/identities?employeeId={xodim.Id}" })
        {
            var royxat = await JsonAsync(await admin.GetAsync(Yol(yol), ct));
            royxat.GetProperty("total").GetInt32().ShouldBe(0, $"{yol}: {royxat}");
            royxat.GetProperty("items").GetArrayLength().ShouldBe(0, $"{yol}: {royxat}");
        }

        using (var qaytaOchir = await admin.DeleteAsync(Yol($"/identities/{moslikId}"), ct))
        {
            await XatoKutAsync(qaytaOchir, HttpStatusCode.NotFound, "davomat.moslik_topilmadi");
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Import — besh holat, hech narsa yozilmaydi (§4.4, §11.1 #29)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// #29. Bitta .xlsx da beshala holat: JSHSHIR (ismi egizaklarniki bo'lsa ham — JSHSHIR ustun), aniq ism,
    /// egizak ism (ikki nomzod), noma'lum va allaqachon bog'langan (ismi boshqa xodimga aniq mos bo'lsa
    /// ham — bog'langan ustun). Oldindan ko'rish HECH NARSA yozmaydi.
    /// </summary>
    [Fact]
    public async Task Import_oldindan_korish_besh_holatni_ajratadi_va_hech_narsa_yozmaydi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var belgi = Belgi();

        var pinfl = "3150588" + Random.Shared.Next(0, 10_000_000).ToString("D7", CultureInfo.InvariantCulture);
        var pinflli = await XodimAsync(belgi + "p", new DateOnly(2025, 1, 1), pinfl);
        var ismli = await XodimAsync(belgi + "n", new DateOnly(2025, 1, 1));
        var boglangan = await XodimAsync(belgi + "m", new DateOnly(2025, 1, 1));
        var (egizakBir, egizakIkki, egizakIsmi) = await EgizaklarAsync(belgi);

        using (var moslik = await admin.PostAsJsonAsync(Yol("/identities"), new { externalPersonId = "i5-" + belgi, employeeId = boglangan.Id }, ct))
        {
            moslik.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(moslik));
        }

        byte[] fayl;
        using (var kitob = new XLWorkbook())
        {
            var varaq = kitob.AddWorksheet("Verifix");
            varaq.Cell(1, 1).Value = "Employee No";
            varaq.Cell(1, 2).Value = "ФИО";
            varaq.Cell(1, 3).Value = "PINFL";

            string?[][] qatorlar =
            [
                ["i1-" + belgi, egizakIsmi, pinfl],
                ["i2-" + belgi, ismli.Name, null],
                ["i3-" + belgi, egizakIsmi, null],
                ["i4-" + belgi, "Ҳеч Кимов " + belgi, null],
                ["i5-" + belgi, ismli.Name, null],
            ];

            for (var i = 0; i < qatorlar.Length; i++)
            {
                for (var j = 0; j < qatorlar[i].Length; j++)
                {
                    if (qatorlar[i][j] is { } qiymat)
                    {
                        varaq.Cell(i + 2, j + 1).Value = qiymat;
                    }
                }
            }

            using var oqim = new MemoryStream();
            kitob.SaveAs(oqim);
            fayl = oqim.ToArray();
        }

        var mosliklarOldin = await MosliklarSoniAsync(ct);

        using var preview = await Yuklash(admin, fayl, "verifix.xlsx", ct);
        preview.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(preview));

        var natija = await JsonAsync(preview);
        natija.GetProperty("totalRows").GetInt32().ShouldBe(5);

        var qator = natija.GetProperty("rows").EnumerateArray()
            .ToDictionary(item => item.GetProperty("rawPersonId").GetString()![..2]);
        qator.Count.ShouldBe(5);

        qator["i1"].GetProperty("matchKind").GetString().ShouldBe("ExactPinfl");
        qator["i1"].GetProperty("suggestedEmployeeId").GetGuid().ShouldBe(pinflli.Id);

        qator["i2"].GetProperty("matchKind").GetString().ShouldBe("ExactName");
        qator["i2"].GetProperty("suggestedEmployeeId").GetGuid().ShouldBe(ismli.Id);

        qator["i3"].GetProperty("matchKind").GetString().ShouldBe("Ambiguous");
        qator["i3"].GetProperty("suggestedEmployeeId").ValueKind.ShouldBe(JsonValueKind.Null);
        qator["i3"].GetProperty("candidates").EnumerateArray()
            .Select(nomzod => nomzod.GetProperty("employeeId").GetGuid())
            .ShouldBe([egizakBir, egizakIkki], ignoreOrder: true);

        qator["i4"].GetProperty("matchKind").GetString().ShouldBe("None");
        qator["i4"].GetProperty("suggestedEmployeeId").ValueKind.ShouldBe(JsonValueKind.Null);
        qator["i4"].GetProperty("candidates").GetArrayLength().ShouldBe(0);

        qator["i5"].GetProperty("matchKind").GetString().ShouldBe("AlreadyMapped");
        qator["i5"].GetProperty("suggestedEmployeeId").GetGuid().ShouldBe(boglangan.Id);
        qator["i5"].GetProperty("suggestedEmployeeName").GetString().ShouldBe(boglangan.Name);

        (await MosliklarSoniAsync(ct)).ShouldBe(mosliklarOldin, "oldindan ko'rish moslik yaratmasligi kerak");
    }

    /// <summary>
    /// Import chegarasi HTTP orqali (B6 ochiq bandi): aynan 5 000 qator — oldindan ko'rish o'tadi;
    /// 5 001 — 400 <c>davomat.import_qator_kop</c>; tasdiqda ham 5 001 qator — o'sha kod, birorta moslik
    /// yaratilmaydi (yarim import yo'q).
    /// </summary>
    [Fact]
    public async Task Import_5000_qator_otadi_5001_qator_import_qator_kop_va_hech_narsa_yaratilmaydi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var belgi = Belgi();

        byte[] Csv(int qatorlar)
            => Encoding.UTF8.GetBytes(
                "Employee No;Name\r\n" + string.Concat(Enumerable.Range(1, qatorlar)
                    .Select(i => string.Create(CultureInfo.InvariantCulture, $"q{i}-{belgi};Chegara Sinov {i}\r\n"))));

        using (var chegarada = await Yuklash(admin, Csv(AttendanceRules.MaxImportRows), "chegara.csv", ct))
        {
            chegarada.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await JsonAsync(chegarada)).GetProperty("totalRows").GetInt32().ShouldBe(AttendanceRules.MaxImportRows);
        }

        using (var oshgan = await Yuklash(admin, Csv(AttendanceRules.MaxImportRows + 1), "oshgan.csv", ct))
        {
            await XatoKutAsync(oshgan, HttpStatusCode.BadRequest, "davomat.import_qator_kop");
        }

        var xodim = await XodimAsync(belgi, new DateOnly(2025, 1, 1));
        var mosliklarOldin = await MosliklarSoniAsync(ct);

        using (var tasdiq = await admin.PostAsJsonAsync(
                   Yol("/identities/import/confirm"),
                   new
                   {
                       provider = "Verifix",
                       rows = Enumerable.Range(1, AttendanceRules.MaxImportRows + 1)
                           .Select(i => new { rawPersonId = string.Create(CultureInfo.InvariantCulture, $"q{i}-{belgi}"), employeeId = xodim.Id }),
                   },
                   ct))
        {
            await XatoKutAsync(tasdiq, HttpStatusCode.BadRequest, "davomat.import_qator_kop");
        }

        (await MosliklarSoniAsync(ct)).ShouldBe(mosliklarOldin);
    }

    // ════════════════════════════════════════════════════════════
    //  Bitta o'tish — GET /passes/{id} (§6.5, §11.1 #74b)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// #74b. Bog'langan o'tish: turniket surati (<c>photoKey</c>) va xodim kartasi rasmi
    /// (<c>employeePhotoKey</c>) ALOHIDA; bog'lanmagan o'tishda xodim, bo'lim va lavozim — null;
    /// noma'lum identifikator va saqlash muddati vazifasi o'chirgan o'tish — ikkalasi ham 404
    /// <c>davomat.otish_topilmadi</c>.
    /// </summary>
    [Fact]
    public async Task Bitta_otish_ikki_suratni_ajratadi_moslanmaganida_xodim_maydonlari_null_ochirilgani_404()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var belgi = Belgi();

        Guid boglanganId, moslanmaganId, eskiId, xodimId;
        string xodimIsmi, bolimNomi, lavozimNomi, qurilmaNomi = "74b darvoza " + belgi;

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

            var firma = LegalEntity.Create("Ўтиш МЧЖ " + belgi).Value;
            var bolim = Department.Create("Омбор " + belgi).Value;
            var lavozim = Position.Create("Омборчи " + belgi).Value;
            var xodim = Employee.Create(FullName.Create("Суратов", "Ходим", belgi).Value).Value;
            xodim.SetPhoto($"employees/74b-{belgi}.jpg");
            var munosabat = Employment.Hire(xodim.Id, firma.Id, new DateOnly(2025, 1, 1), bolim.Id, lavozim.Id).Value;
            var qurilma = AttendanceDevice.Register(qurilmaNomi, RandomNumberGenerator.GetBytes(32), "tst00000", DateTimeOffset.UtcNow).Value;

            var boglangan = Otish(qurilma.Id, "74b-a-" + belgi, Lahza(new DateOnly(2026, 7, 10), 8, 0), PassDirection.In, $"attendance/74b-{belgi}.jpg");
            boglangan.Resolve(xodim.Id, boglangan.ReceivedAt);
            var moslanmagan = Otish(qurilma.Id, "74b-u-" + belgi, Lahza(new DateOnly(2026, 7, 10), 9, 0), PassDirection.In);
            var eski = Otish(qurilma.Id, "74b-e-" + belgi, Lahza(new DateOnly(2020, 1, 10), 12, 0), PassDirection.In);

            kontekst.AddRange(firma, bolim, lavozim, xodim, qurilma, boglangan, moslanmagan, eski);
            kontekst.Add(munosabat);
            await kontekst.SaveChangesAsync(ct);

            (boglanganId, moslanmaganId, eskiId, xodimId) = (boglangan.Id, moslanmagan.Id, eski.Id, xodim.Id);
            (xodimIsmi, bolimNomi, lavozimNomi) = (xodim.FullName.Full, bolim.Name, lavozim.Name);
        }

        var bitta = await JsonAsync(await admin.GetAsync(Yol($"/passes/{boglanganId}"), ct));
        bitta.GetProperty("id").GetGuid().ShouldBe(boglanganId);
        bitta.GetProperty("photoKey").GetString().ShouldBe($"attendance/74b-{belgi}.jpg");
        bitta.GetProperty("employeePhotoKey").GetString().ShouldBe($"employees/74b-{belgi}.jpg");
        bitta.GetProperty("employeeId").GetGuid().ShouldBe(xodimId);
        bitta.GetProperty("employeeName").GetString().ShouldBe(xodimIsmi);
        bitta.GetProperty("department").GetString().ShouldBe(bolimNomi);
        bitta.GetProperty("position").GetString().ShouldBe(lavozimNomi);
        bitta.GetProperty("deviceName").GetString().ShouldBe(qurilmaNomi);

        var moslanmaganOtish = await JsonAsync(await admin.GetAsync(Yol($"/passes/{moslanmaganId}"), ct));
        moslanmaganOtish.GetProperty("rawPersonId").GetString().ShouldBe("74b-u-" + belgi);
        foreach (var maydon in new[] { "employeeId", "employeeName", "employeePhotoKey", "department", "position", "photoKey" })
        {
            moslanmaganOtish.GetProperty(maydon).ValueKind.ShouldBe(JsonValueKind.Null, maydon);
        }

        using (var yoq = await admin.GetAsync(Yol($"/passes/{Guid.NewGuid()}"), ct))
        {
            await XatoKutAsync(yoq, HttpStatusCode.NotFound, "davomat.otish_topilmadi");
        }

        using (var hali = await admin.GetAsync(Yol($"/passes/{eskiId}"), ct))
        {
            hali.StatusCode.ShouldBe(HttpStatusCode.OK, "tozalashdan OLDIN eski o'tish ochiladi — 404 aynan tozalashdan bo'lsin");
        }

        using (var muddat = await admin.PutAsJsonAsync(
                   new Uri("/api/settings", UriKind.Relative),
                   new
                   {
                       items = new[]
                       {
                           new { key = SystemSettingKeys.PhotoRetentionDays, value = "60" },
                           new { key = SystemSettingKeys.PassRetentionDays, value = "365" },
                       },
                   },
                   ct))
        {
            muddat.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(muddat));
        }

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            // Soat qotirilgan: bugun 2026-09-16 ⇒ chegara 2025-09-16. 2020-yilgi o'tish o'chadi,
            // 2026-07-10 dagilari qoladi — test yili qanday bo'lmasin.
            await ActivatorUtilities.CreateInstance<AttendancePassPurgeJob>(
                    qamrov.ServiceProvider,
                    new QotirilganSoat(new DateTimeOffset(2026, 9, 16, 7, 0, 0, TimeSpan.Zero)))
                .RunAsync(ct);
        }

        using (var tozalangan = await admin.GetAsync(Yol($"/passes/{eskiId}"), ct))
        {
            await XatoKutAsync(tozalangan, HttpStatusCode.NotFound, "davomat.otish_topilmadi");
        }

        using (var qolgan = await admin.GetAsync(Yol($"/passes/{boglanganId}"), ct))
        {
            qolgan.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qolgan));
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Regressiya qo'riqchilari (§11.1 #81)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// #81. OpenAPI hujjati yig'iladi va davomatning HAR BIR yo'li unda bor — so'rov yozuvidagi
    /// <c>Guid = default</c> tuzog'i (§0.2 #8) butun hujjatni yiqitardi va <c>./bonvi gen</c> to'xtardi.
    /// </summary>
    /// <remarks>
    /// <c>/openapi/v1.json</c> marshruti faqat Development'da ulanadi (Testing'da Hangfire serveri ham
    /// ko'tarilardi). Shu sababli hujjat marshrut chaqiradigan generatorning O'ZIDAN —
    /// <see cref="IOpenApiDocumentProvider"/> — yig'iladi: 500 ni aynan shu joy berardi.
    /// </remarks>
    [Fact]
    public async Task OpenAPI_hujjati_yigiladi_va_davomatning_har_bir_yolini_oz_ichiga_oladi()
    {
        var ct = TestContext.Current.CancellationToken;

        var hujjat = await _factory.Services
            .GetRequiredKeyedService<IOpenApiDocumentProvider>("v1")
            .GetOpenApiDocumentAsync(ct);

        var yollar = hujjat.Paths.Keys.ToHashSet(StringComparer.Ordinal);

        string[] kutilgan =
        [
            "/api/attendance/ingest/{token}",
            "/api/attendance/devices", "/api/attendance/devices/{id}", "/api/attendance/devices/{id}/rotate-token",
            "/api/attendance/devices/{id}/activate", "/api/attendance/devices/{id}/deactivate",
            "/api/attendance/identities", "/api/attendance/identities/{id}", "/api/attendance/identities/unmatched",
            "/api/attendance/identities/import", "/api/attendance/identities/import/confirm",
            "/api/attendance/schedules", "/api/attendance/schedules/resolve", "/api/attendance/schedules/{id}",
            "/api/attendance/schedules/{id}/activate", "/api/attendance/schedules/{id}/deactivate",
            "/api/attendance/schedule-assignments", "/api/attendance/schedule-assignments/{id}",
            "/api/attendance/passes", "/api/attendance/passes/{id}", "/api/attendance/passes/{id}/photo",
            "/api/attendance/board", "/api/attendance/dashboard", "/api/attendance/former-employee-alerts",
            "/api/attendance/timesheet",
            "/api/attendance/reports/exceptions", "/api/attendance/reports/summary",
            "/api/attendance/export/timesheet", "/api/attendance/export/passes",
            "/api/attendance/export/exceptions", "/api/attendance/export/summary",
            "/api/attendance/recompute",
        ];

        kutilgan.Where(yol => !yollar.Contains(yol)).ShouldBeEmpty(string.Join(", ", yollar.Order(StringComparer.Ordinal)));
    }

    private static readonly Guid Birorta = Guid.Parse("0199a1b2-0000-7000-8000-0000000000b6");

    /// <summary><c>attendance.manage</c> ostidagi HAMMA endpoint (SPEC §7.3).</summary>
    private static readonly (HttpMethod Usul, string Yol)[] BoshqaruvEndpointlari =
    [
        (HttpMethod.Get, "/devices"),
        (HttpMethod.Get, $"/devices/{Birorta}"),
        (HttpMethod.Post, "/devices"),
        (HttpMethod.Put, $"/devices/{Birorta}"),
        (HttpMethod.Post, $"/devices/{Birorta}/rotate-token"),
        (HttpMethod.Post, $"/devices/{Birorta}/activate"),
        (HttpMethod.Post, $"/devices/{Birorta}/deactivate"),
        (HttpMethod.Delete, $"/devices/{Birorta}"),
        (HttpMethod.Get, "/identities"),
        (HttpMethod.Post, "/identities"),
        (HttpMethod.Delete, $"/identities/{Birorta}"),
        (HttpMethod.Get, "/identities/unmatched"),
        (HttpMethod.Post, "/identities/import"),
        (HttpMethod.Post, "/identities/import/confirm"),
        (HttpMethod.Post, "/schedules"),
        (HttpMethod.Put, $"/schedules/{Birorta}"),
        (HttpMethod.Delete, $"/schedules/{Birorta}"),
        (HttpMethod.Post, $"/schedules/{Birorta}/activate"),
        (HttpMethod.Post, $"/schedules/{Birorta}/deactivate"),
        (HttpMethod.Post, "/schedule-assignments"),
        (HttpMethod.Delete, $"/schedule-assignments/{Birorta}"),
        (HttpMethod.Post, "/recompute"),
    ];

    /// <summary><c>attendance.view</c> + <c>reports.export</c> ostidagi eksportlar.</summary>
    private static readonly (HttpMethod Usul, string Yol)[] EksportEndpointlari =
    [
        (HttpMethod.Get, "/export/timesheet?year=2026&month=9"),
        (HttpMethod.Get, "/export/passes"),
        (HttpMethod.Get, "/export/exceptions?kind=Late"),
        (HttpMethod.Get, "/export/summary"),
    ];

    /// <summary><c>attendance.view</c> ostidagi o'qish endpointlari.</summary>
    private static readonly (HttpMethod Usul, string Yol)[] KorishEndpointlari =
    [
        (HttpMethod.Get, "/schedules"),
        (HttpMethod.Get, $"/schedules/{Birorta}"),
        (HttpMethod.Get, $"/schedules/resolve?employeeId={Birorta}&date=2026-09-01"),
        (HttpMethod.Get, "/schedule-assignments"),
        (HttpMethod.Get, "/passes"),
        (HttpMethod.Get, $"/passes/{Birorta}"),
        (HttpMethod.Get, $"/passes/{Birorta}/photo"),
        (HttpMethod.Get, "/board"),
        (HttpMethod.Get, "/dashboard"),
        (HttpMethod.Get, "/former-employee-alerts"),
        (HttpMethod.Get, "/timesheet?year=2026&month=9"),
        (HttpMethod.Get, "/reports/exceptions?kind=Late"),
        (HttpMethod.Get, "/reports/summary"),
    ];

    // ────────────────────────────────────────────────────────────
    //  Muhit tayyorlash
    // ────────────────────────────────────────────────────────────

    private sealed record Xodim(Guid Id, string Name);

    private sealed record KunQatori(AttendanceDayStatus Status, int WorkedMinutes);

    /// <summary>Qotirilgan soat — tozalash vazifasining «bugun»i test yiliga bog'liq bo'lmasin.</summary>
    private sealed class QotirilganSoat(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;

        public DateOnly Today => utcNow.TashkentDate();
    }

    /// <summary>Qabul qilingan o'tish — qabul lahzasi UTC da (timestamptz faqat UTC ni yozadi), og'ishsiz.</summary>
    private static AttendancePass Otish(Guid qurilmaId, string shaxs, DateTimeOffset lahza, PassDirection yonalish, string? suratKaliti = null)
        => AttendancePass.Record(
            qurilmaId,
            AttendanceIdentityProvider.Verifix,
            shaxs,
            lahza,
            lahza.ToUniversalTime().AddSeconds(3),
            Guid.NewGuid().ToString("N"),
            yonalish,
            DirectionSource.Device,
            PassMethod.Face,
            photoKey: suratKaliti).Value;

    /// <summary>Yangi qurilma va uning MOSLANMAGAN o'tishlari — xuddi terminal yuborgandek, qotirilgan sanalarda.</summary>
    private async Task<List<Guid>> MoslanmaganOtishlarAsync(string shaxs, params (DateTimeOffset Lahza, PassDirection Yonalish)[] otishlar)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var qurilma = AttendanceDevice.Register(
            "Moslik turniketi " + Belgi(),
            RandomNumberGenerator.GetBytes(32),
            "tst00000",
            DateTimeOffset.UtcNow).Value;

        var yozuvlar = otishlar.Select(otish => Otish(qurilma.Id, shaxs, otish.Lahza, otish.Yonalish)).ToList();

        kontekst.Add(qurilma);
        kontekst.AddRange(yozuvlar);
        await kontekst.SaveChangesAsync(TestContext.Current.CancellationToken);

        return [.. yozuvlar.Select(otish => otish.Id)];
    }

    private async Task<List<Guid?>> OtishEgalariAsync(List<Guid> otishlar, CancellationToken ct)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();

        return await qamrov.ServiceProvider.GetRequiredService<AppDbContext>().AttendancePasses.AsNoTracking()
            .Where(otish => otishlar.Contains(otish.Id))
            .Select(otish => otish.ResolvedEmployeeId)
            .ToListAsync(ct);
    }

    /// <summary>F.I.Sh. aynan bir xil ikki xodim — har biri o'z firmasida.</summary>
    private async Task<(Guid Bir, Guid Ikki, string Ism)> EgizaklarAsync(string belgi)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var bir = Employee.Create(FullName.Create("Эгизаков", "Ходим", belgi).Value).Value;
        var ikki = Employee.Create(FullName.Create("Эгизаков", "Ходим", belgi).Value).Value;
        var birFirma = LegalEntity.Create("Эгизак-1 МЧЖ " + belgi).Value;
        var ikkiFirma = LegalEntity.Create("Эгизак-2 МЧЖ " + belgi).Value;

        kontekst.AddRange(bir, ikki, birFirma, ikkiFirma);
        kontekst.AddRange(
            Employment.Hire(bir.Id, birFirma.Id, new DateOnly(2025, 1, 1)).Value,
            Employment.Hire(ikki.Id, ikkiFirma.Id, new DateOnly(2025, 1, 1)).Value);
        await kontekst.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (bir.Id, ikki.Id, bir.FullName.Full);
    }

    /// <summary>Barcha moslik yozuvlari — yumshoq o'chirilganlari bilan.</summary>
    private async Task<int> MosliklarSoniAsync(CancellationToken ct)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();

        return await qamrov.ServiceProvider.GetRequiredService<AppDbContext>().AttendanceIdentities
            .IgnoreQueryFilters()
            .CountAsync(ct);
    }

    /// <summary>Ikkala saqlash muddati sozlamasining bazadagi qiymati (<c>null</c> — yozuv yo'q, sukut).</summary>
    private async Task<(string? Surat, string? Otish)> SozlamaQiymatlariAsync(CancellationToken ct)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var sozlamalar = await qamrov.ServiceProvider.GetRequiredService<AppDbContext>().SystemSettings.AsNoTracking()
            .Where(setting => setting.Key == SystemSettingKeys.PhotoRetentionDays || setting.Key == SystemSettingKeys.PassRetentionDays)
            .ToDictionaryAsync(setting => setting.Key, setting => setting.Value, ct);

        return (sozlamalar.GetValueOrDefault(SystemSettingKeys.PhotoRetentionDays), sozlamalar.GetValueOrDefault(SystemSettingKeys.PassRetentionDays));
    }

    private static Uri Yol(string yol) => new("/api/attendance" + yol, UriKind.Relative);

    private static string Belgi() => Guid.NewGuid().ToString("N")[..8];

    private static string Iso(DateOnly sana) => sana.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string IsoLahza(DateTimeOffset lahza) => lahza.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);

    private static DateOnly BugunToshkentda() => DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(Tashkent).DateTime);

    private static DateTimeOffset Lahza(DateOnly sana, int soat, int daqiqa)
        => new(sana.ToDateTime(new TimeOnly(soat, daqiqa)), Tashkent);

    private async Task<Xodim> XodimAsync(string belgi, DateOnly qabul, string? pinfl = null)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var firma = LegalEntity.Create("Давомат МЧЖ " + belgi).Value;
        var xodim = Employee.Create(FullName.Create("Қодиров", "Ходим", belgi).Value).Value;

        if (pinfl is not null)
        {
            xodim.SetPinfl(Pinfl.FromTrusted(pinfl)).IsSuccess.ShouldBeTrue();
        }

        var munosabat = Employment.Hire(xodim.Id, firma.Id, qabul).Value;

        kontekst.AddRange(firma, xodim);
        kontekst.Add(munosabat);
        await kontekst.SaveChangesAsync(TestContext.Current.CancellationToken);

        return new Xodim(xodim.Id, xodim.FullName.Full);
    }

    private static object JadvalTanasi(string nom, int? dayOff = null) => new
    {
        name = nom,
        graceInMinutes = 10,
        graceOutMinutes = 10,
        note = (string?)null,
        days = Enumerable.Range(1, 7).Select(kun => kun == dayOff
            ? (object)new { dayIndex = kun, isWorking = false }
            : new { dayIndex = kun, isWorking = true, startTime = "09:00", endTime = "18:00", breakMinutes = 0 }),
    };

    private static async Task<Guid> JadvalYaratAsync(HttpClient admin, string nom)
    {
        using var javob = await admin.PostAsJsonAsync(Yol("/schedules"), JadvalTanasi(nom), TestContext.Current.CancellationToken);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var jadval = await JsonAsync(javob);
        jadval.GetProperty("days").GetArrayLength().ShouldBe(7);

        return jadval.GetProperty("id").GetGuid();
    }

    /// <summary>Har kuni 09:00–18:00 jadval va uni XODIM pog'onasida biriktirish.</summary>
    private static async Task<Guid> XodimgaJadvalAsync(HttpClient admin, Guid xodimId, string belgi)
    {
        var jadvalId = await JadvalYaratAsync(admin, "Jadval " + belgi);

        using var javob = await admin.PostAsJsonAsync(
            Yol("/schedule-assignments"),
            new { workScheduleId = jadvalId, scope = "Employee", employeeId = xodimId, effectiveFrom = "2025-01-01" },
            TestContext.Current.CancellationToken);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        return jadvalId;
    }

    private async Task<KunQatori?> KunAsync(Guid xodimId, DateOnly sana, CancellationToken ct)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        return await kontekst.AttendanceDays.AsNoTracking()
            .Where(day => day.EmployeeId == xodimId && day.LocalDate == sana)
            .Select(day => new KunQatori(day.Status, day.WorkedMinutes))
            .SingleOrDefaultAsync(ct);
    }

    private static HttpRequestMessage Hodisalar(
        string token,
        params (string Shaxs, DateTimeOffset Lahza, string Yonalish)[] hodisalar)
    {
        var tana = JsonSerializer.Serialize(new
        {
            events = hodisalar.Select((hodisa, i) => new
            {
                personId = hodisa.Shaxs,
                occurredAt = IsoLahza(hodisa.Lahza),
                direction = hodisa.Yonalish,
                eventId = $"{hodisa.Shaxs}-{hodisa.Lahza.ToUnixTimeSeconds()}-{i}",
            }),
        });

        return JsonSorov(token, tana);
    }

    private static HttpRequestMessage JsonSorov(string token, string tana)
    {
        var sorov = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/attendance/ingest", UriKind.Relative))
        {
            Content = new StringContent(tana, Encoding.UTF8, "application/json"),
        };
        sorov.Headers.Add("X-Bonvi-Device-Token", token);

        return sorov;
    }

    private static async Task<HttpResponseMessage> Yuklash(HttpClient mijoz, byte[] fayl, string nomi, CancellationToken ct)
    {
        using var forma = new MultipartFormDataContent();
        var qism = new ByteArrayContent(fayl);
        qism.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        forma.Add(qism, "file", nomi);

        return await mijoz.PostAsync(Yol("/identities/import"), forma, ct);
    }

    /// <summary>RBAC so'rovi — tanasi minimal: avtorizatsiya tanani o'qishdan OLDIN hal qiladi.</summary>
    private static async Task<HttpResponseMessage> YuborAsync(HttpClient mijoz, HttpMethod usul, string yol, CancellationToken ct)
    {
        using var sorov = new HttpRequestMessage(usul, Yol(yol));

        if (yol == "/identities/import")
        {
            var forma = new MultipartFormDataContent { { new ByteArrayContent("x"u8.ToArray()), "file", "f.csv" } };
            sorov.Content = forma;
        }
        else if (usul == HttpMethod.Post || usul == HttpMethod.Put)
        {
            sorov.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        return await mijoz.SendAsync(sorov, ct);
    }

    private async Task<string> TokenAsync(params string[] ruxsatlar)
    {
        using var scope = _factory.Services.CreateScope();

        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var login = PhoneNumber.Create(BonviApiFactory.TestAdminLogin).Value;
        var user = await users.GetByLoginAsync(login, TestContext.Current.CancellationToken);
        user.ShouldNotBeNull();

        return tokens.CreateTokens(user, [], ruxsatlar.ToHashSet(StringComparer.Ordinal)).AccessToken;
    }

    /// <summary>Berilgan hisob nomidan, berilgan ruxsatlar bilan token — boshqa tezlik bo'linmasi kerak bo'lganda.</summary>
    private async Task<string> HisobTokeniAsync(string login, params string[] ruxsatlar)
    {
        using var scope = _factory.Services.CreateScope();

        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var user = await users.GetByLoginAsync(PhoneNumber.Create(login).Value, TestContext.Current.CancellationToken);
        user.ShouldNotBeNull();

        return tokens.CreateTokens(user, [], ruxsatlar.ToHashSet(StringComparer.Ordinal)).AccessToken;
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
