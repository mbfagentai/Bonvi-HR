using System.Globalization;
using Bonvi.Domain.Common;

namespace Bonvi.Domain.ValueObjects;

/// <summary>
/// Pul miqdori. Oklad, rag'bat, jarima — hammasi shu tip orqali o'tadi.
/// </summary>
/// <remarks>
/// <para>
/// <c>decimal</c> ishlatiladi, <c>double</c> emas: <c>double</c> kasr sonlarni
/// aniq saqlamaydi va oylik hisobida tiyin darajasida xato beradi.
/// </para>
/// <para>
/// Manfiy qiymat qabul qilinmaydi — jarima ham musbat son sifatida saqlanadi,
/// uning "manfiy" ekani amaliyot turidan (<c>Penalty</c>) kelib chiqadi.
/// </para>
/// </remarks>
public sealed class Money : ValueObject
{
    /// <summary>O'zbek so'mi — tizimning asosiy valyutasi.</summary>
    public const string Uzs = "UZS";

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>Miqdor. Bazada <c>numeric(14,2)</c>.</summary>
    public decimal Amount { get; }

    /// <summary>Valyuta kodi (ISO 4217).</summary>
    public string Currency { get; }

    /// <summary>Nol miqdor.</summary>
    public static Money Zero => new(0m, Uzs);

    /// <summary>Ko'rsatish uchun: <c>4 500 000 so'm</c>.</summary>
    public string Formatted => Currency == Uzs
        ? $"{Amount.ToString("#,##0", CultureInfo.InvariantCulture).Replace(',', ' ')} so'm"
        : $"{Amount.ToString("#,##0.00", CultureInfo.InvariantCulture)} {Currency}";

    public static Result<Money> Create(decimal amount, string currency = Uzs)
    {
        if (amount < 0)
        {
            return Error.Validation("pul.manfiy", "Pul miqdori manfiy bo'lishi mumkin emas.");
        }

        if (decimal.Round(amount, 2) != amount)
        {
            return Error.Validation("pul.aniqlik", "Pul miqdorida ikkitadan ortiq kasr xona bo'lishi mumkin emas.");
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
        {
            return Error.Validation("pul.valyuta", "Valyuta kodi 3 harfdan iborat bo'lishi kerak (masalan UZS).");
        }

        return new Money(amount, currency.ToUpperInvariant());
    }

    /// <summary>Tekshiruvsiz yaratadi. FAQAT bazadan o'qishda ishlatiladi.</summary>
    public static Money FromTrusted(decimal amount, string currency) => new(amount, currency);

    public Result<Money> Add(Money other) => other.Currency != Currency
        ? Error.Validation("pul.valyuta_mos_emas", "Turli valyutadagi summalarni qo'shib bo'lmaydi.")
        : Create(Amount + other.Amount, Currency);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }

    public override string ToString() => Formatted;
}
