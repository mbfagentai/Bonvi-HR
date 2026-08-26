using Bonvi.Application.Abstractions;
using Bonvi.Application.Identity.Users.Commands.DeactivateUser;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;
using NSubstitute;

namespace Bonvi.Application.Tests.Identity.Users;

/// <summary>
/// Foydalanuvchini faolsizlantirish mantig'ining testlari — asosan ikkita
/// xavfsizlik himoyasiga qaratilgan: o'zini o'zi bloklash va oxirgi admin.
/// </summary>
public sealed class DeactivateUserCommandHandlerTests
{
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRoleRepository _roles = Substitute.For<IRoleRepository>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private DeactivateUserCommandHandler Handler => new(_currentUser, _users, _roles, _auditLogger, _unitOfWork);

    private static Role AdminRole() => Role.Create(SystemRoles.Admin, "Administrator", isSystem: true).Value;

    private static User YangiFoydalanuvchi(string phone, string fullName)
    {
        var login = PhoneNumber.Create(phone).Value;
        var ism = FullName.Parse(fullName).Value;
        return User.Create(login, "xesh", ism).Value;
    }

    private void ActingAs(User user)
    {
        _currentUser.UserId.Returns(user.Id);
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
    }

    [Fact]
    public async Task Administrator_ozini_ozi_faolsizlantira_olmaydi()
    {
        var adminRole = AdminRole();
        var actingAdmin = YangiFoydalanuvchi("905088888", "Тошматов Акбар");
        actingAdmin.AssignRole(adminRole.Id);
        ActingAs(actingAdmin);

        var boshqaAdmin = YangiFoydalanuvchi("905011111", "Rahimov Sardor");
        boshqaAdmin.AssignRole(adminRole.Id);

        _roles.GetAllAsync(Arg.Any<CancellationToken>()).Returns([adminRole]);
        _users.ListAsync(Arg.Any<CancellationToken>()).Returns([actingAdmin, boshqaAdmin]);

        var natija = await Handler.HandleAsync(
            new DeactivateUserCommand(actingAdmin.Id),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.ozini_blokash");
        actingAdmin.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Oxirgi_faol_administrator_faolsizlantirilmaydi()
    {
        var adminRole = AdminRole();

        var actor = YangiFoydalanuvchi("905000000", "Boshqa Amaldor");
        ActingAs(actor);

        var yagonaAdmin = YangiFoydalanuvchi("905088888", "Тошматов Акбар");
        yagonaAdmin.AssignRole(adminRole.Id);

        _users.GetByIdAsync(yagonaAdmin.Id, Arg.Any<CancellationToken>()).Returns(yagonaAdmin);
        _roles.GetAllAsync(Arg.Any<CancellationToken>()).Returns([adminRole]);
        _users.ListAsync(Arg.Any<CancellationToken>()).Returns([yagonaAdmin]);

        var natija = await Handler.HandleAsync(
            new DeactivateUserCommand(yagonaAdmin.Id),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.oxirgi_administrator");
        yagonaAdmin.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Ikkinchi_administrator_muvaffaqiyatli_faolsizlantiriladi()
    {
        var adminRole = AdminRole();

        var actor = YangiFoydalanuvchi("905000000", "Boshqa Amaldor");
        ActingAs(actor);

        var birinchiAdmin = YangiFoydalanuvchi("905088888", "Тошматов Акбар");
        birinchiAdmin.AssignRole(adminRole.Id);
        var ikkinchiAdmin = YangiFoydalanuvchi("905011111", "Rahimov Sardor");
        ikkinchiAdmin.AssignRole(adminRole.Id);

        _users.GetByIdAsync(ikkinchiAdmin.Id, Arg.Any<CancellationToken>()).Returns(ikkinchiAdmin);
        _roles.GetAllAsync(Arg.Any<CancellationToken>()).Returns([adminRole]);
        _users.ListAsync(Arg.Any<CancellationToken>()).Returns([birinchiAdmin, ikkinchiAdmin]);

        var natija = await Handler.HandleAsync(
            new DeactivateUserCommand(ikkinchiAdmin.Id),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Message : null);
        ikkinchiAdmin.IsActive.ShouldBeFalse();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Hr_menejerini_faolsizlantirish_cheklovsiz_utadi()
    {
        var actor = YangiFoydalanuvchi("905000000", "Boshqa Amaldor");
        ActingAs(actor);

        var hrRole = Role.Create(SystemRoles.HrManager, "HR menejeri", isSystem: true).Value;
        var hrUser = YangiFoydalanuvchi("905088877", "Каримов Жасур");
        hrUser.AssignRole(hrRole.Id);

        _users.GetByIdAsync(hrUser.Id, Arg.Any<CancellationToken>()).Returns(hrUser);
        _roles.GetAllAsync(Arg.Any<CancellationToken>()).Returns([hrRole]);

        var natija = await Handler.HandleAsync(
            new DeactivateUserCommand(hrUser.Id),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Message : null);
        hrUser.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Mavjud_bolmagan_foydalanuvchi_topilmadi_xatosini_beradi()
    {
        var actor = YangiFoydalanuvchi("905000000", "Boshqa Amaldor");
        ActingAs(actor);

        var yoqId = Guid.CreateVersion7();
        _users.GetByIdAsync(yoqId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var natija = await Handler.HandleAsync(
            new DeactivateUserCommand(yoqId),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.topilmadi");
    }
}
