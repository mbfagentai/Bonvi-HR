using Bonvi.Application.Identity.Users.Queries.ListUsers;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;
using NSubstitute;

namespace Bonvi.Application.Tests.Identity.Users;

/// <summary>Foydalanuvchilar ro'yxatining testlari.</summary>
public sealed class ListUsersQueryHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRoleRepository _roles = Substitute.For<IRoleRepository>();

    private ListUsersQueryHandler Handler => new(_users, _roles);

    private static User YangiFoydalanuvchi(string phone, string fullName)
    {
        var login = PhoneNumber.Create(phone).Value;
        var ism = FullName.Parse(fullName).Value;
        return User.Create(login, "xesh", ism).Value;
    }

    [Fact]
    public async Task Royxat_rol_kodi_va_nomi_bilan_qaytadi()
    {
        var superAdminRole = Role.Create(SystemRoles.SuperAdmin, "Super administrator", isSystem: true).Value;
        var superAdmin = YangiFoydalanuvchi("905088888", "Тошматов Акбар");
        superAdmin.SetRole(superAdminRole.Id);

        var hrRole = Role.Create(SystemRoles.HrManager, "HR menejeri", isSystem: true).Value;
        var hr = YangiFoydalanuvchi("905088877", "Каримов Жасур");
        hr.SetRole(hrRole.Id);

        _users.ListAsync(Arg.Any<CancellationToken>()).Returns([superAdmin, hr]);
        _roles.GetAllAsync(Arg.Any<CancellationToken>()).Returns([superAdminRole, hrRole]);

        var natija = await Handler.HandleAsync(TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.Count.ShouldBe(2);

        var superAdminDto = natija.Value.Single(dto => dto.Login == "+998905088888");
        superAdminDto.RoleCode.ShouldBe(SystemRoles.SuperAdmin);
        superAdminDto.RoleName.ShouldBe("Super administrator");

        // Parol yoki uning xeshi DTO'da umuman yo'q — tip darajasida kafolatlangan
        var hasPasswordField = typeof(Bonvi.Application.Identity.Users.Dtos.UserSummaryDto)
            .GetProperties()
            .Any(p => p.Name.Contains("password", StringComparison.OrdinalIgnoreCase));

        hasPasswordField.ShouldBeFalse();
    }

    /// <summary>
    /// Ro'yxatda rasm KALITI bo'lishi shart: usiz interfeys har bir qator
    /// uchun keraksiz 404 so'rov yuborardi.
    /// </summary>
    [Fact]
    public async Task Royxatda_rasm_kaliti_qaytadi()
    {
        var hrRole = Role.Create(SystemRoles.HrManager, "HR menejeri", isSystem: true).Value;

        var rasmli = YangiFoydalanuvchi("905088877", "Каримов Жасур");
        rasmli.SetRole(hrRole.Id);
        rasmli.SetPhoto("users/rasm.jpg");

        var rasmsiz = YangiFoydalanuvchi("905011111", "Rahimov Sardor");
        rasmsiz.SetRole(hrRole.Id);

        _users.ListAsync(Arg.Any<CancellationToken>()).Returns([rasmli, rasmsiz]);
        _roles.GetAllAsync(Arg.Any<CancellationToken>()).Returns([hrRole]);

        var natija = await Handler.HandleAsync(TestContext.Current.CancellationToken);

        natija.Value.Single(dto => dto.Login == "+998905088877").PhotoKey.ShouldBe("users/rasm.jpg");
        natija.Value.Single(dto => dto.Login == "+998905011111").PhotoKey.ShouldBeNull();
    }

    [Fact]
    public async Task Faolsiz_foydalanuvchilar_ham_royxatda_koradi()
    {
        var user = YangiFoydalanuvchi("905088877", "Каримов Жасур");
        user.Deactivate();

        _users.ListAsync(Arg.Any<CancellationToken>()).Returns([user]);
        _roles.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);

        var natija = await Handler.HandleAsync(TestContext.Current.CancellationToken);

        natija.Value.Single().IsActive.ShouldBeFalse();
    }
}
