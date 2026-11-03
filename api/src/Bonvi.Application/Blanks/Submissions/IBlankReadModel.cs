using Bonvi.Domain.Blanks;

namespace Bonvi.Application.Blanks.Submissions;

/// <summary>
/// «Menga kutilmoqda» filtri: chaqiruvchi ELEKTRON bajara oladigan ochiq qadamlar (§6.2).
/// </summary>
/// <param name="EmployeeId">Chaqiruvchining <c>users.employee_id</c> si (shaxs rollari uchun).</param>
/// <param name="OfficeRoles">Chaqiruvchi bajara oladigan ofis rollari (ruxsat va biriktirish hisobga olingan).</param>
public sealed record BlankWaitingFor(Guid? EmployeeId, IReadOnlyCollection<string> OfficeRoles);

/// <summary>Ro'yxat filtri (§7 #6) — Application tekshirib, tayyor qiymatlar bilan beradi.</summary>
public sealed record BlankSubmissionFilter(
    string? TemplateCode,
    BlankSubmissionStatus? Status,
    Guid? EmployeeId,
    Guid? CandidateId,
    IReadOnlyCollection<Guid>? DepartmentIds,
    DateTimeOffset? CreatedFrom,
    DateTimeOffset? CreatedBefore,
    string? Search,
    BlankWaitingFor? WaitingFor,
    bool Incomplete,
    DateOnly? OverdueBefore,
    int Page,
    int PageSize)
{
    public int Skip => (Page - 1) * PageSize;
}

/// <summary>Ro'yxat sahifasi.</summary>
public sealed record BlankSubmissionPage(IReadOnlyList<BlankSubmissionListItemDto> Items, int Total);

/// <summary>Xodim sub'yekt nusxasi: faol (bo'lmasa oxirgi) ish joyi va uning bo'limi.</summary>
public sealed record BlankEmployeeSubject(Guid EmployeeId, Guid? EmploymentId, Guid? DepartmentId);

/// <summary>Blanka o'qish modeli (Infrastructure, EF, AsNoTracking).</summary>
public interface IBlankReadModel
{
    Task<IReadOnlyList<BlankTemplateDto>> ListTemplatesAsync(CancellationToken cancellationToken = default);

    Task<BlankTemplateDto?> GetTemplateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<BlankTemplateVersionDto?> GetVersionAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Versiya ta'rifi (xom JSON) — keshlash uchun.</summary>
    Task<string?> GetDefinitionJsonAsync(Guid versionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BlankRoleDto>> ListRolesAsync(CancellationToken cancellationToken = default);

    Task<BlankSubmissionPage> SearchAsync(BlankSubmissionFilter filter, CancellationToken cancellationToken = default);

    /// <summary>To'liq ko'rinish; <see cref="BlankSubmissionDetailDto.Can"/> — <see cref="BlankCanDto.Nothing"/> (uni Application to'ldiradi).</summary>
    Task<BlankSubmissionDetailDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Guid?> FindIdByClientRequestAsync(Guid clientRequestId, CancellationToken cancellationToken = default);

    /// <summary>Xodim (o'chirilmagan) bo'lsa — uning ish joyi nusxasi, aks holda <c>null</c>.</summary>
    Task<BlankEmployeeSubject?> FindEmployeeSubjectAsync(Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>Nomzod bo'lsa — vakansiyasining bo'limi (<c>Guid.Empty</c> emas), aks holda <c>null</c>.</summary>
    Task<(bool Found, Guid? DepartmentId)> FindCandidateSubjectAsync(Guid candidateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Javoblardagi havolalardan (ref, docRef) birinchi YO'Q bo'lganining yo'li; hammasi bor bo'lsa <c>null</c>.
    /// O'chirilgan yozuv ham «bor» (tarix uchun); docRef — bekor qilinmagan, boshqa blanka.
    /// </summary>
    Task<string?> FirstMissingReferenceAsync(
        IReadOnlyList<Bonvi.Application.Blanks.Definitions.BlankAnswerReference> references,
        Guid submissionId,
        CancellationToken cancellationToken = default);

    Task<Guid?> GetUserEmployeeIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Berilganlardan bazada YO'Q (yoki o'chirilgan) foydalanuvchilar.</summary>
    Task<IReadOnlyList<Guid>> MissingUserIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);
}
