using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.Me;

/// <summary>
/// «Jamoam» uchun o'qish modeli — panel qatorlarida yo'q bo'lgan maydonlar: telefon va a'zo oyi sarlavhasining nomlari.
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
/// Telefon va katalog nomlaridan boshqa HECH NARSA qaytmaydi — JSHSHIR, pasport, manzil bu
/// yo'lga umuman tushmasligi uchun (§C2).
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

    /// <summary>
    /// Bo'lim va lavozim NOMLARI — o'chirilganlari bilan (panel qatoridagi nomlar bilan bir xil qoida).
    /// </summary>
    /// <remarks>
    /// A'zo oyi sarlavhasi uchun: bo'lim va lavozim munosabatning joriy maydonidan emas, sanadagi
    /// joyidan (<c>EmploymentPlacement</c>) keladi — boshqa bo'limga ketgan odamning eski oyini
    /// ochgan boshliq uning YANGI bo'limini bilib olmasin.
    /// </remarks>
    /// <param name="departmentId">Bo'lim; <c>null</c> — nomi ham <c>null</c>.</param>
    /// <param name="positionId">Lavozim; <c>null</c> — nomi ham <c>null</c>.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<TeamPlacementNames> PlacementNamesAsync(
        Guid? departmentId,
        Guid? positionId,
        CancellationToken cancellationToken = default);
}

/// <summary>Sanadagi joyning nomlari.</summary>
/// <param name="Department">Bo'lim nomi.</param>
/// <param name="Position">Lavozim nomi.</param>
public sealed record TeamPlacementNames(string? Department, string? Position);
