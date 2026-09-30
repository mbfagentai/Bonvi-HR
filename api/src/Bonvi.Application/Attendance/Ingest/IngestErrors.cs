using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Ingest;

/// <summary>Qurilmadan hodisa qabul qilish yo'lidagi xatolar.</summary>
/// <remarks>
/// <para>
/// Bu yerda faqat QABUL yo'lining o'zi yasaydigan kodlar turadi.
/// <c>davomat.vaqt_yaroqsiz</c> va <c>davomat.shaxs_raqami_bosh</c> domenda
/// (<c>AttendancePass.Record</c>) tug'iladi — ikkinchi nusxasi bu yerda
/// yozilsa, bir kun matni ajralib ketardi. Ular ham shu yerdan qaytariladi,
/// lekin o'sha domen <c>Error</c> i orqali.
/// </para>
/// </remarks>
public static class IngestErrors
{
    /// <summary>
    /// Autentifikatsiyaning HAR QANDAY nosozligi — bitta kod, bitta matn.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Token yo'q, token noto'g'ri, imzo mos emas, manzil ruxsat etilmagan —
    /// to'rttasi ham AYNAN shu javobni oladi. Tekshiruvchi qaysi bosqichda
    /// yiqilganini bilmasligi kerak: aks holda u avval tokenni, keyin imzoni
    /// alohida-alohida tanlab ko'rardi. Haqiqiy sabab faqat logga tushadi
    /// (<see cref="IAttendanceIngestJournal.AuthenticationFailed"/>).
    /// </para>
    /// <para>
    /// ⚠️ <c>ErrorType</c> da 401 yo'q, shu sababli bu xato <c>Forbidden</c>
    /// turi bilan yuradi va HTTP holatini endpoint o'zi 401 ga o'giradi
    /// (<c>AttendanceIngestEndpoints</c>). Umumiy <c>ResultExtensions</c> ga
    /// yangi tur qo'shilmadi: 401 butun tizimda faqat shu yerda domen
    /// natijasidan chiqadi, qolgan hamma joyda uni JWT o'rta qatlami beradi.
    /// </para>
    /// </remarks>
    public static Error Unauthorized { get; } =
        Error.Forbidden("davomat.ingest_ruxsat_yoq", "Qurilma tasdiqlanmadi.");

    /// <summary>Token yaroqli, lekin qurilma o'chirib qo'yilgan.</summary>
    /// <remarks>
    /// ATAYLAB <see cref="Unauthorized"/> dan farq qiladi: chaqiruvchida
    /// yaroqli token bor, ya'ni u hech narsani «tanlab» o'tirmayapti, o'rnatuvchiga
    /// esa «qurilma buzilgan» emas, «o'chirilgan» degan javob kerak.
    /// </remarks>
    public static Error DeviceInactive { get; } =
        Error.Forbidden("davomat.qurilma_faolsiz", "Qurilma faolsizlantirilgan.");

    /// <summary>So'rov tanasi shu qurilmaning tahlilchisi uchun o'qib bo'lmaydigan.</summary>
    public static Error Unreadable { get; } =
        Error.Validation("davomat.hodisa_oqilmadi", "Qurilma hodisasini o'qib bo'lmadi.");

    /// <summary>Bitta so'rovda hodisalar juda ko'p.</summary>
    public static Error TooManyEvents { get; } =
        Error.Validation(
            "davomat.hodisa_kop",
            $"Bitta so'rovda {IngestLimits.MaxEventsPerRequest} tadan ortiq hodisa yuborib bo'lmaydi.");
}
