using System.Linq.Expressions;
using Bonvi.Application.Employees.Abstractions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Persistence.ReadModels;

/// <summary>
/// <see cref="IEmployeeReadModel"/> ning EF Core orqali amalga oshirilishi —
/// ro'yxat ekranlari uchun proyeksiya so'rovlari.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega repozitoriylar papkasida emas.</b> Repozitoriy agregatni yuklaydi
/// va uni o'zgartirish uchun kuzatadi. Bu yerdagi so'rovlar esa agregat
/// qaytarmaydi: ular bir necha jadvalni birlashtirib, ekranga tayyor tekis
/// qatorlar beradi va hech qachon saqlanmaydi. Ikkisi bir papkada tursa,
/// ertami-kechmi kimdir o'qish so'roviga <c>SaveChanges</c> qo'shib qo'yadi.
/// </para>
/// <para>
/// <b>Hamma joyda <c>AsNoTracking</c>.</b> Bu yo'l faqat o'qish uchun:
/// o'zgarish kuzatuvchisiga yozuv qo'shish 25 qatorlik sahifada ham
/// ortiqcha xotira va ortiqcha ish demak.
/// </para>
/// <para>
/// <b>Filtr, saralash va sahifalash BAZADA.</b> Bu portning mavjud bo'lish
/// sababi (izohi <see cref="IEmployeeReadModel"/> da). Shu sababli bu faylda
/// <c>ToListAsync</c> dan KEYIN birorta ham <c>Where</c>, <c>OrderBy</c> yoki
/// <c>Skip</c> yo'q — materializatsiyadan keyin faqat qiymat obyektlarini
/// yig'ish qoladi (masalan <c>Pinfl</c> → satr), ya'ni sof shakl o'zgarishi.
/// </para>
/// </remarks>
public sealed class EmployeeReadModel : IEmployeeReadModel
{
    /// <summary>
    /// <c>ILIKE</c> naqshidagi ekranlash belgisi — teskari chiziq.
    /// </summary>
    /// <remarks>
    /// <c>LikePattern</c> aynan shu belgi bilan ekranlaydi va u xom SQL
    /// yo'lidagi PostgreSQL sukutiga to'g'ri keladi. <c>EF.Functions.ILike</c>
    /// ga esa uni QO'LDA uzatish kerak, aks holda ekranlash o'chib qoladi.
    /// </remarks>
    private const string LikeEscape = "\\";

    private readonly AppDbContext _context;
    private readonly IDepartmentRepository _departments;
    private readonly IClock _clock;

    public EmployeeReadModel(AppDbContext context, IDepartmentRepository departments, IClock clock)
    {
        _context = context;
        _departments = departments;
        _clock = clock;
    }

