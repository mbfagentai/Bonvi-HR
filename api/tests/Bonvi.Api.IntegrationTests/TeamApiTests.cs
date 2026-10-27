using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Domain.Abstractions;
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
/// «Jamoam» — bo'lim boshlig'i o'z odamlarining davomatini ilovadan ko'radi (SPEC-jamoam §G).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ Bu eshikning butun xavfi IDOR va maydon sizishi: manzilda xodim identifikatori bor va
/// javob HR panelining hisobidan quriladi. Shu sababli har bir test haqiqiy bazada, haqiqiy
/// xodim tokeni bilan yuradi — qamrov, «boshliqmi» va «hali ham xodimmi» qarorlari faqat
/// shu yerda birga ko'rinadi.
/// </para>
/// <para>
/// Har test O'Z dunyosini quradi (<see cref="DunyoAsync"/>): noyob belgili firma, besh bo'limli
/// daraxt va olti odam. Sinf bazasi umumiy, lekin qamrov daraxtga bog'liq, ya'ni qo'shni
/// testning odamlari hech qachon bu yerdagi panelga tushmaydi.
/// </para>
/// </remarks>
public sealed class TeamApiTests : IClassFixture<BonviApiFactory>
{
    private static readonly TimeSpan Tashkent = TimeSpan.FromHours(5);

    private readonly BonviApiFactory _factory;

    public TeamApiTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ════════════════════════════════════════════════════════════
    //  Kim kira oladi
    // ════════════════════════════════════════════════════════════

    /// <summary>Boshliq bo'lmagan xodim to'rtala endpointda «jamoa.boshliq_emas» (403) oladi.</summary>
    /// <remarks>
    /// A'zoning oyi ham, rasmi ham — garchi a'zo shu xodimning o'z sexidan bo'lsa ham:
    /// hamkasb bo'lish ko'rish huquqini bermaydi, faqat boshliqlik beradi.
    /// </remarks>
    [Fact]
    public async Task Boshliq_bolmagan_xodim_jamoa_endpointlarida_403_oladi()
    {
        using var admin = await AdminMijozAsync();
        var dunyo = await DunyoAsync();
        using var oddiy = await IlovaMijoziAsync(admin, dunyo.Oddiy.Id);

        foreach (var yol in JamoaYollari(dunyo.Azo.Id))
        {
            using var javob = await oddiy.GetAsync(Yol(yol), Ct);
            await XatoKutAsync(javob, HttpStatusCode.Forbidden, "jamoa.boshliq_emas");
        }

        (await MeAsync(oddiy)).GetProperty("isTeamLead").GetBoolean().ShouldBeFalse();
    }

