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

    /// <summary>Rag'bat turi — «91.14 Тақдирланган ходимлар» varag'i uchun.</summary>
    AwardType,

    /// <summary>
    /// Jarima turi.
    /// </summary>
    /// <remarks>
    /// Bloknotda jarima varag'i YO'Q, ya'ni bu ma'lumotnomadan ko'chirishda
    /// hech narsa izlanmaydi. U baribir yuklanadi: <c>Incentive</c> agregati
    /// ikkala turni ham qabul qiladi va jadval faqat rag'bat uchun to'ldirilsa,
    /// keyin jarima qo'shilganda buni sezmay qolish oson bo'lardi.
    /// </remarks>
    PenaltyType,
}

/// <summary>Ma'lumotnomaning bitta yozuvi.</summary>
/// <param name="Id">Bazadagi identifikator.</param>
/// <param name="HasAmount">Tur pul bilan bog'liqmi.</param>
/// <remarks>
/// <see cref="HasAmount"/> faqat rag'bat va jarima turlarida ma'noli, qolgan
/// ma'lumotnomalarda doim <c>false</c>. U ATAYLAB identifikator bilan BIR
/// yozuvda saqlanadi: <c>Incentive.Grant</c> ikkalasini ham talab qiladi, va
/// ularni ikki alohida qidiruvdan yig'ish bir turning identifikatorini
/// boshqasining «pullimi» javobi bilan qo'shib yuborish xavfini tug'dirardi.
/// </remarks>
public readonly record struct CatalogEntry(Guid Id, bool HasAmount);

/// <summary>
/// Kanonik nom → bazadagi identifikator jadvali.
/// </summary>
/// <remarks>
/// <para>
/// <c>aliases.json</c> Excel'dagi xom qiymatni <b>kanonik nomga</b> aylantiradi
/// (<c>«Коровул»</c> → <c>«Қоровул»</c>), bu jadval esa nomni
/// <b>identifikatorga</b>. Ikkisi ataylab ajratilgan: nom barqaror va hujjatda
/// ko'rinadi, identifikator esa har bir bazada boshqacha.
/// </para>
/// <para>
/// Ulanishsiz ishlaganda (<c>--dry-run</c>, baza ko'rsatilmagan) identifikator
/// nom asosida <b>deterministik</b> hisoblanadi. Bunday qiymat hech qachon
/// bazaga yozilmaydi — u faqat «nom ma'lumotnomada bormi?» degan savolga
/// javob berish uchun kerak.
/// </para>
/// </remarks>
public sealed class CatalogIndex
{
    private readonly Dictionary<CatalogKind, Dictionary<string, CatalogEntry>> _tables;

    private CatalogIndex(Dictionary<CatalogKind, Dictionary<string, CatalogEntry>> tables) => _tables = tables;

    /// <summary>Ma'lumotnomadagi yozuvlar soni.</summary>
    public int Count(CatalogKind kind) => _tables[kind].Count;

    /// <summary>Nom bo'yicha butun yozuvni topadi — identifikator va «pullimi».</summary>
    public CatalogEntry? Entry(CatalogKind kind, string? code)
        => code is { Length: > 0 } && _tables[kind].TryGetValue(code, out var entry) ? entry : null;

    /// <summary>Nom bo'yicha identifikator topadi.</summary>
    public Guid? Find(CatalogKind kind, string? code) => Entry(kind, code)?.Id;

