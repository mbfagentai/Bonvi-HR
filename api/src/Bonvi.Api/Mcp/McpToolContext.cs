using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Bonvi.Api.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Infrastructure.Mcp;
using Bonvi.Infrastructure.Persistence;
using ModelContextProtocol;

namespace Bonvi.Api.Mcp;

/// <summary>Foydalanuvchida bo'lim ruxsati yo'q. Jurnalga <c>denied</c> bo'lib tushadi.</summary>
public sealed class McpAccessDeniedException(string message) : McpException(message);

/// <summary>
/// MCP vositasi tegishi mumkin bo'lgan HAMMA narsa: kim so'rayapti, O'QISH
/// bazasi, ruxsat tekshiruvi, chegaralar va chiqish (<c>docs/SPEC-mcp.md</c> §3, §7).
/// </summary>
/// <remarks>
/// <para>
/// Vositalar boshqa hech narsa olmaydi — xususan so'rovning o'qish-yozish
/// <see cref="AppDbContext"/> ini HECH QACHON: <see cref="Db"/> —
/// <c>bonvi_mcp</c> roli bilan ochilgan kontekst. Refleksiya testi har bir
/// vosita turining konstruktori faqat shu sinfni qabul qilishini tekshiradi.
/// </para>
/// <para>
/// <b>So'rov yozishda qoida:</b> har doim <c>Select(...)</c> bilan PROYEKSIYA.
/// Butun entity'ni yuklash (<c>ToListAsync()</c> to'g'ridan-to'g'ri) yopiq
/// ustunlarni ham so'raydi va PostgreSQL <c>42501</c> bilan rad etadi — bu
/// xato emas, himoya.
/// </para>
/// </remarks>
public sealed partial class McpToolContext(IHttpContextAccessor http, McpDatabase database) : IAsyncDisposable
{
    /// <summary>Ro'yxatning eng katta sahifasi.</summary>
    public const int MaxPageSize = 200;

    public const int DefaultPageSize = 50;

    /// <summary>Sana oralig'ining eng katta uzunligi.</summary>
    public const int MaxRangeDays = 366;

    private const string StateKey = "Bonvi.Mcp.CallState";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        // O'zbek va kirill harflari o'qiladigan bo'lib qolsin.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private AppDbContext? _db;

    /// <summary>Chaqiruv holati — jurnal filtri bilan bo'lishiladi (<c>HttpContext.Items</c> da).</summary>
    public sealed class CallState
    {
        public bool Denied { get; set; }

        public int? RowCount { get; set; }
    }

    /// <summary>O'qish konteksti (<c>bonvi_mcp</c>, kuzatuvsiz, yozish rad etiladi). Kerak bo'lganda ochiladi.</summary>
    public AppDbContext Db => _db ??= database.CreateReadOnlyContext();

    public ClaimsPrincipal User => http.HttpContext?.User ?? new ClaimsPrincipal();

    /// <summary>Bugungi sana — Toshkent vaqtida.</summary>
    public static DateOnly Today => DateTimeOffset.UtcNow.TashkentDate();

    public static CallState StateOf(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Items[StateKey] is CallState state)
        {
            return state;
        }

