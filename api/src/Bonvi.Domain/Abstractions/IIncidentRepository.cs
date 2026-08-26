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
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyList<Incident>> ListAsync(
        DateOnly? from = null,
        DateOnly? to = null,
        IncidentSeverity? severity = null,
        CancellationToken cancellationToken = default);

    /// <summary>Xodimning barcha hodisalari.</summary>
    Task<IReadOnlyList<Incident>> GetByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
}
