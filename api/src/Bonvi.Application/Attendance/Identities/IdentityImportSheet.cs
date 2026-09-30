using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Common;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Identities;

/// <summary>Import faylidan ajratib olingan bitta qator.</summary>
/// <param name="RowNumber">Fayldagi qator raqami.</param>
/// <param name="RawPersonId">Shaxs raqami — qirqilgan.</param>
/// <param name="FullName">F.I.Sh. (ustuni bo'lsa).</param>
/// <param name="Pinfl">JSHSHIR (ustuni bo'lsa).</param>
public sealed record IdentityImportLine(int RowNumber, string RawPersonId, string? FullName, string? Pinfl);

/// <summary>
/// Verifix ro'yxatining sarlavhasini topadi va qatorlarini ajratadi — SPEC §4.4.
/// </summary>
/// <remarks>
/// <para>
/// <b>Sarlavha birinchi qatorda bo'lishi shart emas.</b> Verifix eksportida
/// tepada sarlavha matni, sana, bo'sh qatorlar bo'ladi — shu sababli birinchi
/// <see cref="HeaderScanRows"/> qatordan shaxs raqami aliasi bor birinchisi
/// sarlavha deb olinadi (ETL dagi <c>AliasTable</c> usuli).
/// </para>
/// <para>
/// <b>Bitta qatorda bir nechta mos ustun</b> bo'lsa (masalan «ID» va
/// «Employee No»), ro'yxatda OLDINROQ turgan alias yutadi: aniqrog'i
/// oldinda, umumiyrog'i («ID», «Kod») oxirida.
/// </para>
/// <para>
/// Shaxs raqami bo'sh qator tashlab yuboriladi (yakun, izoh, bo'lim nomi
/// qatorlari) — uni hech kimga bog'lab bo'lmaydi va ro'yxatda faqat
/// shovqin bo'lardi.
/// </para>
/// </remarks>
public static class IdentityImportSheet
{
    /// <summary>Sarlavha qidiriladigan birinchi qatorlar soni.</summary>
    public const int HeaderScanRows = 10;

    /// <summary>Shaxs raqami ustuni — aniqrog'i oldinda.</summary>
    private static readonly string[] PersonIdAliases =
    [
        "employeeNo", "Employee No", "Employee ID", "Person ID", "ID",
        "Табельный номер", "Персональный номер", "Ходим рақами", "Xodim raqami", "Kod",
    ];

    /// <summary>F.I.Sh. ustuni.</summary>
    private static readonly string[] FullNameAliases =
    [
        "ФИО", "Ф.И.О.", "F.I.Sh.", "FIO", "Name", "Ходим", "Xodim", "Сотрудник",
    ];

    /// <summary>JSHSHIR ustuni.</summary>
    private static readonly string[] PinflAliases = ["ЖШШИР", "JSHSHIR", "ПИНФЛ", "PINFL"];

    /// <summary>O'quvchidan nechta qator so'ralishi kerak — chegaradan oshganini bilish uchun bittasi ortiq.</summary>
    public static int RowsToRead => HeaderScanRows + AttendanceRules.MaxImportRows + 1;

    /// <summary>Sarlavhani topib, ma'lumot qatorlarini qaytaradi.</summary>
    /// <param name="rows">Fayldagi bo'sh bo'lmagan qatorlar.</param>
    public static Result<IReadOnlyList<IdentityImportLine>> Parse(IReadOnlyList<ImportSheetRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        for (var index = 0; index < Math.Min(HeaderScanRows, rows.Count); index++)
        {
            var header = rows[index].Cells;
            var personIdColumn = FindColumn(header, PersonIdAliases);

            if (personIdColumn is not { } idColumn)
            {
                continue;
            }

            var nameColumn = FindColumn(header, FullNameAliases);
            var pinflColumn = FindColumn(header, PinflAliases);

            var lines = new List<IdentityImportLine>();

            foreach (var row in rows.Skip(index + 1))
            {
                var personId = Cell(row, idColumn);
                if (personId is null)
                {
                    continue;
                }

                if (lines.Count == AttendanceRules.MaxImportRows)
                {
                    return AttendanceErrors.ImportTooManyRows;
                }

                lines.Add(new IdentityImportLine(
                    row.RowNumber,
                    personId,
                    nameColumn is { } name ? Cell(row, name) : null,
                    pinflColumn is { } pinfl ? Cell(row, pinfl) : null));
            }

            return lines;
        }

        return AttendanceErrors.ImportIdColumnMissing;
    }

    private static int? FindColumn(IReadOnlyList<string> header, string[] aliases)
    {
        int? best = null;
        var bestRank = int.MaxValue;

        for (var column = 0; column < header.Count; column++)
        {
            var key = Key(header[column]);
            if (key.Length == 0)
            {
                continue;
            }

            var rank = Array.FindIndex(aliases, alias => Key(alias) == key);
            if (rank >= 0 && rank < bestRank)
            {
                best = column;
                bestRank = rank;
            }
        }

        return best;
    }

    private static string? Cell(ImportSheetRow row, int column)
    {
        if (column >= row.Cells.Count)
        {
            return null;
        }

        var value = row.Cells[column].Trim();
        return value.Length == 0 ? null : value;
    }

    /// <summary>Alias kaliti: kichik harf, bo'shliqlar bittadan (ETL dagi <c>AliasTable.Key</c> qoidasi).</summary>
    private static string Key(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
}
