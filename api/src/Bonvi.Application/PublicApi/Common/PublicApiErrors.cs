using Bonvi.Domain.Common;

namespace Bonvi.Application.PublicApi.Common;

/// <summary>
/// Tashqi API ning xatolari — bitta joyda.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b><see cref="InvalidKeyCode"/> — bitta xato, to'rtta sabab.</b> Kalit
/// yuborilmagan, shakli buzuq, noma'lum yoki bekor qilingan bo'lsa,
/// mijozga AYNAN bir xil javob boradi. Bu ataylab: har bir farq
/// hujumchiga bir bit ma'lumot beradi — «bu prefiks bazada bor», «bu
/// kalit ilgari ishlagan». Qo'llab-quvvatlash uchun esa farq kerak emas:
/// iste'molchi ikkala holatda ham yangi kalit so'raydi.
/// </para>
/// <para>
/// Xato kodlari <c>tashqi.</c> bilan boshlanadi va to'rtala tilga
/// tarjima qilinadi (<c>web/src/shared/i18n/locales/*.json</c>), garchi
/// ularni odam kamdan-kam ko'rsa ham: <c>./bonvi check</c> bu shartnomani
/// tekshiradi va istisno qoldirish uni jimgina buzardi.
/// </para>
/// </remarks>
public static class PublicApiErrors
{
    /// <summary>Kalit yo'q, shakli buzuq, noma'lum yoki bekor qilingan — 401.</summary>
    /// <remarks>
    /// Turi <see cref="ErrorType.Forbidden"/> EMAS va <see cref="ErrorType.Validation"/>
    /// ham emas: bu javob HTTP qatlamida 401 bo'lib qaytadi va uni
    /// <see cref="Result"/> quvuri emas, autentifikatsiya sxemasi yozadi
    /// (<c>ApiKeyAuthenticationHandler</c>). Bu yerda u faqat KOD va MATN
    /// manbai sifatida turadi — ikki joyda ikki xil matn bo'lmasin.
    /// </remarks>
    public const string InvalidKeyCode = "tashqi.kalit_notogri";

    /// <summary>401 javobidagi matn.</summary>
    public const string InvalidKeyMessage = "API kaliti yaroqsiz yoki bekor qilingan.";

    /// <summary>Shu nomdagi kuchdagi kalit allaqachon bor — 409.</summary>
    public static Error NameTaken => Error.Conflict(
        "tashqi.kalit_nomi_band",
        "Bu nom bilan kuchdagi kalit allaqachon mavjud.");

    /// <summary>Ommaviy marshrutda noma'lum xodim — 404.</summary>
    /// <remarks>
    /// ⭐ Ichki <c>xodim.topilmadi</c> dan ATAYLAB alohida kod: ommaviy
    /// yuzaning xatolari o'z nom fazosida yashaydi va iste'molchi ichki
    /// kodlarga bog'lanib qolmaydi. Yumshoq o'chirilgan xodim ham shu
    /// xatoni oladi — «bor edi, endi yo'q» degan javob
    /// <c>/employees/deletions</c> dan keladi.
    /// </remarks>
    public static Error EmployeeNotFound => Error.NotFound(
        "tashqi.xodim_topilmadi",
        "Xodim topilmadi.");

    /// <summary>Xodimda rasm yo'q — 404.</summary>
    public static Error PhotoNotFound => Error.NotFound(
        "tashqi.surat_yoq",
        "Xodimning rasmi yo'q.");

    /// <summary>Kalit topilmadi (ichki boshqaruv endpoint'i) — 404.</summary>
    public static Error KeyNotFound => Error.NotFound(
        "tashqi.kalit_topilmadi",
        "API kaliti topilmadi.");
}
