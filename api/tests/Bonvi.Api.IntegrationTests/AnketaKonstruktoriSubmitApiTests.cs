using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bonvi.Api.Common;
using Bonvi.Application.Blanks;
using Bonvi.Application.Recruiting.Forms;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.Settings;
using Bonvi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Konstruktor anketasini yuborish (SPEC-anketa-konstruktori §6, AC-AK4/5/9) va HR ko'rinishi
/// (§9, AC-AK11) — soxta HRMentor bilan.
/// </summary>
/// <remarks>
/// Versiyalar bazada faqat o'sadi va testlar tartibi kafolatlanmagan: har test avval v1 ni
/// sinxronlaydi (v2 allaqachon amalda bo'lsa — e'tiborsiz, lekin v1 saqlangan bo'ladi), keyin
/// faqat o'zi aytgan versiya bilan yuboradi.
/// </remarks>
public sealed class AnketaKonstruktoriSubmitApiTests : IClassFixture<BonviApiFactory>
{
    private const string SessionPath = "/api/kiosk/questionnaire/session";
    private const string SubmitPath = "/api/kiosk/questionnaire";
    private const string PreviewPath = "/api/kiosk/questionnaire/preview";

    private static int _telefonSanogi;
    private static int _ipSanogi;

    private readonly BonviApiFactory _factory;

    public AnketaKonstruktoriSubmitApiTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ────────────────────────────────────────────────────────────
    //  AC-AK9 / AC-AK5: urug' = o'rnatilgan anketa
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Urug_bilan_yuborilgan_anketa_ornatilgani_bilan_bir_xil_nomzod_va_xulosa_beradi()
    {
        var hrMentor = new HrMentorFake();
        await using var fabrika = Fabrika(_factory, hrMentor);
        using var admin = await AdminAsync(fabrika, hrMentor);

        var bugun = Bugun(fabrika);
        var vakansiya = await VakansiyaAsync(admin);
        using var mehmon = Mehmon(fabrika);

        // Bir odam, ikki yo'l — faqat telefon (va undan kelib chiqqan tug'ilgan sana) boshqa,
        // aks holda ikkinchisi birinchisining kartasiga qo'shilardi
        var ichki = QuestionnaireSamples.Full(bugun, Telefon()) with { VacancyId = vakansiya };
        var konstruktor = QuestionnaireSamples.Full(bugun, Telefon()) with { VacancyId = vakansiya };

        // AC-AK5: e'lon bor, lekin forma versiya yubormadi — o'rnatilgan anketa, o'zgarishsiz
        var ichkiRaqam = await YuborOkAsync(mehmon, QuestionnaireFormJson.Write(ichki), definitionVersion: null);
        var konstruktorRaqam = await YuborOkAsync(mehmon, DynamicFormSamples.From(konstruktor).ToJsonString(), definitionVersion: "1");

        await using var qamrov = fabrika.Services.CreateAsyncScope();
        var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var (ichkiAnketa, ichkiNomzod) = await AnketaAsync(db, ichkiRaqam);
        var (konAnketa, konNomzod) = await AnketaAsync(db, konstruktorRaqam);

        ichkiAnketa.FormVersion.ShouldBe(CandidateForm.HrF02Version);
        ichkiAnketa.DefinitionVersion.ShouldBeNull();
        konAnketa.FormVersion.ShouldBe("4.0", "ta'rifning yorlig'i");
        konAnketa.DefinitionVersion.ShouldBe(1);
        konAnketa.TextsRevision.ShouldBeNull("so'zlar ta'rifda — HR matnlari reviziyasi yozilmaydi");
        konAnketa.FormCode.ShouldBe(ichkiAnketa.FormCode);
        konAnketa.Language.ShouldBe(ichkiAnketa.Language);
        konAnketa.FileId.ShouldNotBeNull();

        // Nomzod kartasi — bir xil
        konNomzod.Id.ShouldNotBe(ichkiNomzod.Id);
        konNomzod.FullName.ShouldBe(ichkiNomzod.FullName);
        konNomzod.VacancyId.ShouldBe(ichkiNomzod.VacancyId);
        konNomzod.Source.ShouldBe(ichkiNomzod.Source);
        konNomzod.Status.ShouldBe(ichkiNomzod.Status);
        konNomzod.CurrentStageId.ShouldBe(ichkiNomzod.CurrentStageId);
        konNomzod.Address.ShouldBe(ichkiNomzod.Address);
        konNomzod.Speciality.ShouldBe(ichkiNomzod.Speciality);
        konNomzod.ReferralChannelId.ShouldBe(ichkiNomzod.ReferralChannelId);
        konNomzod.ReferralDetail.ShouldBe(ichkiNomzod.ReferralDetail);
        konNomzod.Phone!.Value.ShouldBe(konstruktor.Phone);
        konNomzod.BirthDate.ShouldBe(konstruktor.BirthDate);
        konNomzod.PhotoKey.ShouldNotBeNull();

        // Anketa xulosasi — HAR BIR ustun (yangisi qo'shilsa ham shu yerda solishtiriladi)
        var ustunlar = typeof(CandidateQuestionnaire)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.Name is not (nameof(Entity.Id) or nameof(CandidateQuestionnaire.CandidateId) or nameof(Entity.DomainEvents))
                               && property.PropertyType != typeof(DateTimeOffset)
                               && property.PropertyType != typeof(DateTimeOffset?))
            .ToList();

