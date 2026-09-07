using System.Reflection;
using System.Text.Json;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Catalog;
using Bonvi.Domain.Common;
using Bonvi.Domain.Organization;
using Bonvi.Domain.Recruiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Bonvi.Infrastructure.Persistence.Seed;

/// <summary>Ma'lumotnomalarni urug'lantirish natijasi.</summary>
/// <param name="Created">Yangi yaratilgan yozuvlar soni.</param>
/// <param name="Skipped">Allaqachon mavjud bo'lgani uchun tegilmagan yozuvlar soni.</param>
/// <param name="MissingFiles">Topilmagan JSON fayllar soni.</param>
public sealed record CatalogSeedResult(int Created, int Skipped, int MissingFiles);

/// <summary>
/// Ma'lumotnomalarning boshlang'ich qiymatlarini JSON fayllardan bazaga yozadi.
/// </summary>
/// <remarks>
/// <para>
/// Nega migratsiyada emas: ma'lumotnoma yozuvi — <b>ma'lumot</b>, sxema emas.
/// Migratsiyaga yozilsa, HR bo'lim nomini tuzatgan zahoti keyingi migratsiya
/// uni eski holiga qaytarib qo'yardi.
/// </para>
/// <para>
/// Amal <b>idempotent</b>, lekin QATOR bo'yicha emas — ma'lumotnoma BUTUNLIGICHA.
/// Har bir ma'lumotnoma FAQAT bazada hali bironta yozuvi bo'lmasa quyiladi;
/// birorta yozuvi bo'lsa, butun JSON fayli o'tkazib yuboriladi (batafsil sabab —
/// <c>SeedCatalogAsync</c> ichidagi izohda). Shu sababli har ishga tushishda
/// chaqirish xavfsiz, LEKIN «yetishmaganini to'ldiradi» degani emas: ma'lumotnoma
/// allaqachon to'lgan bo'lsa, JSON'ga keyin qo'shilgan qator ham HR uni qo'lda
/// kiritmaguncha o'zi paydo bo'lmaydi. (Ilgari, kod olib tashlanishidan oldin,
/// har bir qator alohida — avval kod, keyin nom bo'yicha — tekshirilardi; endi
/// bunday emas.)
/// </para>
/// <para>
/// Fayl topilmasa — xato tashlanmaydi, ogohlantirish yoziladi va o'sha
/// ma'lumotnoma o'tkazib yuboriladi. Sabab: fayllar bosqichma-bosqich
/// tayyorlanadi va yarmi yo'qligi uchun butun tizim ishga tushmasligi
/// mumkin emas.
/// </para>
/// </remarks>
public sealed partial class CatalogSeeder
{
    /// <summary>
    /// JSON o'qish sozlamalari. <c>Web</c> — camelCase va registrga sezgir emas,
    /// ya'ni fayldagi <c>sortOrder</c> C# dagi <c>SortOrder</c> ga tushadi.
    /// Statik: har chaqiruvda yangisini yaratish qimmat (CA1869).
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Resurslar aynan shu yig'malaga joylashtirilgan.</summary>
    private static readonly Assembly ResourceAssembly = typeof(CatalogSeeder).Assembly;

    private readonly AppDbContext _context;
    private readonly IDepartmentRepository _departments;
    private readonly ILogger<CatalogSeeder> _logger;

    public CatalogSeeder(
        AppDbContext context,
        IDepartmentRepository departments,
        ILogger<CatalogSeeder> logger)
    {
        _context = context;
        _departments = departments;
        _logger = logger;
    }

    // ────────────────────────────────────────────────────────────
    //  Log xabarlari — manba generatori orqali (CA1848).
    //  Oddiy `_logger.LogInformation($"…")` har chaqiruvda satr yig'adi,
    //  hatto log darajasi o'chirilgan bo'lsa ham.
    // ────────────────────────────────────────────────────────────

    [LoggerMessage(
        EventId = 2100,
        Level = LogLevel.Warning,
        Message = "Ma'lumotnoma fayli topilmadi, o'tkazib yuborildi: {FileName}")]
    private partial void LogFileMissing(string fileName);

