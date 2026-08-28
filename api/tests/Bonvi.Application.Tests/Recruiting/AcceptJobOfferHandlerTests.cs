using Bonvi.Application.Recruiting.Offers;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Recruiting;
using NSubstitute;

namespace Bonvi.Application.Tests.Recruiting;

/// <summary>
/// Voronkaning oxirgi qadami: taklif qabul qilinadi va nomzod XODIMGA
/// aylanadi.
/// </summary>
/// <remarks>
/// Bu — 1-bosqich bilan 2-bosqichni bog'laydigan yagona nuqta, shu sababli
/// eng zich qoplanadigan joy. Testlar ishga qabulning MOCK qilingan
/// nusxasini emas, HAQIQIY <c>HireEmployeeCommandHandler</c> ni ishlatadi:
/// aks holda «sinov muddati ochiladimi» degan asosiy savol tekshirilmasdan
/// qolardi.
/// </remarks>
public sealed class AcceptJobOfferHandlerTests
{
    private readonly RecruitingTestContext _context = new();

    private AcceptJobOfferCommandHandler Qabul => new(
        _context.Offers,
        _context.Candidates,
        _context.Vacancies,
        _context.Employees.Employments,
        _context.Hire,
        _context.Mapper,
        _context.Clock);

    [Fact]
    public async Task Nomavjud_taklif_qabul_qilinmaydi()
    {
        var natija = await Qabul.HandleAsync(
            new AcceptJobOfferCommand(Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("taklif.topilmadi");
    }

    [Fact]
    public async Task Javob_berilgan_taklif_qayta_qabul_qilinmaydi()
    {
        var (nomzod, _, taklif) = Muhit();
        taklif.Decline(RecruitingTestContext.Today);

        var natija = await Qabul.HandleAsync(
            new AcceptJobOfferCommand(taklif.Id), TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("taklif.javob_berilgan");
        nomzod.Status.ShouldBe(CandidateStatus.InProgress, "nomzod holatiga tegilmaydi");
    }

    [Fact]
    public async Task Allaqachon_ishga_olingan_nomzod_ikkinchi_marta_olinmaydi()
    {
        var (nomzod, _, taklif) = Muhit();
        nomzod.MarkHired(RecruitingTestContext.Today);

        var natija = await Qabul.HandleAsync(
            new AcceptJobOfferCommand(taklif.Id), TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.allaqachon_ishga_olingan");
    }

    [Fact]
    public async Task Ishga_qabul_HODISASI_topilsa_ham_ikkinchi_karta_yaratilmaydi()
    {
        // Ikkinchi to'siq: nomzodning holati qandaydir sabab bilan
        // yozilmay qolgan bo'lsa ham, ma'lumotning O'ZI («bu nomzoddan
        // ishga qabul hodisasi bor») takroriy qabulni to'sadi.
        var (nomzod, _, taklif) = Muhit();

        var mavjud = Employment
            .Hire(Guid.CreateVersion7(), Guid.CreateVersion7(), RecruitingTestContext.Today, candidateId: nomzod.Id)
            .Value;

        _context.Employees.Employments
            .FindByCandidateAsync(nomzod.Id, Arg.Any<CancellationToken>())
            .Returns(mavjud);

        var natija = await Qabul.HandleAsync(
            new AcceptJobOfferCommand(taklif.Id), TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.allaqachon_ishga_olingan");
        _context.Employees.Employees.DidNotReceive().Add(Arg.Any<Employee>());
    }

    [Fact]
    public async Task Qabul_qilinganda_xodim_yaratiladi_va_sinov_muddati_ochiladi()
    {
        var (nomzod, vakansiya, taklif) = Muhit();

        var natija = await Qabul.HandleAsync(
            new AcceptJobOfferCommand(taklif.Id, OrderNo: "185-b"),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);

        // 1. Taklif yopildi
        natija.Value.Offer.Status.ShouldBe(JobOfferStatus.Accepted);

        // 2. Nomzod voronkadan chiqdi
        natija.Value.Candidate.Status.ShouldBe(CandidateStatus.Hired);
        nomzod.IsActive.ShouldBeFalse();

        // 3. Xodim va mehnat munosabati yaratildi — vakansiya va taklif
        //    shartlari bilan
        var mehnat = natija.Value.Hire.Employment;
        mehnat.LegalEntityId.ShouldBe(vakansiya.LegalEntityId);
        mehnat.PositionId.ShouldBe(vakansiya.PositionId);
        mehnat.HireDate.ShouldBe(taklif.StartDate, "ishga qabul sanasi TAKLIFdan keladi");
        mehnat.HireOrderNo.ShouldBe("185-b");

        // 4. Sinov muddati MAJBURIY va u avtomatik ochiladi
        mehnat.Status.ShouldBe(EmploymentStatus.Probation);
        mehnat.Probation.ShouldNotBeNull();
        mehnat.Probation.EndDate.ShouldBe(taklif.StartDate.AddDays(90), "lavozimsiz standart — 90 kun");

        _context.Employees.Employees.Received(1).Add(Arg.Any<Employee>());
        _context.Employees.Probations.Received(1).Add(Arg.Any<Probation>());
    }

    [Fact]
    public async Task Nomzodning_malumoti_xodim_kartasiga_kochadi()
    {
        var (nomzod, _, taklif) = Muhit();

        Employee? yaratilgan = null;
        _context.Employees.Employees
            .When(repo => repo.Add(Arg.Any<Employee>()))
            .Do(call => yaratilgan = call.Arg<Employee>());

        var natija = await Qabul.HandleAsync(
            new AcceptJobOfferCommand(taklif.Id), TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);

        yaratilgan.ShouldNotBeNull();
        yaratilgan.FullName.Full.ShouldBe(nomzod.FullName.Full);
        yaratilgan.Phone!.Value.ShouldBe(nomzod.Phone!.Value);
        yaratilgan.Speciality.ShouldBe(nomzod.Speciality);
    }

    [Fact]
    public async Task Ishga_qabul_HODISASIDA_nomzodga_havola_qoladi()
    {
        // «Bu xodim qayerdan keldi?» degan savolning javobi shu yerda —
        // bog'lanish ishga qabulning O'ZI bilan bitta tranzaksiyada yoziladi.
        var (nomzod, _, taklif) = Muhit();

        Employment? mehnat = null;
        _context.Employees.Employments
            .When(repo => repo.Add(Arg.Any<Employment>()))
            .Do(call => mehnat = call.Arg<Employment>());

        var natija = await Qabul.HandleAsync(
            new AcceptJobOfferCommand(taklif.Id), TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);

        mehnat.ShouldNotBeNull();
        var qabul = mehnat.Events.Single(hodisa => hodisa.Type == EmploymentEventType.Hire);
        qabul.CandidateId.ShouldBe(nomzod.Id);
    }

    [Fact]
    public async Task Sinovsiz_qabul_YOQ()
    {
        // 1-bosqichning qoidasi rekruting orqali ham chetlab o'tilmasligi
        // kerak: «hamma sinovdan o'tadi, istisnosiz».
        var (nomzod, _, taklif) = Muhit();

        var natija = await Qabul.HandleAsync(
            new AcceptJobOfferCommand(taklif.Id, ProbationDays: 0),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("sinov.majburiy");
        nomzod.Status.ShouldBe(
            CandidateStatus.Hired,
            "o'zgarish XOTIRADA qoladi, lekin saqlanmaydi — SaveChanges chaqirilmagan");

        await _context.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Hammasi_BITTA_tranzaksiyada_saqlanadi()
    {
        // Taklif va nomzod AVVAL o'zgartiriladi, ishga qabul OXIRIDA
        // chaqiriladi va uning yagona `SaveChangesAsync` chaqiruvi
        // uchalasini birga yozadi. Ikkinchi saqlash bo'lsa, «xodim
        // yaratildi, nomzod voronkada qoldi» holati mumkin bo'lardi.
        var (_, _, taklif) = Muhit();

        var natija = await Qabul.HandleAsync(
            new AcceptJobOfferCommand(taklif.Id), TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);
        await _context.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Qora_royxatdagi_odam_uchun_qabul_toxtaydi()
    {
        // Ishga qabulning MAVJUD qoidasi rekruting orqali ham ishlaydi:
        // mavjud xodim kartasi ko'rsatilgan bo'lsa va u qora ro'yxatda
        // bo'lsa, qabul bloklanadi.
        var (nomzod, _, taklif) = Muhit();
        var xodim = _context.Employees.ExistingEmployee();
        _context.Employees.GiveFlag(xodim.Id, EmployeeFlagType.Blacklist);

        var natija = await Qabul.HandleAsync(
            new AcceptJobOfferCommand(taklif.Id, EmployeeId: xodim.Id),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("xodim.qora_royxatda");
        nomzod.Status.ShouldBe(CandidateStatus.Hired, "xotirada o'zgardi, lekin saqlanmadi");
        await _context.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Standart muhit: ochiq vakansiya, voronkadagi nomzod va unga yuborilgan taklif.</summary>
    private (Candidate Candidate, Vacancy Vacancy, JobOffer Offer) Muhit()
    {
        var vakansiya = _context.ExistingVacancy();
        var anketa = _context.ExistingStage();
        _context.Funnel(anketa);

        var nomzod = _context.ExistingCandidate(vakansiya, anketa);
        var taklif = _context.ExistingOffer(nomzod, vakansiya);

        return (nomzod, vakansiya, taklif);
    }
}
