using System.Globalization;
using System.Text.RegularExpressions;

namespace Bonvi.Application.Blanks.Definitions;

/// <summary>
/// Blanka shablonining ta'rifi — <c>blank_template_versions.definition</c> JSON'ining o'qilgan shakli (SPEC-blankalar §4).
/// </summary>
/// <remarks>
/// <para>
/// Ta'rif o'zgarmas: e'lon qilingan versiya hech qachon tahrirlanmaydi, shuning uchun model faqat
/// o'qish uchun. Yaratish yagona yo'l — <see cref="BlankDefinitionValidator.Validate"/>: tekshiruvdan
/// o'tmagan ta'rif qo'lga tushmaydi.
/// </para>
/// <para>
/// Domain entitilariga BOG'LANMAYDI — dvigatel oddiy turlarda ishlaydi, saqlash qatlami esa JSON
/// matnni beradi va qaytarib oladi.
/// </para>
/// </remarks>
public sealed class BlankDefinition
{
    private readonly Dictionary<string, BlankField> fieldsById;
    private readonly Dictionary<string, BlankSection> sectionsByFieldId;

    internal BlankDefinition(
        string code,
        string version,
        string title,
        string subject,
        IReadOnlyList<BlankSection> sections,
        IReadOnlyList<BlankStep> steps)
    {
        Code = code;
        Version = version;
        Title = title;
        Subject = subject;
        Sections = sections;
        Steps = steps;

        Fields = [.. sections.SelectMany(section => section.Fields)];
        fieldsById = new Dictionary<string, BlankField>(StringComparer.Ordinal);
        sectionsByFieldId = new Dictionary<string, BlankSection>(StringComparer.Ordinal);

        foreach (var section in sections)
        {
            foreach (var field in section.Fields)
            {
                fieldsById.TryAdd(field.Id, field);
                sectionsByFieldId.TryAdd(field.Id, section);
            }
        }
    }

    /// <summary>Har doim 1.</summary>
    public int Schema { get; init; } = 1;

    public string Code { get; }

    public string Version { get; }

    public string Title { get; }

    /// <summary><c>employee</c> | <c>candidate</c> | <c>none</c>.</summary>
    public string Subject { get; }

    /// <summary><c>final</c> | <c>draft</c> (draft → PDF'da «Loyiha» yozuvi).</summary>
    public string LegalStatus { get; init; } = "final";

    public string? Organization { get; init; }

    public string? Retention { get; init; }

    /// <summary>Arizaning <c>due_date</c> i olinadigan sana maydoni.</summary>
    public string? DueDateField { get; init; }

    public IReadOnlyList<BlankSection> Sections { get; }

    public IReadOnlyList<BlankStep> Steps { get; }

    /// <summary>Bo'limlardagi YUQORI darajadagi maydonlar, hujjat tartibida (jadval ustunlari kirmaydi).</summary>
    public IReadOnlyList<BlankField> Fields { get; }

    /// <summary>Yuqori darajadagi maydon (jadval ustuni emas) yoki <c>null</c>.</summary>
    public BlankField? FindField(string id) => fieldsById.GetValueOrDefault(id);

    /// <summary>Maydon joylashgan bo'lim yoki <c>null</c>.</summary>
    public BlankSection? SectionOf(string fieldId) => sectionsByFieldId.GetValueOrDefault(fieldId);

    public BlankSection? FindSection(string id) => Sections.FirstOrDefault(section => section.Id == id);

    public BlankStep? FindStep(string id) => Steps.FirstOrDefault(step => step.Id == id);

    /// <summary>Qaralama bo'limlari — yaratuvchi topshirishdan oldin to'ldiradi (§4.2).</summary>
    public IReadOnlySet<string> DraftSectionIds()
        => Sections.Where(section => section.Fill.At == BlankFillAt.Draft).Select(section => section.Id).ToHashSet(StringComparer.Ordinal);

    /// <summary>Shu qadam imzolovchisi to'ldiradigan bo'limlar.</summary>
    public IReadOnlySet<string> StepSectionIds(string stepId)
        => Sections.Where(section => section.Fill.At == BlankFillAt.Step && section.Fill.Step == stepId)
            .Select(section => section.Id).ToHashSet(StringComparer.Ordinal);

    /// <summary>«open» bo'limlar — tasdiqlangunga qadar istalgan payt to'ldiriladi.</summary>
    public IReadOnlySet<string> OpenSectionIds()
        => Sections.Where(section => section.Fill.At == BlankFillAt.Open).Select(section => section.Id).ToHashSet(StringComparer.Ordinal);
}

