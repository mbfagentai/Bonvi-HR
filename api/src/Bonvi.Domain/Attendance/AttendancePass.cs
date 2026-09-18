using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Domain.Attendance;

/// <summary>
/// Bitta xom o'tish — bitta odam, bitta lahza, bitta turniket.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>O'tish QURILMA AYTGANINI saqlaydi, talqinni emas.</b> Tabelning
/// yo'nalish taxminlari (navbat bilan kirish/chiqish, sakrashlarni
/// tashlash) <c>attendance_days</c> ga yoziladi va bu yerga QAYTA
/// YOZILMAYDI. Aks holda jadval tuzatilib qayta hisoblanganda tarixning
/// o'zi o'zgarib ketardi va «bazada shunday turibdi» degan dalil
/// yo'qolardi.
/// </para>
/// <para>
/// <b>Nega <c>IAuditable</c> ham, <c>ISoftDeletable</c> ham emas.</b>
/// O'tishni hech kim tahrirlamaydi va qo'lda o'chirmaydi — o'chirish
/// endpointi umuman yo'q (SPEC §1.2 #5). Yagona o'chirish — saqlash muddati
/// siyosati: tungi vazifa <c>attendance.passRetentionDays</c> dan eski
/// qatorlarni hammasini bir xil qoida bilan o'chiradi (§9.2.1), va u yumshoq
/// o'chirish bo'lishi mumkin emas — maqsad qator va diskni bo'shatish.
/// Audit ustunlari «buni kim o'zgartirdi» degan savolga javob beradi, bu
/// yerda esa o'sha savolning ma'nosi yo'q. <see cref="ReceivedAt"/>
/// <c>created_at</c> ning o'rnini bosadi.
/// </para>
/// <para>
/// <b>Hajm.</b> 540 xodim × kuniga 10 tagacha o'tish ≈ kuniga 5 400 qator,
/// yiliga ≈ 2 mln. Bo'linish (partitioning) ATAYLAB qilinmadi: sukut bir
/// yillik saqlash muddati bilan jadval ~2 mln qator atrofida to'xtaydi, qator
/// esa tor — surat va xom hodisa oldinroq tozalanadi (<see cref="PurgePhoto"/>).
/// Qayta ko'rib chiqish chegarasi (SPEC §1.2 #10): kimdir muddatni <c>0</c>
/// («hech qachon») qilsa VA jadval ~10 mln qatordan oshsa — indekslar
/// xotiraga sig'may qolishidan oldin <c>local_date</c> bo'yicha bo'lish.
/// </para>
/// </remarks>
public sealed class AttendancePass : AggregateRoot
{
    private AttendancePass(
        Guid id,
        Guid deviceId,
        AttendanceIdentityProvider identityProvider,
        string rawPersonId,
        DateTimeOffset occurredAt,
        DateTimeOffset receivedAt,
        string dedupeKey)
        : base(id)
    {
        DeviceId = deviceId;
        IdentityProvider = identityProvider;
        RawPersonId = rawPersonId;
        DedupeKey = dedupeKey;

        // ⭐ Vaqt AYNAN shu yerda va faqat shu yerda bo'linadi: lahza (UTC),
        // qurilma yozgan mahalliy vaqt, uning siljishi va Toshkent kalendar
        // sanasi. Ular uchta boshqa joyda hisoblansa, tizim besh soatga
        // ajralib ketardi — `BonviTime` izohidagi ogohlantirish.
        OccurredAt = occurredAt.ToUniversalTime();
        DeviceLocalTime = occurredAt.DateTime;
        DeviceOffsetMinutes = (short)occurredAt.Offset.TotalMinutes;
        LocalDate = occurredAt.TashkentDate();
        ReceivedAt = receivedAt;
        ClockSkewSeconds = (int)(receivedAt - occurredAt).TotalSeconds;

        Direction = PassDirection.Unknown;
        DirectionSource = DirectionSource.Unknown;
        Method = PassMethod.Unknown;
    }

    /// <summary>EF Core uchun.</summary>
    private AttendancePass()
    {
        RawPersonId = string.Empty;
        DedupeKey = string.Empty;
    }

    /// <summary>Qaysi qurilma yozgan.</summary>
    public Guid DeviceId { get; private set; }

