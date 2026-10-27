using Bonvi.Application.Abstractions;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Me;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Bonvi.Application.Tests.Me;

/// <summary>
/// «Jamoam» ko'rish auditi — takror yozilmaslik oynasi va «audit yiqilsa javob buzilmaydi» (SPEC-jamoam §C6).
/// </summary>
public sealed class TeamViewAuditTests
{
    private static readonly DateTimeOffset Boshi = new(2026, 10, 27, 4, 0, 0, TimeSpan.Zero);

    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ITeamViewJournal _journal = Substitute.For<ITeamViewJournal>();
    private readonly TeamViewWindow _window;

    private readonly CurrentEmployeeIds _boshliq = new(Guid.NewGuid(), Guid.NewGuid());

    public TeamViewAuditTests()
    {
        _clock.UtcNow.Returns(Boshi);
        _window = new TeamViewWindow(_clock);
    }

    private TeamViewAudit Audit => new(_auditLogger, _unitOfWork, _window, _journal);

    [Fact]
    public async Task Azoni_korish_oy_boyicha_10_daqiqada_bir_marta_yoziladi_boshqa_oy_va_azo_alohida()
    {
        var azo = Guid.NewGuid();
        var boshqa = Guid.NewGuid();

        await Audit.MemberViewedAsync(_boshliq, azo, 2026, 10);
        await Audit.MemberViewedAsync(_boshliq, azo, 2026, 10);  // o'sha oyni yangilash — yozilmaydi
        await Audit.MemberViewedAsync(_boshliq, azo, 2026, 9);   // boshqa oy — boshqa ochilgan narsa
        await Audit.MemberViewedAsync(_boshliq, boshqa, 2026, 10);

        Yozuvlar("Employee", azo).ShouldBe(
        [
            """{"action":"team_member_viewed","year":2026,"month":10}""",
            """{"action":"team_member_viewed","year":2026,"month":9}""",
        ]);
        Yozuvlar("Employee", boshqa).Count.ShouldBe(1);

        // 9 daqiqa 59 soniya — hali oynada; 10 daqiqa — yangi qator.
        _clock.UtcNow.Returns(Boshi + TeamViewWindow.MemberWindow - TimeSpan.FromSeconds(1));
        await Audit.MemberViewedAsync(_boshliq, azo, 2026, 10);
        Yozuvlar("Employee", azo).Count.ShouldBe(2);

        _clock.UtcNow.Returns(Boshi + TeamViewWindow.MemberWindow);
        await Audit.MemberViewedAsync(_boshliq, azo, 2026, 10);
        Yozuvlar("Employee", azo).Count.ShouldBe(3);
    }

    /// <summary>
    /// Panel — sana va bo'lim bo'yicha 30 daqiqada bir marta; holat va qidiruv yangi qator emas,
    /// qidiruv MATNI esa umuman yo'q (faqat «searched»).
    /// </summary>
    [Fact]
    public async Task Panelni_korish_sana_va_bolim_boyicha_30_daqiqada_bir_marta_qidiruv_matnisiz()
    {
        var bugun = new DateOnly(2026, 10, 27);
        var sex = Guid.NewGuid();

        await Audit.BoardViewedAsync(_boshliq, bugun, null, null, searched: false);

        // O'sha panelning filtri — holat va qidiruv yangi qator emas.
        _clock.UtcNow.Returns(Boshi + TimeSpan.FromMinutes(29));
        await Audit.BoardViewedAsync(_boshliq, bugun, null, AttendanceDashboardStatus.Absent, searched: true);

        // Boshqa sana va boshqa bo'lim — boshqa ochilgan narsa.
        await Audit.BoardViewedAsync(_boshliq, bugun.AddDays(-1), null, null, searched: false);
        await Audit.BoardViewedAsync(_boshliq, bugun, sex, AttendanceDashboardStatus.Late, searched: true);

        Yozuvlar("EmployeeAccount", _boshliq.AccountId).ShouldBe(
        [
            """{"action":"team_board_viewed","date":"2026-10-27","departmentId":null,"status":null,"searched":false}""",
            """{"action":"team_board_viewed","date":"2026-10-26","departmentId":null,"status":null,"searched":false}""",
            $$"""{"action":"team_board_viewed","date":"2026-10-27","departmentId":"{{sex}}","status":"Late","searched":true}""",
        ]);

        _clock.UtcNow.Returns(Boshi + TeamViewWindow.BoardWindow);
        await Audit.BoardViewedAsync(_boshliq, bugun, null, null, searched: false);

        Yozuvlar("EmployeeAccount", _boshliq.AccountId).Count.ShouldBe(4);
    }

    [Fact]
    public async Task Audit_yiqilsa_jurnalga_yoziladi_va_keyingi_sorov_qayta_urinadi()
    {
        var azo = Guid.NewGuid();
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("baza yo'q"));

        // Istisno TASHQARIGA chiqmaydi — o'qish javobi buzilmaydi.
        await Should.NotThrowAsync(() => Audit.MemberViewedAsync(_boshliq, azo, 2026, 10));

        _journal.Received(1).AuditNotRecorded("team_member_viewed", Arg.Any<InvalidOperationException>());

        // Oyna egallanmay qoldi: yozuv tushmagan, ya'ni keyingi ko'rish yana urinadi.
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        await Audit.MemberViewedAsync(_boshliq, azo, 2026, 10);

        Yozuvlar("Employee", azo).Count.ShouldBe(2);
    }

    /// <summary>Navbatga qo'yilgan ko'rish yozuvlarining tafsilotlari — shu obyekt bo'yicha.</summary>
    private List<string?> Yozuvlar(string entityName, Guid entityId)
        => [
            .. _auditLogger.ReceivedCalls()
                .Where(call => call.GetMethodInfo().Name == nameof(IAuditLogger.Record))
                .Select(call => call.GetArguments())
                .Where(args =>
                    (AuditAction)args[0]! == AuditAction.Export &&
                    (string)args[1]! == entityName &&
                    (string?)args[2] == entityId.ToString() &&
                    args[3] is null)
                .Select(args => (string?)args[4]),
        ];
}
