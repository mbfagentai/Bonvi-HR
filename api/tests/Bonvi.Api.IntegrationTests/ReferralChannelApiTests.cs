using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>«Qayerdan bilgan» ma'lumotnomasining yozuvi (test uchun).</summary>
public sealed record ReferralChannelResponse(Guid Id, string Name, bool IsActive, string? Note, bool AsksDetail);

/// <summary>Tanlash ro'yxatining qatori (test uchun).</summary>
public sealed record ReferralOptionResponse(Guid Id, string Name, bool IsActive, bool AsksDetail);

/// <summary>Nomzod kartasi / ro'yxat qatorining «qayerdan bilgan» qismi (test uchun).</summary>
public sealed record ReferralCandidateResponse(
    Guid Id,
    Guid? ReferralChannelId,
    string? ReferralChannelName,
    string? ReferralDetail);

/// <summary>Nomzodlar ro'yxati sahifasi (test uchun).</summary>
public sealed record ReferralCandidatePageResponse(IReadOnlyList<ReferralCandidateResponse> Items, int Total);

/// <summary>Hisobot qatori (test uchun).</summary>
public sealed record ReferralReportRowResponse(Guid? ChannelId, string? ChannelName, bool IsActive, int Candidates, int Hired);

/// <summary>«Nomzodlar qayerdan bilgan» hisoboti (test uchun).</summary>
public sealed record ReferralReportResponse(IReadOnlyList<ReferralReportRowResponse> Rows, int TotalCandidates, int TotalHired);

/// <summary>
/// «Ish haqida qayerdan bilgan» — ma'lumotnoma, nomzodning majburiy maydoni, filtr, eksport va
/// hisobot (SPEC-qayerdan-bilgan §8).
/// </summary>
/// <remarks>
/// Baza sinf uchun bitta: boshlang'ich sakkiz kanal FAOLSIZLANTIRILMAYDI (boshqa testlar ularga
/// tayanadi) — faolsizlantirish kerak bo'lgan joyda test O'Z kanalini yaratadi. Hisobot va filtr
/// testlari o'z vakansiyasi bilan ajratiladi.
/// </remarks>
public sealed class ReferralChannelApiTests : IClassFixture<BonviApiFactory>
{
    private const string CatalogPath = "/api/catalog/referral-channels";
    private const string OptionsPath = "/api/recruiting/referral-channels";
    private const string CandidatesPath = "/api/recruiting/candidates";
    private const string ReportPath = "/api/reports/candidate-referrals";
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly BonviApiFactory _factory;

    public ReferralChannelApiTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Belgi() => Guid.NewGuid().ToString("N")[..8];

