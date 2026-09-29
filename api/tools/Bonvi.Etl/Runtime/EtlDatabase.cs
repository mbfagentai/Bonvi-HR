using Bonvi.Application.Abstractions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Catalog;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Feedback;
using Bonvi.Domain.Organization;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.Safety;
using Bonvi.Infrastructure.Persistence;
using Bonvi.Infrastructure.Persistence.Interceptors;
using Bonvi.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Bonvi.Etl.Runtime;

/// <summary>
/// ETL ko'chirilgan yozuvlarning muallifi.
/// </summary>
/// <remarks>
/// <c>UserId = null</c> ATAYLAB: ko'chirilgan yozuvni hech kim kiritmagan.
/// Uni biror foydalanuvchiga yozib qo'yish — soxta audit izi bo'lardi.
/// Excel'dan kelgan yozuvni ajratish uchun <c>EmploymentEvent.IsImported</c>
/// va <c>Employee.NeedsReview</c> bor.
/// </remarks>
internal sealed class EtlCurrentUser : ICurrentUser
{
    public Guid? UserId => null;

    public string? UserName => "etl";

    public bool IsAuthenticated => false;

    public bool HasPermission(string permission) => false;
}

/// <summary>
/// ETL uchun baza qatlami.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ulanishsiz ham ishlaydi.</b> <c>--dry-run</c> rejimida
/// <c>--connection</c> berilmasa, baza «bo'sh» deb hisoblanadi va butun
/// ko'chirish faqat Excel ustida tekshiriladi. Bu HR bilan hisobotni
/// muhokama qilish uchun yetarli va migratsiya hali tayyor bo'lmaganda ham
/// ishlaydi.
/// </para>
/// <para>
/// <b>Yozish faqat <c>--apply</c> bilan.</b> Quruq yurishda
/// <see cref="SaveStepAsync"/> hech narsa qilmaydi — hatto ulanish bo'lsa ham.
/// </para>
/// <para>
/// <b>Har bir qadam alohida tranzaksiyada.</b> Xato bo'lsa faqat o'sha qadam
/// qaytariladi: xodimlar yozilib, hodisalar yozilmay qolishi mumkin va bu
/// to'g'ri — ko'chirishni to'g'rilab, qolgan qadamni qayta yurgizsa bo'ladi.
/// </para>
/// </remarks>
public sealed class EtlDatabase : IAsyncDisposable
{
    private readonly AppDbContext? _context;
    private IDbContextTransaction? _transaction;

    private EtlDatabase(AppDbContext? context) => _context = context;

    /// <summary>Bazaga ulanganmi.</summary>
    public bool IsConnected => _context is not null;

    /// <summary>Ulanishsiz rejim — baza bo'sh deb qaraladi.</summary>
    public static EtlDatabase Offline() => new(null);

    /// <summary>PostgreSQL'ga ulanadi.</summary>
    public static EtlDatabase Connect(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)

