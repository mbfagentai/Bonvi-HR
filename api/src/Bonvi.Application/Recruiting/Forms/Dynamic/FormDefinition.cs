using Bonvi.Application.Recruiting.Forms.Texts;

namespace Bonvi.Application.Recruiting.Forms.Dynamic;

/*
  ═══════════════════════════════════════════════════════════════════
   HRMentor DA YIG'ILGAN ANKETA — TA'RIF (SPEC-anketa-konstruktori,
   HRMentor SPEC §19.3, `schema: 2`)
   HR anketani HRMentor konstruktorida yig'adi va e'lon qiladi; bu yerga
   tekshirilgan, O'ZGARMAS nusxasi keladi (`form_definitions`). Bu fayldagi
   turlar — o'sha JSON ning o'qilgan ko'rinishi: forma, tekshiruvchi, PDF va
   nomzod kartasiga yozish shulardan foydalanadi.
   JSON ni faqat `FormDefinitionChecker` + `FormDefinitionReader` o'qiydi:
   tekshiruvdan o'tmagan ta'rif hech qachon shu turlarga aylanmaydi.
  ═══════════════════════════════════════════════════════════════════
*/

/// <summary>Maydon turi — JSON dagi <c>kind</c>.</summary>
public enum FieldKind
{
    ShortText,
    LongText,
    Number,
    Money,
    Date,
    Month,
    Year,
    Phone,
    Email,
    Telegram,
    YesNo,
    Choice,
    MultiChoice,
    Levels,
    Repeat,
    Period,
    YearRange,
    Consent,
    Info,
    Checklist,
    Vacancy,
    Photo,
    Signature,
    Referral,
}

/// <summary>Turlar to'plamlari va JSON nomlari (HRMentor <c>definition.py</c> bilan bir xil).</summary>
public static class FieldKinds
{
    private static readonly Dictionary<string, FieldKind> ByWire = new(StringComparer.Ordinal)
    {
        ["short_text"] = FieldKind.ShortText,
        ["long_text"] = FieldKind.LongText,
        ["number"] = FieldKind.Number,
        ["money"] = FieldKind.Money,
        ["date"] = FieldKind.Date,
        ["month"] = FieldKind.Month,
        ["year"] = FieldKind.Year,
        ["phone"] = FieldKind.Phone,
        ["email"] = FieldKind.Email,
        ["telegram"] = FieldKind.Telegram,
        ["yes_no"] = FieldKind.YesNo,
        ["choice"] = FieldKind.Choice,
        ["multi_choice"] = FieldKind.MultiChoice,
        ["levels"] = FieldKind.Levels,
        ["repeat"] = FieldKind.Repeat,
        ["period"] = FieldKind.Period,
        ["year_range"] = FieldKind.YearRange,
        ["consent"] = FieldKind.Consent,
        ["info"] = FieldKind.Info,
        ["checklist"] = FieldKind.Checklist,
        ["vacancy"] = FieldKind.Vacancy,
        ["photo"] = FieldKind.Photo,
        ["signature"] = FieldKind.Signature,
        ["referral"] = FieldKind.Referral,
    };

    private static readonly Dictionary<FieldKind, string> ToWireName = ByWire.ToDictionary(pair => pair.Value, pair => pair.Key);

    /// <summary>Tizim qismlari — har biri ko'pi bilan bir marta, nomzod ekranida.</summary>
    public static IReadOnlySet<FieldKind> System { get; } =
        new HashSet<FieldKind> { FieldKind.Vacancy, FieldKind.Photo, FieldKind.Signature, FieldKind.Referral };

    /// <summary>Faqat takrorlanuvchi jadval ustunida.</summary>
    public static IReadOnlySet<FieldKind> ColumnOnly { get; } = new HashSet<FieldKind> { FieldKind.Period, FieldKind.YearRange };

    /// <summary>Takrorlanuvchi jadval ustuni bo'la oladigan turlar.</summary>
    public static IReadOnlySet<FieldKind> Column { get; } = new HashSet<FieldKind>
    {
        FieldKind.ShortText,
        FieldKind.LongText,
        FieldKind.Number,
        FieldKind.Money,
        FieldKind.Date,
        FieldKind.Month,
        FieldKind.Year,
        FieldKind.Phone,
        FieldKind.YesNo,
        FieldKind.Choice,
        FieldKind.Period,
        FieldKind.YearRange,
    };

    /// <summary>Nomzod bo'limida bo'la oladigan turlar.</summary>
    public static IReadOnlySet<FieldKind> Candidate { get; } =
        Enum.GetValues<FieldKind>().Except(ColumnOnly).Except([FieldKind.Checklist]).ToHashSet();

