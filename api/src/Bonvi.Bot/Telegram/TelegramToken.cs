namespace Bonvi.Bot.Telegram;

/// <summary>
/// Botning JORIY Telegram tokeni — bitta o'zgaruvchan yacheyka.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega token endi shunchaki sozlama emas.</b> Ilgari u
/// <c>.env</c> dan bir marta o'qilib, <c>HttpClient.BaseAddress</c>
/// ichiga qotirib qo'yilardi — ya'ni tokenni almashtirish uchun
/// konteynerni qayta ishga tushirish SHART edi. Endi token tizim
/// sozlamalarida turadi va super administrator uni ekrandan
/// o'zgartiradi (sabab <c>SystemSettingKeys.BotToken</c> izohida),
/// ya'ni u ish paytida almashishi mumkin bo'lib qoldi.
/// </para>
/// <para>
/// <b>Nega alohida sinf, <c>BotWorker</c> ichidagi maydon emas.</b>
/// Tokenni O'QIYDIGAN joy — <see cref="TelegramClient"/>, uni
/// BELGILAYDIGAN joy — <c>BotWorker</c>. Ikkalasi mustaqil ravishda
/// yasaladi va biri ikkinchisini bilmaydi; ular orasida bitta
/// umumiy yacheyka turibdi.
/// </para>
/// <para>
/// <b>Nega <c>volatile</c>.</b> Yozuvchi va o'quvchi — bitta jarayonning
/// turli nuqtalari, hech qanday qulf orasida emas. Bu yerda kerakli
/// kafolat eng oddiysi: yozilgan qiymat KEYINGI o'qishda ko'rinsin.
/// Qiymat <c>string</c> (o'zgarmas tur), ya'ni yarim yozilgan holat
/// bo'lishi mumkin emas — qulf ortiqcha bo'lardi.
/// </para>
/// </remarks>
public sealed class TelegramToken
{
    private volatile string _value = string.Empty;

    /// <summary>Joriy token. Hech qachon <c>null</c> emas — bo'sh satr bo'lishi mumkin.</summary>
    public string Value
    {
        get => _value;
        set => _value = value ?? string.Empty;
    }

    /// <summary>Token umuman qo'yilganmi.</summary>
    public bool IsSet => _value.Length > 0;
}
