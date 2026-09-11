using Bonvi.Application.Notifications.Abstractions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.ValueObjects;
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
/// va nomzod — hammasi <c>ISoftDeletable</c>, ya'ni global filtr
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
        var boundary = new DateTimeOffset(since.ToDateTime(TimeOnly.MinValue), BonviTime.TashkentOffset)
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
                DateOnly.FromDateTime(row.CreatedAt.ToOffset(BonviTime.TashkentOffset).DateTime))),
        ];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CandidateContactRow>> ListCandidateContactsAsync(
        DateOnly until,
        CancellationToken cancellationToken = default)
    {
        // ⭐ HOLAT sharti — bu so'rovdagi eng muhim qator. Nomzod
        // voronkadan chiqqanda sana domenning o'zida tozalanadi
        // (`Candidate.Close`), ya'ni bugun bu shart bitta ham qator
        // qo'shmaydi va OLIB TASHLASA HAM ro'yxat o'zgarmasdi. Baribir
        // yozilgan, chunki u BOSHQA savolga javob beradi: «bu ro'yxatda
        // kim bo'lishi mumkin?». Domendagi tozalash — invariant, bu esa
        // ta'rif, va ular bir joyda turmaydi. Excel'dan ko'chirilgan
        // yozuvlar (3110 ta) `Close` dan umuman o'tmaydi — ular bazaga
        // to'g'ridan-to'g'ri yoziladi; ertaga ETL aloqa sanasini ham
        // ko'chira boshlasa, shart bo'lmagan ro'yxat bir necha yil oldin
        // rad etilgan odamlarga qo'ng'iroq qilishga chaqirardi va buni
        // hech kim kutmagan bo'lardi.
        //
        // ⚠️ REZERV esa CHIQADI va bu ataylab (foydalanuvchi qarori,
        // 2026-09-11). Domen rezervga o'tkazishda aloqa sanasini SAQLAB
        // qoladi — «o'rin bo'shaganda chaqiramiz» (`Candidate.Close`
        // izohi). Agar qo'ng'iroqcha faqat `InProgress` ni ko'rsatsa, HR
        // sanani qo'yardi-yu, u hech qachon ogohlantirmasdi: ya'ni
        // saqlashning butun sababi yo'qolardi. Shu sababli bu so'rov
        // qo'shni `ListNewCandidatesAsync` dan ATAYLAB farq qiladi —
        // «yangi nomzod» faol voronka haqidagi savol, «aloqa qilish
        // kerak» esa HR ning bugungi ish ro'yxati, va rezerv aynan shu
        // ro'yxatning bir qismi.
        //
        // Suhbat ogohlantirishi (`ListCandidateInterviewsAsync`) bu
        // qoidaga QO'SHILMADI: rezervdagi nomzodga suhbat belgilanmaydi —
        // suhbat tayinlash uni voronkaga qaytaradi.
        //
        // Shart qisman indeksga tushadi: `ix_candidates_next_contact_date`
        // (`next_contact_date IS NOT NULL` filtri bilan).
        var rows = await (
                from candidate in _context.Candidates.AsNoTracking()
                where (candidate.Status == CandidateStatus.InProgress
                        || candidate.Status == CandidateStatus.Reserve)
                    && candidate.NextContactDate != null
                    && candidate.NextContactDate <= until
                select new
                {
                    CandidateId = candidate.Id,
                    candidate.FullName.LastName,
                    candidate.FullName.FirstName,
                    candidate.FullName.MiddleName,
                    candidate.NextContactDate,
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. rows.Select(row => new CandidateContactRow(
                row.CandidateId,
                FullName.FromTrusted(row.LastName, row.FirstName, row.MiddleName).Full,
                // `!` xavfsiz: `NULL` qatorlar so'rovning o'zida
                // chiqarib tashlangan.
                row.NextContactDate!.Value)),
        ];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CandidateInterviewRow>> ListCandidateInterviewsAsync(
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken = default)
    {
        // Chegaralar UTC'ga keltiriladi: `interview_at` — `timestamptz` va
        // Npgsql unga faqat nol siljishli qiymat bilan taqqoslash
        // parametrini yuboradi (`ListNewCandidatesAsync` dagi bilan bir xil
        // sabab). Lahza o'zgarmaydi, faqat ifodalanishi.
        var start = from.ToUniversalTime();
        var end = until.ToUniversalTime();

        // ⭐ Holat sharti bu yerda yuqoridagidan ham MUHIMROQ va uning
        // sababi boshqa. Aloqa sanasi voronkadan chiqishda tozalanadi,
        // SUHBAT VAQTI esa QOLADI (`Candidate.Close` unga tegmaydi) — u
        // tarixning bir qismi: nomzod chaqirilgan, kelgan, ko'rilgan.
        // Ya'ni shartsiz so'rov eng odatiy holatda yiqilardi: soat 15:00
        // ga chaqirilgan nomzod tushda «kelolmayman» desa (voz kechish)
        // yoki HR uni suhbatgacha rad etsa, qo'ng'iroq baribir 14:00 da
        // «bu odam hozir keladi» deb turardi.
        //
        // ⚠️ Bu so'rov indeksga TUSHMAYDI: `interview_at` bo'yicha
        // alohida indeks yo'q (bor indeks — taklif NAVBATI uchun va
        // uning filtri boshqa). Ataylab: jadval bir necha ming qator va
        // so'rov qo'ng'iroq bosilganda ketadi; indeks har bir suhbat
        // taklifida yozish narxini oshirardi, foydasi esa o'lchanmas
        // darajada kichik. Nomzodlar soni o'n minglarga chiqsa —
        // qisman indeks (`interview_at IS NOT NULL`) qo'shiladigan
        // birinchi joy shu.
        var rows = await (
                from candidate in _context.Candidates.AsNoTracking()
                where candidate.Status == CandidateStatus.InProgress
                    && candidate.InterviewAt != null
                    && candidate.InterviewAt >= start
                    && candidate.InterviewAt <= end
                select new
                {
                    CandidateId = candidate.Id,
                    candidate.FullName.LastName,
                    candidate.FullName.FirstName,
                    candidate.FullName.MiddleName,
                    candidate.InterviewAt,
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. rows.Select(row => new CandidateInterviewRow(
                row.CandidateId,
                FullName.FromTrusted(row.LastName, row.FirstName, row.MiddleName).Full,
                row.InterviewAt!.Value)),
        ];
    }
}
