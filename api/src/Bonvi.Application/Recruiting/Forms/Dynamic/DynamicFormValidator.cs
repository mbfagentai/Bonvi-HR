using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Bonvi.Application.Recruiting.Common;
using Bonvi.Application.Recruiting.Referrals;
using Bonvi.Domain.Common;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.Recruiting.Forms.Dynamic;

/// <summary>
/// Tekshirilgan, TOZALANGAN dinamik anketa — <c>{ maydon: qiymat }</c> (HRMentor SPEC §19.3 shakllari).
/// </summary>
/// <remarks>
/// Bazaga ham, PDF ga ham, nomzod kartasiga ham faqat shu boradi: bo'sh qiymatlar, yashirin
/// maydonlarning javoblari (R5) va ta'rifda yo'q kalitlar tashlangan; telefon kanonik, tanlovlar
/// variantlar tartibida.
/// </remarks>
public sealed class DynamicFormAnswers
{
    public DynamicFormAnswers(FormDefinition definition, JsonObject values)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(values);

        Definition = definition;
        Values = values;
    }

    /// <summary>Anketa qaysi versiya bilan tekshirilgan.</summary>
    public FormDefinition Definition { get; }

    /// <summary>Javoblar — maydon identifikatori bo'yicha.</summary>
    public JsonObject Values { get; }

    /// <summary>Maydonning javobi; yo'q bo'lsa — <c>null</c>.</summary>
    public JsonNode? Value(string fieldId) => Values[fieldId];

    /// <summary>Bog'langan maydonning javobi (SPEC-anketa-konstruktori §3).</summary>
    public JsonNode? Bound(string binding) => Definition.Bound(binding) is { } field ? Values[field.Id] : null;

    public string? BoundText(string binding) => Text(Bound(binding));

    public long? BoundNumber(string binding) => Number(Bound(binding));

    public bool? BoundFlag(string binding) => Flag(Bound(binding));

    /// <summary>Bazaga yoziladigan matn.</summary>
    public string Write() => Values.ToJsonString();

    /// <summary>Bazadagi javoblarni o'qiydi (PDF ni qayta chizish uchun); buzilgan — <c>null</c>.</summary>
    public static DynamicFormAnswers? Read(FormDefinition definition, string? json)
        => FormDefinitionReader.Parse(json) is { } values ? new DynamicFormAnswers(definition, values) : null;

    public static string? Text(JsonNode? node)
        => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    /// <remarks>
    /// Kelgan JSON (JsonElement) da <c>long</c> o'qiladi; kodda yasalgan qiymat (<c>JsonValue&lt;int&gt;</c>)
    /// <c>long</c> ga o'zi o'tmaydi — shuning uchun <c>int</c> ham. Kasr son — <c>null</c>.
    /// </remarks>
    public static long? Number(JsonNode? node)
        => node is not JsonValue value ? null
            : value.TryGetValue<long>(out var number) ? number
            : value.TryGetValue<int>(out var small) ? small
            : null;

    public static bool? Flag(JsonNode? node)
        => node is JsonValue value && value.TryGetValue<bool>(out var flag) ? flag : null;
}

/// <summary>
/// Konstruktor anketasining SERVER tekshiruvi (SPEC-anketa-konstruktori §6.1).
/// </summary>
/// <remarks>
/// <para>
/// O'rnatilgan anketadagi <see cref="QuestionnaireFormValidator"/> ning qoidalari — endi maydon
/// turi va ta'rif kalitlaridan: majburiylik, uzunlik, oraliq, variantlar, yosh, oylar, davr,
/// darajalar jadvali, takrorlanuvchi qatorlar, telefon, belgilar, rozilik va imzo. Xato kodlari
/// o'sha (<c>anketa.*</c>), <c>detail</c> — maydon yo'li (<c>jobs[1].period</c>,
/// <c>languages.rows.Russian</c>); birinchi xato qaytadi.
/// </para>
/// <para>
/// ⭐ <b>Yashirin maydonning javobi tashlanadi</b> (R5): maydonlar forma tartibida yuriladi va
/// sharti TOZALANGAN javoblarga qarab hisoblanadi — tashlangan javobga bog'liq maydon ham yashirinadi.
/// </para>
/// <para>
/// <b>Bog'lanish qoidalari maydon nima desa ham</b>: tug'ilgan sana — yosh
/// <see cref="FormProfile.MinAge"/>–<see cref="FormProfile.MaxAge"/>, nomzod telefoni — faqat
/// O'zbekiston mobil raqami, imzodagi ism — bog'langan familiya va ism.
/// </para>
/// </remarks>
public static partial class DynamicFormValidator
{
    /// <summary>Takrorlanuvchi jadvalning eng ko'p qatori (shartnoma: <c>max_rows</c> ≤ 20).</summary>
    public const int MaxRepeatRows = 20;

