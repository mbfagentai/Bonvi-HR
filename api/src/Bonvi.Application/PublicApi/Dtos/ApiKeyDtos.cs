using Bonvi.Domain.PublicApi;

namespace Bonvi.Application.PublicApi.Dtos;

/// <summary>
/// API kaliti — sozlamalar jadvalidagi bitta qator.
/// </summary>
/// <remarks>
/// ⚠️ Bu yerda kalitning O'ZI YO'Q va hech qachon bo'lmaydi. Ko'rinadigan
/// yagona qism — <see cref="Prefix"/>: u ochiq saqlanadi va «qaysi kalit»
/// degan savolga javob berish uchun yetarli.
/// </remarks>
/// <param name="Id">Kalit yozuvi.</param>
/// <param name="Name">Qaysi loyihaga berilgan.</param>
/// <param name="Prefix">Kalitning ochiq qismi (8 belgi).</param>
/// <param name="CreatedAt">Yaratilgan vaqti (UTC).</param>
/// <param name="LastUsedAt">Oxirgi ishlatilgan vaqti. Aniqligi — daqiqa.</param>
/// <param name="LastUsedIp">So'rov kelgan oxirgi IP.</param>
/// <param name="RevokedAt">Bekor qilingan vaqti. <c>null</c> — kalit kuchda.</param>
/// <param name="IsActive">Kalit hozir ishlaydimi.</param>
/// <param name="Scope">Doira: Basic — xodimlar va ma'lumotnomalar; FullRead — barcha bo'limlar (pasport, JSHSHIR, fayllarsiz); DepartmentAttendance — bitta bo'limning davomati.</param>
/// <param name="DepartmentId">Bo'lim davomati kalitining bo'limi; boshqa doiralarda <c>null</c>.</param>
/// <param name="DepartmentName">Shu bo'limning nomi (o'chirilgan bo'lsa <c>null</c>).</param>
public sealed record ApiKeyDto(
    Guid Id,
    string Name,
    string Prefix,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    string? LastUsedIp,
    DateTimeOffset? RevokedAt,
    bool IsActive,
    ApiKeyScope Scope,
    Guid? DepartmentId = null,
    string? DepartmentName = null);

/// <summary>
/// Endigina yaratilgan kalit — <b>kalit matni bir marta ko'rinadigan yagona joy</b>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Key"/> bazada saqlanmaydi va boshqa hech qanday so'rovdan
/// qaytmaydi. Foydalanuvchi uni shu javobdan ko'chirib olmasa, yagona
/// yo'l — kalitni bekor qilib, yangisini yaratish.
/// </para>
/// <para>
/// Aynan shu sababli ekran uni nusxa olish tugmasi va ochiq ogohlantirish
/// bilan ko'rsatishi shart (SPEC §6).
/// </para>
/// </remarks>
/// <param name="Id">Kalit yozuvi.</param>
/// <param name="Name">Qaysi loyihaga berilgan.</param>
/// <param name="Prefix">Kalitning ochiq qismi — keyin ham ko'rinadi.</param>
/// <param name="Key">TO'LIQ kalit. FAQAT shu javobda.</param>
/// <param name="Scope">Kalit doirasi.</param>
/// <param name="DepartmentId">Bo'lim davomati kalitining bo'limi.</param>
public sealed record ApiKeyCreatedDto(
    Guid Id,
    string Name,
    string Prefix,
    string Key,
    ApiKeyScope Scope,
    Guid? DepartmentId = null);
