using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bonvi.Api.Extensions;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Ommaviy javobdagi xodim — testlar uchun kerakli maydonlar.</summary>
public sealed record PublicEmployeeResponse(
    Guid Id,
    string FullName,
    string LastName,
    string? Phone,
    string? Status,
    DateOnly? HireDate,
    DateOnly? TerminationDate,
    IReadOnlyList<PublicEmploymentResponse> Employments,
    DateTimeOffset UpdatedAt);

/// <summary>Ommaviy javobdagi mehnat munosabati — testlar uchun.</summary>
public sealed record PublicEmploymentResponse(
    Guid Id,
    PublicRefResponse? LegalEntity,
    PublicRefResponse? Department,
    PublicRefResponse? Position,
    decimal Rate,
    string Status,
    bool IsPrimary);

/// <summary>Ma'lumotnoma havolasi — testlar uchun.</summary>
public sealed record PublicRefResponse(Guid Id, string Name);

/// <summary>Sahifalangan ommaviy javob — testlar uchun.</summary>
public sealed record PublicPageResponse(
    IReadOnlyList<PublicEmployeeResponse> Items,
    int Page,
    int PageSize,
    int Total,
    int PageCount);

/// <summary>O'chirilgan xodim yozuvi — testlar uchun.</summary>
public sealed record PublicDeletionResponse(Guid Id, DateTimeOffset DeletedAt);

/// <summary><c>/ping</c> javobi — testlar uchun.</summary>
public sealed record PublicPingResponse(bool Ok, string Key, DateTimeOffset ServerTime);