    /// <summary>Son va pul chegarasi (shartnoma: ±1 000 000 000).</summary>
    public const long NumberLimit = 1_000_000_000;

    /// <summary>«Boshqa til» nomi.</summary>
    public const int MaxOtherNameLength = QuestionnaireFormValidator.MaxNameLength;

    /// <summary>Imzo o'rnidagi ism.</summary>
    public const int MaxSignatureNameLength = QuestionnaireFormValidator.MaxShortLength;

    /// <summary>Shahar yoki xorijiy telefon matni.</summary>
    public const int MaxLoosePhoneLength = 30;

    /// <summary>Sana va oy chegarasi — «yil 0» kabi qiymatlar uchun.</summary>
    public const int MinYear = 1900;

    public const int MaxYear = 2100;

    /// <summary>Ish davri oylari shu oydan (o'rnatilgan anketadagi qoida).</summary>
    public const int MinPeriodYear = 1950;

    /// <summary>Javoblarni tekshiradi va tozalangan nusxasini qaytaradi.</summary>
    /// <param name="answers">Formadan kelgan <c>{ maydon: qiymat }</c>.</param>
    /// <param name="definition">Nomzod ko'rgan versiya.</param>
    /// <param name="today">Toshkent bo'yicha bugun (yosh va oy chegaralari).</param>
    /// <param name="referralChannels">«Qayerdan bilgan» ning FAOL kanallari.</param>
    public static Result<DynamicFormAnswers> Validate(
        JsonObject answers,
        FormDefinition definition,
        DateOnly today,
        IReadOnlyCollection<ReferralChannelOptionDto> referralChannels)
    {
        ArgumentNullException.ThrowIfNull(answers);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(referralChannels);

        try
        {
            return new Check(definition, today, referralChannels).Run(answers);
        }
        catch (FieldFailure failure)
        {
            return failure.Error;
        }
    }

    /// <summary>Maydon ko'rinadimi — sharti tozalangan javoblarga qarab (R5).</summary>
    public static bool Visible(FormField field, JsonObject answers)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(answers);

        if (field.VisibleIf is not { } condition)
        {
            return true;
        }

        var value = answers[condition.Field];

        if (condition.Is is { } expected)
        {
            return DynamicFormAnswers.Flag(value) == expected;
        }

        var chosen = value switch
        {
            JsonArray items => items.Select(DynamicFormAnswers.Text).OfType<string>().ToList(),
            _ => DynamicFormAnswers.Text(value) is { } single ? [single] : [],
        };

