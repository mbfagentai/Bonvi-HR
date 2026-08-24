using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Bonvi.Application.Identity.Abstractions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Yuklangan hisob rasmining kaliti (test uchun).</summary>
public sealed record AccountPhotoResponse(string PhotoKey);

/// <summary>Joriy foydalanuvchi javobi — test uchun faqat kerakli maydonlar.</summary>
public sealed record MeResponse(Guid Id, string Login, string? PhotoKey);

/// <summary>
/// Hisob rasmining uchi-uchiga testlari.
/// </summary>
/// <remarks>
/// <para>
/// Eng muhim tekshiruv — <see cref="Boshqa_hisobning_rasmiga_tegib_bolmaydi"/>:
/// rasm manzilida foydalanuvchi identifikatori yo'q, ya'ni «boshqa odamning
/// suratini almashtirish» so'rovini YOZIB ham bo'lmasligi kerak. Test buni
/// ikki hisob bilan mahkamlaydi: biri yuklaydi, ikkinchisi o'ziniki bo'lmagan
/// rasmni ko'ra olmaydi.
/// </para>
/// <para>
/// Rasm quvurining o'zi (SkiaSharp, diskka yozish, ETag) xodim testlarida
/// batafsil tekshirilgan — bu yerda takrorlanmaydi, faqat hisobga xos qismi
/// va zanjir haqiqatan ulanganini isbotlovchi minimal tekshiruv qoladi.
/// </para>
/// </remarks>
public sealed class AccountPhotoApiTests : IClassFixture<BonviApiFactory>
{
    /// <summary>Serverdan kutiladigan tomon o'lchami.</summary>
    private const int KutilganTomon = 512;

    private static readonly Uri PhotoUri = new("/api/auth/me/photo", UriKind.Relative);
    private static readonly Uri MeUri = new("/api/auth/me", UriKind.Relative);

    private readonly BonviApiFactory _factory;

    public AccountPhotoApiTests(BonviApiFactory factory) => _factory = factory;

    /// <summary>
    /// Yuklash → «me» da kalit → berish → kesh → o'chirish.
    /// </summary>
    /// <remarks>
    /// Bosqichlar ataylab bitta testda: ular bir-birining natijasiga tayanadi
    /// va alohida testlar bir xil tayyorgarlikni takrorlagan bo'lardi.
    /// </remarks>
    [Fact]
    public async Task Hisob_rasmining_toliq_hayot_sikli_ishlaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var client = await AdminMijozAsync();

