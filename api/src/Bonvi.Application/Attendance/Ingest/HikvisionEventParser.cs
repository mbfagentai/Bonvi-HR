using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Ingest;

/// <summary>
/// Hikvision yuz terminalining «HTTP Listening» hodisasini o'qiydi.
/// </summary>
/// <remarks>
/// <para>
/// Terminal odatda <c>multipart/form-data</c> yuboradi: <c>event_log</c>
/// qismida hodisa JSON'i, keyingi qismda yuz surati (SPEC §3.4.1). Surat
/// o'chirilgan terminal hodisani oddiy <c>application/json</c> tana qilib
/// ham yuborishi mumkin — ikkalasi ham shu yerda o'qiladi, chunki bu BITTA
/// qurilma turining ikki ko'rinishi, «tanani hidlab tahlilchi tanlash» emas.
/// </para>
/// <para>
/// ⭐ <b>Qaysi hodisa o'tish emas.</b> Terminal o'tishdan tashqari eshik
/// holati, signal, buzish urinishi va «yurak urishi» ham yuboradi. Ularni
/// o'tish sifatida saqlash taxtaga soxta odamlarni chiqarardi, 400 bilan
/// rad etish esa terminalni ularni qayta-qayta yuborishga majbur qilardi.
/// Shu sababli ular 200 bilan TASHLANADI (bo'sh ro'yxat). Tashlanadiganlar:
/// </para>
/// <list type="bullet">
///   <item><c>eventType</c> — <c>AccessControllerEvent</c> emas;</item>
///   <item><c>majorEventType</c> berilgan va 5 emas (1 signal, 2 nosozlik,
///         3 amal): Hikvision tasnifida o'tish faqat 5 («hodisa») ichida
///         bo'ladi;</item>
///   <item><c>majorEventType = 5</c> va kichik tur RAD ETILGANLAR oilasida;</item>
///   <item>shaxs raqamisiz hodisa, agar kichik turi tanish O'TISH turi
///         bo'lmasa — eshik ochildi/yopildi kabi hodisalarda shaxs yo'q va
///         ulardan hech kimning davomatini tiklab bo'lmaydi.</item>
/// </list>
/// <para>
/// ⚠️ <b>Kichik tur raqamlari proshivkaga qarab farq qiladi.</b> Shu
/// sababli ruxsat ro'yxati NOMLI va izohli, tanish bo'lmagan tur esa
/// <see cref="AcceptUnknownSubTypes"/> tufayli O'TISH deb qabul qilinadi:
/// yangi proshivka kelgan kuni butun korxonaning davomati jimgina
/// yo'qolgandan ko'ra, bitta ortiqcha hodisa yozilgani yaxshi. Har bir
/// notanish tur logga BIR MARTA tushadi — ro'yxatni to'ldirish uchun.
/// </para>
/// </remarks>
public sealed class HikvisionEventParser : IAttendanceEventParser
{
    /// <summary>Hikvision «hodisa» asosiy turi — o'tishlar faqat shu ichida.</summary>
    public const int MajorEvent = 5;

    /// <summary>
    /// Tanish bo'lmagan kichik tur o'tish deb qabul qilinsinmi.
    /// </summary>
    /// <remarks>
    /// 1-versiyada <c>true</c>: noma'lum proshivka davomatni YOZADI, tashlamaydi.
    /// Rad etilgan turlar oilasi (<see cref="DeniedSubTypes"/>) bundan
    /// qat'i nazar har doim tashlanadi.
    /// </remarks>
    public const bool AcceptUnknownSubTypes = true;