        state = new CallState();
        context.Items[StateKey] = state;
        return state;
    }

    private CallState State => http.HttpContext is { } context ? StateOf(context) : new CallState();

    public ValueTask DisposeAsync() => _db?.DisposeAsync() ?? ValueTask.CompletedTask;

    /// <summary>Foydalanuvchining rolida shu ruxsat bormi.</summary>
    public bool Has(string permission) => User.HasClaim(CurrentUser.PermissionClaimType, permission);

    /// <summary>Ruxsat bo'lmasa — rad etadi (jurnalda <c>denied</c>).</summary>
    /// <param name="permission">Ruxsat kodi, masalan <c>employees.view</c>.</param>
    /// <param name="area">Foydalanuvchiga tushunarli bo'lim nomi, masalan «Xodimlar».</param>
    public void Require(string permission, string area)
    {
        if (Has(permission))
        {
            return;
        }

        State.Denied = true;
        throw new McpAccessDeniedException(
            $"Ruxsat yo'q: bu ma'lumot «{area}» bo'limiga tegishli, sizning rolingizda u ochilmagan.");
    }

    public static int Page(int? page) => Math.Max(1, page ?? 1);

    public static int PageSize(int? size) => Math.Clamp(size ?? DefaultPageSize, 1, MaxPageSize);

    /// <summary><c>YYYY-MM-DD</c> ni o'qiydi; bo'sh — <paramref name="fallback"/>.</summary>
    public static DateOnly Date(string? value, DateOnly fallback, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        throw new McpException($"«{name}» sanasi YYYY-MM-DD ko'rinishida bo'lsin (masalan 2026-09-01).");
    }

    /// <summary>Tekshirilgan oraliq, ko'pi bilan <paramref name="maxDays"/> kun.</summary>
    public static (DateOnly From, DateOnly To) Range(string? from, string? to, int defaultDays = 30, int maxDays = MaxRangeDays)
    {
        var end = Date(to, Today, "to");
        var start = Date(from, end.AddDays(-(defaultDays - 1)), "from");

        if (start > end)
        {
            throw new McpException("«from» sanasi «to» dan keyin bo'lishi mumkin emas.");
        }

        if (end.DayNumber - start.DayNumber + 1 > maxDays)
        {
            throw new McpException($"Sana oralig'i ko'pi bilan {maxDays} kun bo'lishi mumkin.");
        }

        return (start, end);
    }

    /// <summary>Lahzani Toshkent vaqtida ISO ko'rinishga keltiradi (<c>2026-10-01T14:30</c>).</summary>
    public static string? Local(DateTimeOffset? instant) =>
        instant?.ToTashkent().ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Sana <c>YYYY-MM-DD</c>.</summary>
    public static string? Iso(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    // ---------------------------------------------------------------------
    //  Chiqish
    // ---------------------------------------------------------------------

    /// <summary>
    /// Vosita natijasini JSON qiladi. <paramref name="rows"/> — jurnaldagi qatorlar soni.
    /// </summary>
    /// <remarks>
    /// Chuqur himoya: nomi sir, pasport yoki faylga o'xshagan HAR QANDAY xossa
    /// chiqishdan olib tashlanadi — u qanday yo'l bilan kelib qolgan bo'lmasin.
    /// Asosiy himoya baribir bazada (yopiq ustunlar), bu ikkinchi qatlam.
    /// </remarks>
    public string Json(object payload, int? rows = null)
    {
        State.RowCount = rows;

        var node = JsonSerializer.SerializeToNode(payload, JsonOptions);
        Scrub(node);
        return node?.ToJsonString(JsonOptions) ?? "null";
    }

    /// <summary>Hech qachon serverdan chiqmaydigan xossa nomlari (katta-kichik harf farqsiz).</summary>
    public static bool IsForbiddenName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var n = name.ToLowerInvariant().Replace("_", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);

        return n is "pin" or "otp" or "login"
            || n.Contains("password", StringComparison.Ordinal)
            || n.Contains("passwd", StringComparison.Ordinal)
            || n.Contains("hash", StringComparison.Ordinal)
            || n.Contains("salt", StringComparison.Ordinal)
            || n.Contains("token", StringComparison.Ordinal)
            || n.Contains("secret", StringComparison.Ordinal)
            || n.Contains("apikey", StringComparison.Ordinal)
            || n.Contains("credential", StringComparison.Ordinal)
            || n.Contains("passport", StringComparison.Ordinal)
            || n.Contains("pasport", StringComparison.Ordinal)
            || n.Contains("pinfl", StringComparison.Ordinal)
            || n.Contains("jshshir", StringComparison.Ordinal)
            || n.Contains("filekey", StringComparison.Ordinal)
            || n.Contains("filename", StringComparison.Ordinal)
            || n.Contains("photokey", StringComparison.Ordinal)
            || n.Contains("screenshot", StringComparison.Ordinal)
            || n.Contains("storagekey", StringComparison.Ordinal);
    }

    private static void Scrub(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(pair => pair.Key).ToList())
                {
                    if (IsForbiddenName(key))
                    {
                        obj.Remove(key);
                    }
                    else if (Redacted(obj[key]) is { } text)
                    {
                        obj[key] = text;
                    }
                    else
                    {
                        Scrub(obj[key]);
                    }
                }

                break;

            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    if (Redacted(array[i]) is { } text)
                    {
                        array[i] = text;
                    }
                    else
                    {
                        Scrub(array[i]);
                    }
                }

                break;
        }
    }

    /// <summary>
    /// Matn qiymatidagi JSHSHIR (14 raqam) va pasport raqamiga (ikki bosh harf +
    /// 7 raqam) o'xshagan bo'laklarni yashiradi; o'zgarish bo'lmasa <c>null</c>.
    /// </summary>
    /// <remarks>
    /// Erkin matn (izoh, xulosa, anketa) egasining qarori bilan OCHIQ, lekin HR
    /// u yerga pasport raqamini yozib qo'ygan bo'lishi mumkin — hujjat izohi
    /// aynan shu sababdan yopilgan. Bu uchinchi qatlam: ustun emas, QIYMAT
    /// bo'yicha (xavfsizlik tekshiruvi, 2026-10-01).
    /// </remarks>
    private static string? Redacted(JsonNode? node)
    {
        if (node is not JsonValue value || !value.TryGetValue(out string? text) || string.IsNullOrEmpty(text))
        {
            return null;
        }

        var cleaned = PassportLike().Replace(PinflLike().Replace(text, Hidden), Hidden);
        return ReferenceEquals(cleaned, text) || cleaned == text ? null : cleaned;
    }

    private const string Hidden = "[yashirilgan]";

    [System.Text.RegularExpressions.GeneratedRegex(@"(?<!\d)\d{14}(?!\d)", System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex PinflLike();

    [System.Text.RegularExpressions.GeneratedRegex(@"(?<![\p{L}\d])[A-ZА-ЯЎҚҒҲ]{2}\s?\d{7}(?!\d)", System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex PassportLike();
}
