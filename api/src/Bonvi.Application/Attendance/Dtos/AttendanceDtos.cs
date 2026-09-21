using Bonvi.Domain.Attendance;

namespace Bonvi.Application.Attendance.Dtos;

// ════════════════════════════════════════════════════════════════════
//  Davomat moduli DTO'lari — BITTA fayl, bo'limlarga ajratilgan.
//
//  ⚠️ Bu fayl KETMA-KET yoziladi (SPEC §10.1): uni B5 yaratdi, B6
//  qurilma / moslik / jadval DTO'lari bilan KENGAYTIRADI. Yangi DTO
//  mos bo'limga qo'shiladi — fayl oxiriga «boshqalar» bo'lib emas,
//  aks holda bir modulning 40 ta yozuvi orasidan kerakligini topib
//  bo'lmay qoladi.
//
//  Vaqtlar HAMMA JOYDA UTC lahza bo'lib ketadi (`DateTimeOffset`),
//  Toshkent vaqtiga ekran o'giradi. Server «09:03» degan matn
//  YUBORMAYDI: bir xil lahza ikki joyda ikki xil formatlanib qolardi.
//  Istisno — Excel fayli: u brauzerda ochilmaydi, shu sababli
//  `AttendanceExcelWriter` Toshkentga o'zi o'giradi.
// ════════════════════════════════════════════════════════════════════

// ────────────────────────────────────────────────────────────────────
//  1. O'tishlar jurnali (SPEC §6.5)
// ────────────────────────────────────────────────────────────────────

/// <summary>
/// Bitta o'tish — qurilma NIMA DEGAN bo'lsa, o'shanday.
/// </summary>
/// <remarks>
/// <para>
/// Bu yerda talqin YO'Q: yo'nalishning navbat bilan taxmini (§5.5)
/// <c>attendance_days</c> ga yoziladi, o'tishga qaytarilmaydi. Jurnal
/// «qurilma aslida nima dedi» degan savolga javob beradi va bahsda aynan
/// shu kerak.
/// </para>
/// <para>
/// <b>Ikkita rasm kaliti yonma-yon.</b> <see cref="PhotoKey"/> — turniket
/// suratga olgan YUZ, <see cref="EmployeePhotoKey"/> — xodim kartasidagi
/// rasm. HR ularni yonma-yon qo'yib «bu rostdan o'shami» deb tekshiradi;
/// bittasi yetmaydi.
/// </para>
/// <para>
/// Bo'lim va lavozim — O'TISH SANASIDA amalda bo'lgan asosiy mehnat
/// munosabatidan (§5.1), bugungisidan emas: o'tgan oyning jurnali xodim
/// boshqa bo'limga o'tgani uchun o'zgarib qolmasligi kerak.
/// </para>
/// </remarks>
/// <param name="Id">O'tish identifikatori.</param>
/// <param name="OccurredAt">O'tish lahzasi (UTC).</param>
/// <param name="LocalDate">O'tish tushgan Toshkent kalendar sanasi.</param>
/// <param name="ReceivedAt">Server qabul qilgan lahza.</param>
/// <param name="ClockSkewSeconds">
/// <c>received_at − occurred_at</c>, ishorali. Qurilma soati qanchaga
/// adashganini ko'rsatadi.
/// </param>
/// <param name="SkewWarning">
/// Og'ish chegaradan (<see cref="AttendanceRules.ClockSkewToleranceSeconds"/>)
/// oshganmi. Serverda hisoblanadi — chegara klientda ikkinchi marta
/// yozilsa, bir kun ikkalasi ajralib ketadi.
/// </param>
/// <param name="OffsetAssumed">Qurilma vaqt siljishini yubormagan va +5 taxmin qilingan.</param>
/// <param name="Direction">Qurilma aytgan yo'nalish.</param>
/// <param name="DirectionSource">Yo'nalish qayerdan olingan.</param>
/// <param name="Method">Shaxs qanday tasdiqlangan.</param>
/// <param name="DeviceId">Qaysi qurilma.</param>
/// <param name="DeviceName">Qurilma nomi — keyin o'chirilgan qurilmaniki ham: jurnal o'tishning manbasini yo'qotmasligi kerak.</param>
/// <param name="DeviceLocation">
/// Qurilmaning ERKIN MATNLI joyi («Ofis», «Sex kirishi»). Bu ma'lumotnoma
/// EMAS — tizimda «lokatsiya» degan yozuv turi yo'q (SPEC-panel §A), ya'ni
/// maydon faqat ko'rsatish uchun va filtr sifatida ishlatilmaydi.
/// </param>
/// <param name="DeviceProvider">Qurilma turi (Hikvision / Generic) — qurilma o'chirilgan bo'lsa <c>null</c>.</param>
/// <param name="IdentityProvider">Shaxs raqami qaysi raqamlar fazosida.</param>
/// <param name="RawPersonId">Qurilmadagi shaxs raqami — moslik yo'q bo'lsa ekranda SHU ko'rinadi.</param>
/// <param name="EmployeeId">Xodim. <c>null</c> — moslanmagan o'tish.</param>
/// <param name="EmployeeName">Xodimning to'liq ismi.</param>
/// <param name="EmployeePhotoKey">Xodim kartasidagi rasm kaliti.</param>
/// <param name="Department">O'tish sanasidagi bo'lim nomi.</param>
/// <param name="Position">O'tish sanasidagi lavozim nomi.</param>
/// <param name="PhotoKey">
/// Turniket suratining kaliti. Baytlar <c>/passes/{id}/photo</c> orqali
/// olinadi. <c>null</c> — surat bo'lmagan yoki tozalash ishi o'chirgan.
/// </param>
/// <param name="PhotoPurgedAt">To'ldirilgan bo'lsa — surat saqlash muddati tugab o'chirilgan.</param>
/// <param name="LateMinutes">
/// Kechikish daqiqalari — FAQAT shu o'tish xodimning o'z kunidagi BIRINCHI kirishi
/// bo'lsa va o'sha kun kechikkan bo'lsa; boshqa har bir o'tishda <c>null</c>.
/// Manba — saqlangan kun yozuvi (<c>first_in_at</c> aynan shu lahza); bugungi hali
/// qayta hisoblanmagan o'tishda — taxtadagi AYNAN o'sha hisoblash. Klient kechikishni
/// «Kechikkanlar» hisobotidan qidirib moslamaydi: bu ikkinchi, mo'rt nusxa bo'lardi.
/// </param>
public sealed record AttendancePassDto(
    Guid Id,
    DateTimeOffset OccurredAt,
    DateOnly LocalDate,
    DateTimeOffset ReceivedAt,
    int ClockSkewSeconds,
    bool SkewWarning,
    bool OffsetAssumed,
    PassDirection Direction,
    DirectionSource DirectionSource,
    PassMethod Method,
    Guid DeviceId,
    string? DeviceName,
    string? DeviceLocation,
    AttendanceProvider? DeviceProvider,
    AttendanceIdentityProvider IdentityProvider,
    string RawPersonId,
    Guid? EmployeeId,
    string? EmployeeName,
    string? EmployeePhotoKey,
    string? Department,
    string? Position,
    string? PhotoKey,
    DateTimeOffset? PhotoPurgedAt,
    int? LateMinutes);

// ────────────────────────────────────────────────────────────────────
//  2. Jonli taxta va ishdan ketganlar ogohlantirishi (SPEC §6.5, §8.7)
// ────────────────────────────────────────────────────────────────────

/// <summary>«Hozir binoda» taxtasidagi bitta odam.</summary>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="FullName">To'liq ism.</param>
/// <param name="PhotoKey">Xodim rasmi — taxtada yuz ko'rinadi, bosh harflar emas.</param>
/// <param name="Department">Bugungi asosiy munosabatdagi bo'lim.</param>
/// <param name="Position">Bugungi asosiy munosabatdagi lavozim.</param>
/// <param name="SinceAt">Oxirgi <c>In</c> o'tishning lahzasi — «08:03 dan beri».</param>
/// <param name="LastPassId">O'sha o'tish — jurnaldagi yozuvga havola uchun.</param>
/// <param name="LastDeviceName">O'sha o'tishni yozgan qurilma.</param>
public sealed record BoardItemDto(
    Guid EmployeeId,
    string FullName,
    string? PhotoKey,
    string? Department,
    string? Position,
    DateTimeOffset SinceAt,
    Guid LastPassId,
    string? LastDeviceName);

