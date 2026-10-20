using Bonvi.Application.Attendance.Dtos;
using Bonvi.Domain.Attendance;

namespace Bonvi.Application.Me;

/// <summary>
/// Belgi QAYERDAN kelgani — xodim ko'radigan sodda ko'rinish.
/// </summary>
/// <remarks>
/// <para>
/// <c>PassMethod</c> dan HOSIL QILINADI va uni almashtirmaydi: xodimga
/// «yuz», «karta» yoki «barmoq izi» orasidagi farq kerak emas — u
/// terminaldan o'tganini biladi. Unga kerak bo'lgan yagona ajratish:
/// terminalmi yoki o'zining telefonimi.
/// </para>
/// <para>
/// ⚠️ Simda AYNAN shu nomlar yuriladi, o'zbekcha matn EMAS (SPEC §E
/// «Terminal | Mobil ilova» deb yozgan bo'lsa ham). Sabab loyihaning
/// qat'iy qoidasi: server foydalanuvchi matnini yubormaydi, interfeys
/// uni to'rt tilda o'zi hosil qiladi. Tayyor o'zbekcha satr rus tilini
/// tanlagan foydalanuvchida ham o'zbekcha bo'lib qolardi.
/// </para>
/// </remarks>
public enum MarkSource
{
    /// <summary>Yuz terminali yoki turniket.</summary>
    Terminal = 1,

    /// <summary>Xodimning o'z telefoni.</summary>
    Mobile = 2,
}

/// <summary>Kun ichidagi bitta belgi — xodim ko'radigan qatorlar.</summary>
/// <remarks>
/// Qurilma identifikatori, surat va koordinata ATAYLAB yo'q: ularning
/// birortasi ham xodimga kerak emas, koordinata esa uning o'z joylashuvi —
/// ilova uni allaqachon bilardi.
/// </remarks>
/// <param name="At">Belgi lahzasi (UTC; ilova Toshkent vaqtida ko'rsatadi).</param>
/// <param name="Direction">Kirish yoki chiqish.</param>
/// <param name="Source">Terminalmi yoki telefonmi.</param>
/// <param name="SiteName">Telefon belgisida — obyekt nomi; terminalda <c>null</c>.</param>
public sealed record MyMarkDto(
    DateTimeOffset At,
    PassDirection Direction,
    MarkSource Source,
    string? SiteName);

/// <summary>
/// Xodim oyining bitta kuni: HR ko'radigan katakning O'ZI + o'sha kundagi belgilar.
/// </summary>
/// <remarks>
/// ⚠️ Katak <b>ichma-ich</b> turadi (<c>day.status</c>), maydonlari yoyib
/// yozilmagan. Sabab: <see cref="TimesheetCellDto"/> da o'n bitta maydon bor
/// va ularni ko'chirib yozish ikkita ta'rifni hosil qilardi — HR tomoniga
/// yangi maydon qo'shilgan kuni xodim ekrani jimgina eskirardi. SPEC §E
/// «HR ko'radigan AYNAN o'sha kataklar» degan, va bu shuning to'g'ridan-
/// to'g'ri ifodasi.
/// </remarks>
/// <param name="Day">Tabel katagi — HR ko'radigan ko'rinishning o'zi.</param>
/// <param name="Marks">
/// Shu KATAKKA tushgan belgilar, vaqt bo'yicha o'sish tartibida — tabel
/// qo'ygan kun bo'yicha (tungi smenaning ertalabki chiqishi kechagi katakda),
/// kalendar sanasi bo'yicha emas.
/// </param>
public sealed record MyAttendanceDayDto(TimesheetCellDto Day, IReadOnlyList<MyMarkDto> Marks);

/// <summary>
/// «Hozir» — belgilash kartasi uchun: keyingi tugma, oxirgi belgi va joriy smena kuni.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega server hisoblaydi.</b> Ilova ilgari keyingi yo'nalishni BUGUNGI
/// kalendar kunining belgilaridan chiqarardi. Tungi smenada bu yolg'on: 22:00
/// da «Ishga keldim» bosgan odamga soat 06:00 da (yangi kalendar kuni, belgi
/// yo'q) yana «Ishga keldim» taklif qilinardi va ilova <c>In</c> yuborardi —
/// tabelda esa smena ikkita kirish va chiqishsiz bo'lib qolardi. Qoida endi
/// BIR joyda: oxirgi <c>AttendanceRules.BoardLookbackHours</c> soatdagi
/// belgilar, «Hozir binoda» taxtasidagi bilan bir xil oyna.
/// </para>
/// <para>
/// <see cref="Date"/> — belgilar tushadigan KATAK (tabel qoidasi bo'yicha,
/// SPEC §5.3), kalendar sanasi emas: ochiq tungi smenada dushanba, garchi
/// soat seshanba 06:00 bo'lsa ham. Ilova bosh ekranda aynan shu katakni
/// ko'rsatadi.
/// </para>
/// </remarks>
/// <param name="Date">Joriy smena tegishli bo'lgan katak sanasi (Toshkent).</param>
/// <param name="NextDirection">Tugma yuboradigan yo'nalish: ichkarida bo'lsa <c>Out</c>, aks holda <c>In</c>.</param>
/// <param name="LastMark">Oxirgi belgi (qarash oynasi ichida); bo'lmasa <c>null</c>.</param>
public sealed record MyCurrentStateDto(
    DateOnly Date,
    PassDirection NextDirection,
    MyMarkDto? LastMark);

