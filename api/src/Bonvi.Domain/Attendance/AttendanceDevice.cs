using Bonvi.Domain.Common;

namespace Bonvi.Domain.Attendance;

/// <summary>
/// Ro'yxatga olingan terminal — turniket yoki yuz qurilmasi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Qurilma bizni token orqali taniydi, biz uni ham SHU token orqali
/// taniymiz.</b> Hodisa tanasidagi <c>deviceName</c>, <c>ipAddress</c>,
/// <c>macAddress</c> identifikatsiyada UMUMAN ishlatilmaydi: ular
/// hujumchi yozadigan satrlar. Tokenning o'zi saqlanmaydi — faqat
/// SHA-256 xeshi (<see cref="TokenHash"/>), xuddi <c>RefreshToken</c>
/// dagidek: baza sizib chiqsa, tokenlardan foydalanib bo'lmaydi.
/// </para>
/// <para>
/// <b>Nega parol KDF emas, oddiy SHA-256.</b> Token — 256 bit mashina
/// tanlagan tasodif, ya'ni taxmin qilinadigan narsa yo'q. Argon2/bcrypt
/// past entropiyali INSON sirini sekinlashtirish uchun bor; bu yerda u
/// har bir o'tishning issiq yo'liga ~100 ms qo'shardi, xolos.
/// </para>
/// <para>
/// <b>Kuzatuv ustunlari (<see cref="LastSeenAt"/>, <see cref="LastEventAt"/>,
/// <see cref="LastSkewSeconds"/>) mashina tomonidan yoziladi</b> va HR
/// uchun yagona diagnostika oynasi: «terminal jim qoldi» va «terminal
/// so'rov yuboryapti, lekin hammasi rad etilyapti» — bu ikki BOSHQA
/// nosozlik va ular shu ikki ustun bilan ajratiladi.
/// </para>
/// <para>
/// Ro'yxatda ochiq eshik boshqaruvi uchun kerak bo'ladigan hamma narsa
/// allaqachon bor (model, seriya, oxirgi IP, imzo siri slotı) — lekin
/// v1 eshikni BOSHQARMAYDI, faqat yozib boradi. Sabab SPEC §1.2 #1 da:
/// kirishga ruxsat berish real vaqtli va xavfsizlik uchun kritik yo'l,
/// bizning API o'chganda ham odamlar ishga kira olishi kerak.
/// </para>
/// </remarks>
public sealed class AttendanceDevice : AggregateRoot, IAuditable, ISoftDeletable
{
    /// <summary>Token xeshi — SHA-256, ya'ni har doim aynan 32 bayt.</summary>
    /// <remarks>
    /// Bu domen invarianti, ustun cheklovi emas: PostgreSQL <c>bytea</c>
    /// uzunlik olmaydi. Shu sababli tekshiruv shu yerda turadi.
    /// </remarks>
    public const int TokenHashLength = 32;

    /// <summary>Ekranda ko'rsatiladigan boshlang'ich belgilar soni.</summary>
    /// <remarks>
    /// Tokenning o'zi bir marta — berilgan lahzada — ko'rsatiladi va
    /// boshqa hech qachon. HR ikki qurilmani ajrata olishi uchun shuncha
    /// belgi yetadi; 43 belgilik tokendan 8 tasi taxmin qilishga hech
    /// narsa bermaydi.
    /// </remarks>
    public const int TokenPrefixLength = 8;

    private AttendanceDevice(
        Guid id,
        string name,
        byte[] tokenHash,
        string tokenPrefix,
        DateTimeOffset tokenIssuedAt)
        : base(id)
    {
        Name = name;
        TokenHash = tokenHash;
        TokenPrefix = tokenPrefix;
        TokenIssuedAt = tokenIssuedAt;
        Provider = AttendanceProvider.Hikvision;
        IdentityProvider = AttendanceIdentityProvider.Verifix;
        DirectionRole = DeviceDirectionRole.ReportsDirection;
        AuthMode = DeviceAuthMode.Token;
        IsActive = true;
    }

    /// <summary>EF Core uchun.</summary>
    private AttendanceDevice()
    {
        Name = string.Empty;
        TokenHash = [];
        TokenPrefix = string.Empty;
    }

    /// <summary>Ko'rinadigan nom — «Markaziy ofis — kirish turnigeti».</summary>
    public string Name { get; private set; }

    /// <summary>Korpusdagi seriya raqami — o'rnatuvchi uchun.</summary>
    public string? SerialNumber { get; private set; }

    /// <summary>Model (masalan <c>DS-K1T671M</c>) — faqat hujjat uchun.</summary>
    public string? Model { get; private set; }

