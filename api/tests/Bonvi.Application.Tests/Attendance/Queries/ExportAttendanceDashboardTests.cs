using Bonvi.Application.Abstractions;
using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Attendance.Queries;
using Bonvi.Application.Reports.Export;
using Bonvi.Application.Reports.Queries;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using NSubstitute;

namespace Bonvi.Application.Tests.Attendance.Queries;

/// <summary>
/// «Statistika» jadvalining Excel eksporti — filtr, chegara va avtomatik «ketdi» (handler darajasi).
/// </summary>
/// <remarks>
/// Uchi-uchiga sinovlar (<c>AttendanceDashboardExportApiTests</c>) haqiqiy soat bilan ishlaydi va
/// o'tishsiz holatlarni tekshiradi; bu yerda — o'qish modeli soxta, ya'ni o'sha yerda yasab
/// bo'lmaydigan qatorlar (22:00 dagi avtomatik chiqish) va 5 000 dan katta jadval.
/// </remarks>
public sealed class ExportAttendanceDashboardTests
{
    private static readonly DateOnly Bugun = new(2026, 10, 22);

    private readonly IAttendanceReadModel _readModel = Substitute.For<IAttendanceReadModel>();
    private readonly IExcelTableWriter _writer = Substitute.For<IExcelTableWriter>();
    private readonly IAuditLogger _audit = Substitute.For<IAuditLogger>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private IReadOnlyList<ExcelColumn<AttendanceDashboardItemDto>>? _ustunlar;

    public ExportAttendanceDashboardTests()
    {
        _clock.Today.Returns(Bugun);

        _writer
            .Write(
                Arg.Any<string>(),
                Arg.Do<IReadOnlyList<ExcelColumn<AttendanceDashboardItemDto>>>(columns => _ustunlar = columns),
                Arg.Any<IReadOnlyList<AttendanceDashboardItemDto>>())
            .Returns([1, 2, 3]);
    }

    private ExportAttendanceDashboardQueryHandler Handler => new(_readModel, _writer, _audit, _unitOfWork, _clock);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Ekrandagi_filtr_sahifasiz_soraladi_va_fayl_nomida_jadval_sanasi()
    {
        var sana = new DateOnly(2026, 10, 15);
        Jadval(sana, totalItems: 1, Qator(leftAt: null, leftAuto: false));

        var natija = await Handler.HandleAsync(
            new GetAttendanceDashboardQuery(sana, "  Anvar ", Status: AttendanceDashboardStatus.Late, Sort: "-name", Page: 3, PageSize: 20),
            Ct);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.FileName.ShouldBe("Bonvi_davomat_statistika_2026-10-15.xlsx");

        // Qidiruv va holat — jadvalniki (serverda), sahifa — birinchi, chegara + 1
        await _readModel.Received(1).GetDashboardAsync(
            Arg.Is<DashboardFilter>(filter =>
                filter.Date == sana
                && filter.Search == "Anvar"
                && filter.Status == AttendanceDashboardStatus.Late
                && filter.Descending
                && filter.Page == 1
                && filter.PageSize == ExcelExport.MaxRows + 1),
            Arg.Any<CancellationToken>());

        // Hamma argument matcher bilan (`ExportIncentivesTests` dagi izoh)
        _audit.Received(1).Record(
            Arg.Is(AuditAction.Export),
            Arg.Is("AttendanceDay"),
            Arg.Is<string?>(id => id == null),
            Arg.Any<string?>(),
            Arg.Is<string?>(detail => detail != null && !detail.Contains("Anvar", StringComparison.Ordinal)));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Chegaradan_oshsa_rad_etiladi_fayl_ham_jurnal_ham_yozilmaydi()
    {
        Jadval(Bugun, totalItems: ExcelExport.MaxRows + 1);

        var natija = await Handler.HandleAsync(new GetAttendanceDashboardQuery(), Ct);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("eksport.juda_kop");
        _writer.DidNotReceiveWithAnyArgs().Write<AttendanceDashboardItemDto>(default!, default!, default!);
        _audit.DidNotReceiveWithAnyArgs().Record(default, default!, default);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Jadvalsiz kunning avtomatik 22:00 dagi «ketdi»si haqiqiy chiqish emas — ekrandagi «avto»
    /// izohi faylda ham bor; haqiqiy chiqish esa Excel vaqti bo'lib qoladi.
    /// </summary>
    [Fact]
    public async Task Avtomatik_chiqish_avto_izohi_bilan_haqiqiysi_vaqt_bolib()
    {
        var avto = new DateTimeOffset(2026, 10, 15, 17, 0, 0, TimeSpan.Zero);
        var haqiqiy = new DateTimeOffset(2026, 10, 15, 13, 5, 0, TimeSpan.Zero);
        var avtoQator = Qator(avto, leftAuto: true);
        var haqiqiyQator = Qator(haqiqiy, leftAuto: false);
        Jadval(Bugun, totalItems: 2, avtoQator, haqiqiyQator);

        (await Handler.HandleAsync(new GetAttendanceDashboardQuery(), Ct)).IsSuccess.ShouldBeTrue();

        var ketgan = _ustunlar.ShouldNotBeNull().Single(column => column.Header == "Ketgan");
        ketgan.Value(avtoQator).ShouldBe("15.10.2026 22:00 (avto)", "Toshkent vaqtida");
        ketgan.Value(haqiqiyQator).ShouldBe(haqiqiy);

        var holat = _ustunlar.Single(column => column.Header == "Holat");
        holat.Value(avtoQator).ShouldBe("Keldi");
    }

    private void Jadval(DateOnly sana, int totalItems, params AttendanceDashboardItemDto[] qatorlar)
        => _readModel
            .GetDashboardAsync(Arg.Any<DashboardFilter>(), Arg.Any<CancellationToken>())
            .Returns(new DashboardPage(
                sana,
                IsToday: false,
                DateTimeOffset.UtcNow,
                Total: totalItems,
                new AttendanceDashboardCountsDto(0, 0, 0, 0, 0, 0, 0, totalItems),
                qatorlar,
                totalItems));

    private static AttendanceDashboardItemDto Qator(DateTimeOffset? leftAt, bool leftAuto)
        => new(
            Guid.NewGuid(),
            "Karimov Anvar",
            PhotoKey: null,
            PositionName: "Operator",
            DepartmentName: "Sex",
            ArrivedAt: leftAt?.AddHours(-8),
            leftAt,
            AttendanceDashboardStatus.Arrived,
            LateMinutes: null,
            leftAuto);
}
