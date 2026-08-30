using Bonvi.Application.Settings.Dtos;
using Bonvi.Domain.Common;
using Bonvi.Domain.Settings;

namespace Bonvi.Application.Settings;

/// <summary>
/// Barcha tizim sozlamalari — joriy qiymati va ta'rifi bilan.
/// </summary>
/// <remarks>
/// <para>
/// Parametrsiz: sozlamalar oltita va ular BIRGA ko'rsatiladi. Filtr
/// yoki sahifalash ma'nosiz bo'lardi — ro'yxat kodda belgilangan va
/// hech qachon o'smaydi.
/// </para>
/// <para>
/// <b>Javobda YOZILMAGAN sozlamalar ham bor.</b> Ular bazada yo'q,
/// lekin ekranda ko'rinishi SHART: aks holda tizim endi o'rnatilganda
/// sozlamalar sahifasi bo'sh chiqardi va foydalanuvchi umuman nima
/// sozlash mumkinligini bilmasdi.
/// </para>
/// </remarks>
public sealed record GetSettingsQuery;

public sealed class GetSettingsQueryHandler
{
    private readonly SystemSettingsReader _reader;

    public GetSettingsQueryHandler(SystemSettingsReader reader) => _reader = reader;

    public async Task<Result<IReadOnlyList<SystemSettingDto>>> HandleAsync(
        GetSettingsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var settings = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(settings.Describe());
    }
}

/// <summary>
/// Telegram bot o'qiydigan sozlamalar (<c>bot.</c> bilan boshlanadiganlar).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega ALOHIDA use-case, garchi qiymatlar umumiy ro'yxatda ham
/// bo'lsa.</b> Sabab RUXSAT: bot hisobida atigi ikkita ruxsat bor
/// (<c>recruiting.view</c> va <c>recruiting.manage</c>) va
/// <c>settings.manage</c> unda YO'Q — u faqat super administratorda
/// (<c>Permissions.SettingsManage</c> izohi). Botga <c>settings.manage</c>
/// berish esa uni butun tizim sozlamalarini O'ZGARTIRA oladigan
/// hisobga aylantirardi; bot tokeni esa Telegram serverlariga
/// chiqadigan va sizib ketishi mumkin bo'lgan yagona sir
/// (<c>SystemRoles.RecruitingBot</c> izohi).
/// </para>
/// <para>
/// Ikkinchi sabab — MAZMUN. Botga bildirishnoma chegaralari umuman
/// kerak emas va ular unga ko'rinmasligi ham kerak: har bir hisob
/// faqat o'z ishi uchun zarur ma'lumotni oladi.
/// </para>
/// </remarks>
public sealed record GetBotSettingsQuery;

public sealed class GetBotSettingsQueryHandler
{
    private readonly SystemSettingsReader _reader;

    public GetBotSettingsQueryHandler(SystemSettingsReader reader) => _reader = reader;

    public async Task<Result<BotSettingsDto>> HandleAsync(
        GetBotSettingsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var settings = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);

        // Tiplashgan kirish: kalit → qiymat o'girish shu yerda tugaydi
        // va botga tayyor uchta maydon boradi (sababi `BotSettingsDto` da).
        return Result.Success(new BotSettingsDto(
            settings.Text(SystemSettingKeys.BotAboutCyrillic),
            settings.Text(SystemSettingKeys.BotAboutLatin),
            settings.Text(SystemSettingKeys.BotAboutRussian),
            settings.Text(SystemSettingKeys.BotContactsCyrillic),
            settings.Text(SystemSettingKeys.BotContactsLatin),
            settings.Text(SystemSettingKeys.BotContactsRussian),
            settings.Text(SystemSettingKeys.BotLocation),
            settings.Photo(SystemSettingKeys.BotLogo).Length > 0,
            settings.Text(SystemSettingKeys.BotSocialInstagram),
            settings.Text(SystemSettingKeys.BotSocialTelegram),
            settings.Text(SystemSettingKeys.BotSocialFacebook),
            settings.Text(SystemSettingKeys.BotSocialWebsite),
            settings.Number(SystemSettingKeys.BotConversationTimeoutMinutes)));
    }
}

/// <summary>
/// Botning Telegram tokeni — bot jarayoni uchun.
/// </summary>
/// <remarks>
/// ALOHIDA use-case va alohida endpoint: sabab
/// <see cref="Dtos.BotTokenDto"/> izohida. Bu yagona joy, undan sir
/// ochiq holda chiqadi — shu sababli u boshqa hech qanday sozlama
/// bilan bir javobga QO'SHILMAYDI: bir kun kimdir <c>/bot</c> ning
/// ruxsatini kengaytirsa, token u bilan birga ketmasin.
/// </remarks>
public sealed record GetBotTokenQuery;

public sealed class GetBotTokenQueryHandler
{
    private readonly SystemSettingsReader _reader;

    public GetBotTokenQueryHandler(SystemSettingsReader reader) => _reader = reader;

    public async Task<Result<BotTokenDto>> HandleAsync(
        GetBotTokenQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var settings = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);

        // Qo'yilmagan token — XATO EMAS (404 ham emas): bu normal holat
        // va bot uni ko'rib `.env` dagi zaxiraga tushadi.
        return Result.Success(new BotTokenDto(settings.Secret(SystemSettingKeys.BotToken)));
    }
}
