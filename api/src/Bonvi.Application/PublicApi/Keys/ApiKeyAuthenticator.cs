using Bonvi.Application.PublicApi.Abstractions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.PublicApi;

namespace Bonvi.Application.PublicApi.Keys;

/// <summary>
/// Autentifikatsiyadan o'tgan kalit — ommaviy so'rov egasining kimligi.
/// </summary>
/// <remarks>
/// Bu «foydalanuvchi» EMAS: unda na ruxsatlar, na xodim bog'lanishi bor
/// va u hech qanday ichki endpoint'ga kira olmaydi. Faqat uchta qiymat
/// bor, va har biri ishlatiladi: <paramref name="Id"/> — «oxirgi
/// ishlatilgan» belgisi uchun, <paramref name="Name"/> —
/// <c>/ping</c> javobi uchun, <paramref name="Prefix"/> — tezlik
/// cheklovining bo'linmasi va loglar uchun.
/// </remarks>
public sealed record ApiKeyIdentity(Guid Id, string Name, string Prefix);

/// <summary>
/// <c>X-API-Key</c> sarlavhasini tekshiradi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tekshiruv tartibi va uning sababi:</b>
/// </para>
/// <list type="number">
///   <item><b>Format</b> — <c>ApiKeyFormat.TryRead</c>. Shakli buzuq matn
///         bazaga umuman bormaydi: aks holda har qanday tasodifiy satr
///         bitta indeks so'rovi bo'lardi va ommaviy yuza arzon
///         «so'rov bombasi» ga aylanardi;</item>
///   <item><b>Prefiks bo'yicha izlash</b> — ochiq, qisqa va unikal
///         ustun;</item>
///   <item><b>Xeshni vaqt bo'yicha barqaror taqqoslash</b> — topilmagan
///         prefiks ham AYNI ishni bajaradi (sababi
///         <c>ApiKeyFormat.Matches</c> izohida);</item>
///   <item><b>Bekor qilinganlikni tekshirish</b> — eng OXIRIDA. Avval
///         bajarilsa, bekor qilingan kalit sirni tekshirmasdan rad
///         etilardi va javob vaqti «bu prefiks bor» degan ma'lumotni
///         berardi.</item>
/// </list>
/// <para>
/// Barcha muvaffaqiyatsiz yo'llar <c>null</c> qaytaradi — chaqiruvchi
/// ularni <b>bitta</b> xato bilan javob qiladi
/// (<c>PublicApiErrors.InvalidKeyCode</c>).
/// </para>
/// <para>
/// ⚠️ <b>Kalit hech qayerda log qilinmaydi.</b> Bu sinfda birorta ham
/// <c>ILogger</c> yo'q va bo'lmasligi ham kerak: yagona log qilishga
/// arzigulik qiymat — prefiks, u esa tezlik cheklovi yo'lida allaqachon
/// mavjud.
/// </para>
/// </remarks>
public sealed class ApiKeyAuthenticator
{
    private readonly IApiKeyRepository _keys;
    private readonly IApiKeyUsageTracker _usage;

    public ApiKeyAuthenticator(IApiKeyRepository keys, IApiKeyUsageTracker usage)
    {
        _keys = keys;
        _usage = usage;
    }

    /// <summary>
    /// Sarlavha qiymatini tekshiradi. <c>null</c> — kalit yaroqsiz.
    /// </summary>
    /// <param name="rawKey"><c>X-API-Key</c> sarlavhasining qiymati.</param>
    /// <param name="sourceIp">So'rov kelgan IP — «oxirgi ishlatilgan» yozuvi uchun.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public async Task<ApiKeyIdentity?> AuthenticateAsync(
        string? rawKey,
        string? sourceIp,
        CancellationToken cancellationToken = default)
    {
        if (!ApiKeyFormat.TryRead(rawKey, out var prefix, out var hash))
        {
            return null;
        }

        var key = await _keys.FindByPrefixAsync(prefix, cancellationToken).ConfigureAwait(false);

        // Taqqoslash topilmagan holatda HAM bajariladi — vaqt bo'yicha
        // tenglashtirish uchun (izohi `ApiKeyFormat.Matches` da).
        if (!ApiKeyFormat.Matches(key?.KeyHash, hash) || key is null || key.IsRevoked)
        {
            return null;
        }

        // Yozuv so'rovni KUTMAYDI va yiqilsa ham o'qishga tegmaydi.
        _usage.Schedule(key.Id, sourceIp);

        return new ApiKeyIdentity(key.Id, key.Name, key.KeyPrefix);
    }
}
