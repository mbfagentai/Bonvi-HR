using Bonvi.Domain.Common;

namespace Bonvi.Domain.Organization;

/// <summary>
/// Yo'nalish — biznesning yirik tarmog'i: Velo, Metan, Shina, Logistika…
/// </summary>
/// <remarks>
/// <para>
/// Excel'da bu ustunda 26 xil qiymat bor edi, lekin ularning ko'pi
/// <b>kombinatsiya</b>: «Вело, метан, шина, логистика». Ya'ni bitta
/// xodim bir nechta yo'nalishga tegishli bo'lishi mumkin.
/// </para>
/// <para>
/// Shu sababli bu yerda faqat <b>alohida</b> yo'nalishlar saqlanadi
/// (taxminan 8 ta), xodim bilan bog'lanish esa ko'p-ga-ko'p.
/// </para>
/// </remarks>
public sealed class Direction : ReferenceEntity
{
    private Direction(Guid id, string name, string? code) : base(id, name, code) { }

    private Direction() { }

    /// <summary>Interfeysda ajratib ko'rsatish uchun rang (masalan <c>#34C759</c>).</summary>
    public string? Color { get; private set; }

    public static Result<Direction> Create(string? name, string? code = null)
    {
        var normalized = Normalize(name);
        if (normalized is null)
        {
            return Error.Validation("yonalish.nom_bosh", "Yo'nalish nomi kiritilishi shart.");
        }

        return new Direction(NewId(), normalized, code);
    }

    public Result SetColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            Color = null;
            return Result.Success();
        }

        var value = color.Trim();
        var valid = value.Length == 7
            && value[0] == '#'
            && value[1..].All(char.IsAsciiHexDigit);

        if (!valid)
        {
            return Error.Validation("yonalish.rang", "Rang «#RRGGBB» ko'rinishida bo'lishi kerak.");
        }

        Color = value.ToUpperInvariant();
        return Result.Success();
    }
}
