using Bonvi.Domain.Identity;

namespace Bonvi.Application.Identity.EmployeeAuth;

/// <summary>
/// Xodim ilovasiga kirishdagi muvaffaqiyatsiz urinishlar — <b>(login, manba IP)</b> juftligi bo'yicha.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega hisob qatorida emas.</b> Ilgari blok hisobning o'zida edi
/// (<see cref="EmployeeAccount.RecordFailedLogin"/>) va ikki muammo bor edi:
/// </para>
/// <list type="number">
///   <item>
///     <b>Hisob borligini oshkor qilardi.</b> Blok faqat MAVJUD hisobda
///     yonardi va «bloklangan» javobi paroldan OLDIN qaytardi: istalgan
///     raqamga besh marta noto'g'ri parol yuborib, oltinchisida
///     <c>xodim.bloklangan</c> olinsa — bu Bonvi xodimi. Notanish raqam
///     esa hech qachon bloklanmasdi.
///   </item>
///   <item>
///     <b>Nishonli blok.</b> 950 ta login bir-biriga ma'lum telefon raqamlari:
///     istalgan hamkasbni besh urinish bilan HAMMA JOYDA 15 daqiqaga
///     qulflab qo'yish mumkin edi — va buni har 15 daqiqada takrorlash.
///   </item>
/// </list>
/// <para>
/// Endi hisob KIRITILGAN LOGIN matni (hisob bor-yo'qligidan qat'i nazar) va
/// manba IP bo'yicha olib boriladi: notanish raqam ham xuddi shunday
/// bloklanadi (javoblar farqlanmaydi), va bloklangan — hujumchining IP'si,
/// jabrlanuvchining o'z telefoni esa ishlashda davom etadi.
/// </para>
/// <para>
/// ⚠️ <b>Blok davomida parol TEKSHIRILMAYDI.</b> Tekshirilib, to'g'ri
/// parolga boshqa javob berilsa, blok «parol to'g'rimi?» degan savolga javob
/// beradigan oracle'ga aylanardi va hech narsani to'smasdi. Bloklangan
/// juftlikka hisob bor-yo'qligidan qat'i nazar bir xil
/// <c>xodim.bloklangan</c> qaytadi — ya'ni u ham hech narsani oshkor qilmaydi.
/// </para>
/// <para>
/// Chegara va muddat <see cref="EmployeeAccount.MaxFailedAttempts"/> /
/// <see cref="EmployeeAccount.LockoutMinutes"/> dan olinadi — HR hisoblari
/// bilan bir xil son. IP bo'yicha umumiy oqimni esa tezlik cheklovi
/// (<c>RateLimitingExtensions</c>, alohida «employee-login» bo'linmasi)
/// chegaralaydi.
/// </para>
/// <para>
/// <b>Jarayon ichida</b> (<c>AttendanceIdentityCache</c> bilan bir xil
/// qaror): API bitta nusxada ishlaydi, holat esa qisqa (15 daqiqa) va
/// qayta ishga tushishda yo'qolishi zararsiz. Ikkinchi nusxa paydo bo'lsa,
/// bu sinf taqsimlangan keshga ko'chadi (<c>docs/PENDING_WIRING.md</c>).
/// </para>
/// </remarks>
public sealed class EmployeeLoginThrottle
{
    /// <summary>Lug'at shu hajmdan oshsa, eskirgan yozuvlar tozalanadi.</summary>
    private const int SweepThreshold = 10_000;

    /// <summary>
    /// Tozalashdan keyin ham shundan katta bo'lsa — lug'at butunlay bo'shatiladi.
    /// </summary>
    /// <remarks>
    /// Xotira himoyasi: minglab IP va loginni aylantirgan hujum xotirani
    /// cheksiz o'stira olmaydi. Bo'shatish «ochiq» tomonga yiqiladi (bloklar
    /// yo'qoladi), lekin IP bo'yicha tezlik cheklovi joyida qoladi.
    /// </remarks>
    private const int HardLimit = 100_000;

    private static readonly TimeSpan Lockout = TimeSpan.FromMinutes(EmployeeAccount.LockoutMinutes);

    private readonly Dictionary<(string Login, string Ip), Entry> _entries = [];
    private readonly Lock _sync = new();

    /// <summary>Juftlik shu lahzada bloklanganmi.</summary>
    /// <param name="login">Kiritilgan login (<see cref="Key"/> bilan normallashtirilgan).</param>
    /// <param name="ip">Manba IP; noma'lum bo'lsa <c>null</c>.</param>
    /// <param name="now">Joriy lahza.</param>
    /// <returns>Blok qancha qolgani; blok bo'lmasa <c>null</c>.</returns>
    public TimeSpan? LockedFor(string login, string? ip, DateTimeOffset now)
    {
        lock (_sync)
        {
            if (!_entries.TryGetValue((login, IpKey(ip)), out var entry) || entry.LockedUntil is not { } until)
            {
                return null;
            }

            return until > now ? until - now : null;
        }
    }

