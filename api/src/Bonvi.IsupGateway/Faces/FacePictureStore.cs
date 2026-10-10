using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Bonvi.IsupGateway.Faces;

/// <summary>
/// Terminal yuklab oladigan yuz rasmlari — bir martalik, qisqa umrli havolalar.
/// </summary>
/// <remarks>
/// <para>
/// ISUP orqali faqat matn o'tadi, shu sababli rasm terminalga
/// <c>faceURL</c> bo'lib boradi va terminal uni o'zi yuklab oladi
/// (docs/SPEC-verifikatsiya-isup.md, G3). Havola — 128 bitlik tasodifiy
/// token: taxmin qilib bo'lmaydi, ro'yxati yo'q.
/// </para>
/// <para>
/// Rasm faqat yozish so'rovi davomida yashaydi: so'rov tugashi bilan
/// (<see cref="Remove"/>) yoki eng ko'pi <c>FaceUrlTtlSeconds</c> dan keyin
/// o'chadi. Xotirada, diskda emas — konteyner o'chsa hech narsa qolmaydi.
/// </para>
/// </remarks>
internal sealed class FacePictureStore(TimeProvider time)
{
    private readonly ConcurrentDictionary<string, Entry> _pictures = new(StringComparer.Ordinal);

    public string Add(byte[] jpeg, TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(jpeg);

        Purge();

        var token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        _pictures[token] = new Entry(jpeg, time.GetUtcNow() + lifetime);

        return token;
    }

    public bool TryGet(string token, out byte[] jpeg)
    {
        if (_pictures.TryGetValue(token, out var entry) && entry.ExpiresAt > time.GetUtcNow())
        {
            jpeg = entry.Jpeg;
            return true;
        }

        jpeg = [];
        return false;
    }

    public void Remove(string token) => _pictures.TryRemove(token, out _);

    internal int Count => _pictures.Count;

    private void Purge()
    {
        var now = time.GetUtcNow();

        foreach (var (token, entry) in _pictures)
        {
            if (entry.ExpiresAt <= now)
            {
                _pictures.TryRemove(token, out _);
            }
        }
    }

    private sealed record Entry(byte[] Jpeg, DateTimeOffset ExpiresAt);
}
