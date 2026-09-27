using Bonvi.Application.Identity.Abstractions;
using Bonvi.Application.Identity.EmployeeAuth;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;
using NSubstitute;

namespace Bonvi.Application.Tests.Identity;

/// <summary>
/// Xodim ilovasiga kirishdagi blok — (login, IP) juftligi bo'yicha.
/// </summary>
/// <remarks>
/// Audit topilmasi (2026-09-27): blok hisob qatorida edi va parol
/// tekshiruvidan OLDIN qaytardi — ya'ni (a) raqamning Bonvi xodiminiki ekanini
/// oshkor qilardi va (b) istalgan hamkasbni hamma joyda qulflab qo'yish
/// mumkin edi. Bu testlar yangi qoidani mahkamlaydi.
/// </remarks>
public sealed class EmployeeLoginCommandHandlerTests
{
    private const string Parol = "Togri_parol_2026";
    private const string HujumchiIp = "203.0.113.7";
    private const string XodimIp = "198.51.100.20";

    private static readonly DateTimeOffset Now = new(2026, 9, 27, 8, 0, 0, TimeSpan.FromHours(5));

    private readonly IEmployeeAccountRepository _accounts = Substitute.For<IEmployeeAccountRepository>();
    private readonly IEmployeeRefreshTokenRepository _refreshTokens = Substitute.For<IEmployeeRefreshTokenRepository>();
    private readonly IEmployeeRepository _employees = Substitute.For<IEmployeeRepository>();
    private readonly IEmploymentRepository _employments = Substitute.For<IEmploymentRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IEmployeeTokenService _tokens = Substitute.For<IEmployeeTokenService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IEmployeeAuthJournal _journal = Substitute.For<IEmployeeAuthJournal>();
    private readonly EmployeeLoginThrottle _throttle = new();

    private readonly EmployeeAccount _account;

    public EmployeeLoginCommandHandlerTests()
    {
        _clock.UtcNow.Returns(Now);

        var employee = Employee.Create(FullName.Create("Qodirov", "Ilova", "Testovich").Value).Value;
        _account = EmployeeAccount.Issue(employee.Id, "xesh", "shifrlangan", Now).Value;

        // Login — profil telefoni: ombor uni xodimlar jadvali orqali topadi.
        _accounts.FindByLoginAsync(Arg.Is<PhoneNumber>(login => login.Value == "+998901234567"), Arg.Any<CancellationToken>())
            .Returns(_account);

        _employees.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(employee);
        _employments.IsEmployedOnAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(true);

        _hasher.Hash(Arg.Any<string>()).Returns("soxta_xesh");
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns((false, false));
        _hasher.Verify("xesh", Parol).Returns((true, false));

        _tokens.CreateTokens(Arg.Any<EmployeeAccount>())
            .Returns(new TokenPair("access", Now.AddMinutes(15), "refresh", "refresh_xesh", Now.AddDays(7)));
    }

    private EmployeeLoginCommandHandler Handler => new(
        _accounts,
        _refreshTokens,
        _employees,
        _employments,
        _hasher,
        _tokens,
        _unitOfWork,
        _clock,
        _journal,
        _throttle);

