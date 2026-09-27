namespace Bonvi.Domain.Attendance;

/// <summary>
/// Davomat modulining o'lchov chegaralari — bitta joyda.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega hammasi bitta joyda.</b> Bu sonlar ikki tomondan ishlatiladi:
/// qabul qilish yo'li (skew, tana hajmi) va tabel hisoblash (kun oynasi,
/// sakrash). Ular ajralib qolsa, qabul qilingan o'tish hisoblashga
/// tushmay, jimgina yo'qolardi — <c>NotificationRules</c> dagi bilan
/// AYNAN bir xil mulohaza.
/// </para>
/// <para>
/// ⚠️ <b>Nega bulardan faqat BITTASI sozlama.</b> Sozlamaga chiqarilgani —
/// rasm saqlash muddati (<see cref="DefaultPhotoRetentionDays"/>), chunki
/// egasi aynan shuni so'ragan. Qolganlari konstanta: har bir sozlama
/// sozlamalar ekranidagi qator, ta'rif, tekshiruv oralig'i va TO'RTTA
/// tarjima degani, va ularning birortasi so'ralmagan. Keyinchalik
/// kerak bo'lsa, konstantani sozlamaga ko'chirish bir qatorlik ish —
/// <c>NotificationRules</c> da aynan shunday qilingan: konstanta sukut
/// qiymat bo'lib qoladi.
/// </para>
/// </remarks>
public static class AttendanceRules
{
    /// <summary>Telefondagi ikki belgi orasidagi eng kam vaqt (soniya).</summary>
    /// <remarks>
    /// <para>
    /// Turniket oldida odam ikkilanadi, terminal yuzni ikki marta ushlaydi,
    /// telefonda tugma ikki marta bosiladi. Xodim ilovasi shu oraliqdagi
    /// ikkinchi belgini qabul qilmaydi (<c>davomat.takroriy_belgi</c>).
    /// </para>
    /// <para>
    /// ⚠️ <b>Kun hisoblashi bu son bilan ISHLAMAYDI</b> (egasining qarori,
    /// 2026-09-27). Yo'nalishi MA'LUM ketma-ket takror (ikki kirish yoki ikki
    /// chiqish) oralig'idan qat'i nazar bitta voqea: birinchi kirish, oxirgi
    /// chiqish (<c>PassPairing</c>). Yo'nalishi NOMA'LUM o'qishlar (bitta
    /// umumiy terminal) esa hech qachon birlashtirilmaydi — navbat bilan
    /// kirish/chiqish. Tashlangan takror o'chirilmaydi: jurnalda qoladi va kun
    /// yozuvida <c>ignored_pass_count</c> ga tushadi.
    /// </para>
    /// </remarks>
    public const int BounceSeconds = 60;

    /// <summary>Qurilma soati shu chegaragacha og'sa — normal holat.</summary>
    /// <remarks>
    /// Besh daqiqa — terminal NTP'siz bir necha oyda to'playdigan tabiiy
    /// og'ish. Undan kattasi qurilma kartasida qizil belgi bo'lib
    /// chiqadi, lekin ma'lumot BARIBIR ishlatiladi: besh daqiqa oldinda
    /// yurgan terminal ham kim o'tganini to'g'ri aytyapti, uni tashlab
    /// yuborish odamni soat nosozligi uchun «kelmagan» ro'yxatiga
    /// qo'yardi.
    /// </remarks>
    public const int ClockSkewToleranceSeconds = 300;

    /// <summary>Bundan uzoq og'ish — og'ish emas, buzuq qurilma: o'tish RAD ETILADI.</summary>
    /// <remarks>
    /// 1970 yoki 2106 yilni da'vo qilayotgan terminal sekinlashgan emas,
    /// ishdan chiqqan. Unga yozishga ruxsat berilsa, <c>attendance_days</c>
    /// yozuvlari ma'nosiz sanalarga sochilib ketardi va ularni topib
    /// tozalash qo'lda ish bo'lardi.
    /// </remarks>
    public const int MaxClockSkewDays = 30;

