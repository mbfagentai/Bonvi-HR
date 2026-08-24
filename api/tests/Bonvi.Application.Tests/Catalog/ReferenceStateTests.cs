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
    private readonly ReorderReferenceCommandValidator _validator = new();

    private DeactivateReferenceCommandHandler<TerminationReason> Faolsizlantiruvchi
        => new(_repository, _unitOfWork);

    private ActivateReferenceCommandHandler<TerminationReason> Faollashtiruvchi
        => new(_repository, _unitOfWork);

    private ReorderReferenceCommandHandler<TerminationReason> Tartiblovchi
        => new(_repository, _validator, _unitOfWork);

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
    public async Task Faqat_orni_ozgargan_yozuvlar_yuklanadi()
    {
        // `ListAsync` kuzatuvsiz qaytaradi — o'zgartirish uchun yozuv
        // qayta, kuzatiladigan holda yuklanishi kerak. Lekin faqat
        // haqiqatan siljiganlari.
        var birinchi = TerminationReason.Create("Birinchi").Value;
        birinchi.SetSortOrder(0);

        var ikkinchi = TerminationReason.Create("Ikkinchi").Value;
        ikkinchi.SetSortOrder(1);

        _repository
            .ListAsync(false, Arg.Any<CancellationToken>())
            .Returns(new List<TerminationReason> { birinchi, ikkinchi });

        _repository.GetByIdAsync(birinchi.Id, Arg.Any<CancellationToken>()).Returns(birinchi);
        _repository.GetByIdAsync(ikkinchi.Id, Arg.Any<CancellationToken>()).Returns(ikkinchi);

        var natija = await Tartiblovchi.HandleAsync(
            new ReorderReferenceCommand([ikkinchi.Id, birinchi.Id]),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Message : null);
        ikkinchi.SortOrder.ShouldBe(0);
        birinchi.SortOrder.ShouldBe(1);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Takroriy_identifikator_rad_etiladi()
    {
        var id = Guid.NewGuid();

        var natija = await Tartiblovchi.HandleAsync(
            new ReorderReferenceCommand([id, id]),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("malumotnoma.tartib_takror");
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
