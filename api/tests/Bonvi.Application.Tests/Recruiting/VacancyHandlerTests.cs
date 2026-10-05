using Bonvi.Application.Recruiting.Vacancies;
using Bonvi.Domain.Recruiting;
using NSubstitute;

namespace Bonvi.Application.Tests.Recruiting;

/// <summary>
/// Vakansiya nomining bandligi — ulanish qatlami.
/// </summary>
/// <remarks>
/// Nom 2026-09-29 dan vakansiyaning o'zida (sababi <c>Vacancy</c>
/// izohida). API testlari (<c>RecruitingApiTests</c>) ochish va tahrirlashni
/// haqiqiy bazada tekshiradi; bu yerda esa API orqali yasab bo'lmaydigan
/// holat — ko'chirishdan qolgan bir xil nomli IKKI vakansiya — tekshiriladi.
/// </remarks>
public sealed class VacancyHandlerTests
{
    private readonly RecruitingTestContext _context = new();

    private OpenVacancyCommandHandler Ochish => new(
        _context.Vacancies, _context.Mapper, _context.Clock, _context.UnitOfWork);

    private UpdateVacancyCommandHandler Tahrir => new(
        _context.Vacancies, _context.Mapper, _context.UnitOfWork);

    private ChangeVacancyStateCommandHandler Holat => new(
        _context.Vacancies, _context.Mapper, _context.Clock, _context.UnitOfWork);

    [Fact]
    public async Task Band_nom_bilan_vakansiya_ochilmaydi()
    {
        _context.Vacancies
            .TitleTakenAsync("Сотувчи", null, false, Arg.Any<CancellationToken>())
            .Returns(true);

        // Bo'shliqlar yig'ilgan nom so'raladi — takror tekshiruvi aynan
        // saqlanadigan shakl bo'yicha bo'lishi kerak.
        var natija = await Ochish.HandleAsync(
            new OpenVacancyCommand("  Сотувчи "), TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("vakansiya.nom_band");
        _context.Vacancies.DidNotReceive().Add(Arg.Any<Vacancy>());
        await _context.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rad_etilgan_tahrir_vakansiyaga_tegmaydi()
    {
        var vakansiya = _context.ExistingVacancy("Омборчи");
        _context.Vacancies
            .TitleTakenAsync("Сотувчи", vakansiya.Id, false, Arg.Any<CancellationToken>())
            .Returns(true);

        var natija = await Tahrir.HandleAsync(
            new UpdateVacancyCommand(vakansiya.Id, "Сотувчи", Openings: 5),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("vakansiya.nom_band");
        vakansiya.Title.ShouldBe("Омборчи", "tekshiruv yozuvga tegishdan OLDIN");
        vakansiya.Openings.ShouldBe(1);
    }

    [Fact]
    public async Task Nomi_ozgarmasa_eski_takror_tahrirni_tosmaydi()
    {
        // Ko'chirishdan oldin bitta nomga bir nechta vakansiya ochilardi va
        // migratsiya ularning hammasiga O'SHA nomni yozgan. Tekshiruv har
        // saqlashda ishlasa, HR bunday vakansiyaning o'rin sonini ham
        // o'zgartira olmasdi.
        var vakansiya = _context.ExistingVacancy("Сотувчи");
        _context.Vacancies
            .TitleTakenAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var natija = await Tahrir.HandleAsync(
            new UpdateVacancyCommand(vakansiya.Id, "  сотувчи ", Openings: 5),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        vakansiya.Openings.ShouldBe(5);
        await _context.Vacancies.DidNotReceive().TitleTakenAsync(
            Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Ochiq_vakansiyani_qayta_ochish_takror_bolsa_ham_zararsiz()
    {
        // Eskirgan ro'yxatdan kelgan takroriy bosish: vakansiya allaqachon
        // ochiq, shu nomli boshqa ochig'i ham bor (ko'chirishdan qolgan).
        var vakansiya = _context.ExistingVacancy("Сотувчи");
        _context.Vacancies
            .TitleTakenAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var natija = await Holat.HandleAsync(
            new ReopenVacancyCommand(vakansiya.Id), TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        vakansiya.Status.ShouldBe(VacancyStatus.Open);
    }

    [Fact]
    public async Task Shu_nomli_boshqasi_OCHIQ_bolsa_qayta_ochilmaydi()
    {
        var vakansiya = _context.ExistingVacancy("Сотувчи", open: false);
        _context.Vacancies
            .TitleTakenAsync("Сотувчи", vakansiya.Id, true, Arg.Any<CancellationToken>())
            .Returns(true);

        var natija = await Holat.HandleAsync(
            new ReopenVacancyCommand(vakansiya.Id), TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("vakansiya.nom_band");
        vakansiya.Status.ShouldBe(VacancyStatus.Closed);
    }

    [Fact]
    public async Task Shu_nomli_boshqasi_YOPIQ_bolsa_qayta_ochiladi()
    {
        // Qayta ochishda faqat OCHIQlar so'raladi: ko'chirishdan qolgan
        // bir xil nomli yopiq vakansiyalar bir-birini to'smasligi kerak.
        var vakansiya = _context.ExistingVacancy("Сотувчи", open: false);
        _context.Vacancies
            .TitleTakenAsync("Сотувчи", vakansiya.Id, false, Arg.Any<CancellationToken>())
            .Returns(true);

        var natija = await Holat.HandleAsync(
            new ReopenVacancyCommand(vakansiya.Id), TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        vakansiya.Status.ShouldBe(VacancyStatus.Open);
    }
}
