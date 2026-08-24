using Bonvi.Application.Catalog.Common;
using Bonvi.Application.Catalog.Dtos;
using Bonvi.Application.Catalog.Positions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Organization;
using NSubstitute;

namespace Bonvi.Application.Tests.Catalog;

/// <summary>
/// Lavozimlar va ularning vazifalari bilan bog'liq testlar.
/// </summary>
/// <remarks>
/// Vazifalar HR'ning aniq talabi: «lavozimni tanlaganda avtomatik vazifasi
/// ham yuklanadi». Shu sababli ular lavozimning ajralmas qismi sifatida
/// tekshiriladi.
/// </remarks>
public sealed class PositionCatalogTests
{
    private readonly IPositionRepository _positions = Substitute.For<IPositionRepository>();
    private readonly IDepartmentRepository _departments = Substitute.For<IDepartmentRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly SaveReferenceCommandValidator _validator = new();
    private readonly PositionDefinition _definition = new();

    private CreatePositionCommandHandler Yaratuvchi
        => new(_positions, _departments, _definition, _validator, _unitOfWork);

    private GetPositionDutiesQueryHandler Vazifalar => new(_positions);

    private AddPositionDutyCommandHandler VazifaQoshuvchi => new(_positions, _unitOfWork);

    private ReorderPositionDutiesCommandHandler VazifaTartiblovchi => new(_positions, _unitOfWork);

    private static Position VazifaliLavozim()
    {
        var position = Position.Create("Qorovul").Value;
        position.AddDuty("Hududni kuzatish");
        position.AddDuty("Kirish-chiqishni qayd etish");
        return position;
    }

    // ────────── Validatsiya ──────────

    [Fact]
    public async Task Notogri_daraja_rad_etiladi()
    {
        var natija = await Yaratuvchi.HandleAsync(
            new SaveReferenceCommand("Qorovul", Grade: 6),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("lavozim.daraja");
    }

    [Fact]
    public async Task Haddan_uzun_sinov_muddati_rad_etiladi()
    {
        // 180 kun — mehnat kodeksi chegarasi
        var natija = await Yaratuvchi.HandleAsync(
            new SaveReferenceCommand("Qorovul", ProbationDays: 200),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("lavozim.sinov_muddati");
    }

    [Fact]
    public async Task Mavjud_bolmagan_bolim_bilan_lavozim_yaratib_bolmaydi()
    {
        // Aks holda xato tashqi kalit cheklovidan kelib chiqib, 500 bo'lardi
        _departments.ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        var natija = await Yaratuvchi.HandleAsync(
            new SaveReferenceCommand("Qorovul", DepartmentId: Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("bolim.topilmadi");
    }

    [Fact]
    public async Task Lavozim_darajasi_va_sinov_muddati_bilan_yaratiladi()
    {
        _positions
            .NameExistsAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var natija = await Yaratuvchi.HandleAsync(
            new SaveReferenceCommand("Bo'lim boshlig'i", Grade: 3, ProbationDays: 90),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Message : null);
        natija.Value.Grade.ShouldBe(3);
        natija.Value.ProbationDays.ShouldBe(90);
        natija.Value.Duties.ShouldBeEmpty();
    }

    // ────────── Vazifalar ──────────

    [Fact]
    public async Task Lavozim_vazifalari_tartib_boyicha_qaytadi()
    {
        var position = VazifaliLavozim();
        _positions.GetWithDutiesAsync(position.Id, Arg.Any<CancellationToken>()).Returns(position);

        var natija = await Vazifalar.HandleAsync(
            new GetPositionDutiesQuery(position.Id),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.Count.ShouldBe(2);
        natija.Value[0].Text.ShouldBe("Hududni kuzatish");
        natija.Value[0].SortOrder.ShouldBe(0);
        natija.Value[1].SortOrder.ShouldBe(1);
    }

    [Fact]
    public async Task Mavjud_bolmagan_lavozimning_vazifalari_404()
    {
        _positions
            .GetWithDutiesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Position?)null);

        var natija = await Vazifalar.HandleAsync(
            new GetPositionDutiesQuery(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("lavozim.topilmadi");
    }

    [Fact]
    public async Task Takroriy_vazifa_qoshib_bolmaydi()
    {
        // «Ҳудудни кузатиш» va «Ҳудудни  кузатиш» — bitta vazifa
        var position = VazifaliLavozim();
        _positions.GetWithDutiesAsync(position.Id, Arg.Any<CancellationToken>()).Returns(position);

        var natija = await VazifaQoshuvchi.HandleAsync(
            new AddPositionDutyCommand(position.Id, "  hududni   kuzatish "),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("lavozim.vazifa_takror");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Vazifa_qoshilgach_toliq_royxat_qaytadi()
    {
        var position = VazifaliLavozim();
        _positions.GetWithDutiesAsync(position.Id, Arg.Any<CancellationToken>()).Returns(position);

        var natija = await VazifaQoshuvchi.HandleAsync(
            new AddPositionDutyCommand(position.Id, "Smena jurnalini to'ldirish"),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Message : null);

        // Interfeys ro'yxatni qayta so'ramasligi uchun to'liq holat qaytariladi
        natija.Value.Count.ShouldBe(3);
        natija.Value[2].Text.ShouldBe("Smena jurnalini to'ldirish");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Vazifalar_tartibi_ozgartiriladi()
    {
        var position = VazifaliLavozim();
        _positions.GetWithDutiesAsync(position.Id, Arg.Any<CancellationToken>()).Returns(position);

        IReadOnlyList<Guid> teskari =
        [
            .. position.Duties.OrderByDescending(duty => duty.SortOrder).Select(duty => duty.Id),
        ];

        var natija = await VazifaTartiblovchi.HandleAsync(
            new ReorderPositionDutiesCommand(position.Id, teskari),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Message : null);
        natija.Value[0].Text.ShouldBe("Kirish-chiqishni qayd etish");
        natija.Value[1].Text.ShouldBe("Hududni kuzatish");
    }
}
