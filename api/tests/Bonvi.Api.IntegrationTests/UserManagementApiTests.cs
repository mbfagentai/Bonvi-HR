using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Identity;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Foydalanuvchi (test uchun).</summary>
/// <remarks>
/// <c>RoleCode</c> — AYNAN BITTA (ro'yxat emas): uch rolli modelda hisobda
/// bittadan ortiq rol bo'lmaydi. <c>PhotoKey</c> ham shu javobda: ro'yxat
/// avatarni shu kalit bo'yicha so'raydi.
/// </remarks>
public sealed record UserSummaryResponse(
    Guid Id,
    string Login,
    string FullName,
    string ShortName,
    bool IsActive,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset? LockedUntil,
    string? PhotoKey,
    string? RoleCode,
    string? RoleName);

/// <summary>Yuklangan foydalanuvchi rasmining kaliti (test uchun).</summary>
public sealed record UserPhotoResponse(string PhotoKey);

/// <summary>
/// Foydalanuvchilarni boshqarish HTTP qatlamining uchi-uchiga testlari.
/// </summary>
/// <remarks>
/// <para>
/// Haqiqiy PostgreSQL ustida ishlaydi. Testlar bitta bazani baham ko'radi,
/// shu sababli har bir test o'ziga xos telefon raqami ishlatadi.
/// </para>
/// <para>
/// <b>Rahbariyat holatiga bog'liq testlar O'Z bazasida ishlaydi.</b>
/// «Atigi bitta» qoidasi butun tizim holatiga tegishli: umumiy bazada
/// bitta test bosh rahbar yaratsa, keyingisi uni yarata olmay qolardi va
/// natija testlar tartibiga bog'lanib qolardi.
/// </para>
/// </remarks>
public sealed class UserManagementApiTests : IClassFixture<BonviApiFactory>
{
    private static readonly Uri UsersUri = new("/api/users", UriKind.Relative);
    private static readonly Uri LoginUri = new("/api/auth/login", UriKind.Relative);
    private static readonly Uri MeUri = new("/api/auth/me", UriKind.Relative);

    private static int _telefonSonagichi;

    private readonly BonviApiFactory _factory;

    public UserManagementApiTests(BonviApiFactory factory) => _factory = factory;