    /// <summary>Hodisani qaysi tahlilchi o'qiydi.</summary>
    public AttendanceProvider Provider { get; private set; }

    /// <summary>Hodisadagi shaxs raqami qaysi raqamlar fazosida.</summary>
    public AttendanceIdentityProvider IdentityProvider { get; private set; }

    /// <summary>Qurilma yo'nalishni o'zi aytadimi yoki roli bilan belgilanadimi.</summary>
    public DeviceDirectionRole DirectionRole { get; private set; }

    /// <summary>Qaysi firmaga tegishli — ixtiyoriy, hisobot uchun.</summary>
    public Guid? LegalEntityId { get; private set; }

    /// <summary>Qaysi bo'limga tegishli — ixtiyoriy, hisobot uchun.</summary>
    public Guid? DepartmentId { get; private set; }

    /// <summary>Joylashuvi — erkin matn, «1-qavat, chap turniket».</summary>
    public string? LocationNote { get; private set; }

    /// <summary>Tasdiqlash usuli.</summary>
    public DeviceAuthMode AuthMode { get; private set; }

    /// <summary>Qabul tokenining SHA-256 xeshi. Ochiq token saqlanmaydi.</summary>
    public byte[] TokenHash { get; private set; }

    /// <summary>Tokenning birinchi <see cref="TokenPrefixLength"/> belgisi — ekranda ko'rsatish uchun.</summary>
    public string TokenPrefix { get; private set; }

    /// <summary>Token qachon berilgan (yoki oxirgi marta aylantirilgan).</summary>
    public DateTimeOffset TokenIssuedAt { get; private set; }

    /// <summary>Tokenni kim bergan — foydalanuvchi identifikatori, tashqi kalitsiz (audit ustunlaridek).</summary>
    public Guid? TokenIssuedBy { get; private set; }

    /// <summary>
    /// HMAC imzosining siri — <c>ISecretProtector</c> bilan shifrlangan holda.
    /// </summary>
    /// <remarks>
    /// <c>AuthMode = TokenAndHmac</c> bo'lganda MAJBURIY. Shifrlash
    /// qatlami domenga tegishli emas: bu yerga allaqachon shifrlangan
    /// satr keladi — <c>bot.token</c> sozlamasidagi bilan bir xil qoida.
    /// </remarks>
    public string? HmacSecret { get; private set; }

    /// <summary>
    /// Ruxsat etilgan manbalar — vergul bilan ajratilgan CIDR ro'yxati.
    /// </summary>
    /// <remarks>
    /// Bo'sh ⇒ IP bo'yicha cheklov YO'Q, va bugun aynan shunday: terminallar
    /// o'zgaruvchan (dinamik) uy internetida turibdi. Maydon statik manzil
    /// paydo bo'lgan kun uchun tayyor turadi.
    /// </remarks>
    public string? AllowedIps { get; private set; }

    /// <summary>Qurilma faolmi. Faolsiz qurilmaning tokeni o'tmaydi (403).</summary>
    public bool IsActive { get; private set; }

    /// <summary>Tasdiqlashgacha YETIB KELGAN oxirgi so'rov vaqti.</summary>
    /// <remarks>
    /// <see cref="LastEventAt"/> bilan farqi diagnostik: bu ikkisi
    /// ajralib qolsa, qurilma gapiryapti-yu, hodisalari rad etilyapti
    /// (noto'g'ri tahlilchi, buzuq soat, bo'sh shaxs raqami).
    /// </remarks>
    public DateTimeOffset? LastSeenAt { get; private set; }

    /// <summary>Oxirgi QABUL QILINGAN o'tish vaqti.</summary>
    public DateTimeOffset? LastEventAt { get; private set; }

    /// <summary>Oxirgi so'rov kelgan manba manzili — mashina yozadi.</summary>
    public string? LastSeenIp { get; private set; }

    /// <summary>Oxirgi qabul qilingan o'tishdagi soat og'ishi (soniya, ishorali).</summary>
    /// <remarks>
    /// ⚠️ Bu son «qurilma soati» ni o'lchamaydi, u YETKAZIB BERISH
    /// kechikishini ham o'z ichiga oladi: <c>qabul qilingan − sodir
    /// bo'lgan</c>. Qurilmaning o'zi push qilganda ikkisi bir xil narsa
    /// edi; agent orqali tortib olinganda esa agentning bir necha
    /// daqiqalik uzilishi ham shu songa qo'shiladi. Shuning uchun
    /// soatning HAQIQIY og'ishi endi <see cref="ClockOffsetSeconds"/> da.
    /// </remarks>
    public int? LastSkewSeconds { get; private set; }

