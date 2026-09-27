using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Xodim ilovasi (PWA) — uchi-uchiga (SPEC-xodim-ilovasi §I).
/// </summary>
/// <remarks>
/// <para>
/// Bu yerda tekshirilayotgan narsalarning aksariyati faqat HAQIQIY bazada
/// ko'rinadi: auditoriya bo'yicha ajratish, unikal indekslar, geo-to'siq
/// hisobi va telefon belgisining tabelga tushishi.
/// </para>
/// <para>
/// ⚠️ Sinf ichidagi testlar bitta bazani bo'lishadi. Shu sababli har bir
/// test O'Z xodimi, O'Z obyekti va o'z hisobi bilan ishlaydi — obyektlar
/// esa global ro'yxat, ya'ni koordinatalar ATAYLAB bir-biridan uzoq
/// nuqtalarga tarqatilgan (<see cref="Nuqta"/>): aks holda bir testning
/// obyekti ikkinchisining belgisiga «eng yaqin» bo'lib chiqardi.
/// </para>
/// </remarks>
public sealed partial class EmployeeAppApiTests : IClassFixture<BonviApiFactory>
{
    private static readonly TimeSpan Tashkent = TimeSpan.FromHours(5);

    private readonly BonviApiFactory _factory;

    public EmployeeAppApiTests(BonviApiFactory factory) => _factory = factory;

    // ════════════════════════════════════════════════════════════
    //  Geo-to'siq: ichkarida, tashqarida, faolsiz obyekt
    // ════════════════════════════════════════════════════════════

    /// <summary>To'siq ichidagi belgi qabul qilinadi va o'tish yoziladi.</summary>
    [Fact]
    public async Task Obyekt_hududidagi_belgi_qabul_qilinadi()
    {
        var ct = TestContext.Current.CancellationToken;
        var belgi = Belgi();
        using var admin = await AdminMijozAsync();

        var xodim = await XodimAsync(belgi);
        await BelgilashniYoqAsync(admin, xodim.Id);
        var markaz = Nuqta(1);
        await ObyektAsync(admin, "Ombor " + belgi, markaz, radius: 200);

        using var ilova = await IlovaMijoziAsync(admin, xodim.Id);

        // Markazdan ~90 metr shimolda: to'siq ichida.
        using var javob = await BelgilaAsync(ilova, "In", Siljit(markaz, metr: 90));
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var natija = await JsonAsync(javob);
        natija.GetProperty("direction").GetString().ShouldBe("In");
        natija.GetProperty("workSiteName").GetString().ShouldBe("Ombor " + belgi);

        // ⭐ Vaqt SERVERNIKI: mijoz uni umuman yubormaydi.
        var qabulLahza = natija.GetProperty("occurredAt").GetDateTimeOffset();
        (DateTimeOffset.UtcNow - qabulLahza).TotalMinutes.ShouldBeLessThan(5);

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var otish = await kontekst.AttendancePasses.AsNoTracking()
            .SingleAsync(pass => pass.ResolvedEmployeeId == xodim.Id, ct);

        otish.DeviceId.ShouldBe(AttendanceDevice.MobileAppId);
        otish.Method.ShouldBe(PassMethod.Mobile);
        otish.DirectionSource.ShouldBe(DirectionSource.Employee);
        otish.IdentityProvider.ShouldBe(AttendanceIdentityProvider.Mobile);
        otish.RawPersonId.ShouldBe(xodim.Id.ToString());

        /*
          ⭐ TOSHKENT SILJISHI (ma'lumot modeli §D.7) — eng oson
          o'tkazib yuboriladigan xato. `AttendancePass.Record` bitta
          argumentdan beshta ustun yasaydi; UTC berilsa, jurnal har bir
          telefon belgisini besh soat oldin ko'rsatardi va kechqurungi
          belgi tabelda BOSHQA KUNGA tushardi.
        */
        otish.DeviceOffsetMinutes.ShouldBe((short)300);
        otish.OffsetAssumed.ShouldBeFalse();
        otish.LocalDate.ShouldBe(DateOnly.FromDateTime(otish.OccurredAt.ToOffset(Tashkent).DateTime));
        otish.ClockSkewSeconds.ShouldBe(0);

        var koordinata = await kontekst.MobileCheckIns.AsNoTracking()
            .SingleAsync(row => row.PassId == otish.Id, ct);

        koordinata.EmployeeId.ShouldBe(xodim.Id, "INV-3: da'vo va moslik bir xil bo'lishi kerak");
        koordinata.SiteRadiusMeters.ShouldBe(200, "qaror lahzasidagi radius muzlatiladi");
        ((double)koordinata.DistanceMeters).ShouldBeInRange(80, 100);
        koordinata.SiteShape.ShouldBe(WorkSiteShape.Circle, "doirada dalil — markazgacha masofa");
    }

    /// <summary>To'siqdan tashqaridagi belgi rad etiladi.</summary>
    [Fact]
    public async Task Obyekt_hududidan_tashqaridagi_belgi_rad_etiladi()
    {
        var belgi = Belgi();
        using var admin = await AdminMijozAsync();

        var xodim = await XodimAsync(belgi);
        await BelgilashniYoqAsync(admin, xodim.Id);
        var markaz = Nuqta(2);
        await ObyektAsync(admin, "Filial " + belgi, markaz, radius: 200);

        // Markazdan ~500 metr: 200 metrlik to'siqdan tashqarida.
        using var javob = await BelgilaAsync(
            await IlovaMijoziAsync(admin, xodim.Id),
            "In",
            Siljit(markaz, metr: 500));

        await XatoKutAsync(javob, HttpStatusCode.BadRequest, "davomat.obyektdan_uzoq");
        await OtishYoqAsync(xodim.Id);
    }

