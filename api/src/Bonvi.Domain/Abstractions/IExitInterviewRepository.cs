using Bonvi.Domain.Feedback;

namespace Bonvi.Domain.Abstractions;

/// <summary>«Qayta aloqa» (exit interview) yozuvlari uchun saqlash porti.</summary>
public interface IExitInterviewRepository : IRepository<ExitInterview>
{
    /// <summary>Berilgan mehnat munosabatiga tegishli suhbat (bittadan ortiq bo'lmaydi).</summary>
    Task<ExitInterview?> GetByEmploymentAsync(Guid employmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Barcha yozuvlar — suhbat sanasi bo'yicha kamayish tartibida.
    /// </summary>
    /// <param name="from">Quyi chegara (o'zi ham kiradi). <c>null</c> — chegarasiz.</param>
    /// <param name="to">Yuqori chegara (o'zi ham kiradi). <c>null</c> — chegarasiz.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyList<ExitInterview>> ListAsync(
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default);
}
