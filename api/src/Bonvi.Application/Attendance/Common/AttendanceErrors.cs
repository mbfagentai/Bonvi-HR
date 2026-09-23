using Bonvi.Application.Attendance.Mobile;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Common;

/// <summary>
/// Davomat use-case'larining xatolari — Application qatlamida paydo bo'ladiganlari.
/// </summary>
/// <remarks>
/// <para>
/// Domen o'z xatolarini (jadval, qurilma, moslik tekshiruvi) o'zi qaytaradi;
/// bu yerda faqat so'rov darajasidagilari: davr, oy, hisobot turi, topilmagan
/// yozuv va eksport chegarasi. <c>SafetyErrors</c> bilan bir xil shakl.
/// </para>
/// <para>
/// ⚠️ Har bir kod 4 tilda tarjima qilinishi SHART, aks holda
/// <c>./bonvi check</c> yiqiladi (SPEC §0.2 #5). Bu fayl KETMA-KET
/// kengaytiriladi — B6 qurilma/moslik/jadval kodlarini shu yerga qo'shadi.
/// </para>
/// </remarks>
public static class AttendanceErrors
{
    // ── Davr (hisobotlar va jurnal) ─────────────────────────────────

    /// <summary>Davr oxiri boshlanishidan oldin.</summary>
    /// <remarks>
    /// Kod biriktirmadagi <c>effectiveTo &lt; effectiveFrom</c> bilan BIR XIL
    /// (domen allaqachon chiqaradi) — ma'nosi ham bir xil: «tugash
    /// boshlanishdan oldin». Ikkinchi kod ochish bitta xatoga ikki xil
    /// tarjima degani bo'lardi.
    /// </remarks>
    public static Error PeriodReversed { get; } = Error.Validation(
        "davomat.davr_notogri",
        "Davr oxiri boshlanishidan oldin bo'lishi mumkin emas.");

    /// <summary>Davr <see cref="AttendanceRules.MaxRecomputeDays"/> kundan uzun.</summary>
    /// <remarks>
    /// Chegara qayta hisoblash bilan BIR XIL (bir chorak). Hisobotni uzunroq
    /// davrga ochish mumkin edi, lekin «hisobotda ko'rdim — qayta hisoblashga
    /// berolmayman» degan nomuvofiqlik chiqardi; bitta son esa bitta qoida.
    /// </remarks>
    public static Error PeriodTooLong { get; } = Error.Validation(
        "davomat.davr_uzun",
        $"Davr {AttendanceRules.MaxRecomputeDays} kundan oshmasligi kerak. Davrni qisqartiring.");

    /// <summary>Tabel uchun yil yoki oy noto'g'ri.</summary>
    /// <remarks>
    /// <c>davomat.davr_notogri</c> ga QO'SHILMADI: u «tugash boshlanishdan
    /// oldin» degan aniq ma'noga ega (SPEC §6.9) va «13-oy» uchun o'sha
    /// tarjima foydalanuvchini chalg'itardi.
    /// </remarks>
    public static Error MonthInvalid { get; } = Error.Validation(
        "davomat.oy_notogri",
        "Yil yoki oy noto'g'ri: oy 1 dan 12 gacha, yil 2000 dan 2100 gacha bo'lishi kerak.");

    // ── Hisobotlar ──────────────────────────────────────────────────

    /// <summary>Chetlanishlar hisobotida tur ko'rsatilmagan yoki noma'lum.</summary>
    /// <remarks>
    /// Tur MAJBURIY: turi yo'q «chetlanishlar» ro'yxati kechikkan, kelmagan
    /// va muammoli kunlarni bitta jadvalga aralashtirardi va ustunlarning
    /// yarmi ma'nosiz bo'lib qolardi.
    /// </remarks>
    public static Error ExceptionKindInvalid { get; } = Error.Validation(
        "davomat.hisobot_turi_notogri",
        "Hisobot turi noto'g'ri. Ruxsat etilgan qiymatlar: Late, EarlyLeave, Absent, Missing.");

