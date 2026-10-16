using Bonvi.Application.Employees.Abstractions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Persistence.ReadModels;

/// <summary>
/// <see cref="IIncentiveReadModel"/> ning EF Core orqali amalga oshirilishi —
/// «Rag'bat va jarima» ro'yxati uchun proyeksiya so'rovi.
/// </summary>
/// <remarks>
/// <para>
/// Fayl <c>Persistence/ReadModels</c> da, repozitoriylar yonida emas —
/// sababi <see cref="EmployeeReadModel"/> izohida batafsil: bu yerdagi
/// so'rovlar agregat qaytarmaydi va hech qachon saqlanmaydi.
/// </para>
/// <para>
/// <b>Filtr, saralash va sahifalash BAZADA.</b> Shu sababli
/// <c>ToListAsync</c> dan keyin birorta ham <c>Where</c> yoki
/// <c>OrderBy</c> yo'q: materializatsiyadan so'ng faqat qiymat
/// obyektlarini yig'ish qoladi (<c>FullName</c> → satr, <c>Money</c> →
/// summa + valyuta) va tur nomini lug'atdan olish.
/// </para>
/// </remarks>
public sealed class IncentiveReadModel : IIncentiveReadModel
{
    private readonly AppDbContext _context;
    private readonly IDepartmentRepository _departments;

