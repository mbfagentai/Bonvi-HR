namespace Bonvi.Domain.Attendance;

/// <summary>Qurilma hodisasini qaysi TAHLILCHI (parser) o'qiydi.</summary>
/// <remarks>
/// <para>
/// Tahlilchi hodisa TANASINI emas, QURILMA yozuvini ko'rib tanlanadi.
/// Tanani hidlab tanlash chaqiruvchiga tahlilchini o'zi tanlash imkonini
/// berardi — tahlilchi esa hujum yuzasi.
/// </para>
/// <para>
/// Yangi ishlab chiqaruvchi qo'shish — shu yerga bitta qiymat va bitta
/// sinf: endpoint, buyruq ishlovchisi, sxema va interfeys tegilmaydi.
/// </para>
/// </remarks>
public enum AttendanceProvider
{
    /// <summary>Hikvision yuz terminali — «HTTP Listening» orqali multipart yuboradi.</summary>
    Hikvision = 1,

    /// <summary>Oddiy JSON — vositachi agent, ZKTeco moslamasi va barcha testlar.</summary>
    Generic = 2,
}

/// <summary>
/// Qurilma hodisasidagi shaxs raqami QAYSI raqamlar fazosiga tegishli.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega <see cref="AttendanceProvider"/> dan AYRIM enum.</b> Bittasi
/// «qanday o'qiladi», ikkinchisi «kimning raqamlari» degan savolga javob
/// beradi va ular mustaqil o'zgaradi: Verifix ro'yxatidan to'ldirilgan
/// ZKTeco terminaliga YANGI tahlilchi kerak, lekin u MAVJUD mosliklarga
/// tushishi shart. Ikkalasi bitta enumga qo'shilganida har bir xodimni
/// ikkinchi marta bog'lashga to'g'ri kelardi.
/// </para>
/// </remarks>
public enum AttendanceIdentityProvider
{
    /// <summary>Verifix provisioning qilgan raqamlar — bugungi barcha terminallar shu yerda.</summary>
    Verifix = 1,

    /// <summary>Hikvision terminalining o'z ro'yxati.</summary>
    Hikvision = 2,

    /// <summary>ZKTeco terminalining o'z ro'yxati.</summary>
    ZkTeco = 3,

    /// <summary>Qo'lda kiritilgan raqamlar fazosi (sinov va tuzatish uchun).</summary>
    Manual = 4,
}

/// <summary>Qurilma qaysi yo'nalishni bildiradi.</summary>
/// <remarks>
/// Qurilma yo'nalishni O'ZI aytsa (<see cref="ReportsDirection"/>) unga
/// ishoniladi; aytmasa, o'rnatuvchi shu yerdan «bu turniket faqat kirish»
/// deb belgilaydi. Uchinchi yo'l yo'q — taxmin qilish o'rniga
/// <see cref="PassDirection.Unknown"/> yoziladi va tabel hisoblashda
/// navbat bilan ajratadi.
/// </remarks>
public enum DeviceDirectionRole
{
    /// <summary>Yo'nalish hodisaning o'zida keladi.</summary>
    ReportsDirection = 1,

    /// <summary>Faqat kirish turnigeti.</summary>
    EntryOnly = 2,

    /// <summary>Faqat chiqish turnigeti.</summary>
    ExitOnly = 3,
}

/// <summary>Qurilma qanday tasdiqlanadi.</summary>
/// <remarks>
/// <para>
/// Hikvision proshivkasi so'rov tanasini imzolay OLMAYDI, shu sababli
/// terminallar <see cref="Token"/> rejimida qoladi.
/// <see cref="TokenAndHmac"/> bugun ishlatilmaydi va ataylab shunday:
/// vositachi agent yoki ZKTeco moslamasi qo'shilgan kunda imzoni yoqish
/// SOZLAMA bo'lishi kerak, qayta loyihalash emas.
/// </para>
/// </remarks>
public enum DeviceAuthMode
{
    /// <summary>Faqat token (yo'lda yoki sarlavhada).</summary>
    Token = 1,

    /// <summary>Token + tananing HMAC-SHA256 imzosi.</summary>
    TokenAndHmac = 2,
}

/// <summary>Moslik qayerdan paydo bo'lgan.</summary>
public enum IdentitySource
{
    /// <summary>Excel/CSV ro'yxatidan ko'chirilgan.</summary>
    Import = 1,

    /// <summary>HR qo'lda bog'lagan.</summary>
    Manual = 2,
}

