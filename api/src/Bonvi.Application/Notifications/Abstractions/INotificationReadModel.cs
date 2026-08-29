using Bonvi.Domain.Recruiting;

namespace Bonvi.Application.Notifications.Abstractions;

/// <summary>
/// Bildirishnoma SHARTLARINI bazadan o'qish porti.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega fon vazifasi (Hangfire, cron) YO'Q.</b> Bu qaror
/// <c>PLAN.md</c> S6 da bir marta qabul qilingan va shu yerda ham amal
/// qiladi. Ikkita sabab bor:
/// </para>
/// <list type="number">
///   <item>
///     <b>Bildirishnomalarning ko'pchiligi HODISA emas, HOLAT.</b>
///     «Sinov 7 kundan keyin tugaydi», «ertaga tug'ilgan kun», «taklif
///     muddati o'tib ketdi» — bularning sodir bo'lgan LAHZASI yo'q. Hech
///     kim hech narsa qilmasa ham, vaqt o'tishi bilan ular o'z-o'zidan
///     rost bo'lib qoladi. Bunday shartni «hodisa» ga aylantirish uchun
///     har kuni ishlaydigan vazifa kerak — ya'ni hodisani SUN'IY yasash
///     kerak, holbuki javob allaqachon ma'lumotning ichida turibdi.
///   </item>
///   <item>
///     <b>Tizimdan atigi bir necha kishi foydalanadi.</b> Uchta HR
///     menejeri va bosh rahbar. Kuniga bir marta ishlaydigan vazifa
///     uchun butun bir infratuzilma (jadval, ishchi jarayon, qayta
///     urinish siyosati, monitoring paneli) qo'yish — kuchsiz
///     asoslangan murakkablik. Uning o'z nosozliklari ham bor:
///     vazifa tunda ishlamay qolsa, kun bo'yi HECH KIM hech qanday
///     bildirishnoma olmasdi va buni faqat kechqurun sezishardi.
///   </item>
/// </list>
/// <para>
/// Shu sababli hisoblash <b>o'qish paytida</b> bajariladi: foydalanuvchi
/// qo'ng'iroqni bosganda server joriy shartlarni tekshiradi. Buning
/// narxi — bir necha indeksli so'rov, foydasi — ma'lumot HAR DOIM joriy
/// va sinxronlashtiriladigan ikkinchi nusxa umuman yo'q.
/// </para>
/// <para>
/// <b>Nega alohida port, <c>IEmployeeReadModel</c> ga qo'shilmadi.</b> U
/// port ro'yxat ekranlariga xizmat qiladi — sahifa, saralash, filtr.
/// Bu yerdagi so'rovlarda esa na sahifa bor, na saralash: har biri
/// «shu shartga tushgan hamma yozuv» ni qaytaradi va ular uchta turli
/// bo'limdan keladi (xodim, sinov, rekruting). Bittasiga yig'ilsa,
/// xodimlar porti nomzodlarni ham bilib qolardi.
/// </para>
/// <para>
/// <b>Sinov yozuvisiz qolganlar bu yerda YO'Q.</b> Ular uchun
/// <c>IEmployeeReadModel.ListMissingProbationsAsync</c> allaqachon bor va
/// «sinovsiz qolgan munosabat» ta'rifi ANIQ BITTA joyda turishi kerak.
/// Ikkinchi nusxa yozilsa, <c>/api/employments/missing</c> sahifasi bilan
/// bildirishnoma bir kun turlicha javob berardi.
/// </para>
/// <para>
/// Amalga oshirilishi Infrastructure qatlamida (proyeksiya so'rovi).
/// </para>
/// </remarks>
public interface INotificationReadModel
{
    /// <summary>
    /// Muddati yaqinlashgan yoki o'tib ketgan, hamon YAKUNLANMAGAN sinovlar.
    /// </summary>
    /// <remarks>
    /// Bitta so'rov uchala sinov turini ham qoplaydi (yaqinlashgan, bugun
    /// tugaydigan, o'tib ketgan): ular faqat <c>EndDate</c> ning bugunga
    /// nisbatiga qarab ajraladi va uchta alohida so'rov bir xil indeksni
    /// uch marta o'qib chiqardi. Turni yig'uvchi tanlaydi.
    /// </remarks>
    /// <param name="until">
    /// Shu sanagacha (o'zi ham kiradi) tugaydigan sinovlar. Yuqori chegara
    /// bor, quyi chegara YO'Q — muddati o'tib ketganlar qancha eski
    /// bo'lsa ham kerak, aynan ular unutilib qoladi.
    /// </param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyList<ProbationDeadlineRow>> ListProbationDeadlinesAsync(
        DateOnly until,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Berilgan ikki kunda tug'ilgan kuni bo'lgan <b>ishlayotgan</b> xodimlar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Yil e'tiborga olinmaydi — faqat kun va oy (bosh sahifadagi
    /// «bugungi tug'ilgan kunlar» sanog'i bilan AYNAN bir xil qoida).
    /// </para>
    /// <para>
    /// Arxivdagilar chiqmaydi: ishdan ketgan odamni tabriklashga chaqirish
    /// noqulay vaziyat yaratardi. Sinovdagilar esa kiradi — ular bugun
    /// ishga keladi.
    /// </para>
    /// </remarks>
    /// <param name="today">Bugungi sana.</param>
    /// <param name="tomorrow">Ertangi sana — ikkalasi bitta so'rovda olinadi.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyList<BirthdayRow>> ListBirthdaysAsync(
        DateOnly today,
        DateOnly tomorrow,
        CancellationToken cancellationToken = default);

    /// <summary>Yaqinda qo'shilgan va hamon voronkada turgan nomzodlar.</summary>
    /// <param name="since">Shu sanadan boshlab qo'shilganlar (o'zi ham kiradi).</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyList<NewCandidateRow>> ListNewCandidatesAsync(
        DateOnly since,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Javob muddati yaqinlashgan yoki o'tib ketgan, hamon JAVOB KUTAYOTGAN takliflar.
    /// </summary>
    /// <remarks>
    /// Muddatsiz takliflar (<c>ExpiresAt is null</c>) bu yerga tushmaydi:
    /// HR ataylab «o'ylab ko'ring» deb muddatsiz taklif qilishi mumkin va
    /// unga eslatma yuborish uchun hech qanday sana yo'q.
    /// </remarks>
    /// <param name="until">Shu sanagacha (o'zi ham kiradi) muddati tugaydiganlar.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyList<JobOfferDeadlineRow>> ListOfferDeadlinesAsync(
        DateOnly until,
        CancellationToken cancellationToken = default);
}

