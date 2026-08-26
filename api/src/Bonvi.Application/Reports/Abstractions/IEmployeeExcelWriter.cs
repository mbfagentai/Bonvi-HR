using Bonvi.Application.Employees.Dtos;

namespace Bonvi.Application.Reports.Abstractions;

/// <summary>
/// Xodimlar ro'yxatini Excel (<c>.xlsx</c>) faylga yozadigan port.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega Application'da interfeys, amalga oshirilishi Infrastructure'da.</b>
/// <c>ClosedXML</c> — texnologiya tafsiloti (xuddi EF Core, fayl tizimi
/// kabi). Use-case «xodimlar ro'yxatini Excel qilib ber» deb so'raydi,
/// «qanday kutubxona bilan» — bilishi shart emas. Arxitektura testi
/// (<c>Application_EF_Core_ga_boglanmasligi_kerak</c> qatoridagi oiladan)
/// buni kompilyatsiya darajasida ta'minlaydi.
/// </para>
/// <para>
/// Qatorlar ALLAQACHON tayyor DTO: yozuvchi hech qanday biznes qoidasini
/// (masalan oklad kimga ko'rinishi) bilmaydi — bu qaror
/// <c>ExportEmployeesQueryHandler</c> da, xuddi ekrandagi ro'yxat kabi
/// allaqachon qabul qilingan.
/// </para>
/// </remarks>
public interface IEmployeeExcelWriter
{
    /// <summary>Qatorlarni bitta <c>.xlsx</c> faylining baytlariga aylantiradi.</summary>
    byte[] Write(IReadOnlyList<EmployeeListItemDto> rows);
}
