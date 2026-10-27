using Bonvi.Application.Attendance.Dtos;
using Bonvi.Domain.Attendance;

namespace Bonvi.Application.Attendance.Abstractions;

/// <summary>
/// Davomat bo'yicha <b>o'qish</b> porti — jurnal, jonli taxta, tabel va hisobotlar.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega repozitoriylar yetmaydi.</b> <c>IAttendanceDayRepository</c> va
/// boshqalar agregatni identifikator bo'yicha yuklaydi — bu yozish
/// tomonining ishi. Bu ekranlar esa boshqa narsani so'raydi: o'tish,
/// qurilma, xodim, mehnat munosabati va ma'lumotnoma nomlarini BIRLASHTIRGAN,
/// filtrlangan va sahifalangan jadval (<c>IEmployeeReadModel</c> bilan
/// bir xil sabab).
/// </para>
/// <para>
/// ⭐ <b>Saqlangan kun QAYTA HISOBLANMAYDI va QAYTA YOZILMAYDI.</b> Tabel
/// va hisobotlar <c>attendance_days</c> qatorlarini o'qiydi: hisobot
/// ochilganda kunni qayta hisoblash ekranni sekinlashtirar va «tabelda bir
/// son, hisobotda boshqa son» degan holatni ochib berardi. Hisoblash kodi
/// (<c>Attendance/Scheduling</c>, <c>Attendance/Computation</c>) faqat ikki
/// joyda CHAQIRILADI — qatori YO'Q katakning holatini hosil qilishda
/// (§5.7.1) va taxtaning «bugun kim kutilmoqda / kechikdi» savolida — va
/// ikkalasida ham qoida qayta yozilmaydi, tungi hisoblashning o'zi
/// ishlatiladi. Shunda saqlangan va hosil qilingan katak, taxta va tabel
/// bir-biriga zid gapira olmaydi.
/// </para>
/// <para>
/// <b>Hajm.</b> Yiliga ~2 mln o'tish va ~200 ming kun yozuvi. Filtr,
/// sahifalash va sanash BAZADA; xotirada faqat cheklangan to'plamlar
/// qayta ishlanadi (bir sahifaning kataklari, oxirgi 30 soat o'tishlari)
/// — har bir metod izohida chegarasi yozilgan.
/// </para>
/// <para>
/// ⚠️ Bu fayl KETMA-KET yoziladi (SPEC §10.1): B5 yaratdi, B6 moslanmagan
/// o'tishlar va import nomzodlari bilan kengaytiradi. Yangi metod mos
/// bo'limga qo'shiladi.
/// </para>
/// <para>Amalga oshirilishi Infrastructure qatlamida (<c>AttendanceReadModel</c>).</para>
/// </remarks>
public interface IAttendanceReadModel
{
    // ── 1. O'tishlar jurnali ────────────────────────────────────────

    /// <summary>O'tishlar jurnali — sahifalangan, sukut bo'yicha eng yangisi birinchi.</summary>
    /// <param name="filter">Filtr, saralash va sahifa.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<AttendancePage<AttendancePassDto>> SearchPassesAsync(
        PassSearchFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>Bitta o'tish — jurnaldagi bilan AYNAN bir xil shaklda.</summary>
    /// <remarks>
    /// Taxtadagi qizil ogohlantirish <c>?passId=</c> bilan jurnalga
    /// havola beradi; o'sha o'tish jurnalning joriy sahifasida bo'lmasligi
    /// mumkin. Shakl ro'yxatnikidan farq qilsa, bitta yozuv ikki joyda ikki
    /// xil ko'rinardi — shu sababli ikkalasi bitta proyeksiyadan yig'iladi.
    /// </remarks>
    /// <param name="passId">O'tish identifikatori.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    /// <returns>O'tish yoki <c>null</c> — bunday identifikator yo'q.</returns>
    Task<AttendancePassDto?> FindPassAsync(
        Guid passId,
        CancellationToken cancellationToken = default);

    // ── 2. Jonli taxta ──────────────────────────────────────────────

