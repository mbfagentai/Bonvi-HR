using System.Text.Json.Nodes;
using Bonvi.Application.Recruiting.Forms;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Blanka 3.1 (HR rahbari, egasi tasdiqlagan, 2026-10-27): 5-bo'limda «Avval ishlaganmisiz?»,
/// umumiy tajriba qatori yo'q, ishga chiqish — to'rt variant, kredit — majburiy savol. Bazadagi
/// 3.0 anketasi esa eski javoblari va o'z versiyasi bilan chiziladi.
/// </summary>
public sealed partial class QuestionnairePdfRendererTests
{
    private const string StartChoicesTenDays =
        "☐ Qabul qilingan kundan ☐ Qabul qilingandan 1 hafta keyin ☑ Qabul qilingandan 10 kun keyin ☐ Qabul qilingandan 15 kun keyin";

    [Fact]
    public async Task Avval_ishlagan_ishga_chiqish_tanlovi_va_kredit_tolovi_chiziladi()
    {
        var matn = Matn(await _renderer.RenderAsync(Model(QuestionnaireSamples.Full(Bugun), photo: null, signature: null), TestContext.Current.CancellationToken));

        matn.ShouldContain("5. MEHNAT FAOLIYATI Avval ishlaganmisiz? ☑ Ha ☐ Yo‘q", Case.Sensitive, "tanlov bo'lim boshida");
        matn.ShouldContain("«Metan Gaz» MChJ");
        matn.ShouldContain("Karimov Anvar");
        matn.ShouldContain(StartChoicesTenDays, Case.Sensitive);
        matn.ShouldContain("☐ Yo‘q ☑ Ha, oylik to‘lov: 1 200 000 so‘m", Case.Sensitive);
        matn.ShouldContain("Brak ulushini 3 oyda ikki baravar kamaytirdik.");

        matn.ShouldNotContain("Umumiy ish tajribangiz", Case.Sensitive, "3.1 da bu qator yo'q");
        matn.ShouldNotContain("«____» ____________ 20___ y.", Case.Sensitive, "sana chizig'i o'rniga tanlov");
    }

    [Fact]
    public async Task Avval_ishlamagan_jadvallar_bosh_ishga_kundan_kredit_yoq()
    {
        var matn = Matn(await _renderer.RenderAsync(Model(QuestionnaireSamples.Minimal(Bugun), photo: null, signature: null), TestContext.Current.CancellationToken));

        matn.ShouldContain("Avval ishlaganmisiz? ☐ Ha ☑ Yo‘q", Case.Sensitive);
        matn.ShouldContain(
            "☑ Qabul qilingan kundan ☐ Qabul qilingandan 1 hafta keyin ☐ Qabul qilingandan 10 kun keyin ☐ Qabul qilingandan 15 kun keyin",
            Case.Sensitive);
        matn.ShouldContain("☑ Yo‘q ☐ Ha, oylik to‘lov: ________ so‘m", Case.Sensitive);

        // Jadvallar qog'ozdagidek bo'sh qatorlar bilan qoladi (ish joylari 4, tavsiya beruvchilar 2)
        matn.ShouldContain("Ishdan ketish sababi 1 2 3 4 6. TAVSIYA BERUVCHILAR", Case.Sensitive);
        matn.ShouldContain("Telefon raqami 1 2 7. OILA A’ZOLARI", Case.Sensitive);
    }

    /// <summary>
    /// 3.0 da saqlangan anketa (tanlov yo'q, aniq sana bor; umumiy tajriba bor) — HR «PDF» ni bosganda
    /// sana avvalgidek chiqadi, sarlavha esa «v3.0» bo'lib qoladi. Javoblar tekshiruvchidan
    /// o'tmaydi — <c>CandidateFormQueries</c> dagidek bazadan to'g'ridan-to'g'ri o'qiladi.
    /// </summary>
    [Fact]
    public async Task Eski_3_0_anketasi_sanasi_va_oz_versiyasi_bilan_chiziladi()
    {
        var eski = JsonNode.Parse(QuestionnaireFormJson.Write(
            QuestionnaireFormValidator.Validate(QuestionnaireSamples.Full(Bugun), Bugun, ReferralSeeds.Options).Value))!.AsObject();

        eski.Remove("hasWorkExperience");
        eski.Remove("startAfter");
        eski["availableFrom"] = "2026-11-03";
        eski["totalExperienceYears"] = 5;
        eski["totalExperienceMonths"] = 3;

        var javoblar = QuestionnaireFormJson.Read(eski.ToJsonString());
        javoblar.ShouldNotBeNull("bazadagi 3.0 yozuvi o'qiladi");
        javoblar.AvailableFrom.ShouldBe(new DateOnly(2026, 11, 3));

        var model = Model(QuestionnaireSamples.Full(Bugun), photo: null, signature: null) with
        {
            Answers = javoblar,
            FormVersion = "3.0",
        };

        var matn = Matn(await _renderer.RenderAsync(model, TestContext.Current.CancellationToken));

        matn.ShouldContain("HR-F02 · Nomzod anketasi · v3.0");
        matn.ShouldContain("Blanka HR-F02 · Versiya 3.0");
        matn.ShouldContain("Ishga qachondan chiqa olasiz? 03.11.2026", Case.Sensitive);
        matn.ShouldNotContain("Qabul qilingan kundan", Case.Sensitive, "tanlov yo'q — sana chiqadi");
        matn.ShouldContain("Avval ishlaganmisiz? ☐ Ha ☐ Yo‘q", Case.Sensitive, "3.0 da bu savol bo'lmagan");
        matn.ShouldNotContain("Umumiy ish tajribangiz", Case.Sensitive);
    }
}
