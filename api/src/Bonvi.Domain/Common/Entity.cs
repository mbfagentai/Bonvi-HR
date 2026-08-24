namespace Bonvi.Domain.Common;

/// <summary>
/// Barcha domen obyektlarining asosi.
/// Tenglik faqat <see cref="Id"/> bo'yicha aniqlanadi — maydonlar bo'yicha emas.
/// </summary>
public abstract class Entity : IEquatable<Entity>
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected Entity(Guid id) => Id = id;

    /// <summary>Faqat EF Core materializatsiyasi uchun.</summary>
    protected Entity() { }

    /// <summary>
    /// Birlamchi kalit. GUID v7 — vaqt bo'yicha tartiblangan,
    /// shu sababli indeks parchalanmaydi (tasodifiy GUID'dan farqli).
    /// </summary>
    public Guid Id { get; protected init; }

    /// <summary>Hali tarqatilmagan domen hodisalari.</summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>Yangi identifikator hosil qiladi (vaqt bo'yicha o'suvchi).</summary>
    public static Guid NewId() => Guid.CreateVersion7();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>Hodisalar tarqatilgandan so'ng chaqiriladi.</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();

    public bool Equals(Entity? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;
        return Id != Guid.Empty && Id == other.Id;
    }

    public override bool Equals(object? obj) => Equals(obj as Entity);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(Entity? left, Entity? right) => Equals(left, right);

    public static bool operator !=(Entity? left, Entity? right) => !Equals(left, right);
}
