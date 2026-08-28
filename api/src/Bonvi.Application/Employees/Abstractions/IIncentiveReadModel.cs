using Bonvi.Domain.Employees;

namespace Bonvi.Application.Employees.Abstractions;

/// <summary>
/// Rag'bat va jarima bo'yicha <b>o'qish</b> porti — umumiy ro'yxat ekrani uchun.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega <see cref="Domain.Abstractions.IIncentiveRepository"/> yetmaydi.</b>
/// U agregatni xodim bo'yicha yuklaydi — xodim kartasidagi tab aynan shuni
/// so'raydi. Bu yerdagi savol esa teskari: «avgustda kimlarga jarima
/// yozildi», «shu yilgi barcha mukofotlar». Javob uchun xodim nomi,
/// ma'lumotnomadagi tur nomi va bo'lim/firma bog'lanishi bilan
/// BIRLASHTIRILGAN, sahifalangan jadval kerak — repozitoriy bunday
/// proyeksiyani bilmasligi kerak.
/// </para>
/// <para>
/// <b>Nega <see cref="IEmployeeReadModel"/> ga qo'shilmadi.</b> U xodimlar
/// moduliga tegishli va <c>employees.view</c> chegarasida yashaydi; rag'bat/
/// jarima esa ALOHIDA ruxsat bilan himoyalangan (<c>incentives.view</c> —
/// sababi <c>Permissions.IncentivesView</c> izohida: pul va intizomiy chora).
/// Ikkalasini bitta portga yig'ish ertami-kechmi «xodimlar ro'yxatini
/// ochgan odam jarimalarni ham ko'ra oladi» degan jimgina sizishga olib
/// kelardi.
/// </para>
/// <para>
/// Amalga oshirilishi Infrastructure qatlamida (proyeksiya so'rovi):
/// filtr, saralash va sahifalash BAZADA bajariladi —
/// <see cref="IEmployeeReadModel"/> dagi bilan bir xil sabab.
/// </para>
/// </remarks>
public interface IIncentiveReadModel
{
    /// <summary>Barcha rag'bat va jarima yozuvlari — xodimdan qat'i nazar.</summary>
    /// <remarks>
    /// Har bir YOZUV alohida qator bo'lib chiqadi, ya'ni bitta xodim
    /// ro'yxatda bir necha marta ko'rinishi mumkin. Bu ataylab: HR
    /// «Каримовга nechta jarima berilgan» degan yig'ma raqamni emas,
    /// «qachon, nima uchun, qaysi buyruq bilan» degan FAKTlarni izlaydi —
    /// yig'ma ko'rinish aynan shu tafsilotni yo'qotardi.
    /// </remarks>
    Task<IncentiveListPage> ListIncentivesAsync(
        IncentiveListFilter filter,
        CancellationToken cancellationToken = default);
}

/// <summary>Rag'bat/jarima ro'yxatini saralash ustunlari.</summary>
/// <remarks>
/// Ataylab enum (<see cref="EmployeeSortField"/> dagi bilan bir xil sabab):
/// mijozdan kelgan matn to'g'ridan-to'g'ri SQL'ga tushmaydi va
/// qo'llab-quvvatlanmaydigan ustun so'ralganda sukut bo'yicha tartib ishlaydi.
/// </remarks>
public enum IncentiveSortField
{
    /// <summary>Rag'bat/jarima sanasi (sukut bo'yicha, yangidan eskiga).</summary>
    Date = 0,

    /// <summary>Xodim familiyasi, ismi.</summary>
    EmployeeName = 1,
}

