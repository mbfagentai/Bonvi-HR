using Bonvi.Application.Abstractions;
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

    public bool IsDuplicateName(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        // Istisno EF ning `DbUpdateException` i ichida keladi, lekin
        // to'g'ridan-to'g'ri ham berilishi mumkin — ikkalasi ham qaraladi.
        var postgres = exception as PostgresException
            ?? (exception as DbUpdateException)?.InnerException as PostgresException;

        return postgres is { SqlState: PostgresErrorCodes.UniqueViolation }
            && postgres.ConstraintName is { } name
            && name.StartsWith(NameIndexPrefix, StringComparison.Ordinal)
            && name.EndsWith(NameIndexSuffix, StringComparison.Ordinal);
    }
}
