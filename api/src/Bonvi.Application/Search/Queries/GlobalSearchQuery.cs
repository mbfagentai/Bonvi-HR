using Bonvi.Application.Abstractions;
using Bonvi.Application.Employees.Abstractions;
using Bonvi.Application.Recruiting.Abstractions;
using Bonvi.Application.Search.Dtos;
using Bonvi.Domain.Common;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.Search.Queries;

/// <summary>
/// Global qidiruv — yuqori paneldagi maydon: bitta matn bilan barcha
/// bo'limlardan (xodimlar, nomzodlar, vakansiyalar) qidirish.
/// </summary>
/// <param name="Q">Qidiruv matni: F.I.Sh., JSHSHIR, pasport, telefon yoki vakansiya nomi.</param>
public sealed record GlobalSearchQuery(string? Q);

/// <summary>
/// <see cref="GlobalSearchQuery"/> ni bajaradi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>O'zining qidiruv qoidasi YO'Q.</b> Har bir guruh o'sha bo'limning
/// ro'yxati ishlatadigan o'qish modelidan o'tadi (<see cref="IEmployeeReadModel"/>,
/// <see cref="IRecruitingReadModel"/>), ya'ni «Каримов» ni lotinda yozish,
/// JSHSHIR, telefon ajratgichlari — hammasi bo'limlardagi bilan AYNAN bir
/// xil. Aks holda HR odamni global qidiruvda topib, bo'limda topolmasdi
/// (yoki teskarisi) — loyihaning eng qimmat xatosi (topilmagan odam →
/// dublikat karta) shundan boshlanadi.
/// </para>
/// <para>
/// Bo'limlardan farqi bitta: HECH KIM yashirilmaydi. Ro'yxatlar sukut
/// bo'yicha ishdan ketganlarni, ishga olingan, rad etilgan, rezerv va
/// qora ro'yxatdagi nomzodlarni hamda Excel arxivini chiqarib tashlaydi —
/// ularning har biri o'z bo'limida. Global qidiruv esa «bu odam bizda
/// QAYERDA?» degan savolga javob beradi.
/// </para>
/// <para>
/// <b>One exception: one person comes up in one place</b> (owner's request, 2026-11-05). A
/// candidate of any status who already is an employee is left out of the candidate group,
/// because the employee group shows that person. It applies only to a caller who can see
/// employees: for anyone else the employee group is <c>null</c>, and hiding the candidate would
/// leave them no row at all.
/// </para>
/// <para>
/// <b>Ruxsat guruh bo'yicha.</b> Endpoint <c>employees.view</c> YOKI
/// <c>recruiting.view</c> bilan ochiladi, javob esa torayadi: ruxsati yo'q
/// guruh <c>null</c> bo'ladi va uning so'rovi bazaga UMUMAN yuborilmaydi.
/// </para>
/// </remarks>
public sealed class GlobalSearchQueryHandler
{
    /// <summary>Qisqaroq matn qidirilmaydi.</summary>
    /// <remarks>
    /// Bitta harf yoki raqam («a», «9») minglab yozuvga mos tushadi —
    /// natija ma'nosiz, baza esa har bosishda butun jadvalni ko'radi.
    /// </remarks>
    public const int MinLength = 2;

    /// <summary>Juda uzun matn kesiladi — qidiruvda 100 belgidan ortig'i ma'nosiz.</summary>
    public const int MaxLength = 100;

    /// <summary>Har guruhda ko'rsatiladigan yozuvlar soni.</summary>
    public const int EmployeeLimit = 6;

    /// <inheritdoc cref="EmployeeLimit"/>
    public const int CandidateLimit = 6;

    /// <inheritdoc cref="EmployeeLimit"/>
    public const int VacancyLimit = 3;

    private readonly IEmployeeReadModel _employees;
    private readonly IRecruitingReadModel _recruiting;
    private readonly ICurrentUser _currentUser;

    public GlobalSearchQueryHandler(
        IEmployeeReadModel employees,
        IRecruitingReadModel recruiting,
        ICurrentUser currentUser)
    {
        _employees = employees;
        _recruiting = recruiting;
        _currentUser = currentUser;
    }