    /// <summary>
    /// Raqamlar fazosi — qabul lahzasida qurilmadan KO'CHIRILADI.
    /// </summary>
    /// <remarks>
    /// Ataylab nusxa, birlashtirish (join) emas: qurilmaning fazasi
    /// keyinchalik o'zgartirilsa, eski o'tishlar o'sha paytdagi faza
    /// bo'yicha qolishi kerak — aks holda bir kecha ichida minglab o'tish
    /// «boshqa odamga» tegishli bo'lib qolardi.
    /// </remarks>
    public AttendanceIdentityProvider IdentityProvider { get; private set; }

    /// <summary>Qurilma yuborgan shaxs raqami.</summary>
    public string RawPersonId { get; private set; }

    /// <summary>Topilgan xodim. <c>null</c> ⇒ moslik yo'q («bog'lanmagan o'tish»).</summary>
    public Guid? ResolvedEmployeeId { get; private set; }

    /// <summary>Moslik qachon qo'llanilgan — qabulda yoki keyingi to'ldirishda.</summary>
    public DateTimeOffset? ResolvedAt { get; private set; }

    /// <summary>O'tish lahzasi, UTC.</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Qurilma yozgan mahalliy vaqt — siljishsiz, o'zgartirilmagan holida.</summary>
    /// <remarks>
    /// Nosozlikni tekshirish uchun: qurilma soati noto'g'ri bo'lsa, farq
    /// aynan shu ustun bilan <see cref="OccurredAt"/> orasida ko'rinadi.
    /// </remarks>
    public DateTime DeviceLocalTime { get; private set; }

    /// <summary>Qurilma bergan siljish (daqiqa). Toshkent uchun +300.</summary>
    public short DeviceOffsetMinutes { get; private set; }

    /// <summary>
    /// Siljish TAXMIN qilinganmi.
    /// </summary>
    /// <remarks>
    /// Qurilma siljishni bermasa Toshkent (+5) qabul qilinadi va shu
    /// bayroq qo'yiladi. Rad etilgan muqobil — UTC deb hisoblash:
    /// terminallar Qo'qonda, ya'ni har bir o'tish besh soatga surilib,
    /// xato «hamma ertalab soat 3 da keladi» bo'lib ko'rinardi.
    /// </remarks>
    public bool OffsetAssumed { get; private set; }

    /// <summary>O'tish TOSHKENT kalendarining qaysi kuniga tushadi.</summary>
    public DateOnly LocalDate { get; private set; }

    /// <summary>Server soati bo'yicha qabul qilingan lahza.</summary>
    public DateTimeOffset ReceivedAt { get; private set; }

    /// <summary>Soat og'ishi: <see cref="ReceivedAt"/> − <see cref="OccurredAt"/>, ishorali.</summary>
    public int ClockSkewSeconds { get; private set; }

    /// <summary>Yo'nalish.</summary>
    public PassDirection Direction { get; private set; }

    /// <summary>Yo'nalish qayerdan olingan.</summary>
    public DirectionSource DirectionSource { get; private set; }

    /// <summary>Shaxs qanday tasdiqlangan.</summary>
    public PassMethod Method { get; private set; }

    /// <summary>Yuz surati kaliti (<c>IFileStorage</c>). Tozalangandan keyin <c>null</c>.</summary>
    public string? PhotoKey { get; private set; }

    /// <summary>Surat va xom hodisa qachon o'chirilgan.</summary>
    /// <remarks>
    /// «Surat hech qachon bo'lmagan» va «surat bor edi, muddati o'tib
    /// o'chirildi» — ikki boshqa holat, va ular faqat shu ustun bilan
    /// ajratiladi. Ikkalasida ham <see cref="PhotoKey"/> <c>null</c>.
    /// </remarks>
    public DateTimeOffset? PhotoPurgedAt { get; private set; }

    /// <summary>Manba hodisaning JSON tanasi. Surat bilan birga tozalanadi.</summary>
    public string? RawEvent { get; private set; }

    /// <summary>
    /// Takrorni ushlash kaliti — <c>(device_id, dedupe_key)</c> unikal.
    /// </summary>
    /// <remarks>
    /// Kalitni buyruq ishlovchisi yasaydi, tahlilchi emas (SPEC §3.6):
    /// qurilma bergan hodisa raqami bo'lsa — o'sha, bo'lmasa hodisani
    /// jismonan aniqlaydigan uchlik (shaxs + soniyagacha yaxlitlangan
    /// vaqt + yo'nalish).
    /// </remarks>
    public string DedupeKey { get; private set; }

