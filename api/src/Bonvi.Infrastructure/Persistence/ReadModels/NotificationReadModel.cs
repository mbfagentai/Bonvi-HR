using Bonvi.Application.Notifications.Abstractions;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Persistence.ReadModels;

/// <summary>
/// <see cref="INotificationReadModel"/> ning EF Core orqali amalga
/// oshirilishi — bildirishnoma SHARTLARI uchun proyeksiya so'rovlari.
/// </summary>
/// <remarks>
/// <para>
/// Fayl <c>Persistence/ReadModels</c> da, repozitoriylar yonida emas:
/// bu yerdagi so'rovlar agregat qaytarmaydi va hech qachon saqlanmaydi
/// (<see cref="EmployeeReadModel"/> izohidagi bilan bir xil sabab).
/// </para>
/// <para>
/// <b>Har bir so'rov O'ZI cheklaydi.</b> Birorta metod «hammasini olib
/// kel, keyin C# da filtrlaymiz» degan yo'ldan bormaydi: sana chegarasi,
/// holat sharti va «ishlayotganmi» tekshiruvi bazada bajariladi. Sabab
/// oddiy — bu so'rovlar qo'ng'iroq bosilgan HAR SAFAR ketadi va ular
/// hech qachon sahifalanmaydi.
/// </para>
/// <para>
/// <b>Yumshoq o'chirilganlar o'z-o'zidan chetda.</b> Xodim, munosabat,
/// nomzod va taklif — hammasi <c>ISoftDeletable</c>, ya'ni global filtr
/// ularni allaqachon yashiradi. Bu yerda qo'shimcha shart yozilmaydi:
/// takrorlangan shart bir kun global filtr o'zgarganda unutilardi.
/// </para>
/// </remarks>
public sealed class NotificationReadModel : INotificationReadModel
{
    private readonly AppDbContext _context;