/// <summary>Bo'limni kim va qachon to'ldiradi (§4.2).</summary>
public enum BlankFillAt
{
    Draft = 0,
    Step = 1,
    Open = 2,
}

/// <summary><c>fill</c>: <c>at</c> = draft | step | open; <c>step</c> faqat <c>at = step</c> da.</summary>
public sealed record BlankFill(BlankFillAt At, string? Step)
{
    public static BlankFill Draft { get; } = new(BlankFillAt.Draft, null);
}

/// <summary>Blanka bo'limi (§4.2).</summary>
public sealed record BlankSection(
    string Id,
    string Title,
    string? Hint,
    BlankCondition? When,
    BlankFill Fill,
    IReadOnlyList<BlankField> Fields);

/// <summary>Maydon turi (§4.3).</summary>
public enum BlankFieldType
{
    Text = 0,
    LongText,
    WholeNumber,
    Percent,
    Money,
    Date,
    DateTime,
    Month,
    Year,
    DateRange,
    YesNo,
    Choice,
    Multi,
    Check,
    Score,
    Ref,
    DocRef,
    Order,
    Static,
    File,
    Table,
    Computed,
}

/// <summary>
/// Maydon (§4.3). Bitta sinf hamma tur uchun: turga xos kalitlar boshqa turda <c>null</c> / bo'sh —
/// o'qish paytida ruxsat etilmagan kalit rad etiladi, ya'ni bu yerga tushmaydi.
/// </summary>
public sealed class BlankField
{
    public required string Id { get; init; }

    public required BlankFieldType Type { get; init; }

    public string? Label { get; init; }

    public string? Hint { get; init; }

    public bool Required { get; init; }

    public BlankCondition? When { get; init; }

    public bool ReadOnly { get; init; }

    /// <summary>§4.6 kaliti (<c>subject.hireDate</c>).</summary>
    public string? Prefill { get; init; }

    public decimal? PdfWidth { get; init; }

    /// <summary>text / longtext.</summary>
    public int? MaxLength { get; init; }

    /// <summary>int / money / year (son) yoki date / datetime / month (<c>today</c>, <c>today+N</c>, sana).</summary>
    public BlankBound? Min { get; init; }

    public BlankBound? Max { get; init; }

    /// <summary>percent: o'tish chegarasi (masalan 70) — faqat ko'rsatish uchun.</summary>
    public decimal? PassAt { get; init; }

    /// <summary>choice / multi.</summary>
    public IReadOnlyList<BlankOption> Options { get; init; } = [];

    /// <summary>multi.</summary>
    public int? MaxSelected { get; init; }

    /// <summary>ref: employee | candidate | department | position | direction.</summary>
    public string? RefKind { get; init; }

    /// <summary>docRef: qaysi shablonlarning arizalariga havola mumkin.</summary>
    public IReadOnlyList<string> TemplateCodes { get; init; } = [];

    /// <summary>static: matn (<c>{{fieldId}}</c> o'rinbosarlari bilan).</summary>
    public string? Text { get; init; }

    /// <summary>table: ustunlar.</summary>
    public IReadOnlyList<BlankField> Columns { get; init; } = [];

    public int? MinRows { get; init; }

    public int? MaxRows { get; init; }

    public IReadOnlyList<BlankFixedRow> FixedRows { get; init; } = [];

    /// <summary>computed: number | date | bool | text.</summary>
    public string? ValueType { get; init; }

    public BlankExpr? Expr { get; init; }

    /// <summary>file: pdf | image | office | any (§8).</summary>
    public IReadOnlyList<string> Accept { get; init; } = [];

    public int? MaxFiles { get; init; }

    public int? MaxSizeMb { get; init; }

    /// <summary>file: environment | user | null.</summary>
    public string? Capture { get; init; }

    /// <summary>Matn chegarasi: berilmagan bo'lsa text 200, longtext 2000.</summary>
    public int EffectiveMaxLength => MaxLength ?? (Type == BlankFieldType.LongText ? 2000 : 200);

    /// <summary>Fayllar soni chegarasi (sukut 5).</summary>
    public int EffectiveMaxFiles => MaxFiles ?? 5;

    /// <summary>Jadval qatorlari chegarasi (sukut va eng ko'pi 200).</summary>
    public int EffectiveMaxRows => FixedRows.Count > 0 ? FixedRows.Count : MaxRows ?? 200;

