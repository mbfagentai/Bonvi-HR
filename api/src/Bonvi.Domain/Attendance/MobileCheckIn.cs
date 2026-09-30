using Bonvi.Domain.Common;

namespace Bonvi.Domain.Attendance;

/// <summary>
/// Telefondan kelgan belgining ORQASIDAGI koordinata.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega <c>attendance_passes</c> ga ustun bo'lib qo'shilmadi.</b> O'tish
/// QURILMA AYTGANINI saqlaydi, talqinni emas (<see cref="AttendancePass"/>
/// izohidagi markaziy qoida). Bu yerdagi to'rttala son esa XULOSA: obyektni
/// server topdi, masofani server hisobladi, qabul qilish qarorini server
/// qabul qildi. Ular dalil qatoriga tegishli emas.
/// </para>
/// <para>
/// Saqlash haqidagi «NULL joy egallaydi» degan dalil ishlatilmadi va u
/// noto'g'ri edi: PostgreSQL NULL uchun bir BIT ajratadi. Haqiqiy sabablar
/// boshqa — yozish yo'li (qabul modulning eng issiq va eng xavfli kodi,
/// unga kuniga 100 qator uchun yangi ustun qo'shish arzimaydi) va
/// «99 % NULL ustun keyingisini chaqiradi» qoidasi.
/// </para>
/// <para>
/// <b>Nega <see cref="Entity"/>, agregat ildizi emas.</b> Bu qatorga faqat
/// o'z o'tishi orqali murojaat qilinadi: uni hech kim tahrirlamaydi,
/// qidirmaydi va alohida o'chirmaydi. Saqlash muddati ham o'tishniki —
/// <c>pass_id</c> bazada <c>ON DELETE CASCADE</c>.
/// </para>
/// </remarks>
public sealed class MobileCheckIn : Entity
{
    private MobileCheckIn(
        Guid id,
        Guid passId,
        Guid employeeId,
        Guid workSiteId,
        decimal latitude,
        decimal longitude,
        decimal accuracyMeters,
        decimal distanceMeters,
        int siteRadiusMeters,
        WorkSiteShape siteShape,
        DateTimeOffset createdAt)
        : base(id)
    {
        PassId = passId;
        EmployeeId = employeeId;
        WorkSiteId = workSiteId;
        Latitude = latitude;
        Longitude = longitude;
        AccuracyMeters = accuracyMeters;
        DistanceMeters = distanceMeters;
        SiteRadiusMeters = siteRadiusMeters;
        SiteShape = siteShape;
        CreatedAt = createdAt;
    }

    /// <summary>EF Core uchun.</summary>
    private MobileCheckIn()
    {
    }

    /// <summary>Shu koordinata tushuntirayotgan o'tish.</summary>
    public Guid PassId { get; private set; }

    /// <summary>
    /// Koordinatani YUBORGAN xodim.
    /// </summary>
    /// <remarks>
    /// <c>attendance_passes.resolved_employee_id</c> bilan ATAYLAB
    /// takrorlanadi va sabab tezlik emas (yiliga ~36 000 qator). Ular ikki
    /// xil faktni yozadi: bu ustun «kirgan odam shu koordinatani yubordi»
    /// degan DA'VONI, o'sha ustun esa «bu o'tish shu xodimga biriktirildi»
    /// degan XULOSANI — va ikkinchisini keyinchalik to'ldirish vazifasi
    /// qayta yozishi mumkin. Bugun ular har doim mos keladi, chunki yozuvchi
    /// bitta (INV-3).
    /// </remarks>
    public Guid EmployeeId { get; private set; }

    /// <summary>Belgi qaysi obyektda qabul qilingan.</summary>
    public Guid WorkSiteId { get; private set; }

    /// <summary>Telefon bergan kenglik.</summary>
    public decimal Latitude { get; private set; }

    /// <summary>Telefon bergan uzunlik.</summary>
    public decimal Longitude { get; private set; }

    /// <summary>Telefon e'lon qilgan aniqlik, metr.</summary>
    /// <remarks>
    /// Butun son EMAS: 0,4 ni 0 ga yaxlitlash ekranda «mukammal aniqlik»
    /// bo'lib ko'rinardi va bu yolg'on bo'lardi.
    /// </remarks>
    public decimal AccuracyMeters { get; private set; }

    /// <summary>Qaror lahzasida hisoblangan masofa, metr — MUZLATILGAN.</summary>
    /// <remarks>
    /// Nimagacha o'lchangani <see cref="SiteShape"/> ga bog'liq: doirada —
    /// MARKAZGACHA, ko'pburchakda — CHETIGACHA (ichida 0). Ikkala holatda
    /// ham qabul qilingan qatorda <c>DistanceMeters ≤ SiteRadiusMeters</c>
    /// (sababi <see cref="GeofenceReading"/> izohida).
    /// </remarks>
    public decimal DistanceMeters { get; private set; }