    // ── O'tishlar ───────────────────────────────────────────────────

    /// <summary>Bunday o'tish yo'q.</summary>
    public static Error PassNotFound { get; } = Error.NotFound(
        "davomat.otish_topilmadi",
        "O'tish yozuvi topilmadi.");

    // ── Eksport ─────────────────────────────────────────────────────

    /// <summary>
    /// Eksport chegarasidan oshdi.
    /// </summary>
    /// <remarks>
    /// Umumiy <c>eksport.juda_kop</c> ISHLATILMADI: uning tarjimasida xodimlar
    /// eksportining chegarasi («5000 ta») yozilgan, davomatniki boshqa. Bir
    /// kodni ikki chegara bilan ishlatish ekranda noto'g'ri sonni ko'rsatardi.
    /// </remarks>
    /// <param name="total">Filtrga mos yozuvlar soni.</param>
    /// <param name="limit">Shu eksportning chegarasi.</param>
    public static Error ExportTooLarge(int total, int limit) => Error.Validation(
        "davomat.eksport_juda_kop",
        $"Tanlangan filtrga {total} yozuv mos keladi — eksport chegarasi {limit}. " +
        "Davrni qisqartiring yoki bo'lim/xodim bo'yicha toraytiring.");

    /// <summary>O'tish bor, lekin surati yo'q — qurilma yubormagan yoki saqlash muddati tugab o'chirilgan.</summary>
    /// <remarks>
    /// <see cref="PassNotFound"/> dan ATAYLAB alohida: «o'tish yo'q» (havola
    /// eskirgan) va «o'tish bor, surati yo'q» ekranda ikki xil gap —
    /// ikkinchisida jurnal qatori ochiladi, faqat rasm o'rnida belgi turadi.
    /// </remarks>
    public static Error PassPhotoMissing { get; } = Error.NotFound(
        "davomat.surat_yoq",
        "Bu o'tishning surati yo'q: qurilma yubormagan yoki saqlash muddati tugagan.");

    // ── Qurilmalar ──────────────────────────────────────────────────

    /// <summary>Bunday qurilma yo'q.</summary>
    public static Error DeviceNotFound { get; } = Error.NotFound(
        "davomat.qurilma_topilmadi",
        "Qurilma topilmadi.");

    /// <summary>Shu nomli qurilma allaqachon bor.</summary>
    public static Error DeviceNameTaken { get; } = Error.Conflict(
        "davomat.qurilma_nom_takror",
        "Bunday nomli qurilma allaqachon ro'yxatda bor.");

    /// <summary>Shu seriya raqamli qurilma allaqachon bor.</summary>
    public static Error DeviceSerialTaken { get; } = Error.Conflict(
        "davomat.qurilma_seriya_takror",
        "Bunday seriya raqamli qurilma allaqachon ro'yxatda bor.");

    /// <summary>O'tishlari bor qurilmani o'chirib bo'lmaydi.</summary>
    /// <remarks>
    /// Faolsizlantirish — to'g'ri yo'l: jurnal har bir o'tishning qaysi
    /// qurilmadan kelganini ko'rsatishda davom etadi (SPEC §6.2).
    /// </remarks>
    public static Error DeviceHasPasses { get; } = Error.Conflict(
        "davomat.qurilma_otishlari_bor",
        "Bu qurilma yozgan o'tishlar bor — uni o'chirib bo'lmaydi, faolsizlantiring.");

    /// <summary>IP ro'yxatida o'qib bo'lmaydigan element bor.</summary>
    /// <remarks>
    /// ⚠️ Saqlashda rad etiladi, chunki qabul yo'li o'qib bo'lmaydigan elementni
    /// «hech narsani qamramaydi» deb oladi: xato yozilgan ro'yxat qurilmani
    /// JIMGINA qulflab qo'yardi va HR buni faqat tabel bo'shab qolganda
    /// sezardi (B3 izohi).
    /// </remarks>
    public static Error AllowedIpsInvalid { get; } = Error.Validation(
        "davomat.ip_royxati_notogri",
        "IP ro'yxati noto'g'ri: vergul bilan ajratilgan IPv4/IPv6 manzil yoki CIDR (masalan 10.0.0.0/8) kutiladi.");

