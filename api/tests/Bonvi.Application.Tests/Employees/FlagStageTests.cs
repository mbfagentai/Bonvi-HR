using Bonvi.Application.Employees.Employments;
using Bonvi.Application.Employees.Flags;
using Bonvi.Domain.Employees;
using NSubstitute;

namespace Bonvi.Application.Tests.Employees;

/// <summary>
/// «Belgi qaysi bosqichda qo'yiladi» qoidasi.
/// </summary>
/// <remarks>
/// <para>
/// Foydalanuvchining aniq qarori: ishlab turgan xodimga faqat «nazoratda»
/// yoki «qobiliyatli», ishdan ketganga esa faqat «rezerv» yoki «qora
/// ro'yxat» (sabab <c>EmployeeFlagStage</c> izohida).
/// </para>
/// <para>
/// <b>Nega bu testlar <see cref="SingleActiveFlagTests"/> dan ALOHIDA.</b>
/// U yerdagi qoida bitta xodimning BELGILARI orasidagi munosabat haqida,
/// bu yerdagisi esa belgi va MEHNAT MUNOSABATI orasidagi. Ikkalasi bir
/// faylga qo'shilsa, «ishlayaptimi?» sozlamasi har bir testga tarqalib,
/// birinchi qoidaning testlari o'z mavzusidan uzoqlashardi.
/// </para>
/// <para>
/// Qoidaning ikkinchi yarmi — belgilarning O'ZI yopilishi: bo'shatish
/// «nazoratda» ni, ishga qabul «rezerv» ni yopadi. U ham shu yerda, chunki
/// bu bitta qoidaning ikki tomoni: belgi noto'g'ri bosqichda TURA olmasligi
/// kerak, faqat qo'yila olmasligi emas.
/// </para>
/// </remarks>
public sealed class FlagStageTests
{
    private readonly EmployeeTestContext _context = new();

    private RaiseFlagCommandHandler Qoyuvchi => new(
        _context.Flags,
        _context.Employees,
        _context.Employments,
        _context.Clock,
        _context.UnitOfWork);

    private TerminateEmploymentCommandHandler Boshatuvchi => new(
        _context.Employments,
        _context.Probations,
        _context.TerminationReasons,
        _context.Flags,
        _context.Departments,
        _context.FlagGuard,
        _context.Mapper,
        _context.Clock,
        _context.UnitOfWork);

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

    // ────────── Qo'yish: bosqich mos kelishi kerak ──────────

    [Fact]
    public async Task Ishlayotgan_xodimga_nazorat_qoyiladi()
    {
        var employee = _context.ExistingEmployee();
        _context.NowEmployed(employee.Id, _context.ExistingEmployment(employee.Id));

        var natija = await Qoyuvchi.HandleAsync(
            new RaiseFlagCommand(employee.Id, EmployeeFlagType.Monitoring),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);
        _context.Flags.Received(1).Add(Arg.Any<EmployeeFlag>());
    }

