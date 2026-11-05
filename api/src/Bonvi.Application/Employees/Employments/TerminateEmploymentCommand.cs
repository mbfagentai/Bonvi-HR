using Bonvi.Application.Employees.Common;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.Processes.Gates;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Catalog;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;

namespace Bonvi.Application.Employees.Employments;

/// <summary>
/// Ishdan bo'shatish.
/// </summary>
/// <param name="EmploymentId">Yakunlanayotgan mehnat munosabati.</param>
/// <param name="TerminationDate">Ishdan ketish sanasi. Bo'sh bo'lsa — bugun.</param>
/// <param name="ReasonId">Ketish sababi (ma'lumotnomadan).</param>
/// <param name="OrderNo">Buyruq raqami.</param>
/// <param name="OrderDate">Buyruq sanasi.</param>
/// <param name="Note">Izoh.</param>
/// <param name="Confirm">
/// Belgi (qora ro'yxat / nazorat) bo'lgan xodimni bo'shatishga rozilik.
/// <c>false</c> bo'lsa amal 409 bilan to'xtaydi va ogohlantirish qaytadi.
/// </param>
public sealed record TerminateEmploymentCommand(
    Guid EmploymentId,
    DateOnly? TerminationDate = null,
    Guid? ReasonId = null,
    string? OrderNo = null,
    DateOnly? OrderDate = null,
    string? Note = null,
    bool Confirm = false);