    public IncentiveReadModel(AppDbContext context, IDepartmentRepository departments)
    {
        _context = context;
        _departments = departments;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// So'rov uch qadamdan iborat va uchalasi ham serverda bajariladi:
    /// </para>
    /// <list type="number">
    ///   <item>yozuvlar filtrlanadi (tur, sana oralig'i);</item>
    ///   <item>xodim bilan birlashtiriladi — ism, rasm va (kerak bo'lsa)
    ///         bo'lim / firma sharti shu yerdan keladi;</item>
    ///   <item>saralash va sahifalash qo'llanadi.</item>
    /// </list>
    /// <para>
    /// Tur NOMI ataylab alohida so'rovda va faqat SHU SAHIFADAGI turlar
    /// uchun olinadi — sababi <see cref="TypeNamesAsync{TType}"/> izohida.
    /// </para>
    /// <para>
    /// Umumiy son sahifalashdan OLDIN va tur nomlarisiz sanaladi: nom
    /// qidirish alohida so'rov, ya'ni <c>COUNT(*)</c> ga umuman ta'sir
    /// qilmaydi.
    /// </para>
    /// </remarks>
    public async Task<IncentiveListPage> ListIncentivesAsync(
        IncentiveListFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var incentives = _context.Incentives.AsNoTracking();

        if (filter.Kind is { } kind)
        {
            incentives = incentives.Where(incentive => incentive.Kind == kind);
        }

        // Sana oralig'i IKKI TOMONDAN yopiq: HR «1 avgustdan 31 avgustgacha»
        // deganda 31-avgustdagi jarima ham kirishi kerak (buyruqlar
        // ro'yxatidagi bilan bir xil qoida — ikkita ekran bir xil
        // parametrga bir xil javob berishi shart).
        if (filter.From is { } from)
        {
            incentives = incentives.Where(incentive => incentive.Date >= from);
        }

        if (filter.To is { } to)
        {
            incentives = incentives.Where(incentive => incentive.Date <= to);
        }

        var employees = EmployeeSearchQuery.Matching(_context, filter.Search);

        var matched =
            from incentive in incentives
            join employee in employees
                on incentive.EmployeeId equals employee.Id
            select new
            {
                Incentive = incentive,
                employee.FullName.LastName,
                employee.FullName.FirstName,
                employee.FullName.MiddleName,
                employee.PhotoKey,
            };

        // Bo'lim va firma — XODIMNING munosabatlari orqali.
        //
        // NEGA to'g'ridan-to'g'ri emas: rag'bat/jarima mehnat munosabatiga
        // emas, SHAXSGA bog'langan (sababi `Incentive` izohida — odam
        // boshqa firmaga o'tsa ham tarixi yo'qolmasligi kerak). Ya'ni
        // yozuvning o'zida bo'lim ham, firma ham yo'q va ular faqat
        // egasidan kelib chiqadi.
        //
        // NEGA «istalgan munosabat» yetarli: holdingda bitta odam ikki
        // firmada 0,5 stavkada ishlashi mumkin (PLAN.md, 1.2). «Sotuv
        // bo'limidagilarga qanday jarima berilgan» degan savolga u ham
        // javob bo'ladi — faqat asosiy munosabatga qarash o'rindoshlarni
        // jimgina tashlab yuborardi.
        var employmentFilterUsed = false;
        var employments = _context.Employments.AsNoTracking();

        if (filter.DepartmentId is { } departmentId)
        {
            // Butun shox olinadi, aniq moslik emas: xodimlar va buyruqlar
            // ro'yxatlarida bo'lim filtri allaqachon shunday ishlaydi va
            // bir xil parametr uch ekranda uch xil javob bermasligi kerak.
            var branch = await BranchIdsAsync(departmentId, cancellationToken).ConfigureAwait(false);

            employments = employments.Where(employment =>
                employment.DepartmentId != null && branch.Contains(employment.DepartmentId.Value));

            employmentFilterUsed = true;
        }

        if (filter.LegalEntityId is { } legalEntityId)
        {
            employments = employments.Where(employment => employment.LegalEntityId == legalEntityId);
            employmentFilterUsed = true;
        }

        if (employmentFilterUsed)
        {
            // Ikkala shart ham BITTA munosabatga qo'llanadi (ketma-ket
            // `Where`, ikkita alohida ro'yxatning kesishmasi emas): «VIKOR
            // firmasidagi Sotuv bo'limi» so'rovi aynan shu ma'noni
            // beradi — VIKOR'da omborchi, boshqa firmada sotuvchi bo'lgan
            // odam bu javobga tushmasligi kerak.
            var owners = employments.Select(employment => employment.EmployeeId);

            matched = matched.Where(row => owners.Contains(row.Incentive.EmployeeId));
        }

        var total = await matched.CountAsync(cancellationToken).ConfigureAwait(false);

        // Saralash shu yerda, alohida metodda emas: kalitlar anonim tipning
        // maydonlari va ularni metod chegarasidan o'tkazib bo'lmaydi
        // (`EmployeeReadModel` dagi bilan bir xil sabab).
        var ordered = filter.Sort switch
        {
            IncentiveSortField.EmployeeName => filter.Descending
                ? matched.OrderByDescending(row => row.LastName).ThenByDescending(row => row.FirstName)
                : matched.OrderBy(row => row.LastName).ThenBy(row => row.FirstName),
            _ => filter.Descending
                ? matched.OrderByDescending(row => row.Incentive.Date)
                : matched.OrderBy(row => row.Incentive.Date),
        };

        var page = await ordered
            // Oxirgi mezon sahifalash uchun MAJBURIY: bir kunda o'nlab
            // yozuv bo'lishi mumkin (bitta buyruq bilan yigirma kishiga
            // mukofot) va teng qiymatlarda PostgreSQL tartibni
            // kafolatlamaydi — bitta yozuv ikkinchi sahifada takrorlanib,
            // boshqasi umuman tushib qolardi.
            //
            // Yo'nalish DOIM kamayish: `Id` — v7 GUID, ya'ni yozilgan vaqt
            // bo'yicha o'sadi, teskari tartibda esa «oxirgi kiritilgani
            // tepada» degan ma'noni beradi. Bu sukut bo'yicha sana
            // tartibi bilan bir yo'nalishda qoladi va sahifalashni
            // saralash ustunidan qat'i nazar barqaror qiladi.
            .ThenByDescending(row => row.Incentive.Id)
            .Skip(filter.Skip)
            .Take(filter.PageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var awardNames = await TypeNamesAsync(
                _context.AwardTypes,
                [.. page
                    .Where(row => row.Incentive.Kind == IncentiveKind.Award)
                    .Select(row => row.Incentive.TypeId)
                    .Distinct()],
                cancellationToken)
            .ConfigureAwait(false);

        var penaltyNames = await TypeNamesAsync(
                _context.PenaltyTypes,
                [.. page
                    .Where(row => row.Incentive.Kind == IncentiveKind.Penalty)
                    .Select(row => row.Incentive.TypeId)
                    .Distinct()],
                cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<IncentiveListRow> items =
        [
            .. page.Select(row => new IncentiveListRow(
                row.Incentive.Id,
                row.Incentive.EmployeeId,
                FullName.FromTrusted(row.LastName, row.FirstName, row.MiddleName).Full,
                row.PhotoKey,
                row.Incentive.Kind,
                row.Incentive.TypeId,
                TypeName(row.Incentive, awardNames, penaltyNames),
                row.Incentive.Date,
                row.Incentive.Amount?.Amount,
                row.Incentive.Amount?.Currency,
                row.Incentive.Percent,
                row.Incentive.OrderNo,
                row.Incentive.OrderDate,
                row.Incentive.Reason,
                row.Incentive.GiftDescription)),
        ];

        return new IncentiveListPage(items, total);
    }

    // ════════════════════════════════════════════════════════════
    //  Yordamchilar
    // ════════════════════════════════════════════════════════════

    /// <summary>Bo'lim va uning BARCHA ichki bo'limlari identifikatorlari.</summary>
    /// <remarks>
    /// Daraxt bo'yicha aylanish <c>DepartmentRepository</c> dagi rekursiv
    /// CTE bilan bajariladi — u halqadan himoyalangan va allaqachon
    /// sinovdan o'tgan. Uni bu yerda qayta yozish «avlod» tushunchasining
    /// uchinchi ta'rifini yaratardi.
    /// </remarks>
    private async Task<List<Guid>> BranchIdsAsync(Guid departmentId, CancellationToken cancellationToken)
    {
        var descendants = await _departments
            .GetDescendantIdsAsync(departmentId, cancellationToken)
            .ConfigureAwait(false);

        return [departmentId, .. descendants];
    }

    /// <summary>Berilgan ma'lumotnoma yozuvlarining nomlari.</summary>
    /// <remarks>
    /// <para>
    /// <b>Nega alohida so'rov, chapdan birlashtirish emas.</b> Tur ikkita
    /// ma'lumotnomaning BIRIDA yotadi va qaysi biri ekani <c>Kind</c> ga
    /// bog'liq (tashqi kalit yo'q — sababi <c>IncentiveConfiguration</c>
    /// da). Asosiy so'rovga ikkala jadval ham chapdan qo'shilib, keyin
    /// <c>CASE</c> bilan tanlansa, so'rov o'qilmaydigan darajada
    /// murakkablashardi. Nom esa faqat KO'RSATISH uchun: unga filtr ham,
    /// saralash ham qo'llanmaydi, ya'ni uni sahifalashdan keyin olish
    /// natijani o'zgartirmaydi. So'rov faqat shu sahifada uchraydigan
    /// turlar bo'yicha ketadi — odatda 5–10 ta yozuv.
    /// </para>
    /// <para>
    /// Ataylab faqat FAOL yozuvlar emas: faoliyatdan chiqarilgan tur ham
    /// nomi bilan ko'rinishi kerak — eski buyruqda unga havola qolgan va
    /// katakni bo'sh qoldirish «ma'lumot yo'qolgan» degan taassurot
    /// berardi (<c>EmployeeReadModel.DirectionNamesAsync</c> dagi bilan
    /// bir xil qoida).
    /// </para>
    /// </remarks>
    private static async Task<Dictionary<Guid, string>> TypeNamesAsync<TType>(
        IQueryable<TType> source,
        List<Guid> typeIds,
        CancellationToken cancellationToken)
        where TType : ReferenceEntity
    {
        if (typeIds.Count == 0)
        {
            return [];
        }

        var found = await source
            .AsNoTracking()
            .Where(type => typeIds.Contains(type.Id))
            .Select(type => new { type.Id, type.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return found.ToDictionary(type => type.Id, type => type.Name);
    }

    /// <summary>Yozuvning turi qaysi ma'lumotnomadan ekanini <c>Kind</c> hal qiladi.</summary>
    private static string? TypeName(
        Incentive incentive,
        Dictionary<Guid, string> awardNames,
        Dictionary<Guid, string> penaltyNames)
    {
        var names = incentive.Kind == IncentiveKind.Award ? awardNames : penaltyNames;

        return names.TryGetValue(incentive.TypeId, out var name) ? name : null;
    }
}