            // Api bilan BIR XIL nomlash qoidasi. Aks holda ETL
            // «employees» o'rniga «Employees» jadvalini qidirardi.
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new AuditableInterceptor(new EtlCurrentUser(), new SystemClock()))
            .Options;

        return new EtlDatabase(new AppDbContext(options));
    }

    /// <summary>Ulanishni tekshiradi va jadvallar mavjudligiga ishonch hosil qiladi.</summary>
    public async Task EnsureReadyAsync(CancellationToken cancellationToken)
    {
        if (_context is null)
        {
            return;
        }

        if (!await _context.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException(
                "Bazaga ulanib bo'lmadi. Ulanish satrini va postgres ishlab turganini tekshiring.");
        }

        var pending = await _context.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false);
        var list = pending.ToList();

        if (list.Count > 0)
        {
            throw new InvalidOperationException(
                $"Bazada qo'llanmagan {list.Count} ta migratsiya bor ({string.Join(", ", list)}). " +
                "Avval «./bonvi db-update» ni bajaring — aks holda jadvallar mos kelmaydi.");
        }
    }

    // ────────────────────────────────────────────────────────────
    //  O'qish
    // ────────────────────────────────────────────────────────────

    /// <summary>Ma'lumotnomalarni kod → identifikator jadvaliga yig'adi.</summary>
    public async Task<CatalogIndex> LoadCatalogsAsync(CancellationToken cancellationToken)
    {
        if (_context is null)
        {
            return CatalogIndex.FromSeedFiles();
        }

        return CatalogIndex.FromTables(
            await NamesAsync<LegalEntity>(cancellationToken).ConfigureAwait(false),
            await NamesAsync<Direction>(cancellationToken).ConfigureAwait(false),
            await NamesAsync<Department>(cancellationToken).ConfigureAwait(false),
            await NamesAsync<Position>(cancellationToken).ConfigureAwait(false),
            await NamesAsync<TerminationReason>(cancellationToken).ConfigureAwait(false),
            await AwardTypesAsync(cancellationToken).ConfigureAwait(false),
            await PenaltyTypesAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Rag'bat turlari — identifikatori bilan birga «pullimi» belgisi ham.
    /// </summary>
    /// <remarks>
    /// <see cref="NamesAsync{TEntity}"/> ishlatilmaydi: <c>HasAmount</c>
    /// maydoni <c>ReferenceEntity</c> da yo'q va <c>AwardType</c> bilan
    /// <c>PenaltyType</c> ning boshqa umumiy ota-tipi ham yo'q. Ikkita qisqa
    /// metod yozish generikni shu ikki tur uchungina sun'iy interfeys bilan
    /// kengaytirishdan sodda va oshkora.
    /// </remarks>
    private async Task<Dictionary<string, CatalogEntry>> AwardTypesAsync(CancellationToken cancellationToken)
        => await _context!.AwardTypes
            .AsNoTracking()
            .ToDictionaryAsync(
                type => type.Name,
                type => new CatalogEntry(type.Id, type.HasAmount),
                StringComparer.OrdinalIgnoreCase,
                cancellationToken)
            .ConfigureAwait(false);

    /// <summary>Jarima turlari.</summary>
    private async Task<Dictionary<string, CatalogEntry>> PenaltyTypesAsync(CancellationToken cancellationToken)
        => await _context!.PenaltyTypes
            .AsNoTracking()
            .ToDictionaryAsync(
                type => type.Name,
                type => new CatalogEntry(type.Id, type.HasAmount),
                StringComparer.OrdinalIgnoreCase,
                cancellationToken)
            .ConfigureAwait(false);

    /// <summary>Mavjud xodimlar — takrorlanmaslik uchun.</summary>
    public async Task<IReadOnlyList<Employee>> LoadEmployeesAsync(CancellationToken cancellationToken)
        => _context is null
            ? []
            : await _context.Employees.ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>Mavjud mehnat munosabatlari — hodisalari bilan.</summary>
    public async Task<IReadOnlyList<Employment>> LoadEmploymentsAsync(CancellationToken cancellationToken)
        => _context is null
            ? []
            : await _context.Employments
                .Include(employment => employment.Events)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

    /// <summary>Mavjud sinov muddatlari.</summary>
    public async Task<IReadOnlyList<Probation>> LoadProbationsAsync(CancellationToken cancellationToken)
        => _context is null
            ? []
            : await _context.Probations.ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>Mavjud belgilar (rezerv, qora ro'yxat, nazorat).</summary>
    public async Task<IReadOnlyList<EmployeeFlag>> LoadFlagsAsync(CancellationToken cancellationToken)
        => _context is null
            ? []
            : await _context.EmployeeFlags.ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Mavjud qobiliyat profillari.
    /// </summary>
    /// <remarks>
    /// <c>AsNoTracking</c> ATAYLAB qo'yilmagan: xodimda profil bitta va u
    /// qayta yaratilmaydi, <b>to'ldiriladi</b> — bo'sh maydoniga Excel'dagi
    /// qiymat yoziladi. Kuzatilmagan obyektga kiritilgan o'zgarish bazaga
    /// umuman yetib bormasdi.
    /// </remarks>
    public async Task<IReadOnlyList<EmployeeCompetency>> LoadCompetenciesAsync(CancellationToken cancellationToken)
        => _context is null
            ? []
            : await _context.EmployeeCompetencies.ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>Mavjud rag'bat va jarimalar — takrorlanmaslik uchun.</summary>
    public async Task<IReadOnlyList<Incentive>> LoadIncentivesAsync(CancellationToken cancellationToken)
        => _context is null
            ? []
            : await _context.Incentives.ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>Mavjud «qayta aloqa» suhbatlari — takrorlanmaslik uchun.</summary>
    public async Task<IReadOnlyList<ExitInterview>> LoadExitInterviewsAsync(CancellationToken cancellationToken)
        => _context is null
            ? []
            : await _context.ExitInterviews.ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>Mavjud baxtsiz hodisalar — takrorlanmaslik uchun.</summary>
    public async Task<IReadOnlyList<Incident>> LoadIncidentsAsync(CancellationToken cancellationToken)
        => _context is null
            ? []
            : await _context.Incidents.ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>Voronka bosqichlari — kengash qadami «Кенгаш» ni shu yerdan topadi.</summary>
    /// <remarks>
    /// Faol emaslari ham yuklanadi: bosqich keyinchalik o'chirilgan
    /// bo'lishi mumkin, tarixiy yozuv esa baribir unga tegishli.
    /// </remarks>
    public async Task<IReadOnlyList<PipelineStage>> LoadPipelineStagesAsync(CancellationToken cancellationToken)
        => _context is null
            ? []
            : await _context.PipelineStages.ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>Vakansiyalar — arxiv vakansiyasi allaqachon bormi.</summary>
    public async Task<IReadOnlyList<Vacancy>> LoadVacanciesAsync(CancellationToken cancellationToken)
        => _context is null
            ? []
            : await _context.Vacancies.ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Mavjud nomzodlar — bosqich yozuvlari bilan.
    /// </summary>
    /// <remarks>
    /// <c>AsNoTracking</c> YO'Q: kengash qadami mavjud nomzodga yangi
    /// bosqich yozuvi qo'shishi mumkin, kuzatilmagan obyektdagi
    /// o'zgarish esa bazaga yetib bormasdi (qobiliyat profilidagi bilan
    /// bir xil mulohaza).
    /// </remarks>
    public async Task<IReadOnlyList<Candidate>> LoadCandidatesAsync(CancellationToken cancellationToken)
        => _context is null
            ? []
            : await _context.Candidates
                .Include(candidate => candidate.StageEntries)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

    // ────────────────────────────────────────────────────────────
    //  Yozish
    // ────────────────────────────────────────────────────────────

    /// <summary>Yangi agregatni saqlashga qo'yadi.</summary>
    public void Add<TAggregate>(TAggregate aggregate)
        where TAggregate : AggregateRoot
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _ = _context?.Add(aggregate);
    }

    /// <summary>Qadam uchun tranzaksiya ochadi.</summary>
    public async Task BeginStepAsync(bool apply, CancellationToken cancellationToken)
    {
        if (_context is null || !apply)
        {
            return;
        }

        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Qadam o'zgarishlarini saqlaydi va tranzaksiyani yopadi.</summary>
    /// <returns>Bazaga yozilgan qatorlar soni (quruq yurishda 0).</returns>
    public async Task<int> SaveStepAsync(bool apply, CancellationToken cancellationToken)
    {
        if (_context is null || !apply)
        {
            // Quruq yurishda kuzatuvchidagi o'zgarishlar bazaga chiqmasligi
            // kerak, lekin keyingi qadam uchun ular ham kerak emas.
            _context?.ChangeTracker.Clear();
            return 0;
        }

        var affected = await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (_transaction is not null)
        {
            await _transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            await _transaction.DisposeAsync().ConfigureAwait(false);
            _transaction = null;
        }

        return affected;
    }

    /// <summary>Qadamni bekor qiladi — xato bo'lganda.</summary>
    public async Task RollbackStepAsync(CancellationToken cancellationToken)
    {
        if (_transaction is not null)
        {
            await _transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            await _transaction.DisposeAsync().ConfigureAwait(false);
            _transaction = null;
        }

        _context?.ChangeTracker.Clear();
    }

    /// <summary>Ma'lumotnomaning «nom → identifikator» xaritasi.</summary>
    /// <remarks>
    /// Ilgari bu xarita KOD bo'yicha qurilardi va <c>aliases.json</c> ham
    /// kodga bog'lanardi. Kod qo'lda yoziladigan maydon edi va ikkilanishga
    /// olib kelardi (bir nom, ikki kod) — shu sababli u butunlay olib
    /// tashlandi. Nom esa ma'lumotnoma bo'yicha unikal (bazada
    /// <c>lower(name)</c> ustidagi indeks), ya'ni o'zi yetarli kalit.
    /// </remarks>
    private async Task<Dictionary<string, Guid>> NamesAsync<TEntity>(CancellationToken cancellationToken)
        where TEntity : ReferenceEntity
        => await _context!.Set<TEntity>()
            .AsNoTracking()
            .ToDictionaryAsync(
                entity => entity.Name,
                entity => entity.Id,
                StringComparer.OrdinalIgnoreCase,
                cancellationToken)
            .ConfigureAwait(false);

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync().ConfigureAwait(false);
        }

        if (_context is not null)
        {
            await _context.DisposeAsync().ConfigureAwait(false);
        }
    }
}