    /// <summary>«Hozir kim ishda» — to'g'ridan-to'g'ri o'tishlardan, kun yozuvlaridan EMAS.</summary>
    /// <remarks>
    /// Kun yozuvlari 15 daqiqada bir qayta hisoblanadi; taxta esa HOZIRGI
    /// holatni ko'rsatishi kerak. Shu sababli sanoqlar xom o'tishlar va
    /// mehnat munosabatlaridan shu zahoti yig'iladi.
    /// </remarks>
    /// <param name="filter">Firma, bo'lim va qidiruv. Ishdan ketganlar ogohlantirishiga TA'SIR QILMAYDI.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<AttendanceBoardDto> GetBoardAsync(
        BoardFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Faqat ishdan ketganlar ogohlantirishi — taxta ichidagi bilan bir xil qatorlar.
    /// </summary>
    /// <remarks>
    /// Nav nishoni va qurilmalar sahifasi uchun: ular HAR ekranda turadi va
    /// butun taxtani so'rashi kerak emas. Ikkala yo'l bitta ichki hisoblashni
    /// chaqiradi — ogohlantirish taxtada bor-u, nishonda yo'q bo'lib qolishi
    /// mumkin emas.
    /// </remarks>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyList<FormerEmployeeAlertDto>> GetFormerEmployeeAlertsAsync(
        CancellationToken cancellationToken = default);

    // ── 2a. Asosiy sahifa paneli ────────────────────────────────────

    /// <summary>
    /// Kunlik davomat paneli: holatlar bo'yicha sanoq va sahifalangan xodimlar jadvali.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Taxta kabi XOM O'TISHLARDAN, kun yozuvlaridan emas: bugun qayta
    /// hisoblashni kutmaydi, o'tgan sana esa xuddi shu qoida bilan chiqadi.
    /// O'tishning kuni, birinchi kirish va oxirgi chiqish — tungi hisoblashning
    /// o'zi (<c>AttendanceDayBuilder</c>), holat — <c>DashboardStatusRule</c>.
    /// </para>
    /// <para>
    /// <b>Hajm.</b> Holat butun to'plam (~540 odam) uchun XOTIRADA bir marta
    /// hisoblanadi, keyin filtrlanadi va sahifalanadi: sanoqlar har bir odamni
    /// talab qiladi, holat bo'yicha filtrni esa SQL da ifodalab bo'lmaydi.
    /// So'rovlar soni na to'plamga, na sahifa hajmiga bog'liq.
    /// </para>
    /// </remarks>
    /// <param name="filter">Sana, filtrlar, saralash va sahifa.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<DashboardPage> GetDashboardAsync(
        DashboardFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Davr grafigi: oraliqning HAR BIR kuni uchun holatlar bo'yicha sanoq.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Panelning o'zi bilan BIR XIL qoida — xom o'tishlar, <c>AttendanceDayBuilder</c>
    /// va <c>DashboardStatusRule</c>. Bir kunlik oraliq so'ralsa, natija
    /// <see cref="GetDashboardAsync"/> ning <c>Counts</c> i bilan aynan teng
    /// bo'lishi shart; ikkinchi qoida yozilmaydi.
    /// </para>
    /// <para>
    /// <b>Hajm.</b> Kun bo'yicha AYLANMAYDI: 92 kunga 92 ta so'rov ~14 soniya
    /// bo'lardi. O'tishlar BO'LAKLAB (<c>TrendPassChunkDays</c> kunlik) o'qiladi
    /// — so'rovlar soni oraliq uzunligiga emas, bo'laklar soniga bog'liq (92
    /// kunga 7 ta), xotirada esa bir vaqtning o'zida bir bo'lakning o'tishlari
    /// turadi. Munosabatlar, jadvallar va odamlar ro'yxati BIR MARTA yuklanadi.
    /// </para>
    /// <para>
    /// Jadval qatori qaytmaydi (<see cref="AttendanceTrendDto"/> izohi) —
    /// natija sanoqlarga yig'iladi, ya'ni xotira oraliq uzunligiga chiziqli
    /// emas, kunlar soniga teng oltita sondan iborat.
    /// </para>
    /// </remarks>
    /// <param name="filter">Oraliq va org-filtr.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<AttendanceTrendDto> GetDashboardTrendAsync(
        DashboardTrendFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Yaqinlashib kelayotgan tug'ilgan kunlar — eng yaqinidan boshlab.
    /// </summary>
    /// <remarks>
    /// <para>
    /// To'plam panelning BUGUNGI to'plami bilan bir xil: bugunni qamraydigan,
    /// org-filtrga mos munosabati bor xodimlar. Shu sababli bo'lim tanlansa
    /// ro'yxat ham o'sha bo'limnikiga aylanadi (§E2) — ekranda ikki xil
    /// odamlar to'plami yonma-yon turmaydi.
    /// </para>
    /// <para>
    /// <b>Bosh sahifadagi <c>BirthdayQuery</c> ishlatilmaydi</b> va bu ataylab:
    /// u FAQAT berilgan kunlarni biladi va org-filtri yo'q. Bu yerda esa
    /// «keyingi nechta odam» so'raladi — kun oynasi umuman yo'q.
    /// </para>
    /// <para>
    /// <b>Hajm.</b> To'plamning tug'ilgan sanalari (~950 tor qator) bir marta
    /// o'qiladi va xotirada tartiblanadi: keyingi sanani SQL da hisoblash
    /// 29-fevral va yil chegarasini bazaga ko'chirardi. Munosabat va
    /// ma'lumotnoma nomlari faqat CHIQQAN o'nta odam uchun yuklanadi.
    /// </para>
    /// </remarks>
    /// <param name="filter">Bugungi sana, org-filtr va ro'yxat uzunligi.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyList<AttendanceBirthdayDto>> GetUpcomingBirthdaysAsync(
        BirthdayFilter filter,
        CancellationToken cancellationToken = default);

    // ── 2d. Yo'qlik so'rovlari ──────────────────────────────────────

    /// <summary>Yo'qlik so'rovlari — xodim kartasi va umumiy navbat uchun.</summary>
    /// <remarks>
    /// <para>
    /// Bitta metod IKKI ekranga xizmat qiladi: xodim kartasida
    /// <c>EmployeeId</c> berilgan, umumiy navbatda esa yo'q. Ikkinchi metod
    /// yozilsa, ikkala ekran bir xil so'rovni ikki xil tartibda ko'rsatishi
    /// mumkin bo'lardi.
    /// </para>
    /// <para>
    /// Tartib: YANGISI birinchi (<c>created_at</c> kamayish bo'yicha).
    /// Kutayotgan so'rov tepada turishi kerak, HR esa ularni kelish
    /// tartibida ko'radi.
    /// </para>
    /// </remarks>
    /// <param name="filter">Xodim, holat, davr va sahifa.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<AttendancePage<AbsenceRequestDto>> SearchAbsenceRequestsAsync(
        AbsenceRequestFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>Bitta so'rov — qaror qabul qilingandan keyin javob uchun.</summary>
    /// <param name="id">So'rov.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<AbsenceRequestDto?> FindAbsenceRequestAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>Kutayotgan so'rovlar SONI — nav nishoni uchun.</summary>
    /// <remarks>
    /// Ro'yxat emas, faqat son: nishon har ekranda turadi va ro'yxatni
    /// yuklash unga kerak emas (<c>UnmatchedCount</c> bilan bir xil naqsh).
    /// </remarks>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<int> CountPendingAbsenceRequestsAsync(CancellationToken cancellationToken = default);

    // ── 3. Tabel ────────────────────────────────────────────────────

    /// <summary>
    /// Tabel: har bir xodimga oyning HAR BIR kuni uchun katak.
    /// </summary>
    /// <remarks>
    /// Saqlangan kunlar + yozuvi yo'q kunlar uchun §5.7.1 bo'yicha hosil
    /// qilingan kataklar. Hosil qilish XOTIRADA, bir marta bajariladi —
    /// sahifadagi xodimlarning kunlari, munosabatlari, biriktirmalari va
    /// jadvallari o'zgarmas sondagi so'rov bilan yuklangandan keyin.
    /// Katak uchun alohida so'rov YO'Q: 540 × 31 = 16 740 katak.
    /// </remarks>
    /// <param name="filter">Oy, filtrlar va sahifa.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<TimesheetPage> GetTimesheetAsync(
        TimesheetFilter filter,
        CancellationToken cancellationToken = default);

    // ── 4. Hisobotlar ───────────────────────────────────────────────

    /// <summary>Chetlanishlar: kechikkanlar, erta ketganlar, kelmaganlar yoki muammoli kunlar.</summary>
    /// <param name="filter">Tur, davr, filtrlar, saralash va sahifa.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<AttendancePage<AttendanceExceptionDto>> GetExceptionsAsync(
        ExceptionFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>Xulosa: davr bo'yicha har bir xodimning jami ko'rsatkichlari.</summary>
    /// <param name="filter">Davr, filtrlar, saralash va sahifa.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<AttendancePage<AttendanceSummaryDto>> GetSummaryAsync(
        SummaryFilter filter,
        CancellationToken cancellationToken = default);

    // ── 5. Qurilmalar ───────────────────────────────────────────────

    /// <summary>Har bir qurilma <paramref name="since"/> dan beri QABUL QILGAN o'tishlar soni.</summary>
    /// <remarks>
    /// <c>received_at</c> bo'yicha, <c>occurred_at</c> emas: savol «terminal
    /// bugun ishladimi», va soati adashgan qurilmaning hozir kelgan o'tishi
    /// ham shu sanoqqa kirishi kerak. Qurilma reyestri repozitoriydan keladi,
    /// bu faqat unga qo'shiladigan son — bitta guruhlangan so'rov.
    /// </remarks>
    /// <param name="deviceIds">Qurilmalar.</param>
    /// <param name="since">Quyi chegara (lahza).</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    /// <returns>Qurilma → son; o'tishi yo'q qurilma lug'atda bo'lmaydi.</returns>
    Task<IReadOnlyDictionary<Guid, int>> CountRecentPassesAsync(
        IReadOnlyCollection<Guid> deviceIds,
        DateTimeOffset since,
        CancellationToken cancellationToken = default);

    // ── 6. Mosliklar va import ──────────────────────────────────────

    /// <summary>Mosliklar ro'yxati — sahifalangan, xodim ismi bo'yicha.</summary>
    /// <param name="filter">Qidiruv, xodim, fazo va sahifa.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<AttendancePage<AttendanceIdentityDto>> SearchIdentitiesAsync(
        IdentitySearchFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>Bitta moslik — ro'yxatdagi bilan AYNAN bir xil shaklda.</summary>
    /// <param name="identityId">Moslik.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<AttendanceIdentityDto?> FindIdentityAsync(
        Guid identityId,
        CancellationToken cancellationToken = default);

    /// <summary>Moslanmagan o'tishlar, shaxs raqami bo'yicha guruhlangan — SPEC §4.2.</summary>
    /// <remarks>
    /// Eng ko'p o'tgani birinchi: kuniga o'n marta o'tayotgan odamni bog'lash
    /// bitta adashib kirgan mehmonnikidan ko'ra ko'proq tabel katagini tiklaydi.
    /// </remarks>
    /// <param name="filter">Sahifa.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<AttendancePage<UnmatchedGroupDto>> ListUnmatchedAsync(
        UnmatchedFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Import qatorlariga nomzod xodimlar — BARCHA qatorlar uchun bitta chaqiruvda.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>SPEC §2.10 dagi shakldan farq — ataylab.</b> U yerda metod bitta
    /// qator uchun (<c>fio, pinfl, take</c>). 5 000 qatorlik faylda bu 5 000 ta
    /// so'rov bo'lardi. Bu yerda so'rovlar soni qatorlar soniga bog'liq emas.
    /// </para>
    /// <para>
    /// Qaysi qoida yutishi (JSHSHIR → F.I.Sh. → noaniq) Application'da hal
    /// qilinadi; bu metod faqat mos kelganlarni beradi. Ism YIG'ILGAN holda
    /// solishtiriladi (<c>SearchFolding</c> — Infrastructure ichida, shuning
    /// uchun metod shu yerda). Transliteratsiya yo'q: lotinda yozilgan ism
    /// kirilldagi xodimga mos kelmaydi (loyiha qoidasi).
    /// </para>
    /// </remarks>
    /// <param name="lookups">Qatorlar — fayldagi tartibda.</param>
    /// <param name="take">Har bir ro'yxatda eng ko'pi nechta nomzod qaytsin.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    /// <returns>Har bir qator uchun bitta natija, xuddi shu tartibda.</returns>
    Task<IReadOnlyList<ImportCandidateMatch>> FindEmployeeCandidatesAsync(
        IReadOnlyList<ImportCandidateLookup> lookups,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>Berilgan identifikatorlardan qaysilari mavjud (o'chirilmagan) xodim.</summary>
    /// <remarks>Import tasdig'i uchun: qatorma-qator tekshiruv 5 000 ta so'rov bo'lardi.</remarks>
    /// <param name="employeeIds">Tekshiriladigan identifikatorlar.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlySet<Guid>> ExistingEmployeeIdsAsync(
        IReadOnlyCollection<Guid> employeeIds,
        CancellationToken cancellationToken = default);

    // ── 7. Jadval biriktirmalari ────────────────────────────────────

    /// <summary>Biriktirmalar — nomlari bilan, pog'ona va boshlanish sanasi bo'yicha.</summary>
    /// <remarks>
    /// Sahifalanmaydi: biriktirmalar o'nlab yoki bir necha yuz, ekran esa
    /// ularni bir jadvalda ko'rsatadi. Nomlar sahifadagi identifikatorlar
    /// bo'yicha to'plab olinadi — qatorlar soniga bog'liq so'rov yo'q.
    /// </remarks>
    /// <param name="filter">Pog'ona, obyekt va sana.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyList<ScheduleAssignmentDto>> ListAssignmentsAsync(
        AssignmentFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>Bitta biriktirma — ro'yxatdagi bilan bir xil shaklda.</summary>
    /// <param name="assignmentId">Biriktirma.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<ScheduleAssignmentDto?> FindAssignmentAsync(
        Guid assignmentId,
        CancellationToken cancellationToken = default);
}