    /// <summary>Muvaffaqiyatsiz urinishni yozadi; chegaraga yetsa juftlik bloklanadi.</summary>
    /// <remarks>
    /// Hisoblagich blok qo'yilgan lahzada NOLGA tushadi (blok kuchaymaydi) —
    /// <see cref="EmployeeAccount.RecordFailedLogin"/> dagi ataylabki qaror.
    /// Oxirgi xatodan beri <see cref="EmployeeAccount.LockoutMinutes"/> o'tgan
    /// bo'lsa, eski xatolar hisobga olinmaydi: haftada bir marta adashgan
    /// odam oyning oxirida «bloklanib» qolmasligi kerak.
    /// </remarks>
    /// <param name="login">Kiritilgan login (<see cref="Key"/> bilan normallashtirilgan).</param>
    /// <param name="ip">Manba IP; noma'lum bo'lsa <c>null</c>.</param>
    /// <param name="now">Joriy lahza.</param>
    public void RecordFailure(string login, string? ip, DateTimeOffset now)
    {
        lock (_sync)
        {
            var key = (login, IpKey(ip));

            _entries.TryGetValue(key, out var entry);

            var failures = entry is not null && now - entry.LastFailureAt < Lockout ? entry.Failures + 1 : 1;

            _entries[key] = failures >= EmployeeAccount.MaxFailedAttempts
                ? new Entry(0, now, now.Add(Lockout))
                : new Entry(failures, now, entry?.LockedUntil);

            if (_entries.Count > SweepThreshold)
            {
                Sweep(now);
            }
        }
    }

    /// <summary>Muvaffaqiyatli kirish — juftlikning tarixi o'chadi.</summary>
    /// <param name="login">Kiritilgan login (<see cref="Key"/> bilan normallashtirilgan).</param>
    /// <param name="ip">Manba IP; noma'lum bo'lsa <c>null</c>.</param>
    public void RecordSuccess(string login, string? ip)
    {
        lock (_sync)
        {
            _entries.Remove((login, IpKey(ip)));
        }
    }

    /// <summary>
    /// Kiritilgan loginni kalitga aylantiradi: to'g'ri telefon raqami — normal
    /// ko'rinishida, aks holda qirqilgan va kichik harfli matn.
    /// </summary>
    /// <remarks>
    /// «+998 90 123 45 67» va «901234567» BITTA kalit bo'lishi kerak — aks holda
    /// har bir yozilish shakli yangi beshta urinish berardi. Noto'g'ri formatdagi
    /// login ham hisoblanadi: u ham «noto'g'ri ma'lumot» javobini oladi va
    /// javoblar ikki toifaga bo'linib qolmasligi kerak. Uzunligi cheklangan —
    /// kalit xotirada yashaydi.
    /// </remarks>
    /// <param name="normalizedPhone">To'g'ri raqamning normal ko'rinishi; format xato bo'lsa <c>null</c>.</param>
    /// <param name="raw">Mijoz yuborgan matn.</param>
    public static string Key(string? normalizedPhone, string? raw)
    {
        if (normalizedPhone is not null)
        {
            return "p:" + normalizedPhone;
        }

        var text = (raw ?? string.Empty).Trim().ToUpperInvariant();

        return "r:" + (text.Length > 64 ? text[..64] : text);
    }

    private static string IpKey(string? ip) => string.IsNullOrWhiteSpace(ip) ? "?" : ip;

    /// <summary>Muddati o'tgan yozuvlarni tozalaydi (qulf ichida chaqiriladi).</summary>
    private void Sweep(DateTimeOffset now)
    {
        var stale = _entries
            .Where(pair =>
                (pair.Value.LockedUntil is not { } until || until <= now)
                && now - pair.Value.LastFailureAt >= Lockout)
            .Select(pair => pair.Key)
            .ToList();

        foreach (var key in stale)
        {
            _entries.Remove(key);
        }

        if (_entries.Count > HardLimit)
        {
            _entries.Clear();
        }
    }

    /// <summary>Bitta juftlikning holati.</summary>
    /// <param name="Failures">Blokgacha to'plangan xatolar.</param>
    /// <param name="LastFailureAt">Oxirgi xato lahzasi.</param>
    /// <param name="LockedUntil">Blok tugashi; blok bo'lmasa <c>null</c>.</param>
    private sealed record Entry(int Failures, DateTimeOffset LastFailureAt, DateTimeOffset? LockedUntil);
}
