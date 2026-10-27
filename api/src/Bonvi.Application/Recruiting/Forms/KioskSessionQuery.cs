using Bonvi.Application.Recruiting.Abstractions;
using Bonvi.Application.Recruiting.Common;
using Bonvi.Application.Settings;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Catalog;
using Bonvi.Domain.Common;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Application.Recruiting.Forms;

/// <summary>Onlayn anketadagi vakansiya tanlovi — faqat nomi.</summary>
/// <param name="Id">Vakansiya.</param>
/// <param name="Title">E'lon nomi.</param>
public sealed record KioskVacancyDto(Guid Id, string Title);

/// <summary>«Qayerdan bildingiz?» tanlovi — onlayn anketa uchun (SPEC-qayerdan-bilgan §5).</summary>
/// <param name="Id">Kanal — javobdagi <c>referralChannelId</c>.</param>
/// <param name="Name">Nom — ma'lumotnomadagidek.</param>
/// <param name="AsksDetail">Tanlanganda aniqlik («kim / qaysi manba») so'raladimi.</param>
public sealed record KioskReferralChannelDto(Guid Id, string Name, bool AsksDetail);

/// <summary>Onlayn anketa ochilganda oladigan hamma narsa.</summary>
/// <param name="Today">Toshkent bo'yicha bugun — sana chegaralari (tug'ilgan sana, ishga chiqish) shundan.</param>
/// <param name="Vacancies">Nomzod qabul qilayotgan vakansiyalar, «Umumiy anketa» siz, nomi bo'yicha.</param>
/// <param name="GeneralPoolAvailable">«Umumiy (istalgan lavozim)» tanlovi ko'rsatilsinmi.</param>
/// <param name="ReferralChannels">«Qayerdan bildingiz?» — FAOL kanallar, alifbo tartibida.</param>
/// <param name="TurnstileSiteKey">
/// Bot tekshiruvi (Cloudflare Turnstile) vidjetining OCHIQ kaliti (SPEC §2.3.2); <c>null</c> —
/// tekshiruv o'chiq, forma vidjetni chizmaydi va token yubormaydi.
/// </param>
/// <param name="FormVersion">
/// Serverdagi blanka versiyasi. Forma o'zinikidan farq qilsa, bosh ekranda sahifani bir marta
/// yangilaydi — deploy paytida ochiq qolgan planshet yangi formani o'zi oladi.
/// </param>
public sealed record KioskSessionDto(
    DateOnly Today,
    IReadOnlyList<KioskVacancyDto> Vacancies,
    bool GeneralPoolAvailable,
    IReadOnlyList<KioskReferralChannelDto> ReferralChannels,
    string? TurnstileSiteKey,
    string FormVersion);

/// <summary>Onlayn anketa seansi — ochiq (login va kalitsiz).</summary>
public sealed record GetKioskSessionQuery;

/// <remarks>
/// <para>
/// Javobda nomzodlar haqida HECH NARSA yo'q — faqat ochiq vakansiyalar NOMI
/// (ular baribir e'lon qilingan), «qayerdan bilgan» kanallari (nom va bayroq, izohsiz) va
/// bot tekshiruvining OCHIQ kaliti (u baribir sahifa kodida ko'rinadi; maxfiy kalit — hech qachon).
/// Endpoint OCHIQ: havolani kim ochsa, shu ko'radi.
/// </para>
/// <para>
/// Ro'yxat chegarasi 500: ochiq vakansiyalar o'nlab, chegara faqat buzilgan
/// holatda javob cheksiz o'smasligi uchun.
/// </para>
/// </remarks>
public sealed class GetKioskSessionQueryHandler
{
    private const int MaxVacancies = 500;

    private readonly IRecruitingReadModel _readModel;
    private readonly IVacancyRepository _vacancies;
    private readonly IReferenceRepository<ReferralChannel> _referralChannels;
    private readonly SystemSettingsReader _settings;
    private readonly IQuestionnaireBotCheck _botCheck;
    private readonly IClock _clock;

    public GetKioskSessionQueryHandler(
        IRecruitingReadModel readModel,
        IVacancyRepository vacancies,
        IReferenceRepository<ReferralChannel> referralChannels,
        SystemSettingsReader settings,
        IQuestionnaireBotCheck botCheck,
        IClock clock)
    {
        _readModel = readModel;
        _vacancies = vacancies;
        _referralChannels = referralChannels;
        _settings = settings;
        _botCheck = botCheck;
        _clock = clock;
    }

    public async Task<Result<KioskSessionDto>> HandleAsync(
        GetKioskSessionQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var poolId = await GeneralPoolVacancy.IdAsync(_settings, cancellationToken).ConfigureAwait(false);

        var page = await _readModel
            .ListVacanciesAsync(
                new VacancyListFilter(
                    Status: VacancyStatus.Open,
                    PageSize: MaxVacancies,
                    Sort: VacancySortField.Title,
                    Descending: false,
                    ExcludedVacancyId: poolId),
                cancellationToken)
            .ConfigureAwait(false);

        var pool = poolId is { } id
            ? await _vacancies.GetByIdAsync(id, cancellationToken).ConfigureAwait(false)
            : null;

        var channels = await _referralChannels.ListAsync(onlyActive: true, cancellationToken).ConfigureAwait(false);

        return new KioskSessionDto(
            _clock.Today,
            [.. page.Items.Select(row => new KioskVacancyDto(row.Id, row.Title))],
            pool is { AcceptsCandidates: true },
            [.. channels.Select(channel => new KioskReferralChannelDto(channel.Id, channel.Name, channel.AsksDetail))],
            _botCheck.SiteKey,
            CandidateForm.HrF02Version);
    }
}
