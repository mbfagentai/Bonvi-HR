using Bonvi.Application.Abstractions;
using Bonvi.Application.Identity.Users.Commands.ActivateUser;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;
using NSubstitute;

namespace Bonvi.Application.Tests.Identity.Users;

/// <summary>
/// Foydalanuvchini qayta faollashtirish mantig'ining testlari.
/// </summary>
/// <remarks>
/// Faollashtirish — hisoblar sonini OSHIRADIGAN yagona amal, shu sababli
/// «atigi bitta» qoidasi bu yerda ham tekshiriladi.
/// </remarks>
public sealed class ActivateUserCommandHandlerTests
{
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRoleRepository _roles = Substitute.For<IRoleRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private readonly Role _superAdminRole =
        TestRoles.Standart(SystemRoles.SuperAdmin);

    private readonly Role _mainBossRole =
        TestRoles.Standart(SystemRoles.MainBoss);

    private readonly Role _hrRole =
        TestRoles.Standart(SystemRoles.HrManager);

    private ActivateUserCommandHandler Handler => new(_currentUser, _users, _roles, _unitOfWork);

    public ActivateUserCommandHandlerTests()
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
    public async Task Faolsiz_hisob_qayta_faollashtiriladi()
    {
        var acting = Mavjud("905088888", "Тошматов Акбар", _superAdminRole);
        ActingAs(acting);

        var hrUser = Mavjud("905088877", "Каримов Жасур", _hrRole);
        hrUser.Deactivate();

        var natija = await Handler.HandleAsync(
            new ActivateUserCommand(hrUser.Id),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Message : null);
        hrUser.IsActive.ShouldBeTrue();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Eski bosh rahbarni qaytarish — o'rniga yangisi qo'yilgan bo'lsa —
    /// tizimda IKKITA faol bosh rahbar hosil qilardi.
    /// </summary>
    [Fact]
    public async Task Orni_egallangan_bosh_rahbar_qaytarilmaydi()
    {
        var acting = Mavjud("905088888", "Тошматов Акбар", _superAdminRole);
        ActingAs(acting);

        var eskiRahbar = Mavjud("905011111", "Rahimov Sardor", _mainBossRole);
        eskiRahbar.Deactivate();

        var yangiRahbar = YangiFoydalanuvchi("905022222", "Юсупов Бекзод");
        yangiRahbar.SetRole(_mainBossRole.Id);

        _users.ListAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns([acting, eskiRahbar, yangiRahbar]);

        var natija = await Handler.HandleAsync(
            new ActivateUserCommand(eskiRahbar.Id),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.bosh_rahbar_mavjud");
        eskiRahbar.IsActive.ShouldBeFalse();
    }

    /// <summary>O'rin bo'sh bo'lsa — eski rahbar qaytariladi.</summary>
    [Fact]
    public async Task Orni_bosh_bosh_rahbar_qaytariladi()
    {
        var acting = Mavjud("905088888", "Тошматов Акбар", _superAdminRole);
        ActingAs(acting);

        var eskiRahbar = Mavjud("905011111", "Rahimov Sardor", _mainBossRole);
        eskiRahbar.Deactivate();

        _users.ListAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns([acting, eskiRahbar]);

        var natija = await Handler.HandleAsync(
            new ActivateUserCommand(eskiRahbar.Id),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Message : null);
        eskiRahbar.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Bosh_rahbar_super_administratorni_faollashtira_olmaydi()
    {
        var acting = Mavjud("905011111", "Rahimov Sardor", _mainBossRole);
        ActingAs(acting);

        var superAdmin = Mavjud("905088888", "Тошматов Акбар", _superAdminRole);
        superAdmin.Deactivate();

        var natija = await Handler.HandleAsync(
            new ActivateUserCommand(superAdmin.Id),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.rolga_haqqi_yoq");
        superAdmin.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Mavjud_bolmagan_foydalanuvchi_topilmadi_xatosini_beradi()
    {
        var acting = Mavjud("905088888", "Тошматов Акбар", _superAdminRole);
        ActingAs(acting);

        var yoqId = Guid.CreateVersion7();
        _users.GetByIdAsync(yoqId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var natija = await Handler.HandleAsync(
            new ActivateUserCommand(yoqId),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.topilmadi");
    }
}
