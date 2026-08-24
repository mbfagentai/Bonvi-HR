using System.Linq.Expressions;
using Bonvi.Application.Dashboard.Abstractions;
using Bonvi.Domain.Employees;
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
        // Yil e'tiborga olinmaydi — faqat kun va oy. PostgreSQL buni
        // `date_part` bilan bajaradi, ya'ni sana ustunini C# ga olib
        // chiqish shart emas.
        //
        // Faqat ishlayotganlar: arxivdagi odamning tug'ilgan kuni bilan
        // HR ni tabriklashga chaqirish ma'nosiz bo'lardi. Sinovdagilar ham
        // kiradi — ular ham bugun ishga keladi.
        var birthdays = await _context.Employees
            .AsNoTracking()
            .CountAsync(
                employee =>
                    employee.BirthDate != null
                    && employee.BirthDate.Value.Month == range.Today.Month
                    && employee.BirthDate.Value.Day == range.Today.Day
                    && _context.Employments.Any(employment =>
                        employment.EmployeeId == employee.Id
                        && (employment.Status == EmploymentStatus.Active
                            || employment.Status == EmploymentStatus.Probation)),
                cancellationToken)
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