    /// <summary>
    /// Qurilma soatining serverdan og'ishi (soniya, ishorali) — TO'G'RIDAN-TO'G'RI o'lchov.
    /// </summary>
    /// <remarks>
    /// Agent terminaldan uning JORIY vaqtini so'raydi va shu lahzadagi
    /// server vaqti bilan solishtiradi. Bu o'lchov hodisalar navbatiga,
    /// tarmoq kechikishiga va agentning uzilishiga BOG'LIQ EMAS —
    /// aynan shuning uchun u alohida ustunda yashaydi.
    /// </remarks>
    public int? ClockOffsetSeconds { get; private set; }

    /// <summary>Soat oxirgi marta qachon tekshirilgan.</summary>
    public DateTimeOffset? ClockCheckedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>Qurilma soati ishonchsiz darajada og'ganmi.</summary>
    /// <remarks>
    /// Hisoblanadi, saqlanmaydi — <c>WorkScheduleDay.CrossesMidnight</c>
    /// dagi bilan bir xil sabab: saqlangan bayroq o'zi tasvirlayotgan
    /// songa zid bo'lib qolishi mumkin.
    /// </remarks>
    /// <remarks>
    /// ⭐ To'g'ridan-to'g'ri o'lchov (<see cref="ClockOffsetSeconds"/>) bo'lsa
    /// FAQAT u ishlatiladi: o'tishdan olingan son yetkazib berish
    /// kechikishini ham o'z ichiga oladi va agent bir necha daqiqa
    /// o'chgandan keyin soxta «soat nosoz» belgisini yoqib yuborardi.
    /// O'lchov yo'q qurilmalar (o'zi push qiladiganlar) uchun eski
    /// manba zaxira bo'lib qoladi.
    /// </remarks>
    public bool HasClockDrift
        => (ClockOffsetSeconds ?? LastSkewSeconds) is { } skew
            && Math.Abs(skew) > AttendanceRules.ClockSkewToleranceSeconds;

    /// <summary>Yangi qurilmani ro'yxatga oladi.</summary>
    /// <remarks>
    /// Token bu yerda YASALMAYDI: tasodif manbai (<c>RandomNumberGenerator</c>)
    /// va xeshlash Infrastructure'ning ishi, domen esa texnologiyani
    /// bilmaydi. Bu yerga tayyor xesh va ko'rsatish uchun prefiks keladi.
    /// </remarks>
    public static Result<AttendanceDevice> Register(
        string? name,
        byte[] tokenHash,
        string tokenPrefix,
        DateTimeOffset tokenIssuedAt,
        Guid? tokenIssuedBy = null,
        AttendanceProvider provider = AttendanceProvider.Hikvision,
        AttendanceIdentityProvider identityProvider = AttendanceIdentityProvider.Verifix,
        DeviceDirectionRole directionRole = DeviceDirectionRole.ReportsDirection,
        DeviceAuthMode authMode = DeviceAuthMode.Token,
        string? serialNumber = null,
        string? model = null,
        Guid? legalEntityId = null,
        Guid? departmentId = null,
        string? locationNote = null,
        string? hmacSecret = null,
        string? allowedIps = null)
    {
        var trimmedName = Trim(name);
        if (trimmedName is null)
        {
            return Error.Validation("davomat.qurilma_nom_bosh", "Qurilma nomi kiritilishi shart.");
        }

        var secret = Trim(hmacSecret);
        if (authMode == DeviceAuthMode.TokenAndHmac && secret is null)
        {
            return Error.Validation(
                "davomat.imzo_siri_yoq",
                "Imzo rejimi uchun maxfiy kalit kiritilishi shart.");
        }

        EnsureToken(tokenHash, tokenPrefix);

        return new AttendanceDevice(NewId(), trimmedName, tokenHash, tokenPrefix, tokenIssuedAt)
        {
            TokenIssuedBy = tokenIssuedBy,
            Provider = provider,
            IdentityProvider = identityProvider,
            DirectionRole = directionRole,
            AuthMode = authMode,
            SerialNumber = Trim(serialNumber),
            Model = Trim(model),
            LegalEntityId = legalEntityId,
            DepartmentId = departmentId,
            LocationNote = Trim(locationNote),
            HmacSecret = secret,
            AllowedIps = Trim(allowedIps),
        };
    }

