using System.Text.Json;
using System.Text.Json.Nodes;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Ingest;

/// <summary>
/// Oddiy JSON hodisalarni o'qiydi — vositachi agent, ZKTeco moslamasi va testlar uchun.
/// </summary>
/// <remarks>
/// <para>
/// Shakl (SPEC §3.4.2): <c>{ "events": [ { personId, occurredAt, direction,
/// method, eventId, photoBase64, minor } ] }</c> (<c>minor</c> — ixtiyoriy,
/// 2026-09-27 dan). <c>events</c> o'ramisiz bitta
/// obyekt ham qabul qilinadi va bitta elementli ro'yxat deb qaraladi —
/// Postel qonuni QURILMALARGA nisbatan qo'llanadi, bizga emas, va bu uch
/// qatorga tushadi. Ildizdagi yalang massiv esa ATAYLAB qabul qilinmaydi:
/// shartnomada yo'q va uni qo'shish har bir kelajakdagi adapterni o'ziga
/// qulay shaklni tanlashga undardi.
/// </para>
/// <para>
/// ⚠️ Multipart tana bu tahlilchida — «o'qib bo'lmadi». Tahlilchi qurilma
/// yozuvidan tanlanadi; <c>Generic</c> qurilmaning multipart yuborishi
/// noto'g'ri sozlangan qurilma, taxmin qilinadigan narsa emas (§3.4.3).
/// </para>
/// <para>
/// Hodisalar soni bu yerda TEKSHIRILMAYDI: chegara barcha tahlilchilar uchun
/// bitta va uni buyruq ishlovchisi qo'llaydi (<see cref="IngestLimits.MaxEventsPerRequest"/>).
/// </para>
/// </remarks>
public sealed class JsonEventParser : IAttendanceEventParser
{
    public AttendanceProvider Provider => AttendanceProvider.Generic;

    public Task<Result<IReadOnlyList<RawAttendanceEvent>>> ParseAsync(
        AttendanceIngestPayload payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return Task.FromResult(Parse(payload));
    }

    private static Result<IReadOnlyList<RawAttendanceEvent>> Parse(AttendanceIngestPayload payload)
    {
        if (payload.IsMultipart)
        {
            return IngestErrors.Unreadable;
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(IngestBytes.WithoutUtf8Bom(payload.Body).Span);
        }
        catch (JsonException)
        {
            return IngestErrors.Unreadable;
        }

        if (root is not JsonObject rootObject)
        {
            return IngestErrors.Unreadable;
        }

        JsonArray items;

        if (rootObject.TryGetPropertyValue("events", out var eventsNode))
        {
            // `events` bor, lekin massiv emas (yoki `null`) — bu shartnomaning
            // buzilishi, «hodisa yo'q» emas.
            if (eventsNode is not JsonArray array)
            {
                return IngestErrors.Unreadable;
            }

            items = array;
        }
        else
        {
            // O'ramisiz bitta hodisa. Ildiz tugunining ota-onasi yo'q, ya'ni
            // uni nusxasiz massivga qo'yish mumkin.
            items = new JsonArray(rootObject);
        }

        var events = new List<RawAttendanceEvent>(items.Count);

        foreach (var item in items)
        {
            if (item is not JsonObject eventObject)
            {
                return IngestErrors.Unreadable;
            }

            // Rad etilgan urinish — o'tish EMAS va tashlanadi (`ReadMinor` izohi).
            if (HikvisionEventTypes.IsDenied(ReadMinor(eventObject)))
            {
                continue;
            }

            var parsed = ReadEvent(eventObject);
            if (parsed.IsFailure)
            {
                return parsed.Error;
            }

            events.Add(parsed.Value);
        }

        return Result.Success<IReadOnlyList<RawAttendanceEvent>>(events);
    }

    private static Result<RawAttendanceEvent> ReadEvent(JsonObject item)
    {
        var personId = ReadText(item, "personId");
        if (personId is null)
        {
            return Error.Validation("davomat.shaxs_raqami_bosh", "Hodisada shaxs raqami yo'q.");
        }

        if (!IngestTime.TryParse(ReadText(item, "occurredAt"), out var occurredAt, out var offsetAssumed))
        {
            return IngestErrors.Unreadable;
        }

        if (!TryReadEnum(item, "direction", PassDirection.Unknown, out var direction)
            || !TryReadEnum(item, "method", PassMethod.Unknown, out var method))
        {
            return IngestErrors.Unreadable;
        }

        var photo = ReadPhoto(item);

        // Surat `raw_event` ga TUSHMAYDI: u yerda megabaytlik base64 satr
        // `jsonb` ni shishirardi va surat baribir alohida faylda turadi.
        var rawEvent = item.DeepClone().AsObject();
        rawEvent.Remove("photoBase64");

        return new RawAttendanceEvent(
            RawPersonId: personId,
            OccurredAt: occurredAt,
            OffsetAssumed: offsetAssumed,
            Direction: direction,
            DirectionSource: direction == PassDirection.Unknown ? DirectionSource.Unknown : DirectionSource.Device,
            Method: method,
            ExternalEventId: ReadText(item, "eventId"),
            PhotoBytes: photo,
            PhotoContentType: null,
            RawEventJson: rawEvent.ToJsonString());
    }

