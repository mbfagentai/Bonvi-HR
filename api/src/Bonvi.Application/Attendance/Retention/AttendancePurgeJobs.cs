using System.Diagnostics;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Domain.Abstractions;

namespace Bonvi.Application.Attendance.Retention;

/// <summary>
/// <c>attendance:purge-photos</c> — muddati o'tgan turniket suratini va xom hodisani o'chiradi,
/// o'tish QATORI qoladi (SPEC §3.10).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>QAYTARIB BO'LMAYDI.</b> Fayl diskdan o'chadi, <c>photo_key</c> va
/// <c>raw_event</c> <c>NULL</c> bo'ladi. «Bu men emasman» degan nizo odatda shu
/// hafta chiqadi — muddat shunga yarasha tanlanadi (sukut 60 kun).
/// </para>
/// <para>
/// <b>Nega bu ish ixtiyoriy emas.</b> Kuniga ~5 400 o'tish × ~40 KB: 60 kunda
/// ~13 GB, tozalashsiz esa yiliga ~80 GB — 2 GB xotirali VPS diskini to'ldiradi.
/// </para>
/// <para>
/// <b>Faqat qatordagi kalit bo'yicha — papka supurilmaydi.</b> O'tish surati
/// xodim rasmi va HR hujjatlari bilan BIR <c>yyyy/MM/</c> papkasida yotadi;
/// «hech bir o'tish havola qilmagan fayl» deb supurish shartnoma skanlarini
/// o'chirib yuborardi (koordinator qarori, B3).
/// </para>
/// <para>
/// <b>Avval fayl, keyin qator.</b> Fayl bilan qator orasida yiqilsa, qator
/// yo'q faylga ishora qilib qoladi va keyingi yurish uni zararsiz qayta
/// o'chiradi. Teskari tartibda fayl hech kim bilmaydigan yetimga aylanardi.
/// </para>
/// <para>
/// <c>attendance_days</c> ga TEGMAYDI: kun surat bilan hisoblanmagan.
/// </para>
/// </remarks>
public sealed class AttendancePhotoPurgeJob
{
    /// <summary>Bir to'plamdagi o'tishlar soni (SPEC §3.10).</summary>
    public const int BatchSize = 2000;

    /// <summary>Bir yurishning eng uzun vaqti — qolgani ertaga.</summary>
    public static readonly TimeSpan TimeBudget = TimeSpan.FromMinutes(5);

    private readonly AttendanceRetention _retention;
    private readonly IAttendancePassRepository _passes;
    private readonly IFileStorage _storage;
    private readonly IAttendanceJobJournal _journal;
    private readonly IClock _clock;

    public AttendancePhotoPurgeJob(
        AttendanceRetention retention,
        IAttendancePassRepository passes,
        IFileStorage storage,
        IAttendanceJobJournal journal,
        IClock clock)
    {
        _retention = retention;
        _passes = passes;
        _storage = storage;
        _journal = journal;
        _clock = clock;
    }

    /// <summary>Muddati o'tgan suratlarni tozalaydi.</summary>
    /// <param name="cancellationToken">Bekor qilish belgisi (Hangfire beradi).</param>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var policy = await _retention.ReadAsync(cancellationToken).ConfigureAwait(false);

        // Toshkent kuni boshidagi lahza: «60 kundan eski» kalendar kuni bo'yicha,
        // yurish soatiga qarab surilmaydi.
        var receivedBefore = PurgeFiles.StartOfTashkentDay(_clock.Today.AddDays(-policy.PhotoRetentionDays));

        var started = Stopwatch.GetTimestamp();
        var failed = new HashSet<Guid>();
        var purged = 0;
        var completed = false;

        while (Stopwatch.GetElapsedTime(started) < TimeBudget)
        {
            var batch = await _passes
                .ListExpiredPhotosAsync(receivedBefore, BatchSize, cancellationToken)
                .ConfigureAwait(false);

            if (batch.Count == 0)
            {
                completed = true;
                break;
            }

            var removed = await PurgeFiles
                .DeleteAsync(_storage, batch, failed, cancellationToken)
                .ConfigureAwait(false);

            if (removed.Count == 0)
            {
                // To'plamda faqat o'chmayotgan fayllar qoldi — aylanib yurish ma'nosiz.
                break;
            }

            purged += await _passes
                .MarkPhotosPurgedAsync(removed, _clock.UtcNow, cancellationToken)
                .ConfigureAwait(false);
        }

        _journal.PhotoPurgeFinished(
            new PhotoPurgeReport(policy.PhotoRetentionDays, receivedBefore, purged, failed.Count, completed));
    }
}

