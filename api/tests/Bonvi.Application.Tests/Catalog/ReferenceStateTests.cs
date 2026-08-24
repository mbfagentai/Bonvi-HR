using Bonvi.Application.Catalog.Commands;
using Bonvi.Application.Catalog.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Catalog;
using NSubstitute;

namespace Bonvi.Application.Tests.Catalog;

/// <summary>
/// Faollik holati va tartib amallarining testlari.
/// </summary>
public sealed class ReferenceStateTests
{
    private readonly IReferenceRepository<TerminationReason> _repository =
        Substitute.For<IReferenceRepository<TerminationReason>>();

    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private DeactivateReferenceCommandHandler<TerminationReason> Faolsizlantiruvchi
        => new(_repository, _unitOfWork);

    private ActivateReferenceCommandHandler<TerminationReason> Faollashtiruvchi
        => new(_repository, _unitOfWork);
    [Fact]
    public async Task Faolsizlantirish_yozuvni_ochirmaydi_sababni_saqlaydi()
    {
        var sabab = TerminationReason.Create("Ish yoqmadi").Value;
        _repository.GetByIdAsync(sabab.Id, Arg.Any<CancellationToken>()).Returns(sabab);

        var natija = await Faolsizlantiruvchi.HandleAsync(
            new DeactivateReferenceCommand(sabab.Id, "Excel'dan kelgan takroriy qiymat"),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        sabab.IsActive.ShouldBeFalse();
        sabab.Note.ShouldBe("Excel'dan kelgan takroriy qiymat");

        // Yozuv o'chirilmasligi kerak: eski buyruqlarda unga havola bor
        _repository.DidNotReceive().Remove(Arg.Any<TerminationReason>());
    }

    [Fact]
    public async Task Faollashtirish_yozuvni_qaytaradi()
    {
        var sabab = TerminationReason.Create("O'z xohishi bilan").Value;
        sabab.Deactivate("xato bosildi");
        _repository.GetByIdAsync(sabab.Id, Arg.Any<CancellationToken>()).Returns(sabab);

        var natija = await Faollashtiruvchi.HandleAsync(
            new ActivateReferenceCommand(sabab.Id),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        sabab.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Mavjud_bolmagan_yozuvni_faolsizlantirib_bolmaydi()
    {
        _repository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((TerminationReason?)null);

        var natija = await Faolsizlantiruvchi.HandleAsync(
            new DeactivateReferenceCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("malumotnoma.topilmadi");
    }
}