    [Fact]
    public async Task Ishlayotgan_xodimga_rezerv_qoyilmaydi()
    {
        var employee = _context.ExistingEmployee();
        _context.NowEmployed(employee.Id, _context.ExistingEmployment(employee.Id));

        var natija = await Qoyuvchi.HandleAsync(
            new RaiseFlagCommand(employee.Id, EmployeeFlagType.Reserve),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("belgi.xodim_ishlayapti");

        _context.Flags.DidNotReceive().Add(Arg.Any<EmployeeFlag>());
        await _context.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Ketgan_xodimga_rezerv_qoyiladi()
    {
        // Sukut bo'yicha kontekstda faol munosabat yo'q — ya'ni xodim ketgan.
        var employee = _context.ExistingEmployee();

        var natija = await Qoyuvchi.HandleAsync(
            new RaiseFlagCommand(employee.Id, EmployeeFlagType.Reserve),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);
        _context.Flags.Received(1).Add(Arg.Any<EmployeeFlag>());
    }

    [Fact]
    public async Task Ketgan_xodimga_nazorat_qoyilmaydi()
    {
        var employee = _context.ExistingEmployee();

        var natija = await Qoyuvchi.HandleAsync(
            new RaiseFlagCommand(employee.Id, EmployeeFlagType.Monitoring),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("belgi.xodim_ketgan");
    }

    /// <summary>
    /// Bosqich xatosini tasdiq OCHMAYDI.
    /// </summary>
    /// <remarks>
    /// «Bir vaqtda bitta belgi» qoidasidan asosiy farqi shu: u yerda
    /// <c>confirm=true</c> yo'l ochadi, chunki ikkala holat ham ma'noli.
    /// Bu yerda esa saqlanadigan ma'no yo'q va tasdiq hech narsani
    /// o'zgartirmasligi kerak — aks holda interfeys «tasdiqlang» tugmasini
    /// chizib, HR ni bekorga bosishga majbur qilardi.
    /// </remarks>
    [Fact]
    public async Task Bosqich_xatosi_tasdiq_bilan_ham_otmaydi()
    {
        var employee = _context.ExistingEmployee();
        _context.NowEmployed(employee.Id, _context.ExistingEmployment(employee.Id));

        var natija = await Qoyuvchi.HandleAsync(
            new RaiseFlagCommand(employee.Id, EmployeeFlagType.Blacklist, Reason: "o'g'irlik", Confirm: true),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("belgi.xodim_ishlayapti");
    }

    /// <summary>
    /// Bosqich kuchdagi belgidan OLDIN tekshiriladi.
    /// </summary>
    /// <remarks>
    /// Aks holda HR avval «eskisini yopamizmi?» degan savolga javob berib,
    /// keyin «bu belgi bu yerda qo'yilmaydi» xatosini olardi — ya'ni bir
    /// qadam bekorga.
    /// </remarks>
    [Fact]
    public async Task Bosqich_kuchdagi_belgidan_oldin_tekshiriladi()
    {
        var employee = _context.ExistingEmployee();
        _context.NowEmployed(employee.Id, _context.ExistingEmployment(employee.Id));
        _context.ExistingFlag(employee.Id, EmployeeFlagType.Monitoring);

        var natija = await Qoyuvchi.HandleAsync(
            new RaiseFlagCommand(employee.Id, EmployeeFlagType.Reserve),
            TestContext.Current.CancellationToken);

        natija.Error.Code.ShouldBe("belgi.xodim_ishlayapti");
    }

    /// <summary>
    /// O'rindosh xodim ikkinchi firmadan ketsa ham ISHLAYOTGAN hisoblanadi.
    /// </summary>
    /// <remarks>
    /// Belgi SHAXSGA qo'yiladi, munosabatga emas: «VIKOR + Вело унион 0,5
    /// ставка ўриндош» Excel'da odatiy yozuv edi va bunday odam bitta
    /// firmadan ketganda ham bizda ishlashda davom etadi.
    /// </remarks>
    [Fact]
    public async Task Orindosh_xodim_bitta_firmadan_ketsa_ham_ishlayotgan_hisoblanadi()
    {
        var employee = _context.ExistingEmployee();
        _context.NowEmployed(employee.Id, _context.ExistingEmployment(employee.Id));

        var natija = await Qoyuvchi.HandleAsync(
            new RaiseFlagCommand(employee.Id, EmployeeFlagType.Capable),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);
    }

    // ────────── Bo'shatish: ishlab turganlar belgisi yopiladi ──────────

    [Fact]
    public async Task Boshatishda_nazorat_belgisi_yopiladi()
    {
        var employee = _context.ExistingEmployee();
        var employment = _context.ExistingEmployment(employee.Id);
        var flag = _context.ExistingFlag(employee.Id, EmployeeFlagType.Monitoring);

        // Bazada munosabat hali FAOL: u xotirada tugatiladi, lekin
        // `GetActiveByEmployeeAsync` — `AsNoTracking` so'rov.
        _context.NowEmployed(employee.Id, employment);

        var natija = await Boshatuvchi.HandleAsync(
            new TerminateEmploymentCommand(employment.Id, Confirm: true),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);

        // Yozuv O'CHIRILMAYDI — tarix qoladi, faqat muddati yopiladi
        flag.EndDate.ShouldBe(EmployeeTestContext.Today);
        flag.Reason.ShouldNotBeNull();
        flag.Reason.ShouldContain("ishdan ketdi");
    }

    [Fact]
    public async Task Boshqa_firmada_ishlayotgan_bolsa_nazorat_yopilmaydi()
    {
        var employee = _context.ExistingEmployee();
        var ketayotgan = _context.ExistingEmployment(employee.Id);
        var qoladigan = _context.ExistingEmployment(employee.Id);
        var flag = _context.ExistingFlag(employee.Id, EmployeeFlagType.Monitoring);

        _context.NowEmployed(employee.Id, ketayotgan, qoladigan);

        // `ExistingEmployment` ikkinchi chaqiruvda `GetWithEventsAsync` ni
        // qayta yozadi — bo'shatiladigani ATAYLAB qaytariladi.
        _context.Employments
            .GetWithEventsAsync(ketayotgan.Id, Arg.Any<CancellationToken>())
            .Returns(ketayotgan);

        var natija = await Boshatuvchi.HandleAsync(
            new TerminateEmploymentCommand(ketayotgan.Id, Confirm: true),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);

        // Odam hali ham bizda ishlayapti — nazorat kuchda qoladi
        flag.EndDate.ShouldBeNull();
    }

    /// <summary>
    /// Orqaga sana bilan bo'shatilganda ham belgi yopiladi.
    /// </summary>
    /// <remarks>
    /// Buyruq kechikib kiritilganda ketish sanasi belgining qo'yilgan
    /// kunidan OLDIN bo'lishi mumkin. Belgilar o'sha sana bo'yicha
    /// izlansa, bunday yozuv umuman topilmasdi
    /// (<c>GetInForceAsync</c> — <c>StartDate &lt;= sana</c>) va ketgan
    /// odamda ochiq «nazorat» bo'lib qolardi.
    /// </remarks>
    [Fact]
    public async Task Orqaga_sana_bilan_boshatishda_ham_nazorat_yopiladi()
    {
        var employee = _context.ExistingEmployee();
        var employment = _context.ExistingEmployment(employee.Id);
        _context.NowEmployed(employee.Id, employment);

        // Belgi BUGUN qo'yilgan, bo'shatish esa bir oy oldingi sana bilan
        var flag = _context.ExistingFlag(
            employee.Id,
            EmployeeFlagType.Monitoring,
            startDate: EmployeeTestContext.Today);

        var ketgan = EmployeeTestContext.Today.AddMonths(-1);

        var natija = await Boshatuvchi.HandleAsync(
            new TerminateEmploymentCommand(employment.Id, ketgan, Confirm: true),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);

        // O'z boshlanish sanasidan oldin yopilmaydi — o'sha kunning o'zida
        flag.EndDate.ShouldBe(EmployeeTestContext.Today);
    }

    /// <summary>
    /// Ketish belgisi bo'shatishda O'ZI qo'yilmaydi.
    /// </summary>
    /// <remarks>
    /// «Ketdi» degani «rezervda» degani emas — bu HR ning qarori. Tizim
    /// uni taxmin qilsa, rezerv ro'yxati bir yilda barcha ketganlarning
    /// ro'yxatiga aylanib, ma'nosini yo'qotardi.
    /// </remarks>
    [Fact]
    public async Task Boshatish_ozi_ketish_belgisini_qoymaydi()
    {
        var employee = _context.ExistingEmployee();
        var employment = _context.ExistingEmployment(employee.Id);
        _context.NowEmployed(employee.Id, employment);

        var natija = await Boshatuvchi.HandleAsync(
            new TerminateEmploymentCommand(employment.Id),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);
        _context.Flags.DidNotReceive().Add(Arg.Any<EmployeeFlag>());
    }

    // ────────── Ishga qabul: ketish belgisi yopiladi ──────────

    /// <summary>
    /// Rezervdagi odam ishga olinsa, belgi yopiladi.
    /// </summary>
    /// <remarks>
    /// Rezerv — «hozir olinmadi, lekin kerak bo'lsa chaqirsa bo'ladi».
    /// Odam chaqirildi va keldi: belgi bajarildi. Ochiq qoldirilsa,
    /// «rezerv» ro'yxatida ishlab turgan xodim turib qolardi.
    /// </remarks>
    [Fact]
    public async Task Qayta_ishga_olishda_rezerv_yopiladi()
    {
        var employee = _context.ExistingEmployee();
        var flag = _context.ExistingFlag(employee.Id, EmployeeFlagType.Reserve);

        var natija = await Qabul.HandleAsync(
            new HireEmployeeCommand(Guid.NewGuid(), EmployeeId: employee.Id),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);

        flag.EndDate.ShouldBe(EmployeeTestContext.Today);
        flag.Reason.ShouldNotBeNull();
        flag.Reason.ShouldContain("qayta ishga olindi");

        _context.Flags.DidNotReceive().Remove(Arg.Any<EmployeeFlag>());
    }
}