    /// <summary>HR bo'limida (B qism) bo'la oladigan turlar.</summary>
    public static IReadOnlySet<FieldKind> Office { get; } =
        Enum.GetValues<FieldKind>().Except(ColumnOnly).Except(System).Except([FieldKind.Consent]).ToHashSet();

    /// <summary>Variantli turlar.</summary>
    public static IReadOnlySet<FieldKind> WithOptions { get; } =
        new HashSet<FieldKind> { FieldKind.Choice, FieldKind.MultiChoice, FieldKind.Levels };

    /// <summary>Bog'lana oladigan turlar.</summary>
    public static IReadOnlySet<FieldKind> Bindable { get; } =
        Enum.GetValues<FieldKind>().Except(ColumnOnly).Except([FieldKind.Info, FieldKind.Checklist]).ToHashSet();

    public static bool TryParse(string? wire, out FieldKind kind)
        => ByWire.TryGetValue(wire ?? string.Empty, out kind);

    public static string Wire(this FieldKind kind) => ToWireName[kind];

    /// <summary>Javob saqlamaydigan turlar (matn, ro'yxat) — javoblar ichida bo'lmaydi.</summary>
    public static bool HasAnswer(this FieldKind kind)
        => kind is not (FieldKind.Info or FieldKind.Checklist or FieldKind.Photo);
}

/// <summary>Uch tildagi matn. HRMentor beradigan ta'rifda har doim to'liq (§18.5 qoidasi).</summary>
public sealed record FormText(string Latin, string Cyrillic, string Russian)
{
    /// <summary>Tanlangan tildagi matn; bo'sh bo'lsa — lotin.</summary>
    public string In(string? language)
    {
        var text = language switch
        {
            FormTexts.Cyrillic => Cyrillic,
            FormTexts.Russian => Russian,
            _ => Latin,
        };

        return string.IsNullOrWhiteSpace(text) ? Latin : text;
    }
}

public sealed record FormOption(string Value, FormText Label, string? Group, string? PdfLabel)
{
    /// <summary>PDF dagi so'z — <c>pdf_label</c> yoki lotin nomi.</summary>
    public string Printed => PdfLabel ?? Label.Latin;
}

/// <summary>Variantlar guruhi yoki <c>levels</c> ning jihati (so'zlashuv, o'qish…).</summary>
public sealed record FormLabelled(string Id, FormText Label);

/// <summary><c>visible_if</c>: yes_no da <c>is</c>, choice / multi_choice da <c>in</c>.</summary>
public sealed record FormCondition(string Field, bool? Is, IReadOnlyList<string>? In);

/// <summary>«Manzilim shu bilan bir xil» — boshqa qisqa matnni ko'chiradigan belgi.</summary>
public sealed record FormSameAs(string Field, FormText Label);

/// <summary><c>levels</c> dagi «Boshqa til» qatorlari.</summary>
public sealed record FormLevelsOther(int Max, FormText Label);

/// <summary>B qism ro'yxatining bandi (<c>auto</c>: photo / signature — onlayn olinganda ☑).</summary>
public sealed record FormChecklistItem(string Label, string? Auto, string? Note);

/// <summary>Bitta maydon (yoki takrorlanuvchi jadvalning ustuni). Faqat turiga tegishli kalitlar to'ladi.</summary>
public sealed record FormField
{
    public const int DefaultShortLength = 200;
    public const int DefaultLongLength = 1000;

    public required string Id { get; init; }

    public required FieldKind Kind { get; init; }

    /// <summary><c>info</c> da bo'lmasligi mumkin.</summary>
    public FormText? Label { get; init; }

    public FormText? Hint { get; init; }

    public bool Required { get; init; }

    public string? Bind { get; init; }

    public FormCondition? VisibleIf { get; init; }

    public string? PdfLabel { get; init; }

    public bool PdfHidden { get; init; }

    // ── matn ──
    public int? MaxLength { get; init; }

    public int MinLetters { get; init; }

    public FormSameAs? SameAs { get; init; }

    // ── son, pul ──
    public long? Min { get; init; }

    public long? Max { get; init; }

    // ── sana, yil ──
    public int? MinAge { get; init; }

    public int? MaxAge { get; init; }

    public bool NotBeforeToday { get; init; }

    public int? MinYear { get; init; }

    public int? MaxYear { get; init; }

    /// <summary><c>"max_year": "today"</c>.</summary>
    public bool MaxYearToday { get; init; }