    public async Task<Result<GlobalSearchDto>> HandleAsync(
        GlobalSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var text = (query.Q ?? string.Empty).Trim();

        if (text.Length > MaxLength)
        {
            text = text[..MaxLength];
        }

        var canSeeEmployees = _currentUser.HasPermission(Permissions.EmployeesView);
        var canSeeRecruiting = _currentUser.HasPermission(Permissions.RecruitingView);

        // Qisqa matnda bazaga borilmaydi, lekin guruhlar ruxsatga qarab
        // baribir to'ldiriladi: ekran «qidirildi, topilmadi» bilan «ruxsat
        // yo'q» ni shu farqdan biladi.
        if (text.Length < MinLength)
        {
            return Result.Success(new GlobalSearchDto(
                text,
                canSeeEmployees ? new GlobalSearchEmployeesDto(0, []) : null,
                canSeeRecruiting ? new GlobalSearchCandidatesDto(0, []) : null,
                canSeeRecruiting ? new GlobalSearchVacanciesDto(0, []) : null));
        }

        // So'rovlar KETMA-KET: bitta DbContext parallel so'rovni ko'tarmaydi.
        var employees = canSeeEmployees
            ? await SearchEmployeesAsync(text, cancellationToken).ConfigureAwait(false)
            : null;

        var candidates = canSeeRecruiting
            ? await SearchCandidatesAsync(text, canSeeEmployees, cancellationToken).ConfigureAwait(false)
            : null;

        var vacancies = canSeeRecruiting
            ? await SearchVacanciesAsync(text, cancellationToken).ConfigureAwait(false)
            : null;

        return Result.Success(new GlobalSearchDto(text, employees, candidates, vacancies));
    }

    private async Task<GlobalSearchEmployeesDto> SearchEmployeesAsync(
        string text,
        CancellationToken cancellationToken)
    {
        // Status = null va HideTerminated = false — barcha holatlar,
        // munosabati yo'qlar ham. Tartib: ishlayotganlar birinchi.
        var page = await _employees
            .SearchEmployeesAsync(
                new EmployeeSearchFilter(
                    Search: text,
                    PageSize: EmployeeLimit,
                    Sort: EmployeeSortField.EmployedFirst),
                cancellationToken)
            .ConfigureAwait(false);

        return new GlobalSearchEmployeesDto(
            page.Total,
            [
                .. page.Items.Select(row => new GlobalSearchEmployeeDto(
                    row.Id,
                    FullName.FromTrusted(row.LastName, row.FirstName, row.MiddleName).Full,
                    row.PhotoKey,
                    row.Status,
                    row.PositionName,
                    row.DepartmentName,
                    row.ActiveFlags)),
            ]);
    }

    private async Task<GlobalSearchCandidatesDto> SearchCandidatesAsync(
        string text,
        bool canSeeEmployees,
        CancellationToken cancellationToken)
    {
        var page = await _recruiting
            .SearchCandidatesAsync(
                new CandidateSearchFilter(
                    Search: text,
                    PageSize: CandidateLimit,
                    IncludeArchive: true,
                    AllStatuses: true,
                    HideIfEmployee: canSeeEmployees),
                cancellationToken)
            .ConfigureAwait(false);

        return new GlobalSearchCandidatesDto(
            page.Total,
            [
                .. page.Items.Select(row => new GlobalSearchCandidateDto(
                    row.Id,
                    FullName.FromTrusted(row.LastName, row.FirstName, row.MiddleName).Full,
                    row.HasPhoto,
                    row.Status,
                    row.VacancyName,
                    row.CurrentStageName,
                    row.CreatedAt,
                    row.HiredEmployeeId)),
            ]);
    }

    private async Task<GlobalSearchVacanciesDto> SearchVacanciesAsync(
        string text,
        CancellationToken cancellationToken)
    {
        // Status = null — ochiq ham, yopiq ham.
        var page = await _recruiting
            .ListVacanciesAsync(
                new VacancyListFilter(Search: text, PageSize: VacancyLimit),
                cancellationToken)
            .ConfigureAwait(false);

        return new GlobalSearchVacanciesDto(
            page.Total,
            [
                .. page.Items.Select(row => new GlobalSearchVacancyDto(
                    row.Id,
                    row.Title,
                    row.Status,
                    row.Openings,
                    row.CandidateCount)),
            ]);
    }
}
