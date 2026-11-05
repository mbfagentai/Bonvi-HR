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
/// Asosiy e'tibor to'rtta himoyaga: «kim kimga tega oladi», «atigi bitta»,
/// «oxirgi rahbar» va «o'z rolini o'zgartirish».
/// </remarks>
public sealed class UpdateUserCommandHandlerTests
{
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRoleRepository _roles = Substitute.For<IRoleRepository>();
    private readonly IUserAttendanceScopeRepository _attendanceScopes = Substitute.For<IUserAttendanceScopeRepository>();
    private readonly IAuditLogger _audit = Substitute.For<IAuditLogger>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private readonly Role _superAdminRole =
        TestRoles.Standart(SystemRoles.SuperAdmin);

    private readonly Role _mainBossRole =
        TestRoles.Standart(SystemRoles.MainBoss);

    private readonly Role _hrRole =
        TestRoles.Standart(SystemRoles.HrManager);

    private UpdateUserCommandHandler Handler => new(_currentUser, _users, _roles, _attendanceScopes, _audit, _unitOfWork);

    public UpdateUserCommandHandlerTests()
        => _roles.GetAllAsync(Arg.Any<CancellationToken>()).Returns([_superAdminRole, _mainBossRole, _hrRole]);

    private static User YangiFoydalanuvchi(string phone, string fullName)
    {
        var login = PhoneNumber.Create(phone).Value;
        var ism = FullName.Parse(fullName).Value;
        return User.Create(login, "xesh", ism).Value;
    }

    /// <summary>Bazadan topiladigan foydalanuvchi hosil qiladi.</summary>
    private User Mavjud(string phone, string fullName, Role role)
    {
        var user = YangiFoydalanuvchi(phone, fullName);
        user.SetRole(role.Id);
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _users.LoginExistsAsync(Arg.Any<PhoneNumber>(), user.Id, Arg.Any<CancellationToken>()).Returns(false);
        return user;
    }

    /// <summary>Joriy foydalanuvchini shu <paramref name="user"/> sifatida sozlaydi.</summary>
    private void ActingAs(User user) => _currentUser.UserId.Returns(user.Id);

