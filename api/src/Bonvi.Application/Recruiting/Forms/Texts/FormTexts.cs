using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Text.Unicode;

namespace Bonvi.Application.Recruiting.Forms.Texts;

/// <summary>Ro'yxatning bitta varianti — HR tartibi, nomi (har til) va yashirinmi.</summary>
/// <param name="Value">Javobda saqlanadigan qiymat (<c>Apartment</c>, <c>msWord</c>, HR qo'shgani — <c>x-…</c>).</param>
/// <param name="Group">Guruh (faqat kompyuter ro'yxatida: <c>os</c>, <c>office</c>, <c>special</c>).</param>
/// <param name="Label">Nomi: <c>uz-Latn</c>, <c>uz-Cyrl</c>, <c>ru</c>.</param>
/// <param name="Hidden">Yangi anketada taklif qilinmaydi; tanlangani baribir qabul qilinadi va chiqadi.</param>
public sealed record FormTextOption(
    string Value,
    string? Group,
    IReadOnlyDictionary<string, string> Label,
    bool Hidden);

/// <summary>HR o'zgartirgan ro'yxat — butunicha (guruh nomlari va variantlar HR tartibida).</summary>
public sealed record FormTextList(
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Groups,
    IReadOnlyList<FormTextOption> Options);

/// <summary>
/// HR-F02 ning amaldagi HR matnlari — bitta <c>form_text_snapshots</c> qatori
/// (SPEC-blankalar-hrmentor §5). Bo'sh (<see cref="Empty"/>) — o'rnatilgan matnlar.
/// </summary>
/// <remarks>
/// Saqlangan <c>payload</c> ni <see cref="HrF02TextCatalog.Shape"/> yasagan: undagi har bir matn
/// katalogdan o'tgan (kalit, uzunlik, o'rin belgilari), lotin matnlarda tutuq qog'ozdagidek.
/// Bu sinf uni faqat o'qiydi.
/// </remarks>
public sealed partial class FormTexts
{
    public const string Latin = "uz-Latn";
    public const string Cyrillic = "uz-Cyrl";
    public const string Russian = "ru";

    /// <summary>Anketa tillari — kiosk bilan bir xil.</summary>
    public static readonly IReadOnlyList<string> Languages = [Latin, Cyrillic, Russian];

    /// <summary><c>payload</c> va katalog JSON i — barqaror (xesh shundan olinadi), harflar qochirilmaydi.</summary>
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    public static FormTexts Empty { get; } = new(null, new Dictionary<string, IReadOnlyDictionary<string, string>>(), new Dictionary<string, string>(), new Dictionary<string, FormTextList>());

    private FormTexts(
        int? revision,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> strings,
        IReadOnlyDictionary<string, string> blank,
        IReadOnlyDictionary<string, FormTextList> lists)
    {
        Revision = revision;
        Strings = strings;
        Blank = blank;
        Lists = lists;
    }

    /// <summary>Reviziya; o'rnatilgan matnlarda <c>null</c>.</summary>
    public int? Revision { get; }

    /// <summary>Anketa matnlari: til → kalit → matn (faqat HR o'zgartirganlari).</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Strings { get; }

    /// <summary>PDF matnlari: kalit → lotin matn (faqat HR o'zgartirganlari).</summary>
    public IReadOnlyDictionary<string, string> Blank { get; }

    /// <summary>HR o'zgartirgan ro'yxatlar.</summary>
    public IReadOnlyDictionary<string, FormTextList> Lists { get; }

    /// <summary>
    /// Saqlangan qatordan. O'qib bo'lmaydigan qator (qo'lda buzilgan) anketani yiqitmaydi —
    /// o'rnatilgan matnlar.
    /// </summary>
    public static FormTexts FromPayload(int revision, string payloadJson)
    {
        ArgumentNullException.ThrowIfNull(payloadJson);

        try
        {
            return JsonSerializer.Deserialize<PayloadJson>(payloadJson, Json) is { } payload
                ? From(revision, payload)
                : Empty;
        }
        catch (JsonException)
        {
            return Empty;
        }
    }