/// <summary>Yakunlanmagan sinov muddati — bildirishnoma uchun kerak bo'lgan minimum.</summary>
/// <remarks>
/// Qatorda rasm kaliti ham, identifikatorlar to'plami ham yo'q:
/// bildirishnoma kartochka emas, bir qatorlik matn va bitta havola.
/// Ortiqcha maydon bu yerda foydasiz tarmoq yuki bo'lardi.
/// </remarks>
/// <param name="ProbationId">Sinov yozuvi — kalitning va havolaning asosi.</param>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="EmployeeName">To'liq F.I.Sh. — matnda ko'rinadi.</param>
/// <param name="DepartmentName">Bo'lim nomi (bo'lsa) — bir xil ismli odamlarni ajratadi.</param>
/// <param name="PositionName">Lavozim nomi (bo'lsa).</param>
/// <param name="EndDate">Sinov tugash sanasi — kalitning ikkinchi qismi.</param>
public sealed record ProbationDeadlineRow(
    Guid ProbationId,
    Guid EmployeeId,
    string EmployeeName,
    string? DepartmentName,
    string? PositionName,
    DateOnly EndDate);

/// <summary>Tug'ilgan kun — bitta xodim, bitta sana.</summary>
/// <param name="EmployeeId">Xodim — kalitning va havolaning asosi.</param>
/// <param name="EmployeeName">To'liq F.I.Sh.</param>
/// <param name="BirthDate">Tug'ilgan sana — yosh shundan hisoblanadi.</param>
public sealed record BirthdayRow(Guid EmployeeId, string EmployeeName, DateOnly BirthDate);

/// <summary>Yangi nomzod.</summary>
/// <param name="CandidateId">Nomzod — kalitning va havolaning asosi.</param>
/// <param name="CandidateName">To'liq F.I.Sh.</param>
/// <param name="VacancyTitle">Qaysi e'longa (vakansiya nomi). Vakansiya o'chirilgan bo'lsa — <c>null</c>.</param>
/// <param name="Source">Qayerdan kelgan: HR qo'lda kiritdimi yoki Telegram bot.</param>
/// <param name="AddedOn">Qo'shilgan kun (Toshkent vaqti bo'yicha).</param>
public sealed record NewCandidateRow(
    Guid CandidateId,
    string CandidateName,
    string? VacancyTitle,
    CandidateSource Source,
    DateOnly AddedOn);

/// <summary>Javob kutayotgan ish taklifi.</summary>
/// <remarks>
/// Oklad qatorda XOM holda keladi (miqdor + valyuta), tayyor matn emas:
/// uni ko'rsatish yoki YASHIRISH <c>salary.view</c> ruxsatiga bog'liq va
/// bu qaror Application qatlamida qabul qilinadi (<c>SalaryAccess</c>
/// bilan bir xil naqsh).
/// </remarks>
/// <param name="OfferId">Taklif — kalitning asosi.</param>
/// <param name="CandidateId">Nomzod — havola nomzod kartasiga olib boradi.</param>
/// <param name="CandidateName">Nomzodning to'liq F.I.Sh.</param>
/// <param name="VacancyTitle">Qaysi e'longa (vakansiya nomi).</param>
/// <param name="ExpiresAt">Javob kutiladigan oxirgi sana — kalitning ikkinchi qismi.</param>
/// <param name="SalaryAmount">Taklif qilingan oylik ish haqi.</param>
/// <param name="SalaryCurrency">Valyuta kodi (ISO 4217).</param>
public sealed record JobOfferDeadlineRow(
    Guid OfferId,
    Guid CandidateId,
    string CandidateName,
    string? VacancyTitle,
    DateOnly ExpiresAt,
    decimal SalaryAmount,
    string SalaryCurrency);
