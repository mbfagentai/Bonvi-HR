using Bonvi.Domain.ValueObjects;
using Bonvi.Etl.Excel;

namespace Bonvi.Etl.Mapping;

/// <summary>Telefon katagini ajratish natijasi.</summary>
/// <param name="Primary">Asosiy raqam (birinchi to'g'ri raqam).</param>
/// <param name="Second">Ikkinchi raqam (bo'lsa).</param>
/// <param name="Extra">Uchinchi va undan keyingi raqamlar — modelda joy yo'q.</param>
/// <param name="Rejected">Raqamga aylanmagan bo'laklar — hisobotga tushadi.</param>
public readonly record struct PhoneCellResult(
    PhoneNumber? Primary,
    PhoneNumber? Second,
    IReadOnlyList<string> Extra,
    IReadOnlyList<string> Rejected)
{
    /// <summary>Bo'sh natija.</summary>
    public static readonly PhoneCellResult None = new(null, null, [], []);
}

/// <summary>
/// Telefon katagini bir yoki bir nechta raqamga ajratadi.
/// </summary>
/// <remarks>
/// <para>
/// Excel'da bitta katakda ikki, ba'zan to'rt raqam yotibdi va ajratgichlar
/// har xil: <c>/</c>, <c>\</c>, vergul, ketma-ket bo'shliqlar yoki umuman
/// ajratgichsiz (<c>947026789905083221</c>).
/// </para>
/// <para>
/// <b>Nega ikki bosqichli ajratish.</b> Domendagi
/// <see cref="PhoneNumber.SplitMultiple"/> avval ajratgichni sinaydi, keyin
/// uzunlik bo'yicha bo'ladi. Lekin haqiqiy ma'lumotda ikkalasi ARALASH
/// bo'lishi mumkin: <c>«935599355/944491527/941341341\948152998»</c> —
/// ajratgich bo'yicha uchga bo'linadi, uchinchisi esa ichida yana ikkita
/// raqam saqlaydi. Shu sababli har bir bo'lak qayta ajratiladi.
/// </para>
/// <para>
/// <b>Taxmin qilinmaydi.</b> Bo'lak to'g'ri raqamga aylanmasa, u tashlab
/// yuborilmaydi — <see cref="PhoneCellResult.Rejected"/> ga tushadi va
/// hisobotda ko'rinadi. Aks holda «Тели йўқ» kabi yozuvlar jimgina
/// yo'qolardi.
/// </para>
/// </remarks>
public static class PhoneCell
{
    /// <summary>Bir bo'lakni qayta ajratishga urinishlar chegarasi.</summary>
    private const int MaxDepth = 2;

    /// <summary>Katakni raqamlarga ajratadi.</summary>
    public static PhoneCellResult Parse(ExcelCell cell)
    {
        if (cell.IsEmpty)
        {
            return PhoneCellResult.None;
        }

        var accepted = new List<PhoneNumber>();
        var rejected = new List<string>();

        Collect(cell.Text, depth: 0, accepted, rejected);

        return new PhoneCellResult(
            accepted.Count > 0 ? accepted[0] : null,
            accepted.Count > 1 ? accepted[1] : null,
            accepted.Count > 2 ? [.. accepted.Skip(2).Select(phone => phone.Value)] : [],
            rejected);
    }

    private static void Collect(string input, int depth, List<PhoneNumber> accepted, List<string> rejected)
    {
        var parts = PhoneNumber.SplitMultiple(input);

        // Ajratish hech narsa bermadi (bitta bo'lak, o'zgarishsiz) — bu yakuniy qiymat.
        var isTerminal = parts.Count == 1 && string.Equals(parts[0], input, StringComparison.Ordinal);

        foreach (var part in parts)
        {
            var phone = PhoneNumber.Create(part);

            if (phone.IsSuccess)
            {
                // Bir xil raqam ikki marta yozilgan bo'lishi mumkin.
                if (!accepted.Contains(phone.Value))
                {
                    accepted.Add(phone.Value);
                }

                continue;
            }

            if (isTerminal || depth >= MaxDepth)
            {
                rejected.Add(part);
                continue;
            }

            Collect(part, depth + 1, accepted, rejected);
        }
    }
}
