using Bonvi.Application.Employees.Probations;
using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;
using NSubstitute;

namespace Bonvi.Application.Tests.Employees;

/// <summary>
/// Sinovdan doimiy xodimga o'tkazish — bitta amalda.
/// </summary>
/// <remarks>
/// <para>
/// HR talabi so'zma-so'z: «sinovdan o'tgan bo'lsa, bir bosishda xodimlarga
/// o'tkazib, unga kerakli hamma narsani — lavozim, oklad, bo'lim — qo'yib
/// yuborsak». Ilgari bu ikki qadam edi va ikkinchisi (lavozim o'zgarishi)
/// muntazam unutilardi: odam doimiy bo'lardi-yu, «stajyor» lavozimida
/// qolib ketardi.
/// </para>
/// <para>
/// Bu yerdagi testlar aynan shu bitta amalning chegaralarini mahkamlaydi:
/// nima o'zgaradi, tarixda nima qoladi va qaysi holatda amal to'xtaydi.
/// </para>
/// </remarks>
public sealed class ProbationPromotionTests
{
    private readonly EmployeeTestContext _context = new();

    private ResolveProbationCommandHandler Xulosa => new(
        _context.Probations,
        _context.Employments,
        _context.Positions,
        _context.Departments,
        _context.Mapper,
        _context.Clock,
        _context.UnitOfWork);

    /// <summary>Sinovda turgan munosabat va unga bog'langan sinov muddati.</summary>
    private (Employment Employment, Probation Probation) Sinovdagi(decimal? oklad = null)
    {
        var employee = _context.ExistingEmployee();
        var employment = _context.ExistingEmployment(
            employee.Id,
            hireDate: EmployeeTestContext.Today.AddDays(-90),
            salary: oklad);

        var probation = Probation.Start(employment.Id, employment.HireDate, 90).Value;
        _context.Probations.GetByIdAsync(probation.Id, Arg.Any<CancellationToken>()).Returns(probation);

        return (employment, probation);
    }

    // ────────── Asosiy yo'l ──────────

