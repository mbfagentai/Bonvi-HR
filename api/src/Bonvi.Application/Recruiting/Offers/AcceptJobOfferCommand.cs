using Bonvi.Application.Employees.Commands;
using Bonvi.Application.Employees.Employments;
using Bonvi.Application.Recruiting.Common;
using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Application.Recruiting.Offers;

/// <summary>
/// Taklif qabul qilindi — nomzod XODIMGA aylanadi.
/// </summary>
/// <remarks>
/// <para>
/// So'rovda faqat ishga qabulning HUJJATIY qismi so'raladi (buyruq raqami,
/// sanasi, stavka). Shaxsiy ma'lumot NOMZODDAN ko'chadi va uni qayta
/// kiritish talab qilinmaydi — voronkaning butun ma'nosi shu.
/// </para>
/// <para>
/// <see cref="Pinfl"/> va <see cref="Passport"/> ATAYLAB shu yerda: nomzodda
/// ular bo'lmaydi (u hujjat topshirmagan), xodimda esa JSHSHIR — asosiy
/// identifikator. Ishga qabul kuni nomzod hujjatlarini keltiradi va HR
/// ularni aynan shu formada kiritadi. Bo'sh qoldirilsa ham qabul o'tadi:
/// Excel'da ham 540 xodimdan 3 tasida JSHSHIR yo'q edi.
/// </para>
/// </remarks>
/// <param name="OfferId">Taklif.</param>
/// <param name="RespondedAt">Javob sanasi. Bo'sh — bugun.</param>
/// <param name="ResponseNote">Javob izohi.</param>
/// <param name="HireDate">Ishga qabul sanasi. Bo'sh — taklifdagi ish boshlash sanasi.</param>
/// <param name="OrderNo">Ishga qabul buyrug'ining raqami.</param>
/// <param name="OrderDate">Buyruq sanasi.</param>
/// <param name="Pinfl">JSHSHIR — ishga qabul kuni kiritiladi.</param>
/// <param name="Passport">Pasport seriyasi va raqami.</param>
/// <param name="EmployeeId">
/// Mavjud xodim kartasi — QAYTA ishga olishda. Bo'sh bo'lsa, JSHSHIR
/// bo'yicha qidiriladi va topilmasa yangi karta ochiladi (ishga qabulning
/// odatiy qoidasi, <c>HireEmployeeCommandHandler</c> izohiga qarang).
/// </param>
/// <param name="Rate">Stavka: 1.0 — to'liq, 0.5 — o'rindoshlik.</param>
/// <param name="IsPrimary">Asosiy ish joyimi.</param>
/// <param name="ResponsibleEmployeeId">Biriktirilgan xodim (mentor).</param>
/// <param name="ProbationDays">
/// Sinov muddati (kun). Bo'sh — lavozimdagi qiymat, u ham bo'lmasa 90 kun.
/// NOL yoki manfiy qiymat XATO: sinovsiz qabul yo'q.
/// </param>
public sealed record AcceptJobOfferCommand(
    Guid OfferId,
    DateOnly? RespondedAt = null,
    string? ResponseNote = null,
    DateOnly? HireDate = null,
    string? OrderNo = null,
    DateOnly? OrderDate = null,
    string? Pinfl = null,
    string? Passport = null,
    Guid? EmployeeId = null,
    decimal? Rate = null,
    bool? IsPrimary = null,
    Guid? ResponsibleEmployeeId = null,
    int? ProbationDays = null);

