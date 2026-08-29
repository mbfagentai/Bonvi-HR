using Bonvi.Domain.Employees;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Application.Employees.Abstractions;

/// <summary>
/// Xodimlar bo'yicha <b>o'qish</b> porti — ro'yxat ekranlari uchun.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega Domain'dagi repozitoriy yetmaydi.</b> <see cref="Domain.Abstractions.IEmployeeRepository"/>
/// agregatni <i>identifikator bo'yicha</i> yuklaydi — bu yozish tomonining vazifasi.
/// Ro'yxat ekrani esa butunlay boshqa narsani so'raydi: sahifalangan, saralangan,
/// yettita filtr bilan cheklangan va bo'lim/lavozim/firma nomlari bilan
/// <i>birlashtirilgan</i> jadval. Agar bu Domain portiga qo'shilsa, domen
/// sahifalash, saralash va ko'rsatish modelini bilib qolardi.
/// </para>
/// <para>
/// <b>Nega xotirada filtrlanmaydi.</b> Bugun 540 xodim, arxiv bilan 2000+.
/// Hammasini yuklab, keyin <c>Where</c> qilish — har bir sahifa uchun butun
/// jadvalni tarmoqdan olib o'tish demak. Filtr, saralash va sahifalash
/// <b>bazada</b> bajariladi; bu port aynan shuning uchun bor.
/// </para>
/// <para>
/// Amalga oshirilishi Infrastructure qatlamida (proyeksiya so'rovi).
/// </para>
/// </remarks>
public interface IEmployeeReadModel
{
    /// <summary>Xodimlar ro'yxati — asosiy ekran.</summary>
    Task<EmployeeSearchPage> SearchEmployeesAsync(
        EmployeeSearchFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>Sinov muddatlari ro'yxati — xodim nomi va lavozimi bilan.</summary>
    Task<IReadOnlyList<ProbationRow>> ListProbationsAsync(
        ProbationListFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Holati «sinovda», lekin sinov muddati yozuvi YO'Q munosabatlar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Bu qanday paydo bo'ladi.</b> <c>Employment.Hire</c> munosabatni
    /// doim <c>Probation</c> holatida ochadi. Tizim ichida qabul qilingan
    /// xodimga sinov muddati birga tug'iladi va uni yopish bilan holat
    /// <c>Active</c> ga o'tadi. Excel'dan ko'chirishda esa boshqacha:
    /// sinov ma'lumoti faqat «91.5» varag'ida bor va u yerda bo'lmagan
    /// odam holatsiz qoladi — 2010-yilda ishga kirgan xodim ham «sinovda»
    /// bo'lib turaveradi.
    /// </para>
    /// <para>
    /// <b>Nega bu jimgina yo'qotish.</b> Bunday odam xodimlar ro'yxatida
    /// ham ko'rinmaydi (u yerda faqat doimiylar), sinov muddatlari
    /// sahifasida ham (u yerda sinov YOZUVLARI ro'yxatlanadi). Ya'ni
    /// tizimda bor, ekranda yo'q — hech qanday xato ham chiqmaydi.
    /// </para>
    /// <para>
    /// Qaror ATAYLAB avtomatlashtirilmagan: ro'yxat HR ga ko'rsatiladi va
    /// u tasdiqlaydi. «3 oydan oshgani — doimiy» degan qoida bilan tizim
    /// o'zi yopsa, haqiqatan sinovda turgan bir necha odam ham qo'shilib
    /// ketardi va buni keyin ajratib bo'lmasdi.
    /// </para>
    /// </remarks>
    Task<IReadOnlyList<MissingProbationRow>> ListMissingProbationsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Belgi qo'yilgan xodimlar ro'yxati (rezerv / qora ro'yxat / nazorat / qobiliyatli).</summary>
    /// <param name="type">Belgi turi — to'rt ro'yxatning qaysi biri.</param>
    /// <param name="onlyActive">Faqat berilgan kunda kuchda bo'lganlar.</param>
    /// <param name="on">Faollik qaysi kunga hisoblanadi.</param>
    /// <param name="search">
    /// XODIM bo'yicha qidiruv: F.I.Sh., JSHSHIR, pasport yoki telefon —
    /// xodimlar ro'yxatidagi bilan AYNAN bir xil qoida. <c>null</c> yoki
    /// bo'sh — qidiruvsiz. Qora ro'yxat yuzlab yozuvga yetishi mumkin va
    /// «bu odam qora ro'yxatdami?» degan savolga ko'z bilan varaqlab javob
    /// topib bo'lmaydi.
    /// </param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyList<EmployeeFlagRow>> ListFlagsAsync(
        EmployeeFlagType type,
        bool onlyActive,
        DateOnly on,
        string? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>Barcha buyruqlar — xodimdan qat'i nazar, bitta oqimda.</summary>
    /// <remarks>
    /// Xodim kartasidagi lenta bitta odamning yo'lini ko'rsatadi. HR esa
    /// teskari savol beradi: «avgustda kimlar ishga olindi», «185-buyruq
    /// kimniki edi». Bu savollarga javob beruvchi ro'yxat aynan shu port
    /// orqali quriladi va u ham sahifalanadi — hodisalar soni munosabatlar
    /// sonidan bir necha barobar ko'p bo'ladi.
    /// </remarks>
    Task<EmploymentEventPage> ListEmploymentEventsAsync(
        EmploymentEventFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Xodim rekrutingdan kelgan bo'lsa — uning NOMZODLIK bosqichlari
    /// («Кенгаш хулосаси» va boshqa suhbat xulosalari).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega o'qish modelida, rekruting repozitoriysida emas.</b> Savol
    /// «bu nomzodning tarixi qanday» emas, «BU XODIM qayerdan kelgan va u
    /// haqda nima yozilgan» — ya'ni yo'l xodimdan boshlanadi va to'rtta
    /// jadvaldan (munosabat → hodisa → bosqich yozuvi → bosqich nomi)
    /// o'tadi. Buni agregat yuklovchi repozitoriylar bilan yig'ish bir
    /// necha ketma-ket so'rov bo'lardi; proyeksiya esa bitta.
    /// </para>
    /// <para>
    /// <b>Bog'lanish <c>EmploymentEvent.CandidateId</c> orqali.</b> Nomzod
    /// yozuvining O'ZI so'ralmaydi — bu ataylab: nomzod yumshoq
    /// o'chirilgan bo'lsa ham xodimning kelib chiqishi ko'rinib turishi
    /// kerak, chunki fakt o'zgarmagan (sababi <c>EmploymentEvent.CandidateId</c>
    /// izohida). Nomzodlar jadvaliga birlashtirilsa, global yumshoq
    /// o'chirish filtri butun tarixni jimgina yo'q qilib qo'yardi.
    /// </para>
    /// <para>
    /// Bo'sh ro'yxat — XATO EMAS va u odatiy holat: 2347 xodimning deyarli
    /// hammasi Excel'dan ko'chirilgan va ularda nomzodlik yozuvi umuman
    /// yo'q.
    /// </para>
    /// </remarks>
    Task<IReadOnlyList<CandidateStageRow>> ListCandidateStagesAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default);
}

/// <summary>Xodimlar ro'yxatini saralash ustunlari.</summary>
/// <remarks>
/// Ataylab enum: mijozdan kelgan matn to'g'ridan-to'g'ri SQL'ga tushmaydi
/// va qo'llab-quvvatlanmaydigan ustun so'ralganda sukut bo'yicha tartib ishlaydi.
/// </remarks>
public enum EmployeeSortField
{
    /// <summary>Familiya, ism (sukut bo'yicha).</summary>
    Name = 0,

    /// <summary>Ishga kirish sanasi.</summary>
    HireDate = 1,

    /// <summary>Bo'lim nomi.</summary>
    Department = 2,

    /// <summary>Lavozim nomi.</summary>
    Position = 3,

    /// <summary>Mehnat munosabati holati.</summary>
    Status = 4,

    /// <summary>Tug'ilgan sana.</summary>
    BirthDate = 5,
}

/// <summary>
/// Xodimlar ro'yxatining filtri.
/// </summary>
/// <param name="Search">FIO / JSHSHIR / telefon / pasport bo'yicha qidiruv matni.</param>
/// <param name="DepartmentId">Bo'lim (ichki bo'limlari bilan birga qidirilishi kutiladi).</param>
/// <param name="PositionId">Lavozim.</param>
/// <param name="DirectionId">Yo'nalish — xodimga biriktirilgan ko'p-ga-ko'p bog'lanish.</param>
/// <param name="LegalEntityId">Yuridik shaxs (holding ichidagi firma).</param>
/// <param name="Status">Mehnat munosabati holati. <c>null</c> — arxiv bilan birga hammasi.</param>
/// <param name="NeedsReview">Faqat tuzatish kerak bo'lgan yozuvlar (Excel'dan kelgan shubhali ma'lumot).</param>
/// <param name="Page">Sahifa raqami, 1 dan boshlanadi.</param>
/// <param name="PageSize">Sahifadagi yozuvlar soni.</param>
/// <param name="Sort">Saralash ustuni.</param>
/// <param name="Descending">Teskari tartib.</param>
public sealed record EmployeeSearchFilter(
    string? Search = null,
    Guid? DepartmentId = null,
    Guid? PositionId = null,
    Guid? DirectionId = null,
    Guid? LegalEntityId = null,
    EmploymentStatus? Status = null,
    bool? NeedsReview = null,
    int Page = 1,
    int PageSize = 25,
    EmployeeSortField Sort = EmployeeSortField.Name,
    bool Descending = false)
{
    /// <summary>Nechta yozuv o'tkazib yuboriladi.</summary>
    public int Skip => (Page - 1) * PageSize;
}

/// <summary>Bitta sahifa: yozuvlar va umumiy soni.</summary>
/// <param name="Items">Shu sahifadagi yozuvlar.</param>
/// <param name="Total">Filtrga mos keladigan umumiy yozuvlar soni.</param>
public sealed record EmployeeSearchPage(IReadOnlyList<EmployeeSearchRow> Items, int Total);

/// <summary>
/// Ro'yxatdagi bitta qator — xodim va uning <b>asosiy</b> mehnat munosabati.
/// </summary>
/// <remarks>
/// Bir xodimning bir nechta munosabati bo'lishi mumkin, lekin ro'yxatda
/// har bir shaxs BIR MARTA ko'rinadi: HR «540 xodim» deb sanaydi, «610 ta
/// shartnoma» deb emas. Shu sababli qatorga <c>IsPrimary</c> munosabat
/// (bo'lmasa — eng oxirgisi) qo'shiladi.
/// </remarks>
public sealed record EmployeeSearchRow(
    Guid Id,
    string LastName,
    string FirstName,
    string? MiddleName,
    DateOnly? BirthDate,
    string? Pinfl,
    string? Passport,
    string? Phone,
    string? PhotoKey,
    bool NeedsReview,
    Guid? EmploymentId,
    Guid? LegalEntityId,
    string? LegalEntityName,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? PositionId,
    string? PositionName,
    EmploymentStatus? Status,
    DateOnly? HireDate,
    decimal? Rate,
    decimal? SalaryAmount,
    string? SalaryCurrency,
    IReadOnlyList<EmployeeFlagType> ActiveFlags,
    // Yo'nalishlar — xodimga biriktirilgan ko'p-ga-ko'p bog'lanish.
    // Ro'yxatda ular ism ostidagi kichik nishonlar bo'lib turadi va HR
    // ularga qarab «bu odam qaysi biznesda» degan savolga darhol javob
    // topadi. Nomi bilan keladi: identifikator ekranda hech nima demaydi.
    IReadOnlyList<EmployeeDirectionRow> Directions);

/// <summary>Yo'nalish — identifikator va ekranga chiqadigan nom.</summary>
public sealed record EmployeeDirectionRow(Guid Id, string Name);

/// <summary>Sinov muddatlari ro'yxatining filtri.</summary>
/// <param name="Status">Holat bo'yicha filtr. <c>null</c> — hammasi.</param>
/// <param name="EndingWithinDays">Shuncha kun ichida tugaydiganlar (odatda 7).</param>
/// <param name="Today">Bugungi sana — «qancha kun qoldi» shundan hisoblanadi.</param>
/// <param name="Search">
/// XODIM bo'yicha qidiruv: F.I.Sh., JSHSHIR, pasport yoki telefon —
/// xodimlar ro'yxatidagi bilan AYNAN bir xil qoida (sababi
/// <see cref="EmployeeSearchFilter.Search"/> da). Ataylab faqat ismga
/// cheklanmagan: sinovdagi ikkita «Каримов» bo'lsa, HR ularni JSHSHIR
/// bilan ajratadi.
/// </param>
public sealed record ProbationListFilter(
    ProbationStatus? Status,
    int? EndingWithinDays,
    DateOnly Today,
    string? Search = null);

/// <summary>Sinov muddati — xodim ma'lumoti bilan birga.</summary>
public sealed record ProbationRow(
    Guid Id,
    Guid EmploymentId,
    DateOnly StartDate,
    DateOnly EndDate,
    ProbationStatus Status,
    DateOnly? ResolvedAt,
    string? Conclusion,
    Guid? ReviewerEmployeeId,
    Guid EmployeeId,
    string EmployeeName,
    // Rasm kaliti — ro'yxatda xodim yuzi ko'rinishi uchun. HR odamni
    // ismidan emas, yuzidan taniydi, shu sababli u har bir ro'yxatda kerak.
    string? PhotoKey,
    string? DepartmentName,
    string? PositionName,
    // Identifikatorlar NOM bilan birga: «sinovdan o'tkazish» formasi
    // bo'lim va lavozim tanlovini avvalgi qiymat bilan to'ldiradi, nom
    // esa uni tanlab bo'lmaydi. Ikkalasi ham bitta so'rovdan keladi —
    // forma ochilganda qo'shimcha murojaat bo'lmasin.
    Guid? DepartmentId,
    Guid? PositionId,
    DateOnly HireDate);

/// <summary>
/// Sinov yozuvisiz «sinovda» qolgan munosabat — HR tasdiqlashi uchun qator.
/// </summary>
/// <param name="EmploymentId">Yopilishi kerak bo'lgan mehnat munosabati.</param>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="EmployeeName">To'liq ism — ro'yxatda ko'rinadi.</param>
/// <param name="PhotoKey">Rasm kaliti; HR odamni yuzidan taniydi.</param>
/// <param name="LegalEntityName">Qaysi firmada — holdingda bu muhim.</param>
/// <param name="DepartmentName">Bo'lim nomi.</param>
/// <param name="PositionName">Lavozim nomi.</param>
/// <param name="HireDate">Ishga kirgan sana.</param>
/// <param name="WorkedDays">
/// Bugungi kunga qancha ishlagani. HR qarorini aynan shu raqamga qarab
/// qabul qiladi: 16 yil ishlagan odam sinovda tura olmaydi.
/// </param>
public sealed record MissingProbationRow(
    Guid EmploymentId,
    Guid EmployeeId,
    string EmployeeName,
    string? PhotoKey,
    string? LegalEntityName,
    string? DepartmentName,
    string? PositionName,
    DateOnly HireDate,
    int WorkedDays);

/// <summary>
/// Buyruqlar ro'yxatining filtri.
/// </summary>
/// <param name="Search">
/// BUYRUQ RAQAMI <b>yoki</b> XODIM bo'yicha qidiruv (F.I.Sh., JSHSHIR,
/// pasport, telefon — xodimlar ro'yxatidagi bilan aynan bir xil qoida).
/// <para>
/// ⚠️ Ilgari bu yerda faqat buyruq raqami tekshirilardi: «HR qo'lida
/// qog'oz buyruq bilan keladi» deb izohlangan edi. Bu holatlarning
/// faqat yarmi — ikkinchi yarmida HR odamni biladi, buyruq raqamini esa
/// yo'q («Каримовga qanday buyruqlar chiqqan?») va bu savolga ekran
/// umuman javob bermasdi. Ikkala shart «YOKI» bilan birlashtirildi,
/// eski xatti-harakat esa buzilmadi.
/// </para>
/// </param>
/// <param name="Type">Hodisa turi (qabul, ko'chirish, bo'shatish...).</param>
/// <param name="From">Kuchga kirish sanasining quyi chegarasi (o'zi ham kiradi).</param>
/// <param name="To">Kuchga kirish sanasining yuqori chegarasi (o'zi ham kiradi).</param>
/// <param name="DepartmentId">
/// Bo'lim — ICHKI bo'limlari bilan birga (xodimlar ro'yxatidagi kabi).
/// Hodisaning yangi yoki eski bo'limiga mos keladi; hodisada bo'lim
/// umuman ko'rsatilmagan bo'lsa — munosabatning joriy bo'limiga.
/// </param>
/// <param name="LegalEntityId">Munosabat tegishli bo'lgan yuridik shaxs.</param>
/// <param name="Page">Sahifa raqami, 1 dan boshlanadi.</param>
/// <param name="PageSize">Sahifadagi yozuvlar soni.</param>
public sealed record EmploymentEventFilter(
    string? Search = null,
    EmploymentEventType? Type = null,
    DateOnly? From = null,
    DateOnly? To = null,
    Guid? DepartmentId = null,
    Guid? LegalEntityId = null,
    int Page = 1,
    int PageSize = 25)
{
    /// <summary>Nechta yozuv o'tkazib yuboriladi.</summary>
    public int Skip => (Page - 1) * PageSize;
}

/// <summary>Buyruqlarning bitta sahifasi: yozuvlar va umumiy soni.</summary>
/// <param name="Items">Shu sahifadagi hodisalar.</param>
/// <param name="Total">Filtrga mos keladigan umumiy hodisalar soni.</param>
public sealed record EmploymentEventPage(IReadOnlyList<EmploymentEventRow> Items, int Total);

/// <summary>
/// Buyruqlar ro'yxatining bitta qatori.
/// </summary>
/// <remarks>
/// <para>
/// Bo'lim va lavozim shu yerda NOM bo'lib turadi, identifikator bo'lib emas:
/// qator ekranda o'qiladi, va har bir nom uchun ma'lumotnomaga qayta so'rov
/// yuborish 25 qatorlik sahifada 100 ta ortiqcha so'rov bo'lardi.
/// </para>
/// <para>
/// Oklad ATAYLAB xom holida (summa + valyuta) qaytadi, tayyor DTO bo'lib
/// emas: uni ko'rsatish yoki yashirish <c>salary.view</c> ruxsatiga bog'liq
/// va bu qaror Application qatlamida — o'qish modeli ruxsatlarni bilmaydi.
/// </para>
/// </remarks>
public sealed record EmploymentEventRow(
    Guid Id,
    Guid EmploymentId,
    Guid EmployeeId,
    string EmployeeName,
    // Rasm kaliti — ro'yxatda xodim yuzi ko'rinishi uchun. HR odamni
    // ismidan emas, yuzidan taniydi, shu sababli u har bir ro'yxatda kerak.
    string? PhotoKey,
    EmploymentEventType Type,
    DateOnly EffectiveDate,
    string? OrderNo,
    DateOnly? OrderDate,
    string? FromDepartment,
    string? ToDepartment,
    string? FromPosition,
    string? ToPosition,
    decimal? FromSalaryAmount,
    string? FromSalaryCurrency,
    decimal? ToSalaryAmount,
    string? ToSalaryCurrency,
    string? Note,
    bool IsImported);

/// <summary>
/// Xodimning nomzodlik davridagi bitta bosqich yozuvi.
/// </summary>
/// <remarks>
/// Bosqich nomi CHAPDAN birlashtiriladi: ma'lumotnomadan bosqich
/// o'chirilgan bo'lsa ham xulosa matni yo'qolmasligi kerak — yozuvning
/// asosiy qiymati aynan matnda.
/// </remarks>
/// <param name="Id">Bosqich yozuvining identifikatori.</param>
/// <param name="CandidateId">Qaysi nomzod yozuvidan.</param>
/// <param name="StageId">Bosqich.</param>
/// <param name="StageName">Bosqich nomi. <c>null</c> — bosqich topilmadi.</param>
/// <param name="Date">Bosqich o'tkazilgan sana.</param>
/// <param name="Outcome">Natija: kutilmoqda / o'tdi / o'tmadi.</param>
/// <param name="Conclusion">Xulosa matni («Кенгаш хулосаси»).</param>
/// <param name="RecordedBy">Kim yozdi — TIZIM FOYDALANUVCHISI.</param>
/// <param name="RecordedByName">Yozgan foydalanuvchining F.I.Sh.</param>
/// <param name="RecordedAt">Qachon yozildi.</param>
public sealed record CandidateStageRow(
    Guid Id,
    Guid CandidateId,
    Guid StageId,
    string? StageName,
    DateOnly Date,
    StageOutcome Outcome,
    string? Conclusion,
    Guid? RecordedBy,
    string? RecordedByName,
    DateTimeOffset RecordedAt);

/// <summary>Belgi — xodim nomi bilan birga.</summary>
public sealed record EmployeeFlagRow(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    // Rasm kaliti — ro'yxatda xodim yuzi ko'rinishi uchun. HR odamni
    // ismidan emas, yuzidan taniydi, shu sababli u har bir ro'yxatda kerak.
    string? PhotoKey,
    EmployeeFlagType Type,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Reason,
    Guid? ResponsibleEmployeeId,
    string? ResponsibleName);
