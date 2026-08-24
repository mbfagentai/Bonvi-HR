namespace Bonvi.Application.Catalog.Dtos;

/// <summary>
/// Yuridik shaxs ma'lumotnomasi yozuvi.
/// </summary>
/// <param name="Id">Identifikator.</param>
/// <param name="Name">Nom.</param>
/// <param name="Code">Qisqa kod (integratsiya uchun).</param>
/// <param name="SortOrder">Ro'yxatdagi tartib.</param>
/// <param name="IsActive">Faolmi — faol emaslari yangi yozuvlarda tanlanmaydi.</param>
/// <param name="Note">Izoh (masalan nima uchun faol emas).</param>
/// <param name="TaxId">STIR — 9 xona.</param>
/// <param name="FullName">To'liq rasmiy nom (shartnoma va buyruqlar uchun).</param>
public sealed record LegalEntityDto(
    Guid Id,
    string Name,
    string? Code,
    int SortOrder,
    bool IsActive,
    string? Note,
    string? TaxId,
    string? FullName);

/// <summary>Yo'nalish ma'lumotnomasi yozuvi.</summary>
/// <param name="Id">Identifikator.</param>
/// <param name="Name">Nom.</param>
/// <param name="Code">Qisqa kod.</param>
/// <param name="SortOrder">Ro'yxatdagi tartib.</param>
/// <param name="IsActive">Faolmi.</param>
/// <param name="Note">Izoh.</param>
/// <param name="Color">Interfeysda ajratib ko'rsatish uchun rang: <c>#RRGGBB</c>.</param>
public sealed record DirectionDto(
    Guid Id,
    string Name,
    string? Code,
    int SortOrder,
    bool IsActive,
    string? Note,
    string? Color);

/// <summary>Ishdan ketish sababi.</summary>
/// <param name="Id">Identifikator.</param>
/// <param name="Name">Nom.</param>
/// <param name="Code">Qisqa kod.</param>
/// <param name="SortOrder">Ro'yxatdagi tartib.</param>
/// <param name="IsActive">Faolmi.</param>
/// <param name="Note">Izoh.</param>
/// <param name="IsVoluntary">Xodimning tashabbusi bilanmi.</param>
/// <param name="IsRehireable">Shu sabab bilan ketganni qayta ishga olish mumkinmi.</param>
public sealed record TerminationReasonDto(
    Guid Id,
    string Name,
    string? Code,
    int SortOrder,
    bool IsActive,
    string? Note,
    bool IsVoluntary,
    bool IsRehireable);

/// <summary>Rag'bat turi.</summary>
/// <param name="Id">Identifikator.</param>
/// <param name="Name">Nom.</param>
/// <param name="Code">Qisqa kod.</param>
/// <param name="SortOrder">Ro'yxatdagi tartib.</param>
/// <param name="IsActive">Faolmi.</param>
/// <param name="Note">Izoh.</param>
/// <param name="HasAmount">Pul bilan bog'liqmi — xarajat hisoboti shundan chiqadi.</param>
public sealed record AwardTypeDto(
    Guid Id,
    string Name,
    string? Code,
    int SortOrder,
    bool IsActive,
    string? Note,
    bool HasAmount);

/// <summary>Jarima turi.</summary>
/// <param name="Id">Identifikator.</param>
/// <param name="Name">Nom.</param>
/// <param name="Code">Qisqa kod.</param>
/// <param name="SortOrder">Ro'yxatdagi tartib.</param>
/// <param name="IsActive">Faolmi.</param>
/// <param name="Note">Izoh.</param>
/// <param name="HasAmount">Pul jarimasimi.</param>
/// <param name="Severity">Og'irlik darajasi: 1 — ogohlantirish, 3 — oxirgi ogohlantirish.</param>
public sealed record PenaltyTypeDto(
    Guid Id,
    string Name,
    string? Code,
    int SortOrder,
    bool IsActive,
    string? Note,
    bool HasAmount,
    int Severity);

/// <summary>Hujjat turi.</summary>
/// <param name="Id">Identifikator.</param>
/// <param name="Name">Nom.</param>
/// <param name="Code">Qisqa kod.</param>
/// <param name="SortOrder">Ro'yxatdagi tartib.</param>
/// <param name="IsActive">Faolmi.</param>
/// <param name="Note">Izoh.</param>
/// <param name="IsRequired">Har bir xodimda bo'lishi shartmi.</param>
public sealed record DocumentTypeDto(
    Guid Id,
    string Name,
    string? Code,
    int SortOrder,
    bool IsActive,
    string? Note,
    bool IsRequired);