    /// <summary>
    /// <c>photoBase64</c> ni baytlarga ochadi.
    /// </summary>
    /// <remarks>
    /// Yaroqsiz base64 — hodisa xatosi EMAS: surat tashlanadi, o'tish baribir
    /// yoziladi (SPEC §3.10). Suratning haqiqiy rasm ekanini keyin buyruq
    /// ishlovchisi baytlarga qarab tekshiradi.
    /// </remarks>
    private static byte[]? ReadPhoto(JsonObject item)
    {
        var encoded = ReadText(item, "photoBase64");
        if (encoded is null)
        {
            return null;
        }

        try
        {
            return Convert.FromBase64String(encoded);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Sanoq qiymatini NOMI bo'yicha o'qiydi.
    /// </summary>
    /// <remarks>
    /// Sanoqlar tizimda son emas, nom bilan yuradi (<c>Program.cs</c> dagi
    /// <c>JsonStringEnumConverter</c> izohi). <c>Enum.TryParse</c> esa «1»
    /// kabi sonni ham, «In,Out» kabi bayroqlar yig'indisini ham jimgina qabul
    /// qiladi (ikkinchisi <c>3</c> = <c>Unknown</c> bo'lib chiqardi) — shu
    /// sababli raqam bilan boshlanadigan va vergulli qiymat alohida rad
    /// etiladi. Maydon yo'q yoki <c>null</c> ⇒ <paramref name="fallback"/>.
    /// </remarks>
    private static bool TryReadEnum<TEnum>(JsonObject item, string name, TEnum fallback, out TEnum value)
        where TEnum : struct, Enum
    {
        value = fallback;

        var node = item[name];
        if (node is null)
        {
            return true;
        }

        if (node.GetValueKind() != JsonValueKind.String)
        {
            return false;
        }

        var text = node.GetValue<string>().Trim();

        return text.Length > 0
            && !char.IsAsciiDigit(text[0])
            && text[0] != '-'
            && !text.Contains(',', StringComparison.Ordinal)
            && Enum.TryParse(text, ignoreCase: true, out value)
            && Enum.IsDefined(value);
    }

    /// <summary>
    /// Ixtiyoriy <c>minor</c> — Hikvision hodisasining kichik turi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Qurilma agenti terminal jurnalidan o'qigan kichik turni shu maydonda
    /// uzatadi. Agent rad etilgan urinishlarni (ruxsat yo'q, muddatdan
    /// tashqari, yuz tasdiqlanmadi…) o'zi ham tashlaydi; bu yerda ikkinchi
    /// marta tekshiriladi — ro'yxati eski agent yoki boshqa vositachi ularni
    /// yuborsa ham, o'tish sifatida yozilmasin (SPEC §3.4.1, §3.4.2).
    /// </para>
    /// <para>
    /// Maydon yo'q yoki son emas — <c>null</c>, ya'ni hodisa odatdagidek
    /// o'qiladi: <c>minor</c> ni bilmaydigan manba (ZKTeco moslamasi,
    /// testlar) shartnomadan tashqariga chiqmaydi.
    /// </para>
    /// </remarks>
    private static int? ReadMinor(JsonObject item)
        => item["minor"] is JsonValue value
            && value.GetValueKind() == JsonValueKind.Number
            && value.TryGetValue<int>(out var minor)
                ? minor
                : null;

    /// <summary>Matn yoki sonni matn sifatida o'qiydi (<c>personId: 10427</c> ham bo'lishi mumkin).</summary>
    private static string? ReadText(JsonObject item, string name)
    {
        var node = item[name];
        if (node is null)
        {
            return null;
        }

        var text = node.GetValueKind() switch
        {
            JsonValueKind.String => node.GetValue<string>(),
            JsonValueKind.Number => node.ToJsonString(),
            _ => null,
        };

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}
