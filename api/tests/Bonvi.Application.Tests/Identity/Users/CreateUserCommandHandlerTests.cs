using Bonvi.Application.Abstractions;
using Bonvi.Application.Identity.Abstractions;
using Bonvi.Application.Identity.Users.Commands.CreateUser;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;
using NSubstitute;

namespace Bonvi.Application.Tests.Identity.Users;

/// <summary>
/// Yangi foydalanuvchi yaratish mantig'ining testlari.
/// </summary>
/// <remarks>
/// Oddiy tekshiruvlardan tashqari uch rolli modelning ikkita qoidasi ham
/// shu yerda: «kim kimga rol bera oladi» va «atigi bitta».
/// </remarks>
public sealed class CreateUserCommandHandlerTests
{
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRoleRepository _roles = Substitute.For<IRoleRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private readonly Role _superAdminRole =
        Role.Create(SystemRoles.SuperAdmin, "Super administrator", isSystem: true).Value;

    private readonly Role _mainBossRole =
        Role.Create(SystemRoles.MainBoss, "Bosh rahbar", isSystem: true).Value;

    private readonly Role _hrRole =
        Role.Create(SystemRoles.HrManager, "HR menejeri", isSystem: true).Value;

    private CreateUserCommandHandler Handler
        => new(_currentUser, _users, _roles, _hasher, _auditLogger, _unitOfWork);

    public CreateUserCommandHandlerTests()
    {
        _hasher.Hash(Arg.Any<string>()).Returns(callInfo => "xesh:" + callInfo.Arg<string>());
        _roles.GetAllAsync(Arg.Any<CancellationToken>()).Returns([_superAdminRole, _mainBossRole, _hrRole]);
        _users.LoginExistsAsync(Arg.Any<PhoneNumber>(), null, Arg.Any<CancellationToken>()).Returns(false);
    }

    private static User YangiFoydalanuvchi(string phone, string fullName)
    {
        var login = PhoneNumber.Create(phone).Value;
        var ism = FullName.Parse(fullName).Value;
        return User.Create(login, "xesh", ism).Value;
    }

    /// <summary>So'rovni shu roldagi hisob yuborayotgan qilib sozlaydi.</summary>
    private User ActingAs(Role role, string phone = "905088888")
    {
        var user = YangiFoydalanuvchi(phone, "Тошматов Акбар");
        user.SetRole(role.Id);

        _currentUser.UserId.Returns(user.Id);
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        return user;
    }

