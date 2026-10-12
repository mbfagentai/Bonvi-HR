using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Bonvi.Infrastructure.Persistence.Auditing;

/// <summary>
/// Bitta kuzatilayotgan yozuvdan audit o'zgarishini o'qiydi: amal, o'zgargan maydonlar va
/// eski/yangi qiymatlar (SPEC-rollar-va-audit §7.2).
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><b>Yaratish</b> — bo'sh bo'lmagan, tashlab ketilmagan HAMMA maydon («yangi»).</item>
///   <item><b>Tahrir</b> — faqat haqiqatan o'zgarganlari (EF taqqoslagichi bilan); ro'yxat
///         bo'sh qolsa (masalan faqat «oxirgi kirish» yangilangan) — qator UMUMAN yo'q.</item>
///   <item><b>O'chirish</b> (yumshoq ham) — oxirgi holat («eski»), maydonlar ro'yxatisiz.</item>
/// </list>
/// <para>
/// Murakkab xossalar nuqtali nom bilan yoyiladi (<c>Salary.Amount</c>,
/// <c>FullName.LastName</c>). Birlamchi kalit qiymatlarga yozilmaydi — u
/// <c>entity_id</c> da turadi va hech qachon o'zgarmaydi.
/// </para>
/// </remarks>
internal static class AuditEntryReader
{
    /// <summary>O'zgarishni o'qiydi; yozadigan narsa bo'lmasa <c>null</c>.</summary>
    /// <param name="entry">Kuzatuvdagi yozuv (holati Added/Modified/Deleted).</param>
    /// <param name="rule">Shu turning qoidasi.</param>
    public static CapturedChange? Read(EntityEntry entry, AuditEntityRule rule)
    {
        var action = ActionOf(entry);
        if (action is null)
        {
            return null;
        }

        var keyNames = entry.Metadata.FindPrimaryKey()?.Properties.Select(key => key.Name).ToHashSet(StringComparer.Ordinal)
            ?? [];

        var fields = Fields(entry)
            .Where(field => !keyNames.Contains(field.Name) && !rule.IsExcluded(field.Name))
            .ToList();

        return action.Value switch
        {
            AuditAction.Create => Created(entry, rule, fields),
            AuditAction.Delete => Deleted(entry, rule, fields),
            _ => Updated(entry, rule, fields),
        };
    }

    /// <summary>Yozuvning identifikatori matn ko'rinishida.</summary>
    /// <param name="entry">Kuzatuvdagi yozuv.</param>
    public static string? EntityIdOf(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key is null)
        {
            return null;
        }

        var values = key.Properties
            .Select(property => Convert.ToString(entry.Property(property.Name).CurrentValue, CultureInfo.InvariantCulture));