    /// <summary>HR tokeni — administratorniki ham — <c>/api/me/team/*</c> ga 401 bilan o'tmaydi.</summary>
    [Fact]
    public async Task HR_tokeni_jamoa_endpointlariga_otmaydi()
    {
        using var admin = await AdminMijozAsync();
        var dunyo = await DunyoAsync();

        foreach (var yol in JamoaYollari(dunyo.Azo.Id))
        {
            using var javob = await admin.GetAsync(Yol(yol), Ct);
            javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, yol);
        }
    }

    /// <summary>Ishdan ketgan boshliq — «xodim.hisob_faol_emas» (403), access token muddatini kutmasdan.</summary>
    [Fact]
    public async Task Ishdan_ketgan_boshliq_hisob_faol_emas_oladi()
    {
        using var admin = await AdminMijozAsync();
        var dunyo = await DunyoAsync();
        using var boshliq = await IlovaMijoziAsync(admin, dunyo.Boshliq.Id);

        using (var oldin = await boshliq.GetAsync(Yol("/api/me/team"), Ct))
        {
            oldin.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(oldin));
        }

        await IshdanBoshatAsync(dunyo.Boshliq.Id);

        foreach (var yol in JamoaYollari(dunyo.Azo.Id))
        {
            using var javob = await boshliq.GetAsync(Yol(yol), Ct);
            await XatoKutAsync(javob, HttpStatusCode.Forbidden, "xodim.hisob_faol_emas");
        }
    }

    /// <summary>
    /// HR boshliqni almashtiradi — eski boshliq ESKI token bilan keyingi so'rovdayoq 403, yangisi 200.
    /// </summary>
    /// <remarks>
    /// Almashtirish HR ning haqiqiy saqlash yo'lidan (<c>PUT /api/catalog/departments/{id}</c>) —
    /// ya'ni §E dagi saqlash ham shu yerda isbotlanadi. Qamrov tokenda emas, bazada.
    /// </remarks>
    [Fact]
    public async Task Boshliq_almashtirilsa_eskisi_keyingi_sorovda_403_oladi()
    {
        using var admin = await AdminMijozAsync();
        var dunyo = await DunyoAsync();
        using var eski = await IlovaMijoziAsync(admin, dunyo.Boshliq.Id);
        using var yangi = await IlovaMijoziAsync(admin, dunyo.Oddiy.Id);

        using (var oldin = await eski.GetAsync(Yol("/api/me/team"), Ct))
        {
            oldin.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(oldin));
        }

        using (var saqlash = await BolimniSaqlaAsync(admin, dunyo.Zavod, dunyo.ZavodNomi, dunyo.Oddiy.Id))
        {
            saqlash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(saqlash));
            (await JsonAsync(saqlash)).GetProperty("headEmployeeId").GetGuid().ShouldBe(dunyo.Oddiy.Id);
        }

        using (var keyin = await eski.GetAsync(Yol("/api/me/team"), Ct))
        {
            await XatoKutAsync(keyin, HttpStatusCode.Forbidden, "jamoa.boshliq_emas");
        }

        (await MeAsync(eski)).GetProperty("isTeamLead").GetBoolean().ShouldBeFalse();

        using var yangiJavob = await yangi.GetAsync(Yol("/api/me/team"), Ct);
        yangiJavob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yangiJavob));
        (await MeAsync(yangi)).GetProperty("isTeamLead").GetBoolean().ShouldBeTrue();
    }

    // ════════════════════════════════════════════════════════════
    //  Qamrov — aynan o'z daraxti
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Boshliq aynan o'z daraxtini ko'radi — ichma-ich bo'limlar bilan; qo'shni zavod va ota bo'lim yo'q.
    /// </summary>
    [Fact]
    public async Task Boshliq_aynan_oz_daraxtini_koradi_qoshni_bolim_kirmaydi()
    {
        using var admin = await AdminMijozAsync();
        var dunyo = await DunyoAsync();
        using var boshliq = await IlovaMijoziAsync(admin, dunyo.Boshliq.Id);

        (await MeAsync(boshliq)).GetProperty("isTeamLead").GetBoolean().ShouldBeTrue();

        using var qamrov = await boshliq.GetAsync(Yol("/api/me/team"), Ct);
        qamrov.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qamrov));

        var bolimlar = (await JsonAsync(qamrov)).GetProperty("departments").EnumerateArray().ToList();

        // Daraxt tartibi: zavod, sex, sexning ichidagisi.
        bolimlar.Select(bolim => bolim.GetProperty("id").GetGuid()).ShouldBe([dunyo.Zavod, dunyo.Sex, dunyo.Ichki]);

        var zavod = bolimlar[0];
        zavod.GetProperty("name").GetString().ShouldBe(dunyo.ZavodNomi);
        zavod.GetProperty("parentId").GetGuid().ShouldBe(dunyo.Ota);
        zavod.GetProperty("level").GetInt32().ShouldBe(1);
        zavod.GetProperty("path").GetString()!.ShouldEndWith(" / " + dunyo.ZavodNomi);

        // Panel — butun qamrov: zavod (boshliqning o'zi), sex va ichma-ich bo'lim.
        var panel = await PanelAsync(boshliq, "");
        Odamlar(panel).ShouldBe(
            [dunyo.Boshliq.Id, dunyo.Azo.Id, dunyo.IchkiAzo.Id, dunyo.Oddiy.Id],
            ignoreOrder: true);
        panel.GetProperty("total").GetInt32().ShouldBe(4);
    }

    /// <summary>
    /// Panel qidiruvi faqat ism va telefon bo'yicha — jamoa a'zosining JSHSHIR yoki pasport raqami hech kimni topmaydi.
    /// </summary>
    [Fact]
    public async Task Panel_qidiruvi_ism_va_telefon_boyicha_jshshir_va_pasport_bilan_topilmaydi()
    {
        using var admin = await AdminMijozAsync();
        var dunyo = await DunyoAsync();
        using var boshliq = await IlovaMijoziAsync(admin, dunyo.Boshliq.Id);

        var jshshir = "3110294" + DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture)[^7..];
        var pasport = "AA" + DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture)[^7..];

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
            var azo = await kontekst.Employees.SingleAsync(xodim => xodim.Id == dunyo.Azo.Id, Ct);

            // Natija ATAYLAB tekshirilmaydi (`PublicApiLeakTests` dagidek): sana mos kelmasa ham qiymat o'rnatiladi.
            azo.SetPinfl(Pinfl.FromTrusted(jshshir));
            azo.SetPassport(PassportSeries.FromTrusted(pasport));
            await kontekst.SaveChangesAsync(Ct);
        }

        // HR qidiruvi uchun bu to'g'ri, boshliq uchun esa «bu raqam jamoamdami» degan savolning javobi bo'lardi.
        Odamlar(await PanelAsync(boshliq, $"search={jshshir}")).ShouldBeEmpty();
        Odamlar(await PanelAsync(boshliq, $"search={pasport}")).ShouldBeEmpty();

        Odamlar(await PanelAsync(boshliq, $"search={Uri.EscapeDataString(dunyo.Azo.Familiya)}")).ShouldBe([dunyo.Azo.Id]);
        Odamlar(await PanelAsync(boshliq, $"search={dunyo.Azo.Telefon[^7..]}")).ShouldBe([dunyo.Azo.Id]);
    }

    /// <summary>
    /// <c>departmentId</c> qamrov ichida toraytiradi (ichki bo'limlari bilan); tashqaridagi — 404.
    /// </summary>
    [Fact]
    public async Task Bolim_filtri_qamrov_ichida_toraytiradi_tashqaridagi_404()
    {
        using var admin = await AdminMijozAsync();
        var dunyo = await DunyoAsync();
        using var boshliq = await IlovaMijoziAsync(admin, dunyo.Boshliq.Id);

        var sex = await PanelAsync(boshliq, $"departmentId={dunyo.Sex}");
        Odamlar(sex).ShouldBe([dunyo.Azo.Id, dunyo.IchkiAzo.Id, dunyo.Oddiy.Id], ignoreOrder: true);

        foreach (var begona in new[] { dunyo.Qoshni, dunyo.Ota, Guid.NewGuid() })
        {
            using var javob = await boshliq.GetAsync(Yol($"/api/me/team/board?departmentId={begona}"), Ct);
            await XatoKutAsync(javob, HttpStatusCode.NotFound, "jamoa.bolim_topilmadi");
        }
    }

    /// <summary>
    /// Qamrovdan tashqaridagi xodim — oy ham, rasm ham «jamoa.xodim_topilmadi» (404), xuddi yo'q odamdagidek.
    /// </summary>
    [Fact]
    public async Task Qamrovdan_tashqari_xodim_oy_va_rasmda_404_oladi()
    {
        using var admin = await AdminMijozAsync();
        var dunyo = await DunyoAsync();
        using var boshliq = await IlovaMijoziAsync(admin, dunyo.Boshliq.Id);
        var (yil, oy) = BuOy();

        foreach (var begona in new[] { dunyo.Begona.Id, dunyo.Katta.Id, Guid.NewGuid() })
        {
            using (var oyi = await boshliq.GetAsync(Yol($"/api/me/team/members/{begona}/attendance?year={yil}&month={oy}"), Ct))
            {
                await XatoKutAsync(oyi, HttpStatusCode.NotFound, "jamoa.xodim_topilmadi");
            }

            using var rasmi = await boshliq.GetAsync(Yol($"/api/me/team/members/{begona}/photo"), Ct);
            await XatoKutAsync(rasmi, HttpStatusCode.NotFound, "jamoa.xodim_topilmadi");
        }

        // Qamrovdagi, rasmsiz — oy ochiladi, rasm esa «xodim.rasm_yoq».
        using (var ichkiOy = await boshliq.GetAsync(Yol($"/api/me/team/members/{dunyo.IchkiAzo.Id}/attendance?year={yil}&month={oy}"), Ct))
        {
            ichkiOy.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(ichkiOy));
        }

        using (var rasmsiz = await boshliq.GetAsync(Yol($"/api/me/team/members/{dunyo.IchkiAzo.Id}/photo"), Ct))
        {
            await XatoKutAsync(rasmsiz, HttpStatusCode.NotFound, "xodim.rasm_yoq");
        }

        // Oy qoidasi — «/api/me/attendance» niki.
        using var notogri = await boshliq.GetAsync(Yol($"/api/me/team/members/{dunyo.Azo.Id}/attendance?year={yil}&month=13"), Ct);
        await XatoKutAsync(notogri, HttpStatusCode.BadRequest, "davomat.oy_notogri");
    }

    /// <summary>A'zoning oyi — «/api/me/attendance» ning AYNAN o'zi; sarlavhada telefon va «hasPhoto».</summary>
    [Fact]
    public async Task Azoning_oyi_uning_oz_ilovasidagi_bilan_bir_xil()
    {
        using var admin = await AdminMijozAsync();
        var dunyo = await DunyoAsync();
        using var boshliq = await IlovaMijoziAsync(admin, dunyo.Boshliq.Id);
        using var azo = await IlovaMijoziAsync(admin, dunyo.Azo.Id);
        var (yil, oy) = BuOy();

        using var jamoadan = await boshliq.GetAsync(Yol($"/api/me/team/members/{dunyo.Azo.Id}/attendance?year={yil}&month={oy}"), Ct);
        jamoadan.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(jamoadan));

        using var ozidan = await azo.GetAsync(Yol($"/api/me/attendance?year={yil}&month={oy}"), Ct);
        ozidan.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(ozidan));

        var javob = await JsonAsync(jamoadan);

        javob.GetProperty("attendance").GetRawText().ShouldBe((await JsonAsync(ozidan)).GetRawText());

        var sarlavha = javob.GetProperty("employee");
        sarlavha.GetProperty("employeeId").GetGuid().ShouldBe(dunyo.Azo.Id);
        sarlavha.GetProperty("fullName").GetString().ShouldBe(dunyo.Azo.FullName);
        sarlavha.GetProperty("hasPhoto").GetBoolean().ShouldBeFalse();
        sarlavha.GetProperty("departmentName").GetString().ShouldBe(dunyo.SexNomi);
        sarlavha.GetProperty("phone").GetString().ShouldBe(PhoneNumber.Create(dunyo.Azo.Telefon).Value.Formatted);
    }

    /// <summary>A'zoning rasmi — baytlar, shaxsiy kesh, ETag va 304 («/api/me/photo» dagidek).</summary>
    [Fact]
    public async Task Azoning_rasmi_etag_va_304_bilan_beriladi()
    {
        using var admin = await AdminMijozAsync();
        var dunyo = await DunyoAsync();
        await RasmYuklaAsync(admin, dunyo.Azo.Id);
        using var boshliq = await IlovaMijoziAsync(admin, dunyo.Boshliq.Id);

        var panel = await PanelAsync(boshliq, $"search={Uri.EscapeDataString(dunyo.Azo.Familiya)}");
        panel.GetProperty("items").EnumerateArray().ShouldHaveSingleItem().GetProperty("hasPhoto").GetBoolean().ShouldBeTrue();

        var yol = Yol($"/api/me/team/members/{dunyo.Azo.Id}/photo");
        string belgi;

        using (var javob = await boshliq.GetAsync(yol, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
            javob.Content.Headers.ContentType!.MediaType.ShouldBe("image/jpeg");
            javob.Headers.CacheControl!.Private.ShouldBeTrue();
            belgi = javob.Headers.ETag!.Tag;
        }

        using var sorov = new HttpRequestMessage(HttpMethod.Get, yol);
        sorov.Headers.TryAddWithoutValidation("If-None-Match", belgi);

        using var takror = await boshliq.SendAsync(sorov, Ct);
        takror.StatusCode.ShouldBe(HttpStatusCode.NotModified);
    }

    // ════════════════════════════════════════════════════════════
    //  Panel = HR paneli
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Bitta bo'lim va sana uchun boshliq panelining sanoqlari va holatlari HR panelidagi bilan AYNAN bir xil.
    /// </summary>
    /// <remarks>
    /// O'tgan sana ataylab: o'tgan kunning holati «hozir» ga bog'liq emas, ya'ni ikki so'rov
    /// orasidagi millisoniyalar natijani o'zgartira olmaydi. Holatlar turli bo'lishi uchun
    /// jadval va o'tishlar bor — vaqtida, kechikkan va kelmagan.
    /// </remarks>
    [Fact]
    public async Task Panel_sanoqlari_HR_paneli_bilan_bir_xil()
    {
        using var admin = await AdminMijozAsync();
        var dunyo = await DunyoAsync(jadvalVaOtishlar: true);
        using var boshliq = await IlovaMijoziAsync(admin, dunyo.Boshliq.Id);
        var sana = Iso(dunyo.OtishKuni);

        foreach (var bolim in new[] { dunyo.Zavod, dunyo.Sex })
        {
            using var hrJavob = await admin.GetAsync(
                Yol($"/api/attendance/dashboard?date={sana}&departmentId={bolim}&pageSize=200"), Ct);
            hrJavob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(hrJavob));
            var hr = await JsonAsync(hrJavob);

            var jamoa = await PanelAsync(boshliq, $"date={sana}&departmentId={bolim}&pageSize=200");

            jamoa.GetProperty("total").GetInt32().ShouldBe(hr.GetProperty("total").GetInt32());
            jamoa.GetProperty("counts").GetRawText().ShouldBe(hr.GetProperty("counts").GetRawText());
            jamoa.GetProperty("date").GetString().ShouldBe(sana);
            jamoa.GetProperty("isToday").GetBoolean().ShouldBeFalse();

            Holatlar(jamoa).ShouldBe(Holatlar(hr));
        }

        // Butun qamrov (bo'limsiz) — zavodning o'zi bilan teng: qamrov ildizi zavod.
        using var zavodJavob = await admin.GetAsync(
            Yol($"/api/attendance/dashboard?date={sana}&departmentId={dunyo.Zavod}"), Ct);
        var zavod = await JsonAsync(zavodJavob);
        var hammasi = await PanelAsync(boshliq, $"date={sana}");
        hammasi.GetProperty("counts").GetRawText().ShouldBe(zavod.GetProperty("counts").GetRawText());

        // Sanoqlar bo'sh emas — aks holda tenglik hech narsani isbotlamasdi.
        var sanoq = hammasi.GetProperty("counts");
        sanoq.GetProperty("onTime").GetInt32().ShouldBe(1);
        sanoq.GetProperty("late").GetInt32().ShouldBe(1);
        sanoq.GetProperty("absent").GetInt32().ShouldBe(2);

        var kechikkan = hammasi.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("employeeId").GetGuid() == dunyo.IchkiAzo.Id);
        kechikkan.GetProperty("status").GetString().ShouldBe("Late");
        kechikkan.GetProperty("lateMinutes").GetInt32().ShouldBe(40);
        kechikkan.GetProperty("phone").GetString().ShouldBe(PhoneNumber.Create(dunyo.IchkiAzo.Telefon).Value.Formatted);

        // Holat va qidiruv — paneldagidek: faqat jadvalni toraytiradi.
        var faqatKelmagan = await PanelAsync(boshliq, $"date={sana}&status=Absent");
        faqatKelmagan.GetProperty("totalItems").GetInt32().ShouldBe(2);
        faqatKelmagan.GetProperty("total").GetInt32().ShouldBe(4);
    }

    /// <summary>
    /// Panel filtrining qo'shimcha maydoni: ildizlar daraxtlarining BIRLASHMASI; bo'sh to'plam — hech kim.
    /// </summary>
    /// <remarks>
    /// O'qish modelining o'zida: «ildiz yo'q» hech qachon «hamma» bo'lib ketmasligi kerak — aks
    /// holda qamrovi bo'sh bo'lib qolgan so'rov butun korxonani ko'rsatardi. Bitta ildiz esa
    /// mavjud <c>DepartmentId</c> filtri bilan aynan bir xil to'plam beradi.
    /// </remarks>
    [Fact]
    public async Task Panel_filtri_ildizlarni_birlashtiradi_bosh_toplam_hech_kim()
    {
        var dunyo = await DunyoAsync();
        var sana = BugunToshkentda();

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var oqish = qamrov.ServiceProvider.GetRequiredService<IAttendanceReadModel>();

        var ikkiDaraxt = await oqish.GetDashboardAsync(
            new DashboardFilter(sana, DepartmentRoots: [dunyo.Sex, dunyo.Qoshni], PageSize: 200),
            Ct);

        ikkiDaraxt.Items.Select(item => item.EmployeeId).ShouldBe(
            [dunyo.Azo.Id, dunyo.Oddiy.Id, dunyo.IchkiAzo.Id, dunyo.Begona.Id],
            ignoreOrder: true);

        var hechKim = await oqish.GetDashboardAsync(new DashboardFilter(sana, DepartmentRoots: []), Ct);
        hechKim.Total.ShouldBe(0);
        hechKim.Items.ShouldBeEmpty();

        var ildiz = await oqish.GetDashboardAsync(new DashboardFilter(sana, DepartmentRoots: [dunyo.Zavod], PageSize: 200), Ct);
        var bolim = await oqish.GetDashboardAsync(new DashboardFilter(sana, DepartmentId: dunyo.Zavod, PageSize: 200), Ct);

        ildiz.Total.ShouldBe(bolim.Total);
        ildiz.Counts.ShouldBe(bolim.Counts);
        ildiz.Items.Select(item => item.EmployeeId).ShouldBe(bolim.Items.Select(item => item.EmployeeId));
    }

    /// <summary>
    /// Javoblarda HR maydonlari YO'Q: rasm kaliti, JSHSHIR, pasport, oklad, qurilmalar holati (§C2).
    /// </summary>
    [Fact]
    public async Task Jamoa_javoblarida_HR_maydonlari_yoq()
    {
        using var admin = await AdminMijozAsync();
        var dunyo = await DunyoAsync();
        await RasmYuklaAsync(admin, dunyo.Azo.Id);
        using var boshliq = await IlovaMijoziAsync(admin, dunyo.Boshliq.Id);
        var (yil, oy) = BuOy();

        string[] yollar =
        [
            "/api/me/team",
            "/api/me/team/board",
            $"/api/me/team/members/{dunyo.Azo.Id}/attendance?year={yil}&month={oy}",
        ];

        foreach (var yol in yollar)
        {
            using var javob = await boshliq.GetAsync(Yol(yol), Ct);
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

            var kalitlar = Kalitlar(await JsonAsync(javob)).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var taqiqlangan in TaqiqlanganKalitlar)
            {
                kalitlar.ShouldNotContain(taqiqlangan, $"{yol} — «{taqiqlangan}» bo'lmasligi kerak");
            }
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Audit
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// A'zoning oyi va panel audit jurnaliga yoziladi — bajaruvchi boshliq, takrorlar oynada yozilmaydi.
    /// </summary>
    [Fact]
    public async Task Korish_audit_jurnaliga_yoziladi_va_takrorlanmaydi()
    {
        using var admin = await AdminMijozAsync();
        var dunyo = await DunyoAsync();
        using var boshliq = await IlovaMijoziAsync(admin, dunyo.Boshliq.Id);
        var (yil, oy) = BuOy();

        for (var i = 0; i < 3; i++)
        {
            using var oyi = await boshliq.GetAsync(Yol($"/api/me/team/members/{dunyo.Azo.Id}/attendance?year={yil}&month={oy}"), Ct);
            oyi.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(oyi));

            using var panel = await boshliq.GetAsync(Yol("/api/me/team/board"), Ct);
            panel.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(panel));
        }

        // Rad etilgan so'rov (qamrovdan tashqari) — ko'rish emas, yozilmaydi.
        using (var rad = await boshliq.GetAsync(Yol($"/api/me/team/members/{dunyo.Begona.Id}/attendance?year={yil}&month={oy}"), Ct))
        {
            rad.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var azoQatorlari = await kontekst.AuditLogs.AsNoTracking()
            .Where(log => log.EntityName == "Employee" && log.EntityId == dunyo.Azo.Id.ToString())
            .ToListAsync(Ct);

        var korish = azoQatorlari
            .Where(log => log.NewValues != null && log.NewValues.Contains("team_member_viewed", StringComparison.Ordinal))
            .ToList();
        var bitta = korish.ShouldHaveSingleItem("uch marta ko'rildi — 10 daqiqalik oynada bitta qator");

        bitta.Action.ShouldBe(AuditAction.Export);
        bitta.ActorKind.ShouldBe(AuditActorKind.EmployeeAccount);
        bitta.ActorName.ShouldBe(dunyo.Boshliq.FullName);
        bitta.Operation.ShouldBe("GetTeamMemberAttendance");

        using (var tafsilot = JsonDocument.Parse(bitta.NewValues!))
        {
            tafsilot.RootElement.GetProperty("action").GetString().ShouldBe("team_member_viewed");
            tafsilot.RootElement.GetProperty("year").GetInt32().ShouldBe(yil);
            tafsilot.RootElement.GetProperty("month").GetInt32().ShouldBe(oy);
        }

        // Begonaning kartasi yaratilgani (Create) jurnalda bor — ko'rish izi esa yo'q.
        var begonaQatorlari = await kontekst.AuditLogs.AsNoTracking()
            .Where(log => log.EntityName == "Employee" && log.EntityId == dunyo.Begona.Id.ToString())
            .ToListAsync(Ct);
        begonaQatorlari.ShouldNotContain(log => log.Action == AuditAction.Export);

        var hisob = await kontekst.EmployeeAccounts.AsNoTracking()
            .Where(row => row.EmployeeId == dunyo.Boshliq.Id)
            .Select(row => row.Id)
            .SingleAsync(Ct);

        var panelQatorlari = await kontekst.AuditLogs.AsNoTracking()
            .Where(log => log.EntityName == "EmployeeAccount" && log.EntityId == hisob.ToString())
            .ToListAsync(Ct);

        var panelKorish = panelQatorlari
            .Where(log => log.NewValues != null && log.NewValues.Contains("team_board_viewed", StringComparison.Ordinal))
            .ToList();

        panelKorish.ShouldHaveSingleItem("uch marta ochildi — 30 daqiqalik oynada bitta qator")
            .ActorName.ShouldBe(dunyo.Boshliq.FullName);
    }

    // ════════════════════════════════════════════════════════════
    //  «Rahbar» (§D)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Rahbar — bo'limdan yuqoriga birinchi tirik boshliq: o'zi boshliq bo'lsa ota bo'lim,
    /// ishdan ketgan boshliq tashlab ketiladi; ilova profili va HR kartasi bir xil gapiradi.
    /// </summary>
    [Fact]
    public async Task Rahbar_ozi_boshliq_bolsa_otadan_ishdan_ketgan_boshliq_tashlab_ketiladi()
    {
        using var admin = await AdminMijozAsync();
        var dunyo = await DunyoAsync();
        using var azo = await IlovaMijoziAsync(admin, dunyo.Azo.Id);
        using var boshliq = await IlovaMijoziAsync(admin, dunyo.Boshliq.Id);
        using var katta = await IlovaMijoziAsync(admin, dunyo.Katta.Id);

        // Sexning boshlig'i yo'q — zavod boshlig'i.
        (await MeAsync(azo)).GetProperty("manager").GetProperty("fullName").GetString().ShouldBe(dunyo.Boshliq.FullName);

        var karta = await KartaAsync(admin, dunyo.Azo.Id);
        karta.GetProperty("manager").GetProperty("employeeId").GetGuid().ShouldBe(dunyo.Boshliq.Id);
        karta.GetProperty("manager").GetProperty("fullName").GetString().ShouldBe(dunyo.Boshliq.FullName);

        // Zavod boshlig'ining o'zi — o'z zavodi tashlab ketiladi, ota bo'lim boshlig'i.
        (await MeAsync(boshliq)).GetProperty("manager").GetProperty("fullName").GetString().ShouldBe(dunyo.Katta.FullName);

        // Eng yuqoridagi — rahbari yo'q.
        (await MeAsync(katta)).GetProperty("manager").ValueKind.ShouldBe(JsonValueKind.Null);
        (await KartaAsync(admin, dunyo.Katta.Id)).GetProperty("manager").ValueKind.ShouldBe(JsonValueKind.Null);

        // Zavod boshlig'i ishdan ketdi — yurish davom etadi.
        await IshdanBoshatAsync(dunyo.Boshliq.Id);

        (await MeAsync(azo)).GetProperty("manager").GetProperty("fullName").GetString().ShouldBe(dunyo.Katta.FullName);
        (await KartaAsync(admin, dunyo.Azo.Id)).GetProperty("manager").GetProperty("employeeId").GetGuid().ShouldBe(dunyo.Katta.Id);
    }

    // ════════════════════════════════════════════════════════════
    //  Bo'limni saqlash (§E)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Yo'q yoki o'chirilgan xodim boshliq qilinmaydi — «bolim.boshliq_topilmadi» (400).
    /// </summary>
    /// <remarks>
    /// O'ZGARMAGAN boshliq esa qayta tekshirilmaydi: forma joriy qiymatni har saqlashda yuboradi,
    /// keyinroq o'chirilgan boshliq bo'limni qayta nomlashni to'sib qo'ymasin.
    /// </remarks>
    [Fact]
    public async Task Bolim_saqlashda_yoq_boshliq_400_qaytaradi()
    {
        using var admin = await AdminMijozAsync();
        var dunyo = await DunyoAsync();

        using (var yangi = await admin.PostAsJsonAsync(
                   Yol("/api/catalog/departments"),
                   new { name = "Yangi " + dunyo.Belgi, headEmployeeId = Guid.NewGuid() },
                   Ct))
        {
            await XatoKutAsync(yangi, HttpStatusCode.BadRequest, "bolim.boshliq_topilmadi");
        }

        await XodimniOchirAsync(dunyo.Begona.Id);

        using (var ochirilgan = await BolimniSaqlaAsync(admin, dunyo.Sex, dunyo.SexNomi, dunyo.Begona.Id))
        {
            await XatoKutAsync(ochirilgan, HttpStatusCode.BadRequest, "bolim.boshliq_topilmadi");
        }

        // Zavodning boshlig'i (o'zgarmagan) keyin o'chirildi — qayta nomlash baribir o'tadi.
        await XodimniOchirAsync(dunyo.Boshliq.Id);

        using var qaytaNomlash = await BolimniSaqlaAsync(admin, dunyo.Zavod, dunyo.ZavodNomi + " 2", dunyo.Boshliq.Id);
        qaytaNomlash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qaytaNomlash));
    }

    // ════════════════════════════════════════════════════════════
    //  Dunyo
    // ════════════════════════════════════════════════════════════

    /// <summary>Test odami — kirish uchun telefon va kutilgan ism bilan.</summary>
    private sealed record Odam(Guid Id, string Telefon, string Familiya, string FullName);

    /// <summary>
    /// <code>
    /// Ota (boshlig'i: Katta)
    /// ├─ Zavod (boshlig'i: Boshliq; Boshliq shu yerda ishlaydi)
    /// │  └─ Sex (Azo, Oddiy)
    /// │     └─ Ichki (IchkiAzo)
    /// └─ Qoshni (Begona)
    /// </code>
    /// </summary>
    private sealed record Dunyo(
        string Belgi,
        Guid Ota,
        Guid Zavod,
        string ZavodNomi,
        Guid Sex,
        string SexNomi,
        Guid Ichki,
        Guid Qoshni,
        Odam Katta,
        Odam Boshliq,
        Odam Azo,
        Odam Oddiy,
        Odam IchkiAzo,
        Odam Begona,
        DateOnly OtishKuni);

    /// <summary>Daraxt, odamlar va (xohlansa) jadval bilan o'tgan kunning o'tishlari.</summary>
    /// <remarks>
    /// <paramref name="jadvalVaOtishlar"/>: Ota daraxtiga har kuni 09:00–18:00 jadval va
    /// <see cref="Dunyo.OtishKuni"/> (ikki kun oldin) uchun o'tishlar — Boshliq 08:50 (vaqtida),
    /// IchkiAzo 09:40 (40 daqiqa kechikkan), Azo va Oddiy — yo'q (kelmagan).
    /// </remarks>
    private async Task<Dunyo> DunyoAsync(bool jadvalVaOtishlar = false)
    {
        var belgi = "JM" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var otishKuni = BugunToshkentda().AddDays(-2);

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var firma = LegalEntity.Create("Jamoa MCHJ " + belgi).Value;

        var ota = Department.Create("Departament " + belgi).Value;
        var zavod = Department.Create("Zavod " + belgi, ota.Id).Value;
        var sex = Department.Create("Sex " + belgi, zavod.Id).Value;
        var ichki = Department.Create("Ichki " + belgi, sex.Id).Value;
        var qoshni = Department.Create("Qoshni " + belgi, ota.Id).Value;

        kontekst.AddRange(firma, ota, zavod, sex, ichki, qoshni);

        Odam Ishga(string familiya, Department bolim)
        {
            var telefon = TelefonRaqami();
            var ism = FullName.Create(familiya + belgi, "Jamoa", null).Value;
            var xodim = Employee.Create(ism).Value;
            xodim.SetPhones(PhoneNumber.Create(telefon).Value);

            kontekst.AddRange(xodim, Employment.Hire(xodim.Id, firma.Id, new DateOnly(2025, 1, 1), bolim.Id).Value);

            return new Odam(xodim.Id, telefon, familiya + belgi, ism.Full);
        }

        var katta = Ishga("Katta", ota);
        var boshliq = Ishga("Boshliq", zavod);
        var azo = Ishga("Azo", sex);
        var oddiy = Ishga("Oddiy", sex);
        var ichkiAzo = Ishga("Ichki", ichki);
        var begona = Ishga("Begona", qoshni);

        ota.AssignHead(katta.Id);
        zavod.AssignHead(boshliq.Id);

        if (jadvalVaOtishlar)
        {
            var jadval = WorkSchedule.Create(
                "Jamoa 9-18 " + belgi,
                [.. Enumerable.Range(1, 7).Select(kun => new WorkScheduleDayInput(kun, IsWorking: true, new TimeOnly(9, 0), new TimeOnly(18, 0)))]).Value;

            var biriktirma = ScheduleAssignment.Assign(jadval.Id, ScheduleScope.Department, ota.Id, new DateOnly(2025, 1, 1)).Value;

            var qurilma = AttendanceDevice.Register("Jamoa turniketi " + belgi, RandomNumberGenerator.GetBytes(32), "tst00000", DateTimeOffset.UtcNow).Value;

            AttendancePass Otish(Guid xodimId, int soat, int daqiqa)
            {
                var lahza = new DateTimeOffset(otishKuni.ToDateTime(new TimeOnly(soat, daqiqa)), Tashkent);
                var otish = AttendancePass.Record(
                    qurilma.Id,
                    AttendanceIdentityProvider.Verifix,
                    $"{belgi}-{xodimId:N}",
                    lahza,
                    lahza.ToUniversalTime(),
                    Guid.NewGuid().ToString("N"),
                    PassDirection.In,
                    DirectionSource.Device,
                    PassMethod.Face).Value;

                otish.Resolve(xodimId, lahza.ToUniversalTime());
                return otish;
            }

            kontekst.AddRange(jadval, biriktirma, qurilma, Otish(boshliq.Id, 8, 50), Otish(ichkiAzo.Id, 9, 40));
        }

        await kontekst.SaveChangesAsync(Ct);

        // Daraj va yo'l — HR saqlaganidagi kabi.
        await qamrov.ServiceProvider.GetRequiredService<IDepartmentRepository>().RebuildHierarchyAsync(Ct);

        return new Dunyo(
            belgi,
            ota.Id,
            zavod.Id,
            zavod.Name,
            sex.Id,
            sex.Name,
            ichki.Id,
            qoshni.Id,
            katta,
            boshliq,
            azo,
            oddiy,
            ichkiAzo,
            begona,
            otishKuni);
    }

    // ════════════════════════════════════════════════════════════
    //  Yordamchilar
    // ════════════════════════════════════════════════════════════

    /// <summary>Javobda bo'lmasligi shart bo'lgan kalitlar (registrsiz).</summary>
    private static readonly string[] TaqiqlanganKalitlar =
        ["photoKey", "pinfl", "passport", "salary", "deviceHealth", "birthDate", "address"];

    private static string[] JamoaYollari(Guid azoId)
    {
        var (yil, oy) = BuOy();

        return
        [
            "/api/me/team",
            "/api/me/team/board",
            $"/api/me/team/members/{azoId}/attendance?year={yil}&month={oy}",
            $"/api/me/team/members/{azoId}/photo",
        ];
    }

    private static Uri Yol(string yol) => new(yol, UriKind.Relative);

    private static string Iso(DateOnly sana) => sana.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateOnly BugunToshkentda()
        => DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(Tashkent).DateTime);

    private static (int Yil, int Oy) BuOy()
    {
        var bugun = BugunToshkentda();
        return (bugun.Year, bugun.Month);
    }

    /// <summary>Takrorlanmas 9 xonali raqam — operator kodi 0/1 bilan boshlanmaydi.</summary>
    private static string TelefonRaqami()
        => "9" + Random.Shared.Next(10_000_000, 99_999_999).ToString(CultureInfo.InvariantCulture);

    private static IEnumerable<Guid> Odamlar(JsonElement panel)
        => panel.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("employeeId").GetGuid());

    private static Dictionary<Guid, string> Holatlar(JsonElement panel)
        => panel.GetProperty("items").EnumerateArray().ToDictionary(
            item => item.GetProperty("employeeId").GetGuid(),
            item => item.GetProperty("status").GetString()!);

    /// <summary>JSON dagi BARCHA kalitlar — ichma-ich obyekt va massivlar bilan.</summary>
    private static IEnumerable<string> Kalitlar(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    yield return property.Name;

                    foreach (var ichki in Kalitlar(property.Value))
                    {
                        yield return ichki;
                    }
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var ichki in Kalitlar(item))
                    {
                        yield return ichki;
                    }
                }

                break;
        }
    }

    private static async Task<JsonElement> PanelAsync(HttpClient ilova, string sorov)
    {
        using var javob = await ilova.GetAsync(Yol("/api/me/team/board?" + sorov), Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        return await JsonAsync(javob);
    }

    private static async Task<JsonElement> MeAsync(HttpClient ilova)
    {
        using var javob = await ilova.GetAsync(Yol("/api/me"), Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        return await JsonAsync(javob);
    }

    private static async Task<JsonElement> KartaAsync(HttpClient admin, Guid xodimId)
    {
        using var javob = await admin.GetAsync(Yol($"/api/employees/{xodimId}"), Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        return await JsonAsync(javob);
    }

    private static Task<HttpResponseMessage> BolimniSaqlaAsync(HttpClient admin, Guid bolimId, string nom, Guid? boshliqId)
        => admin.PutAsJsonAsync(Yol($"/api/catalog/departments/{bolimId}"), new { name = nom, headEmployeeId = boshliqId }, Ct);

    private static async Task RasmYuklaAsync(HttpClient admin, Guid xodimId)
    {
        using var forma = new MultipartFormDataContent();
        using var qism = new ByteArrayContent(TestImages.PngYasa(40, 40));
        qism.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        forma.Add(qism, "file", "surat.png");

        using var javob = await admin.PostAsync(Yol($"/api/employees/{xodimId}/photo"), forma, Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
    }

    /// <summary>Kecha ishdan ketgan — ishdan ketish SANA bo'yicha kuchga kiradi.</summary>
    private async Task IshdanBoshatAsync(Guid xodimId)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var munosabat = await kontekst.Employments.SingleAsync(row => row.EmployeeId == xodimId, Ct);
        munosabat.Terminate(BugunToshkentda().AddDays(-1), reasonId: null).IsSuccess.ShouldBeTrue();

        await kontekst.SaveChangesAsync(Ct);
    }

    /// <summary>Xodimni yumshoq o'chiradi.</summary>
    private async Task XodimniOchirAsync(Guid xodimId)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        kontekst.Remove(await kontekst.Employees.SingleAsync(row => row.Id == xodimId, Ct));
        await kontekst.SaveChangesAsync(Ct);
    }

    /// <summary>HR «Ilova» bo'limini ochadi (hisob kafolatlanadi), parolni ko'radi va kiradi.</summary>
    private async Task<HttpClient> IlovaMijoziAsync(HttpClient admin, Guid xodimId)
    {
        string login;

        using (var bolim = await admin.GetAsync(Yol($"/api/employees/{xodimId}/app-access"), Ct))
        {
            bolim.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(bolim));
            login = (await JsonAsync(bolim)).GetProperty("login").GetString()!;
        }

        string parol;

        using (var javob = await admin.GetAsync(Yol($"/api/employees/{xodimId}/app-access/password"), Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
            parol = (await JsonAsync(javob)).GetProperty("password").GetString()!;
        }

        using var mijoz = _factory.CreateClient();
        using var kirish = await mijoz.PostAsJsonAsync(Yol("/api/employee-auth/login"), new { login, password = parol }, Ct);
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
            Yol("/api/auth/login"),
            new { login = BonviApiFactory.TestAdminLogin, password = BonviApiFactory.TestAdminPassword },
            Ct);

        javob.EnsureSuccessStatusCode();

        var auth = await javob.Content.ReadFromJsonAsync<AuthResponse>(Ct);
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
        => await javob.Content.ReadAsStringAsync(Ct);
}
