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
    /// 2 soniya — jonli kadr (<see cref="SnapshotChannel"/>) o'tgan odamni
    /// hali terminal oldida ushlashi uchun. So'rov tor oraliqli va
    /// terminal uchun sezilmaydi (0.1 soniyadan kam).
    /// </remarks>
    [Range(1, 120)]
    public int EventsIntervalSeconds { get; set; } = 2;

    /// <summary>
    /// Jonli kadr olinadigan kanal — bo'sh bo'lsa kadr olinmaydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Nega jonli kadr, terminalning o'z surati emas.</b> O'tish
    /// suratini terminal faqat <c>saveCapPic</c>/<c>uploadCapPic</c> yoqilganda
    /// saqlaydi — bu BUTUN qurilmaning sozlamasi va u Verifix'ga
    /// yuboriladigan hodisalarni ham o'zgartirardi. Egasining sharti:
    /// Verifix uzilib qolmasin. Jonli kadr esa oddiy o'qish so'rovi —
    /// qurilma sozlamasiga tegmaydi.
    /// </para>
    /// <para>
    /// MinMoe'da kanal <c>101</c> (2026-09-21 da tekshirilgan: <c>1</c> —
    /// 404, <c>101</c> — 79 KB JPEG, 0.07 soniya).
    /// </para>
    /// </remarks>
    public string SnapshotChannel { get; set; } = "101";

    /// <summary>
    /// Hodisa shundan eski bo'lsa, kadr BIRIKTIRILMAYDI (soniya).
    /// </summary>
    /// <remarks>
    /// ⚠️ Kadr hodisa o'qilgan lahzada olinadi, o'tish lahzasida emas. Eski
    /// hodisaga (agent uzilib qaytganda) kadr biriktirilsa, unda boshqa
    /// odam yoki bo'sh xona bo'lardi — noto'g'ri surat suratsizlikdan
    /// yomonroq. Shu sababli faqat yangi hodisalar kadr oladi.
    /// </remarks>
    [Range(1, 60)]
    public int SnapshotMaxAgeSeconds { get; set; } = 6;

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

    /// <summary>
    /// Holat fayli YO'Q bo'lganda (yangi o'rnatish, yo'qolgan tom) terminal
    /// jurnalidan necha soat orqaga o'qiladi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 48 soat — dam olish kunlarini qamraydi: juma kechqurun o'chgan va
    /// dushanba ertalab yangidan o'rnatilgan agent o'rtadagi o'tishlarni
    /// ham olib keladi. Ilgari bir soat edi va undan eskisi jimgina
    /// yo'qolardi.
    /// </para>
    /// <para>
    /// Qayta yuborish ZARARSIZ: qabul kaliti terminal raqami va vaqtidan
    /// yasaladi (<c>minmoe-{raqam}-{vaqt}</c>) va server takrorni o'zi
    /// tashlaydi. Kattaroq qiymat faqat birinchi o'qishni uzaytiradi.
    /// </para>
    /// </remarks>
    [Range(1, 24 * 30)]
    public int ColdStartHours { get; set; } = 48;
}
