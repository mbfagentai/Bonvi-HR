using Bonvi.Domain.Attendance;

namespace Bonvi.Application.Attendance.Mobile;

/// <summary>
/// Qabul qilingan belgining kvitansiyasi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <see cref="OccurredAt"/> — SERVERNING vaqti, telefonniki emas. Ilova
/// ekranda aynan shuni ko'rsatadi: telefonning soati noto'g'ri qo'yilgan
/// bo'lsa, odam «men 08:30 da belgiladim» deb o'ylab, tabelda 11:00 ni
/// ko'rardi va bunga hech qanday tushuntirish topib bo'lmasdi (SPEC §B3).
/// </para>
/// <para>
/// Masofa va aniqlik ATAYLAB qaytarilmaydi: belgi qabul qilingan bo'lsa,
/// u to'siq ichida edi — bu yagona kerakli javob. Sonlarni ko'rsatish
/// «188 metr — demak 200 dan yaqin turishim kerak» degan o'yinni boshlab
/// berardi.
/// </para>
/// </remarks>
/// <param name="OccurredAt">Belgi lahzasi — SERVER soati bo'yicha, UTC.</param>
/// <param name="Direction">Qabul qilingan yo'nalish.</param>
/// <param name="WorkSiteName">Belgi qaysi obyektda qabul qilindi.</param>
public sealed record MobileCheckInResultDto(
    DateTimeOffset OccurredAt,
    PassDirection Direction,
    string WorkSiteName);
