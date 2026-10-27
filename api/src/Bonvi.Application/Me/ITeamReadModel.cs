using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.Me;

/// <summary>
/// «Jamoam» uchun o'qish modeli — panel qatorlarida yo'q bo'lgan yagona maydon: telefon.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega alohida port.</b> <see cref="IEmployeeAppReadModel"/> ning har bir metodi BITTA
/// xodimni oladi (o'zining). Bu yerda esa bir nechta xodim — panel sahifasidagilar — va
/// xavfsizlik shartnomasi boshqa: identifikatorlarni chaqiruvchi QAMROVDAN o'tkazib bo'lgan
/// bo'lishi shart (panel natijasi yoki <see cref="TeamScope.Covers"/>). Port o'zi hech narsani
/// tekshirmaydi va «hammasi» degan variantni taklif qilmaydi.
/// </para>
/// <para>
/// Telefondan boshqa HECH NARSA qaytmaydi — JSHSHIR, pasport, manzil bu yo'lga umuman
/// tushmasligi uchun (§C2).
/// </para>
/// </remarks>
public interface ITeamReadModel
{
    /// <summary>Xodimlarning asosiy telefoni — bitta so'rovda; raqami yo'qlar natijada yo'q.</summary>
    /// <param name="employeeIds">Qamrovdan o'tgan xodimlar (sahifa — ko'pi bilan 200).</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyDictionary<Guid, PhoneNumber>> PhonesAsync(
        IReadOnlyCollection<Guid> employeeIds,
        CancellationToken cancellationToken = default);
}
