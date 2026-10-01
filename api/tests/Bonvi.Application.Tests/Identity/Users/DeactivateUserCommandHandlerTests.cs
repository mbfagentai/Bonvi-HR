using Bonvi.Application.Abstractions;
using Bonvi.Application.Identity.Users.Commands.DeactivateUser;
using Bonvi.Application.Mcp;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;
using NSubstitute;

namespace Bonvi.Application.Tests.Identity.Users;

/// <summary>
/// Foydalanuvchini faolsizlantirish mantig'ining testlari — uchta himoyaga
/// qaratilgan: kim kimga tega oladi, oxirgi rahbar va o'zini o'zi bloklash.
/// </summary>
public sealed class DeactivateUserCommandHandlerTests
{
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRoleRepository _roles = Substitute.For<IRoleRepository>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IMcpGrantRevoker _mcpGrants = Substitute.For<IMcpGrantRevoker>();

    private readonly Role _superAdminRole =
        TestRoles.Standart(SystemRoles.SuperAdmin);

    private readonly Role _mainBossRole =
        TestRoles.Standart(SystemRoles.MainBoss);

    private readonly Role _hrRole =
        TestRoles.Standart(SystemRoles.HrManager);

    private DeactivateUserCommandHandler Handler => new(_currentUser, _users, _roles, _auditLogger, _unitOfWork, _mcpGrants);

    public DeactivateUserCommandHandlerTests()
        => _roles.GetAllAsync(Arg.Any<CancellationToken>()).Returns([_superAdminRole, _mainBossRole, _hrRole]);

    private static User YangiFoydalanuvchi(string phone, string fullName)
    {
        var login = PhoneNumber.Create(phone).Value;
        var ism = FullName.Parse(fullName).Value;
        return User.Create(login, "xesh", ism).Value;
    }

    private User Mavjud(string phone, string fullName, Role role)
    {
        var user = YangiFoydalanuvchi(phone, fullName);
        user.SetRole(role.Id);
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        return user;
    }

    private void ActingAs(User user) => _currentUser.UserId.Returns(user.Id);

    [Fact]
    public async Task Rahbar_ozini_ozi_faolsizlantira_olmaydi()
    {
        var acting = Mavjud("905088888", "Тошматов Акбар", _superAdminRole);
        ActingAs(acting);

        var boshqaRahbar = YangiFoydalanuvchi("905011111", "Rahimov Sardor");
        boshqaRahbar.SetRole(_mainBossRole.Id);

        _users.ListAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns([acting, boshqaRahbar]);

        var natija = await Handler.HandleAsync(
            new DeactivateUserCommand(acting.Id),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.ozini_blokash");
        acting.IsActive.ShouldBeTrue();
        await _mcpGrants.DidNotReceive().RevokeAllForUserAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Oxirgi_faol_rahbar_faolsizlantirilmaydi()
    {
        var yagonaRahbar = Mavjud("905088888", "Тошматов Акбар", _superAdminRole);
        ActingAs(yagonaRahbar);

        _users.ListAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns([yagonaRahbar]);

        var natija = await Handler.HandleAsync(
            new DeactivateUserCommand(yagonaRahbar.Id),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.oxirgi_administrator");
        yagonaRahbar.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Super_administrator_bosh_rahbarni_faolsizlantira_oladi()
    {
        var acting = Mavjud("905088888", "Тошматов Акбар", _superAdminRole);
        ActingAs(acting);

        var boshRahbar = Mavjud("905011111", "Rahimov Sardor", _mainBossRole);
        _users.ListAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns([acting, boshRahbar]);

        var natija = await Handler.HandleAsync(
            new DeactivateUserCommand(boshRahbar.Id),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Message : null);
        boshRahbar.IsActive.ShouldBeFalse();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Bosh_rahbar_super_administratorni_faolsizlantira_olmaydi()
    {
        var acting = Mavjud("905011111", "Rahimov Sardor", _mainBossRole);
        ActingAs(acting);

        var superAdmin = Mavjud("905088888", "Тошматов Акбар", _superAdminRole);
        _users.ListAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns([acting, superAdmin]);

        var natija = await Handler.HandleAsync(
            new DeactivateUserCommand(superAdmin.Id),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.rolga_haqqi_yoq");
        superAdmin.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Hr_menejerini_faolsizlantirish_cheklovsiz_utadi()
    {
        var acting = Mavjud("905011111", "Rahimov Sardor", _mainBossRole);
        ActingAs(acting);

        var hrUser = Mavjud("905088877", "Каримов Жасур", _hrRole);
        _users.ListAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns([acting, hrUser]);

        var natija = await Handler.HandleAsync(
            new DeactivateUserCommand(hrUser.Id),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Message : null);
        hrUser.IsActive.ShouldBeFalse();

        // AI (MCP) ulanishlari ham yopiladi — hisob qayta faollashtirilganda
        // eski ulanish yangi roziliksiz «tirilib» qolmasin.
        await _mcpGrants.Received(1).RevokeAllForUserAsync(hrUser.Id, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Mavjud_bolmagan_foydalanuvchi_topilmadi_xatosini_beradi()
    {
        var acting = Mavjud("905088888", "Тошматов Акбар", _superAdminRole);
        ActingAs(acting);

        var yoqId = Guid.CreateVersion7();
        _users.GetByIdAsync(yoqId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var natija = await Handler.HandleAsync(
            new DeactivateUserCommand(yoqId),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.topilmadi");
    }
}
