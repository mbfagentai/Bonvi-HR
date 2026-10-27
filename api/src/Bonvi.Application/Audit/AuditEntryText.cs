using System.Globalization;
using System.Text.Json;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;

namespace Bonvi.Application.Audit;

/// <summary>
/// Jurnal qatorini O'QILADIGAN matnga aylantiradi — Excel eksporti uchun
/// (<c>SPEC-excel-eksport.md</c> §2 #9).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ Ekrandagi qoidalarning C# nusxasi (<c>web/src/features/audit/lib/audit-format.ts</c>):
/// bajaruvchi (<c>actorView</c>), amal (<c>entryActionLabel</c> + <c>operationLabel</c>) va
/// bir qatorlik xulosa (<c>changeSummary</c>). Fayl ekrandagidan BOSHQA gap aytmasligi
/// kerak — qoida o'zgarsa, ikkala joyda. Yorliqlarning o'zi — <see cref="AuditLabels"/>.
/// </para>
/// <para>
/// Yashirilgan maydon (<see cref="AuditChangeDto.Masked"/>) serverdan allaqachon QIYMATSIZ
/// keladi — bu yerda faqat «•••» yoziladi. Ya'ni fayl ekrandan ko'proq narsa ko'rsata olmaydi:
/// ko'rinish va yashirish qoidasi ro'yxatniki bilan bitta (<c>AuditEntryAssembler</c>).
/// </para>
/// <para>
/// Sonlar ekrandagidek: ming ajratgichi — bo'sh joy, kasr — vergul («4 000 000», «2,5»).
/// Vaqt — Toshkent vaqtida.
/// </para>
/// </remarks>
public static class AuditEntryText
{
    /// <summary>Yashirilgan qiymat — ekrandagi «•••».</summary>
    public const string MaskedText = "•••";

    /// <summary>Bo'sh qiymat — «yuklanmadimi?» degan savol tug'ilmasin.</summary>
    public const string EmptyText = "—";

    /// <summary>Xulosada nomi bilan ko'rsatiladigan maydonlar soni — qolgani «va yana N ta».</summary>
    private const int SummaryFields = 4;

    private const string EmployeeApp = "Xodim (ilova)";
    private const string Anonymous = "Tizimga kirmagan";
    private const string PasswordViewed = "Xodim ilovasi paroli ko'rildi";
    private const string RevealPasswordOperation = "RevealEmployeePassword";

    /// <summary>
    /// «Jamoam» ko'rish izlari (SPEC-jamoam §C6) — tafsilotdagi <c>action</c> bo'yicha; ular ham
    /// <see cref="AuditAction.Export"/> bo'lib yoziladi, xuddi parolni ko'rish kabi.
    /// </summary>
    private static readonly Dictionary<string, string> TeamViews = new(StringComparer.Ordinal)
    {
        ["team_member_viewed"] = "Jamoa: xodim davomati ko'rildi",
        ["team_board_viewed"] = "Jamoa paneli ko'rildi",
    };

    /// <summary>Telefon raqami saqlanadigan maydonlar — «+998 90 123 45 67» ko'rinishida.</summary>
    private static readonly HashSet<string> PhoneFields = new(StringComparer.Ordinal) { "Phone", "SecondPhone", "Login" };

    /// <summary>Ekrandagi son ko'rinishi (<c>ru-RU</c>): «4 000 000», «2,5».</summary>
    private static readonly NumberFormatInfo Numbers = NumberFormatInfo.ReadOnly(new NumberFormatInfo
    {
        NumberGroupSeparator = " ",
        NumberDecimalSeparator = ",",
    });

    // ════════════════════════════════════════════════════════ bajaruvchi

    /// <summary>«Kim» — ism; tizim, qurilma va kirmagan so'rov uchun o'z nomi.</summary>
    /// <param name="actor">Qatordagi bajaruvchi.</param>
    public static string ActorName(AuditActorDto actor)
    {
        ArgumentNullException.ThrowIfNull(actor);

        return actor.Kind switch
        {
            AuditActorKind.System => "Tizim",
            AuditActorKind.Device => "Qurilma",
            AuditActorKind.EmployeeAccount => actor.Name ?? EmployeeApp,

            // Ochiq onlayn anketa — o'z nomi («Onlayn anketa»); notanish login bilan kirish
            // urinishi — normallashgan telefon raqami
            AuditActorKind.Anonymous => actor.Name ?? FormatPhone(actor.Login) ?? Anonymous,

            _ => actor.Name ?? FormatPhone(actor.Login) ?? "Noma'lum",
        };
    }

