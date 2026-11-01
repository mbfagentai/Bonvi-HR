using Bonvi.Application.Recruiting.Abstractions;
using System.Text.Json;
using Bonvi.Application.Recruiting.Common;
using Bonvi.Application.Recruiting.Forms.Dynamic;
using Bonvi.Application.Recruiting.Forms.Texts;
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

/// <summary>
/// HR ning HRMentor da o'zgartirgan anketa matnlari (SPEC-blankalar-hrmentor §5.1) — faqat
/// anketa matnlari; PDF blanka matnlari nomzodga yuborilmaydi.
/// </summary>
/// <param name="Revision">Reviziya — forma yuborishda qaytaradi (<c>textsRevision</c>).</param>
/// <param name="Strings">Til → kalit (<c>kiosk.*</c>) → matn; faqat HR o'zgartirganlari.</param>
/// <param name="Lists">Ro'yxat → HR ning butun ro'yxati (tartib, nomlar, yashirilganlar).</param>
public sealed record KioskTextsDto(
    int Revision,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Strings,
    IReadOnlyDictionary<string, FormTextList> Lists);

/// <summary>
/// HRMentor konstruktorida yig'ilgan, e'lon qilingan anketa (SPEC-anketa-konstruktori §5.3) — forma
/// shundan chiziladi va yuborishda <c>definitionVersion</c> ni qaytaradi.
/// </summary>
/// <param name="Version">Versiya raqami.</param>
/// <param name="Label">Bosma versiya («4.0»).</param>
/// <param name="Definition">Ta'rif (HRMentor SPEC §19.3, <c>schema: 2</c>) — matnlar uch tilda to'liq.</param>
public sealed record KioskDefinitionDto(int Version, string Label, JsonElement Definition);

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
/// <param name="Texts">
/// HR matnlari; <c>null</c> — o'rnatilgan matnlar (ulanish o'chiq, reviziya yo'q yoki konstruktor
/// anketasi amalda — so'zlar ta'rifning o'zida).
/// </param>
/// <param name="Definition">Konstruktor anketasi; <c>null</c> — dasturdagi o'rnatilgan anketa.</param>
public sealed record KioskSessionDto(
    DateOnly Today,
    IReadOnlyList<KioskVacancyDto> Vacancies,
    bool GeneralPoolAvailable,
    IReadOnlyList<KioskReferralChannelDto> ReferralChannels,
    string? TurnstileSiteKey,
    string FormVersion,
    KioskTextsDto? Texts = null,
    KioskDefinitionDto? Definition = null);

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
    private readonly FormTextsReader _texts;
    private readonly FormDefinitionsReader _definitions;

    public GetKioskSessionQueryHandler(
        IRecruitingReadModel readModel,
        IVacancyRepository vacancies,
        IReferenceRepository<ReferralChannel> referralChannels,
        SystemSettingsReader settings,
        IQuestionnaireBotCheck botCheck,
        IClock clock,
        FormTextsReader texts,
        FormDefinitionsReader definitions)
    {
        _readModel = readModel;
        _vacancies = vacancies;
        _referralChannels = referralChannels;
        _settings = settings;
        _botCheck = botCheck;
        _clock = clock;
        _texts = texts;
        _definitions = definitions;
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

        // Konstruktor anketasi amalda bo'lsa HR matnlari kerak emas — so'zlar ta'rifning o'zida (§5.3)
        var definition = await _definitions.CurrentAsync(cancellationToken).ConfigureAwait(false);
        var texts = definition is null
            ? await _texts.CurrentAsync(cancellationToken).ConfigureAwait(false)
            : FormTexts.Empty;

        return new KioskSessionDto(
            _clock.Today,
            [.. page.Items.Select(row => new KioskVacancyDto(row.Id, row.Title))],
            pool is { AcceptsCandidates: true },
            [.. channels.Select(channel => new KioskReferralChannelDto(channel.Id, channel.Name, channel.AsksDetail))],
            _botCheck.SiteKey,
            CandidateForm.HrF02Version,
            texts.Revision is { } revision ? new KioskTextsDto(revision, texts.Strings, texts.Lists) : null,
            definition is null ? null : new KioskDefinitionDto(definition.Version, definition.Label, definition.Json));
    }
}
