using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Application.Recruiting.Abstractions;

/// <summary>
/// Vakansiya so'rovlari — HR ro'yxati (docs/SPEC-vakansiya-sorovi.md).
/// </summary>
public interface IVacancyRequestReadModel
{
    /// <summary>HR ro'yxati — yangidan eskiga.</summary>
    Task<VacancyRequestPage> SearchAsync(
        VacancyRequestFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>Bitta so'rov — bo'lsa.</summary>
    Task<VacancyRequestDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>HR ro'yxatining filtri.</summary>
/// <param name="Status">Holat; <c>null</c> — hammasi.</param>
/// <param name="Search">Lavozim, bo'lim yoki so'rovchi bo'yicha.</param>
/// <param name="CreatedFrom">Yuborilgan sana — boshi (UTC, kiradi).</param>
/// <param name="CreatedBefore">Yuborilgan sana — oxiri (UTC, kirmaydi).</param>
/// <param name="Page">Sahifa (1 dan).</param>
/// <param name="PageSize">Sahifa hajmi.</param>
public sealed record VacancyRequestFilter(
    VacancyRequestStatus? Status,
    string? Search,
    DateTimeOffset? CreatedFrom,
    DateTimeOffset? CreatedBefore,
    int Page,
    int PageSize)
{
    /// <summary>O'tkazib yuboriladigan qatorlar.</summary>
    public int Skip => (Page - 1) * PageSize;
}

/// <summary>HR ro'yxatining sahifasi.</summary>
/// <param name="Items">Qatorlar.</param>
/// <param name="Total">Filtrga mos jami.</param>
public sealed record VacancyRequestPage(IReadOnlyList<VacancyRequestDto> Items, int Total);