// ════════════════════════════════════════════════════════════════════
//  Umumiy
// ════════════════════════════════════════════════════════════════════

/// <summary>Bitta sahifa: yozuvlar va filtrga mos umumiy soni.</summary>
/// <typeparam name="T">Qator turi.</typeparam>
/// <param name="Items">Shu sahifadagi yozuvlar.</param>
/// <param name="Total">Filtrga mos keladigan umumiy yozuvlar soni.</param>
public sealed record AttendancePage<T>(IReadOnlyList<T> Items, int Total);

// ════════════════════════════════════════════════════════════════════
//  1. O'tishlar jurnali
// ════════════════════════════════════════════════════════════════════

/// <summary>O'tishlar jurnalining filtri.</summary>
/// <remarks>
/// Davr MAJBURIY va ikki tomondan yopiq. Jadval yiliga ~2 mln qator
/// o'sadi; chegarasiz so'rov bir kun kelib butun arxivni saralashga
/// urinardi. Davrni to'ldirish va uzunligini tekshirish use-case'da
/// (<c>AttendanceQueryParsing</c>).
/// </remarks>
/// <param name="From">Toshkent sanasi bo'yicha quyi chegara (o'zi ham kiradi).</param>
/// <param name="To">Yuqori chegara (o'zi ham kiradi).</param>
/// <param name="EmployeeId">Faqat shu xodim.</param>
/// <param name="DeviceId">Faqat shu qurilma.</param>
/// <param name="Direction">Faqat shu yo'nalish.</param>
/// <param name="Method">Faqat shu tasdiqlash usuli.</param>
/// <param name="Matched"><c>true</c> — moslanganlar, <c>false</c> — moslanmaganlar, <c>null</c> — hammasi.</param>
/// <param name="LegalEntityId">Davrda shu firmada munosabati bo'lgan xodimlar.</param>
/// <param name="DepartmentId">Davrda shu bo'limda (ichki bo'limlari bilan) munosabati bo'lganlar.</param>
/// <param name="Search">Xodim (F.I.Sh., JSHSHIR, pasport, telefon) YOKI qurilmadagi shaxs raqami.</param>
/// <param name="Descending">Vaqt bo'yicha teskari tartib (sukut — eng yangisi birinchi).</param>
/// <param name="Page">Sahifa, 1 dan.</param>
/// <param name="PageSize">Sahifa hajmi.</param>
public sealed record PassSearchFilter(
    DateOnly From,
    DateOnly To,
    Guid? EmployeeId = null,
    Guid? DeviceId = null,
    PassDirection? Direction = null,
    PassMethod? Method = null,
    bool? Matched = null,
    Guid? LegalEntityId = null,
    Guid? DepartmentId = null,
    string? Search = null,
    bool Descending = true,
    int Page = 1,
    int PageSize = 25)
{
    /// <summary>Nechta yozuv o'tkazib yuboriladi.</summary>
    public int Skip => (Page - 1) * PageSize;
}

