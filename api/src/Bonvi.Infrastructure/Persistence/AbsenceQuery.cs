using Bonvi.Domain.Attendance;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Persistence;

/// <summary>
/// «Qaysi kunlar tasdiqlangan so'rov bilan oqlangan» sharti — YAGONA nusxa.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega alohida fayl.</b> Bu shart IKKI joyda kerak: tungi (va qo'lda)
/// qayta hisoblashda (<c>AttendanceComputationSource</c>) va tabelning
/// hosil qilingan kataklarida (<c>AttendanceReadModel</c>). Ikkalasi bir
/// xil savolga javob beradi va ajralib ketsa, natija jimgina zid bo'lardi:
/// saqlangan kun «Sababli», hosil qilingani esa «Kelmadi» bo'lib chiqardi —
/// va HR buni faqat tabelni ko'z bilan solishtirganda sezardi. Bu
/// <see cref="BirthdayQuery"/> dagi bilan bir xil sabab va bir xil davo.
/// </para>
/// <para>
/// <b>Faqat TASDIQLANGAN so'rov.</b> Kutayotgan so'rov hali qaror emas,
/// rad etilgani esa qarorning o'zi — ikkalasi ham kunni o'zgartirmaydi.
/// </para>
/// <para>
/// <b>Vaqt oralig'i e'tiborga OLINMAYDI</b> (yarim kunlik so'rov):
/// birinchi bosqichda tasdiqlangan so'rov faqat «kelmadi» ni oqlaydi
/// (SPEC-so'rovlar §B3), ya'ni kun ichidagi soatlar hisobga kirmaydi.
/// </para>
/// </remarks>
internal static class AbsenceQuery
{
    /// <summary>
    /// Berilgan xodimlarning [from, to] oralig'idagi oqlangan sanalari.
    /// </summary>
    /// <param name="context">Ma'lumotlar bazasi konteksti.</param>
    /// <param name="employeeIds">Xodimlar.</param>
    /// <param name="from">Davr boshi (o'zi ham kiradi).</param>
    /// <param name="to">Davr oxiri (o'zi ham kiradi).</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public static async Task<Dictionary<Guid, IReadOnlySet<DateOnly>>> ExcusedDatesAsync(
        AppDbContext context,
        IReadOnlyCollection<Guid> employeeIds,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(employeeIds);

        if (employeeIds.Count == 0)
        {
            return [];
        }

        var ids = employeeIds.ToList();

        var rows = await context.AbsenceRequests
            .AsNoTracking()
            .Where(request =>
                ids.Contains(request.EmployeeId) &&
                request.Status == AbsenceRequestStatus.Approved &&
                request.FromDate <= to &&
                request.ToDate >= from)
            .Select(request => new { request.EmployeeId, request.FromDate, request.ToDate })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var result = new Dictionary<Guid, IReadOnlySet<DateOnly>>();

        foreach (var row in rows)
        {
            if (!result.TryGetValue(row.EmployeeId, out var dates))
            {
                dates = new HashSet<DateOnly>();
                result[row.EmployeeId] = dates;
            }

            // So'ralgan oraliqdan tashqarisi yoyilmaydi: 92 kunlik so'rov
            // bitta kun so'ralganda 92 ta sana yasab o'tirmasligi kerak.
            var start = row.FromDate < from ? from : row.FromDate;
            var finish = row.ToDate > to ? to : row.ToDate;

            for (var date = start; date <= finish; date = date.AddDays(1))
            {
                ((HashSet<DateOnly>)dates).Add(date);
            }
        }

        return result;
    }
}