    /// <summary>
    /// O'tish deb tan olinadigan kichik turlar (<c>majorEventType = 5</c>).
    /// </summary>
    /// <remarks>
    /// Raqamlar Hikvision ISAPI «MINOR_*» jadvalidan. ⚠️ SPEC loyihasida
    /// <c>76</c> «barmoq izi» deb yozilgan; Hikvision jadvalida 76 —
    /// <c>MINOR_FACE_VERIFY_FAIL</c> (yuz tasdiqlanMADI), barmoq izi esa 38.
    /// 76 ni o'tish deb olish rad etilgan har bir yuzni «kirdi» qilib
    /// yozardi, shu sababli u <see cref="DeniedSubTypes"/> da.
    /// </remarks>
    private static readonly FrozenSet<int> PassSubTypes = new[]
    {
        1,   // MINOR_LEGAL_CARD_PASS — yaroqli karta
        2,   // MINOR_CARD_AND_PSW_PASS — karta + parol
        38,  // MINOR_FINGERPRINT_COMPARE_PASS — barmoq izi mos keldi
        75,  // MINOR_FACE_VERIFY_PASS — yuz tasdiqlandi
    }.ToFrozenSet();

    /// <summary>
    /// Rad etilgan / muvaffaqiyatsiz urinishlar — har doim tashlanadi.
    /// </summary>
    /// <remarks>
    /// Bular «odam o'tmoqchi bo'ldi, terminal o'tkazmadi». Ular o'tish emas:
    /// ularni yozish ishdan bo'shatilgan odamning har bir rad etilgan
    /// urinishini «kirdi» qilib ko'rsatardi.
    /// </remarks>
    private static readonly FrozenSet<int> DeniedSubTypes = new[]
    {
        3,   // MINOR_CARD_AND_PSW_FAIL
        4,   // MINOR_CARD_AND_PSW_OVER_TIME
        5,   // MINOR_CARD_AND_PSW_TIMEOUT
        6,   // MINOR_CARD_NO_RIGHT — ruxsat yo'q
        7,   // MINOR_CARD_INVALID_PERIOD — muddatdan tashqari
        8,   // MINOR_CARD_OUT_OF_DATE — karta muddati o'tgan
        9,   // MINOR_INVALID_CARD — noma'lum karta
        10,  // MINOR_ANTI_SNEAK_FAIL
        11,  // MINOR_INTERLOCK_DOOR_NOT_CLOSE
        39,  // MINOR_FINGERPRINT_COMPARE_FAIL
        76,  // MINOR_FACE_VERIFY_FAIL — yuz tasdiqlanmadi
    }.ToFrozenSet();

    private readonly IAttendanceIngestJournal _journal;

    /// <summary>Logga allaqachon tushgan notanish turlar — har biri bir marta.</summary>
    /// <remarks>
    /// Jarayon umri davomida o'sadi, lekin chegaralangan: Hikvision kichik
    /// turlari yuzdan kam. Sinf singleton, shu sababli oqimga xavfsiz lug'at.
    /// </remarks>
    private readonly ConcurrentDictionary<(int Major, int Sub), byte> _reportedUnknown = new();

    public HikvisionEventParser(IAttendanceIngestJournal journal) => _journal = journal;

    public AttendanceProvider Provider => AttendanceProvider.Hikvision;

    public Task<Result<IReadOnlyList<RawAttendanceEvent>>> ParseAsync(
        AttendanceIngestPayload payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return Task.FromResult(Parse(payload));
    }

    private Result<IReadOnlyList<RawAttendanceEvent>> Parse(AttendanceIngestPayload payload)
    {
        IngestFilePart? photoPart = null;
        byte[] eventBytes;

        if (payload.IsMultipart)
        {
            var eventPart = FindEventPart(payload.Parts);
            if (eventPart is null)
            {
                return IngestErrors.Unreadable;
            }

            eventBytes = eventPart.Content;
            photoPart = FindPhotoPart(payload.Parts, eventPart);
        }
        else
        {
            eventBytes = payload.Body;
        }

        var json = IngestBytes.WithoutUtf8Bom(eventBytes);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return IngestErrors.Unreadable;
        }