/// <summary>
/// Ishdan ketgan (yoki umuman ishlamaydigan) odamning o'tishi — XAVFSIZLIK hodisasi.
/// </summary>
/// <remarks>
/// <para>
/// Shart <see cref="AttendanceDayStatus.NotEmployed"/> bilan bir xil
/// (o'tish sanasini qamraydigan mehnat munosabati yo'q), lekin u tungi
/// hisoblashni KUTMAYDI — to'g'ridan-to'g'ri o'tishdan hisoblanadi.
/// Talabning butun ma'nosi shu: bu yerda ishlamaydigan odam HOZIRGINA
/// binoga kirdi.
/// </para>
/// <para>
/// ⚠️ Ro'yxatda har bir ODAM bir marta — uning ENG SO'NGGI o'tishi bilan,
/// qolganlari <see cref="PassCount"/> da (SPEC §6.5). Nav nishoni «N ta» va
/// qurilmalar sahifasi «N xodim o'tdi» deydi: o'tish bo'yicha sanalsa, bir
/// marta kirib-chiqqan odam «2 xodim» bo'lib ko'rinardi.
/// </para>
/// </remarks>
/// <param name="PassId">O'tish — jurnalga havola (<c>?passId=</c>).</param>
/// <param name="EmployeeId">Xodim kartasiga havola.</param>
/// <param name="FullName">To'liq ism.</param>
/// <param name="PhotoKey">Xodim kartasidagi rasm.</param>
/// <param name="OccurredAt">O'tish lahzasi (UTC).</param>
/// <param name="DeviceId">Kirgizib yuborgan qurilma — tuzatish AYNAN shu yerda qilinadi.</param>
/// <param name="DeviceName">Qurilma nomi.</param>
/// <param name="Direction">Yo'nalish.</param>
/// <param name="TerminationDate">
/// Xodimning eng so'nggi tugagan munosabatining bo'shash sanasi. <c>null</c> —
/// odam umuman ishga olinmagan (yoki qabul sanasi hali kelmagan).
/// </param>
/// <param name="LegalEntityName">O'sha tugagan munosabatning firmasi.</param>
/// <param name="PassCount">
/// Shu odamning oynadagi shunday o'tishlari soni. Qator faqat eng
/// so'nggisini ko'rsatadi; «3 marta o'tdi» — bir marta adashib kirgan
/// emas, propuski hamon ishlayotgani haqida dalil.
/// </param>
public sealed record FormerEmployeeAlertDto(
    Guid PassId,
    Guid EmployeeId,
    string FullName,
    string? PhotoKey,
    DateTimeOffset OccurredAt,
    Guid DeviceId,
    string? DeviceName,
    PassDirection Direction,
    DateOnly? TerminationDate,
    string? LegalEntityName,
    int PassCount);

/// <summary>
/// Faqat ishdan ketganlar ogohlantirishi — nav nishoni va qurilmalar sahifasi uchun.
/// </summary>
/// <remarks>
/// Taxtadagi <see cref="AttendanceBoardDto.FormerEmployeeAlerts"/> bilan
/// AYNAN bir xil qatorlar (bitta metod hisoblaydi — ikkalasi hech qachon
/// bir-biriga zid chiqmaydi). Alohida so'rov, chunki nishon HAR ekranda
/// turadi: butun taxtani har 30 soniyada har bir sahifadan so'rash
/// kutilayotganlar, kechikkanlar va jadvallarni behuda qayta hisoblardi.
/// </remarks>
/// <param name="Count">Nechta ODAM (o'tish emas).</param>
/// <param name="Items">Qatorlar, eng yangisi birinchi.</param>
public sealed record FormerEmployeeAlertsDto(
    int Count,
    IReadOnlyList<FormerEmployeeAlertDto> Items);

/// <summary>«Hozir kim ishda» taxtasi.</summary>
/// <remarks>
/// <para>
/// ⚠️ <see cref="LateCount"/> HALI KELMAGANLARNI ham sanaydi (SPEC §6.5):
/// soat 09:30 da «5 kechikdi» ichida yetib kelmagan to'rt kishi ham
/// bo'lishi shart, aks holda taxta boshqa savolga javob beradi.
/// </para>
/// <para>
/// Filtrlar (<c>legalEntityId</c>, <c>departmentId</c>, <c>search</c>)
/// sanoqlarga va <see cref="Items"/> ga ta'sir qiladi,
/// <see cref="FormerEmployeeAlerts"/> ga EMAS — bo'lim filtri yashira
/// oladigan xavfsizlik ogohlantirishi ogohlantirish emas.
/// </para>
/// </remarks>
/// <param name="AsOf">Taxta qaysi lahza uchun hisoblangan — «yangilandi HH:mm».</param>
/// <param name="InsideCount">Hozir ichkaridagilar soni.</param>
/// <param name="ExpectedCount">Bugun jadval bo'yicha kutilayotganlar.</param>
/// <param name="AbsentCount">Kutilganlardan bugun birorta o'tishi yo'qlar.</param>
/// <param name="LateCount">Kutilganlardan kechikkanlar — hali kelmaganlar bilan birga.</param>
/// <param name="ClockWarningDeviceCount">Soati adashgan faol qurilmalar soni.</param>
/// <param name="Items">
/// Ichkaridagilar, ISM bo'yicha — kartalar har 30 soniyalik yangilanishda
/// joyida qolsin (kirish vaqti bo'yicha tartibda har yangi kirish butun
/// panjarani siljitardi).
/// </param>
/// <param name="FormerEmployeeAlerts">Ishdan ketganlar ogohlantirishi — filtrsiz.</param>
public sealed record AttendanceBoardDto(
    DateTimeOffset AsOf,
    int InsideCount,
    int ExpectedCount,
    int AbsentCount,
    int LateCount,
    int ClockWarningDeviceCount,
    IReadOnlyList<BoardItemDto> Items,
    IReadOnlyList<FormerEmployeeAlertDto> FormerEmployeeAlerts);

// ────────────────────────────────────────────────────────────────────
//  2a. Asosiy sahifa — kunlik davomat paneli (GET /dashboard)
// ────────────────────────────────────────────────────────────────────

/// <summary>Paneldagi odamning shu kundagi holati — diagrammaning oltita bo'lagi.</summary>
/// <remarks>
/// <para>
/// ⚠️ <b><see cref="AttendanceDayStatus"/> emas — ataylab.</b> Kun holati tabel
/// uchun va kun TUGAGANDAN keyingi savolga javob beradi («chiqish yozilmagan»).
/// Panel esa kun DAVOMIDA «kim qanday» deydi: 09:30 da chiqishi yo'q odam
/// anomaliya emas, u ishda. Shu sababli bu yerda kechikish holatga aylanadi
/// (tabelda u alohida son) va tabelda yo'q «hali boshlanmagan» paydo bo'ladi.
/// </para>
/// <para>
/// Saqlanmaydi — faqat javob va filtr, shuning uchun Domain'da emas
/// (<c>ExceptionKind</c> bilan bir xil sabab). Raqamlar baribir aniq: mijoz
/// nomini yuboradi, lekin sanoq kengaysa eski qiymatlar siljimasligi kerak.
/// </para>
/// <para>
/// Shu sanada mehnat munosabati yo'q odam uchun qiymat YO'Q: u panelga umuman
/// tushmaydi. Unday odamning o'tishi — taxtadagi ishdan ketganlar ogohlantirishi.
/// </para>
/// </remarks>
public enum AttendanceDashboardStatus
{
    /// <summary>Kelgan, yon berish ichida: kelgan lahza ≤ reja boshlanishi + kelishdagi yon berish.</summary>
    OnTime = 1,

    /// <summary>Kelgan, yon berishdan keyin — daqiqasi REJA BOSHLANISHIDAN sanaladi.</summary>
    Late = 2,

    /// <summary>Ish kuni, o'tish yo'q, smena allaqachon boshlangan (o'tgan sana — doim shu).</summary>
    Absent = 3,

