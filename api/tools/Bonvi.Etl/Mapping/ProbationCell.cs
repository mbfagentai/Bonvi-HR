using Bonvi.Etl.Excel;

namespace Bonvi.Etl.Mapping;

/// <summary>«СИНОВ МУДДАТИ» katagining ma'nosi.</summary>
public enum ProbationCellKind
{
    /// <summary>Katak bo'sh — sinov muddati haqida ma'lumot yo'q.</summary>
    Empty = 0,

    /// <summary>«Тугатилган» — sinov muddati muvaffaqiyatli tugagan.</summary>
    Completed = 1,

    /// <summary>Sana — sinov muddati shu kunda tugaydi.</summary>
    EndDate = 2,

    /// <summary>Tushunarsiz qiymat (odatda ustun siljishi).</summary>
    Unknown = 3,
}

/// <summary>Katak tahlili natijasi.</summary>
/// <param name="Kind">Katak nimani anglatadi.</param>
/// <param name="EndDate">Tugash sanasi (faqat <see cref="ProbationCellKind.EndDate"/> uchun).</param>
/// <param name="Raw">Xom qiymat — hisobotda ko'rsatish uchun.</param>
public readonly record struct ProbationCellResult(ProbationCellKind Kind, DateOnly? EndDate, string Raw);

/// <summary>
/// «СИНОВ МУДДАТИ» ustunini o'qiydi.
/// </summary>
/// <remarks>
/// <para>
/// Bu ustunda ikki xil narsa yotibdi: 411 katakda «Тугатилган» so'zi
/// (muddat allaqachon tugagan), qolganida esa muddat tugash sanasi
/// (Excel seriyasi).
/// </para>
/// <para>
/// <b>Arxiv varag'ida ustun siljib ketgan</b>: 400 dan ortiq katakda
/// lavozim nomi («Юк ташувчи», «Краска ходими») yozilgan. Bunday qiymat
/// <see cref="ProbationCellKind.Unknown"/> bo'ladi va <b>ishlatilmaydi</b> —
/// noto'g'ri ustundan olingan qiymatni sinov muddati deb qo'yish
/// bo'sh maydondan yomonroq bo'lardi.
/// </para>
/// </remarks>
public static class ProbationCell
{
    /// <summary>
    /// «Тугатилган» so'zining Excel'da uchraydigan yozilishlari.
    /// </summary>
    /// <remarks>
    /// Kirill imlosida <c>ў/у</c>, <c>қ/к</c>, <c>ғ/г</c> almashib yoziladi.
    /// Solishtirishdan oldin ular bir shaklga keltiriladi, shu sababli
    /// ro'yxatda ham «tozalangan» ko'rinish turadi.
    /// </remarks>
    private static readonly string[] CompletedForms = ["тугатилган", "тугатилди", "тугаган", "утган", "утди"];

    /// <summary>Katakni tahlil qiladi.</summary>
    public static ProbationCellResult Parse(ExcelCell cell)
    {
        if (cell.IsEmpty)
        {
            return new ProbationCellResult(ProbationCellKind.Empty, null, string.Empty);
        }

        var raw = cell.Text;

        // Sana avval sinaladi: son katakda «Тугатилган» bo'lishi mumkin emas.
        if (ExcelDate.Read(cell) is { } date)
        {
            return new ProbationCellResult(ProbationCellKind.EndDate, date, raw);
        }

        var key = Fold(raw);

        foreach (var form in CompletedForms)
        {
            if (key.Contains(form, StringComparison.Ordinal))
            {
                return new ProbationCellResult(ProbationCellKind.Completed, null, raw);
            }
        }

        return new ProbationCellResult(ProbationCellKind.Unknown, null, raw);
    }

    /// <summary>
    /// Kirill imlo variantlarini bir shaklga keltiradi.
    /// </summary>
    /// <remarks>
    /// «Тўгатилган», «Тугатилган», «Тўғатилган» — bir xil so'zning uch
    /// yozilishi. Excel'da uchalasi ham uchraydi.
    /// </remarks>
    private static string Fold(string value)
    {
        var lowered = AliasTable.Key(value);
        return lowered
            .Replace('ў', 'у')
            .Replace('қ', 'к')
            .Replace('ғ', 'г')
            .Replace('ҳ', 'х');
    }
}
