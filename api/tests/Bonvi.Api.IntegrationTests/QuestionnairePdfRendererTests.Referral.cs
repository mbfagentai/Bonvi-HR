using Bonvi.Application.Recruiting.Forms;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// 8-bo'limning 1-savoli — «Bu vakansiya haqida qayerdan bildingiz?» variantlari «Qayerdan bilgan»
/// ma'lumotnomasidan (SPEC-qayerdan-bilgan §5).
/// </summary>
public sealed partial class QuestionnairePdfRendererTests
{
    [Fact]
    public async Task Qayerdan_bildingiz_hamma_faol_kanal_va_tanlangani_aniqligi_bilan()
    {
        var javoblar = QuestionnaireSamples.Full(Bugun);
        var pdf = await _renderer.RenderAsync(Model(javoblar, photo: null, signature: null), TestContext.Current.CancellationToken);
        var matn = Matn(pdf);

        matn.ShouldContain("☑ Tanishlar orqali, kim/qaysi: Karimov Anvar (sex boshlig‘i)", Case.Sensitive);

        foreach (var kanal in ReferralSeeds.All.Where(kanal => kanal.Id != ReferralSeeds.Acquaintance))
        {
            matn.ShouldContain("☐ " + kanal.Name, Case.Sensitive, kanal.Name);
        }

        // Eski qattiq ro'yxatning matni yo'q — variantlar endi ma'lumotnomadan
        matn.ShouldNotContain("Tanish / xodim tavsiyasi", Case.Sensitive);
    }

    [Fact]
    public async Task Qayerdan_bildingiz_faolsizlantirilgan_tanlov_ham_chiziladi()
    {
        var javoblar = QuestionnaireSamples.Minimal(Bugun);

        // Chizish paytidagi ro'yxat: tanlangan kanal faolsizlantirilgan — u baribir ☑ bo'lib turadi
        IReadOnlyList<QuestionnairePdfChoice> variantlar =
        [
            .. ReferralSeeds.PdfChoices(chosen: null),
            new QuestionnairePdfChoice("Gazeta", Chosen: true),
        ];

        var model = Model(javoblar with { ReferralChannelId = ReferralSeeds.Telegram }, photo: null, signature: null) with
        {
            ReferralChoices = variantlar,
        };

        var matn = Matn(await _renderer.RenderAsync(model, TestContext.Current.CancellationToken));

        matn.ShouldContain("☑ Gazeta", Case.Sensitive);
        matn.ShouldContain("☐ Telegram", Case.Sensitive);
    }
}