/// <summary>
/// <c>attendance:purge-passes</c> — muddati o'tgan o'tish QATORINI surati bilan birga o'chiradi (SPEC §9.2.1).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>QAYTARIB BO'LMAYDI.</b> Qator bazadan butunlay o'chadi (yumshoq emas),
/// fayl diskdan o'chadi. «O'sha kuni 08:03 da aynan shu odam o'tgan» degan
/// dalil yo'qoladi.
/// </para>
/// <para>
/// ⚠️⚠️ <b>ATTENDANCE_DAYS GA TEGILMAYDI. HECH QANDAY KUN QAYTA HISOBLASHGA
/// BELGILANMAYDI.</b> Tabel, ishlangan soat, kechikish — hosila yozuv va u
/// abadiy qoladi; o'chadigani faqat xom dalil. O'tish o'chirilgach uning kuni
/// qayta hisoblansa, kun OMON QOLGAN o'tishlardan quriladi va bir yil oldingi
/// tabel jimgina NOLGA aylanadi — bu modulda qilish mumkin bo'lgan eng og'ir
/// xato. Shu sababli: (1) bu sinf kun jadvaliga umuman murojaat qilmaydi va
/// <c>processed_at</c> ga tegmaydi; (2) birinchi qatorni o'chirishdan OLDIN
/// <c>attendance.frozenThrough</c> chegarasi ko'tariladi va yagona yozuvchi
/// (<c>AttendanceDayRecomputer</c>) o'sha sanagacha bo'lgan kunlarni boshqa
/// hech qachon qayta hisoblamaydi.
/// </para>
/// <para>
/// <b><c>0</c> — «hech qachon»</b> va bu metoddagi BIRINCHI tekshiruv: tartib
/// keyinchalik tasodifan o'zgartirilsa ham, u o'chirishga aylanib qolmasin.
/// </para>
/// <para>
/// <b>Sana chegarasi <c>local_date</c> bo'yicha</b>, SPEC dagi <c>received_at</c>
/// emas — ikki sabab: (1) <c>ix_attendance_passes_local_date</c> indeksi bor,
/// <c>received_at</c> da esa (suratsiz qatorlar uchun) yo'q — har to'plam
/// ~2 mln qatorli jadvalni to'liq ko'zdan kechirardi; (2) kun aynan
/// <c>local_date</c> bo'yicha quriladi, ya'ni muzlatish chegarasi ham aniq
/// shu sana bo'ladi, soat og'ishi (30 kungacha) uni noaniq qilmaydi.
/// </para>
/// <para>
/// <b>Bog'langan-u hisobga olinmagan o'tish o'chirilmaydi</b> (<c>processed_at IS NULL</c>):
/// bunchalik eski kutayotgan o'tish — buzilgan navbat belgisi, o'chirish uni
/// yashirardi; ular sanalib, ogohlantirish yoziladi.
/// </para>
/// <para>
/// <b>Bog'lanmagan o'tish esa O'CHIRILADI</b> — AMENDED 2026-09-17 (egasi xom
/// yozuvlar uchun bir yilni tanlagan). U hech qachon tabelga qo'shila olmaydi:
/// sanasi o'chirishdan oldin muzlatiladi, ya'ni keyin kiritilgan moslik ham uni
/// qayta hisoblamaydi. Qoldirish jadvalni mehmon va sinov kartalari, hech kim
/// moslamaydigan raqamlar bilan cheksiz o'stirardi. Ro'yxat bilan o'chirish
/// orasida to'ldirish uni bog'lab qo'ysa — o'chirish shartni qayta tekshiradi
/// va qatorni qoldiradi (<c>IAttendancePassRepository.DeleteExpiredAsync</c> izohi).
/// </para>
/// <para>
/// Muddat — <c>max(pass, photo)</c>: o'tish suratidan oldin ketmaydi. Surat
/// muddati hali o'tmagan bo'lsa ham fayl qatordan OLDIN o'chiriladi — qator
/// ketgach kalitni hech narsa bilmaydi. <see cref="AttendancePhotoPurgeJob"/>
/// dan keyin (03:45) yuradi, ya'ni odatda fayli allaqachon yo'q.
/// </para>
/// </remarks>
public sealed class AttendancePassPurgeJob
{
    /// <summary>Bir to'plamdagi o'tishlar soni (SPEC §9.2.1).</summary>
    public const int BatchSize = 2000;

    /// <summary>Bir yurishning eng uzun vaqti — qolgani ertaga.</summary>
    /// <remarks>
    /// Yetuk bazada birinchi yurish bir necha kecha davom etishi mumkin — bu normal.
    /// </remarks>
    public static readonly TimeSpan TimeBudget = TimeSpan.FromMinutes(5);

    private readonly AttendanceRetention _retention;
    private readonly IAttendancePassRepository _passes;
    private readonly IFileStorage _storage;
    private readonly IAttendanceJobJournal _journal;
    private readonly IClock _clock;

    public AttendancePassPurgeJob(
        AttendanceRetention retention,
        IAttendancePassRepository passes,
        IFileStorage storage,
        IAttendanceJobJournal journal,
        IClock clock)
    {
        _retention = retention;
        _passes = passes;
        _storage = storage;
        _journal = journal;
        _clock = clock;
    }

