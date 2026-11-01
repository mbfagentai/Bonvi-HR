using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Bonvi.Application.Blanks;
using Bonvi.Domain.Common;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Application.Recruiting.Forms.Texts;

/// <summary>Katalogning bitta matni.</summary>
/// <param name="Key">Kalit — kiosk tarjima kaliti yoki <c>pdf.…</c>.</param>
/// <param name="IsBlank">PDF matni (faqat lotin) — aks holda anketa matni (uch til).</param>
/// <param name="Name">HR ko'radigan nom.</param>
/// <param name="Multiline">HRMentor da ko'p qatorli maydon.</param>
/// <param name="MaxLength">Eng uzun matn.</param>
/// <param name="Default">Asl matn — til bo'yicha.</param>
public sealed record CatalogTextItem(
    string Key,
    bool IsBlank,
    string Name,
    bool Multiline,
    int MaxLength,
    IReadOnlyDictionary<string, string> Default);

/// <summary>Ro'yxat guruhi (kompyuter: os / office / special).</summary>
public sealed record CatalogListGroup(string Id, IReadOnlyDictionary<string, string> Default);

/// <summary>Katalogning bitta ro'yxati.</summary>
public sealed record CatalogListDefinition(
    string Id,
    string Title,
    bool Open,
    int MinVisible,
    int MaxOptions,
    IReadOnlyList<CatalogListGroup> Groups,
    IReadOnlyList<FormTextOption> Options);

/// <summary>Tekshirilgan HR matnlari — saqlashga tayyor.</summary>
public sealed record ShapedTexts(int Revision, string PayloadJson);

/// <summary>
/// HR-F02 matnlari katalogi (SPEC-blankalar-hrmentor §4): HR HRMentor da nimani tahrirlay
/// olishi — anketa matnlari, PDF matnlari va ro'yxatlar. Katalog shu yerda YIG'ILADI va
/// HRMentor ga yuboriladi; HR ning javobi (<see cref="Shape"/>) shu katalog bo'yicha tekshiriladi.
/// </summary>
/// <remarks>
/// <para>
/// Anketa matnlari — <c>kiosk-texts.json</c> (kiosk tarjimalaridan eksport,
/// <c>web/scripts/export-kiosk-texts.mjs</c>); PDF matnlari — <see cref="QuestionnairePdfTexts"/>;
/// ro'yxat qiymatlari — javob saqlaydigan sanoqlar (<see cref="FormHousingType"/> va boshq.).
/// </para>
/// <para>
/// <b>Barqarorlik:</b> bir xil manbadan bir xil JSON (va <see cref="Hash"/>) chiqadi — katalog
/// faqat o'zgarganda qayta yuboriladi.
/// </para>
/// </remarks>
public sealed partial class HrF02TextCatalog
{
    public const string Title = "Nomzod anketasi (HR-F02)";

    public const string HousingList = "housing";
    public const string MaritalList = "marital";
    public const string MilitaryList = "military";
    public const string DriverLicenseList = "driverLicense";
    public const string EducationList = "education";
    public const string StartAfterList = "startAfter";
    public const string RelationList = "relation";
    public const string LanguageList = "language";
    public const string ComputerList = "computer";

    /// <summary>Kompyuter ro'yxatining guruhlari — <see cref="QuestionnaireFormCatalog.ComputerGroups"/> tartibida.</summary>
    public static readonly IReadOnlyList<string> ComputerGroupIds = ["os", "office", "special"];

    /// <summary>HR qo'shgan variantning qiymati — HRMentor yasaydi: <c>x-&lt;slug&gt;</c>.</summary>
    public const string AddedPrefix = "x-";

    /// <summary>Kompyuter ro'yxatida ko'pi bilan nechta dastur (HR qo'shganlari bilan).</summary>
    public const int MaxComputerOptions = 40;

    /// <summary>Variant va guruh nomining eng katta uzunligi (HRMentor <c>MAX_LABEL_LENGTH</c>).</summary>
    public const int MaxLabelLength = 200;

    /// <summary>HR ko'radigan nomning uzunligi (qisqartiriladi).</summary>
    private const int MaxNameLength = 120;

    private const string Resource = "Bonvi.Application.Recruiting.Forms.Texts.kiosk-texts.json";

    private static readonly Lazy<HrF02TextCatalog> Shared = new(() => Build(ReadResource()));

    private readonly Dictionary<string, CatalogTextItem> _items;
    private readonly Dictionary<string, CatalogListDefinition> _lists;