    [LoggerMessage(
        EventId = 2101,
        Level = LogLevel.Information,
        Message = "«{Catalog}»: {Created} ta yaratildi, {Skipped} ta o'tkazib yuborildi")]
    private partial void LogCatalogSeeded(string catalog, int created, int skipped);

    [LoggerMessage(
        EventId = 2102,
        Level = LogLevel.Information,
        Message = "Ma'lumotnomalar urug'lantirildi: {Created} ta yangi, {Skipped} ta mavjud, {MissingFiles} ta fayl yo'q")]
    private partial void LogTotal(int created, int skipped, int missingFiles);

    [LoggerMessage(
        EventId = 2103,
        Level = LogLevel.Warning,
        Message = "«{Name}» yozuvi qabul qilinmadi: {Reason}")]
    private partial void LogRowRejected(string name, string reason);

    [LoggerMessage(
        EventId = 2104,
        Level = LogLevel.Debug,
        Message = "«{Catalog}» allaqachon to'ldirilgan — urug' fayli ({Rows} qator) o'tkazib yuborildi")]
    private partial void LogCatalogSkipped(string catalog, int rows);

    /// <summary>
    /// Barcha ma'lumotnomalarni urug'lantiradi.
    /// </summary>
    /// <remarks>
    /// Tartib muhim: bo'lim yuridik shaxsga, lavozim esa bo'limga NOM orqali
    /// murojaat qiladi. Ular oldin yaratilmasa, havola bo'sh qolardi.
    /// </remarks>
    public async Task<CatalogSeedResult> SeedAsync(CancellationToken cancellationToken = default)
    {
        var stats = new SeedStats();

        await SeedLegalEntitiesAsync(stats, cancellationToken).ConfigureAwait(false);
        await SeedDirectionsAsync(stats, cancellationToken).ConfigureAwait(false);
        await SeedDepartmentsAsync(stats, cancellationToken).ConfigureAwait(false);
        await SeedPositionsAsync(stats, cancellationToken).ConfigureAwait(false);
        await SeedTerminationReasonsAsync(stats, cancellationToken).ConfigureAwait(false);
        await SeedAwardTypesAsync(stats, cancellationToken).ConfigureAwait(false);
        await SeedPenaltyTypesAsync(stats, cancellationToken).ConfigureAwait(false);
        await SeedDocumentTypesAsync(stats, cancellationToken).ConfigureAwait(false);
        await SeedPipelineStagesAsync(stats, cancellationToken).ConfigureAwait(false);

        LogTotal(stats.Created, stats.Skipped, stats.MissingFiles);

        return new CatalogSeedResult(stats.Created, stats.Skipped, stats.MissingFiles);
    }

    // ────────────────────────────────────────────────────────────
    //  Ma'lumotnomalar
    // ────────────────────────────────────────────────────────────