    [Fact]
    public async Task Sinovdan_otgan_xodim_bir_amalda_doimiy_bolib_yangi_shartlarni_oladi()
    {
        var (employment, probation) = Sinovdagi(oklad: 4_000_000m);
        var yangiLavozim = Guid.NewGuid();
        var yangiBolim = Guid.NewGuid();

        var natija = await Xulosa.HandleAsync(
            new ResolveProbationCommand(
                probation.Id,
                ProbationStatus.Passed,
                PositionId: yangiLavozim,
                DepartmentId: yangiBolim,
                SalaryAmount: 6_000_000m,
                Rate: 1.0m,
                OrderNo: "144-b",
                Conclusion: "Sinovdan muvaffaqiyatli o'tdi"),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);

        // Holat: endi doimiy — ya'ni xodimlar ro'yxatida ko'rinadi
        employment.Status.ShouldBe(EmploymentStatus.Active);
        probation.Status.ShouldBe(ProbationStatus.Passed);

        // Yangi shartlar munosabatga tushdi
        employment.PositionId.ShouldBe(yangiLavozim);
        employment.DepartmentId.ShouldBe(yangiBolim);
        employment.Salary!.Amount.ShouldBe(6_000_000m);
        employment.Rate.ShouldBe(1.0m);

        await _context.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Tarixda IKKI hodisa qoladi, bitta emas.
    /// </summary>
    /// <remarks>
    /// «Sinovdan o'tdi» va «lavozimi ko'tarildi» — boshqa-boshqa faktlar va
    /// hisobotda alohida sanaladi. Bittaga qo'shib yuborilsa, «sinovdan
    /// keyin okladi qancha oshdi» degan savolga javob yo'qolardi.
    /// </remarks>
    [Fact]
    public async Task Tarixda_sinov_natijasi_va_lavozim_ozgarishi_alohida_qoladi()
    {
        var (employment, probation) = Sinovdagi(oklad: 4_000_000m);

        _ = await Xulosa.HandleAsync(
            new ResolveProbationCommand(
                probation.Id,
                ProbationStatus.Passed,
                PositionId: Guid.NewGuid(),
                SalaryAmount: 6_000_000m),
            TestContext.Current.CancellationToken);

        var turlar = employment.Events.Select(hodisa => hodisa.Type).ToList();

        turlar.ShouldContain(EmploymentEventType.ProbationResult);
        turlar.ShouldContain(EmploymentEventType.Promotion);

        // Oklad o'zgarishi lavozim hodisasining ICHIDA — eski va yangi
        // qiymat yonma-yon turadi, alohida qator emas.
        var kotarilish = employment.Events.Last(hodisa => hodisa.Type == EmploymentEventType.Promotion);
        kotarilish.FromSalary!.Amount.ShouldBe(4_000_000m);
        kotarilish.ToSalary!.Amount.ShouldBe(6_000_000m);
    }

    /// <summary>
    /// Faqat oklad o'zgarsa — «lavozim o'zgarishi» EMAS, «oklad o'zgarishi».
    /// </summary>
    /// <remarks>
    /// Aks holda oddiy oklad oshishi hisobotda «ko'tarilish» bo'lib
    /// ko'rinardi va «kim ko'tarildi» degan savolga yolg'on javob berardi.
    /// </remarks>
    [Fact]
    public async Task Faqat_oklad_ozgarsa_kotarilish_hodisasi_yozilmaydi()
    {
        var (employment, probation) = Sinovdagi(oklad: 4_000_000m);

        _ = await Xulosa.HandleAsync(
            new ResolveProbationCommand(
                probation.Id,
                ProbationStatus.Passed,
                SalaryAmount: 5_000_000m),
            TestContext.Current.CancellationToken);

        var turlar = employment.Events.Select(hodisa => hodisa.Type).ToList();

        turlar.ShouldContain(EmploymentEventType.SalaryChange);
        turlar.ShouldNotContain(EmploymentEventType.Promotion);
        turlar.ShouldNotContain(EmploymentEventType.Transfer);
    }

    /// <summary>Hech narsa berilmasa — eski xulq: faqat holat o'zgaradi.</summary>
    [Fact]
    public async Task Tayinlashsiz_xulosa_avvalgidek_ishlaydi()
    {
        var (employment, probation) = Sinovdagi(oklad: 4_000_000m);

        var natija = await Xulosa.HandleAsync(
            new ResolveProbationCommand(probation.Id, ProbationStatus.Passed),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        employment.Status.ShouldBe(EmploymentStatus.Active);
        employment.Salary!.Amount.ShouldBe(4_000_000m);

        employment.Events
            .Count(hodisa => hodisa.Type == EmploymentEventType.ProbationResult)
            .ShouldBe(1);
    }

    // ────────── Chegaralar ──────────

    /// <summary>
    /// Sinovdan O'TMAGAN xodimga lavozim tayinlab bo'lmaydi.
    /// </summary>
    /// <remarks>
    /// Bu deyarli har doim kiritish xatosi: HR «o'tmadi» ni tanlab, keyin
    /// pastdagi maydonlarni tozalashni unutgan. Jimgina bajarilsa, ishdan
    /// ketayotgan odamga yangi oklad yozib qo'yilardi.
    /// </remarks>
    [Theory]
    [InlineData(ProbationStatus.Failed)]
    [InlineData(ProbationStatus.LeftEarly)]
    [InlineData(ProbationStatus.Cancelled)]
    public async Task Otmagan_xodimga_lavozim_tayinlab_bolmaydi(ProbationStatus holat)
    {
        var (_, probation) = Sinovdagi();

        var natija = await Xulosa.HandleAsync(
            new ResolveProbationCommand(
                probation.Id,
                holat,
                SalaryAmount: 9_000_000m),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("sinov.tayinlash_otmagan");

        // Blok chinakam: sinov ham yopilmagan
        probation.Status.ShouldBe(ProbationStatus.InProgress);
        await _context.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Ma'lumotnomada yo'q lavozim — sinov YOPILMASDAN to'xtaydi.
    /// </summary>
    /// <remarks>
    /// Tekshiruv ataylab eng boshida: aks holda sinov yopilib, keyin
    /// lavozim topilmay, tranzaksiya bekor bo'lardi — HR esa «nima
    /// saqlandi, nima yo'q» degan holatda qolardi.
    /// </remarks>
    [Fact]
    public async Task Mavjud_bolmagan_lavozim_bilan_sinov_yopilmaydi()
    {
        var (_, probation) = Sinovdagi();
        var yoq = Guid.NewGuid();
        _context.Positions.ExistsAsync(yoq, Arg.Any<CancellationToken>()).Returns(false);

        var natija = await Xulosa.HandleAsync(
            new ResolveProbationCommand(probation.Id, ProbationStatus.Passed, PositionId: yoq),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("lavozim.topilmadi");
        probation.Status.ShouldBe(ProbationStatus.InProgress);
    }

    /// <summary>Chegaradan chiqqan stavka — domen qoidasi (0 dan katta, 2 dan oshmaydi).</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-0.5)]
    [InlineData(2.5)]
    public async Task Notogri_stavka_qabul_qilinmaydi(double stavka)
    {
        var (_, probation) = Sinovdagi();

        var natija = await Xulosa.HandleAsync(
            new ResolveProbationCommand(
                probation.Id,
                ProbationStatus.Passed,
                Rate: (decimal)stavka),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("mehnat.stavka");
    }

    /// <summary>
    /// Munosabat allaqachon yopilgan bo'lsa, tayinlashni qo'yadigan joy yo'q.
    /// </summary>
    /// <remarks>
    /// Jimgina tashlab yuborish eng yomon variant bo'lardi: HR oklad
    /// qo'ygan deb o'ylab ketardi va buni faqat oylik hisoblanganda
    /// bilib qolardi.
    /// </remarks>
    [Fact]
    public async Task Yopilgan_munosabatga_tayinlash_xato_beradi()
    {
        var (employment, probation) = Sinovdagi();
        _ = employment.Terminate(EmployeeTestContext.Today, null);

        var natija = await Xulosa.HandleAsync(
            new ResolveProbationCommand(
                probation.Id,
                ProbationStatus.Passed,
                SalaryAmount: 5_000_000m),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("sinov.tayinlash_joyi_yoq");
    }

    /// <summary>Valyuta kodi noto'g'ri bo'lsa, sinov yopilmaydi.</summary>
    [Fact]
    public async Task Notogri_valyuta_bilan_sinov_yopilmaydi()
    {
        var (_, probation) = Sinovdagi();

        var natija = await Xulosa.HandleAsync(
            new ResolveProbationCommand(
                probation.Id,
                ProbationStatus.Passed,
                SalaryAmount: 5_000_000m,
                SalaryCurrency: "SO'M"),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("pul.valyuta");
        probation.Status.ShouldBe(ProbationStatus.InProgress);
    }
}
