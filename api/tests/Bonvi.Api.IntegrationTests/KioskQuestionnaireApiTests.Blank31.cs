using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bonvi.Application.Recruiting.Forms;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Blanka 3.1 (HR rahbari, egasi tasdiqlagan, 2026-10-27): «Avval ishlaganmisiz?», ishga chiqish
/// tanlovi va majburiy kredit savoli — API orqali.
/// </summary>
public sealed partial class KioskQuestionnaireApiTests
{
    /// <summary>
    /// «Yo'q» — ish joyi va tavsiya beruvchisiz anketa qabul qilinadi; formada qolib ketgan qatorlar
    /// va ishga oid ikki javob bazaga TUSHMAYDI, HR kartasida esa «Avval ishlamagan».
    /// </summary>
    [Fact]
    public async Task Avval_ishlamagan_anketa_qabul_qilinadi_ish_qatorlari_saqlanmaydi()
    {
        using var mehmon = Mehmon();

        var javoblar = QuestionnaireSamples.Minimal(Bugun, Telefon()) with
        {
            Jobs = [new FormJob("Qoralamadagi tashkilot", "Sotuvchi", "2024-01", null, true)],
            References = [new FormReference("Qoralamadagi tavsiyachi")],
            LastJobAchievement = "Qoralamadagi yutuq",
            BestIdea = "Qoralamadagi g‘oya",
        };

        using var javob = await YuborAsync(mehmon, javoblar, Guid.NewGuid());
        javob.StatusCode.ShouldBe(HttpStatusCode.Created, await MatnAsync(javob));
        var natija = await javob.Content.ReadFromJsonAsync<KioskSubmittedResponse>(Ct);

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var anketa = await db.CandidateForms.AsNoTracking().SingleAsync(form => form.Number == natija!.FormNumber, Ct);
        var nomzod = await db.Candidates.AsNoTracking()
            .Include(candidate => candidate.Questionnaire)
            .SingleAsync(candidate => candidate.Id == anketa.CandidateId, Ct);

        anketa.FormVersion.ShouldBe("3.1");
        anketa.Answers.ShouldNotContain("Qoralamadagi");

        using var hujjat = JsonDocument.Parse(anketa.Answers);
        hujjat.RootElement.GetProperty("hasWorkExperience").GetBoolean().ShouldBeFalse();
        hujjat.RootElement.GetProperty("jobs").GetArrayLength().ShouldBe(0);
        hujjat.RootElement.GetProperty("references").GetArrayLength().ShouldBe(0);
        hujjat.RootElement.TryGetProperty("lastJobAchievement", out _).ShouldBeFalse();
        hujjat.RootElement.TryGetProperty("bestIdea", out _).ShouldBeFalse();
        hujjat.RootElement.GetProperty("startAfter").GetString().ShouldBe("OnHireDay");
        hujjat.RootElement.GetProperty("hasLoan").GetBoolean().ShouldBeFalse();
        hujjat.RootElement.TryGetProperty("loanMonthlyPayment", out _).ShouldBeFalse();
        hujjat.RootElement.GetProperty("motivators").GetString().ShouldBe(javoblar.Motivators);
        hujjat.RootElement.GetProperty("successDefinition").GetString().ShouldBe(javoblar.SuccessDefinition);

        nomzod.Questionnaire.ShouldNotBeNull();
        nomzod.Questionnaire.WorkExperience.ShouldBe("Avval ishlamagan");

        // HR «PDF» tugmasi — saqlangan javoblardan qayta chiziladi
        using var korish = await HrAsync(Permissions.RecruitingView);
        using var pdf = await korish.GetAsync(new Uri($"/api/recruiting/candidates/{nomzod.Id}/forms/{anketa.Id}/pdf", UriKind.Relative), Ct);
        pdf.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(pdf));
        (await PdfmiAsync(pdf)).ShouldBeTrue();
    }

    /// <summary>
    /// Bazadagi 3.0 anketasi (aniq sana va umumiy tajriba bilan, «avval ishlaganmisiz?» va tanlovsiz) —
    /// HR ro'yxatida «3.0», PDF esa sanasi va «v3.0» sarlavhasi bilan qayta chiziladi.
    /// </summary>
    [Fact]
    public async Task Eski_3_0_anketasi_HR_PDFida_sanasi_va_oz_versiyasi_bilan()
    {
        using var mehmon = Mehmon();

        using var javob = await YuborAsync(mehmon, QuestionnaireSamples.Full(Bugun, Telefon()), Guid.NewGuid());
        javob.StatusCode.ShouldBe(HttpStatusCode.Created, await MatnAsync(javob));
        var natija = await javob.Content.ReadFromJsonAsync<KioskSubmittedResponse>(Ct);

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
        var anketa = await db.CandidateForms.AsNoTracking().SingleAsync(form => form.Number == natija!.FormNumber, Ct);

        // 3.0 ko'rinishiga qaytaramiz — o'sha kuni yuborilgan anketa bazada shunday turadi
        var eski = JsonNode.Parse(anketa.Answers)!.AsObject();
        eski.Remove("hasWorkExperience");
        eski.Remove("startAfter");
        eski["availableFrom"] = "2026-11-03";
        eski["totalExperienceYears"] = 5;
        eski["totalExperienceMonths"] = 3;
        var eskiJson = eski.ToJsonString();

        await db.Database.ExecuteSqlAsync(
            $"UPDATE candidate_forms SET form_version = '3.0', answers = {eskiJson}::jsonb WHERE id = {anketa.Id}",
            Ct);

        using var korish = await HrAsync(Permissions.RecruitingView);

        var qatorlar = await korish.GetFromJsonAsync<List<CandidateFormResponse>>(
            new Uri($"/api/recruiting/candidates/{anketa.CandidateId}/forms", UriKind.Relative), Ct);
        qatorlar.ShouldNotBeNull().ShouldHaveSingleItem().FormVersion.ShouldBe("3.0");

        using var pdf = await korish.GetAsync(new Uri($"/api/recruiting/candidates/{anketa.CandidateId}/forms/{anketa.Id}/pdf", UriKind.Relative), Ct);
        pdf.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(pdf));

        var matn = QuestionnairePdfRendererTests.Matn(await pdf.Content.ReadAsByteArrayAsync(Ct));
        matn.ShouldContain("HR-F02 · Nomzod anketasi · v3.0");
        matn.ShouldContain("Ishga qachondan chiqa olasiz? 03.11.2026", Case.Sensitive);
        matn.ShouldNotContain("Umumiy ish tajribangiz", Case.Sensitive);
    }

    /// <summary>
    /// 3.1 ning majburiy javoblari — xato kod va maydon yo'li bilan, hech narsa yozilmaydi.
    /// </summary>
    [Fact]
    public async Task Blanka_3_1_majburiy_javoblari_maydon_yoli_bilan_400()
    {
        using var mehmon = Mehmon();

        foreach (var (nom, javoblar, kod, maydon) in new[]
                 {
                     ("ishlaganmi — javobsiz", QuestionnaireSamples.Minimal(Bugun, Telefon()) with { HasWorkExperience = null }, "anketa.majburiy", "hasWorkExperience"),
                     ("ishlagan, ish joyisiz", QuestionnaireSamples.Full(Bugun, Telefon()) with { Jobs = [] }, "anketa.majburiy", "jobs"),
                     ("ishlagan, tavsiyasiz", QuestionnaireSamples.Full(Bugun, Telefon()) with { References = [] }, "anketa.majburiy", "references"),
                     ("ishlagan, yutuqsiz", QuestionnaireSamples.Full(Bugun, Telefon()) with { LastJobAchievement = null }, "anketa.majburiy", "lastJobAchievement"),
                     ("ishga chiqish tanlanmagan", QuestionnaireSamples.Minimal(Bugun, Telefon()) with { StartAfter = null }, "anketa.majburiy", "startAfter"),
                     ("kredit javobsiz", QuestionnaireSamples.Minimal(Bugun, Telefon()) with { HasLoan = null }, "anketa.majburiy", "hasLoan"),
                     ("kredit bor, to'lovsiz", QuestionnaireSamples.Minimal(Bugun, Telefon()) with { HasLoan = true }, "anketa.majburiy", "loanMonthlyPayment"),
                     ("BE toifasi", QuestionnaireSamples.Full(Bugun, Telefon()) with { DriverLicenseCategories = ["BE"] }, "anketa.notogri", "driverLicenseCategories"),
                 })
        {
            var yuborish = Guid.NewGuid();

            using (var javob = await YuborAsync(mehmon, javoblar, yuborish))
            {
                javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest, nom);

                using var xato = JsonDocument.Parse(await MatnAsync(javob));
                xato.RootElement.GetProperty("title").GetString().ShouldBe(kod, nom);
                xato.RootElement.GetProperty("detail").GetString().ShouldBe(maydon, nom);
                xato.RootElement.GetProperty("field").GetString().ShouldBe(maydon, nom);
            }

            await using var qamrov = _factory.Services.CreateAsyncScope();
            var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

            (await db.CandidateForms.CountAsync(form => form.ClientSubmissionId == yuborish, Ct)).ShouldBe(0, nom);
            (await db.Candidates.CountAsync(candidate => candidate.Phone == PhoneNumber.FromTrusted(javoblar.Phone!), Ct)).ShouldBe(0, nom);
        }
    }

    /// <summary>
    /// 3.1 chiqqanda brauzerda OCHIQ qolgan 3.0 sahifa (ofis plansheti) — u yangi savollarni bilmaydi va
    /// aniq sana yuboradi. Anketa 3.0 qoidasi bilan qabul qilinadi va «3.0» bo'lib yoziladi (egasi:
    /// «serverga chiqarishda uzilish bo'lmasin»).
    /// </summary>
    [Fact]
    public async Task Ochiq_qolgan_3_0_sahifadan_kelgan_anketa_qabul_qilinadi()
    {
        using var mehmon = Mehmon();

        var eski = QuestionnaireSamples.Full(Bugun, Telefon()) with
        {
            HasWorkExperience = null,
            StartAfter = null,
            AvailableFrom = Bugun.AddDays(3),
            References = [],
            HasLoan = null,
            LoanMonthlyPayment = null,
            Motivators = null,
            SuccessDefinition = null,
            LastJobAchievement = null,
            BestIdea = null,
            DriverLicenseCategories = ["BE", "B"],
        };

        using var javob = await YuborAsync(mehmon, eski, Guid.NewGuid());
        javob.StatusCode.ShouldBe(HttpStatusCode.Created, await MatnAsync(javob));
        var natija = await javob.Content.ReadFromJsonAsync<KioskSubmittedResponse>(Ct);

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var anketa = await db.CandidateForms.AsNoTracking().SingleAsync(form => form.Number == natija!.FormNumber, Ct);
        anketa.FormVersion.ShouldBe("3.0");

        using var hujjat = JsonDocument.Parse(anketa.Answers);
        hujjat.RootElement.GetProperty("availableFrom").GetString().ShouldBe(Bugun.AddDays(3).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        hujjat.RootElement.TryGetProperty("startAfter", out _).ShouldBeFalse();
        hujjat.RootElement.TryGetProperty("hasWorkExperience", out _).ShouldBeFalse();
        hujjat.RootElement.GetProperty("jobs").GetArrayLength().ShouldBe(2);
        hujjat.RootElement.GetProperty("driverLicenseCategories").EnumerateArray()
            .Select(category => category.GetString()).ShouldBe(["B", "BE"]);

        // Sana o'tib ketgan bo'lsa — o'sha 3.0 qoidasi, eski sahifa bu maydonni taniydi
        using var otgan = await YuborAsync(mehmon, eski with { Phone = Telefon(), AvailableFrom = Bugun.AddDays(-1) }, Guid.NewGuid());
        otgan.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using var xato = JsonDocument.Parse(await MatnAsync(otgan));
        xato.RootElement.GetProperty("field").GetString().ShouldBe("availableFrom");
    }
}