    /// <summary>Ish kuni, o'tish yo'q, smena hali boshlanmagan: kelajak sana yoki bugun reja boshlanishidan oldin.</summary>
    NotStarted = 4,

    /// <summary>Jadval bo'yicha dam olish kuni — o'tish bo'lsa ham.</summary>
    DayOff = 5,

    /// <summary>Munosabat bor, lekin jadval topilmadi.</summary>
    NoSchedule = 6,

    /// <summary>
    /// Sababli — kelmagan, lekin tasdiqlangan yo'qlik so'rovi bor
    /// (SPEC-so'rovlar §B3).
    /// </summary>
    /// <remarks>
    /// ⚠️ Faqat <see cref="Absent"/> ning o'rnini bosadi va faqat shu:
    /// kelgan odam (hatto kechikkan bo'lsa ham) «Sababli» bo'lmaydi, smenasi
    /// boshlanmagan kun ham. Tabeldagi <c>AttendanceDayStatus.Excused</c>
    /// bilan AYNAN bir xil qoida — bir kun ikki ekranda ikki xil o'qilmasin.
    /// </remarks>
    Excused = 7,
}

/// <summary>Panel diagrammasi: oltita holat bo'yicha odamlar soni.</summary>
/// <remarks>
/// Oltitasining yig'indisi DOIM <see cref="AttendanceDashboardDto.Total"/> ga
/// teng — har bir odam aynan bitta holatda. Qidiruv va holat filtri bu yerga
/// TA'SIR QILMAYDI: diagramma «bugun bo'lim qanday» degan savolga javob beradi,
/// jadvalda bitta odamni qidirish esa uni «vaqtida kelgan: 1» ga aylantirmasligi
/// kerak.
/// </remarks>
/// <param name="OnTime">Vaqtida kelganlar.</param>
/// <param name="Late">Kechikkanlar.</param>
/// <param name="Absent">Kelmaganlar.</param>
/// <param name="NotStarted">Smenasi hali boshlanmaganlar.</param>
/// <param name="DayOff">Dam olish kunidagilar.</param>
/// <param name="NoSchedule">Jadvali yo'qlar.</param>
/// <param name="Excused">Sababli — tasdiqlangan so'rovi borlar.</param>
public sealed record AttendanceDashboardCountsDto(
    int OnTime,
    int Late,
    int Absent,
    int NotStarted,
    int DayOff,
    int NoSchedule,
    int Excused);

/// <summary>Panel jadvalidagi bitta xodim.</summary>
/// <remarks>
/// Bo'lim va lavozim — SHU SANADAGI asosiy munosabatdan (§5.1), bugungisidan
/// emas: o'tgan kunning paneli xodim keyin boshqa bo'limga o'tgani uchun
/// o'zgarib qolmasligi kerak (jurnal bilan bir xil qoida).
/// </remarks>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="FullName">To'liq ism.</param>
/// <param name="PhotoKey">Xodim kartasidagi rasm — turniket surati EMAS.</param>
/// <param name="PositionName">Shu sanadagi asosiy munosabatning lavozimi.</param>
/// <param name="DepartmentName">Shu sanadagi asosiy munosabatning bo'limi.</param>
/// <param name="ArrivedAt">
/// Kunning birinchi kirishi (UTC). Kirish yo'q-u, o'tish bor bo'lsa — kunning
/// birinchi o'tishi. <c>null</c> — shu kunga tegishli o'tish yo'q.
/// </param>
/// <param name="LeftAt">Kunning oxirgi chiqishi (UTC) — tungi smenada ertangi kunga tushadi; chiqish yo'q — <c>null</c>.</param>
/// <param name="Status">Holat.</param>
/// <param name="LateMinutes">
/// Faqat <see cref="AttendanceDashboardStatus.Late"/> da: reja boshlanishidan
/// kelgan lahzagacha butun daqiqalar (kesilgan). Boshqa holatda <c>null</c>.
/// </param>
public sealed record AttendanceDashboardItemDto(
    Guid EmployeeId,
    string FullName,
    string? PhotoKey,
    string? PositionName,
    string? DepartmentName,
    DateTimeOffset? ArrivedAt,
    DateTimeOffset? LeftAt,
    AttendanceDashboardStatus Status,
    int? LateMinutes);

/// <summary>Asosiy sahifa paneli: holatlar diagrammasi va sahifalangan xodimlar jadvali.</summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Ikki xil «jami».</b> <see cref="Total"/> va <see cref="Counts"/> — firma,
/// bo'lim va lavozim filtridan KEYINGI, lekin qidiruv va holat filtridan OLDINGI
/// to'plam. <see cref="TotalItems"/> va <see cref="PageCount"/> — jadvalning o'zi,
/// hamma filtrdan keyin. Diagramma bo'lagini bosib jadvalni shu holat bilan
/// cheklash diagrammaning o'zini o'zgartirmasligi kerak.
/// </para>
/// <para>
/// Xom o'tishlardan, shu zahoti (taxta kabi), saqlangan kun yozuvlaridan EMAS:
/// bugun 15 daqiqalik qayta hisoblashni kutmaydi, o'tgan sana esa xuddi shu
/// qoida bilan chiqadi.
/// </para>
/// </remarks>
/// <param name="Date">Qaysi Toshkent sanasi.</param>
/// <param name="IsToday">Sana bugunmi — «hozir» degan yozuv faqat shunda ma'noli.</param>
/// <param name="AsOf">Server holatlarni hisoblagan lahza — «yangilandi HH:mm».</param>
/// <param name="Total">To'plamdagi odamlar: shu sanada munosabati bor, org-filtrga mos (qidiruv va holatdan oldin).</param>
/// <param name="Counts">Holatlar bo'yicha sanoq — <see cref="Total"/> bilan bir xil to'plam.</param>
/// <param name="Items">Shu sahifadagi xodimlar, ism bo'yicha.</param>
/// <param name="Page">Joriy sahifa (1 dan).</param>
/// <param name="PageSize">Sahifa hajmi.</param>
/// <param name="TotalItems">Jadvalga tushganlar — org-filtr, qidiruv va holatdan keyin.</param>
/// <param name="PageCount">Jami sahifalar (<see cref="TotalItems"/> bo'yicha).</param>
public sealed record AttendanceDashboardDto(
    DateOnly Date,
    bool IsToday,
    DateTimeOffset AsOf,
    int Total,
    AttendanceDashboardCountsDto Counts,
    IReadOnlyList<AttendanceDashboardItemDto> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int PageCount);

/// <summary>Davr grafigining bitta kuni — <see cref="AttendanceTrendDto"/> ning qatori.</summary>
/// <remarks>
/// <para>
/// Sanoqlar panelning O'ZI bilan bir xil qoidadan (<c>DashboardStatusRule</c>):
/// bir kunlik davr tanlansa grafikning shu kundagi nuqtalari
/// <c>GET /dashboard</c> ning <c>counts</c> i bilan AYNAN teng chiqadi.
/// Ikki ekran bitta savolga ikki xil son bermasligi kerak.
/// </para>
/// <para>
/// ⚠️ Jadval qatori YO'Q va bu ataylab: 92 kunlik davrda har kunning har bir
/// xodimi ~50 ming qator bo'lardi. Grafik faqat kunning suratini ko'rsatadi;
/// bitta kunni ochish — o'sha kunning paneli.
/// </para>
/// </remarks>
/// <param name="Date">Toshkent sanasi.</param>
/// <param name="Total">Shu sanada munosabati bor va org-filtrga mos odamlar.</param>
/// <param name="Counts">Oltita holat — yig'indisi DOIM <paramref name="Total"/>.</param>
public sealed record AttendanceTrendDayDto(
    DateOnly Date,
    int Total,
    AttendanceDashboardCountsDto Counts);

