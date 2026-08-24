using System.Globalization;
using System.Text;
using Bonvi.Domain.ValueObjects;
using Bonvi.Etl.Excel;

namespace Bonvi.Etl.Mapping;

/// <summary>«ОКЛАД» / «Белгиланган иш хақи» katagining ma'nosi.</summary>
public enum SalaryCellKind
{
    /// <summary>Katak bo'sh — oklad haqida ma'lumot yo'q.</summary>
    Empty = 0,

    /// <summary>Aniq summa.</summary>
    Amount = 1,

    /// <summary>«Ўзгаришсиз» — oklad o'zgarmadi, eskisi kuchda qoladi.</summary>
    Unchanged = 2,

    /// <summary>«Ишбай» — ishbay to'lov, belgilangan oklad yo'q.</summary>
    PieceRate = 3,

    /// <summary>Tushunarsiz qiymat (KPI izohi, lavozim nomi, ustun siljishi).</summary>
    Unknown = 4,
}

/// <summary>Katak tahlili natijasi.</summary>
/// <param name="Kind">Katak nimani anglatadi.</param>
/// <param name="Salary">Summa (faqat <see cref="SalaryCellKind.Amount"/> uchun).</param>
/// <param name="Raw">Xom qiymat — hisobotda ko'rsatish uchun.</param>
public readonly record struct SalaryCellResult(SalaryCellKind Kind, Money? Salary, string Raw);

/// <summary>
/// Oklad ustunini o'qiydi.
/// </summary>
/// <remarks>
/// <para>
/// Excel'da bu ustunda to'rt xil narsa yotibdi: son (<c>4500000</c>),
/// bo'shliq bilan yozilgan son (<c>«3 000 000»</c>), holat so'zi
/// (<c>«Ўзгаришсиз»</c>, <c>«Ишбай»</c>) va izoh
/// (<c>«4000000+КПИ»</c>, <c>«KPI бўйича»</c>).
/// </para>
/// <para>
/// <b>Aralash qiymat ATAYLAB o'qilmaydi.</b> <c>«4000000+КПИ»</c> dan
/// 4 000 000 ni olib qo'yish mumkin edi, lekin unda KPI qismi jimgina
/// yo'qolardi va bazada «okladi 4 mln» degan noto'liq fakt qolardi.
/// Bunday katak <see cref="SalaryCellKind.Unknown"/> bo'ladi, hisobotga
/// tushadi va HR uni o'zi kiritadi — butun faylda ular 20 tacha.
/// </para>
/// <para>
/// <b>«Ишбай» — bo'sh emas.</b> Ishbay to'lovda belgilangan oklad
/// yo'q, ya'ni bo'sh maydon bu yerda to'g'ri javob. Uni <c>Unknown</c>
/// dan ajratish kerak: <c>Unknown</c> — xato, <c>PieceRate</c> — fakt,
/// va u hisobotni shovqinga to'ldirmasligi lozim.
/// </para>
/// </remarks>
public static class SalaryCell
{
    /// <summary>«Ўзгаришсиз» so'zining Excel'da uchraydigan yozilishlari.</summary>
    private static readonly string[] UnchangedForms = ["узгаришсиз", "узгармади", "узгариш йук"];

    /// <summary>«Ишбай» — ish hajmiga qarab to'lanadi.</summary>
    private static readonly string[] PieceRateForms = ["ишбай", "иш бай"];

    /// <summary>
    /// Ma'no tashimaydigan qiymatlar — bo'sh katak bilan bir xil.
    /// </summary>
    /// <remarks>
    /// <c>«Оклад»</c> — sarlavhaning ma'lumot qatoriga tushib qolgani (uchala
    /// hodisa varag'ida jami 87 marta), <c>«-»</c> esa HR ning «yozadigan
    /// narsa yo'q» belgisi. Ularni <see cref="SalaryCellKind.Unknown"/> deb
    /// belgilash 100 dan ortiq foydasiz hisobot satri berardi va HR ning ish
    /// ro'yxatini shovqinga to'ldirardi.
    /// </remarks>
    private static readonly string[] MeaninglessForms = ["-", "—", "–", "оклад", "иш хаки", "иш хақи"];

