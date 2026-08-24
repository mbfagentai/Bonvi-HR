namespace Bonvi.Application.Catalog.Dtos;

/// <summary>Lavozim vazifasi — bitta band.</summary>
/// <param name="Id">Identifikator.</param>
/// <param name="Text">Vazifa matni.</param>
/// <param name="SortOrder">Ro'yxatdagi tartib.</param>
public sealed record PositionDutyDto(Guid Id, string Text, int SortOrder);

/// <summary>
/// Lavozim — ro'yxat ko'rinishi (vazifalarsiz).
/// </summary>
/// <remarks>
/// Vazifalar ataylab qo'shilmagan: ro'yxatda 150 ta lavozim bo'ladi va
/// har birining vazifalarini yuborish javobni bir necha barobar kattalashtiradi.
/// Vazifalar faqat bitta lavozim tanlanganda yuklanadi.
/// </remarks>
/// <param name="Id">Identifikator.</param>
/// <param name="Name">Nom.</param>
/// <param name="Code">Qisqa kod.</param>
/// <param name="SortOrder">Ro'yxatdagi tartib.</param>
/// <param name="IsActive">Faolmi.</param>
/// <param name="Note">Izoh.</param>
/// <param name="DepartmentId">Lavozim qaysi bo'limga tegishli.</param>
/// <param name="Grade">Daraja: 1 — ishchi … 5 — rahbariyat.</param>
/// <param name="ProbationDays">Standart sinov muddati (kun).</param>
public sealed record PositionDto(
    Guid Id,
    string Name,
    string? Code,
    int SortOrder,
    bool IsActive,
    string? Note,
    Guid? DepartmentId,
    int Grade,
    int? ProbationDays);

/// <summary>
/// Lavozim — to'liq ko'rinish, vazifalari bilan.
/// </summary>
/// <param name="Id">Identifikator.</param>
/// <param name="Name">Nom.</param>
/// <param name="Code">Qisqa kod.</param>
/// <param name="SortOrder">Ro'yxatdagi tartib.</param>
/// <param name="IsActive">Faolmi.</param>
/// <param name="Note">Izoh.</param>
/// <param name="DepartmentId">Lavozim qaysi bo'limga tegishli.</param>
/// <param name="Grade">Daraja.</param>
/// <param name="ProbationDays">Standart sinov muddati (kun).</param>
/// <param name="Duties">Lavozim vazifalari — tartiblangan.</param>
public sealed record PositionDetailsDto(
    Guid Id,
    string Name,
    string? Code,
    int SortOrder,
    bool IsActive,
    string? Note,
    Guid? DepartmentId,
    int Grade,
    int? ProbationDays,
    IReadOnlyList<PositionDutyDto> Duties);
