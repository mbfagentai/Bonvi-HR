using Bonvi.Application.Safety;
using Bonvi.Application.Tests.Employees;
using Bonvi.Domain.Safety;
using NSubstitute;

namespace Bonvi.Application.Tests.Safety;

/// <summary>Baxtsiz hodisa yozish, tuzatish, yakunlash va o'chirish — ulanish qatlami.</summary>
public sealed class IncidentHandlerTests
{
    private readonly EmployeeTestContext _context = new();

    private RecordIncidentCommandHandler Yozish => new(
        _context.Incidents, _context.Employees, _context.SafetyMapper, _context.UnitOfWork);

    private UpdateIncidentCommandHandler Tuzatish => new(
        _context.Incidents, _context.Employees, _context.SafetyMapper, _context.UnitOfWork);

    private ConcludeIncidentCommandHandler Yakunlash => new(
        _context.Incidents, _context.SafetyMapper, _context.Clock, _context.UnitOfWork);

    private DeleteIncidentCommandHandler Ochirish => new(_context.Incidents, _context.UnitOfWork);

    // ────────────────────── Yozish ──────────────────────

    [Fact]
    public async Task Nomavjud_xodimga_yozilmaydi()
    {
        var natija = await Yozish.HandleAsync(
            new RecordIncidentCommand(
                Guid.CreateVersion7(), EmployeeTestContext.Today, "Joy", "Tavsif", IncidentSeverity.Minor),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("baxtsiz_hodisa.xodim_topilmadi");
    }

    [Fact]
    public async Task Togri_malumot_bilan_yoziladi_va_xodim_nomi_qaytadi()
    {
        var employee = _context.ExistingEmployee();

        var natija = await Yozish.HandleAsync(
            new RecordIncidentCommand(
                employee.Id, EmployeeTestContext.Today, "Metan zavodi", "Qo'l jarohati", IncidentSeverity.Severe,
                MedicalAidProvided: true, MedicalAidNote: "Tez tibbiy yordam chaqirildi"),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.EmployeeId.ShouldBe(employee.Id);
        natija.Value.EmployeeName.ShouldBe(employee.FullName.Full);
        natija.Value.Severity.ShouldBe(IncidentSeverity.Severe);
        natija.Value.CommissionConclusion.ShouldBeNull("xulosa yaratishda MAJBURIY emas");

        _context.Incidents.Received(1).Add(Arg.Any<Incident>());
        await _context.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ────────────────────── Tuzatish ──────────────────────

    [Fact]
    public async Task Nomavjud_yozuv_tuzatilmaydi()
    {
        var natija = await Tuzatish.HandleAsync(
            new UpdateIncidentCommand(
                Guid.CreateVersion7(), EmployeeTestContext.Today, "Joy", "Tavsif", IncidentSeverity.Minor),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("baxtsiz_hodisa.topilmadi");
    }

    [Fact]
    public async Task Mavjud_yozuv_tuzatiladi_lekin_xodim_saqlanadi()
    {
        var employee = _context.ExistingEmployee();
        var incident = Incident
            .Record(employee.Id, EmployeeTestContext.Today, "Joy", "Tavsif", IncidentSeverity.Minor)
            .Value;

        _context.Incidents.GetByIdAsync(incident.Id, Arg.Any<CancellationToken>()).Returns(incident);

        var natija = await Tuzatish.HandleAsync(
            new UpdateIncidentCommand(
                incident.Id, EmployeeTestContext.Today, "Yangi joy", "Yangi tavsif", IncidentSeverity.Fatal),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.Location.ShouldBe("Yangi joy");
        natija.Value.Severity.ShouldBe(IncidentSeverity.Fatal);
        natija.Value.EmployeeId.ShouldBe(employee.Id);
    }

    // ────────────────────── Yakunlash (komissiya xulosasi) ──────────────────────

    [Fact]
    public async Task Xulosasiz_yakunlanmaydi()
    {
        var employee = _context.ExistingEmployee();
        var incident = Incident
            .Record(employee.Id, EmployeeTestContext.Today, "Joy", "Tavsif", IncidentSeverity.Minor)
            .Value;

        _context.Incidents.GetByIdAsync(incident.Id, Arg.Any<CancellationToken>()).Returns(incident);

        var natija = await Yakunlash.HandleAsync(
            new ConcludeIncidentCommand(incident.Id, null),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("baxtsiz_hodisa.xulosa_bosh");
    }

    [Fact]
    public async Task Xulosa_muvaffaqiyatli_yoziladi()
    {
        var employee = _context.ExistingEmployee();
        var incident = Incident
            .Record(employee.Id, EmployeeTestContext.Today, "Joy", "Tavsif", IncidentSeverity.Fatal)
            .Value;

        _context.Incidents.GetByIdAsync(incident.Id, Arg.Any<CancellationToken>()).Returns(incident);

        var natija = await Yakunlash.HandleAsync(
            new ConcludeIncidentCommand(incident.Id, "Xavfsizlik qoidalari buzilgan"),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.CommissionConclusion.ShouldBe("Xavfsizlik qoidalari buzilgan");
        natija.Value.ConcludedAt.ShouldBe(EmployeeTestContext.Today);
    }

    // ────────────────────── O'chirish ──────────────────────

    [Fact]
    public async Task Nomavjud_yozuv_ochirilmaydi()
    {
        var natija = await Ochirish.HandleAsync(
            new DeleteIncidentCommand(Guid.CreateVersion7()),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("baxtsiz_hodisa.topilmadi");
    }

    [Fact]
    public async Task Mavjud_yozuv_ochiriladi()
    {
        var employee = _context.ExistingEmployee();
        var incident = Incident
            .Record(employee.Id, EmployeeTestContext.Today, "Joy", "Tavsif", IncidentSeverity.Minor)
            .Value;

        _context.Incidents.GetByIdAsync(incident.Id, Arg.Any<CancellationToken>()).Returns(incident);

        var natija = await Ochirish.HandleAsync(
            new DeleteIncidentCommand(incident.Id),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        _context.Incidents.Received(1).Remove(incident);
        await _context.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
