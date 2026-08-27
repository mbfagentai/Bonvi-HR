using System.Globalization;
using Bonvi.Etl;
using Bonvi.Etl.Excel;
using Bonvi.Etl.Mapping;
using Bonvi.Etl.Reporting;
using Bonvi.Etl.Runtime;
using Bonvi.Etl.Steps;

// ═══════════════════════════════════════════════════════════════
//  ETL — Excel'dan bazaga bir martalik ko'chirish
//
//  Standart rejim — QURUQ YURISH. Bazaga yozish uchun «--apply»
//  ataylab talab qilinadi va u «--connection» siz ishlamaydi.
//  Sabab: bu vosita bir marta ishlaydi, lekin tasodifan ikkinchi
//  marta ishga tushsa ishlab turgan bazani buzishi mumkin edi.
// ═══════════════════════════════════════════════════════════════

if (args.Length == 0 || Array.Exists(args, arg => arg is "--help" or "-h" or "help"))
{
    Usage.Print();
    return ExitCode.Ok;
}

var (options, parseError) = EtlOptions.Parse(args);

if (options is null)
{
    Console.Error.WriteLine($"❌ {parseError}");
    Console.Error.WriteLine("   Yordam uchun: --help");
    return ExitCode.BadArguments;
}

if (!File.Exists(options.Source))
{
    Console.Error.WriteLine($"❌ Excel fayl topilmadi: «{options.Source}»");
    Console.Error.WriteLine("   Faylni loyihaning «data/» jildiga qo'ying — ./bonvi etl uni konteynerga ulaydi.");
    return ExitCode.BadArguments;
}

using var cancellation = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    // Ctrl+C dan keyin ham hisobot yozilishi kerak: tugallanmagan
    // ko'chirishning hisoboti — tugallanmagan ko'chirishning yagona izi.
    e.Cancel = true;
    cancellation.Cancel();
    Console.WriteLine();
    Console.WriteLine("⏹  To'xtatilmoqda — hisobot yoziladi...");
};

var report = new EtlReport();

try
{
    return await RunAsync(options, report, cancellation.Token).ConfigureAwait(false);
}
catch (OperationCanceledException)
{
    await WriteReportAsync(options, report, CancellationToken.None).ConfigureAwait(false);
    Console.WriteLine("⏹  Ko'chirish to'xtatildi.");
    return ExitCode.Cancelled;
}
catch (Exception ex)
{
    // Bu yerga faqat kutilmagan xato tushadi: qadam ichidagi xatolar
    // qadamning o'zida tutiladi va hisobotga yoziladi. Shu sababli bu
    // yerda to'liq izni ko'rsatish kerak — u dasturchiga qaratilgan.
    Console.Error.WriteLine($"❌ Ko'chirish yiqildi: {ex.GetType().Name}: {ex.Message}");
    Console.Error.WriteLine(ex.StackTrace);
    await WriteReportAsync(options, report, CancellationToken.None).ConfigureAwait(false);
    return ExitCode.Failed;
}

