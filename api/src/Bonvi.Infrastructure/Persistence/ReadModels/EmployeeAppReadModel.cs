using Bonvi.Application.Me;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Persistence.ReadModels;

/// <summary><see cref="IEmployeeAppReadModel"/> ning EF Core orqali amalga oshirilishi.</summary>
/// <remarks>
/// <para>
/// Hamma so'rov <c>AsNoTracking</c>: bu yerdan o'qilgan hech narsa
/// o'zgarmaydi.
/// </para>
/// <para>
/// ⚠️ Ikkala metod ham xodim identifikatorini MAJBURIY argument sifatida
/// oladi va «hammasi» degan variantni taklif qilmaydi — bu o'qish
/// modelining butun xavfsizlik shartnomasi (sababi interfeys izohida).
/// </para>
/// </remarks>
internal sealed class EmployeeAppReadModel : IEmployeeAppReadModel
{
    private readonly AppDbContext _context;

    public EmployeeAppReadModel(AppDbContext context) => _context = context;

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Bo'lim va lavozim BUGUNGI munosabatdan olinadi, oxirgi kun
    /// yozuvining suratidan emas: xodimga o'zining HOZIRGI joyi
    /// ko'rsatiladi. (Tabel esa teskarisini qiladi va to'g'ri qiladi —
    /// hisobot ko'chirishdan keyin ham o'zgarmasligi kerak.)
    /// </para>
    /// <para>
    /// Bir nechta amaldagi munosabatda ASOSIYsi tanlanadi
    /// (<c>is_primary</c>), u ham bo'lmasa — eng kechroq qabul qilingani.
    /// Ikki firmada ishlaydigan odamga ikkita bo'lim ko'rsatishning
    /// ma'nosi yo'q: ilovada bu bitta qator sarlavha, ro'yxat emas.
    /// </para>
    /// </remarks>
    public async Task<MyProfileRow?> ProfileAsync(
        Guid employeeId,
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        var person = await _context.Employees
            .AsNoTracking()
            .Where(employee => employee.Id == employeeId)
            .Select(employee => new
            {
                employee.FullName.LastName,
                employee.FullName.FirstName,
                employee.FullName.MiddleName,
                employee.PhotoKey,
                employee.MobileCheckInAllowed,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (person is null)
        {
            return null;
        }

        var employment = await _context.Employments
            .AsNoTracking()
            .Where(row =>
                row.EmployeeId == employeeId &&
                row.HireDate <= today &&
                (row.TerminationDate == null || row.TerminationDate >= today))
            .OrderByDescending(row => row.IsPrimary)
            .ThenByDescending(row => row.HireDate)
            .Select(row => new { row.DepartmentId, row.PositionId })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var department = employment?.DepartmentId is { } departmentId
            ? await _context.Departments
                .AsNoTracking()
                .Where(row => row.Id == departmentId)
                .Select(row => row.Name)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false)
            : null;

        var position = employment?.PositionId is { } positionId
            ? await _context.Positions
                .AsNoTracking()
                .Where(row => row.Id == positionId)
                .Select(row => row.Name)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false)
            : null;

        var fullName = FullName.FromTrusted(person.LastName, person.FirstName, person.MiddleName);

        return new MyProfileRow(
            fullName.Full,
            fullName.ShortForm,
            person.PhotoKey,
            department,
            position,
            person.MobileCheckInAllowed);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Ikkita so'rov, kun bo'yicha aylanish YO'Q: birinchisi butun oyning
    /// o'tishlarini (<c>ix_attendance_passes_employee_time</c>), ikkinchisi
    /// esa FAQAT telefon belgilarining obyekt nomlarini oladi
    /// (<c>ux_mobile_check_ins_pass</c> bo'yicha
    /// <c>WHERE pass_id = ANY(…)</c>). 31 ta so'rov o'rniga 2 ta.
    /// </para>
    /// <para>
    /// ⚠️ Ikkinchi so'rov faqat <c>method = Mobile</c> bo'lgan o'tishlar
    /// uchun yuboriladi: terminal o'tishlarida koordinata umuman yo'q va
    /// ular uchun bo'sh ro'yxat bilan borish bekorga.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyDictionary<DateOnly, IReadOnlyList<MyMarkDto>>> MarksAsync(
        Guid employeeId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var passes = await _context.AttendancePasses
            .AsNoTracking()
            .Where(pass =>
                pass.ResolvedEmployeeId == employeeId &&
                pass.LocalDate >= from &&
                pass.LocalDate <= to)
            .OrderBy(pass => pass.OccurredAt)
            .Select(pass => new
            {
                pass.Id,
                pass.LocalDate,
                pass.OccurredAt,
                pass.Direction,
                pass.Method,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (passes.Count == 0)
        {
            return new Dictionary<DateOnly, IReadOnlyList<MyMarkDto>>();
        }

        var mobileIds = passes
            .Where(pass => pass.Method == PassMethod.Mobile)
            .Select(pass => pass.Id)
            .ToList();

        var siteNames = mobileIds.Count == 0
            ? []
            : await _context.MobileCheckIns
                .AsNoTracking()
                .Where(checkIn => mobileIds.Contains(checkIn.PassId))
                .Join(
                    _context.WorkSites.AsNoTracking().IgnoreQueryFilters(),
                    checkIn => checkIn.WorkSiteId,
                    site => site.Id,
                    (checkIn, site) => new { checkIn.PassId, site.Name })
                .ToDictionaryAsync(row => row.PassId, row => row.Name, cancellationToken)
                .ConfigureAwait(false);

        // `IgnoreQueryFilters` yuqorida ATAYLAB: obyekt keyinchalik yumshoq
        // o'chirilgan bo'lsa ham, o'sha paytdagi belgi qayerda qabul
        // qilinganini aytishda davom etishi kerak — aks holda tarix
        // jimgina «obyektsiz» bo'lib qolardi.

        return passes
            .GroupBy(pass => pass.LocalDate)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<MyMarkDto>)
                [
                    .. group.Select(pass => new MyMarkDto(
                        pass.OccurredAt,
                        pass.Direction,
                        pass.Method == PassMethod.Mobile ? MarkSource.Mobile : MarkSource.Terminal,
                        pass.Method == PassMethod.Mobile ? siteNames.GetValueOrDefault(pass.Id) : null)),
                ]);
    }
}
