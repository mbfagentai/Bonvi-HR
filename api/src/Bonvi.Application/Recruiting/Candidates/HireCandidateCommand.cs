using Bonvi.Application.Employees.Commands;
using Bonvi.Application.Employees.Employments;
using Bonvi.Application.Recruiting.Common;
using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Application.Recruiting.Candidates;

/// <summary>
/// Nomzod XODIMGA aylanadi — to'g'ridan-to'g'ri, oraliq qadamsiz.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega bu yo'l kerak.</b> Foydalanuvchi qarori (2026-09-07):
/// nomzodning uchta yakuni bor — «xodim sifatida olindi», «rezerv» va
/// «qabul qilinmadi». Ularning birinchisi hech qanday oraliq qadamni
/// talab qilmaydi: amalda HR nomzod bilan gaplashib, kelishib, o'sha
/// kuni hujjatga o'tkazadi. Ilgari bu yo'l rasmiy ish taklifi orqali
/// o'tardi — u ortiqcha bo'lgani uchun butunlay olib tashlandi
/// (<c>docs/SPEC-nomzod-yakuni.md</c>, 3-bo'lim).
/// </para>
/// <para>
/// <b>So'rovda faqat ishga qabulning HUJJATIY qismi bor.</b> Shaxsiy
/// ma'lumot NOMZODDAN ko'chadi va qayta kiritish talab qilinmaydi —
/// voronkaning butun ma'nosi shu. <see cref="Pinfl"/> va
/// <see cref="Passport"/> esa ATAYLAB shu yerda: nomzodda ular
/// bo'lmaydi (u hujjat topshirmagan), xodimda esa JSHSHIR — asosiy
/// identifikator va dublikat qidiruvi aynan unga tayanadi.
/// </para>
/// <para>
/// <b>Majburiy maydon IKKITA: firma va lavozim.</b> Qolganining zaxirasi
/// bor: oklad — yo'q bo'lsa munosabat oklad yozuvisiz ochiladi, ishga
/// qabul sanasi — bugun, sinov muddati — lavozimdagi qiymat yoki 90 kun.
/// </para>
/// </remarks>
/// <param name="CandidateId">Qaysi nomzod ishga olinmoqda.</param>
/// <param name="LegalEntityId">
/// Qaysi FIRMAGA rasmiylashtiriladi — <c>legal_entities</c> ma'lumotnomasidan.
/// MAJBURIY: <c>HireEmployeeCommand.LegalEntityId</c> nullable emas, ya'ni
/// ishga qabul usiz umuman bajarilmaydi.
/// </param>
/// <param name="PositionId">
/// Qaysi LAVOZIMGA — <c>positions</c> ma'lumotnomasidan. MAJBURIY
/// (sababi <c>RecruitingErrors.CandidateHirePositionMissing</c> izohida).
/// </param>
/// <param name="DepartmentId">
/// Qaysi BO'LIMGA — <c>departments</c> ma'lumotnomasidan. Ixtiyoriy,
/// <c>HireEmployeeCommand</c> dagi bilan bir xil.
/// </param>
/// <param name="HireDate">Ishga qabul sanasi. Bo'sh — bugun.</param>
/// <param name="OrderNo">Ishga qabul buyrug'ining raqami.</param>
/// <param name="OrderDate">Buyruq sanasi.</param>
/// <param name="Pinfl">JSHSHIR — ishga qabul kuni kiritiladi.</param>
/// <param name="Passport">Pasport seriyasi va raqami.</param>
/// <param name="EmployeeId">
/// Mavjud xodim kartasi — QAYTA ishga olishda. Bo'sh bo'lsa, JSHSHIR
/// bo'yicha qidiriladi va topilmasa yangi karta ochiladi (ishga qabulning
/// odatiy qoidasi, <c>HireEmployeeCommandHandler</c> izohiga qarang).
/// </param>
/// <param name="SalaryAmount">
/// Kelishilgan oylik ish haqi. Bo'sh bo'lsa oklad yozuvi umuman
/// ochilmaydi — <c>HireEmployeeCommand</c> dagi bilan bir xil qoida
/// (HR formasi ham okladsiz qabulga ruxsat beradi).
/// </param>
/// <param name="SalaryCurrency">Valyuta. Bo'sh — UZS.</param>
/// <param name="Rate">Stavka: 1.0 — to'liq, 0.5 — o'rindoshlik.</param>
/// <param name="IsPrimary">Asosiy ish joyimi.</param>
/// <param name="ResponsibleEmployeeId">Biriktirilgan xodim (mentor).</param>
/// <param name="ProbationDays">
/// Sinov muddati (kun). Bo'sh — lavozimdagi qiymat, u ham bo'lmasa 90 kun.
/// NOL yoki manfiy qiymat XATO: sinovsiz qabul yo'q.
/// </param>
public sealed record HireCandidateCommand(
    Guid CandidateId,
    Guid LegalEntityId,
    Guid PositionId,
    Guid? DepartmentId = null,
    DateOnly? HireDate = null,
    string? OrderNo = null,
    DateOnly? OrderDate = null,
    string? Pinfl = null,
    string? Passport = null,
    Guid? EmployeeId = null,
    decimal? SalaryAmount = null,
    string? SalaryCurrency = null,
    decimal? Rate = null,
    bool? IsPrimary = null,
    Guid? ResponsibleEmployeeId = null,
    int? ProbationDays = null);