    // ────────────────────────────────────────────────────────────
    //  Ma'lumotnoma (SPEC §3)
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Malumotnoma_yaratiladi_tahrirlanadi_faolsizlantiriladi_catalog_manage_bilan()
    {
        using var hr = await MijozAsync(Permissions.CatalogView, Permissions.CatalogManage);
        var nom = "Instagram " + Belgi();

        using var yaratish = await hr.PostAsJsonAsync(new Uri(CatalogPath, UriKind.Relative), new { name = nom, asksDetail = true }, Ct);
        yaratish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yaratish));
        var kanal = (await yaratish.Content.ReadFromJsonAsync<ReferralChannelResponse>(Ct))!;

        kanal.Name.ShouldBe(nom);
        kanal.IsActive.ShouldBeTrue();
        kanal.AsksDetail.ShouldBeTrue();

        using var tahrir = await hr.PutAsJsonAsync(
            new Uri($"{CatalogPath}/{kanal.Id}", UriKind.Relative),
            new { name = nom + " (reklama)", asksDetail = false, note = "HR qo'shdi" },
            Ct);
        tahrir.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(tahrir));
        var tahrirlangan = (await tahrir.Content.ReadFromJsonAsync<ReferralChannelResponse>(Ct))!;
        tahrirlangan.Name.ShouldBe(nom + " (reklama)");
        tahrirlangan.AsksDetail.ShouldBeFalse();
        tahrirlangan.Note.ShouldBe("HR qo'shdi");

        // Nom — registrsiz yagona: boshlang'ich «Telegram» bilan to'qnashadi
        using var takror = await hr.PostAsJsonAsync(new Uri(CatalogPath, UriKind.Relative), new { name = "telegram" }, Ct);
        takror.StatusCode.ShouldBe(HttpStatusCode.Conflict, await MatnAsync(takror));
        (await MatnAsync(takror)).ShouldContain("malumotnoma.nom_band");

        using (var ochirish = await hr.PostAsync(new Uri($"{CatalogPath}/{kanal.Id}/deactivate", UriKind.Relative), null, Ct))
        {
            ochirish.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(ochirish));
        }

        var royxat = (await hr.GetFromJsonAsync<List<ReferralChannelResponse>>(new Uri(CatalogPath, UriKind.Relative), Ct))!;
        royxat.Single(row => row.Id == kanal.Id).IsActive.ShouldBeFalse("faolsizlantirilgan kanal ro'yxatda qoladi");
        ReferralSeeds.All.ShouldAllBe(seed => royxat.Any(row => row.Id == seed.Id), "sakkizta boshlang'ich kanal");
        royxat.Single(row => row.Id == ReferralSeeds.Acquaintance).AsksDetail.ShouldBeTrue();

        var faollari = await hr.GetFromJsonAsync<List<ReferralChannelResponse>>(new Uri(CatalogPath + "?onlyActive=true", UriKind.Relative), Ct);
        faollari!.ShouldNotContain(row => row.Id == kanal.Id);

        using (var yoqish = await hr.PostAsync(new Uri($"{CatalogPath}/{kanal.Id}/activate", UriKind.Relative), null, Ct))
        {
            yoqish.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(yoqish));
        }
    }

    [Fact]
    public async Task Malumotnoma_ruxsatsiz_ochilmaydi()
    {
        using var faqatKorish = await MijozAsync(Permissions.CatalogView);
        using var yozish = await faqatKorish.PostAsJsonAsync(new Uri(CatalogPath, UriKind.Relative), new { name = "Radio " + Belgi() }, Ct);
        yozish.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var rekruter = await MijozAsync(Permissions.RecruitingView, Permissions.RecruitingManage);
        using var oqish = await rekruter.GetAsync(new Uri(CatalogPath, UriKind.Relative), Ct);
        oqish.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "ma'lumotnoma — catalog.view");

        using var mehmon = _factory.CreateClient();
        using var tokensiz = await mehmon.GetAsync(new Uri(CatalogPath, UriKind.Relative), Ct);
        tokensiz.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ────────────────────────────────────────────────────────────
    //  Tanlash ro'yxati — rekruting ruxsati bilan (bot, HR formasi)
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Tanlash_royxati_bot_huquqlari_bilan_ochiladi_faqat_faollari_alifbo_tartibida()
    {
        var faolEmas = await KanalYaratAsync("Gazeta " + Belgi(), asksDetail: false, faol: false);

        // Bot hisobining AYNAN o'sha ruxsatlari — catalog.view yo'q
        using var bot = await MijozAsync([.. SystemRoles.Defaults[SystemRoles.RecruitingBot].Permissions]);

        var faollari = (await bot.GetFromJsonAsync<List<ReferralOptionResponse>>(new Uri(OptionsPath, UriKind.Relative), Ct))!;

        faollari.ShouldNotContain(row => row.Id == faolEmas);
        faollari.ShouldAllBe(row => row.IsActive);
        faollari.Where(row => ReferralSeeds.All.Any(seed => seed.Id == row.Id)).Select(row => row.Name).ShouldBe(
            ["Boshqa", "E’lon", "hh.uz", "Instagram / Facebook", "Kollej / OTM", "OLX.uz", "Tanishlar orqali", "Telegram"],
            "alifbo tartibida, bazaning o'zbek kolatsiyasi bo'yicha");
        faollari.Single(row => row.Id == ReferralSeeds.Other).AsksDetail.ShouldBeTrue();
        faollari.Single(row => row.Id == ReferralSeeds.Telegram).AsksDetail.ShouldBeFalse();

        var hammasi = await bot.GetFromJsonAsync<List<ReferralOptionResponse>>(new Uri(OptionsPath + "?includeInactive=true", UriKind.Relative), Ct);
        hammasi!.Single(row => row.Id == faolEmas).IsActive.ShouldBeFalse("filtr uchun — eski nomzodlar o'sha kanal bilan");

        // JSON'da HR izohi YO'Q — botga ichki matn bermaymiz
        using var xom = await bot.GetAsync(new Uri(OptionsPath, UriKind.Relative), Ct);
        (await MatnAsync(xom)).ShouldNotContain("\"note\"");

        using var begona = await MijozAsync(Permissions.CatalogView, Permissions.EmployeesView);
        using var rad = await begona.GetAsync(new Uri(OptionsPath, UriKind.Relative), Ct);
        rad.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "ro'yxat — recruiting.view");
    }

    // ────────────────────────────────────────────────────────────
    //  Yangi nomzod — kanal majburiy (SPEC §5)
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Yangi_nomzodda_kanal_majburiy_faol_va_aniqlik_qoidasi_bilan()
    {
        using var hr = await MijozAsync(Permissions.RecruitingView, Permissions.RecruitingManage);
        var vakansiya = await VakansiyaAsync();
        var faolEmas = await KanalYaratAsync("Radio " + Belgi(), asksDetail: false, faol: false);

        (await XatoAsync(hr, new { vacancyId = vakansiya, lastName = "Кадиров", firstName = "Азиз" }))
            .ShouldBe("qayerdan.majburiy");
        (await XatoAsync(hr, new { vacancyId = vakansiya, lastName = "Кадиров", firstName = "Азиз", referralChannelId = faolEmas }))
            .ShouldBe("qayerdan.topilmadi");
        (await XatoAsync(hr, new { vacancyId = vakansiya, lastName = "Кадиров", firstName = "Азиз", referralChannelId = Guid.CreateVersion7() }))
            .ShouldBe("qayerdan.topilmadi");
        (await XatoAsync(hr, new { vacancyId = vakansiya, lastName = "Кадиров", firstName = "Азиз", referralChannelId = ReferralSeeds.Acquaintance, referralDetail = " " }))
            .ShouldBe("qayerdan.izoh_majburiy");
        (await XatoAsync(hr, new { vacancyId = vakansiya, lastName = "Кадиров", firstName = "Азиз", referralChannelId = ReferralSeeds.Acquaintance, referralDetail = new string('a', 201) }))
            .ShouldBe("qayerdan.izoh_uzun");

        (await JamiAsync(hr, $"vacancyId={vakansiya}")).ShouldBe(0, "rad etilgan so'rovlar nomzod yozmaydi");

        var nomzod = await NomzodAsync(hr, vakansiya, "Кадиров", ReferralSeeds.Acquaintance, "  Рустамов Баҳодир ");

        nomzod.ReferralChannelId.ShouldBe(ReferralSeeds.Acquaintance);
        nomzod.ReferralChannelName.ShouldBe("Tanishlar orqali");
        nomzod.ReferralDetail.ShouldBe("Рустамов Баҳодир");

        var qator = (await SahifaAsync(hr, $"vacancyId={vakansiya}")).Items.ShouldHaveSingleItem();
        qator.ReferralChannelName.ShouldBe("Tanishlar orqali", "ro'yxat qatorida ham");
        qator.ReferralDetail.ShouldBe("Рустамов Баҳодир");

        // Aniqlik so'ramaydigan kanalda kelgan matn saqlanmaydi
        var telegram = await NomzodAsync(hr, vakansiya, "Телеграмов", ReferralSeeds.Telegram, "eski matn");
        telegram.ReferralDetail.ShouldBeNull();
    }

    [Fact]
    public async Task Bot_hisobi_kanal_bilan_ariza_beradi()
    {
        using var bot = await MijozAsync([.. SystemRoles.Defaults[SystemRoles.RecruitingBot].Permissions]);

        using var javob = await bot.PostAsJsonAsync(
            new Uri(CandidatesPath, UriKind.Relative),
            new
            {
                vacancyId = await VakansiyaAsync(),
                lastName = "Ботов",
                firstName = "Азиз",
                source = "TelegramBot",
                telegramChatId = 990001L,
                telegramLanguage = "uz-Cyrl",
                referralChannelId = ReferralSeeds.Other,
                referralDetail = "Bekatdagi e'lon",
                questionnaire = new
                {
                    education = "SecondarySchool",
                    maritalStatus = "Married",
                    russianLevel = "Elementary",
                    expectedSalary = "From1To2Million",
                },
            },
            Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        var nomzod = (await javob.Content.ReadFromJsonAsync<ReferralCandidateResponse>(Ct))!;

        nomzod.ReferralChannelId.ShouldBe(ReferralSeeds.Other);
        nomzod.ReferralChannelName.ShouldBe("Boshqa");
        nomzod.ReferralDetail.ShouldBe("Bekatdagi e'lon");
    }

    // ────────────────────────────────────────────────────────────
    //  HR tahriri (SPEC §5, «HR edit»)
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Tahrir_kanalsiz_eskisini_saqlaydi_borini_tozalamaydi_faolsizini_saqlaydi()
    {
        using var hr = await MijozAsync(Permissions.RecruitingView, Permissions.RecruitingManage);
        var vakansiya = await VakansiyaAsync();
        var eski = await EskiNomzodAsync(vakansiya, "Эскиев");

        // Kanalsiz ESKI nomzod — HR boshqa maydonni tuzatadi, kanal so'ralmaydi
        var tuzatildi = await TahrirAsync(hr, eski, new { lastName = "Эскиев", firstName = "Азиз", note = "Qayta qo'ng'iroq" });
        tuzatildi.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(tuzatildi));
        (await KartaAsync(hr, eski)).ReferralChannelId.ShouldBeNull();

        var vaqtinchalik = await KanalYaratAsync("Ko'rgazma " + Belgi(), asksDetail: false, faol: true);

        var qoyildi = await TahrirAsync(hr, eski, new { lastName = "Эскиев", firstName = "Азиз", referralChannelId = vaqtinchalik });
        qoyildi.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qoyildi));

        // Kanali bor — tozalab bo'lmaydi
        var tozalash = await TahrirAsync(hr, eski, new { lastName = "Эскиев", firstName = "Азиз" });
        tozalash.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(tozalash));
        (await MatnAsync(tozalash)).ShouldContain("qayerdan.majburiy");

        // Kanal keyin faolsizlantirildi — o'zgarmagan holda saqlanadi
        await HolatAsync(vaqtinchalik, faol: false);

        var ozgarmagan = await TahrirAsync(hr, eski, new { lastName = "Эскиев", firstName = "Азиз", referralChannelId = vaqtinchalik, speciality = "Payvandchi" });
        ozgarmagan.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(ozgarmagan));
        (await KartaAsync(hr, eski)).ReferralChannelId.ShouldBe(vaqtinchalik);

        // Boshqa faolsizlantirilgan kanalni YANGI tanlov sifatida qo'yib bo'lmaydi
        var boshqaEski = await KanalYaratAsync("Varaqa " + Belgi(), asksDetail: false, faol: false);
        var yangi = await TahrirAsync(hr, eski, new { lastName = "Эскиев", firstName = "Азиз", referralChannelId = boshqaEski });
        yangi.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(yangi));
        (await MatnAsync(yangi)).ShouldContain("qayerdan.topilmadi");

        // Faol kanalga almashtirish — HR xatoni tuzatadi
        var almashtirish = await TahrirAsync(hr, eski, new { lastName = "Эскиев", firstName = "Азиз", referralChannelId = ReferralSeeds.Olx });
        almashtirish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(almashtirish));
        (await KartaAsync(hr, eski)).ReferralChannelName.ShouldBe("OLX.uz");
    }

    // ────────────────────────────────────────────────────────────
    //  Ro'yxat filtri va Excel (SPEC §6)
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Royxat_kanal_va_korsatilmagan_boyicha_filtrlanadi_eksportda_ustun_bor()
    {
        using var hr = await MijozAsync(Permissions.RecruitingView, Permissions.RecruitingManage, Permissions.ReportsExport);
        var vakansiya = await VakansiyaAsync();

        var telegram = await NomzodAsync(hr, vakansiya, "Филтров", ReferralSeeds.Telegram);
        var tanish = await NomzodAsync(hr, vakansiya, "Танишов", ReferralSeeds.Acquaintance, "Каримов Анвар");
        var eski = await EskiNomzodAsync(vakansiya, "Эскиев");

        (await SahifaAsync(hr, $"vacancyId={vakansiya}&referralChannelId={ReferralSeeds.Telegram}")).Items.Select(row => row.Id)
            .ShouldBe([telegram.Id]);
        (await SahifaAsync(hr, $"vacancyId={vakansiya}&referralMissing=true")).Items.Select(row => row.Id)
            .ShouldBe([eski], "«Ko'rsatilmagan» — kanalsiz eski nomzodlar");
        (await JamiAsync(hr, $"vacancyId={vakansiya}&referralChannelId={ReferralSeeds.Telegram}&referralMissing=true")).ShouldBe(0);
        (await JamiAsync(hr, $"vacancyId={vakansiya}")).ShouldBe(3);

        using var javob = await hr.GetAsync(new Uri($"{CandidatesPath}/export?vacancyId={vakansiya}&referralChannelId={ReferralSeeds.Acquaintance}", UriKind.Relative), Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        using var kitob = new XLWorkbook(new MemoryStream(await javob.Content.ReadAsByteArrayAsync(Ct)));
        var varaq = kitob.Worksheet(1);
        var ustun = Enumerable.Range(1, varaq.LastColumnUsed()!.ColumnNumber())
            .Single(index => varaq.Cell(1, index).GetString() == "Qayerdan bilgan");

        varaq.LastRowUsed()!.RowNumber().ShouldBe(2, "filtr eksportda ham — bitta qator");
        varaq.Cell(2, ustun).GetString().ShouldBe("Tanishlar orqali — Каримов Анвар");
        tanish.ReferralDetail.ShouldBe("Каримов Анвар");
    }

    // ────────────────────────────────────────────────────────────
    //  Hisobot (SPEC §7)
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Hisobot_davr_vakansiya_korsatilmagan_qatori_va_ishga_olinganlar_bilan()
    {
        var urug = await HisobotUrugiAsync();
        using var korish = await MijozAsync(Permissions.ReportsView, Permissions.RecruitingView);

        var hisobot = await korish.GetFromJsonAsync<ReferralReportResponse>(
            new Uri($"{ReportPath}?from=2026-03-01&to=2026-03-31&vacancyId={urug.Vakansiya}", UriKind.Relative), Ct);

        hisobot.ShouldNotBeNull();
        // Nomzodlar soni bo'yicha kamayish; teng sonlilar — nom bo'yicha, «Ko'rsatilmagan» oxirida
        hisobot.Rows.Select(row => (row.ChannelId, row.ChannelName, row.IsActive, row.Candidates, row.Hired)).ShouldBe(
        [
            (ReferralSeeds.Telegram, "Telegram", true, 3, 1),
            (urug.FaolEmas, urug.FaolEmasNomi, false, 1, 0),
            (ReferralSeeds.Acquaintance, "Tanishlar orqali", true, 1, 1),
            (null, null, false, 1, 0),
        ]);
        hisobot.TotalCandidates.ShouldBe(6, "davrdan tashqari, boshqa vakansiya va Excel arxivi kirmaydi");
        hisobot.TotalHired.ShouldBe(2);

        // Davrsiz — fevraldagi nomzod ham qo'shiladi
        var hammasi = await korish.GetFromJsonAsync<ReferralReportResponse>(new Uri($"{ReportPath}?vacancyId={urug.Vakansiya}", UriKind.Relative), Ct);
        hammasi!.Rows.Single(row => row.ChannelId == ReferralSeeds.Telegram).Candidates.ShouldBe(4);

        using var teskari = await korish.GetAsync(new Uri($"{ReportPath}?from=2026-03-02&to=2026-03-01", UriKind.Relative), Ct);
        teskari.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await MatnAsync(teskari)).ShouldContain("royxat.sana_oraligi_notogri");
    }

    [Theory]
    [InlineData(Permissions.ReportsView)]
    [InlineData(Permissions.RecruitingView)]
    public async Task Hisobot_ikkala_ruxsatsiz_ochilmaydi(string ruxsat)
    {
        using var mijoz = await MijozAsync(ruxsat);

        using var javob = await mijoz.GetAsync(new Uri(ReportPath, UriKind.Relative), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "hisobot — reports.view VA recruiting.view");
    }

    [Fact]
    public async Task Hisobot_eksporti_reports_export_bilan_jami_qatori_bilan()
    {
        var urug = await HisobotUrugiAsync();
        var sorov = $"/export?from=2026-03-01&to=2026-03-31&vacancyId={urug.Vakansiya}";

        using (var eksportsiz = await MijozAsync(Permissions.ReportsView, Permissions.RecruitingView))
        using (var rad = await eksportsiz.GetAsync(new Uri(ReportPath + sorov, UriKind.Relative), Ct))
        {
            rad.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "Excel — ustiga reports.export");
        }

        using (var rekrutingsiz = await MijozAsync(Permissions.ReportsView, Permissions.ReportsExport))
        using (var rad = await rekrutingsiz.GetAsync(new Uri(ReportPath + sorov, UriKind.Relative), Ct))
        {
            rad.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "nomzod sonlari — recruiting.view siz ochilmaydi");
        }

        using var mijoz = await MijozAsync(Permissions.ReportsView, Permissions.RecruitingView, Permissions.ReportsExport);
        using var javob = await mijoz.GetAsync(new Uri(ReportPath + sorov, UriKind.Relative), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        javob.Content.Headers.ContentType?.MediaType.ShouldBe(ExcelContentType);
        (javob.Content.Headers.ContentDisposition?.FileNameStar ?? javob.Content.Headers.ContentDisposition?.FileName)
            .ShouldNotBeNull().ShouldContain("Bonvi_nomzodlar_qayerdan_bilgan_");

        using var kitob = new XLWorkbook(new MemoryStream(await javob.Content.ReadAsByteArrayAsync(Ct)));
        var varaq = kitob.Worksheet(1);

        Enumerable.Range(1, 4).Select(ustun => varaq.Cell(1, ustun).GetString())
            .ShouldBe(["Qayerdan bilgan", "Nomzodlar", "Ishga olingan", "Ishga olingan, %"]);

        varaq.Cell(2, 1).GetString().ShouldBe("Telegram");
        varaq.Cell(2, 2).GetValue<int>().ShouldBe(3);
        varaq.Cell(2, 3).GetValue<int>().ShouldBe(1);
        varaq.Cell(2, 4).GetValue<decimal>().ShouldBe(33.3m);
        varaq.Cell(3, 1).GetString().ShouldBe($"{urug.FaolEmasNomi} (faol emas)");
        varaq.Cell(4, 1).GetString().ShouldBe("Tanishlar orqali");
        varaq.Cell(4, 4).GetValue<decimal>().ShouldBe(100m);
        varaq.Cell(5, 1).GetString().ShouldBe("Ko'rsatilmagan");
        varaq.Cell(6, 1).GetString().ShouldBe("Jami");
        varaq.Cell(6, 2).GetValue<int>().ShouldBe(6);
        varaq.Cell(6, 3).GetValue<int>().ShouldBe(2);
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    /// <summary>Hisobot uchun o'z vakansiyasi: davr — 2026-yil mart, Toshkent kuni bo'yicha.</summary>
    private async Task<(Guid Vakansiya, Guid FaolEmas, string FaolEmasNomi)> HisobotUrugiAsync()
    {
        var faolEmasNomi = "Gazeta " + Belgi();
        var faolEmas = await KanalYaratAsync(faolEmasNomi, asksDetail: false, faol: true);

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var vakansiya = Vacancy.Open("Hisobot " + Belgi(), new DateOnly(2026, 1, 1), openings: 5).Value;
        var boshqa = Vacancy.Open("Boshqa " + Belgi(), new DateOnly(2026, 1, 1), openings: 5).Value;
        kontekst.AddRange(vakansiya, boshqa);

        var qatorlar = new List<(Candidate Nomzod, DateTimeOffset Yaratilgan)>
        {
            // Telegram: 3 tasi martda (biri — Toshkentda 1-mart 00:30), 1 tasi ishga olingan
            (Nomzod(vakansiya.Id, ReferralSeeds.Telegram, hired: true), new DateTimeOffset(2026, 3, 10, 5, 0, 0, TimeSpan.Zero)),
            (Nomzod(vakansiya.Id, ReferralSeeds.Telegram), new DateTimeOffset(2026, 3, 11, 5, 0, 0, TimeSpan.Zero)),
            (Nomzod(vakansiya.Id, ReferralSeeds.Telegram), new DateTimeOffset(2026, 2, 28, 19, 30, 0, TimeSpan.Zero)),

            // Davrdan tashqari: fevral va Toshkentda 1-aprel 00:30
            (Nomzod(vakansiya.Id, ReferralSeeds.Telegram), new DateTimeOffset(2026, 2, 10, 5, 0, 0, TimeSpan.Zero)),
            (Nomzod(vakansiya.Id, ReferralSeeds.Acquaintance), new DateTimeOffset(2026, 3, 31, 19, 30, 0, TimeSpan.Zero)),

            (Nomzod(vakansiya.Id, ReferralSeeds.Acquaintance, hired: true), new DateTimeOffset(2026, 3, 12, 5, 0, 0, TimeSpan.Zero)),
            (Nomzod(vakansiya.Id, faolEmas), new DateTimeOffset(2026, 3, 15, 5, 0, 0, TimeSpan.Zero)),

            // Kanalsiz eski nomzod — «Ko'rsatilmagan»
            (Nomzod(vakansiya.Id, null), new DateTimeOffset(2026, 3, 16, 5, 0, 0, TimeSpan.Zero)),

            // Excel arxivi — kirmaydi
            (Candidate.Register(FullName.Create("Архивов", "Азиз", null).Value, vakansiya.Id, CandidateSource.Excel).Value,
                new DateTimeOffset(2026, 3, 17, 5, 0, 0, TimeSpan.Zero)),

            // Boshqa vakansiya — vakansiya filtrida kirmaydi
            (Nomzod(boshqa.Id, ReferralSeeds.Telegram), new DateTimeOffset(2026, 3, 18, 5, 0, 0, TimeSpan.Zero)),
        };

        kontekst.Candidates.AddRange(qatorlar.Select(row => row.Nomzod));
        await kontekst.SaveChangesAsync(Ct);

        foreach (var (nomzod, yaratilgan) in qatorlar)
        {
            await kontekst.Candidates
                .Where(row => row.Id == nomzod.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.CreatedAt, yaratilgan), Ct);
        }

        // Kanal shundan keyin faolsizlantiriladi — hisobotda baribir qoladi
        await HolatAsync(faolEmas, faol: false);

        return (vakansiya.Id, faolEmas, faolEmasNomi);

        static Candidate Nomzod(Guid vakansiyaId, Guid? kanal, bool hired = false)
        {
            var nomzod = Candidate.Register(FullName.Create("Ҳисоботов", "Азиз", null).Value, vakansiyaId).Value;

            if (kanal is { } id)
            {
                nomzod.SetReferral(id, null).IsSuccess.ShouldBeTrue();
            }

            if (hired)
            {
                nomzod.MarkHired(new DateOnly(2026, 3, 20)).IsSuccess.ShouldBeTrue();
            }

            return nomzod;
        }
    }

    private async Task<Guid> KanalYaratAsync(string nom, bool asksDetail, bool faol)
    {
        using var hr = await MijozAsync(Permissions.CatalogView, Permissions.CatalogManage);

        using var javob = await hr.PostAsJsonAsync(new Uri(CatalogPath, UriKind.Relative), new { name = nom, asksDetail }, Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        var id = (await javob.Content.ReadFromJsonAsync<ReferralChannelResponse>(Ct))!.Id;

        if (!faol)
        {
            await HolatAsync(id, faol: false);
        }

        return id;
    }

    private async Task HolatAsync(Guid kanal, bool faol)
    {
        using var hr = await MijozAsync(Permissions.CatalogView, Permissions.CatalogManage);

        using var javob = await hr.PostAsync(
            new Uri($"{CatalogPath}/{kanal}/{(faol ? "activate" : "deactivate")}", UriKind.Relative), null, Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(javob));
    }

    private async Task<Guid> VakansiyaAsync()
    {
        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);

        using var javob = await admin.PostAsJsonAsync(
            new Uri("/api/recruiting/vacancies", UriKind.Relative),
            new { title = "Qayerdan bilgan " + Belgi(), openings = 3, openedAt = new DateOnly(2026, 9, 1) },
            Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        using var hujjat = JsonDocument.Parse(await MatnAsync(javob));
        return hujjat.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<ReferralCandidateResponse> NomzodAsync(
        HttpClient hr,
        Guid vakansiya,
        string familiya,
        Guid kanal,
        string? aniqlik = null)
    {
        using var javob = await hr.PostAsJsonAsync(
            new Uri(CandidatesPath, UriKind.Relative),
            new { vacancyId = vakansiya, lastName = familiya, firstName = "Азиз", referralChannelId = kanal, referralDetail = aniqlik },
            Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        return (await javob.Content.ReadFromJsonAsync<ReferralCandidateResponse>(Ct))!;
    }

    /// <summary>Kanalsiz ESKI nomzod — Jobster ko'chirmasidagidek, to'g'ridan-to'g'ri bazaga.</summary>
    private async Task<Guid> EskiNomzodAsync(Guid vakansiya, string familiya)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var nomzod = Candidate.Register(FullName.Create(familiya, "Азиз", null).Value, vakansiya).Value;
        kontekst.Candidates.Add(nomzod);
        await kontekst.SaveChangesAsync(Ct);

        return nomzod.Id;
    }

    private static async Task<string?> XatoAsync(HttpClient hr, object tana)
    {
        using var javob = await hr.PostAsJsonAsync(new Uri(CandidatesPath, UriKind.Relative), tana, Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(javob));

        using var hujjat = JsonDocument.Parse(await MatnAsync(javob));
        return hujjat.RootElement.GetProperty("title").GetString();
    }

    private static Task<HttpResponseMessage> TahrirAsync(HttpClient hr, Guid nomzod, object tana)
        => hr.PutAsJsonAsync(new Uri($"{CandidatesPath}/{nomzod}", UriKind.Relative), tana, Ct);

    private static async Task<ReferralCandidateResponse> KartaAsync(HttpClient hr, Guid nomzod)
        => (await hr.GetFromJsonAsync<ReferralCandidateResponse>(new Uri($"{CandidatesPath}/{nomzod}", UriKind.Relative), Ct))!;

    private static async Task<ReferralCandidatePageResponse> SahifaAsync(HttpClient hr, string sorov)
        => (await hr.GetFromJsonAsync<ReferralCandidatePageResponse>(new Uri($"{CandidatesPath}?{sorov}", UriKind.Relative), Ct))!;

    private static async Task<int> JamiAsync(HttpClient hr, string sorov) => (await SahifaAsync(hr, sorov)).Total;

    private async Task<HttpClient> MijozAsync(params string[] ruxsatlar)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await TestAccounts.TokenAsync(_factory, ruxsatlar));

        return client;
    }

    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(Ct);
}
