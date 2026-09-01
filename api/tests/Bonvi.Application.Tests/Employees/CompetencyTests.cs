using Bonvi.Application.Employees.Competencies;
using Bonvi.Domain.Employees;
using NSubstitute;

namespace Bonvi.Application.Tests.Employees;

/// <summary>Qobiliyatlar profilini saqlash va o'qish — ulanish qatlami.</summary>
public sealed class CompetencyTests
{
    private readonly EmployeeTestContext _context = new();

    private SaveEmployeeCompetencyCommandHandler Saqlash => new(
        _context.Competencies,
        _context.Employees,
        _context.UnitOfWork);

    private GetEmployeeCompetencyQueryHandler Olish => new(
        _context.Competencies,
        _context.Employees);

    [Fact]
    public async Task Nomavjud_xodimga_profil_saqlanmaydi()
    {
        var natija = await Saqlash.HandleAsync(
            new SaveEmployeeCompetencyCommand(Guid.CreateVersion7(), Languages: "Rus tili"),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("xodim.topilmadi");
    }

    [Fact]
    public async Task Birinchi_marta_saqlashda_yangi_profil_yaratiladi()
    {
        var employee = _context.ExistingEmployee();

        _context.Competencies
            .GetByEmployeeAsync(employee.Id, Arg.Any<CancellationToken>())
            .Returns((EmployeeCompetency?)null);

        var natija = await Saqlash.HandleAsync(
            new SaveEmployeeCompetencyCommand(
                employee.Id,
                Languages: "Инглиз ва рус тили",
                ComputerLiteracyPercent: 80,
                Specialization: "Ихтисодиёт"),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.EmployeeId.ShouldBe(employee.Id);
        natija.Value.ComputerLiteracyPercent.ShouldBe(80);

        _context.Competencies.Received(1).Add(Arg.Any<EmployeeCompetency>());
        await _context.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Ikkinchi_marta_saqlashda_mavjud_profil_yangilanadi_yangisi_yaratilmaydi()
    {
        var employee = _context.ExistingEmployee();
        var existing = EmployeeCompetency.Create(employee.Id).Value;
        existing.SetLanguages("Rus tili");

        _context.Competencies
            .GetByEmployeeAsync(employee.Id, Arg.Any<CancellationToken>())
            .Returns(existing);

        var natija = await Saqlash.HandleAsync(
            new SaveEmployeeCompetencyCommand(employee.Id, Languages: "Ingliz tili"),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.Languages.ShouldBe("Ingliz tili");
        natija.Value.Id.ShouldBe(existing.Id, "yangi yozuv emas, xuddi o'sha profil yangilanishi kerak");

        _context.Competencies.DidNotReceive().Add(Arg.Any<EmployeeCompetency>());
    }

    [Fact]
    public async Task Tillar_royxati_saqlanadi_va_dtoda_qaytadi()
    {
        var employee = _context.ExistingEmployee();

        _context.Competencies
            .GetByEmployeeAsync(employee.Id, Arg.Any<CancellationToken>())
            .Returns((EmployeeCompetency?)null);

        var natija = await Saqlash.HandleAsync(
            new SaveEmployeeCompetencyCommand(
                employee.Id,
                LanguageSkills:
                [
                    new LanguageSkill(LanguageCode.English, LanguageLevel.UpperIntermediate),
                    new LanguageSkill(LanguageCode.Russian, LanguageLevel.Native),
                ]),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.LanguageSkills.Count.ShouldBe(2);
        natija.Value.LanguageSkills.ShouldContain(
            l => l.Language == LanguageCode.English && l.Level == LanguageLevel.UpperIntermediate);
    }

    [Fact]
    public async Task Takror_til_bilan_saqlash_rad_etiladi()
    {
        var employee = _context.ExistingEmployee();

        _context.Competencies
            .GetByEmployeeAsync(employee.Id, Arg.Any<CancellationToken>())
            .Returns((EmployeeCompetency?)null);

        var natija = await Saqlash.HandleAsync(
            new SaveEmployeeCompetencyCommand(
                employee.Id,
                LanguageSkills:
                [
                    new LanguageSkill(LanguageCode.English, LanguageLevel.Elementary),
                    new LanguageSkill(LanguageCode.English, LanguageLevel.Advanced),
                ]),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("qobiliyat.til_takror");
        await _context.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Notogri_foiz_bilan_saqlash_rad_etiladi()
    {
        var employee = _context.ExistingEmployee();

        _context.Competencies
            .GetByEmployeeAsync(employee.Id, Arg.Any<CancellationToken>())
            .Returns((EmployeeCompetency?)null);

        var natija = await Saqlash.HandleAsync(
            new SaveEmployeeCompetencyCommand(employee.Id, ComputerLiteracyPercent: 150),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("qobiliyat.kompyuter_foiz");
        await _context.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Profil_hali_yoq_bolsa_null_qaytadi_xato_emas()
    {
        var employee = _context.ExistingEmployee();

        _context.Competencies
            .GetByEmployeeAsync(employee.Id, Arg.Any<CancellationToken>())
            .Returns((EmployeeCompetency?)null);

        var natija = await Olish.HandleAsync(
            new GetEmployeeCompetencyQuery(employee.Id),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.ShouldBeNull();
    }

    [Fact]
    public async Task Nomavjud_xodim_uchun_sorov_xato_qaytaradi()
    {
        var natija = await Olish.HandleAsync(
            new GetEmployeeCompetencyQuery(Guid.CreateVersion7()),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("xodim.topilmadi");
    }
}
