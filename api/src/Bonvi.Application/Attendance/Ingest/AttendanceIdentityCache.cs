using System.Collections.Concurrent;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;

namespace Bonvi.Application.Attendance.Ingest;

/// <summary>
/// «Qurilma raqami → xodim» mosliklarining jarayon ichidagi qisqa keshi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega kesh.</b> Kuniga ~5 400 o'tish ~540 ta raqam atrofida aylanadi,
/// ya'ni bir hovuch qator to'xtovsiz qayta o'qiladi (SPEC §4.1). Yozuv
/// 60 soniya yashaydi.
/// </para>
/// <para>
/// ⚠️ <b>Faqat TOPILGAN moslik keshlanadi, «topilmadi» — hech qachon.</b>
/// Bu SPEC dan ataylab chekinish va uning sababi poyga (race):
/// </para>
/// <list type="number">
///   <item>o'tish keladi, moslik yo'q — «topilmadi» o'qiladi;</item>
///   <item>shu lahzada HR moslikni yaratadi, to'ldirish (backfill) o'tadi va
///         keshdan yozuvni o'chiradi;</item>
///   <item>birinchi so'rov «topilmadi» ni keshga YOZADI va o'tishni
///         bog'lanmagan holda saqlaydi.</item>
/// </list>
/// <para>
/// To'ldirish allaqachon o'tib bo'lgan, ya'ni keyingi 60 soniyadagi HAR BIR
/// o'tish bog'lanmagan bo'lib qoladi va ularni hech narsa qayta ko'rmaydi.
/// Salbiy javob keshlanmasa, poyga oynasi mikrosoniyaga qisqaradi va faqat
/// bitta o'tishga tegadi. Narxi — bog'lanmagan raqam uchun har safar bitta
/// indeks so'rovi; bu raqamlar oz va ularni HR baribir tez bog'laydi.
/// </para>
/// <para>
/// <b>Nega <c>IMemoryCache</c> emas.</b> Application qatlami kesh
/// kutubxonasiga bog'lanmagan, bu yerda esa kerak bo'lgani — muddatli
/// lug'at va aniq kalit bo'yicha o'chirish. Jarayon bitta nusxada ishlaydi
/// (API replikasiz), shu sababli taqsimlangan kesh ham kerak emas.
/// </para>
/// </remarks>
public sealed class AttendanceIdentityCache
{
    /// <summary>Yozuvning umri.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    /// <summary>Lug'at shu hajmdan oshsa, muddati o'tganlar tozalanadi.</summary>
    /// <remarks>
    /// Moslik ~540 ta; chegara ulardan o'n barobar katta. U faqat xotira
    /// o'sib ketmasligi uchun — oddiy ishda tozalash deyarli ishga tushmaydi.
    /// </remarks>
    private const int SweepThreshold = 5_000;

    private readonly ConcurrentDictionary<(AttendanceIdentityProvider Provider, string PersonId), Entry> _entries = new();
    private readonly IClock _clock;

    public AttendanceIdentityCache(IClock clock) => _clock = clock;

    /// <summary>Keshdagi xodimni oladi.</summary>
    /// <param name="provider">Raqamlar fazosi.</param>
    /// <param name="rawPersonId">Qurilmadagi shaxs raqami.</param>
    /// <param name="employeeId">Topilgan xodim.</param>
    /// <returns>Yozuv bor va muddati o'tmagan bo'lsa — <c>true</c>.</returns>
    public bool TryGet(AttendanceIdentityProvider provider, string rawPersonId, out Guid employeeId)
    {
        employeeId = Guid.Empty;

        if (!_entries.TryGetValue((provider, rawPersonId), out var entry))
        {
            return false;
        }

        if (entry.ExpiresAt <= _clock.UtcNow)
        {
            _entries.TryRemove(new KeyValuePair<(AttendanceIdentityProvider, string), Entry>((provider, rawPersonId), entry));
            return false;
        }

        employeeId = entry.EmployeeId;
        return true;
    }