/// <summary>Davr grafigi: kunma-kun holatlar (<c>GET /dashboard/trend</c>).</summary>
/// <remarks>
/// <para>
/// ⭐ <see cref="Days"/> da oraliqning HAR BIR sanasi bor, o'sish tartibida,
/// TIRQISHSIZ. Hech kim ishlamagan kun ham qatorda turadi — <c>Total = 0</c>
/// va oltita nol bilan. Sabab: grafik o'z o'qini o'zi to'qib chiqarmasligi
/// kerak, aks holda ta'til haftasi kalendarda umuman ko'rinmay, chiziq ikki
/// uzoq sanani to'g'ri chiziq bilan bog'lab qo'yardi.
/// </para>
/// <para>
/// Qidiruv, holat filtri, saralash va sahifa YO'Q: bu grafik, ro'yxat emas
/// (<c>GET /dashboard</c> dan farqi shu).
/// </para>
/// </remarks>
/// <param name="From">Davr boshi (Toshkent sanasi).</param>
/// <param name="To">Davr oxiri — ikkala chegara ham kiradi.</param>
/// <param name="AsOf">Server butun oraliqni hisoblagan lahza — BITTA qiymat, paneldagi bilan bir ma'noda.</param>
/// <param name="Days">Har bir sana, tirqishsiz.</param>
public sealed record AttendanceTrendDto(
    DateOnly From,
    DateOnly To,
    DateTimeOffset AsOf,
    IReadOnlyList<AttendanceTrendDayDto> Days);

// ────────────────────────────────────────────────────────────────────
//  2c. Tug'ilgan kunlar paneli (SPEC-panel §E)
// ────────────────────────────────────────────────────────────────────

/// <summary>Yaqinlashib kelayotgan bitta tug'ilgan kun.</summary>
/// <remarks>
/// <para>
/// <b>Bu bosh sahifadagi ro'yxat EMAS</b> (<c>DashboardBirthdayDto</c>). U
/// «bugun kimning tug'ilgan kuni» degan savolga javob beradi va filtrsiz;
/// bu yerdagi esa «keyingi navbatda kim» va u EKRANDAGI org-filtrga
/// bo'ysunadi (§E2). Ikki javob bir xil ko'rinsa ham bir xil savol emas —
/// shu sababli ikkita alohida DTO.
/// </para>
/// <para>
/// Lavozim va bo'lim — BUGUNGI asosiy munosabatdan (§5.1), panelning
/// jadvali bilan bir xil manbadan: bitta ekranda bir odam ikki xil bo'limda
/// ko'rinmasligi kerak.
/// </para>
/// </remarks>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="FullName">To'liq ism.</param>
/// <param name="PhotoKey">Xodim kartasidagi rasm — turniket surati EMAS.</param>
/// <param name="PositionName">Bugungi asosiy munosabatning lavozimi.</param>
/// <param name="DepartmentName">Bugungi asosiy munosabatning bo'limi.</param>
/// <param name="BirthDate">Tug'ilgan sana, YILI bilan.</param>
/// <param name="NextBirthday">
/// Keyingi nishonlanadigan sana — bugun ham kiradi. 29-fevralda tug'ilganlar
/// uchun bu KEYINGI KABISA yili (§E4.4): sun'iy 28-fevral yoki 1-mart
/// tanlanmaydi.
/// </param>
/// <param name="DaysUntil">Bugundan <paramref name="NextBirthday"/> gacha kunlar; bugun — 0.</param>
/// <param name="Age">
/// <paramref name="NextBirthday"/> da to'ladigan yosh — sana SHUBHALI bo'lsa
/// (<c>&gt; 0 and &lt; 120</c> dan tashqarida) <c>null</c>. Chegara bosh
/// sahifadagi ro'yxat bilan AYNAN bir xil.
/// </param>
public sealed record AttendanceBirthdayDto(
    Guid EmployeeId,
    string FullName,
    string? PhotoKey,
    string? PositionName,
    string? DepartmentName,
    DateOnly BirthDate,
    DateOnly NextBirthday,
    int DaysUntil,
    int? Age);

// ────────────────────────────────────────────────────────────────────
//  2d. Yo'qlik so'rovlari (SPEC-so'rovlar §B)
// ────────────────────────────────────────────────────────────────────

/// <summary>Bitta yo'qlik so'rovi.</summary>
/// <remarks>
/// <para>
/// Ism va yo'qlik turining NOMI javobda bor: ro'yxat ularsiz o'qilmaydi va
/// klient ikkinchi so'rov yuborishga majbur bo'lardi (modulning boshqa
/// ro'yxatlaridagi bilan bir xil qoida).
/// </para>
/// <para>
/// <see cref="FromTime"/>/<see cref="ToTime"/> — faqat BIR KUNLIK so'rovda
/// bo'lishi mumkin (§B2). Ikkalasi ham <c>null</c> — butun kun(lar).
/// </para>
/// </remarks>
/// <param name="Id">So'rov.</param>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="EmployeeName">Xodimning F.I.Sh.</param>
/// <param name="EmployeePhotoKey">Xodim kartasidagi rasm kaliti.</param>
/// <param name="PositionName">Bugungi asosiy munosabatning lavozimi.</param>
/// <param name="DepartmentName">Bugungi asosiy munosabatning bo'limi.</param>
/// <param name="AbsenceTypeId">Yo'qlik turi.</param>
/// <param name="AbsenceTypeName">Yo'qlik turining nomi.</param>
/// <param name="FromDate">Davr boshi (Toshkent sanasi), shu kun ham kiradi.</param>
/// <param name="ToDate">Davr oxiri, shu kun ham kiradi.</param>
/// <param name="FromTime">Kun ichidagi boshlanish vaqti — faqat bir kunlik so'rovda.</param>
/// <param name="ToTime">Kun ichidagi tugash vaqti — faqat bir kunlik so'rovda.</param>
/// <param name="Note">Xodimning izohi.</param>
/// <param name="Status">Holat.</param>
/// <param name="DecisionNote">HR ning qarordagi izohi.</param>
/// <param name="DecidedByName">Qarorni kim qabul qilgan.</param>
/// <param name="DecidedAt">Qaror lahzasi (UTC).</param>
/// <param name="CreatedAt">So'rov yaratilgan lahza (UTC) — ro'yxat shu bo'yicha tartiblanadi.</param>
public sealed record AbsenceRequestDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    string? EmployeePhotoKey,
    string? PositionName,
    string? DepartmentName,
    Guid AbsenceTypeId,
    string AbsenceTypeName,
    DateOnly FromDate,
    DateOnly ToDate,
    TimeOnly? FromTime,
    TimeOnly? ToTime,
    string? Note,
    AbsenceRequestStatus Status,
    string? DecisionNote,
    string? DecidedByName,
    DateTimeOffset? DecidedAt,
    DateTimeOffset CreatedAt);

/// <summary>Kutayotgan so'rovlar soni — nav nishoni uchun.</summary>
/// <remarks>
/// Ro'yxat emas, faqat son: nishon HAR ekranda turadi
/// (<c>FormerEmployeeAlertsDto</c> bilan bir xil naqsh).
/// </remarks>
/// <param name="Pending">Qaror kutayotgan so'rovlar.</param>
public sealed record AbsenceRequestCountDto(int Pending);

// ────────────────────────────────────────────────────────────────────
//  3. Tabel (SPEC §6.6, §5.7.1)
// ────────────────────────────────────────────────────────────────────