// ════════════════════════════════════════════════════════════════════
//  2. Jonli taxta
// ════════════════════════════════════════════════════════════════════

/// <summary>Jonli taxtaning filtri.</summary>
/// <param name="LegalEntityId">Bugungi asosiy munosabati shu firmada bo'lganlar.</param>
/// <param name="DepartmentId">Bugungi asosiy munosabati shu bo'limda (ichki bo'limlari bilan).</param>
/// <param name="Search">Xodim bo'yicha qidiruv (F.I.Sh., JSHSHIR, pasport, telefon).</param>
public sealed record BoardFilter(
    Guid? LegalEntityId = null,
    Guid? DepartmentId = null,
    string? Search = null);

// ════════════════════════════════════════════════════════════════════
//  2a. Asosiy sahifa paneli
// ════════════════════════════════════════════════════════════════════

/// <summary>Asosiy sahifa panelining filtri.</summary>
/// <remarks>
/// <para>
/// <b>Firma / bo'lim / lavozim — tabeldagi bilan bir xil ma'noda</b>
/// (<see cref="TimesheetFilter"/>): shu sanani qamraydigan BIRORTA munosabati
/// filtrga mos odam to'plamga kiradi, bo'lim — ichki bo'limlari bilan. Ikki
/// firmada ishlaydigan odam ikkala firmaning panelida ham turadi; ko'rsatilgan
/// bo'lim va jadval esa baribir asosiy munosabatdan (§5.1). Taxtadan farqi
/// ataylab: panel tabelning «bugungi ustuni», va bitta filtr ikki ekranda ikki
/// xil odamlar ro'yxatini bermasligi kerak.
/// </para>
/// <para>
/// <see cref="Search"/> va <see cref="Status"/> faqat JADVALNI toraytiradi —
/// sanoq va «jami» ularsiz hisoblanadi.
/// </para>
/// </remarks>
/// <param name="Date">Toshkent sanasi.</param>
/// <param name="LegalEntityId">Firma.</param>
/// <param name="DepartmentId">Bo'lim (ichki bo'limlari bilan).</param>
/// <param name="PositionId">Lavozim.</param>
/// <param name="Search">Xodim bo'yicha qidiruv (F.I.Sh., JSHSHIR, pasport, telefon) — faqat jadval.</param>
/// <param name="Status">Faqat shu holatdagilar — faqat jadval.</param>
/// <param name="Descending">Ism bo'yicha teskari tartib (sukut — A→Z).</param>
/// <param name="Page">Sahifa, 1 dan.</param>
/// <param name="PageSize">Sahifa hajmi.</param>
/// <param name="DepartmentRoots">
/// Bo'lim boshlig'ining qamrovi (SPEC-jamoam §C2): shu ildizlarning daraxtlari BIRLASHMASI
/// (har biri ichki bo'limlari bilan, <see cref="DepartmentId"/> dagi ma'noda). <c>null</c> —
/// cheklov yo'q (HR paneli). ⚠️ BO'SH to'plam — hech kim: «ildiz yo'q» hech qachon «hamma»
/// degani emas.
/// </param>
/// <param name="SearchNameAndPhoneOnly">
/// Qidiruv FAQAT F.I.Sh. va telefon bo'yicha — JSHSHIR va pasportsiz (tashqi API'dagi
/// <c>MatchingPublic</c> qoidasi). Bo'lim boshlig'i paneli uchun: aks holda boshliq qo'lidagi
/// JSHSHIR yoki pasport raqamini yuborib, «bu odam mening jamoamdami» degan savolga javob
/// olardi — sizib chiqadigan narsa qiymat emas, MOSLIK faktining o'zi.
/// </param>
public sealed record DashboardFilter(
    DateOnly Date,
    Guid? LegalEntityId = null,
    Guid? DepartmentId = null,
    Guid? PositionId = null,
    string? Search = null,
    AttendanceDashboardStatus? Status = null,
    bool Descending = false,
    int Page = 1,
    int PageSize = 50,
    IReadOnlyCollection<Guid>? DepartmentRoots = null,
    bool SearchNameAndPhoneOnly = false)
{
    /// <summary>Nechta xodim o'tkazib yuboriladi.</summary>
    public int Skip => (Page - 1) * PageSize;
}

