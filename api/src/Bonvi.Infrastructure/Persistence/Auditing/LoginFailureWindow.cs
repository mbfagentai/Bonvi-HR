using Microsoft.Extensions.Caching.Memory;

namespace Bonvi.Infrastructure.Persistence.Auditing;

/// <summary>
/// Bir xil muvaffaqiyatsiz kirish (manba IP + login + sabab) <see cref="Window"/> ichida audit
/// jurnaliga faqat BIR MARTA yoziladi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega.</b> Audit jurnali faqat qo'shiladi va uni DDL'siz tozalab bo'lmaydi (trigger).
/// Ilgari notanish login hech narsa yozmasdi; endi har urinish qator bo'lsa, tarqoq parol
/// tanlash (IP bo'yicha 5/daqiqa) disk'ni kuniga gigabaytlab to'ldirardi. Birinchi qator
/// «kim, qayerdan, qaysi loginni urindi» ga javob beradi; takrorining sanog'i esa xavfsizlik
/// uchun kerakli ma'lumot emas (bloklar baribir ishlaydi).
/// </para>
/// <para>
/// Jarayon xotirasida, hajmi cheklangan: to'lib qolsa yangi kalit saqlanmaydi va qator
/// yoziladi — ya'ni «ko'proq yozish» tomonga yiqiladi, iz yo'qolmaydi. Qayta ishga
/// tushishda yana bittadan yoziladi — zararsiz. <c>PermissionDenied</c> dagi 10 daqiqalik
/// oyna bilan bir xil qaror (<c>AuthenticationExtensions</c>).
/// </para>
/// </remarks>
public sealed class LoginFailureWindow : IDisposable
{
    /// <summary>Takror yozilmaydigan oyna.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private readonly MemoryCache _seen = new(new MemoryCacheOptions { SizeLimit = 50_000 });

    /// <summary>Shu kalit oynada birinchi marta ko'rinyaptimi (ha bo'lsa — eslab qoladi).</summary>
    /// <param name="key">Manba IP, login va sababdan yig'ilgan kalit.</param>
    public bool FirstInWindow(string key)
    {
        if (_seen.TryGetValue(key, out _))
        {
            return false;
        }

        _seen.Set(key, true, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = Window,
            Size = 1,
        });

        return true;
    }

    /// <inheritdoc />
    public void Dispose() => _seen.Dispose();
}