static async Task<int> RunAsync(EtlOptions options, EtlReport report, CancellationToken cancellationToken)
{
    Banner.Print(options);

    using var workbook = new ExcelWorkbook(options.Source);
    var aliases = AliasTable.Load();

    await using var database = options.Connection is { Length: > 0 } connection
        ? EtlDatabase.Connect(connection)
        : EtlDatabase.Offline();

    await database.EnsureReadyAsync(cancellationToken).ConfigureAwait(false);

    var catalogs = await database.LoadCatalogsAsync(cancellationToken).ConfigureAwait(false);
    var workspace = new EtlWorkspace(options, workbook, aliases, catalogs, database, report);

    await LoadExistingAsync(database, workspace, cancellationToken).ConfigureAwait(false);

    Console.WriteLine($"   Ma'lumotnoma : {catalogs.Count(CatalogKind.Department)} bo'lim, "
        + $"{catalogs.Count(CatalogKind.Position)} lavozim, "
        + $"{catalogs.Count(CatalogKind.LegalEntity)} yuridik shaxs");
    Console.WriteLine($"   Bazada       : {workspace.Employees.Count} xodim");
    Console.WriteLine();

    var steps = Plan(options, workspace);

    foreach (var (step, run) in steps)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await ExecuteAsync(step, run, workspace, cancellationToken).ConfigureAwait(false);
    }

    // Varaqlar dangasa o'qiladi, ya'ni buzuq kataklar ro'yxati faqat
    // barcha qadamlar tugagandan keyin to'liq bo'ladi.
    ReportUnreadableCells(workbook, report);

    Summary.Print(report, options);
    await WriteReportAsync(options, report, cancellationToken).ConfigureAwait(false);

    return Array.Exists(report.Steps.ToArray(), summary => summary.Failure is not null)
        ? ExitCode.Failed
        : ExitCode.Ok;
}

// Tanlangan qadamlarni bajarilish tartibida yig'adi.
// Tartib majburiy va o'zgartirilmaydi. Xodimsiz mehnat munosabati,
// munosabatsiz hodisa yoki sinov muddati yozib bo'lmaydi. Foydalanuvchi
// `--steps` bilan bir nechtasini tanlasa ham, ular shu tartibda ketadi.
// Bitta qadamni alohida yurgizish ataylab ruxsat etilgan: ko'chirishdan
// keyin masalan faqat teglarni qayta yurgizish kerak bo'lishi mumkin.
// Idempotentlik buni xavfsiz qiladi.
static List<(EtlStep Step, Action Run)> Plan(EtlOptions options, EtlWorkspace workspace)
{
    var plan = new List<(EtlStep, Action)>();

    if (options.Has(EtlStep.Employees))
    {
        plan.Add((EtlStep.Employees, new EmployeesStep(workspace).Run));
    }

    if (options.Has(EtlStep.Employments))
    {
        plan.Add((EtlStep.Employments, new EmploymentsStep(workspace).Run));
    }

    if (options.Has(EtlStep.Events))
    {
        plan.Add((EtlStep.Events, new EventsStep(workspace).Run));
    }

    if (options.Has(EtlStep.Probations))
    {
        plan.Add((EtlStep.Probations, new ProbationsStep(workspace).Run));
    }

    if (options.Has(EtlStep.Flags))
    {
        plan.Add((EtlStep.Flags, new FlagsStep(workspace).Run));
    }

    // Profil boyitish eng oxirida: qobiliyatlar jadvali ham, rag'batlar
    // varag'i ham faqat MAVJUD odam haqida gapiradi va yangi xodim
    // yaratmaydi. Undan oldin yurgizilsa, xodimlar qadami hali topmagan
    // odamlar «topilmadi» bo'lib hisobotga tushardi.
    if (options.Has(EtlStep.Profiles))
    {
        plan.Add((EtlStep.Profiles, new ProfilesStep(workspace).Run));
    }

    return plan;
}

// Bitta qadamni alohida tranzaksiyada bajaradi.
// Qadam yiqilsa faqat o'sha qadam qaytariladi va ko'chirish TO'XTAYDI:
// keyingi qadamlar oldingisining natijasiga tayanadi, ularni yarim
// ma'lumot ustida yurgizish soxta hisobot berardi.
static async Task ExecuteAsync(
    EtlStep step,
    Action run,
    EtlWorkspace workspace,
    CancellationToken cancellationToken)
{
    var name = step.ToString().ToLowerInvariant();
    var started = DateTimeOffset.UtcNow;

    Console.Write($"   {name,-12} ");

    var summary = workspace.Report.Step(name);

    try
    {
        await workspace.Database.BeginStepAsync(workspace.Options.Apply, cancellationToken).ConfigureAwait(false);

        run();

        var written = await workspace.Database
            .SaveStepAsync(workspace.Options.Apply, cancellationToken)
            .ConfigureAwait(false);

        var elapsed = DateTimeOffset.UtcNow - started;

        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{summary.Read,6} o'qildi  {summary.Created,6} yangi  {summary.Updated,6} mavjud  "
            + $"{summary.Skipped,5} tashlandi  {written,6} yozildi  {elapsed.TotalSeconds,6:F1}s"));
    }
    catch (Exception ex)
    {
        summary.Failure = ex.Message;
        await workspace.Database.RollbackStepAsync(CancellationToken.None).ConfigureAwait(false);

        Console.WriteLine($"❌ {ex.Message}");
        throw;
    }
}