    /// <summary>Faolsizlantirilgan obyekt belgini qabul qilmaydi.</summary>
    /// <remarks>
    /// To'siqning ICHIDA turgan odam ham rad etiladi — «faolsiz» aynan
    /// shuni bildiradi. Tarix esa joyida qoladi, obyekt o'chirilmaydi.
    /// </remarks>
    [Fact]
    public async Task Faolsiz_obyektda_belgi_qabul_qilinmaydi()
    {
        var ct = TestContext.Current.CancellationToken;
        var belgi = Belgi();
        using var admin = await AdminMijozAsync();

        var xodim = await XodimAsync(belgi);
        await BelgilashniYoqAsync(admin, xodim.Id);
        var markaz = Nuqta(3);
        var obyektId = await ObyektAsync(admin, "Mavsumiy " + belgi, markaz, radius: 500);

        using (var ochirish = await admin.PostAsync(Yol($"/{obyektId}/deactivate"), null, ct))
        {
            ochirish.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(ochirish));
        }

        using var javob = await BelgilaAsync(
            await IlovaMijoziAsync(admin, xodim.Id),
            "In",
            markaz);

        await XatoKutAsync(javob, HttpStatusCode.BadRequest, "davomat.obyektdan_uzoq");
        await OtishYoqAsync(xodim.Id);
    }

    /// <summary>
    /// Rad etish xabaridagi masofa — CHEGARAGACHA, markazgacha emas (SPEC-joylashuvlar §B).
    /// </summary>
    /// <remarks>
    /// Doira qarori o'zgarmagan (yuqoridagi test), o'zgargani faqat odamga
    /// aytiladigan son: 500 m markazgacha, 200 m radius → «300 m».
    /// </remarks>
    [Fact]
    public async Task Doiradan_tashqarida_rad_etishda_chegaragacha_masofa_aytiladi()
    {
        var belgi = Belgi();
        using var admin = await AdminMijozAsync();

        var xodim = await XodimAsync(belgi);
        await BelgilashniYoqAsync(admin, xodim.Id);
        var markaz = Nuqta(8);
        await ObyektAsync(admin, "Chegara " + belgi, markaz, radius: 200);

        using var javob = await BelgilaAsync(
            await IlovaMijoziAsync(admin, xodim.Id),
            "In",
            Siljit(markaz, metr: 500));

        await XatoKutAsync(javob, HttpStatusCode.BadRequest, "davomat.obyektdan_uzoq");
        (await AytilganMasofaAsync(javob)).ShouldBeInRange(295, 305);
    }

    // ════════════════════════════════════════════════════════════
    //  Geo-to'siq: ko'pburchak (SPEC-joylashuvlar §B)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Ko'pburchak ichidagi belgi qabul qilinadi; dalil qatori — shakl, chetgacha masofa (0) va zaxira.
    /// </summary>
    [Fact]
    public async Task Kopburchak_ichidagi_belgi_qabul_qilinadi_va_dalil_muzlatiladi()
    {
        var ct = TestContext.Current.CancellationToken;
        var belgi = Belgi();
        using var admin = await AdminMijozAsync();

        var xodim = await XodimAsync(belgi);
        await BelgilashniYoqAsync(admin, xodim.Id);
        var markaz = Nuqta(9);
        var obyektId = await KopburchakAsync(admin, "Sex " + belgi, markaz, zaxira: 40);

        using var javob = await BelgilaAsync(await IlovaMijoziAsync(admin, xodim.Id), "In", markaz);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        (await JsonAsync(javob)).GetProperty("workSiteName").GetString().ShouldBe("Sex " + belgi);

        var koordinata = await KoordinataAsync(xodim.Id, ct);

        koordinata.WorkSiteId.ShouldBe(obyektId);
        koordinata.SiteShape.ShouldBe(WorkSiteShape.Polygon);
        koordinata.DistanceMeters.ShouldBe(0m, "ichkarida chetgacha masofa 0");
        koordinata.SiteRadiusMeters.ShouldBe(40, "ko'pburchakda muzlatiladigan ruxsat — zaxira");
    }

    /// <summary>Chetdan tashqarida, lekin zaxira ichida — qabul; dalil masofasi ~30 m.</summary>
    [Fact]
    public async Task Kopburchakdan_tashqarida_zaxira_ichidagi_belgi_qabul_qilinadi()
    {
        var ct = TestContext.Current.CancellationToken;
        var belgi = Belgi();
        using var admin = await AdminMijozAsync();

        var xodim = await XodimAsync(belgi);
        await BelgilashniYoqAsync(admin, xodim.Id);
        var markaz = Nuqta(10);
        await KopburchakAsync(admin, "Ombor-zaxira " + belgi, markaz, zaxira: 50);

        using var javob = await BelgilaAsync(
            await IlovaMijoziAsync(admin, xodim.Id),
            "In",
            Siljit(ShimoliyQirra(markaz), metr: 30));

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var koordinata = await KoordinataAsync(xodim.Id, ct);
        koordinata.SiteShape.ShouldBe(WorkSiteShape.Polygon);
        ((double)koordinata.DistanceMeters).ShouldBeInRange(29, 31);
        koordinata.SiteRadiusMeters.ShouldBe(50);
    }

    /// <summary>Zaxiradan tashqarida — rad etiladi va xabarda CHETGACHA masofa (~120 m) turadi.</summary>
    [Fact]
    public async Task Kopburchak_zaxirasidan_tashqaridagi_belgi_rad_etiladi_va_chetgacha_masofa_aytiladi()
    {
        var belgi = Belgi();
        using var admin = await AdminMijozAsync();

        var xodim = await XodimAsync(belgi);
        await BelgilashniYoqAsync(admin, xodim.Id);
        var markaz = Nuqta(11);
        await KopburchakAsync(admin, "Uzoq-sex " + belgi, markaz, zaxira: 50);

        using var javob = await BelgilaAsync(
            await IlovaMijoziAsync(admin, xodim.Id),
            "In",
            Siljit(ShimoliyQirra(markaz), metr: 120));

        await XatoKutAsync(javob, HttpStatusCode.BadRequest, "davomat.obyektdan_uzoq");
        (await AytilganMasofaAsync(javob)).ShouldBeInRange(115, 125, "markazgacha (~231 m) emas");
        await OtishYoqAsync(xodim.Id);
    }

    // ════════════════════════════════════════════════════════════
    //  Aniqlik, takror va shaxsiy ruxsat
    // ════════════════════════════════════════════════════════════

    /// <summary>Chegaradan yomon aniqlik rad etiladi — obyektning ustida turgan bo'lsa ham.</summary>
    [Fact]
    public async Task Aniqligi_yomon_joylashuv_rad_etiladi()
    {
        var belgi = Belgi();
        using var admin = await AdminMijozAsync();

        var xodim = await XodimAsync(belgi);
        await BelgilashniYoqAsync(admin, xodim.Id);
        var markaz = Nuqta(4);
        await ObyektAsync(admin, "Aniqlik " + belgi, markaz, radius: 500);

        using var javob = await BelgilaAsync(
            await IlovaMijoziAsync(admin, xodim.Id),
            "In",
            markaz,
            aniqlik: 350);

        await XatoKutAsync(javob, HttpStatusCode.BadRequest, "davomat.joylashuv_aniq_emas");
        await OtishYoqAsync(xodim.Id);
    }

    /// <summary>
    /// 60 soniya ichidagi ikkinchi belgi rad etiladi (<c>AttendanceRules.BounceSeconds</c>).
    /// </summary>
    /// <remarks>
    /// Birinchisi qabul qilingan bo'lishi SHART: aks holda test takror
    /// qoidasini emas, boshqa rad etishni o'lchab qolardi.
    /// </remarks>
    [Fact]
    public async Task Oltmish_soniya_ichidagi_ikkinchi_belgi_rad_etiladi()
    {
        var ct = TestContext.Current.CancellationToken;
        var belgi = Belgi();
        using var admin = await AdminMijozAsync();

        var xodim = await XodimAsync(belgi);
        await BelgilashniYoqAsync(admin, xodim.Id);
        var markaz = Nuqta(5);
        await ObyektAsync(admin, "Takror " + belgi, markaz, radius: 300);

        using var ilova = await IlovaMijoziAsync(admin, xodim.Id);

        using (var birinchi = await BelgilaAsync(ilova, "In", markaz))
        {
            birinchi.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(birinchi));
        }

        // Yo'nalish BOSHQA — takror qoidasi baribir to'sadi: savol «yaqinda
        // belgiladimi», «shu tomonga belgiladimi» emas.
        using var ikkinchi = await BelgilaAsync(ilova, "Out", markaz);
        await XatoKutAsync(ikkinchi, HttpStatusCode.Conflict, "davomat.takroriy_belgi");

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        (await kontekst.AttendancePasses.AsNoTracking()
            .CountAsync(pass => pass.ResolvedEmployeeId == xodim.Id, ct))
            .ShouldBe(1);
    }

    /// <summary>Ruxsati yoqilmagan xodim belgilay olmaydi — 403.</summary>
    /// <remarks>
    /// Sukut bo'yicha bayroq HAMMADA o'chiq, ya'ni bu test ayni sukut
    /// holatni tekshiradi: hisobi bor, obyektning ustida turibdi, lekin
    /// HR ruxsat bermagan.
    /// </remarks>
    [Fact]
    public async Task Ruxsatsiz_xodim_belgilay_olmaydi()
    {
        var belgi = Belgi();
        using var admin = await AdminMijozAsync();

        var xodim = await XodimAsync(belgi);
        var markaz = Nuqta(6);
        await ObyektAsync(admin, "Ruxsat " + belgi, markaz, radius: 500);

        using var ilova = await IlovaMijoziAsync(admin, xodim.Id);

        using (var rad = await BelgilaAsync(ilova, "In", markaz))
        {
            await XatoKutAsync(rad, HttpStatusCode.Forbidden, "davomat.mobil_ruxsat_yoq");
        }

        await OtishYoqAsync(xodim.Id);

        // HR yoqadi — endi o'sha so'rov o'tadi. Bayroqdan boshqa hech
        // narsa o'zgarmadi, ya'ni rad etishning sababi aynan u edi.
        await BelgilashniYoqAsync(admin, xodim.Id);

        using var qabul = await BelgilaAsync(ilova, "In", markaz);
        qabul.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qabul));
    }

    // ════════════════════════════════════════════════════════════
    //  Auditoriya bo'yicha ajratish — IKKI TOMONGA
    // ════════════════════════════════════════════════════════════

    /// <summary>Xodim tokeni HR endpointiga o'tmaydi.</summary>
    /// <remarks>
    /// 401, 403 emas: token HR sxemasida UMUMAN qabul qilinmaydi
    /// (auditoriya boshqa), ya'ni ruxsat tekshiruvigacha yetib bormaydi.
    /// «Deyarli ruxsatli» degan holat mavjud emas.
    /// </remarks>
    [Fact]
    public async Task Xodim_tokeni_HR_endpointiga_otmaydi()
    {
        var ct = TestContext.Current.CancellationToken;
        var belgi = Belgi();
        using var admin = await AdminMijozAsync();
        var xodim = await XodimAsync(belgi);

        using var ilova = await IlovaMijoziAsync(admin, xodim.Id);

        foreach (var yol in HrYollari)
        {
            using var javob = await ilova.GetAsync(new Uri(yol, UriKind.Relative), ct);

            javob.StatusCode.ShouldBe(
                HttpStatusCode.Unauthorized,
                $"{yol} — xodim tokeni HR endpointida qabul qilinmasligi kerak");
        }
    }

    /// <summary>HR tokeni — hatto administratorniki ham — <c>/api/me/*</c> ga o'tmaydi.</summary>
    /// <remarks>
    /// Teskari tomon va u bir xil darajada muhim: ruxsat da'vosi bilan
    /// qo'riqlanmagan endpointga eng keng huquqli token ham kirmasligi
    /// kerak, aks holda «kim bu so'rovni yubordi» degan savol javobsiz
    /// qolardi (xodim identifikatori tokendan olinadi).
    /// </remarks>
    [Fact]
    public async Task HR_tokeni_api_me_ga_otmaydi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();

        foreach (var yol in XodimYollari)
        {
            using var javob = await admin.GetAsync(new Uri(yol, UriKind.Relative), ct);

            javob.StatusCode.ShouldBe(
                HttpStatusCode.Unauthorized,
                $"{yol} — HR tokeni xodim endpointida qabul qilinmasligi kerak");
        }
    }

    // ════════════════════════════════════════════════════════════
    //  INV-2: ishdan ketgan xodim kira olmaydi
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Ishdan ketgan xodim ilovaga KIRA OLMAYDI — INV-2.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hisob LOGIN bo'yicha topiladi, xodim orqali emas, ya'ni EF ning
    /// yumshoq o'chirish filtri bu yo'lga tushmaydi va hech qanday baza
    /// cheklovi bu qoidani ifodalay olmaydi. Uni faqat kirish
    /// ishlovchisidagi tekshiruv va SHU test qo'riqlaydi.
    /// </para>
    /// <para>
    /// Javob oddiy «noto'g'ri ma'lumot» bo'lishi ham tekshiriladi:
    /// ajratilgan javob endpointni «bu raqam Bonvi xodimimi?» degan
    /// savolga javob beradigan xizmatga aylantirardi.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Ishdan_ketgan_xodim_ilovaga_kira_olmaydi()
    {
        var ct = TestContext.Current.CancellationToken;
        var belgi = Belgi();
        using var admin = await AdminMijozAsync();

        var xodim = await XodimAsync(belgi);
        var parol = await HisobOchAsync(admin, xodim.Id);

        // Avval kirish ISHLAYDI — ya'ni keyingi rad etish aynan ishdan
        // ketish sababli bo'ladi.
        using (var oldin = await KirishAsync(xodim.Login, parol))
        {
            oldin.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(oldin));
        }

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
            var munosabat = await kontekst.Employments.SingleAsync(row => row.EmployeeId == xodim.Id, ct);

            // Kecha ishdan ketgan: ishdan ketish SANA bo'yicha kuchga
            // kiradi, HR yozib qo'ygan lahzada emas.
            munosabat.Terminate(BugunToshkentda().AddDays(-1), reasonId: null).IsSuccess.ShouldBeTrue();
            await kontekst.SaveChangesAsync(ct);
        }

        using var keyin = await KirishAsync(xodim.Login, parol);
        await XatoKutAsync(keyin, HttpStatusCode.BadRequest, "xodim.notogri_malumot");
    }

    // ════════════════════════════════════════════════════════════
    //  Telefon belgisi TABELGA terminal o'tishi kabi tushadi
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Telefondan belgilangan kun tabelda AYNAN terminal o'tishidagidek hisoblanadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Butun loyihaning asosiy da'vosi: telefon belgisi oddiy
    /// <c>AttendancePass</c> bo'lib yoziladi, ya'ni tabel, panel va
    /// hisobotlarga BIRORTA yangi qoida qo'shilmaydi. Test ikkita bir xil
    /// jadvalli xodimni yonma-yon qo'yadi — biri terminaldan, ikkinchisi
    /// telefondan o'tadi — va kataklari bir xil chiqishini talab qiladi.
    /// </para>
    /// <para>
    /// ⚠️ Vaqtlar QO'LDA berilmaydi (telefon yo'lida bu imkonsiz — vaqtni
    /// server qo'yadi), shu sababli terminal o'tishlari ham AYNAN o'sha
    /// lahzalarda yuboriladi va solishtirish kun HOLATI hamda belgilar
    /// soni bo'yicha bo'ladi.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Telefon_belgisi_tabelga_terminal_otishi_kabi_tushadi()
    {
        var ct = TestContext.Current.CancellationToken;
        var belgi = Belgi();
        using var admin = await AdminMijozAsync();

        var telefonli = await XodimAsync(belgi + "t");
        var terminalli = await XodimAsync(belgi + "q");

        await XodimgaJadvalAsync(admin, telefonli.Id, belgi + "t");
        await XodimgaJadvalAsync(admin, terminalli.Id, belgi + "q");

        await BelgilashniYoqAsync(admin, telefonli.Id);
        var markaz = Nuqta(7);
        await ObyektAsync(admin, "Tabel " + belgi, markaz, radius: 300);

        // ── Telefondan ──
        using (var ilova = await IlovaMijoziAsync(admin, telefonli.Id))
        using (var qabul = await BelgilaAsync(ilova, "In", markaz))
        {
            qabul.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qabul));
        }

        // ── Terminaldan, SHU lahzada ──
        var lahza = await OtishLahzasiAsync(telefonli.Id, ct);
        var shaxs = "t-" + belgi;
        var token = await QurilmaTokeniAsync(admin, "Turniket " + belgi);

        using (var moslik = await admin.PostAsJsonAsync(
                   new Uri("/api/attendance/identities", UriKind.Relative),
                   new { provider = "Verifix", externalPersonId = shaxs, employeeId = terminalli.Id },
                   ct))
        {
            moslik.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(moslik));
        }

        using (var mijoz = _factory.CreateClient())
        using (var qabul = await mijoz.SendAsync(Hodisa(token, shaxs, lahza), ct))
        {
            qabul.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qabul));
        }

        var kun = DateOnly.FromDateTime(lahza.ToOffset(Tashkent).DateTime);

        /*
          IKKALA KUN HAM QAYTA HISOBLANADI — qo'lda, ayni bitta chaqiruv
          bilan.

          ⚠️ Bu test hisoblash NAVBATINI tekshirmaydi: qabul qilingan
          o'tish `processed_at = NULL` bilan yotadi va uni davriy vazifa
          (`AttendanceDirtyRecomputeJob`) oladi, ya'ni test uni kutib
          o'tirsa beqaror bo'lardi. Tekshirilayotgani BOSHQA va aniqroq:
          bir xil vaqtdagi TELEFON belgisi va TERMINAL o'tishi bir xil
          hisoblanadimi. Shu sababli ikkalasiga ham AYNAN bitta yo'l
          (HR ning qayta hisoblash endpointi) qo'llanadi — agar telefon
          belgisi «boshqacha» bo'lganida, farq aynan shu yerda chiqardi.
        */
        foreach (var xodimId in new[] { telefonli.Id, terminalli.Id })
        {
            using var hisobla = await admin.PostAsJsonAsync(
                new Uri("/api/attendance/recompute", UriKind.Relative),
                new { from = Iso(kun), to = Iso(kun), employeeId = xodimId },
                ct);

            // 202 — endpoint shartnomasi (`Produces<AttendanceRecomputeAcceptedDto>(202)`);
            // `Testing` muhitida navbat sinxron, ya'ni javob qaytganda kun hisoblangan.
            hisobla.StatusCode.ShouldBe(HttpStatusCode.Accepted, await MatnAsync(hisobla));
        }

        var telefonKatak = await KatakAsync(admin, telefonli.Id, kun);
        var terminalKatak = await KatakAsync(admin, terminalli.Id, kun);

        telefonKatak.GetProperty("status").GetString()
            .ShouldBe("MissingExit", "faqat kirish bor — chiqish yozilmagan");
        telefonKatak.GetProperty("status").GetString()
            .ShouldBe(terminalKatak.GetProperty("status").GetString());
        telefonKatak.GetProperty("firstInAt").GetDateTimeOffset()
            .ShouldBe(terminalKatak.GetProperty("firstInAt").GetDateTimeOffset());
        telefonKatak.GetProperty("lateMinutes").GetInt32()
            .ShouldBe(terminalKatak.GetProperty("lateMinutes").GetInt32());

        // ⭐ Farq FAQAT manbada — xodim ekrani buni ko'rsatishi kerak.
        using var mening = await (await IlovaMijoziAsync(admin, telefonli.Id))
            .GetAsync(new Uri($"/api/me/attendance?year={kun.Year}&month={kun.Month}", UriKind.Relative), ct);

        mening.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(mening));

        var kunlar = (await JsonAsync(mening)).GetProperty("days").EnumerateArray()
            .Single(item => item.GetProperty("day").GetProperty("date").GetString() == Iso(kun));

        var belgilar = kunlar.GetProperty("marks").EnumerateArray().ShouldHaveSingleItem();
        belgilar.GetProperty("source").GetString().ShouldBe("Mobile");
        belgilar.GetProperty("siteName").GetString().ShouldBe("Tabel " + belgi);
        belgilar.GetProperty("direction").GetString().ShouldBe("In");
    }

    // ════════════════════════════════════════════════════════════
    //  Ro'yxatlar va yordamchilar
    // ════════════════════════════════════════════════════════════

    /// <summary>Xodim tokeni urinib ko'radigan HR endpointlari.</summary>
    /// <remarks>
    /// Ataylab TURLI ruxsat chegaralaridan: davomat ko'rish, ma'lumotnoma,
    /// xodimlar ro'yxati va o'z profili. Oxirgisi eng muhimi —
    /// <c>/api/auth/me</c> ruxsat KODI bilan qo'riqlanmaydi
    /// (<c>RequireAuthorization()</c>), ya'ni uni faqat auditoriya
    /// to'sadi.
    /// </remarks>
    private static readonly string[] HrYollari =
    [
        "/api/auth/me",
        "/api/attendance/timesheet?year=2026&month=9",
        "/api/attendance/work-sites",
        "/api/employees",
    ];

    /// <summary>HR tokeni urinib ko'radigan xodim endpointlari.</summary>
    private static readonly string[] XodimYollari =
    [
        "/api/me",
        "/api/me/attendance?year=2026&month=9",
        "/api/me/requests",
        "/api/me/photo",
    ];

    private sealed record Xodim(Guid Id, string Login);

    private static Uri Yol(string yol) => new("/api/attendance/work-sites" + yol, UriKind.Relative);

    private static string Belgi() => Guid.NewGuid().ToString("N")[..8];

    private static string Iso(DateOnly sana) => sana.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateOnly BugunToshkentda()
        => DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(Tashkent).DateTime);

    /// <summary>
    /// Test uchun BIR-BIRIDAN UZOQ markazlar.
    /// </summary>
    /// <remarks>
    /// ⚠️ Obyektlar ro'yxati GLOBAL: belgilash eng yaqin faol obyektni
    /// qidiradi va sinf bazasi umumiy. Har bir testga o'z kengligini
    /// berish (bir daraja ≈ 111 km) qo'shni testning obyekti hech qachon
    /// «eng yaqin» bo'lib chiqmasligini kafolatlaydi.
    /// </remarks>
    private static (decimal Lat, decimal Lon) Nuqta(int raqam) => (41.0m + raqam, 69.24m);

    /// <summary>Nuqtani SHIMOLGA siljitadi (metr).</summary>
    /// <remarks>
    /// Faqat kenglik bo'yicha: uzunlik bo'yicha bir daraja kenglikka
    /// qarab qisqaradi va test o'zi bilan o'zi geodeziya qilib o'tirmasligi
    /// kerak. Kenglik bo'yicha esa bir daraja har yerda ~111 320 m.
    /// </remarks>
    private static (decimal Lat, decimal Lon) Siljit((decimal Lat, decimal Lon) nuqta, int metr)
        => (nuqta.Lat + (metr / 111_320m), nuqta.Lon);

    private async Task<Xodim> XodimAsync(string belgi)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var firma = LegalEntity.Create("Ilova MCHJ " + belgi).Value;
        var xodim = Employee.Create(FullName.Create("Qodirov", "Ilova", belgi).Value).Value;

        // Login — xodim kartasidagi raqamdan olinadi, ya'ni u unikal
        // bo'lishi kerak: `ux_employee_accounts_login` bitta raqamga
        // ikkinchi hisob ochishga yo'l qo'ymaydi (bu ataylab).
        var telefon = TelefonRaqami();
        xodim.SetPhones(PhoneNumber.Create(telefon).Value);

        kontekst.AddRange(firma, xodim);
        kontekst.Add(Employment.Hire(xodim.Id, firma.Id, new DateOnly(2025, 1, 1)).Value);
        await kontekst.SaveChangesAsync(ct);

        return new Xodim(xodim.Id, telefon);
    }

    /// <summary>Takrorlanmas 9 xonali raqam — operator kodi 0/1 bilan boshlanmaydi.</summary>
    private static string TelefonRaqami()
        => "9" + Random.Shared.Next(10_000_000, 99_999_999).ToString(CultureInfo.InvariantCulture);

    private static async Task BelgilashniYoqAsync(HttpClient admin, Guid xodimId)
    {
        using var javob = await admin.PutAsJsonAsync(
            new Uri($"/api/employees/{xodimId}/mobile-check-in", UriKind.Relative),
            new { allowed = true },
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        (await JsonAsync(javob)).GetProperty("mobileCheckInAllowed").GetBoolean().ShouldBeTrue();
    }

    private static async Task<Guid> ObyektAsync(
        HttpClient admin,
        string nom,
        (decimal Lat, decimal Lon) markaz,
        int radius)
    {
        using var javob = await admin.PostAsJsonAsync(
            Yol("/"),
            new { name = nom, shape = "Circle", latitude = markaz.Lat, longitude = markaz.Lon, radiusMeters = radius },
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        return (await JsonAsync(javob)).GetProperty("id").GetGuid();
    }

    /// <summary>
    /// ~220 × 170 m kvadrat ko'pburchak obyekt; <paramref name="markaz"/> — uning o'rtasi.
    /// </summary>
    /// <remarks>
    /// Shimoliy qirra markazdan roppa-rosa 0,001° kenglik (~111 m) shimolda —
    /// testlar nuqtani SHU qirradan <see cref="Siljit"/> bilan suradi.
    /// </remarks>
    private static async Task<Guid> KopburchakAsync(
        HttpClient admin,
        string nom,
        (decimal Lat, decimal Lon) markaz,
        int zaxira)
    {
        using var javob = await admin.PostAsJsonAsync(
            Yol("/"),
            new
            {
                name = nom,
                shape = "Polygon",
                edgeBufferMeters = zaxira,
                polygon = new[]
                {
                    new { lat = markaz.Lat - 0.001m, lng = markaz.Lon - 0.001m },
                    new { lat = markaz.Lat - 0.001m, lng = markaz.Lon + 0.001m },
                    new { lat = markaz.Lat + 0.001m, lng = markaz.Lon + 0.001m },
                    new { lat = markaz.Lat + 0.001m, lng = markaz.Lon - 0.001m },
                },
            },
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        return (await JsonAsync(javob)).GetProperty("id").GetGuid();
    }

    /// <summary>Kvadratning shimoliy qirrasi (<see cref="KopburchakAsync"/>).</summary>
    private static (decimal Lat, decimal Lon) ShimoliyQirra((decimal Lat, decimal Lon) markaz)
        => (markaz.Lat + 0.001m, markaz.Lon);

    /// <summary>HR hisob ochadi va BIR MARTALIK parolni qaytaradi.</summary>
    private static async Task<string> HisobOchAsync(HttpClient admin, Guid xodimId)
    {
        using var javob = await admin.PostAsJsonAsync(
            new Uri($"/api/employees/{xodimId}/app-access", UriKind.Relative),
            new { },
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var natija = await JsonAsync(javob);
        natija.GetProperty("account").GetProperty("mustChangePassword").GetBoolean().ShouldBeTrue();

        return natija.GetProperty("password").GetString()!;
    }

    private async Task<HttpResponseMessage> KirishAsync(string login, string parol)
        => await _factory.CreateClient().PostAsJsonAsync(
            new Uri("/api/employee-auth/login", UriKind.Relative),
            new { login, password = parol },
            TestContext.Current.CancellationToken);

    /// <summary>Hisob ochadi, kiradi va tayyor mijoz qaytaradi.</summary>
    private async Task<HttpClient> IlovaMijoziAsync(HttpClient admin, Guid xodimId)
    {
        var ct = TestContext.Current.CancellationToken;

        var mavjud = await admin.GetAsync(new Uri($"/api/employees/{xodimId}/app-access", UriKind.Relative), ct);
        var bor = (await JsonAsync(mavjud)).GetProperty("account").ValueKind != JsonValueKind.Null;
        mavjud.Dispose();

        string parol;
        string login;

        if (bor)
        {
            using var tiklash = await admin.PostAsync(
                new Uri($"/api/employees/{xodimId}/app-access/reset-password", UriKind.Relative),
                null,
                ct);

            tiklash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(tiklash));
            var natija = await JsonAsync(tiklash);
            parol = natija.GetProperty("password").GetString()!;
            login = natija.GetProperty("account").GetProperty("login").GetString()!;
        }
        else
        {
            parol = await HisobOchAsync(admin, xodimId);

            using var karta = await admin.GetAsync(
                new Uri($"/api/employees/{xodimId}/app-access", UriKind.Relative),
                ct);

            login = (await JsonAsync(karta)).GetProperty("account").GetProperty("login").GetString()!;
        }

        var client = _factory.CreateClient();

        using var kirish = await KirishAsync(login, parol);
        kirish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(kirish));

        var token = (await JsonAsync(kirish)).GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        /*
          Bir martalik parol bilan olingan token `/api/me/*` da 403
          (`xodim.parol_almashtirilsin`) oladi — ilovadagi majburiy birinchi
          qadam kabi, parol shu yerda almashtiriladi va JAVOBDAGI yangi token
          ishlatiladi (almashtirish eski sessiyalarni yopadi).
        */
        using var almashtirish = await client.PostAsJsonAsync(
            new Uri("/api/employee-auth/password", UriKind.Relative),
            new { currentPassword = parol, newPassword = YangiParol },
            ct);

        almashtirish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(almashtirish));

        var yangi = (await JsonAsync(almashtirish)).GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", yangi);

        return client;
    }

    /// <summary>Testlarda xodim o'rnatadigan parol.</summary>
    private const string YangiParol = "Ilova_parol_2026";

    private static Task<HttpResponseMessage> BelgilaAsync(
        HttpClient ilova,
        string yonalish,
        (decimal Lat, decimal Lon) nuqta,
        decimal aniqlik = 12)
        => ilova.PostAsJsonAsync(
            new Uri("/api/me/check-in", UriKind.Relative),
            new
            {
                direction = yonalish,
                latitude = nuqta.Lat,
                longitude = nuqta.Lon,
                accuracyMeters = aniqlik,
            },
            TestContext.Current.CancellationToken);

    /// <summary>Xodimning YAGONA qabul qilingan belgisining dalil qatori.</summary>
    private async Task<MobileCheckIn> KoordinataAsync(Guid xodimId, CancellationToken ct)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        return await kontekst.MobileCheckIns.AsNoTracking().SingleAsync(row => row.EmployeeId == xodimId, ct);
    }

    /// <summary>
    /// Rad etish xabaridagi masofa: «… Eng yaqin obyekt: X — 120 m.» (<c>AttendanceErrors.OutsideWorkSite</c>).
    /// </summary>
    private static async Task<int> AytilganMasofaAsync(HttpResponseMessage javob)
    {
        var xabar = JsonDocument.Parse(await MatnAsync(javob)).RootElement.GetProperty("detail").GetString()!;
        var moslik = System.Text.RegularExpressions.Regex.Match(xabar, @"— (\d+) m\.$");

        moslik.Success.ShouldBeTrue(xabar);

        return int.Parse(moslik.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    private async Task OtishYoqAsync(Guid xodimId)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        (await kontekst.AttendancePasses.AsNoTracking()
            .AnyAsync(pass => pass.ResolvedEmployeeId == xodimId, TestContext.Current.CancellationToken))
            .ShouldBeFalse("rad etilgan belgi hech qanday o'tish yozmasligi kerak");
    }

    private async Task<DateTimeOffset> OtishLahzasiAsync(Guid xodimId, CancellationToken ct)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        return await kontekst.AttendancePasses.AsNoTracking()
            .Where(pass => pass.ResolvedEmployeeId == xodimId)
            .Select(pass => pass.OccurredAt)
            .SingleAsync(ct);
    }

    private static async Task<string> QurilmaTokeniAsync(HttpClient admin, string nom)
    {
        using var javob = await admin.PostAsJsonAsync(
            new Uri("/api/attendance/devices", UriKind.Relative),
            new { name = nom, provider = "Generic" },
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        return (await JsonAsync(javob)).GetProperty("token").GetString()!;
    }

    private static HttpRequestMessage Hodisa(string token, string shaxs, DateTimeOffset lahza)
    {
        var tana = JsonSerializer.Serialize(new
        {
            events = new[]
            {
                new
                {
                    personId = shaxs,

                    // Kasr soniyalar BILAN: telefon belgisining lahzasi server
                    // soatidan olinadi va mikrosoniyagacha saqlanadi. Soniyagacha
                    // kesilsa, terminal o'tishi «aynan o'sha lahza» bo'lmay qolib,
                    // `firstInAt` solishtiruvi kasr farqi bilan yiqiladi.
                    occurredAt = lahza.ToOffset(Tashkent)
                        .ToString("yyyy-MM-dd'T'HH:mm:ss.fffffffzzz", CultureInfo.InvariantCulture),
                    direction = "In",
                    eventId = $"{shaxs}-{lahza.ToUnixTimeSeconds()}",
                },
            },
        });

        var sorov = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/attendance/ingest", UriKind.Relative))
        {
            Content = new StringContent(tana, System.Text.Encoding.UTF8, "application/json"),
        };
        sorov.Headers.Add("X-Bonvi-Device-Token", token);

        return sorov;
    }

    private static object JadvalTanasi(string nom) => new
    {
        name = nom,
        graceInMinutes = 10,
        graceOutMinutes = 10,
        days = Enumerable.Range(1, 7).Select(kun => new
        {
            dayIndex = kun,
            isWorking = true,
            startTime = "00:05",
            endTime = "23:55",
            breakMinutes = 0,
        }),
    };

    /// <summary>
    /// Har kuni deyarli sutka bo'yi ochiq jadval va uni XODIM pog'onasida biriktirish.
    /// </summary>
    /// <remarks>
    /// Oyna ataylab keng (00:05–23:55): belgilash vaqtini test tanlay
    /// olmaydi (uni server qo'yadi), ya'ni tor smena test qaysi soatda
    /// yurganiga qarab «kechikdi» yoki «erta ketdi» berib, beqaror
    /// bo'lardi.
    /// </remarks>
    private static async Task XodimgaJadvalAsync(HttpClient admin, Guid xodimId, string belgi)
    {
        var ct = TestContext.Current.CancellationToken;

        using var jadval = await admin.PostAsJsonAsync(
            new Uri("/api/attendance/schedules", UriKind.Relative),
            JadvalTanasi("Ilova jadvali " + belgi),
            ct);

        jadval.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(jadval));
        var jadvalId = (await JsonAsync(jadval)).GetProperty("id").GetGuid();

        using var biriktirma = await admin.PostAsJsonAsync(
            new Uri("/api/attendance/schedule-assignments", UriKind.Relative),
            new { workScheduleId = jadvalId, scope = "Employee", employeeId = xodimId, effectiveFrom = "2025-01-01" },
            ct);

        biriktirma.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(biriktirma));
    }

    private static async Task<JsonElement> KatakAsync(HttpClient admin, Guid xodimId, DateOnly kun)
    {
        using var tabel = await admin.GetAsync(
            new Uri(
                $"/api/attendance/timesheet?year={kun.Year}&month={kun.Month}&employeeId={xodimId}",
                UriKind.Relative),
            TestContext.Current.CancellationToken);

        tabel.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(tabel));

        var qator = (await JsonAsync(tabel)).GetProperty("items").EnumerateArray().ShouldHaveSingleItem();

        return qator.GetProperty("cells").EnumerateArray()
            .Single(cell => cell.GetProperty("date").GetString() == Iso(kun));
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