    /// <summary>Muddati o'tgan o'tishlarni o'chiradi.</summary>
    /// <param name="cancellationToken">Bekor qilish belgisi (Hangfire beradi).</param>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var policy = await _retention.ReadAsync(cancellationToken).ConfigureAwait(false);

        // ⚠️ BIRINCHI tekshiruv — «hech qachon o'chirilmasin». Undan oldin hech narsa turmasin.
        if (policy.KeepsPassesForever)
        {
            _journal.PassPurgeDisabled();
            return;
        }

        var retentionDays = policy.EffectivePassRetentionDays;
        var localDateBefore = _clock.Today.AddDays(-retentionDays);

        var kept = await _passes
            .CountExpiredPendingAsync(localDateBefore, cancellationToken)
            .ConfigureAwait(false);

        if (kept > 0)
        {
            _journal.ExpiredPendingPassesKept(localDateBefore, kept);
        }

        var started = Stopwatch.GetTimestamp();
        var failed = new HashSet<Guid>();
        var frozen = false;
        int deletedPasses = 0, deletedFiles = 0;
        var completed = false;

        while (Stopwatch.GetElapsedTime(started) < TimeBudget)
        {
            var batch = await _passes
                .ListExpiredPassesAsync(localDateBefore, BatchSize, cancellationToken)
                .ConfigureAwait(false);

            if (batch.Count == 0)
            {
                completed = true;
                break;
            }

            if (!frozen)
            {
                // ⚠️ Birinchi o'chirishdan OLDIN va alohida saqlanadi (sinf izohi).
                await _retention.FreezeThroughAsync(localDateBefore, cancellationToken).ConfigureAwait(false);
                frozen = true;
            }

            var photos = batch.Where(pass => pass.PhotoKey is not null).ToList();

            var photosRemoved = await PurgeFiles
                .DeleteAsync(_storage, photos, failed, cancellationToken)
                .ConfigureAwait(false);

            // Fayli o'chmagan qator QOLADI — u ketsa, fayl yetim bo'lardi.
            List<Guid> deletable =
            [
                .. batch.Where(pass => pass.PhotoKey is null).Select(pass => pass.Id),
                .. photosRemoved,
            ];

            if (deletable.Count == 0)
            {
                break;
            }

            deletedFiles += photosRemoved.Count;
            deletedPasses += await _passes.DeleteExpiredAsync(deletable, cancellationToken).ConfigureAwait(false);
        }

        _journal.PassPurgeFinished(
            new PassPurgeReport(retentionDays, localDateBefore, deletedPasses, deletedFiles, failed.Count, completed));
    }
}

/// <summary>Ikkala tozalash vazifasining fayl qadami — bir xil qoida bilan.</summary>
internal static class PurgeFiles
{
    /// <summary>Toshkent kalendar kunining boshlanish lahzasi — UTC da.</summary>
    /// <remarks>
    /// UTC ga ATAYLAB keltiriladi: Npgsql <c>timestamptz</c> parametriga faqat
    /// nol siljishli qiymatni yozadi va +05:00 li lahza so'rovni yiqitadi.
    /// </remarks>
    /// <param name="date">Toshkent sanasi.</param>
    public static DateTimeOffset StartOfTashkentDay(DateOnly date)
        => new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), BonviTime.TashkentOffset).ToUniversalTime();

    /// <summary>Fayllarni o'chiradi va fayli haqiqatan yo'q bo'lgan o'tishlarni qaytaradi.</summary>
    /// <remarks>
    /// <para>
    /// Fayl allaqachon yo'q bo'lsa — muvaffaqiyat: maqsad «diskda qolmasin».
    /// </para>
    /// <para>
    /// O'chmagan fayl (ruxsat, disk xatosi) butun ishni to'xtatmaydi: uning
    /// qatori tegilmaydi va <paramref name="failed"/> ga yoziladi, shu yurishda
    /// qayta urinilmaydi. Aks holda zaxiradan noto'g'ri egasi bilan tiklangan
    /// bitta fayl har kecha butun tozalashni yiqitib, diskni to'ldirardi.
    /// </para>
    /// </remarks>
    /// <param name="storage">Fayl saqlash porti.</param>
    /// <param name="passes">Surati bor o'tishlar.</param>
    /// <param name="failed">Shu yurishda o'chmagan o'tishlar — to'ldirib boriladi.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public static async Task<List<Guid>> DeleteAsync(
        IFileStorage storage,
        IReadOnlyCollection<ExpiredPass> passes,
        HashSet<Guid> failed,
        CancellationToken cancellationToken)
    {
        var removed = new List<Guid>(passes.Count);

        foreach (var pass in passes)
        {
            if (pass.PhotoKey is not { } key || failed.Contains(pass.Id))
            {
                continue;
            }

            try
            {
                await storage.DeleteAsync(key, cancellationToken).ConfigureAwait(false);
                removed.Add(pass.Id);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                failed.Add(pass.Id);
            }
        }

        return removed;
    }
}
