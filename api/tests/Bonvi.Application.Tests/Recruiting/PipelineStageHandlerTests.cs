using Bonvi.Application.Recruiting.Stages;
using Bonvi.Domain.Recruiting;
using NSubstitute;

namespace Bonvi.Application.Tests.Recruiting;

/// <summary>
/// Voronka bosqichlari — tartib va uni o'zgartirish.
/// </summary>
/// <remarks>
/// Voronkada TARTIB — ma'lumotning o'zi, shu sababli u eng ko'p
/// tekshiriladigan xatti-harakat: alifbo bo'yicha chizilgan voronka
/// («Job Offer», «Кенгаш», «Телефон суҳбат») ma'nosini butunlay yo'qotadi.
/// </remarks>
public sealed class PipelineStageHandlerTests
{
    private readonly RecruitingTestContext _context = new();

    private ListPipelineStagesQueryHandler Royxat => new(_context.Stages);

    private CreatePipelineStageCommandHandler Qoshish => new(_context.Stages, _context.UnitOfWork);

    private UpdatePipelineStageCommandHandler Tahrirlash => new(_context.Stages, _context.UnitOfWork);

    private ReorderPipelineStagesCommandHandler Tartiblash => new(_context.Stages, _context.UnitOfWork);

    private SetPipelineStageActiveCommandHandler Holat => new(_context.Stages, _context.UnitOfWork);

    [Fact]
    public async Task Royxat_alifbo_emas_VORONKA_tartibida_qaytadi()
    {
        var anketa = _context.ExistingStage("Анкета", 0);
        var kengash = _context.ExistingStage("Кенгаш", 3);
        var telefon = _context.ExistingStage("Телефон суҳбат", 1);
        _context.Funnel(kengash, telefon, anketa);

        var natija = await Royxat.HandleAsync(
            new ListPipelineStagesQuery(), TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.Select(bosqich => bosqich.Name).ShouldBe(["Анкета", "Телефон суҳбат", "Кенгаш"]);
    }

    [Fact]
    public async Task Yangi_bosqich_voronkaning_OXIRIGA_qoshiladi()
    {
        _context.Stages.MaxSortOrderAsync(Arg.Any<CancellationToken>()).Returns(4);
        _context.Stages.NameExistsAsync("Синов", null, Arg.Any<CancellationToken>()).Returns(false);

        var natija = await Qoshish.HandleAsync(
            new CreatePipelineStageCommand("Синов"), TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.SortOrder.ShouldBe(5, "eng katta tartib + 1");
        _context.Stages.Received(1).Add(Arg.Any<PipelineStage>());
    }

    [Fact]
    public async Task Bosh_voronkada_birinchi_bosqich_nolinchi_boladi()
    {
        _context.Stages.MaxSortOrderAsync(Arg.Any<CancellationToken>()).Returns((int?)null);

        var natija = await Qoshish.HandleAsync(
            new CreatePipelineStageCommand("Анкета"), TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.SortOrder.ShouldBe(0);
    }

    [Fact]
    public async Task Takroriy_nom_qabul_qilinmaydi()
    {
        _context.Stages.NameExistsAsync("Кенгаш", null, Arg.Any<CancellationToken>()).Returns(true);

        var natija = await Qoshish.HandleAsync(
            new CreatePipelineStageCommand("Кенгаш"), TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("bosqich.nom_band");
    }

    [Fact]
    public async Task Nomavjud_bosqich_tahrirlanmaydi()
    {
        var natija = await Tahrirlash.HandleAsync(
            new UpdatePipelineStageCommand(Guid.CreateVersion7(), "Кенгаш"),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("bosqich.topilmadi");
    }

    [Fact]
    public async Task Qayta_tartiblash_nolladan_qayta_raqamlaydi()
    {
        var anketa = _context.ExistingStage("Анкета", 0);
        var telefon = _context.ExistingStage("Телефон суҳбат", 1);
        var kengash = _context.ExistingStage("Кенгаш", 2);
        _context.Funnel(anketa, telefon, kengash);

        var natija = await Tartiblash.HandleAsync(
            new ReorderPipelineStagesCommand([kengash.Id, anketa.Id, telefon.Id]),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        kengash.SortOrder.ShouldBe(0);
        anketa.SortOrder.ShouldBe(1);
        telefon.SortOrder.ShouldBe(2);
        natija.Value.Select(bosqich => bosqich.Name).ShouldBe(["Кенгаш", "Анкета", "Телефон суҳбат"]);
    }

    [Fact]
    public async Task Yarim_royxat_bilan_tartiblanmaydi()
    {
        // Aks holda qolgan bosqichlar eski raqamlari bilan qolib, voronkada
        // ikkita bosqich bir o'rinda turib qolardi — va bu hech qanday
        // xato bermasdi.
        var anketa = _context.ExistingStage("Анкета", 0);
        var telefon = _context.ExistingStage("Телефон суҳбат", 1);
        _context.Funnel(anketa, telefon);

        var natija = await Tartiblash.HandleAsync(
            new ReorderPipelineStagesCommand([telefon.Id]),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("bosqich.tartib_royxati");
        anketa.SortOrder.ShouldBe(0, "rad etilgan amal hech narsani o'zgartirmaydi");
    }

    [Fact]
    public async Task Takrorlangan_identifikator_bilan_tartiblanmaydi()
    {
        var anketa = _context.ExistingStage("Анкета", 0);
        var telefon = _context.ExistingStage("Телефон суҳбат", 1);
        _context.Funnel(anketa, telefon);

        var natija = await Tartiblash.HandleAsync(
            new ReorderPipelineStagesCommand([anketa.Id, anketa.Id]),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("bosqich.tartib_royxati");
    }

    [Fact]
    public async Task Bosqich_faolsizlantiriladi_va_qayta_yoqiladi()
    {
        var kengash = _context.ExistingStage("Кенгаш", 3);

        var ochirish = await Holat.HandleAsync(
            new SetPipelineStageActiveCommand(kengash.Id, IsActive: false, "Кенгаш бекор қилинди"),
            TestContext.Current.CancellationToken);

        ochirish.IsSuccess.ShouldBeTrue();
        ochirish.Value.IsActive.ShouldBeFalse();
        ochirish.Value.SortOrder.ShouldBe(3, "faolsizlantirish voronka tartibini buzmaydi");

        var yoqish = await Holat.HandleAsync(
            new SetPipelineStageActiveCommand(kengash.Id, IsActive: true),
            TestContext.Current.CancellationToken);

        yoqish.IsSuccess.ShouldBeTrue();
        yoqish.Value.IsActive.ShouldBeTrue();
    }
}
