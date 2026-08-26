using Bonvi.Application.Abstractions;
using Bonvi.Application.Identity.Users.Commands.ActivateUser;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;
using NSubstitute;

namespace Bonvi.Application.Tests.Identity.Users;

/// <summary>Foydalanuvchini qayta faollashtirish mantig'ining testlari.</summary>
public sealed class ActivateUserCommandHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private ActivateUserCommandHandler Handler => new(_users, _auditLogger, _unitOfWork);

    private static User FaolsizFoydalanuvchi()
    {
        var login = PhoneNumber.Create("905088877").Value;
        var ism = FullName.Parse("Каримов Жасур").Value;
        var user = User.Create(login, "xesh", ism).Value;
        user.Deactivate();
        return user;
    }

    [Fact]
    public async Task Faolsiz_hisob_qayta_faollashtiriladi()
    {
        var user = FaolsizFoydalanuvchi();
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var natija = await Handler.HandleAsync(
            new ActivateUserCommand(user.Id),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        user.IsActive.ShouldBeTrue();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Mavjud_bolmagan_foydalanuvchi_topilmadi_xatosini_beradi()
    {
        var yoqId = Guid.CreateVersion7();
        _users.GetByIdAsync(yoqId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var natija = await Handler.HandleAsync(
            new ActivateUserCommand(yoqId),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.topilmadi");
    }
}
