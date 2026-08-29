using Bonvi.Application.Employees.Employments;
using Bonvi.Domain.Employees;
using NSubstitute;

namespace Bonvi.Application.Tests.Employees;

/// <summary>
/// Bo'shatishdagi blok qoidasi.
/// </summary>
/// <remarks>
/// HR so'zma-so'z so'ragan qoida: qora ro'yxatdagi yoki nazoratdagi odam
/// «jimgina» ketib qolmasligi kerak. Shu sababli birinchi so'rov 409 bilan
/// qaytadi, ataylab yuborilgan tasdiq esa o'tadi.
/// </remarks>
public sealed class TerminationBlockTests
{
    private readonly EmployeeTestContext _context = new();

    private TerminateEmploymentCommandHandler Boshatuvchi => new(
        _context.Employments,
        _context.Probations,
        _context.TerminationReasons,
        _context.Flags,
        _context.FlagGuard,
        _context.Mapper,
        _context.Clock,
        _context.UnitOfWork);

    [Fact]
    public async Task Belgisiz_xodim_tasdiqsiz_boshatiladi()
    {
        var employee = _context.ExistingEmployee();
        var employment = _context.ExistingEmployment(employee.Id);

        var natija = await Boshatuvchi.HandleAsync(
            new TerminateEmploymentCommand(employment.Id),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);
        natija.Value.Warnings.ShouldBeEmpty();
        employment.Status.ShouldBe(EmploymentStatus.Terminated);
    }

    [Fact]
    public async Task Qora_royxatdagi_xodim_tasdiqsiz_boshatilmaydi()
    {
        var employee = _context.ExistingEmployee();
        var employment = _context.ExistingEmployment(employee.Id);
        _context.GiveFlag(employee.Id, EmployeeFlagType.Blacklist);

        var natija = await Boshatuvchi.HandleAsync(
            new TerminateEmploymentCommand(employment.Id),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("xodim.qora_royxatda");

        // Munosabat tegilmagan bo'lishi kerak — 409 dan keyin holat o'zgarmaydi
        employment.Status.ShouldNotBe(EmploymentStatus.Terminated);
        await _context.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Nazoratdagi_xodim_tasdiqsiz_boshatilmaydi()
    {
        var employee = _context.ExistingEmployee();
        var employment = _context.ExistingEmployment(employee.Id);
        _context.GiveFlag(employee.Id, EmployeeFlagType.Monitoring);

        var natija = await Boshatuvchi.HandleAsync(
            new TerminateEmploymentCommand(employment.Id),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("xodim.nazoratda");
    }

    [Fact]
    public async Task Tasdiq_bilan_boshatiladi_va_sabab_ogohlantirishda_qoladi()
    {
        var employee = _context.ExistingEmployee();
        var employment = _context.ExistingEmployment(employee.Id);
        _context.GiveFlag(employee.Id, EmployeeFlagType.Blacklist);

        var natija = await Boshatuvchi.HandleAsync(
            new TerminateEmploymentCommand(employment.Id, Confirm: true),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);
        employment.Status.ShouldBe(EmploymentStatus.Terminated);

        // Tasdiq belgini o'chirmaydi: nima uchun to'xtaganini javob eslab qoladi
        natija.Value.Warnings.ShouldContain(w => w.Code == "xodim.qora_royxatda");
    }

    [Fact]
    public async Task Ochiq_sinov_muddati_erta_ketdi_deb_yopiladi()
    {
        var employee = _context.ExistingEmployee();
        var employment = _context.ExistingEmployment(employee.Id, hireDate: EmployeeTestContext.Today.AddDays(-30));

        var probation = Probation.Start(employment.Id, employment.HireDate, 90).Value;
        _context.Probations
            .GetByEmploymentAsync(employment.Id, Arg.Any<CancellationToken>())
            .Returns(probation);

        var natija = await Boshatuvchi.HandleAsync(
            new TerminateEmploymentCommand(employment.Id),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);

        // Aks holda «sinovdagilar» ro'yxatida ishdan ketgan odam turib qolardi
        probation.Status.ShouldBe(ProbationStatus.LeftEarly);
    }

    [Fact]
    public async Task Ketish_sanasi_ishga_kirishdan_oldin_bololmaydi()
    {
        var employee = _context.ExistingEmployee();
        var employment = _context.ExistingEmployment(employee.Id, hireDate: EmployeeTestContext.Today.AddDays(-10));

        var natija = await Boshatuvchi.HandleAsync(
            new TerminateEmploymentCommand(
                employment.Id,
                TerminationDate: EmployeeTestContext.Today.AddDays(-20)),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("mehnat.ketish_sanasi");
    }

    [Fact]
    public async Task Yakunlangan_munosabatni_qayta_boshatib_bolmaydi()
    {
        var employee = _context.ExistingEmployee();
        var employment = _context.ExistingEmployment(employee.Id);
        employment.Terminate(EmployeeTestContext.Today, null);

        var natija = await Boshatuvchi.HandleAsync(
            new TerminateEmploymentCommand(employment.Id),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("mehnat.tugagan");
    }
}
