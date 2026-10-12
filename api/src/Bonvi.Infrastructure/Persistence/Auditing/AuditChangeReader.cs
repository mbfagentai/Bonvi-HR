using System.Buffers;
using System.Text.Json;
using Bonvi.Application.Audit;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Bonvi.Infrastructure.Persistence.Auditing;

/// <summary>
/// Jurnal qatoridagi JSON'ni maydonlarga ajratadi — O'QISH tomoni (SPEC-rollar-va-audit §8.3).
/// </summary>
/// <remarks>
/// <para>
/// Yozish tomoni (<see cref="AuditEntryReader"/>, <see cref="AuditValueSerializer"/>) bilan
/// SHARTNOMA: maydon nomlari EF xossasi (murakkab xossada nuqtali — <c>Salary.Amount</c>),
/// sana <c>yyyy-MM-dd</c>, sanoq — nomi, sir — <see cref="AuditCatalog.MaskedValue"/>.
/// </para>
/// <para>
/// <b>Kengaytirishdan oldingi qatorlar ham o'qiladi</b> (foydalanuvchi boshqaruvi qo'lda
/// yozgan, dev'da 21 ta): ularda <c>changed_fields</c> yo'q, eski/yangi esa TO'LIQ nusxa —
/// o'zgarganlar ikkalasini solishtirib topiladi; maydon nomlari kichik harfli
/// (<c>permissions</c>) va EF xossasiga mos kelmasa, turi JSON qiymatidan aniqlanadi.
/// </para>
/// <para>
/// Bazaga ulanmaydi va ruxsatni bilmaydi — faqat qiymatlarni o'qiydi. Kim nimani ko'rishi
/// <c>AuditEntryAssembler</c> da.
/// </para>
/// </remarks>
public static class AuditChangeReader
{
    /// <summary>Qatorning maydonlari — yozilish tartibida, pul juftlari birlashtirilgan.</summary>
    /// <param name="entityType">Entity'ning EF turi (hodisada va notanish turda <c>null</c>).</param>
    /// <param name="action">Amal.</param>
    /// <param name="oldValues">Eski qiymatlar (JSON obyekt).</param>
    /// <param name="newValues">Yangi qiymatlar yoki hodisa tafsiloti (JSON obyekt).</param>
    /// <param name="changedFields">O'zgargan maydonlar; eski qatorlarda va o'chirishda <c>null</c>.</param>
    public static IReadOnlyList<AuditFieldValue> Read(
        IReadOnlyEntityType? entityType,
        AuditAction action,
        string? oldValues,
        string? newValues,
        IReadOnlyList<string>? changedFields)
    {
        using var oldDocument = Parse(oldValues);
        using var newDocument = Parse(newValues);

        var oldRoot = oldDocument?.RootElement;
        var newRoot = newDocument?.RootElement;

        var names = FieldNames(action, oldRoot, newRoot, changedFields);
        var fields = new List<AuditFieldValue>(names.Count);
        var mergedMoney = new HashSet<string>(StringComparer.Ordinal);

        foreach (var name in names)
        {
            if (MoneyPrefix(entityType, name) is { } money)
            {
                if (mergedMoney.Add(money))
                {
                    fields.Add(MergeMoney(money, oldRoot, newRoot));
                }

                continue;
            }

            fields.Add(new AuditFieldValue(name, ValueOf(oldRoot, name), ValueOf(newRoot, name), IsMoney: false));
        }

        return fields;
    }

    /// <summary>Qiymat yozish tomonida yashirilganmi (<c>"***"</c>) — sir, hech qachon ko'rsatilmaydi.</summary>
    /// <param name="field">Maydon.</param>
    public static bool IsWriteMasked(AuditFieldValue field)
    {
        ArgumentNullException.ThrowIfNull(field);
        return IsMaskedValue(field.Old) || IsMaskedValue(field.New);
    }

