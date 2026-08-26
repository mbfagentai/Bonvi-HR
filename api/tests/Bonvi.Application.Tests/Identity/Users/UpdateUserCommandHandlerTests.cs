using Bonvi.Application.Abstractions;
using Bonvi.Application.Identity.Users.Commands.UpdateUser;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;
using NSubstitute;

namespace Bonvi.Application.Tests.Identity.Users;

/// <summary>
/// Foydalanuvchini tahrirlash mantig'ining testlari.
/// </summary>
/// <remarks>
/// Asosiy e'tibor — ikkita xavfsizlik himoyasiga: administrator o'zidan
/// admin rolini olib tashlay olmasligi va tizimdagi OXIRGI administrator
/// himoyalanishi kerak.
/// </remarks>
public sealed class UpdateUserCommandHandlerTests
{
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRoleRepository _roles = Substitute.For<IRoleRepository>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private UpdateUserCommandHandler Handler => new(_currentUser, _users, _roles, _auditLogger, _unitOfWork);

    private static Role AdminRole() => Role.Create(SystemRoles.Admin, "Administrator", isSystem: true).Value;

    private static Role HrRole() => Role.Create(SystemRoles.HrManager, "HR menejeri", isSystem: true).Value;

    private static User YangiFoydalanuvchi(string phone, string fullName)
    {
        var login = PhoneNumber.Create(phone).Value;
        var ism = FullName.Parse(fullName).Value;
        return User.Create(login, "xesh", ism).Value;
    }

    /// <summary>Joriy foydalanuvchini shu <paramref name="user"/> sifatida sozlaydi.</summary>
    private void ActingAs(User user)
    {
        _currentUser.UserId.Returns(user.Id);
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
    }

