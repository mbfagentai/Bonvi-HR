using Bonvi.Application.Catalog.Common;
using Bonvi.Application.Catalog.Dtos;
using Bonvi.Application.Employees.Commands;
using Bonvi.Application.Employees.Common;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.Employees.Employments;

/// <summary>
/// Ishga qabul — <b>yagona forma</b>.
/// </summary>
/// <remarks>
/// <para>
/// HR talabi so'zma-so'z: «bitta blanka to'ldirilishi kerak». Shu sababli
/// bu yerda shaxs, firma, bo'lim, lavozim, oklad va buyruq BIRGA keladi.
/// Ilgari bu uchta alohida amal edi va yarim to'ldirilgan yozuvlar qolib
/// ketardi.
/// </para>
/// <para>
/// <b>Xodim ikki yo'l bilan aniqlanadi.</b> <see cref="EmployeeId"/> berilsa —
/// mavjud shaxs (masalan ikkinchi firmaga o'rindosh sifatida). Berilmasa —
/// avval JSHSHIR bo'yicha qidiriladi: odam qaytib kelgan bo'lishi mumkin va
/// unga IKKINCHI karta ochish mumkin emas. Topilmasa yangi karta yaratiladi.
/// </para>
/// <para>
/// <b>Har qanday qabul sinovdan boshlanadi.</b> Foydalanuvchining aniq
/// qarori: «hamma sinovdan o'tadi, istisnosiz». Shu sababli
/// <see cref="ProbationDays"/> muddatning UZUNLIGINI o'zgartiradi, uni
/// o'chira olmaydi: nol yoki manfiy qiymat «sinov.majburiy» xatosi bo'lib
/// qaytadi. Ilgari nol sinovsiz qabul degani edi va yangi xodim
/// <c>/sinov</c> ro'yxatiga umuman tushmasdi. Bo'sh qoldirilsa lavozimdagi
/// qiymat, u ham bo'lmasa 90 kun olinadi.
/// </para>
/// <para>
/// <b><see cref="CandidateId"/> — 2-bosqich bilan yagona bog'lanish.</b> U
/// faqat rekrutingdan keladi va ishga qabul HODISASIGA yoziladi
/// (<see cref="EmploymentEvent.CandidateId"/>). Bu yerda ATAYLAB oddiy
/// ixtiyoriy maydon: ishga qabulning o'zi rekruting haqida hech narsa
/// bilmaydi va HR formasi uni yubormaydi.
/// </para>
/// </remarks>
public sealed record HireEmployeeCommand(
    Guid LegalEntityId,
    Guid? EmployeeId = null,
    SaveEmployeeCommand? Person = null,
    Guid? DepartmentId = null,
    Guid? PositionId = null,
    DateOnly? HireDate = null,
    string? OrderNo = null,
    DateOnly? OrderDate = null,
    decimal? SalaryAmount = null,
    string? SalaryCurrency = null,
    decimal? Rate = null,
    bool? IsPrimary = null,
    Guid? ResponsibleEmployeeId = null,
    int? ProbationDays = null,
    Guid? CandidateId = null);

public sealed class HireEmployeeCommandHandler
{
    /// <summary>Lavozimda ko'rsatilmagan bo'lsa — standart sinov muddati.</summary>
    /// <remarks>Mehnat kodeksida odatiy muddat 3 oy; Bonvi shuni ishlatadi.</remarks>
    public const int DefaultProbationDays = 90;

    /// <summary>Bir odamning faol stavkalari yig'indisi uchun chegara.</summary>
    public const decimal MaxTotalRate = 1.5m;

