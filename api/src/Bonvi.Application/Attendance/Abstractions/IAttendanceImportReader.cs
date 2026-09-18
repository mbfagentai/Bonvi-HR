namespace Bonvi.Application.Attendance.Abstractions;

/// <summary>Import faylining formati — kengaytmadan aniqlanadi.</summary>
public enum ImportFileFormat
{
    /// <summary>Excel 2007+ (<c>.xlsx</c>).</summary>
    Xlsx = 1,

    /// <summary>Matnli jadval (<c>.csv</c>), UTF-8.</summary>
    Csv = 2,
}

/// <summary>Fayldagi bitta bo'sh bo'lmagan qator — kataklar MATN bo'lib.</summary>
/// <param name="RowNumber">Fayldagi qator raqami (1 dan) — operator Excel'da aynan shu qatorni topadi.</param>
/// <param name="Cells">Kataklar, 0-ustundan; bo'sh katak — bo'sh satr.</param>
public sealed record ImportSheetRow(int RowNumber, IReadOnlyList<string> Cells);

/// <summary>
/// Jadval faylini (xlsx/csv) matn kataklariga aylantiradigan port.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega port faqat O'QIYDI, sarlavhani tanimaydi.</b> Qaysi ustun shaxs
/// raqami ekani — qoida (SPEC §4.4 aliaslari), fayl o'qish esa texnologiya
/// (ClosedXML). Qoida Application'da qolsa, u bazasiz va fayllarsiz
/// testlanadi, kutubxona almashsa esa qoida tegilmaydi.
/// </para>
/// <para>
/// Son katagi MATNGA o'giriladi, formatlanmaydi: Excel «10427» ni son qilib
/// saqlaydi, va «Umumiy» formatdagi 14 xonali JSHSHIR ko'rinishida
/// «3,12346E+13» bo'lib qolardi.
/// </para>
/// </remarks>
public interface IAttendanceImportReader
{
    /// <summary>Birinchi varaqning bo'sh bo'lmagan qatorlarini o'qiydi.</summary>
    /// <param name="content">Fayl mazmuni.</param>
    /// <param name="format">Format.</param>
    /// <param name="maxRows">
    /// Shuncha qatordan keyin o'qish to'xtaydi — chaqiruvchi chegaradan
    /// OSHGANINI bilishi uchun chegaradan kamida bitta ko'p so'raydi. 10 MB
    /// fayldan millionlab qator xotiraga tushmasligi uchun.
    /// </param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    /// <returns>Qatorlar; fayl bu formatda o'qilmasa — <c>null</c>.</returns>
    Task<IReadOnlyList<ImportSheetRow>?> ReadAsync(
        Stream content,
        ImportFileFormat format,
        int maxRows,
        CancellationToken cancellationToken = default);
}
