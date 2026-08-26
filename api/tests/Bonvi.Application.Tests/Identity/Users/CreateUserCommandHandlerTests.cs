using Bonvi.Application.Abstractions;
using Bonvi.Application.Identity.Abstractions;
using Bonvi.Application.Identity.Users.Commands.CreateUser;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;
using NSubstitute;

namespace Bonvi.Application.Tests.Identity.Users;

/// <summary>Yangi foydalanuvchi yaratish mantig'ining testlari.</summary>
public sealed class CreateUserCommandHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRoleRepository _roles = Substitute.For<IRoleRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private CreateUserCommandHandler Handler => new(_users, _roles, _hasher, _auditLogger, _unitOfWork);

    private static Role HrManagerRole()
        => Role.Create(SystemRoles.HrManager, "HR menejeri", isSystem: true).Value;

    public CreateUserCommandHandlerTests()
    {
        _hasher.Hash(Arg.Any<string>()).Returns(callInfo => "xesh:" + callInfo.Arg<string>());
    }

    [Fact]
    public async Task Togri_malumot_bilan_foydalanuvchi_yaratiladi()
    {
        var hrRole = HrManagerRole();
        _roles.GetByCodeAsync(SystemRoles.HrManager, Arg.Any<CancellationToken>()).Returns(hrRole);
        _users.LoginExistsAsync(Arg.Any<PhoneNumber>(), null, Arg.Any<CancellationToken>()).Returns(false);

        var natija = await Handler.HandleAsync(
            new CreateUserCommand("905088877", "Каримов", "Жасур", null, "Kuchli_Parol1", [SystemRoles.HrManager]),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Message : null);
        natija.Value.Login.ShouldBe("+998905088877");
        natija.Value.RoleCodes.ShouldContain(SystemRoles.HrManager);
        _users.Received(1).Add(Arg.Any<User>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Parol_xeshlanadi_ochiq_holda_saqlanmaydi()
    {
        var hrRole = HrManagerRole();
        _roles.GetByCodeAsync(SystemRoles.HrManager, Arg.Any<CancellationToken>()).Returns(hrRole);
        _users.LoginExistsAsync(Arg.Any<PhoneNumber>(), null, Arg.Any<CancellationToken>()).Returns(false);

        User? qoshilgan = null;
        _users.When(x => x.Add(Arg.Any<User>())).Do(callInfo => qoshilgan = callInfo.Arg<User>());

        await Handler.HandleAsync(
            new CreateUserCommand("905088877", "Каримов", "Жасур", null, "Kuchli_Parol1", [SystemRoles.HrManager]),
            TestContext.Current.CancellationToken);

        qoshilgan.ShouldNotBeNull();
        qoshilgan.PasswordHash.ShouldBe("xesh:Kuchli_Parol1");
        qoshilgan.PasswordHash.ShouldNotBe("Kuchli_Parol1");
    }

    [Fact]
    public async Task Qisqa_parol_rad_etiladi()
    {
        var natija = await Handler.HandleAsync(
            new CreateUserCommand("905088877", "Каримов", "Жасур", null, "qisqa", [SystemRoles.HrManager]),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.parol_qisqa");
        _users.DidNotReceive().Add(Arg.Any<User>());
    }

    [Fact]
    public async Task Rolsiz_yaratish_rad_etiladi()
    {
        var natija = await Handler.HandleAsync(
            new CreateUserCommand("905088877", "Каримов", "Жасур", null, "Kuchli_Parol1", []),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.rol_bosh");
    }

    [Fact]
    public async Task Notogri_rol_kodi_rad_etiladi()
    {
        _roles.GetByCodeAsync("notanish_rol", Arg.Any<CancellationToken>()).Returns((Role?)null);

        var natija = await Handler.HandleAsync(
            new CreateUserCommand("905088877", "Каримов", "Жасур", null, "Kuchli_Parol1", ["notanish_rol"]),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.rol_topilmadi");
    }

    [Fact]
    public async Task Band_login_rad_etiladi()
    {
        var hrRole = HrManagerRole();
        _roles.GetByCodeAsync(SystemRoles.HrManager, Arg.Any<CancellationToken>()).Returns(hrRole);
        _users.LoginExistsAsync(Arg.Any<PhoneNumber>(), null, Arg.Any<CancellationToken>()).Returns(true);

        var natija = await Handler.HandleAsync(
            new CreateUserCommand("905088877", "Каримов", "Жасур", null, "Kuchli_Parol1", [SystemRoles.HrManager]),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.login_band");
        _users.DidNotReceive().Add(Arg.Any<User>());
    }

    [Fact]
    public async Task Muvaffaqiyatli_yaratishda_audit_yozuvi_qoyiladi()
    {
        var hrRole = HrManagerRole();
        _roles.GetByCodeAsync(SystemRoles.HrManager, Arg.Any<CancellationToken>()).Returns(hrRole);
        _users.LoginExistsAsync(Arg.Any<PhoneNumber>(), null, Arg.Any<CancellationToken>()).Returns(false);

        await Handler.HandleAsync(
            new CreateUserCommand("905088877", "Каримов", "Жасур", null, "Kuchli_Parol1", [SystemRoles.HrManager]),
            TestContext.Current.CancellationToken);

        _auditLogger.Received(1).Record(
            AuditAction.Create,
            "User",
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Is<string?>(json => json != null && !json.Contains("Kuchli_Parol1", StringComparison.Ordinal)));
    }
}