    /// <summary>Qayta hisoblash bu o'tishni ko'rdimi. <c>null</c> ⇒ navbatda turibdi.</summary>
    public DateTimeOffset? ProcessedAt { get; private set; }

    /// <summary>Qurilma soati ishonchsiz darajada og'ganmi.</summary>
    /// <remarks>
    /// Chegaradan oshgan o'tish BARIBIR ishlatiladi — faqat jurnalda va
    /// qurilma kartasida belgilanadi. Sabab
    /// <see cref="AttendanceRules.ClockSkewToleranceSeconds"/> izohida.
    /// </remarks>
    public bool HasClockDrift
        => Math.Abs(ClockSkewSeconds) > AttendanceRules.ClockSkewToleranceSeconds;

    /// <summary>Qurilmadan kelgan o'tishni yozib oladi.</summary>
    /// <remarks>
    /// <paramref name="occurredAt"/> tahlilchi qaytargan holida keladi:
    /// siljish bilan bo'lsa — o'sha siljishda, bo'lmasa chaqiruvchi uni
    /// Toshkentga keltirib, <paramref name="offsetAssumed"/> ni qo'yadi.
    /// </remarks>
    public static Result<AttendancePass> Record(
        Guid deviceId,
        AttendanceIdentityProvider identityProvider,
        string? rawPersonId,
        DateTimeOffset occurredAt,
        DateTimeOffset receivedAt,
        string dedupeKey,
        PassDirection direction = PassDirection.Unknown,
        DirectionSource directionSource = DirectionSource.Unknown,
        PassMethod method = PassMethod.Unknown,
        bool offsetAssumed = false,
        string? photoKey = null,
        string? rawEvent = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dedupeKey);

        var personId = Trim(rawPersonId);
        if (personId is null)
        {
            return Error.Validation("davomat.shaxs_raqami_bosh", "Hodisada shaxs raqami yo'q.");
        }

        // Taqqoslash KUNDA, soniyada emas: absurd sanada (1970 yoki 2106)
        // soniyalar farqi `int` ga sig'maydi va quyidagi ayirish jimgina
        // to'lib ketardi.
        if (Math.Abs((receivedAt - occurredAt).TotalDays) > AttendanceRules.MaxClockSkewDays)
        {
            return Error.Validation("davomat.vaqt_yaroqsiz", "Hodisa vaqti haqiqatga to'g'ri kelmaydi.");
        }

        return new AttendancePass(NewId(), deviceId, identityProvider, personId, occurredAt, receivedAt, dedupeKey)
        {
            Direction = direction,
            DirectionSource = directionSource,
            Method = method,
            OffsetAssumed = offsetAssumed,
            PhotoKey = Trim(photoKey),
            RawEvent = rawEvent,
        };
    }

    /// <summary>Xodimni biriktiradi — qabulda yoki moslik kechroq kiritilganda.</summary>
    public void Resolve(Guid employeeId, DateTimeOffset at)
    {
        ResolvedEmployeeId = employeeId;
        ResolvedAt = at;

        // Yangi xodim — kun yozuvi endi boshqacha: o'tish qayta
        // hisoblash navbatiga qaytariladi.
        ProcessedAt = null;
    }

    /// <summary>Kun yozuvi shu o'tish bilan qayta hisoblandi.</summary>
    public void MarkProcessed(DateTimeOffset at) => ProcessedAt = at;

    /// <summary>
    /// Suratni va xom hodisani o'chiradi — QATORNING o'zi qoladi.
    /// </summary>
    /// <remarks>
    /// O'tishning fakti («kim, qachon, qayerdan») abadiy saqlanadi; o'rin
    /// egallaydigani surat va xom JSON, ular esa muddati o'tgach
    /// ma'lumot bermaydi. Fayl tizimidagi faylni chaqiruvchi o'chiradi —
    /// domen fayl tizimini bilmaydi.
    /// </remarks>
    public void PurgePhoto(DateTimeOffset at)
    {
        PhotoKey = null;
        RawEvent = null;
        PhotoPurgedAt = at;
    }

    private static string? Trim(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
