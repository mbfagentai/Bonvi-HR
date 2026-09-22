using Bonvi.Application.Abstractions;
using Bonvi.Application.Common.Documents;
using Bonvi.Application.Common.Photos;
using Bonvi.Application.Employees.Employments;
using Bonvi.Application.Recruiting.Candidates;
using Bonvi.Application.Recruiting;
using Bonvi.Application.Settings;
using Bonvi.Application.Tests.Employees;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.Settings;
using Bonvi.Domain.ValueObjects;
using NSubstitute;

namespace Bonvi.Application.Tests.Recruiting;

/// <summary>
/// Rekruting use-case'lari uchun umumiy mock to'plami.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="EmployeeTestContext"/> ni ICHIGA oladi, nusxa ko'chirmaydi:
/// nomzodni ishga olish 1-bosqichning HAQIQIY
/// <see cref="HireEmployeeCommandHandler"/> ini chaqiradi va u yerdagi
/// barcha bog'liqliklar (xodim, munosabat, sinov, lavozim, firma, belgilar)
/// allaqachon o'sha kontekstda yig'ilgan. Ikkinchi nusxa yozilsa, ishga
/// qabulning qoidalari testda BOSHQA sozlamalar bilan tekshirilardi.
/// </para>
/// <para>
/// Sukut bo'yicha holat «hammasi joyida»: vakansiya ochiq, bosqichlar faol,
/// nomzod jarayonda. Har bir test faqat O'ZIGA kerakli farqni o'rnatadi.
/// </para>
/// </remarks>
internal sealed class RecruitingTestContext
{
    /// <summary>Testlardagi «bugun» — xodimlar konteksti bilan bir xil sana.</summary>
    public static readonly DateOnly Today = EmployeeTestContext.Today;

    public RecruitingTestContext()
    {
        Candidates.ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);

        Candidates
            .CountByVacancyAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((0, 0));

