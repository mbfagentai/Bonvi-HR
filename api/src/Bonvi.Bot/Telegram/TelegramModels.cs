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
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("error_code")] int? ErrorCode = null);

/// <summary>
/// Xabar yuborish natijasi — xabarnomalar (rad etish, suhbat taklifi) uchun.
/// </summary>
public enum TelegramDelivery
{
    /// <summary>Telegram xabarni qabul qildi.</summary>
    Delivered = 1,

    /// <summary>
    /// Telegram ANIQ rad etdi (4xx: nomzod botni bloklagan, chat yo'q) —
    /// qayta urinish natijani o'zgartirmaydi.
    /// </summary>
    Rejected = 2,

    /// <summary>
    /// Telegram'ga yetib bo'lmadi yoki u vaqtincha javob bermadi (tarmoq, 429,
    /// 5xx) — xabar KETMAGAN, keyinroq yuborish mumkin.
    /// </summary>
    Unreachable = 3,
}

/// <summary>Bir dona yangilik.</summary>
/// <remarks>
/// Faqat KERAKLI maydonlar. Telegram javobi o'nlab maydonli, lekin
/// anketa boti uchun xabar matni, chat va tugma bosilishi yetarli —
/// qolganini o'qish faqat shovqin qo'shardi.
/// </remarks>
public sealed record TelegramUpdate(
    [property: JsonPropertyName("update_id")] long UpdateId,
    [property: JsonPropertyName("message")] TelegramMessage? Message,
    [property: JsonPropertyName("callback_query")] TelegramCallbackQuery? CallbackQuery,
    [property: JsonPropertyName("my_chat_member")] TelegramChatMemberUpdate? MyChatMember = null);

/// <summary>The bot's own membership changed: it was added to a chat or removed from one.</summary>
public sealed record TelegramChatMemberUpdate(
    [property: JsonPropertyName("chat")] TelegramChat Chat);

public sealed record TelegramMessage(
    [property: JsonPropertyName("message_id")] long MessageId,
    [property: JsonPropertyName("chat")] TelegramChat Chat,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("contact")] TelegramContact? Contact,
    [property: JsonPropertyName("photo")] IReadOnlyList<TelegramPhotoSize>? Photo = null,
    [property: JsonPropertyName("from")] TelegramUser? From = null);

/// <summary>The sender. The username can match a name written on the online form, the id identifies a council member.</summary>
public sealed record TelegramUser(
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("id")] long? Id = null);

/// <summary>
/// Yuborilgan rasmning BITTA o'lchamdagi ko'rinishi.
/// </summary>
/// <remarks>
/// <para>
/// Telegram har bir rasmni bir necha o'lchamda saqlaydi va xabarda
/// ularning HAMMASINI yuboradi — kichigidan kattasiga. Ya'ni «rasm»
/// aslida ro'yxat va undan bittasini tanlash kerak
/// (<see cref="TelegramPhotos.BestFileId"/>).
/// </para>
/// <para>
/// <c>file_size</c> BO'LMASLIGI mumkin (Telegram uni har doim ham
/// bermaydi) — shu sababli u <c>null</c> bo'la oladi va tanlashda
/// «noma'lum» degan ma'noni tashiydi.
/// </para>
/// </remarks>
/// <param name="FileId">Faylning belgisi — yuklab olish shu bilan.</param>
/// <param name="Width">Eni.</param>
/// <param name="Height">Bo'yi.</param>
/// <param name="FileSize">Hajmi baytda — noma'lum bo'lsa <c>null</c>.</param>
public sealed record TelegramPhotoSize(
    [property: JsonPropertyName("file_id")] string FileId,
    [property: JsonPropertyName("width")] int Width,
    [property: JsonPropertyName("height")] int Height,
    [property: JsonPropertyName("file_size")] long? FileSize = null);

/// <summary>
/// Yuborilgan rasmlardan keraklisini tanlaydi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>ENG KATTASI, lekin chegara ichida.</b> Server rasmni o'zi
/// 512×512 ga qirqadi, ya'ni sifat qanchalik yuqori bo'lsa natija
/// shunchalik yaxshi. Lekin serverning hajm chegarasi bor (10 MB) va
/// undan katta fayl 400 bilan qaytardi — nomzod uchun bu «surat
/// yuklanmadi» degani bo'lardi.
/// </para>
/// <para>
/// <b>Tartibga ishonilmaydi.</b> Telegram ro'yxatni o'sish tartibida
/// beradi, lekin bu hujjatda KAFOLAT qilinmagan xatti-harakat —
/// piksellar soni bo'yicha solishtirish esa har qanday tartibda
/// ishlaydi.
/// </para>
/// </remarks>
public static class TelegramPhotos
{
    /// <summary>
    /// Yuklab olinadigan rasmning belgisi.
    /// </summary>
    /// <param name="sizes">Xabardagi barcha o'lchamlar.</param>
    /// <param name="maxBytes">Server qabul qiladigan eng katta hajm.</param>
    /// <returns>Tanlangan <c>file_id</c>; rasm bo'lmasa <c>null</c>.</returns>
    public static string? BestFileId(IReadOnlyList<TelegramPhotoSize>? sizes, long maxBytes)
    {
        if (sizes is not { Count: > 0 })
        {
            return null;
        }

        var ordered = sizes.OrderByDescending(size => (long)size.Width * size.Height).ToList();

        var fitting = ordered.Find(size => size.FileSize is null || size.FileSize <= maxBytes);

        /*
          Hech biri sig'masa ENG KICHIGI olinadi: u chegaradan o'tish
          ehtimoli eng yuqori nusxa. O'tmasa ham ariza yo'qolmaydi —
          rasm arizadan muhimroq emas.
        */
        return (fitting ?? ordered[^1]).FileId;
    }
}

public sealed record TelegramChat(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("type")] string? Type = null,
    [property: JsonPropertyName("title")] string? Title = null)
{
    /// <summary>A one-to-one chat. Telegram always sends the type; group ids are negative when it is absent.</summary>
    public bool IsPrivate => Type is null ? Id > 0 : Type == "private";
}

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
/// <summary>
/// Xabar ostidagi tugma — yo AMAL, yo HAVOLA.
/// </summary>
/// <remarks>
/// <para>
/// Ikkalasi bitta turda: Telegram uchun ham ular bitta ro'yxatning
/// elementlari va faqat maydoni bilan farq qiladi
/// (<c>callback_data</c> yoki <c>url</c>). Ikkita alohida tur qilish
/// tugmalar qatorini «u yoki bu» ro'yxatiga aylantirardi, holbuki
/// amaliy farq bitta maydonda.
/// </para>
/// <para>
/// Havolali tugma bosilganda botga HECH NARSA kelmaydi — Telegram uni
/// o'zi ochadi. Shu sababli <see cref="CallbackData"/> unda bo'sh.
/// </para>
/// </remarks>
/// <param name="Text">Tugmadagi yozuv.</param>
/// <param name="CallbackData">Bosilganda botga keladigan belgi.</param>
/// <param name="Url">Ochiladigan havola. Berilsa, tugma HAVOLA bo'ladi.</param>
/// <param name="WebAppUrl">A Mini App page, opened with signed launch data.</param>
public sealed record InlineButton(string Text, string? CallbackData = null, string? Url = null, string? WebAppUrl = null)
{
    /// <summary>Opens a Mini App with signed launch data. Telegram allows it only in a private chat.</summary>
    public static InlineButton WebApp(string text, string url) => new(text, WebAppUrl: url);

    /// <summary>Havolali tugma.</summary>
    public static InlineButton Link(string text, string url) => new(text, Url: url);
}
