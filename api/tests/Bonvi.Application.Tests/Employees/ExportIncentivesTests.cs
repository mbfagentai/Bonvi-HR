using Bonvi.Application.Abstractions;
using Bonvi.Application.Employees.Abstractions;
using Bonvi.Application.Employees.Incentives;
using Bonvi.Application.Reports.Abstractions;
using Bonvi.Application.Reports.Queries;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using NSubstitute;

namespace Bonvi.Application.Tests.Employees;

/// <summary>Rag'bat/jarima Excel eksporti — chegara va jurnal.</summary>
public sealed class ExportIncentivesTests
{
    private readonly IIncentiveReadModel _readModel = Substitute.For<IIncentiveReadModel>();
    private readonly IIncentiveExcelWriter _writer = Substitute.For<IIncentiveExcelWriter>();
    private readonly IAuditLogger _audit = Substitute.For<IAuditLogger>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private ExportIncentivesQueryHandler Handler => new(_readModel, _writer, _audit, _unitOfWork, _clock);

    [Fact]
    public async Task Chegaradan_oshsa_rad_etiladi_fayl_ham_jurnal_ham_yozilmaydi()
    {
        // Fayl jimgina qirqilmaydi: HR 5000 dan ortiq yozuvning bir qismini
        // «hammasi» deb olib qolmasligi kerak.
        _readModel.ListIncentivesAsync(Arg.Any<IncentiveListFilter>(), Arg.Any<CancellationToken>())
            .Returns(new IncentiveListPage([], ExportIncentivesQueryHandler.MaxExportRows + 1));

        var natija = await Handler.HandleAsync(new ListIncentivesQuery(), TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("eksport.juda_kop");
        _writer.DidNotReceive().Write(Arg.Any<IReadOnlyList<IncentiveListRow>>());
        _audit.DidNotReceive().Record(
            Arg.Any<AuditAction>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Chegara_ichida_fayl_yoziladi_va_ortiqcha_bitta_qator_soraladi()
    {
        _readModel.ListIncentivesAsync(Arg.Any<IncentiveListFilter>(), Arg.Any<CancellationToken>())
            .Returns(new IncentiveListPage([], ExportIncentivesQueryHandler.MaxExportRows));
        _writer.Write(Arg.Any<IReadOnlyList<IncentiveListRow>>()).Returns([1, 2, 3]);

        var natija = await Handler.HandleAsync(new ListIncentivesQuery(), TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.Content.ShouldBe([1, 2, 3]);

        // Sahifasiz: birinchi sahifa, chegara + 1 — oshganini bilish uchun.
        await _readModel.Received(1).ListIncentivesAsync(
            Arg.Is<IncentiveListFilter>(filter =>
                filter.Page == 1 && filter.PageSize == ExportIncentivesQueryHandler.MaxExportRows + 1),
            Arg.Any<CancellationToken>());
        // Hamma argument matcher bilan: NSubstitute `null` literalini matcher'lar
        // orasida aralashtirsa, ularni boshqa o'ringa surib yuboradi.
        _audit.Received(1).Record(
            Arg.Is(AuditAction.Export),
            Arg.Is("Incentive"),
            Arg.Is<string?>(id => id == null),
            Arg.Any<string?>(),
            Arg.Is<string?>(detail => detail != null && detail.Contains("\"rows\":0", StringComparison.Ordinal)));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
