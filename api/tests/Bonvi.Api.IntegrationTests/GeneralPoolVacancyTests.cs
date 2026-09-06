using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bonvi.Api.Startup;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.Settings;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Botga beriladigan sozlamalar — testga kerakli qismi.</summary>
/// <remarks>
/// Ataylab TO'LIQ emas: test aynan oferta havolasi haqida. Qolgan
/// maydonlar bu yerda bo'lmagani javobda ular yo'q degani emas — JSON
/// o'qishda mos kelmagan maydonlar shunchaki tashlanadi.
/// </remarks>
public sealed record BotSettingsResponse(string OfferUrl, int ConversationTimeoutMinutes);

/// <summary>
/// Anketa oqimining tizim sozlamasi: «Umumiy anketa» vakansiyasi
/// (urug'lantirish, yashirin sozlama, bot ro'yxatidagi filtr) va
/// ommaviy oferta havolasi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega bu testlar bor.</b> Bu vakansiya tizimning YAGONA
/// «tushadigan joyi»: ochiq ish o'rni bo'lmaganda nomzodning anketasi
/// aynan shu yerga yoziladi (<c>Candidate.VacancyId</c> majburiy).
/// Uning uchta xossasi jimgina buzilishi mumkin va uchalasi ham faqat
/// prod'da ko'rinardi:
/// </para>
/// <list type="number">
///   <item>
///   urug'lantirish TAKRORLANSA, har qayta ishga tushishda yangi
///   vakansiya paydo bo'lardi va nomzodlar o'nlab bir xil nomli
///   ro'yxatga bo'linib ketardi;
///   </item>
///   <item>
///   identifikator sozlamasi ekranga chiqib qolsa, uni o'zgartirgan
///   odam botni ishlamaydigan qilib qo'yardi — va sababini hech kim
///   ko'rmasdi;
///   </item>
///   <item>
///   bot ro'yxatida ko'rinib qolsa, nomzod uni ODDIY ish o'rni deb
///   tanlardi.
///   </item>
/// </list>
/// <para>
/// Testlar o'z bazasida ishlaydi (<c>IClassFixture</c>) va ular ichida
/// urug'lantirish QAYTA chaqiriladi — shu sababli bu sinf boshqa
/// sinflar bilan holat baham ko'rmasligi SHART.
/// </para>
/// </remarks>
public sealed class GeneralPoolVacancyTests : IClassFixture<BonviApiFactory>
{
    private static readonly Uri LoginUri = new("/api/auth/login", UriKind.Relative);
    private static readonly Uri SettingsUri = new("/api/settings", UriKind.Relative);
    private static readonly Uri BotSettingsUri = new("/api/settings/bot", UriKind.Relative);
    private static readonly Uri VacanciesUri = new("/api/recruiting/vacancies", UriKind.Relative);

    private readonly BonviApiFactory _factory;

    public GeneralPoolVacancyTests(BonviApiFactory factory) => _factory = factory;

    /// <summary>
    /// Ikkinchi ishga tushish HECH NARSA yaratmaydi va HECH NARSANI
    /// o'zgartirmaydi.
    /// </summary>
    /// <remarks>
    /// Deploy'da ilova kuniga bir necha marta qayta ishga tushadi.
    /// Urug'lantirish idempotent bo'lmasa, bir oydan keyin bazada
    /// o'nlab «Umumiy anketa» vakansiyasi yotardi, sozlama esa
    /// oxirgisini ko'rsatib turardi — ya'ni undan oldingi
    /// anketalarning hammasi HR ko'rmaydigan vakansiyalarda qolib
    /// ketardi.
    /// </remarks>
    [Fact]
    public async Task Urugh_ikkinchi_marta_ishga_tushsa_yangi_vakansiya_yaratmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        // Fabrika ko'tarilganda urug'lantirish ALLAQACHON bir marta o'tgan.
        using var mijoz = await AdminMijozAsync();

        var (avvalgiVakansiyaId, avvalgiNomId) = await UmumiyAnketaAsync();

