using Bonvi.Application.Settings.Common;
using Bonvi.Application.Settings.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Settings;

namespace Bonvi.Application.Settings;

/// <summary>Bitta sozlamaning yangi qiymati.</summary>
/// <param name="Key">Kalit — <c>SystemSettingKeys</c> dagi qiymat.</param>
/// <param name="Value">
/// Yangi qiymat, MATN ko'rinishida (son ham matn bo'lib keladi).
/// <para>
/// Nega son ham matn: so'rov tanasi bitta ro'yxat va undagi yozuvlar
/// har xil turda. JSON'da ularni bitta maydonga sig'dirish uchun yo
/// <c>object</c>, yo matn kerak. <c>object</c> bo'lganda «7» va 7
/// bir-biridan farq qilmay qolardi va tekshiruv ikki xil yo'ldan
/// ketardi.
/// </para>
/// <para>
/// SIR turidagi sozlamada (<c>bot.token</c>) bu maydon OCHIQ token
/// bo'lib keladi va serverda shifrlanib yoziladi. BO'SH satr esa
/// «o'chirish» degani — yozuvning o'zi olib tashlanadi
/// (<c>SystemSettingKind.Secret</c> izohi).
/// </para>
/// </param>
public sealed record SystemSettingUpdate(string? Key, string? Value);

/// <summary>
/// Bir nechta sozlamani BIRDAN yozadi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega bittalab emas.</b> Sozlamalar ekrani bitta forma: HR uchta
/// chegarani ham o'zgartirib, «Saqlash» ni bosadi. Har biri alohida
/// so'rov bo'lganda ikkinchisi yiqilsa, birinchisi allaqachon yozilgan
/// bo'lardi va tizim YARIM o'zgartirilgan holatda qolardi.
/// </para>
/// <para>
/// ⭐ <b>Hammasi yoki hech narsa.</b> Avval BARCHA yozuvlar tekshiriladi,
/// keyingina biror narsa o'zgartiriladi. Tekshiruv va yozish
/// aralashtirilsa, ro'yxatning yarmi saqlanib, uchinchisida xato
/// chiqishi mumkin edi — va foydalanuvchi xato xabarini ko'rib, aslida
/// nima saqlanganini bilmasdi.
/// </para>
/// </remarks>
/// <param name="Items">O'zgartiriladigan sozlamalar.</param>
public sealed record UpdateSettingsCommand(IReadOnlyList<SystemSettingUpdate>? Items);