    // ── Mosliklar va import ─────────────────────────────────────────

    /// <summary>Bunday moslik yo'q.</summary>
    public static Error IdentityNotFound { get; } = Error.NotFound(
        "davomat.moslik_topilmadi",
        "Moslik yozuvi topilmadi.");

    /// <summary>Bu shaxs raqami allaqachon bog'langan.</summary>
    /// <remarks>
    /// Boshqa xodimga qayta bog'lash — tuzatish emas, boshqa qaror: avval eski
    /// moslik uziladi, keyin yangisi yaratiladi, va tarixda ikkalasi ham qoladi
    /// (<c>AttendanceIdentity</c> izohi).
    /// </remarks>
    public static Error IdentityTaken { get; } = Error.Conflict(
        "davomat.moslik_takror",
        "Bu shaxs raqami allaqachon xodimga bog'langan.");

    /// <summary>Shaxs raqami ustun sig'imidan uzun.</summary>
    /// <remarks>
    /// Domen bo'sh raqamni tekshiradi (<c>davomat.shaxs_raqami_bosh</c>), uzunlikni
    /// esa yo'q — tekshirilmasa, 65 belgilik raqam bazada <c>varchar(64)</c> ga
    /// urilib 500 qaytarardi. Hech bir terminal bunday raqam bermaydi, ya'ni bu
    /// deyarli har doim faylda noto'g'ri ustun tanlangani belgisi.
    /// </remarks>
    public static Error PersonIdTooLong { get; } = Error.Validation(
        "davomat.shaxs_raqami_uzun",
        $"Shaxs raqami {AttendanceIdentity.MaxExternalPersonIdLength} belgidan oshmasligi kerak.");

    /// <summary>Xodim identifikatori berilgan, lekin bunday xodim yo'q.</summary>
    /// <remarks>
    /// Bo'sh identifikator — boshqa kod (<c>davomat.xodim_yoq</c>, 400, domen
    /// chiqaradi). Bitta kod ikki HTTP holatini bildirmasligi kerak (SPEC §6.9.1).
    /// </remarks>
    public static Error EmployeeNotFound { get; } = Error.NotFound(
        "davomat.xodim_topilmadi",
        "Xodim topilmadi.");

    /// <summary>Import faylida shaxs raqami ustuni topilmadi.</summary>
    public static Error ImportIdColumnMissing { get; } = Error.Validation(
        "davomat.import_ustun_topilmadi",
        "Faylning birinchi 10 qatorida shaxs raqami ustuni (masalan «Employee No», «Табельный номер») topilmadi.");

    /// <summary>Fayl turi qo'llanmaydi yoki fayl o'qilmadi.</summary>
    public static Error ImportFileType { get; } = Error.Validation(
        "davomat.import_fayl_turi",
        "Faqat .xlsx yoki .csv fayl qabul qilinadi.");

    /// <summary>Import fayli juda katta.</summary>
    public static Error ImportFileTooLarge { get; } = Error.Validation(
        "davomat.import_fayl_katta",
        "Import fayli 10 MB dan oshmasligi kerak.");

    /// <summary>Import qatorlari chegaradan ko'p.</summary>
    public static Error ImportTooManyRows { get; } = Error.Validation(
        "davomat.import_qator_kop",
        $"Import {AttendanceRules.MaxImportRows} qatordan oshmasligi kerak. Faylni bo'lib yuklang.");

    // ── Jadvallar va biriktirmalar ──────────────────────────────────

    /// <summary>Jadval identifikatori berilgan, lekin bunday jadval yo'q.</summary>
    public static Error ScheduleNotFound { get; } = Error.NotFound(
        "davomat.jadval_topilmadi",
        "Ish jadvali topilmadi.");