/// <summary>
/// Nomzodni to'g'ridan-to'g'ri xodimga aylantiradi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega ishga qabul QAYTA YOZILMAYDI.</b> Bu handler o'zi xodim
/// yaratmaydi — u 1-bosqichning MAVJUD <see cref="HireEmployeeCommand"/>
/// oqimini chaqiradi. O'sha oqim ichida qimmatga tushgan qoidalar bor: JSHSHIR
/// bo'yicha dublikat qidiruvi, qora ro'yxat blokirovkasi, stavka
/// chegarasi ogohlantirishi va eng muhimi MAJBURIY sinov muddati.
/// Rekruting o'zining ikkinchi «qabul» yo'lini yozganida, bu qoidalarning
/// bittasi ham ishlamasdi va yangi xodim <c>/sinov</c> ro'yxatiga umuman
/// tushmasdi.
/// </para>
/// <para>
/// <b>Nega hammasi BITTA tranzaksiyada.</b> Nomzod AVVAL yopiladi, ishga
/// qabul esa OXIRIDA chaqiriladi. Ikkalasi ham bir xil <c>DbContext</c> ni
/// ishlatadi (so'rov doirasida bitta nusxa), ya'ni ishga qabulning
/// yakuniy <c>SaveChangesAsync</c> chaqiruvi HAR IKKI o'zgarishni birga
/// yozadi: nomzod «ishga olingan», xodim va uning sinov muddati
/// yaratilgan. Ishga qabul rad etilsa (masalan odam qora ro'yxatda),
/// hech narsa saqlanmaydi — <c>SaveChanges</c> umuman chaqirilmaydi va
/// o'zgarishlar so'rov bilan birga yo'qoladi.
/// </para>
/// <para>
/// <b>Nega tartib TESKARI emas.</b> Avval ishga qabul qilib, keyin
/// nomzodni yopish tabiiyroq ko'rinadi, lekin u IKKI marta saqlashni
/// talab qilardi (ishga qabul o'zi saqlaydi). Ikkinchisi yiqilsa, xodim
/// yaratilgan, nomzod esa voronkada faol bo'lib qolardi — va takroriy
/// urinish IKKINCHI xodim kartasini yaratardi.
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
public sealed class HireCandidateCommandHandler
{
    private readonly ICandidateRepository _candidates;
    private readonly IEmploymentRepository _employments;
    private readonly IDepartmentRepository _departments;
    private readonly HireEmployeeCommandHandler _hire;
    private readonly RecruitingMapper _mapper;
    private readonly IClock _clock;

    public HireCandidateCommandHandler(
        ICandidateRepository candidates,
        IEmploymentRepository employments,
        IDepartmentRepository departments,
        HireEmployeeCommandHandler hire,
        RecruitingMapper mapper,
        IClock clock)
    {
        _candidates = candidates;
        _employments = employments;
        _departments = departments;
        _hire = hire;
        _mapper = mapper;
        _clock = clock;
    }

