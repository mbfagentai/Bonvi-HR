using Bonvi.Application.Abstractions;
using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Common;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Identity.EmployeeAccounts;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Me;

/// <summary>Xodimning bir oylik davomati.</summary>
/// <param name="Year">Yil.</param>
/// <param name="Month">Oy (1–12).</param>
public sealed record GetMyAttendanceQuery(int Year, int Month);

/// <summary>
/// <see cref="GetMyAttendanceQuery"/> ni bajaradi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Kataklar HR TABELIDAN olinadi</b>, o'z hisobidan emas: bitta xodimga
/// cheklangan <see cref="TimesheetFilter"/> bilan
/// <see cref="IAttendanceReadModel.GetTimesheetAsync"/> chaqiriladi. Sabab
/// SPEC §E da yozilgan — «HR ko'radigan AYNAN o'sha kataklar». Ikkinchi
/// hisoblash yo'li yozilsa, xodim va HR bitta kunni ikki xil ko'radigan kun
/// albatta kelardi va o'shanda kim haq ekanini aytib bo'lmasdi.
/// </para>
/// <para>
/// ⚠️ Xodim identifikatori SO'ROVDAN olinmaydi — TOKENDAN. Shu sababli
/// «boshqa odamning tabelini ko'rish» degan holat texnik jihatdan mavjud
/// emas va uni alohida ruxsat bilan qo'riqlash kerak emas.
/// </para>
/// <para>
/// Belgilar (<see cref="MyMarkDto"/>) alohida, bitta so'rov bilan olinadi
/// va kataklarga sana bo'yicha ulanadi: tabelning o'zi o'tishlarni
/// o'qimaydi va o'qishi ham kerak emas (u <c>attendance_days</c> ustida
/// quriladi).
/// </para>
/// </remarks>
public sealed class GetMyAttendanceQueryHandler
{
    private readonly ICurrentEmployee _currentEmployee;
    private readonly IEmployeeAccountRepository _accounts;
    private readonly IAttendanceReadModel _timesheet;
    private readonly IEmployeeAppReadModel _readModel;
    private readonly IClock _clock;

    public GetMyAttendanceQueryHandler(
        ICurrentEmployee currentEmployee,
        IEmployeeAccountRepository accounts,
        IAttendanceReadModel timesheet,
        IEmployeeAppReadModel readModel,
        IClock clock)
    {
        _currentEmployee = currentEmployee;
        _accounts = accounts;
        _timesheet = timesheet;
        _readModel = readModel;
        _clock = clock;
    }

    public async Task<Result<MyAttendanceDto>> HandleAsync(
        GetMyAttendanceQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var month = AttendanceQueryParsing.Month(query.Year, query.Month);
        if (month.IsFailure)
        {
            return month.Error;
        }

        if (_currentEmployee.AccountId is not { } accountId)
        {
            return EmployeeAccountErrors.InvalidRefreshToken;
        }

        var account = await _accounts.GetByIdAsync(accountId, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return EmployeeAccountErrors.AccountInactive;
        }

        var (days, totals) = await MonthAsync(account.EmployeeId, query.Year, query.Month, cancellationToken)
            .ConfigureAwait(false);

        // «Hozir» — so'ralgan oydan qat'i nazar: belgilash kartasi keyingi
        // yo'nalishni va joriy smena katagini shundan oladi (MyCurrentState).
        var now = _clock.UtcNow;
        var recent = await _readModel
            .RecentMarksAsync(account.EmployeeId, now.AddHours(-AttendanceRules.BoardLookbackHours), cancellationToken)
            .ConfigureAwait(false);

        return new MyAttendanceDto(
            query.Year,
            query.Month,
            days,
            totals,
            MyCurrentState.From(recent, now, _clock.Today));
    }

    /// <summary>
    /// Bitta xodimning oyi — kunlari va jami; <c>/api/me/attendance</c> ning O'ZI, xodim identifikatori bilan.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ Bo'lim boshlig'i a'zoning oyini ham SHU metod orqali oladi
    /// (SPEC-jamoam §C3): a'zoning oyi uning o'z ilovasidagidek ko'rinishi kerak,
    /// ikkinchi nusxa esa bir kun kelib boshqacha ko'rsatardi. «Hozir» bu yerda
    /// ATAYLAB yo'q — u oyning emas, xodimning BUGUNGI holati va boshliqqa berilmaydi.
    /// </para>
    /// <para>
    /// ⚠️ Ruxsat bu yerda TEKSHIRILMAYDI va oy ham: chaqiruvchi oyni
    /// (<c>AttendanceQueryParsing.Month</c>) va «bu odamni ko'rishga haqqi borligini»
    /// o'zi hal qilib bo'lgan bo'lishi shart. Shu sababli metod <c>internal</c> —
    /// endpoint uni to'g'ridan-to'g'ri chaqira olmaydi.
    /// </para>
    /// </remarks>
    /// <param name="employeeId">Xodim.</param>
    /// <param name="year">Yil (tekshirilgan).</param>
    /// <param name="month">Oy (tekshirilgan).</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    internal async Task<(IReadOnlyList<MyAttendanceDayDto> Days, TimesheetTotalsDto Totals)> MonthAsync(
        Guid employeeId,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        // Sahifa hajmi 1: filtr bitta xodimga cheklangan, ya'ni natijada
        // ko'pi bilan bitta qator bo'ladi.
        var filter = new TimesheetFilter(
            year,
            month,
            LegalEntityId: null,
            DepartmentId: null,
            PositionId: null,
            EmployeeId: employeeId,
            Search: null,
            Page: 1,
            PageSize: 1);

        var page = await _timesheet.GetTimesheetAsync(filter, cancellationToken).ConfigureAwait(false);

        var marks = await _readModel
            .MarksAsync(employeeId, filter.MonthStart, filter.MonthEnd, cancellationToken)
            .ConfigureAwait(false);

        // Qator yo'q bo'lishi NORMAL holat: odam shu oyda hali ishlamagan
        // (yangi qabul qilingan). U holda kataklar ham bo'sh bo'ladi va
        // ilova «bu oyda ma'lumot yo'q» degan ekranni chizadi — xato emas.
        var row = page.Items.Count == 0 ? null : page.Items[0];

        IReadOnlyList<MyAttendanceDayDto> days = row is null
            ? []
            :
            [
                .. row.Cells.Select(cell => new MyAttendanceDayDto(
                    cell,
                    marks.TryGetValue(cell.Date, out var list) ? list : [])),
            ];

        return (days, row?.Totals ?? EmptyTotals);
    }

    /// <summary>Qatori yo'q oy uchun nollar — <c>null</c> emas.</summary>
    /// <remarks>
    /// Mijoz «jami yo'q» va «jami nol» ni ajratishi kerak emas: ikkalasida
    /// ham ekranda nol turadi. <c>null</c> bo'lsa, ilovaning har bir
    /// ko'rsatkichida qo'shimcha tekshiruv paydo bo'lardi.
    /// </remarks>
    private static readonly TimesheetTotalsDto EmptyTotals = new(0, 0, 0, 0, 0, 0);
}
