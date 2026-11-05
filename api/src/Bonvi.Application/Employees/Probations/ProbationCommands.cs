using Bonvi.Application.Abstractions;
using Bonvi.Application.Catalog.Common;
using Bonvi.Application.Employees.Common;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.Employees.Employments;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.Employees.Probations;

/// <summary>
/// Sinov muddatini yakunlaydi va — o'tgan bo'lsa — xodimni bir amalda
/// doimiy holatga o'tkazadi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega bitta amal, ikkita emas.</b> HR talabi so'zma-so'z: «sinovdan
/// o'tgan bo'lsa, bir bosishda xodimlarga o'tkazib, unga kerakli
/// hamma narsani — lavozim, oklad, bo'lim — qo'yib yuborsak». Ilgari bu
/// ikki qadam edi: avval sinov yopiladi, keyin xodim kartasidan
/// «lavozim o'zgarishi» yoziladi. Ikkinchi qadam unutilardi va odam
/// doimiy holatga o'tgani bilan eski, «stajyor» lavozimida qolib ketardi.
/// </para>
/// <para>
/// <b>Maydonlar ixtiyoriy va bo'sh qoldirilgani «o'zgarmadi» degani</b> —
/// <see cref="Employments.TransferEmployeeCommand"/> bilan bir xil qoida:
/// forma avvalgi qiymatlarni oldindan to'ldiradi va HR faqat o'zgargan
/// qatorga tegadi.
/// </para>
/// <para>
/// <b>«O'tmadi» va «o'zi ketdi» — shu amalning o'zida ishdan bo'shatish</b>
/// (egasining qarori, 2026-10-14). Ilgari natija yozilardi, xodim esa
/// «sinovda» holatida qolib ketardi: «Xodimlar» ro'yxatida ham, kartada ham
/// sinovdagi odam bo'lib turardi va bo'shatishni HR alohida eslashi kerak
/// edi. Endi <see cref="Termination"/> majburiy va munosabat shu
/// tranzaksiyada yopiladi.
/// </para>
/// <para>
/// <b>Tarixda ikki hodisa qoladi</b>, bitta emas: «sinov natijasi» va
/// «lavozim o'zgarishi» (yoki «oklad o'zgarishi»). Ular boshqa-boshqa
/// faktlar va hisobotda ham alohida sanaladi; bittaga qo'shib yuborsak,
/// «sinovdan keyin okladi qancha oshdi» degan savolga javob yo'qolardi.
/// </para>
/// </remarks>
/// <param name="ProbationId">Sinov muddati identifikatori.</param>
/// <param name="Status">Natija: o'tdi / o'tmadi / erta ketdi. «Bekor qilindi» qo'lda tanlanmaydi.</param>
/// <param name="ResolvedAt">Yakunlangan sana. Bo'sh bo'lsa — bugun.</param>
/// <param name="Conclusion">Xulosa matni.</param>
/// <param name="ReviewerEmployeeId">Xulosa bergan mas'ul.</param>
/// <param name="PositionId">Yangi lavozim. Faqat <c>Passed</c> bilan.</param>
/// <param name="DepartmentId">Yangi bo'lim. Faqat <c>Passed</c> bilan.</param>
/// <param name="SalaryAmount">Yangi oklad. Faqat <c>Passed</c> bilan.</param>
/// <param name="SalaryCurrency">Oklad valyutasi. Bo'sh bo'lsa — so'm.</param>
/// <param name="Rate">Yangi stavka (1.0 — to'liq, 0.5 — yarim).</param>
/// <param name="OrderNo">Buyruq raqami.</param>
/// <param name="OrderDate">Buyruq sanasi.</param>
/// <param name="Termination">
/// Ishdan bo'shatish ma'lumoti — faqat «o'tmadi» / «o'zi ketdi» bilan va ular
/// uchun MAJBURIY (munosabat allaqachon yopilgan bo'lmasa).
/// </param>
/// <param name="ConfirmTermination">
/// Xodim qora ro'yxatda yoki nazoratda bo'lsa ham bo'shatish — oddiy
/// bo'shatishdagi <c>confirm</c> bilan aynan bir xil.
/// </param>
public sealed record ResolveProbationCommand(
    Guid ProbationId,
    ProbationStatus Status,
    DateOnly? ResolvedAt = null,
    string? Conclusion = null,
    Guid? ReviewerEmployeeId = null,
    Guid? PositionId = null,
    Guid? DepartmentId = null,
    decimal? SalaryAmount = null,
    string? SalaryCurrency = null,
    decimal? Rate = null,
    string? OrderNo = null,
    DateOnly? OrderDate = null,
    ProbationTermination? Termination = null,
    bool ConfirmTermination = false)
{
    /// <summary>Natija xodimning ketishini anglatadimi.</summary>
    public bool IsLeaving => Status is ProbationStatus.Failed or ProbationStatus.LeftEarly;

    /// <summary>Doimiy holatga o'tkazishda biror maydon to'ldirilganmi.</summary>
    /// <remarks>
    /// Ular faqat <see cref="ProbationStatus.Passed"/> bilan ma'noga ega:
    /// sinovdan o'tmagan odamga yangi lavozim tayinlash — kiritish xatosi,
    /// va uni jimgina bajarish o'rniga to'xtatib, HR ga aytish kerak.
    /// </remarks>
    public bool HasAppointment =>
        PositionId is not null
        || DepartmentId is not null
        || SalaryAmount is not null
        || Rate is not null;
}

