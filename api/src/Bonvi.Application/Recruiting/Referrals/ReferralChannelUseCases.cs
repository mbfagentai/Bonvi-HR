using Bonvi.Application.Recruiting.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Catalog;
using Bonvi.Domain.Common;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Application.Recruiting.Referrals;

/// <summary>
/// «Qayerdan bilgan» kanali — tanlash ro'yxati uchun (HR formasi, nomzodlar filtri, bot).
/// </summary>
/// <remarks>
/// Ma'lumotnoma DTO'sidan (<c>ReferralChannelDto</c>) farqi — izoh YO'Q: bu ro'yxatni
/// bot ham o'qiydi, HR ning ichki izohi («nega faolsizlantirildi») unga kerak emas.
/// </remarks>
/// <param name="Id">Kanal.</param>
/// <param name="Name">Nom — ma'lumotnomadagidek.</param>
/// <param name="IsActive">Faolmi — faol emasi yangi tanlovda ko'rsatilmaydi, filtrda esa bor.</param>
/// <param name="AsksDetail">Tanlanganda aniqlik so'raladimi.</param>
public sealed record ReferralChannelOptionDto(Guid Id, string Name, bool IsActive, bool AsksDetail);

/// <summary>Kanallar ro'yxatini so'rash.</summary>
/// <param name="IncludeInactive">
/// <c>true</c> — faolsizlantirilganlari ham (nomzodlar ro'yxatining filtri: eski nomzodlar
/// o'sha kanal bilan qolgan). Sukut — faqat faollari (yangi tanlov).
/// </param>
public sealed record ListReferralChannelOptionsQuery(bool IncludeInactive = false);

/// <summary>
/// Kanallar ro'yxati — REKRUTING ruxsati bilan (SPEC-qayerdan-bilgan §5).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega ma'lumotnoma endpointi yetmaydi.</b> U <c>catalog.view</c> bilan ochiladi, bu
/// ro'yxat esa nomzod kiritadigan HAR KIMGA kerak: bot hisobida faqat
/// <c>recruiting.view/manage</c> bor (sababi <c>SystemRoles.RecruitingBot</c> izohida) va
/// rekruter ham ma'lumotnomalarni ko'rmasligi mumkin. Botga <c>catalog.view</c> berish uning
/// tor huquqlarini kengaytirardi — shu sababli ro'yxat rekruting guruhida, o'z DTO'si bilan.
/// </para>
/// <para>
/// Tartib alifbo — repozitoriydan kelgandek (<c>name</c> ustunidagi o'zbek ICU kolatsiyasi);
/// bu yerda qayta saralanmaydi.
/// </para>
/// </remarks>
public sealed class ListReferralChannelOptionsQueryHandler
{
    private readonly IReferenceRepository<ReferralChannel> _channels;

    public ListReferralChannelOptionsQueryHandler(IReferenceRepository<ReferralChannel> channels)
        => _channels = channels;

    public async Task<Result<IReadOnlyList<ReferralChannelOptionDto>>> HandleAsync(
        ListReferralChannelOptionsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var channels = await _channels
            .ListAsync(onlyActive: !query.IncludeInactive, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<ReferralChannelOptionDto> options =
        [
            .. channels.Select(channel => new ReferralChannelOptionDto(
                channel.Id,
                channel.Name,
                channel.IsActive,
                channel.AsksDetail)),
        ];

        return Result.Success(options);
    }
}

/// <summary>
/// Nomzodning «qayerdan bilgan» tanlovini tekshiradi va qo'yadi — ma'lumotnomaga qarab.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega alohida xizmat.</b> Qoida UCH joyda kerak — HR ning «Nomzod qo'shish» va tahriri
/// (shu xizmat orqali), bot arizasi (o'sha «Nomzod qo'shish» yo'li) va onlayn anketa (u
/// faol kanallar ro'yxatini tekshiruvchiga beradi). Nomzod agregati ma'lumotnomani ko'rmaydi,
/// shu sababli «kanal faolmi» va «aniqlik so'raladimi» shu yerda; nomzodning o'z qoidalari
/// (kanal bo'sh emas, aniqlik ≤ 200) — <see cref="Candidate.SetReferral"/> da.
/// </para>
/// <para>
/// <b>Aniqlik faqat so'raydigan kanalda saqlanadi.</b> YANGI tanlovda kanal aniqlik
/// so'ramasa, kelgan matn tashlanadi: forma «Tanishlar orqali — Rustamov» dan «Telegram» ga
/// o'tganda eski matnni yuborib qolsa, kartada «Telegram — Rustamov» degan yolg'on qator
/// chiqardi.
/// </para>
/// </remarks>
public sealed class ReferralChannelChoice
{
    private readonly IReferenceRepository<ReferralChannel> _channels;