/// <summary>
/// Bo'shatish use-case'i.
/// </summary>
/// <remarks>
/// <para>
/// <b>Blok qoidasi — HR aynan shuni so'radi.</b> Qora ro'yxatdagi yoki
/// nazoratdagi odam «jimgina» ketib qolmasligi kerak: uning bo'yicha
/// xavfsizlik xizmatining ochiq savoli yoki moddiy javobgarligi bo'lishi
/// mumkin. Shu sababli birinchi so'rov 409 bilan qaytadi va nima uchun
/// to'xtaganini aytadi; HR ataylab <c>confirm=true</c> yuborsa — o'tadi.
/// </para>
/// <para>
/// <b>Nega taqiq emas, tasdiq.</b> Odam haqiqatda ketayotgan bo'lsa,
/// tizim uni ushlab tura olmaydi — aks holda HR yozuvni umuman
/// yangilamay qo'yadi va baza haqiqatdan uzoqlashadi.
/// </para>
/// </remarks>
public sealed class TerminateEmploymentCommandHandler
{
    private readonly IEmploymentRepository _employments;
    private readonly IProbationRepository _probations;
    private readonly IReferenceRepository<TerminationReason> _reasons;
    private readonly IEmployeeFlagRepository _flags;
    private readonly IDepartmentRepository _departments;
    private readonly EmployeeFlagGuard _flagGuard;
    private readonly IProcessGate _processGate;
    private readonly EmployeeMapper _mapper;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public TerminateEmploymentCommandHandler(
        IEmploymentRepository employments,
        IProbationRepository probations,
        IReferenceRepository<TerminationReason> reasons,
        IEmployeeFlagRepository flags,
        IDepartmentRepository departments,
        EmployeeFlagGuard flagGuard,
        IProcessGate processGate,
        EmployeeMapper mapper,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _employments = employments;
        _probations = probations;
        _reasons = reasons;
        _flags = flags;
        _departments = departments;
        _flagGuard = flagGuard;
        _processGate = processGate;
        _mapper = mapper;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EmploymentChangeDto>> HandleAsync(
        TerminateEmploymentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var employment = await _employments
            .GetWithEventsAsync(command.EmploymentId, cancellationToken)
            .ConfigureAwait(false);

        if (employment is null)
        {
            return EmployeeErrors.EmploymentNotFound;
        }

        if (employment.Status == EmploymentStatus.Terminated)
        {
            return EmployeeErrors.EmploymentClosed;
        }

        if (command.ReasonId is { } reasonId
            && !await _reasons.ExistsAsync(reasonId, cancellationToken).ConfigureAwait(false))
        {
            return Error.NotFound("ketish_sababi.topilmadi", "Ishdan ketish sababi topilmadi.");
        }

        var flags = await _flagGuard.InspectAsync(employment.EmployeeId, cancellationToken).ConfigureAwait(false);

        var warnings = new List<HrWarning>();

        if (flags.Blacklisted)
        {
            warnings.Add(HrWarning.From(EmployeeErrors.Blacklisted));
        }

        if (flags.Monitored)
        {
            warnings.Add(HrWarning.From(EmployeeErrors.UnderMonitoring));
        }

        if (!flags.IsClean && !command.Confirm)
        {
            // Qaysi belgi jiddiyroq — o'shanisi kodda qaytadi, chunki
            // interfeys xato kodiga qarab matn va rang tanlaydi. Qolgan
            // sabablar `Related` da: xodim bir vaqtda ham qora ro'yxatda,
            // ham nazoratda bo'lishi mumkin va ikkalasi ham qarorga
            // ta'sir qiladi. Ularsiz HR birinchi to'siqni yopib,
            // ikkinchisiga urilib, «yana nima bor?» deb qolardi.
            return flags.Blacklisted
                ? EmployeeErrors.Blacklisted.With(
                    [.. warnings.Select(warning => warning.Code)])
                : EmployeeErrors.UnderMonitoring;
        }

        var terminationDate = command.TerminationDate ?? _clock.Today;

        // ── Jarayon to'siqlari Q12, Q13 (docs/SPEC-mening-ishlarim.md §8) ──
        // `confirm` bularni aylanib o'tmaydi: u faqat belgilar haqidagi tasdiq.
        var gate = await _processGate
            .CheckAsync(GateContext.ForTerminate(employment.Id, employment.EmployeeId, employment.LegalEntityId, terminationDate), cancellationToken)
            .ConfigureAwait(false);

        if (gate.Blocking is { } blocked)
        {
            return blocked;
        }

        warnings.AddRange(gate.Warnings);

        var terminated = employment.Terminate(
            terminationDate,
            command.ReasonId,
            command.OrderNo,
            command.OrderDate,
            command.Note);

        if (terminated.IsFailure)
        {
            return terminated.Error;
        }

        // Ochiq sinov muddati yopiladi: odam sinovni tugatmasdan ketdi.
        // Aks holda «sinovdagilar» ro'yxatida ishdan ketgan odam turib qolardi —
        // Excel'da aynan shunday bo'lgan.
        var probation = await _probations
            .GetByEmploymentAsync(employment.Id, cancellationToken)
            .ConfigureAwait(false);

        if (probation is { Status: ProbationStatus.InProgress })
        {
            var resolved = probation.Resolve(
                ProbationStatus.LeftEarly,
                terminationDate,
                command.Note ?? "Sinov muddati tugamasdan ishdan bo'shadi");

            if (resolved.IsFailure)
            {
                return resolved.Error;
            }
        }

        // Ishlab turgan xodim belgilari («nazoratda», «qobiliyatli») ketish
        // bilan MA'NOSINI YO'QOTADI: ketgan odamni hech kim kuzatmaydi va
        // unga hech kim yuklama bermaydi. Naqsh yuqoridagi sinov muddati
        // bilan bir xil — aks holda «nazoratdagilar» ro'yxatida ishdan
        // ketgan odam turib qolardi.
        //
        // Ketish belgilari (rezerv, qora ro'yxat) BU YERDA qo'yilmaydi:
        // ular HR ning qarori, tizimning taxmini emas. Bo'shatgandan keyin
        // xodim kartasida tanlanadi (`EmployeeFlagStage` izohi).
        var leavesCompletely = await LeavesCompletelyAsync(employment, cancellationToken).ConfigureAwait(false);

        var closed = await CloseEmployedFlagsAsync(employment, terminationDate, leavesCompletely, cancellationToken)
            .ConfigureAwait(false);

        if (closed.IsFailure)
        {
            return closed.Error;
        }

        if (leavesCompletely)
        {
            await ReleaseHeadshipsAsync(employment.EmployeeId, cancellationToken).ConfigureAwait(false);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var dto = await _mapper.ToDtoAsync(employment, probation, cancellationToken).ConfigureAwait(false);

        return Result.Success(new EmploymentChangeDto(dto, warnings));
    }

    /// <summary>
    /// Bu munosabat xodimning OXIRGI faol munosabatimi — ya'ni u BUTUNLAY ketyaptimi.
    /// </summary>
    /// <remarks>
    /// <b>Nega joriy munosabat <c>Id</c> bo'yicha chiqarib tashlanadi.</b>
    /// <c>GetActiveByEmployeeAsync</c> — <c>AsNoTracking</c> so'rov, ya'ni
    /// u BAZANI o'qiydi. Hozirgina xotirada tugatilgan munosabat esa hali
    /// saqlanmagan va so'rovga «faol» bo'lib qaytadi. Uni ajratmasak,
    /// xodim hech qachon «butunlay ketgan» bo'lmasdi.
    /// </remarks>
    private async Task<bool> LeavesCompletelyAsync(Employment employment, CancellationToken cancellationToken)
    {
        var others = await _employments
            .GetActiveByEmployeeAsync(employment.EmployeeId, cancellationToken)
            .ConfigureAwait(false);

        return !others.Any(other => other.Id != employment.Id);
    }

    /// <summary>
    /// Butunlay ketgan xodimni boshliq bo'lgan barcha bo'limlaridan bo'shatadi — shu ish birligida.
    /// </summary>
    /// <remarks>
    /// <para>
    /// O'qish yo'llari ketgan boshliqni baribir tashlab ketadi (uning tokeni
    /// <c>xodim.hisob_faol_emas</c>, «Rahbar» undan yuqoriga yuradi), lekin bo'lim formasida
    /// ketgan odam boshliq bo'lib turaverardi va HR yangisini tayinlash kerakligini ko'rmasdi
    /// (SPEC-jamoam). O'zgarish bo'limning audit yozuviga tushadi.
    /// </para>
    /// <para>
    /// ⚠️ Kelajak sana bilan bo'shatilganda ham DARHOL: munosabat ham darhol «yakunlangan»
    /// bo'ladi (<c>Status</c>), ish oxirigacha esa yangi boshliqni tayinlash HR ning ishi.
    /// </para>
    /// </remarks>
    private async Task ReleaseHeadshipsAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var headed = await _departments.GetHeadedByAsync(employeeId, cancellationToken).ConfigureAwait(false);

        foreach (var department in headed)
        {
            department.AssignHead(null);
        }
    }

    /// <summary>
    /// Xodim BUTUNLAY ketganda ishlab turganlar uchun qo'yilgan belgilarni yopadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Faqat OXIRGI munosabat tugaganda</b> (<see cref="LeavesCompletelyAsync"/>). O'rindosh
    /// xodim ikkinchi firmadan ketsa ham bizda ishlashda davom etadi va nazorat kuchda
    /// qolishi kerak — belgi SHAXSGA qo'yilgan, munosabatga emas.
    /// </para>
    /// <para>
    /// <b>Nega belgilar KECHROQ sana bo'yicha izlanadi.</b>
    /// <c>GetInForceAsync</c> «shu kuni kuchda bo'lgan» yozuvlarni beradi,
    /// ya'ni <c>StartDate &lt;= sana</c>. Bo'shatish ORQAGA sana bilan
    /// yozilganda (buyruq kechikib kiritilgan) belgi ketish kunidan KEYIN
    /// qo'yilgan bo'lishi mumkin — o'sha sana bilan izlansak, u umuman
    /// topilmasdi va ketgan odamda ochiq «nazorat» bo'lib qolardi.
    /// Kelajakdagi bo'shatishda esa aksincha: o'sha kunga qadar qo'yilgan
    /// belgilar ham yopilishi kerak. Ikkala holatni bitta shart qamraydi
    /// — kechroq sana.
    /// </para>
    /// </remarks>
    private async Task<Result> CloseEmployedFlagsAsync(
        Employment employment,
        DateOnly terminationDate,
        bool leavesCompletely,
        CancellationToken cancellationToken)
    {
        if (!leavesCompletely)
        {
            return Result.Success();
        }

        var today = _clock.Today;
        var asOf = terminationDate > today ? terminationDate : today;

        var inForce = await _flags
            .GetInForceAsync(employment.EmployeeId, asOf, cancellationToken)
            .ConfigureAwait(false);

        foreach (var flag in inForce)
        {
            if (!EmployeeFlagStages.IsForEmployed(flag.Type))
            {
                continue;
            }

            // Belgi O'Z boshlanish sanasidan oldin yopilmaydi — `Supersede`
            // buni «belgi.sana» bilan rad etadi va butun bo'shatish
            // yiqilardi. Orqaga sana bilan bo'shatilgan xodimda keyinroq
            // qo'yilgan belgi shu sababli o'z kuni bilan yopiladi: qoida
            // «ketgan odamda ishlab turganlar belgisi qolmasin» degani,
            // «hammasi ketish kuni bilan yopilsin» emas.
            var closeAt = terminationDate < flag.StartDate ? flag.StartDate : terminationDate;

            var result = flag.Supersede(closeAt, "xodim ishdan ketdi");
            if (result.IsFailure)
            {
                return result.Error;
            }
        }

        return Result.Success();
    }
}
