using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bonvi.Domain.Identity;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Onlayn anketa seansidagi «qayerdan bildingiz?» tanlovi (test uchun).</summary>
public sealed record KioskReferralChannelResponse(Guid Id, string Name, bool AsksDetail);

/// <summary>Seansning «qayerdan bilgan» qismi (test uchun).</summary>
public sealed record KioskReferralSessionResponse(IReadOnlyList<KioskReferralChannelResponse> ReferralChannels);

/// <summary>
/// Onlayn anketada «Ish haqida qayerdan bilgan» — ma'lumotnomadan (SPEC-qayerdan-bilgan §5).
/// </summary>
public sealed partial class KioskQuestionnaireApiTests
{
    private const string ReferralCatalogPath = "/api/catalog/referral-channels";

    [Fact]
    public async Task Seans_faol_kanallarni_beradi_anketa_kanalni_nomzodga_yozadi()
    {
        var faolEmas = await ReferralKanalAsync("Gazeta " + Guid.NewGuid().ToString("N")[..8], faol: false);

        using var mehmon = Mehmon();

        var seans = await mehmon.GetFromJsonAsync<KioskReferralSessionResponse>(new Uri(SessionPath, UriKind.Relative), Ct);

        seans!.ReferralChannels.ShouldNotContain(row => row.Id == faolEmas, "faolsizlantirilgan kanal yangi tanlovda yo'q");
        seans.ReferralChannels.Where(row => ReferralSeeds.All.Any(seed => seed.Id == row.Id)).Select(row => row.Name).ShouldBe(
            ["Boshqa", "E’lon", "hh.uz", "Instagram / Facebook", "Kollej / OTM", "OLX.uz", "Tanishlar orqali", "Telegram"]);
        seans.ReferralChannels.Single(row => row.Id == ReferralSeeds.Acquaintance).AsksDetail.ShouldBeTrue();

        // Yangi nomzod: kanal va aniqlik nomzodning o'ziga (anketaga emas)
        var telefon = Telefon();
        using var javob = await YuborAsync(mehmon, QuestionnaireSamples.Full(Bugun, telefon), Guid.NewGuid());
        javob.StatusCode.ShouldBe(HttpStatusCode.Created, await MatnAsync(javob));

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
        var nomzod = await db.Candidates.AsNoTracking().SingleAsync(row => row.Phone == Domain.ValueObjects.PhoneNumber.FromTrusted(telefon), Ct);

        nomzod.ReferralChannelId.ShouldBe(ReferralSeeds.Acquaintance);
        nomzod.ReferralDetail.ShouldBe("Karimov Anvar (sex boshlig‘i)");
    }

    [Fact]
    public async Task Anketada_kanal_majburiy_va_faol_bolishi_shart_aniqlik_soralsa_majburiy()
    {
        var faolEmas = await ReferralKanalAsync("Radio " + Guid.NewGuid().ToString("N")[..8], faol: false);

        using var mehmon = Mehmon();

        foreach (var (javoblar, kod, maydon) in new[]
                 {
                     (QuestionnaireSamples.Minimal(Bugun, Telefon()) with { ReferralChannelId = null }, "anketa.majburiy", "referralChannelId"),
                     (QuestionnaireSamples.Minimal(Bugun, Telefon()) with { ReferralChannelId = faolEmas }, "qayerdan.topilmadi", "referralChannelId"),
                     (QuestionnaireSamples.Minimal(Bugun, Telefon()) with { ReferralChannelId = ReferralSeeds.Other, ReferralDetail = null }, "anketa.majburiy", "referralDetail"),
                 })
        {
            using var javob = await YuborAsync(mehmon, javoblar, Guid.NewGuid());
            javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest, maydon);

            using var xato = JsonDocument.Parse(await MatnAsync(javob));
            xato.RootElement.GetProperty("title").GetString().ShouldBe(kod, maydon);
            xato.RootElement.GetProperty("field").GetString().ShouldBe(maydon);
        }
    }

    [Fact]
    public async Task Mavjud_nomzodda_kanal_faqat_bosh_bolsa_toldiriladi()
    {
        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        var vakansiya = await VakansiyaAsync(admin);

        // Kanalsiz ESKI nomzod (Jobster ko'chirmasi) — jarayonda, telefon va ism mos
        var telefon = Telefon();
        Guid eskiId;

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
            var eski = Domain.Recruiting.Candidate.Register(
                Domain.ValueObjects.FullName.Create("Karimova", "Oydin", null).Value,
                vakansiya,
                phone: Domain.ValueObjects.PhoneNumber.FromTrusted(telefon)).Value;

            db.Candidates.Add(eski);
            await db.SaveChangesAsync(Ct);
            eskiId = eski.Id;
        }

        using var mehmon = Mehmon();
        var javoblar = QuestionnaireSamples.Minimal(Bugun, telefon) with
        {
            LastName = "Karimova",
            FirstName = "Oydin",
            SignatureName = "Karimova Oydin",
        };

        using var javob = await YuborAsync(mehmon, javoblar, Guid.NewGuid());
        javob.StatusCode.ShouldBe(HttpStatusCode.Created, await MatnAsync(javob));

        await using var tekshiruv = _factory.Services.CreateAsyncScope();
        var kontekst = tekshiruv.ServiceProvider.GetRequiredService<AppDbContext>();
        var nomzod = await kontekst.Candidates.AsNoTracking().SingleAsync(row => row.Id == eskiId, Ct);

        nomzod.ReferralChannelId.ShouldBe(ReferralSeeds.Telegram, "bo'sh maydon anketadan to'ldiriladi");
    }

    private async Task<Guid> ReferralKanalAsync(string nom, bool faol)
    {
        using var hr = await HrAsync(Permissions.CatalogView, Permissions.CatalogManage);

        using var javob = await hr.PostAsJsonAsync(new Uri(ReferralCatalogPath, UriKind.Relative), new { name = nom }, Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        using var hujjat = JsonDocument.Parse(await MatnAsync(javob));
        var id = hujjat.RootElement.GetProperty("id").GetGuid();

        if (!faol)
        {
            using var ochirish = await hr.PostAsync(new Uri($"{ReferralCatalogPath}/{id}/deactivate", UriKind.Relative), null, Ct);
            ochirish.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        return id;
    }
}
