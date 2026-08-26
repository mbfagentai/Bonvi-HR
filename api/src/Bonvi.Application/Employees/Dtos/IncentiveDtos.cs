using Bonvi.Domain.Employees;

namespace Bonvi.Application.Employees.Dtos;

/// <summary>Rag'bat yoki jarima yozuvi (91.14 + HR so'ragan yangi jarima imkoniyati).</summary>
public sealed record IncentiveDto(
    Guid Id,
    Guid EmployeeId,
    IncentiveKind Kind,
    Guid TypeId,
    // Tur NOMI identifikatorining yonida — bo'lim/lavozim/firma bilan bir
    // xil naqsh: aks holda ro'yxatda «tur» ustuni doim bo'sh chiqardi.
    string? TypeName,
    DateOnly Date,
    MoneyDto? Amount,
    string? GiftDescription,
    string? Reason,
    string? OrderNo,
    DateOnly? OrderDate);