// O'qib bo'lmagan kataklarni hisobotga chiqaradi.
// Bunday katak bo'sh emas — u yo'qotilgan ma'lumot, va HR uni
// Excel'da ochib o'zi ko'rishi kerak. Manzil (varaq!katak) shu uchun.
static void ReportUnreadableCells(ExcelWorkbook workbook, EtlReport report)
{
    foreach (var address in workbook.UnreadableCells)
    {
        var separator = address.IndexOf('!', StringComparison.Ordinal);

        report.Add(
            "excel",
            separator > 0 ? address[..separator] : address,
            0,
            string.Empty,
            "katak o'qilmadi — Excel'da qiymati buzuq",
            address,
            EtlAction.LeftEmpty);
    }
}

// Bazadagi mavjud holatni xotiraga o'qiydi — idempotentlik uchun.
static async Task LoadExistingAsync(
    EtlDatabase database,
    EtlWorkspace workspace,
    CancellationToken cancellationToken)
{
    foreach (var employee in await database.LoadEmployeesAsync(cancellationToken).ConfigureAwait(false))
    {
        workspace.Register(employee);
    }

    foreach (var employment in await database.LoadEmploymentsAsync(cancellationToken).ConfigureAwait(false))
    {
        workspace.Register(employment);
    }

    foreach (var probation in await database.LoadProbationsAsync(cancellationToken).ConfigureAwait(false))
    {
        workspace.Register(probation);
    }

    foreach (var flag in await database.LoadFlagsAsync(cancellationToken).ConfigureAwait(false))
    {
        workspace.Register(flag);
    }

    foreach (var competency in await database.LoadCompetenciesAsync(cancellationToken).ConfigureAwait(false))
    {
        workspace.Register(competency);
    }

    foreach (var incentive in await database.LoadIncentivesAsync(cancellationToken).ConfigureAwait(false))
    {
        workspace.Register(incentive);
    }
}

static async Task WriteReportAsync(EtlOptions options, EtlReport report, CancellationToken cancellationToken)
{
    try
    {
        await report.WriteCsvAsync(options.ReportPath, cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"📄 Hisobot: {options.ReportPath}  ({report.Issues.Count} satr)");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"⚠️  Hisobot yozilmadi ({options.ReportPath}): {ex.Message}");
    }
}

/// <summary>Chiqish kodlari — CI va skript uchun.</summary>
internal static class ExitCode
{
    public const int Ok = 0;
    public const int Failed = 1;
    public const int BadArguments = 2;
    public const int Cancelled = 130;
}

/// <summary>Ishga tushirish sharoitini ko'rsatadi.</summary>
/// <remarks>
/// Rejim (quruq yurish / yozish) ATAYLAB eng ko'zga tashlanadigan joyda:
/// «--apply» ni tasodifan yozib qo'yish — bu vositadagi eng qimmat xato.
/// </remarks>
internal static class Banner
{
    public static void Print(EtlOptions options)
    {
        var mode = options.Apply
            ? "✍️  YOZISH — o'zgarishlar bazaga saqlanadi"
            : "🔍 QURUQ YURISH — bazaga hech narsa yozilmaydi";

        Console.WriteLine();
        Console.WriteLine("═══ BONVI HR — ma'lumot ko'chirish ═══════════════════════");
        Console.WriteLine($"   Rejim        : {mode}");
        Console.WriteLine($"   Manba        : {options.Source}");
        Console.WriteLine($"   Baza         : {(options.Connection is null ? "ulanmagan (faqat Excel tekshiriladi)" : "ulangan")}");
        Console.WriteLine($"   Qadamlar     : {options.Steps}");

        if (options.Limit is { } limit)
        {
            Console.WriteLine($"   Cheklov      : har varaqdan {limit} qator (sinov rejimi)");
        }

        Console.WriteLine();
    }
}

