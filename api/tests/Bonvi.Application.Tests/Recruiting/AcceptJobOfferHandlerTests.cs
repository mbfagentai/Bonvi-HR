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
    /// <summary>
    /// Testlardagi firma.
    /// </summary>
    /// <remarks>
    /// Alohida mock kerak emas: <c>EmployeeTestContext</c> firmalar
    /// ma'lumotnomasi uchun «har qanday identifikator mavjud» deb
    /// javob beradi. Muhimi shu identifikator BUYRUQDAN mehnat
    /// munosabatiga o'zgarmasdan yetib borishi.
    /// </remarks>
    private static readonly Guid FirmaId = Guid.CreateVersion7();

    private readonly RecruitingTestContext _context = new();

    private AcceptJobOfferCommandHandler Qabul => new(
        _context.Offers,
        _context.Candidates,
        _context.Employees.Employments,
        _context.Employees.Departments,
        _context.Hire,
        _context.Mapper,
        _context.Clock);

    /// <summary>Firma, bo'lim va lavozimsiz bu amal endi umuman bajarilmaydi.</summary>
    /// <remarks>
    /// Ilgari uchalasi vakansiyadan kelardi va buyruqda ular umuman yo'q
    /// edi. Endi vakansiya faqat e'lon (sababi <c>Vacancy</c> izohida), shu
    /// sababli HAR BIR test ularni ataylab beradi — bu yordamchi o'sha
    /// takrorni bir joyga yig'adi va shu bilan birga «bu qiymatlar
    /// qayerdan keladi» degan savolni ochiq qoldirmaydi.
    /// </remarks>
    private AcceptJobOfferCommand Buyruq(
        Guid taklifId,
        Guid? lavozimId = null,
        Guid? firmaId = null,
        Guid? bolimId = null,
        string? buyruqRaqami = null,
        int? sinovKunlari = null,
        Guid? xodimId = null)
        => new(
            taklifId,
            firmaId ?? FirmaId,
            lavozimId ?? _context.Employees.ExistingPosition().Id,
            bolimId,
            OrderNo: buyruqRaqami,
            EmployeeId: xodimId,
            ProbationDays: sinovKunlari);

    [Fact]
    public async Task Nomavjud_taklif_qabul_qilinmaydi()
    {
        var natija = await Qabul.HandleAsync(
            Buyruq(Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("taklif.topilmadi");
    }

    [Fact]
    public async Task Javob_berilgan_taklif_qayta_qabul_qilinmaydi()
    {
        var (nomzod, _, taklif) = Muhit();
        taklif.Decline(RecruitingTestContext.Today);

        var natija = await Qabul.HandleAsync(
            Buyruq(taklif.Id), TestContext.Current.CancellationToken);

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
            Buyruq(taklif.Id), TestContext.Current.CancellationToken);

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
            Buyruq(taklif.Id), TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.allaqachon_ishga_olingan");
        _context.Employees.Employees.DidNotReceive().Add(Arg.Any<Employee>());
    }

    [Fact]
    public async Task Qabul_qilinganda_xodim_yaratiladi_va_sinov_muddati_ochiladi()
    {
        var (nomzod, _, taklif) = Muhit();

        var lavozim = _context.Employees.ExistingPosition();

        var natija = await Qabul.HandleAsync(
            Buyruq(taklif.Id, lavozimId: lavozim.Id, buyruqRaqami: "185-b"),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);

        // 1. Taklif yopildi
        natija.Value.Offer.Status.ShouldBe(JobOfferStatus.Accepted);

        // 2. Nomzod voronkadan chiqdi
        natija.Value.Candidate.Status.ShouldBe(CandidateStatus.Hired);
        nomzod.IsActive.ShouldBeFalse();

        // 3. Xodim va mehnat munosabati yaratildi. Firma va lavozim
        //    BUYRUQDAN keladi (vakansiyadan EMAS — u endi faqat e'lon),
        //    ish boshlash sanasi esa taklifdan.
        var mehnat = natija.Value.Hire.Employment;
        mehnat.LegalEntityId.ShouldBe(FirmaId);
        mehnat.PositionId.ShouldBe(lavozim.Id);
        mehnat.HireDate.ShouldBe(taklif.StartDate, "ishga qabul sanasi TAKLIFdan keladi");
        mehnat.HireOrderNo.ShouldBe("185-b");

        // 4. Sinov muddati MAJBURIY va u avtomatik ochiladi
        mehnat.Status.ShouldBe(EmploymentStatus.Probation);
        mehnat.Probation.ShouldNotBeNull();
        mehnat.Probation.EndDate.ShouldBe(
            taklif.StartDate.AddDays(90),
            "lavozimda muddat ko'rsatilmagan — umumiy qoida 90 kun");

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
            Buyruq(taklif.Id), TestContext.Current.CancellationToken);

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
            Buyruq(taklif.Id), TestContext.Current.CancellationToken);

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
            Buyruq(taklif.Id, sinovKunlari: 0),
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
            Buyruq(taklif.Id), TestContext.Current.CancellationToken);

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
            Buyruq(taklif.Id, xodimId: xodim.Id),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("xodim.qora_royxatda");
        nomzod.Status.ShouldBe(CandidateStatus.Hired, "xotirada o'zgardi, lekin saqlanmadi");
        await _context.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Lavozimsiz_qabul_qilinmaydi()
    {
        // ⭐ Rekruting qatlamining O'Z qoidasi: foydalanuvchi «аниқ лавозим
        // танланади» dedi. `HireEmployeeCommand.PositionId` ning o'zi
        // ixtiyoriyligicha qoladi (HR formasi va ETL unga tayanadi) — bu
        // majburiylik FAQAT shu yerda.
        var (nomzod, _, taklif) = Muhit();

        var natija = await Qabul.HandleAsync(
            Buyruq(taklif.Id, lavozimId: Guid.Empty), TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("taklif.lavozim_yoq");
        nomzod.Status.ShouldBe(
            CandidateStatus.InProgress,
            "tekshiruv eng boshida — taklif va nomzodga umuman tegilmaydi");

        await _context.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Yuridik_shaxssiz_qabul_qilinmaydi()
    {
        var (nomzod, _, taklif) = Muhit();

        var natija = await Qabul.HandleAsync(
            Buyruq(taklif.Id, firmaId: Guid.Empty), TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("taklif.yuridik_shaxs_yoq");
        nomzod.Status.ShouldBe(CandidateStatus.InProgress);
    }

    [Fact]
    public async Task Nomavjud_bolim_bilan_qabul_qilinmaydi()
    {
        // Bo'lim IXTIYORIY, lekin ko'rsatilgan bo'lsa — mavjud bo'lishi
        // shart. Ishga qabul oqimining o'zi buni tekshirmaydi.
        var (_, _, taklif) = Muhit();
        var bolimId = Guid.CreateVersion7();

        _context.Employees.Departments
            .ExistsAsync(bolimId, Arg.Any<CancellationToken>())
            .Returns(false);

        var natija = await Qabul.HandleAsync(
            Buyruq(taklif.Id, bolimId: bolimId), TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("taklif.bolim_topilmadi");
    }

    [Fact]
    public async Task Bolim_ham_MEHNAT_munosabatiga_kochadi()
    {
        // «Компаниянинг бўлимлари билан ҳам танланиб» — bo'lim ham aynan
        // shu formada tanlanadi va hujjatga tushadi.
        var (_, _, taklif) = Muhit();
        var bolimId = Guid.CreateVersion7();

        var natija = await Qabul.HandleAsync(
            Buyruq(taklif.Id, bolimId: bolimId), TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);
        natija.Value.Hire.Employment.DepartmentId.ShouldBe(bolimId);
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