    /// <summary>Shu nomli jadval allaqachon bor.</summary>
    public static Error ScheduleNameTaken { get; } = Error.Conflict(
        "davomat.jadval_nom_takror",
        "Bunday nomli ish jadvali allaqachon bor.");

    /// <summary>Jadvalga hali biriktirma ishora qiladi — o'chirib ham, faolsizlantirib ham bo'lmaydi.</summary>
    /// <remarks>
    /// Faolsizlantirish ham shu kod bilan rad etiladi (koordinator qarori,
    /// 2026-09-17): hisoblash <c>IsActive</c> ga qaramaydi, ya'ni ruxsat
    /// berilsa ekran «o'chiq» deydi, tabel esa jadvalni ishlatishda davom
    /// etardi. To'g'ri yo'l — avval biriktirmalarni olib tashlash.
    /// </remarks>
    public static Error ScheduleAssigned { get; } = Error.Conflict(
        "davomat.jadval_biriktirilgan",
        "Bu jadval hali biriktirilgan — avval uning biriktirmalarini olib tashlang.");

    /// <summary>Faolsiz jadvalni biriktirib bo'lmaydi.</summary>
    /// <remarks>
    /// <see cref="ScheduleAssigned"/> ning ikkinchi tomoni: «faolsiz jadvalda
    /// tirik biriktirma yo'q» degan qoida ikkala yo'ldan ham buzilmasligi kerak.
    /// </remarks>
    public static Error ScheduleInactive { get; } = Error.Conflict(
        "davomat.jadval_faolsiz",
        "Faolsiz jadvalni biriktirib bo'lmaydi — avval uni faollashtiring.");

    /// <summary>Bunday biriktirma yo'q.</summary>
    public static Error AssignmentNotFound { get; } = Error.NotFound(
        "davomat.biriktirma_topilmadi",
        "Jadval biriktirmasi topilmadi.");

    /// <summary>Xuddi shu pog'ona, obyekt va boshlanish sanasi bilan biriktirma allaqachon bor.</summary>
    public static Error AssignmentTaken { get; } = Error.Conflict(
        "davomat.biriktirma_takror",
        "Shu obyektga shu sanadan boshlab jadval allaqachon biriktirilgan.");

    /// <summary>Pog'ona va ko'rsatilgan obyektlar bir-biriga mos emas.</summary>
    /// <remarks>
    /// Kod domendagi bilan AYNAN bir xil (<c>ScheduleAssignment.Assign</c>): so'rovda
    /// uchta obyekt maydoni bor, domen esa bittasini oladi — «ortiqcha maydon
    /// ham to'ldirilgan» holati shu yerda ushlanadi, ma'nosi esa o'sha.
    /// </remarks>
    public static Error AssignmentTargetMismatch { get; } = Error.Validation(
        "davomat.biriktirma_obyekt_yoq",
        "Biriktirma pog'onasi va ko'rsatilgan obyekt bir-biriga mos kelmaydi.");

    // ── Agent (SPEC-agent §D4) ──────────────────────────────────────

    /// <summary>Agent hisobotida juda ko'p qator.</summary>
    /// <remarks>
    /// Qurilma sig'imi 3000 kishi. Undan kattasi xato yoki hujum — ikkala
    /// holda ham qabul qilib, minglab qatorni bitta tranzaksiyada yozishning
    /// ma'nosi yo'q.
    /// </remarks>
    public static Error AgentReportTooLarge { get; } = Error.Validation(
        "davomat.agent_hisobot_uzun",
        "Hisobotdagi qatorlar soni chegaradan oshdi.");

    /// <summary>Agent yuborgan qurilma vaqti ishonarsiz.</summary>
    public static Error DeviceClockUnreadable { get; } = Error.Validation(
        "davomat.qurilma_vaqti_notogri",
        "Qurilma vaqti o'qib bo'lmaydigan qiymat berdi.");

    // ── Obyektlar (SPEC-xodim-ilovasi §D1) ──────────────────────────

