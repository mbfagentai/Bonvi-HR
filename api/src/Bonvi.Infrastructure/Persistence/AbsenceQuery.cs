using Bonvi.Application.Attendance.Computation;
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
/// <see cref="ExcusedDatesAsync"/> da vaqt oralig'i e'tiborga OLINMAYDI: har
/// qanday tasdiqlangan so'rov qamragan sana «kelmadi» ni oqlaydi. Soatlar
/// faqat «ish vaqti hisoblanadigan» turda — <see cref="WorkCreditsAsync"/>
/// (SPEC-so'rovlar §B3 AMENDED 2026-10-17).
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

    /// <summary>
    /// «Ish vaqti hisoblanadigan» turdagi tasdiqlangan so'rovlar — [from, to] ni qamraganlari.
    /// </summary>
    /// <remarks>
    /// Tur faolsizlantirilgan yoki o'chirilgan bo'lsa ham hisoblanadi
    /// (<c>IgnoreQueryFilters</c>): qaror allaqachon qabul qilingan, tur
    /// ro'yxatdan olinsa o'tgan kunlar o'zgarib ketmasligi kerak. Chaqiruvchi
    /// oraliqni <see cref="WorkCreditRule.RequestDatesNeeded"/> dan oladi.
    /// </remarks>
    /// <param name="context">Ma'lumotlar bazasi konteksti.</param>
    /// <param name="employeeIds">Xodimlar.</param>
    /// <param name="from">Davr boshi (o'zi ham kiradi).</param>
    /// <param name="to">Davr oxiri (o'zi ham kiradi).</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public static async Task<Dictionary<Guid, IReadOnlyCollection<WorkCredit>>> WorkCreditsAsync(
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
        var workTypes = context.AbsenceTypes
            .IgnoreQueryFilters()
            .Where(type => type.CountsAsWorkTime)
            .Select(type => type.Id);

        var rows = await context.AbsenceRequests
            .AsNoTracking()
            .Where(request =>
                ids.Contains(request.EmployeeId) &&
                request.Status == AbsenceRequestStatus.Approved &&
                request.FromDate <= to &&
                request.ToDate >= from &&
                // ⚠️ Ochiq yozilgan: ichki so'rovdagi `IgnoreQueryFilters` EF'da BUTUN
                // so'rovga tarqaladi va so'rovning o'chirilganlik filtrini ham o'chiradi.
                request.DeletedAt == null &&
                workTypes.Contains(request.AbsenceTypeId))
            .Select(request => new { request.EmployeeId, request.FromDate, request.ToDate, request.FromTime, request.ToTime })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .GroupBy(row => row.EmployeeId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<WorkCredit>)
                [
                    .. group.Select(row => new WorkCredit(row.FromDate, row.ToDate, row.FromTime, row.ToTime)),
                ]);
    }
}