    [Fact]
    public async Task Super_administrator_hr_menejerni_bosh_rahbarga_kotaradi()
    {
        var acting = Mavjud("905088888", "Тошматов Акбар", _superAdminRole);
        ActingAs(acting);

        var target = Mavjud("905088877", "Каримов Жасур", _hrRole);
        _users.ListAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns([acting, target]);

        var natija = await Handler.HandleAsync(
            new UpdateUserCommand(target.Id, "905088877", "Каримов", "Жасур", null, SystemRoles.MainBoss),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Message : null);
        natija.Value.RoleCode.ShouldBe(SystemRoles.MainBoss);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Rol ALMASHTIRILADI, qo'shilmaydi — hisobda doim bitta rol.</summary>
    [Fact]
    public async Task Rol_almashtiriladi_qoshilmaydi()
    {
        var acting = Mavjud("905088888", "Тошматов Акбар", _superAdminRole);
        ActingAs(acting);

        var target = Mavjud("905088877", "Каримов Жасур", _hrRole);
        _users.ListAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns([acting, target]);

        await Handler.HandleAsync(
            new UpdateUserCommand(target.Id, "905088877", "Каримов", "Жасур", null, SystemRoles.MainBoss),
            TestContext.Current.CancellationToken);

        target.RoleIds.Count.ShouldBe(1);
        target.RoleId.ShouldBe(_mainBossRole.Id);
    }

    /// <summary>
    /// Rol o'zgarmasa, foydalanuvchilar ro'yxati umuman so'ralmaydi:
    /// ism tuzatish uchun butun jadvalni o'qish ortiqcha.
    /// </summary>
    [Fact]
    public async Task Faqat_ism_tahrirlanganda_royxat_soralmaydi()
    {
        var acting = Mavjud("905088888", "Тошматов Акбар", _superAdminRole);
        ActingAs(acting);

        var target = Mavjud("905088877", "Каримов Жасур", _hrRole);

        var natija = await Handler.HandleAsync(
            new UpdateUserCommand(target.Id, "905088877", "Каримов", "Жасурбек", null, SystemRoles.HrManager),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Message : null);
        await _users.DidNotReceive().ListAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // ─────────────────────────── Kim kimga tega oladi ───────────────────────────

    [Fact]
    public async Task Bosh_rahbar_super_administratorga_tegolmaydi()
    {
        var acting = Mavjud("905088888", "Тошматов Акбар", _mainBossRole);
        ActingAs(acting);

        var target = Mavjud("905011111", "Rahimov Sardor", _superAdminRole);

        var natija = await Handler.HandleAsync(
            new UpdateUserCommand(target.Id, "905011111", "Rahimov", "Sardor", null, SystemRoles.HrManager),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.rolga_haqqi_yoq");
        target.RoleId.ShouldBe(_superAdminRole.Id, "hech narsa o'zgarmagan bo'lishi kerak");
    }

    [Fact]
    public async Task Bosh_rahbar_hr_menejerni_rahbariyatga_kotara_olmaydi()
    {
        var acting = Mavjud("905088888", "Тошматов Акбар", _mainBossRole);
        ActingAs(acting);

        var target = Mavjud("905088877", "Каримов Жасур", _hrRole);

        var natija = await Handler.HandleAsync(
            new UpdateUserCommand(target.Id, "905088877", "Каримов", "Жасур", null, SystemRoles.SuperAdmin),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.rolga_haqqi_yoq");
        target.RoleId.ShouldBe(_hrRole.Id);
    }

    [Fact]
    public async Task Bosh_rahbar_hr_menejerni_tahrirlay_oladi()
    {
        var acting = Mavjud("905088888", "Тошматов Акбар", _mainBossRole);
        ActingAs(acting);

        var target = Mavjud("905088877", "Каримов Жасур", _hrRole);

        var natija = await Handler.HandleAsync(
            new UpdateUserCommand(target.Id, "905088877", "Каримов", "Жасурбек", null, SystemRoles.HrManager),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Message : null);
    }

    // ─────────────────────────── «Atigi bitta» ───────────────────────────

    [Fact]
    public async Task Ikkinchi_bosh_rahbar_tayinlanmaydi()
    {
        var acting = Mavjud("905088888", "Тошматов Акбар", _superAdminRole);
        ActingAs(acting);

        var mavjudRahbar = YangiFoydalanuvchi("905011111", "Rahimov Sardor");
        mavjudRahbar.SetRole(_mainBossRole.Id);

        var target = Mavjud("905088877", "Каримов Жасур", _hrRole);
        _users.ListAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns([acting, mavjudRahbar, target]);

        var natija = await Handler.HandleAsync(
            new UpdateUserCommand(target.Id, "905088877", "Каримов", "Жасур", null, SystemRoles.MainBoss),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.bosh_rahbar_mavjud");
        target.RoleId.ShouldBe(_hrRole.Id);
    }

    // ─────────────────────────── Oxirgi rahbar va o'z roli ───────────────────────────

    [Fact]
    public async Task Oxirgi_faol_rahbarni_hr_menejerga_aylantirib_bolmaydi()
    {
        var yagonaRahbar = Mavjud("905088888", "Тошматов Акбар", _superAdminRole);
        ActingAs(yagonaRahbar);

        _users.ListAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns([yagonaRahbar]);

        var natija = await Handler.HandleAsync(
            new UpdateUserCommand(yagonaRahbar.Id, "905088888", "Тошматов", "Акбар", null, SystemRoles.HrManager),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.oxirgi_administrator");
        yagonaRahbar.RoleId.ShouldBe(_superAdminRole.Id, "hech narsa o'zgarmagan bo'lishi kerak");
    }

    [Fact]
    public async Task Oz_rolini_ozgartirish_rad_etiladi_boshqa_rahbar_bolsa_ham()
    {
        var acting = Mavjud("905088888", "Тошматов Акбар", _superAdminRole);
        ActingAs(acting);

        var boshqaRahbar = YangiFoydalanuvchi("905011111", "Rahimov Sardor");
        boshqaRahbar.SetRole(_mainBossRole.Id);

        _users.ListAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns([acting, boshqaRahbar]);

        var natija = await Handler.HandleAsync(
            new UpdateUserCommand(acting.Id, "905088888", "Тошматов", "Акбар", null, SystemRoles.HrManager),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.oz_rolini_ozgartirish");
        acting.RoleId.ShouldBe(_superAdminRole.Id, "hech narsa o'zgarmagan bo'lishi kerak");
    }

    /// <summary>O'z ismini tahrirlash — rol o'zgarmagani uchun cheklovsiz.</summary>
    [Fact]
    public async Task Oz_ismini_tahrirlash_mumkin()
    {
        var acting = Mavjud("905088888", "Тошматов Акбар", _superAdminRole);
        ActingAs(acting);

        var natija = await Handler.HandleAsync(
            new UpdateUserCommand(acting.Id, "905088888", "Тошматов", "Акбарбек", null, SystemRoles.SuperAdmin),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Message : null);
    }

    // ─────────────────────────── Oddiy tekshiruvlar ───────────────────────────

    [Fact]
    public async Task Mavjud_bolmagan_foydalanuvchi_topilmadi_xatosini_beradi()
    {
        var acting = Mavjud("905088888", "Тошматов Акбар", _superAdminRole);
        ActingAs(acting);

        var yoqId = Guid.CreateVersion7();
        _users.GetByIdAsync(yoqId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var natija = await Handler.HandleAsync(
            new UpdateUserCommand(yoqId, "905088877", "Каримов", "Жасур", null, SystemRoles.HrManager),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.topilmadi");
    }

    [Fact]
    public async Task Band_loginga_ozgartirish_rad_etiladi()
    {
        var acting = Mavjud("905088888", "Тошматов Акбар", _superAdminRole);
        ActingAs(acting);

        var target = Mavjud("905088877", "Каримов Жасур", _hrRole);
        _users.LoginExistsAsync(Arg.Any<PhoneNumber>(), target.Id, Arg.Any<CancellationToken>()).Returns(true);

        var natija = await Handler.HandleAsync(
            new UpdateUserCommand(target.Id, "905011111", "Каримов", "Жасур", null, SystemRoles.HrManager),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.login_band");
    }

    // ─────────────── Ruxsatlar solishtiriladi (rol kodi emas) ───────────────

    /// <summary>
    /// HR menejeri o'zidan kuchli (bosh rahbar) hisobga tegolmaydi — hatto
    /// kelajakda unga <c>users.manage</c> berilsa ham.
    /// </summary>
    [Fact]
    public async Task Hr_menejer_bosh_rahbarga_tegolmaydi()
    {
        var acting = Mavjud("905088888", "Тошматов Акбар", _hrRole);
        ActingAs(acting);

        var target = Mavjud("905011111", "Rahimov Sardor", _mainBossRole);

        var natija = await Handler.HandleAsync(
            new UpdateUserCommand(target.Id, "905011111", "Rahimov", "Sardor", null, SystemRoles.MainBoss),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.rolga_haqqi_yoq");
    }

    [Fact]
    public async Task Maxsus_rol_egasi_ozidan_kuchli_hisobga_tegolmaydi()
    {
        var kadrBoshligi = TestRoles.Maxsus("Kadr bo'limi boshlig'i", Permissions.UsersManage, Permissions.EmployeesView);
        _roles.GetAllAsync(Arg.Any<CancellationToken>()).Returns([_superAdminRole, _mainBossRole, _hrRole, kadrBoshligi]);

        var acting = Mavjud("905088888", "Тошматов Акбар", kadrBoshligi);
        ActingAs(acting);

        var target = Mavjud("905088877", "Каримов Жасур", _hrRole);

        var natija = await Handler.HandleAsync(
            new UpdateUserCommand(target.Id, "905088877", "Каримов", "Жасурбек", null, SystemRoles.HrManager),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.rolga_haqqi_yoq");
        target.FullName.Full.ShouldNotContain("Жасурбек");
    }

    /// <summary>
    /// Rolsiz hisob — bo'sh ruxsatlar to'plami: <c>users.manage</c> li har
    /// kim unga (o'zidan kuchsiz) rol bera oladi.
    /// </summary>
    [Fact]
    public async Task Rolsiz_hisobga_ozidan_kuchsiz_rol_beriladi()
    {
        var kadrBoshligi = TestRoles.Maxsus(
            "Kadr bo'limi boshlig'i", Permissions.UsersManage, Permissions.EmployeesView, Permissions.EmploymentView);
        var kuzatuvchi = TestRoles.Maxsus("Kuzatuvchi", Permissions.EmployeesView);
        _roles.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([_superAdminRole, _mainBossRole, _hrRole, kadrBoshligi, kuzatuvchi]);

        var acting = Mavjud("905088888", "Тошматов Акбар", kadrBoshligi);
        ActingAs(acting);

        var rolsiz = YangiFoydalanuvchi("905088877", "Каримов Жасур");
        _users.GetByIdAsync(rolsiz.Id, Arg.Any<CancellationToken>()).Returns(rolsiz);
        _users.LoginExistsAsync(Arg.Any<PhoneNumber>(), rolsiz.Id, Arg.Any<CancellationToken>()).Returns(false);
        _users.ListAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns([acting, rolsiz]);

        var natija = await Handler.HandleAsync(
            new UpdateUserCommand(rolsiz.Id, "905088877", "Каримов", "Жасур", null, kuzatuvchi.Code),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Message : null);
        rolsiz.RoleId.ShouldBe(kuzatuvchi.Id);
    }
}
