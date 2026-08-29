using Bonvi.Application.Abstractions;
using Bonvi.Application.Common.Documents;
using Bonvi.Application.Employees.Common;
using Bonvi.Application.Employees.Documents;
using Bonvi.Application.Employees.Incentives;
using Bonvi.Application.Feedback;
using Bonvi.Application.Safety;
using Bonvi.Application.Safety.Common;
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

        // Sukut bo'yicha xodimda kuchdagi belgi YO'Q. Bo'sh ro'yxat ataylab
        // aniq beriladi: NSubstitute `Task<IReadOnlyList<T>>` uchun `null`
        // qaytaradi va belgi qo'yishga tegadigan har bir test tushunarsiz
        // NRE bilan yiqilardi.
        Flags
            .GetInForceAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<EmployeeFlag>());

        // Sukut bo'yicha xodim HECH QAYERDA ishlamaydi. Sabab yuqoridagi
        // bilan bir xil (NSubstitute `null` qaytaradi), lekin bu tanlovning
        // MA'NOSI ham bor: belgi bosqichi shu ro'yxatga qarab aniqlanadi
        // va bo'sh ro'yxat «ishdan ketgan» degani — ya'ni rezerv/qora
        // ro'yxat testlari qo'shimcha sozlashsiz ishlaydi. Ishlab turgan
        // xodim kerak bo'lganda `NowEmployed` chaqiriladi.
        Employments
            .GetActiveByEmployeeAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Employment>());

        LegalEntities.ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        TerminationReasons.ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        Departments.ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        Positions.ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
    }

    public IEmployeeRepository Employees { get; } = Substitute.For<IEmployeeRepository>();

    public IEmploymentRepository Employments { get; } = Substitute.For<IEmploymentRepository>();

    public IProbationRepository Probations { get; } = Substitute.For<IProbationRepository>();

    public IEmployeeFlagRepository Flags { get; } = Substitute.For<IEmployeeFlagRepository>();

    public IEmployeeCompetencyRepository Competencies { get; } = Substitute.For<IEmployeeCompetencyRepository>();

    public IPositionRepository Positions { get; } = Substitute.For<IPositionRepository>();

    public IDepartmentRepository Departments { get; } = Substitute.For<IDepartmentRepository>();

    public IReferenceRepository<LegalEntity> LegalEntities { get; } =
        Substitute.For<IReferenceRepository<LegalEntity>>();

    public IReferenceRepository<TerminationReason> TerminationReasons { get; } =
        Substitute.For<IReferenceRepository<TerminationReason>>();

    public IReferenceRepository<AwardType> AwardTypes { get; } =
        Substitute.For<IReferenceRepository<AwardType>>();

    public IReferenceRepository<PenaltyType> PenaltyTypes { get; } =
        Substitute.For<IReferenceRepository<PenaltyType>>();

    public IIncentiveRepository Incentives { get; } = Substitute.For<IIncentiveRepository>();

    public IReferenceRepository<DocumentType> DocumentTypes { get; } =
        Substitute.For<IReferenceRepository<DocumentType>>();

    public IEmployeeDocumentRepository Documents { get; } = Substitute.For<IEmployeeDocumentRepository>();

    public IExitInterviewRepository ExitInterviews { get; } = Substitute.For<IExitInterviewRepository>();

    public IIncidentRepository Incidents { get; } = Substitute.For<IIncidentRepository>();

    public IFileStorage Storage { get; } = Substitute.For<IFileStorage>();

    public IFileContentInspector Inspector { get; } = Substitute.For<IFileContentInspector>();

    public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

    public IClock Clock { get; } = Substitute.For<IClock>();

    public ICurrentUser CurrentUser { get; } = Substitute.For<ICurrentUser>();

    public CatalogNameResolver Names => new(
        Departments, Positions, LegalEntities, TerminationReasons, AwardTypes, PenaltyTypes, DocumentTypes);

    public EmployeeMapper Mapper => new(CurrentUser, Names, Clock);

    public EmployeeFlagGuard FlagGuard => new(Flags, Clock);

    public IncentiveTypeResolver Types => new(AwardTypes, PenaltyTypes);

    public DocumentFileStore Files => new(Storage, Inspector);

    public ExitInterviewMapper FeedbackMapper => new(Employments, Employees, Names);

    public IncidentMapper SafetyMapper => new(Employees, Names);

    /// <summary>
    /// Hodisa kunidagi lavozimni topuvchi — MOCK emas, haqiqiy sinf.
    /// </summary>
    /// <remarks>
    /// Uning butun mantig'i mehnat munosabatlarini sana bo'yicha
    /// saralashdan iborat. Mock qilinsa, aynan shu saralash testdan
    /// tashqarida qolardi — `FlagGuard` va `Names` bilan bir xil qaror.
    /// </remarks>
    public IncidentPositionResolver IncidentPositions => new(Employments);

    /// <summary>Oklad ko'rish ruxsatini yoqadi/o'chiradi.</summary>
    public void AllowSalary(bool allowed)
        => CurrentUser.HasPermission(Permissions.SalaryView).Returns(allowed);

    /// <summary>Xodimga faol belgi qo'yadi.</summary>
    public void GiveFlag(Guid employeeId, EmployeeFlagType type)
        => Flags
            .HasActiveFlagAsync(employeeId, type, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(true);

    /// <summary>
    /// Xodimda KUCHDAGI belgi bor deb ko'rsatadi va o'sha yozuvni qaytaradi.
    /// </summary>
    /// <remarks>
    /// <see cref="GiveFlag"/> dan farqi: u faqat «bormi?» degan savolga
    /// javob beradi (ishga qabul va bo'shatish to'siqlari uchun), bu esa
    /// yozuvning O'ZINI beradi — «bir vaqtda atigi bitta belgi» qoidasi
    /// eskisini YOPISHI kerak, ya'ni unga obyekt kerak.
    /// </remarks>
    public EmployeeFlag ExistingFlag(
        Guid employeeId,
        EmployeeFlagType type = EmployeeFlagType.Blacklist,
        DateOnly? startDate = null,
        DateOnly? endDate = null)
    {
        var flag = EmployeeFlag
            .Raise(employeeId, type, startDate ?? Today.AddDays(-30), "eski sabab", endDate)
            .Value;

        Flags
            .GetInForceAsync(employeeId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns([flag]);

        return flag;
    }

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

    /// <summary>Bazada mavjud rag'bat turi.</summary>
    public AwardType ExistingAwardType(bool hasAmount = false)
    {
        var type = AwardType.Create("Ташаккурнома").Value;
        type.SetHasAmount(hasAmount);
        AwardTypes.GetByIdAsync(type.Id, Arg.Any<CancellationToken>()).Returns(type);
        return type;
    }

    /// <summary>Bazada mavjud jarima turi.</summary>
    public PenaltyType ExistingPenaltyType(bool hasAmount = false)
    {
        var type = PenaltyType.Create("Ҳайфсан").Value;
        type.SetHasAmount(hasAmount);
        PenaltyTypes.GetByIdAsync(type.Id, Arg.Any<CancellationToken>()).Returns(type);
        return type;
    }

    /// <summary>Bazada mavjud hujjat turi.</summary>
    public DocumentType ExistingDocumentType(string name = "Мехнат шартномаси")
    {
        var type = DocumentType.Create(name).Value;
        DocumentTypes.GetByIdAsync(type.Id, Arg.Any<CancellationToken>()).Returns(type);
        DocumentTypes.ExistsAsync(type.Id, Arg.Any<CancellationToken>()).Returns(true);
        return type;
    }

    /// <summary>
    /// Xodim HOZIR ishlab turibdi deb ko'rsatadi.
    /// </summary>
    /// <remarks>
    /// Belgi bosqichi (<c>EmployeeFlagStage</c>) shu ro'yxatga qarab
    /// aniqlanadi: bo'sh bo'lsa xodim ketgan hisoblanadi.
    /// <see cref="ExistingEmployment"/> buni O'ZI qilmaydi — u yakunlangan
    /// munosabatni ham yasashi mumkin va o'shanda «ishlayapti» deb
    /// ko'rsatish yolg'on bo'lardi.
    /// </remarks>
    public void NowEmployed(Guid employeeId, params Employment[] employments)
        => Employments
            .GetActiveByEmployeeAsync(employeeId, Arg.Any<CancellationToken>())
            .Returns(employments);

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
