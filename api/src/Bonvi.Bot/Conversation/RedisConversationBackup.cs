using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Bonvi.Bot.Conversation;

/// <summary>
/// Suhbat holatining Redis'dagi nusxasi — bot qayta ishga tushganda
/// yarim to'ldirilgan anketa yo'qolmasligi uchun.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega <see cref="IDistributedCache"/>.</b> API keshi ham aynan shu
/// abstraksiya orqali ishlaydi va paket versiyasi markazda bitta —
/// botga Redis uchun ikkinchi mijoz kutubxonasi qo'shilmadi. Kalit
/// prefiksi (<c>InstanceName</c>) API kalitlaridan ajratib turadi.
/// </para>
/// <para>
/// <b>Sanoqlar MATN bo'lib yoziladi</b> (<see cref="JsonStringEnumConverter"/>).
/// Nusxa bir deploy'dan ikkinchisiga o'tadi: raqam bilan saqlanganda
/// sanoqqa yangi qiymat qo'shilishi eski nusxani jimgina BOSHQA javobga
/// aylantirardi. Nomi o'zgarsa esa nusxa o'qilmaydi va nomzod anketani
/// boshidan boshlaydi — bu noto'g'ri javobdan ko'ra yaxshi.
/// </para>
/// <para>
/// <b>Redis ishlamasa — 30 soniya unga murojaat QILINMAYDI.</b> Mijoz
/// uzilgan ulanishda buyruqni navbatga qo'yib, kutish muddati tugaguncha
/// ushlab turadi. Bot yangiliklarni birma-bir ishlaydi, ya'ni har qadamda
/// shu kutish HAMMA nomzodlarga qo'shilib borardi. Birinchi xatodan
/// keyin nusxa bir muddat chetlab o'tiladi va suhbat xotirada davom etadi.
/// </para>
/// </remarks>
public sealed partial class RedisConversationBackup : IConversationBackup
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Xatodan keyin Redis qancha vaqt chetlab o'tiladi.</summary>
    private static readonly TimeSpan Pause = TimeSpan.FromSeconds(30);

    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisConversationBackup> _logger;
    private readonly TimeProvider _time;
    private long _skipUntilTicks;

    public RedisConversationBackup(
        IDistributedCache cache,
        ILogger<RedisConversationBackup> logger,
        TimeProvider? time = null)
    {
        _cache = cache;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Redis hozir chetlab o'tilyaptimi (yaqinda xato bergan).</summary>
    private bool Paused => _time.GetUtcNow().UtcTicks < Interlocked.Read(ref _skipUntilTicks);

    /// <inheritdoc />
    public ConversationState? Load(long chatId)
    {
        if (Paused)
        {
            return null;
        }

        try
        {
            var bytes = _cache.Get(Key(chatId));

            return bytes is null ? null : JsonSerializer.Deserialize<ConversationState>(bytes, Json);
        }
        catch (JsonException ex)
        {
            // Nusxa buzilgan — Redis emas: uni chetlab o'tishga sabab yo'q
            LogFailed(ex, "o'qish");
            return null;
        }
        catch (Exception ex)
        {
            Fail(ex, "o'qish");
            return null;
        }
    }

    /// <inheritdoc />
    public void Store(long chatId, ConversationState state, TimeSpan lifetime)
    {
        if (Paused)
        {
            return;
        }

        try
        {
            _cache.Set(
                Key(chatId),
                JsonSerializer.SerializeToUtf8Bytes(state, Json),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = lifetime });
        }
        catch (Exception ex)
        {
            Fail(ex, "yozish");
        }
    }

    /// <inheritdoc />
    public void Remove(long chatId)
    {
        if (Paused)
        {
            return;
        }

        try
        {
            _cache.Remove(Key(chatId));
        }
        catch (Exception ex)
        {
            Fail(ex, "o'chirish");
        }
    }

    private void Fail(Exception ex, string action)
    {
        Interlocked.Exchange(ref _skipUntilTicks, (_time.GetUtcNow() + Pause).UtcTicks);
        LogFailed(ex, action);
    }

    private static string Key(long chatId) => "conversation:" + chatId.ToString(System.Globalization.CultureInfo.InvariantCulture);

    [LoggerMessage(
        EventId = 3301,
        Level = LogLevel.Warning,
        Message = "Suhbat nusxasini Redis'da {Action} bajarilmadi — suhbat xotirada davom etadi")]
    private partial void LogFailed(Exception ex, string action);
}