    private Task<Bonvi.Domain.Common.Result<EmployeeAuthResultDto>> KirAsync(string login, string parol, string ip)
        => Handler.HandleAsync(new EmployeeLoginCommand(login, parol, ip), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Hujumchi_blokni_faqat_ozining_IP_siga_yoqadi()
    {
        for (var i = 0; i < EmployeeAccount.MaxFailedAttempts; i++)
        {
            (await KirAsync("901234567", "taxmin-" + i, HujumchiIp)).Error.Code.ShouldBe("xodim.notogri_malumot");
        }

        (await KirAsync("901234567", "yana", HujumchiIp)).Error.Code.ShouldBe("xodim.bloklangan");

        // Xodimning o'zi — boshqa manzildan — bemalol kiradi.
        var egasi = await KirAsync("+998 90 123 45 67", Parol, XodimIp);
        egasi.IsSuccess.ShouldBeTrue(egasi.IsFailure ? egasi.Error.Code : string.Empty);

        // Egasining kirishi yozilgan — hujumchining xatolari hisobga tegmagan.
        _account.LastLoginAt.ShouldBe(Now);
    }

    [Fact]
    public async Task Blok_davomida_parol_tekshirilmaydi()
    {
        for (var i = 0; i < EmployeeAccount.MaxFailedAttempts; i++)
        {
            await KirAsync("901234567", "taxmin-" + i, HujumchiIp);
        }

        _hasher.ClearReceivedCalls();

        var natija = await KirAsync("901234567", Parol, HujumchiIp);

        natija.Error.Code.ShouldBe("xodim.bloklangan", "to'g'ri parol ham — aks holda blok oracle bo'lardi");
        _hasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Notanish_raqam_mavjud_hisob_kabi_bloklanadi()
    {
        for (var i = 0; i < EmployeeAccount.MaxFailedAttempts; i++)
        {
            (await KirAsync("907777777", "taxmin-" + i, HujumchiIp)).Error.Code.ShouldBe("xodim.notogri_malumot");
        }

        var blok = await KirAsync("907777777", "yana", HujumchiIp);

        blok.Error.Code.ShouldBe("xodim.bloklangan");
        blok.Error.Details["retryAfterMinutes"].ShouldBe(EmployeeAccount.LockoutMinutes);
    }

    [Fact]
    public async Task Yozilish_shakli_blokdan_qochish_yoli_emas()
    {
        // «901234567», «+998901234567», «+998 90 123-45-67» — bitta login, bitta hisoblagich.
        string[] shakllar = ["901234567", "+998901234567", "+998 90 123-45-67", "998901234567", "90 123 45 67"];

        foreach (var shakl in shakllar)
        {
            await KirAsync(shakl, "taxmin", HujumchiIp);
        }

        (await KirAsync("901234567", Parol, HujumchiIp)).Error.Code.ShouldBe("xodim.bloklangan");
    }

    [Fact]
    public async Task Muvaffaqiyatli_kirish_hisoblagichni_tozalaydi()
    {
        for (var i = 0; i < EmployeeAccount.MaxFailedAttempts - 1; i++)
        {
            await KirAsync("901234567", "taxmin-" + i, XodimIp);
        }

        (await KirAsync("901234567", Parol, XodimIp)).IsSuccess.ShouldBeTrue();

        // Tozalangan: yana to'rtta xato — hali blok yo'q.
        for (var i = 0; i < EmployeeAccount.MaxFailedAttempts - 1; i++)
        {
            (await KirAsync("901234567", "taxmin-" + i, XodimIp)).Error.Code.ShouldBe("xodim.notogri_malumot");
        }
    }

    [Fact]
    public void Eski_xatolar_blok_muddatidan_keyin_unutiladi()
    {
        var throttle = new EmployeeLoginThrottle();
        var kalit = EmployeeLoginThrottle.Key("998901234567", "901234567");

        for (var i = 0; i < EmployeeAccount.MaxFailedAttempts - 1; i++)
        {
            throttle.RecordFailure(kalit, HujumchiIp, Now);
        }

        // Bir soatdan keyin — yangi hisob: bitta xato blok yoqmaydi.
        var keyin = Now.AddHours(1);
        throttle.RecordFailure(kalit, HujumchiIp, keyin);
        throttle.LockedFor(kalit, HujumchiIp, keyin).ShouldBeNull();

        // Blok o'z muddati bilan tugaydi.
        for (var i = 0; i < EmployeeAccount.MaxFailedAttempts; i++)
        {
            throttle.RecordFailure(kalit, HujumchiIp, keyin);
        }

        throttle.LockedFor(kalit, HujumchiIp, keyin).ShouldNotBeNull();
        throttle.LockedFor(kalit, HujumchiIp, keyin.AddMinutes(EmployeeAccount.LockoutMinutes)).ShouldBeNull();
    }
}