/// <summary>
/// Voronkaning oxirgi qadami: taklif qabul qilinadi va nomzod xodimga
/// aylanadi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega ishga qabul QAYTA YOZILMAYDI.</b> Bu handler o'zi xodim
/// yaratmaydi — u 1-bosqichning MAVJUD <see cref="HireEmployeeCommand"/>
/// oqimini chaqiradi. Sabab: o'sha oqim ichida qimmatga tushgan qoidalar
/// bor — JSHSHIR bo'yicha dublikat qidiruvi, qora ro'yxat blokirovkasi,
/// stavka chegarasi ogohlantirishi va eng muhimi MAJBURIY sinov muddati.
/// Rekruting o'z «qabul» yo'lini yozganida, bu qoidalarning bittasi ham
/// ishlamasdi va yangi xodim <c>/sinov</c> ro'yxatiga umuman tushmasdi.
/// </para>
/// <para>
/// <b>Nega hammasi BITTA tranzaksiyada.</b> Taklif va nomzod AVVAL
/// o'zgartiriladi, ishga qabul esa OXIRIDA chaqiriladi. Ikkalasi ham bir xil
/// <c>DbContext</c> ni ishlatadi (so'rov doirasida bitta nusxa), ya'ni
/// ishga qabulning yakuniy <c>SaveChangesAsync</c> chaqiruvi UCHALA
/// o'zgarishni birga yozadi: taklif «qabul qilindi», nomzod «ishga
/// olingan», xodim va uning sinov muddati yaratilgan. Ishga qabul rad
/// etilsa (masalan xodim qora ro'yxatda), hech narsa saqlanmaydi —
/// <c>SaveChanges</c> umuman chaqirilmaydi va o'zgarishlar so'rov bilan
/// birga yo'qoladi.
/// </para>
/// <para>
/// <b>Nega tartib TESKARI emas.</b> Avval ishga qabul qilib, keyin nomzodni
/// yopish tabiiyroq ko'rinadi, lekin u IKKI marta saqlashni talab qilardi
/// (ishga qabul o'zi saqlaydi). Ikkinchisi yiqilsa, xodim yaratilgan,
/// nomzod esa voronkada faol bo'lib qolardi — va takroriy urinish IKKINCHI
/// xodim kartasini yaratardi.
/// </para>
/// <para>
/// <b>Takrorga qarshi ikkita to'siq.</b> Birinchisi — nomzodning holati
/// (<c>Candidate.MarkHired</c> ikkinchi marta chaqirilsa xato beradi).
/// Ikkinchisi — ishga qabul hodisasi bo'yicha qidiruv
/// (<c>IEmploymentRepository.FindByCandidateAsync</c>): u ma'lumotning
/// O'ZIDAN kelib chiqadi va holat qandaydir sabab bilan yozilmay qolgan
/// holatda ham ishlaydi.
/// </para>
/// </remarks>
public sealed class AcceptJobOfferCommandHandler
{
    private readonly IJobOfferRepository _offers;
    private readonly ICandidateRepository _candidates;
    private readonly IVacancyRepository _vacancies;
    private readonly IEmploymentRepository _employments;
    private readonly HireEmployeeCommandHandler _hire;
    private readonly RecruitingMapper _mapper;
    private readonly IClock _clock;

    public AcceptJobOfferCommandHandler(
        IJobOfferRepository offers,
        ICandidateRepository candidates,
        IVacancyRepository vacancies,
        IEmploymentRepository employments,
        HireEmployeeCommandHandler hire,
        RecruitingMapper mapper,
        IClock clock)
    {
        _offers = offers;
        _candidates = candidates;
        _vacancies = vacancies;
        _employments = employments;
        _hire = hire;
        _mapper = mapper;
        _clock = clock;
    }