    public ReferralChannelChoice(IReferenceRepository<ReferralChannel> channels) => _channels = channels;

    /// <summary>
    /// YANGI tanlov: yangi nomzod yoki kanalni almashtirish. Kanal majburiy va FAOL bo'lishi
    /// shart; kanal aniqlik so'rasa — aniqlik ham.
    /// </summary>
    /// <param name="candidate">Nomzod (saqlanmagan bo'lishi ham mumkin).</param>
    /// <param name="channelId">Tanlangan kanal; bo'sh — <c>qayerdan.majburiy</c>.</param>
    /// <param name="detail">Aniqlik.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public async Task<Result> ApplyNewAsync(
        Candidate candidate,
        Guid? channelId,
        string? detail,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (channelId is not { } id || id == Guid.Empty)
        {
            return Candidate.ReferralRequired;
        }

        var channel = await _channels.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        if (channel is not { IsActive: true })
        {
            return RecruitingErrors.ReferralChannelNotFound;
        }

        if (!channel.AsksDetail)
        {
            return candidate.SetReferral(channel.Id, detail: null);
        }

        return string.IsNullOrWhiteSpace(detail)
            ? RecruitingErrors.ReferralDetailRequired
            : candidate.SetReferral(channel.Id, detail);
    }

    /// <summary>
    /// HR TAHRIRI (SPEC §5, «HR edit»).
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item>Kanal berilmagan: kanalsiz ESKI nomzod shundayligicha qoladi (HR boshqa
    ///         maydonni tuzatyapti); kanali bor nomzodni tozalab bo'lmaydi —
    ///         <c>qayerdan.majburiy</c>.</item>
    ///   <item>Boshqa kanal — yangi tanlov (<see cref="ApplyNewAsync"/>): faol bo'lishi shart.</item>
    ///   <item>O'sha kanal — faolsizlantirilgan bo'lsa ham saqlanadi. Aniqlik kelganidek
    ///         yoziladi; kanal aniqlik so'rasa va nomzodda aniqlik BOR bo'lsa, uni bo'shatib
    ///         bo'lmaydi. Aniqliksiz eski yozuv (bot anketasidan ko'chgan «Tanishlar
    ///         orqali») esa shundayligicha saqlanadi.</item>
    /// </list>
    /// </remarks>
    /// <param name="candidate">Tahrirlanayotgan nomzod.</param>
    /// <param name="channelId">So'rovdagi kanal.</param>
    /// <param name="detail">So'rovdagi aniqlik.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public async Task<Result> ApplyEditAsync(
        Candidate candidate,
        Guid? channelId,
        string? detail,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (channelId is not { } id || id == Guid.Empty)
        {
            return candidate.ReferralChannelId is null
                ? Result.Success()
                : Candidate.ReferralRequired;
        }

        if (id != candidate.ReferralChannelId)
        {
            return await ApplyNewAsync(candidate, id, detail, cancellationToken).ConfigureAwait(false);
        }

        var channel = await _channels.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        // Tashqi kalit kanalni o'chirishga yo'l qo'ymaydi — bu faqat buzilgan baza holati
        if (channel is null)
        {
            return RecruitingErrors.ReferralChannelNotFound;
        }

        if (channel.AsksDetail && candidate.ReferralDetail is not null && string.IsNullOrWhiteSpace(detail))
        {
            return RecruitingErrors.ReferralDetailRequired;
        }

        return candidate.SetReferral(channel.Id, detail);
    }

    /// <summary>
    /// Kanal nomi va aniqlik bitta qatorda: «Tanishlar orqali — Rustamov Bahodir».
    /// </summary>
    /// <remarks>
    /// Kartadagi, Excel eksportidagi va xodim kartasiga ko'chadigan matndagi shakl bitta
    /// (SPEC §6). Kanalsiz nomzodda <c>null</c>.
    /// </remarks>
    public static string? Label(string? channelName, string? detail)
        => channelName is null
            ? null
            : string.IsNullOrWhiteSpace(detail) ? channelName : $"{channelName} — {detail.Trim()}";
}