public sealed class UpdateSettingsCommandHandler
{
    private readonly ISystemSettingRepository _settings;
    private readonly SystemSettingsReader _reader;
    private readonly ISecretProtector _protector;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateSettingsCommandHandler(
        ISystemSettingRepository settings,
        SystemSettingsReader reader,
        ISecretProtector protector,
        IUnitOfWork unitOfWork)
    {
        _settings = settings;
        _reader = reader;
        _protector = protector;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IReadOnlyList<SystemSettingDto>>> HandleAsync(
        UpdateSettingsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var prepared = Prepare(command.Items);
        if (prepared.IsFailure)
        {
            return prepared.Error;
        }

        foreach (var (key, value, clearSecret) in prepared.Value)
        {
            var existing = await _settings.FindAsync(key, cancellationToken).ConfigureAwait(false);

            /*
              SIRNI O'CHIRISH — yagona holat, unda yozuv YO'Q qilinadi.

              Matnni bo'shatish (bot matnini olib tashlash) esa bo'sh
              satr YOZADI va qator joyida qoladi: matnda «bo'sh» —
              haqiqiy, ataylab tanlangan qiymat. Sirda esa bo'sh qiymat
              degan narsa yo'q, u yo bor yo yo'q — shu sababli
              «o'chirilgan sir» va «hech qachon qo'yilmagan sir» bazada
              ham bir xil ko'rinishi kerak. Aks holda bo'sh shifrlangan
              qator qolib, `IsSet` yolg'on gapirib qolardi.
            */
            if (clearSecret)
            {
                if (existing is not null)
                {
                    _settings.Remove(existing);
                }

                continue;
            }

            if (existing is null)
            {
                // Yozuv BIRINCHI marta qiymat berilganda paydo bo'ladi:
                // jadvalda faqat ataylab o'zgartirilgan sozlamalar yotadi
                // (`SystemSetting` izohi).
                var created = SystemSetting.Create(key, value, _protector);
                if (created.IsFailure)
                {
                    // Bu yerga yetib bo'lmaydi: kalit va qiymat
                    // `Prepare` da AYNAN shu qoidalar bo'yicha
                    // tekshirilgan. Shunga qaramay natija e'tibordan
                    // qoldirilmaydi — `Result` ni tashlab yuborish shu
                    // loyihada rad etilgan naqsh.
                    return created.Error;
                }

                _settings.Add(created.Value);
                continue;
            }

            var updated = existing.SetValue(value, _protector);
            if (updated.IsFailure)
            {
                return updated.Error;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Keshni tozalash SHART: `_reader` shu so'rovda allaqachon
        // o'qilgan bo'lishi mumkin va u eski qiymatlarni tutib turardi.
        _reader.Forget();

        // Javobda TO'LIQ ro'yxat qaytadi, faqat o'zgarganlar emas: ekran
        // shu javob bilan butunlay yangilanadi va ikkinchi so'rov
        // yubormaydi. Yon ta'siri ham foydali — agar kimdir boshqa
        // oynada boshqa sozlamani o'zgartirgan bo'lsa, u ham ko'rinadi.
        var settings = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(settings.Describe());
    }

    /// <summary>
    /// So'rovni tekshiradi va tozalangan (kalit, qiymat) juftliklarini qaytaradi.
    /// </summary>
    /// <remarks>
    /// Bu yerda BAZAGA umuman murojaat qilinmaydi: hamma qoida
    /// <see cref="SystemSettingKeys"/> ta'rifidan kelib chiqadi. Shu
    /// sababli xato so'rovda bitta ham <c>SELECT</c> yuborilmaydi.
    /// </remarks>
    private static Result<IReadOnlyList<(string Key, string Value, bool ClearSecret)>> Prepare(
        IReadOnlyList<SystemSettingUpdate>? items)
    {
        if (items is null || items.Count == 0)
        {
            return SettingsErrors.NothingToUpdate;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var prepared = new List<(string Key, string Value, bool ClearSecret)>(items.Count);

        foreach (var item in items)
        {
            if (item is null)
            {
                return SystemSettingErrors.EmptyKey;
            }

            if (SystemSettingKeys.Find(item.Key) is not { } definition)
            {
                // Bo'sh kalit va noma'lum kalit — ikki xil xato: birinchisi
                // so'rov noto'g'ri yig'ilganini, ikkinchisi esa mijoz
                // tizimda YO'Q sozlamani bilishini bildiradi (masalan
                // frontend yangiroq versiyadan).
                return string.IsNullOrWhiteSpace(item.Key)
                    ? SystemSettingErrors.EmptyKey
                    : SystemSettingErrors.UnknownKey;
            }

            if (!seen.Add(definition.Key))
            {
                return SettingsErrors.DuplicateKey;
            }

            // Tur, chegara va uzunlik — hammasi ta'rifning O'ZIDA
            // tekshiriladi (`SystemSettingDefinition.Normalize`).
            // Bu yerda ikkinchi nusxa yozilmaydi.
            var normalized = definition.Normalize(item.Value);
            if (normalized.IsFailure)
            {
                return normalized.Error;
            }

            prepared.Add((
                definition.Key,
                normalized.Value,
                ClearSecret: definition.Kind == SystemSettingKind.Secret
                    && normalized.Value.Length == 0));
        }

        return Result.Success<IReadOnlyList<(string Key, string Value, bool ClearSecret)>>(prepared);
    }
}
