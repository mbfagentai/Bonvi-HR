using Bonvi.Application.Attendance.Requests;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Persistence.ReadModels;

/// <summary>
/// <see cref="IAbsenceTypeLookup"/> ning EF Core orqali amalga oshirilishi.
/// </summary>
/// <remarks>
/// Ma'lumotnomaning O'ZI <c>Catalog</c> modulida; davomat buyruqlari undan
/// faqat «bor-yo'qligi va faolligi» ni so'raydi va shu sababli bu port juda
/// tor. Yumshoq o'chirilgan tur TOPILMAYDI (global filtr) — o'chirilgan
/// turga yangi so'rov yozib bo'lmasligi kerak.
/// </remarks>
public sealed class AbsenceTypeLookup : IAbsenceTypeLookup
{
    private readonly AppDbContext _context;

    public AbsenceTypeLookup(AppDbContext context) => _context = context;

    /// <inheritdoc />
    public Task<AbsenceTypeState?> FindAsync(Guid id, CancellationToken cancellationToken = default)
        => _context.AbsenceTypes
            .AsNoTracking()
            .Where(type => type.Id == id)
            .Select(type => new AbsenceTypeState(type.Id, type.Name, type.IsActive, type.EmployeeCanRequest))
            .FirstOrDefaultAsync(cancellationToken);
}
