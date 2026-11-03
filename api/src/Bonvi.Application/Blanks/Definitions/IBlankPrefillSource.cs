using System.Globalization;
using System.Text.Json.Nodes;

namespace Bonvi.Application.Blanks.Definitions;

/// <summary>Oldindan to'ldirish manbalari (SPEC-blankalar §4.6). Boshqa kalit → ta'rif yaroqsiz.</summary>
public static class BlankPrefillKeys
{
    public const string SubjectFullName = "subject.fullName";
    public const string SubjectPositionName = "subject.positionName";
    public const string SubjectDepartmentName = "subject.departmentName";
    public const string SubjectDirectionName = "subject.directionName";
    public const string SubjectHireDate = "subject.hireDate";
    public const string SubjectTerminationDate = "subject.terminationDate";
    public const string SubjectProbationEndDate = "subject.probationEndDate";
    public const string SubjectDepartmentHeadId = "subject.departmentHeadId";
    public const string CurrentUserEmployeeId = "currentUser.employeeId";
    public const string Today = "today";
    public const string SubjectPriorF13Count12m = "subject.priorF13Count12m";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        SubjectFullName, SubjectPositionName, SubjectDepartmentName, SubjectDirectionName, SubjectHireDate,
        SubjectTerminationDate, SubjectProbationEndDate, SubjectDepartmentHeadId, CurrentUserEmployeeId, Today,
        SubjectPriorF13Count12m,
    };
}

/// <summary>Qoralama yaratilayotgan arizaning konteksti — manba shu bo'yicha ma'lumot topadi.</summary>
/// <param name="SubjectKind"><c>employee</c> | <c>candidate</c> | <c>none</c>.</param>
/// <param name="SubjectEmployeeId">Subyekt xodim bo'lsa.</param>
/// <param name="SubjectCandidateId">Subyekt nomzod bo'lsa.</param>
/// <param name="CurrentUserEmployeeId">Yaratayotgan foydalanuvchining <c>users.employee_id</c> si.</param>
/// <param name="Today">Toshkent bo'yicha bugun.</param>
public sealed record BlankPrefillContext(
    string SubjectKind,
    Guid? SubjectEmployeeId,
    Guid? SubjectCandidateId,
    Guid? CurrentUserEmployeeId,
    DateOnly Today);

/// <summary>
/// Manbadan kelgan qiymat — maydon turiga <see cref="BlankPrefill.Apply"/> moslaydi.
/// </summary>
/// <remarks>
/// Shaxsga oid kalitlar ham <see cref="Id"/>, ham <see cref="Text"/> beradi: <c>subject.fullName</c>
/// ref maydonga <c>{id, label}</c>, matn maydoniga faqat ism bo'lib tushadi (§4.9 namunasi).
/// </remarks>
public sealed record BlankPrefillValue(string? Text = null, Guid? Id = null, DateOnly? Date = null, long? Number = null);

/// <summary>
/// Oldindan to'ldirish PORTI (§4.6) — tizim ma'lumotidan (xodim kartasi, bo'lim boshlig'i, HR-F13
/// tarixi) qiymat oladi. Faqat qoralama yaratilganda bir marta chaqiriladi; amalga oshirish —
/// Infrastructure'da (S5).
/// </summary>
public interface IBlankPrefillSource
{
    /// <summary>So'ralgan kalitlarni hisoblaydi; ma'lumot yo'q kalit natijaga kirmaydi (maydon bo'sh qoladi).</summary>
    Task<IReadOnlyDictionary<string, BlankPrefillValue>> ResolveAsync(
        BlankPrefillContext context,
        IReadOnlyCollection<string> keys,
        CancellationToken cancellationToken = default);
}

/// <summary>Ta'rifdagi prefill kalitlarini yig'ish va natijani boshlang'ich javoblarga aylantirish.</summary>
public static class BlankPrefill
{
    /// <summary>Ta'rif ishlatadigan prefill kalitlari (takrorsiz).</summary>
    public static IReadOnlyCollection<string> KeysOf(BlankDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return definition.Fields.Select(field => field.Prefill).OfType<string>().Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Boshlang'ich javoblar. Turga mos kelmagan qiymat (masalan, sana maydoniga matn) tashlanadi.
    /// Natijani chaqiruvchi baribir <see cref="BlankAnswersValidator"/> dan o'tkazadi.
    /// </summary>
    public static JsonObject Apply(BlankDefinition definition, IReadOnlyDictionary<string, BlankPrefillValue> values)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(values);

        var answers = new JsonObject();

        foreach (var field in definition.Fields)
        {
            if (field.Prefill is { } key && values.TryGetValue(key, out var value) && Convert(field, value) is { } node)
            {
                answers[field.Id] = node;
            }
        }

        return answers;
    }

    private static JsonNode? Convert(BlankField field, BlankPrefillValue value) => field.Type switch
    {
        BlankFieldType.Text or BlankFieldType.LongText =>
            value.Text is not null ? JsonValue.Create(value.Text)
            : value.Date is { } date ? JsonValue.Create(BlankValues.FormatDate(date))
            : value.Number is { } number ? JsonValue.Create(number.ToString(CultureInfo.InvariantCulture))
            : null,
        BlankFieldType.Ref when value.Id is { } id => new JsonObject { ["id"] = id.ToString(), ["label"] = value.Text },
        BlankFieldType.Date when value.Date is { } date => JsonValue.Create(BlankValues.FormatDate(date)),
        BlankFieldType.WholeNumber or BlankFieldType.Money or BlankFieldType.Year when value.Number is { } number => JsonValue.Create(number),
        _ => null,
    };
}