    // ────────────────────────────────────────────────────────────
    //  Ruxsatlar
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Tokensiz_royxat_401_qaytaradi()
    {
        using var client = _factory.CreateClient();

        using var javob = await client.GetAsync(UsersUri, TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Users_manage_ruxsati_yoqligi_403_beradi()
    {
        using var adminClient = await AdminMijozAsync(_factory);
        using var hrClient = await HrManagerMijozAsync(_factory, adminClient);

        using var royxat = await hrClient.GetAsync(UsersUri, TestContext.Current.CancellationToken);

        royxat.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var yaratish = await hrClient.PostAsJsonAsync(
            UsersUri,
            YangiHisob(NoyobTelefon(), SystemRoles.HrManager),
            TestContext.Current.CancellationToken);

        // HR menejer boshqa hech kimning hisobini yarata olmaydi — foydalanuvchi
        // boshqaruvi butunlay rahbariyatga tegishli
        yaratish.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ────────────────────────────────────────────────────────────
    //  To'liq hayot sikli
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Foydalanuvchining_toliq_hayot_sikli_ishlaydi()
    {
        using var client = await AdminMijozAsync(_factory);
        var telefon = NoyobTelefon();
        const string boshlangichParol = "Boshlangich_Parol1";

        // 1. Yaratish
        using var yaratish = await client.PostAsJsonAsync(
            UsersUri,
            new
            {
                login = telefon,
                lastName = "Каримов",
                firstName = "Жасур",
                password = boshlangichParol,
                roleCode = SystemRoles.HrManager,
            },
            TestContext.Current.CancellationToken);

        yaratish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yaratish));

        var yaratilgan = await yaratish.Content.ReadFromJsonAsync<UserSummaryResponse>(
            TestContext.Current.CancellationToken);

        yaratilgan.ShouldNotBeNull();
        yaratilgan.IsActive.ShouldBeTrue();
        yaratilgan.RoleCode.ShouldBe(SystemRoles.HrManager);
        yaratilgan.PhotoKey.ShouldBeNull("yangi hisobda rasm yo'q");
        // Parol javobda hech qachon qaytmasligi kerak
        (await MatnAsync(yaratish)).ShouldNotContain(boshlangichParol);

        // 2. Ro'yxatda ko'rinishi
        using var royxat1 = await client.GetAsync(UsersUri, TestContext.Current.CancellationToken);

        var hammasi = await royxat1.Content.ReadFromJsonAsync<List<UserSummaryResponse>>(
            TestContext.Current.CancellationToken);

        hammasi.ShouldNotBeNull();
        hammasi.ShouldContain(u => u.Id == yaratilgan.Id);

        // 3. Yangi hisob bilan haqiqatan kirish mumkin
        using var yangiHisobKirish = await _factory.CreateClient().PostAsJsonAsync(
            LoginUri,
            new { login = telefon, password = boshlangichParol },
            TestContext.Current.CancellationToken);

        yangiHisobKirish.StatusCode.ShouldBe(HttpStatusCode.OK);
        var eskiSessiya = await yangiHisobKirish.Content.ReadFromJsonAsync<AuthResponse>(
            TestContext.Current.CancellationToken);
        eskiSessiya.ShouldNotBeNull();

        // 4. Tahrirlash — ism o'zgaradi, rol o'sha
        using var tahrir = await client.PutAsJsonAsync(
            new Uri($"/api/users/{yaratilgan.Id}", UriKind.Relative),
            new
            {
                login = telefon,
                lastName = "Каримов",
                firstName = "Жасур",
                middleName = "Baxtiyorovich",
                roleCode = SystemRoles.HrManager,
            },
            TestContext.Current.CancellationToken);

        tahrir.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(tahrir));
        var tahrirlangan = await tahrir.Content.ReadFromJsonAsync<UserSummaryResponse>(
            TestContext.Current.CancellationToken);
        tahrirlangan.ShouldNotBeNull();
        tahrirlangan.FullName.ShouldContain("Baxtiyorovich");
        tahrirlangan.RoleCode.ShouldBe(SystemRoles.HrManager);

        // 5. Parolni tiklash — eski sessiya bekor bo'lishi kerak
        const string yangiParol = "Yangilangan_Parol1";

        using var tiklash = await client.PostAsJsonAsync(
            new Uri($"/api/users/{yaratilgan.Id}/reset-password", UriKind.Relative),
            new { newPassword = yangiParol },
            TestContext.Current.CancellationToken);

        tiklash.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(tiklash));

        using var eskiTokenBilanYangilash = await _factory.CreateClient().PostAsJsonAsync(
            new Uri("/api/auth/refresh", UriKind.Relative),
            new { refreshToken = eskiSessiya.RefreshToken },
            TestContext.Current.CancellationToken);

        eskiTokenBilanYangilash.StatusCode.ShouldBe(
            HttpStatusCode.Forbidden, "parol tiklanganda eski sessiya bekor qilinishi kerak");

        // Yangi parol bilan kirish ishlaydi
        using var yangiParolBilanKirish = await _factory.CreateClient().PostAsJsonAsync(
            LoginUri,
            new { login = telefon, password = yangiParol },
            TestContext.Current.CancellationToken);

        yangiParolBilanKirish.StatusCode.ShouldBe(HttpStatusCode.OK);

        // 6. Faolsizlantirish — kirish yopiladi
        using var faolsizlantirish = await client.PostAsync(
            new Uri($"/api/users/{yaratilgan.Id}/deactivate", UriKind.Relative),
            content: null,
            TestContext.Current.CancellationToken);

