using Bonvi.Application.Identity.EmployeeAuth;
using Microsoft.Extensions.Logging;

namespace Bonvi.Infrastructure.Services;

/// <summary>
/// <see cref="IEmployeeAuthJournal"/> — <c>ILogger</c> orqali, EventId 5400+.
/// </summary>
/// <remarks>
/// <para>
/// Infrastructure qatlamida (<c>AttendanceJobJournal</c> bilan bir xil joy):
/// bu yerda HTTP tushunchasi yo'q, faqat identifikatorlar yoziladi.
/// </para>
/// <para>
/// ⚠️ Ikkinchi va uchinchi yozuv OGOHLANTIRISH darajasida, ya'ni ular xato
/// jurnali fayliga ham tushadi. Bu ataylab: «ishdan ketgan odam ilovaga
/// kirmoqchi bo'ldi» va «bekor qilingan token qayta ishlatildi» — HR yoki
/// xavfsizlik e'tibor berishi kerak bo'lgan ikki hodisa. Birinchisi esa
/// ma'lumot darajasida: o'chirilgan xodimning hisobi odatda oddiy ma'lumot
/// tozalashdan keyin qoladigan iz.
/// </para>
/// </remarks>
internal sealed partial class EmployeeAuthJournal : IEmployeeAuthJournal
{
    private readonly ILogger<EmployeeAuthJournal> _logger;

    public EmployeeAuthJournal(ILogger<EmployeeAuthJournal> logger) => _logger = logger;

    public void EmployeeMissing(Guid accountId) => LogEmployeeMissing(accountId);

    public void NotEmployed(Guid employeeId) => LogNotEmployed(employeeId);

    public void RefreshTokenReused(Guid accountId) => LogRefreshTokenReused(accountId);

    [LoggerMessage(
        EventId = 5400,
        Level = LogLevel.Information,
        Message = "Xodim ilovasiga kirish rad etildi: {Hisob} hisobi o'chirilgan xodimga tegishli (INV-1)")]
    private partial void LogEmployeeMissing(Guid hisob);

    [LoggerMessage(
        EventId = 5401,
        Level = LogLevel.Warning,
        Message = "Xodim ilovasiga kirish rad etildi: {Xodim} bugungi kunda ishlamaydi (INV-2)")]
    private partial void LogNotEmployed(Guid xodim);

    [LoggerMessage(
        EventId = 5402,
        Level = LogLevel.Warning,
        Message = "Xodim hisobi {Hisob}: bekor qilingan yangilash tokeni qayta ishlatildi — barcha sessiyalar yopildi")]
    private partial void LogRefreshTokenReused(Guid hisob);
}
