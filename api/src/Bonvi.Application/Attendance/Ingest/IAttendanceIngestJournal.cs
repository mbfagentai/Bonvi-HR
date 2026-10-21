namespace Bonvi.Application.Attendance.Ingest;

/// <summary>Autentifikatsiya qaysi bosqichda yiqilgani — FAQAT log uchun.</summary>
/// <remarks>
/// Bu qiymat HTTP javobiga hech qachon tushmaydi: tashqariga to'rttasi ham
/// bir xil <c>davomat.ingest_ruxsat_yoq</c> bo'lib chiqadi (sabab
/// <see cref="IngestErrors.Unauthorized"/> izohida).
/// </remarks>
public enum IngestAuthFailure
{
    /// <summary>Token umuman berilmagan (yo'lda ham, sarlavhada ham).</summary>
    MissingToken = 1,

    /// <summary>Bunday token xeshi bilan qurilma yo'q (yoki o'chirilgan).</summary>
    UnknownToken = 2,

    /// <summary>Imzo rejimidagi qurilma: imzo yo'q yoki mos kelmadi.</summary>
    BadSignature = 3,

    /// <summary>
    /// Imzo siri bazada bor, lekin uni ochib bo'lmadi.
    /// </summary>
    /// <remarks>
    /// Tashqaridan <see cref="BadSignature"/> dan farqsiz, lekin logda
    /// ATAYLAB alohida: bu hujum emas, shifrlash kaliti almashtirilgani
    /// (<c>ISecretProtector.Unprotect</c> izohi). Ajratilmasa, kalit
    /// almashgan kuni barcha imzoli qurilmalar «birdaniga buzildi» bo'lib
    /// ko'rinardi va sabab qidirish soatlab cho'zilardi.
    /// </remarks>
    SecretUnreadable = 4,

    /// <summary>So'rov manzili qurilmaning IP ro'yxatiga kirmaydi.</summary>
    SourceNotAllowed = 5,
}

/// <summary>
/// Qabul yo'lining diagnostika jurnali — port.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega port, <c>ILogger</c> emas.</b> Application qatlami hozirgacha
/// hech qayerda log yozmaydi va loglash kutubxonasiga bog'lanmagan. Qabul
/// yo'lida esa log — xavfsizlik talabi: 401 ning HAQIQIY sababi faqat shu
/// yerda qoladi (SPEC §3.2). Log yozuvi so'rov usuli, TOKENSIZ yo'l va
/// manba IP ni o'z ichiga olishi kerak — bular HTTP tushunchalari, ya'ni
/// amalga oshirish Api qatlamida (<c>AttendanceIngestJournal</c>), xuddi
/// <c>ICurrentUser</c> kabi.
/// </para>
/// <para>
/// ⚠️ Metodlarga shaxs raqami, xodim nomi yoki to'liq token BERILMAYDI.
/// Token uchun faqat birinchi 8 belgi — HR ekranda ko'radigan prefiks.
/// </para>
/// </remarks>
public interface IAttendanceIngestJournal
{
    /// <summary>Autentifikatsiya rad etildi.</summary>
    /// <param name="reason">Haqiqiy sabab.</param>
    /// <param name="tokenPrefix">Taqdim etilgan tokenning birinchi belgilari; token bo'lmasa <c>null</c>.</param>
    /// <param name="sourceIp">So'rov manzili.</param>
    void AuthenticationFailed(IngestAuthFailure reason, string? tokenPrefix, string? sourceIp);

    /// <summary>Yaroqli token, lekin qurilma faolsiz.</summary>
    /// <param name="deviceId">Qurilma.</param>
    void DeviceInactive(Guid deviceId);

    /// <summary>Qurilma tasdiqlandi, lekin tanasi rad etildi.</summary>
    /// <param name="deviceId">Qurilma.</param>
    /// <param name="errorCode">Javobda qaytgan xato kodi.</param>
    void PayloadRejected(Guid deviceId, string errorCode);

    /// <summary>Hodisa vaqti ishonib bo'lmaydigan darajada uzoq — rad etildi.</summary>
    /// <param name="deviceId">Qurilma.</param>
    /// <param name="claimedAt">Qurilma DA'VO qilgan vaqt — soati qanchalik adashganini ko'rish uchun.</param>
    void ClockRejected(Guid deviceId, DateTimeOffset claimedAt);

    /// <summary>
    /// Terminal push'idagi 30 kundan eski hodisa yozilmadi, lekin qabul qilindi — terminal
    /// eski jurnalini yuboryapti va xatoda o'sha hodisani qayta yuborardi.
    /// </summary>
    /// <param name="deviceId">Qurilma.</param>
    /// <param name="claimedAt">Hodisa vaqti.</param>
    void StaleBacklogSkipped(Guid deviceId, DateTimeOffset claimedAt);

    /// <summary>Hikvision'ning tanish bo'lmagan hodisa kichik turi BIRINCHI marta ko'rildi.</summary>
    /// <param name="majorEventType">Asosiy tur (<c>majorEventType</c>).</param>
    /// <param name="subEventType">Kichik tur (<c>subEventType</c>).</param>
    void UnknownSubTypeSeen(int majorEventType, int subEventType);

    /// <summary>Agent boshqa terminalning seriyasi bilan keldi — so'rov rad etildi.</summary>
    /// <param name="deviceId">Qurilma yozuvi.</param>
    /// <param name="presentedSerial">Agent o'qigan seriya; berilmagan bo'lsa <c>null</c>.</param>
    void TerminalMismatch(Guid deviceId, string? presentedSerial);
}
