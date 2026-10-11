using Bonvi.Application.PublicApi.Abstractions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Employees;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Persistence.ReadModels;

/// <summary>
/// <see cref="IPublicApiReadModel"/> ning EF Core orqali amalga oshirilishi —
/// tashqi loyihalarga beriladigan proyeksiyalar.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Bu faylning butun mazmuni — CHEGARA.</b> Har bir so'rov ustunlarni
/// ATAYLAB sanab o'tadi va agregatni butunligicha o'qimaydi. Sabab: xodimlar
/// jadvalida JSHSHIR, pasport, manzil, sudlanganlik va ko'rib chiqish
/// izohlari bor. Agregatni o'qib, keyin «keraksizini tashlab yuborish»
/// ishonchga tayanardi — bir unutilgan maydon esa ma'lumotni holdingning
/// boshqa bazasiga jimgina ko'chirib qo'yardi.
/// </para>
/// <para>
/// <b>Filtr, saralash va sahifalash BAZADA</b> — ichki o'qish modeli bilan
/// bir xil qoida: 2000+ yozuvni tarmoqdan olib o'tish mumkin emas.
/// </para>
/// <para>
/// <b>Hamma joyda <c>AsNoTracking</c></b>: bu yo'l faqat o'qish uchun va
/// ommaviy yuzada yozish UMUMAN yo'q.
/// </para>
/// </remarks>
public sealed class PublicApiReadModel : IPublicApiReadModel
{
    /// <summary>
    /// <c>/employees/deletions</c> bitta javobda beradigan eng katta yozuv soni.
    /// </summary>
    /// <remarks>
    /// Chegara ataylab: javob SAHIFALANMAYDI (SPEC §4) va u holda so'rov
    /// vaqt o'tib cheksiz o'sishi mumkin bo'lardi. Iste'molchi ro'yxatni
    /// <c>?since=</c> bilan kichik ushlab turadi — aynan shu uchun bu
    /// parametr bor. Chegaraga tegilsa, keyingi so'rov oxirgi
    /// <c>deletedAt</c> dan davom etadi.
    /// </remarks>
    public const int MaxDeletions = 5000;

    private readonly AppDbContext _context;
    private readonly IDepartmentRepository _departments;

    public PublicApiReadModel(AppDbContext context, IDepartmentRepository departments)
    {
        _context = context;
        _departments = departments;
    }

