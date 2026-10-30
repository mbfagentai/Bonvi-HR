using Bonvi.Application.Abstractions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Employees;

namespace Bonvi.Application.Employees.Probations;

/// <summary>
/// <c>probations:auto-pass</c> — muddati tugagan, lekin HR natija kiritmagan sinovni
/// avtomatik «O'tdi» qiladi va xodimni faol (doimiy) xodimga o'tkazadi.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>2026-10-30 dan REJADA YO'Q</b> (egasi: «sinov muddati tugasa ham tugadi deb ogohlantirib
/// chiqib tursa yetarli»): <c>BackgroundJobs</c> jadval yozuvini o'chiradi. Muddati o'tgan sinov
/// «Sinov muddati o'tib ketgan» bildirishnomasi bilan turadi, natijani HR kiritadi. Klass qaytarish
/// oson bo'lishi uchun qoldirilgan.
/// </para>
/// <para>
/// <b>Egasining qarori (2026-10-22):</b> «sinov muddat tugasa avtomatik active
/// xodimga aylansin». Bu 2026-10-14 dagi «muddat tabiiy o'tib ketganlar avtomatik
/// o'tmaydi — HR qaror qiladi» qoidasini BEKOR qiladi. HR «o'tmadi» / «o'zi ketdi»
/// ni muddat tugashidan OLDIN kiritishi kerak — keyin xodim allaqachon doimiy.
/// </para>
/// <para>
/// <b>Qachon:</b> sinovning oxirgi kuni (<see cref="Probation.EndDate"/>) to'liq
/// o'tgach — Toshkent bo'yicha keyingi kun. Natija sanasi esa tugash sanasining
/// O'ZI: xodim aynan o'sha kuni sinovdan chiqqan, vazifa ishlagan kun emas
/// (server o'chiq turgan bo'lsa ham sana to'g'ri qoladi).
/// </para>
/// <para>
/// <b>HR «O'tdi» bilan bir xil yo'l</b> (<see cref="ResolveProbationCommandHandler"/>):
/// sinov yopiladi, munosabat «sinovda» bo'lsa — «faol» ga o'tadi va mehnat
/// tarixiga «sinov natijasi» hodisasi yoziladi. Lavozim va oklad o'zgarmaydi —
/// ularni faqat HR tayinlaydi. Munosabat allaqachon yopilgan bo'lsa (bo'shatilgan,
/// sinov esa ochiq qolib ketgan) — faqat sinov natijasi yoziladi.
/// </para>
/// <para>
/// Har bir sinov ALOHIDA saqlanadi: yurish o'rtada to'xtasa, o'tkazilganlari
/// saqlanib qoladi, keyingi yurish qolganidan davom etadi. Saqlashda xato chiqsa
/// vazifa yiqiladi va Hangfire uni keyinroq qayta ishga tushiradi.
/// </para>
/// </remarks>
public sealed class ProbationAutoPassJob
{
    /// <summary>Audit jurnalidagi amal nomi — vazifa identifikatori bilan bir xil.</summary>
    public const string AuditOperation = "job:probations:auto-pass";

    /// <summary>Sinov xulosasi va mehnat tarixidagi izoh — kim qaror qilgani ko'rinib tursin.</summary>
    public const string Conclusion = "Avtomatik: sinov muddati tugadi, HR natija kiritmagan.";

    /// <summary>
    /// Bir vaqtda faqat BITTA yurish (Hangfire jadvali va qayta urinishi bir lahzaga
    /// tushsa ham) — <c>EmployeeAccountProvisioningJob</c> bilan bir xil sabab.
    /// </summary>
    private static readonly SemaphoreSlim SingleRun = new(1, 1);

    private readonly IProbationRepository _probations;
    private readonly IEmploymentRepository _employments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IAuditScope _auditScope;

    public ProbationAutoPassJob(
        IProbationRepository probations,
        IEmploymentRepository employments,
        IUnitOfWork unitOfWork,
        IClock clock,
        IAuditScope auditScope)
    {
        _probations = probations;
        _employments = employments;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _auditScope = auditScope;
    }

    /// <summary>Muddati tugagan barcha sinovlarni «o'tdi» qiladi.</summary>
    /// <param name="cancellationToken">Bekor qilish belgisi (Hangfire beradi).</param>
    /// <returns>Shu yurishda o'tkazilgan sinovlar soni.</returns>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        await SingleRun.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // Audit qatorlari «tizim, shu vazifa» bo'lib yoziladi
            using var scope = _auditScope.Use(AuditOperation);

            var expired = await _probations
                .GetExpiredIdsAsync(_clock.Today, cancellationToken)
                .ConfigureAwait(false);

            var passed = 0;

            foreach (var probationId in expired)
            {
                if (await PassAsync(probationId, cancellationToken).ConfigureAwait(false))
                {
                    passed++;
                }
            }

            return passed;
        }
        finally
        {
            SingleRun.Release();
        }
    }

    private async Task<bool> PassAsync(Guid probationId, CancellationToken cancellationToken)
    {
        var probation = await _probations.GetByIdAsync(probationId, cancellationToken).ConfigureAwait(false);

        // Ro'yxat olingandan beri HR o'zi yakunlagan bo'lishi mumkin — unda tegilmaydi
        if (probation is null || probation.Status != ProbationStatus.InProgress)
        {
            return false;
        }

        var at = probation.EndDate;

        if (probation.Resolve(ProbationStatus.Passed, at, Conclusion).IsFailure)
        {
            return false;
        }

        var employment = await _employments
            .GetWithEventsAsync(probation.EmploymentId, cancellationToken)
            .ConfigureAwait(false);

        if (employment is { Status: EmploymentStatus.Probation })
        {
            var completed = employment.CompleteProbation(at, Conclusion);
            if (completed.IsFailure)
            {
                return false;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }
}
