using System.Globalization;
using System.Text;

namespace Bonvi.Etl.Reporting;

/// <summary>ETL muammo yozuvi bo'yicha ko'rilgan chora.</summary>
public static class EtlAction
{
    /// <summary>Maydon bo'sh qoldirildi.</summary>
    public const string LeftEmpty = "bo'sh qoldirildi";

    /// <summary>Yozuv «tekshirish kerak» deb belgilandi.</summary>
    public const string Flagged = "tekshirishga belgilandi";

    /// <summary>Qator butunlay o'tkazib yuborildi.</summary>
    public const string RowSkipped = "qator o'tkazib yuborildi";

    /// <summary>Yozuv allaqachon bor — tegilmadi.</summary>
    public const string AlreadyExists = "mavjud, tegilmadi";

    /// <summary>Qiymat e'tiborsiz qoldirildi (ma'lumotnomaga tegishli emas).</summary>
    public const string Ignored = "e'tiborsiz qoldirildi";

    /// <summary>
    /// Yozuv kiritildi, lekin O'ZGARTIRILGAN holda.
    /// </summary>
    /// <remarks>
    /// <see cref="RowSkipped"/> dan farqi muhim: qator YO'QOLMADI, ya'ni
    /// HR uni qaytadan kiritishi shart emas — lekin tizim qoidasiga
    /// moslash uchun biror maydon o'zgartirildi (masalan belgi yopiq
    /// holda kiritildi). Bu ikkalasini bir chora bilan belgilash HR ni
    /// «nima yo'qoldi?» degan savol bilan qoldirardi.
    /// </remarks>
    public const string Adjusted = "moslashtirib kiritildi";
}

/// <summary>Hisobotdagi bitta satr.</summary>
/// <param name="Step">Ko'chirish qadami.</param>
/// <param name="Sheet">Excel varag'i.</param>
/// <param name="Row">Qator raqami.</param>
/// <param name="Employee">Xodim (F.I.Sh) — kim haqida ekani darhol ko'rinsin.</param>
/// <param name="Problem">Muammoning qisqa nomi.</param>
/// <param name="Value">Muammoli xom qiymat.</param>
/// <param name="Action">Ko'rilgan chora.</param>
public sealed record EtlIssue(
    string Step,
    string Sheet,
    int Row,
    string Employee,
    string Problem,
    string Value,
    string Action);

/// <summary>Bitta qadamning jamlanmasi.</summary>
public sealed class StepSummary
{
    public StepSummary(string name) => Name = name;

    /// <summary>Qadam nomi (<c>employees</c>, <c>events</c>…).</summary>
    public string Name { get; }

    /// <summary>Excel'dan o'qilgan qatorlar.</summary>
    public int Read { get; set; }

    /// <summary>Yaratilgan (yoki quruq yurishda — yaratiladigan) yozuvlar.</summary>
    public int Created { get; set; }

    /// <summary>Yangilangan mavjud yozuvlar.</summary>
    public int Updated { get; set; }

    /// <summary>O'tkazib yuborilgan qatorlar.</summary>
    public int Skipped { get; set; }

    /// <summary>«Tekshirish kerak» deb belgilangan yozuvlar.</summary>
    public int NeedsReview { get; set; }

    /// <summary>Qadam yiqilgan bo'lsa — sabab.</summary>
    public string? Failure { get; set; }
}

/// <summary>
/// ETL hisoboti: muammolar CSV fayliga, jamlanma konsolga.
/// </summary>
/// <remarks>
/// <para>
/// <b>Hisobot — bu vositaning asosiy mahsuloti</b>, bazadagi yozuvlar emas.
/// Sabab: ko'chirishdan keyin HR nima tekshirishi kerakligini bilishi shart.
/// «540 xodim ko'chirildi» degan xabar foydasiz; «12 ta JSHSHIR noto'g'ri,
/// mana qaysi qatorlarda» — foydali.
/// </para>
/// <para>
/// CSV ataylab: HR uni Excel'da ochadi, filtrlaydi va bevosita ish
/// ro'yxati sifatida ishlatadi.
/// </para>
/// </remarks>
public sealed class EtlReport
{
    private readonly List<EtlIssue> _issues = [];
    private readonly Dictionary<string, StepSummary> _steps = new(StringComparer.Ordinal);
    private readonly List<StepSummary> _order = [];