    /// <summary>Bunday obyekt yo'q.</summary>
    public static Error WorkSiteNotFound { get; } = Error.NotFound(
        "davomat.obyekt_topilmadi",
        "Obyekt topilmadi.");

    /// <summary>Shakl berilmagan yoki noma'lum son yuborilgan (SPEC-joylashuvlar §C).</summary>
    /// <remarks>
    /// JSON enum'ni son sifatida ham qabul qiladi, ya'ni <c>"shape": 7</c>
    /// bog'lashdan o'tib keladi; shakl umuman yuborilmasa esa 0 bo'ladi.
    /// Ikkalasi ham jimgina «doira» deb o'qilmasligi kerak.
    /// </remarks>
    public static Error WorkSiteShapeInvalid { get; } = Error.Validation(
        "obyekt.shakl_notogri",
        "Shakl «Circle» (doira) yoki «Polygon» (ko'pburchak) bo'lishi kerak.");

    /// <summary>Doira markazining kengligi berilmagan.</summary>
    /// <remarks>
    /// Kod domendagi oraliq xatosi bilan AYNAN bir xil
    /// (<c>WorkSite.Create</c>): ekran uchun ikkalasi ham «kenglik
    /// noto'g'ri» degan bitta gap, yangi kod bitta xatoga ikkinchi tarjima
    /// bo'lardi.
    /// </remarks>
    public static Error WorkSiteLatitudeMissing { get; } = Error.Validation(
        "davomat.obyekt_kenglik",
        "Doira markazining kengligi kiritilishi shart.");

    /// <summary>Doira markazining uzunligi berilmagan — kodi domendagi bilan bir xil.</summary>
    public static Error WorkSiteLongitudeMissing { get; } = Error.Validation(
        "davomat.obyekt_uzunlik",
        "Doira markazining uzunligi kiritilishi shart.");

    /// <summary>Doira radiusi berilmagan — kodi domendagi bilan bir xil.</summary>
    public static Error WorkSiteRadiusMissing { get; } = Error.Validation(
        "davomat.obyekt_radiusi",
        $"Doira radiusi kiritilishi shart: {WorkSite.MinRadiusMeters} dan {WorkSite.MaxRadiusMeters} metrgacha.");

    // ── «Mobil ilova» sintetik qurilmasi (§D5, K4) ──────────────────

    /// <summary>Qurilma formasida <c>Mobile</c> provayderi tanlangan.</summary>
    /// <remarks>
    /// ⚠️ Bu ATAYLAB serverda to'siladi, formada yashirish bilan
    /// cheklanmasdan. <c>Mobile</c> bilan ro'yxatga olingan HAQIQIY terminal
    /// qabul yo'lida tahlilchi topolmay <c>InvalidOperationException</c>
    /// tashlardi, ya'ni HR tushunarli 400 o'rniga 500 olardi — va buni
    /// faqat terminal jim qolganda sezardi.
    /// </remarks>
    public static Error DeviceProviderMobile { get; } = Error.Validation(
        "davomat.qurilma_mobil_provayder",
        "«Mobil ilova» provayderi faqat tizimning ichki qatori uchun — terminalga tanlab bo'lmaydi.");

    /// <summary>«Mobil ilova» qatoriga tegishga urinish.</summary>
    /// <remarks>
    /// Bitta kod to'rt amalni to'sadi: o'chirish, faollashtirish, tahrirlash
    /// va tokenni aylantirish. Ular bitta sababdan rad etiladi — bu qator
    /// terminal emas, ilovaning o'tishlari uchun ushlab turilgan yozuv; uni
    /// faollashtirish 950 ta xodim kartasida hech qachon bajarilmaydigan
    /// «Mobil ilova — Rasm yo'q» qatorini yoqardi, tokenini berish esa
    /// ilovaga ISHLAYDIGAN qabul tokeni yasardi.
    /// </remarks>
    public static Error MobileAppDeviceProtected { get; } = Error.Conflict(
        "davomat.qurilma_mobil_ilova",
        "«Mobil ilova» — tizimning ichki qatori: uni o'zgartirib, yoqib yoki o'chirib bo'lmaydi.");