/// <summary>
/// Tabelning bitta katagi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ Katak oyning HAR BIR kuni uchun keladi. Yozuvi yo'q kunlar SERVERDA
/// hosil qilinadi (§5.7.1) va barcha daqiqa maydonlari 0 bo'ladi: klient
/// bo'shliqni o'zi to'ldirmaydi, chunki «ishlamaydi», «dam olish» va «hali
/// hisoblanmagan» orasidagi farqni faqat server biladi.
/// </para>
/// <para>
/// <b>Reja vaqtlari ham shu yerda</b> (<see cref="PlanStartAt"/>,
/// <see cref="PlanEndAt"/>). Ular xodim kartasidagi kalendar uchun: ekran
/// «08:30–18:00 reja / 08:50–19:19 haqiqiy» degan ikki qatorni yonma-yon
/// chizadi. Klient ularni jadvaldan O'ZI hisoblay olmaydi va hisoblamasligi
/// ham kerak — kunning rejasi biriktirma, jadval va bo'lim daraxti orqali
/// aniqlanadi (<c>ScheduleResolver</c>), va u allaqachon kun yozuvida
/// SURAT bo'lib saqlangan: keyin jadval o'zgarsa ham o'tgan oyning
/// kalendari o'zgarmaydi.
/// </para>
/// <para>
/// <b>Tanaffus ikkala soni ham shu yerda.</b> Tanaffus ishlangan vaqtdan
/// AYRILADI (§5.5.1): binoda 8 soat turgan odamda «7:00» yozilganda HR
/// darhol «nega?» deb so'raydi va javob katak ochilganda ko'rinishi kerak.
/// Ikkalasi ham 0 — oyna belgilanmagan (yoki tanaffus yo'q) kun.
/// </para>
/// </remarks>
/// <param name="Date">Sana.</param>
/// <param name="Status">Kun holati — saqlangan yoki hosil qilingan.</param>
/// <param name="WorkedMinutes">Ishlangan daqiqalar (tanaffus ushlanmasi ayrilgandan keyin).</param>
/// <param name="LateMinutes">Kechikish, daqiqa.</param>
/// <param name="EarlyLeaveMinutes">Erta ketish, daqiqa.</param>
/// <param name="FirstInAt">Birinchi kirish (UTC).</param>
/// <param name="LastOutAt">Oxirgi chiqish (UTC) — tungi smenada ertangi kunga tushadi.</param>
/// <param name="BreakTakenMinutes">Tanaffus oynasi ichida binodan tashqarida o'tgan daqiqalar.</param>
/// <param name="BreakDeductionMinutes">Ishlangan vaqtdan ayrilgan tanaffus daqiqalari.</param>
/// <param name="PlanStartAt">
/// Kun REJASINING boshlanishi (UTC) — jadval bo'yicha, haqiqiy kelish emas.
/// <c>null</c>: dam olish kuni, jadvali yo'q kun yoki hali hisoblanmagan
/// (hosil qilingan) katak.
/// </param>
/// <param name="PlanEndAt">
/// Reja tugashi (UTC) — tungi smenada ERTANGI kunga tushadi, xuddi
/// <paramref name="LastOutAt"/> kabi.
/// </param>
public sealed record TimesheetCellDto(
    DateOnly Date,
    AttendanceDayStatus Status,
    int WorkedMinutes,
    int LateMinutes,
    int EarlyLeaveMinutes,
    DateTimeOffset? FirstInAt,
    DateTimeOffset? LastOutAt,
    int BreakTakenMinutes,
    int BreakDeductionMinutes,
    DateTimeOffset? PlanStartAt,
    DateTimeOffset? PlanEndAt);

/// <summary>
/// Bir xodimning oy bo'yicha jami ko'rsatkichlari.
/// </summary>
/// <remarks>
/// Kun sanoqlari HOLAT bo'yicha va bir-birini kesmaydi:
/// <see cref="PresentDays"/> — faqat <c>Present</c>, «chiqish yozilmagan»
/// kun unga KIRMAYDI. Sabab: xulosa hisobotida o'sha kunlar alohida
/// «muammoli kunlar» ustunida turadi va ikki joyda sanalsa, yig'indi
/// oydagi kunlar sonidan oshib ketardi.
/// </remarks>
/// <param name="WorkedMinutes">Ishlangan daqiqalar yig'indisi.</param>
/// <param name="PlannedMinutes">Rejadagi daqiqalar yig'indisi (hosil qilingan kataklar 0 beradi).</param>
/// <param name="PresentDays"><c>Present</c> holatidagi kunlar.</param>
/// <param name="AbsentDays"><c>Absent</c> holatidagi kunlar — hosil qilinganlari bilan birga.</param>
/// <param name="LateMinutes">Kechikishlar yig'indisi.</param>
/// <param name="EarlyLeaveMinutes">Erta ketishlar yig'indisi.</param>
public sealed record TimesheetTotalsDto(
    int WorkedMinutes,
    int PlannedMinutes,
    int PresentDays,
    int AbsentDays,
    int LateMinutes,
    int EarlyLeaveMinutes);

/// <summary>Tabelning bitta qatori — bitta xodim, oyning barcha kunlari.</summary>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="FullName">To'liq ism.</param>
/// <param name="PhotoKey">Xodim rasmi.</param>
/// <param name="Department">Oy oxiridagi (yoki oxirgi ishlagan kunidagi) asosiy bo'lim.</param>
/// <param name="Position">Xuddi shu kundagi lavozim.</param>
/// <param name="Cells">Oyning HAR BIR kuni uchun bitta katak, sana tartibida.</param>
/// <param name="Totals">Oy bo'yicha jami.</param>
public sealed record TimesheetRowDto(
    Guid EmployeeId,
    string FullName,
    string? PhotoKey,
    string? Department,
    string? Position,
    IReadOnlyList<TimesheetCellDto> Cells,
    TimesheetTotalsDto Totals);

/// <summary>Tabel — xodim bo'yicha sahifalangan.</summary>
/// <param name="Year">Yil.</param>
/// <param name="Month">Oy (1–12).</param>
/// <param name="Days">Oyning barcha kunlari — ustunlar shu ro'yxatdan chiziladi.</param>
/// <param name="Items">Shu sahifadagi xodimlar.</param>
/// <param name="Page">Joriy sahifa (1 dan).</param>
/// <param name="PageSize">Sahifa hajmi.</param>
/// <param name="Total">Filtrga mos xodimlar soni.</param>
/// <param name="PageCount">Jami sahifalar.</param>
/// <param name="LastComputedAt">
/// Qaytarilgan qatorlardagi eng so'nggi hisoblash lahzasi
/// (<c>max(computed_at)</c>) — «Oxirgi hisoblash: HH:mm» (§8.7).
/// <c>null</c> — bu sahifada umuman hisoblangan kun yo'q.
/// </param>
public sealed record AttendanceTimesheetDto(
    int Year,
    int Month,
    IReadOnlyList<DateOnly> Days,
    IReadOnlyList<TimesheetRowDto> Items,
    int Page,
    int PageSize,
    int Total,
    int PageCount,
    DateTimeOffset? LastComputedAt);

// ────────────────────────────────────────────────────────────────────
//  4. Hisobotlar — chetlanishlar va xulosa (SPEC §6.6)
// ────────────────────────────────────────────────────────────────────

/// <summary>Chetlanishlar hisobotining bitta qatori — bitta xodimning bitta kuni.</summary>
/// <remarks>
/// Bo'lim va lavozim — kun yozuvidagi SURATdan, xodimning bugungi
/// holatidan emas: hisobot ko'chirishdan keyin ham o'zgarmasligi kerak
/// (§2.6).
/// </remarks>
/// <param name="Date">Kun.</param>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="EmployeeName">To'liq ism.</param>
/// <param name="PhotoKey">Xodim rasmi.</param>
/// <param name="Department">Kun yozuvidagi bo'lim.</param>
/// <param name="Position">Kun yozuvidagi lavozim.</param>
/// <param name="PlanStartAt">Rejadagi boshlanish (UTC).</param>
/// <param name="PlanEndAt">Rejadagi tugash (UTC).</param>
/// <param name="FirstInAt">Birinchi kirish (UTC).</param>
/// <param name="LastOutAt">Oxirgi chiqish (UTC).</param>
/// <param name="LateMinutes">Kechikish, daqiqa.</param>
/// <param name="EarlyLeaveMinutes">Erta ketish, daqiqa.</param>
/// <param name="WorkedMinutes">Ishlangan daqiqalar.</param>
/// <param name="Status">Kun holati.</param>
public sealed record AttendanceExceptionDto(
    DateOnly Date,
    Guid EmployeeId,
    string EmployeeName,
    string? PhotoKey,
    string? Department,
    string? Position,
    DateTimeOffset? PlanStartAt,
    DateTimeOffset? PlanEndAt,
    DateTimeOffset? FirstInAt,
    DateTimeOffset? LastOutAt,
    int LateMinutes,
    int EarlyLeaveMinutes,
    int WorkedMinutes,
    AttendanceDayStatus Status);

