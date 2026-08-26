using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.Reports.Abstractions;
using Bonvi.Domain.Employees;
using ClosedXML.Excel;

namespace Bonvi.Infrastructure.Reports;

/// <summary>
/// <see cref="IEmployeeExcelWriter"/> ning <c>ClosedXML</c> orqali amalga oshirilishi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Sarlavhalar tili — o'zbekcha (lotin).</b> Butun tizim shu tilda va Excel
/// faylini frontend TARJIMA qila olmaydi — u statik fayl, brauzerda emas,
/// Excel'ning o'zida ochiladi. Shu sababli matn shu yerda, qattiq yozilgan.
/// </para>
/// <para>
/// <b>Sana ustunlari — HAQIQIY Excel sana (matn emas), format <c>dd.mm.yyyy</c>.</b>
/// Bu butun tizimdagi ko'rinish qoidasi bilan bir xil (masalan bosh sahifadagi
/// davr yozuvi «25.07 – 24.08»). Matn emas, HAQIQIY sana qilib yozilishi
/// muhim: HR Excel'ning o'zida sana bo'yicha saralashi yoki filtrlashi
/// kerak bo'lsa, matn ustunda bu ishlamaydi.
/// </para>
/// <para>
/// <b>Oklad — ikkita alohida ustun (son + valyuta), formatlangan matn emas.</b>
/// Ekranda «4 500 000 so'm» degan bitta matn ko'rinadi, bu yerda esa son va
/// valyuta ajratilgan: bu MA'LUMOT jihatidan bir xil (ekranda ko'ringan aynan
/// shu summa), lekin Excel'da foydali shaklda — HR ustunni yig'indilashi
/// yoki saralashi kerak, bu esa matn ustunida ishlamaydi. Aynan shu — Excel
/// eksportining butun maqsadi (HR'ning odati: jamlash, filtrlash, o'zi
/// hisoblash).
/// </para>
/// </remarks>
public sealed class EmployeeExcelWriter : IEmployeeExcelWriter
{
    private const string DateFormat = "dd.mm.yyyy";

    private static readonly string[] Headers =
    [
        "F.I.O.",
        "Tug'ilgan sana",
        "JSHSHIR",
        "Pasport",
        "Telefon",
        "Yuridik shaxs",
        "Bo'lim",
        "Lavozim",
        "Holat",
        "Ishga kirgan sana",
        "Stavka",
        "Oylik summasi",
        "Valyuta",
        "Yo'nalishlar",
        "Belgilar",
        "Tekshirish kerak",
    ];

    /// <inheritdoc />
    public byte[] Write(IReadOnlyList<EmployeeListItemDto> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Xodimlar");

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

        // Sarlavha doim ko'rinib turadi — HR yuzlab qatorni pastga
        // aylantirganda ustun nomini qayta-qayta yuqoriga chiqarmaydi.
        sheet.SheetView.FreezeRows(1);

        // Har bir ustun mazmuniga qarab kengayadi — sukut bo'yicha tor
        // ustunlar (masalan «F.I.O.») matnni kesib qo'yardi.
        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteRow(IXLWorksheet sheet, int excelRow, EmployeeListItemDto row)
    {
        sheet.Cell(excelRow, 1).Value = row.FullName;

        SetDate(sheet.Cell(excelRow, 2), row.BirthDate);

        sheet.Cell(excelRow, 3).Value = row.Pinfl ?? string.Empty;
        sheet.Cell(excelRow, 4).Value = row.Passport ?? string.Empty;
        sheet.Cell(excelRow, 5).Value = row.Phone ?? string.Empty;
        sheet.Cell(excelRow, 6).Value = row.LegalEntity ?? string.Empty;
        sheet.Cell(excelRow, 7).Value = row.Department ?? string.Empty;
        sheet.Cell(excelRow, 8).Value = row.Position ?? string.Empty;
        sheet.Cell(excelRow, 9).Value = StatusLabel(row.Status);

        SetDate(sheet.Cell(excelRow, 10), row.HireDate);

        if (row.Rate is { } rate)
        {
            sheet.Cell(excelRow, 11).Value = rate;
        }

        if (row.Salary is { } salary)
        {
            sheet.Cell(excelRow, 12).Value = salary.Amount;
            sheet.Cell(excelRow, 13).Value = salary.Currency;
        }

        sheet.Cell(excelRow, 14).Value = string.Join(", ", row.Directions.Select(direction => direction.Name));
        sheet.Cell(excelRow, 15).Value = string.Join(", ", row.Flags.Select(FlagLabel));
        sheet.Cell(excelRow, 16).Value = row.NeedsReview ? "Ha" : "Yo'q";
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

    /// <summary>
    /// Holat — o'zbekcha matn.
    /// </summary>
    /// <remarks>
    /// Bu yerga qat'iy yozilgan: mijoz tomonidagi tarjima faylini Excel
    /// o'qiy olmaydi, u statik fayl. <c>web/</c> dagi lug'atlar bilan
    /// bog'liq emas — ular ekran uchun, bu yerdagi esa faylning o'zi uchun.
    /// </remarks>
    private static string StatusLabel(EmploymentStatus? status) => status switch
    {
        EmploymentStatus.Probation => "Sinov muddatida",
        EmploymentStatus.Active => "Doimiy",
        EmploymentStatus.Terminated => "Ishdan bo'shagan",
        _ => string.Empty,
    };

    private static string FlagLabel(EmployeeFlagType type) => type switch
    {
        EmployeeFlagType.Reserve => "Rezerv",
        EmployeeFlagType.Blacklist => "Qora ro'yxat",
        EmployeeFlagType.Monitoring => "Nazoratda",
        EmployeeFlagType.Capable => "Qobiliyatli",
        _ => type.ToString(),
    };
}
