using Bonvi.Application.Employees.Employments;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using NSubstitute;

namespace Bonvi.Application.Tests.Employees;

/// <summary>
/// Butunlay ketgan xodim boshliq bo'lgan bo'limlaridan bo'shatiladi (SPEC-jamoam).
/// </summary>
/// <remarks>
/// Aks holda bo'lim formasida ketgan odam boshliq bo'lib turaverardi va HR yangisini
/// tayinlash kerakligini ko'rmasdi. Qoida belgilarni yopish bilan bir xil: faqat OXIRGI
/// munosabat tugaganda — o'rindosh boshqa firmadan ketsa, bo'limini boshqarishda davom etadi.
/// </remarks>
public sealed class HeadshipReleaseTests
{
    private readonly EmployeeTestContext _context = new();

    private TerminateEmploymentCommandHandler Boshatuvchi => new(
        _context.Employments,
        _context.Probations,
        _context.TerminationReasons,
        _context.Flags,
        _context.Departments,
        _context.FlagGuard,
        _context.Mapper,
        _context.Clock,
        _context.UnitOfWork);

    [Fact]
    public async Task Oxirgi_munosabati_tugagan_boshliq_barcha_bolimlaridan_boshatiladi()
    {
        var employee = _context.ExistingEmployee();
        var employment = _context.ExistingEmployment(employee.Id);

        // Bazadagi holat: hozirgi munosabat hali «faol» qaytadi (saqlanmagan) — u hisoblanmasligi kerak.
        _context.NowEmployed(employee.Id, employment);

        var zavod = Bolim("Metan zavod", employee.Id);
        var sex = Bolim("Plazma sexi", employee.Id);
        _context.Departments
            .GetHeadedByAsync(employee.Id, Arg.Any<CancellationToken>())
            .Returns([zavod, sex]);

        var natija = await Boshatuvchi.HandleAsync(
            new TerminateEmploymentCommand(employment.Id),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);
        zavod.HeadEmployeeId.ShouldBeNull();
        sex.HeadEmployeeId.ShouldBeNull();

        // Bitta ish birligi: bo'shatish va boshliqlikdan bo'shash birga saqlanadi.
        await _context.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Boshqa_faol_munosabati_bor_boshliq_bolimini_saqlaydi()
    {
        var employee = _context.ExistingEmployee();
        var employment = _context.ExistingEmployment(employee.Id);
        var orindosh = _context.ExistingEmployment(employee.Id);
        _context.NowEmployed(employee.Id, employment, orindosh);

        var zavod = Bolim("Metan zavod", employee.Id);
        _context.Departments
            .GetHeadedByAsync(employee.Id, Arg.Any<CancellationToken>())
            .Returns([zavod]);

        var natija = await Boshatuvchi.HandleAsync(
            new TerminateEmploymentCommand(employment.Id),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);
        zavod.HeadEmployeeId.ShouldBe(employee.Id);
        await _context.Departments.DidNotReceive()
            .GetHeadedByAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Kelajak sana bilan bo'shatilganda ham darhol: munosabat ham darhol «yakunlangan» bo'ladi.
    /// </summary>
    [Fact]
    public async Task Kelajak_sana_bilan_boshatilganda_ham_darhol_boshatiladi()
    {
        var employee = _context.ExistingEmployee();
        var employment = _context.ExistingEmployment(employee.Id);

        var zavod = Bolim("Metan zavod", employee.Id);
        _context.Departments
            .GetHeadedByAsync(employee.Id, Arg.Any<CancellationToken>())
            .Returns([zavod]);

        var natija = await Boshatuvchi.HandleAsync(
            new TerminateEmploymentCommand(employment.Id, TerminationDate: EmployeeTestContext.Today.AddDays(14)),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);
        zavod.HeadEmployeeId.ShouldBeNull();
    }

    [Fact]
    public async Task Rad_etilgan_boshatish_boshliqqa_tegmaydi()
    {
        var employee = _context.ExistingEmployee();
        var employment = _context.ExistingEmployment(employee.Id);
        _context.GiveFlag(employee.Id, EmployeeFlagType.Blacklist);

        var zavod = Bolim("Metan zavod", employee.Id);
        _context.Departments
            .GetHeadedByAsync(employee.Id, Arg.Any<CancellationToken>())
            .Returns([zavod]);

        var natija = await Boshatuvchi.HandleAsync(
            new TerminateEmploymentCommand(employment.Id),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        zavod.HeadEmployeeId.ShouldBe(employee.Id);
    }

    private static Department Bolim(string nomi, Guid boshliq)
    {
        var bolim = Department.Create(nomi).Value;
        bolim.AssignHead(boshliq);
        return bolim;
    }
}
