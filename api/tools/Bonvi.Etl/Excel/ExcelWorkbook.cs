using System.Globalization;
using ClosedXML.Excel;

namespace Bonvi.Etl.Excel;

/// <summary>Varaqning bitta qatori: ustun raqami → katak.</summary>
/// <remarks>
/// Ustunlar 1 dan boshlab raqamlanadi (A = 1) — Excel'dagidek. Ko'chirish
/// qoidalarida ustunlar aynan shu raqam bilan yozilgan, chunki HR bloknotida
/// sarlavhalar takrorlanadi va nomga tayanib bo'lmaydi.
/// </remarks>
public sealed class ExcelRow
{
    private readonly Dictionary<int, ExcelCell> _cells;

    internal ExcelRow(int number, Dictionary<int, ExcelCell> cells)
    {
        Number = number;
        _cells = cells;
    }

    /// <summary>Excel'dagi qator raqami (1 dan).</summary>
    public int Number { get; }

    /// <summary>Qatorda umuman ma'lumot bormi.</summary>
    public bool IsEmpty => _cells.Count == 0;

    /// <summary>Ustun qiymati. Katak bo'lmasa — <see cref="ExcelCell.Empty"/>.</summary>
    public ExcelCell this[int column]
        => _cells.TryGetValue(column, out var cell) ? cell : ExcelCell.Empty;

    /// <summary>Ustundagi tozalangan matn (bo'lmasa — bo'sh satr).</summary>
    public string Text(int column) => this[column].Text;

    /// <summary>Ustunda qiymat bormi.</summary>
    public bool Has(int column) => this[column].HasValue;
}

/// <summary>
/// Excel faylini o'qiydi va uni <see cref="ExcelRow"/> ro'yxatiga aylantiradi.
/// </summary>
/// <remarks>
/// <para>
/// Butun varaq bir yo'la xotiraga o'qiladi. Eng katta varaq — 3000 qator,
/// bu bir necha megabayt; oqim bilan o'qish murakkabligi bunga arzimaydi.
/// </para>
/// <para>
/// Buning evaziga ko'chirish qadamlari qatorlar bo'ylab bir necha marta
/// yura oladi (masalan blok sarlavhasini topish uchun oldinga qarash).
/// </para>
/// </remarks>
public sealed class ExcelWorkbook : IDisposable
{
    private readonly XLWorkbook _workbook;
    private readonly Dictionary<string, IReadOnlyList<ExcelRow>> _cache = new(StringComparer.Ordinal);
    private readonly List<string> _unreadable = [];

    public ExcelWorkbook(string path)
    {
        Path = path;
        _workbook = new XLWorkbook(path);
    }

    /// <summary>Fayl yo'li — hisobotda ko'rsatiladi.</summary>
    public string Path { get; }

    /// <summary>Fayldagi barcha varaq nomlari.</summary>
    public IReadOnlyList<string> SheetNames
        => [.. _workbook.Worksheets.Select(sheet => sheet.Name)];

    /// <summary>
    /// O'qib bo'lmagan kataklar manzillari.
    /// </summary>
    /// <remarks>
    /// Bunday katak <b>bo'sh deb qaralmaydi, u yo'qotilgan ma'lumot</b> —
    /// shu sababli manzillar saqlanadi va ko'chirish oxirida hisobotga
    /// chiqariladi. Aks holda bir necha katak jimgina yo'qolardi.
    /// </remarks>
    public IReadOnlyList<string> UnreadableCells => _unreadable;

    /// <summary>Varaq mavjudmi.</summary>
    public bool HasSheet(string name) => _workbook.Worksheets.Contains(name);

    /// <summary>
    /// Varaq qatorlarini qaytaradi. Varaq topilmasa — bo'sh ro'yxat
    /// (ko'chirish to'xtamaydi, hisobotga yoziladi).
    /// </summary>
    public IReadOnlyList<ExcelRow> Rows(string sheetName)
    {
        if (_cache.TryGetValue(sheetName, out var cached))
        {
            return cached;
        }

        if (!_workbook.Worksheets.TryGetWorksheet(sheetName, out var sheet))
        {
            _cache[sheetName] = [];
            return [];
        }

        var rows = new List<ExcelRow>();

        foreach (var row in sheet.RowsUsed())
        {
            var cells = new Dictionary<int, ExcelCell>();

            foreach (var cell in row.CellsUsed())
            {
                var value = Convert(cell, sheetName);
                if (value.HasValue)
                {
                    cells[cell.Address.ColumnNumber] = value;
                }
            }

            rows.Add(new ExcelRow(row.RowNumber(), cells));
        }

        _cache[sheetName] = rows;
        return rows;
    }