        // Nomzoddan hech kim ishga olinmagan — takroriy qabul to'sig'i
        // faqat ataylab yoqilgan testlarda ishlaydi.
        Employees.Employments
            .FindByCandidateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Domain.Employees.Employment?)null);

        // Sukut bo'yicha BIRONTA sozlama yozilmagan — «Umumiy anketa» ham
        // sozlanmagan. Bu ataylab: xizmat vakansiyasiga tayanadigan test
        // uni ochiq talab qilsin, qolganlari esa undan bexabar qolsin.
        SettingRows
            .GetAllAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<SystemSetting>>([]);
    }

    /// <summary>1-bosqichning mock to'plami — ishga qabul oqimi shundan quriladi.</summary>
    public EmployeeTestContext Employees { get; } = new();

    public IPipelineStageRepository Stages { get; } = Substitute.For<IPipelineStageRepository>();

    public IVacancyRepository Vacancies { get; } = Substitute.For<IVacancyRepository>();

    public ICandidateRepository Candidates { get; } = Substitute.For<ICandidateRepository>();

    /// <summary>Sozlamalar jadvali — «Umumiy anketa» havolasi shu yerda yashaydi.</summary>
    public ISystemSettingRepository SettingRows { get; } = Substitute.For<ISystemSettingRepository>();

    /// <summary>
    /// Sozlamalarni o'qiydigan HAQIQIY sinf (mock emas).
    /// </summary>
    /// <remarks>
    /// <see cref="SystemSettingsReader"/> — <c>sealed</c>, ya'ni uni mock
    /// qilib bo'lmaydi; lekin bunga hojat ham yo'q: u faqat repozitoriy
    /// ustidagi yupqa qatlam va uning O'Z qoidasi (yozilmagan kalit —
    /// sukut qiymat) testda ham ishlashi kerak.
    /// </remarks>
    public SystemSettingsReader Settings => new(SettingRows, Substitute.For<ISecretProtector>());

    /// <summary>Vakansiya nomlari ma'lumotnomasi — vakansiyaning yagona havolasi.</summary>
    /// <remarks>
    /// Ilgari vakansiya lavozim, bo'lim va firmaga bog'langan edi va ularning
    /// mock'lari <see cref="EmployeeTestContext"/> dan olinardi. Endi
    /// vakansiyada kompaniya tuzilmasidan hech narsa yo'q (sababi
    /// <c>Vacancy</c> izohida), shu sababli bu yerda faqat SHU ma'lumotnoma
    /// kerak.
    /// </remarks>
    public IReferenceRepository<VacancyTitle> Titles { get; } =
        Substitute.For<IReferenceRepository<VacancyTitle>>();

    public IUnitOfWork UnitOfWork => Employees.UnitOfWork;

    /// <summary>Rasm qayta ishlovchisining o'rinbosari.</summary>
    /// <remarks>
    /// <see cref="EmployeeTestContext"/> da yo'q, chunki xodim hujjatlari
    /// hech qachon qayta kodlanmaydi (sababi <c>DocumentFileStore</c>
    /// izohida). Nomzod fayllarining quvuri esa rasmni kichraytiradi va
    /// shu portga tayanadi.
    /// </remarks>
    public IImageProcessor Images { get; } = Substitute.For<IImageProcessor>();

    /// <summary>
    /// Nomzod fayllarining HAQIQIY quvuri — faqat disk, tekshiruvchi va
    /// rasm ishlovchisi o'rinbosar.
    /// </summary>
    /// <remarks>
    /// <see cref="EmployeeTestContext.Files"/> bilan bir xil naqsh:
    /// <c>ScaledDocumentStore</c> — <c>sealed</c> sinf va uni mock qilib
    /// bo'lmaydi; hojat ham yo'q, uning o'z qoidasi (hajm chegarasi, tur
    /// tekshiruvi) testda ham ishlashi kerak.
    /// </remarks>
    public ScaledDocumentStore Files => new(Employees.Storage, Employees.Inspector, Images);

    public IClock Clock => Employees.Clock;

    /// <summary>
    /// Nomzoddan xodimga ko'chirish — HAQIQIY sinf, mock emas.
    /// </summary>
    /// <remarks>
    /// Ishga qabul handleri bilan bir xil sabab: ko'chirishning O'Z
    /// qoidalari bor (surat nusxalanadi, fayl ko'chadi, anketa qisman
    /// tushadi) va aynan ular testda ishlashi kerak.
    /// </remarks>
    public CandidateHandover Handover => new(
        Employees.DocumentTypes,
        Employees.Documents,
        Employees.Competencies,
        new SquarePhotoStore(Employees.Storage, Images),
        Employees.Clock);

    public RecruitingMapper Mapper => new(
        Stages, Vacancies, Candidates, Employees.Employments, Employees.Employees, Titles);

    /// <summary>1-bosqichning HAQIQIY ishga qabul handleri (mock emas).</summary>
    /// <remarks>
    /// Aynan shu narsa tekshirilishi kerak: nomzod ishga olinganda sinov
    /// muddati ochiladimi, qora ro'yxat to'sadimi. Mock qilingan handler
    /// bularning hech birini ko'rsatmasdi.
    /// </remarks>
    public HireEmployeeCommandHandler Hire => new(
        Employees.Employees,
        Employees.Employments,
        Employees.Probations,
        Employees.Positions,
        Employees.LegalEntities,
        Employees.Flags,
        Employees.FlagGuard,
        Employees.Mapper,
        Employees.Clock,
        Employees.UnitOfWork);

    /// <summary>Bazada mavjud faol bosqich.</summary>
    public PipelineStage ExistingStage(string name = "Анкета", int sortOrder = 0, bool active = true)
    {
        var stage = PipelineStage.Create(name, sortOrder).Value;
        if (!active)
        {
            stage.Deactivate();
        }

        Stages.GetByIdAsync(stage.Id, Arg.Any<CancellationToken>()).Returns(stage);
        Stages.ExistsAsync(stage.Id, Arg.Any<CancellationToken>()).Returns(true);

        return stage;
    }

    /// <summary>Voronkaning to'liq ro'yxatini o'rnatadi (tartibi bilan).</summary>
    public void Funnel(params PipelineStage[] stages)
    {
        Stages
            .ListAsync(false, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<PipelineStage>>([.. stages]);

        Stages
            .ListAsync(true, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<PipelineStage>>([.. stages.Where(stage => stage.IsActive)]);
    }

    /// <summary>Bazada mavjud vakansiya nomi (ma'lumotnoma yozuvi).</summary>
    public VacancyTitle ExistingTitle(string name = "Сотувчи-консультант")
    {
        var title = VacancyTitle.Create(name).Value;

        Titles.GetByIdAsync(title.Id, Arg.Any<CancellationToken>()).Returns(title);
        Titles.ExistsAsync(title.Id, Arg.Any<CancellationToken>()).Returns(true);

        return title;
    }

    /// <summary>Bazada mavjud vakansiya.</summary>
    /// <remarks>
    /// Ilgari bu yerda lavozim ATAYLAB haqiqiy lavozim edi: u vakansiyadan
    /// to'g'ridan-to'g'ri ishga qabul buyrug'iga tushardi. Endi bunday
    /// bog'lanish yo'q — lavozim qabul BUYRUG'IDA beriladi (sababi
    /// <c>HireCandidateCommand</c> izohida), vakansiyada esa faqat e'lon
    /// nomi qoldi.
    /// </remarks>
    public Vacancy ExistingVacancy(Guid? vacancyTitleId = null, bool open = true)
    {
        var vacancy = Vacancy.Open(vacancyTitleId ?? ExistingTitle().Id, Today.AddDays(-10)).Value;

        if (!open)
        {
            vacancy.Close(Today.AddDays(-1));
        }

        Vacancies.GetByIdAsync(vacancy.Id, Arg.Any<CancellationToken>()).Returns(vacancy);
        Vacancies.ExistsAsync(vacancy.Id, Arg.Any<CancellationToken>()).Returns(true);

        return vacancy;
    }

    /// <summary>
    /// «Umumiy anketa» xizmat vakansiyasini sozlab beradi.
    /// </summary>
    /// <remarks>
    /// Vakansiyaning O'ZI ham yaratiladi va sozlamaga uning
    /// identifikatori yoziladi — server aynan shu ikki qadamdan o'tadi
    /// (<c>DatabaseInitializer.SeedGeneralPoolVacancyAsync</c>).
    /// </remarks>
    public Vacancy GeneralPoolVacancy(bool open = true)
    {
        var vacancy = ExistingVacancy(open: open);

        var setting = SystemSetting
            .CreateServerValue(SystemSettingKeys.GeneralPoolVacancyId, vacancy.Id.ToString())
            .Value;

        SettingRows
            .GetAllAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<SystemSetting>>([setting]);

        return vacancy;
    }

    /// <summary>Bazada mavjud nomzod (bosqichlari bilan yuklanadi).</summary>
    public Candidate ExistingCandidate(Vacancy vacancy, PipelineStage? stage = null)
    {
        var candidate = Candidate.Register(
            FullName.Create("Каримов", "Азиз", null).Value,
            vacancy.Id,
            CandidateSource.Manual,
            phone: PhoneNumber.Create("905088888").Value,
            speciality: "Иқтисодчи").Value;

        if (stage is not null)
        {
            candidate.RecordStage(stage.Id, Today);
        }

        Candidates.GetByIdAsync(candidate.Id, Arg.Any<CancellationToken>()).Returns(candidate);
        Candidates.GetWithStagesAsync(candidate.Id, Arg.Any<CancellationToken>()).Returns(candidate);

        return candidate;
    }
}
