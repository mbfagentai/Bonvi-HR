using Bonvi.Application.Abstractions;
using Bonvi.Application.Employees.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Catalog;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using NSubstitute;

namespace Bonvi.Application.Tests.Employees;

/// <summary>
/// Xodim use-case'lari uchun umumiy mock to'plami.
/// </summary>
/// <remarks>
/// <para>
/// Har bir test sinfida sakkizta bog'liqlikni qo'lda yig'ish o'rniga bitta
/// kontekst: testlar «nima tekshirilyapti» dan boshlanadi, «qanday qilib
/// qurilyapti» dan emas.
/// </para>
/// <para>
/// Sukut bo'yicha holat ataylab «hammasi joyida»: yuridik shaxs mavjud,
/// belgilar yo'q, stavka bo'sh. Har bir test faqat O'ZIGA kerakli farqni
/// o'rnatadi va shu farq testning mazmuni bo'lib ko'rinadi.
/// </para>
/// </remarks>
internal sealed class EmployeeTestContext
{
    /// <summary>Testlardagi «bugun» — qat'iy sana, chunki sinov muddati kunlarda hisoblanadi.</summary>
    public static readonly DateOnly Today = new(2026, 3, 10);

    public EmployeeTestContext()
    {
        Clock.Today.Returns(Today);
        Clock.UtcNow.Returns(new DateTimeOffset(Today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));

        LegalEntities.ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        TerminationReasons.ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        Departments.ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        Positions.ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
    }

    public IEmployeeRepository Employees { get; } = Substitute.For<IEmployeeRepository>();

    public IEmploymentRepository Employments { get; } = Substitute.For<IEmploymentRepository>();

    public IProbationRepository Probations { get; } = Substitute.For<IProbationRepository>();

    public IEmployeeFlagRepository Flags { get; } = Substitute.For<IEmployeeFlagRepository>();

    public IPositionRepository Positions { get; } = Substitute.For<IPositionRepository>();

    public IDepartmentRepository Departments { get; } = Substitute.For<IDepartmentRepository>();

    public IReferenceRepository<LegalEntity> LegalEntities { get; } =
        Substitute.For<IReferenceRepository<LegalEntity>>();

    public IReferenceRepository<TerminationReason> TerminationReasons { get; } =
        Substitute.For<IReferenceRepository<TerminationReason>>();

    public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

    public IClock Clock { get; } = Substitute.For<IClock>();

    public ICurrentUser CurrentUser { get; } = Substitute.For<ICurrentUser>();

    public CatalogNameResolver Names => new(Departments, Positions, LegalEntities, TerminationReasons);

    public EmployeeMapper Mapper => new(CurrentUser, Names, Clock);

    public EmployeeFlagGuard FlagGuard => new(Flags, Clock);

    /// <summary>Oklad ko'rish ruxsatini yoqadi/o'chiradi.</summary>
    public void AllowSalary(bool allowed)
        => CurrentUser.HasPermission(Permissions.SalaryView).Returns(allowed);

    /// <summary>Xodimga faol belgi qo'yadi.</summary>
    public void GiveFlag(Guid employeeId, EmployeeFlagType type)
        => Flags
            .HasActiveFlagAsync(employeeId, type, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(true);

    /// <summary>Bazada mavjud xodim.</summary>
    public Employee ExistingEmployee(string lastName = "Karimov", string firstName = "Aziz")
    {
        var employee = Employee.Create(FullName.Create(lastName, firstName, null).Value).Value;
        Employees.GetByIdAsync(employee.Id, Arg.Any<CancellationToken>()).Returns(employee);
        Employees.ExistsAsync(employee.Id, Arg.Any<CancellationToken>()).Returns(true);
        return employee;
    }

    /// <summary>Sinov muddati kuni belgilangan lavozim.</summary>
    public Position ExistingPosition(string name = "Menejer", int? probationDays = null)
    {
        var position = Position.Create(name).Value;
        if (probationDays is not null)
        {
            position.SetProbationDays(probationDays);
        }

        Positions.GetWithDutiesAsync(position.Id, Arg.Any<CancellationToken>()).Returns(position);
        Positions.GetByIdAsync(position.Id, Arg.Any<CancellationToken>()).Returns(position);
        return position;
    }

    /// <summary>Faol mehnat munosabati (bazada bor deb hisoblanadi).</summary>
    public Employment ExistingEmployment(
        Guid employeeId,
        DateOnly? hireDate = null,
        decimal? salary = null)
    {
        var employment = Employment.Hire(
            employeeId,
            Guid.NewGuid(),
            hireDate ?? Today.AddDays(-400),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "12-b").Value;

        if (salary is { } amount)
        {
            employment.SetSalary(Money.Create(amount).Value, employment.HireDate);
        }

        Employments.GetWithEventsAsync(employment.Id, Arg.Any<CancellationToken>()).Returns(employment);
        Employments.GetByIdAsync(employment.Id, Arg.Any<CancellationToken>()).Returns(employment);

        return employment;
    }
}