/// <summary>
/// Sinov yakunidagi ishdan bo'shatish — <see cref="TerminateEmploymentCommand"/>
/// maydonlarining o'zi.
/// </summary>
/// <param name="Date">Ketish sanasi. Bo'sh bo'lsa — sinov yakunlangan sana.</param>
/// <param name="ReasonId">Ketish sababi (ma'lumotnoma).</param>
/// <param name="OrderNo">Bo'shatish buyrug'i raqami.</param>
/// <param name="OrderDate">Bo'shatish buyrug'i sanasi.</param>
/// <param name="Note">Izoh. Bo'sh bo'lsa — sinov xulosasi.</param>
public sealed record ProbationTermination(
    DateOnly? Date = null,
    Guid? ReasonId = null,
    string? OrderNo = null,
    DateOnly? OrderDate = null,
    string? Note = null);

/// <summary>Sinov muddatini uzaytiradi.</summary>
public sealed record ExtendProbationCommand(Guid ProbationId, DateOnly NewEndDate);

/// <summary>
/// Sinov muddatini yakunlaydi va natijani mehnat munosabatiga o'tkazadi.
/// </summary>
/// <remarks>
/// Sinovdan o'tgan xodimning munosabati <c>Probation</c> dan <c>Active</c> ga
/// o'tishi KERAK — aks holda ro'yxatda u abadiy «sinovda» bo'lib qolardi va
/// aynan shu Excel'dagi 411 ta «Тугатилган» yozuvining sababi edi.
/// </remarks>
public sealed class ResolveProbationCommandHandler
{
    private readonly IProbationRepository _probations;
    private readonly IEmploymentRepository _employments;
    private readonly IPositionRepository _positions;
    private readonly IDepartmentRepository _departments;
    private readonly EmployeeMapper _mapper;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TerminateEmploymentCommandHandler _terminate;

    public ResolveProbationCommandHandler(
        IProbationRepository probations,
        IEmploymentRepository employments,
        IPositionRepository positions,
        IDepartmentRepository departments,
        EmployeeMapper mapper,
        ICurrentUser currentUser,
        IClock clock,
        IUnitOfWork unitOfWork,
        TerminateEmploymentCommandHandler terminate)
    {
        _probations = probations;
        _employments = employments;
        _positions = positions;
        _departments = departments;
        _mapper = mapper;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _terminate = terminate;
    }