/// <summary>O'tishning yo'nalishi.</summary>
/// <remarks>
/// <see cref="Unknown"/> — to'liq huquqli qiymat, «ma'lumot yo'q» emas:
/// qurilma yo'nalishni bermasa, o'tish SHU holida saqlanadi va tabel uni
/// navbat bilan kirish/chiqishga ajratadi. Ajratish natijasi o'tishga
/// QAYTA YOZILMAYDI — aks holda jadval tuzatilib qayta hisoblanganda
/// tarix o'zgarib ketardi.
/// </remarks>
public enum PassDirection
{
    /// <summary>Kirish.</summary>
    In = 1,

    /// <summary>Chiqish.</summary>
    Out = 2,

    /// <summary>Noma'lum — qurilma aytmadi va roli ham yordam bermadi.</summary>
    Unknown = 3,
}

/// <summary>Yo'nalish QAYERDAN olingan.</summary>
/// <remarks>
/// Saqlanadi, chunki «qurilma aytdi» va «biz uning rolidan xulosa qildik»
/// tortishuvda bir xil emas: ikkinchisi rol noto'g'ri qo'yilgan bo'lsa
/// xato bo'lishi mumkin va qurilma kartasidagi ogohlantirish aynan shu
/// ustundan hisoblanadi.
/// </remarks>
public enum DirectionSource
{
    /// <summary>Hodisaning o'zida kelgan.</summary>
    Device = 1,

    /// <summary>Qurilmaning rolidan kelib chiqqan.</summary>
    DeviceRole = 2,

    /// <summary>Aniqlanmadi.</summary>
    Unknown = 3,
}

/// <summary>Shaxs qanday tasdiqlangan.</summary>
/// <remarks>
/// Faqat ma'lumot uchun: hisobotda ishlatilmaydi, lekin o'tishlar
/// jurnalida «yuz emas, karta bilan o'tibdi» degan savol tez-tez
/// beriladi va javobi shu ustunda turadi.
/// </remarks>
public enum PassMethod
{
    /// <summary>Yuz.</summary>
    Face = 1,

    /// <summary>Karta.</summary>
    Card = 2,

    /// <summary>Barmoq izi.</summary>
    Fingerprint = 3,

    /// <summary>Parol / PIN.</summary>
    Password = 4,

    /// <summary>Boshqa — qurilma aytdi, lekin bizga tanish emas.</summary>
    Other = 5,

    /// <summary>Qurilma umuman aytmadi.</summary>
    Unknown = 6,
}

/// <summary>Ish jadvalining turi.</summary>
/// <remarks>
/// <para>
/// v1 da faqat <see cref="Weekly"/> ishlaydi.
/// <see cref="Cycle"/> — ATAYLAB qoldirilgan chok: 2/2 kabi aylanma
/// jadvallar haftalik naqsh bilan ifodalanmaydi, lekin ularni qo'shish
/// uchun yangi jadval ham, ma'lumot ko'chirish ham kerak bo'lmasligi
/// kerak. Bugun <c>WorkSchedule.Create</c> bu turni rad etadi.
/// </para>
/// </remarks>
public enum WorkScheduleKind
{
    /// <summary>Haftalik naqsh — 7 kun, dushanbadan yakshanbagacha.</summary>
    Weekly = 1,

    /// <summary>Aylanma (2/2 va h.k.) — hali amalga oshirilmagan.</summary>
    Cycle = 2,
}

/// <summary>Jadval biriktirmasi KIMGA tegishli.</summary>
/// <remarks>
/// <para>
/// To'rt pog'ona, ustuvorligi <see cref="Employee"/> → <see cref="Position"/>
/// → <see cref="Department"/> → <see cref="Default"/>.
/// </para>
/// <para>
/// <b>Nega lavozim bo'limdan ustun.</b> Qorovul qaysi bo'limga
/// biriktirilganidan qat'i nazar qorovul smenasida ishlaydi: bo'lim —
/// hisobot chizig'i, lavozim esa odam AYNAN nima qilishi. Teskarisi
/// har bir qorovulga ofis 9–18 jadvalini berardi.
/// </para>
/// </remarks>
public enum ScheduleScope
{
    /// <summary>Aniq xodim — eng kuchli pog'ona.</summary>
    Employee = 1,

    /// <summary>Lavozim.</summary>
    Position = 2,

    /// <summary>Bo'lim (va uning ostidagilar — eng yaqin ota-bo'lim yutadi).</summary>
    Department = 3,

    /// <summary>Tizim bo'yicha sukut jadval.</summary>
    Default = 4,
}

