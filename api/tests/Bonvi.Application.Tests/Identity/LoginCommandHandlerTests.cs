using Bonvi.Application.Identity.Abstractions;
using Bonvi.Application.Identity.Commands.Login;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;
using NSubstitute;

namespace Bonvi.Application.Tests.Identity;

/// <summary>
/// Kirish mantig'ining testlari.
/// </summary>
/// <remarks>
/// Bu testlar asosan <b>xavfsizlik</b> qoidalarini himoyalaydi.
/// Ular buzilsa, tizim ma'lumot oshkor qila boshlaydi — lekin
/// hech qanday funksional test yiqilmaydi. Shu sababli aynan shu
/// qoidalar test bilan mahkamlangan.
/// </remarks>
public sealed class LoginCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 9, 0, 0, TimeSpan.FromHours(5));

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenService _tokens = Substitute.For<ITokenService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private LoginCommandHandler Handler => new(_users, _refreshTokens, _hasher, _tokens, _unitOfWork, _clock);

    public LoginCommandHandlerTests()
    {
        _clock.UtcNow.Returns(Now);

        _tokens.CreateTokens(
                Arg.Any<User>(),
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<IReadOnlySet<string>>())
            .Returns(new TokenPair("access", Now.AddMinutes(15), "refresh", "refresh_xesh", Now.AddDays(7)));

        _users.GetRoleCodesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>([SystemRoles.HrManager]));

        _users.GetPermissionsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlySet<string>>(
                new HashSet<string>(StringComparer.Ordinal) { Permissions.EmployeesView }));
    }

    private static User FaolFoydalanuvchi()
    {
        var login = PhoneNumber.Create("905088888").Value;
        var ism = FullName.Parse("Мамажонов Бобуржон Фазлитдин ўғли").Value;
        return User.Create(login, "saqlangan_xesh", ism).Value;
    }

    // ────────── Muvaffaqiyatli kirish ──────────

    [Fact]
    public async Task Togri_parol_bilan_kirish_token_qaytaradi()
    {
        var user = FaolFoydalanuvchi();
        _users.GetByLoginAsync(Arg.Any<PhoneNumber>(), Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify("saqlangan_xesh", "parol").Returns((true, false));

        var natija = await Handler.HandleAsync(
            new LoginCommand("905088888", "parol", "10.0.0.1"),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Message : null);
        natija.Value.AccessToken.ShouldBe("access");
        natija.Value.RefreshToken.ShouldBe("refresh");
        natija.Value.User.Login.ShouldBe("+998905088888");
        natija.Value.User.Roles.ShouldContain(SystemRoles.HrManager);
        user.LastLoginAt.ShouldBe(Now);
    }

    [Fact]
    public async Task Muvaffaqiyatli_kirishda_refresh_token_saqlanadi()
    {
        var user = FaolFoydalanuvchi();
        _users.GetByLoginAsync(Arg.Any<PhoneNumber>(), Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns((true, false));

        await Handler.HandleAsync(
            new LoginCommand("905088888", "parol", "10.0.0.1"),
            TestContext.Current.CancellationToken);

        // Ochiq token emas, XESH saqlanishi kerak
        _refreshTokens.Received(1).Add(Arg.Is<RefreshToken>(t => t.TokenHash == "refresh_xesh"));
    }

    // ────────── Ma'lumot oshkor qilmaslik ──────────

    [Fact]
    public async Task Mavjud_bolmagan_foydalanuvchi_va_notogri_parol_BIR_XIL_xato_beradi()
    {
        // Eng muhim xavfsizlik qoidasi: hujumchi qaysi raqamlar
        // tizimda ro'yxatdan o'tganini aniqlay olmasligi kerak
        _users.GetByLoginAsync(Arg.Any<PhoneNumber>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns((false, false));

        var yoq = await Handler.HandleAsync(
            new LoginCommand("905088888", "parol", null),
            TestContext.Current.CancellationToken);

        var user = FaolFoydalanuvchi();
        _users.GetByLoginAsync(Arg.Any<PhoneNumber>(), Arg.Any<CancellationToken>()).Returns(user);

        var notogri = await Handler.HandleAsync(
            new LoginCommand("905088888", "notogri", null),
            TestContext.Current.CancellationToken);

        yoq.IsFailure.ShouldBeTrue();
        notogri.IsFailure.ShouldBeTrue();
        yoq.Error.Code.ShouldBe(notogri.Error.Code);
        yoq.Error.Message.ShouldBe(notogri.Error.Message);
        yoq.Error.Code.ShouldBe("auth.notogri_malumot");
    }

    [Fact]
    public async Task Foydalanuvchi_topilmasa_ham_xesh_tekshiruvi_bajariladi()
    {
        // Vaqt bo'yicha oshkor qilmaslik: javob vaqtidagi farq bo'yicha
        // login mavjudligini bilib olish mumkin bo'lmasligi kerak
        _users.GetByLoginAsync(Arg.Any<PhoneNumber>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns((false, false));

        await Handler.HandleAsync(
            new LoginCommand("905088888", "parol", null),
            TestContext.Current.CancellationToken);

        _hasher.Received(1).Verify(Arg.Any<string>(), "parol");
    }

    [Fact]
    public async Task Notogri_formatdagi_raqam_ham_umumiy_xato_beradi()
    {
        var natija = await Handler.HandleAsync(
            new LoginCommand("12345", "parol", null),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("auth.notogri_malumot");
    }

    // ────────── Bloklash ──────────

    [Fact]
    public async Task Notogri_parolda_xato_urinish_qayd_etiladi()
    {
        var user = FaolFoydalanuvchi();
        _users.GetByLoginAsync(Arg.Any<PhoneNumber>(), Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns((false, false));

        await Handler.HandleAsync(
            new LoginCommand("905088888", "notogri", null),
            TestContext.Current.CancellationToken);

        user.FailedLoginAttempts.ShouldBe(1);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Bloklangan_foydalanuvchi_togri_parol_bilan_ham_kira_olmaydi()
    {
        var user = FaolFoydalanuvchi();
        for (var i = 0; i < 5; i++)
        {
            user.RecordFailedLogin(Now);
        }

        _users.GetByLoginAsync(Arg.Any<PhoneNumber>(), Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns((true, false));

        var natija = await Handler.HandleAsync(
            new LoginCommand("905088888", "parol", null),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("auth.bloklangan");

        // Parol umuman tekshirilmasligi kerak
        _hasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Faolsizlantirilgan_hisob_kira_olmaydi()
    {
        var user = FaolFoydalanuvchi();
        user.Deactivate();

        _users.GetByLoginAsync(Arg.Any<PhoneNumber>(), Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns((true, false));

        var natija = await Handler.HandleAsync(
            new LoginCommand("905088888", "parol", null),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("auth.hisob_faol_emas");
    }

    // ────────── Xesh yangilash ──────────

    [Fact]
    public async Task Eski_algoritmli_xesh_muvaffaqiyatli_kirishdan_keyin_yangilanadi()
    {
        var user = FaolFoydalanuvchi();
        _users.GetByLoginAsync(Arg.Any<PhoneNumber>(), Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify("saqlangan_xesh", "parol").Returns((true, true));
        _hasher.Hash("parol").Returns("yangi_kuchli_xesh");

        var natija = await Handler.HandleAsync(
            new LoginCommand("905088888", "parol", null),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        user.PasswordHash.ShouldBe("yangi_kuchli_xesh");
    }
}
