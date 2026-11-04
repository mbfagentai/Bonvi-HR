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

    /// <summary>
    /// «Mobil ilova» — SINTETIK qurilma qatorining qat'iy identifikatori.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Telefondan kelgan belgi ham o'tish bo'lib yoziladi, o'tishda esa
    /// <see cref="AttendancePass.DeviceId"/> MAJBURIY. Rad etilgan muqobil —
    /// uni <c>null</c> qilish: u jurnal, eksport, panel va o'qish modelining
    /// o'nlab joyiga tarqalardi, va eng yomoni
    /// <c>ux_attendance_passes_device_dedupe</c> ga tegardi — PostgreSQL
    /// unikal indeksda NULL larni BIR-BIRIDAN FARQLI deb hisoblaydi, ya'ni
    /// modulning eng muhim indeksi telefon belgilarini jimgina takrorlashga
    /// ruxsat berib qo'yardi.
    /// </para>
    /// <para>
    /// Qiymat qat'iy yozilgan: kodning bu qatorni TANIYDIGAN yagona yo'li shu
    /// (nom bo'yicha qidirish uni birinchi qayta nomlashda sindirardi).
    /// Qator migratsiya bilan yaratiladi.
    /// </para>
    /// </remarks>
    public static readonly Guid MobileAppId = Guid.Parse("01996f00-0000-7000-8000-00000000b071");

    /// <summary>
    /// «HR (qo'lda)» — HR qo'lda qo'shgan belgilarning SINTETIK qurilma qatori (SPEC-qolda-belgi §A).
    /// </summary>
    /// <remarks>
    /// <see cref="MobileAppId"/> bilan AYNAN bir xil sabab va bir xil qoida: faolsiz,
    /// tokeni o'lik, migratsiya yaratadi. Qiymat qat'iy yozilgan.
    /// </remarks>
    public static readonly Guid HrManualId = Guid.Parse("01996f00-0000-7000-8000-00000000b072");

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
    /// <remarks>
    /// ⚠️ «Mobil ilova» (<see cref="MobileAppId"/>) qatorida bu yerda 32 ta
    /// NOL bayt turadi va buni «tuzatish» kerak EMAS. Tasdiqlovchi qurilmani
    /// <c>SHA256(kelgan token)</c> bo'yicha qidiradi; butunlay nol bo'lgan
    /// SHA-256 natijasini beradigan satr amalda mavjud emas, ya'ni bu qator
    /// hech qachon tasdiqdan o'ta olmaydi. Tokenni «aylantirish» esa ilovaga
    /// ISHLAYDIGAN qabul tokeni berib qo'yardi — SPEC aynan shuni taqiqlaydi.
    /// </remarks>
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

    /// <summary>
    /// Agent oxirgi marta qachon murojaat qilgan — ro'yxat, hisobot, soat yoki holat.
    /// </summary>
    /// <remarks>
    /// <see cref="LastSeenAt"/> dan farqi: u HAR QANDAY tasdiqlangan so'rovda
    /// yangilanadi (o'zi push qiladigan terminalniki ham). Bu esa faqat
    /// agent endpointlarida — ya'ni «bu qurilma agent orqali ishlaydi»
    /// degan belgi ham shu (<c>null</c> ⇒ agent hech qachon gapirmagan).
    /// </remarks>
    public DateTimeOffset? AgentSeenAt { get; private set; }

    /// <summary>
    /// Terminalning O'ZI tasdiqlagan oxirgi aloqa (SPEC-agent §L).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Yangilanadi: agent terminal soatini o'qiganda
    /// (<see cref="NoteClock"/>), hodisa kelganda (qabul yo'li — hodisa
    /// terminal jurnalidan o'qilgan yoki terminalning o'zi yuborgan).
    /// Yangilanmaydi: agent faqat ro'yxat so'raganda — u terminalga yeta
    /// olmasa ham ro'yxat so'rayveradi.
    /// </para>
    /// <para>
    /// ⚠️ Aynan shu farq yo'qligi sababli uzilgan terminal «hozirgina»
    /// deb ko'rinib turardi (2026-09-27 tekshiruvi: <c>last_seen_at</c> —
    /// bugun, soat va hodisa — besh kun oldin).
    /// </para>
    /// </remarks>
    public DateTimeOffset? TerminalSeenAt { get; private set; }

    /// <summary>
    /// Agent terminaldan o'qigan seriya raqami — tokenni JISMONIY terminalga bog'laydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Birinchi ko'rilgani yoziladi, keyin talab qilinadi</b> (trust on
    /// first use). Boshqa terminal ro'yxatini shu token bilan so'rasa, server
    /// rad etadi (<c>davomat.terminal_mos_emas</c>): aks holda ikkinchi
    /// terminaldagi Bonvi odamlari «ro'yxatda yo'q» deb o'chirib tashlanardi.
    /// </para>
    /// <para>
    /// HR kiritadigan <see cref="SerialNumber"/> dan ALOHIDA: u korpusdagi
    /// yorliqdan qo'lda yoziladi va ISAPI qaytaradigan to'liq satrdan farq
    /// qiladi — bitta harf xatosi agentni butunlay to'xtatib qo'yardi.
    /// Terminal almashtirilsa, bog'lanish tokenni aylantirish bilan
    /// tozalanadi (<see cref="RotateToken"/>).
    /// </para>
    /// </remarks>
    public string? TerminalSerial { get; private set; }

    /// <summary>Oxirgi RAD ETILGAN hodisa so'rovi — o'qib bo'lmadi yoki vaqti yaroqsiz.</summary>
    /// <remarks>
    /// «Terminal gaplashyapti-yu, hodisalari rad etilyapti» ogohlantirishi
    /// shundan chiqadi (<see cref="EventsRejected"/>). Ilgari u
    /// <c>last_seen_at − last_event_at &gt; 30 daqiqa</c> dan taxmin
    /// qilinardi va agent qurilmasida HAR KECHASI yolg'on yonardi: agent
    /// har daqiqada ro'yxat so'raydi, tunda esa hech kim o'tmaydi.
    /// </remarks>
    public DateTimeOffset? LastRejectedAt { get; private set; }

    /// <summary>Agentning oxirgi solishtirish hisoboti qachon kelgan.</summary>
    public DateTimeOffset? ReconciledAt { get; private set; }

    /// <summary>
    /// Oxirgi solishtirishda agent ATAYLAB bajarmagan o'chirishlar soni.
    /// </summary>
    /// <remarks>
    /// Ro'yxat bo'sh bo'lsa yoki terminaldagi Bonvi odamlarining beshdan
    /// biridan ko'pi o'chirilishi kerak bo'lsa, agent o'chirmaydi
    /// (ommaviy o'chirish to'sig'i, SPEC-agent §L) va bu yerga sonini
    /// yozadi — HR kartada ko'radi va o'zi tasdiqlaydi
    /// (<see cref="ApproveRemovals"/>).
    /// </remarks>
    public int HeldRemovals { get; private set; }

    /// <summary>
    /// Terminaldagi Bonvi fazasidagi, lekin BU server ajratmagan odamlar soni.
    /// </summary>
    /// <remarks>
    /// Noldan katta bo'lsa — shu terminalga boshqa Bonvi o'rnatmasi (masalan
    /// sinov serveri) ham yozyapti. Agent ularga tegmaydi, lekin raqamlar
    /// to'qnashishi mumkin — HR buni ko'rishi shart.
    /// </remarks>
    public int ForeignPersons { get; private set; }

    /// <summary>
    /// HR ushlab qolingan o'chirishlarga shu lahzagacha ruxsat bergan (UTC).
    /// </summary>
    /// <remarks>
    /// Vaqt bilan cheklangan va bu ataylab: ruxsat BIR-IKKI solishtirish
    /// uchun beriladi (agent har daqiqada tekshiradi), doimiy «to'siqni
    /// o'chirish» tugmasi esa keyingi xato ro'yxatda ham hammani o'chirib
    /// yuborardi.
    /// </remarks>
    public DateTimeOffset? RemovalsApprovedUntil { get; private set; }

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

    /// <summary>
    /// Sintetik «Mobil ilova» qatori — terminal EMAS (<see cref="MobileAppId"/> izohi).
    /// </summary>
    /// <remarks>
    /// Qurilmalar sog'lig'i sanoqlariga (faol, ulangan, uzilgan) kirmaydi va
    /// ekranda boshqaruv tugmalarisiz ko'rsatiladi: uni tahrirlab, yoqib
    /// yoki o'chirib bo'lmaydi.
    /// </remarks>
    public bool IsSynthetic => Id == MobileAppId || Id == HrManualId || Provider == AttendanceProvider.Mobile;

    /// <summary>
    /// Soat ogohlantirishi: faol, so'nggi sutkada gapirgan va soati chegaradan og'gan.
    /// </summary>
    /// <remarks>
    /// Qurilmalar sahifasi, Statistika sahifasi va eski taxta BITTA qoidadan
    /// o'qiydi — ilgari taxta boshqa o'lchovni (<see cref="LastSkewSeconds"/>)
    /// ishlatardi va internet uzilishidan keyin soxta banner yoqardi.
    /// </remarks>
    /// <param name="now">Hozirgi lahza (UTC).</param>
    public bool ClockWarningAt(DateTimeOffset now)
        => IsActive
            && HasClockDrift
            && LastSeenAt is { } seen
            && now - seen <= TimeSpan.FromHours(AttendanceRules.ClockWarningRecentHours);

    /// <summary>
    /// Terminal gaplashyapti, lekin oxirgi so'rovi rad etilgan va undan keyin
    /// birorta ham o'tish qabul qilinmagan.
    /// </summary>
    public bool EventsRejected
        => LastRejectedAt is { } rejected && (LastEventAt is not { } accepted || rejected > accepted);

    /// <summary>
    /// <paramref name="now"/> lahzasidagi aloqa holati va u qachondan beri shunday.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ Chegara JADVALGA bog'liq emas (<see cref="AttendanceRules.AgentSilenceMinutes"/>
    /// izohi): agent o'tish bo'lmasa ham har daqiqada gapiradi, ya'ni tunda
    /// ham jimlik — uzilish.
    /// </para>
    /// <para>
    /// O'zi push qiladigan terminal (agent hech qachon gapirmagan) uchun
    /// jimlik hech narsa demaydi — u faqat hodisa bo'lganda yuboradi. Uning
    /// holati faqat «ulangan» yoki «hech qachon ulanmagan».
    /// </para>
    /// </remarks>
    /// <param name="now">Hozirgi lahza (UTC).</param>
    /// <returns>Holat va boshlanish lahzasi; noma'lum bo'lsa lahza <c>null</c>.</returns>
    public (DeviceLinkState State, DateTimeOffset? Since) LinkAt(DateTimeOffset now)
    {
        if (LastSeenAt is null)
        {
            return (DeviceLinkState.NeverConnected, null);
        }

        if (AgentSeenAt is not { } agent)
        {
            return (DeviceLinkState.Online, LastSeenAt);
        }

        var limit = TimeSpan.FromMinutes(AttendanceRules.AgentSilenceMinutes);

        if (now - agent > limit)
        {
            return (DeviceLinkState.AgentOffline, agent);
        }

        // Terminal hech qachon javob bermagan bo'lsa ham — uzilish: agent
        // ro'yxat so'rab turibdi, lekin terminalga birinchi kundan yeta
        // olmayapti (noto'g'ri IP yoki parol). «Qachondan» — noma'lum.
        if (TerminalSeenAt is not { } terminal)
        {
            return (DeviceLinkState.TerminalUnreachable, null);
        }

        return now - terminal > limit
            ? (DeviceLinkState.TerminalUnreachable, terminal)
            : (DeviceLinkState.Online, terminal);
    }

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
    /// <remarks>
    /// Terminal bog'lanishi (<see cref="TerminalSerial"/>) ham TOZALANADI:
    /// yangi token — yangi o'rnatish, masalan almashtirilgan terminal. Yangi
    /// agentning birinchi so'rovi yangi seriyani yozadi.
    /// </remarks>
    public void RotateToken(byte[] tokenHash, string tokenPrefix, DateTimeOffset at, Guid? by)
    {
        EnsureToken(tokenHash, tokenPrefix);

        TokenHash = tokenHash;
        TokenPrefix = tokenPrefix;
        TokenIssuedAt = at;
        TokenIssuedBy = by;
        TerminalSerial = null;
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
        NoteTerminal(at);
        LastEventAt = at;
        LastSkewSeconds = skewSeconds;
    }

    /// <summary>Agent murojaat qildi — ro'yxat, hisobot, soat yoki holat.</summary>
    public void NoteAgent(DateTimeOffset at, string? ip)
    {
        NoteSeen(at, ip);
        AgentSeenAt = at;
    }

    /// <summary>Terminalning o'zi javob berdi — hodisasi keldi yoki soati o'qildi.</summary>
    public void NoteTerminal(DateTimeOffset at)
    {
        if (TerminalSeenAt is not { } seen || at > seen)
        {
            TerminalSeenAt = at;
        }
    }

    /// <summary>Hodisa so'rovi rad etildi (o'qib bo'lmadi, vaqt yaroqsiz).</summary>
    public void NoteRejected(DateTimeOffset at) => LastRejectedAt = at;

    /// <summary>Agent qurilma soatini o'lchadi.</summary>
    /// <remarks>
    /// Soat terminaldan o'qilgan — ya'ni bu ham terminalning o'zi javob
    /// bergani (<see cref="TerminalSeenAt"/>).
    /// </remarks>
    /// <param name="offsetSeconds">Qurilma vaqti minus server vaqti, soniyada.</param>
    /// <param name="at">O'lchov lahzasi.</param>
    public void NoteClock(int offsetSeconds, DateTimeOffset at)
    {
        NoteAgent(at, ip: null);
        NoteTerminal(at);
        ClockOffsetSeconds = offsetSeconds;
        ClockCheckedAt = at;
    }

    /// <summary>Terminal seriyasining eng katta uzunligi — ustun sig'imi.</summary>
    public const int TerminalSerialMaxLength = 100;

    /// <summary>Token boshqa terminal bilan ishlatilmoqda.</summary>
    public static Error TerminalMismatch { get; } = Error.Conflict(
        "davomat.terminal_mos_emas",
        "Bu token boshqa terminalga bog'langan. Terminal almashtirilgan bo'lsa, qurilma tokenini yangilang.");

    /// <summary>
    /// Agent gapirayotgan terminal shu qurilma yozuviga tegishlimi (trust on first use).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Seriya birinchi marta kelganda YOZILADI; keyin har so'rovda
    /// solishtiriladi. Farq qilsa — rad: shu token boshqa terminalni
    /// boshqarmoqchi (nusxalangan agent sozlamasi, almashtirilgan terminal).
    /// </para>
    /// <para>
    /// Seriya kelmasa: qurilma hali bog'lanmagan bo'lsa — o'tadi (eski
    /// agent yoki terminal hali o'qilmagan); bog'langan bo'lsa va
    /// <paramref name="required"/> — rad. Qabul yo'li uchun
    /// <paramref name="required"/> <c>false</c>: u yerga o'zi push qiladigan
    /// terminal ham keladi va u seriya yubormaydi.
    /// </para>
    /// </remarks>
    /// <param name="serial">Agent o'qigan seriya; berilmagan bo'lsa <c>null</c>.</param>
    /// <param name="required">Bog'langan qurilmada seriya majburiymi.</param>
    public Result ConfirmTerminal(string? serial, bool required)
    {
        var presented = Trim(serial);

        if (presented is null)
        {
            return TerminalSerial is not null && required ? TerminalMismatch : Result.Success();
        }

        if (presented.Length > TerminalSerialMaxLength)
        {
            return TerminalMismatch;
        }

        if (TerminalSerial is null)
        {
            TerminalSerial = presented;
            return Result.Success();
        }

        return string.Equals(TerminalSerial, presented, StringComparison.OrdinalIgnoreCase)
            ? Result.Success()
            : TerminalMismatch;
    }

    /// <summary>Agentning solishtirish hisoboti.</summary>
    /// <param name="at">Hisobot lahzasi.</param>
    /// <param name="ip">So'rov manzili.</param>
    /// <param name="heldRemovals">Ommaviy o'chirish to'sig'i ushlab qolgan o'chirishlar.</param>
    /// <param name="foreignPersons">Terminaldagi begona Bonvi-raqamli odamlar.</param>
    public void NoteReconcile(DateTimeOffset at, string? ip, int heldRemovals, int foreignPersons)
    {
        NoteAgent(at, ip);
        ReconciledAt = at;
        HeldRemovals = Math.Max(0, heldRemovals);
        ForeignPersons = Math.Max(0, foreignPersons);
    }

    /// <summary>HR ushlab qolingan o'chirishlarni tasdiqladi — bir necha daqiqaga.</summary>
    /// <param name="until">Ruxsat shu lahzagacha amal qiladi.</param>
    public void ApproveRemovals(DateTimeOffset until) => RemovalsApprovedUntil = until;

    /// <summary>HR ruxsati <paramref name="now"/> lahzasida amaldami.</summary>
    public bool RemovalsApprovedAt(DateTimeOffset now) => RemovalsApprovedUntil is { } until && now < until;

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