/// <summary>
/// Tashqi API: kirish yo'llari, chegaralar va o'qish xatti-harakati.
/// </summary>
/// <remarks>
/// Maydon chegarasi ALOHIDA sinfda (<c>PublicApiLeakTests</c>) — u
/// modulning asosiy testi va uni boshqa tekshiruvlar orasida yo'qotib
/// qo'ymaslik kerak.
/// </remarks>
public sealed class PublicApiTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public PublicApiTests(BonviApiFactory factory) => _factory = factory;

    // ────────────────────────────────────────────────────────────
    //  Kirish yo'li — TO'RT xil rad javobi, BITTA xato kodi
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Kalit yo'q, buzuq, noma'lum yoki bekor qilingan — hammasi 401 va
    /// AYNAN bir xil kod.
    /// </summary>
    /// <remarks>
    /// To'rttasi bitta testda ataylab: tekshirilayotgan narsa alohida
    /// holatlar emas, ularning <b>farqsizligi</b>. Alohida testlarda
    /// bittasining kodi o'zgarib ketsa ham hammasi yashil qolardi.
    /// </remarks>
    [Fact]
    public async Task Yaroqsiz_kalitning_tort_holati_ham_bir_xil_401_beradi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var yol = new Uri($"{PublicApiHarness.Prefix}/ping", UriKind.Relative);

        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);

        var kalit = await PublicApiHarness.KalitYaratAsync(admin, PublicApiHarness.NoyobNom("Rad"));
        var bekorQilingan = await PublicApiHarness.KalitYaratAsync(admin, PublicApiHarness.NoyobNom("Bekor"));

        using (var bekorlash = await admin.PostAsync(
            new Uri($"/api/api-keys/{bekorQilingan.Id}/revoke", UriKind.Relative),
            content: null,
            bekorQilish))
        {
            bekorlash.StatusCode.ShouldBe(HttpStatusCode.OK, await PublicApiHarness.MatnAsync(bekorlash));
        }

        // Sir qismining OXIRGI belgisi almashtiriladi: prefiks TO'G'RI
        // qoladi, ya'ni server yozuvni topadi va faqat xesh mos kelmaydi.
        // Aynan shu holat «prefiks bor» degan ma'lumotni sizdirishi mumkin
        // bo'lgan holat.
        var oxirgi = kalit.Key[^1] == 'a' ? 'b' : 'a';
        var notogriSir = kalit.Key[..^1] + oxirgi;

        var holatlar = new (string Nima, string? Kalit)[]
        {
            ("kalit yuborilmadi", null),
            ("shakli buzuq", "shunchaki-matn"),
            ("noma'lum prefiks", "bonvi_zzzzzzzz_" + new string('z', 32)),
            ("sir noto'g'ri", notogriSir),
            ("bekor qilingan", bekorQilingan.Key),
        };

        foreach (var (nima, qiymat) in holatlar)
        {
            using var mijoz = _factory.CreateClient();

            if (qiymat is not null)
            {
                mijoz.DefaultRequestHeaders.Add(ApiKeyAuthenticationHandler.HeaderName, qiymat);
            }

            using var javob = await mijoz.GetAsync(yol, bekorQilish);

            javob.StatusCode.ShouldBe(
                HttpStatusCode.Unauthorized,
                $"{nima}: {await PublicApiHarness.MatnAsync(javob)}");

            var muammo = await javob.Content.ReadFromJsonAsync<ProblemResponse>(bekorQilish);

            muammo.ShouldNotBeNull();
            muammo.Title.ShouldBe("tashqi.kalit_notogri", nima);
        }
    }

    /// <summary>
    /// Yaroqli API kaliti ICHKI endpoint'ga kira olmaydi.
    /// </summary>
    /// <remarks>
    /// Ichki endpoint'lar standart (<c>Bearer</c>) sxemadan o'tadi va
    /// <c>X-API-Key</c> sarlavhasi ular uchun umuman mavjud emas. Bu
    /// konfiguratsiya bilan yopilgan, tekshiruv yozish bilan emas —
    /// test aynan shu kafolatni qo'riqlaydi.
    /// </remarks>
    [Fact]
    public async Task Kalit_ichki_endpointga_kira_olmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        var kalit = await PublicApiHarness.KalitYaratAsync(admin, PublicApiHarness.NoyobNom("Ichki"));

        using var mijoz = PublicApiHarness.KalitMijozi(_factory, kalit.Key);

        // Uchta xil ichki yo'l: ro'yxat, ma'lumotnoma va kalitlarning
        // O'ZINI boshqarish. Oxirgisi eng muhim — kalit bilan yangi kalit
        // yasab bo'lmasligi kerak.
        var yollar = new[] { "/api/employees", "/api/catalog/legal-entities", "/api/api-keys" };

        foreach (var yol in yollar)
        {
            using var javob = await mijoz.GetAsync(new Uri(yol, UriKind.Relative), bekorQilish);

            javob.StatusCode.ShouldBeOneOf(
                HttpStatusCode.Unauthorized,
                HttpStatusCode.Forbidden);
        }
    }

    /// <summary>
    /// Foydalanuvchi JWT'si OMMAVIY endpoint'ga kira olmaydi.
    /// </summary>
    /// <remarks>
    /// Ommaviy yuzaga KIRISHNING BITTA yo'li bor. Super administratorning
    /// to'liq huquqli tokeni ham bu yerda ishlamaydi: siyosat faqat kalit
    /// sxemasini sanaydi.
    /// </remarks>
    [Fact]
    public async Task Foydalanuvchi_tokeni_ommaviy_endpointga_kira_olmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);

        using var javob = await admin.GetAsync(
            new Uri($"{PublicApiHarness.Prefix}/ping", UriKind.Relative),
            bekorQilish);

        javob.StatusCode.ShouldBe(
            HttpStatusCode.Unauthorized,
            await PublicApiHarness.MatnAsync(javob));

        var muammo = await javob.Content.ReadFromJsonAsync<ProblemResponse>(bekorQilish);

        muammo.ShouldNotBeNull();
        muammo.Title.ShouldBe("tashqi.kalit_notogri");
    }

    /// <summary>Ommaviy yuzada YOZISH metodlari umuman map qilinmagan.</summary>
    /// <remarks>
    /// Tekshiruv 405 (<c>MethodNotAllowed</c>) ni kutadi: marshrut BOR,
    /// lekin faqat <c>GET</c> uchun. 404 ham qabul qilinadi — ikkalasi
    /// ham «bu metod bu yerda yo'q» degani va aniq kod marshrutlash
    /// tafsiloti.
    /// </remarks>
    [Fact]
    public async Task Ommaviy_yuzada_yozish_metodlari_yoq()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        var kalit = await PublicApiHarness.KalitYaratAsync(admin, PublicApiHarness.NoyobNom("Yozish"));

        using var mijoz = PublicApiHarness.KalitMijozi(_factory, kalit.Key);

        var yol = new Uri($"{PublicApiHarness.Prefix}/employees", UriKind.Relative);

        var metodlar = new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Patch, HttpMethod.Delete };

        foreach (var metod in metodlar)
        {
            using var sorov = new HttpRequestMessage(metod, yol)
            {
                Content = JsonContent.Create(new { lastName = "Тест", firstName = "Тест" }),
            };

            using var javob = await mijoz.SendAsync(sorov, bekorQilish);

            javob.StatusCode.ShouldBeOneOf(
                HttpStatusCode.MethodNotAllowed,
                HttpStatusCode.NotFound);
        }
    }

    // ────────────────────────────────────────────────────────────
    //  O'qish xatti-harakati
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Ping_kalit_nomini_qaytaradi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        var nom = PublicApiHarness.NoyobNom("BonviCall");
        var kalit = await PublicApiHarness.KalitYaratAsync(admin, nom);

        using var mijoz = PublicApiHarness.KalitMijozi(_factory, kalit.Key);

        using var javob = await mijoz.GetAsync(
            new Uri($"{PublicApiHarness.Prefix}/ping", UriKind.Relative),
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await PublicApiHarness.MatnAsync(javob));

        var ping = await javob.Content.ReadFromJsonAsync<PublicPingResponse>(bekorQilish);

        ping.ShouldNotBeNull();
        ping.Ok.ShouldBeTrue();
        ping.Key.ShouldBe(nom);

        // Prefiks ham, kalitning o'zi ham javobda bo'lmasligi kerak.
        var json = await PublicApiHarness.MatnAsync(javob);
        json.ShouldNotContain(kalit.Key);
        json.ShouldNotContain(kalit.Prefix);
    }

    /// <summary>
    /// Yumshoq o'chirilgan xodim ro'yxatda YO'Q, «deletions» da BOR.
    /// </summary>
    [Fact]
    public async Task Ochirilgan_xodim_royxatda_yoq_ochirilganlarda_bor()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var belgi = Guid.NewGuid().ToString("N")[..8];

        var xodimId = await XodimYaratAsync("Ўчирилган" + belgi).ConfigureAwait(true);

        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        var kalit = await PublicApiHarness.KalitYaratAsync(admin, PublicApiHarness.NoyobNom("Ochirish"));
        using var mijoz = PublicApiHarness.KalitMijozi(_factory, kalit.Key);

        // O'chirishdan OLDIN topilishi kerak — aks holda test «yo'q» ni
        // «umuman yaratilmagan» dan ajrata olmasdi.
        using (var oldin = await mijoz.GetAsync(
            new Uri($"{PublicApiHarness.Prefix}/employees/{xodimId}", UriKind.Relative),
            bekorQilish))
        {
            oldin.StatusCode.ShouldBe(HttpStatusCode.OK, await PublicApiHarness.MatnAsync(oldin));
        }

        var ochirishVaqti = DateTimeOffset.UtcNow.AddSeconds(-5);
        await OchirAsync(xodimId).ConfigureAwait(true);

        // ── Ro'yxatda yo'q ────────────────────────────────────────
        using (var bitta = await mijoz.GetAsync(
            new Uri($"{PublicApiHarness.Prefix}/employees/{xodimId}", UriKind.Relative),
            bekorQilish))
        {
            bitta.StatusCode.ShouldBe(HttpStatusCode.NotFound);

            var muammo = await bitta.Content.ReadFromJsonAsync<ProblemResponse>(bekorQilish);
            muammo!.Title.ShouldBe("tashqi.xodim_topilmadi");
        }

        using (var royxat = await mijoz.GetAsync(
            new Uri($"{PublicApiHarness.Prefix}/employees?search=Ўчирилган{belgi}", UriKind.Relative),
            bekorQilish))
        {
            var sahifa = await royxat.Content.ReadFromJsonAsync<PublicPageResponse>(bekorQilish);

            sahifa.ShouldNotBeNull();
            sahifa.Items.ShouldBeEmpty("o'chirilgan xodim ro'yxatda ko'rinmasligi kerak");
        }

        // ── «deletions» da bor ────────────────────────────────────
        using (var ochirilganlar = await mijoz.GetAsync(
            new Uri(
                $"{PublicApiHarness.Prefix}/employees/deletions?since={Uri.EscapeDataString(ochirishVaqti.ToString("O", CultureInfo.InvariantCulture))}",
                UriKind.Relative),
            bekorQilish))
        {
            ochirilganlar.StatusCode.ShouldBe(
                HttpStatusCode.OK,
                await PublicApiHarness.MatnAsync(ochirilganlar));

            var yozuvlar = await ochirilganlar.Content
                .ReadFromJsonAsync<IReadOnlyList<PublicDeletionResponse>>(bekorQilish);

            yozuvlar.ShouldNotBeNull();
            yozuvlar.ShouldContain(yozuv => yozuv.Id == xodimId);
        }
    }

    /// <summary>
    /// <c>updatedSince</c> keyin o'zgargan yozuvni beradi, oldingisini bermaydi.
    /// </summary>
    [Fact]
    public async Task UpdatedSince_faqat_keyin_ozgargan_yozuvni_beradi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var belgi = Guid.NewGuid().ToString("N")[..8];

        var eskiId = await XodimYaratAsync("Синх" + belgi + "Эски").ConfigureAwait(true);

        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        var kalit = await PublicApiHarness.KalitYaratAsync(admin, PublicApiHarness.NoyobNom("Sinx"));
        using var mijoz = PublicApiHarness.KalitMijozi(_factory, kalit.Key);

        // Chegara SERVERNING o'z qiymatidan olinadi, test mashinasining
        // soatidan emas: `updatedAt` ni baza yozadi va ikki manba orasidagi
        // bir necha millisekundlik farq testni tebranuvchi qilib qo'yardi.
        var eski = await XodimAsync(mijoz, eskiId).ConfigureAwait(true);
        var chegara = eski.UpdatedAt;

        // Yangi yozuv chegaradan KEYIN tug'ilishi kafolatlanishi kerak.
        // `timestamptz` mikrosekundgacha aniq, lekin bir xil millisekundda
        // ikki yozuv hosil bo'lishi mumkin — kichik kutish shu tebranishni
        // yopadi.
        await Task.Delay(20, bekorQilish);

        var yangiId = await XodimYaratAsync("Синх" + belgi + "Янги").ConfigureAwait(true);

        using var javob = await mijoz.GetAsync(
            new Uri(
                $"{PublicApiHarness.Prefix}/employees?search=Синх{belgi}&updatedSince={Uri.EscapeDataString(chegara.ToString("O", CultureInfo.InvariantCulture))}",
                UriKind.Relative),
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await PublicApiHarness.MatnAsync(javob));

        var sahifa = await javob.Content.ReadFromJsonAsync<PublicPageResponse>(bekorQilish);

        sahifa.ShouldNotBeNull();
        sahifa.Items.ShouldContain(item => item.Id == yangiId, "keyin yaratilgan yozuv qaytishi kerak");
        sahifa.Items.ShouldNotContain(item => item.Id == eskiId, "oldin yaratilgani qaytmasligi kerak");

        // Chegarasiz so'rovda IKKALASI ham bo'lishi kerak — aks holda test
        // «filtr ishladi» bilan «qidiruv umuman ishlamadi» ni ajrata olmasdi.
        using var chegarasiz = await mijoz.GetAsync(
            new Uri($"{PublicApiHarness.Prefix}/employees?search=Синх{belgi}", UriKind.Relative),
            bekorQilish);

        var hammasi = await chegarasiz.Content.ReadFromJsonAsync<PublicPageResponse>(bekorQilish);

        hammasi.ShouldNotBeNull();
        hammasi.Items.Count.ShouldBe(2);
    }

    /// <summary>
    /// <c>updatedAt</c> mehnat munosabati o'zgarganda ham siljiydi.
    /// </summary>
    /// <remarks>
    /// ⭐ Bu testning sababi amaliy: xodimni boshqa bo'limga ko'chirish
    /// <c>employments</c> jadvalini o'zgartiradi, xodim KARTASIGA esa
    /// tegmaydi (ular alohida agregat). Agar <c>updatedAt</c> faqat
    /// kartaga qarasa, bosqichma-bosqich sinxronlanayotgan iste'molchi
    /// ko'chirishni HECH QACHON ko'rmasdi va uning bazasidagi bo'lim
    /// eskiligicha qolib ketardi — hech qanday xatosiz.
    /// </remarks>
    [Fact]
    public async Task UpdatedAt_munosabat_ozgarganda_ham_siljiydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var belgi = Guid.NewGuid().ToString("N")[..8];

        var xodimId = await XodimYaratAsync("Кўчирилган" + belgi, ishBilan: true).ConfigureAwait(true);

        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        var kalit = await PublicApiHarness.KalitYaratAsync(admin, PublicApiHarness.NoyobNom("Kochirish"));
        using var mijoz = PublicApiHarness.KalitMijozi(_factory, kalit.Key);

        var oldin = await XodimAsync(mijoz, xodimId).ConfigureAwait(true);

        await Task.Delay(20, bekorQilish);

        // FAQAT munosabat o'zgaradi — xodim kartasiga tegilmaydi.
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var munosabat = context.Employments.First(item => item.EmployeeId == xodimId);
            munosabat.SetRate(0.5m);

            await context.SaveChangesAsync(bekorQilish);
        }

        var keyin = await XodimAsync(mijoz, xodimId).ConfigureAwait(true);

        keyin.UpdatedAt.ShouldBeGreaterThan(
            oldin.UpdatedAt,
            "munosabat o'zgarishi «updatedAt» ga chiqishi kerak");

        // Va u aynan shu chegara bilan qaytib kelishi kerak.
        using var javob = await mijoz.GetAsync(
            new Uri(
                $"{PublicApiHarness.Prefix}/employees?updatedSince={Uri.EscapeDataString(oldin.UpdatedAt.ToString("O", CultureInfo.InvariantCulture))}&search=Кўчирилган{belgi}",
                UriKind.Relative),
            bekorQilish);

        var sahifa = await javob.Content.ReadFromJsonAsync<PublicPageResponse>(bekorQilish);

        sahifa.ShouldNotBeNull();
        sahifa.Items.ShouldContain(item => item.Id == xodimId);
    }

    /// <summary>Sahifa hajmi kesiladi, oxiridan keyingi sahifa bo'sh qaytadi.</summary>
    [Fact]
    public async Task Sahifalash_chegarani_kesadi_va_oxiridan_keyin_bosh_beradi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        var kalit = await PublicApiHarness.KalitYaratAsync(admin, PublicApiHarness.NoyobNom("Sahifa"));
        using var mijoz = PublicApiHarness.KalitMijozi(_factory, kalit.Key);

        using (var katta = await mijoz.GetAsync(
            new Uri($"{PublicApiHarness.Prefix}/employees?pageSize=100000", UriKind.Relative),
            bekorQilish))
        {
            katta.StatusCode.ShouldBe(HttpStatusCode.OK, await PublicApiHarness.MatnAsync(katta));

            var sahifa = await katta.Content.ReadFromJsonAsync<PublicPageResponse>(bekorQilish);

            sahifa.ShouldNotBeNull();
            sahifa.PageSize.ShouldBe(200, "chegara 200 ga kesilishi kerak");
        }

        using (var uzoq = await mijoz.GetAsync(
            new Uri($"{PublicApiHarness.Prefix}/employees?page=99999", UriKind.Relative),
            bekorQilish))
        {
            uzoq.StatusCode.ShouldBe(
                HttpStatusCode.OK,
                "oxiridan keyingi sahifa XATO emas: " + await PublicApiHarness.MatnAsync(uzoq));

            var sahifa = await uzoq.Content.ReadFromJsonAsync<PublicPageResponse>(bekorQilish);

            sahifa.ShouldNotBeNull();
            sahifa.Items.ShouldBeEmpty();
        }

        using (var notogriHolat = await mijoz.GetAsync(
            new Uri($"{PublicApiHarness.Prefix}/employees?status=terminatd", UriKind.Relative),
            bekorQilish))
        {
            // Filtr — boshqa gap: noto'g'ri qiymat JIMGINA hammasini
            // bermasligi kerak (sababi so'rov handlerining izohida).
            notogriHolat.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

            var muammo = await notogriHolat.Content.ReadFromJsonAsync<ProblemResponse>(bekorQilish);
            muammo!.Title.ShouldBe("tashqi.holat_notogri");
        }
    }

    /// <summary>To'rtta ma'lumotnoma ham ochiladi, bo'limlarda ota yozuv bor.</summary>
    [Fact]
    public async Task Malumotnoma_royxatlari_ochiladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var belgi = Guid.NewGuid().ToString("N")[..8];

        // Daraxt bo'lagi ataylab: `parentId` FAQAT bo'limlarda to'ladi va
        // bu SPEC §4 dagi yagona farq — uni tekshirmasa, maydon jimgina
        // yo'qolib ketishi mumkin edi.
        var (otaId, bolaId) = await BolimJuftiAsync(belgi).ConfigureAwait(true);

        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        var kalit = await PublicApiHarness.KalitYaratAsync(admin, PublicApiHarness.NoyobNom("Malumotnoma"));
        using var mijoz = PublicApiHarness.KalitMijozi(_factory, kalit.Key);

        var yollar = new[] { "legal-entities", "departments", "positions", "directions" };

        foreach (var yol in yollar)
        {
            using var javob = await mijoz.GetAsync(
                new Uri($"{PublicApiHarness.Prefix}/reference/{yol}", UriKind.Relative),
                bekorQilish);

            javob.StatusCode.ShouldBe(
                HttpStatusCode.OK,
                $"{yol}: {await PublicApiHarness.MatnAsync(javob)}");

            var yozuvlar = await javob.Content
                .ReadFromJsonAsync<IReadOnlyList<PublicReferenceItemResponse>>(bekorQilish);

            yozuvlar.ShouldNotBeNull();

            if (yol != "departments")
            {
                continue;
            }

            var ota = yozuvlar.FirstOrDefault(item => item.Id == otaId);
            var bola = yozuvlar.FirstOrDefault(item => item.Id == bolaId);

            ota.ShouldNotBeNull();
            ota.ParentId.ShouldBeNull("eng yuqori darajadagi bo'limda ota yozuv yo'q");

            bola.ShouldNotBeNull();
            bola.ParentId.ShouldBe(otaId);
        }
    }

    /// <summary>Rasmi yo'q xodim uchun surat marshruti 404 va ommaviy kod beradi.</summary>
    [Fact]
    public async Task Surat_yoq_bolsa_tashqi_kod_bilan_404()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        var xodimId = await XodimYaratAsync("Расмсиз" + Guid.NewGuid().ToString("N")[..8])
            .ConfigureAwait(true);

        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        var kalit = await PublicApiHarness.KalitYaratAsync(admin, PublicApiHarness.NoyobNom("Surat"));
        using var mijoz = PublicApiHarness.KalitMijozi(_factory, kalit.Key);

        using var javob = await mijoz.GetAsync(
            new Uri($"{PublicApiHarness.Prefix}/employees/{xodimId}/photo", UriKind.Relative),
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var muammo = await javob.Content.ReadFromJsonAsync<ProblemResponse>(bekorQilish);
        muammo!.Title.ShouldBe("tashqi.surat_yoq");
    }

    /// <summary>
    /// Rasm ichki marshrut bilan bir xil kesh shartnomasida qaytadi.
    /// </summary>
    /// <remarks>
    /// <c>ETag</c> va <c>private</c> keshi bu yerda ayniqsa muhim: tashqi
    /// iste'molchi xodimlar ro'yxatini chizganda o'nlab rasm so'raydi va
    /// ularning har birini qayta yuklab olish trafikni behuda
    /// ko'paytirardi. <c>private</c> esa majburiy — surat shaxsiy ma'lumot
    /// va oradagi umumiy proksida saqlanmasligi kerak.
    /// </remarks>
    [Fact]
    public async Task Surat_ETag_va_shaxsiy_kesh_bilan_qaytadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        var xodimId = await XodimYaratAsync("Расмли" + Guid.NewGuid().ToString("N")[..8])
            .ConfigureAwait(true);

        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);

        using (var forma = new MultipartFormDataContent())
        using (var qism = new ByteArrayContent(TestImages.PngYasa(32, 32)))
        {
            qism.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            forma.Add(qism, "file", "surat.png");

            using var yuklash = await admin.PostAsync(
                new Uri($"/api/employees/{xodimId}/photo", UriKind.Relative),
                forma,
                bekorQilish);

            yuklash.StatusCode.ShouldBe(HttpStatusCode.OK, await PublicApiHarness.MatnAsync(yuklash));
        }

        var kalit = await PublicApiHarness.KalitYaratAsync(admin, PublicApiHarness.NoyobNom("Rasm"));
        using var mijoz = PublicApiHarness.KalitMijozi(_factory, kalit.Key);

        var yol = new Uri($"{PublicApiHarness.Prefix}/employees/{xodimId}/photo", UriKind.Relative);

        string belgi;

        using (var birinchi = await mijoz.GetAsync(yol, bekorQilish))
        {
            birinchi.StatusCode.ShouldBe(HttpStatusCode.OK, await PublicApiHarness.MatnAsync(birinchi));
            birinchi.Content.Headers.ContentType?.MediaType.ShouldBe("image/jpeg");

            birinchi.Headers.ETag.ShouldNotBeNull();
            belgi = birinchi.Headers.ETag!.Tag;

            birinchi.Headers.CacheControl.ShouldNotBeNull();
            birinchi.Headers.CacheControl!.Private.ShouldBeTrue("surat umumiy proksida saqlanmasin");
            birinchi.Headers.CacheControl.MaxAge.ShouldBe(TimeSpan.FromDays(1));
        }

        using var sorov = new HttpRequestMessage(HttpMethod.Get, yol);
        sorov.Headers.TryAddWithoutValidation("If-None-Match", belgi);

        using var ikkinchi = await mijoz.SendAsync(sorov, bekorQilish);

        ikkinchi.StatusCode.ShouldBe(
            HttpStatusCode.NotModified,
            "o'zgarmagan rasm qayta yuborilmasligi kerak");
    }

    // ────────────────────────────────────────────────────────────
    //  Tezlik cheklovi
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Kalit bilan chegaradan oshgan so'rov 429 va <c>Retry-After</c> oladi.
    /// </summary>
    /// <remarks>
    /// Chegara test uchun pasaytiriladi: haqiqiy 120 ni sanash uchun 121
    /// so'rov yuborish testni sekinlashtirardi va hech narsa qo'shmasdi —
    /// tekshirilayotgan narsa SIYOSAT ishlayotgani, aniq son emas.
    /// </remarks>
    [Fact]
    public async Task Kalit_bilan_chegaradan_oshsa_429_va_RetryAfter()
    {
        const int chegara = 3;
        var bekorQilish = TestContext.Current.CancellationToken;

        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        var kalit = await PublicApiHarness.KalitYaratAsync(admin, PublicApiHarness.NoyobNom("Tezlik"));

        using var cheklangan = CheklanganFabrika(chegara, anonimChegara: 50);
        using var mijoz = cheklangan.CreateClient();
        mijoz.DefaultRequestHeaders.Add(ApiKeyAuthenticationHandler.HeaderName, kalit.Key);

        var yol = new Uri($"{PublicApiHarness.Prefix}/ping", UriKind.Relative);

        for (var i = 0; i < chegara; i++)
        {
            using var oq = await mijoz.GetAsync(yol, bekorQilish);
            oq.StatusCode.ShouldBe(HttpStatusCode.OK, $"{i + 1}-so'rov o'tishi kerak");
        }

        using var oshgan = await mijoz.GetAsync(yol, bekorQilish);

        oshgan.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        oshgan.Headers.RetryAfter.ShouldNotBeNull();
    }

    /// <summary>
    /// KALITSIZ so'rovlar ham sanaladi — kalit tanlab ko'rishga qarshi.
    /// </summary>
    /// <remarks>
    /// ⭐ Bu test konveyer TARTIBINI qo'riqlaydi. Tezlik cheklovi
    /// avtorizatsiyadan KEYIN qo'yilsa, 401 bilan rad etilgan so'rov
    /// cheklovga umuman yetib bormaydi — ya'ni kalit tanlab ko'rish
    /// cheksiz bo'lib qolardi va buni hech qanday boshqa test
    /// ko'rsatmasdi.
    /// </remarks>
    [Fact]
    public async Task Kalitsiz_urinishlar_ham_sanaladi()
    {
        const int chegara = 3;
        var bekorQilish = TestContext.Current.CancellationToken;

        using var cheklangan = CheklanganFabrika(kalitChegara: 50, anonimChegara: chegara);
        using var mijoz = cheklangan.CreateClient();

        var yol = new Uri($"{PublicApiHarness.Prefix}/ping", UriKind.Relative);

        for (var i = 0; i < chegara; i++)
        {
            using var rad = await mijoz.GetAsync(yol, bekorQilish);
            rad.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, $"{i + 1}-urinish 401 bo'lishi kerak");
        }

        using var oshgan = await mijoz.GetAsync(yol, bekorQilish);

        oshgan.StatusCode.ShouldBe(
            HttpStatusCode.TooManyRequests,
            "kalitsiz urinishlar chegarasi ishlashi kerak");

        oshgan.Headers.RetryAfter.ShouldNotBeNull();
    }

    /// <summary>
    /// Ishdan ketgan xodimning OXIRGI ish joyi javobda qoladi.
    /// </summary>
    /// <remarks>
    /// Egasining qarori (2026-09-18): boshqa loyihalar eski yozuvlarda
    /// «bu odam qaysi bo'lim va lavozimda edi» degan savolga javob bera
    /// olishi kerak. Faol munosabat yo'q bo'lsa ASOSIYSI yolg'iz qaytadi —
    /// to'liq tarix baribir chiqmaydi.
    /// </remarks>
    [Fact]
    public async Task Ishdan_ketganda_oxirgi_ish_joyi_qoladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var belgi = Guid.NewGuid().ToString("N")[..8];

        var xodimId = await XodimYaratAsync("Кетган" + belgi, ishBilan: true).ConfigureAwait(true);

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var munosabat = context.Employments.First(item => item.EmployeeId == xodimId);
            munosabat.Terminate(new DateOnly(2026, 9, 1), null).IsSuccess.ShouldBeTrue();

            await context.SaveChangesAsync(bekorQilish);
        }

        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        var kalit = await PublicApiHarness.KalitYaratAsync(admin, PublicApiHarness.NoyobNom("Ketgan"));
        using var mijoz = PublicApiHarness.KalitMijozi(_factory, kalit.Key);

        var xodim = await XodimAsync(mijoz, xodimId).ConfigureAwait(true);

        xodim.Status.ShouldBe("Terminated");
        xodim.TerminationDate.ShouldBe(new DateOnly(2026, 9, 1));

        xodim.Employments.Count.ShouldBe(1, "oxirgi ish joyi bitta yozuv bo'lib qolishi kerak");

        var oxirgi = xodim.Employments[0];

        oxirgi.IsPrimary.ShouldBeTrue();
        oxirgi.Status.ShouldBe("Terminated");
        oxirgi.Department.ShouldNotBeNull();
        oxirgi.Department.Name.ShouldBe("Бўлим Кетган" + belgi);
        oxirgi.Position.ShouldNotBeNull();
        oxirgi.Position.Name.ShouldBe("Лавозим Кетган" + belgi);
    }

    /// <summary>
    /// Qayta ishga olingan xodim eski (tugagan) davri bo'yicha filtrga tushmaydi.
    /// </summary>
    /// <remarks>
    /// 2026-10-11, prod: №91 importidan keyin <c>status=Terminated</c>
    /// ishlayotgan 52 kishini ham qaytardi — filtr barcha davrlarga qarar
    /// edi, javobdagi <c>status</c> esa asosiy munosabatdan olinadi.
    /// Ishdan ketgan odam esa holati va OXIRGI bo'limi bo'yicha topilaveradi.
    /// </remarks>
    [Fact]
    public async Task Qayta_ishga_olingan_eski_davri_boyicha_filtrga_tushmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var belgi = "Қайта" + Guid.NewGuid().ToString("N")[..8];

        var xodimId = await XodimYaratAsync(belgi, ishBilan: true).ConfigureAwait(true);
        var ketganId = await XodimYaratAsync(belgi + "К", ishBilan: true).ConfigureAwait(true);

        Guid eskiBolim, yangiBolim, ketganBolim;

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var eski = context.Employments.First(item => item.EmployeeId == xodimId);
            eski.Terminate(new DateOnly(2024, 1, 31), null).IsSuccess.ShouldBeTrue();
            eski.SetPrimary(false);
            eskiBolim = eski.DepartmentId!.Value;

            var bolim = Department.Create("Янги бўлим " + belgi).Value;
            context.Departments.Add(bolim);
            yangiBolim = bolim.Id;

            context.Employments.Add(Employment
                .Hire(xodimId, eski.LegalEntityId, new DateOnly(2025, 1, 1), bolim.Id, eski.PositionId)
                .Value);

            var ketgan = context.Employments.First(item => item.EmployeeId == ketganId);
            ketgan.Terminate(new DateOnly(2026, 9, 1), null).IsSuccess.ShouldBeTrue();
            ketganBolim = ketgan.DepartmentId!.Value;

            await context.SaveChangesAsync(bekorQilish);
        }

        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        var kalit = await PublicApiHarness.KalitYaratAsync(admin, PublicApiHarness.NoyobNom("Qayta"));
        using var mijoz = PublicApiHarness.KalitMijozi(_factory, kalit.Key);

        async Task<List<Guid>> IdlarAsync(string filtr)
        {
            using var javob = await mijoz.GetAsync(
                new Uri($"{PublicApiHarness.Prefix}/employees?search={Uri.EscapeDataString(belgi)}&{filtr}", UriKind.Relative),
                bekorQilish);

            javob.StatusCode.ShouldBe(HttpStatusCode.OK, $"{filtr}: {await PublicApiHarness.MatnAsync(javob)}");

            var sahifa = await javob.Content.ReadFromJsonAsync<PublicPageResponse>(bekorQilish);
            sahifa.ShouldNotBeNull();
            return [.. sahifa.Items.Select(item => item.Id)];
        }

        (await IdlarAsync("status=Terminated")).ShouldBe([ketganId], "ishlayotgan odam «ketganlar»da chiqmasligi kerak");
        (await IdlarAsync("status=Probation")).ShouldBe([xodimId]);
        (await IdlarAsync($"departmentId={eskiBolim}")).ShouldBeEmpty("eski bo'lim ro'yxatida qayta ishga olingan odam yo'q");
        (await IdlarAsync($"departmentId={yangiBolim}")).ShouldBe([xodimId]);
        (await IdlarAsync($"departmentId={ketganBolim}")).ShouldBe([ketganId], "ketgan odam oxirgi bo'limi bo'yicha topiladi");

        var xodim = await XodimAsync(mijoz, xodimId).ConfigureAwait(true);
        xodim.Status.ShouldBe("Probation");
        xodim.Employments.Select(item => item.Department?.Id).ShouldBe([yangiBolim]);
    }

    /// <summary>
    /// Har bir filtr va ularning birikmasi SQL'ga tarjima bo'ladi.
    /// </summary>
    /// <remarks>
    /// ⭐ Boshqa testlar filtrlarning MAZMUNINI tekshiradi va shu sababli
    /// ularni bittalab, mos ma'lumot bilan chaqiradi. Bu yerda esa mazmun
    /// emas, TARJIMA tekshiriladi: EF ifodani SQL'ga o'gira olmasa, so'rov
    /// faqat shu birikma so'ralganda 500 beradi va bunday xato ish
    /// muhitigacha yetib borishi mumkin. Shuning uchun qiymatlar ataylab
    /// hech narsaga mos kelmaydigan (`Guid.CreateVersion7`) — javobning
    /// bo'sh bo'lishi normal, muhimi 200 qaytishi.
    /// </remarks>
    [Fact]
    public async Task Filtrlar_SQL_ga_tarjima_boladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        var kalit = await PublicApiHarness.KalitYaratAsync(admin, PublicApiHarness.NoyobNom("Filtr"));
        using var mijoz = PublicApiHarness.KalitMijozi(_factory, kalit.Key);

        var bosh = Guid.CreateVersion7();

        var sorovlar = new[]
        {
            "?status=Active",
            $"?legalEntityId={bosh}",
            $"?departmentId={bosh}",
            $"?positionId={bosh}",
            $"?directionId={bosh}",
            $"?status=Terminated&legalEntityId={bosh}&departmentId={bosh}&positionId={bosh}&directionId={bosh}",
            $"?status=Active&updatedSince=2020-01-01T00:00:00Z",
        };

        foreach (var sorov in sorovlar)
        {
            using var javob = await mijoz.GetAsync(
                new Uri($"{PublicApiHarness.Prefix}/employees{sorov}", UriKind.Relative),
                bekorQilish);

            javob.StatusCode.ShouldBe(
                HttpStatusCode.OK,
                $"{sorov}: {await PublicApiHarness.MatnAsync(javob)}");
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    private WebApplicationFactory<Program> CheklanganFabrika(int kalitChegara, int anonimChegara)
        => _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(
                "RateLimiting:PublicApi:PermitLimit",
                kalitChegara.ToString(CultureInfo.InvariantCulture));

            builder.UseSetting(
                "RateLimiting:PublicApiAnonymous:PermitLimit",
                anonimChegara.ToString(CultureInfo.InvariantCulture));
        });

    private static async Task<PublicEmployeeResponse> XodimAsync(HttpClient mijoz, Guid id)
    {
        using var javob = await mijoz.GetAsync(
            new Uri($"{PublicApiHarness.Prefix}/employees/{id}", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await PublicApiHarness.MatnAsync(javob));

        var xodim = await javob.Content.ReadFromJsonAsync<PublicEmployeeResponse>(
            TestContext.Current.CancellationToken);

        xodim.ShouldNotBeNull();
        return xodim;
    }

    /// <summary>
    /// Test uchun xodim yaratadi — to'g'ridan-to'g'ri baza orqali.
    /// </summary>
    /// <remarks>
    /// API orqali emas: ba'zi testlarga mehnat munosabati ham kerak va
    /// uni ishga qabul oqimidan o'tkazish testga aloqasi yo'q o'nlab
    /// tekshiruvni (ma'lumotnoma, belgi, sinov muddati) olib kelardi.
    /// </remarks>
    private async Task<Guid> XodimYaratAsync(string familiya, bool ishBilan = false)
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var xodim = Employee.Create(FullName.Create(familiya, "Ходим", null).Value).Value;
        context.Employees.Add(xodim);

        if (ishBilan)
        {
            var firma = LegalEntity.Create("Фирма " + familiya).Value;
            var bolim = Department.Create("Бўлим " + familiya).Value;
            var lavozim = Position.Create("Лавозим " + familiya).Value;

            context.LegalEntities.Add(firma);
            context.Departments.Add(bolim);
            context.Positions.Add(lavozim);

            context.Employments.Add(Employment
                .Hire(xodim.Id, firma.Id, new DateOnly(2021, 3, 1), bolim.Id, lavozim.Id)
                .Value);
        }

        await context.SaveChangesAsync(bekorQilish);

        return xodim.Id;
    }

    /// <summary>Ota va bola bo'lim yaratadi — daraxt bo'lagi.</summary>
    private async Task<(Guid Ota, Guid Bola)> BolimJuftiAsync(string belgi)
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var ota = Department.Create("Ота бўлим " + belgi).Value;
        var bola = Department.Create("Бола бўлим " + belgi, ota.Id).Value;

        context.Departments.Add(ota);
        context.Departments.Add(bola);

        await context.SaveChangesAsync(bekorQilish);

        return (ota.Id, bola.Id);
    }

    /// <summary>
    /// Xodimni YUMSHOQ o'chiradi.
    /// </summary>
    /// <remarks>
    /// <c>Remove</c> ataylab: audit interceptori uni <c>deleted_at</c> ga
    /// aylantiradi (haqiqiy tizimdagi bilan aynan bir xil yo'l), ya'ni
    /// test ustunni qo'lda to'ldirib «o'chirish»ni taqlid qilmaydi.
    /// </remarks>
    private async Task OchirAsync(Guid xodimId)
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var xodim = context.Employees.First(item => item.Id == xodimId);
        context.Employees.Remove(xodim);

        await context.SaveChangesAsync(bekorQilish);
    }
}

/// <summary>Ma'lumotnoma qatori — testlar uchun.</summary>
public sealed record PublicReferenceItemResponse(Guid Id, string Name, Guid? ParentId);