    /// <summary>
    /// «Roli» — ekrandagi ikkinchi qator: O'SHA LAHZADAGI rol nomi yoki bajaruvchi turi.
    /// </summary>
    /// <remarks>Rol nomi ma'lumot, tarjima qilinmaydi (bugungi nomi ham emas — SPEC §9.5).</remarks>
    /// <param name="actor">Qatordagi bajaruvchi.</param>
    public static string? ActorDetail(AuditActorDto actor)
    {
        ArgumentNullException.ThrowIfNull(actor);

        return actor.Kind switch
        {
            AuditActorKind.System or AuditActorKind.Device => null,
            AuditActorKind.EmployeeAccount => EmployeeApp,
            AuditActorKind.Anonymous => actor.Name is null && actor.Login is null ? null : Anonymous,
            _ => actor.RoleName,
        };
    }

    // ════════════════════════════════════════════════════════════ amal

    /// <summary>
    /// «Amal» — amal turi va (tanish bo'lsa) biznes amalining nomi: «O'zgartirildi · Ko'chirish».
    /// </summary>
    /// <remarks>
    /// Ekranda ular yonma-yon (belgi va matn). Istisno ekrandagidek: xodim ilovasi parolini
    /// KO'RISH ham <see cref="AuditAction.Export"/> bo'lib yoziladi, lekin unga «Excelga yuklandi»
    /// deyish yolg'on bo'lardi. «Jamoam» ko'rishlari ham xuddi shunday — «Ko'rildi».
    /// </remarks>
    /// <param name="entry">Jurnal qatori.</param>
    public static string Action(AuditEntryDto entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var action = entry.Action switch
        {
            AuditAction.Export when entry.Operation == RevealPasswordOperation => "Parol ko'rildi",
            AuditAction.Export when TeamViewOf(entry) is not null => "Ko'rildi",
            _ => ActionLabel(entry.Action),
        };

        return OperationLabel(entry.Operation) is { } operation ? $"{action} · {operation}" : action;
    }

    /// <summary>«Yozuv turi» — «Buyruq», «Xodim kartasi»; notanishi — xom nom.</summary>
    /// <param name="entityName">Entity nomi.</param>
    public static string EntityType(string entityName)
        => AuditLabels.Entities.TryGetValue(entityName, out var label) ? label : entityName;

    // ═════════════════════════════════════════════════ bir qatorlik xulosa

    /// <summary>
    /// «O'zgarishlar» — bitta qatorda; bo'sh satr — aytadigan narsa yo'q.
    /// </summary>
    /// <remarks>
    /// Ekrandagi qoida: kirish hodisasi va eksport — tafsilotdan bitta jumla; bitta maydonli
    /// tahrir — qiymati bilan («Oklad: 4 000 000 so'm → 5 000 000 so'm»); qolganida — birinchi
    /// to'rtta maydon nomi va «va yana N ta».
    /// </remarks>
    /// <param name="entry">Jurnal qatori.</param>
    public static string Summary(AuditEntryDto entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (EventSummary(entry) is { } detail)
        {
            return detail;
        }

        var changes = entry.Changes;
        if (changes.Count == 0)
        {
            return string.Empty;
        }

        if (entry.Action == AuditAction.Update && changes.Count == 1 && !IsRolePermissions(entry, changes[0]))
        {
            var only = changes[0];
            var label = FieldLabel(only.Field);

            if (only.Masked)
            {
                return $"{label}: {MaskedText}";
            }

            var before = ValueText(entry, only, old: true) ?? EmptyText;
            var after = ValueText(entry, only, old: false) ?? EmptyText;
            return $"{label}: {before} → {after}";
        }

        var labels = string.Join(", ", changes.Take(SummaryFields).Select(change => FieldLabel(change.Field)));
        var rest = changes.Count - SummaryFields;

        return rest > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{labels} va yana {rest} ta")
            : labels;
    }

    /// <summary>
    /// Kirish hodisalari va eksport — tafsilotdan bitta jumla; boshqa amallarda <see langword="null"/>
    /// (xulosa maydonlardan quriladi).
    /// </summary>
    private static string? EventSummary(AuditEntryDto entry)
    {
        JsonElement? ValueOf(string field)
            => entry.Changes.FirstOrDefault(change => change.Field == field)?.New;

        switch (entry.Action)
        {
            case AuditAction.LoginFailed:
                return StringOf(ValueOf("reason")) is { } reason
                       && AuditLabels.LoginReasons.TryGetValue(reason, out var reasonLabel)
                    ? reasonLabel
                    : string.Empty;

            case AuditAction.PermissionDenied:
                return string.Join(' ', new[] { StringOf(ValueOf("method")), StringOf(ValueOf("path")) }.OfType<string>());

            case AuditAction.Logout:
                return ValueOf("allSessions") is { ValueKind: JsonValueKind.True } ? "Barcha qurilmalardan" : string.Empty;

            case AuditAction.Login:
            case AuditAction.TokenReuse:
                return string.Empty;

            case AuditAction.Export:
                if (StringOf(ValueOf("action")) == "password_viewed")
                {
                    return PasswordViewed;
                }

                if (TeamViewOf(entry) is { } teamView)
                {
                    return teamView;
                }

                return ValueOf("rows") is { ValueKind: JsonValueKind.Number } rows
                    ? $"{Number(rows)} ta qator"
                    : string.Empty;

            default:
                return null;
        }
    }

