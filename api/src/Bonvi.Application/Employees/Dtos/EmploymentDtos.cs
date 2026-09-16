using Bonvi.Application.Catalog.Dtos;
using Bonvi.Application.Employees.Common;
using Bonvi.Domain.Employees;

namespace Bonvi.Application.Employees.Dtos;

/// <summary>Mehnat munosabati.</summary>
public sealed record EmploymentDto(
    Guid Id,
    Guid EmployeeId,
    Guid LegalEntityId,
    string? LegalEntity,
    Guid? DepartmentId,
    string? Department,
    Guid? PositionId,
    string? Position,
    DateOnly HireDate,
    string? HireOrderNo,
    DateOnly? HireOrderDate,
    MoneyDto? Salary,
    decimal Rate,
    bool IsPrimary,
    EmploymentStatus Status,
    DateOnly? TerminationDate,
    Guid? TerminationReasonId,
    // Sabab NOMI identifikatorining yonida — bo'lim, lavozim va firma
    // bilan bir xil naqsh. Usiz kartadagi «Ishdan ketish sababi» qatori
    // doim bo'sh chiqardi: mijozda identifikatorni nomga aylantiradigan
    // hech narsa yo'q va HR aynan shu ma'lumotni qidiradi.
    string? TerminationReason,
    string? TerminationOrderNo,
    string? TerminationNote,
    Guid? ResponsibleEmployeeId,
    bool NeedsReview,
    string? ReviewNote,
    int WorkedDays,
    ProbationDto? Probation);

/// <summary>Mehnat yo'lidagi bitta hodisa (buyruq).</summary>
/// <remarks>
/// <c>UpdatedAt</c> — terish xatosi tuzatilgan lahza; tegilmagan yozuvda
/// <c>null</c> va o'shanda interfeys «tahrirlandi» qaydini umuman
/// chizmaydi (<c>EmploymentEvent.UpdatedAt</c> izohi).
/// </remarks>
public sealed record EmploymentEventDto(
    Guid Id,
    Guid EmploymentId,
    EmploymentEventType Type,
    DateOnly EffectiveDate,
    string? OrderNo,
    DateOnly? OrderDate,
    Guid? FromDepartmentId,
    string? FromDepartment,
    Guid? ToDepartmentId,
    string? ToDepartment,
    Guid? FromPositionId,
    string? FromPosition,
    Guid? ToPositionId,
    string? ToPosition,
    MoneyDto? FromSalary,
    MoneyDto? ToSalary,
    string? Note,
    DateTimeOffset RecordedAt,
    DateTimeOffset? UpdatedAt,
    bool IsImported);

/// <summary>
/// Timeline'ning bitta elementi: hodisa + u qaysi firmadagi ishga tegishli.
/// </summary>
/// <remarks>
/// Firma nomi aynan shu yerda kerak: Bonvi — holding va bitta odamning
/// lentasida ikki firmaning hodisalari aralashadi. Firma ko'rsatilmasa,
/// lenta «ikki marta ishga qabul qilindi» degan chalkash ko'rinish beradi.
/// </remarks>
public sealed record TimelineItemDto(
    Guid EmploymentId,
    string? LegalEntity,
    EmploymentEventDto Event);

/// <summary>
/// Buyruqlar ro'yxatining bitta qatori.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="EmploymentEventDto"/> dan farqi — u BITTA xodim kartasidagi
/// lenta uchun: kim ekani allaqachon ma'lum, ammo bo'lim/lavozimning
/// identifikatorlari kerak (kartadan havola qo'yiladi). Bu yerda esa
/// teskari: ro'yxatda xodimning kimligi va nomlar muhim, identifikatorlar
/// esa qatorni og'irlashtiradi.
/// </para>
/// <para>
/// Bo'lim va lavozim juftlik bo'lib turadi («qayerdan → qayerga»): HR
/// ro'yxatga qarab «Sotuvdan Omborga o'tdi» degan xulosani bitta qatorda
/// o'qishi kerak, ikkita ekranni solishtirmasdan.
/// </para>
/// </remarks>
public sealed record EmploymentEventListItemDto(
    Guid Id,
    Guid EmploymentId,
    Guid EmployeeId,
    string EmployeeName,
    string? PhotoKey,
    EmploymentEventType Type,
    DateOnly EffectiveDate,
    string? OrderNo,
    DateOnly? OrderDate,
    string? FromDepartment,
    string? ToDepartment,
    string? FromPosition,
    string? ToPosition,
    MoneyDto? FromSalary,
    MoneyDto? ToSalary,
    string? Note,
    bool IsImported);

/// <summary>Xodimning butun mehnat yo'li — bitta tartiblangan lentada.</summary>
public sealed record EmployeeTimelineDto(
    Guid EmployeeId,
    string FullName,
    IReadOnlyList<TimelineItemDto> Items);

/// <summary>Sinov muddati.</summary>
public sealed record ProbationDto(
    Guid Id,
    Guid EmploymentId,
    DateOnly StartDate,
    DateOnly EndDate,
    ProbationStatus Status,
    DateOnly? ResolvedAt,
    string? Conclusion,
    Guid? ReviewerEmployeeId,
    int DaysLeft,
    bool NeedsWarning);

/// <summary>Sinov muddati — xodim ma'lumoti bilan (ro'yxat ekrani uchun).</summary>
public sealed record ProbationListItemDto(
    Guid Id,
    Guid EmploymentId,
    Guid EmployeeId,
    string EmployeeName,
    string? PhotoKey,
    string? Department,
    string? Position,
    Guid? DepartmentId,
    Guid? PositionId,
    DateOnly HireDate,
    DateOnly StartDate,
    DateOnly EndDate,
    ProbationStatus Status,
    DateOnly? ResolvedAt,
    string? Conclusion,
    int DaysLeft,
    bool NeedsWarning);

/// <summary>
/// Ishga qabul natijasi.
/// </summary>
/// <remarks>
/// Yagona formaning javobi ham yagona: yaratilgan munosabat, lavozimning
/// vazifalari (HR ularni darhol ko'rmoqchi) va ogohlantirishlar.
/// </remarks>
public sealed record HireResultDto(
    EmploymentDto Employment,
    IReadOnlyList<PositionDutyDto> Duties,
    IReadOnlyList<HrWarning> Warnings);

/// <summary>Mehnat munosabatidagi o'zgarish natijasi (ko'chirish, oklad, vazifa, bo'shatish).</summary>
public sealed record EmploymentChangeDto(
    EmploymentDto Employment,
    IReadOnlyList<HrWarning> Warnings);