    [Fact]
    public async Task Togri_malumot_bilan_foydalanuvchi_yaratiladi()
    {
        ActingAs(_superAdminRole);

        var natija = await Handler.HandleAsync(
            new CreateUserCommand("905088877", "Каримов", "Жасур", null, "Kuchli_Parol1", SystemRoles.HrManager),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Message : null);
        natija.Value.Login.ShouldBe("+998905088877");
        natija.Value.RoleCode.ShouldBe(SystemRoles.HrManager);
        _users.Received(1).Add(Arg.Any<User>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Yaratilgan_hisobda_aynan_bitta_rol_boladi()
    {
        ActingAs(_superAdminRole);

        User? qoshilgan = null;
        _users.When(x => x.Add(Arg.Any<User>())).Do(callInfo => qoshilgan = callInfo.Arg<User>());

        await Handler.HandleAsync(
            new CreateUserCommand("905088877", "Каримов", "Жасур", null, "Kuchli_Parol1", SystemRoles.HrManager),
            TestContext.Current.CancellationToken);

        qoshilgan.ShouldNotBeNull();
        qoshilgan.RoleIds.Count.ShouldBe(1);
        qoshilgan.RoleId.ShouldBe(_hrRole.Id);
    }

    [Fact]
    public async Task Parol_xeshlanadi_ochiq_holda_saqlanmaydi()
    {
        ActingAs(_superAdminRole);

        User? qoshilgan = null;
        _users.When(x => x.Add(Arg.Any<User>())).Do(callInfo => qoshilgan = callInfo.Arg<User>());

        await Handler.HandleAsync(
            new CreateUserCommand("905088877", "Каримов", "Жасур", null, "Kuchli_Parol1", SystemRoles.HrManager),
            TestContext.Current.CancellationToken);

        qoshilgan.ShouldNotBeNull();
        qoshilgan.PasswordHash.ShouldBe("xesh:Kuchli_Parol1");
        qoshilgan.PasswordHash.ShouldNotBe("Kuchli_Parol1");
    }

    [Fact]
    public async Task Qisqa_parol_rad_etiladi()
    {
        ActingAs(_superAdminRole);

        var natija = await Handler.HandleAsync(
            new CreateUserCommand("905088877", "Каримов", "Жасур", null, "qisqa", SystemRoles.HrManager),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.parol_qisqa");
        _users.DidNotReceive().Add(Arg.Any<User>());
    }

    [Fact]
    public async Task Rolsiz_yaratish_rad_etiladi()
    {
        ActingAs(_superAdminRole);

        var natija = await Handler.HandleAsync(
            new CreateUserCommand("905088877", "Каримов", "Жасур", null, "Kuchli_Parol1", null),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.rol_bosh");
    }

    [Fact]
    public async Task Notogri_rol_kodi_rad_etiladi()
    {
        ActingAs(_superAdminRole);

        var natija = await Handler.HandleAsync(
            new CreateUserCommand("905088877", "Каримов", "Жасур", null, "Kuchli_Parol1", "notanish_rol"),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.rol_topilmadi");
    }

    [Fact]
    public async Task Band_login_rad_etiladi()
    {
        ActingAs(_superAdminRole);
        _users.LoginExistsAsync(Arg.Any<PhoneNumber>(), null, Arg.Any<CancellationToken>()).Returns(true);

        var natija = await Handler.HandleAsync(
            new CreateUserCommand("905088877", "Каримов", "Жасур", null, "Kuchli_Parol1", SystemRoles.HrManager),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.login_band");
        _users.DidNotReceive().Add(Arg.Any<User>());
    }

    [Fact]
    public async Task Muvaffaqiyatli_yaratishda_audit_yozuvi_qoyiladi()
    {
        ActingAs(_superAdminRole);

        await Handler.HandleAsync(
            new CreateUserCommand("905088877", "Каримов", "Жасур", null, "Kuchli_Parol1", SystemRoles.HrManager),
            TestContext.Current.CancellationToken);

        _auditLogger.Received(1).Record(
            AuditAction.Create,
            "User",
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Is<string?>(json => json != null && !json.Contains("Kuchli_Parol1", StringComparison.Ordinal)));
    }

    // ─────────────────────────── Kim kimga rol bera oladi ───────────────────────────

    [Fact]
    public async Task Bosh_rahbar_hr_menejer_yarata_oladi()
    {
        ActingAs(_mainBossRole);

        var natija = await Handler.HandleAsync(
            new CreateUserCommand("905088877", "Каримов", "Жасур", null, "Kuchli_Parol1", SystemRoles.HrManager),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Message : null);
    }

    [Fact]
    public async Task Bosh_rahbar_super_administrator_yarata_olmaydi()
    {
        ActingAs(_mainBossRole);

        var natija = await Handler.HandleAsync(
            new CreateUserCommand("905088877", "Каримов", "Жасур", null, "Kuchli_Parol1", SystemRoles.SuperAdmin),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.rolga_haqqi_yoq");
        _users.DidNotReceive().Add(Arg.Any<User>());
    }

    [Fact]
    public async Task Bosh_rahbar_ikkinchi_bosh_rahbar_ham_yarata_olmaydi()
    {
        ActingAs(_mainBossRole);

        var natija = await Handler.HandleAsync(
            new CreateUserCommand("905088877", "Каримов", "Жасур", null, "Kuchli_Parol1", SystemRoles.MainBoss),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.rolga_haqqi_yoq");
    }

    // ─────────────────────────── «Atigi bitta» ───────────────────────────

    [Fact]
    public async Task Ikkinchi_bosh_rahbar_yaratilmaydi()
    {
        var acting = ActingAs(_superAdminRole);

        var mavjudRahbar = YangiFoydalanuvchi("905011111", "Rahimov Sardor");
        mavjudRahbar.SetRole(_mainBossRole.Id);
        _users.ListAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns([acting, mavjudRahbar]);

        var natija = await Handler.HandleAsync(
            new CreateUserCommand("905088877", "Каримов", "Жасур", null, "Kuchli_Parol1", SystemRoles.MainBoss),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.bosh_rahbar_mavjud");
        _users.DidNotReceive().Add(Arg.Any<User>());
    }

    [Fact]
    public async Task Ikkinchi_super_administrator_yaratilmaydi()
    {
        var acting = ActingAs(_superAdminRole);
        _users.ListAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns([acting]);

        var natija = await Handler.HandleAsync(
            new CreateUserCommand("905088877", "Каримов", "Жасур", null, "Kuchli_Parol1", SystemRoles.SuperAdmin),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.super_admin_mavjud");
    }

    /// <summary>
    /// «Atigi bitta» faqat FAOL hisoblarni sanaydi — odam almashadi.
    /// </summary>
    [Fact]
    public async Task Faolsizlantirilgan_bosh_rahbar_ornini_yangisi_egallay_oladi()
    {
        var acting = ActingAs(_superAdminRole);

        var eskiRahbar = YangiFoydalanuvchi("905011111", "Rahimov Sardor");
        eskiRahbar.SetRole(_mainBossRole.Id);
        eskiRahbar.Deactivate();

        _users.ListAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns([acting, eskiRahbar]);

        var natija = await Handler.HandleAsync(
            new CreateUserCommand("905088877", "Каримов", "Жасур", null, "Kuchli_Parol1", SystemRoles.MainBoss),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Message : null);
        natija.Value.RoleCode.ShouldBe(SystemRoles.MainBoss);
    }

    /// <summary>
    /// HR menejer yaratishda foydalanuvchilar ro'yxati umuman so'ralmaydi —
    /// «atigi bitta» qoidasi unga taalluqli emas.
    /// </summary>
    [Fact]
    public async Task Hr_menejer_yaratishda_ortiqcha_soro_ketmaydi()
    {
        ActingAs(_superAdminRole);

        await Handler.HandleAsync(
            new CreateUserCommand("905088877", "Каримов", "Жасур", null, "Kuchli_Parol1", SystemRoles.HrManager),
            TestContext.Current.CancellationToken);

        await _users.DidNotReceive().ListAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}
