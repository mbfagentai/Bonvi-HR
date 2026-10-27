using Bonvi.Application.Reports.Queries;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Reports.Export;

/// <summary>Excel katagining turi — qiymat qanday yozilishi va formatlanishi.</summary>
public enum ExcelCellKind
{
    /// <summary>Oddiy matn. <see cref="bool"/> qiymat «Ha» / «Yo'q» bo'lib yoziladi.</summary>
    Text,

    /// <summary>Uzun erkin matn (izoh, sabab) — ustun 60 belgidan kengaymaydi, qator ichida o'raladi.</summary>
    LongText,

    /// <summary>Haqiqiy Excel sanasi (<c>dd.mm.yyyy</c>); qiymat — <see cref="DateOnly"/>.</summary>
    Date,

    /// <summary>Sana va vaqt, TOSHKENT vaqtida (<c>dd.mm.yyyy hh:mm</c>); qiymat — <see cref="DateTimeOffset"/>.</summary>
    DateTime,

    /// <summary>Butun son (<c>#,##0</c>).</summary>
    WholeNumber,

    /// <summary>Kasrli son (<c>#,##0.##</c>).</summary>
    FractionalNumber,
}

/// <summary>Jadval ustuni: sarlavha, qatordan qiymat olish va katak turi.</summary>
/// <param name="Header">Sarlavha — o'zbekcha (lotin), ekrandagi ustun nomi bilan bir xil.</param>
/// <param name="Value">Qatordan qiymat; <see langword="null"/> — bo'sh katak.</param>
/// <param name="Kind">Katak turi.</param>
public sealed record ExcelColumn<TRow>(string Header, Func<TRow, object?> Value, ExcelCellKind Kind = ExcelCellKind.Text);

/// <summary>Yuklab olinadigan Excel fayl.</summary>
/// <param name="Content"><c>.xlsx</c> faylining baytlari.</param>
/// <param name="FileName">Fayl nomi: <c>Bonvi_&lt;nom&gt;_&lt;sana&gt;.xlsx</c>.</param>
public sealed record ExcelExportFile(byte[] Content, string FileName);

/// <summary>
/// Istalgan ro'yxatni bitta varaqli Excel jadvalga yozadigan port
/// (<c>SPEC-excel-eksport.md</c>).
/// </summary>
/// <remarks>
/// Xodimlar va rag'bat eksportlarining (<c>EmployeeExcelWriter</c>,
/// <c>IncentiveExcelWriter</c>) qoidalari bitta joyda: qalin sarlavha, kulrang
/// fon, qotirilgan birinchi qator, avtofiltr, haqiqiy Excel sana va sonlari.
/// Ustunlar ro'yxati use-case'da e'lon qilinadi — <c>ClosedXML</c> esa
/// Infrastructure tafsiloti bo'lib qoladi.
/// </remarks>
public interface IExcelTableWriter
{
    /// <summary>Qatorlarni bitta <c>.xlsx</c> faylining baytlariga aylantiradi.</summary>
    byte[] Write<TRow>(string sheetName, IReadOnlyList<ExcelColumn<TRow>> columns, IReadOnlyList<TRow> rows);
}

/// <summary>Barcha ro'yxat eksportlari uchun umumiy qoidalar.</summary>
public static class ExcelExport
{
    /// <summary>Bitta eksportdagi eng ko'p qator — xodimlar eksporti bilan bir xil chegara.</summary>
    public const int MaxRows = ExportEmployeesQueryHandler.MaxExportRows;

    /// <summary>
    /// Chegaradan oshgan natija: fayl jimgina qirqilmaydi, HR filtrni toraytiradi.
    /// Xabar xodimlar eksportiniki bilan bir xil.
    /// </summary>
    public static Error TooMany(int total) => Error.Validation(
        "eksport.juda_kop",
        $"Tanlangan filtrga {total} yozuv mos keladi — eksport chegarasi " +
        $"{MaxRows}. Filtrni toraytirib (masalan sana yoki bo'lim bo'yicha) qayta urinib ko'ring.");

    /// <summary>Fayl nomi: <c>Bonvi_nomzodlar_2026-10-22.xlsx</c>.</summary>
    public static string FileName(string slug, DateOnly today) => $"Bonvi_{slug}_{today:yyyy-MM-dd}.xlsx";
}
