using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bonvi.Application.Identity.Abstractions;
using Bonvi.Application.Identity.EmployeeAccounts;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Xodim ilovasi — hisob AVTOMATIK, login PROFIL telefoni, parol HR da (egasining qarori, 2026-09-27).
/// </summary>
/// <remarks>
/// <para>
/// Qo'lda «hisob ochish», bir martalik parol va «hisobni yopish» testlari shu
/// qarordan keyin bu yerdagilar bilan ALMASHTIRILDI: fon vazifasi va
/// «Ilova» bo'limi hisobni o'zi ochadi, login — profildagi asosiy telefon
/// (tahrir shu zahoti kuchga kiradi), parol — 6 raqam va HR uni ko'radi,
/// yagona o'chirgich — ishdan ketish.
/// </para>
/// <para>
/// ⚠️ Sinf testlari bitta bazada KETMA-KET yuradi (bitta xUnit to'plami),
/// ya'ni vazifaning «ikkinchi yurishda hech narsa qilmaydi» degan
/// tekshiruvi boshqa testning xodimiga to'qnashmaydi.
/// </para>
/// </remarks>
public sealed partial class EmployeeAppApiTests
{
    // ════════════════════════════════════════════════════════════
    //  Hisob avtomatik ochiladi
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Vazifa hisobi yo'q har bir o'chirilmagan xodimga hisob ochadi; o'chirilganni tashlaydi; takrorda hech narsa qilmaydi.
    /// </summary>
    [Fact]
    public async Task Vazifa_hisobsiz_xodimlarga_hisob_ochadi_va_idempotent()
    {
        var ct = TestContext.Current.CancellationToken;
        var belgi = Belgi();

        var telefonli = await XodimAsync(belgi + "a");
        var raqamsiz = await XodimTelefonBilanAsync(belgi + "b", telefon: null);
        var ochirilgan = await XodimAsync(belgi + "c");

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
            kontekst.Remove(await kontekst.Employees.SingleAsync(row => row.Id == ochirilgan.Id, ct));
            await kontekst.SaveChangesAsync(ct);
        }

        (await HisoblarAsync(telefonli.Id, raqamsiz.Id, ochirilgan.Id)).ShouldBeEmpty("hali hech kimda hisob yo'q");

        var birinchi = await VazifaAsync();
        birinchi.ShouldBeGreaterThanOrEqualTo(2);

        var oldin = await HisoblarAsync(telefonli.Id, raqamsiz.Id, ochirilgan.Id);

        oldin.Keys.ShouldBe([telefonli.Id, raqamsiz.Id], ignoreOrder: true, "o'chirilgan xodimga hisob ochilmaydi");
        oldin.Values.ShouldAllBe(hisob => hisob.PasswordProtected != null && hisob.PasswordHash != string.Empty);

        // Ikkinchi yurish — hech narsa: yangi hisob ham, parol almashishi ham yo'q.
        (await VazifaAsync()).ShouldBe(0);

        var keyin = await HisoblarAsync(telefonli.Id, raqamsiz.Id, ochirilgan.Id);

        foreach (var (xodimId, hisob) in oldin)
        {
            keyin[xodimId].Id.ShouldBe(hisob.Id);
            keyin[xodimId].PasswordHash.ShouldBe(hisob.PasswordHash);
            keyin[xodimId].PasswordSetAt.ShouldBe(hisob.PasswordSetAt);
        }