/// <summary>Xulosa hisobotining bitta qatori — bitta xodim, butun davr.</summary>
/// <remarks>
/// <para>
/// Faqat SAQLANGAN kun yozuvlaridan yig'iladi. Hosil qilinadigan
/// kataklar (§5.7.1) bu yerga kirmaydi: «dam olish» va «ishlamagan»
/// hech narsani sanamaydi, hosil qilingan «kelmagan» esa faqat
/// qayta hisoblash hali yetib kelmagan kunda bo'ladi.
/// </para>
/// <para>
/// Kun ustunlari bir-birini kesmaydi (sababi <see cref="TimesheetTotalsDto"/> da).
/// Bo'lim va lavozim — davrdagi ENG SO'NGGI kun yozuvining surati.
/// </para>
/// </remarks>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="EmployeeName">To'liq ism.</param>
/// <param name="PhotoKey">Xodim rasmi.</param>
/// <param name="Department">Davrning oxirgi yozuvidagi bo'lim.</param>
/// <param name="Position">Davrning oxirgi yozuvidagi lavozim.</param>
/// <param name="PlannedDays">Rejada ish kuni bo'lgan kunlar (<c>planned_minutes &gt; 0</c>).</param>
/// <param name="PresentDays"><c>Present</c> kunlar.</param>
/// <param name="AbsentDays"><c>Absent</c> kunlar.</param>
/// <param name="DayOffWorkedDays">Dam olish kunida ishlangan kunlar.</param>
/// <param name="WorkedMinutes">Ishlangan daqiqalar.</param>
/// <param name="PlannedMinutes">Rejadagi daqiqalar.</param>
/// <param name="LateMinutes">Kechikishlar yig'indisi.</param>
/// <param name="EarlyLeaveMinutes">Erta ketishlar yig'indisi.</param>
/// <param name="OvertimeMinutes">Ortiqcha ishlangan vaqt — hisoblanadi, HECH QACHON pulga aylanmaydi.</param>
/// <param name="ProblemDays"><c>MissingExit + MissingEntry + NoSchedule</c> — «hisoblab bo'lmagan» kunlar.</param>
public sealed record AttendanceSummaryDto(
    Guid EmployeeId,
    string EmployeeName,
    string? PhotoKey,
    string? Department,
    string? Position,
    int PlannedDays,
    int PresentDays,
    int AbsentDays,
    int DayOffWorkedDays,
    int WorkedMinutes,
    int PlannedMinutes,
    int LateMinutes,
    int EarlyLeaveMinutes,
    int OvertimeMinutes,
    int ProblemDays);

// ────────────────────────────────────────────────────────────────────
//  5. Eksport (SPEC §6.7)
// ────────────────────────────────────────────────────────────────────

/// <summary>Excel faylga aylantirilgan davomat hisoboti.</summary>
/// <param name="Content"><c>.xlsx</c> faylining baytlari.</param>
/// <param name="FileName">Yuklab olinadigan fayl nomi (SPEC §6.7 dagi shakl).</param>
public sealed record AttendanceExportFile(byte[] Content, string FileName);

// ────────────────────────────────────────────────────────────────────
//  6. Qurilmalar (SPEC §6.2)
// ────────────────────────────────────────────────────────────────────

/// <summary>
/// Ro'yxatga olingan terminal — boshqaruv ekrani uchun.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Token bu yerda HECH QACHON yo'q</b> — faqat <see cref="TokenPrefix"/>.
/// Token bir marta, berilgan lahzada <see cref="AttendanceDeviceCreatedDto"/>
/// bilan qaytadi. HMAC siri esa umuman qaytmaydi, hatto yashirilgan holda
/// ham: faqat <see cref="HasHmacSecret"/>.
/// </para>
/// <para>
/// Firma va bo'lim nomlari O'QISH paytida qo'shiladi (surat emas): qurilma
/// bugun qayerda turganini ko'rsatadi, tarixni emas.
/// </para>
/// </remarks>
/// <param name="Id">Qurilma.</param>
/// <param name="Name">Nom.</param>
/// <param name="SerialNumber">Seriya raqami.</param>
/// <param name="Model">Model.</param>
/// <param name="Provider">Hodisani qaysi tahlilchi o'qiydi.</param>
/// <param name="IdentityProvider">Shaxs raqamlari qaysi fazoda.</param>
/// <param name="DirectionRole">Yo'nalish qayerdan olinadi.</param>
/// <param name="LegalEntityId">Firma.</param>
/// <param name="LegalEntityName">Firma nomi.</param>
/// <param name="DepartmentId">Bo'lim.</param>
/// <param name="DepartmentName">Bo'lim nomi.</param>
/// <param name="LocationNote">Joylashuvi.</param>
/// <param name="AuthMode">Tasdiqlash usuli.</param>
/// <param name="HasHmacSecret">Imzo siri qo'yilganmi — sirning o'zi qaytmaydi.</param>
/// <param name="AllowedIps">Ruxsat etilgan manbalar (CIDR ro'yxati).</param>
/// <param name="TokenPrefix">Tokenning birinchi 8 belgisi — ekranda qurilmalarni ajratish uchun.</param>
/// <param name="TokenIssuedAt">Token qachon berilgan.</param>
/// <param name="IsActive">Faolmi.</param>
/// <param name="LastSeenAt">Tasdiqdan o'tgan oxirgi so'rov.</param>
/// <param name="LastEventAt">Oxirgi qabul qilingan o'tish.</param>
/// <param name="LastSeenIp">Oxirgi so'rov manzili.</param>
/// <param name="LastSkewSeconds">Oxirgi o'tishdagi soat og'ishi.</param>
/// <param name="PassCount24h">So'nggi 24 soatda qabul qilingan o'tishlar (<c>received_at</c> bo'yicha).</param>
/// <param name="SkewWarning">
/// Soat og'ishi chegaradan oshganmi — SERVERDA hisoblanadi
/// (<see cref="AttendanceDevice.HasClockDrift"/>), klient qayta hisoblamaydi.
/// </param>
public sealed record AttendanceDeviceDto(
    Guid Id,
    string Name,
    string? SerialNumber,
    string? Model,
    AttendanceProvider Provider,
    AttendanceIdentityProvider IdentityProvider,
    DeviceDirectionRole DirectionRole,
    Guid? LegalEntityId,
    string? LegalEntityName,
    Guid? DepartmentId,
    string? DepartmentName,
    string? LocationNote,
    DeviceAuthMode AuthMode,
    bool HasHmacSecret,
    string? AllowedIps,
    string TokenPrefix,
    DateTimeOffset TokenIssuedAt,
    bool IsActive,
    DateTimeOffset? LastSeenAt,
    DateTimeOffset? LastEventAt,
    string? LastSeenIp,
    int? LastSkewSeconds,
    int PassCount24h,
    bool SkewWarning);

/// <summary>Yangi qurilma yoki yangilangan token — tokenning YAGONA ko'rinishi.</summary>
/// <remarks>
/// Ichma-ich (<c>{ device, token }</c>), tekis emas: qurilma DTO si bitta qayta
/// ishlatiladigan tur bo'lib qoladi va bir martalik sir undan ko'rinib ajralib
/// turadi (SPEC §6.2). Bu javob yo'qolsa, tokenni qayta ko'rish yo'li yo'q —
/// faqat yangisini berish.
/// </remarks>
/// <param name="Device">Qurilma.</param>
/// <param name="Token">Ochiq token — terminal sozlamasiga shu zahoti yoziladi.</param>
public sealed record AttendanceDeviceCreatedDto(AttendanceDeviceDto Device, string Token);

// ────────────────────────────────────────────────────────────────────
//  7. Mosliklar va Verifix importi (SPEC §4.2–§4.4, §6.3)
// ────────────────────────────────────────────────────────────────────

/// <summary>Qurilma raqami ↔ xodim moslik yozuvi.</summary>
/// <param name="Id">Moslik.</param>
/// <param name="Provider">Raqamlar fazosi.</param>
/// <param name="ExternalPersonId">Qurilmadagi shaxs raqami.</param>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="EmployeeName">Xodimning to'liq ismi.</param>
/// <param name="PhotoKey">XODIM kartasidagi rasm (modul bo'yicha yagona qoida, §6.5).</param>
/// <param name="DisplayNameInDevice">Qurilma ro'yxatidagi F.I.Sh.</param>
/// <param name="Source">Import yoki qo'lda.</param>
/// <param name="Note">Izoh.</param>
/// <param name="CreatedAt">Qachon bog'langan.</param>
public sealed record AttendanceIdentityDto(
    Guid Id,
    AttendanceIdentityProvider Provider,
    string ExternalPersonId,
    Guid EmployeeId,
    string? EmployeeName,
    string? PhotoKey,
    string? DisplayNameInDevice,
    IdentitySource Source,
    string? Note,
    DateTimeOffset CreatedAt);

