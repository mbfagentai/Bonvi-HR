using Bonvi.Application.Employees.Incentives;
using Bonvi.Domain.Catalog;
using Bonvi.Domain.Employees;
using NSubstitute;

namespace Bonvi.Application.Tests.Employees;

/// <summary>Rag'bat/jarima berish, tuzatish va o'chirish — ulanish qatlami.</summary>
public sealed class IncentiveHandlerTests
{
    private readonly EmployeeTestContext _context = new();

    private GrantIncentiveCommandHandler Berish => new(
        _context.Incentives,
        _context.Employees,
        _context.Types,
        _context.Mapper,
        _context.UnitOfWork);

    private UpdateIncentiveCommandHandler Tuzatish => new(
        _context.Incentives,
        _context.Types,
        _context.Mapper,
        _context.UnitOfWork);

    private DeleteIncentiveCommandHandler Ochirish => new(_context.Incentives, _context.UnitOfWork);

    // ────────────────────── Berish ──────────────────────

    [Fact]
    public async Task Nomavjud_xodimga_ragbat_berilmaydi()
    {
        var turi = _context.ExistingAwardType();

        var natija = await Berish.HandleAsync(
            new GrantIncentiveCommand(Guid.CreateVersion7(), IncentiveKind.Award, turi.Id, EmployeeTestContext.Today),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("xodim.topilmadi");
    }

    [Fact]
    public async Task Nomavjud_tur_bilan_berilmaydi()
    {
        var employee = _context.ExistingEmployee();

        _context.AwardTypes.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((AwardType?)null);

        var natija = await Berish.HandleAsync(
            new GrantIncentiveCommand(employee.Id, IncentiveKind.Award, Guid.CreateVersion7(), EmployeeTestContext.Today),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("ragbat_jarima.ragbat_turi_topilmadi");
    }

    [Fact]
    public async Task Pulli_turga_summasiz_berish_rad_etiladi()
    {
        var employee = _context.ExistingEmployee();
        var turi = _context.ExistingPenaltyType(hasAmount: true);

        var natija = await Berish.HandleAsync(
            new GrantIncentiveCommand(
                employee.Id, IncentiveKind.Penalty, turi.Id, EmployeeTestContext.Today,
                Reason: "Intizom buzilishi"),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("ragbat_jarima.summa_kerak");
        await _context.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Togri_malumot_bilan_ragbat_beriladi_va_tur_nomi_qaytadi()
    {
        var employee = _context.ExistingEmployee();
        var turi = _context.ExistingAwardType(hasAmount: true);

        var natija = await Berish.HandleAsync(
            new GrantIncentiveCommand(
                employee.Id, IncentiveKind.Award, turi.Id, EmployeeTestContext.Today,
                Amount: 500_000m, OrderNo: "12-b"),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.TypeName.ShouldBe(turi.Name);
        natija.Value.Amount.ShouldNotBeNull();
        natija.Value.Amount!.Amount.ShouldBe(500_000m);

        _context.Incentives.Received(1).Add(Arg.Any<Incentive>());
        await _context.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Jarima_sababsiz_berilmaydi()
    {
        var employee = _context.ExistingEmployee();
        var turi = _context.ExistingPenaltyType();

        var natija = await Berish.HandleAsync(
            new GrantIncentiveCommand(employee.Id, IncentiveKind.Penalty, turi.Id, EmployeeTestContext.Today),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("ragbat_jarima.sabab_bosh");
    }

    // ────────────────────── Tuzatish ──────────────────────

    [Fact]
    public async Task Nomavjud_yozuv_tuzatilmaydi()
    {
        var turi = _context.ExistingAwardType();

        var natija = await Tuzatish.HandleAsync(
            new UpdateIncentiveCommand(Guid.CreateVersion7(), turi.Id, EmployeeTestContext.Today),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("ragbat_jarima.topilmadi");
    }

    [Fact]
    public async Task Mavjud_yozuv_yangi_tur_va_summa_bilan_tuzatiladi()
    {
        var eskiTur = _context.ExistingAwardType();
        var incentive = Incentive
            .Grant(Guid.CreateVersion7(), IncentiveKind.Award, eskiTur.Id, EmployeeTestContext.Today, typeHasAmount: false)
            .Value;

        _context.Incentives.GetByIdAsync(incentive.Id, Arg.Any<CancellationToken>()).Returns(incentive);

        var yangiTur = _context.ExistingAwardType(hasAmount: true);

        var natija = await Tuzatish.HandleAsync(
            new UpdateIncentiveCommand(incentive.Id, yangiTur.Id, EmployeeTestContext.Today, Amount: 1_000_000m),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.TypeId.ShouldBe(yangiTur.Id);
        natija.Value.Amount!.Amount.ShouldBe(1_000_000m);
        natija.Value.Kind.ShouldBe(IncentiveKind.Award);
    }

    // ────────────────────── O'chirish ──────────────────────

    [Fact]
    public async Task Nomavjud_yozuv_ochirilmaydi()
    {
        var natija = await Ochirish.HandleAsync(
            new DeleteIncentiveCommand(Guid.CreateVersion7()),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("ragbat_jarima.topilmadi");
    }

    [Fact]
    public async Task Mavjud_yozuv_ochiriladi()
    {
        var turi = _context.ExistingAwardType();
        var incentive = Incentive
            .Grant(Guid.CreateVersion7(), IncentiveKind.Award, turi.Id, EmployeeTestContext.Today, typeHasAmount: false)
            .Value;

        _context.Incentives.GetByIdAsync(incentive.Id, Arg.Any<CancellationToken>()).Returns(incentive);

        var natija = await Ochirish.HandleAsync(
            new DeleteIncentiveCommand(incentive.Id),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        _context.Incentives.Received(1).Remove(incentive);
        await _context.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
