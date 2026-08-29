using Bonvi.Application.Employees.Common;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;

namespace Bonvi.Application.Employees.Flags;

/// <summary>Xodimga belgi qo'yadi.</summary>
/// <remarks>
/// Qora ro'yxat uchun sabab MAJBURIY — bu qoida domenda
/// (<c>belgi.sabab_bosh</c>). Excel'da sababsiz yozuvlar bor edi va bir
/// yildan keyin hech kim nima uchun ekanini bilmasdi.
/// </remarks>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="Type">Belgi turi.</param>
/// <param name="StartDate">Boshlanish sanasi. Bo'sh — bugun.</param>
/// <param name="EndDate">Tugash sanasi (muddatli belgilar uchun).</param>
/// <param name="Reason">Sabab. Qora ro'yxat uchun majburiy.</param>
/// <param name="ResponsibleEmployeeId">Mas'ul xodim.</param>
/// <param name="Confirm">
/// Kuchdagi belgini ALMASHTIRISHGA rozilik.
/// <para>
/// Xodimda bir vaqtda atigi bitta kuchdagi belgi bo'ladi. Eskisi bo'lsa,
/// birinchi urinish <c>belgi.boshqa_belgi_bor</c> bilan to'xtaydi va HR
/// nima yo'qolishini ko'radi; shu bayroq bilan qayta yuborilganda eskisi
/// yopilib yangisi ochiladi. Bu <c>TerminateEmploymentCommand.Confirm</c>
/// bilan bir xil naqsh — jimgina bajarish HR ni «belgi qayerga ketdi?»
/// degan savol bilan qoldirardi.
/// </para>
/// </param>
public sealed record RaiseFlagCommand(
    Guid EmployeeId,
    EmployeeFlagType Type,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    string? Reason = null,
    Guid? ResponsibleEmployeeId = null,
    bool Confirm = false);

/// <summary>Belgini yopadi (masalan nazorat muddati tugadi).</summary>
public sealed record CloseFlagCommand(Guid FlagId, DateOnly? ClosedAt = null, string? Note = null);

/// <summary>
/// Belgi qo'yadi va «bir vaqtda atigi bitta belgi» qoidasini qo'llaydi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega qoida shu yerda, domenda emas.</b> U BIR XODIMNING boshqa
/// belgilarini bilishni talab qiladi, <c>EmployeeFlag</c> esa alohida
/// agregat va faqat o'zini ko'radi. Agregatlar orasidagi qoida — aynan
/// use-case qatlamining ishi (<c>EmployeeFlagGuard</c> bilan bir xil
/// bo'linish: o'qish repozitoriyda, qaror bu yerda).
/// </para>
/// <para>
/// <b>Ikkalasi BITTA tranzaksiyada.</b> Eskisini yopish va yangisini
/// ochish orasida <c>SaveChangesAsync</c> yo'q: u ikki marta chaqirilsa,
/// birinchisi o'tib ikkinchisi yiqilganda xodim BELGISIZ qolardi — ya'ni
/// qora ro'yxatdagi odam jimgina toza bo'lib chiqardi. Bitta saqlash esa
/// EF uchun bitta tranzaksiya: yo ikkalasi ham yoziladi, yo hech biri.
/// </para>
/// </remarks>
public sealed class RaiseFlagCommandHandler
{
    private readonly IEmployeeFlagRepository _flags;
    private readonly IEmployeeRepository _employees;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public RaiseFlagCommandHandler(
        IEmployeeFlagRepository flags,
        IEmployeeRepository employees,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _flags = flags;
        _employees = employees;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EmployeeFlagDto>> HandleAsync(
        RaiseFlagCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var exists = await _employees.ExistsAsync(command.EmployeeId, cancellationToken).ConfigureAwait(false);
        if (!exists)
        {
            return EmployeeErrors.NotFound;
        }

        var today = _clock.Today;
        var startDate = command.StartDate ?? today;

        // Yangi belgi AVVAL yig'iladi: formaning o'z xatosi (sababsiz qora
        // ro'yxat, teskari sanalar) tasdiqlash so'rashdan OLDIN chiqishi
        // kerak — aks holda HR «eskisini yopamizmi?» degan savolga javob
        // berib, keyin «sabab kiriting» xatosini olardi.
        var raised = EmployeeFlag.Raise(
            command.EmployeeId,
            command.Type,
            startDate,
            command.Reason,
            command.EndDate,
            command.ResponsibleEmployeeId);

        if (raised.IsFailure)
        {
            return raised.Error;
        }

        // Faollik YANGI belgi kuchga kiradigan kunga qarab hisoblanadi,
        // bugunga emas: belgi orqaga sana bilan kiritilishi mumkin va
        // o'shanda ham «o'sha kuni nima kuchda edi» degan savol to'g'ri.
        //
        // Yozuvlar KUZATILADIGAN holda olinadi (`GetByEmployeeAsync` dan
        // farqli — u `AsNoTracking`): ular shu yerda o'zgartiriladi va
        // kuzatuvsiz `Supersede` hech qanday `UPDATE` yubormasdan jimgina
        // yo'qolib ketardi.
        var inForce = await _flags
            .GetInForceAsync(command.EmployeeId, startDate, cancellationToken)
            .ConfigureAwait(false);

        if (inForce.Count > 0 && !command.Confirm)
        {
            return EmployeeErrors.FlagAlreadyInForce;
        }

        foreach (var flag in inForce)
        {
            // Almashtirish kuni — yangi belgining boshlanish kuni. Eski belgi
            // o'sha kun oxirigacha amal qiladi (tizimdagi «yopish» ning mavjud
            // ma'nosi), ya'ni qora ro'yxat bloki almashtirish kunida ham
            // ishlashda davom etadi — bu ataylab: himoya bir kunga bo'lsa ham
            // uzilib qolmasligi kerak.
            var superseded = flag.Supersede(startDate, "boshqa belgi bilan almashtirildi");
            if (superseded.IsFailure)
            {
                return superseded.Error;
            }
        }

        _flags.Add(raised.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(EmployeeMapper.ToDto(raised.Value, today));
    }
}

public sealed class CloseFlagCommandHandler
{
    private readonly IEmployeeFlagRepository _flags;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public CloseFlagCommandHandler(IEmployeeFlagRepository flags, IClock clock, IUnitOfWork unitOfWork)
    {
        _flags = flags;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EmployeeFlagDto>> HandleAsync(
        CloseFlagCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var flag = await _flags.GetByIdAsync(command.FlagId, cancellationToken).ConfigureAwait(false);
        if (flag is null)
        {
            return EmployeeErrors.FlagNotFound;
        }

        var today = _clock.Today;

        var closed = flag.Close(command.ClosedAt ?? today, command.Note);
        if (closed.IsFailure)
        {
            return closed.Error;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(EmployeeMapper.ToDto(flag, today));
    }
}
