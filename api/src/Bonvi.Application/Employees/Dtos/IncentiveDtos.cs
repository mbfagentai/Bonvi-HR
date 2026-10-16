using Bonvi.Domain.Employees;

namespace Bonvi.Application.Employees.Dtos;

/// <summary>Rag'bat yoki jarima yozuvi (91.14 + HR so'ragan yangi jarima imkoniyati).</summary>
public sealed record IncentiveDto(
    Guid Id,
    Guid EmployeeId,
    IncentiveKind Kind,
    Guid TypeId,
    // Tur NOMI identifikatorining yonida — bo'lim/lavozim/firma bilan bir
    // xil naqsh: aks holda ro'yxatda «tur» ustuni doim bo'sh chiqardi.
    string? TypeName,
    DateOnly Date,
    MoneyDto? Amount,
    // Jarimada summa o'rniga foiz bo'lishi mumkin («4 %») — ikkalasi birga emas.
    decimal? Percent,
    string? GiftDescription,
    string? Reason,
    // Buyruq faqat rag'batda — jarima uchun buyruq chiqarilmaydi.
    string? OrderNo,
    DateOnly? OrderDate);

/// <summary>
/// Umumiy ro'yxatning bitta qatori — yozuv va uning EGASI.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega <see cref="IncentiveDto"/> ning o'zi yetmaydi.</b> U xodim
/// kartasi ichida ishlatiladi: u yerda egasi kim ekani sahifaning
/// sarlavhasida turadi. Umumiy ro'yxatda esa har qator boshqa odamniki
/// bo'lishi mumkin — ism va rasm kaliti qatorning O'ZIDA kerak.
/// </para>
/// <para>
/// <b>Nega sabab va sovg'a yo'q.</b> Jadval shakli foydalanuvchi bilan
/// kelishilgan: sana · xodim · turi · summa · buyruq. Sabab — erkin va
/// ba'zan uzun matn; u yozuvning tafsiloti va xodim kartasidagi tabda
/// to'liq ko'rinadi. Uni bu yerga qo'shish qatorni ikki barobar
/// balandlashtirib, ro'yxatning asosiy vazifasini — «qachon, kimga,
/// qaysi buyruq bilan» ni tez ko'zdan kechirishni — buzardi.
/// </para>
/// <para>
/// <b>Summa <c>salary.view</c> ga bog'liq EMAS.</b> Xodim kartasidagi tab
/// (<see cref="IncentiveDto"/>, <c>EmployeeMapper</c>) allaqachon shunday
/// ishlaydi va ikkinchi qoida paydo bo'lmasligi kerak — batafsil sabab
/// <c>ListIncentivesQueryHandler</c> dagi summa yig'uvchi metodda.
/// </para>
/// </remarks>
public sealed record IncentiveListItemDto(
    Guid Id,
    // Qator bosilganda xodim kartasi ochiladi, shu sababli identifikator
    // ham, ism ham, rasm kaliti ham bitta so'rovdan keladi.
    Guid EmployeeId,
    string EmployeeName,
    string? PhotoKey,
    IncentiveKind Kind,
    Guid TypeId,
    string? TypeName,
    DateOnly Date,
    MoneyDto? Amount,
    decimal? Percent,
    string? OrderNo,
    DateOnly? OrderDate);
