using Bonvi.Application.Abstractions;
using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Recompute;
using Bonvi.Application.Catalog.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Catalog;

namespace Bonvi.Application.Attendance.Requests;

/// <summary>
/// Yo'qlik turining «ish vaqti hisoblanadi» bayrog'i o'zgarganda o'sha turdagi
/// tasdiqlangan so'rovlar kunlarini qayta hisoblashga qo'yadi (SPEC-so'rovlar §B3 AMENDED).
/// </summary>
/// <remarks>
/// <para>
/// Usiz bayroq faqat KEYINGI qarorlarga ta'sir qilardi: HR «Ishxona ishi bilan»
/// turini yoqsa, kechagi tasdiqlangan so'rovlar «Sababli · 0 soat» bo'lib
/// qolaverardi va tabel ikki xil qoidada hisoblangan bo'lardi.
/// </para>
/// <para>
/// Hisoblash NAVBATDA (fon vazifasi): tur o'nlab so'rovga tegishli bo'lishi
/// mumkin va ma'lumotnoma oynasi ularni kutib turmasligi kerak. Bir xodimning
/// kesishgan yoki tutash davrlari bitta vazifaga birlashtiriladi. Muzlatilgan
/// davr (<c>attendance.frozenThrough</c>) ni hisoblovchining o'zi himoya qiladi.
/// </para>
/// <para>
/// ⚠️ Ilgak bayroq SAQLANGANDAN keyin ishlaydi: navbatga qo'yish yiqilsa, bayroqni
/// qaytarib bo'lmaydi va bir xil qiymatni qayta saqlash hech narsa qilmaydi. Shu
/// sababli xato yutilmaydi-yu, javobni ham buzmaydi — jurnalga «qo'lda qayta
/// hisoblang» deb yoziladi (tungi hisoblash faqat oxirgi uch kunni tuzatadi).
/// </para>
/// </remarks>
public sealed class AbsenceTypeWorkTimeHook : IReferenceSavedHook<AbsenceType>
{
    private readonly IAbsenceRequestRepository _requests;
    private readonly IBackgroundJobQueue _jobs;
    private readonly IAttendanceJobJournal _journal;

    public AbsenceTypeWorkTimeHook(
        IAbsenceRequestRepository requests,
        IBackgroundJobQueue jobs,
        IAttendanceJobJournal journal)
    {
        _requests = requests;
        _jobs = jobs;
        _journal = journal;
    }

    /// <inheritdoc />
    public object? Snapshot(AbsenceType entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return entity.CountsAsWorkTime;
    }

    /// <inheritdoc />
    public async Task OnSavedAsync(AbsenceType entity, object? snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (snapshot is bool before && before == entity.CountsAsWorkTime)
        {
            return;
        }

        var approved = await _requests
            .ListApprovedByTypeAsync(entity.Id, cancellationToken)
            .ConfigureAwait(false);

        var periods = Periods(approved);

        try
        {
            foreach (var (employeeId, from, to) in periods)
            {
                await _jobs.EnqueueAsync<AttendanceRecomputeRangeJob>(job => job.RunAsync(
                        from,
                        to,
                        employeeId,
                        CancellationToken.None))
                    .ConfigureAwait(false);
            }
        }
#pragma warning disable CA1031 // Bayroq allaqachon saqlangan — xato jurnalga, javob buzilmaydi (sinf izohi).
        catch (Exception error) when (error is not OperationCanceledException)
#pragma warning restore CA1031
        {
            _journal.WorkTimeRecomputeNotQueued(periods.Count, error);
        }
    }

    /// <summary>
    /// So'rovlar ta'sir qiladigan davrlar — har xodimda kesishgan va tutash davrlar birlashtirilgan.
    /// </summary>
    /// <param name="requests">Tasdiqlangan so'rovlar.</param>
    internal static List<(Guid EmployeeId, DateOnly From, DateOnly To)> Periods(IEnumerable<Domain.Attendance.AbsenceRequest> requests)
    {
        var result = new List<(Guid EmployeeId, DateOnly From, DateOnly To)>();

        foreach (var group in requests
                     .Select(request => (request.EmployeeId, Days: DecideAbsenceRequestCommandHandler.AffectedDays(request)))
                     .GroupBy(item => item.EmployeeId))
        {
            (DateOnly From, DateOnly To)? current = null;

            foreach (var (from, to) in group.Select(item => item.Days).OrderBy(days => days.From))
            {
                if (current is { } open && from <= open.To.AddDays(1))
                {
                    current = (open.From, to > open.To ? to : open.To);
                    continue;
                }

                if (current is { } done)
                {
                    result.Add((group.Key, done.From, done.To));
                }

                current = (from, to);
            }

            if (current is { } last)
            {
                result.Add((group.Key, last.From, last.To));
            }
        }

        return result;
    }
}
