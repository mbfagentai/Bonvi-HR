using Bonvi.Application.Employees.Flags;
using Bonvi.Domain.Employees;
using NSubstitute;

namespace Bonvi.Application.Tests.Employees;

/// <summary>
/// «Bir vaqtda atigi bitta kuchdagi belgi» qoidasi.
/// </summary>
/// <remarks>
/// <para>
/// Foydalanuvchining aniq qarori, istisnosiz: qora ro'yxatdagi odam ayni
/// paytda rezervda ham bo'la olmaydi. Qoida <b>backendda qat'iy</b>
/// tekshiriladi — interfeysdagi tasdiqlash oynasi faqat qulaylik, va bu
/// testlar aynan shuni qadab turadi: tasdiqsiz so'rov o'tmaydi, tasdiq
/// bilan esa eskisi YOPILADI (o'chirilmaydi).
/// </para>
/// <para>
/// Tekshiruv naqshi bo'shatishdagi bilan bir xil
/// (<see cref="TerminationBlockTests"/>): birinchi so'rov 409, ataylab
/// yuborilgan tasdiq o'tadi.
/// </para>
/// </remarks>
public sealed class SingleActiveFlagTests
{
    private readonly EmployeeTestContext _context = new();

    private RaiseFlagCommandHandler Qoyuvchi => new(
        _context.Flags,
        _context.Employees,
        _context.Employments,
        _context.Clock,
        _context.UnitOfWork);

    [Fact]
    public async Task Belgisiz_xodimga_tasdiqsiz_qoyiladi()
    {
        var employee = _context.ExistingEmployee();

        var natija = await Qoyuvchi.HandleAsync(
            new RaiseFlagCommand(employee.Id, EmployeeFlagType.Reserve),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);
        _context.Flags.Received(1).Add(Arg.Any<EmployeeFlag>());
    }

    [Fact]
    public async Task Kuchdagi_belgi_borida_tasdiqsiz_qoyilmaydi()
    {
        var employee = _context.ExistingEmployee();
        _context.ExistingFlag(employee.Id, EmployeeFlagType.Blacklist);

        var natija = await Qoyuvchi.HandleAsync(
            new RaiseFlagCommand(employee.Id, EmployeeFlagType.Reserve),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("belgi.boshqa_belgi_bor");

        // 409 dan keyin bazaga HECH NARSA yozilmaydi
        _context.Flags.DidNotReceive().Add(Arg.Any<EmployeeFlag>());
        await _context.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Tasdiq_bilan_eskisi_yopiladi_va_yangisi_ochiladi()
    {
        var employee = _context.ExistingEmployee();
        var eski = _context.ExistingFlag(employee.Id, EmployeeFlagType.Blacklist);

        var natija = await Qoyuvchi.HandleAsync(
            new RaiseFlagCommand(employee.Id, EmployeeFlagType.Reserve, Confirm: true),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);

        // Eskisi O'CHIRILMAYDI — tarix qoladi, faqat muddati yopiladi
        eski.EndDate.ShouldBe(EmployeeTestContext.Today);
        eski.Reason.ShouldNotBeNull();
        eski.Reason.ShouldContain("almashtirildi");

        _context.Flags.DidNotReceive().Remove(Arg.Any<EmployeeFlag>());

        // Ikkalasi BITTA tranzaksiyada: yopish va ochish orasida saqlash yo'q
        await _context.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Formaning_ozi_xato_bolsa_tasdiq_soralmaydi()
    {
        // Sababsiz qora ro'yxat — formaning O'Z xatosi. U tasdiqlash
        // savolidan OLDIN chiqishi kerak, aks holda HR «eskisini
        // yopamizmi?» degan savolga javob berib, keyin «sabab kiriting»
        // xatosini olardi.
        var employee = _context.ExistingEmployee();
        _context.ExistingFlag(employee.Id, EmployeeFlagType.Monitoring);

        var natija = await Qoyuvchi.HandleAsync(
            new RaiseFlagCommand(employee.Id, EmployeeFlagType.Blacklist),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("belgi.sabab_bosh");
    }

    [Fact]
    public async Task Bir_xil_turdagi_belgi_ham_takrorlanmaydi()
    {
        // Qoida TURGA bog'liq emas: ikkita ochiq «nazorat» ham ikkita
        // kuchdagi belgi degani va ro'yxatda ikki marta ko'rinardi.
        var employee = _context.ExistingEmployee();

        // «Nazorat» — ishlab turganlar belgisi (`EmployeeFlagStage`), ya'ni
        // xodim ishlayotgan bo'lishi kerak. Aks holda test bosqich xatosiga
        // urilib, tekshirmoqchi bo'lgan qoidasiga umuman yetib bormasdi.
        _context.NowEmployed(employee.Id, _context.ExistingEmployment(employee.Id));

        _context.ExistingFlag(employee.Id, EmployeeFlagType.Monitoring);

        var natija = await Qoyuvchi.HandleAsync(
            new RaiseFlagCommand(employee.Id, EmployeeFlagType.Monitoring),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("belgi.boshqa_belgi_bor");
    }
}