    /// <summary>Topilgan moslikni eslab qoladi.</summary>
    /// <param name="provider">Raqamlar fazosi.</param>
    /// <param name="rawPersonId">Qurilmadagi shaxs raqami.</param>
    /// <param name="employeeId">Topilgan xodim.</param>
    public void Remember(AttendanceIdentityProvider provider, string rawPersonId, Guid employeeId)
    {
        var now = _clock.UtcNow;

        if (_entries.Count >= SweepThreshold)
        {
            foreach (var pair in _entries)
            {
                if (pair.Value.ExpiresAt <= now)
                {
                    _entries.TryRemove(pair);
                }
            }
        }

        _entries[(provider, rawPersonId)] = new Entry(employeeId, now + Lifetime);
    }

    /// <summary>
    /// Yozuvni darhol unutadi — moslik yaratilganda YOKI uzilganda chaqirilishi SHART.
    /// </summary>
    /// <remarks>
    /// Uzilgan moslik keshda qolsa, keyingi 60 soniyadagi o'tishlar eski
    /// xodimga yozilardi. Bu metodni moslikni boshqaradigan buyruqlar
    /// (yaratish, o'chirish, import) SAQLASHDAN KEYIN chaqiradi.
    /// </remarks>
    /// <param name="provider">Raqamlar fazosi.</param>
    /// <param name="rawPersonId">Qurilmadagi shaxs raqami.</param>
    public void Evict(AttendanceIdentityProvider provider, string rawPersonId)
    {
        ArgumentNullException.ThrowIfNull(rawPersonId);

        _entries.TryRemove((provider, rawPersonId.Trim()), out _);
    }

    private readonly record struct Entry(Guid EmployeeId, DateTimeOffset ExpiresAt);
}

/// <summary>
/// Qurilmadagi shaxs raqamini xodimga bog'laydi — kesh, keyin baza.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ Xodimning MEHNAT HOLATIGA QARALMAYDI va bu ataylab. Ishdan ketgan
/// odamning moslik yozuvi o'chmaydi (moslik shaxsga tegishli, shartnomaga
/// emas), ya'ni uning o'tishi ham xodimga bog'lanadi. Aynan shu bog'lanish
/// jonli taxtadagi «ishdan ketgan xodim o'tdi» ogohlantirishini beradi
/// (SPEC §6.5, §8.7) — bu yerda filtr qo'yilsa, o'sha ogohlantirish
/// jimgina o'ladi va bu xavfsizlik nuqsoni bo'ladi.
/// </para>
/// </remarks>
public sealed class AttendanceIdentityResolver
{
    private readonly IAttendanceIdentityRepository _identities;
    private readonly AttendanceIdentityCache _cache;

    public AttendanceIdentityResolver(IAttendanceIdentityRepository identities, AttendanceIdentityCache cache)
    {
        _identities = identities;
        _cache = cache;
    }

    /// <summary>Xodimni topadi.</summary>
    /// <param name="provider">Qurilmaning raqamlar fazosi.</param>
    /// <param name="rawPersonId">Qurilmadagi shaxs raqami.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    /// <returns>Xodim identifikatori; moslik yo'q bo'lsa <c>null</c> — o'tish baribir saqlanadi.</returns>
    public async Task<Guid?> ResolveAsync(
        AttendanceIdentityProvider provider,
        string rawPersonId,
        CancellationToken cancellationToken)
    {
        if (_cache.TryGet(provider, rawPersonId, out var cached))
        {
            return cached;
        }

        var identity = await _identities
            .FindAsync(provider, rawPersonId, cancellationToken)
            .ConfigureAwait(false);

        if (identity is null)
        {
            return null;
        }

        _cache.Remember(provider, rawPersonId, identity.EmployeeId);
        return identity.EmployeeId;
    }
}