        return string.Join(',', values);
    }

    /// <summary>Yozuvdagi <see cref="Guid"/> maydon — o'chirishda oxirgi (asl) qiymati.</summary>
    /// <param name="entry">Kuzatuvdagi yozuv.</param>
    /// <param name="propertyName">Maydon nomi.</param>
    /// <param name="original">Asl qiymat olinsinmi.</param>
    public static Guid? ReadGuid(EntityEntry entry, string propertyName, bool original)
    {
        var property = entry.Property(propertyName);
        var value = original ? property.OriginalValue : property.CurrentValue;

        return value is Guid id && id != Guid.Empty ? id : null;
    }

    private static AuditAction? ActionOf(EntityEntry entry) => entry.State switch
    {
        EntityState.Added => AuditAction.Create,
        EntityState.Deleted => AuditAction.Delete,
        EntityState.Modified when IsSoftDelete(entry) => AuditAction.Delete,
        EntityState.Modified => AuditAction.Update,
        _ => null,
    };

    /// <summary>
    /// <c>AuditableInterceptor</c> <c>Delete</c> ni <c>Modified</c> + <c>DeletedAt</c> ga aylantirgan —
    /// bu jurnal uchun o'chirish, tahrir emas.
    /// </summary>
    private static bool IsSoftDelete(EntityEntry entry)
    {
        if (entry.Entity is not ISoftDeletable)
        {
            return false;
        }

        var deletedAt = entry.Property(nameof(ISoftDeletable.DeletedAt));
        return deletedAt.OriginalValue is null && deletedAt.CurrentValue is not null;
    }

    private static CapturedChange Created(EntityEntry entry, AuditEntityRule rule, List<AuditField> fields)
    {
        var written = fields.Where(field => field.Entry.CurrentValue is not null).ToList();

        var json = Json(writer =>
        {
            foreach (var field in written)
            {
                WriteField(writer, entry, rule, field, field.Entry.CurrentValue);
            }
        });

        return new CapturedChange(
            entry,
            rule,
            AuditAction.Create,
            UsesOriginalValues: false,
            [.. written.Select(field => field.Name)],
            OldValues: null,
            NewValues: json);
    }

    private static CapturedChange Deleted(EntityEntry entry, AuditEntityRule rule, List<AuditField> fields)
    {
        var json = Json(writer =>
        {
            foreach (var field in fields.Where(field => field.Entry.OriginalValue is not null))
            {
                WriteField(writer, entry, rule, field, field.Entry.OriginalValue);
            }
        });

        return new CapturedChange(
            entry,
            rule,
            AuditAction.Delete,
            UsesOriginalValues: true,
            ChangedFields: null,
            OldValues: json,
            NewValues: null);
    }

    private static CapturedChange? Updated(EntityEntry entry, AuditEntityRule rule, List<AuditField> fields)
    {
        var changed = fields
            .Where(field => field.Entry.IsModified
                && !field.Entry.Metadata.GetValueComparer().Equals(field.Entry.OriginalValue, field.Entry.CurrentValue))
            .ToList();

        if (changed.Count == 0)
        {
            return null;
        }

        var oldJson = Json(writer =>
        {
            foreach (var field in changed)
            {
                WriteField(writer, entry, rule, field, field.Entry.OriginalValue);
            }
        });

        var newJson = Json(writer =>
        {
            foreach (var field in changed)
            {
                WriteField(writer, entry, rule, field, field.Entry.CurrentValue);
            }
        });

        return new CapturedChange(
            entry,
            rule,
            AuditAction.Update,
            UsesOriginalValues: false,
            [.. changed.Select(field => field.Name)],
            oldJson,
            newJson);
    }

    /// <summary>Yashirilgan maydonning bo'sh bo'lmagan qiymati — <see cref="AuditCatalog.MaskedValue"/>.</summary>
    private static void WriteField(Utf8JsonWriter writer, EntityEntry entry, AuditEntityRule rule, AuditField field, object? value)
    {
        writer.WritePropertyName(field.Name);

        if (value is not null && rule.IsMasked(entry.Entity, field.Name))
        {
            writer.WriteStringValue(AuditCatalog.MaskedValue);
            return;
        }

        AuditValueSerializer.WriteValue(writer, field.Entry.Metadata, value);
    }

    private static string Json(Action<Utf8JsonWriter> body)
    {
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer, AuditValueSerializer.WriterOptions))
        {
            writer.WriteStartObject();
            body(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>Oddiy va murakkab (yoyilgan) maydonlar — EF tartibida.</summary>
    private static IEnumerable<AuditField> Fields(EntityEntry entry)
    {
        foreach (var property in entry.Properties)
        {
            yield return new AuditField(property.Metadata.Name, property);
        }

        foreach (var complex in entry.ComplexProperties)
        {
            foreach (var nested in Flatten(complex, complex.Metadata.Name))
            {
                yield return nested;
            }
        }
    }

    private static IEnumerable<AuditField> Flatten(ComplexPropertyEntry complex, string prefix)
    {
        foreach (var property in complex.Properties)
        {
            yield return new AuditField(prefix + "." + property.Metadata.Name, property);
        }

        foreach (var inner in complex.ComplexProperties)
        {
            foreach (var nested in Flatten(inner, prefix + "." + inner.Metadata.Name))
            {
                yield return nested;
            }
        }
    }

    private sealed record AuditField(string Name, PropertyEntry Entry);
}

/// <summary>O'qilgan, hali qatorga aylanmagan bitta o'zgarish.</summary>
/// <param name="Entry">Kuzatuvdagi yozuv — xodim va yorliq kalitlari shundan o'qiladi.</param>
/// <param name="Rule">Turning qoidasi.</param>
/// <param name="Action">Amal.</param>
/// <param name="UsesOriginalValues">Kalitlar asl qiymatdan o'qilsinmi (o'chirishda).</param>
/// <param name="ChangedFields">O'zgargan maydonlar; o'chirishda <c>null</c>.</param>
/// <param name="OldValues">Eski qiymatlar (JSON).</param>
/// <param name="NewValues">Yangi qiymatlar (JSON).</param>
internal sealed record CapturedChange(
    EntityEntry Entry,
    AuditEntityRule Rule,
    AuditAction Action,
    bool UsesOriginalValues,
    IReadOnlyList<string>? ChangedFields,
    string? OldValues,
    string? NewValues)
{
    /// <summary>Entity nomi — CLR turi (<c>Employee</c>, <c>EmploymentEvent</c>).</summary>
    public string EntityName => Entry.Metadata.ClrType.Name;

    /// <summary>Kalit maydonining qiymati.</summary>
    /// <param name="propertyName">Maydon nomi.</param>
    public Guid? ReadGuid(string? propertyName)
        => propertyName is null ? null : AuditEntryReader.ReadGuid(Entry, propertyName, UsesOriginalValues);
}