    private readonly IEmployeeRepository _employees;
    private readonly IEmploymentRepository _employments;
    private readonly IProbationRepository _probations;
    private readonly IPositionRepository _positions;
    private readonly IReferenceRepository<LegalEntity> _legalEntities;
    private readonly IEmployeeFlagRepository _flags;
    private readonly EmployeeFlagGuard _flagGuard;
    private readonly EmployeeMapper _mapper;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public HireEmployeeCommandHandler(
        IEmployeeRepository employees,
        IEmploymentRepository employments,
        IProbationRepository probations,
        IPositionRepository positions,
        IReferenceRepository<LegalEntity> legalEntities,
        IEmployeeFlagRepository flags,
        EmployeeFlagGuard flagGuard,
        EmployeeMapper mapper,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _employees = employees;
        _employments = employments;
        _probations = probations;
        _positions = positions;
        _legalEntities = legalEntities;
        _flags = flags;
        _flagGuard = flagGuard;
        _mapper = mapper;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<HireResultDto>> HandleAsync(
        HireEmployeeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var legalEntityExists = await _legalEntities
            .ExistsAsync(command.LegalEntityId, cancellationToken)
            .ConfigureAwait(false);

        if (!legalEntityExists)
        {
            return EmployeeErrors.LegalEntityNotFound;
        }

        var resolved = await ResolveEmployeeAsync(command, cancellationToken).ConfigureAwait(false);
        if (resolved.IsFailure)
        {
            return resolved.Error;
        }

        var (employee, isNew) = resolved.Value;
        var warnings = new List<HrWarning>();

        // ── Dublikat munosabat tekshiruvi ──
        // O'rindoshlik (bir odam ikki firmada) TO'SILMAYDI — Bonvi'da bu
        // odatiy hol. To'siladigani boshqa narsa: AYNAN SHU firmada
        // ikkinchi ochiq munosabat. Busiz «ishga qabul» tugmasi ikki marta
        // bosilsa, odamning shu firmadagi stavkasi, okladi va xodimlar
        // sanog'i ikki barobar bo'lib ketardi — va bu hech qanday xato
        // bermasdi, chunki ro'yxat har shaxsni baribir bir marta chizadi.
        if (!isNew)
        {
            var open = await _employments
                .GetActiveByEmployeeAsync(employee.Id, cancellationToken)
                .ConfigureAwait(false);

            if (open.Any(existing => existing.LegalEntityId == command.LegalEntityId))
            {
                return EmployeeErrors.AlreadyEmployedHere;
            }
        }

        // ── Qayta ishga olish tekshiruvi ──
        // Yangi karta uchun belgi bo'lishi mumkin emas, shu sababli faqat
        // mavjud shaxs tekshiriladi.
        if (!isNew)
        {
            var flags = await _flagGuard.InspectAsync(employee.Id, cancellationToken).ConfigureAwait(false);

            if (flags.Blacklisted)
            {
                // Qora ro'yxat — BLOK. Bu HR'ning aniq qoidasi: bunday odam
                // qayta ishga olinmaydi, avval belgi yopilishi kerak.
                return EmployeeErrors.Blacklisted;
            }

            if (flags.Monitored)
            {
                // Nazorat — faqat ogohlantirish: ishga olish davom etadi.
                warnings.Add(HrWarning.From(EmployeeErrors.UnderMonitoring));
            }
        }

        var position = command.PositionId is { } positionId
            ? await _positions.GetWithDutiesAsync(positionId, cancellationToken).ConfigureAwait(false)
            : null;

        if (command.PositionId is not null && position is null)
        {
            return CatalogErrors.PositionNotFound;
        }

        var hireDate = command.HireDate ?? _clock.Today;

        var hired = Employment.Hire(
            employee.Id,
            command.LegalEntityId,
            hireDate,
            command.DepartmentId,
            command.PositionId,
            command.OrderNo,
            command.OrderDate,
            command.CandidateId);

        if (hired.IsFailure)
        {
            return hired.Error;
        }

        var employment = hired.Value;

        if (command.Rate is { } rate)
        {
            var applied = employment.SetRate(rate);
            if (applied.IsFailure)
            {
                return applied.Error;
            }
        }

        if (command.IsPrimary is { } isPrimary)
        {
            employment.SetPrimary(isPrimary);
        }

        employment.AssignResponsible(command.ResponsibleEmployeeId);

        if (command.SalaryAmount is { } amount)
        {
            var money = Money.Create(amount, command.SalaryCurrency ?? Money.Uzs);
            if (money.IsFailure)
            {
                return money.Error;
            }

            var applied = employment.SetSalary(money.Value, hireDate, command.OrderNo, command.OrderDate);
            if (applied.IsFailure)
            {
                return applied.Error;
            }
        }

        // ── Stavka nazorati ──
        // Blok emas, ogohlantirish: Bonvi'da o'rindoshlik odatiy hol, lekin
        // 1.5 dan oshgani odatda kiritishdagi xato (yoki hisobga olinmagan
        // ikkinchi ish) — HR buni ko'rishi kerak.
        var existingRate = isNew
            ? 0m
            : await _employments.GetTotalActiveRateAsync(employee.Id, cancellationToken).ConfigureAwait(false);

        var totalRate = existingRate + employment.Rate;
        if (totalRate > MaxTotalRate)
        {
            warnings.Add(HrWarning.RateExceeded(totalRate));
        }

        if (isNew)
        {
            _employees.Add(employee);
        }

        _employments.Add(employment);

        // ── Sinov muddati AVTOMATIK va ISTISNOSIZ ──
        //
        // HR uni qo'lda ochishni unutardi va keyin «kim sinovda» degan
        // savolga javob yo'q edi. Endi u ishga qabul bilan bir tranzaksiyada
        // tug'iladi.
        //
        // Sinovsiz qabul yo'li YOPILDI. Ilgari bu yerda ikkinchi tarmoq bor
        // edi: `probationDays == 0` bo'lsa sinov yozuvi umuman ochilmasdi va
        // munosabat darhol «faol» bo'lardi. Natijada yangi xodim `/sinov`
        // ro'yxatida ko'rinmasdi, uni hech kim baholamasdi va «hamma sinovdan
        // o'tadi» qoidasi jimgina buzilardi. Endi nol yoki manfiy kun domendan
        // «sinov.majburiy» xatosi bo'lib qaytadi — tarmoq emas, xato.
        //
        // NEGA bu yerda qayta tekshirilmaydi: chegara `Probation` ning O'ZIDA
        // turibdi. Shu shartni bu yerda takrorlasak, ikkita joyda ikkita
        // haqiqat paydo bo'lardi va biri o'zgarganda ikkinchisi eskirardi.
        var probationDays = command.ProbationDays ?? PositionProbationDays(position) ?? DefaultProbationDays;

        var started = Probation.Start(employment.Id, hireDate, probationDays);
        if (started.IsFailure)
        {
            return started.Error;
        }

        var probation = started.Value;
        _probations.Add(probation);

        // ── Ketish belgisi ishga qabul bilan yopiladi ──
        //
        // Rezerv — «hozir olinmadi, lekin kerak bo'lsa chaqirsa bo'ladi».
        // Odam chaqirildi va keldi: belgi bajarildi, uni ochiq qoldirish
        // «rezerv» ro'yxatiga ishlab turgan xodimni qo'yib qo'yish demak.
        // Bo'shatishdagi bilan bir xil naqsh, faqat teskari yo'nalishda.
        //
        // Qora ro'yxat bu yerga YETIB KELMAYDI: u yuqorida amalni butunlay
        // to'xtatadi (`EmployeeErrors.Blacklisted`). Shart baribir tur
        // bo'yicha yozilgan, holat bo'yicha emas — «qora ro'yxatdagi odam
        // shu yerga tushmaydi» degan taxminni kodning ikkinchi joyiga
        // ko'chirish keyingi o'zgarishda jimgina buzilardi.
        var releasedFlags = await CloseDepartureFlagsAsync(employee.Id, hireDate, cancellationToken)
            .ConfigureAwait(false);

        if (releasedFlags.IsFailure)
        {
            return releasedFlags.Error;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<PositionDutyDto> duties = position is null
            ? []
            : PositionDefinition.ToDuties(position);

        var dto = await _mapper.ToDtoAsync(employment, probation, cancellationToken).ConfigureAwait(false);

        return Result.Success(new HireResultDto(dto, duties, warnings));
    }

    /// <summary>Ishdan ketishda qo'yilgan belgilarni ishga qabul bilan yopadi.</summary>
    /// <remarks>
    /// <para>
    /// Yozuv O'CHIRILMAYDI — muddati yopiladi va sabab yozib qo'yiladi:
    /// «bu odam falon sanagacha rezervda edi» degan fakt qayta ishga
    /// olinganidan keyin ham to'g'ri qoladi
    /// (<c>EmployeeFlag.Supersede</c> izohi).
    /// </para>
    /// <para>
    /// Sanalar bilan ishlash bo'shatishdagi bilan AYNAN bir xil, sabab ham
    /// bir xil — <c>TerminateEmploymentCommandHandler.CloseEmployedFlagsAsync</c>
    /// izohiga qarang.
    /// </para>
    /// </remarks>
    private async Task<Result> CloseDepartureFlagsAsync(
        Guid employeeId,
        DateOnly hireDate,
        CancellationToken cancellationToken)
    {
        var today = _clock.Today;
        var asOf = hireDate > today ? hireDate : today;

        var inForce = await _flags
            .GetInForceAsync(employeeId, asOf, cancellationToken)
            .ConfigureAwait(false);

        foreach (var flag in inForce)
        {
            if (!EmployeeFlagStages.IsForDeparture(flag.Type))
            {
                continue;
            }

            var closeAt = hireDate < flag.StartDate ? flag.StartDate : hireDate;

            var result = flag.Supersede(closeAt, "qayta ishga olindi");
            if (result.IsFailure)
            {
                return result.Error;
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Lavozimda belgilangan sinov muddati — faqat ma'noli qiymat.
    /// </summary>
    /// <remarks>
    /// Ma'lumotnoma endi nolni saqlay olmaydi
    /// (<see cref="Position.SetProbationDays"/>), lekin bazada eski qatorlar
    /// qolgan bo'lishi mumkin — Excel'dan ko'chirish paytida nol yozilgan
    /// yozuvlar bor. Bunday qiymat «belgilanmagan» deb qaraladi va standart
    /// 90 kunga tushadi.
    ///
    /// NEGA xato emas: HR hech narsa kiritmagan, aybi ham yo'q — u shunchaki
    /// eski lavozimni tanlagan. Butun ishga qabulni to'xtatib, uni avval
    /// ma'lumotnomani tuzatishga majburlash amaliy ish jarayonini to'sardi.
    /// Ataylab yuborilgan <c>ProbationDays: 0</c> esa boshqa narsa: u aniq
    /// «sinovsiz» degan so'rov va u domendan xato bo'lib qaytadi.
    /// </remarks>
    private static int? PositionProbationDays(Position? position)
        => position?.ProbationDays is { } days && days >= Probation.MinDays ? days : null;

    /// <summary>
    /// Xodimni topadi yoki yaratadi.
    /// </summary>
    /// <remarks>
    /// JSHSHIR bo'yicha qidiruv — dublikatga qarshi yagona ishonchli yo'l.
    /// Excel'da bitta odam uch marta yozilgan holatlar bor edi va uning
    /// mehnat tarixi uch bo'lakka bo'linib ketgandi.
    /// </remarks>
    private async Task<Result<(Employee Employee, bool IsNew)>> ResolveEmployeeAsync(
        HireEmployeeCommand command,
        CancellationToken cancellationToken)
    {
        if (command.EmployeeId is { } employeeId)
        {
            var existing = await _employees.GetByIdAsync(employeeId, cancellationToken).ConfigureAwait(false);
            return existing is null
                ? EmployeeErrors.NotFound
                : Result.Success((existing, false));
        }

        if (command.Person is not { } person)
        {
            return Error.Validation(
                "xodim.malumot_yoq",
                "Xodim ko'rsatilmagan: «employeeId» yoki «person» maydonlaridan biri to'ldirilishi kerak.");
        }

        var pinfl = EmployeeFields.ParsePinfl(person.Pinfl);
        if (pinfl.IsFailure)
        {
            return pinfl.Error;
        }

        if (pinfl.Value is { } value)
        {
            var found = await _employees.FindByPinflAsync(value, cancellationToken).ConfigureAwait(false);
            if (found is not null)
            {
                return Result.Success((found, false));
            }
        }

        var fullName = FullName.Create(person.LastName, person.FirstName, person.MiddleName);
        if (fullName.IsFailure)
        {
            return fullName.Error;
        }

        var created = Employee.Create(fullName.Value, person.BirthDate);
        if (created.IsFailure)
        {
            return created.Error;
        }

        var applied = EmployeeFields.Apply(created.Value, person);
        return applied.IsFailure
            ? applied.Error
            : Result.Success((created.Value, true));
    }
}
