using System.ComponentModel.DataAnnotations;

namespace Bonvi.DeviceAgent;

/// <summary>
/// Agent sozlamalari — HAMMASI muhitdan (<c>.env</c> → docker-compose).
/// </summary>
/// <remarks>
/// Faylda sukut sir yo'q: terminal paroli va qurilma tokeni git tarixiga
/// tushmasligi kerak (<c>BotOptions</c> dagi bilan bir xil qoida).
/// </remarks>
public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    /// <summary>Bonvi API manzili — <c>https://hr.bonvi.uz</c> yoki ichki manzil.</summary>
    [Required(AllowEmptyStrings = false)]
    public string ApiBaseUrl { get; set; } = "http://api:8080";

    /// <summary>
    /// Qurilmaning qabul tokeni — qurilma kartasida bir marta ko'rsatiladi.
    /// </summary>
    /// <remarks>
    /// Agent Bonvi ga aynan shu token bilan murojaat qiladi: hodisa
    /// yuborish ham, ro'yxat so'rash ham. Ya'ni agentda JWT, login va
    /// parol umuman yo'q — eng yomon holatda ham u bitta terminalning
    /// ro'yxatidan ortig'ini ko'ra olmaydi.
    /// </remarks>
    [Required(AllowEmptyStrings = false)]
    public string DeviceToken { get; set; } = string.Empty;

    /// <summary>Terminalning tarmoqdagi manzili — <c>192.168.1.50</c> yoki <c>host:port</c>.</summary>
    [Required(AllowEmptyStrings = false)]
    public string DeviceHost { get; set; } = string.Empty;

    /// <summary>Terminal administratorining logini.</summary>
    [Required(AllowEmptyStrings = false)]
    public string DeviceUser { get; set; } = "admin";

    /// <summary>Terminal administratorining paroli.</summary>
    /// <remarks>
    /// ⚠️ Noto'g'ri parol bilan urinish terminalda BLOKLASHGA olib keladi
    /// (Hikvision `retryLoginTime` ni kamaytiradi va hisobni qulflaydi).
    /// Shu sababli agent parolni TANLAMAYDI: xato kelsa, halqa xatoni
    /// yozadi va keyingi urinishgacha kutadi.
    /// </remarks>
    [Required(AllowEmptyStrings = false)]
    public string DevicePassword { get; set; } = string.Empty;

    /// <summary>Hodisa halqasining oralig'i (soniya).</summary>
    /// <remarks>
    /// 5 soniya — jonli taxta uchun yetarli va terminal uchun sezilmaydi.
    /// </remarks>
    [Range(2, 120)]
    public int EventsIntervalSeconds { get; set; } = 5;

    /// <summary>Ro'yxat tekshiruvining oralig'i (soniya).</summary>
    /// <remarks>
    /// 60 soniya: yangi xodim yoki almashgan rasm bir daqiqa ichida
    /// terminalga tushadi. Bundan tez-tez so'rashning ma'nosi yo'q —
    /// ro'yxat kunda bir necha marta o'zgaradi.
    /// </remarks>
    [Range(10, 3600)]
    public int RosterIntervalSeconds { get; set; } = 60;

    /// <summary>Holat fayli — hodisa kursori shu yerda.</summary>
    /// <remarks>
    /// Konteyner o'chib yonganda kursor yo'qolmasligi uchun bu yo'l
    /// TOMga (volume) tushishi kerak. Yo'qolsa ham falokat emas: takror
    /// hodisalar serverda <c>ux_attendance_passes_device_dedupe</c> bilan
    /// tashlanadi.
    /// </remarks>
    [Required(AllowEmptyStrings = false)]
    public string StatePath { get; set; } = "/data/agent-state.json";

    /// <summary>Eshik raqami — yozilgan odamga qaysi eshik ochiladi.</summary>
    [Range(1, 32)]
    public int DoorNo { get; set; } = 1;
}