/// <summary>Belgi va u tushgan katak — o'qish modeli qaytaradigan shakl.</summary>
/// <param name="Date">Tabel qoidasi bo'yicha tegishli kun (SPEC §5.3), kalendar sanasi emas.</param>
/// <param name="Mark">Belgining o'zi.</param>
public sealed record MyAttributedMark(DateOnly Date, MyMarkDto Mark);

/// <summary>Xodimning bir oylik davomati.</summary>
/// <param name="Year">Yil.</param>
/// <param name="Month">Oy (1–12).</param>
/// <param name="Days">Oyning har bir kuni — katak va belgilar bilan.</param>
/// <param name="Totals">Oy bo'yicha jami — HR tabelidagi bilan bir xil hisob.</param>
/// <param name="Current">
/// «Hozir» — so'ralgan oydan qat'i nazar HOZIRGI lahza uchun (belgilash kartasi).
/// </param>
public sealed record MyAttendanceDto(
    int Year,
    int Month,
    IReadOnlyList<MyAttendanceDayDto> Days,
    TimesheetTotalsDto Totals,
    MyCurrentStateDto Current);

/// <summary>Yo'qlik turi — ilova so'rov yaratishda shu ro'yxatdan tanlaydi.</summary>
/// <param name="Id">Identifikator.</param>
/// <param name="Name">Nom.</param>
/// <param name="CountsAsWorkTime">
/// Tasdiqlangan vaqt ish vaqti hisoblanadimi; <c>false</c> — shaxsiy tur: soatlar ishlangan vaqtdan
/// ayriladi (SPEC-sorov-tahriri §5). Forma tur ostida shuni aytadi.
/// </param>
public sealed record MyAbsenceTypeDto(Guid Id, string Name, bool CountsAsWorkTime = false);

/// <summary>
/// Xodimning o'zi haqidagi ma'lumot — ilovaning birinchi so'rovi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Yo'qlik turlari SHU YERDA</b> va bu ataylab: ilova so'rov
/// yaratishi uchun ro'yxat kerak, lekin u HR endpointiga
/// (<c>/api/catalog/absence-types</c>, <c>catalog.view</c> ostida) murojaat
/// qila OLMAYDI — xodim tokenida ruxsat da'volari umuman yo'q. Ro'yxat
/// kichik, kamdan-kam o'zgaradi va ilova uni baribir keshlaydi, ya'ni
/// ikkinchi so'rov faqat ikkinchi aylanish bo'lardi.
/// </para>
/// <para>
/// Tug'ilgan kun, lavozim tarixi, bank rekvizitlari va boshqalar ATAYLAB
/// yo'q: ular xodimga davomat belgilashda yordam bermaydi va har bir
/// qo'shimcha maydon telefonga tushadigan shaxsiy ma'lumotni ko'paytiradi.
/// </para>
/// </remarks>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="FullName">To'liq ism-familiya.</param>
/// <param name="ShortName">Qisqa ko'rinish: «Mamajonov B.F.».</param>
/// <param name="PhotoKey">
/// Rasm kaliti; rasm yo'q bo'lsa <c>null</c>. Baytlar — <c>GET /api/me/photo</c>
/// (tokenga bog'langan); kalit mijozda kesh belgisi sifatida ishlatiladi.
/// </param>
/// <param name="Department">Bugungi asosiy bo'lim.</param>
/// <param name="Position">Bugungi lavozim.</param>
/// <param name="MobileCheckInAllowed">Telefondan belgilash yoqilganmi.</param>
/// <param name="AbsenceTypes">Yo'qlik turlari — so'rov yaratish uchun.</param>
/// <param name="RequestBreakRule">
/// Jadval so'rovidagi tanaffus qoidasi (SPEC-sorov-tahriri §4) — forma uni oldindan tekshiradi; yangi
/// server doim to'ldiradi, <c>null</c> — eski server.
/// </param>
public sealed record MeDto(
    Guid EmployeeId,
    string FullName,
    string ShortName,
    string? PhotoKey,
    string? Department,
    string? Position,
    bool MobileCheckInAllowed,
    IReadOnlyList<MyAbsenceTypeDto> AbsenceTypes,
    RequestBreakRuleDto? RequestBreakRule = null);
