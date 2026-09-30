namespace Bonvi.Application.Identity.EmployeeAuth;

/// <summary>
/// Yangilash javobining qisqa xotirasi — javobi yo'lda yo'qolgan so'rov uchun.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Muammo.</b> Yangilash tokeni bir marta ishlatiladi va eskisi darhol
/// bekor qilinadi; bekor qilingan token qayta kelsa — bu o'g'irlik belgisi va
/// hisobning BARCHA sessiyalari yopiladi. Lekin telefonda eng oddiy hodisa —
/// javobning yo'lda yo'qolishi (lift, metro, Wi-Fi'dan uyali tarmoqqa o'tish):
/// server almashtirib bo'lgan, ilova esa yangi juftlikni olmagan va ESKI
/// token bilan qayta so'raydi. Ilgari bu «o'g'irlik» deb baholanardi va
/// xodim hamma qurilmada tizimdan chiqib ketardi.
/// </para>
/// <para>
/// <b>Yechim.</b> Almashtirilgan tokenning javobi <see cref="Grace"/> davomida
/// eslab qolinadi. Shu oynada, o'rniga berilgan token hali ISHLATILMAGAN
/// bo'lsa, eski token bilan kelgan takror so'rov AYNAN O'SHA juftlikni oladi
/// (idempotent): yangi token yasalmaydi, zanjir tarmoqlanmaydi. Ikki tab bir
/// vaqtda yangilasa ham shu yo'l ishlaydi — ikkalasi bitta juftlikka keladi.
/// </para>
/// <para>
/// ⚠️ Oynadan keyin yoki o'rniga berilgan token allaqachon ishlatilgan
/// bo'lsa — eski qoida (o'g'irlik) o'z kuchida.
/// </para>
/// <para>
/// ⚠️ <b>Xotirada ochiq token turadi</b> — <see cref="Grace"/> davomida va faqat
/// shu jarayonning xotirasida (bazada har doim faqat xesh). Aynan shu token
/// o'sha lahzada allaqachon javob tanasida yuborilgan; uni bir daqiqa
/// ushlab turish yangi xavf ochmaydi. Jarayon qayta ishga tushsa xotira
/// yo'qoladi — bu oynada qayta so'ragan mijoz eski qoidaga tushadi.
/// </para>
/// </remarks>
public sealed class EmployeeRefreshReplayCache
{
    /// <summary>Takror so'rov shu vaqt ichida bir xil javob oladi.</summary>
    /// <remarks>
    /// Bir daqiqa — sekin tarmoqdagi odatiy qayta urinish (brauzer 30 s
    /// kutadi, ilova yana bir marta so'raydi) uchun yetarli, o'g'irlangan
    /// tokenning «jim» ishlatilishi uchun esa tor.
    /// </remarks>
    public static readonly TimeSpan Grace = TimeSpan.FromSeconds(60);

    /// <summary>Lug'at shu hajmdan oshsa, muddati o'tganlar tozalanadi.</summary>
    private const int SweepThreshold = 2_000;

    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly Lock _sync = new();

    /// <summary>Almashtirilgan tokenning javobini eslab qoladi.</summary>
    /// <param name="replacedHash">ESKI (hozirgina almashtirilgan) tokenning xeshi.</param>
    /// <param name="successorHash">Uning o'rniga berilgan tokenning xeshi.</param>
    /// <param name="result">Mijozga yuborilgan javob.</param>
    /// <param name="now">Almashtirish lahzasi.</param>
    public void Remember(string replacedHash, string successorHash, EmployeeAuthResultDto result, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(replacedHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(successorHash);
        ArgumentNullException.ThrowIfNull(result);

        lock (_sync)
        {
            _entries[replacedHash] = new Entry(successorHash, result, now.Add(Grace));

            if (_entries.Count > SweepThreshold)
            {
                foreach (var key in _entries.Where(pair => pair.Value.ExpiresAt <= now).Select(pair => pair.Key).ToList())
                {
                    _entries.Remove(key);
                }
            }
        }
    }

    /// <summary>
    /// Almashtirilgan token uchun eslab qolingan javob — oyna ichida va o'rinbosar mos kelsa.
    /// </summary>
    /// <param name="replacedHash">Kelgan (bekor qilingan) tokenning xeshi.</param>
    /// <param name="successorHash">Bazadagi <c>replaced_by_token_hash</c> — xotiradagi bilan bir xil bo'lishi shart.</param>
    /// <param name="now">Joriy lahza.</param>
    /// <returns>Javob; oyna o'tgan yoki yozuv yo'q bo'lsa <c>null</c>.</returns>
    public EmployeeAuthResultDto? Replay(string replacedHash, string successorHash, DateTimeOffset now)
    {
        lock (_sync)
        {
            if (!_entries.TryGetValue(replacedHash, out var entry))
            {
                return null;
            }

            if (entry.ExpiresAt <= now)
            {
                _entries.Remove(replacedHash);
                return null;
            }

            return string.Equals(entry.SuccessorHash, successorHash, StringComparison.Ordinal) ? entry.Result : null;
        }
    }

    /// <summary>Eslab qolingan javob.</summary>
    private sealed record Entry(string SuccessorHash, EmployeeAuthResultDto Result, DateTimeOffset ExpiresAt);
}
