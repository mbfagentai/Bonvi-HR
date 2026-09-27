using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Xodim ilovasi — autentifikatsiya: parol faqat HR da, blok, sessiya yangilash va chiqish.
/// </summary>
/// <remarks>
/// Audit topilmalari (2026-09-27) bo'yicha regressiya testlari: har biri bitta
/// topilmaning oldingi xulqini aniq yiqitadi. Majburiy parol almashtirish
/// testlari egasining qaroridan keyin (2026-09-27: parolni faqat HR beradi)
/// «xodim parolni almashtira olmaydi» va «HR yangi parol bersa eski sessiya
/// to'xtaydi» testlari bilan ALMASHTIRILDI.
/// </remarks>
public sealed partial class EmployeeAppApiTests
{
    // ════════════════════════════════════════════════════════════
    //  Parol — faqat HR da
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Birinchi kirishdan keyin hamma narsa DARHOL ochiq; xodim parolni almashtira olmaydi (endpoint yo'q).
    /// </summary>
    /// <remarks>
    /// Ilgari HR bergan parol «bir martalik» edi: <c>/api/me/*</c> 403
    /// (<c>xodim.parol_almashtirilsin</c>) berardi va faqat
    /// <c>/api/employee-auth/password</c> ochiq edi. Egasining qarori
    /// (2026-09-27): parolni faqat HR beradi va ko'radi.
    /// </remarks>
    [Fact]
    public async Task Xodim_parolni_almashtira_olmaydi_va_birinchi_kirishdan_hammasi_ochiq()
    {
        var ct = TestContext.Current.CancellationToken;
        var belgi = Belgi();
        using var admin = await AdminMijozAsync();

        var xodim = await XodimAsync(belgi);
        await BelgilashniYoqAsync(admin, xodim.Id);
        var markaz = Nuqta(20);
        await ObyektAsync(admin, "Parol " + belgi, markaz, radius: 300);

        var parol = await ParolAsync(admin, xodim.Id);
        var juftlik = await KirganJuftlikAsync(admin, xodim.Id);

        juftlik.TryGetProperty("mustChangePassword", out _).ShouldBeFalse("majburiy almashtirish bayrog'i yo'q");

        using var mijoz = Mijoz(juftlik.GetProperty("accessToken").GetString()!);

        foreach (var yol in XodimYollari.Where(yol => yol != "/api/me/photo"))
        {
            using var javob = await mijoz.GetAsync(new Uri(yol, UriKind.Relative), ct);
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, $"{yol}: {await MatnAsync(javob)}");
        }

        using (var belgilash = await BelgilaAsync(mijoz, "In", markaz))
        {
            belgilash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(belgilash));
        }

        // Parolni almashtirish yo'li umuman YO'Q. Marshrutsiz so'rovga umumiy
        // (HR sxemasidagi) siyosat qo'llanadi: xodim tokeni u yerda 401 oladi,
        // HR tokeni esa marshrut yo'qligini — 404 ni — ko'radi.
        var tana = new { currentPassword = parol, newPassword = "Boshqa_parol_2026" };
        var parolYoli = new Uri("/api/employee-auth/password", UriKind.Relative);

        using (var almashtirish = await mijoz.PostAsJsonAsync(parolYoli, tana, ct))
        {
            almashtirish.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using (var marshrutYoq = await admin.PostAsJsonAsync(parolYoli, tana, ct))
        {
            marshrutYoq.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        // HR ko'rgan parol o'zgarmagan — va u hali ham ishlaydi.
        (await ParolAsync(admin, xodim.Id)).ShouldBe(parol);

        using var yana = await KirishAsync(xodim.Login, parol);
        yana.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yana));
    }

    /// <summary>
    /// HR yangi parol bersa: eski parol ishlamaydi, yangisi ishlaydi, eski sessiya yangilanmaydi.
    /// </summary>
    [Fact]
    public async Task HR_yangi_parol_bersa_eski_parol_va_sessiya_toxtaydi()
    {
        var ct = TestContext.Current.CancellationToken;
        var belgi = Belgi();
        using var admin = await AdminMijozAsync();

        var xodim = await XodimAsync(belgi);
        var eski = await ParolAsync(admin, xodim.Id);
        var juftlik = await KirganJuftlikAsync(admin, xodim.Id);

        using (var yasash = await admin.PostAsync(
                   new Uri($"/api/employees/{xodim.Id}/app-access/password/generate", UriKind.Relative),
                   null,
                   ct))
        {
            yasash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yasash));
        }

        var yangi = await ParolAsync(admin, xodim.Id);
        yangi.ShouldNotBe(eski);

        using (var eskiBilan = await KirishAsync(xodim.Login, eski))
        {
            await XatoKutAsync(eskiBilan, HttpStatusCode.BadRequest, "xodim.notogri_malumot");
        }

        using (var yangiBilan = await KirishAsync(xodim.Login, yangi))
        {
            yangiBilan.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yangiBilan));
        }

        // Eski qurilmaning sessiyasi yangilanmaydi — barcha sessiyalar yopilgan.
        using var yangilash = await YangilaAsync(juftlik.GetProperty("refreshToken").GetString()!);
        await XatoKutAsync(yangilash, HttpStatusCode.Forbidden, "xodim.token_yaroqsiz");
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
        var login = xodim.Login;
        var parol = await ParolAsync(admin, xodim.Id);

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

        // Hisob qatori tegilmagan: blok hisobda EMAS (unda blok ustunlarining
        // o'zi ham yo'q), va hech bir urinish «kirdi» deb yozilmagan.
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
        var hisob = await kontekst.EmployeeAccounts.AsNoTracking()
            .SingleAsync(row => row.EmployeeId == xodim.Id, TestContext.Current.CancellationToken);

        hisob.LastLoginAt.ShouldBeNull();
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
        var birinchi = (await KirganJuftlikAsync(admin, xodim.Id)).GetProperty("refreshToken").GetString()!;

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
        var token = (await KirganJuftlikAsync(admin, xodim.Id)).GetProperty("refreshToken").GetString()!;

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