    // ── telefon ──
    /// <summary>Faqat O'zbekiston mobil raqami (sukut). <c>false</c> — shahar yoki xorijiy ham.</summary>
    public bool MobileOnly { get; init; } = true;

    // ── ha / yo'q ──
    public FormText? Yes { get; init; }

    public FormText? No { get; init; }

    // ── variantlar ──
    public IReadOnlyList<FormOption> Options { get; init; } = [];

    public IReadOnlyList<FormLabelled> Groups { get; init; } = [];

    public string? Style { get; init; }

    public int? MinSelected { get; init; }

    public int? MaxSelected { get; init; }

    // ── levels ──
    public IReadOnlyList<FormLabelled> Aspects { get; init; } = [];

    public int ScaleMin { get; init; }

    public int ScaleMax { get; init; }

    public IReadOnlyList<string> RequiredRows { get; init; } = [];

    public FormLevelsOther? Other { get; init; }

    /// <summary>PDF da javob bo'lmasa ham chiqadigan qatorlar.</summary>
    public IReadOnlyList<string> PrintedRows { get; init; } = [];

    // ── repeat ──
    public IReadOnlyList<FormField> Columns { get; init; } = [];

    public int MinRows { get; init; }

    public int? MaxRows { get; init; }

    public string? Summary { get; init; }

    /// <summary>PDF dagi eng kam qator soni (bo'sh qatorlar qog'oz uchun).</summary>
    public int PrintRowCount { get; init; }

    // ── period ──
    public bool AllowCurrent { get; init; }

    // ── consent, info ──
    public FormText? Text { get; init; }

    // ── checklist ──
    public IReadOnlyList<FormChecklistItem> Items { get; init; } = [];

    /// <summary>Qisqa / uzun matnning chegarasi (sukut bilan).</summary>
    public int TextLimit => MaxLength ?? (Kind == FieldKind.LongText ? DefaultLongLength : DefaultShortLength);

    /// <summary>PDF dagi savol — <c>pdf_label</c> yoki lotin nomi.</summary>
    public string Printed => PdfLabel ?? Label?.Latin ?? string.Empty;

    public FormOption? Option(string value) => Options.FirstOrDefault(option => option.Value == value);
}

public sealed record FormScreen(string Id, FormText? Title, FormText? Hint, IReadOnlyList<FormField> Fields);

public sealed record FormSection(
    string Id,
    bool Office,
    FormText Title,
    string? PdfTitle,
    FormText? Hint,
    IReadOnlyList<FormScreen> Screens)
{
    public IEnumerable<FormField> Fields => Screens.SelectMany(screen => screen.Fields);
}

/// <summary>
/// PDF ning o'z so'zlari (faqat lotin). <c>HeadDepartment</c> — sarlavhadagi «Lavozim» yonidagi katak
/// nomi (qog'oz HR-F02 v2.0 da «Yo'nalish»); bo'lmasa «Bo'lim».
/// </summary>
public sealed record FormPdf(
    string PrintedCode,
    string Title,
    string? Subtitle,
    string? Instruction,
    string? CandidatePart,
    string? OfficePart,
    string? Footer,
    string? HeadDepartment = null);

/// <summary>Tekshirilgan ta'rif.</summary>
public sealed class FormDefinition
{
    private readonly Dictionary<string, FormField> _fields;

    public FormDefinition(string code, string label, FormText title, FormPdf pdf, IReadOnlyList<FormSection> sections)
    {
        Code = code;
        Label = label;
        Title = title;
        Pdf = pdf;
        Sections = sections;
        _fields = sections
            .SelectMany(section => section.Fields)
            .ToDictionary(field => field.Id, StringComparer.Ordinal);
    }

    public string Code { get; }

    /// <summary>Bosma versiya («4.0»).</summary>
    public string Label { get; }

    public FormText Title { get; }

    public FormPdf Pdf { get; }

    public IReadOnlyList<FormSection> Sections { get; }

    /// <summary>Nomzod bo'limlarining yuqori darajadagi maydonlari — forma tartibida.</summary>
    public IEnumerable<FormField> CandidateFields => Sections.Where(section => !section.Office).SelectMany(section => section.Fields);

    public FormField? Field(string id) => _fields.GetValueOrDefault(id);

    /// <summary>Bog'langan maydon (birinchisi — tekshiruvchi takrorni rad etadi).</summary>
    public FormField? Bound(string binding) => CandidateFields.FirstOrDefault(field => field.Bind == binding);

    /// <summary>Tizim maydoni (vakansiya, rasm, imzo, «qayerdan bilgan»).</summary>
    public FormField? System(FieldKind kind) => CandidateFields.FirstOrDefault(field => field.Kind == kind);
}
