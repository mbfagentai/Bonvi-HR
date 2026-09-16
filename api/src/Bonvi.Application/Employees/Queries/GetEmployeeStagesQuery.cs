using Bonvi.Application.Employees.Abstractions;
using Bonvi.Application.Employees.Common;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Employees.Queries;

/// <summary>
/// Xodimning nomzodlik davridagi bosqich xulosalari («Kengash xulosasi»).
/// </summary>
/// <remarks>
/// <para>
/// <b>FAQAT O'QISH.</b> Buyruq yo'q va bo'lmaydi ham: bu bo'lim xodim
/// kartasida yangi yozuv yaratmaydi. Sabab foydalanuvchi qarorida —
/// kengash xulosasi NOMZODNI baholaydi va u rekruting voronkasida,
/// nomzod hali xodim bo'lmagan paytda yoziladi. Uni keyinchalik xodim
/// kartasidan qo'shish mumkin bo'lsa, tizimda ikkita manba paydo
/// bo'lardi: rekrutingdagi haqiqiy tarix va xodim kartasidagi keyin
/// yozilgan «tarix» — va qaysi biri to'g'ri ekanini hech kim ayta
/// olmasdi.
/// </para>
/// <para>
/// Shu sababli bu yerda ham yozish porti chaqirilmaydi: o'qish modeli
/// <c>SaveChanges</c> ni umuman bilmaydi.
/// </para>
/// </remarks>
public sealed record GetEmployeeStagesQuery(Guid EmployeeId);

public sealed class GetEmployeeStagesQueryHandler
{
    private readonly IEmployeeReadModel _readModel;
    private readonly IEmployeeRepository _employees;

    public GetEmployeeStagesQueryHandler(IEmployeeReadModel readModel, IEmployeeRepository employees)
    {
        _readModel = readModel;
        _employees = employees;
    }

    public async Task<Result<IReadOnlyList<EmployeeStageEntryDto>>> HandleAsync(
        GetEmployeeStagesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Xodim bor-yo'qligi ALOHIDA tekshiriladi: busiz bo'sh ro'yxat
        // «bu xodim rekruting orqali kelmagan» va «bunday xodim yo'q»
        // holatlarini farqlamas edi — ikkinchisida interfeys bo'sh bo'lim
        // chizib, xatoni yashirib qo'yardi (`GetEmployeeFlagsQueryHandler`
        // dagi bilan bir xil mulohaza).
        var exists = await _employees.ExistsAsync(query.EmployeeId, cancellationToken).ConfigureAwait(false);
        if (!exists)
        {
            return EmployeeErrors.NotFound;
        }

        var rows = await _readModel
            .ListCandidateStagesAsync(query.EmployeeId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<EmployeeStageEntryDto> items =
        [
            .. rows.Select(row => new EmployeeStageEntryDto(
                row.Id,
                row.CandidateId,
                row.StageId,
                row.StageName,
                row.Date,
                row.Conclusion,
                row.RecordedBy,
                row.RecordedByName,
                row.RecordedAt)),
        ];

        return Result.Success(items);
    }
}
