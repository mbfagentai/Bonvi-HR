using System.Globalization;

namespace Bonvi.Etl;

/// <summary>Ko'chirish qadamlari — <c>--steps</c> orqali tanlanadi.</summary>
[Flags]
public enum EtlStep
{
    None = 0,

    /// <summary>Xodimlar: asosiy ro'yxat va arxiv.</summary>
    Employees = 1,

    /// <summary>Mehnat munosabatlari va ishga qabul buyruqlari.</summary>
    Employments = 2,

    /// <summary>Hodisalar: lavozim o'zgarishi, qo'shimcha vazifa, ishdan ketish.</summary>
    Events = 4,

    /// <summary>Sinov muddatlari.</summary>
    Probations = 8,

    /// <summary>Belgilar: rezerv, qora ro'yxat, nazorat.</summary>
    Flags = 16,

    /// <summary>Hammasi.</summary>
    All = Employees | Employments | Events | Probations | Flags,
}

/// <summary>
/// Buyruq satri sozlamalari.
/// </summary>
/// <remarks>
/// <para>
/// <b>Standart rejim — quruq yurish.</b> Bazaga yozish uchun <c>--apply</c>
/// ATAYLAB talab qilinadi. Bir martalik ko'chirish vositasi tasodifan
/// ishga tushib, ishlab turgan bazani buzishi mumkin emas.
/// </para>
/// </remarks>
public sealed class EtlOptions
{
    /// <summary>Excel fayl yo'li.</summary>
    public required string Source { get; init; }

    /// <summary>PostgreSQL ulanish satri. Bo'sh bo'lsa — ulanishsiz quruq yurish.</summary>
    public string? Connection { get; init; }

    /// <summary>Bazaga haqiqatan yozadimi.</summary>
    public bool Apply { get; init; }

    /// <summary>Bajariladigan qadamlar.</summary>
    public EtlStep Steps { get; init; } = EtlStep.All;

    /// <summary>
    /// Standart hisobot yo'li.
    /// </summary>
    /// <remarks>
    /// <c>/data</c> — xost mashinadagi <c>data/</c> jildi (<c>./bonvi etl</c>
    /// uni konteynerga ulaydi). Hisobot ATAYLAB shu yerga yoziladi: uni HR
    /// Excel'da ochishi kerak, konteyner ichidagi nomlangan tomga esa
    /// xostdan yetib bo'lmaydi.
    /// </remarks>
    public const string DefaultReportPath = "/data/etl-report.csv";

    /// <summary>Excel'da yuridik shaxs bo'lmaganda ishlatiladigan standart kod.</summary>
    public const string DefaultLegalEntity = "mbf";

    /// <summary>CSV hisobot yo'li.</summary>
    public string ReportPath { get; init; } = DefaultReportPath;

    /// <summary>Har bir varaqdan o'qiladigan qatorlar chegarasi (sinov uchun).</summary>
    public int? Limit { get; init; }

    /// <summary>
    /// Excel'da yuridik shaxs ko'rsatilmaganda ishlatiladigan kod.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bu <b>taxmin emas, hujjatlashtirilgan dalil</b>: bloknotning har bir
    /// varag'i sarlavhasida «"MBF COMPANY" ДА ИШЛОВЧИ ХОДИМЛАР» deb yozilgan,
    /// ya'ni bu ro'yxatdagi hamma odam MBF holdingida ishlaydi.
    /// «Мехнат дафтарчаси» ustuni esa mehnat daftarchasi QAYSI korxonada
    /// saqlanishini bildiradi va 540 xodimdan 287 tasida bo'sh.
    /// </para>
    /// <para>
    /// Shunday yaratilgan har bir mehnat munosabati <c>NeedsReview</c> bilan
    /// belgilanadi va hisobotga tushadi — HR uni albatta ko'radi.
    /// </para>
    /// <para>
    /// Bo'sh satr berilsa, dalilsiz mehnat munosabati umuman yaratilmaydi.
    /// </para>
    /// </remarks>
    public string DefaultLegalEntityCode { get; init; } = DefaultLegalEntity;

