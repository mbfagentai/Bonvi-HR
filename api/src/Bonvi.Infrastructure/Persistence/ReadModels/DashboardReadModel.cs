using System.Linq.Expressions;
using Bonvi.Application.Dashboard.Abstractions;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Persistence.ReadModels;

/// <summary>
/// <see cref="IDashboardReadModel"/> ning EF Core orqali amalga oshirilishi —
/// bosh sahifa uchun sanoq so'rovlari.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega har bir son ALOHIDA <c>COUNT</c> so'rovi.</b> O'n ikkita son
/// beshta turli jadvaldan olinadi va ularning har biri boshqa shartga
/// tayanadi. Ularni bitta so'rovga yig'ish uchun o'nlab skalyar ichma-ich
/// so'rovli XOM SQL yozish kerak bo'lardi: u jadval va ustun nomlariga
/// qattiq bog'lanib qolardi, yumshoq o'chirish filtrini (<c>deleted_at</c>)
/// qo'lda takrorlashga majbur qilardi va bitta unutilgan shart jimgina
/// noto'g'ri raqam berardi. Har bir so'rov esa indeks bo'yicha ketadi va
/// bitta son qaytaradi — javob hajmi nolga teng, narxi bir necha
/// millisekund. HR ko'radigan raqamning to'g'riligi bu narxdan qimmat.
/// </para>
/// <para>
/// <b>Nega <c>EXISTS</c>, <c>DISTINCT</c> emas.</b> Barcha xodim sonlari
/// <c>Employees</c> jadvalidan sanaladi va munosabat shart sifatida
/// tekshiriladi (<c>employees.Count(x =&gt; employments.Any(...))</c>).
/// Buning ikki foydasi bor: ikki firmada ishlaydigan odam TA'RIFI BO'YICHA
/// bir marta sanaladi (qator xodimniki, shartnomaniki emas), va yumshoq
/// o'chirilgan xodimning munosabati sanoqqa tushib qolmaydi — global
/// filtr <c>Employees</c> ga ham qo'llanadi.
/// </para>
/// <para>
/// <b>Bu yerda <c>ToListAsync</c> yo'q.</b> Barcha filtrlash va sanash
/// bazada bajariladi; C# ga faqat tayyor sonlar qaytadi.
/// </para>
/// </remarks>
public sealed class DashboardReadModel : IDashboardReadModel
{
    private readonly AppDbContext _context;