/// <summary>
/// Rag'bat/jarima ro'yxatining filtri.
/// </summary>
/// <param name="Search">
/// XODIM bo'yicha qidiruv: F.I.Sh., JSHSHIR, pasport yoki telefon —
/// xodimlar ro'yxatidagi bilan AYNAN bir xil qoida.
/// <para>
/// ⚠️ Ilgari bu yerda FAQAT F.I.Sh. ishlardi va bu ataylab qilingan deb
/// izohlangan edi («HR bu ekranga kimga nima berilgan degan savol bilan
/// keladi»). Amalda esa qoida teskari ishladi: xodimlar ro'yxatida
/// JSHSHIR bilan odamni topgan HR shu matnni rag'bat ro'yxatiga
/// ko'chirganda BO'SH natija olardi — hech qanday xato ham chiqmasdi.
/// Bitta qidiruv maydonining ikki ekranda ikki xil ishlashi tizimga
/// ishonchni yo'qotadi, shu sababli qoida birlashtirildi.
/// </para>
/// </param>
/// <param name="Kind">Rag'bat yoki jarima. Bo'sh bo'lsa — ikkalasi.</param>
/// <param name="From">Sananing quyi chegarasi (o'zi ham kiradi).</param>
/// <param name="To">Sananing yuqori chegarasi (o'zi ham kiradi).</param>
/// <param name="DepartmentId">
/// Bo'lim — ICHKI bo'limlari bilan birga (xodimlar va buyruqlar
/// ro'yxatidagi kabi). Rag'bat XODIMGA beriladi, mehnat munosabatiga emas
/// (sababi <see cref="Incentive"/> izohida), shu sababli shart xodimning
/// munosabatlaridan ISTALGAN BIRIGA mos kelsa yetarli.
/// </param>
/// <param name="LegalEntityId">Yuridik shaxs — bo'lim bilan bir xil mantiqda.</param>
/// <param name="Page">Sahifa raqami, 1 dan boshlanadi.</param>
/// <param name="PageSize">Sahifadagi yozuvlar soni.</param>
/// <param name="Sort">Saralash ustuni.</param>
/// <param name="Descending">Teskari tartib.</param>
public sealed record IncentiveListFilter(
    string? Search = null,
    IncentiveKind? Kind = null,
    DateOnly? From = null,
    DateOnly? To = null,
    Guid? DepartmentId = null,
    Guid? LegalEntityId = null,
    int Page = 1,
    int PageSize = 25,
    IncentiveSortField Sort = IncentiveSortField.Date,
    bool Descending = true)
{
    /// <summary>Nechta yozuv o'tkazib yuboriladi.</summary>
    public int Skip => (Page - 1) * PageSize;
}

/// <summary>Bitta sahifa: yozuvlar va umumiy soni.</summary>
/// <param name="Items">Shu sahifadagi yozuvlar.</param>
/// <param name="Total">Filtrga mos keladigan umumiy yozuvlar soni.</param>
public sealed record IncentiveListPage(IReadOnlyList<IncentiveListRow> Items, int Total);

/// <summary>
/// Ro'yxatdagi bitta qator — rag'bat/jarima yozuvi va uning xodimi.
/// </summary>
/// <remarks>
/// <para>
/// Tur NOMI qatorda tayyor keladi, identifikator emas: jadvalning «Turi»
/// ustuni ekranda o'qiladi va har bir qator uchun ma'lumotnomaga qayta
/// so'rov yuborish 50 qatorlik sahifada 50 ta ortiqcha murojaat bo'lardi
/// (<c>EmploymentEventRow</c> dagi bilan bir xil qoida).
/// </para>
/// <para>
/// Summa ATAYLAB xom holida (miqdor + valyuta) qaytadi, tayyor DTO bo'lib
/// emas: <c>MoneyDto</c> — Application qatlamining tipi va unda ekranga
/// chiqadigan <c>Formatted</c> matni bor. O'qish modeli ko'rsatish
/// modelini bilmasligi kerak.
/// </para>
/// </remarks>
public sealed record IncentiveListRow(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    // Rasm kaliti — ro'yxatda xodim yuzi ko'rinishi uchun. HR odamni
    // ismidan emas, yuzidan taniydi, shu sababli u har bir ro'yxatda kerak.
    string? PhotoKey,
    IncentiveKind Kind,
    Guid TypeId,
    // Ma'lumotnomadan o'chirilgan tur uchun `null` bo'lishi mumkin —
    // tashqi kalit yo'q (izohi `IncentiveConfiguration` da), ya'ni bu
    // kutilgan holat va qator baribir ro'yxatda qoladi.
    string? TypeName,
    DateOnly Date,
    decimal? AmountValue,
    string? AmountCurrency,
    string? OrderNo,
    DateOnly? OrderDate);