    /// <summary>
    /// Qaror qabul qilingan lahzadagi RUXSAT — MUZLATILGAN: doirada radius,
    /// ko'pburchakda chet zaxirasi (SPEC-joylashuvlar §B).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ustun nomi tarixiy (shakllardan oldin faqat radius bor edi) va
    /// ataylab o'zgartirilmadi: ma'nosi <see cref="SiteShape"/> bilan birga
    /// o'qiladi.
    /// </para>
    /// <para>
    /// ⭐ Qabul qilish qarori <c>masofa ≤ ruxsat</c>. Faqat masofa
    /// saqlansa-yu, radius keyinchalik <c>work_sites</c> dan jonli o'qilsa,
    /// HR to'siqni 200 dan 2000 metrga kengaytirgan kunning ertasiga BUTUN
    /// TARIX hukm qilib bo'lmaydigan bo'lib qolardi: «180 m — qabul» va
    /// «1400 m — qabul» yonma-yon turib, qaysi biri o'z vaqtida to'g'ri
    /// bo'lganini ayta olmasdik. <c>AttendancePass</c> qurilmaning raqamlar
    /// fazasini nusxalab olishi bilan bir xil mulohaza.
    /// </para>
    /// </remarks>
    public int SiteRadiusMeters { get; private set; }

    /// <summary>
    /// Belgini QAYSI qoida hukm qilgan — o'sha lahzadagi obyekt shakli.
    /// </summary>
    /// <remarks>
    /// Obyekt keyin doiradan ko'pburchakka (yoki aksincha) o'tkazilsa ham,
    /// tarixiy qator o'zining ikki sonini qanday o'qish kerakligini aytib
    /// turadi.
    /// </remarks>
    public WorkSiteShape SiteShape { get; private set; }

    /// <summary>
    /// Server soati bo'yicha belgi lahzasi — o'tishning <c>occurred_at</c> i bilan bir xil.
    /// </summary>
    /// <remarks>
    /// Ataylab saqlanadi, o'tishdan olinmaydi: birlashtirmasdan sanasini
    /// ayta olmaydigan dalil qatori yomon dalil. Ikkinchi sabab kelajakka
    /// qaraydi — tasdiqlashli oqim qo'shilsa, o'tishning <c>occurred_at</c> i
    /// aynan shu qiymatdan olinishi kerak: soat 09:00 da qilingan belgini
    /// 18:00 da tasdiqlash uni 18:00 dagi kelish qilib yozib qo'ymasin.
    /// </remarks>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>QABUL QILINGAN belgining koordinatasini yozib oladi.</summary>
    /// <remarks>
    /// Tekshiruv yo'q va kerak ham emas: bu yerga faqat qabul qilingan belgi
    /// keladi, ya'ni obyekt ham, masofa ham, radius ham allaqachon
    /// tekshirilgan. Rad etilgan urinish umuman saqlanmaydi (sababi
    /// SPEC-ma'lumot modeli §F.4 da: har bir urinish qator yasaydigan jadval
    /// ochiq suiiste'mol kanali bo'lardi).
    /// </remarks>
    /// <param name="passId">Yaratilgan o'tish.</param>
    /// <param name="employeeId">Belgilagan xodim.</param>
    /// <param name="workSiteId">Topilgan obyekt.</param>
    /// <param name="latitude">Kenglik.</param>
    /// <param name="longitude">Uzunlik.</param>
    /// <param name="accuracyMeters">Telefon bergan aniqlik.</param>
    /// <param name="distanceMeters">Hisoblangan masofa (doirada markazgacha, ko'pburchakda chetgacha).</param>
    /// <param name="siteRadiusMeters">Qaror lahzasidagi ruxsat (radius yoki chet zaxirasi).</param>
    /// <param name="siteShape">Qaror lahzasidagi obyekt shakli.</param>
    /// <param name="createdAt">Server lahzasi.</param>
    public static MobileCheckIn Record(
        Guid passId,
        Guid employeeId,
        Guid workSiteId,
        decimal latitude,
        decimal longitude,
        decimal accuracyMeters,
        decimal distanceMeters,
        int siteRadiusMeters,
        WorkSiteShape siteShape,
        DateTimeOffset createdAt)
        => new(
            NewId(),
            passId,
            employeeId,
            workSiteId,
            latitude,
            longitude,
            accuracyMeters,
            distanceMeters,
            siteRadiusMeters,
            siteShape,
            createdAt);
}
