using Bonvi.Domain.Common;
using Bonvi.Domain.Notifications;

namespace Bonvi.Application.Notifications.Common;

/// <summary>Bildirishnomalar bo'yicha xatolar.</summary>
/// <remarks>
/// Bu yerda «topilmadi» xatosi ATAYLAB yo'q. Bildirishnoma bazada
/// saqlanmaydi — u shartdan hosil bo'ladi. Ya'ni «mavjud emas» degan
/// holat ma'noga ega emas: shart o'zgargan bo'lishi mumkin va bunday
/// kalitni «o'qildi» qilish butunlay zararsiz (yozuv qoladi, hech
/// qachon ishlatilmaydi). Xato faqat kalit UMUMAN noto'g'ri
/// bo'lganda — ya'ni mijoz noto'g'ri narsa yuborganda — beriladi.
/// </remarks>
public static class NotificationErrors
{
    /// <summary>Tizimga kirmagan foydalanuvchi — o'z bildirishnomalari yo'q.</summary>
    /// <remarks>
    /// Amalda bu holatga yetib bo'lmaydi: barcha endpoint'lar
    /// autentifikatsiyadan o'tadi. Lekin «tokendagi identifikator yo'q»
    /// degan chekka holat mavjud (buzilgan token) va u <c>null</c>
    /// bo'yicha yiqilish emas, ma'noli javob bo'lishi kerak.
    /// </remarks>
    public static Error NotAuthenticated { get; } =
        Error.Forbidden(
            "bildirishnoma.foydalanuvchi_aniqlanmadi",
            "Bildirishnomalar shaxsiy — foydalanuvchi aniqlanmadi.");

    /// <summary>Kalit noma'lum turga tegishli yoki umuman kalit emas.</summary>
    public static Error InvalidKey { get; } =
        Error.Validation(
            "bildirishnoma.kalit_notogri",
            "Bildirishnoma kaliti noto'g'ri yoki noma'lum turga tegishli.");

    /// <summary>Bitta so'rovda juda ko'p kalit.</summary>
    public static Error TooManyKeys { get; } =
        Error.Validation(
            "bildirishnoma.kalit_kop",
            $"Bitta so'rovda {NotificationRules.MaxKeysPerRequest} tadan ortiq kalit yuborib bo'lmaydi.");

    /// <summary>Kalit ham, «hammasi» ham ko'rsatilmagan.</summary>
    public static Error NothingToDo { get; } =
        Error.Validation(
            "bildirishnoma.kalit_yoq",
            "Kalitlar ro'yxati bo'sh: qaysi bildirishnoma ustida amal bajarilishi ko'rsatilmagan.");
}