        faolsizlantirish.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(faolsizlantirish));

        using var faolsizKirish = await _factory.CreateClient().PostAsJsonAsync(
            LoginUri,
            new { login = telefon, password = yangiParol },
            TestContext.Current.CancellationToken);

        faolsizKirish.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // 7. Qayta faollashtirish
        using var faollashtirish = await client.PostAsync(
            new Uri($"/api/users/{yaratilgan.Id}/activate", UriKind.Relative),
            content: null,
            TestContext.Current.CancellationToken);

        faollashtirish.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var qaytaKirish = await _factory.CreateClient().PostAsJsonAsync(
            LoginUri,
            new { login = telefon, password = yangiParol },
            TestContext.Current.CancellationToken);

        qaytaKirish.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Band_telefon_raqami_bilan_yaratish_409_qaytaradi()
    {
        using var client = await AdminMijozAsync(_factory);
        var telefon = NoyobTelefon();

        using var birinchi = await client.PostAsJsonAsync(
            UsersUri,
            YangiHisob(telefon, SystemRoles.HrManager),
            TestContext.Current.CancellationToken);

        birinchi.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(birinchi));

        using var ikkinchi = await client.PostAsJsonAsync(
            UsersUri,
            YangiHisob(telefon, SystemRoles.HrManager),
            TestContext.Current.CancellationToken);

        ikkinchi.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await MatnAsync(ikkinchi)).ShouldContain("foydalanuvchi.login_band");
    }

    [Fact]
    public async Task Qisqa_parol_bilan_yaratish_400_qaytaradi()
    {
        using var client = await AdminMijozAsync(_factory);

        using var javob = await client.PostAsJsonAsync(
            UsersUri,
            new
            {
                login = NoyobTelefon(),
                lastName = "Familiya",
                firstName = "Ism",
                password = "qisqa",
                roleCode = SystemRoles.HrManager,
            },
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await MatnAsync(javob)).ShouldContain("foydalanuvchi.parol_qisqa");
    }

    // ────────────────────────────────────────────────────────────
    //  Uch rolli model: «atigi bitta» va «kim kimga tega oladi»
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Boshlang'ich hisob super administrator — ikkinchisi bo'lishi mumkin emas.
    /// </summary>
    [Fact]
    public async Task Ikkinchi_super_administrator_yaratilmaydi()
    {
        using var client = await AdminMijozAsync(_factory);

        using var javob = await client.PostAsJsonAsync(
            UsersUri,
            YangiHisob(NoyobTelefon(), SystemRoles.SuperAdmin),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.Conflict, await MatnAsync(javob));
        (await MatnAsync(javob)).ShouldContain("foydalanuvchi.super_admin_mavjud");
    }

    /// <summary>
    /// Bosh rahbar ham BITTA bo'ladi, lekin o'rni bo'shatilsa — yangisi
    /// qo'yiladi. «Atigi bitta» faqat FAOL hisoblarni sanaydi.
    /// </summary>
    [Fact]
    public async Task Bosh_rahbar_bitta_boladi_ammo_almashtirilishi_mumkin()
    {
        await using var yakka = new BonviApiFactory();
        await yakka.InitializeAsync();

        using var client = await AdminMijozAsync(yakka);

        // 1. Birinchi bosh rahbar — muvaffaqiyat
        var birinchiTelefon = NoyobTelefon();
        using var birinchi = await client.PostAsJsonAsync(
            UsersUri,
            YangiHisob(birinchiTelefon, SystemRoles.MainBoss),
            TestContext.Current.CancellationToken);

        birinchi.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(birinchi));
        var eski = await birinchi.Content.ReadFromJsonAsync<UserSummaryResponse>(
            TestContext.Current.CancellationToken);
        eski.ShouldNotBeNull();

        // 2. Ikkinchisi — rad etiladi
        using var ikkinchi = await client.PostAsJsonAsync(
            UsersUri,
            YangiHisob(NoyobTelefon(), SystemRoles.MainBoss),
            TestContext.Current.CancellationToken);

        ikkinchi.StatusCode.ShouldBe(HttpStatusCode.Conflict, await MatnAsync(ikkinchi));
        (await MatnAsync(ikkinchi)).ShouldContain("foydalanuvchi.bosh_rahbar_mavjud");

        // 3. Eskisi faolsizlantiriladi — lavozim bo'shaydi
        using var faolsizlantirish = await client.PostAsync(
            new Uri($"/api/users/{eski.Id}/deactivate", UriKind.Relative),
            content: null,
            TestContext.Current.CancellationToken);

        faolsizlantirish.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(faolsizlantirish));

        // 4. Endi yangisi qo'yiladi
        using var yangisi = await client.PostAsJsonAsync(
            UsersUri,
            YangiHisob(NoyobTelefon(), SystemRoles.MainBoss),
            TestContext.Current.CancellationToken);

        yangisi.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yangisi));

        // 5. Eskisini qaytarish endi mumkin emas — o'rni band
        using var qaytarish = await client.PostAsync(
            new Uri($"/api/users/{eski.Id}/activate", UriKind.Relative),
            content: null,
            TestContext.Current.CancellationToken);

        qaytarish.StatusCode.ShouldBe(HttpStatusCode.Conflict, await MatnAsync(qaytarish));
        (await MatnAsync(qaytarish)).ShouldContain("foydalanuvchi.bosh_rahbar_mavjud");
    }

    /// <summary>
    /// Bosh rahbarda <c>users.manage</c> BOR (ekranga kiradi), lekin super
    /// administratorga tegolmaydi — farq ruxsatda emas, so'rov mazmunida.
    /// </summary>
    [Fact]
    public async Task Bosh_rahbar_super_administratorga_tegolmaydi()
    {
        await using var yakka = new BonviApiFactory();
        await yakka.InitializeAsync();

        using var adminClient = await AdminMijozAsync(yakka);

        // Bosh rahbar hisobi va uning mijozi
        var telefon = NoyobTelefon();
        const string parol = "Bosh_Rahbar_1";

        using (var yaratish = await adminClient.PostAsJsonAsync(
            UsersUri,
            new
            {
                login = telefon,
                lastName = "Раҳимов",
                firstName = "Сардор",
                password = parol,
                roleCode = SystemRoles.MainBoss,
            },
            TestContext.Current.CancellationToken))
        {
            yaratish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yaratish));
        }

        using var bossClient = await KirishAsync(yakka, telefon, parol);

        // Ro'yxatni KO'RA oladi — `users.manage` bor
        using (var royxat = await bossClient.GetAsync(UsersUri, TestContext.Current.CancellationToken))
        {
            royxat.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(royxat));
        }

        // HR menejer yarata oladi
        using (var hrYaratish = await bossClient.PostAsJsonAsync(
            UsersUri,
            YangiHisob(NoyobTelefon(), SystemRoles.HrManager),
            TestContext.Current.CancellationToken))
        {
            hrYaratish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(hrYaratish));
        }

        // Lekin super administratorning parolini tiklay OLMAYDI
        var superAdminId = await SuperAdminIdAsync(adminClient);

        using (var tiklash = await bossClient.PostAsJsonAsync(
            new Uri($"/api/users/{superAdminId}/reset-password", UriKind.Relative),
            new { newPassword = "Egallab_Olaman1" },
            TestContext.Current.CancellationToken))
        {
            tiklash.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await MatnAsync(tiklash));
            (await MatnAsync(tiklash)).ShouldContain("foydalanuvchi.rolga_haqqi_yoq");
        }

        // Va uni faolsizlantira ham olmaydi
        using (var faolsizlantirish = await bossClient.PostAsync(
            new Uri($"/api/users/{superAdminId}/deactivate", UriKind.Relative),
            content: null,
            TestContext.Current.CancellationToken))
        {
            faolsizlantirish.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await MatnAsync(faolsizlantirish));
            (await MatnAsync(faolsizlantirish)).ShouldContain("foydalanuvchi.rolga_haqqi_yoq");
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Rasm
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Yuklash → ro'yxatda kalit → berish → kesh → o'chirish.
    /// </summary>
    /// <remarks>
    /// Bosqichlar ataylab bitta testda: ular bir-birining natijasiga tayanadi.
    /// Rasm quvurining o'zi (SkiaSharp, diskka yozish) xodim testlarida
    /// batafsil tekshirilgan — bu yerda faqat zanjir ulanganini isbotlovchi
    /// minimal tekshiruv qoladi.
    /// </remarks>
    [Fact]
    public async Task Foydalanuvchi_rasmining_toliq_hayot_sikli_ishlaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var client = await AdminMijozAsync(_factory);

        using var yaratish = await client.PostAsJsonAsync(
            UsersUri,
            YangiHisob(NoyobTelefon(), SystemRoles.HrManager),
            bekorQilish);

        yaratish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yaratish));
        var hisob = await yaratish.Content.ReadFromJsonAsync<UserSummaryResponse>(bekorQilish);
        hisob.ShouldNotBeNull();

        var photoUri = new Uri($"/api/users/{hisob.Id}/photo", UriKind.Relative);

        // Boshida rasm yo'q — «xato» emas, oddiy holat
        using (var boshida = await client.GetAsync(photoUri, bekorQilish))
        {
            boshida.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await MatnAsync(boshida)).ShouldContain("foydalanuvchi.rasm_yoq");
        }

        // 1. Yuklash — 40×20 PNG, ya'ni ATAYLAB kvadrat emas
        string kalit;

        using (var yuklash = await RasmYuborAsync(client, photoUri, TestImages.PngYasa(40, 20)))
        {
            yuklash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yuklash));

            var yuklangan = await yuklash.Content.ReadFromJsonAsync<UserPhotoResponse>(bekorQilish);

            yuklangan.ShouldNotBeNull();
            yuklangan.PhotoKey.ShouldEndWith(".jpg");
            yuklangan.PhotoKey.ShouldNotContain("surat");

            kalit = yuklangan.PhotoKey;
        }

        // 2. Ro'yxat kalitni beradi — interfeys avatarni shundan biladi
        using (var royxat = await client.GetAsync(UsersUri, bekorQilish))
        {
            var hammasi = await royxat.Content.ReadFromJsonAsync<List<UserSummaryResponse>>(bekorQilish);

            hammasi.ShouldNotBeNull();
            hammasi.Single(u => u.Id == hisob.Id).PhotoKey.ShouldBe(kalit);
        }

        // 3. Berish — mazmun haqiqatan JPEG va 512×512
        string belgi;

        using (var oqish = await client.GetAsync(photoUri, bekorQilish))
        {
            oqish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(oqish));
            oqish.Content.Headers.ContentType.ShouldNotBeNull();
            oqish.Content.Headers.ContentType.MediaType.ShouldBe("image/jpeg");

            oqish.Headers.CacheControl.ShouldNotBeNull();
            oqish.Headers.CacheControl.Private.ShouldBeTrue("surat — shaxsiy ma'lumot");

            oqish.Headers.ETag.ShouldNotBeNull();
            belgi = oqish.Headers.ETag.Tag;

            var baytlar = await oqish.Content.ReadAsByteArrayAsync(bekorQilish);

            var (eni, boyi) = TestImages.JpegOlchami(baytlar);
            eni.ShouldBe(512);
            boyi.ShouldBe(512, "markazdan kvadrat qirqilishi kerak");
        }

        // 4. Kesh — «If-None-Match» mos kelsa mazmun qayta yuborilmaydi
        using (var soro = new HttpRequestMessage(HttpMethod.Get, photoUri))
        {
            soro.Headers.TryAddWithoutValidation("If-None-Match", belgi);

            using var javob = await client.SendAsync(soro, bekorQilish);

            javob.StatusCode.ShouldBe(HttpStatusCode.NotModified);
            javob.Headers.ETag.ShouldNotBeNull("304 javobida ham ETag bo'lishi kerak");
        }

        // 5. O'chirish — IDEMPOTENT: ikkinchi marta ham 204
        using (var ochirish = await client.DeleteAsync(photoUri, bekorQilish))
        {
            ochirish.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(ochirish));
        }

        using (var qayta = await client.DeleteAsync(photoUri, bekorQilish))
        {
            qayta.StatusCode.ShouldBe(HttpStatusCode.NoContent, "amal idempotent bo'lishi kerak");
        }

        using (var keyin = await client.GetAsync(photoUri, bekorQilish))
        {
            keyin.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }
    }

    [Fact]
    public async Task Hr_menejer_boshqa_hisobga_rasm_qoya_olmaydi()
    {
        using var adminClient = await AdminMijozAsync(_factory);
        using var hrClient = await HrManagerMijozAsync(_factory, adminClient);

        var nishonId = await SuperAdminIdAsync(adminClient);
        var photoUri = new Uri($"/api/users/{nishonId}/photo", UriKind.Relative);

        using var yuklash = await RasmYuborAsync(hrClient, photoUri, TestImages.PngYasa(20, 20));

        // `users.manage` yo'q — marshrutning o'ziga yetmaydi
        yuklash.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ────────────────────────────────────────────────────────────
    //  O'zini o'zi bloklashdan va oxirgi rahbardan himoya
    // ────────────────────────────────────────────────────────────

    /// <remarks>
    /// Bu test ATAYLAB o'zining ALOHIDA bazasida ishlaydi (umumiy
    /// <see cref="_factory"/> emas): «tizimda bor-yo'g'i bitta rahbar bor»
    /// holatini faqat yangi, hech kim tegmagan bazada kafolatlash mumkin.
    /// </remarks>
    [Fact]
    public async Task Yagona_rahbar_ozini_faolsizlantira_olmaydi()
    {
        await using var yakka = new BonviApiFactory();
        await yakka.InitializeAsync();

        using var client = await AdminMijozAsync(yakka);
        var joriyId = await SuperAdminIdAsync(client);

        using var javob = await client.PostAsync(
            new Uri($"/api/users/{joriyId}/deactivate", UriKind.Relative),
            content: null,
            TestContext.Current.CancellationToken);

        // Bir vaqtda ham "o'zini o'zi", ham "oxirgi rahbar" holati — tizimda
        // boshqa faol rahbar yo'qligi birinchi tekshiriladi
        javob.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await MatnAsync(javob)).ShouldContain("foydalanuvchi.oxirgi_administrator");
    }

    [Fact]
    public async Task Super_administrator_boshqa_rahbar_bolsa_ham_ozini_faolsizlantira_olmaydi()
    {
        await using var yakka = new BonviApiFactory();
        await yakka.InitializeAsync();

        using var client = await AdminMijozAsync(yakka);

        // Bosh rahbar yaratamiz — endi tizimda ikkita rahbar bor
        using (var yaratish = await client.PostAsJsonAsync(
            UsersUri,
            YangiHisob(NoyobTelefon(), SystemRoles.MainBoss),
            TestContext.Current.CancellationToken))
        {
            yaratish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yaratish));
        }

        var joriyId = await SuperAdminIdAsync(client);

        using var javob = await client.PostAsync(
            new Uri($"/api/users/{joriyId}/deactivate", UriKind.Relative),
            content: null,
            TestContext.Current.CancellationToken);

        // Endi boshqa faol rahbar bor — shu sababli aynan "o'zini o'zi
        // bloklash" qoidasi ishga tushadi
        javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await MatnAsync(javob)).ShouldContain("foydalanuvchi.ozini_blokash");
    }

    // ────────────────────────────────────────────────────────────
    //  Audit
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Foydalanuvchi_yaratish_audit_jurnaliga_yoziladi()
    {
        using var client = await AdminMijozAsync(_factory);

        using var yaratish = await client.PostAsJsonAsync(
            UsersUri,
            YangiHisob(NoyobTelefon(), SystemRoles.HrManager),
            TestContext.Current.CancellationToken);

        yaratish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yaratish));
        var yaratilgan = await yaratish.Content.ReadFromJsonAsync<UserSummaryResponse>(
            TestContext.Current.CancellationToken);
        yaratilgan.ShouldNotBeNull();

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var yozuv = await kontekst.AuditLogs
            .Where(log => log.EntityName == "User" && log.EntityId == yaratilgan.Id.ToString())
            .OrderByDescending(log => log.OccurredAt)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        yozuv.ShouldNotBeNull();
        yozuv.Action.ShouldBe(AuditAction.Create);
        yozuv.UserLogin.ShouldBe("+998" + BonviApiFactory.TestAdminLogin);
        yozuv.NewValues.ShouldNotBeNull();
        // Parol audit jurnaliga hech qachon yozilmaydi
        yozuv.NewValues.ShouldNotContain("Parol_12345");
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    private static string NoyobTelefon()
    {
        var raqam = 1000000 + Interlocked.Increment(ref _telefonSonagichi);
        return "90" + raqam.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Yaratish so'rovining standart tanasi — faqat rol va telefon o'zgaradi.</summary>
    private static object YangiHisob(string telefon, string rolKodi) => new
    {
        login = telefon,
        lastName = "Familiya",
        firstName = "Ism",
        password = "Parol_12345",
        roleCode = rolKodi,
    };

    private static async Task<HttpClient> AdminMijozAsync(BonviApiFactory factory)
        => await KirishAsync(factory, BonviApiFactory.TestAdminLogin, BonviApiFactory.TestAdminPassword);

    private static async Task<HttpClient> KirishAsync(BonviApiFactory factory, string login, string parol)
    {
        var client = factory.CreateClient();

        using var javob = await client.PostAsJsonAsync(
            LoginUri,
            new { login, password = parol },
            TestContext.Current.CancellationToken);

        javob.EnsureSuccessStatusCode();

        var auth = await javob.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return client;
    }

    /// <summary>Joriy (kirgan) foydalanuvchining identifikatori.</summary>
    private static async Task<Guid> SuperAdminIdAsync(HttpClient client)
    {
        using var men = await client.GetAsync(MeUri, TestContext.Current.CancellationToken);

        var joriy = await men.Content.ReadFromJsonAsync<UserResponse>(TestContext.Current.CancellationToken);
        joriy.ShouldNotBeNull();

        return joriy.Id;
    }

    /// <summary>
    /// Haqiqiy HR menejer hisobi bilan kirgan mijoz — rahbariyat uchun
    /// mo'ljallangan endpoint'larga ruxsati yo'qligini ko'rsatish uchun.
    /// </summary>
    private static async Task<HttpClient> HrManagerMijozAsync(BonviApiFactory factory, HttpClient adminClient)
    {
        var telefon = NoyobTelefon();
        const string parol = "Hr_Sinov_Paroli1";

        using var yaratish = await adminClient.PostAsJsonAsync(
            UsersUri,
            new
            {
                login = telefon,
                lastName = "HR",
                firstName = "Sinov",
                password = parol,
                roleCode = SystemRoles.HrManager,
            },
            TestContext.Current.CancellationToken);

        yaratish.EnsureSuccessStatusCode();

        return await KirishAsync(factory, telefon, parol);
    }

    /// <summary>Faylni «multipart/form-data» bo'lib, «file» maydonida yuboradi.</summary>
    private static async Task<HttpResponseMessage> RasmYuborAsync(HttpClient client, Uri manzil, byte[] baytlar)
    {
        using var forma = new MultipartFormDataContent();
        using var qism = new ByteArrayContent(baytlar);

        qism.Headers.ContentType = new MediaTypeHeaderValue("image/png");

        // Fayl nomi ataylab beriladi: u javobdagi kalitga TUSHMASLIGI kerak.
        forma.Add(qism, "file", "surat.png");

        return await client.PostAsync(manzil, forma, TestContext.Current.CancellationToken);
    }

    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
