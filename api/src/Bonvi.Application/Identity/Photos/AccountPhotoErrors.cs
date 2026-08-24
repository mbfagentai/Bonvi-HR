using Bonvi.Application.Common.Photos;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Identity.Photos;

/// <summary>
/// Hisob rasmi bo'yicha xatolar.
/// </summary>
/// <remarks>
/// Kodlar xodim rasmi kodlaridan (<c>xodim.rasm_*</c>) ATAYLAB ajratilgan:
/// interfeys ikkalasini boshqa joyda va boshqacha ko'rsatadi, matnlari ham
/// har xil («xodim rasmi» va «hisobingiz rasmi»). Bitta kodni ikki ma'noda
/// ishlatish tarjimani ikkalasiga ham noto'g'ri qilib qo'yardi.
/// </remarks>
public static class AccountPhotoErrors
{
    /// <summary>Hisob bor, lekin rasmi yo'q (yoki fayli yo'qolgan).</summary>
    public static Error NotFound { get; } =
        Error.NotFound("hisob.rasm_yoq", "Hisobingizda rasm yo'q.");

    public static Error TooLarge { get; } =
        Error.Validation("hisob.rasm_katta", "Rasm hajmi 10 MB dan oshmasligi kerak.");

    public static Error TypeNotSupported { get; } =
        Error.Validation("hisob.rasm_turi", "Faqat JPEG, PNG va WebP rasmlar qabul qilinadi.");

    /// <summary>Fayl rasm sifatida ochilmadi — tekshiruv MAZMUN bo'yicha.</summary>
    public static Error Invalid { get; } =
        Error.Validation("hisob.rasm_yaroqsiz", "Fayl rasm emas yoki buzilgan.");

    /// <summary>Umumiy rasm quvurining rad etish sababini hisob kodiga o'giradi.</summary>
    public static Error Rejected(PhotoRejection rejection) => rejection switch
    {
        PhotoRejection.TooLarge => TooLarge,
        PhotoRejection.UnsupportedType => TypeNotSupported,
        PhotoRejection.Invalid => Invalid,
        _ => Invalid,
    };
}
