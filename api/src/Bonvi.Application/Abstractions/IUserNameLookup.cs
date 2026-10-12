namespace Bonvi.Application.Abstractions;

/// <summary>
/// Tizim foydalanuvchilarining F.I.Sh. — «kim kiritdi» degan yozuvlar uchun.
/// </summary>
/// <remarks>
/// O'chirilgan va faolsizlantirilgan hisoblar HAM topiladi: buyruqni kiritgan odam
/// ishdan ketgan bo'lsa ham, uning ismi tarixda qolishi kerak (SPEC-rollar-va-audit §7.9).
/// </remarks>
public interface IUserNameLookup
{
    /// <summary>Berilgan foydalanuvchilarning ismlari — bitta so'rov bilan; topilmaganlari lug'atda yo'q.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);
}
