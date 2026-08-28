using Bonvi.Domain.Safety;

namespace Bonvi.Domain.Abstractions;

/// <summary>Baxtsiz hodisalar jurnali uchun saqlash porti.</summary>
public interface IIncidentRepository : IRepository<Incident>
{
    /// <summary>
    /// Barcha yozuvlar — hodisa sanasi bo'yicha kamayish tartibida.
    /// </summary>
    /// <param name="from">Quyi chegara (o'zi ham kiradi). <c>null</c> — chegarasiz.</param>
    /// <param name="to">Yuqori chegara (o'zi ham kiradi). <c>null</c> — chegarasiz.</param>
    /// <param name="severity">Faqat shu darajadagilar. <c>null</c> — hammasi.</param>
    /// <param name="search">
    /// XODIM bo'yicha qidiruv: F.I.Sh., JSHSHIR, pasport yoki telefon.
    /// <c>null</c> yoki bo'sh — qidiruvsiz.
    /// <para>
    /// <b>Nega hodisa tavsifidan qidirilmaydi.</b> Tavsif, sabab va chora —
    /// erkin matn va ularda begona odamlarning ismi tez-tez uchraydi
    /// («Karimov guvoh bo'ldi»). Qidiruvga qo'shilsa, «Karimov» so'rovi
    /// unga UMUMAN aloqasi yo'q baxtsiz hodisani chiqarardi — bu jurnalda
    /// bunday xato jiddiy.
    /// </para>
    /// </param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyList<Incident>> ListAsync(
        DateOnly? from = null,
        DateOnly? to = null,
        IncidentSeverity? severity = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>Xodimning barcha hodisalari.</summary>
    Task<IReadOnlyList<Incident>> GetByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
}
