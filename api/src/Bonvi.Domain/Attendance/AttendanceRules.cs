using Bonvi.Domain.Abstractions;

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

    /// <summary>
    /// Yo'nalishi NOMA'LUM terminalda bir odamning shu soniya ichidagi takror o'qishi — bitta
    /// o'tish (egasining qarori, 2026-10-30).
    /// </summary>
    /// <remarks>
    /// Bitta umumiy terminal (Propan-Rispetto) har kuni 20–26 kishini 3–10 soniya farq bilan
    /// ikki marta o'qirdi va navbat bilan ajratish ularni «kirdi-chiqdi» qilib, kunning qolgan
    /// juftligini siljitardi (kechki chiqish «kirish» bo'lib qolardi). 15 soniyada odam
    /// turniketdan o'tib, qaytib kela olmaydi. Takror o'chirilmaydi — jurnalda qoladi va
    /// <c>ignored_pass_count</c> ga tushadi. Ilgarigi «noma'lum hech narsani yutmaydi» qarori
    /// (2026-09-27) shu oynadan tashqarida amalda qoladi.
    /// </remarks>
    public const int UnknownRepeatSeconds = 15;

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

    /// <summary>
    /// Agent yoki terminal shuncha daqiqa jim qolsa — bu NOSOZLIK (SPEC-agent §L).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Jadvalga bog'liq EMAS.</b> Agent o'tish bor-yo'qligidan qat'i
    /// nazar har daqiqada ro'yxat so'raydi va terminal soatini o'qiydi —
    /// ya'ni tunda ham, dam olish kunida ham o'n daqiqalik jimlik haqiqiy
    /// uzilish. Shu sababli bu chegara faqat AGENT orqali ishlaydigan
    /// qurilmaga qo'llanadi: o'zi push qiladigan terminal hodisa bo'lmasa
    /// umuman gapirmaydi va uning jimligi hech narsa demaydi.
    /// </para>
    /// <para>
    /// O'n daqiqa — o'nta ketma-ket o'tkazib yuborilgan o'lchov. Undan
    /// qisqasi agentning qayta ishga tushishi yoki bir-ikki daqiqalik
    /// tarmoq uzilishida ham qizil ogohlantirish yoqardi.
    /// </para>
    /// </remarks>
    public const int AgentSilenceMinutes = 10;

    /// <summary>
    /// Soati adashgan qurilma shu soat ichida gapirgan bo'lsagina ogohlantirishga tushadi (SPEC §6.5).
    /// </summary>
    /// <remarks>
    /// Bir haftadan beri jim turgan terminalning eski o'lchovi «hozir soat
    /// adashgan» degani emas — u uchun alohida «uzilgan» belgisi bor.
    /// </remarks>
    public const int ClockWarningRecentHours = 24;

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

    /// <summary>
    /// Jadvalsiz (yoki dam olish) kunining kechki KIRISHini ertangi CHIQISH bilan juftlashda
    /// ikkisi orasidagi eng ko'p soat (egasining qarori, 2026-11-04).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Faqat yo'nalishi ma'lum terminallar uchun (alohida kirish va chiqish terminali, masalan
    /// Metan zavod): 19:00 da kirib ertasi 08:00 da chiqqan tungi ishchi — 13 soat. Yo'nalishsiz
    /// terminalda (Propan-Rispetto) bu qoida ishlamaydi: u yerda kirish/chiqish faqat navbat bilan
    /// taxmin qilinadi va bitta tushib qolgan o'qish butun zanjirni teskari qilardi.
    /// </para>
    /// <para>
    /// 16 soat — eng uzun tungi smenadan keyin ham zaxira; undan uzoq oraliq (masalan juma kuni
    /// chiqishni unutib, dushanba chiqqan odam) juftlanmaydi va kun ochiq qoladi.
    /// </para>
    /// </remarks>
    public const int OvernightExitMaxHours = 16;

    /// <summary>
    /// Jadvalsiz kunda chiqishi yozilmagan odam shu soatda (Toshkent) «ketdi» deb yopilardi
    /// (egasining qarori, 2026-10-12) — ⚠️ 2026-11-03 dan BEKOR: hisoblash endi yopmaydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Qoida 2026-10-12..11-03 da hisoblangan va hali qayta hisoblanmagan kunlarda saqlangan
    /// natija sifatida qoladi — shuning uchun «avto» belgisini taniydigan metodlar
    /// (<see cref="IsNoScheduleAutoExit"/>) ekran va Excel uchun saqlanadi.
    /// </para>
    /// <para>
    /// Grafik biriktirilmagan xodimning kuni — standart 24 soat (kalendar kuni). U terminaldan
    /// o'tmay ketib qolsa, kun ochiq qolmaydi: oxirgi ochiq kirish 22:00 da yopiladi va shu
    /// oraliq ishlangan vaqtga qo'shiladi (<c>AttendanceDayBuilder</c>). O'tishga hech narsa
    /// yozilmaydi — yopilish faqat hisoblashda yashaydi, ya'ni keyin kelgan haqiqiy chiqish
    /// yoki biriktirilgan jadval uni o'z-o'zidan bekor qiladi.
    /// </para>
    /// <para>
    /// ⚠️ 22:00 dan KEYIN ochilgan oraliq yopilmaydi (chiqish yozilmagan bo'lib qoladi), va
    /// jadvali bor kunga bu qoida umuman tegmaydi — u yerda reja bor.
    /// </para>
    /// </remarks>
    public static readonly TimeOnly NoScheduleAutoExitTime = new(22, 0);

    /// <summary>Shu sananing avtomatik «ketdi» lahzasi (UTC) — <see cref="NoScheduleAutoExitTime"/> Toshkent vaqtida.</summary>
    /// <param name="date">Toshkent sanasi.</param>
    public static DateTimeOffset NoScheduleAutoExitAt(DateOnly date)
        => new DateTimeOffset(date.ToDateTime(NoScheduleAutoExitTime), BonviTime.TashkentOffset).ToUniversalTime();

    /// <summary>Kunning oxirgi chiqishi avtomatik yopilishmi — faqat KO'RSATISH uchun («avto» belgisi).</summary>
    /// <remarks>
    /// Belgi saqlanmaydi: yopilish lahzasi aniq 22:00:00 bo'lgani uchun u shundan taniladi.
    /// Roppa-rosa 22:00:00 dagi haqiqiy chiqish ham «avto» ko'rinadi — vaqti baribir o'sha.
    /// </remarks>
    /// <param name="date">Kun.</param>
    /// <param name="lastOutAt">Kunning oxirgi chiqishi.</param>
    public static bool IsNoScheduleAutoExit(DateOnly date, DateTimeOffset? lastOutAt)
        => lastOutAt is { } at && at == NoScheduleAutoExitAt(date);

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
    /// Jadval so'rovidagi tanaffus oralig'ining boshi (kun daqiqasi) —
    /// <c>attendance.requestBreakFrom</c> ning SUKUT qiymati: 12:00.
    /// </summary>
    /// <remarks>
    /// To'rttala son birga — <see cref="RequestBreakRule"/> (SPEC-sorov-tahriri §2.2, egasining
    /// qarori 2026-10-20): tanaffus 12:00–15:00 ichida, 60 daqiqa, jumada 120 gacha.
    /// </remarks>
    public const int DefaultRequestBreakFromMinute = 12 * 60;

    /// <summary>Tanaffus oralig'ining oxiri (kun daqiqasi) — <c>attendance.requestBreakTo</c> sukuti: 15:00.</summary>
    public const int DefaultRequestBreakToMinute = 15 * 60;

    /// <summary>Talab qilingan tanaffus (daqiqa) — <c>attendance.requestBreakMinutes</c> sukuti; jumada eng kami ham shu.</summary>
    public const int DefaultRequestBreakMinutes = 60;

    /// <summary>Jumadagi eng uzun tanaffus (daqiqa) — <c>attendance.requestBreakFridayMaxMinutes</c> sukuti.</summary>
    public const int DefaultRequestBreakFridayMaxMinutes = 120;

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
