using System.Globalization;
using System.Text;
using Bonvi.Application.Attendance.Abstractions;
using ClosedXML.Excel;

namespace Bonvi.Infrastructure.Reports;

/// <summary>
/// <see cref="IAttendanceImportReader"/> — <c>.xlsx</c> ClosedXML bilan, <c>.csv</c> o'zimiz.
/// </summary>
/// <remarks>
/// <para>
/// <b>Holatsiz</b> — bitta nusxa yetarli (<c>EmployeeExcelWriter</c> kabi).
/// </para>
/// <para>
/// <b>Buzuq fayl — <c>null</c>, istisno emas.</b> «.xlsx deb nomlangan PDF»
/// foydalanuvchi xatosi, dastur xatosi emas: u 500 emas, «fayl turi noto'g'ri»
/// bo'lib qaytishi kerak.
/// </para>
/// </remarks>
internal sealed class AttendanceImportReader : IAttendanceImportReader
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ImportSheetRow>?> ReadAsync(
        Stream content,
        ImportFileFormat format,
        int maxRows,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxRows);

        // ClosedXML izlanadigan (seekable) oqim talab qiladi; forma fayli esa
        // har doim ham shunday emas. Hajm 10 MB bilan cheklangan — xotiraga olish arzon.
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        buffer.Position = 0;

        return format switch
        {
            ImportFileFormat.Xlsx => ReadXlsx(buffer, maxRows),
            ImportFileFormat.Csv => ReadCsv(buffer, maxRows),
            _ => null,
        };
    }

    /// <summary>Birinchi varaqning ishlatilgan qatorlari.</summary>
    /// <remarks>
    /// ClosedXML buzuq arxivda turli istisnolar otadi (<c>InvalidDataException</c>,
    /// <c>FileFormatException</c>, <c>XmlException</c> …) va ularning ro'yxati
    /// versiyadan versiyaga o'zgaradi — shu sababli hammasi bitta joyda «o'qilmadi»
    /// bo'ladi.
    /// </remarks>
    private static List<ImportSheetRow>? ReadXlsx(Stream buffer, int maxRows)
    {
        try
        {
            using var workbook = new XLWorkbook(buffer);
            var sheet = workbook.Worksheets.FirstOrDefault();

            if (sheet is null)
            {
                return [];
            }

            var rows = new List<ImportSheetRow>();

            foreach (var row in sheet.RowsUsed())
            {
                var cells = new List<string>();

                foreach (var cell in row.CellsUsed())
                {
                    var column = cell.Address.ColumnNumber - 1;

                    while (cells.Count < column)
                    {
                        cells.Add(string.Empty);
                    }

                    cells.Add(Text(cell));
                }

                if (cells.TrueForAll(string.IsNullOrWhiteSpace))
                {
                    continue;
                }

                rows.Add(new ImportSheetRow(row.RowNumber(), cells));

                if (rows.Count >= maxRows)
                {
                    break;
                }
            }

            return rows;
        }
#pragma warning disable CA1031 // Buzuq fayl — foydalanuvchi xatosi; sabab metod izohida.
        catch (Exception)
#pragma warning restore CA1031
        {
            return null;
        }
    }

    /// <summary>Katak qiymati MATN bo'lib — formatlashsiz.</summary>
    /// <remarks>
    /// Butun son «0» formatida: 10427 → «10427», 14 xonali JSHSHIR → 14 raqam
    /// (<c>double</c> 2^53 gacha butun sonni aniq saqlaydi). Formatlangan satr
    /// («3,12346E+13») ishlatilmaydi.
    /// </remarks>
    private static string Text(IXLCell cell)
    {
        try
        {
            var value = cell.Value;

            if (value.IsBlank || value.IsError)
            {
                return string.Empty;
            }

            if (value.IsNumber)
            {
                var number = value.GetNumber();

                return number == Math.Floor(number) && Math.Abs(number) < 1e15
                    ? number.ToString("0", CultureInfo.InvariantCulture)
                    : number.ToString("R", CultureInfo.InvariantCulture);
            }

            if (value.IsText)
            {
                return value.GetText();
            }

            if (value.IsDateTime)
            {
                return value.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            return value.ToString(CultureInfo.InvariantCulture);
        }
#pragma warning disable CA1031 // Bitta g'alati katak butun faylni yiqitmasin (ETL dagi `ExcelWorkbook` bilan bir xil).
        catch (Exception)
#pragma warning restore CA1031
        {
            return string.Empty;
        }
    }

    /// <summary>CSV — RFC 4180 qo'shtirnoqlari bilan, ajratgich avtomatik.</summary>
    /// <remarks>
    /// <para>
    /// <b>Ajratgich.</b> Rus/o'zbek Excel CSV ni nuqta-vergul bilan saqlaydi
    /// (o'nlik vergul band), boshqalari vergul bilan. Birinchi bo'sh bo'lmagan
    /// qatorda qo'shtirnoqdan tashqarida qaysi belgi (<c>;</c>, <c>,</c>, tab)
    /// ko'p bo'lsa, o'sha olinadi.
    /// </para>
    /// <para>
    /// <b>Kodlash.</b> UTF-8 (BOM bo'lsa, u bo'yicha). Noto'g'ri UTF-8 bayt
    /// (masalan Windows-1251 fayl) — istisno emas, <c>null</c>: kirill harflari
    /// «�» bo'lib ketgan ismlar bilan moslash noto'g'ri takliflar berardi.
    /// </para>
    /// </remarks>
    private static List<ImportSheetRow>? ReadCsv(Stream buffer, int maxRows)
    {
        string text;

        try
        {
            using var reader = new StreamReader(
                buffer,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                detectEncodingFromByteOrderMarks: true);

            text = reader.ReadToEnd();
        }
        catch (DecoderFallbackException)
        {
            return null;
        }

        var delimiter = DetectDelimiter(text);
        var rows = new List<ImportSheetRow>();
        var cells = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var line = 1;
        var recordLine = 1;

        void EndField()
        {
            cells.Add(field.ToString());
            field.Clear();
        }

        bool EndRecord()
        {
            EndField();

            if (!cells.TrueForAll(string.IsNullOrWhiteSpace))
            {
                rows.Add(new ImportSheetRow(recordLine, [.. cells]));
            }

            cells.Clear();
            return rows.Count >= maxRows;
        }

        for (var i = 0; i < text.Length; i++)
        {
            var symbol = text[i];

            if (inQuotes)
            {
                if (symbol == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    if (symbol == '\n')
                    {
                        line++;
                    }

                    field.Append(symbol);
                }

                continue;
            }

            switch (symbol)
            {
                case '"' when field.Length == 0:
                    inQuotes = true;
                    break;

                case '\r':
                    break;

                case '\n':
                    if (EndRecord())
                    {
                        return rows;
                    }

                    line++;
                    recordLine = line;
                    break;

                default:
                    if (symbol == delimiter)
                    {
                        EndField();
                    }
                    else
                    {
                        field.Append(symbol);
                    }

                    break;
            }
        }

        if (field.Length > 0 || cells.Count > 0)
        {
            EndRecord();
        }

        return rows;
    }

    private static char DetectDelimiter(string text)
    {
        var semicolons = 0;
        var commas = 0;
        var tabs = 0;
        var inQuotes = false;
        var seenContent = false;

        foreach (var symbol in text)
        {
            if (symbol == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (inQuotes)
            {
                continue;
            }

            if (symbol == '\n')
            {
                if (seenContent)
                {
                    break;
                }

                continue;
            }

            switch (symbol)
            {
                case ';':
                    semicolons++;
                    break;
                case ',':
                    commas++;
                    break;
                case '\t':
                    tabs++;
                    break;
                default:
                    break;
            }

            seenContent |= !char.IsWhiteSpace(symbol);
        }

        if (semicolons >= commas && semicolons >= tabs && semicolons > 0)
        {
            return ';';
        }

        return tabs > commas ? '\t' : ',';
    }
}
