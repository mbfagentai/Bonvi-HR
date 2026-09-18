using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.PublicApi.Abstractions;

/// <summary>
/// Tashqi API ning <b>yagona</b> o'qish porti.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega <c>IEmployeeReadModel</c> qayta
/// ishlatilmaydi.</b> U ichki ekranlar uchun va uning qatori
/// (<c>EmployeeSearchRow</c>) JSHSHIR, pasport va OKLADNI o'z ichiga
/// oladi. Undan foydalanish «keraksiz maydonlarni tashlab yuborish»
/// ishonchiga tayanardi — ya'ni maydon sizib chiqishi bitta unutilgan
/// <c>Select</c> dan iborat bo'lardi. Bu yerdagi qatorlarda o'sha
/// ma'lumot UMUMAN YO'Q: bazadan ham o'qilmaydi.
/// </para>
/// <para>
/// <b>Filtr, saralash va sahifalash BAZADA</b> — ichki o'qish modeli
/// bilan bir xil qoida va bir xil sabab.
/// </para>
/// </remarks>
public interface IPublicApiReadModel
{
    /// <summary>Xodimlar ro'yxati — ommaviy shakl.</summary>
    Task<PublicEmployeePage> SearchAsync(
        PublicEmployeeFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>Bitta xodim. <c>null</c> — topilmadi yoki yumshoq o'chirilgan.</summary>
    Task<PublicEmployeeRow?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Yumshoq o'chirilgan xodimlar — iste'molchi o'z nusxasini tozalashi uchun.
    /// </summary>
    /// <param name="since">
    /// Shu lahzadan KEYIN o'chirilganlar. <c>null</c> — hammasi.
    /// </param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyList<PublicDeletionRow>> ListDeletionsAsync(
        DateTimeOffset? since,
        CancellationToken cancellationToken = default);

    /// <summary>Ma'lumotnoma yozuvlari — nom bo'yicha tartiblangan.</summary>
    Task<IReadOnlyList<PublicReferenceRow>> ListReferenceAsync(
        PublicReferenceKind kind,
        CancellationToken cancellationToken = default);

    /// <summary>Xodimning rasm kaliti. <c>null</c> — xodim yo'q yoki rasmi yo'q.</summary>
    /// <remarks>
    /// Faqat KALIT o'qiladi, butun xodim emas: rasm marshrutiga boshqa
    /// hech narsa kerak emas va ortiqcha ustunlarni o'qish — ortiqcha
    /// xavf.
    /// </remarks>
    Task<string?> FindPhotoKeyAsync(Guid employeeId, CancellationToken cancellationToken = default);
}

/// <summary>Ommaviy <c>/reference/*</c> marshrutlari qaysi ma'lumotnomaga tegishli.</summary>
public enum PublicReferenceKind
{
    /// <summary>Yuridik shaxslar.</summary>
    LegalEntities = 0,

    /// <summary>Bo'limlar (daraxt — <c>ParentId</c> bilan).</summary>
    Departments = 1,

    /// <summary>Lavozimlar.</summary>
    Positions = 2,

    /// <summary>Biznes yo'nalishlari.</summary>
    Directions = 3,
}

/// <summary>
/// Ommaviy xodimlar ro'yxatining filtri.
/// </summary>
/// <param name="Search">
/// Xodim bo'yicha qidiruv — loyihaning MAVJUD qoidasi
/// (<c>EmployeeSearchQuery</c>): F.I.Sh., JSHSHIR, pasport, telefon.
/// <para>
/// ⚠️ JSHSHIR va pasport bo'yicha QIDIRISH mumkin, lekin ular javobga
/// TUSHMAYDI. Bu ataylab: iste'molchi o'zida bor bo'lgan raqam bilan
/// xodimni topa olishi kerak (aynan shuning uchun kalit berilgan),
/// lekin bazadan yangi shaxsiy ma'lumot OLA olmasligi kerak.
/// </para>
/// </param>
/// <param name="LegalEntityId">Yuridik shaxs bo'yicha filtr.</param>
/// <param name="DepartmentId">Bo'lim — ICHKI bo'limlari bilan birga (ichki ro'yxatdagi kabi).</param>
/// <param name="PositionId">Lavozim bo'yicha filtr.</param>
/// <param name="DirectionId">Yo'nalish bo'yicha filtr.</param>
/// <param name="Status">Mehnat munosabati holati. <c>null</c> — hammasi.</param>
/// <param name="UpdatedSince">
/// Shu lahzadan KEYIN o'zgargan yozuvlar. Berilganda tartib
/// <c>updatedAt</c> bo'yicha O'SISH tartibida bo'ladi — bosqichma-bosqich
/// sinxronlash uchun (aks holda ismga qarab tartiblanadi).
/// </param>
/// <param name="Page">Sahifa raqami, 1 dan.</param>
/// <param name="PageSize">Sahifadagi yozuvlar soni.</param>
public sealed record PublicEmployeeFilter(
    string? Search = null,
    Guid? LegalEntityId = null,
    Guid? DepartmentId = null,
    Guid? PositionId = null,
    Guid? DirectionId = null,
    EmploymentStatus? Status = null,
    DateTimeOffset? UpdatedSince = null,
    int Page = 1,
    int PageSize = 50)
{
    /// <summary>Nechta yozuv o'tkazib yuboriladi.</summary>
    public int Skip => (Page - 1) * PageSize;
}

/// <summary>Bitta sahifa: yozuvlar va umumiy soni.</summary>
public sealed record PublicEmployeePage(IReadOnlyList<PublicEmployeeRow> Items, int Total);

/// <summary>
/// Ommaviy javobning bitta xodim qatori.
/// </summary>
/// <remarks>
/// ⚠️ Bu yozuvga yangi maydon qo'shish — SPEC §1 chegarasini
/// o'zgartirish demak. Nima chiqmasligi <c>PublicEmployeeDto</c> izohida
/// sanab o'tilgan.
/// </remarks>
public sealed record PublicEmployeeRow(
    Guid Id,
    string LastName,
    string FirstName,
    string? MiddleName,
    DateOnly? BirthDate,
    Gender? Gender,
    string? Phone,
    string? TelegramUrl,
    string? PhotoKey,
    EmploymentStatus? Status,
    DateOnly? HireDate,
    DateOnly? TerminationDate,
    IReadOnlyList<PublicEmploymentRow> Employments,
    IReadOnlyList<PublicNamedRow> Directions,
    DateTimeOffset UpdatedAt);

/// <summary>Ommaviy javobdagi bitta mehnat munosabati.</summary>
public sealed record PublicEmploymentRow(
    Guid Id,
    Guid LegalEntityId,
    string? LegalEntityName,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? PositionId,
    string? PositionName,
    decimal Rate,
    EmploymentStatus Status,
    DateOnly HireDate,
    DateOnly? TerminationDate,
    bool IsPrimary);

/// <summary>Nomlangan yozuv — yo'nalish yoki ma'lumotnoma qatori.</summary>
public sealed record PublicNamedRow(Guid Id, string Name);

/// <summary>Ma'lumotnoma qatori — bo'limlarda ota yozuv bilan.</summary>
public sealed record PublicReferenceRow(Guid Id, string Name, Guid? ParentId);

/// <summary>O'chirilgan xodim: identifikator va o'chirilgan lahza.</summary>
public sealed record PublicDeletionRow(Guid Id, DateTimeOffset DeletedAt);