    /// <summary>Ustun (faqat jadvalda) yoki <c>null</c>.</summary>
    public BlankField? FindColumn(string id) => Columns.FirstOrDefault(column => column.Id == id);
}

/// <summary>
/// min / max chegarasi: yo son (<see cref="Number"/>), yo sana ifodasi (<see cref="DateExpression"/>:
/// <c>today</c>, <c>today+N</c>, <c>today-N</c> yoki <c>yyyy-MM-dd</c>).
/// </summary>
public sealed partial record BlankBound(decimal? Number, string? DateExpression)
{
    /// <summary>Sana ifodasini bugungi kunga nisbatan hisoblaydi; noto'g'ri ifoda → <c>null</c>.</summary>
    public DateOnly? ResolveDate(DateOnly today)
    {
        if (DateExpression is null)
        {
            return null;
        }

        if (DateOnly.TryParseExact(DateExpression, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var literal))
        {
            return literal;
        }

        var match = TodayPattern().Match(DateExpression);

        if (!match.Success)
        {
            return null;
        }

        if (!match.Groups[1].Success)
        {
            return today;
        }

        var days = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        return today.AddDays(match.Groups[1].Value == "-" ? -days : days);
    }

    [GeneratedRegex("^today(?:([+-])([0-9]{1,5}))?$", RegexOptions.CultureInvariant)]
    private static partial Regex TodayPattern();
}

/// <summary>choice / multi varianti (§4.3).</summary>
public sealed record BlankOption(string Value, string Label, BlankOptionExtra? Extra);

/// <summary>Variant tanlanganda so'raladigan qo'shimcha qiymat («sabab: ___»).</summary>
public sealed record BlankOptionExtra(BlankFieldType Type, string Label, bool Required, string? RefKind, IReadOnlyList<string> TemplateCodes)
{
    /// <summary>Qo'shimcha qiymatni oddiy maydon sifatida tekshirish uchun.</summary>
    internal BlankField AsField(string id) => new()
    {
        Id = id,
        Type = Type,
        Label = Label,
        Required = Required,
        RefKind = RefKind,
        TemplateCodes = TemplateCodes,
    };
}

/// <summary>Qatʼiy qatorli jadvalning qatori (§4.3).</summary>
public sealed record BlankFixedRow(string Id, string Label, IReadOnlyDictionary<string, BlankFixedCell> Cells);

/// <summary>Qatorning bosma (tahrirlanmaydigan) katagi.</summary>
public sealed record BlankFixedCell(bool ReadOnly, string? Text);

/// <summary>Imzolash usuli (§4.7).</summary>
public enum BlankStepMode
{
    Electronic = 0,
    InPerson = 1,
    Either = 2,
}

/// <summary>Qadam qarori: variantlar va majburiyligi.</summary>
public sealed record BlankDecision(IReadOnlyList<BlankOption> Options, bool Required);

/// <summary>Qadam muddati: <c>field</c> sanasi + <c>days</c>.</summary>
public sealed record BlankDue(string Field, int Days);

/// <summary>Imzolash qadami (§4.7).</summary>
public sealed record BlankStep(
    string Id,
    string Title,
    int Order,
    string Actor,
    BlankStepMode Mode,
    BlankCondition? When,
    BlankDecision? Decision,
    bool CanReject,
    bool CanReturn,
    BlankDue? Due)
{
    /// <summary><c>field:mentor</c> → <c>mentor</c>; boshqa rolda <c>null</c>.</summary>
    public string? ActorField => Actor.StartsWith(BlankActors.FieldPrefix, StringComparison.Ordinal)
        ? Actor[BlankActors.FieldPrefix.Length..]
        : null;
}

/// <summary>Qadam ijrochilari (§6.1).</summary>
public static class BlankActors
{
    public const string FieldPrefix = "field:";

    /// <summary>Foydalanuvchilarga biriktiriladigan lavozim rollari.</summary>
    public static IReadOnlySet<string> OfficeRoles { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "hr1", "hr2", "hr3", "hr4", "hr5", "hr8", "payrollAccountant", "chiefAccountant", "director", "lawyer",
    };

    /// <summary>Har arizada alohida aniqlanadigan shaxs rollari (<c>field:</c> dan tashqari).</summary>
    public static IReadOnlySet<string> PersonRoles { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "subjectEmployee", "departmentHead", "witness", "candidate",
    };
}
