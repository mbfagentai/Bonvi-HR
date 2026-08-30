namespace Bonvi.Infrastructure.Security;

/// <summary>
/// Bazadagi sirlarni shifrlash sozlamasi (<c>Settings</c> bo'limi).
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega alohida kalit MAJBURIY emas.</b> Yangi majburiy muhit
/// o'zgaruvchisi kiritilsa, u to'ldirilmagan har bir mavjud o'rnatma
/// ishga tushmay qolardi — tizim esa bugun ham ishlab turibdi. Kalit
/// berilmasa, u <c>Jwt:SigningKey</c> dan HKDF orqali CHIQARILADI.
/// </para>
/// <para>
/// <b>Nega aynan <c>Jwt:SigningKey</c>.</b> U allaqachon MAJBURIY,
/// allaqachon kamida 32 belgi va allaqachon sir sifatida muomala
/// qilinadi (<c>JwtOptions.SigningKey</c> izohi) — ya'ni yangi sir
/// o'ylab topilmaydi, borining kafolati ishlatiladi. HKDF esa ikkalasini
/// ajratib turadi: shifrlash kaliti imzolash kalitiga TENG bo'lmaydi va
/// biridan ikkinchisini hisoblab bo'lmaydi.
/// </para>
/// <para>
/// ⚠️ <b>Kalit almashsa, saqlangan sirlar OCHILMAY qoladi.</b> Bu
/// yo'qotish emas, bezovtalik: super administrator tokenni ekranda
/// qayta qo'yadi (sozlama «kiritilmagan» bo'lib ko'rinadi). Shu sababli
/// <c>Settings:SecretKey</c> ni ataylab berish tavsiya etiladi — u
/// holda JWT kalitini xohlagancha almashtirsa bo'ladi.
/// </para>
/// </remarks>
public sealed class SecretProtectionOptions
{
    public const string SectionName = "Settings";

    /// <summary>
    /// Shifrlash kaliti. Bo'sh bo'lsa <c>Jwt:SigningKey</c> dan chiqariladi.
    /// </summary>
    /// <remarks>
    /// Uzunligi ahamiyatsiz: qiymat baribir HKDF orqali 256 bitga
    /// keltiriladi. Lekin qisqa kalit taxmin qilish oson bo'ladi —
    /// tavsiya: <c>openssl rand -base64 32</c>.
    /// </remarks>
    public string SecretKey { get; set; } = string.Empty;
}