        using (document)
        {
            return Read(document.RootElement, Encoding.UTF8.GetString(json.Span), photoPart);
        }
    }

    private Result<IReadOnlyList<RawAttendanceEvent>> Read(JsonElement root, string rawJson, IngestFilePart? photoPart)
    {
        // Hikvision hodisasining minimal belgisi — `eventType`. Usiz bu
        // boshqa format (masalan Generic JSON) va u shu qurilmaga tegishli
        // emas: tashlash emas, «o'qib bo'lmadi» — aks holda noto'g'ri
        // sozlangan qurilma 200 olib, hech narsa yozilmay qolardi.
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("eventType", out var eventType)
            || eventType.ValueKind != JsonValueKind.String)
        {
            return IngestErrors.Unreadable;
        }

        if (!string.Equals(eventType.GetString(), "AccessControllerEvent", StringComparison.OrdinalIgnoreCase))
        {
            return Nothing();
        }

        if (!root.TryGetProperty("AccessControllerEvent", out var access) || access.ValueKind != JsonValueKind.Object)
        {
            return IngestErrors.Unreadable;
        }

        var major = ReadInt(access, "majorEventType");
        var sub = ReadInt(access, "subEventType");

        if (major is { } majorType && majorType != MajorEvent)
        {
            return Nothing();
        }

        if (sub is { } subType && DeniedSubTypes.Contains(subType))
        {
            return Nothing();
        }

        var knownPass = sub is { } known && PassSubTypes.Contains(known);
        var unknownSubType = sub is not null && !knownPass;

        if (unknownSubType && _reportedUnknown.TryAdd((major ?? MajorEvent, sub!.Value), 0))
        {
            _journal.UnknownSubTypeSeen(major ?? MajorEvent, sub.Value);
        }

        if (unknownSubType && !AcceptUnknownSubTypes)
        {
            return Nothing();
        }

        var personId = ReadText(access, "employeeNoString")
            ?? ReadText(access, "employeeNo")
            ?? ReadText(access, "cardNo");

        if (personId is null)
        {
            // Tanish o'tish turi, lekin shaxs yo'q — bu buzuq hodisa va
            // o'rnatuvchi uni ko'rishi kerak. Turi noma'lum yoki umuman
            // yo'q bo'lsa esa bu eshik holati kabi shaxssiz hodisa.
            return knownPass
                ? Error.Validation("davomat.shaxs_raqami_bosh", "Hodisada shaxs raqami yo'q.")
                : Nothing();
        }

        if (!IngestTime.TryParse(ReadText(root, "dateTime"), out var occurredAt, out var offsetAssumed))
        {
            return IngestErrors.Unreadable;
        }

        var direction = DirectionOf(ReadText(access, "attendanceStatus"));

        var passEvent = new RawAttendanceEvent(
            RawPersonId: personId,
            OccurredAt: occurredAt,
            OffsetAssumed: offsetAssumed,
            Direction: direction,
            DirectionSource: direction == PassDirection.Unknown ? DirectionSource.Unknown : DirectionSource.Device,
            Method: MethodOf(ReadText(access, "currentVerifyMode")),
            ExternalEventId: ReadText(access, "serialNo"),
            PhotoBytes: photoPart?.Content,
            PhotoContentType: photoPart?.ContentType,
            RawEventJson: rawJson);

        return Result.Success<IReadOnlyList<RawAttendanceEvent>>([passEvent]);
    }

    /// <summary>
    /// Hodisa JSON'i turgan qism.
    /// </summary>
    /// <remarks>
    /// Nomi bo'yicha (<c>event_log</c>; ba'zi proshivkalarda
    /// <c>AccessControllerEvent</c>), topilmasa — JSON yoki matn deb e'lon
    /// qilingan birinchi qism. E'lon qilingan tur bu yerda faqat QIDIRISH
    /// uchun: mazmun baribir JSON sifatida tekshiriladi.
    /// </remarks>
    private static IngestFilePart? FindEventPart(IReadOnlyList<IngestFilePart> parts)
        => parts.FirstOrDefault(part => NameIs(part, "event_log") || NameIs(part, "AccessControllerEvent"))
            ?? parts.FirstOrDefault(part => TypeContains(part, "json"))
            ?? parts.FirstOrDefault(part => TypeContains(part, "text/plain"));

    /// <summary>
    /// Yuz surati turgan qism — hodisa qismidan keyingi birinchi bo'sh bo'lmagan qism.
    /// </summary>
    /// <remarks>
    /// Qism nomi proshivkaga qarab o'zgaradi (<c>Picture</c>, suratning fayl
    /// nomi), shu sababli nom bo'yicha tanlanmaydi. <c>subPicture</c> —
    /// qo'shimcha kadr, ataylab e'tiborsiz qoldiriladi. Bu yerda hech narsa
    /// TEKSHIRILMAYDI: haqiqiy rasm ekanini buyruq ishlovchisi baytlarga
    /// qarab aniqlaydi, e'lon qilingan turga qarab emas.
    /// </remarks>
    private static IngestFilePart? FindPhotoPart(IReadOnlyList<IngestFilePart> parts, IngestFilePart eventPart)
        => parts.FirstOrDefault(part =>
            !ReferenceEquals(part, eventPart)
            && part.Content.Length > 0
            && !NameIs(part, "subPicture"));

    /// <summary>
    /// <c>attendanceStatus</c> → yo'nalish.
    /// </summary>
    /// <remarks>
    /// Tanaffus va qo'shimcha ish belgilari ham kirish/chiqish: tabel ularni
    /// oddiy o'tish sifatida juftlaydi, tanaffusni esa jadval oynasi bo'yicha
    /// o'zi hisoblaydi (SPEC §5.5.1). Qolgan hamma narsa — «qurilma aytmadi»,
    /// va yo'nalish qurilmaning rolidan to'ldiriladi (§3.7).
    /// </remarks>
    private static PassDirection DirectionOf(string? status) => status?.ToUpperInvariant() switch
    {
        "CHECKIN" or "BREAKIN" or "OVERTIMEIN" => PassDirection.In,
        "CHECKOUT" or "BREAKOUT" or "OVERTIMEOUT" => PassDirection.Out,
        _ => PassDirection.Unknown,
    };

    /// <summary>
    /// <c>currentVerifyMode</c> → usul.
    /// </summary>
    /// <remarks>
    /// Tartib muhim: <c>cardOrFace</c> kabi aralash rejimda yuz birinchi
    /// tekshiriladi — yuz terminalida odam amalda yuz bilan o'tadi. Bu faqat
    /// ma'lumot uchun, hisobotda ishlatilmaydi.
    /// </remarks>
    private static PassMethod MethodOf(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return PassMethod.Unknown;
        }

        if (mode.Contains("face", StringComparison.OrdinalIgnoreCase))
        {
            return PassMethod.Face;
        }

        if (mode.Contains("card", StringComparison.OrdinalIgnoreCase))
        {
            return PassMethod.Card;
        }

        if (mode.Contains("fp", StringComparison.OrdinalIgnoreCase)
            || mode.Contains("finger", StringComparison.OrdinalIgnoreCase))
        {
            return PassMethod.Fingerprint;
        }

        if (mode.Contains("pw", StringComparison.OrdinalIgnoreCase)
            || mode.Contains("password", StringComparison.OrdinalIgnoreCase))
        {
            return PassMethod.Password;
        }

        return PassMethod.Other;
    }

    private static Result<IReadOnlyList<RawAttendanceEvent>> Nothing()
        => Result.Success<IReadOnlyList<RawAttendanceEvent>>([]);

    private static bool NameIs(IngestFilePart part, string name)
        => string.Equals(part.Name, name, StringComparison.OrdinalIgnoreCase);

    private static bool TypeContains(IngestFilePart part, string fragment)
        => part.ContentType?.Contains(fragment, StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Maydonni matn sifatida o'qiydi — son bo'lib kelsa ham.
    /// </summary>
    /// <remarks>
    /// Hikvision bir maydonni proshivkaga qarab goh satr, goh son qilib
    /// yuboradi (<c>employeeNo</c>, <c>serialNo</c>). Son invariant madaniyatda
    /// matnga aylantiriladi — aks holda «10427» bir serverda «10 427» bo'lib
    /// qolardi va takror kaliti ham, moslik ham buzilardi.
    /// </remarks>
    private static string? ReadText(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value))
        {
            return null;
        }

        var text = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static int? ReadInt(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String
            && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
    }
}
