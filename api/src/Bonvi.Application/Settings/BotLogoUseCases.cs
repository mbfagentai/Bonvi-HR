using Bonvi.Application.Common.Photos;
using Bonvi.Application.Settings.Common;
using Bonvi.Application.Settings.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Settings;

namespace Bonvi.Application.Settings;

/// <summary>
/// Kompaniya logotipi — botning «Kompaniya haqida» xabaridagi rasm.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega alohida use-case, oddiy sozlama emas.</b> Qolgan
/// sozlamalar — matn va son, ya'ni ularni bitta so'rovda birga saqlash
/// mumkin. Rasm esa fayl: uni tekshirish (hajm, tur, MAZMUN), qirqish,
/// saqlash va eskisini o'chirish kerak. Bu ishni umumiy «sozlamalarni
/// saqlash» so'roviga tiqish uni tanib bo'lmas darajada
/// murakkablashtirardi.
/// </para>
/// <para>
/// <b>Nega <see cref="SquarePhotoStore"/>.</b> Logotip kvadrat bo'ladi
/// va Telegram uni ham kvadrat ko'rsatadi. Xodim va hisob rasmlari
/// uchun qurilgan quvurning aynan o'zi: markazdan qirqish, 512×512
/// JPEG. Ikkinchi quvur qurish rasm tekshiruvini ikki joyda saqlash
/// degani bo'lardi.
/// </para>
/// </remarks>
public sealed record UploadBotLogoCommand(Stream Content, string? ContentType, long SizeBytes);

public sealed class UploadBotLogoCommandHandler
{
    private readonly ISystemSettingRepository _settings;
    private readonly SquarePhotoStore _photos;
    private readonly IUnitOfWork _unitOfWork;

    public UploadBotLogoCommandHandler(
        ISystemSettingRepository settings,
        SquarePhotoStore photos,
        IUnitOfWork unitOfWork)
    {
        _settings = settings;
        _photos = photos;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<BotLogoDto>> HandleAsync(
        UploadBotLogoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var accepted = await _photos
            .AcceptAsync(command.Content, command.ContentType, command.SizeBytes, cancellationToken)
            .ConfigureAwait(false);

        if (accepted.Key is not { } key)
        {
            return BotLogoErrors.Rejected(accepted.Rejection!.Value);
        }

        var existing = await _settings
            .FindAsync(SystemSettingKeys.BotLogo, cancellationToken)
            .ConfigureAwait(false);

        var previousKey = existing?.Value;

        if (existing is null)
        {
            var created = SystemSetting.CreateStored(SystemSettingKeys.BotLogo, key);
            if (created.IsFailure)
            {
                return created.Error;
            }

            _settings.Add(created.Value);
        }
        else
        {
            var applied = existing.SetStoredKey(key);
            if (applied.IsFailure)
            {
                return applied.Error;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        /*
          Eski fayl SAQLANGANDAN KEYIN o'chiriladi. Teskarisi bo'lsa,
          saqlash yiqilganda sozlama eski kalitga ishora qilib qolardi,
          fayl esa allaqachon yo'q bo'lardi — bot rasmsiz qolardi va
          buning sababi hech qayerda ko'rinmasdi.
        */
        if (!string.IsNullOrEmpty(previousKey))
        {
            await _photos.DeleteAsync(previousKey, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(new BotLogoDto(key));
    }
}

/// <summary>Logotipni olib tashlaydi.</summary>
/// <remarks>
/// IDEMPOTENT: rasm allaqachon yo'q bo'lsa ham muvaffaqiyat qaytadi —
/// so'rovning maqsadi «logotip qolmasin», u esa bajarilgan.
/// </remarks>
public sealed class DeleteBotLogoCommandHandler
{
    private readonly ISystemSettingRepository _settings;
    private readonly SquarePhotoStore _photos;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteBotLogoCommandHandler(
        ISystemSettingRepository settings,
        SquarePhotoStore photos,
        IUnitOfWork unitOfWork)
    {
        _settings = settings;
        _photos = photos;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(CancellationToken cancellationToken = default)
    {
        var existing = await _settings
            .FindAsync(SystemSettingKeys.BotLogo, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null || string.IsNullOrEmpty(existing.Value))
        {
            return Result.Success();
        }

        var key = existing.Value;

        /*
          Yozuvning O'ZI olib tashlanadi, bo'sh qiymat yozilmaydi:
          jadvalda faqat ataylab qo'yilgan sozlamalar yotishi kerak
          (`SystemSetting` izohi) va «bo'sh rasm» degan tushuncha
          yo'q — sir bilan bir xil qoida.
        */
        _settings.Remove(existing);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _photos.DeleteAsync(key, cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}

/// <param name="KnownVersion">Klientdagi nusxaning belgisi («If-None-Match»).</param>
public sealed record GetBotLogoQuery(string? KnownVersion = null);

public sealed class GetBotLogoQueryHandler
{
    private readonly SystemSettingsReader _reader;
    private readonly SquarePhotoStore _photos;

    public GetBotLogoQueryHandler(SystemSettingsReader reader, SquarePhotoStore photos)
    {
        _reader = reader;
        _photos = photos;
    }

    public async Task<Result<PhotoContent>> HandleAsync(
        GetBotLogoQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var settings = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        var key = settings.Photo(SystemSettingKeys.BotLogo);

        if (string.IsNullOrEmpty(key))
        {
            return BotLogoErrors.NotFound;
        }

        var content = await _photos.OpenAsync(key, query.KnownVersion, cancellationToken).ConfigureAwait(false);

        return content is null ? BotLogoErrors.NotFound : Result.Success(content);
    }
}