    public async Task<Result<CandidateHireResultDto>> HandleAsync(
        AcceptJobOfferCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var offer = await _offers.GetByIdAsync(command.OfferId, cancellationToken).ConfigureAwait(false);
        if (offer is null)
        {
            return RecruitingErrors.OfferNotFound;
        }

        var candidate = await _candidates
            .GetWithStagesAsync(offer.CandidateId, cancellationToken)
            .ConfigureAwait(false);

        if (candidate is null)
        {
            return RecruitingErrors.CandidateNotFound;
        }

        var guarded = await GuardNotHiredAsync(candidate, cancellationToken).ConfigureAwait(false);
        if (guarded.IsFailure)
        {
            return guarded.Error;
        }

        // Taklif AYNAN qaysi o'ringa berilgan bo'lsa, o'sha o'ringa qabul
        // qilinadi: nomzod keyinroq boshqa vakansiyaga ko'chirilgan bo'lishi
        // mumkin, lekin va'da qilingan lavozim o'zgarmaydi.
        var vacancy = await _vacancies.GetByIdAsync(offer.VacancyId, cancellationToken).ConfigureAwait(false);
        if (vacancy is null)
        {
            return RecruitingErrors.VacancyNotFound;
        }

        var respondedAt = command.RespondedAt ?? _clock.Today;

        var accepted = offer.Accept(respondedAt, command.ResponseNote);
        if (accepted.IsFailure)
        {
            return accepted.Error;
        }

        var marked = candidate.MarkHired(respondedAt);
        if (marked.IsFailure)
        {
            return marked.Error;
        }

        var hired = await _hire
            .HandleAsync(BuildHireCommand(command, offer, candidate, vacancy), cancellationToken)
            .ConfigureAwait(false);

        if (hired.IsFailure)
        {
            // Hech narsa saqlanmadi: `SaveChangesAsync` faqat ishga qabulning
            // oxirida chaqiriladi va u yerga yetib kelinmadi. Taklif va
            // nomzod xotirada o'zgargan, lekin bazaga yozilmagan.
            return hired.Error;
        }

        var offerDto = await _mapper.ToDtoAsync(offer, cancellationToken).ConfigureAwait(false);
        var candidateDto = await _mapper.ToDtoAsync(candidate, cancellationToken).ConfigureAwait(false);

        return Result.Success(new CandidateHireResultDto(offerDto, candidateDto, hired.Value));
    }

    /// <summary>
    /// Nomzodning ma'lumotini ishga qabul buyrug'iga ko'chiradi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ko'chadigan maydonlar: F.I.Sh., tug'ilgan sana, telefon, manzil,
    /// mutaxassislik. Ular xodim kartasida AYNAN shu nomlar bilan turadi va
    /// shu sababli moslashtirish kerak emas.
    /// </para>
    /// <para>
    /// Lavozim, bo'lim va firma VAKANSIYADAN, oklad va ish boshlash sanasi
    /// TAKLIFDAN keladi. Ya'ni ishga qabul formasida HR faqat buyruq
    /// raqamini va hujjat ma'lumotlarini kiritadi — qolgani voronkada
    /// allaqachon kelishilgan.
    /// </para>
    /// </remarks>
    private static HireEmployeeCommand BuildHireCommand(
        AcceptJobOfferCommand command,
        JobOffer offer,
        Candidate candidate,
        Vacancy vacancy)
    {
        var person = new SaveEmployeeCommand(
            candidate.FullName.LastName,
            candidate.FullName.FirstName,
            candidate.FullName.MiddleName,
            candidate.BirthDate,
            Gender: null,
            command.Pinfl,
            command.Passport,
            candidate.Phone?.Value,
            SecondPhone: null,
            candidate.Address,
            candidate.Speciality);

        return new HireEmployeeCommand(
            vacancy.LegalEntityId,
            command.EmployeeId,
            person,
            vacancy.DepartmentId,
            vacancy.PositionId,
            command.HireDate ?? offer.StartDate,
            command.OrderNo,
            command.OrderDate,
            offer.Salary.Amount,
            offer.Salary.Currency,
            command.Rate,
            command.IsPrimary,
            command.ResponsibleEmployeeId,
            command.ProbationDays,
            candidate.Id);
    }

    /// <summary>Nomzod allaqachon ishga olinmaganini tekshiradi.</summary>
    private async Task<Result> GuardNotHiredAsync(Candidate candidate, CancellationToken cancellationToken)
    {
        if (candidate.Status == CandidateStatus.Hired)
        {
            return RecruitingErrors.AlreadyHired;
        }

        var existing = await _employments
            .FindByCandidateAsync(candidate.Id, cancellationToken)
            .ConfigureAwait(false);

        return existing is null ? Result.Success() : RecruitingErrors.AlreadyHired;
    }
}