        // Boshida rasm yo'q — «xato» emas, oddiy holat
        using (var boshida = await client.GetAsync(PhotoUri, bekorQilish))
        {
            boshida.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await XatoKodiAsync(boshida)).ShouldBe("hisob.rasm_yoq");
        }

        // 1. Yuklash — 40×20 PNG, ya'ni ATAYLAB kvadrat emas
        string kalit;

        using (var yuklash = await RasmYuborAsync(client, TestImages.PngYasa(40, 20), "image/png"))
        {
            yuklash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yuklash));

            var yuklangan = await yuklash.Content.ReadFromJsonAsync<AccountPhotoResponse>(bekorQilish);

            yuklangan.ShouldNotBeNull();
            yuklangan.PhotoKey.ShouldNotBeNullOrWhiteSpace();

            // Foydalanuvchi bergan «surat.png» nomi kalitga TUSHMASLIGI shart
            yuklangan.PhotoKey.ShouldEndWith(".jpg");
            yuklangan.PhotoKey.ShouldNotContain("surat");

            kalit = yuklangan.PhotoKey;
        }

        // 2. «/api/auth/me» kalitni beradi — interfeys avatarni shundan biladi
        using (var men = await client.GetAsync(MeUri, bekorQilish))
        {
            men.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(men));

            var malumot = await men.Content.ReadFromJsonAsync<MeResponse>(bekorQilish);

            malumot.ShouldNotBeNull();
            malumot.PhotoKey.ShouldBe(kalit);
        }

        // 3. Berish — mazmun haqiqatan JPEG va 512×512
        string belgi;

        using (var oqish = await client.GetAsync(PhotoUri, bekorQilish))
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
            eni.ShouldBe(KutilganTomon);
            boyi.ShouldBe(KutilganTomon, "markazdan kvadrat qirqilishi kerak");
        }

        // 4. Kesh — o'sha belgi bilan qayta so'ralganda mazmun yuborilmaydi
        using (var soorov = new HttpRequestMessage(HttpMethod.Get, PhotoUri))
        {
            soorov.Headers.TryAddWithoutValidation("If-None-Match", belgi);

            using var javob = await client.SendAsync(soorov, bekorQilish);

            javob.StatusCode.ShouldBe(HttpStatusCode.NotModified);
            javob.Headers.ETag.ShouldNotBeNull();
            javob.Headers.ETag.Tag.ShouldBe(belgi);
        }

        // 5. Almashtirish — kalit o'zgarishi shart, aks holda brauzer eskisini ko'rsatardi
        using (var almashtirish = await RasmYuborAsync(client, TestImages.PngYasa(24, 24), "image/png"))
        {
            almashtirish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(almashtirish));

            var yangi = await almashtirish.Content.ReadFromJsonAsync<AccountPhotoResponse>(bekorQilish);

            yangi.ShouldNotBeNull();
            yangi.PhotoKey.ShouldNotBe(kalit);
        }

        // 6. O'chirish — takroriy so'rov ham xato emas
        using (var ochirish = await client.DeleteAsync(PhotoUri, bekorQilish))
        {
            ochirish.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(ochirish));
        }

        using (var qayta = await client.DeleteAsync(PhotoUri, bekorQilish))
        {
            qayta.StatusCode.ShouldBe(
                HttpStatusCode.NoContent,
                "amal idempotent: rasm yo'q bo'lsa ham xato emas");
        }

        using (var keyin = await client.GetAsync(PhotoUri, bekorQilish))
        {
            keyin.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await XatoKodiAsync(keyin)).ShouldBe("hisob.rasm_yoq");
        }

        using (var men = await client.GetAsync(MeUri, bekorQilish))
        {
            var malumot = await men.Content.ReadFromJsonAsync<MeResponse>(bekorQilish);

            malumot.ShouldNotBeNull();
            malumot.PhotoKey.ShouldBeNull();
        }
    }

    /// <summary>Rasm bo'lmagan fayl ikki bosqichda ham to'xtatiladi.</summary>
    /// <remarks>
    /// Ikkinchi holat asosiysi: mijoz turni «image/png» deb E'LON QILADI,
    /// lekin ichida oddiy matn. Sarlavhaga ishonilsa, saqlash jildiga rasm
    /// bo'lmagan fayl tushib qolardi.
    /// </remarks>
    [Fact]
    public async Task Rasm_bolmagan_fayl_qabul_qilinmaydi()
    {
        using var client = await AdminMijozAsync();

        var matn = Encoding.UTF8.GetBytes("Bu rasm emas, oddiy matn.");

        using (var eLonQilinganTur = await RasmYuborAsync(client, matn, "text/plain"))
        {
            eLonQilinganTur.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await XatoKodiAsync(eLonQilinganTur)).ShouldBe("hisob.rasm_turi");
        }

        using (var soxtaTur = await RasmYuborAsync(client, matn, "image/png"))
        {
            soxtaTur.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await XatoKodiAsync(soxtaTur)).ShouldBe("hisob.rasm_yaroqsiz");
        }

        using var oqish = await client.GetAsync(PhotoUri, TestContext.Current.CancellationToken);
        oqish.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>Tizimga kirmagan mijoz rasmga umuman yeta olmaydi.</summary>
    [Fact]
    public async Task Tokensiz_soorov_rad_etiladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var client = _factory.CreateClient();

        using (var oqish = await client.GetAsync(PhotoUri, bekorQilish))
        {
            oqish.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using (var yuklash = await RasmYuborAsync(client, TestImages.PngYasa(8, 8), "image/png"))
        {
            yuklash.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using (var ochirish = await client.DeleteAsync(PhotoUri, bekorQilish))
        {
            ochirish.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
    }

    /// <summary>
    /// Bir hisobning rasmi ikkinchisiga ko'rinmaydi.
    /// </summary>
    /// <remarks>
    /// Bu — endpoint'ning butun xavfsizlik da'vosi. Manzilda identifikator
    /// bo'lmagani uchun uni «boshqasiga» burib bo'lmaydi; test esa shu
    /// da'voni haqiqiy ikkinchi hisob bilan tekshiradi, chunki kelajakda
    /// kimdir manzilga identifikator qo'shib yuborishi mumkin.
    /// </remarks>
    [Fact]
    public async Task Boshqa_hisobning_rasmiga_tegib_bolmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var admin = await AdminMijozAsync();

        using (var yuklash = await RasmYuborAsync(admin, TestImages.PngYasa(16, 16), "image/png"))
        {
            yuklash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yuklash));
        }

        // Ikkinchi hisob
        var login = TasodifiyLogin();
        await FoydalanuvchiYaratAsync(login, IkkinchiParol);

        using var boshqa = await MijozAsync(login, IkkinchiParol);

        using (var oqish = await boshqa.GetAsync(PhotoUri, bekorQilish))
        {
            oqish.StatusCode.ShouldBe(
                HttpStatusCode.NotFound,
                "ikkinchi hisobning o'z rasmi yo'q — administratorniki unga ko'rinmaydi");

            (await XatoKodiAsync(oqish)).ShouldBe("hisob.rasm_yoq");
        }

        // Ikkinchi hisob o'z rasmini yuklaydi — administratornikiga tegmaydi
        using (var yuklash = await RasmYuborAsync(boshqa, TestImages.PngYasa(12, 12), "image/png"))
        {
            yuklash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yuklash));
        }

        using (var men = await admin.GetAsync(MeUri, bekorQilish))
        {
            var malumot = await men.Content.ReadFromJsonAsync<MeResponse>(bekorQilish);

            malumot.ShouldNotBeNull();
            malumot.PhotoKey.ShouldNotBeNull("administratorning rasmi joyida qolishi kerak");
        }

        // O'zidan keyin tozalash: ikkala hisobning ham fayli qolmasin
        (await boshqa.DeleteAsync(PhotoUri, bekorQilish)).Dispose();
        (await admin.DeleteAsync(PhotoUri, bekorQilish)).Dispose();
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    private Task<HttpClient> AdminMijozAsync()
        => MijozAsync(BonviApiFactory.TestAdminLogin, BonviApiFactory.TestAdminPassword);

    private async Task<HttpClient> MijozAsync(string login, string parol)
    {
        var client = _factory.CreateClient();

        using var javob = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new { login, password = parol },
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var auth = await javob.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return client;
    }

    /// <summary>Test uchun band bo'lmagan telefon raqami.</summary>
    /// <remarks>
    /// Raqam tasodifiy: testlar parallel ishlaydi va qat'iy raqam
    /// «login band» xatosiga olib kelardi. Prefiks «93» — haqiqiy operator
    /// kodi, aks holda telefon qiymat obyekti raqamni rad etadi.
    /// </remarks>
    private static string TasodifiyLogin()
        => "93" + Random.Shared.Next(1_000_000, 9_999_999).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Ikkinchi hisobni to'g'ridan-to'g'ri bazada yaratadi.
    /// </summary>
    /// <remarks>
    /// Nega HTTP orqali emas: foydalanuvchi yaratish endpoint'i hali yo'q
    /// (hisoblarni ishga tushirish bosqichi qo'shadi). Test esa ikkinchi
    /// hisobsiz asosiy da'voni — «rasm faqat o'ziniki» — tekshira olmaydi.
    /// Yozuv shu test bazasida qoladi va u tugagach o'chiriladi.
    /// </remarks>
    private async Task FoydalanuvchiYaratAsync(string login, string parol)
    {
        using var scope = _factory.Services.CreateScope();
        var provider = scope.ServiceProvider;

        var phone = PhoneNumber.Create(login);
        phone.IsSuccess.ShouldBeTrue();

        var fullName = FullName.Create("Тестов", "Тест", middleName: null);
        fullName.IsSuccess.ShouldBeTrue();

        var hasher = provider.GetRequiredService<IPasswordHasher>();
        var user = User.Create(phone.Value, hasher.Hash(parol), fullName.Value);
        user.IsSuccess.ShouldBeTrue();

        provider.GetRequiredService<IUserRepository>().Add(user.Value);

        await provider.GetRequiredService<IUnitOfWork>()
            .SaveChangesAsync(TestContext.Current.CancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Ikkinchi hisobning paroli — testdan tashqarida ishlatilmaydi.</summary>
    private const string IkkinchiParol = "Bonvi_Test_2026";

    /// <summary>Faylni «multipart/form-data» bo'lib, «file» maydonida yuboradi.</summary>
    private static async Task<HttpResponseMessage> RasmYuborAsync(
        HttpClient client,
        byte[] baytlar,
        string tur)
    {
        using var forma = new MultipartFormDataContent();
        using var qism = new ByteArrayContent(baytlar);

        qism.Headers.ContentType = new MediaTypeHeaderValue(tur);

        // Fayl nomi ataylab beriladi: u javobdagi kalitga TUSHMASLIGI kerak.
        forma.Add(qism, "file", "surat.png");

        return await client.PostAsync(PhotoUri, forma, TestContext.Current.CancellationToken);
    }

    private static async Task<string?> XatoKodiAsync(HttpResponseMessage javob)
    {
        var muammo = await javob.Content.ReadFromJsonAsync<ProblemResponse>(
            TestContext.Current.CancellationToken);

        return muammo?.Title;
    }

    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
