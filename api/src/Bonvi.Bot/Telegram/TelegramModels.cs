using System.Text.Json.Serialization;

namespace Bonvi.Bot.Telegram;

/// <summary>
/// Telegram Bot API javobining tashqi qobig'i.
/// </summary>
/// <remarks>
/// Telegram HAR DOIM 200 qaytaradi va muvaffaqiyatni tanadagi
/// <c>ok</c> maydoni bilan aytadi — shu sababli HTTP holat kodiga
/// qarab xulosa qilib bo'lmaydi.
/// </remarks>
/// <typeparam name="T">Natija turi.</typeparam>
public sealed record TelegramResponse<T>(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("result")] T? Result,
    [property: JsonPropertyName("description")] string? Description);

/// <summary>Bir dona yangilik.</summary>
/// <remarks>
/// Faqat KERAKLI maydonlar. Telegram javobi o'nlab maydonli, lekin
/// anketa boti uchun xabar matni, chat va tugma bosilishi yetarli —
/// qolganini o'qish faqat shovqin qo'shardi.
/// </remarks>
public sealed record TelegramUpdate(
    [property: JsonPropertyName("update_id")] long UpdateId,
    [property: JsonPropertyName("message")] TelegramMessage? Message,
    [property: JsonPropertyName("callback_query")] TelegramCallbackQuery? CallbackQuery);

public sealed record TelegramMessage(
    [property: JsonPropertyName("message_id")] long MessageId,
    [property: JsonPropertyName("chat")] TelegramChat Chat,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("contact")] TelegramContact? Contact);

public sealed record TelegramChat(
    [property: JsonPropertyName("id")] long Id);

/// <summary>
/// «Raqamni yuborish» tugmasi bilan kelgan kontakt.
/// </summary>
/// <remarks>
/// <see cref="UserId"/> MUHIM: u kontakt aynan YUBORUVCHINING o'ziga
/// tegishli ekanini tekshirish uchun kerak. Telegram'da boshqa odamning
/// kontaktini ham yuborib bo'ladi va tekshirmasak, nomzod o'z arizasiga
/// begona raqamni qo'yib yuborishi mumkin edi.
/// </remarks>
public sealed record TelegramContact(
    [property: JsonPropertyName("phone_number")] string PhoneNumber,
    [property: JsonPropertyName("user_id")] long? UserId);

public sealed record TelegramCallbackQuery(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("data")] string? Data,
    [property: JsonPropertyName("message")] TelegramMessage? Message);

/// <summary>Suhbat oynasi ostidagi tugmalar (inline keyboard).</summary>
public sealed record InlineButton(string Text, string CallbackData);
