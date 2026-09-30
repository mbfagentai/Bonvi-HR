using Bonvi.Application.Attendance.Dtos;

namespace Bonvi.Application.Attendance.Abstractions;

/// <summary>
/// Davomat hisobotlarini Excel (<c>.xlsx</c>) faylga yozadigan port.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega Application'da interfeys.</b> <c>ClosedXML</c> — texnologiya
/// tafsiloti; use-case «tabelni Excel qilib ber» deydi, qaysi kutubxona
/// bilan ekanini bilmaydi (<c>IEmployeeExcelWriter</c> bilan bir xil
/// sabab, arxitektura testi buni kompilyatsiyada ta'minlaydi).
/// </para>
/// <para>
/// Qatorlar ALLAQACHON tayyor DTO — ekrandagi bilan aynan bir xil
/// so'rovdan. Yozuvchi hech qanday qoidani (qaysi kun «kelmagan», qaysi
/// kun hosil qilingan) bilmaydi va qayta hisoblamaydi: aks holda Excel va
/// ekran bir kun ikki xil son ko'rsatardi.
/// </para>
/// <para>
/// ⭐ <b>Ikkita qat'iy qoida, to'rtala fayl uchun.</b> Sana — HAQIQIY Excel
/// sanasi (<c>dd.mm.yyyy</c>), matn emas: HR saralaydi va filtrlaydi.
/// Davomiylik — IKKI ustun: daqiqa SON bo'lib (yig'indilanadi) va
/// «soat:daqiqa» MATN bo'lib (o'qiladi). Faqat matn bo'lsa yig'ib bo'lmaydi,
/// faqat daqiqa bo'lsa «510» ni hech kim o'qimaydi (SPEC §6.7).
/// </para>
/// </remarks>
public interface IAttendanceExcelWriter
{
    /// <summary>Tabel: oy katagi va har kunning batafsil qatorlari.</summary>
    /// <param name="timesheet">Butun tabel — sahifasiz.</param>
    byte[] WriteTimesheet(AttendanceTimesheetDto timesheet);

    /// <summary>O'tishlar jurnali.</summary>
    /// <param name="rows">O'tishlar, ekrandagi tartibda.</param>
    byte[] WritePasses(IReadOnlyList<AttendancePassDto> rows);

    /// <summary>Chetlanishlar ro'yxati.</summary>
    /// <param name="kind">Qaysi ro'yxat — varaq nomi shundan.</param>
    /// <param name="rows">Qatorlar, ekrandagi tartibda.</param>
    byte[] WriteExceptions(ExceptionKind kind, IReadOnlyList<AttendanceExceptionDto> rows);

    /// <summary>Davr bo'yicha xulosa.</summary>
    /// <param name="rows">Qatorlar, ekrandagi tartibda.</param>
    byte[] WriteSummary(IReadOnlyList<AttendanceSummaryDto> rows);
}