    public async Task<Result<DirectHireResultDto>> HandleAsync(
        HireCandidateCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Shtat birligi AVVAL tekshiriladi: nomzodning holatiga umuman
        // tegilmasdan turib. Tartib muhim — pastda u xotirada
        // o'zgartiriladi va keyin yiqilish «hech narsa saqlanmadi, lekin
        // obyekt allaqachon boshqa holatda» degan holatni yaratardi.
        // Bu yerda esa hech qanday o'zgarish bo'lmagan.
        var placement = await CheckPlacementAsync(command, cancellationToken).ConfigureAwait(false);
        if (placement.IsFailure)
        {
            return placement.Error;
        }

        var candidate = await _candidates
            .GetWithStagesAsync(command.CandidateId, cancellationToken)
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

        /*
          Nomzod voronkadan BUGUN chiqadi, ishga qabul sanasida emas: qaror
          aynan hozir qabul qilinmoqda, ish boshlash esa kelajakda bo'lishi
          mumkin («dushanbadan chiqadi»). Ikkalasini birlashtirish
          `ClosedAt` ga kelajakdagi sanani yozardi va «shu oyda voronkadan
          nechta odam chiqdi» degan hisobot uni umuman ko'rmasdi.
        */
        var marked = candidate.MarkHired(_clock.Today);
        if (marked.IsFailure)
        {
            return marked.Error;
        }

        var hired = await _hire
            .HandleAsync(BuildHireCommand(command, candidate), cancellationToken)
            .ConfigureAwait(false);

        if (hired.IsFailure)
        {
            // Hech narsa saqlanmadi: `SaveChangesAsync` faqat ishga qabulning
            // oxirida chaqiriladi va u yerga yetib kelinmadi. Nomzod
            // xotirada o'zgargan, lekin bazaga yozilmagan.
            return hired.Error;
        }

        var candidateDto = await _mapper.ToDtoAsync(candidate, cancellationToken).ConfigureAwait(false);

        return Result.Success(new DirectHireResultDto(candidateDto, hired.Value));
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
    /// <c>CandidateId</c> buyruqqa qo'shiladi — «bu xodim qayerdan keldi»
    /// degan savolning javobi ishga qabul HODISASIDA saqlanadi
    /// (<c>EmploymentEvent.CandidateId</c>) va u takroriy qabulning
    /// ikkinchi to'sig'i ham bo'lib xizmat qiladi.
    /// </para>
    /// </remarks>
    private static HireEmployeeCommand BuildHireCommand(HireCandidateCommand command, Candidate candidate)
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
            command.LegalEntityId,
            command.EmployeeId,
            person,
            command.DepartmentId,
            command.PositionId,
            command.HireDate,
            command.OrderNo,
            command.OrderDate,
            command.SalaryAmount,
            command.SalaryCurrency,
            command.Rate,
            command.IsPrimary,
            command.ResponsibleEmployeeId,
            command.ProbationDays,
            candidate.Id);
    }

    /// <summary>
    /// Xodim QAYERGA joylashtirilayotgani to'liq ko'rsatilganmi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Firma va lavozim MAJBURIY, bo'lim ixtiyoriy — sabablari
    /// <c>RecruitingErrors.CandidateHireLegalEntityMissing</c> va
    /// <c>CandidateHirePositionMissing</c> izohlarida.
    /// </para>
    /// <para>
    /// <b>Nega faqat «bo'sh emasmi» tekshiriladi, «mavjudmi» emas.</b>
    /// Firma va lavozimning MAVJUDLIGINI ishga qabul oqimining o'zi
    /// tekshiradi va o'zining tanish xatolarini qaytaradi
    /// (<c>firma.topilmadi</c>, <c>lavozim.topilmadi</c>) — ularni bu
    /// yerda takrorlash ikkita haqiqat manbai hosil qilardi. Bo'lim esa
    /// u yerda tekshirilmaydi (u yerda ham ixtiyoriy), shu sababli
    /// yagona qo'shimcha tekshiruv aynan bo'limga tegishli.
    /// </para>
    /// </remarks>
    private async Task<Result> CheckPlacementAsync(
        HireCandidateCommand command,
        CancellationToken cancellationToken)
    {
        if (command.LegalEntityId == Guid.Empty)
        {
            return RecruitingErrors.CandidateHireLegalEntityMissing;
        }

        if (command.PositionId == Guid.Empty)
        {
            return RecruitingErrors.CandidateHirePositionMissing;
        }

        if (command.DepartmentId is { } departmentId
            && !await _departments.ExistsAsync(departmentId, cancellationToken).ConfigureAwait(false))
        {
            return RecruitingErrors.CandidateHireDepartmentNotFound;
        }

        return Result.Success();
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