    /// <summary>Katakni tahlil qiladi.</summary>
    public static SalaryCellResult Parse(ExcelCell cell)
    {
        if (cell.IsEmpty)
        {
            return new SalaryCellResult(SalaryCellKind.Empty, null, string.Empty);
        }

        var raw = cell.Text;

        if (cell.Number is { } number)
        {
            return Amount((decimal)number, raw);
        }

        var key = Fold(raw).TrimEnd('.', ',', ';', ':').Trim();

        if (Array.Exists(MeaninglessForms, form => string.Equals(key, form, StringComparison.Ordinal)))
        {
            return new SalaryCellResult(SalaryCellKind.Empty, null, raw);
        }

        // Solishtirish AYNIQSA to'liq: «Ишбай» — ishbay to'lov, lekin
        // «Ишбай+1000000» — ishbay USTIGA belgilangan qo'shimcha. Ichki
        // qidiruv ikkinchisini birinchisiga aylantirib, qo'shimchani jimgina
        // yo'qotardi. Bunday katak tushunarsiz deb belgilanadi va hisobotga
        // tushadi — butun faylda ular 20 tacha.
        if (Array.Exists(UnchangedForms, form => string.Equals(key, form, StringComparison.Ordinal)))
        {
            return new SalaryCellResult(SalaryCellKind.Unchanged, null, raw);
        }

        if (Array.Exists(PieceRateForms, form => string.Equals(key, form, StringComparison.Ordinal)))
        {
            return new SalaryCellResult(SalaryCellKind.PieceRate, null, raw);
        }

        // «3 000 000» — bo'shliq bilan yozilgan son. Bo'shliqlar olib
        // tashlangandan keyin BUTUNLAY raqam qolishi shart: «4000000+КПИ»
        // bu tekshiruvdan o'tmaydi va tushunarsiz deb belgilanadi.
        if (Digits(raw) is { Length: > 0 } digits
            && decimal.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
        {
            return Amount(parsed, raw);
        }

        return new SalaryCellResult(SalaryCellKind.Unknown, null, raw);
    }

    /// <summary>Summani <see cref="Money"/> ga aylantiradi.</summary>
    /// <remarks>
    /// Manfiy yoki haddan tashqari katta qiymat <c>Money.Create</c> dan
    /// o'tmaydi — bunday katak ham tushunarsiz deb belgilanadi.
    /// </remarks>
    private static SalaryCellResult Amount(decimal value, string raw)
    {
        var money = Money.Create(value);

        return money.IsSuccess
            ? new SalaryCellResult(SalaryCellKind.Amount, money.Value, raw)
            : new SalaryCellResult(SalaryCellKind.Unknown, null, raw);
    }

    /// <summary>
    /// Faqat raqamlardan iborat satr qaytaradi — ajratgichlar olib tashlanadi.
    /// </summary>
    /// <remarks>
    /// Raqam bo'lmagan belgi uchrasa <c>null</c>: «4000000+КПИ» qisman
    /// o'qilmasligi kerak. Oxiridagi nuqta («3000000.») ataylab kechiriladi —
    /// bu HR qo'ygan tinish belgisi, qiymatni o'zgartirmaydi.
    /// </remarks>
    private static string? Digits(string raw)
    {
        var trimmed = raw.TrimEnd('.', ',');
        var builder = new StringBuilder(trimmed.Length);

        foreach (var ch in trimmed)
        {
            if (char.IsDigit(ch))
            {
                _ = builder.Append(ch);
                continue;
            }

            // Minglar ajratgichi: bo'shliq (oddiy va uzilmaydigan), apostrof.
            if (char.IsWhiteSpace(ch) || ch is '\'' or '`')
            {
                continue;
            }

            return null;
        }

        return builder.ToString();
    }

    /// <summary>Kirill imlo variantlarini bir shaklga keltiradi.</summary>
    private static string Fold(string value)
        => AliasTable.Key(value)
            .Replace('ў', 'у')
            .Replace('қ', 'к')
            .Replace('ғ', 'г')
            .Replace('ҳ', 'х');
}
