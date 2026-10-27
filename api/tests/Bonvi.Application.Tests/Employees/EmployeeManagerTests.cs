using Bonvi.Application.Employees.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using NSubstitute;

namespace Bonvi.Application.Tests.Employees;

/// <summary>
/// Xodimning «Rahbar»i — bo'limdan yuqoriga birinchi tirik boshliq (SPEC-jamoam §D).
/// </summary>
/// <remarks>
/// Uch qoida qadalgan: o'zi boshliq bo'lgan bo'lim tashlab ketiladi (zavod direktorining
/// rahbari — zavoddan yuqorida), o'chirilgan yoki ishdan ketgan boshliq tashlab ketiladi,
/// va bugun ishlamaydigan odamning rahbari yo'q.
/// </remarks>
public sealed class EmployeeManagerTests
{
    private static readonly DateOnly Bugun = new(2026, 10, 27);
    private static readonly Guid Firma = Guid.NewGuid();

    private readonly IDepartmentRepository _departments = Substitute.For<IDepartmentRepository>();
    private readonly IEmployeeRepository _employees = Substitute.For<IEmployeeRepository>();
    private readonly IEmploymentRepository _employments = Substitute.For<IEmploymentRepository>();

    private readonly Department _departament = Department.Create("7-Departament").Value;
    private readonly Department _zavod;
    private readonly Department _sex;

    public EmployeeManagerTests()
    {
        _zavod = Department.Create("Metan zavod", _departament.Id).Value;
        _sex = Department.Create("Plazma sexi", _zavod.Id).Value;

        _departments
            .ListAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new List<Department> { _departament, _zavod, _sex });
    }

    private EmployeeManagerResolver Resolver => new(_departments, _employees, _employments);

    // ────────── Nomzodlar zanjiri (sof) ──────────

    [Fact]
    public void Zanjir_yaqinidan_uzogiga_ozi_va_takror_tashlanadi()
    {
        var xodim = Guid.NewGuid();
        var direktor = Guid.NewGuid();
        var bosh = Guid.NewGuid();

        _sex.AssignHead(xodim);
        _zavod.AssignHead(direktor);
        _departament.AssignHead(direktor);

        ManagerChain.Candidates(xodim, _sex.Id, [_departament, _zavod, _sex]).ShouldBe([direktor]);

        _departament.AssignHead(bosh);
        ManagerChain.Candidates(xodim, _sex.Id, [_departament, _zavod, _sex]).ShouldBe([direktor, bosh]);

        // Boshlig'i yo'q bo'lim — nomzod bermaydi, yurish davom etadi.
        _zavod.AssignHead(null);
        ManagerChain.Candidates(xodim, _sex.Id, [_departament, _zavod, _sex]).ShouldBe([bosh]);
    }

    // ────────── Tirik boshliq ──────────

    [Fact]
    public async Task Bolim_boshligi_rahbar_boladi()
    {
        var xodim = Guid.NewGuid();
        var boshliq = Tirik("Karimov", "Anvar");
        _sex.AssignHead(boshliq.Id);

        var rahbar = await Resolver.ResolveAsync(xodim, [Munosabat(xodim, _sex.Id)], Bugun, cancellationToken: Ct);

        rahbar.ShouldNotBeNull();
        rahbar.EmployeeId.ShouldBe(boshliq.Id);
        rahbar.FullName.ShouldBe(boshliq.FullName.Full);
    }

    [Fact]
    public async Task Ozi_boshliq_bolsa_ota_bolim_boshligi_rahbar()
    {
        var direktor = Guid.NewGuid();
        var yuqori = Tirik("Aliyev", "Botir");

        _zavod.AssignHead(direktor);
        _departament.AssignHead(yuqori.Id);

        var rahbar = await Resolver.ResolveAsync(direktor, [Munosabat(direktor, _zavod.Id)], Bugun, cancellationToken: Ct);

        rahbar.ShouldNotBeNull();
        rahbar.EmployeeId.ShouldBe(yuqori.Id, "o'z zavodi tashlab ketiladi, yuqoriga yuriladi");
    }

    [Fact]
    public async Task Ishdan_ketgan_va_ochirilgan_boshliq_tashlab_ketiladi()
    {
        var xodim = Guid.NewGuid();
        var ketgan = Tirik("Ketgan", "Boshliq");
        var ochirilgan = Guid.NewGuid();
        var yuqori = Tirik("Yuqori", "Rahbar");

        _sex.AssignHead(ketgan.Id);
        _zavod.AssignHead(ochirilgan);
        _departament.AssignHead(yuqori.Id);

        // Ketgan — karta bor, lekin bugun ishlamaydi; o'chirilgan — kartasi umuman topilmaydi.
        _employments.IsEmployedOnAsync(ketgan.Id, Bugun, Arg.Any<CancellationToken>()).Returns(false);
        _employees.GetByIdAsync(ochirilgan, Arg.Any<CancellationToken>()).Returns((Employee?)null);

        var rahbar = await Resolver.ResolveAsync(xodim, [Munosabat(xodim, _sex.Id)], Bugun, cancellationToken: Ct);

        rahbar.ShouldNotBeNull();
        rahbar.EmployeeId.ShouldBe(yuqori.Id);
    }

    [Fact]
    public async Task Boshliq_topilmasa_yoki_bugun_ishlamasa_rahbar_yoq()
    {
        var xodim = Guid.NewGuid();

        (await Resolver.ResolveAsync(xodim, [Munosabat(xodim, _sex.Id)], Bugun, cancellationToken: Ct))
            .ShouldBeNull("daraxtda birorta boshliq yo'q");

        _sex.AssignHead(Tirik("Bor", "Boshliq").Id);

        var ketgan = Munosabat(xodim, _sex.Id);
        ketgan.Terminate(Bugun.AddDays(-1), reasonId: null).IsSuccess.ShouldBeTrue();

        (await Resolver.ResolveAsync(xodim, [ketgan], Bugun, cancellationToken: Ct))
            .ShouldBeNull("bugungi asosiy munosabat yo'q — bo'lim ham yo'q");

        (await Resolver.ResolveAsync(xodim, [Munosabat(xodim, bolimId: null)], Bugun, cancellationToken: Ct))
            .ShouldBeNull("bo'limsiz munosabat");
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>O'chirilmagan, bugun ishlaydigan xodim.</summary>
    private Employee Tirik(string familiya, string ism)
    {
        var xodim = Employee.Create(FullName.Create(familiya, ism, null).Value).Value;

        _employees.GetByIdAsync(xodim.Id, Arg.Any<CancellationToken>()).Returns(xodim);
        _employments.IsEmployedOnAsync(xodim.Id, Bugun, Arg.Any<CancellationToken>()).Returns(true);

        return xodim;
    }

    private static Employment Munosabat(Guid xodimId, Guid? bolimId)
        => Employment.Hire(xodimId, Firma, new DateOnly(2025, 1, 1), bolimId).Value;
}
