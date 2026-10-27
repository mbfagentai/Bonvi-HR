using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bonvi.Api.Common;
using Bonvi.Application.Recruiting.Forms;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

public sealed record KioskVacancyResponse(Guid Id, string Title);

public sealed record KioskSessionResponse(
    DateOnly Today,
    IReadOnlyList<KioskVacancyResponse> Vacancies,
    bool GeneralPoolAvailable);

public sealed record KioskSubmittedResponse(int FormNumber, DateTimeOffset SubmittedAt);

public sealed record CandidateFormResponse(
    Guid Id,
    int Number,
    string FormCode,
    string FormVersion,
    DateTimeOffset SubmittedAt,
    string Language,
    Guid? FileId);

/// <summary>
/// Ochiq onlayn anketa (SPEC-planshet-anketa §9, 2026-10-26 tahriri): login va kalitsiz seans va
/// yuborish, boshqa hech narsa ochilmagani, yuborish (yangi va mavjud nomzod), takroriy yuborish,
/// fayllar chegarasi, HR o'qishi.
/// </summary>
/// <remarks>
/// ⚠️ Yuborish bitta mijoz IP'siga soatiga 20 ta (<c>RateLimitPolicies.QuestionnaireSubmit</c>) — har
/// test o'z IP'si bilan keladi (<see cref="ClientIp.HeaderName"/>, <see cref="NoyobIp"/>), aks holda
/// sinfdagi testlar bitta bo'linmani birga yeb qo'yardi.
/// </remarks>
public sealed partial class KioskQuestionnaireApiTests : IClassFixture<BonviApiFactory>
{
    private const string SessionPath = "/api/kiosk/questionnaire/session";
    private const string SubmitPath = "/api/kiosk/questionnaire";

    private static int _telefonSanogi;
    private static int _ipSanogi;

    private readonly BonviApiFactory _factory;

    public KioskQuestionnaireApiTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private DateOnly Bugun => _factory.Services.GetRequiredService<IClock>().Today;

    // ────────────────────────────────────────────────────────────
    //  Ochiqlik: forma ochiq, qolgani — yo'q
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Seans_va_yuborish_hech_qanday_kalitsiz_va_tokensiz_ishlaydi()
    {
        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        var vakansiya = await VakansiyaAsync(admin);

        // Hech qanday sarlavha yo'q — na token, na kalit, na mijoz IP'si
        using var mehmon = _factory.CreateClient();

        using (var seansJavobi = await mehmon.GetAsync(new Uri(SessionPath, UriKind.Relative), Ct))
        {
            seansJavobi.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(seansJavobi));
            (await MatnAsync(seansJavobi)).ShouldNotContain("deviceName", Case.Insensitive, "qurilma nomi endi yo'q");

            var seans = await seansJavobi.Content.ReadFromJsonAsync<KioskSessionResponse>(Ct);
            seans!.Today.ShouldBe(Bugun);
            seans.Vacancies.ShouldContain(row => row.Id == vakansiya);
            seans.GeneralPoolAvailable.ShouldBeTrue();

            await using var qamrov = _factory.Services.CreateAsyncScope();
            var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
            var umumiy = await db.SystemSettings.AsNoTracking()
                .SingleAsync(row => row.Key == Domain.Settings.SystemSettingKeys.GeneralPoolVacancyId, Ct);

            seans.Vacancies.ShouldNotContain(row => row.Id.ToString() == umumiy.Value, "«Umumiy anketa» alohida tanlov");
        }

