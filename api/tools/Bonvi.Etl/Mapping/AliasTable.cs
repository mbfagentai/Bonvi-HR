using System.Text;
using System.Text.Json;
using Bonvi.Etl.Excel;

namespace Bonvi.Etl.Mapping;

/// <summary>Alias jadvalidan qidirish natijasi.</summary>
/// <param name="Known">
/// Xom qiymat jadvalda bormi. <c>false</c> — HR yangi qiymat kiritgan,
/// ma'lumotnoma yangilanishi kerak.
/// </param>
/// <param name="Codes">
/// Kanonik kodlar. Bo'sh bo'lishi mumkin: jadvalda qiymat bor, lekin
/// <c>null</c> ga bog'langan — ya'ni bu ma'lumotnomaga tegishli emas
/// (sana, telefon, noto'g'ri ustunga tushgan qiymat). Bunday katak
/// ATAYLAB bo'sh qoldiriladi.
/// </param>
public readonly record struct AliasMatch(bool Known, IReadOnlyList<string> Codes)
{
    /// <summary>Jadvalda umuman yo'q.</summary>
    public static readonly AliasMatch Unknown = new(false, []);

    /// <summary>Jadvalda bor, lekin ataylab bog'lanmagan.</summary>
    public static readonly AliasMatch Ignored = new(true, []);

    /// <summary>Birinchi kod (yoki <c>null</c>).</summary>
    public string? First => Codes.Count > 0 ? Codes[0] : null;

    /// <summary>Kod topildimi.</summary>
    public bool HasCode => Codes.Count > 0;
}

/// <summary>
/// <c>aliases.json</c> — Excel'dagi xom qiymatni kanonik kodga bog'lovchi jadval.
/// </summary>
/// <remarks>
/// <para>
/// Bu ko'chirishning eng muhim tayanchi. Jadval Excel tahlilidan avtomatik
/// yaratilgan (<c>tools/data/build_reference_data.py</c>) va
/// <c>Bonvi.Infrastructure</c> yig'malasiga resurs sifatida joylashtirilgan —
/// ya'ni u kod bilan bir versiyada yuradi.
/// </para>
/// <para>
/// <b>Uchta javob bo'lishi mumkin</b>, va ularni farqlash muhim:
/// </para>
/// <list type="number">
///   <item>kod topildi — qiymat qo'yiladi;</item>
///   <item>jadvalda bor, lekin <c>null</c> — qiymat ma'lumotnomaga tegishli
///         emas, katak bo'sh qoldiriladi va <b>hisobotga yozilmaydi</b>
///         (bu kutilgan hol, 400 dan ortiq katak shunday);</item>
///   <item>jadvalda yo'q — <b>hisobotga yoziladi</b>: HR yangi qiymat
///         kiritgan yoki imlo o'zgargan.</item>
/// </list>
/// <para>
/// <b>Qidiruv registr va bo'shliqqa sezgir emas.</b> Sabab: Excel'da bir xil
/// qiymat «Маркетинг бўлими» va «Маркетинг  бўлими» ko'rinishida ham,
/// katta-kichik harf farqi bilan ham uchraydi.
/// </para>
/// </remarks>
public sealed class AliasTable
{
    /// <summary>Resurs nomi — <c>Bonvi.Infrastructure</c> yig'malasi ichida.</summary>
    private const string ResourceSuffix = ".aliases.json";

    private readonly Dictionary<string, IReadOnlyList<string>> _legalEntities;
    private readonly Dictionary<string, IReadOnlyList<string>> _directions;
    private readonly Dictionary<string, IReadOnlyList<string>> _departments;
    private readonly Dictionary<string, IReadOnlyList<string>> _departmentBlocks;
    private readonly Dictionary<string, IReadOnlyList<string>> _positions;
    private readonly Dictionary<string, IReadOnlyList<string>> _terminationReasons;
    private readonly Dictionary<string, IReadOnlyList<string>> _awardTypes;

    private AliasTable(IReadOnlyDictionary<string, Dictionary<string, IReadOnlyList<string>>> sections)
    {
        _legalEntities = Section(sections, "legalEntities");
        _directions = Section(sections, "directions");
        _departments = Section(sections, "departments");
        _departmentBlocks = Section(sections, "departmentBlocks");
        _positions = Section(sections, "positions");
        _terminationReasons = Section(sections, "terminationReasons");
        _awardTypes = Section(sections, "awardTypes");
    }

    /// <summary>Yuridik shaxs: bir katakda bir nechtasi bo'lishi mumkin.</summary>
    public AliasMatch LegalEntity(ExcelCell cell) => Find(_legalEntities, cell);

    /// <summary>Yo'nalish: kombinatsiya odatiy hol («Вело, метан, шина»).</summary>
    public AliasMatch Direction(ExcelCell cell) => Find(_directions, cell);

    /// <summary>Bo'lim.</summary>
    public AliasMatch Department(ExcelCell cell) => Find(_departments, cell);