    public async Task<Result<ProbationDto>> HandleAsync(
        ResolveProbationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Pul qoidasi — hamma narsadan OLDIN: sinov yakunidagi yangi oklad
        // ham `employment.salary` ni talab qiladi. Sinovni yakunlashning
        // o'zi (`employment.probation.complete`) okladga huquq bermaydi.
        var salaryAllowed = _currentUser.EnsureCanSendSalary(command.SalaryAmount is not null);
        if (salaryAllowed.IsFailure)
        {
            return salaryAllowed.Error;
        }

        // Ko'chirish qoidasi ham xuddi shunday: sinov yakunidagi yangi lavozim
        // yoki bo'lim — bu ko'chirish va u `employment.transfer` ni talab qiladi.
        // Stavka bu yerga kirmaydi: u shartnoma sharti, alohida amali yo'q
        // (xuddi ishga qabuldagi kabi).
        var moves = command.DepartmentId is not null || command.PositionId is not null;
        if (moves && !_currentUser.HasPermission(Permissions.EmploymentTransfer))
        {
            return EmployeeErrors.TransferPermissionRequired;
        }

        // Tayinlash maydonlari faqat «o'tdi» bilan ma'noga ega. Sinovdan
        // o'tmagan odamga yangi lavozim yozish — kiritish xatosi, va uni
        // jimgina bajarish HR ni butunlay boshqa natijaga olib borardi.
        if (command.HasAppointment && command.Status != ProbationStatus.Passed)
        {
            return Error.Validation(
                "sinov.tayinlash_otmagan",
                "Lavozim, bo'lim, oklad va stavkani faqat sinovdan O'TGAN xodimga tayinlash mumkin.");
        }

        // «Bekor qilindi» qo'lda tanlanmaydi (egasi, 2026-10-14). Prod'da u
        // bir marta ishlatilgan — takror kiritilgan ishga qabulni «bekor
        // qilish» uchun — va natijada xodim sinovsiz, lekin «sinovda»
        // holatida ikki marta qolib ketgan. Sinov xato qo'yilgan bo'lsa —
        // «o'tdi», qabulning o'zi xato bo'lsa — bu ma'lumot tuzatish.
        if (command.Status == ProbationStatus.Cancelled)
        {
            return Error.Validation(
                "sinov.bekor_qilinmaydi",
                "Sinovni «bekor qilindi» deb yakunlab bo'lmaydi: «o'tdi», «o'tmadi» yoki «o'zi ketdi» ni tanlang.");
        }

        // Bo'shatish faqat ketish natijasi bilan — «o'tdi» bilan kelgan
        // bo'shatish ma'lumoti kiritish xatosi (tayinlash qoidasining
        // teskarisi).
        if (command.Termination is not null && !command.IsLeaving)
        {
            return Error.Validation(
                "sinov.bosatish_otganga_yoq",
                "Ishdan bo'shatish faqat «o'tmadi» yoki «o'zi ketdi» natijasi bilan yuboriladi.");
        }

        // «O'tmadi» / «o'zi ketdi» = ishdan bo'shatish, va uning ruxsati
        // alohida (xuddi oklad va ko'chirishdagi kabi). Tekshiruv hamma
        // narsadan OLDIN — sinov yopilib, bo'shatish ruxsatda to'xtasa,
        // HR «nima saqlandi» degan holatda qolardi.
        if (command.IsLeaving && !_currentUser.HasPermission(Permissions.EmploymentTerminate))
        {
            return EmployeeErrors.TerminatePermissionRequired;
        }

        // Ma'lumotnoma yozuvlari OLDIN tekshiriladi — sinov yopilib,
        // keyin lavozim topilmay qolsa, tranzaksiya bekor bo'lardi va HR
        // «nima saqlandi, nima yo'q» degan holatda qolardi.
        if (command.DepartmentId is { } departmentId
            && !await _departments.ExistsAsync(departmentId, cancellationToken).ConfigureAwait(false))
        {
            return CatalogErrors.DepartmentNotFound;
        }

        if (command.PositionId is { } positionId
            && !await _positions.ExistsAsync(positionId, cancellationToken).ConfigureAwait(false))
        {
            return CatalogErrors.PositionNotFound;
        }

        Money? salary = null;
        if (command.SalaryAmount is { } amount)
        {
            var money = Money.Create(amount, command.SalaryCurrency ?? Money.Uzs);
            if (money.IsFailure)
            {
                return money.Error;
            }

            salary = money.Value;
        }

        var probation = await _probations
            .GetByIdAsync(command.ProbationId, cancellationToken)
            .ConfigureAwait(false);

        if (probation is null)
        {
            return EmployeeErrors.ProbationNotFound;
        }

        // Ketishda sinov ketish kuni yopiladi — «o'tmadi» qarori va
        // bo'shatish bitta voqea, ikki sana ularni ajratib yuborardi.
        var at = command.ResolvedAt ?? command.Termination?.Date ?? _clock.Today;

        var resolved = probation.Resolve(command.Status, at, command.Conclusion, command.ReviewerEmployeeId);
        if (resolved.IsFailure)
        {
            return resolved.Error;
        }

        if (command.IsLeaving)
        {
            return await TerminateAsync(command, probation, at, cancellationToken).ConfigureAwait(false);
        }

        if (command.Status == ProbationStatus.Passed)
        {
            var employment = await _employments
                .GetWithEventsAsync(probation.EmploymentId, cancellationToken)
                .ConfigureAwait(false);

            // Munosabat topilmasa ham sinov natijasi saqlanadi: yozuv
            // yo'qolgandan ko'ra, nomukammal bog'lanish yaxshiroq.
            if (employment is { Status: EmploymentStatus.Probation })
            {
                var completed = employment.CompleteProbation(at, command.Conclusion);
                if (completed.IsFailure)
                {
                    return completed.Error;
                }

                var appointed = Appoint(employment, command, salary, at);
                if (appointed.IsFailure)
                {
                    return appointed.Error;
                }
            }
            else if (command.HasAppointment)
            {
                // Munosabat yo'q yoki allaqachon yopilgan — tayinlashni
                // qo'yadigan joy yo'q. Jimgina tashlab yuborish eng yomon
                // variant: HR oklad qo'ygan deb o'ylab ketardi.
                return Error.Conflict(
                    "sinov.tayinlash_joyi_yoq",
                    "Bu sinov muddatiga bog'liq faol mehnat munosabati topilmadi — "
                        + "lavozim va oklad tayinlanmadi.");
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(_mapper.ToDto(probation));
    }

    /// <summary>
    /// «O'tmadi» / «o'zi ketdi» — munosabatni xuddi shu tranzaksiyada yopadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega oddiy bo'shatish chaqiriladi, nusxa yozilmaydi.</b> Bo'shatishda
    /// qimmatga tushgan qoidalar bor: qora ro'yxat / nazorat ogohlantirishi
    /// va tasdig'i, ishlab turganlar belgilarini yopish, sana tekshiruvi.
    /// Ikkinchi nusxa ularning birini albatta unutardi
    /// (<c>HireCandidateCommand</c> dagi naqsh — u ham ishga qabulni
    /// chaqiradi).
    /// </para>
    /// <para>
    /// <b>Nega bitta tranzaksiya.</b> Ikkalasi bir xil <c>DbContext</c> da:
    /// sinov xotirada yopilgan, bo'shatishning yakuniy
    /// <c>SaveChangesAsync</c> i HAR IKKI o'zgarishni birga yozadi. Bo'shatish
    /// rad etilsa (sana noto'g'ri, xodim qora ro'yxatda va tasdiqlanmagan) —
    /// hech narsa saqlanmaydi, sinov «sinovda» qoladi. Bo'shatish sinov
    /// yozuvini qayta o'qiganda EF o'sha kuzatilayotgan nusxani qaytaradi —
    /// u endi «o'tmadi» va «o'zi ketdi» bilan ustidan yozilmaydi.
    /// </para>
    /// <para>
    /// Munosabat allaqachon yopilgan bo'lsa (bo'shatish oldin qilingan,
    /// sinov esa ochiq qolgan), qayta bo'shatish yo'q — faqat natija yoziladi.
    /// </para>
    /// </remarks>
    private async Task<Result<ProbationDto>> TerminateAsync(
        ResolveProbationCommand command,
        Probation probation,
        DateOnly at,
        CancellationToken cancellationToken)
    {
        var employment = await _employments
            .GetWithEventsAsync(probation.EmploymentId, cancellationToken)
            .ConfigureAwait(false);

        if (employment is null or { Status: EmploymentStatus.Terminated })
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return Result.Success(_mapper.ToDto(probation));
        }

        if (command.Termination is not { } termination)
        {
            return Error.Validation(
                "sinov.bosatish_kerak",
                "Sinovdan o'tmagan yoki o'zi ketgan xodim shu yerning o'zida ishdan bo'shatiladi — ketish sanasini kiriting.");
        }

        var terminated = await _terminate
            .HandleAsync(
                new TerminateEmploymentCommand(
                    employment.Id,
                    termination.Date ?? at,
                    termination.ReasonId,
                    termination.OrderNo,
                    termination.OrderDate,
                    termination.Note ?? command.Conclusion,
                    command.ConfirmTermination),
                cancellationToken)
            .ConfigureAwait(false);

        // Rad etilsa hech narsa saqlanmagan: sinov faqat xotirada yopilgan.
        return terminated.IsFailure
            ? terminated.Error
            : Result.Success(_mapper.ToDto(probation) with { Warnings = terminated.Value.Warnings });
    }

    /// <summary>
    /// Doimiy holatga o'tgan xodimga yangi shartlarni qo'yadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega ikki xil hodisa.</b> Lavozim yoki bo'lim o'zgarsa —
    /// <c>Transfer</c> (u okladni ham o'z ichiga oladi va eski/yangi
    /// qiymatni yonma-yon yozadi). Faqat oklad o'zgarsa — <c>SetSalary</c>.
    /// Aks holda oddiy oklad oshishi tarixda «lavozim o'zgarishi» bo'lib
    /// ko'rinardi va «kim ko'tarildi» degan hisobot yolg'on chiqardi.
    /// </para>
    /// <para>
    /// <b>Stavka hodisa yozmaydi</b> — u shartnoma sharti, hodisa emas.
    /// Uning o'zgarishi mehnat yo'li lentasida alohida qator bo'lib
    /// turishi shovqin bo'lardi.
    /// </para>
    /// </remarks>
    private static Result Appoint(
        Employment employment,
        ResolveProbationCommand command,
        Money? salary,
        DateOnly at)
    {
        if (command.Rate is { } rate)
        {
            var applied = employment.SetRate(rate);
            if (applied.IsFailure)
            {
                return applied.Error;
            }
        }

        var moved = command.DepartmentId is not null || command.PositionId is not null;

        if (moved)
        {
            return employment.Transfer(
                at,
                command.DepartmentId,
                command.PositionId,
                salary,
                command.OrderNo,
                command.OrderDate,
                command.Conclusion);
        }

        if (salary is not null)
        {
            return employment.SetSalary(salary, at, command.OrderNo, command.OrderDate);
        }

        return Result.Success();
    }
}

/// <summary>Sinov muddatini uzaytiradi (umumiy chegara — 180 kun).</summary>
public sealed class ExtendProbationCommandHandler
{
    private readonly IProbationRepository _probations;
    private readonly EmployeeMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public ExtendProbationCommandHandler(
        IProbationRepository probations,
        EmployeeMapper mapper,
        IUnitOfWork unitOfWork)
    {
        _probations = probations;
        _mapper = mapper;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ProbationDto>> HandleAsync(
        ExtendProbationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var probation = await _probations
            .GetByIdAsync(command.ProbationId, cancellationToken)
            .ConfigureAwait(false);

        if (probation is null)
        {
            return EmployeeErrors.ProbationNotFound;
        }

        var extended = probation.Extend(command.NewEndDate);
        if (extended.IsFailure)
        {
            return extended.Error;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(_mapper.ToDto(probation));
    }
}