    internal static FormTexts From(int revision, PayloadJson payload) => new(
        revision,
        (payload.Strings ?? []).ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(pair.Value ?? [], StringComparer.Ordinal),
            StringComparer.Ordinal),
        new Dictionary<string, string>(payload.Blank ?? [], StringComparer.Ordinal),
        (payload.Lists ?? [])
            .Where(pair => pair.Value is not null)
            .ToDictionary(pair => pair.Key, pair => pair.Value.ToList(), StringComparer.Ordinal));

    /// <summary>PDF matni: HR niki ?? asl matn.</summary>
    public string Pdf(string key)
        => Blank.TryGetValue(key, out var text) ? text : QuestionnairePdfTexts.Default(key);

    /// <summary>
    /// Qiymatli PDF matni: <c>{{nom}}</c> o'rniga qiymat. O'rin belgilarining to'plami
    /// <see cref="HrF02TextCatalog.Shape"/> da tekshirilgan — HR matnida ham aynan shular.
    /// </summary>
    public string Pdf(string key, params (string Name, string Value)[] values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return Placeholder().Replace(Pdf(key), match =>
        {
            var name = match.Groups[1].Value;
            foreach (var (candidate, value) in values)
            {
                if (string.Equals(candidate, name, StringComparison.Ordinal))
                {
                    return value;
                }
            }

            return match.Value;
        });
    }

    /// <summary>
    /// PDF matnini o'rin belgisi atrofida ikkiga bo'ladi — o'rtasiga belgi (☑) yoki qalin matn
    /// qo'yish uchun («Men, <b>F.I.Sh.</b> (F.I.Sh.):»).
    /// </summary>
    public (string Before, string After) PdfAround(string key, string placeholder)
    {
        var text = Pdf(key);
        var match = Placeholder().Matches(text).FirstOrDefault(m => m.Groups[1].Value == placeholder);

        return match is null
            ? (text, string.Empty)
            : (text[..match.Index], text[(match.Index + match.Length)..]);
    }

    /// <summary>HR shu ro'yxatni o'zgartirganmi.</summary>
    public bool HasList(string listId) => Lists.ContainsKey(listId);

    /// <summary>Ro'yxatning amaldagi variantlari: HR niki ?? katalogdagi.</summary>
    public IReadOnlyList<FormTextOption> Options(string listId)
        => Lists.TryGetValue(listId, out var list) ? list.Options : HrF02TextCatalog.Current.DefaultOptions(listId);

    /// <summary>Guruh nomi (tilda): HR niki ?? katalogdagi; noma'lum guruh — <c>null</c>.</summary>
    public string? GroupLabel(string listId, string groupId, string language)
    {
        if (Lists.TryGetValue(listId, out var list)
            && list.Groups.TryGetValue(groupId, out var labels)
            && labels.TryGetValue(language, out var label))
        {
            return label;
        }

        return HrF02TextCatalog.Current.DefaultGroupLabel(listId, groupId, language);
    }

    /// <summary>Variantning lotin nomi; ro'yxatda yo'q — <c>null</c>.</summary>
    public string? LatinLabel(string listId, string value)
        => Options(listId).FirstOrDefault(option => option.Value == value)?.Label.GetValueOrDefault(Latin);

    /// <summary>Kompyuter ro'yxatining barcha qiymatlari (yashiringanlari ham) — tekshiruvchi uchun.</summary>
    public IReadOnlySet<string> ComputerValues()
        => Options(HrF02TextCatalog.ComputerList).Select(option => option.Value).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Variantning HR O'ZGARTIRGAN lotin nomi; HR tegmagan (katalogdagi bilan bir xil) — <c>null</c>.
    /// HR qo'shgan variant — doim uning nomi.
    /// </summary>
    /// <remarks>
    /// Katalog nomi kioskdagi so'z, qog'oz esa ba'zan boshqacha yozadi («O‘rta (maktab)»). HR ro'yxatni
    /// faqat tartiblasa yoki bitta variantni yashirsa, qolgan variantlar PDF'da QOG'OZDAGIDEK qoladi —
    /// chaqiruvchi <c>null</c> da o'z asl so'zini qo'yadi.
    /// </remarks>
    public static string? ChangedLatin(string listId, FormTextOption option)
    {
        ArgumentNullException.ThrowIfNull(option);

        var label = option.Label.GetValueOrDefault(Latin);
        var known = HrF02TextCatalog.Current.DefaultOptions(listId).FirstOrDefault(candidate => candidate.Value == option.Value);
        if (known is null || label is null)
        {
            return label;
        }

        var original = known.Label[Latin];

        return label == original || label == UzbekTypography.Apply(original) ? null : label;
    }

    /// <summary>Guruhning HR o'zgartirgan lotin nomi; tegmagan — <c>null</c> (<see cref="ChangedLatin"/>).</summary>
    public string? ChangedGroupLatin(string listId, string groupId)
    {
        var label = GroupLabel(listId, groupId, Latin);
        var original = HrF02TextCatalog.Current.DefaultGroupLabel(listId, groupId, Latin);

        return label is null || original is null || label == original || label == UzbekTypography.Apply(original) ? null : label;
    }

    /// <summary>
    /// Kompyuter jadvalining uch ustuni (OS, ofis, maxsus) — PDF va anketa xulosasi uchun.
    /// </summary>
    /// <param name="chosen">Nomzod belgilagan dasturlar (kalit → daraja).</param>
    /// <remarks>
    /// HR ro'yxatni o'zgartirmagan bo'lsa — <see cref="QuestionnaireFormCatalog.ComputerGroups"/> AYNAN.
    /// O'zgartirgan bo'lsa — dasturlar HR tartibida (HR qo'shganlari o'z guruhida); nomlar — HR
    /// o'zgartirgani, aks holda qog'ozdagi (<see cref="ChangedLatin"/>); yashirilgan dastur yoki HR
    /// olib tashlagan asl dastur faqat nomzod uni belgilagan bo'lsa chiqadi (imzolangan javob yo'qolmasin).
    /// </remarks>
    public IReadOnlyList<ComputerSkillGroup> ComputerGroups(IReadOnlyDictionary<string, int> chosen)
    {
        ArgumentNullException.ThrowIfNull(chosen);

        var catalog = QuestionnaireFormCatalog.ComputerGroups;
        if (!HasList(HrF02TextCatalog.ComputerList))
        {
            return catalog;
        }

        var options = Options(HrF02TextCatalog.ComputerList);
        var paper = catalog.SelectMany(group => group.Items).ToDictionary(item => item.Key, item => item.Label, StringComparer.Ordinal);
        var columns = new List<ComputerSkillGroup>(catalog.Count);

        for (var index = 0; index < catalog.Count; index++)
        {
            var groupId = HrF02TextCatalog.ComputerGroupIds[index];
            var items = options
                .Where(option => option.Group == groupId && (!option.Hidden || chosen.ContainsKey(option.Value)))
                .Select(option => new ComputerSkillItem(
                    option.Value,
                    ChangedLatin(HrF02TextCatalog.ComputerList, option)
                        ?? paper.GetValueOrDefault(option.Value)
                        ?? option.Label[Latin]))
                .ToList();

            items.AddRange(catalog[index].Items.Where(item =>
                chosen.ContainsKey(item.Key) && options.All(option => option.Value != item.Key)));

            columns.Add(new ComputerSkillGroup(ChangedGroupLatin(HrF02TextCatalog.ComputerList, groupId) ?? catalog[index].Title, items));
        }

        return columns;
    }

    [GeneratedRegex(@"\{\{\s*([A-Za-z_][A-Za-z0-9_]*)\s*\}\}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    internal static partial Regex Placeholder();

    /// <summary>O'rin belgilari to'plami (HRMentor qoidasi bilan bir xil).</summary>
    internal static HashSet<string> PlaceholdersOf(string text)
        => Placeholder().Matches(text).Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
}

/// <summary><c>form_text_snapshots.payload</c> ning shakli.</summary>
internal sealed class PayloadJson
{
    public int Revision { get; set; }

    public Dictionary<string, Dictionary<string, string>>? Strings { get; set; }

    public Dictionary<string, string>? Blank { get; set; }

    public Dictionary<string, ListJson>? Lists { get; set; }
}

/// <summary>Bitta ro'yxat — payload da va HRMentor javobida bir xil shaklda.</summary>
internal sealed class ListJson
{
    public Dictionary<string, Dictionary<string, string>>? Groups { get; set; }

    public List<OptionJson>? Options { get; set; }

    public FormTextList ToList() => new(
        (Groups ?? []).ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(pair.Value ?? [], StringComparer.Ordinal),
            StringComparer.Ordinal),
        [
            .. (Options ?? [])
                .Where(option => option is { Value: not null, Label: not null })
                .Select(option => new FormTextOption(
                    option.Value!,
                    option.Group,
                    new Dictionary<string, string>(option.Label!, StringComparer.Ordinal),
                    option.Hidden)),
        ]);
}

/// <summary>Bitta variant.</summary>
internal sealed class OptionJson
{
    public string? Value { get; set; }

    public string? Group { get; set; }

    public Dictionary<string, string>? Label { get; set; }

    public bool Hidden { get; set; }
}