        // Vazifa ochgan parol bilan — hech qanday qo'shimcha qadamsiz — kiriladi.
        using var admin = await AdminMijozAsync();
        using var kirish = await KirishAsync(telefonli.Login, await ParolAsync(admin, telefonli.Id));
        kirish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(kirish));
    }

    /// <summary>HR «Ilova» bo'limini ochgan lahzada hisob paydo bo'ladi (vazifani kutmasdan).</summary>
    [Fact]
    public async Task Ilova_bolimi_ochilganda_hisob_shu_zahoti_ochiladi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();

        var xodim = await XodimAsync(Belgi());
        (await HisoblarAsync(xodim.Id)).ShouldBeEmpty();

        using var javob = await admin.GetAsync(new Uri($"/api/employees/{xodim.Id}/app-access", UriKind.Relative), ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var bolim = await JsonAsync(javob);
        bolim.GetProperty("login").GetString().ShouldBe("+998" + xodim.Login);
        bolim.GetProperty("loginIssue").GetString().ShouldBe("None");
        bolim.GetProperty("sharedWith").GetArrayLength().ShouldBe(0);
        bolim.GetProperty("mobileCheckInAllowed").GetBoolean().ShouldBeFalse("sukut bo'yicha o'chiq");
        bolim.GetProperty("account").GetProperty("passwordAvailable").GetBoolean().ShouldBeTrue();
        bolim.GetProperty("account").TryGetProperty("password", out _).ShouldBeFalse("parol bo'lim javobida YO'Q");

        var hisob = (await HisoblarAsync(xodim.Id)).ShouldHaveSingleItem().Value;
        bolim.GetProperty("account").GetProperty("id").GetGuid().ShouldBe(hisob.Id);

        // Takror ochish — o'sha hisob, yangi parol yo'q.
        using var takror = await admin.GetAsync(new Uri($"/api/employees/{xodim.Id}/app-access", UriKind.Relative), ct);
        (await JsonAsync(takror)).GetProperty("account").GetProperty("id").GetGuid().ShouldBe(hisob.Id);
        (await HisoblarAsync(xodim.Id))[xodim.Id].PasswordHash.ShouldBe(hisob.PasswordHash);
    }

    // ════════════════════════════════════════════════════════════
    //  Login — profildagi asosiy telefon
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Profil telefoni + HR ko'rgan 6 xonali parol bilan kiriladi; profilda raqam almashsa — yangisi SHU ZAHOTI, eskisi yo'q.
    /// </summary>
    [Fact]
    public async Task Profil_telefoni_bilan_kiriladi_va_raqam_almashsa_darhol_yangisi_ishlaydi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();

        var xodim = await XodimAsync(Belgi());
        var parol = await ParolAsync(admin, xodim.Id);

        parol.ShouldMatch("^[0-9]{6}$");
        EmployeePassword.IsTriviallyWeak(parol).ShouldBeFalse();

        using (var kirish = await KirishAsync(xodim.Login, parol))
        {
            kirish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(kirish));
        }

        // Boshqa yozilish shakli — o'sha raqam.
        using (var shakl = await KirishAsync("+998 " + xodim.Login, parol))
        {
            shakl.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(shakl));
        }

        var yangiRaqam = TelefonRaqami();
        await TelefonniAlmashtirAsync(xodim.Id, yangiRaqam);

        using (var eski = await KirishAsync(xodim.Login, parol))
        {
            await XatoKutAsync(eski, HttpStatusCode.BadRequest, "xodim.notogri_malumot");
        }

        using (var yangi = await KirishAsync(yangiRaqam, parol))
        {
            yangi.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yangi));
        }

        using var bolim = await admin.GetAsync(new Uri($"/api/employees/{xodim.Id}/app-access", UriKind.Relative), ct);
        (await JsonAsync(bolim)).GetProperty("login").GetString().ShouldBe("+998" + yangiRaqam);
    }

    /// <summary>
    /// Raqam ikki o'chirilmagan xodimda — hech biri kira olmaydi, sababi va ikkinchi xodim bo'limda ko'rinadi.
    /// </summary>
    [Fact]
    public async Task Bir_xil_raqamli_xodimlar_kira_olmaydi_va_sababi_bolimda_korinadi()
    {
        var ct = TestContext.Current.CancellationToken;
        var belgi = Belgi();
        using var admin = await AdminMijozAsync();

        var telefon = TelefonRaqami();
        var birinchi = await XodimTelefonBilanAsync(belgi + "a", telefon);
        var ikkinchi = await XodimTelefonBilanAsync(belgi + "b", telefon);

        var parol = await ParolAsync(admin, birinchi.Id);

        // TO'G'RI parol bilan ham — umumiy javob (oracle yo'q).
        using (var rad = await KirishAsync(telefon, parol))
        {
            await XatoKutAsync(rad, HttpStatusCode.BadRequest, "xodim.notogri_malumot");
        }

        using (var javob = await admin.GetAsync(new Uri($"/api/employees/{birinchi.Id}/app-access", UriKind.Relative), ct))
        {
            var bolim = await JsonAsync(javob);
            bolim.GetProperty("loginIssue").GetString().ShouldBe("SharedPhone");

            var boshqa = bolim.GetProperty("sharedWith").EnumerateArray().ShouldHaveSingleItem();
            boshqa.GetProperty("employeeId").GetGuid().ShouldBe(ikkinchi.Id);
            boshqa.GetProperty("fullName").GetString()!.ShouldContain(belgi + "b");
        }

        // HR profilni tuzatadi (takror yozuv o'chiriladi) — kirish o'zi ishlab ketadi.
        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
            kontekst.Remove(await kontekst.Employees.SingleAsync(row => row.Id == ikkinchi.Id, ct));
            await kontekst.SaveChangesAsync(ct);
        }

        using (var kirish = await KirishAsync(telefon, parol))
        {
            kirish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(kirish));
        }

        using var keyin = await admin.GetAsync(new Uri($"/api/employees/{birinchi.Id}/app-access", UriKind.Relative), ct);
        (await JsonAsync(keyin)).GetProperty("loginIssue").GetString().ShouldBe("None");
    }

    /// <summary>Profilda raqam yo'q — sababi bo'limda; hisob va parol baribir bor.</summary>
    [Fact]
    public async Task Raqamsiz_xodimda_sababi_korinadi_parol_esa_bor()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();

        var xodim = await XodimTelefonBilanAsync(Belgi(), telefon: null);

        using var javob = await admin.GetAsync(new Uri($"/api/employees/{xodim.Id}/app-access", UriKind.Relative), ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var bolim = await JsonAsync(javob);
        bolim.GetProperty("login").ValueKind.ShouldBe(JsonValueKind.Null);
        bolim.GetProperty("loginIssue").GetString().ShouldBe("NoPhone");
        bolim.GetProperty("account").GetProperty("passwordAvailable").GetBoolean().ShouldBeTrue();

        (await ParolAsync(admin, xodim.Id)).ShouldMatch("^[0-9]{6}$");
    }

    // ════════════════════════════════════════════════════════════
    //  Parol — HR yozadi yoki yangisini yasaydi
    // ════════════════════════════════════════════════════════════

    /// <summary>HR yozgan parol tekshiriladi, saqlanadi, ko'rinadi va ishlaydi; «yangi parol» uni almashtiradi.</summary>
    [Fact]
    public async Task HR_oz_parolini_qoyadi_va_yangisini_yasaydi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();

        var xodim = await XodimAsync(Belgi());
        var yol = new Uri($"/api/employees/{xodim.Id}/app-access/password", UriKind.Relative);

        foreach (var (qiymat, kod) in new[]
                 {
                     ("", "xodim.parol_bosh"),
                     ("      ", "xodim.parol_bosh"),
                     ("12345", "xodim.parol_qisqa"),
                     (new string('a', EmployeePassword.MaxLength + 1), "xodim.parol_uzun"),
                 })
        {
            using var rad = await admin.PutAsJsonAsync(yol, new { password = qiymat }, ct);
            await XatoKutAsync(rad, HttpStatusCode.BadRequest, kod);
        }

        using (var qoyish = await admin.PutAsJsonAsync(yol, new { password = "  Bahor2026 " }, ct))
        {
            qoyish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qoyish));
            (await JsonAsync(qoyish)).GetProperty("account").GetProperty("passwordAvailable").GetBoolean().ShouldBeTrue();
        }

        // Chetdagi bo'shliqlarsiz saqlangan — HR ko'rgani AYNAN ishlaydigan qiymat.
        (await ParolAsync(admin, xodim.Id)).ShouldBe("Bahor2026");

        using (var kirish = await KirishAsync(xodim.Login, "Bahor2026"))
        {
            kirish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(kirish));
        }

        using (var yasash = await admin.PostAsync(
                   new Uri($"/api/employees/{xodim.Id}/app-access/password/generate", UriKind.Relative),
                   null,
                   ct))
        {
            yasash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yasash));
        }

        var yangi = await ParolAsync(admin, xodim.Id);
        yangi.ShouldMatch("^[0-9]{6}$");

        using (var eski = await KirishAsync(xodim.Login, "Bahor2026"))
        {
            await XatoKutAsync(eski, HttpStatusCode.BadRequest, "xodim.notogri_malumot");
        }

        using var yangiBilan = await KirishAsync(xodim.Login, yangi);
        yangiBilan.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yangiBilan));
    }

    /// <summary>Shifrlangan nusxa yo'q (eski parol) — ko'rsatib bo'lmaydi, «yangi parol» yechadi.</summary>
    [Fact]
    public async Task Nusxasi_yoq_parol_korinmaydi_va_yangi_parol_yechadi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();

        var xodim = await XodimAsync(Belgi());

        // Bu qaror oldidan berilgan parol: faqat xesh bor.
        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
            var xesh = qamrov.ServiceProvider.GetRequiredService<IPasswordHasher>().Hash("eski-parol");
            kontekst.EmployeeAccounts.Add(EmployeeAccount.Issue(xodim.Id, xesh, null, DateTimeOffset.UtcNow).Value);
            await kontekst.SaveChangesAsync(ct);
        }

        using (var javob = await admin.GetAsync(new Uri($"/api/employees/{xodim.Id}/app-access", UriKind.Relative), ct))
        {
            (await JsonAsync(javob)).GetProperty("account").GetProperty("passwordAvailable").GetBoolean().ShouldBeFalse();
        }

        using (var korish = await admin.GetAsync(new Uri($"/api/employees/{xodim.Id}/app-access/password", UriKind.Relative), ct))
        {
            await XatoKutAsync(korish, HttpStatusCode.Conflict, "xodim.parol_korinmaydi");
        }

        // Eski parol baribir ishlaydi — ko'rinmasligi uni buzmaydi.
        using (var eski = await KirishAsync(xodim.Login, "eski-parol"))
        {
            eski.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(eski));
        }

        using (var yasash = await admin.PostAsync(
                   new Uri($"/api/employees/{xodim.Id}/app-access/password/generate", UriKind.Relative),
                   null,
                   ct))
        {
            (await JsonAsync(yasash)).GetProperty("account").GetProperty("passwordAvailable").GetBoolean().ShouldBeTrue();
        }

        (await ParolAsync(admin, xodim.Id)).ShouldMatch("^[0-9]{6}$");
    }

    // ════════════════════════════════════════════════════════════
    //  RBAC
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// «Ilova» bo'limi, parolni ko'rish, yasash va qo'yish — faqat <c>attendance.manage</c>; xodim tokeni o'tmaydi.
    /// </summary>
    /// <remarks>
    /// Xodim tokeni HR endpointida 401 oladi, 403 emas: u HR sxemasida umuman
    /// qabul qilinmaydi (auditoriya boshqa) — <see cref="Xodim_tokeni_HR_endpointiga_otmaydi"/>
    /// bilan bir xil qoida.
    /// </remarks>
    [Fact]
    public async Task Ilova_bolimi_va_parol_faqat_attendance_manage_ga()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();

        var xodim = await XodimAsync(Belgi());
        var parol = await ParolAsync(admin, xodim.Id);

        using var oquvchi = await RuxsatliMijozAsync(Permissions.AttendanceView, Permissions.EmployeesView);
        using var ilova = await IlovaMijoziAsync(admin, xodim.Id);

        foreach (var (mijoz, kutilgan) in new[] { (oquvchi, HttpStatusCode.Forbidden), (ilova, HttpStatusCode.Unauthorized) })
        {
            foreach (var sorov in HrIlovaSorovlari(xodim.Id))
            {
                using var javob = await mijoz.SendAsync(sorov, ct);
                javob.StatusCode.ShouldBe(kutilgan, $"{sorov.Method} {sorov.RequestUri}");
            }
        }

        // Hech narsa o'zgarmagan: parol o'sha, o'chirgich o'chiq.
        (await ParolAsync(admin, xodim.Id)).ShouldBe(parol);

        // Faqat `attendance.manage` bilan — yetarli.
        using var boshqaruvchi = await RuxsatliMijozAsync(Permissions.AttendanceManage);

        foreach (var sorov in HrIlovaSorovlari(xodim.Id))
        {
            using var javob = await boshqaruvchi.SendAsync(sorov, ct);
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, $"{sorov.Method} {sorov.RequestUri}: {await MatnAsync(javob)}");
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Yagona o'chirgich — ishdan ketish
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Ishdan bo'shatilgan odamning TIRIK tokeni ham darhol to'xtaydi; qayta ishga olinsa — o'sha parol bilan yana ishlaydi.
    /// </summary>
    [Fact]
    public async Task Ishdan_ketgan_xodimning_tokeni_darhol_toxtaydi_qayta_olinsa_ishlaydi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();

        var xodim = await XodimAsync(Belgi());
        var parol = await ParolAsync(admin, xodim.Id);
        var juftlik = await KirganJuftlikAsync(admin, xodim.Id);
        using var mijoz = Mijoz(juftlik.GetProperty("accessToken").GetString()!);

        var oy = new Uri("/api/me/attendance?year=2026&month=9", UriKind.Relative);

        using (var oldin = await mijoz.GetAsync(oy, ct))
        {
            oldin.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(oldin));
        }

        Guid firmaId;
        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
            var munosabat = await kontekst.Employments.SingleAsync(row => row.EmployeeId == xodim.Id, ct);
            munosabat.Terminate(BugunToshkentda().AddDays(-1), reasonId: null).IsSuccess.ShouldBeTrue();
            firmaId = munosabat.LegalEntityId;
            await kontekst.SaveChangesAsync(ct);
        }

        // Access token hali 15 daqiqa amal qiladi — lekin endi o'tmaydi.
        foreach (var yol in new[] { oy, new Uri("/api/me", UriKind.Relative), new Uri("/api/me/requests", UriKind.Relative) })
        {
            using var javob = await mijoz.GetAsync(yol, ct);
            await XatoKutAsync(javob, HttpStatusCode.Forbidden, "xodim.hisob_faol_emas");
        }

        using (var belgilash = await BelgilaAsync(mijoz, "In", Nuqta(30)))
        {
            await XatoKutAsync(belgilash, HttpStatusCode.Forbidden, "xodim.hisob_faol_emas");
        }

        using (var kirish = await KirishAsync(xodim.Login, parol))
        {
            await XatoKutAsync(kirish, HttpStatusCode.BadRequest, "xodim.notogri_malumot");
        }

        using (var yangilash = await YangilaAsync(juftlik.GetProperty("refreshToken").GetString()!))
        {
            await XatoKutAsync(yangilash, HttpStatusCode.Forbidden, "xodim.token_yaroqsiz");
        }

        // Qayta ishga olindi — hech qanday HR amalisiz, o'sha parol bilan.
        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
            kontekst.Add(Employment.Hire(xodim.Id, firmaId, BugunToshkentda()).Value);
            await kontekst.SaveChangesAsync(ct);
        }

        using (var keyin = await mijoz.GetAsync(oy, ct))
        {
            keyin.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(keyin));
        }

        using var qaytaKirish = await KirishAsync(xodim.Login, parol);
        qaytaKirish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qaytaKirish));
    }

    // ════════════════════════════════════════════════════════════
    //  Yordamchilar
    // ════════════════════════════════════════════════════════════

    /// <summary>HR «Ilova» bo'limining barcha endpointlari — har chaqiruvda yangi so'rovlar.</summary>
    private static IEnumerable<HttpRequestMessage> HrIlovaSorovlari(Guid xodimId)
    {
        var asos = $"/api/employees/{xodimId}";

        yield return new HttpRequestMessage(HttpMethod.Get, new Uri(asos + "/app-access", UriKind.Relative));
        yield return new HttpRequestMessage(HttpMethod.Get, new Uri(asos + "/app-access/password", UriKind.Relative));
        yield return new HttpRequestMessage(HttpMethod.Post, new Uri(asos + "/app-access/password/generate", UriKind.Relative));
        yield return new HttpRequestMessage(HttpMethod.Put, new Uri(asos + "/app-access/password", UriKind.Relative))
        {
            Content = JsonContent.Create(new { password = "Bahor2026" }),
        };
        yield return new HttpRequestMessage(HttpMethod.Put, new Uri(asos + "/mobile-check-in", UriKind.Relative))
        {
            Content = JsonContent.Create(new { allowed = false }),
        };
    }

    /// <summary>Xodim — berilgan raqam bilan (yoki raqamsiz) va bugun ishlaydi.</summary>
    private async Task<Xodim> XodimTelefonBilanAsync(string belgi, string? telefon)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var firma = LegalEntity.Create("Ilova MCHJ " + belgi).Value;
        var xodim = Employee.Create(FullName.Create("Qodirov", "Ilova", belgi).Value).Value;

        if (telefon is not null)
        {
            xodim.SetPhones(PhoneNumber.Create(telefon).Value);
        }

        kontekst.AddRange(firma, xodim);
        kontekst.Add(Employment.Hire(xodim.Id, firma.Id, new DateOnly(2025, 1, 1)).Value);
        await kontekst.SaveChangesAsync(ct);

        return new Xodim(xodim.Id, telefon ?? string.Empty);
    }

    /// <summary>Profildagi asosiy telefonni almashtiradi — HR kartani tahrirlagandagi domen amali.</summary>
    private async Task TelefonniAlmashtirAsync(Guid xodimId, string telefon)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var xodim = await kontekst.Employees.SingleAsync(row => row.Id == xodimId, ct);
        xodim.SetPhones(PhoneNumber.Create(telefon).Value);
        await kontekst.SaveChangesAsync(ct);
    }

    /// <summary>Berilgan xodimlarning hisoblari — xodim bo'yicha.</summary>
    private async Task<Dictionary<Guid, EmployeeAccount>> HisoblarAsync(params Guid[] xodimlar)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        return await kontekst.EmployeeAccounts.AsNoTracking()
            .Where(row => xodimlar.Contains(row.EmployeeId))
            .ToDictionaryAsync(row => row.EmployeeId, TestContext.Current.CancellationToken);
    }

    /// <summary>Fon vazifasini bir marta yuritadi (Testing muhitida Hangfire yo'q).</summary>
    private async Task<int> VazifaAsync()
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();

        return await qamrov.ServiceProvider
            .GetRequiredService<EmployeeAccountProvisioningJob>()
            .RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Faqat berilgan ruxsatlar bilan token olgan mijoz (<c>WorkSiteApiTests.MijozAsync</c> yo'li).</summary>
    // Ruxsatlar endi har so'rovda BAZADAN o'qiladi (2026-09-26, `TestAccounts` izohi):
    // administrator tokeniga yozilgan ro'yxat e'tiborsiz qolardi. Har chaqiruv o'z
    // rolini va hisobini yaratadi.
    private async Task<HttpClient> RuxsatliMijozAsync(params string[] ruxsatlar)
    {
        var mijoz = _factory.CreateClient();
        mijoz.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await TestAccounts.TokenAsync(_factory, ruxsatlar));

        return mijoz;
    }
}
