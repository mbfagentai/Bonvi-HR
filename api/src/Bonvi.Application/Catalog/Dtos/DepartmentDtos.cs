namespace Bonvi.Application.Catalog.Dtos;

/// <summary>
/// Bo'lim — tekis ro'yxat ko'rinishi.
/// </summary>
/// <remarks>
/// Daraxt kerak bo'lmagan joylarda (masalan tanlash ro'yxati) shu ishlatiladi:
/// <see cref="Path"/> allaqachon to'liq yo'lni saqlaydi, shu sababli
/// foydalanuvchi bo'limni ierarxiyasiz ham aniq taniydi.
/// </remarks>
/// <param name="Id">Identifikator.</param>
/// <param name="Name">Nom.</param>
/// <param name="Code">Qisqa kod.</param>
/// <param name="SortOrder">Ro'yxatdagi tartib.</param>
/// <param name="IsActive">Faolmi.</param>
/// <param name="Note">Izoh.</param>
/// <param name="ParentId">Ota bo'lim. <c>null</c> — eng yuqori daraja.</param>
/// <param name="Level">Ierarxiyadagi chuqurlik: 0 — eng yuqori.</param>
/// <param name="Path">To'liq yo'l: «7-Departament / Metan zavod / Plazma bo'limi».</param>
/// <param name="LegalEntityId">Bo'lim qaysi yuridik shaxsga tegishli.</param>
/// <param name="HeadEmployeeId">Bo'lim boshlig'i (xodim).</param>
public sealed record DepartmentDto(
    Guid Id,
    string Name,
    string? Code,
    int SortOrder,
    bool IsActive,
    string? Note,
    Guid? ParentId,
    int Level,
    string? Path,
    Guid? LegalEntityId,
    Guid? HeadEmployeeId);

/// <summary>
/// Bo'limlar daraxtining bitta tuguni — ichma-ich.
/// </summary>
/// <remarks>
/// Butun daraxt BITTA so'rovda qaytariladi: interfeys uni ochib-yopib
/// ko'rsatadi va har bir tugun uchun alohida so'rov yubormaydi.
/// Bonvi'da bo'limlar soni yuztadan oshmaydi (Excel'da 73 ta edi),
/// shu sababli to'liq daraxtni bir marta yuborish arzon.
/// </remarks>
/// <param name="Id">Identifikator.</param>
/// <param name="Name">Nom.</param>
/// <param name="Code">Qisqa kod.</param>
/// <param name="SortOrder">Ro'yxatdagi tartib.</param>
/// <param name="IsActive">Faolmi.</param>
/// <param name="ParentId">Ota bo'lim.</param>
/// <param name="Level">Ierarxiyadagi chuqurlik.</param>
/// <param name="Path">To'liq yo'l.</param>
/// <param name="Children">Ichidagi bo'limlar.</param>
public sealed record DepartmentTreeNodeDto(
    Guid Id,
    string Name,
    string? Code,
    int SortOrder,
    bool IsActive,
    Guid? ParentId,
    int Level,
    string? Path,
    IReadOnlyList<DepartmentTreeNodeDto> Children);
