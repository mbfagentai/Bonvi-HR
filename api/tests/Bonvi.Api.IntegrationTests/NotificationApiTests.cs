using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Bildirishnoma havolasi — testga kerakli qismi.</summary>
public sealed record NotificationLinkResponse(string Route, Guid? Id);

/// <summary>Bitta bildirishnoma.</summary>
/// <remarks>
/// <c>Type</c> va <c>Severity</c> ATAYLAB satr: sanoq qiymatlari JSON'da
/// nom bo'lib chiqadi (<c>JsonStringEnumConverter</c>). Ular raqamga
/// qaytsa, bu test deserializatsiyada yiqiladi va o'zgarish e'tibordan
/// chetda qolmaydi.
/// </remarks>
public sealed record NotificationResponse(
    string Key,
    string Type,
    string Severity,
    DateOnly Date,
    IReadOnlyDictionary<string, string> Params,
    NotificationLinkResponse Link,
    bool IsRead,
    DateTimeOffset? ReadAt);

/// <summary>Bildirishnomalar ro'yxati.</summary>
public sealed record NotificationListResponse(
    IReadOnlyList<NotificationResponse> Items,
    int Total,
    int UnreadCount);

/// <summary>O'qilmaganlar soni.</summary>
public sealed record NotificationCountResponse(int UnreadCount);

/// <summary>«O'qildi»/«o'chirildi» amalining natijasi.</summary>
public sealed record NotificationActionResponse(int Affected, int UnreadCount);

