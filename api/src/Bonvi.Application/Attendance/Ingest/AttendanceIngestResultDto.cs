namespace Bonvi.Application.Attendance.Ingest;

/// <summary>
/// Qurilmaga qaytadigan javob — <c>{ ok, stored, duplicates }</c>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Javobda KIM o'tgani haqida hech narsa yo'q</b> — na xodim nomi, na
/// identifikatori, na «moslik topildimi» belgisi (SPEC §6.1). Sizib chiqqan
/// token binoda kim borligini so'raydigan oynaga aylanmasligi kerak.
/// </para>
/// <para>
/// <c>AttendanceDtos.cs</c> da EMAS, shu yerda: bu yozuvni faqat qabul yo'li
/// yasaydi va ishlatadi (SPEC §10.2.1).
/// </para>
/// </remarks>
/// <param name="Ok">Har doim <c>true</c> — xato javobi ProblemDetails bo'lib qaytadi.</param>
/// <param name="Stored">Yangi yozilgan o'tishlar soni.</param>
/// <param name="Duplicates">Allaqachon bor bo'lib chiqqan (qayta yuborilgan) o'tishlar soni.</param>
public sealed record AttendanceIngestResultDto(bool Ok, int Stored, int Duplicates);
