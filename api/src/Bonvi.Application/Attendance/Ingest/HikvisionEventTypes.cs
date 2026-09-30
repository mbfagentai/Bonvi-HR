using System.Collections.Frozen;

namespace Bonvi.Application.Attendance.Ingest;

/// <summary>
/// Hikvision hodisa turlari — qaysi biri o'tish, qaysi biri rad etilgan urinish.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>YAGONA MANBA — server VA agent uchun.</b> Bu fayl ikki joyda
/// kompilyatsiya qilinadi: <c>Bonvi.Application</c> da (terminal o'zi push
/// qilganda <c>HikvisionEventParser</c>, agent yuborgan <c>minor</c>
/// maydonida <c>JsonEventParser</c>) va
/// <c>Bonvi.DeviceAgent</c> da — u yerda loyiha havolasi emas, BOG'LANGAN
/// fayl (<c>&lt;Compile Include … Link=…&gt;</c>), chunki agent server
/// loyihalariga havola qilmaydi. Ro'yxat ikki joyda yozilsa, bir kuni
/// ajralib ketardi: agent rad etilgan urinishni o'tish deb yuborar,
/// server esa uni tashlamas edi (yoki aksincha).
/// </para>
/// <para>
/// Shu sababli bu yerda faqat oddiy <c>System</c> turlari va izohlarda
/// <c>see cref</c> ham yo'q — agentda <c>Bonvi.Domain</c> ham,
/// <c>Bonvi.Application</c> ning boshqa turlari ham yo'q.
/// </para>
/// </remarks>
internal static class HikvisionEventTypes
{
    /// <summary>Hikvision «hodisa» asosiy turi — o'tishlar faqat shu ichida.</summary>
    public const int MajorEvent = 5;

    /// <summary>
    /// O'tish deb tan olinadigan kichik turlar (<c>major = 5</c>).
    /// </summary>
    /// <remarks>
    /// Raqamlar Hikvision ISAPI «MINOR_*» jadvalidan. ⚠️ SPEC loyihasida
    /// <c>76</c> «barmoq izi» deb yozilgan; Hikvision jadvalida 76 —
    /// <c>MINOR_FACE_VERIFY_FAIL</c> (yuz tasdiqlanMADI), barmoq izi esa 38.
    /// 76 ni o'tish deb olish rad etilgan har bir yuzni «kirdi» qilib
    /// yozardi, shu sababli u <see cref="DeniedSubTypes"/> da.
    /// </remarks>
    public static readonly FrozenSet<int> PassSubTypes = new[]
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
    /// urinishini «kirdi» qilib ko'rsatardi — va navbat bilan kirish/chiqish
    /// ajratilganda (SPEC-agent H8) kunning qolgan hamma o'tishini teskari
    /// yo'nalishga ag'darardi.
    /// </remarks>
    public static readonly FrozenSet<int> DeniedSubTypes = new[]
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

    /// <summary>Kichik tur rad etilgan urinishlar oilasidanmi.</summary>
    /// <param name="minor">Kichik tur; <c>null</c> — noma'lum, ya'ni rad etilgan EMAS.</param>
    public static bool IsDenied(int? minor) => minor is { } value && DeniedSubTypes.Contains(value);
}