/// <summary>Panelning bitta sahifasi — sahifa raqamlarini handler qo'yadi (tabel bilan bir xil).</summary>
/// <param name="Date">Qaysi sana.</param>
/// <param name="IsToday">Sana bugunmi.</param>
/// <param name="AsOf">Hisoblangan lahza.</param>
/// <param name="Total">To'plamdagi odamlar — qidiruv va holat filtridan oldin.</param>
/// <param name="Counts">Shu to'plam holatlar bo'yicha.</param>
/// <param name="Items">Shu sahifadagi xodimlar.</param>
/// <param name="TotalItems">Hamma filtrdan keyin jadvalga tushganlar.</param>
public sealed record DashboardPage(
    DateOnly Date,
    bool IsToday,
    DateTimeOffset AsOf,
    int Total,
    AttendanceDashboardCountsDto Counts,
    IReadOnlyList<AttendanceDashboardItemDto> Items,
    int TotalItems);

/// <summary>Davr grafigining filtri.</summary>
/// <remarks>
/// <para>
/// Org-filtr <see cref="DashboardFilter"/> dagi bilan AYNAN bir xil ma'noda
/// va HAR BIR kunga alohida qo'llanadi: 3-sentabrda bo'limga o'tgan odam
/// 1–2-sentabrda to'plamda yo'q, 3-sentabrdan bor. Kunning suratini bugungi
/// bo'lim bo'yicha chizish o'tgan oyning grafigini har ko'chirishda
/// o'zgartirib yuborardi.
/// </para>
/// <para>
/// Qidiruv va holat filtri ATAYLAB yo'q: ular panelda faqat JADVALNI
/// toraytiradi, grafikda esa jadval yo'q.
/// </para>
/// </remarks>
/// <param name="From">Davr boshi (Toshkent sanasi).</param>
/// <param name="To">Davr oxiri — ikkala chegara ham kiradi.</param>
/// <param name="LegalEntityId">Firma.</param>
/// <param name="DepartmentId">Bo'lim (ichki bo'limlari bilan).</param>
/// <param name="PositionId">Lavozim.</param>
public sealed record DashboardTrendFilter(
    DateOnly From,
    DateOnly To,
    Guid? LegalEntityId = null,
    Guid? DepartmentId = null,
    Guid? PositionId = null);

