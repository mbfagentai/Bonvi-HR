namespace Bonvi.Domain.Common;

/// <summary>
/// Qiymat obyekti — identifikatori yo'q, faqat qiymati muhim.
/// Tenglik barcha tarkibiy qismlar bo'yicha aniqlanadi.
/// </summary>
/// <remarks>
/// Nega kerak: <c>string pinfl</c> o'rniga <c>Pinfl pinfl</c> ishlatilganda
/// noto'g'ri qiymat obyekt yaratilish paytida rad etiladi —
/// bazaga yetib bormaydi va hech qachon "jimgina" xatoga aylanmaydi.
/// </remarks>
public abstract class ValueObject : IEquatable<ValueObject>
{
    /// <summary>Tenglikda ishtirok etadigan tarkibiy qismlar.</summary>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public bool Equals(ValueObject? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;
        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override bool Equals(object? obj) => Equals(obj as ValueObject);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var component in GetEqualityComponents())
        {
            hash.Add(component);
        }

        return hash.ToHashCode();
    }

    public static bool operator ==(ValueObject? left, ValueObject? right) => Equals(left, right);

    public static bool operator !=(ValueObject? left, ValueObject? right) => !Equals(left, right);
}