    // ════════════════════════════════════════════════════════════
    //  1. Xodimlar ro'yxati
    // ════════════════════════════════════════════════════════════

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// So'rov IKKI qadamda bajariladi:
    /// </para>
    /// <list type="number">
    ///   <item>filtrga mos xodimlarning KALITLARI tanlanadi, saralanadi va
    ///         sahifalanadi — bazada;</item>
    ///   <item>faqat shu sahifadagi kalitlar bo'yicha to'liq javob
    ///         yig'iladi (<see cref="AssembleAsync"/>).</item>
    /// </list>
    /// <para>
    /// ⭐ <b>Nega ichki ro'yxatdagi LATERAL naqshi emas.</b> U har bir
    /// xodimga BITTA munosabat biriktiradi — jadvalning bitta qatori uchun
    /// shu yetadi. Ommaviy javobda esa munosabatlar RO'YXATI bor, ya'ni
    /// baribir ikkinchi so'rov kerak. Ikki qadam esa N+1 emas: so'rovlar
    /// soni sahifadagi yozuvlar soniga BOG'LIQ EMAS (jami to'rtta).
    /// </para>
    /// </remarks>
    public async Task<PublicEmployeePage> SearchAsync(
        PublicEmployeeFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        // Tashqarida qidiruv FAQAT ism va telefon bo'yicha: JSHSHIR yoki
        // pasport bo'yicha topish imkoniyati qiymatni qaytarmasa ham
        // «bu odam shu yerda ishlaydi» degan faktni sizdirardi (egasining
        // qarori, 2026-09-18 — sababi `MatchingPublic` izohida).
        var employees = EmployeeSearchQuery.MatchingPublic(_context, filter.Search);

        if (filter.DirectionId is { } directionId)
        {
            employees = employees.Where(employee => employee.DirectionIds.Contains(directionId));
        }

        var allEmployments = _context.Employments.AsNoTracking();

        /*
          ⭐ Filtrlar faqat JAVOBDA KO'RINADIGAN munosabatlarga qo'llanadi
          (`AssembleAsync` dagi qoida): yakunlanmaganlari, ishdan ketgan
          xodimda esa ASOSIYSI (oxirgisi).

          Nega: qayta ishga olingan odamning 2021-yildagi tugagan davri ham
          bazada turadi. Filtr barcha davrlarga qarasa, u «ishdan ketganlar»
          va eski bo'lim ro'yxatiga ham tushardi, javobida esa `status:
          "Active"` va yangi bo'lim — iste'molchi ishlayotgan odamni ketgan
          deb belgilab qo'yardi (2026-10-11, №91 importidan keyin prod'da
          52 kishi).
        */
        var employments = allEmployments.Where(employment =>
            employment.Status != EmploymentStatus.Terminated
            || (!allEmployments.Any(other =>
                    other.EmployeeId == employment.EmployeeId && other.Status != EmploymentStatus.Terminated)
                && employment.Id == allEmployments
                    .Where(other => other.EmployeeId == employment.EmployeeId)
                    .OrderByDescending(other => other.IsPrimary)
                    .ThenByDescending(other => other.HireDate)
                    .ThenByDescending(other => other.Id)
                    .Select(other => other.Id)
                    .FirstOrDefault()));

        var employmentRequired = false;

        if (filter.LegalEntityId is { } legalEntityId)
        {
            employments = employments.Where(employment => employment.LegalEntityId == legalEntityId);
            employmentRequired = true;
        }

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

        // Holat — javobdagi yuqori darajadagi `status` bilan AYNAN bir xil
        // manba: ASOSIY munosabat (`AssembleAsync` dagi tartib). Ikkita
        // yakunlanmagan ish joyidan biri sinovda bo'lsa ham, odam faqat
        // bitta holat ro'yxatida chiqadi.
        if (filter.Status is { } status)
        {
            employees = employees.Where(employee => allEmployments
                .Where(employment => employment.EmployeeId == employee.Id)
                .OrderByDescending(employment => employment.IsPrimary)
                .ThenByDescending(employment => employment.HireDate)
                .ThenByDescending(employment => employment.Id)
                .Select(employment => (EmploymentStatus?)employment.Status)
                .FirstOrDefault() == status);
        }

        // Munosabatga tegishli filtr berilgan bo'lsa, mos munosabati
        // bo'lmagan xodim ro'yxatda ko'rinmasligi kerak. `Any` ataylab
        // (ichki ro'yxatdagi LATERAL emas): bu yerda MUNOSABAT emas,
        // XODIM tanlanadi va «bittasi mos kelsa yetadi» degan shart aynan
        // shu.
        if (employmentRequired)
        {
            employees = employees.Where(employee =>
                employments.Any(employment => employment.EmployeeId == employee.Id));
        }

        /*
          ⭐ `updatedAt` — XODIM kartasi va uning MUNOSABATLARIDAN eng
          keyingisi.

          Nega faqat `employees.updated_at` yetmaydi: xodimni boshqa
          bo'limga ko'chirish `employments` jadvalini o'zgartiradi, xodim
          kartasiga esa tegmaydi (ular alohida agregat). Ya'ni faqat
          kartaga qarasak, `?updatedSince=` bilan sinxronlanayotgan
          iste'molchi ko'chirishni HECH QACHON ko'rmasdi — javobdagi
          `department` eskiligicha qolib ketardi va buni hech qanday xato
          ko'rsatmasdi.

          ⚠️ Bu ifoda `AssembleAsync` dagi hisob bilan BIR XIL bo'lishi
          shart: bu yerda u FILTR va TARTIB uchun, u yerda esa javobga
          chiqadigan qiymat uchun. Ikkisi ajralsa, iste'molchi o'zi
          ko'rgan `updatedAt` ni keyingi so'rovda berib, o'sha yozuvni
          qaytadan olardi (yoki — yomonrog'i — o'tkazib yuborardi).
        */
        var stamped =
            from employee in employees
            let own = employee.UpdatedAt ?? employee.CreatedAt
            let jobs = _context.Employments
                .Where(employment => employment.EmployeeId == employee.Id)
                .Max(employment => (DateTimeOffset?)(employment.UpdatedAt ?? employment.CreatedAt))
            select new
            {
                employee.Id,
                employee.FullName.LastName,
                employee.FullName.FirstName,
                Changed = jobs != null && jobs.Value > own ? jobs.Value : own,
            };

        if (filter.UpdatedSince is { } since)
        {
            stamped = stamped.Where(row => row.Changed > since);
        }

        var total = await stamped.CountAsync(cancellationToken).ConfigureAwait(false);

        // Tartib: `updatedSince` berilgan bo'lsa — o'zgarish vaqti bo'yicha
        // O'SISH tartibida (bosqichma-bosqich sinxronlash shunda uzilmaydi),
        // aks holda ism bo'yicha. Oxirgi mezon DOIM `Id`: teng qiymatlarda
        // PostgreSQL tartibni kafolatlamaydi va bitta xodim ikkinchi
        // sahifada takrorlanib, boshqasi tushib qolishi mumkin edi.
        var ordered = filter.UpdatedSince is null
            ? stamped
                .OrderBy(row => row.LastName)
                .ThenBy(row => row.FirstName)
                .ThenBy(row => row.Id)
            : stamped
                .OrderBy(row => row.Changed)
                .ThenBy(row => row.Id);

        var keys = await ordered
            .Skip(filter.Skip)
            .Take(filter.PageSize)
            .Select(row => row.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = await AssembleAsync(keys, cancellationToken).ConfigureAwait(false);

        return new PublicEmployeePage(items, total);
    }

    // ════════════════════════════════════════════════════════════
    //  2. Bitta xodim
    // ════════════════════════════════════════════════════════════

    /// <inheritdoc />
    /// <remarks>
    /// Ro'yxat bilan AYNAN bir xil yig'uvchidan o'tadi
    /// (<see cref="AssembleAsync"/>): ikkinchi nusxa yozilsa, bittasiga
    /// qo'shilgan maydon ikkinchisida paydo bo'lmasdi — va «maydon sizib
    /// chiqishi» testi faqat bitta yo'lni qo'riqlardi.
    /// <para>
    /// Yumshoq o'chirilgan xodim <c>null</c> qaytaradi: global filtr uni
    /// avtomatik yashiradi va bu yerda <c>IgnoreQueryFilters</c> ATAYLAB
    /// yo'q.
    /// </para>
    /// </remarks>
    public async Task<PublicEmployeeRow?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rows = await AssembleAsync([id], cancellationToken).ConfigureAwait(false);

        return rows.Count == 0 ? null : rows[0];
    }