/// <summary>Tug'ilgan kunlar panelining filtri.</summary>
/// <remarks>
/// <para>
/// Org-filtr <see cref="DashboardFilter"/> dagi bilan AYNAN bir xil ma'noda.
/// Davr esa YO'Q: panel «tanlangan oraliqda kimning tug'ilgan kuni bo'lgan»
/// degan savolga emas, «keyingi navbatda kim» degan savolga javob beradi —
/// shu sababli u davr almashtirilganda qayta so'ralmaydi.
/// </para>
/// <para>
/// <see cref="Today"/> — Toshkent sanasi va uni HANDLER qo'yadi
/// (<see cref="Bonvi.Domain.Abstractions.IClock"/> dan): brauzerdagi kun
/// foydalanuvchi qurilmasining sozlamasi va u bilan «bugun» ikki xil
/// bo'lib qolardi.
/// </para>
/// </remarks>
/// <param name="Today">Bugungi Toshkent sanasi.</param>
/// <param name="LegalEntityId">Firma.</param>
/// <param name="DepartmentId">Bo'lim (ichki bo'limlari bilan).</param>
/// <param name="PositionId">Lavozim.</param>
/// <param name="Limit">Nechta odam qaytadi — handler chegaralaydi.</param>
public sealed record BirthdayFilter(
    DateOnly Today,
    Guid? LegalEntityId = null,
    Guid? DepartmentId = null,
    Guid? PositionId = null,
    int Limit = 10);

// ════════════════════════════════════════════════════════════════════
//  2d. Yo'qlik so'rovlari
// ════════════════════════════════════════════════════════════════════

/// <summary>So'rovning shakli — Verifix «Часть дня / Весь день / Несколько дней» (SPEC-jadval-so'rovi §B).</summary>
public enum AbsenceRequestMode
{
    /// <summary>Bir kunning bir qismi — vaqt oralig'i bor.</summary>
    PartDay = 1,

    /// <summary>Bitta butun kun.</summary>
    WholeDay = 2,

    /// <summary>Bir necha kun.</summary>
    MultiDay = 3,
}

/// <summary>Yo'qlik so'rovlari ro'yxatining filtri.</summary>
/// <remarks>
/// <para>
/// <see cref="From"/>/<see cref="To"/> — so'rovning DAVRI bo'yicha kesish
/// («shu oyga tegishli so'rovlar»), yaratilgan sana bo'yicha emas: HR
/// «sentabrda kim yo'q bo'lgan» deb qidiradi, «sentabrda kim yozgan» deb
/// emas. So'rov oraliqqa QISMAN tegsa ham ro'yxatga tushadi.
/// </para>
/// <para>
/// Qidiruv (<see cref="Search"/>) — xodim bo'yicha va u barcha ro'yxatlar
/// bilan bitta shartdan (<c>EmployeeSearchQuery</c>).
/// </para>
/// </remarks>
/// <param name="EmployeeId">Faqat shu xodim — xodim kartasida.</param>
/// <param name="Status">Faqat shu holat.</param>
/// <param name="Decided">
/// <c>true</c> — qaror qabul qilinganlar (tasdiqlangan, rad etilgan yoki
/// bekor qilingan), <c>false</c> — faqat kutayotganlar, <c>null</c> — hammasi.
/// <see cref="Status"/> bilan birga berilsa, ikkalasi ham qo'llanadi.
/// Ekranda ikkita jadval bor (kutayotganlar va qaror qabul qilinganlar) va
/// ularning HAR BIRI o'z sahifalashiga ega bo'lishi kerak — klientda
/// filtrlash «jami» sonini yolg'on qilardi.
/// </param>
/// <param name="From">Davr boshi.</param>
/// <param name="To">Davr oxiri.</param>
/// <param name="Search">Xodim bo'yicha qidiruv.</param>
/// <param name="Page">Sahifa, 1 dan.</param>
/// <param name="PageSize">Sahifa hajmi.</param>
/// <param name="AbsenceTypeId">Yo'qlik turi bo'yicha (SPEC-jadval-so'rovi §B).</param>
/// <param name="Mode">Shakl: qism kun / butun kun / bir necha kun.</param>
public sealed record AbsenceRequestFilter(
    Guid? EmployeeId = null,
    AbsenceRequestStatus? Status = null,
    bool? Decided = null,
    DateOnly? From = null,
    DateOnly? To = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 25,
    Guid? AbsenceTypeId = null,
    AbsenceRequestMode? Mode = null)
{
    /// <summary>Nechta yozuv o'tkazib yuboriladi.</summary>
    public int Skip => (Page - 1) * PageSize;
}

// ════════════════════════════════════════════════════════════════════
//  3. Tabel
// ════════════════════════════════════════════════════════════════════

/// <summary>Tabelning filtri.</summary>
/// <remarks>
/// <para>
/// <b>Kim ro'yxatga tushadi.</b> Oyning birorta kunida munosabati bo'lgan
/// (filtrga mos) xodim YOKI shu oyda filtrga mos kun yozuvi bor xodim.
/// Ikkinchi shart ikki holat uchun: oy o'rtasida boshqa bo'limga
/// ko'chgan odam eski bo'lim tabelida qoladi (kun yozuvida bo'lim SURAT
/// bo'lib turadi), va ishdan ketgan-u, turniketdan o'tgan odam
/// (<c>NotEmployed</c> yozuvi) ko'rinadi — aynan HR ko'rishi kerak
/// bo'lgan anomaliya.
/// </para>
/// </remarks>
/// <param name="Year">Yil.</param>
/// <param name="Month">Oy (1–12).</param>
/// <param name="LegalEntityId">Firma.</param>
/// <param name="DepartmentId">Bo'lim (ichki bo'limlari bilan).</param>
/// <param name="PositionId">Lavozim.</param>
/// <param name="EmployeeId">Faqat shu xodim.</param>
/// <param name="Search">Xodim bo'yicha qidiruv.</param>
/// <param name="Page">Sahifa, 1 dan.</param>
/// <param name="PageSize">Sahifadagi XODIMLAR soni.</param>
public sealed record TimesheetFilter(
    int Year,
    int Month,
    Guid? LegalEntityId = null,
    Guid? DepartmentId = null,
    Guid? PositionId = null,
    Guid? EmployeeId = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 25)
{
    /// <summary>Nechta xodim o'tkazib yuboriladi.</summary>
    public int Skip => (Page - 1) * PageSize;

    /// <summary>Oyning birinchi kuni.</summary>
    public DateOnly MonthStart => new(Year, Month, 1);

    /// <summary>Oyning oxirgi kuni.</summary>
    public DateOnly MonthEnd => MonthStart.AddMonths(1).AddDays(-1);
}