    /// <summary>Nomini o'zgartiradi.</summary>
    public Result Rename(string? name)
    {
        var trimmed = Trim(name);
        if (trimmed is null)
        {
            return Error.Validation("davomat.qurilma_nom_bosh", "Qurilma nomi kiritilishi shart.");
        }

        Name = trimmed;
        return Result.Success();
    }

    /// <summary>
    /// Qurilmaning sozlamalarini qayta yozadi.
    /// </summary>
    /// <remarks>
    /// Nom va token bu yerdan O'ZGARMAYDI: birinchisi
    /// <see cref="Rename"/> orqali (u nom takrorini tekshiradigan
    /// use-case bilan birga ketadi), ikkinchisi faqat
    /// <see cref="RotateToken"/> orqali — token almashuvi HR uchun
    /// alohida, qaytarib bo'lmaydigan amal.
    /// </remarks>
    public Result Reconfigure(
        AttendanceProvider provider,
        AttendanceIdentityProvider identityProvider,
        DeviceDirectionRole directionRole,
        DeviceAuthMode authMode,
        string? serialNumber = null,
        string? model = null,
        Guid? legalEntityId = null,
        Guid? departmentId = null,
        string? locationNote = null,
        string? hmacSecret = null,
        string? allowedIps = null)
    {
        var secret = Trim(hmacSecret);
        if (authMode == DeviceAuthMode.TokenAndHmac && secret is null)
        {
            return Error.Validation(
                "davomat.imzo_siri_yoq",
                "Imzo rejimi uchun maxfiy kalit kiritilishi shart.");
        }

        Provider = provider;
        IdentityProvider = identityProvider;
        DirectionRole = directionRole;
        AuthMode = authMode;
        SerialNumber = Trim(serialNumber);
        Model = Trim(model);
        LegalEntityId = legalEntityId;
        DepartmentId = departmentId;
        LocationNote = Trim(locationNote);
        HmacSecret = secret;
        AllowedIps = Trim(allowedIps);

        return Result.Success();
    }

    /// <summary>Tokenni almashtiradi — eskisi shu lahzada o'ladi.</summary>
    public void RotateToken(byte[] tokenHash, string tokenPrefix, DateTimeOffset at, Guid? by)
    {
        EnsureToken(tokenHash, tokenPrefix);

        TokenHash = tokenHash;
        TokenPrefix = tokenPrefix;
        TokenIssuedAt = at;
        TokenIssuedBy = by;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    /// <summary>So'rov tasdiqlashgacha yetib keldi.</summary>
    public void NoteSeen(DateTimeOffset at, string? ip)
    {
        LastSeenAt = at;
        LastSeenIp = Trim(ip) ?? LastSeenIp;
    }

    /// <summary>
    /// O'tish qabul qilindi.
    /// </summary>
    /// <remarks>
    /// <see cref="LastSeenAt"/> ni ham yangilaydi: qabul qilingan o'tish
    /// ta'rifi bo'yicha tasdiqlashdan o'tgan. Shu sababli chaqiruvchi
    /// <see cref="NoteSeen"/> ni unutsa ham, kuzatuv ustunlari zid
    /// holatga tushib qolmaydi.
    /// </remarks>
    public void NoteEvent(DateTimeOffset at, string? ip, int skewSeconds)
    {
        NoteSeen(at, ip);
        LastEventAt = at;
        LastSkewSeconds = skewSeconds;
    }

    /// <summary>Agent qurilma soatini o'lchadi.</summary>
    /// <param name="offsetSeconds">Qurilma vaqti minus server vaqti, soniyada.</param>
    /// <param name="at">O'lchov lahzasi.</param>
    public void NoteClock(int offsetSeconds, DateTimeOffset at)
    {
        NoteSeen(at, ip: null);
        ClockOffsetSeconds = offsetSeconds;
        ClockCheckedAt = at;
    }

    /// <summary>
    /// Token xeshining shakli — noto'g'ri bo'lsa ISTISNO.
    /// </summary>
    /// <remarks>
    /// Bu yagona joy, bu yerda <c>Result</c> emas, istisno ishlatiladi:
    /// xesh foydalanuvchi kiritmaydi, uni tizimning o'zi yasaydi. Noto'g'ri
    /// uzunlik — dastur xatosi, «kutilgan natija» emas (<c>Error</c>
    /// izohidagi qoida).
    /// </remarks>
    private static void EnsureToken(byte[] tokenHash, string tokenPrefix)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenPrefix);

        if (tokenHash.Length != TokenHashLength)
        {
            throw new ArgumentException(
                $"Token xeshi {TokenHashLength} bayt bo'lishi kerak.",
                nameof(tokenHash));
        }
    }

    private static string? Trim(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
