using Bonvi.Application.Recruiting.Forms;
using Bonvi.Application.Recruiting.Referrals;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// «Qayerdan bilgan» ma'lumotnomasining boshlang'ich sakkiz kanali — migratsiya
/// <c>QayerdanBilgan</c> ularni AYNAN shu identifikatorlar bilan yozadi (oxirgi raqam —
/// eski <c>ReferralSource</c> sanog'ining qiymati).
/// </summary>
/// <remarks>
/// Har bir test bazasi migratsiyadan quriladi, ya'ni bu kanallar har testda bor. Nomzod
/// yaratadigan testlar <see cref="Telegram"/> ni yuboradi — kanal endi majburiy
/// (SPEC-qayerdan-bilgan §5).
/// </remarks>
internal static class ReferralSeeds
{
    /// <summary>«Tanishlar orqali» — aniqlik so'raydi (eski 1).</summary>
    public static readonly Guid Acquaintance = Guid.Parse("0199f0a0-5e1d-7c00-8000-000000000001");

    /// <summary>«Instagram / Facebook» (eski 2).</summary>
    public static readonly Guid SocialNetwork = Guid.Parse("0199f0a0-5e1d-7c00-8000-000000000002");

    /// <summary>«OLX.uz» (eski 3).</summary>
    public static readonly Guid Olx = Guid.Parse("0199f0a0-5e1d-7c00-8000-000000000003");

    /// <summary>«hh.uz» (eski 4).</summary>
    public static readonly Guid HeadHunter = Guid.Parse("0199f0a0-5e1d-7c00-8000-000000000004");

    /// <summary>«Telegram» (eski 5).</summary>
    public static readonly Guid Telegram = Guid.Parse("0199f0a0-5e1d-7c00-8000-000000000005");

    /// <summary>«Boshqa» — aniqlik so'raydi (eski 6).</summary>
    public static readonly Guid Other = Guid.Parse("0199f0a0-5e1d-7c00-8000-000000000006");

    /// <summary>«E’lon» (eski 7).</summary>
    public static readonly Guid Announcement = Guid.Parse("0199f0a0-5e1d-7c00-8000-000000000007");

    /// <summary>«Kollej / OTM» (eski 8).</summary>
    public static readonly Guid EducationalInstitution = Guid.Parse("0199f0a0-5e1d-7c00-8000-000000000008");

    /// <summary>
    /// Sakkizala kanal: identifikator, nom, aniqlik so'raydimi, eski sanoq qiymati — migratsiya
    /// testi va bazasiz testlar uchun.
    /// </summary>
    public static IReadOnlyList<(Guid Id, string Name, bool AsksDetail, int OldValue)> All { get; } =
    [
        (Acquaintance, "Tanishlar orqali", true, 1),
        (SocialNetwork, "Instagram / Facebook", false, 2),
        (Olx, "OLX.uz", false, 3),
        (HeadHunter, "hh.uz", false, 4),
        (Telegram, "Telegram", false, 5),
        (Other, "Boshqa", true, 6),
        (Announcement, "E’lon", false, 7),
        (EducationalInstitution, "Kollej / OTM", false, 8),
    ];

    /// <summary>Faol kanallar — bazasiz tekshiruvchi uchun (onlayn anketa seansidagi ro'yxat).</summary>
    public static IReadOnlyList<ReferralChannelOptionDto> Options { get; } =
        [.. All.Select(channel => new ReferralChannelOptionDto(channel.Id, channel.Name, true, channel.AsksDetail))];

    /// <summary>PDF'dagi «qayerdan bildingiz?» variantlari — <paramref name="chosen"/> ☒.</summary>
    public static IReadOnlyList<QuestionnairePdfChoice> PdfChoices(Guid? chosen)
        => [.. All.OrderBy(channel => channel.Name, StringComparer.Ordinal).Select(channel => new QuestionnairePdfChoice(channel.Name, channel.Id == chosen))];
}
