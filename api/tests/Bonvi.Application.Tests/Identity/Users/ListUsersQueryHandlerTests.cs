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
    public async Task Royxat_rol_kodlari_va_nomlari_bilan_qaytadi()
    {
        var adminRole = Role.Create(SystemRoles.Admin, "Administrator", isSystem: true).Value;
        var admin = YangiFoydalanuvchi("905088888", "Тошматов Акбар");
        admin.AssignRole(adminRole.Id);

        var hrRole = Role.Create(SystemRoles.HrManager, "HR menejeri", isSystem: true).Value;
        var hr = YangiFoydalanuvchi("905088877", "Каримов Жасур");
        hr.AssignRole(hrRole.Id);

        _users.ListAsync(Arg.Any<CancellationToken>()).Returns([admin, hr]);
        _roles.GetAllAsync(Arg.Any<CancellationToken>()).Returns([adminRole, hrRole]);

        var natija = await Handler.HandleAsync(TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.Count.ShouldBe(2);

        var adminDto = natija.Value.Single(dto => dto.Login == "+998905088888");
        adminDto.RoleCodes.ShouldContain(SystemRoles.Admin);
        adminDto.RoleNames.ShouldContain("Administrator");

        // Parol yoki uning xeshi DTO'da umuman yo'q — tip darajasida kafolatlangan
        var hasPasswordField = typeof(Bonvi.Application.Identity.Users.Dtos.UserSummaryDto)
            .GetProperties()
            .Any(p => p.Name.Contains("password", StringComparison.OrdinalIgnoreCase));

        hasPasswordField.ShouldBeFalse();
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