    public DashboardReadModel(AppDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task<DashboardCounts> GetCountsAsync(
        DashboardCountsRange range,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(range);

        // ── Xodimlar ────────────────────────────────────────────────
        //
        // Har bir shart «shunday munosabati BOR odam» degan ma'noni beradi.
        // Holatlar bir-birini istisno qilmaydi: sinovda turgan odam boshqa
        // firmada doimiy bo'lishi mumkin va u ikkala sonda ham ko'rinadi.
        // Bu ataylab — kartalar bir-biriga qo'shilmaydi, har biri o'z
        // savoliga javob beradi.
        var active = await CountEmployeesAsync(
            employment => employment.Status == EmploymentStatus.Active,
            cancellationToken).ConfigureAwait(false);

        var probation = await CountEmployeesAsync(
            employment => employment.Status == EmploymentStatus.Probation,
            cancellationToken).ConfigureAwait(false);

        // «Ishdan ketganlar» — arxiv, ya'ni endi HECH QAYERDA ishlamaydigan
        // odamlar. Oddiy `Status == Terminated` sanog'i noto'g'ri bo'lardi:
        // VIKOR'dan ketib Вело унион'da qolgan o'rindosh ham «ketgan»
        // bo'lib chiqardi va bir vaqtning o'zida «xodimlar» ichida ham
        // turardi. HR bunday raqamga qarab qaror qabul qila olmaydi.
        var former = await _context.Employees
            .AsNoTracking()
            .CountAsync(
                employee =>
                    _context.Employments.Any(employment =>
                        employment.EmployeeId == employee.Id
                        && employment.Status == EmploymentStatus.Terminated)
                    && !_context.Employments.Any(employment =>
                        employment.EmployeeId == employee.Id
                        && employment.Status != EmploymentStatus.Terminated),
                cancellationToken)
            .ConfigureAwait(false);

        // ── Davr ────────────────────────────────────────────────────
        //
        // Qabul sanasi munosabatning O'ZIDA turadi (`HireDate`), buyruqlar
        // oqimida emas. Nega: Excel'dan ko'chirilgan yozuvlarning bir
        // qismida qabul HODISASI yo'q, lekin sana doim bor. Hodisalar
        // bo'yicha sanasak, arxiv ma'lumoti jimgina yo'qolardi.
        var hired = await CountEmployeesAsync(
            employment => employment.HireDate >= range.From && employment.HireDate <= range.To,
            cancellationToken).ConfigureAwait(false);

        // `TerminationDate` — `null` bo'la oladigan ustun. SQL'da `NULL`
        // taqqoslashi hech qachon rost bo'lmaydi, ya'ni hali ishlayotgan
        // munosabat o'z-o'zidan chetda qoladi; qo'shimcha shart kerak emas.
        var left = await CountEmployeesAsync(
            employment => employment.TerminationDate >= range.From
                && employment.TerminationDate <= range.To,
            cancellationToken).ConfigureAwait(false);

        var hiredPrevious = await CountEmployeesAsync(
            employment => employment.HireDate >= range.PreviousFrom
                && employment.HireDate <= range.PreviousTo,
            cancellationToken).ConfigureAwait(false);

        var leftPrevious = await CountEmployeesAsync(
            employment => employment.TerminationDate >= range.PreviousFrom
                && employment.TerminationDate <= range.PreviousTo,
            cancellationToken).ConfigureAwait(false);

        // ── Tug'ilgan kunlar ────────────────────────────────────────
        //
        // Shart shu yerda yozilmaydi — u `BirthdayQuery` da, YAGONA
        // nusxada turadi. Sabab: aynan shu odamlar ro'yxati ikki joyda
        // qayta so'raladi (bu son bosilganda ochiladigan
        // `ListBirthdaysTodayAsync`, hamda bildirishnomalar) va uchalasi
        // BIR XIL javob berishi SHART. Shart bu yerda takrorlansa, ekranda
        // «Bugun 2 ta xodimning tug'ilgan kuni» yozuvi ochilib, ichidan
        // uchta odam chiqishi mumkin edi — batafsil sabab `BirthdayQuery`
        // izohida.
        var birthdays = await BirthdayQuery
            .WithBirthdayOn(_context, range.Today)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        // ── Ma'lumotnomalar ─────────────────────────────────────────
        //
        // Faqat FAOL yozuvlar. Yumshoq o'chirilganlari global filtr bilan
        // allaqachon chetda; faolsizlantirilganlarini esa shu yerda
        // ajratamiz (izohi `DashboardCatalogSizesDto` da).
        var departments = await _context.Departments
            .AsNoTracking()
            .CountAsync(department => department.IsActive, cancellationToken)
            .ConfigureAwait(false);

        var positions = await _context.Positions
            .AsNoTracking()
            .CountAsync(position => position.IsActive, cancellationToken)
            .ConfigureAwait(false);

        var directions = await _context.Directions
            .AsNoTracking()
            .CountAsync(direction => direction.IsActive, cancellationToken)
            .ConfigureAwait(false);

        var legalEntities = await _context.LegalEntities
            .AsNoTracking()
            .CountAsync(legalEntity => legalEntity.IsActive, cancellationToken)
            .ConfigureAwait(false);

        return new DashboardCounts(
            active,
            probation,
            former,
            hired,
            left,
            hiredPrevious,
            leftPrevious,
            birthdays,
            departments,
            positions,
            directions,
            legalEntities);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>Vakansiya soni va o'rinlar yig'indisi — IKKITA alohida so'rov,
    /// bitta emas.</b> Ularni bitta guruhlangan so'rovga yig'ish mumkin
    /// edi, lekin natija bir xil narxda va o'qilishi yomonroq bo'lardi:
    /// ikkala so'rov ham AYNI indeks (holat) bo'yicha ketadi va bittadan
    /// son qaytaradi. Muhimi — ular ikki xil savolga javob beradi va shu
    /// ikkilik kodda ham ko'rinib tursin (sabab
    /// <c>DashboardRecruitingCounts</c> izohida).
    /// </para>
    /// <para>
    /// <b>«Yangi ariza» sanog'i uchun oraliq KUNDAN LAHZAGA aylantiriladi.</b>
    /// <c>Candidate.CreatedAt</c> — <c>timestamptz</c>, davr chegarasi esa
    /// kun. Oddiy <c>ToDateTime(...)</c> UTC yarim tunini berardi va oyna
    /// besh soatga surilib qolardi: kechqurun tushgan ariza ertasi kunga
    /// tushardi. Shu sababli chegara Toshkent yarim tunidan olinadi —
    /// <c>NotificationReadModel</c> dagi bilan AYNAN bir xil qoida va
    /// AYNAN bir xil siljish manbai (<see cref="SystemClock.TashkentOffset"/>):
    /// ikkinchi nusxa yozilsa, «bugun» ikki ekranda ikki xil chiqardi.
    /// </para>
    /// </remarks>
    public async Task<DashboardRecruitingCounts> GetRecruitingCountsAsync(
        DashboardCountsRange range,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(range);

        // ── Vakansiyalar ────────────────────────────────────────────
        //
        // `Open` — «nomzod qabul qiladigan» holat (`Vacancy.Status`).
        // Yopilgani sanalmaydi: karta «hozir nima ustida ishlanyapti»
        // degan savolga javob beradi.
        var openVacancies = await _context.Vacancies
            .AsNoTracking()
            .CountAsync(vacancy => vacancy.Status == VacancyStatus.Open, cancellationToken)
            .ConfigureAwait(false);

        // O'rinlar yig'indisi ATAYLAB `(int?)` ga keltirilgan: ochiq
        // vakansiya bo'lmasa SQL `SUM` NULL qaytaradi va uni to'g'ridan-
        // to'g'ri `int` ga o'qish istisno berardi. `?? 0` bilan «ochiq
        // vakansiya yo'q» holati o'zining tabiiy javobini oladi — nol.
        var openings = await _context.Vacancies
            .AsNoTracking()
            .Where(vacancy => vacancy.Status == VacancyStatus.Open)
            .SumAsync(vacancy => (int?)vacancy.Openings, cancellationToken)
            .ConfigureAwait(false) ?? 0;

        // ── Arizalar (nomzodlar) ────────────────────────────────────
        //
        // Faol voronka — joriy holat, davrga bog'liq emas. Rad etilgan,
        // ishga olingan va voz kechganlar chetda: ular bilan endi
        // ishlanmaydi.
        var activeCandidates = await _context.Candidates
            .AsNoTracking()
            .CountAsync(
                candidate => candidate.Status == CandidateStatus.InProgress,
                cancellationToken)
            .ConfigureAwait(false);

        // Davr chegaralari Toshkent yarim tunida (izohi yuqorida).
        // `ToUniversalTime()` MAJBURIY: Npgsql `timestamptz` ustuniga faqat
        // nol siljishli parametr yuboradi, aks holda istisno tashlanadi.
        // Lahza o'zgarmaydi — faqat ifodalanishi.
        var from = new DateTimeOffset(
                range.From.ToDateTime(TimeOnly.MinValue),
                SystemClock.TashkentOffset)
            .ToUniversalTime();

        // Yuqori chegara — KEYINGI kunning boshi va u QO'SHILMAYDI (`<`).
        // Sabab: oraliq kunlarda yopiq (`To` kuni ham kiradi), lekin
        // vaqt bilan «kunning oxirigacha» ni yozishga urinish klassik
        // 23:59:59.999 xatosini keltirardi — yarim tundan oldingi
        // millisekundlarda kelgan ariza sanoqdan tushib qolardi.
        var toExclusive = new DateTimeOffset(
                range.To.AddDays(1).ToDateTime(TimeOnly.MinValue),
                SystemClock.TashkentOffset)
            .ToUniversalTime();

        // Holat bo'yicha filtr YO'Q va bu ataylab: davrda kelgan ariza
        // keyin rad etilgan bo'lsa ham, u KELGAN. Aks holda eski davrlar
        // raqami vaqt o'tishi bilan o'z-o'zidan kamayib borardi va
        // taqqoslash ma'nosini yo'qotardi.
        //
        // MANBA bo'yicha esa filtr BOR: Excel'dan ko'chirilgan kengash
        // arxivida `CreatedAt` — ko'chirish kuni, ariza kelgan kun emas.
        // Usiz ko'chirish kuni bosh sahifada 3110 ta «yangi ariza»
        // ko'rinardi va bu raqam bilan bir necha oy yashash kerak
        // bo'lardi (davr o'tmagunicha).
        var newCandidates = await _context.Candidates
            .AsNoTracking()
            .CountAsync(
                candidate => candidate.Source != CandidateSource.Excel
                    && candidate.CreatedAt >= from
                    && candidate.CreatedAt < toExclusive,
                cancellationToken)
            .ConfigureAwait(false);

        return new DashboardRecruitingCounts(
            openVacancies,
            openings,
            activeCandidates,
            newCandidates);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Uch qadam: (1) har bir FAOL xodimning bitta VAKILLIK QILUVCHI
    /// munosabati tanlanadi — xuddi <c>EmployeeReadModel.SearchEmployeesAsync</c>
    /// dagi kabi (asosiy, bo'lmasa eng so'nggi ishga kirgan); (2) bo'limlar
    /// daraxti xotiraga olinib, har bir bo'lim ILDIZ ajdodiga (departament)
    /// bog'lanadi — jadval kichik (o'nlab-yuzlab yozuv), bitta so'rovda
    /// olib, C# da aylanish rekursiv SQL yozishdan arzonroq; (3) sonlar
    /// C# da yig'iladi, chunki guruhlash ILDIZ bo'yicha, bazadagi ustun
    /// bevosita bo'yicha emas.
    /// </para>
    /// </remarks>
    public async Task<DashboardBreakdown> GetBreakdownAsync(CancellationToken cancellationToken = default)
    {
        // Har bir FAOL (doimiy) xodimning vakillik qiluvchi munosabati.
        // Bir xodimda ikkita faol munosabat bo'lishi kamdan-kam (o'rindoshlik
        // odatda bittasi sinovda/arxivda bo'ladi), lekin naqsh boshqa
        // o'qish so'rovlari bilan bir xil bo'lishi uchun shu yerda ham
        // qo'llanadi — aks holda «necha kishi» degan savolga ikki xil
        // o'qish modeli ikki xil javob berardi.
        var rows = await (
                from employee in _context.Employees.AsNoTracking()
                from employment in _context.Employments.AsNoTracking()
                    .Where(e => e.EmployeeId == employee.Id && e.Status == EmploymentStatus.Active)
                    .OrderByDescending(e => e.IsPrimary)
                    .ThenByDescending(e => e.HireDate)
                    .ThenByDescending(e => e.Id)
                    .Take(1)
                select new { employee.Id, employee.DirectionIds, employment.DepartmentId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // ── Bo'lim: har biri ILDIZ ajdodiga yig'iladi ──────────────────
        //
        // Daraxt kichik (amalda ~100 yozuv), shu sababli bitta so'rovda
        // xotiraga olinadi va ildiz C# da topiladi. Soft-delete filtri
        // ATAYLAB o'chirilgan (`IgnoreQueryFilters`): xodim munosabati
        // faolsizlantirilgan yoki yumshoq o'chirilgan bo'limga ham
        // ishora qilishi mumkin va bunday zanjirni uzib qo'yish
        // «Belgilanmagan» ga tushirib yuborardi — bo'lim aslida bor.
        var departments = await _context.Departments
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(department => new { department.Id, department.ParentId, department.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var departmentById = departments.ToDictionary(department => department.Id);

        (Guid Id, string Name) RootDepartment(Guid startId)
        {
            var currentId = startId;

            // Halqadan himoya: daraxt nazariy jihatdan buzilishi mumkin
            // (`Department.MoveTo` buni tekshira olmaydi). Bo'lmasa bu
            // aylanish cheksiz davom etardi.
            for (var qadam = 0; qadam < departments.Count + 1; qadam++)
            {
                if (!departmentById.TryGetValue(currentId, out var current))
                {
                    // Zanjir uzilgan (masalan ota butunlay o'chirilgan) —
                    // oxirgi ma'lum bo'g'in ildiz deb olinadi.
                    return (currentId, startId == currentId ? "Noma'lum bo'lim" : departmentById[startId].Name);
                }

                if (current.ParentId is not { } parentId)
                {
                    return (current.Id, current.Name);
                }

                currentId = parentId;
            }

            // Halqa aniqlandi — boshlang'ich bo'limning o'zi «ildiz» deb olinadi.
            var boshlangich = departmentById[startId];
            return (boshlangich.Id, boshlangich.Name);
        }

        var departmentBuckets = new Dictionary<Guid, (string Name, int Count)>();
        var departmentUnassigned = 0;

        foreach (var row in rows)
        {
            if (row.DepartmentId is not { } departmentId)
            {
                departmentUnassigned++;
                continue;
            }

            var (rootId, rootName) = RootDepartment(departmentId);

            departmentBuckets[rootId] = departmentBuckets.TryGetValue(rootId, out var mavjud)
                ? (rootName, mavjud.Count + 1)
                : (rootName, 1);
        }

        List<DashboardBreakdownItem> departmentItems =
        [
            .. departmentBuckets
                .Select(pair => new DashboardBreakdownItem(pair.Key, pair.Value.Name, pair.Value.Count))
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.Name, StringComparer.Ordinal),
        ];

        if (departmentUnassigned > 0)
        {
            departmentItems.Add(new DashboardBreakdownItem(null, "Belgilanmagan", departmentUnassigned));
        }

        // ── Yo'nalish: tekis ro'yxat, ko'p-ga-ko'p ──────────────────────
        //
        // Bitta xodim bir nechta yo'nalishga tegishli bo'lishi mumkin —
        // yig'indi shu sababli `ActiveEmployees` dan OSHISHI mumkin
        // (izohi `DashboardBreakdownItem` da).
        var directionNames = await _context.Directions
            .AsNoTracking()
            .Select(direction => new { direction.Id, direction.Name })
            .ToDictionaryAsync(direction => direction.Id, direction => direction.Name, cancellationToken)
            .ConfigureAwait(false);

        var directionCounts = new Dictionary<Guid, int>();
        var directionUnassigned = 0;

        foreach (var row in rows)
        {
            if (row.DirectionIds.Count == 0)
            {
                directionUnassigned++;
                continue;
            }

            foreach (var directionId in row.DirectionIds)
            {
                directionCounts[directionId] = directionCounts.GetValueOrDefault(directionId) + 1;
            }
        }

        List<DashboardBreakdownItem> directionItems =
        [
            .. directionCounts
                .Select(pair => new DashboardBreakdownItem(
                    pair.Key,
                    directionNames.TryGetValue(pair.Key, out var name) ? name : "Noma'lum yo'nalish",
                    pair.Value))
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.Name, StringComparer.Ordinal),
        ];

        if (directionUnassigned > 0)
        {
            directionItems.Add(new DashboardBreakdownItem(null, "Belgilanmagan", directionUnassigned));
        }

        return new DashboardBreakdown(departmentItems, directionItems);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>Shart bu yerda YO'Q</b> — u <see cref="BirthdayQuery.WithBirthdayOn"/>
    /// dan keladi, ya'ni bu ro'yxat <see cref="GetCountsAsync"/> dagi
    /// <c>birthdaysToday</c> SONI bilan bir xil so'rovdan o'sib chiqadi.
    /// Ikkalasi bir xil satrni ikki marta yozgan bo'lsa, ular birinchi
    /// o'zgarishdayoq ajralib ketardi va ekranda son bilan ro'yxat mos
    /// kelmay qolardi.
    /// </para>
    /// <para>
    /// <b>Munosabat uchun <c>DefaultIfEmpty()</c> KERAK EMAS.</b> Xodim bu
    /// ro'yxatga aynan ishlayotgan munosabati borligi uchun tushgan
    /// (shartning ikkinchi yarmi), ya'ni ichma-ich so'rov hech qachon
    /// bo'sh qaytmaydi. Chapdan biriktirish yozish «bo'lmasligi ham
    /// mumkin» degan yolg'on va'da bo'lardi.
    /// </para>
    /// <para>
    /// <b>Nomlar CHAPDAN biriktiriladi.</b> Bo'lim yoki lavozim
    /// ko'rsatilmagan bo'lishi mumkin (Excel'dan ko'chirilgan yozuvlarda
    /// odatiy hol) va bunday xodim ro'yxatdan tushib qolmasligi kerak —
    /// uning tug'ilgan kuni bo'limidan qat'i nazar bugun
    /// (<c>NotificationReadModel</c> dagi bilan bir xil sabab).
    /// </para>
    /// <para>
    /// <b>Tartib — F.I.Sh. bo'yicha, bazada.</b> Ro'yxatda «muhimlik»
    /// tushunchasi yo'q: hamma bir xil bayram qiladi. Alifbo tartibi esa
    /// barqaror va u tizimdagi qolgan ro'yxatlar bilan bir xil his beradi.
    /// Oxirgi mezon — <c>Id</c>: to'liq bir xil ismli ikki xodimda tartib
    /// so'rovdan so'rovga sakramasligi kerak.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<BirthdayEmployeeRow>> ListBirthdaysTodayAsync(
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        var rows = await (
                from employee in BirthdayQuery.WithBirthdayOn(_context, today)

                    // Vakillik qiluvchi munosabat: asosiysi, u bo'lmasa eng
                    // so'nggi ishga kirgani — `SearchEmployeesAsync` va
                    // `GetBreakdownAsync` dagi bilan AYNAN bir xil naqsh.
                    // Manba esa `WorkingEmployments`: ro'yxatdagi odam
                    // «ishlayapti» deb turib, yonida uch yil oldin yopilgan
                    // shartnomasining bo'limi yozilib qolmasligi uchun.
                from employment in BirthdayQuery.WorkingEmployments(_context)
                    .Where(candidate => candidate.EmployeeId == employee.Id)
                    .OrderByDescending(candidate => candidate.IsPrimary)
                    .ThenByDescending(candidate => candidate.HireDate)
                    .ThenByDescending(candidate => candidate.Id)
                    .Take(1)
                join department in _context.Departments.AsNoTracking()
                    on employment.DepartmentId equals (Guid?)department.Id into departmentMatch
                from department in departmentMatch.DefaultIfEmpty()
                join position in _context.Positions.AsNoTracking()
                    on employment.PositionId equals (Guid?)position.Id into positionMatch
                from position in positionMatch.DefaultIfEmpty()
                orderby employee.FullName.LastName, employee.FullName.FirstName, employee.Id
                select new
                {
                    employee.Id,
                    employee.FullName.LastName,
                    employee.FullName.FirstName,
                    employee.FullName.MiddleName,
                    employee.PhotoKey,
                    employee.BirthDate,
                    DepartmentName = department!.Name,
                    PositionName = position!.Name,
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. rows.Select(row => new BirthdayEmployeeRow(
                row.Id,
                // Ism uchta ustundan yig'iladi — barcha o'qish modellarida
                // bir xil qoida (`FromTrusted`: bazadagi qiymat allaqachon
                // tekshirilgan, uni qayta validatsiya qilish shart emas).
                FullName.FromTrusted(row.LastName, row.FirstName, row.MiddleName).Full,
                row.PhotoKey,
                row.PositionName,
                row.DepartmentName,
                // `!` xavfsiz: `BirthDate == null` bo'lgan xodim shartdan
                // o'tolmaydi (tug'ilgan kuni yo'q odamning bayrami ham yo'q).
                row.BirthDate!.Value)),
        ];
    }

    /// <summary>
    /// Berilgan shartga mos munosabati BOR xodimlarni sanaydi.
    /// </summary>
    /// <remarks>
    /// Natija ODAM soni: bir xodimning uchta mos munosabati bo'lsa ham
    /// u bir marta sanaladi. Aynan shu sababli sanoq <c>Employments</c>
    /// dan emas, <c>Employees</c> dan boshlanadi.
    /// </remarks>
    private Task<int> CountEmployeesAsync(
        Expression<Func<Employment, bool>> shart,
        CancellationToken cancellationToken)
    {
        // Shart ichma-ich so'rovga uzatilishi uchun uni `Employments`
        // ustidagi so'rovga qo'llab, so'ng «bo'sh emasmi» deb tekshiramiz.
        // Ifodani qo'lda birlashtirish shart emas — EF ichma-ich
        // so'rovni `EXISTS (...)` ga aylantiradi.
        var mos = _context.Employments.Where(shart);

        return _context.Employees
            .AsNoTracking()
            .CountAsync(
                employee => mos.Any(employment => employment.EmployeeId == employee.Id),
                cancellationToken);
    }
}