    public NotificationReadModel(AppDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProbationDeadlineRow>> ListProbationDeadlinesAsync(
        DateOnly until,
        CancellationToken cancellationToken = default)
    {
        // Quyi chegara ATAYLAB yo'q: muddati o'tib ketgan sinov qancha
        // eski bo'lsa ham kerak. Aynan eskilari unutilib qolgan bo'ladi va
        // ular uchun bildirishnoma yozilgan (`ProbationOverdue`).
        var rows = await (
                from probation in _context.Probations.AsNoTracking()
                where probation.Status == ProbationStatus.InProgress && probation.EndDate <= until
                join employment in _context.Employments.AsNoTracking()
                    on probation.EmploymentId equals employment.Id
                join employee in _context.Employees.AsNoTracking()
                    on employment.EmployeeId equals employee.Id
                join department in _context.Departments.AsNoTracking()
                    on employment.DepartmentId equals (Guid?)department.Id into departmentMatch
                from department in departmentMatch.DefaultIfEmpty()
                join position in _context.Positions.AsNoTracking()
                    on employment.PositionId equals (Guid?)position.Id into positionMatch
                from position in positionMatch.DefaultIfEmpty()
                select new
                {
                    ProbationId = probation.Id,
                    EmployeeId = employee.Id,
                    employee.FullName.LastName,
                    employee.FullName.FirstName,
                    employee.FullName.MiddleName,
                    DepartmentName = department!.Name,
                    PositionName = position!.Name,
                    probation.EndDate,
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. rows.Select(row => new ProbationDeadlineRow(
                row.ProbationId,
                row.EmployeeId,
                // Ism uchta ustundan yig'iladi — barcha o'qish modellarida
                // bir xil qoida (`FromTrusted`: bazadagi qiymat allaqachon
                // tekshirilgan, uni qayta validatsiya qilish shart emas).
                FullName.FromTrusted(row.LastName, row.FirstName, row.MiddleName).Full,
                row.DepartmentName,
                row.PositionName,
                row.EndDate)),
        ];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BirthdayRow>> ListBirthdaysAsync(
        DateOnly today,
        DateOnly tomorrow,
        CancellationToken cancellationToken = default)
    {
        // Ikki kun BITTA so'rovda: ikkita alohida so'rov bir xil jadvalni
        // ikki marta o'qib chiqardi, natija esa baribir birga ishlatiladi.
        //
        // Shartning O'ZI shu yerda yozilmaydi — u `BirthdayQuery` da,
        // bosh sahifadagi son va uning ostidagi ro'yxat bilan YAGONA
        // nusxada. Ilgari u shu yerda takrorlangan edi va «aynan bir xil
        // qoida» degan izoh bilan qadalgan edi; izoh esa kodni bir xil
        // ushlab tura olmaydi — endi buni tiplar qiladi.
        var rows = await BirthdayQuery
            .WithBirthdayOn(_context, today, tomorrow)
            .Select(employee => new
            {
                employee.Id,
                employee.FullName.LastName,
                employee.FullName.FirstName,
                employee.FullName.MiddleName,
                employee.BirthDate,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. rows.Select(row => new BirthdayRow(
                row.Id,
                FullName.FromTrusted(row.LastName, row.FirstName, row.MiddleName).Full,
                row.BirthDate!.Value)),
        ];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NewCandidateRow>> ListNewCandidatesAsync(
        DateOnly since,
        CancellationToken cancellationToken = default)
    {
        // `CreatedAt` — `timestamptz`, `since` esa KUN. Chegara Toshkent
        // yarim tunidan boshlanadi: server UTC'da ishlaydi va oddiy
        // `since.ToDateTime(...)` bilan taqqoslash beshta soatga surilgan
        // oyna berardi — kechqurun tushgan anketa ertasi kun «kechagi»
        // bo'lib chiqardi.
        //
        // `ToUniversalTime()` MAJBURIY: Npgsql `timestamptz` ustuniga faqat
        // nol siljishli (UTC) qiymat yozadi va Toshkent siljishi bilan
        // yuborilgan parametr istisno tashlaydi. Lahza o'zgarmaydi —
        // faqat ifodalanishi.
        var boundary = new DateTimeOffset(since.ToDateTime(TimeOnly.MinValue), SystemClock.TashkentOffset)
            .ToUniversalTime();

        var rows = await (
                from candidate in _context.Candidates.AsNoTracking()
                where candidate.Status == CandidateStatus.InProgress && candidate.CreatedAt >= boundary
                join vacancy in _context.Vacancies.AsNoTracking()
                    on candidate.VacancyId equals vacancy.Id into vacancyMatch
                from vacancy in vacancyMatch.DefaultIfEmpty()
                join title in _context.VacancyTitles.AsNoTracking()
                    on vacancy.VacancyTitleId equals title.Id into titleMatch
                from title in titleMatch.DefaultIfEmpty()
                select new
                {
                    CandidateId = candidate.Id,
                    candidate.FullName.LastName,
                    candidate.FullName.FirstName,
                    candidate.FullName.MiddleName,
                    // Vakansiya nomi CHAPDAN qo'shiladi: e'loni o'chirilgan
                    // nomzod ham ro'yxatdan tushib qolmasligi kerak
                    // (`RecruitingReadModel` dagi bilan bir xil sabab).
                    VacancyTitle = title!.Name,
                    candidate.Source,
                    candidate.CreatedAt,
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. rows.Select(row => new NewCandidateRow(
                row.CandidateId,
                FullName.FromTrusted(row.LastName, row.FirstName, row.MiddleName).Full,
                row.VacancyTitle,
                row.Source,
                // Kun Toshkent vaqti bo'yicha — foydalanuvchi «kecha
                // qo'shilgan» deganda o'z kunini nazarda tutadi, UTC
                // kunini emas.
                DateOnly.FromDateTime(row.CreatedAt.ToOffset(SystemClock.TashkentOffset).DateTime))),
        ];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<JobOfferDeadlineRow>> ListOfferDeadlinesAsync(
        DateOnly until,
        CancellationToken cancellationToken = default)
    {
        var rows = await (
                from offer in _context.JobOffers.AsNoTracking()
                where offer.Status == JobOfferStatus.Sent
                    // Muddatsiz taklif bu yerga TUSHMAYDI: HR ataylab
                    // «o'ylab ko'ring» deb muddatsiz taklif qilishi mumkin
                    // va unga eslatma yuborish uchun sana yo'q. SQL'da
                    // `NULL <= sana` hech qachon rost bo'lmaydi, lekin
                    // niyat oshkora yozilgan.
                    && offer.ExpiresAt != null
                    && offer.ExpiresAt <= until
                join candidate in _context.Candidates.AsNoTracking()
                    on offer.CandidateId equals candidate.Id
                join vacancy in _context.Vacancies.AsNoTracking()
                    on offer.VacancyId equals vacancy.Id into vacancyMatch
                from vacancy in vacancyMatch.DefaultIfEmpty()
                join title in _context.VacancyTitles.AsNoTracking()
                    on vacancy.VacancyTitleId equals title.Id into titleMatch
                from title in titleMatch.DefaultIfEmpty()
                select new
                {
                    OfferId = offer.Id,
                    CandidateId = candidate.Id,
                    candidate.FullName.LastName,
                    candidate.FullName.FirstName,
                    candidate.FullName.MiddleName,
                    VacancyTitle = title!.Name,
                    offer.ExpiresAt,
                    offer.Salary.Amount,
                    offer.Salary.Currency,
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. rows.Select(row => new JobOfferDeadlineRow(
                row.OfferId,
                row.CandidateId,
                FullName.FromTrusted(row.LastName, row.FirstName, row.MiddleName).Full,
                row.VacancyTitle,
                row.ExpiresAt!.Value,
                row.Amount,
                row.Currency)),
        ];
    }
}