    // ── Telefondan belgilash (SPEC-xodim-ilovasi §E) ────────────────

    /// <summary>Xodimga telefondan belgilash ruxsati berilmagan yoki hisobi yopilgan.</summary>
    /// <remarks>
    /// 403: so'rov to'g'ri, lekin bu imkoniyat shu odamda YOQILMAGAN. Qayta
    /// urinish hech qachon yordam bermaydi va ilova aynan shu sababli bu
    /// xatoda «Qayta urinish» tugmasini ko'rsatmaydi.
    /// </remarks>
    public static Error MobileCheckInNotAllowed { get; } = Error.Forbidden(
        "davomat.mobil_ruxsat_yoq",
        "Telefondan belgilash sizning hisobingizda yoqilmagan. HR bo'limiga murojaat qiling.");

    /// <summary>Telefon bergan joylashuv juda noaniq.</summary>
    /// <remarks>
    /// Chegara <see cref="MobileCheckInRules.MaxAccuracyMeters"/> da. Xabarda
    /// O'LCHANGAN son ham bor: «qayta urinib ko'ring» dan ko'ra «hozirgi
    /// aniqlik ±350 m» odamga nima qilishni aniqroq aytadi.
    /// </remarks>
    /// <param name="accuracyMeters">Telefon aytgan aniqlik.</param>
    public static Error LocationTooCoarse(decimal accuracyMeters) => Error.Validation(
        "davomat.joylashuv_aniq_emas",
        $"Joylashuv aniqligi ±{accuracyMeters:0.#} m — chegara {MobileCheckInRules.MaxAccuracyMeters} m. " +
        "Ochiq joyga chiqib qayta urinib ko'ring.");

    /// <summary>Hech bir faol obyektning hududida emas.</summary>
    /// <remarks>
    /// ⚠️ Faol obyekt UMUMAN yo'q bo'lgan holat ham SHU kod bilan qaytadi
    /// (xabari boshqacha). Sabab: SPEC §E belgilash uchun ROPPA-ROSA to'rtta
    /// kod sanab chiqqan va beshinchisini qo'shish ilovaning xato jadvalini
    /// SPEC bilan ajratib yuborardi. Ikkalasida ham odam uchun natija bir
    /// xil: shu joyda belgilab bo'lmaydi.
    /// </remarks>
    /// <param name="nearestName">Eng yaqin obyekt nomi; hech biri bo'lmasa <c>null</c>.</param>
    /// <param name="distanceMeters">
    /// Uning CHEGARASIGACHA masofa (doirada markazgacha emas — SPEC-joylashuvlar §B).
    /// </param>
    public static Error OutsideWorkSite(string? nearestName, double? distanceMeters) => Error.Validation(
        "davomat.obyektdan_uzoq",
        nearestName is null || distanceMeters is null
            ? "Belgilash uchun faol obyekt topilmadi. HR bo'limiga murojaat qiling."
            : $"Siz obyekt hududidan tashqaridasiz. Eng yaqin obyekt: {nearestName} — {distanceMeters:0} m.");

    /// <summary>Oxirgi belgidan keyin <see cref="AttendanceRules.BounceSeconds"/> soniya o'tmagan.</summary>
    /// <remarks>
    /// Chegara terminal o'tishlarining takror qoidasi bilan AYNAN bir xil
    /// son: telefon belgisiga boshqa chegara qo'yilsa, bitta kunda ikki xil
    /// «takror» ta'rifi bo'lib qolardi.
    /// </remarks>
    /// <param name="lastAt">Oxirgi qabul qilingan belgi lahzasi.</param>
    public static Error CheckInTooSoon(DateTimeOffset lastAt) => Error.Conflict(
        "davomat.takroriy_belgi",
        $"Siz hozirgina belgilagansiz — oxirgi belgi {lastAt.ToTashkent():HH:mm:ss} da qabul qilingan.");
}