    [Fact]
    public async Task Boshqa_foydalanuvchining_rolini_ozgartirish_muvaffaqiyatli()
    {
        var admin = YangiFoydalanuvchi("905088888", "Тошматов Акбар");
        var adminRole = AdminRole();
        admin.AssignRole(adminRole.Id);
        ActingAs(admin);

        var target = YangiFoydalanuvchi("905088877", "Каримов Жасур");
        var hrRole = HrRole();
        target.AssignRole(hrRole.Id);
        _users.GetByIdAsync(target.Id, Arg.Any<CancellationToken>()).Returns(target);
        _users.LoginExistsAsync(Arg.Any<PhoneNumber>(), target.Id, Arg.Any<CancellationToken>()).Returns(false);

        _roles.GetByCodeAsync(SystemRoles.Admin, Arg.Any<CancellationToken>()).Returns(adminRole);
        _roles.GetByCodeAsync(SystemRoles.HrManager, Arg.Any<CancellationToken>()).Returns(hrRole);
        _roles.GetAllAsync(Arg.Any<CancellationToken>()).Returns([adminRole, hrRole]);

        var natija = await Handler.HandleAsync(
            new UpdateUserCommand(target.Id, "905088877", "Каримов", "Жасур", null, [SystemRoles.Admin]),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Message : null);
        natija.Value.RoleCodes.ShouldContain(SystemRoles.Admin);
        natija.Value.RoleCodes.ShouldNotContain(SystemRoles.HrManager);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Ozidan_admin_rolini_olib_tashlash_rad_etiladi_boshqa_admin_bolsa_ham()
    {
        var adminRole = AdminRole();

        var actingAdmin = YangiFoydalanuvchi("905088888", "Тошматов Акбар");
        actingAdmin.AssignRole(adminRole.Id);
        ActingAs(actingAdmin);

        var boshqaAdmin = YangiFoydalanuvchi("905011111", "Rahimov Sardor");
        boshqaAdmin.AssignRole(adminRole.Id);

        // O'zini o'zi tahrirlaydi — admin rolisiz qoldirmoqchi
        _users.GetByIdAsync(actingAdmin.Id, Arg.Any<CancellationToken>()).Returns(actingAdmin);
        _users.LoginExistsAsync(Arg.Any<PhoneNumber>(), actingAdmin.Id, Arg.Any<CancellationToken>()).Returns(false);
        _users.ListAsync(Arg.Any<CancellationToken>()).Returns([actingAdmin, boshqaAdmin]);

        var hrRole = HrRole();
        _roles.GetByCodeAsync(SystemRoles.HrManager, Arg.Any<CancellationToken>()).Returns(hrRole);
        _roles.GetAllAsync(Arg.Any<CancellationToken>()).Returns([adminRole, hrRole]);

        var natija = await Handler.HandleAsync(
            new UpdateUserCommand(
                actingAdmin.Id, "905088888", "Тошматов", "Акбар", null, [SystemRoles.HrManager]),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.ozini_adminlikdan_chetlashtirish");
        actingAdmin.RoleIds.ShouldContain(adminRole.Id, "hech narsa o'zgarmagan bo'lishi kerak");
    }

    [Fact]
    public async Task Oxirgi_administratordan_admin_rolini_olib_tashlash_rad_etiladi()
    {
        var adminRole = AdminRole();

        // Boshqa (masalan hr_manager) foydalanuvchi tahrirlamoqda — o'zini
        // o'zi bloklash bu yerda ishlamaydi, faqat "oxirgi admin" himoyasi ishlaydi
        var actor = YangiFoydalanuvchi("905000000", "Boshqa Amaldor");
        ActingAs(actor);

        var yagonaAdmin = YangiFoydalanuvchi("905088888", "Тошматов Акбар");
        yagonaAdmin.AssignRole(adminRole.Id);

        _users.GetByIdAsync(yagonaAdmin.Id, Arg.Any<CancellationToken>()).Returns(yagonaAdmin);
        _users.LoginExistsAsync(Arg.Any<PhoneNumber>(), yagonaAdmin.Id, Arg.Any<CancellationToken>()).Returns(false);

        // Ro'yxatda faqat shu bitta admin bor
        _users.ListAsync(Arg.Any<CancellationToken>()).Returns([yagonaAdmin]);

        var hrRole = HrRole();
        _roles.GetByCodeAsync(SystemRoles.HrManager, Arg.Any<CancellationToken>()).Returns(hrRole);
        _roles.GetAllAsync(Arg.Any<CancellationToken>()).Returns([adminRole, hrRole]);

        var natija = await Handler.HandleAsync(
            new UpdateUserCommand(
                yagonaAdmin.Id, "905088888", "Тошматов", "Акбар", null, [SystemRoles.HrManager]),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.oxirgi_administrator");
        yagonaAdmin.RoleIds.ShouldContain(adminRole.Id);
    }

    [Fact]
    public async Task Mavjud_bolmagan_foydalanuvchi_topilmadi_xatosini_beradi()
    {
        var actor = YangiFoydalanuvchi("905000000", "Boshqa Amaldor");
        ActingAs(actor);

        var yoqId = Guid.CreateVersion7();
        _users.GetByIdAsync(yoqId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var natija = await Handler.HandleAsync(
            new UpdateUserCommand(yoqId, "905088877", "Каримов", "Жасур", null, [SystemRoles.HrManager]),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.topilmadi");
    }

    [Fact]
    public async Task Band_loginga_ozgartirish_rad_etiladi()
    {
        var actor = YangiFoydalanuvchi("905000000", "Boshqa Amaldor");
        ActingAs(actor);

        var target = YangiFoydalanuvchi("905088877", "Каримов Жасур");
        _users.GetByIdAsync(target.Id, Arg.Any<CancellationToken>()).Returns(target);
        _users.LoginExistsAsync(Arg.Any<PhoneNumber>(), target.Id, Arg.Any<CancellationToken>()).Returns(true);

        var natija = await Handler.HandleAsync(
            new UpdateUserCommand(target.Id, "905011111", "Каримов", "Жасур", null, [SystemRoles.HrManager]),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.login_band");
    }
}
