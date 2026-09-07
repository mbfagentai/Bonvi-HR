using Bonvi.Application.Recruiting.Candidates;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Recruiting;
using NSubstitute;

namespace Bonvi.Application.Tests.Recruiting;

/// <summary>
/// Nomzoddan xodimga o'tish — to'g'ridan-to'g'ri, oraliq qadamsiz.
/// </summary>
/// <remarks>
/// <para>
/// Bu — 1-bosqich bilan 2-bosqichni bog'laydigan YAGONA nuqta (ish
/// taklifi orqali boradigan eski yo'l o'chirildi), shu sababli qoplama
/// shunga yarasha zich.
/// </para>
/// <para>
/// Testlar ishga qabulning MOCK qilingan nusxasini emas, HAQIQIY
/// <c>HireEmployeeCommandHandler</c> ni ishlatadi: aks holda «sinov muddati
/// ochiladimi» va «qora ro'yxat to'sadimi» degan asosiy savollar
/// tekshirilmasdan qolardi — ya'ni aynan shu use-case yozilishining sababi
/// tekshirilmasdi.
/// </para>
/// </remarks>
public sealed class HireCandidateHandlerTests
{
    /// <summary>
    /// Testlardagi firma.
    /// </summary>
    /// <remarks>
    /// Alohida mock kerak emas: <c>EmployeeTestContext</c> firmalar
    /// ma'lumotnomasi uchun «har qanday identifikator mavjud» deb javob
    /// beradi. Muhimi shu identifikator BUYRUQDAN mehnat munosabatiga
    /// o'zgarmasdan yetib borishi.
    /// </remarks>
    private static readonly Guid FirmaId = Guid.CreateVersion7();

    private readonly RecruitingTestContext _context = new();

    private HireCandidateCommandHandler IshgaOlish => new(
        _context.Candidates,
        _context.Employees.Employments,
        _context.Employees.Departments,
        _context.Hire,
        _context.Mapper,
        _context.Clock);

    /// <summary>Firma va lavozimsiz bu amal umuman bajarilmaydi.</summary>
    /// <remarks>
    /// Ikkalasi MAJBURIY, ya'ni har bir test ularni ataylab berishi kerak.
    /// Bu yordamchi o'sha takrorni bir joyga yig'adi va «bu qiymatlar
    /// qayerdan keladi» degan savolni ochiq qoldirmaydi.
    /// </remarks>
    private HireCandidateCommand Buyruq(
        Guid nomzodId,
        Guid? lavozimId = null,
        Guid? firmaId = null,
        Guid? bolimId = null,
        DateOnly? qabulSanasi = null,
        string? buyruqRaqami = null,
        decimal? oklad = null,
        int? sinovKunlari = null,
        Guid? xodimId = null)
        => new(
            nomzodId,
            firmaId ?? FirmaId,
            lavozimId ?? _context.Employees.ExistingPosition().Id,
            bolimId,
            qabulSanasi,
            buyruqRaqami,
            OrderDate: null,
            Pinfl: null,
            Passport: null,
            xodimId,
            oklad,
            SalaryCurrency: null,
            Rate: null,
            IsPrimary: null,
            ResponsibleEmployeeId: null,
            sinovKunlari);