    private async Task SeedLegalEntitiesAsync(SeedStats stats, CancellationToken cancellationToken)
    {
        await SeedCatalogAsync<LegalEntity, LegalEntityRow>(
            "legal-entities.json",
            "Yuridik shaxslar",
            row => LegalEntity.Create(row.Name),
            (entity, row) =>
            {
                entity.SetFullName(row.FullName);
                _ = entity.SetTaxId(row.TaxId);
            },
            stats,
            cancellationToken).ConfigureAwait(false);

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SeedDirectionsAsync(SeedStats stats, CancellationToken cancellationToken)
    {
        await SeedCatalogAsync<Direction, DirectionRow>(
            "directions.json",
            "Yo'nalishlar",
            row => Direction.Create(row.Name),
            (entity, row) => _ = entity.SetColor(row.Color),
            stats,
            cancellationToken).ConfigureAwait(false);

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Bo'limlar. Fayl <b>tekis</b> ro'yxat, ota bo'lim KOD bo'yicha ko'rsatiladi.
    /// </summary>
    /// <remarks>
    /// Ikki bosqich: avval barcha bo'limlar otasiz yaratiladi, keyin kod → id
    /// jadvali orqali bog'lanadi. Sabab: faylda ota bolasidan KEYIN turishi
    /// mumkin va bir o'tishda uning identifikatori hali ma'lum bo'lmasdi.
    /// Oxirida <c>Level</c> va <c>Path</c> bazada rekursiv CTE bilan hisoblanadi.
    /// </remarks>
    private async Task SeedDepartmentsAsync(SeedStats stats, CancellationToken cancellationToken)
    {
        var legalEntities = await NameMapAsync<LegalEntity>(cancellationToken).ConfigureAwait(false);

        var created = await SeedCatalogAsync<Department, DepartmentRow>(
            "departments.json",
            "Bo'limlar",
            row => Department.Create(row.Name, parentId: null),
            (entity, row) =>
            {
                if (NormalizeName(row.LegalEntity) is { } legalName
                    && legalEntities.TryGetValue(legalName, out var legalEntityId))
                {
                    entity.AssignLegalEntity(legalEntityId);
                }
            },
            stats,
            cancellationToken).ConfigureAwait(false);

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (created.Count > 0)
        {
            var byName = await NameMapAsync<Department>(cancellationToken).ConfigureAwait(false);

            foreach (var (entity, row) in created)
            {
                if (NormalizeName(row.Parent) is { } parentName
                    && byName.TryGetValue(parentName, out var parentId))
                {
                    var moved = entity.MoveTo(parentId);
                    if (moved.IsFailure)
                    {
                        LogRowRejected(entity.Name, moved.Error.ToString());
                    }
                }
            }

            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        // Daraxt o'zgargan bo'lishi mumkin — yo'l va daraja qayta hisoblanadi.
        // Hech narsa yaratilmagan bo'lsa ham chaqiriladi: oldingi ishga tushishda
        // uzilib qolgan hisob shu yerda tuzalib ketadi (amal beg'araz).
        await _departments.RebuildHierarchyAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SeedPositionsAsync(SeedStats stats, CancellationToken cancellationToken)
    {
        var departments = await NameMapAsync<Department>(cancellationToken).ConfigureAwait(false);

        await SeedCatalogAsync<Position, PositionRow>(
            "positions.json",
            "Lavozimlar",
            row => Position.Create(row.Name, departmentId: null),
            (entity, row) =>
            {
                if (NormalizeName(row.Department) is { } departmentName
                    && departments.TryGetValue(departmentName, out var departmentId))
                {
                    entity.AssignDepartment(departmentId);
                }

                if (row.Grade is { } grade)
                {
                    _ = entity.SetGrade(grade);
                }

                _ = entity.SetProbationDays(row.ProbationDays);

                foreach (var duty in row.Duties ?? [])
                {
                    _ = entity.AddDuty(duty);
                }
            },
            stats,
            cancellationToken).ConfigureAwait(false);

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SeedTerminationReasonsAsync(SeedStats stats, CancellationToken cancellationToken)
    {
        await SeedCatalogAsync<TerminationReason, TerminationReasonRow>(
            "termination-reasons.json",
            "Ishdan ketish sabablari",
            row => TerminationReason.Create(row.Name),
            (entity, row) =>
            {
                entity.SetVoluntary(row.IsVoluntary ?? true);
                entity.SetRehireable(row.IsRehireable ?? true);
            },
            stats,
            cancellationToken).ConfigureAwait(false);

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SeedAwardTypesAsync(SeedStats stats, CancellationToken cancellationToken)
    {
        await SeedCatalogAsync<AwardType, AwardTypeRow>(
            "award-types.json",
            "Rag'bat turlari",
            row => AwardType.Create(row.Name),
            (entity, row) => entity.SetHasAmount(row.HasAmount ?? false),
            stats,
            cancellationToken).ConfigureAwait(false);

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SeedPenaltyTypesAsync(SeedStats stats, CancellationToken cancellationToken)
    {
        await SeedCatalogAsync<PenaltyType, PenaltyTypeRow>(
            "penalty-types.json",
            "Jarima turlari",
            row => PenaltyType.Create(row.Name),
            (entity, row) =>
            {
                entity.SetHasAmount(row.HasAmount ?? false);

                if (row.Severity is { } severity)
                {
                    _ = entity.SetSeverity(severity);
                }
            },
            stats,
            cancellationToken).ConfigureAwait(false);

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SeedDocumentTypesAsync(SeedStats stats, CancellationToken cancellationToken)
    {
        await SeedCatalogAsync<DocumentType, DocumentTypeRow>(
            "document-types.json",
            "Hujjat turlari",
            row => DocumentType.Create(row.Name),
            (entity, row) => entity.SetRequired(row.IsRequired ?? false),
            stats,
            cancellationToken).ConfigureAwait(false);

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Rekruting voronkasining bosqichlari (2-bosqich).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Tartib FAYLDAGI ketma-ketlikdan olinadi</b>, alohida
    /// <c>sortOrder</c> maydonidan emas. Sabab: voronka — bu ro'yxatning
    /// O'ZI, va faylni o'qiyotgan odam «Анкета → Телефон суҳбат → …» ni
    /// yuqoridan pastga ko'radi. Raqamlar alohida yozilganda ular fayl
    /// tartibidan ajralib ketishi mumkin edi (masalan qator ko'chirilganda
    /// raqam unutiladi) va o'shanda haqiqiy voronka faylga qaraganda
    /// boshqacha bo'lardi.
    /// </para>
    /// <para>
    /// Ro'yxat <c>PLAN.md</c> dagi voronkadan olingan. «Ишга қабул» bosqich
    /// sifatida YO'Q: u voronkaning oxiri emas, undan CHIQISH — nomzod
    /// ishga olinganda 1-bosqichning ishga qabul oqimi ishlaydi va nomzod
    /// xodimga aylanadi.
    /// </para>
    /// <para>
    /// HR bu ro'yxatni to'liq o'zgartira oladi (qo'shish, faolsizlantirish,
    /// qayta tartiblash) — urug' fayli faqat BOSHLANG'ICH holat.
    /// </para>
    /// </remarks>
    private async Task SeedPipelineStagesAsync(SeedStats stats, CancellationToken cancellationToken)
    {
        var order = 0;

        await SeedCatalogAsync<PipelineStage, PipelineStageRow>(
            "pipeline-stages.json",
            "Voronka bosqichlari",
            row => PipelineStage.Create(row.Name, order++),
            apply: null,
            stats,
            cancellationToken).ConfigureAwait(false);

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    // ────────────────────────────────────────────────────────────
    //  Umumiy mexanizm
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Bitta ma'lumotnomani JSON fayldan o'qib, yetishmayotgan yozuvlarni qo'shadi.
    /// </summary>
    /// <returns>
    /// Aynan shu chaqiruvda YARATILGAN yozuvlar va ularning manba qatorlari.
    /// Bo'limlarga ota-bola bog'lanishini qo'shish uchun kerak — mavjud
    /// yozuvlarga tegmaslik uchun ularni ajratib olish shart.
    /// </returns>
    private async Task<List<(TEntity Entity, TRow Row)>> SeedCatalogAsync<TEntity, TRow>(
        string fileName,
        string catalogTitle,
        Func<TRow, Result<TEntity>> create,
        Action<TEntity, TRow>? apply,
        SeedStats stats,
        CancellationToken cancellationToken)
        where TEntity : ReferenceEntity
        where TRow : CatalogRow
    {
        var created = new List<(TEntity Entity, TRow Row)>();

        var rows = ReadRows<TRow>(fileName, stats);
        if (rows is null || rows.Count == 0)
        {
            return created;
        }

        // ⚠️ Ma'lumotnoma BO'SH bo'lgandagina urug'lantiriladi.
        //
        // Ilgari har bir qator alohida tekshirilardi (avval kod, keyin nom
        // bo'yicha) va yo'q bo'lgani qo'shilardi. Kod olib tashlangach bu
        // naqsh xavfli bo'lib qoladi: HR bo'lim nomini tuzatsa (masalan
        // imlo xatosini), keyingi ishga tushishda seeder eski nomni
        // topolmay, uni QAYTA yaratardi — va HR bir kun ochib, ikkita
        // «Риспетто офис» ni ko'rardi.
        //
        // Urug' ma'lumoti — bir martalik poydevor: u Excel tahlilidan
        // olingan va tizim ishga tushganda bir marta quyiladi. Yangi
        // yozuvni HR interfeysdan qo'shadi. Shu sababli to'g'ri semantika
        // «bo'sh bo'lsa quy», «yetishmaganini to'ldir» emas.
        //
        // O'chirilganlar ham hisobga olinadi (`IgnoreQueryFilters`): ular
        // bazada turibdi va ularning ustiga qayta quyish dublikat berardi.
        var alreadySeeded = await _context.Set<TEntity>()
            .IgnoreQueryFilters()
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);

        if (alreadySeeded)
        {
            stats.Skipped += rows.Count;
            LogCatalogSkipped(catalogTitle, rows.Count);
            return created;
        }

        // Fayl ICHIDAGI takrorlarga qarshi: bir xil nom ikki marta uchrasa,
        // ikkinchisi `lower(name)` ustidagi unikal indeksga urilib butun
        // tranzaksiyani yiqitardi.
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var createdCount = 0;
        var skippedCount = 0;

        foreach (var row in rows)
        {
            var name = NormalizeName(row.Name);
            if (name is null)
            {
                skippedCount++;
                continue;
            }

            if (!names.Add(name))
            {
                skippedCount++;
                continue;
            }

            var result = create(row);
            if (result.IsFailure)
            {
                LogRowRejected(name, result.Error.ToString());
                skippedCount++;
                continue;
            }

            var entity = result.Value;
            entity.SetNote(row.Note);

            if (row.IsActive == false)
            {
                entity.Deactivate();
            }

            apply?.Invoke(entity, row);

            _context.Add(entity);

            created.Add((entity, row));
            createdCount++;
        }

        stats.Created += createdCount;
        stats.Skipped += skippedCount;

        LogCatalogSeeded(catalogTitle, createdCount, skippedCount);

        return created;
    }

    /// <summary>
    /// Yig'maga joylashtirilgan JSON faylni o'qiydi. Fayl yo'q bo'lsa —
    /// <c>null</c> va ogohlantirish (istisno TASHLANMAYDI).
    /// </summary>
    /// <remarks>
    /// Resurs nomi to'liq emas, oxiri bo'yicha izlanadi
    /// (<c>Bonvi.Infrastructure.Persistence.Seed.data.directions.json</c>).
    /// Sabab: papka ko'chirilsa yoki loyihaning ildiz nom fazosi o'zgarsa,
    /// to'liq nom jimgina buzilardi.
    /// </remarks>
    private List<TRow>? ReadRows<TRow>(string fileName, SeedStats stats)
        where TRow : CatalogRow
    {
        var suffix = "." + fileName;

        var resourceName = Array.Find(
            ResourceAssembly.GetManifestResourceNames(),
            name => name.EndsWith(suffix, StringComparison.Ordinal));

        if (resourceName is null)
        {
            stats.MissingFiles++;
            LogFileMissing(fileName);
            return null;
        }

        using var stream = ResourceAssembly.GetManifestResourceStream(resourceName);

        if (stream is null)
        {
            stats.MissingFiles++;
            LogFileMissing(fileName);
            return null;
        }

        // JSON buzuq bo'lsa — istisno ATAYLAB tashlanadi: bu dastur xatosi,
        // uni jimgina yutish keyinchalik "nega ma'lumotnoma bo'sh?" degan
        // tushunarsiz savolga olib kelardi.
        return JsonSerializer.Deserialize<List<TRow>>(stream, JsonOptions);
    }

    /// <summary>Kod → identifikator jadvali (kodi bor yozuvlar uchun).</summary>
    /// <summary>
    /// Ma'lumotnomaning «nom → identifikator» xaritasi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Urug' fayllaridagi yozuvlar bir-birini NOM orqali topadi
    /// (<c>parent</c>, <c>legalEntity</c>, <c>department</c>). Ilgari bu
    /// qo'lda yozilgan <c>code</c> orqali edi — u foydalanuvchidan ham
    /// so'ralardi va ikkilanishga olib kelardi (bir nom, ikki kod).
    /// Nom esa ma'lumotnoma bo'yicha unikal, ya'ni o'zi yetarli kalit.
    /// </para>
    /// <para>
    /// Taqqoslash registrga befarq va nom <see cref="NormalizeName"/>
    /// bilan tozalanadi — fayldagi «Маркетинг  бўлими» bazadagi
    /// «Маркетинг бўлими» ni topishi kerak.
    /// </para>
    /// </remarks>
    private async Task<Dictionary<string, Guid>> NameMapAsync<TEntity>(CancellationToken cancellationToken)
        where TEntity : ReferenceEntity
    {
        var rows = await _context.Set<TEntity>()
            .AsNoTracking()
            .Select(entity => new { entity.Id, entity.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var map = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var name = NormalizeName(row.Name);
            if (name is not null)
            {
                map[name] = row.Id;
            }
        }

        return map;
    }

    /// <summary>
    /// Nomni <see cref="ReferenceEntity"/> bilan bir xil tozalaydi:
    /// ortiqcha bo'shliqlar olib tashlanadi. Aks holda «Маркетинг  бўлими»
    /// bazadagi «Маркетинг бўлими» dan farqli ko'rinardi.
    /// </summary>
    private static string? NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length == 0 ? null : string.Join(' ', parts);
    }

    /// <summary>Urug'lantirish davomidagi hisoblagichlar.</summary>
    private sealed class SeedStats
    {
        public int Created { get; set; }

        public int Skipped { get; set; }

        public int MissingFiles { get; set; }
    }

    // ────────────────────────────────────────────────────────────
    //  JSON sxemasi — fayllarni boshqa jarayon tayyorlaydi,
    //  bu yerda faqat o'qish shakli tasvirlangan.
    // ────────────────────────────────────────────────────────────

    /// <summary>Har bir ma'lumotnoma qatorida bo'ladigan umumiy maydonlar.</summary>
    private abstract record CatalogRow
    {
        public string? Name { get; init; }


        /// <summary>Ko'rsatilmasa — faol.</summary>
        public bool? IsActive { get; init; }

        public string? Note { get; init; }
    }

    private sealed record LegalEntityRow : CatalogRow
    {
        public string? FullName { get; init; }

        public string? TaxId { get; init; }
    }

    private sealed record DirectionRow : CatalogRow
    {
        public string? Color { get; init; }
    }

    private sealed record DepartmentRow : CatalogRow
    {
        /// <summary>Ota bo'limning NOMI (fayl ichida yuqorida turishi shart emas).</summary>
        public string? Parent { get; init; }

        /// <summary>Yuridik shaxs nomi.</summary>
        public string? LegalEntity { get; init; }
    }

    private sealed record PositionRow : CatalogRow
    {
        /// <summary>Bo'lim nomi.</summary>
        public string? Department { get; init; }

        public int? Grade { get; init; }

        public int? ProbationDays { get; init; }

        public IReadOnlyList<string>? Duties { get; init; }
    }

    private sealed record TerminationReasonRow : CatalogRow
    {
        public bool? IsVoluntary { get; init; }

        public bool? IsRehireable { get; init; }
    }

    private sealed record AwardTypeRow : CatalogRow
    {
        public bool? HasAmount { get; init; }
    }

    private sealed record PenaltyTypeRow : CatalogRow
    {
        public bool? HasAmount { get; init; }

        public int? Severity { get; init; }
    }

    private sealed record DocumentTypeRow : CatalogRow
    {
        public bool? IsRequired { get; init; }
    }

    /// <summary>
    /// Voronka bosqichi — faqat nom va izoh.
    /// </summary>
    /// <remarks>
    /// Tartib maydoni ATAYLAB yo'q: u fayldagi qatorlar ketma-ketligidan
    /// olinadi (sababi <see cref="SeedPipelineStagesAsync"/> izohida).
    /// </remarks>
    private sealed record PipelineStageRow : CatalogRow;
}