    /// <summary>
    /// ClosedXML katagini ko'chirishga tushunarli ko'rinishga o'tkazadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Katakni o'qishning o'zi yiqilishi mumkin.</b> Bloknotda sana
    /// formatiga bo'yalgan, lekin ichida haqiqiy sana bermaydigan son
    /// yotgan kataklar bor (masalan manfiy qiymat). ClosedXML bunday
    /// katakni o'qiyotganda <c>«Not a legal OleAut date»</c> deb yiqiladi
    /// va butun ko'chirish to'xtardi — bitta buzuq katak tufayli 11 000
    /// yozuv ko'chmay qolardi.
    /// </para>
    /// <para>
    /// Shu sababli xato tutiladi va katak xom matn sifatida o'qishga
    /// urinib ko'riladi. U ham bermasa — katak bo'sh deb qaraladi, lekin
    /// manzili <see cref="UnreadableCells"/> ga tushadi va hisobotda
    /// ko'rinadi.
    /// </para>
    /// </remarks>
    private ExcelCell Convert(IXLCell cell, string sheetName)
    {
        try
        {
            return Read(cell);
        }
        catch (Exception)
        {
            // Butun o'qish qamrab olinadi, faqat `cell.Value` emas: ClosedXML
            // katakni «sana» deb belgilaydi, lekin yiqilish keyinroq —
            // `GetDateTime()` da sodir bo'ladi.
            return Rescue(cell, sheetName);
        }
    }

    /// <summary>Katak qiymatini turi bo'yicha o'qiydi.</summary>
    private static ExcelCell Read(IXLCell cell)
    {
        var value = cell.Value;

        if (value.IsBlank)
        {
            return ExcelCell.Empty;
        }

        if (value.IsDateTime)
        {
            return ExcelCell.FromDate(DateOnly.FromDateTime(value.GetDateTime()));
        }

        if (value.IsNumber)
        {
            return ExcelCell.FromNumber(value.GetNumber());
        }

        if (value.IsBoolean)
        {
            return ExcelCell.FromText(value.GetBoolean() ? "true" : "false");
        }

        // Formula xatosi (#REF!, #N/A) — bu ma'lumot emas, bo'sh deb qaraladi.
        return value.IsError
            ? ExcelCell.Empty
            : ExcelCell.FromText(value.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// O'qilmagan katakdan qiymatni qutqaradi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bunday kataklar — <b>JSHSHIR va telefon raqamlari</b> (arxivda 104 ta,
    /// «91.6» da 82 ta, jami 223 ta). Ular Excel'da son bo'lib yotibdi, lekin
    /// katakka tasodifan <c>dd.mm.yyyy</c> formati berilgan. ClosedXML shu
    /// formatga qarab katakni «sana» deb belgilaydi va
    /// <c>32612914200075</c> ni sanaga aylantirmoqchi bo'lib yiqiladi.
    /// </para>
    /// <para>
    /// <b>Ma'lumot esa joyida</b> — buzilgani faqat ko'rinish.
    /// <c>TryGetValue&lt;double&gt;</c> xom sonni aylantirishsiz qaytaradi va
    /// JSHSHIR butunligicha qutqariladi. Formatni tozalash yordam bermaydi:
    /// katak turi fayl yuklanganda qadalgan.
    /// </para>
    /// <para>
    /// <b>Nega <c>GetFormattedString()</c> emas.</b> U bu katakda
    /// <c>«31.12.1899»</c> qaytaradi — Excel buzuq sanani shunday
    /// ko'rsatadi. Ya'ni JSHSHIR o'rniga soxta sana yozilardi, va bu
    /// bo'sh maydondan ancha yomonroq bo'lardi.
    /// </para>
    /// </remarks>
    private ExcelCell Rescue(IXLCell cell, string sheetName)
    {
        if (cell.TryGetValue<double>(out var number))
        {
            return ExcelCell.FromNumber(number);
        }

        if (cell.TryGetValue<string>(out var text) && text is { Length: > 0 })
        {
            return ExcelCell.FromText(text);
        }

        _unreadable.Add($"{sheetName}!{cell.Address.ToStringRelative()}");
        return ExcelCell.Empty;
    }

    public void Dispose() => _workbook.Dispose();
}
