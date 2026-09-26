using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Bonvi.Api.Extensions;
using Bonvi.Application.PublicApi.Abstractions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.PublicApi;
using Bonvi.Infrastructure.Persistence;
using Bonvi.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// API kalitlarini boshqarish: yaratish, ro'yxat, bekor qilish va
/// «oxirgi ishlatilgan» belgisi.
/// </summary>
public sealed class PublicApiKeyTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public PublicApiKeyTests(BonviApiFactory factory) => _factory = factory;

    // ────────────────────────────────────────────────────────────
    //  Kalit BIR MARTA ko'rinadi
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Kalit yaratish javobida bir marta qaytadi; ro'yxatda esa hech
    /// qachon; bazada faqat xesh yotadi.
    /// </summary>
    /// <remarks>
    /// Uch tekshiruv bitta testda ataylab: ular BITTA kafolatning uch
    /// tomoni («kalit tiklanmaydi»). Alohida bo'lsa, bittasi o'chib
    /// ketganda qolganlari baribir yashil qolardi.
    /// </remarks>
    [Fact]
    public async Task Kalit_faqat_yaratishda_qaytadi_bazada_esa_xesh_yotadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        var nom = PublicApiHarness.NoyobNom("Yaratish");
        var kalit = await PublicApiHarness.KalitYaratAsync(admin, nom);

        // ── 1. Yaratish javobi ────────────────────────────────────
        kalit.Key.ShouldStartWith("bonvi_");
        kalit.Key.ShouldContain(kalit.Prefix);
        kalit.Prefix.Length.ShouldBe(ApiKeyFormat.PrefixLength);
        kalit.Name.ShouldBe(nom);

        // ── 2. Ro'yxat javobi — kalitning bironta bo'lagi ham yo'q ──
        using (var royxat = await admin.GetAsync(new Uri("/api/api-keys", UriKind.Relative), bekorQilish))
        {
            royxat.StatusCode.ShouldBe(HttpStatusCode.OK, await PublicApiHarness.MatnAsync(royxat));

            var json = await PublicApiHarness.MatnAsync(royxat);

            json.Contains(kalit.Prefix, StringComparison.Ordinal)
                .ShouldBeTrue("prefiks ko'rinishi KERAK — u kalitning ochiq qismi");
            json.Contains(kalit.Key, StringComparison.Ordinal)
                .ShouldBeFalse("kalitning o'zi ro'yxatda bo'lmasligi kerak");

            // Sir qismi ham alohida tekshiriladi: kalit boshqa formatda
            // (masalan prefiksisiz) qaytib qolmasin.
            var sir = kalit.Key.Split('_')[2];
            json.Contains(sir, StringComparison.Ordinal)
                .ShouldBeFalse("kalitning sir qismi ro'yxatda bo'lmasligi kerak");
        }

        // ── 3. Bazadagi qiymat kalitga TENG EMAS ──────────────────
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var yozuv = await context.ApiKeys
            .AsNoTracking()
            .FirstAsync(item => item.Id == kalit.Id, bekorQilish);

        yozuv.KeyHash.Length.ShouldBe(ApiKeyFormat.HashLength);
        yozuv.KeyHash.ShouldNotBe(Encoding.UTF8.GetBytes(kalit.Key), "bazada xesh, kalit emas");
        yozuv.KeyHash.ShouldBe(ApiKeyFormat.ComputeHash(kalit.Key), "xesh TO'LIQ kalitdan olinadi");

        // Kalitning hech qanday bo'lagi ochiq ustunlarda turmasligi kerak.
        yozuv.Name.ShouldBe(nom);
        yozuv.KeyPrefix.ShouldBe(kalit.Prefix);
    }

    /// <summary>Shu nomdagi kuchdagi kalit bo'lsa — 409.</summary>
    /// <remarks>
    /// Bekor qilingandan keyin AYNAN o'sha nom qayta ishlatiladi: sizib
    /// ketgan kalitni almashtirish «BonviCall-2» kabi nomlarga olib
    /// kelmasligi kerak (sababi migratsiyadagi qisman indeks izohida).
    /// </remarks>
    [Fact]
    public async Task Nom_band_bolsa_409_bekor_qilingandan_keyin_qayta_ishlatiladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        var nom = PublicApiHarness.NoyobNom("Band");

        var birinchi = await PublicApiHarness.KalitYaratAsync(admin, nom);

        using (var takror = await admin.PostAsJsonAsync(
            new Uri("/api/api-keys", UriKind.Relative),
            new { name = nom },
            bekorQilish))
        {
            takror.StatusCode.ShouldBe(HttpStatusCode.Conflict, await PublicApiHarness.MatnAsync(takror));

            var muammo = await takror.Content.ReadFromJsonAsync<ProblemResponse>(bekorQilish);
            muammo!.Title.ShouldBe("tashqi.kalit_nomi_band");
        }

        // Registrga sezgir emas: «BONVICALL» ham band hisoblanadi.
        using (var boshqaRegistr = await admin.PostAsJsonAsync(
            new Uri("/api/api-keys", UriKind.Relative),
            new { name = nom.ToUpperInvariant() },
            bekorQilish))
        {
            boshqaRegistr.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        }

        using (var bekorlash = await admin.PostAsync(
            new Uri($"/api/api-keys/{birinchi.Id}/revoke", UriKind.Relative),
            content: null,
            bekorQilish))
        {
            bekorlash.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        // Endi nom bo'sh — o'sha loyihaga yangi kalit beriladi.
        var ikkinchi = await PublicApiHarness.KalitYaratAsync(admin, nom);

        ikkinchi.Id.ShouldNotBe(birinchi.Id);
        ikkinchi.Key.ShouldNotBe(birinchi.Key);
    }

    /// <summary>Bo'sh nom — 400.</summary>
    [Fact]
    public async Task Bosh_nom_400_beradi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);

        using var javob = await admin.PostAsJsonAsync(
            new Uri("/api/api-keys", UriKind.Relative),
            new { name = "   " },
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await PublicApiHarness.MatnAsync(javob));

        var muammo = await javob.Content.ReadFromJsonAsync<ProblemResponse>(bekorQilish);
        muammo!.Title.ShouldBe("tashqi.kalit_nomi_bosh");
    }

    /// <summary>Bekor qilish IDEMPOTENT va yozuv ro'yxatda qoladi.</summary>
    [Fact]
    public async Task Bekor_qilish_idempotent_va_yozuv_royxatda_qoladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        var kalit = await PublicApiHarness.KalitYaratAsync(admin, PublicApiHarness.NoyobNom("Idempotent"));

        var yol = new Uri($"/api/api-keys/{kalit.Id}/revoke", UriKind.Relative);

        using (var birinchi = await admin.PostAsync(yol, content: null, bekorQilish))
        {
            birinchi.StatusCode.ShouldBe(HttpStatusCode.OK, await PublicApiHarness.MatnAsync(birinchi));

            var yozuv = await birinchi.Content.ReadFromJsonAsync<ApiKeyListResponse>(bekorQilish);

            yozuv.ShouldNotBeNull();
            yozuv.IsActive.ShouldBeFalse();
            yozuv.RevokedAt.ShouldNotBeNull();
        }

        // Chegara BAZADAN o'qiladi, birinchi javobdan emas: birinchi javob
        // xotiradagi qiymatni qaytaradi (100 ns aniqlik), `timestamptz` esa
        // mikrosekundgacha saqlaydi. Ikkalasini taqqoslash testni oxirgi
        // raqam ustida tebranuvchi qilardi.
        var birinchiVaqt = (await KalitAsync(admin, kalit.Id).ConfigureAwait(true)).RevokedAt;

        birinchiVaqt.ShouldNotBeNull();

        using (var ikkinchi = await admin.PostAsync(yol, content: null, bekorQilish))
        {
            ikkinchi.StatusCode.ShouldBe(
                HttpStatusCode.OK,
                "ikkinchi bekor qilish XATO emas: " + await PublicApiHarness.MatnAsync(ikkinchi));

            var yozuv = await ikkinchi.Content.ReadFromJsonAsync<ApiKeyListResponse>(bekorQilish);

            // Birinchi bekor qilishning vaqti SAQLANADI — audit uchun
            // ma'noli sana aynan u.
            yozuv!.RevokedAt.ShouldBe(birinchiVaqt);
        }

        using (var royxat = await admin.GetAsync(new Uri("/api/api-keys", UriKind.Relative), bekorQilish))
        {
            var yozuvlar = await royxat.Content
                .ReadFromJsonAsync<IReadOnlyList<ApiKeyListResponse>>(bekorQilish);

            yozuvlar.ShouldNotBeNull();
            yozuvlar.ShouldContain(
                item => item.Id == kalit.Id && !item.IsActive,
                "bekor qilingan kalit ro'yxatdan YO'QOLMAYDI — u tarix");
        }

        using (var notanish = await admin.PostAsync(
            new Uri($"/api/api-keys/{Guid.CreateVersion7()}/revoke", UriKind.Relative),
            content: null,
            bekorQilish))
        {
            notanish.StatusCode.ShouldBe(HttpStatusCode.NotFound);

            var muammo = await notanish.Content.ReadFromJsonAsync<ProblemResponse>(bekorQilish);
            muammo!.Title.ShouldBe("tashqi.kalit_topilmadi");
        }
    }

    /// <summary>Kalitlarni boshqarish «settings.manage» ostida.</summary>
    /// <remarks>
    /// Ruxsatsiz, lekin HAQIQIY token — eng xavfli holat: autentifikatsiya
    /// o'tadi, avtorizatsiya esa to'xtatishi kerak.
    /// </remarks>
    [Fact]
    public async Task Ruxsatsiz_foydalanuvchi_kalitlarga_tegolmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await RuxsatsizTokenAsync().ConfigureAwait(true));

        using (var oqish = await client.GetAsync(new Uri("/api/api-keys", UriKind.Relative), bekorQilish))
        {
            oqish.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using (var yozish = await client.PostAsJsonAsync(
            new Uri("/api/api-keys", UriKind.Relative),
            new { name = PublicApiHarness.NoyobNom("Ruxsatsiz") },
            bekorQilish))
        {
            yozish.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using var tokensiz = _factory.CreateClient();

        using var tokensizJavob = await tokensiz.GetAsync(
            new Uri("/api/api-keys", UriKind.Relative),
            bekorQilish);

        tokensizJavob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ────────────────────────────────────────────────────────────
    //  «Oxirgi ishlatilgan» belgisi
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Kalit ishlatilgach «lastUsedAt» to'ladi.
    /// </summary>
    /// <remarks>
    /// Yozuv so'rov yo'lidan TASHQARIDA bajariladi (sababi
    /// <c>ApiKeyUsageTracker</c> izohida), shu sababli test darhol
    /// tekshirmaydi — u qiymat paydo bo'lishini kutadi. Kutish chegarasi
    /// bor: usiz nosozlik testni muzlatib qo'yardi.
    /// </remarks>
    [Fact]
    public async Task Kalit_ishlatilgach_lastUsedAt_toladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        var kalit = await PublicApiHarness.KalitYaratAsync(admin, PublicApiHarness.NoyobNom("Ishlatilgan"));

        using (var mijoz = PublicApiHarness.KalitMijozi(_factory, kalit.Key))
        {
            using var ping = await mijoz.GetAsync(
                new Uri($"{PublicApiHarness.Prefix}/ping", UriKind.Relative),
                bekorQilish);

            ping.StatusCode.ShouldBe(HttpStatusCode.OK, await PublicApiHarness.MatnAsync(ping));
        }

        var kutilgan = await KutAsync(
                async () =>
                {
                    using var scope = _factory.Services.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var yozuv = await context.ApiKeys
                        .AsNoTracking()
                        .FirstAsync(item => item.Id == kalit.Id, bekorQilish);

                    return yozuv.LastUsedAt is not null;
                })
            .ConfigureAwait(true);

        kutilgan.ShouldBeTrue("«oxirgi ishlatilgan» belgisi fon yozuvi bilan to'lishi kerak");
    }

    /// <summary>
    /// «Oxirgi ishlatilgan» yozuvining NOSOZLIGI o'qishga tegmaydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bu SPEC §9 dagi talab: «a failure to update it does not fail the
    /// read». Tekshiruv haqiqiy sinf ustida bajariladi — repozitoriy
    /// ataylab istisno tashlaydi va <c>Schedule</c> dan hech narsa
    /// chiqmasligi kerak.
    /// </para>
    /// <para>
    /// Nega integratsiya so'rovi emas: bazani so'rov davomida sindirishning
    /// ishonchli yo'li yo'q. Kafolat esa aynan shu sinf ichida — u
    /// <c>Task.Run</c> ichidagi hamma narsani tutadi va o'qish so'rovi uni
    /// umuman kutmaydi.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Belgini_yozib_bolmasa_ham_istisno_chiqmaydi()
    {
        var services = new ServiceCollection();
        services.AddScoped<IApiKeyRepository, YiqiladiganKalitRepozitoriysi>();
        services.AddScoped<IUnitOfWork, HechNarsaQilmaydiganUnitOfWork>();

        await using var provider = services.BuildServiceProvider();

        var jurnal = new SanaydiganJurnal();

        var kuzatuvchi = new ApiKeyUsageTracker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new QotirilganSoat(DateTimeOffset.UtcNow),
            jurnal);

        // Asosiy talab: chaqiruv istisno TASHLAMAYDI.
        Should.NotThrow(() => kuzatuvchi.Schedule(Guid.CreateVersion7(), "127.0.0.1"));

        // Fon vazifasi ham «kuzatilmagan istisno» qoldirmasligi kerak —
        // uning isboti: nosozlik OGOHLANTIRISH bo'lib jurnalga tushadi.
        var yozildi = await KutAsync(() => Task.FromResult(jurnal.Ogohlantirishlar > 0)).ConfigureAwait(true);

        yozildi.ShouldBeTrue("nosozlik jimgina yo'qolmasligi, jurnalga tushishi kerak");
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    /// <summary>Ro'yxatdan bitta kalitni oladi.</summary>
    private static async Task<ApiKeyListResponse> KalitAsync(HttpClient admin, Guid id)
    {
        using var javob = await admin.GetAsync(
            new Uri("/api/api-keys", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await PublicApiHarness.MatnAsync(javob));

        var yozuvlar = await javob.Content
            .ReadFromJsonAsync<IReadOnlyList<ApiKeyListResponse>>(TestContext.Current.CancellationToken);

        var kalit = yozuvlar?.FirstOrDefault(item => item.Id == id);

        kalit.ShouldNotBeNull();
        return kalit;
    }

    /// <summary>
    /// Shart bajarilishini kutadi (fon vazifalari uchun).
    /// </summary>
    /// <remarks>
    /// Chegara 5 soniya: fon yozuvi odatda o'n millisekundda tugaydi,
    /// lekin sekin CI mashinasida biroz kechikishi mumkin. Cheksiz
    /// kutish esa nosozlikni «test muzlab qoldi» ga aylantirardi.
    /// </remarks>
    private static async Task<bool> KutAsync(Func<Task<bool>> shart)
    {
        var chegara = DateTimeOffset.UtcNow.AddSeconds(5);

        while (DateTimeOffset.UtcNow < chegara)
        {
            if (await shart().ConfigureAwait(true))
            {
                return true;
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        return false;
    }

    /// <summary>
    /// Haqiqiy, lekin ruxsatlarsiz access token yasaydi.
    /// </summary>
    /// <remarks>
    /// <c>CatalogApiTests.RuxsatsizTokenAsync</c> dagi bilan bir xil naqsh:
    /// bazada faqat administrator bor va unda barcha ruxsat mavjud, shu
    /// sababli token to'g'ridan-to'g'ri token servisidan, bo'sh ruxsat
    /// to'plami bilan olinadi.
    /// <para>
    /// 2026-09-26 dan token tekshiruvi ruxsatlarni BAZADAN oladi — tokenga
    /// bo'sh ro'yxat yozish endi ishlamaydi. Shu sababli ruxsatsiz rolli
    /// haqiqiy hisob yaratiladi (<see cref="TestAccounts"/>).
    /// </para>
    /// </remarks>
    private Task<string> RuxsatsizTokenAsync() => TestAccounts.TokenAsync(_factory);
}

/// <summary>Har doim yiqiladigan repozitoriy — nosozlik yo'lini tekshirish uchun.</summary>
internal sealed class YiqiladiganKalitRepozitoriysi : IApiKeyRepository
{
    public Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("baza yiqildi (test)");

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("baza yiqildi (test)");

    public void Add(ApiKey aggregate) => throw new InvalidOperationException("baza yiqildi (test)");

    public void Remove(ApiKey aggregate) => throw new InvalidOperationException("baza yiqildi (test)");

    public Task<ApiKey?> FindByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("baza yiqildi (test)");

    public Task<bool> ActiveNameExistsAsync(string name, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("baza yiqildi (test)");

    public Task<IReadOnlyList<ApiKey>> ListAsync(CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("baza yiqildi (test)");
}

/// <summary>Hech narsa saqlamaydigan tranzaksiya chegarasi.</summary>
internal sealed class HechNarsaQilmaydiganUnitOfWork : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
}

/// <summary>Qotirilgan soat.</summary>
internal sealed class QotirilganSoat : IClock
{
    private readonly DateTimeOffset _hozir;

    public QotirilganSoat(DateTimeOffset hozir) => _hozir = hozir;

    public DateTimeOffset UtcNow => _hozir;

    public DateOnly Today => DateOnly.FromDateTime(_hozir.UtcDateTime);
}

/// <summary>
/// Ogohlantirishlarni sanaydigan jurnal.
/// </summary>
/// <remarks>
/// <c>IsEnabled</c> ATAYLAB <c>true</c>: <c>[LoggerMessage]</c> manba
/// generatori yozishdan oldin aynan shuni so'raydi va <c>false</c> bo'lsa
/// yozuv umuman bajarilmasdi — test esa «yozildi» ni hech qachon
/// ko'rmasdi.
/// </remarks>
internal sealed class SanaydiganJurnal : ILogger<ApiKeyUsageTracker>
{
    private int _ogohlantirishlar;

    public int Ogohlantirishlar => Volatile.Read(ref _ogohlantirishlar);

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (logLevel >= LogLevel.Warning)
        {
            Interlocked.Increment(ref _ogohlantirishlar);
        }
    }
}