        // Ikkinchi ishga tushish — xuddi konteyner qayta ko'tarilgandek.
        await UrughniQaytaIshgaTushirAsync(bekorQilish);

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        // Nom ham, vakansiya ham BITTA bo'lib qolishi kerak. O'chirilganlar
        // ham sanaladi: takror yaratilgan yozuv keyin o'chirilgan bo'lsa
        // ham, u xato bo'lganicha qoladi.
        var nomlar = await kontekst.VacancyTitles
            .IgnoreQueryFilters()
            .Where(nom => nom.Name == DatabaseInitializer.GeneralPoolTitle)
            .ToListAsync(bekorQilish);

        var nom = nomlar.ShouldHaveSingleItem();
        nom.Id.ShouldBe(avvalgiNomId, "mavjud ma'lumotnoma yozuvi qayta ishlatiladi");

        var vakansiyalar = await kontekst.Vacancies
            .IgnoreQueryFilters()
            .Where(vakansiya => vakansiya.VacancyTitleId == nom.Id)
            .ToListAsync(bekorQilish);

        var vakansiya = vakansiyalar.ShouldHaveSingleItem();
        vakansiya.Id.ShouldBe(avvalgiVakansiyaId, "sozlama ko'rsatgan vakansiya joyida qoladi");
        vakansiya.Status.ShouldBe(VacancyStatus.Open, "yopiq vakansiya nomzod qabul qilmasdi");

        // Sozlama ham TEGILMAGAN: qiymat o'sha-o'sha.
        var sozlama = await kontekst.SystemSettings
            .AsNoTracking()
            .SingleAsync(qator => qator.Key == SystemSettingKeys.GeneralPoolVacancyId, bekorQilish);