/// <summary>Tabelning bitta sahifasi.</summary>
/// <param name="Days">Oyning barcha kunlari.</param>
/// <param name="Items">Shu sahifadagi xodimlar — har birida oyning har bir kuni.</param>
/// <param name="Total">Filtrga mos xodimlar soni.</param>
/// <param name="LastComputedAt">Qaytarilgan qatorlardagi eng so'nggi <c>computed_at</c>.</param>
public sealed record TimesheetPage(
    IReadOnlyList<DateOnly> Days,
    IReadOnlyList<TimesheetRowDto> Items,
    int Total,
    DateTimeOffset? LastComputedAt);

// ════════════════════════════════════════════════════════════════════
//  4. Hisobotlar
// ════════════════════════════════════════════════════════════════════

/// <summary>Chetlanish turi — «qaysi ro'yxat kerak».</summary>
/// <remarks>
/// Faqat so'rov uchun (SPEC §2.7): bazada saqlanmaydi, shu sababli
/// Domain'da emas. Raqamlari baribir aniq — mijoz nomini yuboradi, lekin
/// sanoq kengaysa eski qiymatlar siljimasligi kerak.
/// </remarks>
public enum ExceptionKind
{
    /// <summary>Kechikkanlar — <c>late_minutes &gt; 0</c>.</summary>
    Late = 1,

    /// <summary>Erta ketganlar — <c>early_leave_minutes &gt; 0</c>.</summary>
    EarlyLeave = 2,

    /// <summary>Kelmaganlar — holat <c>Absent</c>.</summary>
    Absent = 3,

    /// <summary>
    /// «Hisoblab bo'lmagan» kunlar: <c>MissingExit</c>, <c>MissingEntry</c>
    /// VA <c>NoSchedule</c>.
    /// </summary>
    /// <remarks>
    /// Uchalasi bitta ro'yxatda, chunki HR aynan shu ro'yxat bilan
    /// ishlaydi: har birida odam yoki jadvalni topib tuzatish kerak
    /// (SPEC §6.6).
    /// </remarks>
    Missing = 4,

    /// <summary>
    /// «Kam ishlaganlar» — erkin kunda normadan kam ishlagan kunlar, <c>shortfall_minutes &gt; 0</c>
    /// (SPEC-erkin-jadval §5.3).
    /// </summary>
    /// <remarks>
    /// Erkin kunda kechikish ham, erta ketish ham yo'q — bu ro'yxatsiz norma bajarilmagan kun
    /// hech qaysi hisobotda ko'rinmasdi.
    /// </remarks>
    Shortfall = 5,
}

/// <summary>Chetlanishlar hisobotini saralash ustunlari.</summary>
/// <remarks>
/// Ataylab enum (<c>EmployeeSortField</c> bilan bir xil sabab): mijozdan
/// kelgan matn to'g'ridan-to'g'ri SQL'ga tushmaydi.
/// </remarks>
public enum ExceptionSortField
{
    /// <summary>Sana (sukut bo'yicha, yangidan eskiga).</summary>
    Date = 0,

    /// <summary>Xodim — familiya, ism.</summary>
    Employee = 1,

    /// <summary>
    /// Daqiqalar: kechikkanlarda kechikish, erta ketganlarda erta ketish, kam ishlaganlarda kam
    /// ishlangan vaqt.
    /// </summary>
    /// <remarks>
    /// Kelmaganlar va muammoli kunlarda daqiqa yo'q — u yerda sana
    /// bo'yicha saralanadi.
    /// </remarks>
    Minutes = 2,
}

/// <summary>Chetlanishlar hisobotining filtri.</summary>
/// <param name="Kind">Qaysi ro'yxat.</param>
/// <param name="From">Davr boshi (o'zi ham kiradi).</param>
/// <param name="To">Davr oxiri (o'zi ham kiradi).</param>
/// <param name="LegalEntityId">Kun yozuvidagi firma.</param>
/// <param name="DepartmentId">Kun yozuvidagi bo'lim (ichki bo'limlari bilan).</param>
/// <param name="PositionId">Kun yozuvidagi lavozim.</param>
/// <param name="EmployeeId">Faqat shu xodim.</param>
/// <param name="MinMinutes">Kamida shuncha daqiqa — faqat <c>Late</c>/<c>EarlyLeave</c>/<c>Shortfall</c> uchun.</param>
/// <param name="Sort">Saralash ustuni.</param>
/// <param name="Descending">Teskari tartib.</param>
/// <param name="Page">Sahifa, 1 dan.</param>
/// <param name="PageSize">Sahifa hajmi.</param>
public sealed record ExceptionFilter(
    ExceptionKind Kind,
    DateOnly From,
    DateOnly To,
    Guid? LegalEntityId = null,
    Guid? DepartmentId = null,
    Guid? PositionId = null,
    Guid? EmployeeId = null,
    int? MinMinutes = null,
    ExceptionSortField Sort = ExceptionSortField.Date,
    bool Descending = true,
    int Page = 1,
    int PageSize = 25)
{
    /// <summary>Nechta yozuv o'tkazib yuboriladi.</summary>
    public int Skip => (Page - 1) * PageSize;
}