    /// <summary>O'tish rejadagi boshlanishdan shuncha soat OLDIN ham shu kunga tegishli.</summary>
    /// <remarks>
    /// Olti soat — erta kelishni qoplaydi, lekin soat 14:00 dagi o'tishni
    /// kechagi tungi smena o'ziniki deb da'vo qila olmaydigan darajada
    /// tor.
    /// </remarks>
    public const int DayWindowPreHours = 6;

    /// <summary>O'tish rejadagi tugashdan shuncha soat KEYIN ham shu kunga tegishli.</summary>
    public const int DayWindowPostHours = 6;

    /// <summary>«Hozir binoda» taxtasi shu oraliqdagi o'tishlarga qaraydi.</summary>
    /// <remarks>
    /// 18 soatdan eski kirish endi «ichkarida» degani emas: odam chiqishda
    /// turniketni aylanib o'tgan yoki chiqish yozilmagan. Cheklovsiz
    /// taxta bir hafta ichida butun korxonani «binoda» deb ko'rsatib
    /// turardi.
    /// </remarks>
    public const int BoardLookbackHours = 18;

    /// <summary>Kelishdagi sukut yon berish (daqiqa).</summary>
    public const int DefaultGraceInMinutes = 10;

    /// <summary>Ketishdagi sukut yon berish (daqiqa).</summary>
    public const int DefaultGraceOutMinutes = 10;

    /// <summary>
    /// Yuz surati necha kun saqlanadi — <c>attendance.photoRetentionDays</c>
    /// sozlamasining SUKUT qiymati.
    /// </summary>
    /// <remarks>
    /// ⚠️ Bu son diskning o'lchami: kuniga ~5 400 o'tish × ~40 KB ≈ 216 MB,
    /// 60 kunda ≈ 13 GB. Tozalash vazifasi ixtiyoriy EMAS — usiz hajm
    /// yiliga ~80 GB o'sadi va VPS diski to'ladi.
    /// </remarks>
    public const int DefaultPhotoRetentionDays = 60;

    /// <summary>
    /// Xom o'tish yozuvi necha kun saqlanadi — <c>attendance.passRetentionDays</c>
    /// sozlamasining SUKUT qiymati (egasining tanlovi: 1 yil).
    /// </summary>
    /// <remarks>
    /// Faqat xom dalil eskiradi: hisoblangan kun (<see cref="AttendanceDay"/>)
    /// bu muddat bilan HECH QACHON o'chirilmaydi, ya'ni eski tabel va
    /// hisobotlar yo'qolmaydi. Sozlama yozuvi bazada bo'lmasa, tozalash
    /// vazifasi shu songa qaytadi.
    /// </remarks>
    public const int DefaultPassRetentionDays = 365;

    /// <summary>
    /// Ishdan ketgan xodimning o'tishi jonli taxtada necha soat qizil
    /// ogohlantirish bo'lib turadi (SPEC §6.5).
    /// </summary>
    /// <remarks>
    /// Bu HR ning reaksiya vaqti: bir kecha-kunduz ichida qurilmadan
    /// o'chirilmagan odam ertasiga yana o'tsa, ogohlantirish yangi o'tish
    /// bilan qayta yonadi. Uzoqroq muddat taxtani eski, allaqachon hal
    /// qilingan holatlar bilan to'ldirib, yangisini ko'rinmas qilardi.
    /// </remarks>
    public const int FormerEmployeeAlertHours = 24;

    /// <summary>Mosliklarni ko'chirishda bitta fayldan qabul qilinadigan eng ko'p qator.</summary>
    public const int MaxImportRows = 5000;

    /// <summary>Bitta qo'lda qayta hisoblash so'rovi qamrab oladigan eng ko'p kun.</summary>
    /// <remarks>
    /// Bir chorak. Undan kattasi bitta so'rovda yuz minglab qatorni qayta
    /// yozardi va uni to'xtatishning yo'li yo'q.
    /// </remarks>
    public const int MaxRecomputeDays = 92;

    /// <summary>Qabul qilish endpointi uchun so'rov tanasining chegarasi (bayt).</summary>
    /// <remarks>
    /// 2 MB. Yuz surati 20–80 KB atrofida, ya'ni chegara ~25 barobar
    /// zaxira bilan turibdi va faqat noto'g'ri sozlangan qurilmani
    /// (yoki suiiste'molni) to'sadi.
    /// </remarks>
    public const int MaxIngestBodyBytes = 2_097_152;
}