    [Fact]
    public async Task Nomavjud_nomzod_ishga_olinmaydi()
    {
        var natija = await IshgaOlish.HandleAsync(
            Buyruq(Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.topilmadi");
    }

    [Fact]
    public async Task Yuridik_shaxssiz_ishga_olinmaydi()
    {
        var nomzod = Nomzod();

        var natija = await IshgaOlish.HandleAsync(
            Buyruq(nomzod.Id, firmaId: Guid.Empty), TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.yuridik_shaxs_yoq");
        nomzod.Status.ShouldBe(
            CandidateStatus.InProgress,
            "tekshiruv eng boshida — nomzodga umuman tegilmaydi");
    }

    [Fact]
    public async Task Lavozimsiz_ishga_olinmaydi()
    {
        // ⭐ Rekruting qatlamining O'Z qoidasi: foydalanuvchi «аниқ лавозим
        // танланади» dedi. `HireEmployeeCommand.PositionId` ning o'zi
        // ixtiyoriyligicha qoladi (HR formasi va ETL unga tayanadi).
        var nomzod = Nomzod();

        var natija = await IshgaOlish.HandleAsync(
            Buyruq(nomzod.Id, lavozimId: Guid.Empty), TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.lavozim_yoq");
        nomzod.Status.ShouldBe(CandidateStatus.InProgress);
        await _context.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Nomavjud_bolim_bilan_ishga_olinmaydi()
    {
        // Bo'lim IXTIYORIY, lekin ko'rsatilgan bo'lsa — mavjud bo'lishi
        // shart. Ishga qabul oqimining o'zi buni tekshirmaydi.
        var nomzod = Nomzod();
        var bolimId = Guid.CreateVersion7();

        _context.Employees.Departments
            .ExistsAsync(bolimId, Arg.Any<CancellationToken>())
            .Returns(false);

        var natija = await IshgaOlish.HandleAsync(
            Buyruq(nomzod.Id, bolimId: bolimId), TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.bolim_topilmadi");
    }

    [Fact]
    public async Task Xodim_yaratiladi_va_sinov_muddati_ochiladi()
    {
        var nomzod = Nomzod();
        var lavozim = _context.Employees.ExistingPosition();
        var bolimId = Guid.CreateVersion7();
        var qabulSanasi = RecruitingTestContext.Today.AddDays(5);

        var natija = await IshgaOlish.HandleAsync(
            Buyruq(
                nomzod.Id,
                lavozimId: lavozim.Id,
                bolimId: bolimId,
                qabulSanasi: qabulSanasi,
                buyruqRaqami: "185-b"),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);

        // 1. Nomzod voronkadan chiqdi
        natija.Value.Candidate.Status.ShouldBe(CandidateStatus.Hired);
        nomzod.IsActive.ShouldBeFalse();

        // 2. Xodim va mehnat munosabati yaratildi — firma, bo'lim va lavozim
        //    BUYRUQDAN keladi.
        var mehnat = natija.Value.Hire.Employment;
        mehnat.LegalEntityId.ShouldBe(FirmaId);
        mehnat.PositionId.ShouldBe(lavozim.Id);
        mehnat.DepartmentId.ShouldBe(bolimId);
        mehnat.HireDate.ShouldBe(qabulSanasi);
        mehnat.HireOrderNo.ShouldBe("185-b");

        // 3. Sinov muddati MAJBURIY va u avtomatik ochiladi — bu butun
        //    use-case mavjud ishga qabul oqimini chaqirishining asosiy sababi.
        mehnat.Status.ShouldBe(EmploymentStatus.Probation);
        mehnat.Probation.ShouldNotBeNull();
        mehnat.Probation.EndDate.ShouldBe(
            qabulSanasi.AddDays(90),
            "lavozimda muddat ko'rsatilmagan — umumiy qoida 90 kun");

        _context.Employees.Employees.Received(1).Add(Arg.Any<Employee>());
        _context.Employees.Probations.Received(1).Add(Arg.Any<Probation>());
    }

    [Fact]
    public async Task Kelishilgan_oklad_MEHNAT_munosabatiga_yoziladi()
    {
        // Taklif yo'q, ya'ni oklad kelishuvining yagona manbai — shu forma.
        //
        // ⚠️ Tekshiruv AGREGAT ustidan, DTO ustidan emas: javobdagi oklad
        // `salary.view` ruxsatiga qarab yashiriladi (`SalaryAccess.Mask`),
        // testdagi foydalanuvchida esa hech qanday ruxsat yo'q.
        var nomzod = Nomzod();

        Employment? mehnat = null;
        _context.Employees.Employments
            .When(repo => repo.Add(Arg.Any<Employment>()))
            .Do(call => mehnat = call.Arg<Employment>());

        var natija = await IshgaOlish.HandleAsync(
            Buyruq(nomzod.Id, oklad: 6_000_000m), TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);

        mehnat.ShouldNotBeNull();
        mehnat.Salary.ShouldNotBeNull();
        mehnat.Salary.Amount.ShouldBe(6_000_000m);
    }

    [Fact]
    public async Task Okladsiz_ham_ishga_olinadi()
    {
        // Oklad IXTIYORIY — `HireEmployeeCommand` dagi bilan bir xil qoida
        // (HR formasi ham okladsiz qabulga ruxsat beradi).
        var nomzod = Nomzod();

        Employment? mehnat = null;
        _context.Employees.Employments
            .When(repo => repo.Add(Arg.Any<Employment>()))
            .Do(call => mehnat = call.Arg<Employment>());

        var natija = await IshgaOlish.HandleAsync(
            Buyruq(nomzod.Id), TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);
        mehnat.ShouldNotBeNull();
        mehnat.Salary.ShouldBeNull("oklad yozuvi ochilmaydi, lekin qabul o'tadi");
    }

    [Fact]
    public async Task Qabul_sanasi_korsatilmasa_BUGUN_olinadi()
    {
        var nomzod = Nomzod();

        var natija = await IshgaOlish.HandleAsync(
            Buyruq(nomzod.Id), TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);
        natija.Value.Hire.Employment.HireDate.ShouldBe(RecruitingTestContext.Today);
    }

    [Fact]
    public async Task Nomzod_BUGUN_voronkadan_chiqadi_qabul_sanasi_kelajakda_bolsa_ham()
    {
        // Qaror ayni hozir qabul qilinmoqda, ish boshlash esa kelajakda
        // («dushanbadan chiqadi»). Ikkalasi birlashtirilsa, «shu oyda
        // voronkadan nechta odam chiqdi» degan hisobot uni ko'rmasdi.
        var nomzod = Nomzod();
        var kelajak = RecruitingTestContext.Today.AddDays(20);

        var natija = await IshgaOlish.HandleAsync(
            Buyruq(nomzod.Id, qabulSanasi: kelajak), TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);
        natija.Value.Candidate.ClosedAt.ShouldBe(RecruitingTestContext.Today);
        natija.Value.Hire.Employment.HireDate.ShouldBe(kelajak);
    }

    [Fact]
    public async Task Nomzodning_malumoti_xodim_kartasiga_kochadi()
    {
        var nomzod = Nomzod();

        Employee? yaratilgan = null;
        _context.Employees.Employees
            .When(repo => repo.Add(Arg.Any<Employee>()))
            .Do(call => yaratilgan = call.Arg<Employee>());

        var natija = await IshgaOlish.HandleAsync(
            Buyruq(nomzod.Id), TestContext.Current.CancellationToken);

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
        var nomzod = Nomzod();

        Employment? mehnat = null;
        _context.Employees.Employments
            .When(repo => repo.Add(Arg.Any<Employment>()))
            .Do(call => mehnat = call.Arg<Employment>());

        var natija = await IshgaOlish.HandleAsync(
            Buyruq(nomzod.Id), TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);

        mehnat.ShouldNotBeNull();
        var qabul = mehnat.Events.Single(hodisa => hodisa.Type == EmploymentEventType.Hire);
        qabul.CandidateId.ShouldBe(nomzod.Id);
    }

    [Fact]
    public async Task Sinovsiz_qabul_YOQ()
    {
        // 1-bosqichning qoidasi bu yo'l orqali ham chetlab o'tilmasligi
        // kerak: «hamma sinovdan o'tadi, istisnosiz».
        var nomzod = Nomzod();

        var natija = await IshgaOlish.HandleAsync(
            Buyruq(nomzod.Id, sinovKunlari: 0), TestContext.Current.CancellationToken);

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
        // Nomzod AVVAL o'zgartiriladi, ishga qabul OXIRIDA chaqiriladi va
        // uning yagona `SaveChangesAsync` chaqiruvi ikkalasini birga yozadi.
        // Ikkinchi saqlash bo'lsa, «xodim yaratildi, nomzod voronkada
        // qoldi» holati mumkin bo'lardi.
        var nomzod = Nomzod();

        var natija = await IshgaOlish.HandleAsync(
            Buyruq(nomzod.Id), TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);
        await _context.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Qora_royxatdagi_odam_uchun_qabul_toxtaydi()
    {
        // Ishga qabulning MAVJUD qoidasi bu yo'l orqali ham ishlaydi:
        // mavjud xodim kartasi ko'rsatilgan va u qora ro'yxatda bo'lsa,
        // qabul bloklanadi.
        var nomzod = Nomzod();
        var xodim = _context.Employees.ExistingEmployee();
        _context.Employees.GiveFlag(xodim.Id, EmployeeFlagType.Blacklist);

        var natija = await IshgaOlish.HandleAsync(
            Buyruq(nomzod.Id, xodimId: xodim.Id), TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("xodim.qora_royxatda");
        nomzod.Status.ShouldBe(CandidateStatus.Hired, "xotirada o'zgardi, lekin saqlanmadi");
        await _context.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Allaqachon_ishga_olingan_nomzod_ikkinchi_marta_olinmaydi()
    {
        var nomzod = Nomzod();
        nomzod.MarkHired(RecruitingTestContext.Today);

        var natija = await IshgaOlish.HandleAsync(
            Buyruq(nomzod.Id), TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.allaqachon_ishga_olingan");
    }

    [Fact]
    public async Task Ishga_qabul_HODISASI_topilsa_ham_ikkinchi_karta_yaratilmaydi()
    {
        // Ikkinchi to'siq: nomzodning holati qandaydir sabab bilan
        // yozilmay qolgan bo'lsa ham, ma'lumotning O'ZI («bu nomzoddan
        // ishga qabul hodisasi bor») takroriy qabulni to'sadi. Shu to'siq
        // tufayli taklif orqali olingan odam bu yo'l bilan ikkinchi marta
        // olinmaydi.
        var nomzod = Nomzod();

        var mavjud = Employment
            .Hire(Guid.CreateVersion7(), Guid.CreateVersion7(), RecruitingTestContext.Today, candidateId: nomzod.Id)
            .Value;

        _context.Employees.Employments
            .FindByCandidateAsync(nomzod.Id, Arg.Any<CancellationToken>())
            .Returns(mavjud);

        var natija = await IshgaOlish.HandleAsync(
            Buyruq(nomzod.Id), TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.allaqachon_ishga_olingan");
        _context.Employees.Employees.DidNotReceive().Add(Arg.Any<Employee>());
    }

    [Fact]
    public async Task Rezervdagi_nomzod_avval_voronkaga_qaytarilishi_kerak()
    {
        // Rezerv — YAKUN, ya'ni nomzod voronkadan chiqqan. Uni to'g'ridan
        // ishga olish yo'li ochilsa, «rezervdan qaytarish» amali ma'nosini
        // yo'qotardi va nomzod voronkaga tushmasdan xodim bo'lardi.
        var nomzod = Nomzod();
        nomzod.MoveToReserve(RecruitingTestContext.Today, "Ҳозир ўрин йўқ");

        var natija = await IshgaOlish.HandleAsync(
            Buyruq(nomzod.Id), TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.jarayonda_emas");
        _context.Employees.Employees.DidNotReceive().Add(Arg.Any<Employee>());
    }

    [Fact]
    public async Task Rezervdan_qaytgan_nomzod_ishga_olinadi()
    {
        // Rezervning butun ma'nosi shu: o'rin bo'shaganda nomzod
        // voronkaga qaytadi va odatiy yo'l bilan xodimga aylanadi.
        var nomzod = Nomzod();
        nomzod.MoveToReserve(RecruitingTestContext.Today, "Ҳозир ўрин йўқ");
        nomzod.ReturnFromReserve();

        var natija = await IshgaOlish.HandleAsync(
            Buyruq(nomzod.Id), TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);
        natija.Value.Candidate.Status.ShouldBe(CandidateStatus.Hired);
        _context.Employees.Employees.Received(1).Add(Arg.Any<Employee>());
    }

    /// <summary>Standart muhit: ochiq vakansiya va voronkadagi nomzod.</summary>
    /// <remarks>
    /// Taklif ATAYLAB yaratilmaydi — bu yo'lning butun mohiyati taklifsiz
    /// ishlashida.
    /// </remarks>
    private Candidate Nomzod()
    {
        var vakansiya = _context.ExistingVacancy();
        var anketa = _context.ExistingStage();
        _context.Funnel(anketa);

        return _context.ExistingCandidate(vakansiya, anketa);
    }
}