        return condition.In is { } values && chosen.Any(values.Contains);
    }

    [GeneratedRegex("^[A-Za-z0-9_]{5,32}$", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex TelegramPattern();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"^[0-9]{4}-[0-9]{2}\z", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex MonthPattern();

    [GeneratedRegex(@"^[0-9]{4}-[0-9]{2}-[0-9]{2}\z", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex DatePattern();

    /// <summary>Tekshiruvni to'xtatadigan birinchi xato — faqat shu sinf ichida ushlanadi.</summary>
    private sealed class FieldFailure(Error error) : Exception(error.Message)
    {
        public Error Error { get; } = error;
    }

    /// <summary>Bitta tekshiruv yurishi (istisno bilan to'xtatish — o'rnatilgan tekshiruvdagi sabab).</summary>
    private sealed class Check(
        FormDefinition definition,
        DateOnly today,
        IReadOnlyCollection<ReferralChannelOptionDto> referralChannels)
    {
        private readonly JsonObject _clean = [];

        public DynamicFormAnswers Run(JsonObject raw)
        {
            foreach (var field in definition.CandidateFields)
            {
                if (!field.Kind.HasAnswer() || !Visible(field, _clean))
                {
                    continue;
                }

                if (Field(field, raw[field.Id], field.Id) is { } value)
                {
                    _clean[field.Id] = value;
                }
            }

            return new DynamicFormAnswers(definition, _clean);
        }

        // ════════════════════════════════════════════════ turlar

        private JsonNode? Field(FormField field, JsonNode? node, string path)
        {
            if (node is null)
            {
                return Empty(field, path);
            }

            return field.Kind switch
            {
                FieldKind.ShortText or FieldKind.LongText => Text(field, node, path),
                FieldKind.Number or FieldKind.Money => Number(field, node, path),
                FieldKind.Date => Date(field, node, path),
                FieldKind.Month => Month(field, node, path, MinYear, new DateOnly(MaxYear, 12, 1)),
                FieldKind.Year => Year(field, node, path),
                FieldKind.Phone => Phone(field, node, path),
                FieldKind.Email => Email(field, node, path),
                FieldKind.Telegram => Telegram(field, node, path),
                FieldKind.YesNo => YesNo(node, path),
                FieldKind.Choice => Choice(field, node, path),
                FieldKind.MultiChoice => MultiChoice(field, node, path),
                FieldKind.Levels => Levels(field, node, path),
                FieldKind.Repeat => Repeat(field, node, path),
                FieldKind.Period => Period(field, node, path),
                FieldKind.YearRange => YearRange(field, node, path),
                FieldKind.Consent => Consent(node, path),
                FieldKind.Vacancy => Vacancy(node, path),
                FieldKind.Signature => Signature(field, node, path),
                FieldKind.Referral => Referral(field, node, path),
                _ => null,
            };
        }

        /// <summary>Javob yo'q: majburiy bo'lsa — xato, aks holda tashlanadi.</summary>
        private static JsonNode? Empty(FormField field, string path)
        {
            switch (field.Kind)
            {
                case FieldKind.Consent:
                    Fail(QuestionnaireFormErrors.ConsentMissing, path);
                    break;

                // «Umumiy anketa» — tanlovning yo'qligi, javob (SPEC-planshet-anketa §4)
                case FieldKind.Vacancy:
                    return null;

                case FieldKind.Signature when field.Required:
                    Missing($"{path}.name");
                    break;

                case FieldKind.Referral when field.Required:
                    Missing($"{path}.channel");
                    break;

                default:
                    if (field.Required)
                    {
                        Missing(path);
                    }

                    break;
            }

            return null;
        }

        private static JsonNode? Text(FormField field, JsonNode node, string path)
        {
            var text = Clean(node, path, field.TextLimit, multiline: field.Kind == FieldKind.LongText);
            if (text is null)
            {
                return Empty(field, path);
            }

            // «.», «-», «1» javob emas (o'rnatilgan anketadagi 2 harf qoidasi, 2026-10-29)
            if (field.MinLetters > 0 && text.Count(char.IsLetter) < field.MinLetters)
            {
                Fail(field.Required ? QuestionnaireFormErrors.Required : QuestionnaireFormErrors.Invalid, path);
            }

            return text;
        }

        private static JsonNode Number(FormField field, JsonNode node, string path)
        {
            var number = Whole(node, path);
            var min = field.Min ?? (field.Kind == FieldKind.Money ? 0 : -NumberLimit);
            var max = field.Max ?? NumberLimit;

            Ensure(number >= min && number <= max, path);
            return number;
        }

        private JsonNode? Date(FormField field, JsonNode node, string path)
        {
            var text = Plain(node, path);
            if (text.Length == 0)
            {
                return Empty(field, path);
            }

            var day = default(DateOnly);
            var valid = DatePattern().IsMatch(text)
                && DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day)
                && day.Year is >= MinYear and <= MaxYear;

            Ensure(valid, path);

            var age = Age(day);

            Ensure(field.MinAge is not { } minAge || age >= minAge, path);
            Ensure(field.MaxAge is not { } maxAge || age <= maxAge, path);
            Ensure(!field.NotBeforeToday || day >= today, path);

            // Nomzod kartasidagi sana — maydon nima desa ham 14–100 (SPEC §3)
            if (field.Bind == FormProfile.BirthDate)
            {
                Ensure(age is >= FormProfile.MinAge and <= FormProfile.MaxAge, path);
            }

            return text;
        }

        private static JsonNode? Month(FormField field, JsonNode node, string path, int fromYear, DateOnly last)
            => Month(OptionalPlain(node, path), path, fromYear, last) ?? Empty(field, path);

        private static string? Month(string? text, string path, int fromYear, DateOnly last)
        {
            if (text is null)
            {
                return null;
            }

            var valid = MonthPattern().IsMatch(text)
                && DateOnly.TryParseExact(text + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month)
                && month.Year >= fromYear
                && month <= last;

            Ensure(valid, path);
            return text;
        }

        private JsonNode Year(FormField field, JsonNode node, string path)
        {
            var year = Whole(node, path);
            Ensure(year >= (field.MinYear ?? MinYear) && year <= LastYear(field), path);
            return year;
        }

        private static JsonNode? Phone(FormField field, JsonNode node, string path)
        {
            // Nomzod telefoni — kartaga va takroriy nomzodni topishga: faqat mobil (SPEC §3)
            var mobile = field.MobileOnly || field.Bind == FormProfile.Phone;

            var text = Clean(node, path, MaxLoosePhoneLength, multiline: false);
            if (text is null)
            {
                return Empty(field, path);
            }

            if (mobile)
            {
                var parsed = PhoneNumber.Create(text);
                if (parsed.IsFailure)
                {
                    // Telefonning o'z xato kodi (`telefon.*`) — to'rt tilga tarjima qilingan
                    throw new FieldFailure(parsed.Error.At(path));
                }

                return parsed.Value.Value;
            }

            var digits = text.Count(char.IsAsciiDigit);
            var allowed = text.All(symbol => char.IsAsciiDigit(symbol) || symbol is '+' or ' ' or '-' or '(' or ')');

            Ensure(allowed && digits is >= 7 and <= 15, path);

            return PhoneNumber.Create(text) is { IsSuccess: true } uzbek ? uzbek.Value.Value : text;
        }

        private static JsonNode? Email(FormField field, JsonNode node, string path)
        {
            var text = Clean(node, path, QuestionnaireFormValidator.MaxShortLength, multiline: false);
            if (text is null)
            {
                return Empty(field, path);
            }

            Ensure(EmailPattern().IsMatch(text), path);
            return text;
        }

        private static JsonNode? Telegram(FormField field, JsonNode node, string path)
        {
            var text = Clean(node, path, QuestionnaireFormValidator.MaxShortLength, multiline: false)?.TrimStart('@');
            if (string.IsNullOrEmpty(text))
            {
                return Empty(field, path);
            }

            Ensure(TelegramPattern().IsMatch(text), path);
            return text;
        }

        private static JsonNode YesNo(JsonNode node, string path)
        {
            var flag = DynamicFormAnswers.Flag(node);
            Ensure(flag is not null, path);
            return flag!.Value;
        }

        private static JsonNode? Choice(FormField field, JsonNode node, string path)
        {
            var value = Plain(node, path);
            if (value.Length == 0)
            {
                return Empty(field, path);
            }

            Ensure(field.Option(value) is not null, path);
            return value;
        }

        private static JsonNode? MultiChoice(FormField field, JsonNode node, string path)
        {
            if (node is not JsonArray items)
            {
                Fail(QuestionnaireFormErrors.Invalid, path);
                return null;
            }

            var chosen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                var value = DynamicFormAnswers.Text(item);
                Ensure(value is not null && field.Option(value) is not null && chosen.Add(value), path);
            }

            if (chosen.Count == 0)
            {
                return Empty(field, path);
            }

            Ensure(field.MinSelected is not { } min || chosen.Count >= min, path);
            Ensure(field.MaxSelected is not { } max || chosen.Count <= max, path);

            // Variantlar tartibida — PDF va kartadagi «B, C» qog'ozdagidek
            return new JsonArray([.. field.Options.Where(option => chosen.Contains(option.Value)).Select(option => (JsonNode)option.Value)]);
        }

        /// <summary>
        /// <c>{ "rows": { qiymat: [jihat bo'yicha daraja] }, "other": [{ "name", "levels" }] }</c>.
        /// </summary>
        /// <remarks>
        /// Darajasi yo'q qator tashlanadi (bilmagan tilini bo'sh qoldirgan nomzod); majburiy qatorda
        /// har jihat bo'lishi shart. Natija — variantlar tartibida, keyin «boshqa»lar kiritilgan tartibda.
        /// </remarks>
        private static JsonNode? Levels(FormField field, JsonNode node, string path)
        {
            if (node is not JsonObject levels)
            {
                Fail(QuestionnaireFormErrors.Invalid, path);
                return null;
            }

            var rows = new Dictionary<string, JsonArray>(StringComparer.Ordinal);

            if (levels["rows"] is { } rowsNode)
            {
                if (rowsNode is not JsonObject given)
                {
                    Fail(QuestionnaireFormErrors.Invalid, $"{path}.rows");
                    return null;
                }

                foreach (var (value, row) in given)
                {
                    var rowPath = $"{path}.rows.{value}";
                    Ensure(field.Option(value) is not null, rowPath);

                    if (Scale(field, row, rowPath) is { } scale)
                    {
                        rows[value] = scale;
                    }
                }
            }

            foreach (var required in field.RequiredRows)
            {
                var complete = rows.TryGetValue(required, out var row) && row.All(level => level is not null);
                Ensure(complete, $"{path}.rows.{required}", missing: true);
            }

            var others = new JsonArray();

            if (levels["other"] is { } otherNode)
            {
                if (otherNode is not JsonArray given)
                {
                    Fail(QuestionnaireFormErrors.Invalid, $"{path}.other");
                    return null;
                }

                Ensure(given.Count == 0 || field.Other is not null, $"{path}.other");
                Ensure(given.Count <= (field.Other?.Max ?? 0), $"{path}.other");

                for (var index = 0; index < given.Count; index++)
                {
                    var otherPath = $"{path}.other[{index}]";
                    if (given[index] is not JsonObject other)
                    {
                        Missing(otherPath);
                        return null;
                    }

                    var name = Clean(other["name"], $"{otherPath}.name", MaxOtherNameLength, multiline: false);
                    if (name is null)
                    {
                        Missing($"{otherPath}.name");
                    }

                    others.Add(new JsonObject
                    {
                        ["name"] = name,
                        ["levels"] = Scale(field, other["levels"], $"{otherPath}.levels")
                            ?? new JsonArray([.. field.Aspects.Select(_ => (JsonNode?)null)]),
                    });
                }
            }

            if (rows.Count == 0 && others.Count == 0)
            {
                return Empty(field, path);
            }

            var result = new JsonObject
            {
                ["rows"] = new JsonObject(field.Options
                    .Where(option => rows.ContainsKey(option.Value))
                    .Select(option => KeyValuePair.Create(option.Value, (JsonNode?)rows[option.Value]))),
            };

            if (others.Count > 0)
            {
                result["other"] = others;
            }

            return result;
        }

        /// <summary>Bitta qatorning darajalari — jihatlar soni, har biri shkalada yoki <c>null</c>.</summary>
        private static JsonArray? Scale(FormField field, JsonNode? node, string path)
        {
            if (node is null)
            {
                return null;
            }

            if (node is not JsonArray levels || levels.Count != field.Aspects.Count)
            {
                Fail(QuestionnaireFormErrors.Invalid, path);
                return null;
            }

            var result = new JsonArray();
            var any = false;

            foreach (var level in levels)
            {
                if (level is null)
                {
                    result.Add(null);
                    continue;
                }

                var value = DynamicFormAnswers.Number(level);
                Ensure(value is { } number && number >= field.ScaleMin && number <= field.ScaleMax, path);

                result.Add(value!.Value);
                any = true;
            }

            return any ? result : null;
        }

        private JsonNode? Repeat(FormField field, JsonNode node, string path)
        {
            if (node is not JsonArray rows)
            {
                Fail(QuestionnaireFormErrors.Invalid, path);
                return null;
            }

            Ensure(rows.Count <= (field.MaxRows ?? MaxRepeatRows), path);

            var result = new JsonArray();

            for (var index = 0; index < rows.Count; index++)
            {
                var rowPath = $"{path}[{index}]";
                if (rows[index] is not JsonObject row)
                {
                    Missing(rowPath);
                    return null;
                }

                var clean = new JsonObject();
                foreach (var column in field.Columns)
                {
                    if (Field(column, row[column.Id], $"{rowPath}.{column.Id}") is { } value)
                    {
                        clean[column.Id] = value;
                    }
                }

                // Bo'sh qator — nomzod qo'shib, to'ldirmagan; hujjatga tushmaydi
                if (clean.Count > 0)
                {
                    result.Add(clean);
                }
            }

            if (result.Count == 0)
            {
                return Empty(field, path);
            }

            Ensure(result.Count >= field.MinRows, path, missing: true);
            return result;
        }

        /// <summary><c>{ from?, to?, current? }</c> — oylar 1950-01 dan joriy oygacha, <c>current</c> → <c>to</c> yo'q.</summary>
        private JsonNode? Period(FormField field, JsonNode node, string path)
        {
            if (node is not JsonObject period)
            {
                Fail(QuestionnaireFormErrors.Invalid, path);
                return null;
            }

            var thisMonth = new DateOnly(today.Year, today.Month, 1);
            var from = Month(OptionalPlain(period["from"], path), path, MinPeriodYear, thisMonth);
            var current = period["current"] is { } flag ? DynamicFormAnswers.Flag(flag) : false;

            Ensure(current is not null, path);
            Ensure(current != true || field.AllowCurrent, path);

            var to = current == true ? null : Month(OptionalPlain(period["to"], path), path, MinPeriodYear, thisMonth);
            Ensure(from is null || to is null || string.CompareOrdinal(from, to) <= 0, path);

            if (from is null && to is null && current != true)
            {
                return Empty(field, path);
            }

            Ensure(!field.Required || (from is not null && (to is not null || current == true)), path, missing: true);

            var result = new JsonObject();
            if (from is not null)
            {
                result["from"] = from;
            }

            if (to is not null)
            {
                result["to"] = to;
            }

            if (current == true)
            {
                result["current"] = true;
            }

            return result;
        }

        private JsonNode? YearRange(FormField field, JsonNode node, string path)
        {
            if (node is not JsonObject range)
            {
                Fail(QuestionnaireFormErrors.Invalid, path);
                return null;
            }

            long? Edge(string key)
            {
                if (range[key] is not { } value)
                {
                    return null;
                }

                var year = Whole(value, path);
                Ensure(year >= (field.MinYear ?? MinYear) && year <= LastYear(field), path);
                return year;
            }

            var from = Edge("from");
            var to = Edge("to");

            Ensure(from is null || to is null || from <= to, path);

            if (from is null && to is null)
            {
                return Empty(field, path);
            }

            Ensure(!field.Required || (from is not null && to is not null), path, missing: true);

            var result = new JsonObject();
            if (from is not null)
            {
                result["from"] = from;
            }

            if (to is not null)
            {
                result["to"] = to;
            }

            return result;
        }

        private static JsonNode Consent(JsonNode node, string path)
        {
            if (DynamicFormAnswers.Flag(node) != true)
            {
                Fail(QuestionnaireFormErrors.ConsentMissing, path);
            }

            return true;
        }

        private static JsonNode? Vacancy(JsonNode node, string path)
        {
            var text = Plain(node, path);
            if (text.Length == 0)
            {
                return null;
            }

            Ensure(Guid.TryParse(text, out var id) && id != Guid.Empty, path);
            return id.ToString();
        }

        private JsonNode? Signature(FormField field, JsonNode node, string path)
        {
            if (node is not JsonObject signature)
            {
                Fail(QuestionnaireFormErrors.Invalid, path);
                return null;
            }

            var namePath = $"{path}.name";
            var name = Clean(signature["name"], namePath, MaxSignatureNameLength, multiline: false);

            if (name is null)
            {
                return Empty(field, path);
            }

            var lastName = Bound(FormProfile.LastName);
            var firstName = Bound(FormProfile.FirstName);

            if (!QuestionnaireFormValidator.SignatureMatches(name, lastName, firstName))
            {
                throw new FieldFailure(QuestionnaireFormErrors.SignatureNameMismatch.At(namePath));
            }

            return new JsonObject { ["name"] = name };
        }

        private JsonNode? Referral(FormField field, JsonNode node, string path)
        {
            if (node is not JsonObject referral)
            {
                Fail(QuestionnaireFormErrors.Invalid, path);
                return null;
            }

            var channelPath = $"{path}.channel";
            if (referral["channel"] is null)
            {
                return Empty(field, path);
            }

            var text = Plain(referral["channel"]!, channelPath);
            var channel = Guid.TryParse(text, out var id)
                ? referralChannels.FirstOrDefault(option => option.Id == id)
                : null;

            if (channel is null)
            {
                Fail(RecruitingErrors.ReferralChannelNotFound, channelPath);
            }

            var result = new JsonObject { ["channel"] = channel!.Id.ToString() };

            // Aniqlik faqat kanal so'rasa: majburiy, aks holda saqlanmaydi (o'rnatilgan anketadagidek)
            if (channel.AsksDetail)
            {
                var detailPath = $"{path}.detail";
                var detail = Clean(referral["detail"], detailPath, Candidate.MaxReferralDetailLength, multiline: false);

                if (detail is null)
                {
                    Missing(detailPath);
                }

                result["detail"] = detail;
            }

            return result;
        }

        // ════════════════════════════════════════════════ yordamchilar

        private string? Bound(string binding)
            => definition.Bound(binding) is { } field ? DynamicFormAnswers.Text(_clean[field.Id]) : null;

        private int Age(DateOnly birthDate)
        {
            var age = today.Year - birthDate.Year;
            return birthDate > today.AddYears(-age) ? age - 1 : age;
        }

        private long LastYear(FormField field)
            => field.MaxYearToday ? today.Year : field.MaxYear ?? MaxYear;

        private static void Fail(Error error, string path)
            => throw new FieldFailure(error.At(path));

        private static void Missing(string path)
            => Fail(QuestionnaireFormErrors.Required, path);

        private static void Ensure(bool condition, string path, bool missing = false)
        {
            if (condition)
            {
                return;
            }

            Fail(missing ? QuestionnaireFormErrors.Required : QuestionnaireFormErrors.Invalid, path);
        }

        /// <summary>Satr qiymat (bo'shliqlarsiz); satr emas — «noto'g'ri».</summary>
        private static string Plain(JsonNode node, string path)
        {
            var text = DynamicFormAnswers.Text(node);
            if (text is null)
            {
                Fail(QuestionnaireFormErrors.Invalid, path);
            }

            return text!.Trim();
        }

        private static string? OptionalPlain(JsonNode? node, string path)
            => node is null ? null : Plain(node, path) is { Length: > 0 } text ? text : null;

        /// <summary>Matn: kesilmaydi (chegaradan oshsa — xato), taqiqlangan belgilar rad; bo'sh — <c>null</c>.</summary>
        private static string? Clean(JsonNode? node, string path, int limit, bool multiline)
        {
            if (node is null)
            {
                return null;
            }

            var text = Plain(node, path);
            if (text.Length == 0)
            {
                return null;
            }

            if (text.Length > limit)
            {
                throw new FieldFailure(QuestionnaireFormErrors.TooLong.At(path, limit));
            }

            if (QuestionnaireFormValidator.HasForbiddenCharacter(text, multiline))
            {
                Fail(QuestionnaireFormErrors.ForbiddenCharacter, path);
            }

            return text;
        }

        /// <summary>Butun son; kasr, satr yoki mantiqiy qiymat — «noto'g'ri».</summary>
        private static long Whole(JsonNode node, string path)
        {
            var number = DynamicFormAnswers.Number(node);
            Ensure(number is not null, path);
            return number!.Value;
        }
    }
}
