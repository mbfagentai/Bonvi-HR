using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Ingest;

/// <summary>
/// Bitta ishlab chiqaruvchining hodisa formatini o'qiydigan tahlilchi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Tahlilchi qurilma YOZUVIGA qarab tanlanadi, tanaga qarab emas</b>
/// (<see cref="AttendanceDevice.Provider"/>). Tanani hidlab tanlash
/// chaqiruvchiga tahlilchini o'zi tanlash imkonini berardi, tahlilchi esa
/// hujum yuzasi. Shu sababli <c>Generic</c> qurilma multipart yuborsa ham,
/// <c>Hikvision</c> qurilma oddiy JSON yuborsa ham — «o'qib bo'lmadi».
/// </para>
/// <para>
/// Yangi ishlab chiqaruvchi (ZKTeco) qo'shish — bitta enum qiymati, shu
/// interfeysning bitta amalga oshirilishi va <c>IngestRegistration</c> da
/// bitta qator. Endpoint, buyruq ishlovchisi, sxema va interfeys tegilmaydi.
/// </para>
/// <para>
/// Tahlilchi singleton bo'lib ro'yxatga olinadi, ya'ni so'rovga oid holat
/// saqlamaydi va oqimga xavfsiz bo'lishi shart (Hikvision tahlilchisidagi
/// «bu turni logga yozdimmi» to'plami — yagona, faqat diagnostik holat).
/// Bazaga murojaat qilmaydi: u faqat baytni ma'noga aylantiradi. Shaxsni
/// xodimga bog'lash, takrorni ushlash va yo'nalishni qurilma rolidan
/// to'ldirish — buyruq ishlovchisining ishi.
/// </para>
/// </remarks>
public interface IAttendanceEventParser
{
    /// <summary>Qaysi qurilma turining formatini o'qiydi.</summary>
    AttendanceProvider Provider { get; }

    /// <summary>
    /// Tanadagi hodisalarni o'qiydi.
    /// </summary>
    /// <remarks>
    /// Bo'sh ro'yxat — XATO EMAS: qurilma o'tish bo'lmagan hodisani (yurak
    /// urishi, eshik holati, rad etilgan yuz) yuborgan va u ataylab
    /// tashlandi. Javob 200 va <c>stored: 0</c> bo'ladi — aks holda qurilma
    /// uni qayta-qayta yuboraverardi.
    /// </remarks>
    /// <param name="payload">Api qatlami o'qigan so'rov tanasi.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    /// <returns>O'tishlar ro'yxati yoki «o'qib bo'lmadi» xatosi.</returns>
    Task<Result<IReadOnlyList<RawAttendanceEvent>>> ParseAsync(
        AttendanceIngestPayload payload,
        CancellationToken cancellationToken);
}
