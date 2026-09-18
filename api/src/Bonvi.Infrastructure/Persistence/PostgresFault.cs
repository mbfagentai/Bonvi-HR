using Bonvi.Application.Abstractions;
using Bonvi.Domain.Attendance;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bonvi.Infrastructure.Persistence;

/// <summary>
/// <see cref="IDatabaseFault"/> ning PostgreSQL uchun amalga oshirilishi.
/// </summary>
/// <remarks>
/// Drayver haqidagi bilim SHU YERDA to'xtaydi — yuqoridagi qatlamlar faqat
/// «nom takrorlandimi» degan javobni oladi. Sabab <see cref="IDatabaseFault"/>
/// izohida.
/// </remarks>
internal sealed class PostgresFault : IDatabaseFault
{
    /// <summary>
    /// Nom bo'yicha unikal indekslarning umumiy naqshi.
    /// </summary>
    /// <remarks>
    /// Indekslar migratsiyada aynan shu qoida bilan yaratiladi:
    /// <c>ux_&lt;jadval&gt;_name</c>. Nom o'zgarsa, bu yerdagi tekshiruv
    /// jimgina ishlamay qo'yadi — shu sababli ikkalasi bir vaqtda
    /// o'zgartiriladi.
    /// </remarks>
    private const string NameIndexPrefix = "ux_";
    private const string NameIndexSuffix = "_name";

    /// <summary>
    /// O'tishlar takrorini to'suvchi indeks — <c>AttendancePassConfiguration</c> dagi nom.
    /// </summary>
    /// <remarks>
    /// Nom o'zgarsa, takror 200 o'rniga 500 bo'lib qaytadi va qurilma uni
    /// cheksiz qayta yuboradi — shu sababli ikkalasi bir vaqtda o'zgartiriladi.
    /// </remarks>
    private const string PassDedupeIndex = "ux_attendance_passes_device_dedupe";

    /// <summary>
    /// Kun yozuvining (xodim, sana) unikalligi — <c>AttendanceDayConfiguration</c> dagi nom.
    /// </summary>
    /// <remarks>
    /// Nom o'zgarsa, parallel qayta hisoblashning birinchi yozuvi yana 500 bo'lib
    /// qaytadi — shu sababli ikkalasi bir vaqtda o'zgartiriladi.
    /// </remarks>
    private const string DayUniqueIndex = "ux_attendance_days_employee_date";

    public bool IsDuplicateName(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var postgres = UniqueViolation(exception);

        return postgres?.ConstraintName is { } name
            && name.StartsWith(NameIndexPrefix, StringComparison.Ordinal)
            && name.EndsWith(NameIndexSuffix, StringComparison.Ordinal);
    }

    public bool IsDuplicatePass(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return string.Equals(UniqueViolation(exception)?.ConstraintName, PassDedupeIndex, StringComparison.Ordinal);
    }

    public bool IsConcurrentDayWrite(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (string.Equals(UniqueViolation(exception)?.ConstraintName, DayUniqueIndex, StringComparison.Ordinal))
        {
            return true;
        }

        // Kun jadvalida parallellik belgisi (concurrency token) YO'Q: EF bu
        // istisnoni faqat UPDATE/DELETE kutilgan bitta qator o'rniga nolga
        // tekkanda tashlaydi — ya'ni qatorni boshqa yozuvchi o'chirib ulgurgan.
        // Faqat kun yozuvlari bo'lsa: boshqa jadvaldagi bunday holat bu
        // yerda «qayta o'qib yoz» bilan yashirilmasligi kerak.
        return exception is DbUpdateConcurrencyException concurrency
            && concurrency.Entries.Count > 0
            && concurrency.Entries.All(entry => entry.Entity is AttendanceDay);
    }

    /// <summary>Unikallik buzilishi bo'lsa — drayver istisnosi, aks holda <c>null</c>.</summary>
    /// <remarks>
    /// Istisno EF ning <c>DbUpdateException</c> i ichida keladi, lekin
    /// to'g'ridan-to'g'ri ham berilishi mumkin — ikkalasi ham qaraladi.
    /// </remarks>
    private static PostgresException? UniqueViolation(Exception exception)
    {
        var postgres = exception as PostgresException
            ?? (exception as DbUpdateException)?.InnerException as PostgresException;

        return postgres is { SqlState: PostgresErrorCodes.UniqueViolation } ? postgres : null;
    }
}