        ustunlar.Count.ShouldBeGreaterThan(10, "solishtirish bo'sh bo'lmasin");

        foreach (var ustun in ustunlar)
        {
            ustun.GetValue(konNomzod.Questionnaire).ShouldBe(ustun.GetValue(ichkiNomzod.Questionnaire), ustun.Name);
        }

        konNomzod.Questionnaire!.OfferAcceptedAt.ShouldNotBeNull();

        // Javoblar — ta'rif shaklida (tekshirilgan nusxa)
        using (var javoblar = JsonDocument.Parse(konAnketa.Answers))
        {
            javoblar.RootElement.GetProperty("phone").GetString().ShouldBe(konstruktor.Phone);
            javoblar.RootElement.GetProperty("carModel").GetString().ShouldBe("Chevrolet Cobalt");
            javoblar.RootElement.GetProperty("signature").GetProperty("name").GetString().ShouldBe(konstruktor.SignatureName);
            javoblar.RootElement.GetProperty("jobs").GetArrayLength().ShouldBe(2);
            javoblar.RootElement.TryGetProperty("Phone", out _).ShouldBeFalse("o'rnatilgan anketa shakli emas");
        }

        // Biriktirilgan PDF va HR qayta chizgani — o'sha versiyadan
        var fayl = konNomzod.Files.ShouldHaveSingleItem();
        fayl.Id.ShouldBe(konAnketa.FileId!.Value);
        fayl.ContentType.ShouldBe("application/pdf");

