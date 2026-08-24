namespace Bonvi.Application.Catalog.Dtos;

/// <summary>Lavozim vazifasi — bitta band.</summary>
/// <param name="Id">Identifikator.</param>
/// <param name="Text">Vazifa matni.</param>
/// <param name="SortOrder">
/// Vazifalarning o'zaro tartibi.
/// </param>
/// <remarks>
/// Bu tartib LAVOZIM ICHIDAGI vazifalar ketma-ketligi va u SAQLANADI —
/// ma'lumotnoma yozuvlarining tartibidan farqli (u olib tashlandi va
/// ro'yxatlar alifbo bo'yicha chiziladi). Sabab: vazifalar mazmunan
/// ketma-ket bajariladi va ularni alifboga tizib qo'yish ma'nosini
/// yo'qotardi — «Sexni yopish» «Aylanma tekshirish» dan oldin turardi.
/// </remarks>
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
/// <param name="IsActive">Faolmi.</param>
/// <param name="Note">Izoh.</param>
/// <param name="DepartmentId">Lavozim qaysi bo'limga tegishli.</param>
/// <param name="Grade">Daraja: 1 — ishchi … 5 — rahbariyat.</param>
/// <param name="ProbationDays">Standart sinov muddati (kun).</param>
public sealed record PositionDto(
    Guid Id,
    string Name,
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
/// <param name="IsActive">Faolmi.</param>
/// <param name="Note">Izoh.</param>
/// <param name="DepartmentId">Lavozim qaysi bo'limga tegishli.</param>
/// <param name="Grade">Daraja.</param>
/// <param name="ProbationDays">Standart sinov muddati (kun).</param>
/// <param name="Duties">Lavozim vazifalari — tartiblangan.</param>
public sealed record PositionDetailsDto(
    Guid Id,
    string Name,
    bool IsActive,
    string? Note,
    Guid? DepartmentId,
    int Grade,
    int? ProbationDays,
    IReadOnlyList<PositionDutyDto> Duties);