    // ════════════════════════════════════════════════════════════
    //  1. Xodimlar ro'yxati — asosiy ekran
    // ════════════════════════════════════════════════════════════

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// So'rov uch qadamdan iborat va uchalasi ham serverda bajariladi:
    /// </para>
    /// <list type="number">
    ///   <item>xodimlar filtrlanadi (matn, yo'nalish, «tuzatish kerak»);</item>
    ///   <item>har bir xodimga <b>bitta</b> mehnat munosabati biriktiriladi
    ///         (LATERAL + <c>LIMIT 1</c>) va bo'lim / lavozim / firma nomlari
    ///         birlashtiriladi;</item>
    ///   <item>saralash va sahifalash qo'llanadi.</item>
    /// </list>
    /// <para>
    /// Belgilar ATAYLAB alohida so'rovda va faqat SHU SAHIFADAGI xodimlar
    /// uchun olinadi. Ularni asosiy so'rovga qo'shish qatorlarni
    /// ko'paytirib yuborardi (bitta xodimda bir nechta belgi bo'lishi
    /// mumkin) va sahifa hajmini buzardi.
    /// </para>
    /// </remarks>
    public async Task<EmployeeSearchPage> SearchEmployeesAsync(
        EmployeeSearchFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var employees = MatchingEmployees(filter.Search);

        if (filter.NeedsReview is { } needsReview)
        {
            employees = employees.Where(employee => employee.NeedsReview == needsReview);
        }

        if (filter.DirectionId is { } directionId)
        {
            // Yo'nalishlar xodimda massiv bo'lib yotadi — shart massiv
            // operatoriga aylanadi va `direction_ids` GIN indeksiga tushadi.
            employees = employees.Where(employee => employee.DirectionIds.Contains(directionId));
        }

        var employments = _context.Employments.AsNoTracking();

        // Munosabatga tegishli filtrlardan birortasi berilgan bo'lsa, mos
        // munosabati bo'lmagan xodim ro'yxatda umuman ko'rinmasligi kerak:
        // «Sotuv bo'limi» so'ralganda ishga hali qabul qilinmaganlar chiqmaydi.
        var employmentRequired = false;

        if (filter.DepartmentId is { } departmentId)
        {
            var branch = await BranchIdsAsync(departmentId, cancellationToken).ConfigureAwait(false);

            employments = employments.Where(employment =>
                employment.DepartmentId != null && branch.Contains(employment.DepartmentId.Value));

            employmentRequired = true;
        }

        if (filter.PositionId is { } positionId)
        {
            employments = employments.Where(employment => employment.PositionId == positionId);
            employmentRequired = true;
        }

        if (filter.LegalEntityId is { } legalEntityId)
        {
            employments = employments.Where(employment => employment.LegalEntityId == legalEntityId);
            employmentRequired = true;
        }

        if (filter.Status is { } status)
        {
            employments = employments.Where(employment => employment.Status == status);
            employmentRequired = true;
        }

        // ⚠️ Ro'yxatda har bir SHAXS bir marta ko'rinadi. Munosabatlar bilan
        // oddiy JOIN qilinsa, ikki firmada ishlaydigan odam ikki qator bo'lib
        // chiqardi va «540 xodim» «610 shartnoma» ga aylanardi.
        //
        // Shu sababli munosabat ichma-ich so'rov bilan tanlanadi: asosiysi,
        // u bo'lmasa — eng oxirgi boshlangani. EF buni
        // `ROW_NUMBER() OVER (PARTITION BY employee_id ORDER BY …) = 1`
        // ga aylantiradi, ya'ni qatorlar soni xodimlar sonidan oshmaydi.
        //
        // Filtrlar ichki so'rovga QO'LLANGANIDAN keyin tanlanadi: «Sotuv
        // bo'limi» so'ralganda o'rindoshlik bo'lsa ham aynan sotuvdagi
        // munosabat ko'rinishi kerak, asosiysi boshqa bo'limda bo'lsa ham.
        var pairs =
            from employee in employees
            from employment in employments
                .Where(candidate => candidate.EmployeeId == employee.Id)
                .OrderByDescending(candidate => candidate.IsPrimary)
                .ThenByDescending(candidate => candidate.HireDate)
                .ThenByDescending(candidate => candidate.Id)
                .Take(1)
                .DefaultIfEmpty()
            select new { Employee = employee, Employment = employment };

        if (employmentRequired)
        {
            pairs = pairs.Where(pair => pair.Employment != null);
        }

        // Nomlar birlashtirish orqali olinadi, ma'lumotnomaga alohida
        // so'rov bilan emas: 25 qatorlik sahifa uchun 75 ta qo'shimcha
        // so'rov (N+1) bo'lardi. `Employment` da navigatsiya xossalari yo'q
        // (agregatlar bir-birini identifikator orqali biladi), shu sababli
        // birlashtirish qo'lda yoziladi.
        var shaped =
            from pair in pairs
            join department in _context.Departments
                on pair.Employment!.DepartmentId equals (Guid?)department.Id into departmentMatch
            from department in departmentMatch.DefaultIfEmpty()
            join position in _context.Positions
                on pair.Employment!.PositionId equals (Guid?)position.Id into positionMatch
            from position in positionMatch.DefaultIfEmpty()
            join legalEntity in _context.LegalEntities
                on pair.Employment!.LegalEntityId equals legalEntity.Id into legalEntityMatch
            from legalEntity in legalEntityMatch.DefaultIfEmpty()
            select new
            {
                pair.Employee,
                pair.Employment,
                DepartmentName = department!.Name,
                PositionName = position!.Name,
                LegalEntityName = legalEntity!.Name,
            };

        // Umumiy son sahifalashdan OLDIN olinadi — «1–25 / 540» yozuvi uchun.
        // Bu alohida `COUNT(*)` so'rovi: qatorlarni sanash uchun ularni
        // tarmoq orqali olib kelish shart emas.
        //
        // Sanash `shaped` bo'yicha emas, `pairs` bo'yicha: nom birlashtirishlari
        // yagona kalit bo'yicha va chapdan, ya'ni qatorlar sonini o'zgartirmaydi.
        // Ularni sanash so'roviga qo'shish faqat ortiqcha ish bo'lardi.
        var total = await pairs.CountAsync(cancellationToken).ConfigureAwait(false);

        // Saralash shu yerda, alohida metodda emas: kalitlar anonim tipning
        // maydonlari va ularni metod chegarasidan o'tkazib bo'lmaydi.
        var descending = filter.Descending;

        var ordered = filter.Sort switch
        {
            EmployeeSortField.HireDate => By(shaped, row => row.Employment!.HireDate, descending),
            EmployeeSortField.Department => By(shaped, row => row.DepartmentName, descending),
            EmployeeSortField.Position => By(shaped, row => row.PositionName, descending),
            EmployeeSortField.Status => By(shaped, row => row.Employment!.Status, descending),
            EmployeeSortField.BirthDate => By(shaped, row => row.Employee.BirthDate, descending),
            _ => Then(
                By(shaped, row => row.Employee.FullName.LastName, descending),
                row => row.Employee.FullName.FirstName,
                descending),
        };

        var page = await ordered
            // Oxirgi mezon DOIM o'sish tartibida. Sababi sahifalash: teng
            // qiymatlarda (bir xil ishga kirish sanasi, bir xil bo'lim)
            // PostgreSQL tartibni kafolatlamaydi va bitta xodim ikkinchi
            // sahifada takrorlanib, boshqasi umuman tushib qolishi mumkin edi.
            .ThenBy(row => row.Employee.Id)
            .Skip(filter.Skip)
            .Take(filter.PageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var activeFlags = await ActiveFlagsAsync(
                [.. page.Select(row => row.Employee.Id)],
                cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<EmployeeSearchRow> items =
        [
            .. page.Select(row => new EmployeeSearchRow(
                row.Employee.Id,
                row.Employee.FullName.LastName,
                row.Employee.FullName.FirstName,
                row.Employee.FullName.MiddleName,
                row.Employee.BirthDate,
                row.Employee.Pinfl?.Value,
                row.Employee.Passport?.Value,
                row.Employee.Phone?.Value,
                row.Employee.PhotoKey,
                row.Employee.NeedsReview,
                row.Employment?.Id,
                row.Employment?.LegalEntityId,
                row.LegalEntityName,
                row.Employment?.DepartmentId,
                row.DepartmentName,
                row.Employment?.PositionId,
                row.PositionName,
                row.Employment?.Status,
                row.Employment?.HireDate,
                row.Employment?.Rate,
                row.Employment?.Salary?.Amount,
                row.Employment?.Salary?.Currency,
                activeFlags.TryGetValue(row.Employee.Id, out var flags) ? flags : [])),
        ];

        return new EmployeeSearchPage(items, total);
    }

    // ════════════════════════════════════════════════════════════
    //  2. Sinov muddatlari
    // ════════════════════════════════════════════════════════════

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Chegara sanasi so'rovdan TASHQARIDA hisoblanadi
    /// (<c>Today.AddDays(n)</c>). Ifoda so'rov ichida qolsa, PostgreSQL uni
    /// har qatorda qayta hisoblardi va <c>(status, end_date)</c> indeksi
    /// ishlatilmasdi — bu naqsh <c>ProbationRepository</c> da ham bor.
    /// </para>
    /// <para>
    /// Munosabat bilan birlashtirish ICHKI: munosabatsiz sinov muddati
    /// ma'nosiz. U faqat munosabat yumshoq o'chirilganda qolib ketishi
    /// mumkin va bunday yozuv ekranda ko'rinmasligi kerak.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<ProbationRow>> ListProbationsAsync(
        ProbationListFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var probations = _context.Probations.AsNoTracking();

        if (filter.Status is { } status)
        {
            probations = probations.Where(probation => probation.Status == status);
        }

        if (filter.EndingWithinDays is { } days)
        {
            var limit = filter.Today.AddDays(days);

            // Pastki chegara ATAYLAB yo'q: muddati allaqachon o'tib ketganlar
            // ham qaytadi. Aynan ular — HR o'tkazib yuborgan holatlar.
            probations = probations.Where(probation => probation.EndDate <= limit);
        }

        var rows = await (
                from probation in probations
                join employment in _context.Employments
                    on probation.EmploymentId equals employment.Id
                join employee in _context.Employees
                    on employment.EmployeeId equals employee.Id
                join department in _context.Departments
                    on employment.DepartmentId equals (Guid?)department.Id into departmentMatch
                from department in departmentMatch.DefaultIfEmpty()
                join position in _context.Positions
                    on employment.PositionId equals (Guid?)position.Id into positionMatch
                from position in positionMatch.DefaultIfEmpty()
                orderby probation.EndDate, employee.FullName.LastName, employee.FullName.FirstName
                select new
                {
                    probation.Id,
                    probation.EmploymentId,
                    probation.StartDate,
                    probation.EndDate,
                    probation.Status,
                    probation.ResolvedAt,
                    probation.Conclusion,
                    probation.ReviewerEmployeeId,
                    EmployeeId = employee.Id,
                    employee.FullName.LastName,
                    employee.FullName.FirstName,
                    employee.FullName.MiddleName,
                    employee.PhotoKey,
                    DepartmentName = department!.Name,
                    PositionName = position!.Name,
                    employment.DepartmentId,
                    employment.PositionId,
                    employment.HireDate,
                })
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. rows.Select(row => new ProbationRow(
                row.Id,
                row.EmploymentId,
                row.StartDate,
                row.EndDate,
                row.Status,
                row.ResolvedAt,
                row.Conclusion,
                row.ReviewerEmployeeId,
                row.EmployeeId,
                FullName.FromTrusted(row.LastName, row.FirstName, row.MiddleName).Full,
                row.PhotoKey,
                row.DepartmentName,
                row.PositionName,
                row.DepartmentId,
                row.PositionId,
                row.HireDate)),
        ];
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Shart bittagina: holati <c>Probation</c>, lekin unga bog'langan
    /// sinov yozuvi yo'q. EF buni <c>NOT EXISTS</c> ga aylantiradi —
    /// munosabatlar jadvali bo'yicha bir marta yuriladi.
    /// </para>
    /// <para>
    /// <b>Sahifalash ATAYLAB yo'q.</b> Bu ro'yxat — bir martalik tozalash
    /// ishi va u tugagach bo'shab qoladi. Sahifalash HR ni «ikkinchi
    /// sahifada yana bormi?» degan savol bilan qoldirardi; bu yerda esa
    /// hammasini bir ekranda ko'rib, bir marta tasdiqlash kerak.
    /// Ko'chirishdan keyin eng katta kutilgan hajm — bir necha yuz qator.
    /// </para>
    /// <para>
    /// Tartib ishga kirish sanasi bo'yicha: eng eskisi tepada turadi —
    /// aynan ular eng aniq holatlar («16 yil sinovda» degani bo'lmaydi).
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<MissingProbationRow>> ListMissingProbationsAsync(
        CancellationToken cancellationToken = default)
    {
        var today = _clock.Today;

        var rows = await (
                from employment in _context.Employments.AsNoTracking()
                where employment.Status == EmploymentStatus.Probation
                   && !_context.Probations.Any(probation => probation.EmploymentId == employment.Id)
                join employee in _context.Employees
                    on employment.EmployeeId equals employee.Id
                join legalEntity in _context.LegalEntities
                    on employment.LegalEntityId equals legalEntity.Id into legalEntityMatch
                from legalEntity in legalEntityMatch.DefaultIfEmpty()
                join department in _context.Departments
                    on employment.DepartmentId equals (Guid?)department.Id into departmentMatch
                from department in departmentMatch.DefaultIfEmpty()
                join position in _context.Positions
                    on employment.PositionId equals (Guid?)position.Id into positionMatch
                from position in positionMatch.DefaultIfEmpty()
                orderby employment.HireDate, employee.FullName.LastName, employee.FullName.FirstName
                select new
                {
                    EmploymentId = employment.Id,
                    EmployeeId = employee.Id,
                    employee.FullName.LastName,
                    employee.FullName.FirstName,
                    employee.FullName.MiddleName,
                    employee.PhotoKey,
                    LegalEntityName = legalEntity!.Name,
                    DepartmentName = department!.Name,
                    PositionName = position!.Name,
                    employment.HireDate,
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. rows.Select(row => new MissingProbationRow(
                row.EmploymentId,
                row.EmployeeId,
                FullName.FromTrusted(row.LastName, row.FirstName, row.MiddleName).Full,
                row.PhotoKey,
                row.LegalEntityName,
                row.DepartmentName,
                row.PositionName,
                row.HireDate,
                // Kunlar soni bazada emas, shu yerda: `DateOnly` ayirmasi
                // PostgreSQL ifodasiga tarjima qilinmaydi va uni SQL'ga
                // itarish uchun xom ifoda yozish kerak bo'lardi — 
                // sahifasiz ro'yxatda bu foyda bermaydi.
                today.DayNumber - row.HireDate.DayNumber)),
        ];
    }

    // ════════════════════════════════════════════════════════════
    //  3. Belgilar: rezerv / qora ro'yxat / nazorat / qobiliyatli
    // ════════════════════════════════════════════════════════════

    /// <inheritdoc />
    /// <remarks>
    /// Faollik sharti hamma joyda BIR XIL yozilgan:
    /// <c>start_date &lt;= kun AND (end_date IS NULL OR end_date &gt;= kun)</c>
    /// — <c>EmployeeFlagRepository</c> dagi bilan aynan bir xil. Uni har joyda
    /// boshqacha yozish belgining bir ekranda ko'rinib, boshqasida
    /// ko'rinmasligining eng keng tarqalgan sababi.
    /// </remarks>
    public async Task<IReadOnlyList<EmployeeFlagRow>> ListFlagsAsync(
        EmployeeFlagType type,
        bool onlyActive,
        DateOnly on,
        CancellationToken cancellationToken = default)
    {
        var flags = _context.EmployeeFlags
            .AsNoTracking()
            .Where(flag => flag.Type == type);

        if (onlyActive)
        {
            flags = flags.Where(flag =>
                flag.StartDate <= on && (flag.EndDate == null || flag.EndDate >= on));
        }

        // Mas'ul ham xodim: `employees` jadvaliga IKKINCHI birlashtirish.
        // Chapdan, chunki mas'ul ko'rsatilmagan bo'lishi mumkin — aks holda
        // mas'ulsiz belgilar ro'yxatdan jimgina tushib qolardi.
        var rows = await (
                from flag in flags
                join employee in _context.Employees
                    on flag.EmployeeId equals employee.Id
                join responsible in _context.Employees
                    on flag.ResponsibleEmployeeId equals (Guid?)responsible.Id into responsibleMatch
                from responsible in responsibleMatch.DefaultIfEmpty()
                orderby flag.StartDate descending,
                    employee.FullName.LastName,
                    employee.FullName.FirstName
                select new
                {
                    flag.Id,
                    flag.EmployeeId,
                    employee.FullName.LastName,
                    employee.FullName.FirstName,
                    employee.FullName.MiddleName,
                    flag.Type,
                    flag.StartDate,
                    flag.EndDate,
                    flag.Reason,
                    flag.ResponsibleEmployeeId,
                    employee.PhotoKey,
                    ResponsibleLastName = (string?)responsible!.FullName.LastName,
                    ResponsibleFirstName = (string?)responsible!.FullName.FirstName,
                    ResponsibleMiddleName = responsible!.FullName.MiddleName,
                })
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. rows.Select(row => new EmployeeFlagRow(
                row.Id,
                row.EmployeeId,
                FullName.FromTrusted(row.LastName, row.FirstName, row.MiddleName).Full,
                row.PhotoKey,
                row.Type,
                row.StartDate,
                row.EndDate,
                row.Reason,
                row.ResponsibleEmployeeId,
                Assemble(row.ResponsibleLastName, row.ResponsibleFirstName, row.ResponsibleMiddleName))),
        ];
    }

    // ════════════════════════════════════════════════════════════
    //  4. Buyruqlar — barcha xodimlar bo'yicha yagona oqim
    // ════════════════════════════════════════════════════════════

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Munosabat va xodim bilan birlashtirish ICHKI. Bu ataylab: hodisa
    /// «kim, qayerda» konteksti bilan birga ma'noga ega, va yumshoq
    /// o'chirilgan munosabat yoki xodimning buyrug'i ro'yxatda
    /// ko'rinmasligi kerak (global filtr bu birlashtirishlarga o'zi
    /// qo'llanadi).
    /// </para>
    /// <para>
    /// Bo'lim va lavozim nomlari CHAPDAN birlashtiriladi va ular ikki
    /// martadan: hodisa «qayerdan → qayerga» ni saqlaydi va ekranda
    /// aynan shu juftlik ko'rsatiladi. Ma'lumotnomaga tashqi kalit yo'q
    /// (izohi <c>EmploymentEventConfiguration</c> da), ya'ni o'chirilgan
    /// lavozim nomi <c>null</c> bo'lib qaytishi — kutilgan holat, qator
    /// esa baribir ro'yxatda qoladi.
    /// </para>
    /// <para>
    /// Umumiy son sahifalashdan OLDIN va nom birlashtirishlarisiz sanaladi:
    /// chapdan birlashtirish qatorlar sonini o'zgartirmaydi, demak ularni
    /// <c>COUNT(*)</c> so'roviga qo'shish faqat ortiqcha ish bo'lardi.
    /// </para>
    /// <para>
    /// Bo'lim filtri hodisaning o'z ustunlaridan KENGROQ: u butun shoxni
    /// (ichki bo'limlar bilan) oladi va bo'limi ko'rsatilmagan hodisani
    /// munosabatning joriy bo'limiga bog'laydi. Ikkalasining sababi
    /// filtr yozilgan joyda batafsil.
    /// </para>
    /// </remarks>
    public async Task<EmploymentEventPage> ListEmploymentEventsAsync(
        EmploymentEventFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var events = _context.EmploymentEvents.AsNoTracking();

        if (filter.Type is { } type)
        {
            events = events.Where(employmentEvent => employmentEvent.Type == type);
        }

        // Sana oralig'i IKKI TOMONDAN yopiq: HR «1 avgustdan 31 avgustgacha»
        // deganda 31-avgustdagi buyruq ham kirishi kerak.
        if (filter.From is { } from)
        {
            events = events.Where(employmentEvent => employmentEvent.EffectiveDate >= from);
        }

        if (filter.To is { } to)
        {
            events = events.Where(employmentEvent => employmentEvent.EffectiveDate <= to);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = LikePattern(filter.Search);

            // Buyruq raqami — oddiy satr ustuni (qiymat obyekti emas), shu
            // sababli xodimlar qidiruvidagi xom SQL kerak emas: `ILike`
            // to'g'ridan-to'g'ri SQL'ga tarjima bo'ladi.
            //
            // ⚠️ Ekranlash belgisi ATAYLAB uchinchi argument bilan beriladi.
            // Ikki argumentli `ILike` Npgsql'da `ESCAPE ''` bo'lib tarjima
            // bo'ladi, ya'ni ekranlash BUTUNLAY o'chadi va `LikePattern`
            // qo'ygan teskari chiziq oddiy belgiga aylanadi. Natijada «50%»
            // deb qidirgan foydalanuvchi hech narsa topmasdi, «_» esa
            // istalgan belgiga mos tushardi (tekshirildi: SQL'da
            // `ILIKE @pattern ESCAPE ''`).
            //
            // `order_no IS NOT NULL` sharti ataylab oldinda: ko'chirilgan
            // hodisalarning katta qismida buyruq raqami yo'q va ular
            // taqqoslashga umuman kirmasligi kerak.
            events = events.Where(employmentEvent =>
                employmentEvent.OrderNo != null
                && EF.Functions.ILike(employmentEvent.OrderNo, pattern, LikeEscape));
        }

        var employments = _context.Employments.AsNoTracking();

        if (filter.LegalEntityId is { } legalEntityId)
        {
            employments = employments.Where(employment => employment.LegalEntityId == legalEntityId);
        }

        var matched =
            from employmentEvent in events
            join employment in employments
                on employmentEvent.EmploymentId equals employment.Id
            join employee in _context.Employees
                on employment.EmployeeId equals employee.Id
            select new
            {
                Event = employmentEvent,
                // Munosabatning JORIY bo'limi — hodisada bo'lim
                // ko'rsatilmagan holat uchun (pastdagi filtrga qara).
                EmploymentDepartmentId = employment.DepartmentId,
                EmployeeId = employee.Id,
                employee.FullName.LastName,
                employee.FullName.FirstName,
                employee.FullName.MiddleName,
                employee.PhotoKey,
            };

        // Bo'lim filtri ATAYLAB shu yerda — munosabat bilan birlashtirilgandan
        // KEYIN: uning zaxira sharti munosabatning bo'limiga tayanadi va
        // hodisalar jadvalining o'zida bunday ustun yo'q.
        if (filter.DepartmentId is { } departmentId)
        {
            // Butun shox olinadi, aniq moslik emas. Xodimlar ro'yxatida
            // bo'lim filtri allaqachon shunday ishlaydi va bir xil
            // parametr ikki ekranda ikki xil javob bermasligi kerak:
            // «Sotuv bo'limi» deganda uning ichki bo'limlari ham kiradi.
            var branch = await BranchIdsAsync(departmentId, cancellationToken).ConfigureAwait(false);

            matched = matched.Where(row =>
                // Bo'lim IKKALA tomondan qidiriladi. «Sotuv bo'limida nima
                // bo'ldi» degan savolga bo'limga kelganlar ham, undan
                // ketganlar ham javob beradi — faqat bittasini olish
                // tarixning yarmini yashirardi.
                (row.Event.ToDepartmentId != null && branch.Contains(row.Event.ToDepartmentId.Value))
                || (row.Event.FromDepartmentId != null && branch.Contains(row.Event.FromDepartmentId.Value))

                // Hodisada bo'lim UMUMAN ko'rsatilmagan bo'lsa — munosabatning
                // joriy bo'limiga tushiladi. Oklad o'zgarishi, bo'shatish va
                // qo'shimcha vazifada `from/to_department_id` odatda NULL
                // bo'ladi va ular tushib qolsa, «shu bo'limda nima bo'ldi»
                // degan savolga eng muhim ikki hodisa javob bermasdi.
                || (row.Event.FromDepartmentId == null
                    && row.Event.ToDepartmentId == null
                    && row.EmploymentDepartmentId != null
                    && branch.Contains(row.EmploymentDepartmentId.Value)));
        }

        var total = await matched.CountAsync(cancellationToken).ConfigureAwait(false);

        var shaped =
            from pair in matched
            join fromDepartment in _context.Departments
                on pair.Event.FromDepartmentId equals (Guid?)fromDepartment.Id into fromDepartmentMatch
            from fromDepartment in fromDepartmentMatch.DefaultIfEmpty()
            join toDepartment in _context.Departments
                on pair.Event.ToDepartmentId equals (Guid?)toDepartment.Id into toDepartmentMatch
            from toDepartment in toDepartmentMatch.DefaultIfEmpty()
            join fromPosition in _context.Positions
                on pair.Event.FromPositionId equals (Guid?)fromPosition.Id into fromPositionMatch
            from fromPosition in fromPositionMatch.DefaultIfEmpty()
            join toPosition in _context.Positions
                on pair.Event.ToPositionId equals (Guid?)toPosition.Id into toPositionMatch
            from toPosition in toPositionMatch.DefaultIfEmpty()
            select new
            {
                pair.Event,
                pair.EmployeeId,
                pair.LastName,
                pair.FirstName,
                pair.MiddleName,
                pair.PhotoKey,
                FromDepartmentName = fromDepartment!.Name,
                ToDepartmentName = toDepartment!.Name,
                FromPositionName = fromPosition!.Name,
                ToPositionName = toPosition!.Name,
            };

        var page = await shaped
            // Eng yangi buyruq tepada — bu ro'yxat «oxirgi paytda nima
            // bo'ldi» degan savolga javob beradi.
            .OrderByDescending(row => row.Event.EffectiveDate)
            // Ikkinchi mezon sahifalash uchun MAJBURIY: bir kunda o'nlab
            // buyruq bo'lishi mumkin (ETL butun oyni bitta sanaga qo'yadi)
            // va teng qiymatlarda PostgreSQL tartibni kafolatlamaydi —
            // bitta hodisa ikkinchi sahifada takrorlanib, boshqasi umuman
            // tushib qolardi. `Id` — v7 GUID, ya'ni yozilgan vaqt bo'yicha
            // o'sadi; teskari tartibda u «oxirgi kiritilgani tepada» degan
            // ma'noni beradi va birinchi mezon bilan bir yo'nalishda qoladi.
            .ThenByDescending(row => row.Event.Id)
            .Skip(filter.Skip)
            .Take(filter.PageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<EmploymentEventRow> items =
        [
            .. page.Select(row => new EmploymentEventRow(
                row.Event.Id,
                row.Event.EmploymentId,
                row.EmployeeId,
                FullName.FromTrusted(row.LastName, row.FirstName, row.MiddleName).Full,
                row.PhotoKey,
                row.Event.Type,
                row.Event.EffectiveDate,
                row.Event.OrderNo,
                row.Event.OrderDate,
                row.FromDepartmentName,
                row.ToDepartmentName,
                row.FromPositionName,
                row.ToPositionName,
                row.Event.FromSalary?.Amount,
                row.Event.FromSalary?.Currency,
                row.Event.ToSalary?.Amount,
                row.Event.ToSalary?.Currency,
                row.Event.Note,
                row.Event.IsImported)),
        ];

        return new EmploymentEventPage(items, total);
    }

    // ════════════════════════════════════════════════════════════
    //  Yordamchilar
    // ════════════════════════════════════════════════════════════

    /// <summary>Matn qidiruvi qo'llangan xodimlar manbai.</summary>
    /// <remarks>
    /// <para>
    /// <b>Nega shart xom SQL'da.</b> JSHSHIR, pasport va telefon — qiymat
    /// obyektlari; ular bazaga <i>konvertor</i> orqali satr bo'lib tushadi.
    /// LINQ ichida <c>employee.Pinfl.Value</c> ifodasi SQL'ga tarjima
    /// QILINMAYDI: konvertordan keyin EF uchun ustun skalyar, uning «ichi»
    /// yo'q. Ya'ni <c>EF.Functions.ILike</c> ni bu ustunlarga qo'llab
    /// bo'lmaydi (tekshirildi — EF «could not be translated» deb yiqiladi).
    /// Qolgan yagona muqobil butun jadvalni xotiraga o'qib, keyin filtrlash
    /// bo'lardi; aynan shu narsa port izohida taqiqlangan.
    /// </para>
    /// <para>
    /// <b>Nega faqat identifikatorlar.</b> Xom so'rovdan to'g'ridan-to'g'ri
    /// <see cref="Employee"/> o'qilsa, EF ustki proyeksiyada complex type
    /// ustunlarini o'z sukut nomlari bilan
    /// (<c>"FullName_LastName"</c>) so'raydi — bizda esa ular
    /// <c>last_name</c>, <c>first_name</c>, <c>middle_name</c>
    /// (<c>EmployeeConfiguration</c> da ataylab qisqartirilgan) va so'rov
    /// «column does not exist» bilan yiqiladi. Shu sababli xom so'rovdan
    /// faqat kalit olinadi, xodimning o'zi esa odatdagi jadvaldan o'qiladi.
    /// </para>
    /// <para>
    /// Natija baribir <b>bitta</b> SQL bayonoti: ichki so'rov ustkisiga
    /// <c>id IN (…)</c> bo'lib qo'shiladi, hech qanday oraliq ro'yxat
    /// tarmoqdan o'tmaydi.
    /// </para>
    /// <para>
    /// FIO ifodasi migratsiyadagi trigramma indeksi bilan AYNAN bir xil:
    /// <c>lower(last_name || ' ' || first_name || ' ' || coalesce(middle_name, ''))</c>.
    /// Bir belgi farq qilsa ham PostgreSQL indeksni ishlatmaydi va qidiruv
    /// jimgina to'liq skanerlashga tushadi.
    /// </para>
    /// <para>
    /// Qidiruv matni <b>parametr</b> bo'lib uzatiladi — <c>FromSql</c>
    /// interpolyatsiyani parametrga aylantiradi, ya'ni SQL in'ektsiya
    /// yo'li yo'q.
    /// </para>
    /// </remarks>
    private IQueryable<Employee> MatchingEmployees(string? search)
    {
        var employees = _context.Employees.AsNoTracking();

        if (string.IsNullOrWhiteSpace(search))
        {
            return employees;
        }

        // Bitta qidiruv matnidan IKKI naqsh: ism ustuni kirillda, kod
        // ustunlari lotin va raqamda saqlanadi va ular boshqa-boshqa
        // «bir xil ko'rinadigan» belgilardan aziyat chekadi.
        // To'liq sabab — `SearchFolding` izohida.
        var namePattern = LikePattern(SearchFolding.ForName(search));
        var codePattern = LikePattern(SearchFolding.ForCode(search));

        // ⚠️ `translate(...)` ning ichidagi ikki satr XOM HOLDA yozilgan,
        // parametr sifatida emas. Bu majburiy: PostgreSQL ifoda ustidagi
        // indeksni faqat so'rovdagi ifoda indeks ta'rifi bilan AYNAN mos
        // tushganda ishlatadi, parametr esa moslikni buzadi. Qiymatlar
        // `SearchFolding` dagi konstantalardan ko'chirilgan va ular
        // `ix_employees_full_name_fold_trgm` indeksida ham shundayligicha
        // turibdi — uchalasi birga o'zgaradi.
        var matching = _context.Employees
            .FromSql(
                $"""
                 SELECT x.*
                 FROM employees AS x
                 WHERE translate(
                           lower(x.last_name || ' ' || x.first_name || ' ' || coalesce(x.middle_name, '')),
                           'қғҳўё', 'кгхуе') ILIKE {namePattern}
                    OR x.pinfl ILIKE {codePattern}
                    OR x.passport ILIKE {codePattern}
                    OR x.phone ILIKE {codePattern}
                    OR x.second_phone ILIKE {codePattern}
                 """)
            .Select(employee => employee.Id);

        return employees.Where(employee => matching.Contains(employee.Id));
    }

    /// <summary>Qidiruv matnini <c>ILIKE</c> naqshiga aylantiradi.</summary>
    /// <remarks>
    /// <c>\</c>, <c>%</c> va <c>_</c> ekranlanadi. Aks holda «50%» deb
    /// qidirgan foydalanuvchi butun jadvalni olib kelardi, «_» esa istalgan
    /// belgiga mos tushardi — ikkalasi ham jimgina noto'g'ri natija beradi.
    /// </remarks>
    private static string LikePattern(string search)
    {
        var escaped = search
            .Trim()
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

        return $"%{escaped}%";
    }

    /// <summary>Bo'lim va uning BARCHA ichki bo'limlari identifikatorlari.</summary>
    /// <remarks>
    /// Daraxt bo'yicha aylanish <c>DepartmentRepository</c> dagi rekursiv
    /// CTE bilan bajariladi — u halqadan himoyalangan va allaqachon
    /// sinovdan o'tgan. Uni bu yerda qayta yozish «avlod» tushunchasining
    /// ikkinchi ta'rifini yaratardi va ular bir kun kelib ajralib ketardi.
    /// </remarks>
    private async Task<List<Guid>> BranchIdsAsync(Guid departmentId, CancellationToken cancellationToken)
    {
        var descendants = await _departments
            .GetDescendantIdsAsync(departmentId, cancellationToken)
            .ConfigureAwait(false);

        return [departmentId, .. descendants];
    }

    /// <summary>Sahifadagi xodimlarning BUGUN kuchda bo'lgan belgilari.</summary>
    private async Task<Dictionary<Guid, IReadOnlyList<EmployeeFlagType>>> ActiveFlagsAsync(
        List<Guid> employeeIds,
        CancellationToken cancellationToken)
    {
        if (employeeIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<EmployeeFlagType>>();
        }

        var today = _clock.Today;

        var found = await _context.EmployeeFlags
            .AsNoTracking()
            .Where(flag =>
                employeeIds.Contains(flag.EmployeeId) &&
                flag.StartDate <= today &&
                (flag.EndDate == null || flag.EndDate >= today))
            .Select(flag => new { flag.EmployeeId, flag.Type })
            // Bir xodimda bir turdagi ikkita belgi bo'lishi mumkin (eskisi
            // yopilib, yangisi ochilgan). Ro'yxatda nishon bir marta
            // ko'rinishi kerak, shu sababli takrorlar BAZADA yo'qotiladi.
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return found
            .GroupBy(flag => flag.EmployeeId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<EmployeeFlagType>)[.. group.Select(flag => flag.Type).Order()]);
    }

    /// <summary>Chapdan birlashtirilgan xodimning to'liq ismini yig'adi.</summary>
    /// <remarks>
    /// Mas'ul ko'rsatilmagan bo'lsa ustunlar NULL bo'ladi — o'shanda nom
    /// o'rniga <c>null</c> qaytadi, bo'shliqlardan iborat satr emas.
    /// </remarks>
    private static string? Assemble(string? lastName, string? firstName, string? middleName)
        => lastName is null || firstName is null
            ? null
            : FullName.FromTrusted(lastName, firstName, middleName).Full;

    private static IOrderedQueryable<TRow> By<TRow, TKey>(
        IQueryable<TRow> source,
        Expression<Func<TRow, TKey>> key,
        bool descending)
        => descending ? source.OrderByDescending(key) : source.OrderBy(key);

    private static IOrderedQueryable<TRow> Then<TRow, TKey>(
        IOrderedQueryable<TRow> source,
        Expression<Func<TRow, TKey>> key,
        bool descending)
        => descending ? source.ThenByDescending(key) : source.ThenBy(key);
}