        using var javob = await YuborAsync(mehmon, QuestionnaireSamples.Minimal(Bugun, Telefon()) with { VacancyId = vakansiya }, Guid.NewGuid());
        javob.StatusCode.ShouldBe(HttpStatusCode.Created, await MatnAsync(javob));
    }

    [Fact]
    public async Task Boshqa_hech_narsa_ochilmagan_HR_yollari_tokensiz_401()
    {
        using var mehmon = Mehmon();
        var nomzod = Guid.NewGuid();

        string[] yollar =
        [
            "/api/recruiting/candidates",
            $"/api/recruiting/candidates/{nomzod}",
            $"/api/recruiting/candidates/{nomzod}/forms",
            $"/api/recruiting/candidates/{nomzod}/forms/{Guid.NewGuid()}/pdf",
            $"/api/recruiting/candidates/{nomzod}/files/{Guid.NewGuid()}",
            $"/api/recruiting/candidates/{nomzod}/photo",
            "/api/recruiting/candidates/export",
            "/api/recruiting/vacancies",
            "/api/employees",
            "/api/api-keys",
            "/api/audit",
            $"{PublicApiHarness.Prefix}/employees",
        ];

        foreach (var yol in yollar)
        {
            using var javob = await mehmon.GetAsync(new Uri(yol, UriKind.Relative), Ct);
            javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, yol);
        }
    }

    [Fact]
    public async Task Planshet_ulash_yollari_olib_tashlangan()
    {
        // HR tokeni bilan — avtorizatsiyadan o'tadi, demak javob marshrutning o'zi haqida
        using var hr = await HrAsync(Permissions.RecruitingManage);

        using (var royxat = await hr.GetAsync(new Uri("/api/recruiting/kiosk-devices", UriKind.Relative), Ct))
        {
            ((int)royxat.StatusCode).ShouldBeOneOf(404, 405);
        }

        using (var ulash = await hr.PostAsJsonAsync(new Uri("/api/recruiting/kiosk-devices", UriKind.Relative), new { name = "Planshet" }, Ct))
        {
            ((int)ulash.StatusCode).ShouldBeOneOf(404, 405);
        }

        using var uzish = await hr.PostAsync(new Uri($"/api/recruiting/kiosk-devices/{Guid.NewGuid()}/revoke", UriKind.Relative), null, Ct);
        ((int)uzish.StatusCode).ShouldBeOneOf(404, 405);
    }

    // ────────────────────────────────────────────────────────────
    //  Yuborish
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Yangi_nomzod_anketa_PDF_va_anketasi_bilan_yaratiladi()
    {
        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        var vakansiya = await VakansiyaAsync(admin);

        const string ip = "203.0.113.47";
        using var mehmon = Mehmon(ip);

        var telefon = Telefon();
        var javoblar = QuestionnaireSamples.Full(Bugun, telefon) with { VacancyId = vakansiya };

        using var javob = await YuborAsync(mehmon, javoblar, Guid.NewGuid());
        javob.StatusCode.ShouldBe(HttpStatusCode.Created, await MatnAsync(javob));

        var natija = await javob.Content.ReadFromJsonAsync<KioskSubmittedResponse>(Ct);
        natija!.FormNumber.ShouldBeGreaterThan(0);

        var matn = await MatnAsync(javob);
        matn.ShouldNotContain("candidate", Case.Insensitive, "ochiq formaga nomzod yozuvi haqida hech narsa qaytmaydi");

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var anketa = await db.CandidateForms.AsNoTracking().SingleAsync(form => form.Number == natija.FormNumber, Ct);
        var nomzod = await db.Candidates.AsNoTracking()
            .Include(candidate => candidate.Files)
            .Include(candidate => candidate.Questionnaire)
            .SingleAsync(candidate => candidate.Id == anketa.CandidateId, Ct);

        nomzod.Source.ShouldBe(CandidateSource.OnlineForm);
        nomzod.VacancyId.ShouldBe(vakansiya);
        nomzod.Phone!.Value.ShouldBe(telefon);
        nomzod.BirthDate.ShouldBe(javoblar.BirthDate);
        nomzod.Address.ShouldBe(javoblar.ActualAddress);
        nomzod.PhotoKey.ShouldNotBeNull();
        nomzod.CurrentStageId.ShouldNotBeNull("nomzod voronkaning birinchi bosqichiga tushadi");

        var birinchiBosqich = await db.PipelineStages.AsNoTracking()
            .Where(stage => stage.IsActive)
            .OrderBy(stage => stage.SortOrder)
            .FirstAsync(Ct);
        nomzod.CurrentStageId.ShouldBe(birinchiBosqich.Id);

        nomzod.Questionnaire.ShouldNotBeNull();
        nomzod.Questionnaire.Education.ShouldBe(EducationLevel.HigherComplete);
        nomzod.Questionnaire.MaritalStatus.ShouldBe(MaritalStatus.Married);
        nomzod.Questionnaire.ExpectedSalary.ShouldBe(SalaryExpectation.From5To7Million, "6 mln — 5–7 oralig'i");
        nomzod.Questionnaire.RussianLevel.ShouldBe(Domain.Employees.LanguageLevel.UpperIntermediate);
        nomzod.ReferralChannelId.ShouldBe(ReferralSeeds.Acquaintance, "«qayerdan bilgan» — nomzodning o'zida");
        nomzod.ReferralDetail.ShouldBe("Karimov Anvar (sex boshlig‘i)");
        nomzod.Questionnaire.DriverLicenseCategories.ShouldBe("BC");
        nomzod.Questionnaire.OfficeSkills.ShouldBe("MS Word — 3, MS Excel — 2, MS PowerPoint — 1, Google Docs / Sheets — 2", "faqat ofis guruhi");
        nomzod.Questionnaire.OfferAcceptedAt.ShouldNotBeNull();

        anketa.FormCode.ShouldBe("HR-F02");
        anketa.Language.ShouldBe("uz-Latn");
        anketa.PhotoKey.ShouldNotBe(nomzod.PhotoKey, "anketaning surati nomzod suratidan mustaqil nusxa");
        anketa.FileId.ShouldNotBeNull();

        var fayl = nomzod.Files.ShouldHaveSingleItem();
        fayl.Id.ShouldBe(anketa.FileId.Value);
        fayl.ContentType.ShouldBe("application/pdf");
        fayl.FileName.ShouldBe($"Anketa-HR-F02-{natija.FormNumber}-Gulomov-Sardor.pdf");

        // Biriktirilgan fayl haqiqiy PDF
        using var yuklash = await admin.GetAsync(new Uri($"/api/recruiting/candidates/{nomzod.Id}/files/{fayl.Id}", UriKind.Relative), Ct);
        yuklash.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await PdfmiAsync(yuklash)).ShouldBeTrue();

        // Javoblar JSON'da, tozalangan holda (Telegram «@» siz, toifalar qog'ozdagi tartibda)
        using var hujjat = JsonDocument.Parse(anketa.Answers);
        hujjat.RootElement.GetProperty("telegram").GetString().ShouldBe("sardor_gulomov");
        hujjat.RootElement.GetProperty("driverLicenseCategories").EnumerateArray().Select(item => item.GetString()).ShouldBe(["B", "C"]);
        hujjat.RootElement.GetProperty("militaryService").GetString().ShouldBe("Served");
        hujjat.RootElement.GetProperty("militaryServiceMonths").GetInt32().ShouldBe(12);
        hujjat.RootElement.GetProperty("computerSkills").EnumerateObject()
            .Select(skill => (skill.Name, skill.Value.GetInt32()))
            .ShouldBe(javoblar.ComputerSkills!.Select(skill => (skill.Key, skill.Value)), ignoreOrder: true, "hamma 11 dastur saqlandi");
        hujjat.RootElement.GetProperty("computerSkillsOther").GetString().ShouldBe("KOMPAS-3D o‘quv versiyasi");

        // Audit — «Onlayn anketa» (Anonymous), mijozning haqiqiy IP'si bilan; javoblar jurnalga tushmaydi
        var audit = await db.AuditLogs.AsNoTracking()
            .SingleAsync(row => row.EntityName == nameof(CandidateForm) && row.EntityId == anketa.Id.ToString(), Ct);
        audit.ActorKind.ShouldBe(AuditActorKind.Anonymous);
        audit.ActorName.ShouldBe("Onlayn anketa");
        audit.UserId.ShouldBeNull();
        audit.IpAddress.ShouldBe(ip, "Caddy bergan X-Bonvi-Client-Ip — Cloudflare chekkasi emas");
        audit.Operation.ShouldBe("SubmitKioskQuestionnaire");
        (audit.NewValues ?? string.Empty).ShouldNotContain("sardor_gulomov");

        // Shu so'rovdagi boshqa qatorlar (yangi nomzod) ham o'sha bajaruvchi bilan
        var nomzodAuditi = await db.AuditLogs.AsNoTracking()
            .Where(row => row.EntityName == nameof(Candidate) && row.EntityId == nomzod.Id.ToString())
            .ToListAsync(Ct);
        nomzodAuditi.ShouldNotBeEmpty();
        nomzodAuditi.ShouldAllBe(row => row.ActorKind == AuditActorKind.Anonymous && row.ActorName == "Onlayn anketa" && row.IpAddress == ip);
    }

    [Fact]
    public async Task Jarayondagi_nomzodga_yoziladi_va_faqat_bosh_maydonlar_toldiriladi()
    {
        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        var vakansiya = await VakansiyaAsync(admin);
        var telefon = Telefon();

        // Botdan kelgan nomzod: anketasi bor, manzili bor, tug'ilgan sanasi YO'Q
        using (var bot = await admin.PostAsJsonAsync(
                   new Uri("/api/recruiting/candidates", UriKind.Relative),
                   new
                   {
                       vacancyId = vakansiya,
                       lastName = "Каримов",
                       firstName = "Азиз",
                       phone = telefon,
                       address = "Botdagi manzil",
                       source = "TelegramBot",
                       telegramChatId = 777001L,
                       referralChannelId = ReferralSeeds.Telegram,
                       questionnaire = new
                       {
                           education = "VocationalSchool",
                           maritalStatus = "Unmarried",
                           russianLevel = "Intermediate",
                           expectedSalary = "From2To3Million",
                       },
                   },
                   Ct))
        {
            bot.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(bot));
        }

        using var mehmon = Mehmon();

        var javoblar = QuestionnaireSamples.Full(Bugun, telefon) with
        {
            LastName = "Karimov",
            FirstName = "Aziz",
            MiddleName = null,
            SignatureName = "Karimov Aziz",
        };

        using var javob = await YuborAsync(mehmon, javoblar, Guid.NewGuid());
        javob.StatusCode.ShouldBe(HttpStatusCode.Created, await MatnAsync(javob));
        var natija = await javob.Content.ReadFromJsonAsync<KioskSubmittedResponse>(Ct);

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var shuTelefon = await db.Candidates.AsNoTracking()
            .Include(candidate => candidate.Questionnaire)
            .Include(candidate => candidate.Files)
            .Where(candidate => candidate.Phone == PhoneNumber.FromTrusted(telefon))
            .ToListAsync(Ct);

        var nomzod = shuTelefon.ShouldHaveSingleItem("yangi karta OCHILMAYDI — bitta odam ikki kartaga bo'linmasin");

        nomzod.Source.ShouldBe(CandidateSource.TelegramBot, "manba tarixiy fakt — o'zgarmaydi");
        nomzod.FullName.LastName.ShouldBe("Каримов", "ism o'zgartirilmaydi");
        nomzod.Address.ShouldBe("Botdagi manzil", "bor maydon ustidan yozilmaydi");
        nomzod.BirthDate.ShouldBe(javoblar.BirthDate, "bo'sh maydon to'ldiriladi");
        nomzod.PhotoKey.ShouldNotBeNull("surat yo'q edi — formadagisi qo'yiladi");

        nomzod.ReferralChannelId.ShouldBe(ReferralSeeds.Telegram, "bot qo'ygan «qayerdan bilgan» ustidan yozilmaydi");
        nomzod.ReferralDetail.ShouldBeNull();

        nomzod.Questionnaire.ShouldNotBeNull();
        nomzod.Questionnaire.Education.ShouldBe(EducationLevel.VocationalSchool, "bot anketasi o'zgarmas");
        nomzod.Questionnaire.ExpectedSalary.ShouldBe(SalaryExpectation.From2To3Million);

        var anketa = await db.CandidateForms.AsNoTracking().SingleAsync(form => form.Number == natija!.FormNumber, Ct);
        anketa.CandidateId.ShouldBe(nomzod.Id);
        nomzod.Files.ShouldContain(file => file.Id == anketa.FileId);
    }

    [Fact]
    public async Task Takroriy_yuborish_boshqa_IPdan_ham_bitta_anketa_va_bitta_nomzod()
    {
        using var birinchiIp = Mehmon();
        using var ikkinchiIp = Mehmon();

        var telefon = Telefon();
        var yuborish = Guid.NewGuid();
        var javoblar = QuestionnaireSamples.Minimal(Bugun, telefon);

        using var birinchi = await YuborAsync(birinchiIp, javoblar, yuborish);
        birinchi.StatusCode.ShouldBe(HttpStatusCode.Created, await MatnAsync(birinchi));
        var avval = await birinchi.Content.ReadFromJsonAsync<KioskSubmittedResponse>(Ct);

        // Wi-Fi'dan mobil internetga o'tgan telefon — IP boshqa, identifikator o'sha: o'sha javob
        using var ikkinchi = await YuborAsync(ikkinchiIp, javoblar, yuborish);
        ikkinchi.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(ikkinchi));
        var keyin = await ikkinchi.Content.ReadFromJsonAsync<KioskSubmittedResponse>(Ct);

        keyin.ShouldBe(avval, "takroriy yuborish o'sha javobni oladi — raqam ham, vaqt ham");

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        (await db.CandidateForms.CountAsync(form => form.ClientSubmissionId == yuborish, Ct)).ShouldBe(1);
        (await db.Candidates.CountAsync(candidate => candidate.Phone == PhoneNumber.FromTrusted(telefon), Ct)).ShouldBe(1);
    }

    [Fact]
    public async Task Fayllar_chegarasi_toldiq_bolsa_ham_anketa_saqlanadi()
    {
        using var mehmon = Mehmon();

        var telefon = Telefon();
        var nomzodId = await TolaNomzodAsync(telefon);

        var javoblar = QuestionnaireSamples.Minimal(Bugun, telefon);

        using var javob = await YuborAsync(mehmon, javoblar, Guid.NewGuid());
        javob.StatusCode.ShouldBe(HttpStatusCode.Created, await MatnAsync(javob));
        var natija = await javob.Content.ReadFromJsonAsync<KioskSubmittedResponse>(Ct);

        using var korish = await HrAsync(Permissions.RecruitingView);
        var anketalar = await korish.GetFromJsonAsync<List<CandidateFormResponse>>(
            new Uri($"/api/recruiting/candidates/{nomzodId}/forms", UriKind.Relative), Ct);

        var anketa = anketalar!.ShouldHaveSingleItem();
        anketa.Number.ShouldBe(natija!.FormNumber);
        anketa.FileId.ShouldBeNull("10 ta fayl bor edi — PDF biriktirilmadi");

        using var pdf = await korish.GetAsync(new Uri($"/api/recruiting/candidates/{nomzodId}/forms/{anketa.Id}/pdf", UriKind.Relative), Ct);
        pdf.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(pdf));
        pdf.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        (await PdfmiAsync(pdf)).ShouldBeTrue("PDF har safar javoblardan qayta chiziladi");
    }

    /// <summary>
    /// Jinsi, pasport va JSHSHIR so'ralmaydi, harbiy xizmat hammaga ixtiyoriy (egasi, 2026-10-26).
    /// Eski forma (yoki qo'lda yasalgan so'rov) bu kalitlarni yuborsa ham anketa qabul qilinadi —
    /// ular noma'lum kalit kabi e'tiborsiz qoladi va bazaga YOZILMAYDI.
    /// </summary>
    [Fact]
    public async Task Jinsi_pasporti_va_harbiy_xizmatisiz_anketa_qabul_qilinadi_eski_kalitlar_saqlanmaydi()
    {
        using var mehmon = Mehmon();

        // 1. Yangi forma: olib tashlangan kalitlarsiz va harbiy xizmatsiz
        var javoblar = QuestionnaireSamples.Minimal(Bugun, Telefon());
        javoblar.MilitaryService.ShouldBeNull("namuna harbiy xizmatsiz");

        using (var toza = await YuborAsync(mehmon, javoblar, Guid.NewGuid()))
        {
            toza.StatusCode.ShouldBe(HttpStatusCode.Created, await MatnAsync(toza));

            var anketa = await SaqlanganAsync(toza);
            anketa.FileId.ShouldNotBeNull("PDF chizildi va biriktirildi");

            using var hujjat = JsonDocument.Parse(anketa.Answers);
            hujjat.RootElement.TryGetProperty("militaryService", out _).ShouldBeFalse();
        }

        // 2. Eski forma: o'sha kalitlar bilan
        var eski = JsonNode.Parse(QuestionnaireFormJson.Write(QuestionnaireSamples.Minimal(Bugun, Telefon())))!.AsObject();
        eski["gender"] = "Female";
        eski["passportNumber"] = "AD1234567";
        eski["passportIssuedBy"] = "Chilonzor tumani IIB";
        eski["passportIssuedAt"] = "2020-01-15";
        eski["pinfl"] = "31203951234567";

        using (var eskiJavob = await YuborXomAsync(mehmon, eski.ToJsonString(), Guid.NewGuid()))
        {
            eskiJavob.StatusCode.ShouldBe(HttpStatusCode.Created, await MatnAsync(eskiJavob));

            var anketa = await SaqlanganAsync(eskiJavob);

            using var hujjat = JsonDocument.Parse(anketa.Answers);
            foreach (var kalit in new[] { "gender", "passportNumber", "passportIssuedBy", "passportIssuedAt", "pinfl" })
            {
                hujjat.RootElement.TryGetProperty(kalit, out _).ShouldBeFalse(kalit);
            }

            anketa.Answers.ShouldNotContain("AD1234567");
            anketa.Answers.ShouldNotContain("31203951234567");
        }

        async Task<CandidateForm> SaqlanganAsync(HttpResponseMessage javob)
        {
            var natija = await javob.Content.ReadFromJsonAsync<KioskSubmittedResponse>(Ct);

            await using var qamrov = _factory.Services.CreateAsyncScope();
            var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

            return await db.CandidateForms.AsNoTracking().SingleAsync(form => form.Number == natija!.FormNumber, Ct);
        }
    }

    [Fact]
    public async Task Tekshiruv_xatosi_kod_va_maydon_bilan_qaytadi()
    {
        using var mehmon = Mehmon();

        var javoblar = QuestionnaireSamples.Minimal(Bugun, Telefon()) with { FamilyMembers = [] };

        using var javob = await YuborAsync(mehmon, javoblar, Guid.NewGuid());
        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using var xato = JsonDocument.Parse(await MatnAsync(javob));
        xato.RootElement.GetProperty("title").GetString().ShouldBe("anketa.majburiy");
        xato.RootElement.GetProperty("detail").GetString().ShouldBe("familyMembers", "forma maydon yo'lini «detail» dan o'qiydi");
        xato.RootElement.GetProperty("field").GetString().ShouldBe("familyMembers");

        // Imzo ismi mos emas
        using var imzo = await YuborAsync(mehmon, QuestionnaireSamples.Minimal(Bugun, Telefon()) with { SignatureName = "Boshqa Odam" }, Guid.NewGuid());
        imzo.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await MatnAsync(imzo)).ShouldContain("anketa.imzo_ism");

        // Imzo rasmi PNG emas
        using var buzuqImzo = await YuborAsync(mehmon, QuestionnaireSamples.Minimal(Bugun, Telefon()), Guid.NewGuid(), signature: [1, 2, 3, 4]);
        buzuqImzo.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await MatnAsync(buzuqImzo)).ShouldContain("anketa.imzo_yaroqsiz");
    }

    /// <summary>
    /// Kompyuter jadvalidan olib tashlangan dastur (2026-10-26, masalan «python») — 400, maydon yo'li
    /// bilan, va hech narsa yozilmaydi: forma faqat 11 kalitni yuboradi.
    /// </summary>
    [Fact]
    public async Task Olib_tashlangan_dastur_kaliti_400_va_hech_narsa_yozilmaydi()
    {
        using var mehmon = Mehmon();

        var telefon = Telefon();
        var yuborish = Guid.NewGuid();
        var javoblar = QuestionnaireSamples.Minimal(Bugun, telefon) with
        {
            ComputerSkills = new Dictionary<string, int> { ["msWord"] = 2, ["python"] = 2 },
        };

        using (var javob = await YuborAsync(mehmon, javoblar, yuborish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

            using var xato = JsonDocument.Parse(await MatnAsync(javob));
            xato.RootElement.GetProperty("title").GetString().ShouldBe("anketa.notogri");
            xato.RootElement.GetProperty("detail").GetString().ShouldBe("computerSkills.python");
        }

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        (await db.CandidateForms.CountAsync(form => form.ClientSubmissionId == yuborish, Ct)).ShouldBe(0);
        (await db.Candidates.CountAsync(candidate => candidate.Phone == PhoneNumber.FromTrusted(telefon), Ct)).ShouldBe(0);
    }

    [Fact]
    public async Task Anketalar_royxati_va_PDF_recruiting_view_bilan_200_siz_403_boshqa_nomzod_404()
    {
        using var mehmon = Mehmon();

        using var javob = await YuborAsync(mehmon, QuestionnaireSamples.Minimal(Bugun, Telefon()), Guid.NewGuid());
        javob.StatusCode.ShouldBe(HttpStatusCode.Created, await MatnAsync(javob));
        var natija = await javob.Content.ReadFromJsonAsync<KioskSubmittedResponse>(Ct);

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
        var anketa = await db.CandidateForms.AsNoTracking().SingleAsync(form => form.Number == natija!.FormNumber, Ct);

        using var begona = await HrAsync(Permissions.EmployeesView);
        using (var royxat = await begona.GetAsync(new Uri($"/api/recruiting/candidates/{anketa.CandidateId}/forms", UriKind.Relative), Ct))
        {
            royxat.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using (var pdf = await begona.GetAsync(new Uri($"/api/recruiting/candidates/{anketa.CandidateId}/forms/{anketa.Id}/pdf", UriKind.Relative), Ct))
        {
            pdf.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using var korish = await HrAsync(Permissions.RecruitingView);

        using (var royxat = await korish.GetAsync(new Uri($"/api/recruiting/candidates/{anketa.CandidateId}/forms", UriKind.Relative), Ct))
        {
            royxat.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(royxat));

            var matn = await MatnAsync(royxat);
            matn.ShouldNotContain("deviceName", Case.Insensitive, "qurilma nomi endi yo'q");

            var qatorlar = await royxat.Content.ReadFromJsonAsync<List<CandidateFormResponse>>(Ct);
            var qator = qatorlar.ShouldNotBeNull().ShouldHaveSingleItem();
            qator.Id.ShouldBe(anketa.Id);
            qator.Number.ShouldBe(anketa.Number);
            qator.FormCode.ShouldBe("HR-F02");
            qator.FormVersion.ShouldBe("3.0");
            qator.Language.ShouldBe("uz-Latn");
            qator.FileId.ShouldBe(anketa.FileId);
        }

        using (var pdf = await korish.GetAsync(new Uri($"/api/recruiting/candidates/{anketa.CandidateId}/forms/{anketa.Id}/pdf", UriKind.Relative), Ct))
        {
            pdf.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(pdf));
            (await PdfmiAsync(pdf)).ShouldBeTrue();
        }

        using var adashgan = await korish.GetAsync(new Uri($"/api/recruiting/candidates/{Guid.NewGuid()}/forms/{anketa.Id}/pdf", UriKind.Relative), Ct);
        adashgan.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await MatnAsync(adashgan)).ShouldContain("anketa.topilmadi");
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    private async Task<HttpClient> HrAsync(params string[] permissions)
    {
        var token = await TestAccounts.TokenAsync(_factory, permissions);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    /// <summary>Ochiq formaning mijozi — tokensiz, kalitsiz, Caddy beradigan mijoz IP'si bilan.</summary>
    private HttpClient Mehmon(string? ip = null) => Mehmon(_factory, ip ?? NoyobIp());

    private static HttpClient Mehmon(WebApplicationFactory<Program> factory, string ip)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ClientIp.HeaderName, ip);

        return client;
    }

    /// <summary>Har test o'z IP'si bilan — yuborish chegarasi IP bo'yicha (sinf izohi).</summary>
    private static string NoyobIp()
    {
        var son = Interlocked.Increment(ref _ipSanogi);

        return string.Create(CultureInfo.InvariantCulture, $"198.51.{son / 250 % 250}.{(son % 250) + 1}");
    }

    private static async Task<HttpResponseMessage> YuborAsync(
        HttpClient client,
        QuestionnaireFormAnswers answers,
        Guid submissionId,
        byte[]? signature = null,
        string website = "",
        string? turnstileToken = null)
    {
        using var forma = new MultipartFormDataContent
        {
            { new StringContent(QuestionnaireFormJson.Write(answers)), "answers" },
            { new StringContent(submissionId.ToString()), "clientSubmissionId" },
            { new StringContent("uz-Latn"), "language" },

            // Forma «tuzoq» maydonni doim yuboradi — odam uni ko'rmaydi va bo'sh qoladi
            { new StringContent(website), "website" },
        };

        // Bot tekshiruvi tokeni — forma faqat seansda kalit bo'lsa yuboradi
        if (turnstileToken is not null)
        {
            forma.Add(new StringContent(turnstileToken), "turnstileToken");
        }

        var rasm = new ByteArrayContent(QuestionnaireSamples.Photo());
        rasm.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        forma.Add(rasm, "photo", "selfi.jpg");

        var imzo = new ByteArrayContent(signature ?? QuestionnaireSamples.Signature());
        imzo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        forma.Add(imzo, "signature", "imzo.png");

        return await client.PostAsync(new Uri(SubmitPath, UriKind.Relative), forma, Ct);
    }

    private static async Task<Guid> VakansiyaAsync(HttpClient admin)
    {
        using var javob = await admin.PostAsJsonAsync(
            new Uri("/api/recruiting/vacancies", UriKind.Relative),
            new { title = "Onlayn anketa sinovi " + Guid.NewGuid().ToString("N")[..8], openings = 1, openedAt = new DateOnly(2026, 9, 1) },
            Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        using var hujjat = JsonDocument.Parse(await MatnAsync(javob));
        return hujjat.RootElement.GetProperty("id").GetGuid();
    }

    /// <summary>Jarayondagi nomzod — o'nta fayli bilan (chegara to'lgan).</summary>
    private async Task<Guid> TolaNomzodAsync(string telefon)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var umumiy = await db.SystemSettings.AsNoTracking()
            .SingleAsync(row => row.Key == Domain.Settings.SystemSettingKeys.GeneralPoolVacancyId, Ct);

        var nomzod = Candidate.Register(
            FullName.Create("Каримова", "Ойдин", null).Value,
            Guid.Parse(umumiy.Value),
            CandidateSource.Manual,
            phone: PhoneNumber.FromTrusted(telefon)).Value;

        for (var index = 0; index < Candidate.MaxFiles; index++)
        {
            nomzod.AttachFile($"sinov/fayl-{index}.pdf", $"fayl-{index}.pdf", "application/pdf", 1024).IsSuccess.ShouldBeTrue();
        }

        db.Candidates.Add(nomzod);
        await db.SaveChangesAsync(Ct);

        return nomzod.Id;
    }

    private static string Telefon()
        => "+99890"
           + Random.Shared.Next(10_000, 99_999).ToString(CultureInfo.InvariantCulture)
           + (Interlocked.Increment(ref _telefonSanogi) % 100).ToString("D2", CultureInfo.InvariantCulture);

    private static async Task<bool> PdfmiAsync(HttpResponseMessage javob)
    {
        var bytes = await javob.Content.ReadAsByteArrayAsync(Ct);
        return bytes.Length > 5 && System.Text.Encoding.ASCII.GetString(bytes, 0, 5) == "%PDF-";
    }

    private static Task<string> MatnAsync(HttpResponseMessage javob) => PublicApiHarness.MatnAsync(javob);
}
