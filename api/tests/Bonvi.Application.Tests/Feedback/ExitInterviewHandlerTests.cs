using Bonvi.Application.Feedback;
using Bonvi.Application.Tests.Employees;
using Bonvi.Domain.Feedback;
using NSubstitute;

namespace Bonvi.Application.Tests.Feedback;

/// <summary>Qayta aloqa yozish, tuzatish va o'chirish — ulanish qatlami.</summary>
public sealed class ExitInterviewHandlerTests
{
    private readonly EmployeeTestContext _context = new();

    private RecordExitInterviewCommandHandler Yozish => new(
        _context.ExitInterviews, _context.Employments, _context.FeedbackMapper, _context.UnitOfWork);

    private UpdateExitInterviewCommandHandler Tuzatish => new(
        _context.ExitInterviews, _context.FeedbackMapper, _context.UnitOfWork);

    private DeleteExitInterviewCommandHandler Ochirish => new(_context.ExitInterviews, _context.UnitOfWork);

    // ────────────────────── Yozish ──────────────────────

    [Fact]
    public async Task Nomavjud_mehnat_munosabatiga_yozilmaydi()
    {
        var natija = await Yozish.HandleAsync(
            new RecordExitInterviewCommand(Guid.CreateVersion7(), EmployeeTestContext.Today, "Sabab"),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("qayta_aloqa.mehnat_topilmadi");
    }

    [Fact]
    public async Task Tugamagan_mehnat_munosabatiga_yozilmaydi()
    {
        var employee = _context.ExistingEmployee();
        var employment = _context.ExistingEmployment(employee.Id);

        var natija = await Yozish.HandleAsync(
            new RecordExitInterviewCommand(employment.Id, EmployeeTestContext.Today, "Sabab"),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("qayta_aloqa.mehnat_tugamagan");
    }

    [Fact]
    public async Task Takroriy_yozuv_rad_etiladi()
    {
        var employee = _context.ExistingEmployee();
        var employment = _context.ExistingEmployment(employee.Id);
        employment.Terminate(EmployeeTestContext.Today, null);

        var mavjud = ExitInterview.Record(employment.Id, EmployeeTestContext.Today.AddDays(-1), "Eski sabab").Value;
        _context.ExitInterviews.GetByEmploymentAsync(employment.Id, Arg.Any<CancellationToken>()).Returns(mavjud);

        var natija = await Yozish.HandleAsync(
            new RecordExitInterviewCommand(employment.Id, EmployeeTestContext.Today, "Yangi sabab"),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("qayta_aloqa.allaqachon_bor");
    }

    [Fact]
    public async Task Togri_malumot_bilan_yoziladi_va_xodim_nomi_qaytadi()
    {
        var employee = _context.ExistingEmployee();
        var employment = _context.ExistingEmployment(employee.Id);
        employment.Terminate(EmployeeTestContext.Today, null);

        var natija = await Yozish.HandleAsync(
            new RecordExitInterviewCommand(
                employment.Id, EmployeeTestContext.Today, "Oylik kam edi", RecommendationAnswer.Yes),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.Reason.ShouldBe("Oylik kam edi");
        natija.Value.EmployeeId.ShouldBe(employee.Id);
        natija.Value.EmployeeName.ShouldBe(employee.FullName.Full);
        natija.Value.WouldRecommend.ShouldBe(RecommendationAnswer.Yes);

        _context.ExitInterviews.Received(1).Add(Arg.Any<ExitInterview>());
        await _context.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ────────────────────── Tuzatish ──────────────────────

    [Fact]
    public async Task Nomavjud_yozuv_tuzatilmaydi()
    {
        var natija = await Tuzatish.HandleAsync(
            new UpdateExitInterviewCommand(Guid.CreateVersion7(), EmployeeTestContext.Today, "Sabab"),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("qayta_aloqa.topilmadi");
    }

    [Fact]
    public async Task Mavjud_yozuv_tuzatiladi_va_mehnat_munosabati_saqlanadi()
    {
        var employee = _context.ExistingEmployee();
        var employment = _context.ExistingEmployment(employee.Id);
        employment.Terminate(EmployeeTestContext.Today, null);

        var interview = ExitInterview.Record(employment.Id, EmployeeTestContext.Today, "Eski sabab").Value;
        _context.ExitInterviews.GetByIdAsync(interview.Id, Arg.Any<CancellationToken>()).Returns(interview);

        var natija = await Tuzatish.HandleAsync(
            new UpdateExitInterviewCommand(interview.Id, EmployeeTestContext.Today, "Yangi sabab"),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.Reason.ShouldBe("Yangi sabab");
        natija.Value.EmploymentId.ShouldBe(employment.Id);
    }

    // ────────────────────── O'chirish ──────────────────────

    [Fact]
    public async Task Nomavjud_yozuv_ochirilmaydi()
    {
        var natija = await Ochirish.HandleAsync(
            new DeleteExitInterviewCommand(Guid.CreateVersion7()),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("qayta_aloqa.topilmadi");
    }

    [Fact]
    public async Task Mavjud_yozuv_ochiriladi()
    {
        var employee = _context.ExistingEmployee();
        var employment = _context.ExistingEmployment(employee.Id);
        employment.Terminate(EmployeeTestContext.Today, null);

        var interview = ExitInterview.Record(employment.Id, EmployeeTestContext.Today, "Sabab").Value;
        _context.ExitInterviews.GetByIdAsync(interview.Id, Arg.Any<CancellationToken>()).Returns(interview);

        var natija = await Ochirish.HandleAsync(
            new DeleteExitInterviewCommand(interview.Id),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        _context.ExitInterviews.Received(1).Remove(interview);
        await _context.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
