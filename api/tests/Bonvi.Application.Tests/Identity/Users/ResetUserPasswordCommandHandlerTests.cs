using Bonvi.Application.Abstractions;
using Bonvi.Application.Identity.Abstractions;
using Bonvi.Application.Identity.Users.Commands.ResetUserPassword;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;
using NSubstitute;

namespace Bonvi.Application.Tests.Identity.Users;

/// <summary>
/// Administrator tomonidan parolni tiklash mantig'ining testlari.
/// </summary>
/// <remarks>
/// Asosiy e'tibor — parol tiklanganda mavjud sessiyalarning bekor
/// qilinishiga va parolning hech qachon javobga/jurnalga tushmasligiga.
/// </remarks>
public sealed class ResetUserPasswordCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 9, 0, 0, TimeSpan.FromHours(5));

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private ResetUserPasswordCommandHandler Handler
        => new(_users, _refreshTokens, _hasher, _auditLogger, _unitOfWork, _clock);

    private static User FaolFoydalanuvchi()
    {
        var login = PhoneNumber.Create("905088877").Value;
        var ism = FullName.Parse("Каримов Жасур").Value;
        return User.Create(login, "eski_xesh", ism).Value;
    }

    public ResetUserPasswordCommandHandlerTests() => _clock.UtcNow.Returns(Now);

    [Fact]
    public async Task Parol_tiklanganda_yangi_xesh_saqlanadi()
    {
        var user = FaolFoydalanuvchi();
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Hash("Yangi_Parol1").Returns("yangi_xesh");

        var natija = await Handler.HandleAsync(
            new ResetUserPasswordCommand(user.Id, "Yangi_Parol1"),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Message : null);
        user.PasswordHash.ShouldBe("yangi_xesh");
        user.PasswordHash.ShouldNotBe("Yangi_Parol1");
    }

    [Fact]
    public async Task Parol_tiklanganda_barcha_sessiyalar_bekor_qilinadi()
    {
        var user = FaolFoydalanuvchi();
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        await Handler.HandleAsync(
            new ResetUserPasswordCommand(user.Id, "Yangi_Parol1"),
            TestContext.Current.CancellationToken);

        await _refreshTokens.Received(1).RevokeAllForUserAsync(user.Id, Now, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Qisqa_parol_rad_etiladi_va_sessiyalar_tegilmaydi()
    {
        var user = FaolFoydalanuvchi();
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var natija = await Handler.HandleAsync(
            new ResetUserPasswordCommand(user.Id, "qisqa"),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.parol_qisqa");
        user.PasswordHash.ShouldBe("eski_xesh");
        await _refreshTokens.DidNotReceive().RevokeAllForUserAsync(
            Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Parol_audit_yozuviga_hech_qachon_yozilmaydi()
    {
        var user = FaolFoydalanuvchi();
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        await Handler.HandleAsync(
            new ResetUserPasswordCommand(user.Id, "Maxfiy_Parol1"),
            TestContext.Current.CancellationToken);

        _auditLogger.Received(1).Record(
            Arg.Any<Domain.Auditing.AuditAction>(),
            "User",
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Is<string?>(json => json != null && !json.Contains("Maxfiy_Parol1", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Mavjud_bolmagan_foydalanuvchi_topilmadi_xatosini_beradi()
    {
        var yoqId = Guid.CreateVersion7();
        _users.GetByIdAsync(yoqId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var natija = await Handler.HandleAsync(
            new ResetUserPasswordCommand(yoqId, "Yangi_Parol1"),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.topilmadi");
    }
}
