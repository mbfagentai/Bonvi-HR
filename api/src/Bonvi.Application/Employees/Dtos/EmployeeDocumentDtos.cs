namespace Bonvi.Application.Employees.Dtos;

/// <summary>
/// Xodimga biriktirilgan fayl haqida ma'lumot. Fayl bayti alohida
/// endpointdan (<c>GET …/documents/{id}/file</c>) olinadi.
/// </summary>
/// <remarks>
/// <c>UploadedAt</c> — bu <c>CreatedAt</c> (audit belgisi): hujjat uchun
/// alohida "yuklangan sana" maydoni ATAYLAB yo'q, chunki u aynan shu bilan
/// bir xil ma'noni ikki marta so'ragan bo'lardi.
/// </remarks>
public sealed record EmployeeDocumentDto(
    Guid Id,
    Guid EmployeeId,
    Guid DocumentTypeId,
    string? DocumentTypeName,
    string FileName,
    string ContentType,
    long SizeBytes,
    string? Note,
    DateTimeOffset UploadedAt);