    /// <summary>Maydonning matnli qiymati (yangisi, bo'lmasa eskisi) — yozuv turini bilish uchun.</summary>
    /// <param name="fields">Qatorning maydonlari.</param>
    /// <param name="name">Maydon nomi (<c>Type</c>).</param>
    public static string? TextOf(IReadOnlyList<AuditFieldValue> fields, string name)
    {
        ArgumentNullException.ThrowIfNull(fields);

        var field = fields.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.Ordinal));
        var value = field?.New ?? field?.Old;

        return value is { ValueKind: JsonValueKind.String } text ? text.GetString() : null;
    }

    /// <summary>Qiymat turi — interfeys qiymatni shunga qarab chizadi (<see cref="AuditValueTypes"/>).</summary>
    /// <param name="entityType">Entity'ning EF turi (<c>null</c> — JSON qiymatidan aniqlanadi).</param>
    /// <param name="rule">Katalog qoidasi (havolalar uchun).</param>
    /// <param name="field">Maydon.</param>
    public static string ValueTypeOf(IReadOnlyEntityType? entityType, AuditEntityRule? rule, AuditFieldValue field)
    {
        ArgumentNullException.ThrowIfNull(field);

        if (field.IsMoney)
        {
            return AuditValueTypes.Money;
        }

        var property = FindProperty(entityType, field.Name);

        if (rule?.RefOf(field.Name) is not null)
        {
            return property is { IsPrimitiveCollection: true } || IsArray(field)
                ? AuditValueTypes.List
                : AuditValueTypes.Ref;
        }

        return property is null ? FromJson(field) : FromProperty(property);
    }

    /// <summary>EF xossasi — nuqtali nom murakkab xossalar orqali topiladi.</summary>
    private static IReadOnlyProperty? FindProperty(IReadOnlyEntityType? entityType, string name)
    {
        if (entityType is null)
        {
            return null;
        }

        IReadOnlyTypeBase current = entityType;
        var parts = name.Split('.');

        for (var index = 0; index < parts.Length - 1; index++)
        {
            if (current.FindComplexProperty(parts[index]) is not { } complex)
            {
                return null;
            }

            current = complex.ComplexType;
        }

        return current.FindProperty(parts[^1]);
    }

    private static string FromProperty(IReadOnlyProperty property)
    {
        var columnType = property.GetColumnType();
        if (string.Equals(columnType, "jsonb", StringComparison.OrdinalIgnoreCase)
            || string.Equals(columnType, "json", StringComparison.OrdinalIgnoreCase))
        {
            return AuditValueTypes.Json;
        }

        if (property.IsPrimitiveCollection)
        {
            return AuditValueTypes.List;
        }

        var clrType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;

        if (clrType.IsEnum)
        {
            return AuditValueTypes.Enum;
        }

        // Qiymat obyekti (telefon, JSHSHIR, pasport) — jurnalda bazadagi ko'rinishi
        // (satr) yotadi, turi ham o'shaniki.
        if (FromClr(clrType) is { } known)
        {
            return known;
        }

        var provider = property.GetValueConverter()?.ProviderClrType;
        return provider is null
            ? AuditValueTypes.Text
            : FromClr(Nullable.GetUnderlyingType(provider) ?? provider) ?? AuditValueTypes.Text;
    }

    private static string? FromClr(Type clrType)
    {
        if (clrType == typeof(string) || clrType == typeof(Guid) || clrType == typeof(byte[])
            || clrType == typeof(TimeOnly) || clrType == typeof(TimeSpan))
        {
            return AuditValueTypes.Text;
        }

        if (clrType == typeof(bool))
        {
            return AuditValueTypes.Bool;
        }

        if (clrType == typeof(DateOnly))
        {
            return AuditValueTypes.Date;
        }

        if (clrType == typeof(DateTimeOffset) || clrType == typeof(DateTime))
        {
            return AuditValueTypes.DateTime;
        }

        if (clrType == typeof(int) || clrType == typeof(long) || clrType == typeof(short) || clrType == typeof(byte)
            || clrType == typeof(decimal) || clrType == typeof(double) || clrType == typeof(float))
        {
            return AuditValueTypes.Number;
        }

        return null;
    }

    /// <summary>EF xossasi topilmasa (eski qator, hodisa tafsiloti) — JSON qiymatining turi.</summary>
    private static string FromJson(AuditFieldValue field)
        => (field.New ?? field.Old)?.ValueKind switch
        {
            JsonValueKind.Number => AuditValueTypes.Number,
            JsonValueKind.True or JsonValueKind.False => AuditValueTypes.Bool,
            JsonValueKind.Array => AuditValueTypes.List,
            JsonValueKind.Object => AuditValueTypes.Json,
            _ => AuditValueTypes.Text,
        };

    private static bool IsArray(AuditFieldValue field)
        => (field.New ?? field.Old)?.ValueKind == JsonValueKind.Array;

    private static bool IsMaskedValue(JsonElement? value)
        => value is { ValueKind: JsonValueKind.String } text
           && string.Equals(text.GetString(), AuditCatalog.MaskedValue, StringComparison.Ordinal);

    /// <summary>
    /// Maydonlar ro'yxati: <c>changed_fields</c> bo'lsa — o'sha (EF tartibi); eski tahrir
    /// qatorida — farq qilgan kalitlar; qolganida (yaratish, o'chirish, hodisa) — hamma kalit.
    /// </summary>
    private static List<string> FieldNames(
        AuditAction action,
        JsonElement? oldRoot,
        JsonElement? newRoot,
        IReadOnlyList<string>? changedFields)
    {
        if (changedFields is { Count: > 0 })
        {
            return [.. changedFields.Distinct(StringComparer.Ordinal)];
        }

        var keys = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var root in new[] { newRoot, oldRoot })
        {
            if (root is not { } element)
            {
                continue;
            }

            foreach (var property in element.EnumerateObject())
            {
                if (seen.Add(property.Name))
                {
                    keys.Add(property.Name);
                }
            }
        }

        // Kengaytirishdan oldingi tahrir: ikkala tomonda TO'LIQ nusxa — o'zgarmaganlar tushadi.
        if (action == AuditAction.Update && oldRoot is not null && newRoot is not null)
        {
            keys.RemoveAll(key =>
            {
                var before = ValueOf(oldRoot, key);
                var after = ValueOf(newRoot, key);

                return before is { } left && after is { } right
                    ? JsonElement.DeepEquals(left, right)
                    : before is null && after is null;
            });
        }

        return keys;
    }

    /// <summary>Kalitning qiymati (nusxa); kalit yo'q yoki JSON <c>null</c> — <c>null</c>.</summary>
    private static JsonElement? ValueOf(JsonElement? root, string name)
        => root is { } element
           && element.TryGetProperty(name, out var value)
           && value.ValueKind != JsonValueKind.Null
            ? value.Clone()
            : null;

    /// <summary><c>Salary.Amount</c> → <c>Salary</c>, agar <c>Salary</c> — <see cref="Money"/> turidagi murakkab xossa.</summary>
    private static string? MoneyPrefix(IReadOnlyEntityType? entityType, string name)
    {
        if (entityType is null)
        {
            return null;
        }

        var dot = name.LastIndexOf('.');
        if (dot <= 0)
        {
            return null;
        }

        var prefix = name[..dot];
        var part = name[(dot + 1)..];

        if (part is not (nameof(Money.Amount) or nameof(Money.Currency)) || prefix.Contains('.', StringComparison.Ordinal))
        {
            return null;
        }

        return entityType.FindComplexProperty(prefix)?.ComplexType.ClrType == typeof(Money) ? prefix : null;
    }

    /// <summary>
    /// <c>X.Amount</c> + <c>X.Currency</c> → bitta qiymat <c>{ "amount": …, "currency": "UZS" }</c>.
    /// </summary>
    /// <remarks>
    /// Tahrirda faqat summa o'zgarsa, valyuta qatorda yo'q (faqat o'zgarganlar yoziladi) —
    /// u holda ikkinchi tomondagi valyuta olinadi, u ham bo'lmasa <c>null</c>. Summasiz
    /// tomon (oklad hali yo'q edi) — <c>null</c>.
    /// </remarks>
    private static AuditFieldValue MergeMoney(string name, JsonElement? oldRoot, JsonElement? newRoot)
    {
        var oldAmount = ValueOf(oldRoot, name + "." + nameof(Money.Amount));
        var oldCurrency = ValueOf(oldRoot, name + "." + nameof(Money.Currency));
        var newAmount = ValueOf(newRoot, name + "." + nameof(Money.Amount));
        var newCurrency = ValueOf(newRoot, name + "." + nameof(Money.Currency));

        return new AuditFieldValue(
            name,
            MoneyValue(oldAmount, oldCurrency ?? newCurrency),
            MoneyValue(newAmount, newCurrency ?? oldCurrency),
            IsMoney: true);
    }

    private static JsonElement? MoneyValue(JsonElement? amount, JsonElement? currency)
    {
        if (amount is null)
        {
            return null;
        }

        // Yashirilgan summa yashirinligicha qoladi — o'qish uni «masked» deb ko'rsatadi.
        if (IsMaskedValue(amount) || IsMaskedValue(currency))
        {
            return JsonSerializer.SerializeToElement(AuditCatalog.MaskedValue);
        }

        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("amount");
            amount.Value.WriteTo(writer);
            writer.WritePropertyName("currency");

            if (currency is { } code)
            {
                code.WriteTo(writer);
            }
            else
            {
                writer.WriteNullValue();
            }

            writer.WriteEndObject();
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }

    /// <summary>JSON obyekt; bo'sh, buzuq yoki obyekt bo'lmagan matn — <c>null</c> (qator baribir chiziladi).</summary>
    private static JsonDocument? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                return document;
            }

            document.Dispose();
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>Qatordagi bitta maydon — hali yashirilmagan, nomlari qo'yilmagan.</summary>
/// <param name="Name">Maydon nomi (pulda — butunligicha, <c>Salary</c>).</param>
/// <param name="Old">Eski qiymat (yo'q yoki JSON <c>null</c> — <c>null</c>).</param>
/// <param name="New">Yangi qiymat.</param>
/// <param name="IsMoney">Pul juftidan birlashtirilgan.</param>
public sealed record AuditFieldValue(string Name, JsonElement? Old, JsonElement? New, bool IsMoney);