    /// <summary>Nomlar ro'yxatini identifikatorlarga aylantiradi (topilmaganini tashlab).</summary>
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
        IReadOnlyDictionary<string, Guid> terminationReasons,
        IReadOnlyDictionary<string, CatalogEntry> awardTypes,
        IReadOnlyDictionary<string, CatalogEntry> penaltyTypes)
        => new(new Dictionary<CatalogKind, Dictionary<string, CatalogEntry>>
        {
            [CatalogKind.LegalEntity] = Copy(legalEntities),
            [CatalogKind.Direction] = Copy(directions),
            [CatalogKind.Department] = Copy(departments),
            [CatalogKind.Position] = Copy(positions),
            [CatalogKind.TerminationReason] = Copy(terminationReasons),
            [CatalogKind.AwardType] = Copy(awardTypes),
            [CatalogKind.PenaltyType] = Copy(penaltyTypes),
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
        => new(new Dictionary<CatalogKind, Dictionary<string, CatalogEntry>>
        {
            [CatalogKind.LegalEntity] = ReadNames("legal-entities.json"),
            [CatalogKind.Direction] = ReadNames("directions.json"),
            [CatalogKind.Department] = ReadNames("departments.json"),
            [CatalogKind.Position] = ReadNames("positions.json"),
            [CatalogKind.TerminationReason] = ReadNames("termination-reasons.json"),
            [CatalogKind.AwardType] = ReadNames("award-types.json"),
            [CatalogKind.PenaltyType] = ReadNames("penalty-types.json"),
        });

    private static Dictionary<string, CatalogEntry> Copy(IReadOnlyDictionary<string, Guid> source)
    {
        var table = new Dictionary<string, CatalogEntry>(source.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var (name, id) in source)
        {
            table[name] = new CatalogEntry(id, HasAmount: false);
        }

        return table;
    }

    private static Dictionary<string, CatalogEntry> Copy(IReadOnlyDictionary<string, CatalogEntry> source)
    {
        var table = new Dictionary<string, CatalogEntry>(source.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var (name, entry) in source)
        {
            table[name] = entry;
        }

        return table;
    }

    /// <summary>
    /// Urug'lantirish faylidagi yozuvlarni o'qiydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Kalit — <b>nom</b>, kod emas. Ma'lumotnomalardan <c>code</c> maydoni
    /// butunlay olib tashlangan (u qo'lda yoziladigan maydon edi va bir nom
    /// ikki kodga aylanib ketardi), <c>aliases.json</c> ham endi to'g'ridan
    /// to'g'ri nomga bog'laydi. Bazadan yuklanadigan jadval
    /// (<c>EtlDatabase.NamesAsync</c>) ham nom bo'yicha qurilgan — ikki yo'l
    /// bir xil kalitni berishi SHART, aks holda ulanishsiz quruq yurish har
    /// bir qiymatni «ma'lumotnomada yo'q» deb ko'rsatardi.
    /// </para>
    /// <para>
    /// <c>hasAmount</c> faqat rag'bat va jarima fayllarida bor; qolganida u
    /// yo'q va <c>false</c> bo'lib qoladi.
    /// </para>
    /// </remarks>
    private static Dictionary<string, CatalogEntry> ReadNames(string fileName)
    {
        var assembly = typeof(Bonvi.Infrastructure.Persistence.AppDbContext).Assembly;
        var suffix = "." + fileName;

        var resourceName = Array.Find(
            assembly.GetManifestResourceNames(),
            name => name.EndsWith(suffix, StringComparison.Ordinal));

        var table = new Dictionary<string, CatalogEntry>(StringComparer.OrdinalIgnoreCase);

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
            if (row.TryGetProperty("name", out var name)
                && name.ValueKind == JsonValueKind.String
                && name.GetString() is { Length: > 0 } value)
            {
                var hasAmount = row.TryGetProperty("hasAmount", out var amount)
                    && amount.ValueKind == JsonValueKind.True;

                table[value] = new CatalogEntry(PlaceholderId(value), hasAmount);
            }
        }

        return table;
    }

    /// <summary>
    /// Nomdan deterministik identifikator yasaydi (faqat ulanishsiz rejim uchun).
    /// </summary>
    /// <remarks>
    /// Xesh bu yerda <b>xavfsizlik uchun emas</b>, faqat barqaror 16 baytli
    /// qiymat olish uchun kerak: bir xil nom → bir xil identifikator, ya'ni
    /// ikki marta ishga tushirilgan quruq yurish bir xil natija beradi.
    /// </remarks>
    private static Guid PlaceholderId(string code)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("bonvi-etl:" + code));
        return new Guid(hash.AsSpan(0, 16));
    }
}