/// <summary>Hisoblangan kunning holati.</summary>
/// <remarks>
/// <para>
/// Tartib — <b>ustuvorlik tartibi emas</b>: holat SPEC §5.6 dagi
/// birinchi mos keluvchi qoida bo'yicha tanlanadi.
/// </para>
/// <para>
/// ⚠️ <see cref="DayOff"/> va <see cref="NotEmployed"/> bazaga deyarli
/// hech qachon tushmaydi: ular uchun yozuv umuman yaratilmaydi (yozuv
/// faqat o'tish bo'lgan yoki ish kuni kutilgan sana uchun ochiladi).
/// Ularni tabel so'rovi bo'sh kataklar o'rniga HOSIL QILADI — enumda
/// turgani shu sababli.
/// </para>
/// <para>
/// Kechikish va erta ketish SON sifatida alohida saqlanadi, holat emas:
/// odam bir kunda ham kechikib kelib, ham erta ketib, ham
/// <see cref="Present"/> bo'lishi mumkin.
/// </para>
/// </remarks>
public enum AttendanceDayStatus
{
    /// <summary>Kelgan — qolgan hamma holat inkor qilingandan keyin.</summary>
    Present = 1,

    /// <summary>Ish kuni kutilgan, birorta o'tish yo'q.</summary>
    Absent = 2,

    /// <summary>Jadval bo'yicha dam olish kuni (tabel so'rovi hosil qiladi).</summary>
    DayOff = 3,

    /// <summary>Dam olish kunida ishlagan.</summary>
    DayOffWorked = 4,

    /// <summary>Oraliq ochiq qoldi — chiqish yozilmagan.</summary>
    MissingExit = 5,

    /// <summary>Yetim chiqish topildi — kirish yozilmagan.</summary>
    MissingEntry = 6,

    /// <summary>Jadval topilmadi; ishlangan vaqt baribir hisoblanadi.</summary>
    NoSchedule = 7,

    /// <summary>
    /// Bu sanada xodimning amaldagi mehnat munosabati yo'q.
    /// </summary>
    /// <remarks>
    /// Yozuv bor holida bu — AYNAN HR ko'rishi kerak bo'lgan anomaliya:
    /// ishdan ketgan odamning propuski hamon ishlayapti.
    /// </remarks>
    NotEmployed = 8,

    /// <summary>
    /// Sababli — kelmagan, lekin tasdiqlangan so'rovi bor
    /// (SPEC-so'rovlar §B3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ Bu holat FAQAT <see cref="Absent"/> ning o'rnini bosadi. Odam
    /// kelgan kun (hatto kechikib kelgan bo'lsa ham) «Sababli» bo'lmaydi:
    /// so'rov kelmaslikni oqlaydi, kelishni emas. Shu qoida tufayli
    /// tasdiqlangan so'rov ishlangan daqiqani ham, kechikishni ham
    /// o'zgartirmaydi — u faqat «kelmadi» degan ayblovni olib tashlaydi.
    /// </para>
    /// <para>
    /// Sanoqlarda: «kelmagan kunlar» ga KIRMAYDI. Ish kuni bo'lgani uchun
    /// rejadagi daqiqalar joyida qoladi — aks holda oyning reja soati
    /// so'rov tasdiqlangani uchun qisqarib ketardi.
    /// </para>
    /// </remarks>
    Excused = 9,

    /// <summary>
    /// Kun hali KELMAGAN — bugundan keyingi ish kuni.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Nega alohida holat, <see cref="Absent"/> emas.</b> Ertaga
    /// ishga kelmaganini bugun ayta olmaymiz. Bu holatsiz oy o'rtasida
    /// ochilgan tabelning qolgan yarmi «Kelmadi» bo'lib qizarardi va
    /// «kelmagan kunlar» sanog'i oy oxiridagi son bilan bir xil bo'lib
    /// ko'rinardi — HR uchun bu shunchaki noto'g'ri raqam.
    /// </para>
    /// <para>
    /// ⚠️ Bu holat SAQLANMAYDI: kelajak kun uchun <c>attendance_days</c>
    /// qatori yaratilmaydi (<c>AttendanceDayBuilder.ShouldStore</c>) va
    /// katak tabel so'rovida hosil qilinadi. Istisno — tasdiqlangan
    /// yo'qlik so'rovi: u kelajakdagi kunni ham <see cref="Excused"/>
    /// qiladi va o'sha qator saqlanadi, chunki qaror BUGUN qabul
    /// qilingan.
    /// </para>
    /// <para>
    /// Sanoqlarda hech qayerga kirmaydi: na kelganga, na kelmaganga.
    /// </para>
    /// </remarks>
    Planned = 10,
}
