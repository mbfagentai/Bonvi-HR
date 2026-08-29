using Bonvi.Application.Employees.Commands;
using Bonvi.Application.Employees.Employments;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using NSubstitute;

namespace Bonvi.Application.Tests.Employees;

/// <summary>
/// Ishga qabul — yagona formaning qoidalari.
/// </summary>
/// <remarks>
/// Bu yerda tekshiriladigan uchala qoida ham HR'ning aniq talabidan kelib
/// chiqqan: qora ro'yxat bloki, stavka nazorati va sinov muddatining
/// avtomatik ochilishi.
/// </remarks>
public sealed class HireEmployeeTests
{
    private readonly EmployeeTestContext _context = new();

    private HireEmployeeCommandHandler Qabul => new(
        _context.Employees,
        _context.Employments,
        _context.Probations,
        _context.Positions,
        _context.LegalEntities,
        _context.Flags,
        _context.FlagGuard,
        _context.Mapper,
        _context.Clock,
        _context.UnitOfWork);

    // ────────── Dublikat munosabat: BLOK ──────────

    /// <summary>
    /// Bir odamni AYNAN SHU firmaga ikkinchi marta ishga olib bo'lmaydi.
    /// </summary>
    /// <remarks>
    /// Bu xato jimgina yuradi: ro'yxat har shaxsni baribir bir marta
    /// chizadi, ya'ni ekranda hech narsa o'zgarmaydi — lekin stavka,
    /// oklad va hisobotdagi xodimlar soni ikki barobar bo'lib ketadi.
    /// HR buni faqat oylik hisoblanganda sezardi.
    /// </remarks>
    [Fact]
    public async Task Shu_firmada_ishlayotgan_xodimni_qayta_ishga_olib_bolmaydi()
    {
        var employee = _context.ExistingEmployee();
        var employment = _context.ExistingEmployment(employee.Id);

        _context.Employments
            .GetActiveByEmployeeAsync(employee.Id, Arg.Any<CancellationToken>())
            .Returns([employment]);

        var natija = await Qabul.HandleAsync(
            new HireEmployeeCommand(employment.LegalEntityId, EmployeeId: employee.Id),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("mehnat.shu_firmada_bor");

        await _context.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// O'rindoshlik — BOSHQA firmaga qabul to'silmaydi.
    /// </summary>
    /// <remarks>
    /// Bonvi holding: «VIKOR + Вело унион 0,5 ставка ўриндош» Excel'da
    /// odatiy yozuv edi. Dublikat tekshiruvi buni buzmasligi shart —
    /// aks holda qoida haqiqiy ish jarayonini to'xtatib qo'yardi.
    /// </remarks>
    [Fact]
    public async Task Boshqa_firmaga_orindosh_qabul_toqnashmaydi()
    {
        var employee = _context.ExistingEmployee();
        var employment = _context.ExistingEmployment(employee.Id);

        _context.Employments
            .GetActiveByEmployeeAsync(employee.Id, Arg.Any<CancellationToken>())
            .Returns([employment]);

        var natija = await Qabul.HandleAsync(
            new HireEmployeeCommand(Guid.NewGuid(), EmployeeId: employee.Id, Rate: 0.5m),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);
        await _context.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Ishdan ketgan xodimni SHU firmaga qayta olish mumkin.
    /// </summary>
    /// <remarks>
    /// Tekshiruv faqat OCHIQ munosabatlarga qaraydi
    /// (<c>GetActiveByEmployeeAsync</c>). Qaytib kelgan odam yangi
    /// munosabat oladi, eskisi esa tarixda qoladi — domen shunday
    /// qurilgan va bu qoida uni buzmasligi kerak.
    /// </remarks>
    [Fact]
    public async Task Ishdan_ketgan_xodimni_shu_firmaga_qayta_olish_mumkin()
    {
        var employee = _context.ExistingEmployee();

        // Ochiq munosabat yo'q — hammasi yakunlangan
        _context.Employments
            .GetActiveByEmployeeAsync(employee.Id, Arg.Any<CancellationToken>())
            .Returns([]);

        var natija = await Qabul.HandleAsync(
            new HireEmployeeCommand(Guid.NewGuid(), EmployeeId: employee.Id),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);
    }

    // ────────── Qora ro'yxat: BLOK ──────────

    [Fact]
    public async Task Qora_royxatdagi_xodimni_qayta_ishga_olib_bolmaydi()
    {
        var employee = _context.ExistingEmployee();
        _context.GiveFlag(employee.Id, EmployeeFlagType.Blacklist);

        var natija = await Qabul.HandleAsync(
            new HireEmployeeCommand(Guid.NewGuid(), EmployeeId: employee.Id),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("xodim.qora_royxatda");

        // Blok chinakam bo'lishi kerak: hech narsa saqlanmagan
        await _context.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Nazoratdagi_xodim_ishga_olinadi_lekin_ogohlantirish_qaytadi()
    {
        var employee = _context.ExistingEmployee();
        _context.GiveFlag(employee.Id, EmployeeFlagType.Monitoring);

        var natija = await Qabul.HandleAsync(
            new HireEmployeeCommand(Guid.NewGuid(), EmployeeId: employee.Id),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);
        natija.Value.Warnings.ShouldContain(w => w.Code == "xodim.nazoratda");

        // Ogohlantirish — bu blok emas: yozuv saqlanadi
        await _context.UnitOfWork.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ────────── Stavka nazorati ──────────

    [Fact]
    public async Task Stavkalar_yigindisi_1_5_dan_oshsa_ogohlantirish_beriladi()
    {
        var employee = _context.ExistingEmployee();

        _context.Employments
            .GetTotalActiveRateAsync(employee.Id, Arg.Any<CancellationToken>())
            .Returns(1.0m);

        var natija = await Qabul.HandleAsync(
            new HireEmployeeCommand(Guid.NewGuid(), EmployeeId: employee.Id, Rate: 0.75m),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);
        natija.Value.Warnings.ShouldContain(w => w.Code == "mehnat.stavka_oshdi");
    }

    [Fact]
    public async Task Chegaraga_teng_stavka_ogohlantirmaydi()
    {
        // 1.5 — ruxsat etilgan chegara, undan OSHGANI ogohlantiradi
        var employee = _context.ExistingEmployee();

        _context.Employments
            .GetTotalActiveRateAsync(employee.Id, Arg.Any<CancellationToken>())
            .Returns(1.0m);

        var natija = await Qabul.HandleAsync(
            new HireEmployeeCommand(Guid.NewGuid(), EmployeeId: employee.Id, Rate: 0.5m),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.Warnings.ShouldBeEmpty();
    }

    // ────────── Sinov muddati AVTOMATIK ──────────

    [Fact]
    public async Task Sinov_muddati_lavozimdagi_kunlar_boyicha_ochiladi()
    {
        var employee = _context.ExistingEmployee();
        var position = _context.ExistingPosition(probationDays: 45);

        Probation? saqlangan = null;
        _context.Probations.When(r => r.Add(Arg.Any<Probation>()))
            .Do(call => saqlangan = call.Arg<Probation>());

        var natija = await Qabul.HandleAsync(
            new HireEmployeeCommand(
                Guid.NewGuid(),
                EmployeeId: employee.Id,
                PositionId: position.Id,
                HireDate: EmployeeTestContext.Today),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);

        saqlangan.ShouldNotBeNull();
        saqlangan.EndDate.ShouldBe(EmployeeTestContext.Today.AddDays(45));
        natija.Value.Employment.Probation.ShouldNotBeNull();
    }

    [Fact]
    public async Task Lavozimda_muddat_korsatilmasa_90_kun_qollanadi()
    {
        var employee = _context.ExistingEmployee();

        Probation? saqlangan = null;
        _context.Probations.When(r => r.Add(Arg.Any<Probation>()))
            .Do(call => saqlangan = call.Arg<Probation>());

        var natija = await Qabul.HandleAsync(
            new HireEmployeeCommand(
                Guid.NewGuid(),
                EmployeeId: employee.Id,
                HireDate: EmployeeTestContext.Today),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        saqlangan.ShouldNotBeNull();
        saqlangan.EndDate.ShouldBe(EmployeeTestContext.Today.AddDays(HireEmployeeCommandHandler.DefaultProbationDays));
    }

    // ────────── Sinovsiz qabul: BLOK ──────────

    /// <summary>
    /// Sinovsiz ishga olib bo'lmaydi — hech qanday yo'l bilan.
    /// </summary>
    /// <remarks>
    /// Foydalanuvchining aniq qarori: «hamma sinovdan o'tadi, istisnosiz».
    /// Ilgari <c>ProbationDays: 0</c> alohida tarmoqni ochardi: sinov yozuvi
    /// yaratilmasdi va munosabat darhol «faol» bo'lardi. Natija jimgina
    /// yomon edi — odam <c>/sinov</c> ro'yxatida umuman ko'rinmasdi va uni
    /// hech kim baholamasdi.
    /// </remarks>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Sinovsiz_ishga_qabul_qilib_bolmaydi(int kun)
    {
        var employee = _context.ExistingEmployee();

        var natija = await Qabul.HandleAsync(
            new HireEmployeeCommand(Guid.NewGuid(), EmployeeId: employee.Id, ProbationDays: kun),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("sinov.majburiy");

        // Blok chinakam: na munosabat, na sinov saqlangan
        _context.Probations.DidNotReceive().Add(Arg.Any<Probation>());
        await _context.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Ma'lumotnomadagi eski nol qabulni TO'XTATMAYDI — standart 90 kun olinadi.
    /// </summary>
    /// <remarks>
    /// Ma'lumotnoma endi nolni saqlay olmaydi, lekin bazada Excel'dan
    /// ko'chirilgan eski qatorlar qolgan bo'lishi mumkin. HR bunga aybdor
    /// emas — u shunchaki lavozimni tanlagan. Butun ishga qabulni to'xtatib,
    /// avval ma'lumotnomani tuzatishga majburlash ish jarayonini to'sardi.
    /// </remarks>
    [Fact]
    public async Task Lavozimdagi_eski_nol_standart_muddatga_tushadi()
    {
        var employee = _context.ExistingEmployee();
        var position = _context.ExistingPosition();
        EskiNolQiymat(position);

        Probation? saqlangan = null;
        _context.Probations.When(r => r.Add(Arg.Any<Probation>()))
            .Do(call => saqlangan = call.Arg<Probation>());

        var natija = await Qabul.HandleAsync(
            new HireEmployeeCommand(
                Guid.NewGuid(),
                EmployeeId: employee.Id,
                PositionId: position.Id,
                HireDate: EmployeeTestContext.Today),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);
        saqlangan.ShouldNotBeNull();
        saqlangan.EndDate.ShouldBe(EmployeeTestContext.Today.AddDays(HireEmployeeCommandHandler.DefaultProbationDays));
    }

    // ────────── Dublikatga qarshi ──────────

    [Fact]
    public async Task JSHSHIR_boyicha_topilgan_xodimga_YANGI_karta_ochilmaydi()
    {
        // Odam ketib, keyin qaytib kelgan holat: shaxs o'sha-o'sha bo'lishi
        // kerak, aks holda uning mehnat tarixi ikkiga bo'linadi.
        var mavjud = _context.ExistingEmployee("Tursunov", "Baxtiyor");

        _context.Employees
            .FindByPinflAsync(Arg.Any<Domain.ValueObjects.Pinfl>(), Arg.Any<CancellationToken>())
            .Returns(mavjud);

        var natija = await Qabul.HandleAsync(
            new HireEmployeeCommand(
                Guid.NewGuid(),
                Person: new SaveEmployeeCommand("Tursunov", "Baxtiyor", Pinfl: "31507851234568")),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);
        natija.Value.Employment.EmployeeId.ShouldBe(mavjud.Id);

        _context.Employees.DidNotReceive().Add(Arg.Any<Domain.Employees.Employee>());
    }

    /// <summary>
    /// Lavozimga Excel'dan qolgan «0 kun» qiymatini qo'yadi.
    /// </summary>
    /// <remarks>
    /// Domen orqali bunday holatni yasab bo'lmaydi — <c>SetProbationDays(0)</c>
    /// endi rad etadi. Lekin BAZADA bunday qator qolgan bo'lishi mumkin va
    /// tekshirilayotgan xulq aynan shu haqda. Yagona yo'l — EF Core
    /// materializatsiyasi qiladigan ishni takrorlash: xususiyatni yopiq
    /// o'rnatgich orqali to'g'ridan-to'g'ri yozish.
    /// </remarks>
    private static void EskiNolQiymat(Position position)
        => typeof(Position)
            .GetProperty(nameof(Position.ProbationDays))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(position, [0]);
}
