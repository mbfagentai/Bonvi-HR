using Bonvi.Application.Abstractions;

namespace Bonvi.Infrastructure.Services;

/// <summary>
/// <see cref="IDataChangeSignal"/> — xotiradagi hisoblagich va bitta
/// kutish nuqtasi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega <see cref="TaskCompletionSource"/>, oddiy <c>Task.Delay</c>
/// halqasi emas.</b> Halqa bilan kutuvchi o'zgarishni faqat keyingi
/// aylanishda sezardi — ya'ni «jonli» bildirishnoma o'rtacha yarim
/// aylanishga kechikardi. Bu yerda esa uyg'otish DARHOL bo'ladi.
/// </para>
/// <para>
/// <b>Nega har uyg'otishda YANGI manba yasaladi.</b>
/// <c>TaskCompletionSource</c> bir martalik: bajarilgach uni qayta
/// ishlatib bo'lmaydi. Shu sababli <see cref="Notify"/> eskisini
/// bajaradi va o'rniga yangisini qo'yadi — keyingi kutuvchilar
/// o'shanga ulanadi.
/// </para>
/// <para>
/// <c>RunContinuationsAsynchronously</c> — MAJBURIY. Usiz kutayotgan
/// oqimlarning davomi <see cref="Notify"/> ni chaqirgan oqimda,
/// qulf ostida bajarilardi: ya'ni bazaga yozayotgan so'rov o'nlab
/// ulangan brauzerga javob yuborishni O'Z ustiga olardi va o'zi
/// sekinlashardi.
/// </para>
/// </remarks>
public sealed class DataChangeSignal : IDataChangeSignal
{
    private readonly object _gate = new();

    private long _version;
    private TaskCompletionSource _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public long Version
    {
        get
        {
            lock (_gate)
            {
                return _version;
            }
        }
    }

    public void Notify()
    {
        TaskCompletionSource waiting;

        lock (_gate)
        {
            _version++;
            waiting = _changed;
            _changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        // Qulfdan TASHQARIDA: davomlar asinxron bo'lsa ham, bajarish
        // amalining o'zi qulfni ushlab turishning hojati yo'q.
        waiting.TrySetResult();
    }

    public async Task<long> WaitAsync(long known, TimeSpan timeout, CancellationToken cancellationToken)
    {
        Task waiting;

        lock (_gate)
        {
            // Chaqiruvchi ORQADA qolgan bo'lishi mumkin: o'zgarish u
            // kutishni boshlagunicha sodir bo'lgan. Bunda umuman
            // kutilmaydi — aks holda signal YO'QOLARDI va keyingisiga
            // qadar (yoki muddat tugagunicha) osilib qolardi.
            if (_version != known)
            {
                return _version;
            }

            waiting = _changed.Task;
        }

        try
        {
            await waiting.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // Muddat tugadi — bu XATO EMAS, kutilgan holat: chaqiruvchi
            // «tirikman» belgisini yuborib, yana kutishga qaytadi.
        }

        return Version;
    }
}
