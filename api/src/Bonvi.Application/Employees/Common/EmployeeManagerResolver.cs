using Bonvi.Application.Attendance.Scheduling;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;

namespace Bonvi.Application.Employees.Common;

/// <summary>
/// Xodimning «Rahbar»i bo'lishi mumkin bo'lganlar — bo'limdan yuqoriga, navbat bilan (SPEC-jamoam §D).
/// </summary>
/// <remarks>
/// <para>
/// Bo'limdan boshlab otalar bo'ylab yuqoriga yuriladi; boshlig'i qo'yilgan va boshliq
/// xodimning O'ZI bo'lmagan har bir bo'lim bitta nomzod beradi. Zavod direktori o'z
/// zavodining boshlig'i — uning rahbari zavoddan yuqoridagi bo'limning boshlig'i.
/// </para>
/// <para>
/// Sof funksiya: «tirikmi» degan savol bu yerda YO'Q (u bazani talab qiladi) —
/// <see cref="EmployeeManagerResolver"/> nomzodlarni shu tartibda tekshiradi. O'chirilgan
/// bo'lim ro'yxatda yo'q, ya'ni zanjir o'sha yerda uziladi (<c>BranchIdsAsync</c> bilan bir
/// xil ma'no); halqa cheksiz aylanmaydi.
/// </para>
/// </remarks>
public static class ManagerChain
{
    /// <summary>Nomzodlar — yaqinidan uzog'iga, takrorsiz.</summary>
    /// <param name="employeeId">Xodim.</param>
    /// <param name="departmentId">Xodimning bugungi asosiy bo'limi.</param>
    /// <param name="departments">O'chirilmagan barcha bo'limlar.</param>
    public static IReadOnlyList<Guid> Candidates(
        Guid employeeId,
        Guid departmentId,
        IReadOnlyList<Department> departments)
    {
        ArgumentNullException.ThrowIfNull(departments);

        var byId = departments.ToDictionary(department => department.Id);
        var candidates = new List<Guid>();
        var seen = new HashSet<Guid>();
        Guid? current = departmentId;

        while (current is { } id && byId.TryGetValue(id, out var department) && seen.Add(id))
        {
            if (department.HeadEmployeeId is { } head && head != employeeId && !candidates.Contains(head))
            {
                candidates.Add(head);
            }

            current = department.ParentId;
        }

        return candidates;
    }
}

/// <summary>
/// Xodimning «Rahbar»i — Verifix'dagi «Руководитель» qatori (SPEC-jamoam §D).
/// </summary>
/// <remarks>
/// <para>
/// Bo'lim — BUGUNGI asosiy munosabatdan (<see cref="PrimaryEmployment.Select"/>, §5.1 qoidasi).
/// Bugun ishlamaydigan odamning rahbari yo'q (<c>null</c>).
/// </para>
/// <para>
/// Boshliq TIRIK bo'lishi shart: o'chirilgan yoki bugun ishlamaydigan (ishdan ketgan) boshliq
/// tashlab ketiladi va yurish davom etadi. Nomzodlar bittadan tekshiriladi — ularning soni
/// daraxt chuqurligidan oshmaydi (amalda 3), ya'ni so'rovlar soni ham shunday.
/// </para>
/// </remarks>
public sealed class EmployeeManagerResolver
{
    private readonly IDepartmentRepository _departments;
    private readonly IEmployeeRepository _employees;
    private readonly IEmploymentRepository _employments;

    public EmployeeManagerResolver(
        IDepartmentRepository departments,
        IEmployeeRepository employees,
        IEmploymentRepository employments)
    {
        _departments = departments;
        _employees = employees;
        _employments = employments;
    }

    /// <summary>Xodimning rahbari; topilmasa <c>null</c>.</summary>
    /// <param name="employeeId">Xodim.</param>
    /// <param name="employments">Uning munosabatlari (chaqiruvchida allaqachon bor).</param>
    /// <param name="today">Toshkent sanasi.</param>
    /// <param name="departments">
    /// O'chirilmagan barcha bo'limlar — chaqiruvchi o'qigan bo'lsa (<c>GET /api/me</c>); <c>null</c> — shu yerda o'qiladi.
    /// </param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public async Task<EmployeeManagerDto?> ResolveAsync(
        Guid employeeId,
        IEnumerable<Employment> employments,
        DateOnly today,
        IReadOnlyList<Department>? departments = null,
        CancellationToken cancellationToken = default)
    {
        if (PrimaryEmployment.Select(employments, today)?.DepartmentId is not { } departmentId)
        {
            return null;
        }

        departments ??= await _departments.ListAsync(onlyActive: false, cancellationToken).ConfigureAwait(false);

        foreach (var candidate in ManagerChain.Candidates(employeeId, departmentId, departments))
        {
            // O'chirilgan xodimni yumshoq o'chirish filtri o'zi tashlaydi.
            var head = await _employees.GetByIdAsync(candidate, cancellationToken).ConfigureAwait(false);
            if (head is null)
            {
                continue;
            }

            var employed = await _employments.IsEmployedOnAsync(candidate, today, cancellationToken).ConfigureAwait(false);
            if (employed)
            {
                return new EmployeeManagerDto(head.Id, head.FullName.Full);
            }
        }

        return null;
    }
}
