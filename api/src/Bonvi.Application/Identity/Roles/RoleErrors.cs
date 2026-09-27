using Bonvi.Domain.Common;

namespace Bonvi.Application.Identity.Roles;

/// <summary>
/// Rollar muharririda uchraydigan xatolar.
/// </summary>
/// <remarks>
/// <para>
/// Xato kodlari BIR joyda — <see cref="Users.Common.UserErrors"/> bilan bir
/// xil sabab: frontend ularni to'rt tilda tarjima qiladi, kod tasodifan
/// o'zgarib ketmasligi kerak.
/// </para>
/// <para>
/// Nom va ruxsat kodlarining xatolari (<c>rol.nom_bosh</c>,
/// <c>rol.nom_uzun</c>, <c>rol.notanish_ruxsat</c>) bu yerda YO'Q: ular
/// qiymatning o'zi haqida va <c>Role</c> domenida tekshiriladi.
/// </para>
/// </remarks>
public static class RoleErrors
{
    /// <summary>Bunday identifikatorli rol yo'q.</summary>
    public static Error NotFound { get; } =
        Error.NotFound("rol.topilmadi", "Rol topilmadi.");

    /// <summary>Super administrator (yoki bot) rolini o'zgartirish yoki o'chirishga urinish.</summary>
    /// <remarks>
    /// <see cref="ErrorType.Conflict"/> (409): so'rov to'g'ri, lekin rolning
    /// holati (himoyalangan) unga qarshi.
    /// </remarks>
    public static Error Protected { get; } =
        Error.Conflict("rol.himoyalangan", "Bu rol himoyalangan — uni o'zgartirib ham, o'chirib ham bo'lmaydi.");

    /// <summary>O'zi egallab turgan rolni o'zgartirish yoki o'chirish.</summary>
    /// <remarks>
    /// Sabab foydalanuvchilardagi «o'z rolini o'zgartira olmaydi» qoidasi
    /// bilan bir xil: bitta noto'g'ri belgi bilan odam o'zini huquqsiz
    /// qoldirishi (yoki o'ziga yo'q huquqni berishi) mumkin edi.
    /// </remarks>
    public static Error OwnRole { get; } =
        Error.Conflict("rol.oz_roli", "O'zingiz egallab turgan rolni o'zgartira yoki o'chira olmaysiz.");

    /// <summary>
    /// Huquqlari so'rovchiniki bilan TENG va rollar yoki hisoblarni boshqaradigan
    /// boshqa rolga tegishga urinish.
    /// </summary>
    /// <remarks>
    /// Foydalanuvchilar tomonidagi «teng huquqli hamkasb» qoidasining
    /// (<c>foydalanuvchi.rolga_haqqi_yoq</c>) rollar tomonidagi juftligi: usiz
    /// ikki teng boshqaruvchidan biri ikkinchisining rolidan
    /// <c>roles.manage</c>/<c>users.manage</c> ni olib, uni o'z qo'l ostiga
    /// tushirib olardi. Bunday rolni faqat super administrator o'zgartiradi.
    /// </remarks>
    public static Error PeerRole { get; } =
        Error.Conflict(
            "rol.teng_huquqli",
            "Bu rolning huquqlari sizniki bilan teng — uni faqat super administrator o'zgartira yoki o'chira oladi.");

    /// <summary>Boshqa rolga O'ZIDA yo'q ruxsatni berishga urinish.</summary>
    /// <remarks>
    /// Aks holda <c>roles.manage</c> ruxsati bor har kim yangi rol yaratib,
    /// uni o'ziga yaqin odamga berib, o'z huquqidan kengroq ish qildira
    /// olardi — ruxsat o'z-o'zidan «barcha ruxsatlar» ga aylanardi.
    /// </remarks>
    public static Error ExceedsOwn(string permission)
        => Error.Conflict(
            "rol.ortiqcha_ruxsat",
            $"«{permission}» ruxsati sizning o'zingizda yo'q — uni boshqa rolga bera olmaysiz.");

    /// <summary>Bunday nomli boshqa rol bor.</summary>
    /// <remarks>
    /// Katta-kichik harf va chetdagi probellar farq qilmaydi: ro'yxatda
    /// «HR menejeri» va «hr menejeri» degan ikkita rol egasini
    /// chalkashtirardi va foydalanuvchiga noto'g'risi berilardi.
    /// </remarks>
    public static Error NameTaken { get; } =
        Error.Conflict("rol.nom_band", "Bunday nomli rol allaqachon bor.");

    /// <summary>Rolni biror hisob egallab turibdi — o'chirib bo'lmaydi.</summary>
    /// <remarks>
    /// Faolsiz hisoblar ham sanaladi: ular qayta faollashtirilsa, rolsiz
    /// hisob paydo bo'lardi.
    /// </remarks>
    public static Error InUse(int holders)
        => Error.Conflict(
            "rol.foydalanuvchisi_bor",
            $"Bu rol {holders} ta hisobga berilgan — avval ularga boshqa rol bering.");
}