        sozlama.Value.ShouldBe(avvalgiVakansiyaId.ToString());
    }

    /// <summary>
    /// Sozlama <c>GET /api/settings</c> javobida ham, yozishda ham YO'Q.
    /// </summary>
    /// <remarks>
    /// Bu HR ning tanlovi emas, tizimning ichki bog'lanishi
    /// (<see cref="SystemSettingKind.Hidden"/>). Ekranga chiqsa, uni
    /// o'zgartirgan odam nomzodlarni mavjud bo'lmagan vakansiyaga
    /// yo'naltirib qo'yardi.
    /// </remarks>
    [Fact]
    public async Task Umumiy_anketa_sozlamasi_ekranda_ham_yozuvda_ham_yopiq()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        using var royxatJavobi = await mijoz.GetAsync(SettingsUri, bekorQilish);
        royxatJavobi.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(royxatJavobi));

        var royxat = await royxatJavobi.Content.ReadFromJsonAsync<List<SettingResponse>>(bekorQilish);
        royxat.ShouldNotBeNull();

        royxat.ShouldNotContain(
            sozlama => sozlama.Key == SystemSettingKeys.GeneralPoolVacancyId,
            "yashirin sozlama ekranga chiqmaydi");

        // Oferta havolasi esa — oddiy, KO'RINADIGAN sozlama.
        royxat.ShouldContain(sozlama => sozlama.Key == SystemSettingKeys.BotOfferUrl);

        // Qo'lda yozishga urinish rad etiladi — hatto super administratorga ham.
        using var yozish = await mijoz.PutAsJsonAsync(
            SettingsUri,
            new
            {
                items = new[]
                {
                    new { key = SystemSettingKeys.GeneralPoolVacancyId, value = Guid.NewGuid().ToString() },
                },
            },
            bekorQilish);

        yozish.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(yozish));
        (await MatnAsync(yozish)).ShouldContain("sozlama.qolda_ozgarmaydi");
    }

    /// <summary>
    /// Ommaviy oferta havolasi — bo'sh bo'lishi mumkin, lekin HAVOLA
    /// bo'lishi shart.
    /// </summary>
    /// <remarks>
    /// Naqsh bo'lmasa, HR «oferta.pdf» deb yozib qo'yardi va bot
    /// tugmasini Telegram butunlay rad etardi — anketaning oxirgi
    /// qadami ishlamay qolardi (<c>SystemSettingKeys.UrlPattern</c>).
    /// </remarks>
    [Fact]
    public async Task Oferta_havolasi_naqsh_boyicha_tekshiriladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        try
        {
            using var notogri = await OfertaniYozAsync(mijoz, "oferta.pdf");
            notogri.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(notogri));
            (await MatnAsync(notogri)).ShouldContain("sozlama.notogri_format");

            using var togri = await OfertaniYozAsync(mijoz, "https://bonvi.uz/oferta");
            togri.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(togri));
        }
        finally
        {
            // Bo'sh qiymat — «oferta qadami chizilmasin» degani; boshqa
            // testlar qoldiq havolani ko'rmasligi kerak.
            using var _ = await OfertaniYozAsync(mijoz, string.Empty);
        }
    }

    /// <summary>
    /// Oferta havolasini BOT o'z endpoint'idan oladi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bot <c>GET /api/settings</c> ga UMUMAN kira olmaydi: unda
    /// <c>settings.manage</c> yo'q va bo'lishi ham kerak emas (sabab
    /// <c>SettingsEndpoints</c> da). Havola <c>/api/settings/bot</c>
    /// javobiga qo'shilmasa, sozlama BOR bo'lardi-yu, uni o'qiy
    /// oladigan hech kim bo'lmasdi.
    /// </para>
    /// <para>
    /// Bo'sh qiymat ham TEKSHIRILADI: bot uchun bo'shlik ma'noli
    /// qiymat — oferta qadami umuman chizilmaydi.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Bot_oferta_havolasini_oz_endpointidan_oladi()
    {
        using var admin = await AdminMijozAsync();
        using var bot = await BotMijozAsync();

        try
        {
            using var yozildi = await OfertaniYozAsync(admin, "https://bonvi.uz/ommaviy-oferta");
            yozildi.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yozildi));

            var sozlamalar = await BotSozlamalariAsync(bot);
            sozlamalar.OfferUrl.ShouldBe("https://bonvi.uz/ommaviy-oferta");

            // Qolgan maydonlar joyida — yangi maydon javobni buzmagan.
            sozlamalar.ConversationTimeoutMinutes.ShouldBeGreaterThan(0);
        }
        finally
        {
            using var _ = await OfertaniYozAsync(admin, string.Empty);
        }

        var tozalangan = await BotSozlamalariAsync(bot);
        tozalangan.OfferUrl.ShouldBe(string.Empty, "bo'sh havola — «oferta qadami chizilmasin»");
    }

    /// <summary>
    /// Bot ochiq vakansiyalar ro'yxatida «Umumiy anketa» YO'Q, HR da esa BOR.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bot nomzodga vakansiyalarni TANLOV bo'lib ko'rsatadi, «Umumiy
    /// anketa» esa tanlanadigan ish o'rni emas — u tushadigan joy.
    /// </para>
    /// <para>
    /// Ikkinchi yarmi ham xuddi shunday muhim: HR uchun bu oddiy
    /// vakansiya. Filtr sukut bo'yicha yoqib qo'yilsa, HR nomzodni
    /// unga qo'lda kirita olmay qolardi va sababini ko'rmasdi.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Bot_royxatida_umumiy_anketa_korinmaydi_HR_da_esa_korinadi()
    {
        using var admin = await AdminMijozAsync();
        using var bot = await BotMijozAsync();

        var (umumiyId, _) = await UmumiyAnketaAsync();

        // Oddiy e'lon — bot ro'yxati bo'shab qolmaganini ko'rsatadi.
        var oddiyId = await OddiyVakansiyaAsync(admin);

        var botRoyxati = await VakansiyalarAsync(bot, "?status=Open&excludeGeneralPool=true&pageSize=200");

        botRoyxati.Items.ShouldContain(qator => qator.Id == oddiyId, "oddiy e'lon botga ko'rinadi");
        botRoyxati.Items.ShouldNotContain(
            qator => qator.Id == umumiyId,
            "«Umumiy anketa» tanlanadigan ish o'rni emas");

        // Sanoq ham filtrdan o'tgan bo'lishi kerak: aks holda oxirgi
        // sahifada «bor, lekin ko'rinmaydi» degan qator paydo bo'lardi.
        var botsiz = await VakansiyalarAsync(admin, "?status=Open&pageSize=200");

        botsiz.Items.ShouldContain(qator => qator.Id == umumiyId, "HR uchun bu oddiy vakansiya");
        botRoyxati.Total.ShouldBe(botsiz.Total - 1);
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    /// <summary>Sozlamada turgan «Umumiy anketa» vakansiyasi va uning nomi.</summary>
    private async Task<(Guid VacancyId, Guid TitleId)> UmumiyAnketaAsync()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var sozlama = await kontekst.SystemSettings
            .AsNoTracking()
            .SingleAsync(qator => qator.Key == SystemSettingKeys.GeneralPoolVacancyId, bekorQilish);

        Guid.TryParse(sozlama.Value, out var vakansiyaId)
            .ShouldBeTrue("urug'lantirish sozlamaga vakansiya identifikatorini yozadi");

        var vakansiya = await kontekst.Vacancies
            .AsNoTracking()
            .SingleAsync(qator => qator.Id == vakansiyaId, bekorQilish);

        return (vakansiya.Id, vakansiya.VacancyTitleId);
    }

    /// <summary>
    /// Urug'lantirishni QAYTA chaqiradi — ilova qayta ishga tushgandek.
    /// </summary>
    /// <remarks>
    /// Xizmat xostdan olinadi, yangisi yasalmaydi: idempotentlik aynan
    /// PROD'dagi obyektda tekshirilishi kerak.
    /// </remarks>
    private async Task UrughniQaytaIshgaTushirAsync(CancellationToken bekorQilish)
    {
        var urugh = _factory.Services
            .GetServices<IHostedService>()
            .OfType<DatabaseInitializer>()
            .Single();

        await urugh.StartAsync(bekorQilish);
    }

    private static async Task<BotSettingsResponse> BotSozlamalariAsync(HttpClient mijoz)
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var javob = await mijoz.GetAsync(BotSettingsUri, bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var sozlamalar = await javob.Content.ReadFromJsonAsync<BotSettingsResponse>(bekorQilish);
        sozlamalar.ShouldNotBeNull();

        return sozlamalar;
    }

    private static async Task<HttpResponseMessage> OfertaniYozAsync(HttpClient mijoz, string qiymat)
        => await mijoz.PutAsJsonAsync(
            SettingsUri,
            new { items = new[] { new { key = SystemSettingKeys.BotOfferUrl, value = qiymat } } },
            TestContext.Current.CancellationToken);

    private static async Task<Guid> OddiyVakansiyaAsync(HttpClient mijoz)
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var nomJavobi = await mijoz.PostAsJsonAsync(
            new Uri("/api/catalog/vacancy-titles", UriKind.Relative),
            new { name = "Омборчи " + Guid.NewGuid().ToString("N")[..6] },
            bekorQilish);

        nomJavobi.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(nomJavobi));

        var nom = await nomJavobi.Content.ReadFromJsonAsync<VacancyTitleResponse>(bekorQilish);
        nom.ShouldNotBeNull();

        using var javob = await mijoz.PostAsJsonAsync(
            VacanciesUri,
            new { vacancyTitleId = nom.Id },
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var vakansiya = await javob.Content.ReadFromJsonAsync<VacancyResponse>(bekorQilish);
        vakansiya.ShouldNotBeNull();

        return vakansiya.Id;
    }

    private static async Task<VacancyPageResponse> VakansiyalarAsync(HttpClient mijoz, string sorov)
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var javob = await mijoz.GetAsync(
            new Uri("/api/recruiting/vacancies" + sorov, UriKind.Relative),
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var sahifa = await javob.Content.ReadFromJsonAsync<VacancyPageResponse>(bekorQilish);
        sahifa.ShouldNotBeNull();

        return sahifa;
    }

    private async Task<HttpClient> AdminMijozAsync()
        => await KirishAsync(BonviApiFactory.TestAdminLogin, BonviApiFactory.TestAdminPassword);

    private async Task<HttpClient> BotMijozAsync()
        => await KirishAsync(BonviApiFactory.TestBotLogin, BonviApiFactory.TestBotPassword);

    private async Task<HttpClient> KirishAsync(string login, string parol)
    {
        var mijoz = _factory.CreateClient();

        using var javob = await mijoz.PostAsJsonAsync(
            LoginUri,
            new { login, password = parol },
            TestContext.Current.CancellationToken);

        javob.EnsureSuccessStatusCode();

        var auth = await javob.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken);
        mijoz.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return mijoz;
    }

    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
