using Bonvi.Application.Employees.Abstractions;
using Bonvi.Application.Reports.Abstractions;
using Bonvi.Domain.Employees;
using ClosedXML.Excel;

namespace Bonvi.Infrastructure.Reports;

/// <summary>
/// <see cref="IIncentiveExcelWriter"/> ning <c>ClosedXML</c> orqali amalga oshirilishi.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="EmployeeExcelWriter"/> qoidalari: sarlavhalar o'zbekcha (lotin),
/// sana — haqiqiy Excel sana (<c>dd.mm.yyyy</c>), summa — son va valyuta
/// alohida ustunda. HR ustunni Excel'ning o'zida yig'indilashi va saralashi
/// kerak — formatlangan matnda bu ishlamaydi.
/// </para>
/// <para>
/// <b>Foiz — haqiqiy Excel foizi.</b> Katakka <c>4 / 100</c> yoziladi va
/// <c>0%</c> formati qo'yiladi: ekranda «4%», hisoblashda 0,04. «4» sonini
/// «%» matni bilan yozish Excel formulalarida 100 barobar xato berardi.
/// </para>
/// <para>
/// Sarlavha qatorida avtofiltr: HR faylni ochgan zahoti tur yoki rag'bat/jarima
/// bo'yicha qayta filtrlay oladi.
/// </para>
/// </remarks>
public sealed class IncentiveExcelWriter : IIncentiveExcelWriter
{
    private const string DateFormat = "dd.mm.yyyy";

    private static readonly string[] Headers =
    [
        "Sana",
        "F.I.O.",
        "Rag'bat / jarima",
        "Turi",
        "Summa",
        "Valyuta",
        "Foiz",
        "Sabab",
        "Sovg'a",
    ];

    /// <inheritdoc />
    public byte[] Write(IReadOnlyList<IncentiveListRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Rag'bat va jarima");

        for (var column = 0; column < Headers.Length; column++)
        {
            sheet.Cell(1, column + 1).Value = Headers[column];
        }

        var headerRow = sheet.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F2F2");

        for (var i = 0; i < rows.Count; i++)
        {
            WriteRow(sheet, i + 2, rows[i]);
        }

        sheet.Range(1, 1, Math.Max(rows.Count, 1) + 1, Headers.Length).SetAutoFilter();
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();

        // Sabab (1000 belgigacha) va sovg'a (300 belgigacha) — erkin matn:
        // ustun haddan tashqari kengayib ketmasin, qator ichida o'ralsin.
        WrapLongText(sheet.Column(8), maxWidth: 60);
        WrapLongText(sheet.Column(9), maxWidth: 40);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteRow(IXLWorksheet sheet, int excelRow, IncentiveListRow row)
    {
        SetDate(sheet.Cell(excelRow, 1), row.Date);

        sheet.Cell(excelRow, 2).Value = row.EmployeeName;
        sheet.Cell(excelRow, 3).Value = row.Kind == IncentiveKind.Penalty ? "Jarima" : "Rag'bat";
        sheet.Cell(excelRow, 4).Value = row.TypeName ?? string.Empty;

        if (row.AmountValue is { } amount)
        {
            var cell = sheet.Cell(excelRow, 5);
            cell.Value = amount;
            cell.Style.NumberFormat.Format = decimal.Truncate(amount) == amount ? "#,##0" : "#,##0.00";
            sheet.Cell(excelRow, 6).Value = row.AmountCurrency ?? string.Empty;
        }

        if (row.Percent is { } percent)
        {
            var cell = sheet.Cell(excelRow, 7);
            cell.Value = percent / 100m;
            cell.Style.NumberFormat.Format = decimal.Truncate(percent) == percent ? "0%" : "0.00%";
        }

        sheet.Cell(excelRow, 8).Value = row.Reason ?? string.Empty;
        sheet.Cell(excelRow, 9).Value = row.GiftDescription ?? string.Empty;
    }

    private static void WrapLongText(IXLColumn column, double maxWidth)
    {
        if (column.Width > maxWidth)
        {
            column.Width = maxWidth;
        }

        column.Style.Alignment.WrapText = true;
    }

    private static void SetDate(IXLCell cell, DateOnly? date)
    {
        if (date is not { } value)
        {
            return;
        }

        cell.Value = value.ToDateTime(TimeOnly.MinValue);
        cell.Style.DateFormat.Format = DateFormat;
    }
}