    /// <summary>Qadam tanlanganmi.</summary>
    public bool Has(EtlStep step) => (Steps & step) == step;

    /// <summary>
    /// Buyruq satrini tahlil qiladi. Xato bo'lsa — sabab matni.
    /// </summary>
    public static (EtlOptions? Options, string? Error) Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? source = null;
        string? connection = null;
        var apply = false;
        var dryRun = false;
        var steps = EtlStep.All;
        string? report = null;
        int? limit = null;
        string? defaultLegalEntity = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            switch (arg)
            {
                case "--source" or "--file":
                    if (!TryValue(args, ref i, out source))
                    {
                        return (null, "«--source» dan keyin fayl yo'li kerak.");
                    }

                    break;

                case "--connection":
                    if (!TryValue(args, ref i, out connection))
                    {
                        return (null, "«--connection» dan keyin ulanish satri kerak.");
                    }

                    break;

                case "--apply":
                    apply = true;
                    break;

                case "--dry-run":
                    dryRun = true;
                    break;

                case "--steps":
                    {
                        if (!TryValue(args, ref i, out var value) || value is null)
                        {
                            return (null, "«--steps» dan keyin qadamlar ro'yxati kerak.");
                        }

                        var (parsed, error) = ParseSteps(value);
                        if (error is not null)
                        {
                            return (null, error);
                        }

                        steps = parsed;
                        break;
                    }

                case "--report":
                    if (!TryValue(args, ref i, out report))
                    {
                        return (null, "«--report» dan keyin fayl yo'li kerak.");
                    }

                    break;

                case "--limit":
                    {
                        if (!TryValue(args, ref i, out var value)
                            || !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
                            || count < 1)
                        {
                            return (null, "«--limit» dan keyin musbat butun son kerak.");
                        }

                        limit = count;
                        break;
                    }

                case "--default-legal-entity":
                    if (!TryValue(args, ref i, out defaultLegalEntity))
                    {
                        return (null, "«--default-legal-entity» dan keyin kod kerak (bo'sh satr ham mumkin).");
                    }

                    break;

                default:
                    return (null, $"Noma'lum parametr: «{arg}». Yordam uchun: --help");
            }
        }

        if (apply && dryRun)
        {
            return (null, "«--apply» va «--dry-run» birga berilmaydi.");
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            return (null, "Excel fayl ko'rsatilmagan: «--source <yo'l>».");
        }

        if (apply && string.IsNullOrWhiteSpace(connection))
        {
            return (null, "«--apply» uchun «--connection» majburiy — qayerga yozilishi aniq bo'lishi kerak.");
        }

        var options = new EtlOptions
        {
            Source = source,
            Connection = string.IsNullOrWhiteSpace(connection) ? null : connection,
            Apply = apply,
            Steps = steps,
            Limit = limit,
            ReportPath = report ?? DefaultReportPath,
            DefaultLegalEntityCode = defaultLegalEntity?.Trim() ?? DefaultLegalEntity,
        };

        return (options, null);
    }

    private static (EtlStep Steps, string? Error) ParseSteps(string value)
    {
        var result = EtlStep.None;

        foreach (var name in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var step = name.ToLowerInvariant() switch
            {
                "employees" or "xodimlar" => EtlStep.Employees,
                "employments" or "mehnat" => EtlStep.Employments,
                "events" or "hodisalar" => EtlStep.Events,
                "probations" or "sinov" => EtlStep.Probations,
                "flags" or "belgilar" => EtlStep.Flags,
                "all" or "hammasi" => EtlStep.All,
                _ => EtlStep.None,
            };

            if (step == EtlStep.None)
            {
                return (EtlStep.None, $"Noma'lum qadam: «{name}». Mumkin: employees, employments, events, probations, flags.");
            }

            result |= step;
        }

        return result == EtlStep.None
            ? (EtlStep.None, "«--steps» bo'sh.")
            : (result, null);
    }

    private static bool TryValue(string[] args, ref int index, out string? value)
    {
        if (index + 1 >= args.Length)
        {
            value = null;
            return false;
        }

        index++;
        value = args[index];
        return true;
    }
}
