using Bonvi.Application.Recruiting.Abstractions;
using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Application.Settings;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.Settings;

namespace Bonvi.Application.Recruiting.Candidates;

/// <summary>
/// Shu odam yangi anketa topshira oladimi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nima uchun bor</b> (2026-09-08, foydalanuvchi talabi): «битта
/// номердан битта анкета». Bitta telefon raqami bilan ikki kun ichida
/// ikkita anketa tushgan va ikkalasi ham rad etilgan edi.
/// </para>
/// <para>
/// ⭐ <b>Nega qoida SERVERDA, botda emas.</b> Ilgari to'siqni bot
/// o'zi yozardi: qidiruv so'roviga <c>status=InProgress</c> qo'shib.
/// Endi qaror sozlamadagi muddatga tayanadi va bot uni takrorlaganda
/// HR ekranda muddatni o'zgartirgach, bot eski qoida bilan ishlab
/// qolardi. Bot endi faqat SAVOL beradi.
/// </para>
/// <para>
/// <b>Ikkita to'siq, shu tartibda.</b> Jarayondagi ariza muddatdan
/// QAT'I NAZAR to'sadi: ikkinchi yozuv ochilsa, HR bir odamni ikki
/// marta ko'rardi. Muddat esa faqat YOPILGAN arizadan keyin
/// hisoblanadi.
/// </para>
/// <para>
/// ⚠️ Qoida FAQAT botga tegishli. HR qo'lda nomzod kiritganda hech
/// qanday to'siq yo'q — u odamni ko'rib turibdi va qarorni o'zi
/// qabul qiladi.
/// </para>
/// </remarks>
/// <param name="Phone">Telegram bergan raqam. Ajratgichlar bo'lishi mumkin.</param>
/// <param name="TelegramChatId">
/// Suhbat raqami — IKKINCHI kalit.
///
/// ⭐ Faqat telefon bo'yicha tekshirish yetmaydi: bir odamda ikkita SIM
/// bo'lishi mumkin va u O'SHA Telegram hisobidan yangi raqam bilan
/// qaytib kelardi. Ikkala kalit birga tekshirilgani uchun bunday
/// aylanma yo'l yopiq.
/// </param>
public sealed record CheckApplicationQuery(string? Phone, long? TelegramChatId = null);

public sealed class CheckApplicationQueryHandler
{
    private readonly IRecruitingReadModel _readModel;
    private readonly SystemSettingsReader _settings;
    private readonly IClock _clock;

    public CheckApplicationQueryHandler(
        IRecruitingReadModel readModel,
        SystemSettingsReader settings,
        IClock clock)
    {
        _readModel = readModel;
        _settings = settings;
        _clock = clock;
    }

    public async Task<Result<ApplicationCheckDto>> HandleAsync(
        CheckApplicationQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var digits = PhoneDigits(query.Phone);

        var latest = await _readModel
            .FindLatestApplicationAsync(digits, query.TelegramChatId, cancellationToken)
            .ConfigureAwait(false);

        if (latest is null)
        {
            return ApplicationCheckDto.Allow();
        }

        // ── 1. Jarayondagi ariza — muddatdan qat'i nazar ──
        if (latest.Status == CandidateStatus.InProgress)
        {
            return ApplicationCheckDto.Blocked(
                ApplicationBlockReason.Active,
                latest.AppliedAt,
                latest.VacancyTitle,
                latest.StageName);
        }

        // ── 2. Qayta ariza muddati ──
        var settings = await _settings.ReadAsync(cancellationToken).ConfigureAwait(false);
        var months = settings.Number(SystemSettingKeys.ReapplyMonths);

        /*
          Hisob `ClosedAt` dan ketadi (izohi `CandidateApplicationRow`
          da). U bo'sh bo'lishi NAZARIY holat — yopilgan nomzodda sana
          har doim bor. Baribir tayanch qiymat qoldirilgan: bo'sh
          sanada `AddMonths` ni chaqirib bo'lmaydi va tekshiruv butun
          anketani to'xtatib qo'yardi.
        */
        var from = latest.ClosedAt ?? DateOnly.FromDateTime(latest.AppliedAt.UtcDateTime);
        var availableFrom = from.AddMonths(months);

        return _clock.Today < availableFrom
            ? ApplicationCheckDto.Blocked(
                ApplicationBlockReason.Cooldown,
                latest.AppliedAt,
                latest.VacancyTitle,
                latest.StageName,
                availableFrom)
            : ApplicationCheckDto.Allow();
    }

    /// <summary>
    /// Raqamdan faqat SONLARNI qoldiradi.
    /// </summary>
    /// <remarks>
    /// Telegram raqamni «+998901234567» ko'rinishida beradi, HR esa
    /// qo'lda «+998 90 123-45-67» deb yozadi. Ikkalasi bir xil odam va
    /// solishtirish shuning uchun faqat raqamlar bo'yicha ketadi —
    /// bazadagi ifoda ham aynan shu (<c>CandidateSearchQuery.WithPhone</c>).
    /// </remarks>
    private static string PhoneDigits(string? phone)
        => phone is null ? string.Empty : new string([.. phone.Where(char.IsAsciiDigit)]);
}
