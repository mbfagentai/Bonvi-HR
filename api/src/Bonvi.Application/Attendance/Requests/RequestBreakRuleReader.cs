using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Settings;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;
using Bonvi.Domain.Settings;

namespace Bonvi.Application.Attendance.Requests;

/// <summary>
/// Amaldagi tanaffus qoidasi — <c>attendance.requestBreak*</c> sozlamalaridan
/// (SPEC-verifikatsiya-sorov-tahriri §2.2).
/// </summary>
/// <remarks>
/// <para>
/// Sozlama yozilmagan bo'lsa <see cref="SystemSettingsReader"/> sukutni beradi — ya'ni
/// <see cref="RequestBreakRule.Default"/> (12:00–15:00, 60, jumada 120). Qoida YOZISH
/// lahzasida o'qiladi: jadval so'rovini yaratish va tahrirlash, HR va ilova uchun bitta joy.
/// </para>
/// <para>
/// ⚠️ Saqlangan sonlar bir-biriga zid bo'lsa (faqat bazani qo'lda tahrirlaganda — ekran buni
/// <c>davomat.tanaffus_qoidasi_nomuvofiq</c> bilan to'sadi) sukut qoida olinadi: zid qoida bilan
/// birorta tanaffus ham o'tmasdi va xodimlar so'rov yubora olmay qolardi.
/// </para>
/// </remarks>
public sealed class RequestBreakRuleReader
{
    private readonly SystemSettingsReader _settings;

    public RequestBreakRuleReader(SystemSettingsReader settings) => _settings = settings;

    /// <summary>Amaldagi qoida.</summary>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public async Task<RequestBreakRule> ReadAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settings.ReadAsync(cancellationToken).ConfigureAwait(false);

        var rule = new RequestBreakRule(
            MinuteOfDay(settings.Number(SystemSettingKeys.RequestBreakFrom)),
            MinuteOfDay(settings.Number(SystemSettingKeys.RequestBreakTo)),
            settings.Number(SystemSettingKeys.RequestBreakMinutes),
            settings.Number(SystemSettingKeys.RequestBreakFridayMaxMinutes));

        return rule.IsConsistent ? rule : RequestBreakRule.Default;
    }

    /// <summary>Kun daqiqasi → soat; chegaradan tashqari qiymat (qo'lda yozilgan) — kun ichiga qisiladi.</summary>
    private static TimeOnly MinuteOfDay(int minute)
        => TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(Math.Clamp(minute, 0, (24 * 60) - 1)));
}

/// <summary>Jadval so'rovidagi tanaffus qoidasi — HR formasi uchun.</summary>
public sealed record GetRequestBreakRuleQuery;

/// <summary><see cref="GetRequestBreakRuleQuery"/> ni bajaradi.</summary>
public sealed class GetRequestBreakRuleQueryHandler
{
    private readonly RequestBreakRuleReader _rule;

    public GetRequestBreakRuleQueryHandler(RequestBreakRuleReader rule) => _rule = rule;

    public async Task<Result<RequestBreakRuleDto>> HandleAsync(
        GetRequestBreakRuleQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return RequestBreakRuleDto.FromRule(await _rule.ReadAsync(cancellationToken).ConfigureAwait(false));
    }
}