    // ════════════════════════════════════════════════════════════
    //  3. O'chirilganlar
    // ════════════════════════════════════════════════════════════

    /// <inheritdoc />
    /// <remarks>
    /// <c>IgnoreQueryFilters</c> — bu tizimda global yumshoq o'chirish
    /// filtrini ataylab chetlab o'tadigan kam sondagi joylardan biri:
    /// so'rovning butun maqsadi aynan o'chirilgan yozuvlarni ko'rish.
    /// Javobda esa faqat IKKITA maydon bor (<c>id</c>, <c>deletedAt</c>) —
    /// o'chirilgan xodim haqida boshqa hech narsa chiqmaydi.
    /// </remarks>
    public async Task<IReadOnlyList<PublicDeletionRow>> ListDeletionsAsync(
        DateTimeOffset? since,
        CancellationToken cancellationToken = default)
    {
        var deleted = _context.Employees
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(employee => employee.DeletedAt != null);

        if (since is { } from)
        {
            deleted = deleted.Where(employee => employee.DeletedAt > from);
        }

        var rows = await deleted
            // Tartib O'SISH bo'yicha: iste'molchi oxirgi ko'rgan
            // `deletedAt` ni keyingi so'rovning `since` iga beradi.
            .OrderBy(employee => employee.DeletedAt)
            .ThenBy(employee => employee.Id)
            .Take(MaxDeletions)
            .Select(employee => new { employee.Id, employee.DeletedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. rows.Select(row => new PublicDeletionRow(row.Id, row.DeletedAt!.Value))];
    }

    // ════════════════════════════════════════════════════════════
    //  4. Ma'lumotnomalar
    // ════════════════════════════════════════════════════════════

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Ota yozuv faqat BO'LIMLARDA bor (ular daraxt), qolgan uchtasida
    /// har doim <c>null</c>. Javob shakli baribir bir xil — sababi
    /// <c>PublicReferenceDto</c> izohida.
    /// </para>
    /// <para>
    /// ⚠️ <b>Saralash PROYEKSIYADAN OLDIN turadi va to'rt shox ataylab
    /// takrorlanadi.</b> Umumiy <c>IQueryable&lt;PublicReferenceRow&gt;</c>
    /// yasab, keyin <c>OrderBy(row =&gt; row.Name)</c> qilish tabiiyroq
    /// ko'rinadi, lekin EF Core buni tarjima QILA OLMAYDI: saralash kaliti
    /// konstruktor chaqiruvining maydoni bo'lib qoladi
    /// («could not be translated»). Har bir shox o'z entity'sini saralab,
    /// keyin qatorga aylantiradi — tartib bazada bajariladi.
    /// </para>
    /// <para>
    /// Alifbo tartibi — loyihadagi barcha ma'lumotnoma ro'yxatlari kabi.
    /// O'zbek kolatsiyasi ustun darajasida qo'yilgan
    /// (<c>DatabaseCollations</c>), shu sababli bu yerda qo'shimcha hech
    /// narsa kerak emas.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<PublicReferenceRow>> ListReferenceAsync(
        PublicReferenceKind kind,
        CancellationToken cancellationToken = default)
        => kind switch
        {
            PublicReferenceKind.Departments => await _context.Departments
                .AsNoTracking()
                .OrderBy(item => item.Name)
                .ThenBy(item => item.Id)
                .Select(item => new PublicReferenceRow(item.Id, item.Name, item.ParentId))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false),