    /// <summary>«Jamoam» ko'rish izining yorlig'i; boshqa qatorda <see langword="null"/>.</summary>
    private static string? TeamViewOf(AuditEntryDto entry)
    {
        var action = entry.Changes.FirstOrDefault(change => change.Field == "action")?.New;

        return StringOf(action) is { } name && TeamViews.TryGetValue(name, out var label) ? label : null;
    }

    // ═══════════════════════════════════════════════════════════ yorliqlar

    private static string ActionLabel(AuditAction action) => action switch
    {
        AuditAction.Create => "Yaratildi",
        AuditAction.Update => "O'zgartirildi",
        AuditAction.Delete => "O'chirildi",
        AuditAction.Login => "Tizimga kirdi",
        AuditAction.LoginFailed => "Kirish muvaffaqiyatsiz",
        AuditAction.Logout => "Tizimdan chiqdi",
        AuditAction.Export => "Excelga yuklandi",
        AuditAction.PermissionDenied => "Ruxsatsiz urinish",
        AuditAction.TokenReuse => "Sessiya o'g'irlanishi shubhasi",
        _ => action.ToString(),
    };

    /// <summary>
    /// Biznes amalining nomi; tanish bo'lmasa — <see langword="null"/> (faqat amal turi qoladi).
    /// Tizim amallari (<c>etl:…</c>, <c>startup:…</c>, <c>job:…</c>) umumiy nom bilan.
    /// </summary>
    private static string? OperationLabel(string? operation)
    {
        if (string.IsNullOrEmpty(operation))
        {
            return null;
        }

        if (AuditLabels.Operations.TryGetValue(operation, out var label))
        {
            return label;
        }

        return operation switch
        {
            _ when operation.StartsWith("etl:", StringComparison.Ordinal) => "Excel'dan ko'chirish",
            _ when operation.StartsWith("startup:", StringComparison.Ordinal) => "Tizim ishga tushishi",
            _ when operation.StartsWith("job:", StringComparison.Ordinal) => "Fon vazifasi",

            // Ma'lumotnomalarning umumiy endpointlari: `ActivateLegalEntity`, `DeactivateDirection`…
            _ when operation.StartsWith("Deactivate", StringComparison.Ordinal) => "Faolsizlantirish",
            _ when operation.StartsWith("Activate", StringComparison.Ordinal) => "Faollashtirish",
            _ => null,
        };
    }

    /// <summary>
    /// Maydon yorlig'i; tarjimasi yo'q — xom nom. Kengaytirishdan oldingi qatorlarda nomlar
    /// kichik harf bilan (<c>permissions</c>) — ular ham taniladi.
    /// </summary>
    private static string FieldLabel(string field)
    {
        var key = field.Replace('.', '_');

        if (AuditLabels.Fields.TryGetValue(key, out var label))
        {
            return label;
        }

        return key.Length > 0 && AuditLabels.Fields.TryGetValue(char.ToUpperInvariant(key[0]) + key[1..], out label)
            ? label
            : field;
    }

    /// <summary>Rolning ruxsatlar ro'yxati — ekranda darajalar farqi bo'lib chiziladi, xulosada esa faqat nomi.</summary>
    private static bool IsRolePermissions(AuditEntryDto entry, AuditChangeDto change)
        => entry.EntityName == "Role" && string.Equals(change.Field, "permissions", StringComparison.OrdinalIgnoreCase);

    // ═══════════════════════════════════════════════════════════ qiymatlar

