using System.Collections;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Bonvi.Infrastructure.Persistence.Auditing;

/// <summary>
/// Audit qatoridagi qiymatlarni JSON'ga yozadi — bir xil shaklda, madaniyatga bog'liq bo'lmagan holda
/// (SPEC-rollar-va-audit §7.5).
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>sanoq → a'zo NOMI (bazada son bo'lsa ham: raqam o'qiydiganga hech narsa aytmaydi
///         va sanoqqa o'rtadan qiymat qo'shilsa tarix jimgina siljirdi);</item>
///   <item><see cref="DateOnly"/> → <c>yyyy-MM-dd</c>; <see cref="DateTimeOffset"/> → ISO-8601 UTC;
///         <see cref="TimeOnly"/> → <c>HH:mm:ss</c>;</item>
///   <item>son, <see cref="bool"/> → JSON son/mantiqiy; <see cref="Guid"/> → satr;</item>
///   <item>qiymat o'giruvchili xossa (telefon, JSHSHIR, pasport) → bazaga yoziladigan ko'rinishi;</item>
///   <item><c>jsonb</c> ustun → JSON obyekt sifatida ichiga (satr bo'lib emas);</item>
///   <item>oddiy ro'yxatlar (<c>RoleIds</c>, <c>Permissions</c>) → JSON massiv;</item>
///   <item><c>byte[]</c> → hech qachon: <see cref="AuditCatalog.MaskedValue"/>;</item>
///   <item>satrlar TO'LIQ — qisqartirish tarixni yo'qotadi.</item>
/// </list>
/// </remarks>
public static class AuditValueSerializer
{
    /// <summary>
    /// Kirill va lotin matni o'qilarli bo'lib qolsin (<c>А</c> emas). Natija HTML'ga
    /// qo'yilmaydi — u bazada yotadi va o'qish tomoni uni JSON sifatida tahlil qiladi.
    /// </summary>
    internal static JsonWriterOptions WriterOptions { get; } = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Bitta xossaning qiymatini yozadi.</summary>
    /// <param name="writer">Ochiq JSON yozuvchisi (xossa nomi allaqachon yozilgan).</param>
    /// <param name="property">EF xossasi — o'giruvchi va ustun turi shundan olinadi.</param>
    /// <param name="value">CLR qiymat.</param>
    public static void WriteValue(Utf8JsonWriter writer, IReadOnlyProperty property, object? value)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(property);

        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        if (value is byte[])
        {
            writer.WriteStringValue(AuditCatalog.MaskedValue);
            return;
        }

        if (value is Enum member)
        {
            writer.WriteStringValue(member.ToString());
            return;
        }

        var converter = property.GetValueConverter() ?? property.GetTypeMapping().Converter;
        var stored = converter is null ? value : converter.ConvertToProvider(value);

        if (stored is string text && IsJsonColumn(property))
        {
            WriteEmbeddedJson(writer, text);
            return;
        }

        WritePlain(writer, stored);
    }

    /// <summary>Oddiy qiymat — xossa metama'lumotisiz (masalan ro'yxat elementi).</summary>
    /// <param name="writer">JSON yozuvchisi.</param>
    /// <param name="value">Qiymat.</param>
    public static void WritePlain(Utf8JsonWriter writer, object? value)
    {
        ArgumentNullException.ThrowIfNull(writer);

        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case string text:
                writer.WriteStringValue(text);
                break;
            case bool flag:
                writer.WriteBooleanValue(flag);
                break;
            case Guid id:
                writer.WriteStringValue(id.ToString("D", CultureInfo.InvariantCulture));
                break;
            case Enum member:
                writer.WriteStringValue(member.ToString());
                break;
            case DateOnly date:
                writer.WriteStringValue(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                break;
            case TimeOnly time:
                writer.WriteStringValue(time.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
                break;
            case DateTimeOffset moment:
                writer.WriteStringValue(moment.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                break;
            case DateTime moment:
                writer.WriteStringValue(moment.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                break;
            case decimal number:
                writer.WriteNumberValue(number);
                break;
            case int number:
                writer.WriteNumberValue(number);
                break;
            case long number:
                writer.WriteNumberValue(number);
                break;
            case short number:
                writer.WriteNumberValue(number);
                break;
            case double number:
                writer.WriteNumberValue(number);
                break;
            case float number:
                writer.WriteNumberValue(number);
                break;
            case byte[]:
                writer.WriteStringValue(AuditCatalog.MaskedValue);
                break;
            case IEnumerable items:
                writer.WriteStartArray();
                foreach (var item in items)
                {
                    WritePlain(writer, item);
                }

                writer.WriteEndArray();
                break;
            default:
                writer.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
        }
    }

    private static bool IsJsonColumn(IReadOnlyProperty property)
        => string.Equals(property.GetColumnType(), "jsonb", StringComparison.OrdinalIgnoreCase)
           || string.Equals(property.GetColumnType(), "json", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// <c>jsonb</c> ustunning matni — obyekt bo'lib ichiga. Buzuq matn (nazariy holat)
    /// satr bo'lib yoziladi: audit hech qachon saqlashni yiqitmasligi kerak.
    /// </summary>
    private static void WriteEmbeddedJson(Utf8JsonWriter writer, string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            document.RootElement.WriteTo(writer);
        }
        catch (JsonException)
        {
            writer.WriteStringValue(text);
        }
    }
}