            PublicReferenceKind.Positions => await _context.Positions
                .AsNoTracking()
                .OrderBy(item => item.Name)
                .ThenBy(item => item.Id)
                .Select(item => new PublicReferenceRow(item.Id, item.Name, null))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false),

            PublicReferenceKind.Directions => await _context.Directions
                .AsNoTracking()
                .OrderBy(item => item.Name)
                .ThenBy(item => item.Id)
                .Select(item => new PublicReferenceRow(item.Id, item.Name, null))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false),

            _ => await _context.LegalEntities
                .AsNoTracking()
                .OrderBy(item => item.Name)
                .ThenBy(item => item.Id)
                .Select(item => new PublicReferenceRow(item.Id, item.Name, null))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false),
        };

    // ════════════════════════════════════════════════════════════
    //  5. Rasm kaliti
    // ════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public Task<string?> FindPhotoKeyAsync(Guid employeeId, CancellationToken cancellationToken = default)
        => _context.Employees
            .AsNoTracking()
            .Where(employee => employee.Id == employeeId)
            .Select(employee => employee.PhotoKey)
            .FirstOrDefaultAsync(cancellationToken);

    // ════════════════════════════════════════════════════════════
    //  Yig'uvchi
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Berilgan kalitlar bo'yicha to'liq ommaviy javobni yig'adi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uchta so'rov — xodimlar, munosabatlar (nomlari bilan) va
    /// yo'nalish nomlari. Soni <b>sahifa hajmiga bog'liq emas</b>: har
    /// biri <c>WHERE id IN (…)</c> bilan bitta murojaatda ketadi.
    /// </para>
    /// <para>
    /// Natija <paramref name="ids"/> TARTIBIDA qaytadi: saralash bazada
    /// bajarilgan va uni bu yerda yo'qotib bo'lmaydi (aks holda sahifalar
    /// o'zaro aralashardi).
    /// </para>
    /// </remarks>
    private async Task<IReadOnlyList<PublicEmployeeRow>> AssembleAsync(
        List<Guid> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        // ⚠️ Ustunlar QO'LDA sanab o'tilgan va bu ataylab (sababi sinf
        // izohida). `Employee` agregatini butunligicha o'qish JSHSHIR,
        // pasport, manzil va sudlanganlik izohini xotiraga olib kelardi.
        var people = await _context.Employees
            .AsNoTracking()
            .Where(employee => ids.Contains(employee.Id))
            .Select(employee => new
            {
                employee.Id,
                employee.FullName.LastName,
                employee.FullName.FirstName,
                employee.FullName.MiddleName,
                employee.BirthDate,
                employee.Gender,
                employee.Phone,
                employee.TelegramUrl,
                employee.PhotoKey,
                employee.DirectionIds,
                Changed = employee.UpdatedAt ?? employee.CreatedAt,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (people.Count == 0)
        {
            return [];
        }

        // Munosabatlar NOMLARI bilan birga, chapdan birlashtirib: har bir
        // nom uchun ma'lumotnomaga alohida so'rov 50 qatorlik sahifada
        // 150 ta ortiqcha murojaat bo'lardi. Oklad ustuni bu yerda
        // UMUMAN o'qilmaydi.
        var jobs = await (
                from employment in _context.Employments
                where ids.Contains(employment.EmployeeId)
                join legalEntity in _context.LegalEntities
                    on employment.LegalEntityId equals legalEntity.Id into legalEntityMatch
                from legalEntity in legalEntityMatch.DefaultIfEmpty()
                join department in _context.Departments
                    on employment.DepartmentId equals (Guid?)department.Id into departmentMatch
                from department in departmentMatch.DefaultIfEmpty()
                join position in _context.Positions
                    on employment.PositionId equals (Guid?)position.Id into positionMatch
                from position in positionMatch.DefaultIfEmpty()
                select new
                {
                    employment.Id,
                    employment.EmployeeId,
                    employment.LegalEntityId,
                    LegalEntityName = legalEntity!.Name,
                    employment.DepartmentId,
                    DepartmentName = department!.Name,
                    employment.PositionId,
                    PositionName = position!.Name,
                    employment.Rate,
                    employment.Status,
                    employment.HireDate,
                    employment.TerminationDate,
                    employment.IsPrimary,
                    Changed = employment.UpdatedAt ?? employment.CreatedAt,
                })
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var byEmployee = jobs.GroupBy(job => job.EmployeeId).ToDictionary(group => group.Key, group => group.ToList());

        var directions = await DirectionNamesAsync(
                [.. people.SelectMany(person => person.DirectionIds).Distinct()],
                cancellationToken)
            .ConfigureAwait(false);

        var assembled = new List<PublicEmployeeRow>(people.Count);
        var order = people.ToDictionary(person => person.Id);

        foreach (var id in ids)
        {
            if (!order.TryGetValue(id, out var person))
            {
                continue;
            }

            var mine = byEmployee.TryGetValue(id, out var found) ? found : [];

            // ASOSIY munosabat — ichki ro'yxat bilan AYNAN bir xil qoida:
            // `IsPrimary`, keyin eng keyin boshlangani, keyin eng keyin
            // yaratilgani. Tartib bir xil bo'lmasa, ichki ekran va tashqi
            // API bitta odam uchun boshqa-boshqa lavozim ko'rsatardi.
            var primary = mine
                .OrderByDescending(job => job.IsPrimary)
                .ThenByDescending(job => job.HireDate)
                .ThenByDescending(job => job.Id)
                .FirstOrDefault();

            /*
              Javobga faqat YAKUNLANMAGAN munosabatlar tushadi (SPEC §5:
              «active»). Sabab: bitta odamda 2011-yildagi tugagan
              shartnoma ham bo'lishi mumkin va u iste'molchining
              «hozir kim qayerda ishlaydi» degan savoliga shovqin
              qo'shardi.

              ⚠️ Ishdan KETGAN xodimda faol munosabat qolmaydi — o'shanda
              ASOSIY (ya'ni oxirgi) munosabat YOLG'IZ o'zi qaytadi.
              Egasining qarori, 2026-09-18: boshqa loyihalar eski
              yozuvlarda «bu odam qaysi bo'lim va lavozimda edi» degan
              savolga javob bera olishi kerak. To'liq tarix baribir
              chiqmaydi — faqat bittasi, va u yuqori darajadagi `status`,
              `hireDate`, `terminationDate` bilan bir xil manbadan.
            */
            var current = mine.Where(job => job.Status != EmploymentStatus.Terminated).ToList();

            if (current.Count == 0 && primary is not null)
            {
                current = [primary];
            }

            var active = current
                .OrderByDescending(job => job.HireDate)
                .ThenByDescending(job => job.Id)
                .Select(job => new PublicEmploymentRow(
                    job.Id,
                    job.LegalEntityId,
                    job.LegalEntityName,
                    job.DepartmentId,
                    job.DepartmentName,
                    job.PositionId,
                    job.PositionName,
                    job.Rate,
                    job.Status,
                    job.HireDate,
                    job.TerminationDate,
                    primary is not null && job.Id == primary.Id))
                .ToList();

            // `updatedAt` — karta va munosabatlardan eng keyingisi.
            // ⚠️ `SearchAsync` dagi SQL ifodasi bilan bir xil qoida
            // (izohi o'sha yerda).
            var changed = person.Changed;
            foreach (var job in mine)
            {
                if (job.Changed > changed)
                {
                    changed = job.Changed;
                }
            }

            assembled.Add(new PublicEmployeeRow(
                person.Id,
                person.LastName,
                person.FirstName,
                person.MiddleName,
                person.BirthDate,
                person.Gender,
                person.Phone?.Value,
                person.TelegramUrl,
                person.PhotoKey,
                primary?.Status,
                primary?.HireDate,
                primary?.TerminationDate,
                active,
                [
                    // Nomi topilmagan yo'nalish TASHLAB YUBORILADI —
                    // ichki ro'yxatdagi bilan bir xil qoida: nomsiz
                    // identifikator iste'molchiga hech narsa demaydi.
                    .. person.DirectionIds
                        .Where(directions.ContainsKey)
                        .Select(directionId => new PublicNamedRow(directionId, directions[directionId])),
                ],
                changed));
        }

        return assembled;
    }

    /// <summary>Bo'lim va uning BARCHA ichki bo'limlari identifikatorlari.</summary>
    /// <remarks>
    /// Daraxt bo'yicha aylanish <c>DepartmentRepository</c> dagi rekursiv
    /// CTE bilan — ichki ro'yxat ham AYNAN shu metodni chaqiradi. Ikkinchi
    /// ta'rif yozilsa, «Sotuv bo'limi» ichki API'da bir, tashqisida boshqa
    /// ma'no olardi.
    /// </remarks>
    private async Task<List<Guid>> BranchIdsAsync(Guid departmentId, CancellationToken cancellationToken)
    {
        var descendants = await _departments
            .GetDescendantIdsAsync(departmentId, cancellationToken)
            .ConfigureAwait(false);

        return [departmentId, .. descendants];
    }

    /// <summary>Berilgan yo'nalishlarning nomlari.</summary>
    private async Task<Dictionary<Guid, string>> DirectionNamesAsync(
        List<Guid> directionIds,
        CancellationToken cancellationToken)
    {
        if (directionIds.Count == 0)
        {
            return [];
        }

        var found = await _context.Directions
            .AsNoTracking()
            .Where(direction => directionIds.Contains(direction.Id))
            .Select(direction => new { direction.Id, direction.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return found.ToDictionary(direction => direction.Id, direction => direction.Name);
    }
}
