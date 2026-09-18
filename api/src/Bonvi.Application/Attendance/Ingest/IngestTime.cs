using System.Globalization;
using System.Text.RegularExpressions;
using Bonvi.Domain.Abstractions;

namespace Bonvi.Application.Attendance.Ingest;

/// <summary>
/// Qurilma yozgan ISO 8601 vaqtini o'qiydi — ikkala tahlilchi uchun bitta qoida.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega <c>DateTimeOffset.TryParse</c> emas.</b> U ikki joyda jimgina
/// adashadi. Birinchisi: siljishsiz satrni («2026-09-17T08:03:12») SERVER
/// mintaqasida deb oladi — konteynerda bu UTC, ya'ni har bir o'tish besh
/// soatga surilib, «hamma ertalab soat 3 da keladi» bo'lib ko'rinardi.
/// Ikkinchisi: «17.09.2026 8:03» kabi ixtiyoriy formatni ham qabul qiladi,
/// SPEC esa ISO 8601 talab qiladi — qurilma sozlamasidagi xato darhol
/// «o'qib bo'lmadi» bo'lib ko'rinishi kerak, taxminiy sana bo'lib emas.
/// </para>
/// <para>
/// Shu sababli avval shakl qat'iy naqsh bilan tekshiriladi va siljish bor-
/// yo'qligi ANIQ bilinadi; keyin qiymat faqat o'sha shaklga mos usul bilan
/// o'qiladi. Siljish yo'q ⇒ Toshkent (+5) qo'yiladi va taxmin belgilanadi
/// (SPEC §3.8).
/// </para>
/// </remarks>
internal static partial class IngestTime
{
    /// <summary>ISO 8601 vaqt satrini o'qiydi.</summary>
    /// <param name="value">Qurilma yozgan satr.</param>
    /// <param name="instant">O'qilgan lahza — siljish bilan.</param>
    /// <param name="offsetAssumed">Satrda siljish yo'q edi va Toshkent qo'yildi.</param>
    /// <returns>Satr ISO 8601 shaklida va haqiqiy sana bo'lsa — <c>true</c>.</returns>
    public static bool TryParse(string? value, out DateTimeOffset instant, out bool offsetAssumed)
    {
        instant = default;
        offsetAssumed = false;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var match = IsoShakl().Match(value.Trim());
        if (!match.Success)
        {
            return false;
        }

        var local = match.Groups["mahalliy"].Value;
        var offset = match.Groups["siljish"].Value;

        if (!DateTime.TryParse(local, CultureInfo.InvariantCulture, DateTimeStyles.None, out var wall))
        {
            return false;
        }

        wall = DateTime.SpecifyKind(wall, DateTimeKind.Unspecified);

        if (offset.Length == 0)
        {
            offsetAssumed = true;
            instant = new DateTimeOffset(wall, BonviTime.TashkentOffset);
            return true;
        }

        if (!TryReadOffset(offset, out var span))
        {
            return false;
        }

        instant = new DateTimeOffset(wall, span);
        return true;
    }

    /// <summary><c>Z</c>, <c>+05:00</c> yoki <c>+0500</c> ko'rinishidagi siljishni o'qiydi.</summary>
    private static bool TryReadOffset(string offset, out TimeSpan span)
    {
        span = TimeSpan.Zero;

        if (offset is "Z" or "z")
        {
            return true;
        }

        var sign = offset[0] == '-' ? -1 : 1;
        var digits = offset[1..].Replace(":", string.Empty, StringComparison.Ordinal);

        if (digits.Length != 4
            || !int.TryParse(digits[..2], NumberStyles.None, CultureInfo.InvariantCulture, out var hours)
            || !int.TryParse(digits[2..], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
            || hours > 14
            || minutes > 59)
        {
            return false;
        }

        span = sign * new TimeSpan(hours, minutes, 0);
        return true;
    }

    /// <summary>
    /// <c>yyyy-MM-ddTHH:mm[:ss[.fffffff]][Z|±HH:mm|±HHmm]</c>.
    /// </summary>
    /// <remarks>
    /// Soniyasiz shakl ham qabul qilinadi — ba'zi proshivkalar shunday
    /// yozadi va u hali ham bir ma'noli. Sana va vaqt orasida faqat
    /// <c>T</c>: bo'sh joyli «2026-09-17 08:03» ISO emas va ataylab rad etiladi.
    /// </remarks>
    [GeneratedRegex(
        @"^(?<mahalliy>\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(?::\d{2}(?:\.\d{1,7})?)?)(?<siljish>Z|z|[+-]\d{2}:?\d{2})?$",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 200)]
    private static partial Regex IsoShakl();
}