    /// <summary>Varaqdagi blok sarlavhasi — bo'limning ikkinchi dalili.</summary>
    public AliasMatch DepartmentBlock(ExcelCell cell) => Find(_departmentBlocks, cell);

    /// <summary>Lavozim.</summary>
    public AliasMatch Position(ExcelCell cell) => Find(_positions, cell);

    /// <summary>Ishdan ketish sababi.</summary>
    public AliasMatch TerminationReason(ExcelCell cell) => Find(_terminationReasons, cell);

    /// <summary>Rag'bat turi.</summary>
    public AliasMatch AwardType(ExcelCell cell) => Find(_awardTypes, cell);

    /// <summary>
    /// Jadvalni <c>Bonvi.Infrastructure</c> yig'malasidan yuklaydi.
    /// </summary>
    /// <remarks>
    /// Resurs to'liq nomi bo'yicha emas, oxiri bo'yicha izlanadi: papka
    /// ko'chirilsa yoki ildiz nom fazosi o'zgarsa, jimgina buzilmasin.
    /// </remarks>
    public static AliasTable Load()
    {
        var assembly = typeof(Bonvi.Infrastructure.Persistence.AppDbContext).Assembly;

        var resourceName = Array.Find(
            assembly.GetManifestResourceNames(),
            name => name.EndsWith(ResourceSuffix, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                "«aliases.json» resursi topilmadi. Bonvi.Infrastructure yig'malasi to'g'ri qurilganmi?");

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("«aliases.json» resursini ochib bo'lmadi.");

        return Parse(stream);
    }

    /// <summary>Oqimdan o'qiydi — testlarda o'z jadvalini berish uchun.</summary>
    public static AliasTable Parse(Stream stream)
    {
        using var document = JsonDocument.Parse(stream);

        var sections = new Dictionary<string, Dictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal);

        foreach (var property in document.RootElement.EnumerateObject())
        {
            // «unmapped» — HR bilan aniqlanishi kerak bo'lgan qiymatlar ro'yxati.
            // U massivlardan iborat va ko'chirishga kerak emas.
            if (property.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var table = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

            foreach (var entry in property.Value.EnumerateObject())
            {
                var key = Key(entry.Name);
                if (key.Length == 0)
                {
                    continue;
                }

                IReadOnlyList<string> codes = entry.Value.ValueKind == JsonValueKind.String
                    ? Split(entry.Value.GetString())
                    : [];

                // Bir xil kalitning ikkinchi ko'rinishi (masalan faqat registr
                // bilan farq qiladigan) — birinchisi kuchda qoladi. Ular bir xil
                // kodga bog'langan, shu sababli tanlov ahamiyatsiz.
                _ = table.TryAdd(key, codes);
            }

            sections[property.Name] = table;
        }

        return new AliasTable(sections);
    }

    /// <summary>Kodlar satrini ajratadi: <c>«velo|metan|shina»</c> → uchta kod.</summary>
    public static IReadOnlyList<string> Split(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return [.. value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }

    private static Dictionary<string, IReadOnlyList<string>> Section(
        IReadOnlyDictionary<string, Dictionary<string, IReadOnlyList<string>>> sections,
        string name)
        => sections.TryGetValue(name, out var table)
            ? table
            : new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

    private static AliasMatch Find(Dictionary<string, IReadOnlyList<string>> table, ExcelCell cell)
    {
        if (cell.IsEmpty)
        {
            return AliasMatch.Ignored;
        }

        if (table.TryGetValue(Key(cell.Text), out var codes))
        {
            return new AliasMatch(true, codes);
        }

        // Alias jadvali Excel'ni Python bilan o'qib tuzilgan va u yerda son
        // katagi «46188.0» ko'rinishida bo'lgan. ClosedXML esa «46188» beradi.
        // Bir xil katak ikki xil kalit bo'lib qolmasligi uchun ikkinchi shakl
        // ham sinaladi.
        if (cell.IsWholeNumber && table.TryGetValue(Key(cell.Text + ".0"), out var numeric))
        {
            return new AliasMatch(true, numeric);
        }

        return AliasMatch.Unknown;
    }

    /// <summary>
    /// Solishtirish kaliti: ortiqcha bo'shliqlar olib tashlanadi va registr
    /// bir xillashtiriladi.
    /// </summary>
    /// <remarks>
    /// Bu <c>tools/data/normalize.py</c> dagi to'liq normallashtirish EMAS
    /// (u kirill imlo variantlarini ham birlashtiradi). Bu yerda ataylab
    /// yumshoqroq qoida: alias jadvalida imlo variantlarining har biri
    /// alohida kalit sifatida allaqachon yozilgan. Agressiv normallashtirish
    /// esa turli qiymatlarni bir kalitga urishtirib yuborardi.
    /// </remarks>
    public static string Key(string? value)
    {
        var normalized = ExcelCell.Normalize(value);
        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        // Kirill uchun `ToLowerInvariant` yetarli: u madaniyatga bog'liq
        // emas va turk «i» muammosini keltirib chiqarmaydi.
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            builder.Append(char.ToLowerInvariant(ch));
        }

        return builder.ToString();
    }
}
