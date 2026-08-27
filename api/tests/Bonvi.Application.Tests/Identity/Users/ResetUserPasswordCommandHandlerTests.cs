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
/// qilinishiga, parolning hech qachon javobga/jurnalga tushmasligiga va
/// «kim kimga tega oladi» qoidasiga (parolni tiklash hisobni egallab olish
/// bilan barobar).
/// </remarks>
public sealed class ResetUserPasswordCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 9, 0, 0, TimeSpan.FromHours(5));

    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRoleRepository _roles = Substitute.For<IRoleRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private readonly Role _superAdminRole =
        Role.Create(SystemRoles.SuperAdmin, "Super administrator", isSystem: true).Value;

    private readonly Role _mainBossRole =
        Role.Create(SystemRoles.MainBoss, "Bosh rahbar", isSystem: true).Value;

    private readonly Role _hrRole =
        Role.Create(SystemRoles.HrManager, "HR menejeri", isSystem: true).Value;

    private ResetUserPasswordCommandHandler Handler
        => new(_currentUser, _users, _roles, _refreshTokens, _hasher, _auditLogger, _unitOfWork, _clock);

    public ResetUserPasswordCommandHandlerTests()
    {
        _clock.UtcNow.Returns(Now);
        _roles.GetAllAsync(Arg.Any<CancellationToken>()).Returns([_superAdminRole, _mainBossRole, _hrRole]);
    }

    private static User YangiFoydalanuvchi(string phone, string fullName)
    {
        var login = PhoneNumber.Create(phone).Value;
        var ism = FullName.Parse(fullName).Value;
        return User.Create(login, "eski_xesh", ism).Value;
    }

    private User Mavjud(string phone, string fullName, Role role)
    {
        var user = YangiFoydalanuvchi(phone, fullName);
        user.SetRole(role.Id);
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        return user;
    }

    private void ActingAs(User user) => _currentUser.UserId.Returns(user.Id);

    /// <summary>Har bir sinovda so'rovni super administrator yuboradi.</summary>
    private User SuperAdminSifatida()
    {
        var acting = Mavjud("905088888", "Тошматов Акбар", _superAdminRole);
        ActingAs(acting);
        return acting;
    }

    [Fact]
    public async Task Parol_tiklanganda_yangi_xesh_saqlanadi()
    {
        SuperAdminSifatida();
        var user = Mavjud("905088877", "Каримов Жасур", _hrRole);
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
        SuperAdminSifatida();
        var user = Mavjud("905088877", "Каримов Жасур", _hrRole);

        await Handler.HandleAsync(
            new ResetUserPasswordCommand(user.Id, "Yangi_Parol1"),
            TestContext.Current.CancellationToken);

        await _refreshTokens.Received(1).RevokeAllForUserAsync(user.Id, Now, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Qisqa_parol_rad_etiladi_va_sessiyalar_tegilmaydi()
    {
        SuperAdminSifatida();
        var user = Mavjud("905088877", "Каримов Жасур", _hrRole);

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
        SuperAdminSifatida();
        var user = Mavjud("905088877", "Каримов Жасур", _hrRole);

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

    /// <summary>
    /// Bosh rahbar super administratorning parolini tiklay olsa, uning
    /// o'rniga kirib olardi — bu «faqat HR menejerlarga tegadi» qoidasining
    /// eng xavfli teshigi bo'lardi.
    /// </summary>
    [Fact]
    public async Task Bosh_rahbar_super_administrator_parolini_tiklay_olmaydi()
    {
        var acting = Mavjud("905011111", "Rahimov Sardor", _mainBossRole);
        ActingAs(acting);

        var superAdmin = Mavjud("905088888", "Тошматов Акбар", _superAdminRole);

        var natija = await Handler.HandleAsync(
            new ResetUserPasswordCommand(superAdmin.Id, "Yangi_Parol1"),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.rolga_haqqi_yoq");
        superAdmin.PasswordHash.ShouldBe("eski_xesh");
        await _refreshTokens.DidNotReceive().RevokeAllForUserAsync(
            Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Bosh_rahbar_hr_menejer_parolini_tiklay_oladi()
    {
        var acting = Mavjud("905011111", "Rahimov Sardor", _mainBossRole);
        ActingAs(acting);

        var hrUser = Mavjud("905088877", "Каримов Жасур", _hrRole);

        var natija = await Handler.HandleAsync(
            new ResetUserPasswordCommand(hrUser.Id, "Yangi_Parol1"),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Message : null);
    }

    [Fact]
    public async Task Mavjud_bolmagan_foydalanuvchi_topilmadi_xatosini_beradi()
    {
        SuperAdminSifatida();

        var yoqId = Guid.CreateVersion7();
        _users.GetByIdAsync(yoqId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var natija = await Handler.HandleAsync(
            new ResetUserPasswordCommand(yoqId, "Yangi_Parol1"),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.topilmadi");
    }
}
