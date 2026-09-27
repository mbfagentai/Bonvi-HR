using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Xodim ilovasi — autentifikatsiya: majburiy parol, blok, sessiya yangilash va chiqish.
/// </summary>
/// <remarks>
/// Audit topilmalari (2026-09-27) bo'yicha regressiya testlari: har biri bitta
/// topilmaning oldingi xulqini aniq yiqitadi.
/// </remarks>
public sealed partial class EmployeeAppApiTests
{
    // ════════════════════════════════════════════════════════════
    //  Majburiy parol — server tomonida
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Bir martalik parol bilan olingan token <c>/api/me/*</c> ga O'TMAYDI — faqat parolni almashtirish ishlaydi.
    /// </summary>
    /// <remarks>
    /// Ilgari qoida faqat ilovada edi: HR bergan parol belgilash, davomat va
    /// so'rovlar uchun cheksiz ishlab turardi.
    /// </remarks>
    [Fact]
    public async Task Bir_martalik_parol_bilan_faqat_parol_almashtirish_ochiq()
    {
        var ct = TestContext.Current.CancellationToken;
        var belgi = Belgi();
        using var admin = await AdminMijozAsync();

        var xodim = await XodimAsync(belgi);
        await BelgilashniYoqAsync(admin, xodim.Id);
        var markaz = Nuqta(20);
        await ObyektAsync(admin, "Parol " + belgi, markaz, radius: 300);

        var (login, birMartalik) = await HisobVaLoginAsync(admin, xodim.Id);

        using var kirish = await KirishAsync(login, birMartalik);
        kirish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(kirish));
        var juftlik = await JsonAsync(kirish);
        juftlik.GetProperty("mustChangePassword").GetBoolean().ShouldBeTrue();

        using var mijoz = Mijoz(juftlik.GetProperty("accessToken").GetString()!);

        foreach (var yol in XodimYollari)
        {
            using var javob = await mijoz.GetAsync(new Uri(yol, UriKind.Relative), ct);
            await XatoKutAsync(javob, HttpStatusCode.Forbidden, "xodim.parol_almashtirilsin");
        }

        using (var belgilash = await BelgilaAsync(mijoz, "In", markaz))
        {
            await XatoKutAsync(belgilash, HttpStatusCode.Forbidden, "xodim.parol_almashtirilsin");
        }

        await OtishYoqAsync(xodim.Id);

        // Joriy parol MAJBURIY — bo'sh yuborilsa (ilova ilgari shunday qilardi) rad etiladi.
        using (var boshParol = await mijoz.PostAsJsonAsync(
                   new Uri("/api/employee-auth/password", UriKind.Relative),
                   new { currentPassword = "", newPassword = YangiParol },
                   ct))
        {
            await XatoKutAsync(boshParol, HttpStatusCode.BadRequest, "xodim.parol_notogri");
        }

        using var almashtirish = await mijoz.PostAsJsonAsync(
            new Uri("/api/employee-auth/password", UriKind.Relative),
            new { currentPassword = birMartalik, newPassword = YangiParol },
            ct);

        almashtirish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(almashtirish));
        var yangi = await JsonAsync(almashtirish);
        yangi.GetProperty("mustChangePassword").GetBoolean().ShouldBeFalse();
        yangi.GetProperty("refreshToken").GetString().ShouldNotBeNullOrWhiteSpace();

        using var keyin = Mijoz(yangi.GetProperty("accessToken").GetString()!);
        using var men = await keyin.GetAsync(new Uri("/api/me", UriKind.Relative), ct);
        men.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(men));
        (await JsonAsync(men)).GetProperty("mustChangePassword").GetBoolean().ShouldBeFalse();

        // Almashtirish javobidagi yangilash tokeni ishlaydi — ilova aynan shuni saqlaydi.
        using var yangilash = await YangilaAsync(yangi.GetProperty("refreshToken").GetString()!);
        yangilash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yangilash));
    }

    /// <summary>
    /// HR parolni tiklasa, ESKI (bayroqsiz) token ham darhol to'xtaydi — bayroq bazadan o'qiladi.
    /// </summary>
    [Fact]
    public async Task HR_parolni_tiklasa_eski_token_ham_toxtaydi()
    {
        var ct = TestContext.Current.CancellationToken;
        var belgi = Belgi();
        using var admin = await AdminMijozAsync();

        var xodim = await XodimAsync(belgi);
        using var ilova = await IlovaMijoziAsync(admin, xodim.Id);

        using (var oldin = await ilova.GetAsync(new Uri("/api/me", UriKind.Relative), ct))
        {
            oldin.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(oldin));
        }

        using (var tiklash = await admin.PostAsync(
                   new Uri($"/api/employees/{xodim.Id}/app-access/reset-password", UriKind.Relative),
                   null,
                   ct))
        {
            tiklash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(tiklash));
        }

        // Access token hali 15 daqiqa amal qiladi — lekin endi o'tmaydi.
        using var keyin = await ilova.GetAsync(new Uri("/api/me/attendance?year=2026&month=9", UriKind.Relative), ct);
        await XatoKutAsync(keyin, HttpStatusCode.Forbidden, "xodim.parol_almashtirilsin");
    }

    // ════════════════════════════════════════════════════════════
    //  Blok: hisob borligini oshkor qilmaydi
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Notanish raqam ham mavjud hisob kabi bloklanadi; blok davomida TO'G'RI parol ham o'tmaydi.
    /// </summary>
    /// <remarks>
    /// Ilgari: notanish raqam hech qachon bloklanmasdi (javoblar farqi hisob
    /// borligini aytib qo'yardi), mavjud hisob esa HAMMA JOYDA qulflanardi.
    /// IP bo'yicha ajratish ilova testlarida (<c>EmployeeLoginCommandHandlerTests</c>).
    /// </remarks>
    [Fact]
    public async Task Blok_notanish_va_mavjud_raqamda_bir_xil()
    {
        var belgi = Belgi();
        using var admin = await AdminMijozAsync();

        var xodim = await XodimAsync(belgi);
        var (login, parol) = await HisobVaLoginAsync(admin, xodim.Id);

        var notanish = TelefonRaqami();

        for (var i = 0; i < 5; i++)
        {
            using var mavjud = await KirishAsync(login, "notogri-" + i);
            await XatoKutAsync(mavjud, HttpStatusCode.BadRequest, "xodim.notogri_malumot");

            using var yoq = await KirishAsync(notanish, "notogri-" + i);
            await XatoKutAsync(yoq, HttpStatusCode.BadRequest, "xodim.notogri_malumot");
        }

        using var mavjudBlok = await KirishAsync(login, "yana-notogri");
        await XatoKutAsync(mavjudBlok, HttpStatusCode.Forbidden, "xodim.bloklangan");

        using var notanishBlok = await KirishAsync(notanish, "yana-notogri");
        await XatoKutAsync(notanishBlok, HttpStatusCode.Forbidden, "xodim.bloklangan");

        // Blok davomida parol tekshirilmaydi — aks holda «to'g'ri» javob oracle bo'lardi.
        using var togri = await KirishAsync(login, parol);
        await XatoKutAsync(togri, HttpStatusCode.Forbidden, "xodim.bloklangan");
        (await JsonAsync(togri)).GetProperty("retryAfterMinutes").GetInt32().ShouldBeInRange(1, 15);

        // Hisob qatori tegilmagan: blok hisobda EMAS.
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
        var hisob = await kontekst.EmployeeAccounts.AsNoTracking()
            .SingleAsync(row => row.EmployeeId == xodim.Id, TestContext.Current.CancellationToken);

        hisob.LockedUntil.ShouldBeNull();
        hisob.FailedLoginAttempts.ShouldBe(0);
    }

    /// <summary>Noto'g'ri JORIY parol sanaladi — beshtadan keyin almashtirish bloklanadi.</summary>
    [Fact]
    public async Task Parol_almashtirishda_notogri_joriy_parol_bloklaydi()
    {
        var ct = TestContext.Current.CancellationToken;
        var belgi = Belgi();
        using var admin = await AdminMijozAsync();

        var xodim = await XodimAsync(belgi);
        using var ilova = await IlovaMijoziAsync(admin, xodim.Id);

        for (var i = 0; i < 5; i++)
        {
            using var notogri = await ilova.PostAsJsonAsync(
                new Uri("/api/employee-auth/password", UriKind.Relative),
                new { currentPassword = "taxmin-" + i, newPassword = "Boshqa_parol_2026" },
                ct);

            await XatoKutAsync(notogri, HttpStatusCode.BadRequest, "xodim.parol_notogri");
        }

        using var togri = await ilova.PostAsJsonAsync(
            new Uri("/api/employee-auth/password", UriKind.Relative),
            new { currentPassword = YangiParol, newPassword = "Boshqa_parol_2026" },
            ct);

        await XatoKutAsync(togri, HttpStatusCode.Forbidden, "xodim.bloklangan");
    }

    // ════════════════════════════════════════════════════════════
    //  Sessiya: yo'qolgan javob va chiqish
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Javobi yo'qolgan yangilash qayta yuborilsa — AYNAN o'sha juftlik; o'rinbosar ishlatilgach — o'g'irlik.
    /// </summary>
    [Fact]
    public async Task Yangilash_javobi_yoqolsa_osha_juftlik_qaytadi()
    {
        var belgi = Belgi();
        using var admin = await AdminMijozAsync();

        var xodim = await XodimAsync(belgi);
        var birinchi = await ParolOrnatilganJuftlikAsync(admin, xodim.Id);

        using var yangilash = await YangilaAsync(birinchi);
        yangilash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yangilash));
        var ikkinchi = await JsonAsync(yangilash);

        // Javob «yo'qoldi» — ilova ESKI token bilan qayta so'raydi.
        using var takror = await YangilaAsync(birinchi);
        takror.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(takror));
        var takrorJuftlik = await JsonAsync(takror);

        takrorJuftlik.GetProperty("refreshToken").GetString()
            .ShouldBe(ikkinchi.GetProperty("refreshToken").GetString(), "zanjir tarmoqlanmasligi kerak");
        takrorJuftlik.GetProperty("accessToken").GetString()
            .ShouldBe(ikkinchi.GetProperty("accessToken").GetString());

        // O'rinbosar ishlatiladi — endi eski tokenning qaytishi yo'qolgan javob emas.
        using var uchinchiJavob = await YangilaAsync(ikkinchi.GetProperty("refreshToken").GetString()!);
        uchinchiJavob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(uchinchiJavob));
        var uchinchi = await JsonAsync(uchinchiJavob);

        using var ogirlik = await YangilaAsync(birinchi);
        await XatoKutAsync(ogirlik, HttpStatusCode.Forbidden, "xodim.token_yaroqsiz");

        // O'g'irlik qoidasi o'z kuchida: hisobning hamma sessiyasi yopildi.
        using var yopilgan = await YangilaAsync(uchinchi.GetProperty("refreshToken").GetString()!);
        await XatoKutAsync(yopilgan, HttpStatusCode.Forbidden, "xodim.token_yaroqsiz");
    }

    /// <summary>Chiqish serverdagi sessiyani yopadi; javob har doim 204.</summary>
    [Fact]
    public async Task Chiqish_yangilash_tokenini_yopadi()
    {
        var ct = TestContext.Current.CancellationToken;
        var belgi = Belgi();
        using var admin = await AdminMijozAsync();

        var xodim = await XodimAsync(belgi);
        var token = await ParolOrnatilganJuftlikAsync(admin, xodim.Id);

        using var anonim = _factory.CreateClient();

        using (var chiqish = await anonim.PostAsJsonAsync(
                   new Uri("/api/employee-auth/logout", UriKind.Relative),
                   new { refreshToken = token },
                   ct))
        {
            chiqish.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(chiqish));
        }

        using var keyin = await YangilaAsync(token);
        await XatoKutAsync(keyin, HttpStatusCode.Forbidden, "xodim.token_yaroqsiz");

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
            var hisobId = await kontekst.EmployeeAccounts.AsNoTracking()
                .Where(row => row.EmployeeId == xodim.Id)
                .Select(row => row.Id)
                .SingleAsync(ct);

            (await kontekst.EmployeeRefreshTokens.AsNoTracking()
                .AnyAsync(row => row.EmployeeAccountId == hisobId && row.RevokedAt == null, ct))
                .ShouldBeFalse("chiqilgan sessiya serverda tirik qolmasligi kerak");
        }

        // Noma'lum yoki bo'sh token — xuddi o'sha javob (holat oracle'i yo'q).
        foreach (object tana in new object[] { new { refreshToken = "yoq-" + belgi }, new { refreshToken = "" } })
        {
            using var javob = await anonim.PostAsJsonAsync(new Uri("/api/employee-auth/logout", UriKind.Relative), tana, ct);
            javob.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(javob));
        }
    }

    // ════════════════════════════════════════════════════════════
    //  O'z rasmi — /api/me/photo
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Xodim FAQAT o'z rasmini oladi; rasmsiz xodim — 404; HR tokeni bu yo'lga o'tmaydi.
    /// </summary>
    [Fact]
    public async Task Xodim_faqat_oz_rasmini_oladi()
    {
        var ct = TestContext.Current.CancellationToken;
        var belgi = Belgi();
        using var admin = await AdminMijozAsync();

        var rasmli = await XodimAsync(belgi + "r");
        var rasmsiz = await XodimAsync(belgi + "s");

        using (var forma = new MultipartFormDataContent())
        using (var qism = new ByteArrayContent(TestImages.PngYasa(40, 40)))
        {
            qism.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            forma.Add(qism, "file", "surat.png");

            using var yuklash = await admin.PostAsync(
                new Uri($"/api/employees/{rasmli.Id}/photo", UriKind.Relative),
                forma,
                ct);

            yuklash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yuklash));
        }

        using var rasmliIlova = await IlovaMijoziAsync(admin, rasmli.Id);
        using var rasmsizIlova = await IlovaMijoziAsync(admin, rasmsiz.Id);

        byte[] ozRasmi;
        using (var javob = await rasmliIlova.GetAsync(new Uri("/api/me/photo", UriKind.Relative), ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
            javob.Content.Headers.ContentType!.MediaType.ShouldBe("image/jpeg");
            javob.Headers.CacheControl!.Private.ShouldBeTrue("surat — shaxsiy ma'lumot");
            ozRasmi = await javob.Content.ReadAsByteArrayAsync(ct);
        }

        using (var hrNusxa = await admin.GetAsync(new Uri($"/api/employees/{rasmli.Id}/photo", UriKind.Relative), ct))
        {
            (await hrNusxa.Content.ReadAsByteArrayAsync(ct)).ShouldBe(ozRasmi, "aynan o'sha fayl");
        }

        // B xodim A ning rasmini ololmaydi — yo'lda identifikator yo'q, u o'zinikini so'raydi.
        using (var begona = await rasmsizIlova.GetAsync(new Uri("/api/me/photo", UriKind.Relative), ct))
        {
            await XatoKutAsync(begona, HttpStatusCode.NotFound, "xodim.rasm_yoq");
        }

        using (var identifikatorBilan = await rasmsizIlova.GetAsync(
                   new Uri($"/api/me/photo?employeeId={rasmli.Id}", UriKind.Relative),
                   ct))
        {
            await XatoKutAsync(identifikatorBilan, HttpStatusCode.NotFound, "xodim.rasm_yoq");
        }

        // HR tokeni — hatto administratorniki — xodim yo'liga o'tmaydi.
        using var hr = await admin.GetAsync(new Uri("/api/me/photo", UriKind.Relative), ct);
        hr.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ════════════════════════════════════════════════════════════
    //  Yordamchilar
    // ════════════════════════════════════════════════════════════

    /// <summary>Hisob ochadi — login va BIR MARTALIK parol.</summary>
    private static async Task<(string Login, string Parol)> HisobVaLoginAsync(HttpClient admin, Guid xodimId)
    {
        var parol = await HisobOchAsync(admin, xodimId);

        using var karta = await admin.GetAsync(
            new Uri($"/api/employees/{xodimId}/app-access", UriKind.Relative),
            TestContext.Current.CancellationToken);

        var login = (await JsonAsync(karta)).GetProperty("account").GetProperty("login").GetString()!;

        return (login, parol);
    }

    /// <summary>Kiradi, parolni o'rnatadi va javobdagi YANGILASH tokenini qaytaradi.</summary>
    private async Task<string> ParolOrnatilganJuftlikAsync(HttpClient admin, Guid xodimId)
    {
        var (login, parol) = await HisobVaLoginAsync(admin, xodimId);

        using var kirish = await KirishAsync(login, parol);
        kirish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(kirish));

        using var mijoz = Mijoz((await JsonAsync(kirish)).GetProperty("accessToken").GetString()!);

        using var almashtirish = await mijoz.PostAsJsonAsync(
            new Uri("/api/employee-auth/password", UriKind.Relative),
            new { currentPassword = parol, newPassword = YangiParol },
            TestContext.Current.CancellationToken);

        almashtirish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(almashtirish));

        return (await JsonAsync(almashtirish)).GetProperty("refreshToken").GetString()!;
    }

    private async Task<HttpResponseMessage> YangilaAsync(string refreshToken)
        => await _factory.CreateClient().PostAsJsonAsync(
            new Uri("/api/employee-auth/refresh", UriKind.Relative),
            new { refreshToken },
            TestContext.Current.CancellationToken);

    private HttpClient Mijoz(string accessToken)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }
}
