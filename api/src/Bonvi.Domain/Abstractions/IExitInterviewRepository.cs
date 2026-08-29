using Bonvi.Domain.Feedback;

namespace Bonvi.Domain.Abstractions;

/// <summary>«Qayta aloqa» (exit interview) yozuvlari uchun saqlash porti.</summary>
public interface IExitInterviewRepository : IRepository<ExitInterview>
{
    /// <summary>Berilgan mehnat munosabatiga tegishli suhbat (bittadan ortiq bo'lmaydi).</summary>
    Task<ExitInterview?> GetByEmploymentAsync(Guid employmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bitta XODIMning barcha suhbatlari — suhbat sanasi bo'yicha kamayish tartibida.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega <see cref="GetByEmploymentAsync"/> yetmaydi.</b> Bitta
    /// shaxsning bir nechta ketishi bo'lishi mumkin (ketib, qaytib kelib,
    /// yana ketgan) va xodim kartasi ularning HAMMASINI ko'rsatishi kerak.
    /// Munosabatlar bo'yicha bittalab so'rash esa klientdan N ta so'rov
    /// talab qilardi.
    /// </para>
    /// <para>
    /// <b>Nega <see cref="ListAsync"/> ning «search» i yaramaydi.</b> U
    /// MATN bo'yicha qidiradi va bir xil ismli ikki odamni ajrata olmaydi
    /// — kartada esa aynan SHU odamning yozuvlari kerak.
    /// </para>
    /// </remarks>
    Task<IReadOnlyList<ExitInterview>> GetByEmployeeAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Barcha yozuvlar — suhbat sanasi bo'yicha kamayish tartibida.
    /// </summary>
    /// <param name="from">Quyi chegara (o'zi ham kiradi). <c>null</c> — chegarasiz.</param>
    /// <param name="to">Yuqori chegara (o'zi ham kiradi). <c>null</c> — chegarasiz.</param>
    /// <param name="search">
    /// XODIM bo'yicha qidiruv: F.I.Sh., JSHSHIR, pasport yoki telefon.
    /// <c>null</c> yoki bo'sh — qidiruvsiz.
    /// <para>
    /// <b>Nega suhbat matnidan qidirilmaydi.</b> Yozuvda uchta erkin matn
    /// bor (sabab, taassurot, qaytish sharti) va ular ko'pincha bir necha
    /// jumla. Ularni qidiruvga qo'shish «Karimov bilan ishlash og'ir edi»
    /// deb yozilgan BEGONA suhbatni Karimov so'rovida chiqarardi — bu
    /// ro'yxatda esa bunday xato odamning obro'siga tegadi. Matn bo'yicha
    /// qidiruv kerak bo'lsa, u ALOHIDA maydon bo'lib qo'shiladi.
    /// </para>
    /// </param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyList<ExitInterview>> ListAsync(
        DateOnly? from = null,
        DateOnly? to = null,
        string? search = null,
        CancellationToken cancellationToken = default);
}