/// <summary>
/// Bildirishnomalarning uchi-uchiga testlari.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ Bu yerda tekshirilayotgan asosiy narsa — KALIT BARQARORLIGI
/// HAQIQIY BAZADA. Bildirishnomalar saqlanmaydi: har so'rovda joriy
/// ma'lumotdan qaytadan hisoblanadi. Agar kalit hisoblashdan hisoblashga
/// o'zgarsa, «o'qildi» belgisi jimgina yo'qolardi — va buni faqat
/// foydalanuvchi sezardi.
/// </para>
/// <para>
/// <b>Nega aniq sonlarga bog'lanilmaydi.</b> Bu sinfning testlari bitta
/// bazani baham ko'radi va har biri o'z yozuvlarini qo'shadi. Shu
/// sababli har bir test O'ZI qo'shgan yozuvni belgisi bo'yicha topadi va
/// faqat unga tegishli xulosa chiqaradi.
/// </para>
/// </remarks>
public sealed class NotificationApiTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public NotificationApiTests(BonviApiFactory factory) => _factory = factory;

    /// <summary>Bitta testning yozuvlarini ajratib turadigan belgi.</summary>
    private static string Belgi() => "BX" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    // ────────────────────────────────────────────────────────────
    //  Ruxsat
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Tokensiz_sorov_401_qaytaradi()
    {
        using var client = _factory.CreateClient();

        using var royxat = await client.GetAsync(
            new Uri("/api/notifications", UriKind.Relative),
            TestContext.Current.CancellationToken);

        using var sanoq = await client.GetAsync(
            new Uri("/api/notifications/unread-count", UriKind.Relative),
            TestContext.Current.CancellationToken);

        royxat.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        sanoq.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ────────────────────────────────────────────────────────────
    //  Tug'ilgan kunlar — har biri alohida
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Foydalanuvchining aniq talabi: yig'ma «bugun 2 ta tug'ilgan kun»
    /// emas, har bir xodim uchun alohida yozuv.
    /// </summary>
    [Fact]
    public async Task Har_bir_tugilgan_kun_alohida_yozuv_boladi()
    {
        var belgi = Belgi();
        using var client = await AdminMijozAsync();

        var bugun = await BugunAsync(client);

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
            var firma = LegalEntity.Create($"{belgi} Туғилган МЧЖ").Value;
            kontekst.Add(firma);

            foreach (var nom in new[] { "Биринчи", "Иккинчи" })
            {
                var xodim = Employee.Create(FullName.Create(nom, belgi, null).Value).Value;
                xodim.SetBirthDate(OttizYilOldin(bugun)).IsSuccess.ShouldBeTrue();

                kontekst.Add(xodim);
                kontekst.Add(Doimiy(xodim.Id, firma.Id, bugun.AddYears(-3)));
            }

            await kontekst.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var royxat = await RoyxatAsync(client);

        var meniki = royxat.Items
            .Where(item => item.Params.TryGetValue("employeeName", out var nom) && nom.Contains(belgi, StringComparison.Ordinal))
            .ToList();

        meniki.Count.ShouldBe(2, "har bir xodim uchun alohida bildirishnoma");
        meniki.Select(item => item.Key).Distinct(StringComparer.Ordinal).Count().ShouldBe(2);
        meniki.ShouldAllBe(item => item.Type == "BirthdayToday");
        meniki.ShouldAllBe(item => item.Link.Route == "Employee");
        meniki.ShouldAllBe(item => !item.IsRead);
    }

    // ────────────────────────────────────────────────────────────
    //  Kalit barqarorligi: o'qildi → qayta hisoblash → hamon o'qilgan
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// ⭐ Butun bo'limning poydevori. Ro'yxat ikkinchi marta so'ralganda
    /// BUTUNLAY yangidan hisoblanadi — kalit barqaror bo'lgani uchun
    /// «o'qildi» belgisi topiladi.
    /// </summary>
    [Fact]
    public async Task Oqilgan_bildirishnoma_qayta_hisoblashdan_song_ham_oqilgan_qoladi()
    {
        var belgi = Belgi();
        using var client = await AdminMijozAsync();

        var bugun = await BugunAsync(client);
        await SinovYaratAsync(belgi, bugun, tugashigaKun: 3);

        var yozuv = await MenikiAsync(client, belgi);
        yozuv.Type.ShouldBe("ProbationEndingSoon");
        yozuv.Severity.ShouldBe("Warning");
        yozuv.Params["daysLeft"].ShouldBe("3");
        yozuv.IsRead.ShouldBeFalse();

        var amal = await AmalAsync(client, "read", new { keys = new[] { yozuv.Key } });
        amal.Affected.ShouldBe(1);

        var keyin = await MenikiAsync(client, belgi);

        keyin.Key.ShouldBe(yozuv.Key, "kalit qayta hisoblashda O'ZGARMASLIGI kerak");
        keyin.IsRead.ShouldBeTrue();
        keyin.ReadAt.ShouldNotBeNull();
    }

    /// <summary>
    /// O'chirilgan bildirishnoma sharti hamon rost bo'lsa ham qaytmaydi —
    /// foydalanuvchining aniq talabi.
    /// </summary>
    [Fact]
    public async Task Ochirilgan_bildirishnoma_qaytmaydi()
    {
        var belgi = Belgi();
        using var client = await AdminMijozAsync();

        var bugun = await BugunAsync(client);

        // Muddati O'TIB KETGAN sinov: shart har kuni rost bo'lib turadi,
        // ya'ni faqat o'chirish uni ro'yxatdan olib tashlaydi.
        await SinovYaratAsync(belgi, bugun, tugashigaKun: -10);

        var yozuv = await MenikiAsync(client, belgi);
        yozuv.Type.ShouldBe("ProbationOverdue");
        yozuv.Severity.ShouldBe("Critical");
        yozuv.Params["daysOverdue"].ShouldBe("10");

        await AmalAsync(client, "delete", new { keys = new[] { yozuv.Key } });

        var royxat = await RoyxatAsync(client);

        royxat.Items.ShouldNotContain(item => item.Key == yozuv.Key);
    }

    // ────────────────────────────────────────────────────────────
    //  Sanoq va tekshiruvlar
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Sanoq endpoint'i ro'yxat javobidagi son bilan mos kelishi kerak:
    /// ikkalasi bir xil yo'ldan hisoblanadi.
    /// </summary>
    [Fact]
    public async Task Sanoq_endpointi_royxat_bilan_mos_keladi()
    {
        var belgi = Belgi();
        using var client = await AdminMijozAsync();

        var bugun = await BugunAsync(client);
        await SinovYaratAsync(belgi, bugun, tugashigaKun: 1);

        var royxat = await RoyxatAsync(client);

        using var javob = await client.GetAsync(
            new Uri("/api/notifications/unread-count", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var sanoq = await javob.Content.ReadFromJsonAsync<NotificationCountResponse>(
            TestContext.Current.CancellationToken);

        sanoq.ShouldNotBeNull();
        sanoq.UnreadCount.ShouldBe(royxat.UnreadCount);
        sanoq.UnreadCount.ShouldBeGreaterThan(0);
    }

    [Theory]
    [InlineData("shunchaki_matn")]
    [InlineData("nomalum.tur:0199c0de-1234-7abc-8def-0123456789ab")]
    public async Task Notogri_kalit_400_qaytaradi(string kalit)
    {
        using var client = await AdminMijozAsync();

        using var javob = await client.PostAsJsonAsync(
            new Uri("/api/notifications/read", UriKind.Relative),
            new { keys = new[] { kalit } },
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(javob));
        (await MatnAsync(javob)).ShouldContain("bildirishnoma.kalit_notogri");
    }

    [Fact]
    public async Task Bosh_royxat_bilan_amal_bajarilmaydi()
    {
        using var client = await AdminMijozAsync();

        using var javob = await client.PostAsJsonAsync(
            new Uri("/api/notifications/delete", UriKind.Relative),
            new { keys = Array.Empty<string>() },
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(javob));
        (await MatnAsync(javob)).ShouldContain("bildirishnoma.kalit_yoq");
    }

    /// <summary>
    /// «Hammasini o'qildi» kalitlar ro'yxatisiz ishlaydi va o'qilmaganlar
    /// sonini nolga tushiradi.
    /// </summary>
    [Fact]
    public async Task Hammasini_oqildi_sanoqni_nolga_tushiradi()
    {
        var belgi = Belgi();
        using var client = await AdminMijozAsync();

        var bugun = await BugunAsync(client);
        await SinovYaratAsync(belgi, bugun, tugashigaKun: 2);

        var amal = await AmalAsync(client, "read", new { all = true });

        amal.UnreadCount.ShouldBe(0);

        var royxat = await RoyxatAsync(client);
        royxat.UnreadCount.ShouldBe(0);
        royxat.Items.ShouldAllBe(item => item.IsRead);
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    /// <summary>Xodim, munosabat va berilgan kunda tugaydigan sinov muddatini yozadi.</summary>
    private async Task SinovYaratAsync(string belgi, DateOnly bugun, int tugashigaKun)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var firma = LegalEntity.Create($"{belgi} Синов МЧЖ").Value;
        var xodim = Employee.Create(FullName.Create("Синовдаги", belgi, null).Value).Value;

        // `Hire` munosabatni DOIM «sinovda» holatida ochadi — bu domen
        // qoidasi («hamma sinovdan o'tadi, istisnosiz»).
        var munosabat = Employment.Hire(xodim.Id, firma.Id, bugun.AddDays(-90)).Value;

        // Sinov yozuvi ATAYLAB `Import` bilan: tugash sanasi aniq
        // qo'yilishi kerak, `Start` esa uni kunlardan hisoblaydi va
        // muddati o'tib ketgan holatni umuman yarata olmaydi.
        var sinov = Probation.Import(munosabat.Id, bugun.AddDays(-90), bugun.AddDays(tugashigaKun)).Value;

        kontekst.Add(firma);
        kontekst.Add(xodim);
        kontekst.Add(munosabat);
        kontekst.Add(sinov);

        await kontekst.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Shu test qo'shgan YAGONA bildirishnomani topadi.</summary>
    private static async Task<NotificationResponse> MenikiAsync(HttpClient client, string belgi)
    {
        var royxat = await RoyxatAsync(client);

        var meniki = royxat.Items
            .Where(item => item.Params.TryGetValue("employeeName", out var nom)
                && nom.Contains(belgi, StringComparison.Ordinal))
            .ToList();

        return meniki.ShouldHaveSingleItem();
    }

    private static async Task<NotificationListResponse> RoyxatAsync(HttpClient client)
    {
        // Chegara ataylab yuqori: bu sinfning testlari bitta bazani baham
        // ko'radi va sukut bo'yicha 50 ta yozuv yetmay qolishi mumkin.
        using var javob = await client.GetAsync(
            new Uri("/api/notifications?limit=200", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var royxat = await javob.Content.ReadFromJsonAsync<NotificationListResponse>(
            TestContext.Current.CancellationToken);

        royxat.ShouldNotBeNull();
        return royxat;
    }

    private static async Task<NotificationActionResponse> AmalAsync(
        HttpClient client,
        string yol,
        object tana)
    {
        using var javob = await client.PostAsJsonAsync(
            new Uri($"/api/notifications/{yol}", UriKind.Relative),
            tana,
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var natija = await javob.Content.ReadFromJsonAsync<NotificationActionResponse>(
            TestContext.Current.CancellationToken);

        natija.ShouldNotBeNull();
        return natija;
    }

    /// <summary>
    /// Serverning «bugun»i.
    /// </summary>
    /// <remarks>
    /// Testda <c>DateTime.Now</c> ishlatilsa, yarim tunda sana serverникidan
    /// (Toshkent vaqti, <c>IClock</c>) ajralib ketardi — va sinov muddati
    /// kunlarda hisoblangani uchun test bir kunda bir marta yiqilardi.
    /// </remarks>
    private static async Task<DateOnly> BugunAsync(HttpClient client)
    {
        using var javob = await client.GetAsync(
            new Uri("/api/dashboard/stats?period=day", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var sonlar = await javob.Content.ReadFromJsonAsync<DashboardStatsResponse>(
            TestContext.Current.CancellationToken);

        sonlar.ShouldNotBeNull();
        return sonlar.To;
    }

    private static Employment Doimiy(Guid xodimId, Guid firmaId, DateOnly sana)
    {
        var munosabat = Employment.Hire(xodimId, firmaId, sana).Value;
        munosabat.CompleteProbation(sana).IsSuccess.ShouldBeTrue();
        return munosabat;
    }

    /// <summary>Berilgan sananing kun-oyi bilan, taxminan 30 yil oldingi sana.</summary>
    /// <remarks>
    /// 29-fevral uchun alohida qadam bor: 30 yil oldingi yil kabisa bo'lmasa,
    /// <c>new DateOnly(...)</c> umuman yaratilmaydi.
    /// </remarks>
    private static DateOnly OttizYilOldin(DateOnly kun)
    {
        var yil = kun.Year - 30;

        while (kun is { Month: 2, Day: 29 } && !DateTime.IsLeapYear(yil))
        {
            yil--;
        }

        return new DateOnly(yil, kun.Month, kun.Day);
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

    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