    private HrF02TextCatalog(
        Dictionary<string, CatalogTextItem> items,
        Dictionary<string, CatalogListDefinition> lists,
        string json)
    {
        _items = items;
        _lists = lists;
        Json = json;
        Hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    /// <summary>Ilovadagi katalog.</summary>
    public static HrF02TextCatalog Current => Shared.Value;

    /// <summary>HRMentor ga yuboriladigan JSON (<c>PUT …/catalog</c>).</summary>
    public string Json { get; }

    /// <summary><see cref="Json"/> ning SHA-256 i (kichik harf hex).</summary>
    public string Hash { get; }

    /// <summary>Matnlar — kalit bo'yicha.</summary>
    public IReadOnlyDictionary<string, CatalogTextItem> Items => _items;

    /// <summary>Ro'yxatlar — id bo'yicha.</summary>
    public IReadOnlyDictionary<string, CatalogListDefinition> Lists => _lists;

    /// <summary>Ro'yxatning katalogdagi variantlari.</summary>
    public IReadOnlyList<FormTextOption> DefaultOptions(string listId) => _lists[listId].Options;

    /// <summary>Guruhning katalogdagi nomi; noma'lum — <c>null</c>.</summary>
    public string? DefaultGroupLabel(string listId, string groupId, string language)
        => _lists.TryGetValue(listId, out var list)
            ? list.Groups.FirstOrDefault(group => group.Id == groupId)?.Default.GetValueOrDefault(language)
            : null;

    // ════════════════════════════════════════════════ yig'ish

    /// <summary>Katalogni <c>kiosk-texts.json</c> mazmunidan yig'adi.</summary>
    /// <exception cref="InvalidOperationException">Eksport to'liq emas (dasturchi xatosi).</exception>
    public static HrF02TextCatalog Build(string kioskTextsJson)
    {
        var source = JsonNode.Parse(kioskTextsJson)?.AsObject()
            ?? throw new InvalidOperationException("kiosk-texts.json bo'sh");

        var texts = FormTexts.Languages.ToDictionary(
            language => language,
            language => source["texts"]?[language]?.AsObject().ToDictionary(
                pair => pair.Key,
                pair => pair.Value!.GetValue<string>(),
                StringComparer.Ordinal)
                ?? throw new InvalidOperationException($"kiosk-texts.json: {language} yo'q"),
            StringComparer.Ordinal);

        string Text(string language, string key)
            => texts[language].TryGetValue(key, out var text) && !string.IsNullOrWhiteSpace(text)
                ? text
                : throw new InvalidOperationException($"kiosk-texts.json: {language} {key} yo'q");

        Dictionary<string, string> AllLanguages(string key)
            => FormTexts.Languages.ToDictionary(language => language, language => Text(language, key), StringComparer.Ordinal);

        var items = new Dictionary<string, CatalogTextItem>(StringComparer.Ordinal);
        var groups = new JsonArray();

        foreach (var group in source["groups"]!.AsArray())
        {
            var array = new JsonArray();
            foreach (var keyNode in group!["keys"]!.AsArray())
            {
                var key = keyNode!.GetValue<string>();
                var item = Item(key, isBlank: false, AllLanguages(key));
                items.Add(key, item);
                array.Add(ItemJson(item));
            }

            groups.Add(new JsonObject
            {
                ["id"] = group["id"]!.GetValue<string>(),
                ["title"] = group["title"]!.GetValue<string>(),
                ["items"] = array,
            });
        }

        foreach (var group in QuestionnairePdfTexts.Groups)
        {
            var array = new JsonArray();
            foreach (var text in group.Items)
            {
                var item = Item(text.Key, isBlank: true, new Dictionary<string, string>(StringComparer.Ordinal) { [FormTexts.Latin] = text.Default });
                items.Add(text.Key, item);
                array.Add(ItemJson(item));
            }

            groups.Add(new JsonObject { ["id"] = group.Id, ["title"] = group.Title, ["items"] = array });
        }

        FormTextOption Option(string value, string labelKey, string? group = null)
            => new(value, group, AllLanguages(labelKey), Hidden: false);

        FormTextOption[] Enum(string prefix, params string[] values)
            => [.. values.Select(value => Option(value, $"{prefix}.{LowerFirst(value)}"))];

        CatalogListDefinition Closed(string id, string title, IReadOnlyList<FormTextOption> options, int minVisible = 1)
            => new(id, title, Open: false, minVisible, options.Count, [], options);

        var computerGroups = ComputerGroupIds
            .Select(id => new CatalogListGroup(id, AllLanguages($"kiosk.computer.group.{id}")))
            .ToList();
        var computerOptions = QuestionnaireFormCatalog.ComputerGroups
            .SelectMany((group, index) => group.Items.Select(item =>
                Option(item.Key, $"kiosk.computer.skill.{item.Key}", ComputerGroupIds[index])))
            .ToList();

        CatalogListDefinition[] lists =
        [
            Closed(HousingList, "Uy turi", Enum("kiosk.housing", nameof(FormHousingType.Apartment), nameof(FormHousingType.House))),
            Closed(MaritalList, "Oilaviy holat", Enum(
                "kiosk.marital",
                nameof(MaritalStatus.Married),
                nameof(MaritalStatus.Unmarried),
                nameof(MaritalStatus.Divorced),
                nameof(MaritalStatus.Widowed))),
            Closed(MilitaryList, "Harbiy xizmat", Enum(
                "kiosk.military",
                nameof(FormMilitaryService.Served),
                nameof(FormMilitaryService.Reserve),
                nameof(FormMilitaryService.Deferred),
                nameof(FormMilitaryService.NotServed))),
            Closed(DriverLicenseList, "Haydovchilik guvohnomasi toifalari", [
                .. QuestionnaireFormCatalog.DriverLicenseCategories.Select(category => new FormTextOption(
                    category,
                    null,
                    FormTexts.Languages.ToDictionary(language => language, _ => category, StringComparer.Ordinal),
                    Hidden: false)),
            ]),
            Closed(EducationList, "Ma’lumot darajasi", Enum(
                "kiosk.education",
                nameof(EducationLevel.SecondarySchool),
                nameof(EducationLevel.VocationalSchool),
                nameof(EducationLevel.HigherIncomplete),
                nameof(EducationLevel.HigherComplete),
                nameof(EducationLevel.HigherMaster))),
            Closed(StartAfterList, "Ishga chiqish muddati", Enum(
                "kiosk.startAfter",
                nameof(FormStartAfter.OnHireDay),
                nameof(FormStartAfter.OneWeek),
                nameof(FormStartAfter.TenDays),
                nameof(FormStartAfter.FifteenDays))),
            Closed(RelationList, "Qarindoshlik", Enum(
                "kiosk.relation",
                nameof(FormFamilyRelation.Father),
                nameof(FormFamilyRelation.Mother),
                nameof(FormFamilyRelation.Spouse),
                nameof(FormFamilyRelation.Brother),
                nameof(FormFamilyRelation.Sister),
                nameof(FormFamilyRelation.Child),
                nameof(FormFamilyRelation.Other))),

            // Rus tili qatori majburiy (SPEC-planshet-anketa) — hech biri yashirilmaydi, faqat nom va tartib
            Closed(LanguageList, "Tillar", Enum(
                "kiosk.language",
                nameof(FormLanguage.Uzbek),
                nameof(FormLanguage.Russian),
                nameof(FormLanguage.English),
                nameof(FormLanguage.Chinese),
                nameof(FormLanguage.Other)), minVisible: 5),

            new(ComputerList, "Kompyuter dasturlari", Open: true, MinVisible: 0, MaxComputerOptions, computerGroups, computerOptions),
        ];

        var listArray = new JsonArray();
        foreach (var list in lists)
        {
            listArray.Add(ListJsonNode(list));
        }

        var catalog = new JsonObject
        {
            ["schema"] = 1,
            ["code"] = CandidateForm.HrF02,
            ["title"] = Title,
            ["groups"] = groups,
            ["lists"] = listArray,
        };

        return new HrF02TextCatalog(
            items,
            lists.ToDictionary(list => list.Id, StringComparer.Ordinal),
            catalog.ToJsonString(FormTexts.Json));
    }

    private static CatalogTextItem Item(string key, bool isBlank, Dictionary<string, string> defaults)
    {
        var longest = defaults.Values.Max(text => text.Length);
        var latin = Spaces().Replace(defaults[FormTexts.Latin], " ").Trim();
        var name = latin.Length <= MaxNameLength ? latin : string.Concat(latin.AsSpan(0, MaxNameLength - 1), "…");

        return new CatalogTextItem(
            key,
            isBlank,
            name,
            Multiline: longest > 100,
            MaxLength: Math.Clamp(longest * 2, 60, 2000),
            defaults);
    }

    private static JsonObject ItemJson(CatalogTextItem item)
    {
        var defaults = new JsonObject();
        foreach (var (language, text) in item.Default)
        {
            defaults[language] = text;
        }

        return new JsonObject
        {
            ["key"] = item.Key,
            ["target"] = item.IsBlank ? "blank" : "form",
            ["name"] = item.Name,
            ["multiline"] = item.Multiline,
            ["max_length"] = item.MaxLength,
            ["default"] = defaults,
        };
    }

    private static JsonObject ListJsonNode(CatalogListDefinition list)
    {
        static JsonObject Labels(IReadOnlyDictionary<string, string> labels)
        {
            var node = new JsonObject();
            foreach (var language in FormTexts.Languages)
            {
                node[language] = labels[language];
            }

            return node;
        }

        var groups = new JsonArray();
        foreach (var group in list.Groups)
        {
            groups.Add(new JsonObject { ["id"] = group.Id, ["default"] = Labels(group.Default) });
        }

        var options = new JsonArray();
        foreach (var option in list.Options)
        {
            options.Add(new JsonObject
            {
                ["value"] = option.Value,
                ["group"] = option.Group,
                ["default"] = Labels(option.Label),
            });
        }

        return new JsonObject
        {
            ["id"] = list.Id,
            ["title"] = list.Title,
            ["open"] = list.Open,
            ["min_visible"] = list.MinVisible,
            ["max_options"] = list.MaxOptions,
            ["groups"] = groups,
            ["options"] = options,
        };
    }

    private static string ReadResource()
    {
        using var stream = typeof(HrF02TextCatalog).Assembly.GetManifestResourceStream(Resource)
            ?? throw new InvalidOperationException($"{Resource} — ichki resurs yo'q (Bonvi.Application.csproj)");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string LowerFirst(string value)
        => string.Concat(char.ToLowerInvariant(value[0]).ToString(CultureInfo.InvariantCulture), value.AsSpan(1));

    // ════════════════════════════════════════════════ HR javobini tekshirish

    /// <summary>
    /// HRMentor <c>GET …/texts</c> javobini katalog bo'yicha tekshiradi va saqlanadigan shaklga
    /// keltiradi (SPEC-blankalar-hrmentor §3.2).
    /// </summary>
    /// <remarks>
    /// <para>Javob <b>shakli</b> buzuq (reviziya yo'q, obyekt emas) — <see cref="HrMentorErrors.TextsInvalid"/>,
    /// hech narsa saqlanmaydi.</para>
    /// <para>Alohida <b>matn</b> yaroqsiz — u tashlanadi, qolgani qabul qilinadi: noma'lum kalit;
    /// bo'sh; katalog chegarasidan uzun; o'rin belgilari (<c>{{nom}}</c>) asl matnnikidan farqli
    /// (i18next yoki PDF qiymatni qo'ya olmasdi). Qator ko'chirish bo'shliqqa aylanadi; lotin
    /// matnda tutuq qog'ozdagidek (<see cref="UzbekTypography"/>).</para>
    /// <para><b>Ro'yxat:</b> faqat katalog qiymatlari (ochiq ro'yxatda HR qo'shgan <c>x-…</c> ham);
    /// yopiq ro'yxatda HR javobida yo'q qiymat oxiriga qo'shiladi; ko'rinadiganlar
    /// <c>min_visible</c> dan kam bo'lsa — ro'yxat o'zgarishi butunlay tashlanadi.</para>
    /// </remarks>
    public Result<ShapedTexts> Shape(string servedJson)
    {
        ArgumentNullException.ThrowIfNull(servedJson);

        JsonObject root;
        try
        {
            root = JsonNode.Parse(servedJson) as JsonObject ?? throw new JsonException();
        }
        catch (JsonException)
        {
            return HrMentorErrors.TextsInvalid;
        }

        if (root["revision"] is not JsonValue revisionNode
            || !revisionNode.TryGetValue<int>(out var revision)
            || revision < 1
            || (root["code"] is { } code && code.GetValueKind() == JsonValueKind.String && code.GetValue<string>() != CandidateForm.HrF02))
        {
            return HrMentorErrors.TextsInvalid;
        }

        var strings = FormTexts.Languages.ToDictionary(language => language, _ => new Dictionary<string, string>(StringComparer.Ordinal), StringComparer.Ordinal);
        var blank = new Dictionary<string, string>(StringComparer.Ordinal);

        if (root["texts"] is JsonObject texts)
        {
            foreach (var (key, node) in texts)
            {
                if (!_items.TryGetValue(key, out var item) || node is not JsonObject values)
                {
                    continue;
                }

                foreach (var (language, fallback) in item.Default)
                {
                    if (Clean(values[language], item.MaxLength, language) is { } text
                        && FormTexts.PlaceholdersOf(text).SetEquals(FormTexts.PlaceholdersOf(fallback)))
                    {
                        (item.IsBlank ? blank : strings[language])[key] = text;
                    }
                }
            }
        }

        var lists = new Dictionary<string, ListJson>(StringComparer.Ordinal);
        if (root["lists"] is JsonObject served)
        {
            foreach (var (id, node) in served)
            {
                if (_lists.TryGetValue(id, out var definition)
                    && node is JsonObject list
                    && ShapeList(definition, list) is { } shaped)
                {
                    lists[id] = shaped;
                }
            }
        }

        var payload = new PayloadJson
        {
            Revision = revision,
            Strings = strings.Where(pair => pair.Value.Count > 0).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            Blank = blank,
            Lists = lists,
        };

        return new ShapedTexts(revision, JsonSerializer.Serialize(payload, FormTexts.Json));
    }

    private static ListJson? ShapeList(CatalogListDefinition definition, JsonObject served)
    {
        var groups = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var group in definition.Groups)
        {
            var labels = served["groups"]?[group.Id] as JsonObject;
            groups[group.Id] = FormTexts.Languages.ToDictionary(
                language => language,
                language => Clean(labels?[language], MaxLabelLength, language) ?? group.Default[language],
                StringComparer.Ordinal);
        }

        var options = new List<OptionJson>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in served["options"] as JsonArray ?? [])
        {
            if (node is not JsonObject option
                || Clean(option["value"], 80, language: null) is not { } value
                || !seen.Add(value))
            {
                continue;
            }

            var known = definition.Options.FirstOrDefault(candidate => candidate.Value == value);
            if (known is null && !(definition.Open && value.StartsWith(AddedPrefix, StringComparison.Ordinal) && OptionValue().IsMatch(value)))
            {
                continue;
            }

            var label = option["label"] as JsonObject;
            var latin = Clean(label?[FormTexts.Latin], MaxLabelLength, FormTexts.Latin);
            if (known is null && latin is null)
            {
                continue;
            }

            var labels = FormTexts.Languages.ToDictionary(
                language => language,
                language => Clean(label?[language], MaxLabelLength, language) ?? known?.Label[language] ?? latin!,
                StringComparer.Ordinal);

            string? group = null;
            if (definition.Groups.Count > 0)
            {
                var sent = Clean(option["group"], 60, language: null);
                group = definition.Groups.Any(candidate => candidate.Id == sent)
                    ? sent
                    : known?.Group ?? definition.Groups[0].Id;
            }

            var hidden = option["hidden"] is JsonValue flag && flag.TryGetValue<bool>(out var isHidden) && isHidden;
            options.Add(new OptionJson { Value = value, Group = group, Label = labels, Hidden = hidden });
        }

        // Yopiq ro'yxat: HR javobida yo'q qiymat — katalogdagidek oxirida. Ochiq ro'yxatda esa
        // yo'qligi — HR uni olib tashlagan.
        if (!definition.Open)
        {
            options.AddRange(definition.Options
                .Where(option => !seen.Contains(option.Value))
                .Select(option => new OptionJson
                {
                    Value = option.Value,
                    Group = option.Group,
                    Label = new Dictionary<string, string>(option.Label, StringComparer.Ordinal),
                }));
        }

        if (options.Count > definition.MaxOptions || options.Count(option => !option.Hidden) < definition.MinVisible)
        {
            return null;
        }

        return new ListJson { Groups = groups.Count > 0 ? groups : null, Options = options };
    }

    /// <summary>Satr: qator ko'chirishsiz, bo'sh emas, chegaradan uzun emas; lotinda tutuq qog'ozdagidek.</summary>
    private static string? Clean(JsonNode? node, int maxLength, string? language)
    {
        if (node is not JsonValue value || !value.TryGetValue<string>(out var raw))
        {
            return null;
        }

        // Ichki bo'shliqlar tegilmaydi: PDF matnlarida qo'sh bo'shliq ataylab («yozing:  0 – …»)
        var text = raw.Replace("\r\n", " ", StringComparison.Ordinal).Replace('\n', ' ').Replace('\r', ' ').Trim();
        if (text.Length == 0 || text.Length > maxLength)
        {
            return null;
        }

        return language == FormTexts.Latin ? UzbekTypography.Apply(text) : text;
    }

    [GeneratedRegex(@"[ \t]{2,}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Spaces();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_.-]{0,79}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex OptionValue();
}