    /// <summary>Barcha muammo yozuvlari.</summary>
    public IReadOnlyList<EtlIssue> Issues => _issues;

    /// <summary>Qadamlar jamlanmasi — bajarilgan tartibda.</summary>
    public IReadOnlyList<StepSummary> Steps => _order;

    /// <summary>Allaqachon mavjud bo'lgani uchun tegilmagan yozuvlar soni.</summary>
    public int AlreadyExisting { get; private set; }

    /// <summary>Qadam jamlanmasini oladi (bo'lmasa yaratadi).</summary>
    public StepSummary Step(string name)
    {
        if (_steps.TryGetValue(name, out var existing))
        {
            return existing;
        }

        var summary = new StepSummary(name);
        _steps[name] = summary;
        _order.Add(summary);
        return summary;
    }

    /// <summary>
    /// Muammoni hisobotga yozadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><see cref="EtlAction.AlreadyExists"/> satr yaratmaydi</b> — faqat
    /// sanaladi. Sabab: hisobot bu HR uchun <b>ish ro'yxati</b>, «bu yozuv
    /// allaqachon bor edi» esa hech qanday ish talab qilmaydi. Ikkinchi
    /// yugurishda bunday satrlar 7000 dan oshadi va haqiqiy muammolarni
    /// ko'mib yuborardi — CSV ochilmay qoladi.
    /// </para>
    /// <para>
    /// Soni yo'qolmaydi: u <see cref="AlreadyExisting"/> da va har bir
    /// qadamning <c>Updated</c> jamlanmasida ko'rinadi.
    /// </para>
    /// </remarks>
    public void Add(string step, string sheet, int row, string employee, string problem, string? value, string action)
    {
        if (string.Equals(action, EtlAction.AlreadyExists, StringComparison.Ordinal))
        {
            AlreadyExisting++;
            return;
        }

        _issues.Add(new EtlIssue(step, sheet, row, employee, problem, value ?? string.Empty, action));
    }

    /// <summary>Hisobotni CSV faylga yozadi.</summary>
    /// <remarks>
    /// <para>
    /// BOM (<c>UTF-8 with signature</c>) ataylab qo'shiladi: usiz Excel
    /// kirill matnini krakozyabra qilib ochadi va hisobot o'qib bo'lmas
    /// holga keladi.
    /// </para>
    /// <para>
    /// Ajratgich sifatida nuqtali vergul EMAS, vergul ishlatiladi —
    /// vazifa shartida shunday belgilangan.
    /// </para>
    /// </remarks>
    public async Task WriteCsvAsync(string path, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        var builder = new StringBuilder();
        builder.AppendLine("qadam,varaq,qator,xodim,muammo,qiymat,harakat");

        foreach (var issue in _issues)
        {
            builder
                .Append(Escape(issue.Step)).Append(',')
                .Append(Escape(issue.Sheet)).Append(',')
                .Append(issue.Row.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(Escape(issue.Employee)).Append(',')
                .Append(Escape(issue.Problem)).Append(',')
                .Append(Escape(issue.Value)).Append(',')
                .Append(Escape(issue.Action)).Append('\n');
        }

        await File.WriteAllTextAsync(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>CSV maydonini qoidaga muvofiq qalqonlaydi.</summary>
    private static string Escape(string value)
    {
        if (value.Length == 0)
        {
            return string.Empty;
        }

        // Yangi qator maydon ichida qolsa, CSV'ni o'qiydigan har bir dastur
        // uni boshqacha talqin qiladi. Bo'shliqqa almashtirish xavfsizroq.
        var clean = value.Replace('\r', ' ').Replace('\n', ' ');

        var needsQuotes = clean.Contains(',', StringComparison.Ordinal)
            || clean.Contains('"', StringComparison.Ordinal);

        return needsQuotes
            ? '"' + clean.Replace("\"", "\"\"", StringComparison.Ordinal) + '"'
            : clean;
    }
}
