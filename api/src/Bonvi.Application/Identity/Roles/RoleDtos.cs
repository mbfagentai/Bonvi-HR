using System.Text.Json.Serialization;

namespace Bonvi.Application.Identity.Roles;

/// <summary>Rol — «Rollar» menyusi va foydalanuvchi formasidagi tanlash uchun.</summary>
/// <param name="Id">Rol identifikatori.</param>
/// <param name="Code">
/// Mashina kodi. Egasi yaratgan rolda u avtomatik (<c>custom_&lt;8 hex&gt;</c>)
/// va ekranda ko'rsatilmaydi; foydalanuvchi formasi rolni shu kod bilan yuboradi.
/// </param>
/// <param name="Name">Egasi yozgan nom — tarjima qilinmaydi, yozilganidek ko'rsatiladi.</param>
/// <param name="IsProtected">
/// Super administrator — muharrirda faqat o'qish uchun ochiladi.
/// </param>
/// <param name="Permissions">Rolning ruxsat kodlari (alifbo tartibida).</param>
/// <param name="UserCount">
/// Shu roldagi hisoblar soni — faol va faolsizlari ham: rol o'chirilishi
/// faqat shu nol bo'lganda mumkin.
/// </param>
public sealed record RoleDto(
    Guid Id,
    string Code,
    string Name,
    bool IsProtected,
    IReadOnlyList<string> Permissions,
    int UserCount);

/// <summary>Rollar muharriri uchun ruxsatlar xaritasi.</summary>
/// <param name="Modules">Bo'limlar — muharrirdagi tartibda.</param>
/// <param name="Extras">Bironta bo'limga tegishli bo'lmagan ruxsatlar (masalan <c>salary.view</c>).</param>
public sealed record PermissionCatalogDto(
    IReadOnlyList<PermissionModuleDto> Modules,
    IReadOnlyList<string> Extras);

/// <summary>Muharrirdagi bitta bo'lim.</summary>
/// <param name="Key">Bo'lim kaliti — interfeys <c>roles.module.&lt;key&gt;</c> bo'yicha tarjima qiladi.</param>
/// <param name="Levels">
/// Bo'limda BOR darajalar: kalit — <c>view</c> | <c>edit</c> | <c>full</c>,
/// qiymat — shu darajaga yetgan rol oladigan BARCHA kodlar (kumulyativ).
/// </param>
/// <param name="Actions">
/// Alohida beriladigan amallar. Amali yo'q bo'limda maydon javobda UMUMAN
/// bo'lmaydi (<c>null</c> emas) — shartnomadagi ko'rinish aynan shunday.
/// </param>
public sealed record PermissionModuleDto(
    string Key,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Levels,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? Actions);