    /// <summary>Bitta qiymatning matni; <see langword="null"/> — qiymat yo'q.</summary>
    private static string? ValueText(AuditEntryDto entry, AuditChangeDto change, bool old)
    {
        var value = old ? change.Old : change.New;
        var label = old ? change.OldLabel : change.NewLabel;

        if (value is not { } element || element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return change.ValueType switch
        {
            AuditValueTypes.Ref => label ?? ShortId(element),
            AuditValueTypes.List => label ?? ListText(element),
            AuditValueTypes.Money => MoneyText(element),
            AuditValueTypes.Date => element.ValueKind == JsonValueKind.String ? DateText(element.GetString()!) : Plain(element),
            AuditValueTypes.DateTime => element.ValueKind == JsonValueKind.String ? DateTimeText(element.GetString()!) : Plain(element),
            AuditValueTypes.Bool => element.ValueKind is JsonValueKind.True or JsonValueKind.False ? YesNo(element.GetBoolean()) : Plain(element),
            AuditValueTypes.Number => NumberText(element),
            AuditValueTypes.Enum => element.ValueKind == JsonValueKind.String ? EnumText(entry.EntityName, change.Field, element.GetString()!) : Plain(element),
            AuditValueTypes.Json => CompactJson(element),
            _ => TextValue(change.Field, element),
        };
    }

    /// <summary>Matnli maydon — kirish sababi, eksport amali va telefonlar alohida.</summary>
    private static string? TextValue(string field, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            return Plain(value);
        }

        var text = value.GetString()!;
        if (text.Length == 0)
        {
            return null;
        }

        if (field == "reason" && AuditLabels.LoginReasons.TryGetValue(text, out var reason))
        {
            return reason;
        }

        if (field == "action" && text == "password_viewed")
        {
            return PasswordViewed;
        }

        if (field == "action" && TeamViews.TryGetValue(text, out var teamView))
        {
            return teamView;
        }

        return PhoneFields.Contains(field) ? FormatPhone(text) : text;
    }

    private static string EnumText(string entityName, string field, string value)
        => AuditLabels.EnumValues.TryGetValue($"{entityName}.{field}", out var labels)
           && labels.TryGetValue(value, out var label)
            ? label
            : value;

    /// <summary>Havola yorlig'i topilmasa — qisqa identifikator (8 belgi), to'liq GUID emas.</summary>
    private static string ShortId(JsonElement value)
    {
        var text = value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();
        return text.Length > 9 ? $"{text[..8]}…" : text;
    }

    private static string? ListText(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            return Plain(value);
        }

        return value.GetArrayLength() == 0 ? null : string.Join(", ", value.EnumerateArray().Select(Plain));
    }

    /// <summary>Pul: <c>{ amount, currency }</c>. Valyuta yo'q bo'lsa — so'm (bazadagi hamma oklad UZS).</summary>
    private static string? MoneyText(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number)
        {
            return Number(value);
        }

        if (value.ValueKind != JsonValueKind.Object
            || !value.TryGetProperty("amount", out var amount)
            || amount.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        var currency = value.TryGetProperty("currency", out var code) && code.ValueKind == JsonValueKind.String
            ? code.GetString()
            : null;

        return $"{Number(amount)} {(string.IsNullOrEmpty(currency) || currency == "UZS" ? "so'm" : currency)}";
    }

    private static string DateText(string value)
    {
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        }

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var instant)
            ? instant.TashkentDate().ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)
            : EmptyText;
    }

    private static string DateTimeText(string value)
        => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var instant)
            ? instant.ToTashkent().ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture)
            : EmptyText;

    private static string NumberText(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number)
        {
            return Number(value);
        }

        return value.ValueKind == JsonValueKind.String
               && decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed.ToString("#,##0.###", Numbers)
            : Plain(value);
    }

    private static string Number(JsonElement value)
        => value.TryGetDecimal(out var number) ? number.ToString("#,##0.###", Numbers) : value.GetRawText();

    private static string YesNo(bool value) => value ? "Ha" : "Yo'q";

    /// <summary>Turi noma'lum qiymat — ixcham matn.</summary>
    private static string Plain(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString()!,
        JsonValueKind.Number => Number(value),
        JsonValueKind.True => YesNo(true),
        JsonValueKind.False => YesNo(false),
        JsonValueKind.Array => string.Join(", ", value.EnumerateArray().Select(Plain)),
        _ => CompactJson(value),
    };

    /// <summary>JSON — ixcham: «status: active, rows: 12». Qavs va qo'shtirnoq HR uchun shovqin.</summary>
    private static string CompactJson(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var parts = value.EnumerateObject()
                .Where(property => property.Value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
                .Select(property => $"{property.Name}: {Plain(property.Value)}")
                .ToList();

            return parts.Count == 0 ? EmptyText : string.Join(", ", parts);
        }

        return value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? EmptyText : value.GetRawText();
    }

    private static string? StringOf(JsonElement? value)
        => value is { ValueKind: JsonValueKind.String } text ? text.GetString() : null;

    /// <summary>«905088888» yoki «+998905088888» → «+998 90 508 88 88»; boshqa shakl — o'zi.</summary>
    private static string? FormatPhone(string? phone)
    {
        if (phone is null)
        {
            return null;
        }

        var digits = string.Concat(phone.Where(char.IsAsciiDigit));
        var local = digits.Length == 12 && digits.StartsWith("998", StringComparison.Ordinal) ? digits[3..] : digits;

        return local.Length != 9
            ? phone
            : $"+998 {local[..2]} {local[2..5]} {local[5..7]} {local[7..]}";
    }
}