/// <summary>Moslanmagan bitta shaxs raqami — uning barcha o'tishlari bir guruhda (SPEC §4.2).</summary>
/// <remarks>
/// Ekran <see cref="SamplePassId"/> orqali YUZNI ko'rsatadi: HR raqamni
/// emas, odamni taniydi.
/// </remarks>
/// <param name="Provider">Raqamlar fazosi.</param>
/// <param name="RawPersonId">Qurilmadagi shaxs raqami.</param>
/// <param name="PassCount">Moslanmagan o'tishlar soni.</param>
/// <param name="FirstSeenAt">Eng birinchi o'tish lahzasi.</param>
/// <param name="LastSeenAt">Eng so'nggi o'tish lahzasi.</param>
/// <param name="LastDeviceId">So'nggi o'tishni yozgan qurilma.</param>
/// <param name="LastDeviceName">O'sha qurilmaning nomi.</param>
/// <param name="SamplePassId">Surati bor eng yangi o'tish; surati bor o'tish yo'q bo'lsa <c>null</c>.</param>
public sealed record UnmatchedGroupDto(
    AttendanceIdentityProvider Provider,
    string RawPersonId,
    int PassCount,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt,
    Guid LastDeviceId,
    string? LastDeviceName,
    Guid? SamplePassId);

/// <summary>Import qatori xodimga qanday moslandi — faqat DTO uchun (SPEC §2.7).</summary>
/// <remarks>Qiymatlar tartibi SPEC dagi bilan bir xil; mijozga NOM bilan boradi.</remarks>
public enum IdentityMatchKind
{
    /// <summary>JSHSHIR bitta xodimniki bilan aynan mos.</summary>
    ExactPinfl = 1,

    /// <summary>F.I.Sh. (yig'ilgan holda) bitta xodimniki bilan aynan mos.</summary>
    ExactName = 2,

    /// <summary>F.I.Sh. 2…5 xodimga mos — operator tanlaydi.</summary>
    Ambiguous = 3,

    /// <summary>Mos xodim topilmadi (yoki juda ko'p).</summary>
    None = 4,

    /// <summary>Bu shaxs raqami allaqachon bog'langan.</summary>
    AlreadyMapped = 5,
}

/// <summary>Import qatoriga nomzod xodim.</summary>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="FullName">To'liq ism.</param>
/// <param name="Pinfl">JSHSHIR.</param>
/// <param name="Department">Bugungi (yoki oxirgi) asosiy munosabatdagi bo'lim.</param>
/// <param name="Position">Xuddi shu munosabatdagi lavozim.</param>
/// <param name="PhotoKey">Xodim kartasidagi rasm.</param>
public sealed record ImportCandidateDto(
    Guid EmployeeId,
    string FullName,
    string? Pinfl,
    string? Department,
    string? Position,
    string? PhotoKey);

/// <summary>Import faylining bitta qatori va uning moslash natijasi.</summary>
/// <param name="RowNumber">Fayldagi qator raqami (Excel'dagidek, 1 dan).</param>
/// <param name="RawPersonId">Shaxs raqami.</param>
/// <param name="FullNameInFile">Fayldagi F.I.Sh.</param>
/// <param name="PinflInFile">Fayldagi JSHSHIR.</param>
/// <param name="MatchKind">Qaysi qoida ishladi.</param>
/// <param name="SuggestedEmployeeId">Taklif qilingan xodim (<c>AlreadyMapped</c> da — hozir bog'langani).</param>
/// <param name="SuggestedEmployeeName">O'sha xodimning ismi.</param>
/// <param name="Candidates">Nomzodlar: aniq moslikda bitta, noaniqda 2…5, topilmaganda bo'sh.</param>
public sealed record IdentityImportRowDto(
    int RowNumber,
    string RawPersonId,
    string? FullNameInFile,
    string? PinflInFile,
    IdentityMatchKind MatchKind,
    Guid? SuggestedEmployeeId,
    string? SuggestedEmployeeName,
    IReadOnlyList<ImportCandidateDto> Candidates);

/// <summary>Importni oldindan ko'rish — HECH NARSA yozilmagan (SPEC §4.4).</summary>
/// <remarks>
/// Serverda saqlanmaydi: mijoz qatorlarni ushlab turadi va faqat tasdiqlangan
/// qarorlarni qaytarib yuboradi. Qoralama uchun jadval, muddat va tozalash
/// vazifasi kerak bo'lardi (§4.4).
/// </remarks>
/// <param name="Provider">Qaysi fazoga moslangan — tasdiqlashda aynan shu qaytariladi.</param>
/// <param name="TotalRows">O'qilgan ma'lumot qatorlari (sarlavha va shaxs raqamisiz qatorlarsiz).</param>
/// <param name="Rows">Har bir qator.</param>
public sealed record IdentityImportPreviewDto(
    AttendanceIdentityProvider Provider,
    int TotalRows,
    IReadOnlyList<IdentityImportRowDto> Rows);

/// <summary>Tasdiqlashda yaratilmagan qator.</summary>
/// <param name="RawPersonId">Shaxs raqami.</param>
/// <param name="Code">Sabab — xato kodi (tarjima kaliti).</param>
public sealed record IdentityImportFailureDto(string RawPersonId, string Code);

/// <summary>Import tasdig'ining natijasi.</summary>
/// <param name="Created">Yaratilgan mosliklar.</param>
/// <param name="Skipped">Allaqachon bog'langani uchun o'tkazib yuborilganlar — xato emas.</param>
/// <param name="Failed">Yaratilmaganlar va sababi.</param>
public sealed record IdentityImportResultDto(
    int Created,
    int Skipped,
    IReadOnlyList<IdentityImportFailureDto> Failed);

// ────────────────────────────────────────────────────────────────────
//  8. Jadvallar va biriktirmalar (SPEC §6.4)
// ────────────────────────────────────────────────────────────────────

/// <summary>Jadvalning bitta kuni.</summary>
/// <param name="DayIndex">ISO hafta kuni (1 — dushanba … 7 — yakshanba).</param>
/// <param name="IsWorking">Ish kunimi.</param>
/// <param name="StartTime">Smena boshlanishi.</param>
/// <param name="EndTime">Smena tugashi.</param>
/// <param name="BreakMinutes">Tanaffus uzunligi (daqiqa).</param>
/// <param name="BreakStart">Tanaffus oynasining boshlanishi.</param>
/// <param name="BreakEnd">Tanaffus oynasining tugashi.</param>
/// <param name="BreakCrossesMidnight">
/// Oyna yarim tundan o'tadimi — AYNAN <c>breakEnd &lt; breakStart</c> soat
/// qiymati bo'yicha; oyna yo'q bo'lsa <c>false</c>. Saqlanmaydi, hisoblanadi
/// (<c>CrossesMidnight</c> bilan bir xil sabab).
/// </param>
public sealed record WorkScheduleDayDto(
    int DayIndex,
    bool IsWorking,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    int BreakMinutes,
    TimeOnly? BreakStart,
    TimeOnly? BreakEnd,
    bool BreakCrossesMidnight);

/// <summary>Yillik to'r katagining manbai.</summary>
public enum ScheduleDaySource
{
    /// <summary>Haftalik naqshdan kelgan — bu sanaga hech narsa yozilmagan.</summary>
    Pattern = 1,

    /// <summary>Shu SANAGA yozilgan katak — naqshni bekor qiladi.</summary>
    Date = 2,
}