/// <summary>Xulosa hisobotini saralash ustunlari.</summary>
public enum SummarySortField
{
    /// <summary>Xodim — familiya, ism (sukut bo'yicha).</summary>
    Employee = 0,

    /// <summary>Ishlangan daqiqalar.</summary>
    WorkedMinutes = 1,

    /// <summary>Kechikishlar yig'indisi.</summary>
    LateMinutes = 2,

    /// <summary>Kelmagan kunlar.</summary>
    AbsentDays = 3,

    /// <summary>Muammoli kunlar.</summary>
    ProblemDays = 4,
}

/// <summary>Xulosa hisobotining filtri.</summary>
/// <param name="From">Davr boshi (o'zi ham kiradi).</param>
/// <param name="To">Davr oxiri (o'zi ham kiradi).</param>
/// <param name="LegalEntityId">Kun yozuvidagi firma.</param>
/// <param name="DepartmentId">Kun yozuvidagi bo'lim (ichki bo'limlari bilan).</param>
/// <param name="PositionId">Kun yozuvidagi lavozim.</param>
/// <param name="Sort">Saralash ustuni.</param>
/// <param name="Descending">Teskari tartib.</param>
/// <param name="Page">Sahifa, 1 dan.</param>
/// <param name="PageSize">Sahifadagi xodimlar soni.</param>
public sealed record SummaryFilter(
    DateOnly From,
    DateOnly To,
    Guid? LegalEntityId = null,
    Guid? DepartmentId = null,
    Guid? PositionId = null,
    SummarySortField Sort = SummarySortField.Employee,
    bool Descending = false,
    int Page = 1,
    int PageSize = 25)
{
    /// <summary>Nechta xodim o'tkazib yuboriladi.</summary>
    public int Skip => (Page - 1) * PageSize;
}

// ════════════════════════════════════════════════════════════════════
//  6. Mosliklar va import
// ════════════════════════════════════════════════════════════════════

/// <summary>Mosliklar ro'yxatining filtri.</summary>
/// <param name="Search">Qurilmadagi shaxs raqami YOKI xodim (F.I.Sh., JSHSHIR, pasport, telefon).</param>
/// <param name="EmployeeId">Faqat shu xodimning mosliklari.</param>
/// <param name="Provider">Faqat shu raqamlar fazosi.</param>
/// <param name="Page">Sahifa, 1 dan.</param>
/// <param name="PageSize">Sahifa hajmi.</param>
public sealed record IdentitySearchFilter(
    string? Search = null,
    Guid? EmployeeId = null,
    AttendanceIdentityProvider? Provider = null,
    int Page = 1,
    int PageSize = 25)
{
    /// <summary>Nechta yozuv o'tkazib yuboriladi.</summary>
    public int Skip => (Page - 1) * PageSize;
}

/// <summary>Moslanmagan guruhlar ro'yxatining filtri.</summary>
/// <param name="Page">Sahifa, 1 dan.</param>
/// <param name="PageSize">Sahifa hajmi.</param>
public sealed record UnmatchedFilter(int Page = 1, int PageSize = 25)
{
    /// <summary>Nechta guruh o'tkazib yuboriladi.</summary>
    public int Skip => (Page - 1) * PageSize;
}

/// <summary>Import qatoridan nomzod qidirish uchun kerakli qism.</summary>
/// <param name="FullName">Fayldagi F.I.Sh.</param>
/// <param name="Pinfl">Fayldagi JSHSHIR.</param>
/// <param name="MappedEmployeeId">Shaxs raqami allaqachon bog'langan xodim — uning kartasi ham kerak.</param>
public sealed record ImportCandidateLookup(string? FullName, string? Pinfl, Guid? MappedEmployeeId);

/// <summary>Bitta import qatori uchun topilganlar.</summary>
/// <param name="Mapped">Allaqachon bog'langan xodimning kartasi (bo'lsa).</param>
/// <param name="ByPinfl">JSHSHIR bo'yicha aniq moslar.</param>
/// <param name="ByName">Yig'ilgan F.I.Sh. bo'yicha aniq moslar.</param>
public sealed record ImportCandidateMatch(
    ImportCandidateDto? Mapped,
    IReadOnlyList<ImportCandidateDto> ByPinfl,
    IReadOnlyList<ImportCandidateDto> ByName);

// ════════════════════════════════════════════════════════════════════
//  7. Jadval biriktirmalari
// ════════════════════════════════════════════════════════════════════

/// <summary>Biriktirmalar ro'yxatining filtri.</summary>
/// <param name="Scope">Faqat shu pog'ona.</param>
/// <param name="EmployeeId">Faqat shu xodimga qilingan biriktirmalar.</param>
/// <param name="DepartmentId">Faqat shu bo'limga qilinganlar (ichki bo'limlarsiz — biriktirma aynan qaysi obyektga qilingani so'raladi).</param>
/// <param name="PositionId">Faqat shu lavozimga qilinganlar.</param>
/// <param name="OnDate">Faqat shu sanada kuchda bo'lganlar.</param>
public sealed record AssignmentFilter(
    ScheduleScope? Scope = null,
    Guid? EmployeeId = null,
    Guid? DepartmentId = null,
    Guid? PositionId = null,
    DateOnly? OnDate = null);
