using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Bonvi.Domain.Abstractions;
using Bonvi.Infrastructure.Identity;
using Microsoft.Extensions.Options;

namespace Bonvi.Infrastructure.Security;

/// <summary>
/// <see cref="ISecretProtector"/> — AES-256-GCM orqali.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega GCM, oddiy AES-CBC emas.</b> GCM shifrlash bilan birga
/// BUTUNLIKNI ham tekshiradi: bazadagi qatorni qo'lda o'zgartirgan odam
/// natijani boshqa tokenga aylantira olmaydi — ochish shunchaki
/// muvaffaqiyatsiz tugaydi. CBC da esa buzilgan qiymat «ochilardi» va
/// axlat matn bo'lib chiqardi.
/// </para>
/// <para>
/// <b>Nega <c>DataProtection</c> (ASP.NET) emas.</b> U kalitlarni O'ZI
/// yaratadi va ularni diskda (yoki bazada) saqlaydi — ya'ni yana bir
/// saqlanadigan holat, uni zaxiralash va konteynerlar orasida bo'lishish
/// kerak bo'lardi. Bu yerda esa kalit muhitdan keladi va hech qayerda
/// saqlanmaydi: bot konteyneri ham, API ham bir xil <c>.env</c> ni
/// o'qiydi.
/// </para>
/// <para>
/// <b>Format:</b> <c>v1.&lt;base64url(nonce ‖ tag ‖ shifr)&gt;</c>.
/// Versiya prefiksi ataylab: kelajakda algoritm almashsa, eski
/// qiymatlarni ham o'qiy olish uchun yagona yo'l shu — prefiksisiz
/// «bu qaysi sxema?» degan savolga javob bo'lmasdi.
/// </para>
/// </remarks>
public sealed class AesGcmSecretProtector : ISecretProtector
{
    /// <summary>Joriy formatning belgisi.</summary>
    private const string Version = "v1";

    /// <summary>GCM uchun tavsiya etilgan nonce uzunligi (96 bit).</summary>
    private const int NonceSize = 12;

    /// <summary>GCM autentifikatsiya tegi (128 bit).</summary>
    private const int TagSize = 16;

    /// <summary>
    /// HKDF ning «info» maydoni — kalitni MAQSADGA bog'laydi.
    /// </summary>
    /// <remarks>
    /// Shu satr tufayli <c>Jwt:SigningKey</c> dan chiqarilgan shifrlash
    /// kaliti imzolash kalitiga teng bo'lmaydi. Uni o'zgartirish —
    /// kalitni almashtirish bilan barobar (saqlangan sirlar ochilmay
    /// qoladi), shu sababli u KONSTANTA va versiya bilan belgilangan.
    /// </remarks>
    private static readonly byte[] KeyPurpose = "bonvi-settings-secret-v1"u8.ToArray();

    private readonly byte[] _key;

    public AesGcmSecretProtector(IOptions<SecretProtectionOptions> options, IOptions<JwtOptions> jwt)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(jwt);

        var configured = options.Value.SecretKey;

        var material = string.IsNullOrWhiteSpace(configured)
            ? jwt.Value.SigningKey
            : configured;

        /*
          HKDF har qanday uzunlikdagi va har qanday sifatdagi satrdan
          256 bitli kalit yasaydi. Oddiy SHA-256 ham shu ishni qilardi,
          lekin HKDF `info` maydonini qo'llab-quvvatlaydi — aynan shu
          tufayli bitta manba satridan bir necha MAQSADGA bir-biriga
          bog'liq bo'lmagan kalitlar chiqarish mumkin.
        */
        _key = HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            Encoding.UTF8.GetBytes(material),
            outputLength: 32,
            info: KeyPurpose);
    }

    public string Protect(string plainText)
    {
        ArgumentNullException.ThrowIfNull(plainText);

        var plain = Encoding.UTF8.GetBytes(plainText);

        var payload = new byte[NonceSize + TagSize + plain.Length];

        var nonce = payload.AsSpan(0, NonceSize);
        var tag = payload.AsSpan(NonceSize, TagSize);
        var cipher = payload.AsSpan(NonceSize + TagSize);

        // Nonce HAR SAFAR yangi: bir xil token ikki marta yozilsa,
        // bazada ikki xil qator bo'ladi. Aks holda «qiymat o'zgardimi?»
        // degan savolga shifrlangan matnni solishtirib javob berish
        // mumkin bo'lib qolardi.
        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag);

        return Version + "." + Base64Url.EncodeToString(payload);
    }

    public string? Unprotect(string protectedText)
    {
        if (string.IsNullOrWhiteSpace(protectedText))
        {
            return null;
        }

        var separator = protectedText.IndexOf('.', StringComparison.Ordinal);

        if (separator <= 0
            || !protectedText.AsSpan(0, separator).SequenceEqual(Version))
        {
            // Noma'lum format: eski sxema yoki umuman sir bo'lmagan
            // qiymat. Istisno tashlanmaydi (izohi `ISecretProtector` da).
            return null;
        }

        if (!Base64Url.IsValid(protectedText.AsSpan(separator + 1)))
        {
            return null;
        }

        var payload = Base64Url.DecodeFromChars(protectedText.AsSpan(separator + 1));

        if (payload.Length < NonceSize + TagSize)
        {
            return null;
        }

        try
        {
            var nonce = payload.AsSpan(0, NonceSize);
            var tag = payload.AsSpan(NonceSize, TagSize);
            var cipher = payload.AsSpan(NonceSize + TagSize);

            var plain = new byte[cipher.Length];

            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(nonce, cipher, tag, plain);

            return Encoding.UTF8.GetString(plain);
        }
        catch (CryptographicException)
        {
            // Kalit almashgan yoki qator buzilgan — kutilgan holat.
            return null;
        }
    }
}