/// <summary>Yillik to'rning bitta katagi (SPEC-jadval-to'ri §C).</summary>
/// <remarks>
/// <para>
/// <see cref="PlannedMinutes"/> — ekranda soat bo'lib chiqadigan son
/// (8,5). U SERVERDA hisoblanadi: tanaffus ayirilishi va tungi smenaning
/// uzunligi qoidalari hisoblash dvigatelida yashaydi va klientda
/// takrorlanmasligi kerak.
/// </para>
/// <para>
/// <see cref="Source"/> — katak naqshdanmi yoki qo'lda yozilganmi. Ekran
/// shu bo'yicha kulrang va to'q kataklarni ajratadi: HR qaysi kunni o'zi
/// o'zgartirganini ko'rib turishi kerak.
/// </para>
/// </remarks>
/// <param name="Date">Toshkent sanasi.</param>
/// <param name="IsWorking">Ish kunimi.</param>
/// <param name="StartTime">Boshlanish vaqti — dam olish kunida <c>null</c>.</param>
/// <param name="EndTime">Tugash vaqti.</param>
/// <param name="BreakMinutes">Ishlangan vaqtdan ayiriladigan tanaffus.</param>
/// <param name="PlannedMinutes">Kun normasi (tanaffus ayirilgan).</param>
/// <param name="Source">Naqshdanmi yoki sanaga yozilganmi.</param>
public sealed record ScheduleYearDayDto(
    DateOnly Date,
    bool IsWorking,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    int BreakMinutes,
    int PlannedMinutes,
    ScheduleDaySource Source);

/// <summary>Jadvalning bir yillik to'ri.</summary>
/// <param name="ScheduleId">Jadval.</param>
/// <param name="ScheduleName">Nomi.</param>
/// <param name="Year">Yil.</param>
/// <param name="Days">Yilning HAR BIR kuni, o'sish tartibida, tirqishsiz.</param>
public sealed record ScheduleYearDto(
    Guid ScheduleId,
    string ScheduleName,
    int Year,
    IReadOnlyList<ScheduleYearDayDto> Days);

/// <summary>Ish jadvali — yettala kuni bilan.</summary>
/// <param name="Id">Jadval.</param>
/// <param name="Name">Nom.</param>
/// <param name="Kind">Turi (v1 da faqat haftalik).</param>
/// <param name="GraceInMinutes">Kelishdagi yon berish.</param>
/// <param name="GraceOutMinutes">Ketishdagi yon berish.</param>
/// <param name="IsActive">Faolmi.</param>
/// <param name="Note">Izoh.</param>
/// <param name="Days">Kunlar, <c>DayIndex</c> tartibida.</param>
public sealed record WorkScheduleDto(
    Guid Id,
    string Name,
    WorkScheduleKind Kind,
    int GraceInMinutes,
    int GraceOutMinutes,
    bool IsActive,
    string? Note,
    IReadOnlyList<WorkScheduleDayDto> Days);

/// <summary>Jadval biriktirmasi.</summary>
/// <remarks>
/// <see cref="Scope"/> ga mos keladigan id/nom juftligi to'ldirilgan, qolgan
/// ikkitasi <c>null</c>; sukut pog'onada uchalasi ham <c>null</c> — bazadagi
/// tekshiruv cheklovining aynan o'zi (§2.5). Nomlar o'qish paytida qo'shiladi.
/// </remarks>
/// <param name="Id">Biriktirma.</param>
/// <param name="WorkScheduleId">Jadval.</param>
/// <param name="WorkScheduleName">Jadval nomi.</param>
/// <param name="Scope">Pog'ona.</param>
/// <param name="EmployeeId">Xodim (xodim pog'onasida).</param>
/// <param name="EmployeeName">Xodimning ismi.</param>
/// <param name="PhotoKey">
/// Xodim kartasidagi rasm — faqat xodim pog'onasida (modul qoidasi, §6.5): ro'yxat
/// odamni yuzi bilan ko'rsatadi, rasm manzilini sinab ko'rib bosh harfga tushmaydi.
/// </param>
/// <param name="PositionId">Lavozim (lavozim pog'onasida).</param>
/// <param name="PositionName">Lavozim nomi.</param>
/// <param name="DepartmentId">Bo'lim (bo'lim pog'onasida).</param>
/// <param name="DepartmentName">Bo'lim nomi.</param>
/// <param name="EffectiveFrom">Kuchga kirish sanasi.</param>
/// <param name="EffectiveTo">Tugash sanasi (shu kun ham kiradi); <c>null</c> — muddatsiz.</param>
public sealed record ScheduleAssignmentDto(
    Guid Id,
    Guid WorkScheduleId,
    string? WorkScheduleName,
    ScheduleScope Scope,
    Guid? EmployeeId,
    string? EmployeeName,
    string? PhotoKey,
    Guid? PositionId,
    string? PositionName,
    Guid? DepartmentId,
    string? DepartmentName,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo);

/// <summary>
/// «Nega bu odamda shu jadval?» — <c>GET /schedules/resolve</c> ning to'liq javobi.
/// </summary>
/// <remarks>
/// <para>
/// Natija faqat ikki null bilan aniqlanadi, shu tartibda:
/// <see cref="EmploymentId"/> <c>null</c> — shu sanada ishlamaydi (qolgan
/// hamma maydon ham <c>null</c>); aks holda <see cref="Scope"/> <c>null</c> —
/// jadval topilmadi; aks holda hal qilingan.
/// </para>
/// <para>
/// ⭐ <see cref="MatchedDepartmentId"/> — «nega unda zavodning jadvali»
/// savolining butun javobi: g'olib biriktirma odamning o'z bo'limida emas,
/// uning OTA bo'limida turgan bo'lishi mumkin (§5.2).
/// </para>
/// </remarks>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="EmployeeName">Xodimning ismi.</param>
/// <param name="Date">So'ralgan sana.</param>
/// <param name="EmploymentId">Sanani qamragan asosiy munosabat (§5.1).</param>
/// <param name="LegalEntityId">O'sha munosabatning firmasi.</param>
/// <param name="LegalEntityName">Firma nomi.</param>
/// <param name="DepartmentId">O'sha munosabatning bo'limi — 3-pog'onaning kirishi.</param>
/// <param name="DepartmentName">Bo'lim nomi.</param>
/// <param name="PositionId">O'sha munosabatning lavozimi — 2-pog'onaning kirishi.</param>
/// <param name="PositionName">Lavozim nomi.</param>
/// <param name="Scope">Yutgan pog'ona.</param>
/// <param name="AssignmentId">Yutgan biriktirma.</param>
/// <param name="EffectiveFrom">Biriktirmaning boshlanishi.</param>
/// <param name="EffectiveTo">Biriktirmaning tugashi.</param>
/// <param name="WorkScheduleId">Hal qilingan jadval.</param>
/// <param name="WorkScheduleName">Jadval nomi.</param>
/// <param name="MatchedDepartmentId">Faqat bo'lim pog'onasida: biriktirmasi yutgan bo'lim (ota bo'lim bo'lishi mumkin).</param>
/// <param name="MatchedDepartmentName">O'sha bo'limning nomi.</param>
/// <param name="Day">Jadvalning shu sana hafta kuniga to'g'ri keladigan kuni.</param>
public sealed record ScheduleResolutionDto(
    Guid EmployeeId,
    string? EmployeeName,
    DateOnly Date,
    Guid? EmploymentId,
    Guid? LegalEntityId,
    string? LegalEntityName,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? PositionId,
    string? PositionName,
    ScheduleScope? Scope,
    Guid? AssignmentId,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo,
    Guid? WorkScheduleId,
    string? WorkScheduleName,
    Guid? MatchedDepartmentId,
    string? MatchedDepartmentName,
    WorkScheduleDayDto? Day);

// ────────────────────────────────────────────────────────────────────
//  9. Qayta hisoblash (SPEC §6.7)
// ────────────────────────────────────────────────────────────────────

/// <summary>Qayta hisoblash navbatga qo'yildi — <c>202 Accepted</c>.</summary>
/// <param name="JobId">Vazifa identifikatori.</param>
/// <param name="EmployeeCount">Nechta xodim qayta hisoblanadi.</param>
/// <param name="DayCount">Davrdagi kalendar kunlar soni (ikki chekka ham kiradi).</param>
public sealed record AttendanceRecomputeAcceptedDto(string JobId, int EmployeeCount, int DayCount);