/// <summary>Yakuniy jamlanma — konsolga.</summary>
internal static class Summary
{
    public static void Print(EtlReport report, EtlOptions options)
    {
        var issues = report.Issues;

        Console.WriteLine();
        Console.WriteLine("─── Natija ───────────────────────────────────────────────");

        var needsReview = 0;
        foreach (var step in report.Steps)
        {
            needsReview += step.NeedsReview;
        }

        Console.WriteLine($"   Tekshirish kerak : {needsReview} yozuv");
        Console.WriteLine($"   Mavjud, tegilmadi: {report.AlreadyExisting} yozuv");
        Console.WriteLine($"   Hisobot satri    : {issues.Count}");

        // Eng ko'p uchragan muammolar — HR nimadan boshlashini biladi.
        var byProblem = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var issue in issues)
        {
            byProblem[issue.Problem] = byProblem.GetValueOrDefault(issue.Problem) + 1;
        }

        if (byProblem.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("   Eng ko'p uchragan muammolar:");

            var top = new List<KeyValuePair<string, int>>(byProblem);
            top.Sort((left, right) => right.Value.CompareTo(left.Value));

            foreach (var entry in top.GetRange(0, Math.Min(8, top.Count)))
            {
                Console.WriteLine($"     {entry.Value,6}  {entry.Key}");
            }
        }

        Console.WriteLine();

        if (!options.Apply)
        {
            Console.WriteLine("   ℹ️  Bu quruq yurish edi. Hisobotni ko'rib chiqing, so'ng:");
            Console.WriteLine("      ./bonvi etl --apply");
        }
    }
}

/// <summary>Buyruq satri yordami.</summary>
internal static class Usage
{
    public static void Print()
    {
        Console.WriteLine("""

            BONVI HR — Excel'dan bazaga ma'lumot ko'chirish

            ISHLATISH
              bonvi-etl --source <fayl.xlsx> [parametrlar]

            PARAMETRLAR
              --source, --file <yo'l>   Excel fayl (majburiy)
              --connection <satr>       PostgreSQL ulanish satri
              --apply                   Bazaga HAQIQATAN yozadi (aks holda quruq yurish)
              --dry-run                 Quruq yurish (standart holat, aniqlik uchun)
              --steps <ro'yxat>         employees,employments,events,probations,flags,profiles
              --report <yo'l>           CSV hisobot yo'li
              --limit <son>             Har varaqdan nechta qator (sinov uchun)
              --default-legal-entity    Yuridik shaxs ko'rsatilmaganda ishlatiladigan kod

            MISOLLAR
              bonvi-etl --source /data/xodimlar.xlsx
                  Bazasiz tekshiruv — faqat Excel o'qiladi va hisobot beriladi.

              bonvi-etl --source /data/xodimlar.xlsx --connection "..." --limit 50
                  Ulanish bilan quruq yurish, har varaqdan 50 qator.

              bonvi-etl --source /data/xodimlar.xlsx --connection "..." --apply
                  Haqiqiy ko'chirish.

            TAMOYIL
              Vosita hech qachon ma'lumotni o'ylab topmaydi. Qiymat noaniq
              bo'lsa — bo'sh qoldiriladi va hisobotga tushadi. Qayta ishga
              tushirilsa dublikat yaratmaydi.

            """);
    }
}
