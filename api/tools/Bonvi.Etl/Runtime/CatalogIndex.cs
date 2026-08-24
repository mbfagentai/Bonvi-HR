using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Bonvi.Etl.Runtime;

/// <summary>Ma'lumotnoma turi — xato xabarlarida ishlatiladi.</summary>
public enum CatalogKind
{
    LegalEntity,
    Direction,
    Department,
    Position,
    TerminationReason,
}

/// <summary>
/// Kanonik kod → bazadagi identifikator jadvali.
/// </summary>
/// <remarks>
/// <para>
/// <c>aliases.json</c> Excel qiymatini <b>kodga</b> aylantiradi
/// (<c>«Коровул»</c> → <c>qorovul</c>), bu jadval esa kodni <b>identifikatorga</b>.
/// Ikkisi ataylab ajratilgan: kod barqaror, identifikator esa har bir bazada
/// boshqacha.
/// </para>
/// <para>
/// Ulanishsiz ishlaganda (<c>--dry-run</c>, baza ko'rsatilmagan) identifikator
/// kod asosida <b>deterministik</b> hisoblanadi. Bunday qiymat hech qachon
/// bazaga yozilmaydi — u faqat «kod ma'lumotnomada bormi?» degan savolga
/// javob berish uchun kerak.
/// </para>
/// </remarks>
public sealed class CatalogIndex
{
    private readonly Dictionary<CatalogKind, Dictionary<string, Guid>> _tables;

    private CatalogIndex(Dictionary<CatalogKind, Dictionary<string, Guid>> tables) => _tables = tables;

    /// <summary>Ma'lumotnomadagi yozuvlar soni.</summary>
    public int Count(CatalogKind kind) => _tables[kind].Count;

    /// <summary>Kod bo'yicha identifikator topadi.</summary>
    public Guid? Find(CatalogKind kind, string? code)
        => code is { Length: > 0 } && _tables[kind].TryGetValue(code, out var id) ? id : null;

    /// <summary>Kodlar ro'yxatini identifikatorlarga aylantiradi (topilmaganini tashlab).</summary>
    public IReadOnlyList<Guid> FindAll(CatalogKind kind, IReadOnlyList<string> codes)
    {
        if (codes.Count == 0)
        {
            return [];
        }

        var result = new List<Guid>(codes.Count);

        foreach (var code in codes)
        {
            if (Find(kind, code) is { } id)
            {
                result.Add(id);
            }
        }

        return result;
    }

    /// <summary>Jadvallarni tayyor lug'atlardan yig'adi (bazadan yuklanganda).</summary>
    public static CatalogIndex FromTables(
        IReadOnlyDictionary<string, Guid> legalEntities,
        IReadOnlyDictionary<string, Guid> directions,
        IReadOnlyDictionary<string, Guid> departments,
        IReadOnlyDictionary<string, Guid> positions,
        IReadOnlyDictionary<string, Guid> terminationReasons)
        => new(new Dictionary<CatalogKind, Dictionary<string, Guid>>
        {
            [CatalogKind.LegalEntity] = Copy(legalEntities),
            [CatalogKind.Direction] = Copy(directions),
            [CatalogKind.Department] = Copy(departments),
            [CatalogKind.Position] = Copy(positions),
            [CatalogKind.TerminationReason] = Copy(terminationReasons),
        });

    /// <summary>
    /// Ma'lumotnomalarni urug'lantirish fayllaridan yig'adi — baza kerak emas.
    /// </summary>
    /// <remarks>
    /// Fayllar <c>Bonvi.Infrastructure</c> yig'malasiga joylashtirilgan va
    /// bazaga aynan shular yoziladi. Ya'ni ulanishsiz tekshiruv ham haqiqiy
    /// ro'yxat bo'yicha ketadi.
    /// </remarks>
    public static CatalogIndex FromSeedFiles()
        => new(new Dictionary<CatalogKind, Dictionary<string, Guid>>
        {
            [CatalogKind.LegalEntity] = ReadCodes("legal-entities.json"),
            [CatalogKind.Direction] = ReadCodes("directions.json"),
            [CatalogKind.Department] = ReadCodes("departments.json"),
            [CatalogKind.Position] = ReadCodes("positions.json"),
            [CatalogKind.TerminationReason] = ReadCodes("termination-reasons.json"),
        });

    private static Dictionary<string, Guid> Copy(IReadOnlyDictionary<string, Guid> source)
    {
        var table = new Dictionary<string, Guid>(source.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var (code, id) in source)
        {
            table[code] = id;
        }

        return table;
    }

    private static Dictionary<string, Guid> ReadCodes(string fileName)
    {
        var assembly = typeof(Bonvi.Infrastructure.Persistence.AppDbContext).Assembly;
        var suffix = "." + fileName;

        var resourceName = Array.Find(
            assembly.GetManifestResourceNames(),
            name => name.EndsWith(suffix, StringComparison.Ordinal));

        var table = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        if (resourceName is null)
        {
            return table;
        }

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return table;
        }

        using var document = JsonDocument.Parse(stream);

        foreach (var row in document.RootElement.EnumerateArray())
        {
            if (row.TryGetProperty("code", out var code)
                && code.ValueKind == JsonValueKind.String
                && code.GetString() is { Length: > 0 } value)
            {
                table[value] = PlaceholderId(value);
            }
        }

        return table;
    }

    /// <summary>
    /// Koddan deterministik identifikator yasaydi (faqat ulanishsiz rejim uchun).
    /// </summary>
    /// <remarks>
    /// Xesh bu yerda <b>xavfsizlik uchun emas</b>, faqat barqaror 16 baytli
    /// qiymat olish uchun kerak: bir xil kod → bir xil identifikator, ya'ni
    /// ikki marta ishga tushirilgan quruq yurish bir xil natija beradi.
    /// </remarks>
    private static Guid PlaceholderId(string code)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("bonvi-etl:" + code));
        return new Guid(hash.AsSpan(0, 16));
    }
}