        using (var yuklash = await admin.GetAsync(new Uri($"/api/recruiting/candidates/{konNomzod.Id}/files/{fayl.Id}", UriKind.Relative), Ct))
        {
            yuklash.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await PdfmiAsync(yuklash)).ShouldBeTrue();
        }

        using var qayta = await admin.GetAsync(new Uri($"/api/recruiting/candidates/{konNomzod.Id}/forms/{konAnketa.Id}/pdf", UriKind.Relative), Ct);
        qayta.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qayta));
        (await PdfmiAsync(qayta)).ShouldBeTrue();
    }

    // ────────────────────────────────────────────────────────────
    //  AC-AK4: nomzod ko'rgan versiya
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Yangi_versiya_chiqsa_eski_sahifadagi_anketa_oz_versiyasi_bilan_saqlanadi()
    {
        var hrMentor = new HrMentorFake();
        await using var fabrika = Fabrika(_factory, hrMentor);
        using var admin = await AdminAsync(fabrika, hrMentor);

        hrMentor.PublishedDefinition = AnketaKonstruktoriSyncApiTests.Elon(2, AnketaKonstruktoriSyncApiTests.Tarif("4.1"));
        await SyncAsync(admin);

        var bugun = Bugun(fabrika);
        using var mehmon = Mehmon(fabrika);

        // Seans — amaldagi versiya va to'liq ta'rif
        using (var seansJavobi = await mehmon.GetAsync(new Uri(SessionPath, UriKind.Relative), Ct))
        {
            seansJavobi.StatusCode.ShouldBe(HttpStatusCode.OK);
            using var seans = JsonDocument.Parse(await MatnAsync(seansJavobi));
            var tarif = seans.RootElement.GetProperty("definition");

            tarif.GetProperty("version").GetInt32().ShouldBe(2);
            tarif.GetProperty("label").GetString().ShouldBe("4.1");
            tarif.GetProperty("definition").GetProperty("sections").GetArrayLength().ShouldBeGreaterThan(5);
            seans.RootElement.GetProperty("formVersion").GetString().ShouldBe(CandidateForm.HrF02Version, "ilovaning qayta yuklash belgisi — yorliq emas");
        }

        // v1 ochiq qolgan sahifadan — v1 bilan; yangi sahifadan — v2 bilan
        var eski = await YuborOkAsync(mehmon, DynamicFormSamples.From(QuestionnaireSamples.Minimal(bugun, Telefon())).ToJsonString(), "1");
        var yangi = await YuborOkAsync(mehmon, DynamicFormSamples.From(QuestionnaireSamples.Minimal(bugun, Telefon())).ToJsonString(), "2");

        await using var qamrov = fabrika.Services.CreateAsyncScope();
        var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var v1 = await db.CandidateForms.AsNoTracking().SingleAsync(form => form.Number == eski, Ct);
        (v1.DefinitionVersion, v1.FormVersion).ShouldBe((1, "4.0"));

        var v2 = await db.CandidateForms.AsNoTracking().SingleAsync(form => form.Number == yangi, Ct);
        (v2.DefinitionVersion, v2.FormVersion).ShouldBe((2, "4.1"));
    }

    [Fact]
    public async Task Notanish_yoki_buzuq_versiya_anketa_versiya_xatosini_beradi()
    {
        var hrMentor = new HrMentorFake();
        await using var fabrika = Fabrika(_factory, hrMentor);
        using var admin = await AdminAsync(fabrika, hrMentor);

        var bugun = Bugun(fabrika);
        using var mehmon = Mehmon(fabrika);
        var javoblar = DynamicFormSamples.From(QuestionnaireSamples.Minimal(bugun, Telefon())).ToJsonString();

        foreach (var versiya in new[] { "99", "0", "-1", "1.0", " 1x", "abc", "99999999999" })
        {
            using var javob = await YuborAsync(mehmon, javoblar, versiya);
            javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest, versiya);

            using var xato = JsonDocument.Parse(await MatnAsync(javob));
            xato.RootElement.GetProperty("title").GetString().ShouldBe("anketa.versiya", versiya);
            xato.RootElement.GetProperty("field").GetString().ShouldBe("definitionVersion", versiya);
        }
    }

    // ────────────────────────────────────────────────────────────
    //  R5: ko'rinmaydigan javob tashlanadi; xato — o'z maydon yo'li bilan
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Yashirin_maydon_javobi_tashlanadi_majburiy_xato_maydon_yoli_bilan_qaytadi()
    {
        var hrMentor = new HrMentorFake();
        await using var fabrika = Fabrika(_factory, hrMentor);
        using var admin = await AdminAsync(fabrika, hrMentor);

        var bugun = Bugun(fabrika);
        using var mehmon = Mehmon(fabrika);

        // Mashinasi yo'q — rusumi ko'rinmaydi, yuborilgan bo'lsa ham saqlanmaydi
        var mashinasiz = QuestionnaireSamples.Minimal(bugun, Telefon()) with { HasCar = false, CarModel = "Damas" };
        var raqam = await YuborOkAsync(mehmon, DynamicFormSamples.From(mashinasiz).ToJsonString(), "1");

        await using (var qamrov = fabrika.Services.CreateAsyncScope())
        {
            var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
            var anketa = await db.CandidateForms.AsNoTracking().SingleAsync(form => form.Number == raqam, Ct);

            using var saqlangan = JsonDocument.Parse(anketa.Answers);
            saqlangan.RootElement.GetProperty("hasCar").GetBoolean().ShouldBeFalse();
            saqlangan.RootElement.TryGetProperty("carModel", out _).ShouldBeFalse("ko'rinmagan savolning javobi hujjatga tushmaydi");
        }

        var minimal = QuestionnaireSamples.Minimal(bugun, Telefon());

        var familiyasiz = DynamicFormSamples.From(minimal);
        familiyasiz.Remove("lastName");

        var oilaIsmsiz = DynamicFormSamples.From(minimal);
        oilaIsmsiz["familyMembers"]![0]!.AsObject().Remove("fullName");

        var boshqaImzo = DynamicFormSamples.From(minimal);
        boshqaImzo["signature"] = new JsonObject { ["name"] = "Boshqa Odam" };

        var kanalsiz = DynamicFormSamples.From(minimal);
        kanalsiz["referral"] = new JsonObject { ["channel"] = Guid.NewGuid().ToString() };

        foreach (var (nom, javoblar, kod, maydon) in new[]
                 {
                     ("familiya yo'q", familiyasiz, "anketa.majburiy", "lastName"),
                     ("oila a'zosining ismi yo'q", oilaIsmsiz, "anketa.majburiy", "familyMembers[0].fullName"),
                     ("imzodagi ism boshqa", boshqaImzo, "anketa.imzo_ism", "signature.name"),
                     ("notanish kanal", kanalsiz, "qayerdan.topilmadi", "referral.channel"),
                 })
        {
            using var javob = await YuborAsync(mehmon, javoblar.ToJsonString(), "1");
            javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest, nom);

            using var xato = JsonDocument.Parse(await MatnAsync(javob));
            xato.RootElement.GetProperty("title").GetString().ShouldBe(kod, nom);
            xato.RootElement.GetProperty("field").GetString().ShouldBe(maydon, nom);
        }
    }

    // ────────────────────────────────────────────────────────────
    //  AC-AK11: HR ko'rinishi — alohida baza (ulanmagan holatdan boshlanadi)
    // ────────────────────────────────────────────────────────────

    public sealed class Korinish : IClassFixture<BonviApiFactory>
    {
        private readonly BonviApiFactory _factory;

        public Korinish(BonviApiFactory factory) => _factory = factory;

        [Fact]
        public async Task Korinish_faqat_HR_ga_qoralamani_jonli_beradi_va_hech_narsa_yozmaydi()
        {
            var hrMentor = new HrMentorFake();
            await using var fabrika = Fabrika(_factory, hrMentor);

            var bugun = Bugun(fabrika);
            var minimal = DynamicFormSamples.From(QuestionnaireSamples.Minimal(bugun, Telefon())).ToJsonString();

            // ── Tokensiz — 401, ochiq formaning ochiqligi bu yerga o'tmaydi
            using (var mehmon = Mehmon(fabrika))
            {
                using var seans = await mehmon.GetAsync(new Uri(PreviewPath, UriKind.Relative), Ct);
                seans.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

                using var pdf = await NamunaAsync(mehmon, minimal);
                pdf.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            }

            // ── Ruxsatsiz — 403
            using (var begona = await MijozAsync(fabrika, Permissions.EmployeesView))
            {
                using var seans = await begona.GetAsync(new Uri(PreviewPath, UriKind.Relative), Ct);
                seans.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

                using var pdf = await NamunaAsync(begona, minimal);
                pdf.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            }

            using var hr = await MijozAsync(fabrika, Permissions.RecruitingView);

            // ── Ulanmagan — 409
            await XatoAsync(hr, HttpStatusCode.Conflict, "hrmentor.not_connected");

            using var admin = await MijozAsync(fabrika, Permissions.SettingsManage, Permissions.EmployeesView);
            await UlaAsync(admin);

            // ── Qoralama yo'q — 502
            await XatoAsync(hr, HttpStatusCode.BadGateway, "hrmentor.not_found");

            // ── HRMentor muammo ko'rsatgan qoralama — 502, joyi bilan
            hrMentor.DraftDefinition = Qoralama(AnketaKonstruktoriSyncApiTests.Tarif("4.1"), issues: 1);
            await XatoAsync(hr, HttpStatusCode.BadGateway, "hrmentor.definition_invalid", "issues");

            // ── BonviHR tushunmaydigan tur — 502
            var notanish = AnketaKonstruktoriSyncApiTests.Tarif("4.1");
            AnketaKonstruktoriSyncApiTests.Maydon(notanish, "mahalla")["kind"] = "map";
            hrMentor.DraftDefinition = Qoralama(notanish);
            await XatoAsync(hr, HttpStatusCode.BadGateway, "hrmentor.definition_invalid");

            // ── HRMentor o'chiq — 502
            hrMentor.Down = HrMentorErrors.Unreachable;
            await XatoAsync(hr, HttpStatusCode.BadGateway, "hrmentor.unreachable");
            hrMentor.Down = null;

            // ── To'g'ri qoralama — har safar jonli
            hrMentor.DraftDefinition = Qoralama(AnketaKonstruktoriSyncApiTests.Tarif("4.1"));
            using (var javob = await hr.GetAsync(new Uri(PreviewPath, UriKind.Relative), Ct))
            {
                javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
                using var seans = JsonDocument.Parse(await MatnAsync(javob));

                var tarif = seans.RootElement.GetProperty("definition");
                tarif.GetProperty("version").GetInt32().ShouldBe(0, "qoralama saqlangan versiya emas");
                tarif.GetProperty("label").GetString().ShouldBe("4.1");
                tarif.GetProperty("definition").GetProperty("sections").GetArrayLength().ShouldBeGreaterThan(5);
                seans.RootElement.GetProperty("formVersion").GetString().ShouldBe(CandidateForm.HrF02Version, "ilovaning qayta yuklash belgisi — yorliq emas");
                seans.RootElement.GetProperty("turnstileSiteKey").ValueKind.ShouldBe(JsonValueKind.Null);
                seans.RootElement.GetProperty("texts").ValueKind.ShouldBe(JsonValueKind.Null);
                seans.RootElement.GetProperty("today").GetString().ShouldBe(bugun.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }

            // Yorlig'i hali berilmagan qoralama — «draft»
            var yorliqsiz = AnketaKonstruktoriSyncApiTests.Tarif("4.1");
            yorliqsiz.Remove("label");
            hrMentor.DraftDefinition = Qoralama(yorliqsiz);
            using (var javob = await hr.GetAsync(new Uri(PreviewPath, UriKind.Relative), Ct))
            {
                javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
                using var seans = JsonDocument.Parse(await MatnAsync(javob));
                seans.RootElement.GetProperty("definition").GetProperty("label").GetString().ShouldBe("draft");
            }

            hrMentor.DraftDefinition = Qoralama(AnketaKonstruktoriSyncApiTests.Tarif("4.1"));

            // ── Namuna PDF — surat va imzo bilan ham, ularsiz ham; hech narsa yozilmaydi
            var (nomzodlar, anketalar, fayllar) = await SanaAsync(fabrika);

            var toliq = DynamicFormSamples.From(QuestionnaireSamples.Full(bugun, Telefon())).ToJsonString();
            using (var javob = await NamunaAsync(hr, toliq, withFiles: true))
            {
                javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
                javob.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
                javob.Content.Headers.ContentDisposition!.FileNameStar.ShouldBe("Anketa_namuna_v4.1.pdf");
                (await PdfmiAsync(javob)).ShouldBeTrue();
            }

            using (var javob = await NamunaAsync(hr, minimal))
            {
                javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
                (await PdfmiAsync(javob)).ShouldBeTrue();
            }

            (await SanaAsync(fabrika)).ShouldBe((nomzodlar, anketalar, fayllar), "ko'rinish nomzod, anketa yoki fayl yozmaydi");

            // ── Noto'g'ri javoblar — yuborishdagidek 400, maydon yo'li bilan
            var familiyasiz = JsonNode.Parse(minimal)!.AsObject();
            familiyasiz.Remove("lastName");
            using (var javob = await NamunaAsync(hr, familiyasiz.ToJsonString()))
            {
                javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
                using var xato = JsonDocument.Parse(await MatnAsync(javob));
                xato.RootElement.GetProperty("title").GetString().ShouldBe("anketa.majburiy");
                xato.RootElement.GetProperty("field").GetString().ShouldBe("lastName");
            }

            using (var javob = await NamunaAsync(hr, "{buzuq"))
            {
                javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
                (await MatnAsync(javob)).ShouldContain("anketa.json");
            }

            using (var javob = await NamunaAsync(hr, minimal, language: "de"))
            {
                javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
                using var xato = JsonDocument.Parse(await MatnAsync(javob));
                xato.RootElement.GetProperty("field").GetString().ShouldBe("language");
            }
        }

        private static async Task XatoAsync(HttpClient hr, HttpStatusCode holat, string kod, string? maydon = null)
        {
            using var javob = await hr.GetAsync(new Uri(PreviewPath, UriKind.Relative), Ct);
            javob.StatusCode.ShouldBe(holat, await MatnAsync(javob));

            using var xato = JsonDocument.Parse(await MatnAsync(javob));
            xato.RootElement.GetProperty("title").GetString().ShouldBe(kod);

            if (maydon is not null)
            {
                xato.RootElement.GetProperty("field").GetString().ShouldBe(maydon);
            }
        }

        private static async Task<(int Nomzodlar, int Anketalar, int Fayllar)> SanaAsync(WebApplicationFactory<Program> fabrika)
        {
            await using var qamrov = fabrika.Services.CreateAsyncScope();
            var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

            return (
                await db.Candidates.IgnoreQueryFilters().CountAsync(Ct),
                await db.CandidateForms.CountAsync(Ct),
                await db.Candidates.IgnoreQueryFilters().SelectMany(candidate => candidate.Files).CountAsync(Ct));
        }

        /// <summary>HRMentor ning <c>GET …/draft</c> javobi.</summary>
        private static string Qoralama(JsonObject taRif, int issues = 0)
            => new JsonObject
            {
                ["code"] = CandidateForm.HrF02,
                ["draft_revision"] = 3,
                ["updated_at"] = "2026-11-01T09:00:00Z",
                ["definition"] = JsonNode.Parse(taRif.ToJsonString()),
                ["issues"] = new JsonArray([.. Enumerable.Range(0, issues).Select(index => (JsonNode)new JsonObject
                {
                    ["path"] = $"sections[{index}]",
                    ["message"] = "Bo'lim bo'sh",
                })]),
            }.ToJsonString();

        private static async Task<HttpResponseMessage> NamunaAsync(HttpClient client, string answers, bool withFiles = false, string language = "uz-Latn")
        {
            using var forma = new MultipartFormDataContent
            {
                { new StringContent(answers), "answers" },
                { new StringContent(language), "language" },
            };

            if (withFiles)
            {
                var rasm = new ByteArrayContent(QuestionnaireSamples.Photo());
                rasm.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                forma.Add(rasm, "photo", "selfi.jpg");

                var imzo = new ByteArrayContent(QuestionnaireSamples.Signature());
                imzo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                forma.Add(imzo, "signature", "imzo.png");
            }

            return await client.PostAsync(new Uri(PreviewPath + "/pdf", UriKind.Relative), forma, Ct);
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    private static WebApplicationFactory<Program> Fabrika(BonviApiFactory factory, HrMentorFake hrMentor)
        => factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IHrMentorClient>(hrMentor)));

    /// <summary>Ulangan, v1 (urug', «4.0») sinxronlangan holat va HR administratori.</summary>
    private static async Task<HttpClient> AdminAsync(WebApplicationFactory<Program> fabrika, HrMentorFake hrMentor)
    {
        var admin = await MijozAsync(
            fabrika,
            Permissions.SettingsManage,
            Permissions.EmployeesView,
            Permissions.RecruitingView,
            Permissions.RecruitingManage);

        await UlaAsync(admin);

        hrMentor.PublishedDefinition = AnketaKonstruktoriSyncApiTests.Elon(1, AnketaKonstruktoriSyncApiTests.Tarif("4.0"));
        await SyncAsync(admin);

        return admin;
    }

    private static DateOnly Bugun(WebApplicationFactory<Program> fabrika) => fabrika.Services.GetRequiredService<IClock>().Today;

    private static async Task<HttpClient> MijozAsync(WebApplicationFactory<Program> factory, params string[] permissions)
    {
        var token = await TestAccounts.TokenAsync(factory, permissions);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    private static async Task UlaAsync(HttpClient admin)
    {
        using var javob = await admin.PutAsJsonAsync(
            new Uri("/api/settings", UriKind.Relative),
            new
            {
                items = new[]
                {
                    new { key = SystemSettingKeys.HrMentorBaseUrl, value = "https://hrmentor.test" },
                    new { key = SystemSettingKeys.HrMentorApiKey, value = "hrm_" + new string('k', 43) },
                },
            },
            Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
    }

    private static async Task SyncAsync(HttpClient admin)
    {
        using var javob = await admin.PostAsync(new Uri("/api/hrmentor/blanks/sync", UriKind.Relative), null, Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        using var natija = JsonDocument.Parse(await MatnAsync(javob));
        natija.RootElement.GetProperty("form").ValueKind.ShouldBe(JsonValueKind.Object, "e'lon qilingan versiya amalda");
    }

    /// <summary>Ochiq formaning mijozi — har test o'z IP'si bilan (yuborish chegarasi IP bo'yicha).</summary>
    private static HttpClient Mehmon(WebApplicationFactory<Program> fabrika)
    {
        var son = Interlocked.Increment(ref _ipSanogi);
        var client = fabrika.CreateClient();
        client.DefaultRequestHeaders.Add(ClientIp.HeaderName, string.Create(CultureInfo.InvariantCulture, $"198.18.{son / 250 % 250}.{(son % 250) + 1}"));

        return client;
    }

    private static async Task<int> YuborOkAsync(HttpClient client, string answers, string? definitionVersion)
    {
        using var javob = await YuborAsync(client, answers, definitionVersion);
        javob.StatusCode.ShouldBe(HttpStatusCode.Created, await MatnAsync(javob));

        using var natija = JsonDocument.Parse(await MatnAsync(javob));
        return natija.RootElement.GetProperty("formNumber").GetInt32();
    }

    private static async Task<HttpResponseMessage> YuborAsync(HttpClient client, string answers, string? definitionVersion)
    {
        using var forma = new MultipartFormDataContent
        {
            { new StringContent(answers), "answers" },
            { new StringContent(Guid.NewGuid().ToString()), "clientSubmissionId" },
            { new StringContent("uz-Latn"), "language" },
            { new StringContent(string.Empty), "website" },
        };

        if (definitionVersion is not null)
        {
            forma.Add(new StringContent(definitionVersion), "definitionVersion");
        }

        var rasm = new ByteArrayContent(QuestionnaireSamples.Photo());
        rasm.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        forma.Add(rasm, "photo", "selfi.jpg");

        var imzo = new ByteArrayContent(QuestionnaireSamples.Signature());
        imzo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        forma.Add(imzo, "signature", "imzo.png");

        return await client.PostAsync(new Uri(SubmitPath, UriKind.Relative), forma, Ct);
    }

    private static async Task<(CandidateForm Anketa, Candidate Nomzod)> AnketaAsync(AppDbContext db, int raqam)
    {
        var anketa = await db.CandidateForms.AsNoTracking().SingleAsync(form => form.Number == raqam, Ct);
        var nomzod = await db.Candidates.AsNoTracking()
            .Include(candidate => candidate.Files)
            .Include(candidate => candidate.Questionnaire)
            .SingleAsync(candidate => candidate.Id == anketa.CandidateId, Ct);

        return (anketa, nomzod);
    }

    private static async Task<Guid> VakansiyaAsync(HttpClient admin)
    {
        using var javob = await admin.PostAsJsonAsync(
            new Uri("/api/recruiting/vacancies", UriKind.Relative),
            new { title = "Konstruktor anketasi " + Guid.NewGuid().ToString("N")[..8], openings = 1, openedAt = new DateOnly(2026, 9, 1) },
            Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        using var hujjat = JsonDocument.Parse(await MatnAsync(javob));
        return hujjat.RootElement.GetProperty("id").GetGuid();
    }

    private static string Telefon()
        => "+99891"
           + Random.Shared.Next(10_000, 99_999).ToString(CultureInfo.InvariantCulture)
           + (Interlocked.Increment(ref _telefonSanogi) % 100).ToString("D2", CultureInfo.InvariantCulture);

    private static async Task<bool> PdfmiAsync(HttpResponseMessage javob)
    {
        var bytes = await javob.Content.ReadAsByteArrayAsync(Ct);
        return bytes.Length > 5 && System.Text.Encoding.ASCII.GetString(bytes, 0, 5) == "%PDF-";
    }

    private static Task<string> MatnAsync(HttpResponseMessage javob) => PublicApiHarness.MatnAsync(javob);
}
