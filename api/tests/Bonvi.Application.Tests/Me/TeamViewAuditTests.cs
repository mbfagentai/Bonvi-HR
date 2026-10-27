using Bonvi.Application.Abstractions;
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
    public async Task Azoni_korish_10_daqiqada_bir_marta_yoziladi_boshqa_azo_alohida()
    {
        var azo = Guid.NewGuid();
        var boshqa = Guid.NewGuid();

        await Audit.MemberViewedAsync(_boshliq, azo, 2026, 10);
        await Audit.MemberViewedAsync(_boshliq, azo, 2026, 9);   // boshqa oy — baribir o'sha juftlik
        await Audit.MemberViewedAsync(_boshliq, boshqa, 2026, 10);

        Yozuvlar("Employee", azo).ShouldBe(["""{"action":"team_member_viewed","year":2026,"month":10}"""]);
        Yozuvlar("Employee", boshqa).Count.ShouldBe(1);

        // 9 daqiqa 59 soniya — hali oynada; 10 daqiqa — yangi qator.
        _clock.UtcNow.Returns(Boshi + TeamViewWindow.MemberWindow - TimeSpan.FromSeconds(1));
        await Audit.MemberViewedAsync(_boshliq, azo, 2026, 10);
        Yozuvlar("Employee", azo).Count.ShouldBe(1);

        _clock.UtcNow.Returns(Boshi + TeamViewWindow.MemberWindow);
        await Audit.MemberViewedAsync(_boshliq, azo, 2026, 10);
        Yozuvlar("Employee", azo).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Panelni_korish_30_daqiqada_bir_marta_hisob_ustida_yoziladi()
    {
        await Audit.BoardViewedAsync(_boshliq);

        _clock.UtcNow.Returns(Boshi + TimeSpan.FromMinutes(29));
        await Audit.BoardViewedAsync(_boshliq);

        Yozuvlar("EmployeeAccount", _boshliq.AccountId).ShouldBe(["""{"action":"team_board_viewed"}"""]);

        _clock.UtcNow.Returns(Boshi + TeamViewWindow.BoardWindow);
        await Audit.BoardViewedAsync(_boshliq);

        Yozuvlar("EmployeeAccount", _boshliq.AccountId).Count.ShouldBe(2);
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
