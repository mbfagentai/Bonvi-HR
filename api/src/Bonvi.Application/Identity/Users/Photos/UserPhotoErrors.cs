using Bonvi.Application.Common.Photos;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Identity.Users.Photos;

/// <summary>
/// Boshqa foydalanuvchining rasmi bo'yicha xatolar.
/// </summary>
/// <remarks>
/// <para>
/// Kodlar hisob rasmi kodlaridan (<c>hisob.rasm_*</c>) ATAYLAB ajratilgan —
/// xuddi <c>xodim.rasm_*</c> ular ikkalasidan ajratilgani kabi. Sabab
/// matnda ko'rinadi: «hisobingizda rasm yo'q» va «bu foydalanuvchida rasm
/// yo'q» — bir xil holat emas, ikkinchisi BOSHQA odam haqida. Bitta kodni
/// ikki ma'noda ishlatish tarjimani ikkalasiga ham noto'g'ri qilib qo'yardi.
/// </para>
/// <para>
/// Rad etish sabablari (hajm, tur, yaroqsizlik) esa <see cref="PhotoRejection"/>
/// dan o'giriladi: quvur bitta, faqat kod nomi bo'limnikiga aylanadi.
/// </para>
/// </remarks>
public static class UserPhotoErrors
{
    /// <summary>Hisob bor, lekin rasmi yo'q (yoki fayli yo'qolgan).</summary>
    public static Error NotFound { get; } =
        Error.NotFound("foydalanuvchi.rasm_yoq", "Bu foydalanuvchining rasmi yo'q.");

    public static Error TooLarge { get; } =
        Error.Validation("foydalanuvchi.rasm_katta", "Rasm hajmi 10 MB dan oshmasligi kerak.");

    public static Error TypeNotSupported { get; } =
        Error.Validation("foydalanuvchi.rasm_turi", "Faqat JPEG, PNG va WebP rasmlar qabul qilinadi.");

    /// <summary>Fayl rasm sifatida ochilmadi — tekshiruv MAZMUN bo'yicha.</summary>
    public static Error Invalid { get; } =
        Error.Validation("foydalanuvchi.rasm_yaroqsiz", "Fayl rasm emas yoki buzilgan.");

    /// <summary>Umumiy rasm quvurining rad etish sababini bo'lim kodiga o'giradi.</summary>
    public static Error Rejected(PhotoRejection rejection) => rejection switch
    {
        PhotoRejection.TooLarge => TooLarge,
        PhotoRejection.UnsupportedType => TypeNotSupported,
        PhotoRejection.Invalid => Invalid,
        _ => Invalid,
    };
}
