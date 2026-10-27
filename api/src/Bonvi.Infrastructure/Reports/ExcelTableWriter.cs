using System.Globalization;
using Bonvi.Application.Reports.Export;
using Bonvi.Domain.Abstractions;
using ClosedXML.Excel;

namespace Bonvi.Infrastructure.Reports;

/// <summary>
/// <see cref="IExcelTableWriter"/> ning <c>ClosedXML</c> orqali amalga oshirilishi.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IncentiveExcelWriter"/> qoidalari, umumiy shaklda: sarlavha qalin va
/// kulrang fonda, birinchi qator qotirilgan, sarlavhada avtofiltr — HR faylni ochgan
/// zahoti istalgan ustun bo'yicha qayta filtrlay oladi.
/// </para>
/// <para>
/// Sana va son — HAQIQIY Excel qiymati, formatlangan matn emas: ustunni Excel'ning
/// o'zida saralash va yig'indilash ishlashi kerak. Vaqt TOSHKENT vaqtida
/// (<see cref="BonviTime.ToTashkent"/>) — ekrandagi bilan bir xil.
/// </para>
/// </remarks>
public sealed class ExcelTableWriter : IExcelTableWriter
{
    private const string DateFormat = "dd.mm.yyyy";
    private const string DateTimeFormat = "dd.mm.yyyy hh:mm";
    private const double LongTextMaxWidth = 60;
    private const double MaxWidth = 50;

    /// <inheritdoc />
    public byte[] Write<TRow>(string sheetName, IReadOnlyList<ExcelColumn<TRow>> columns, IReadOnlyList<TRow> rows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sheetName);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SafeSheetName(sheetName));

        for (var column = 0; column < columns.Count; column++)
        {
            sheet.Cell(1, column + 1).Value = columns[column].Header;
        }

        var headerRow = sheet.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F2F2");

        for (var row = 0; row < rows.Count; row++)
        {
            for (var column = 0; column < columns.Count; column++)
            {
                var definition = columns[column];
                SetCell(sheet.Cell(row + 2, column + 1), definition.Kind, definition.Value(rows[row]));
            }
        }

        if (columns.Count > 0)
        {
            sheet.Range(1, 1, Math.Max(rows.Count, 1) + 1, columns.Count).SetAutoFilter();
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();

        for (var column = 0; column < columns.Count; column++)
        {
            var excelColumn = sheet.Column(column + 1);

            if (columns[column].Kind == ExcelCellKind.LongText)
            {
                // Erkin matn — ustun haddan tashqari kengaymasin, qator ichida o'ralsin
                excelColumn.Width = Math.Min(excelColumn.Width, LongTextMaxWidth);
                excelColumn.Style.Alignment.WrapText = true;
            }
            else if (excelColumn.Width > MaxWidth)
            {
                excelColumn.Width = MaxWidth;
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void SetCell(IXLCell cell, ExcelCellKind kind, object? value)
    {
        switch (value)
        {
            case null:
                return;

            case DateOnly date:
                cell.Value = date.ToDateTime(TimeOnly.MinValue);
                cell.Style.DateFormat.Format = DateFormat;
                return;

            case DateTimeOffset instant:
                cell.Value = instant.ToTashkent().DateTime;
                cell.Style.DateFormat.Format = kind == ExcelCellKind.Date ? DateFormat : DateTimeFormat;
                return;

            case bool flag:
                cell.Value = flag ? "Ha" : "Yo'q";
                return;

            case int or long or short:
                cell.Value = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                cell.Style.NumberFormat.Format = "#,##0";
                return;

            case decimal or double or float:
                var number = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                cell.Value = number;
                cell.Style.NumberFormat.Format =
                    kind == ExcelCellKind.WholeNumber || decimal.Truncate(number) == number ? "#,##0" : "#,##0.##";
                return;

            default:
                cell.Value = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                return;
        }
    }

    /// <summary>
    /// Excel varaq nomi: 31 belgigacha, <c>: \ / ? * [ ]</c> belgilarisiz — aks holda
    /// <c>ClosedXML</c> istisno tashlaydi.
    /// </summary>
    private static string SafeSheetName(string name)
    {
        var cleaned = new string(name.Select(ch => ":\\/?*[]".Contains(ch, StringComparison.Ordinal) ? ' ' : ch).ToArray()).Trim();
        return cleaned.Length <= 31 ? cleaned : cleaned[..31];
    }
}
