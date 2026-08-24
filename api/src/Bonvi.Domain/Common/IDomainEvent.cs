namespace Bonvi.Domain.Common;

/// <summary>
/// Domen hodisasi — biznesda ro'y bergan, o'zgartirib bo'lmaydigan fakt.
/// Masalan: "Xodim ishga qabul qilindi", "Sinov muddati tugadi".
/// </summary>
public interface IDomainEvent
{
    /// <summary>Hodisa qachon ro'y berdi (UTC).</summary>
    DateTimeOffset OccurredOn { get; }
}
