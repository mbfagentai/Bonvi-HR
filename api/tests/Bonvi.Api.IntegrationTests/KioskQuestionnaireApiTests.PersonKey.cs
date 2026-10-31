using System.Net;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Onlayn anketa telefon bo'yicha topilmasa F.I. + tug'ilgan sana bilan ham qidiradi (egasi, 2026-10-31):
/// prod'da odam botga Telegram raqami bilan yozib, anketaga boshqa raqam kiritgan va ikkinchi karta ochilgan edi.
/// </summary>
public sealed partial class KioskQuestionnaireApiTests
{
    [Fact]
    public async Task Boshqa_telefonli_anketa_FI_va_sana_bilan_eski_kartaga_qoshiladi_telefon_almashadi()
    {
        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        var vakansiya = await VakansiyaAsync(admin);
        var (familiya, ism) = Ism();
        var sana = Bugun.AddYears(-30).AddDays(-11);
        var botTelefoni = Telefon();
        var anketaTelefoni = Telefon();

        var id = await BotNomzodiAsync(admin, vakansiya, botTelefoni, familiya.ToUpperInvariant(), ism);
        await TugilganSanaQoyAsync(id, sana);

        using var mehmon = Mehmon();
        using var javob = await YuborAsync(
            mehmon,
            QuestionnaireSamples.Full(Bugun, anketaTelefoni) with
            {
                LastName = familiya,
                FirstName = ism,
                MiddleName = null,
                SignatureName = familiya + " " + ism,
                BirthDate = sana,
            },
            Guid.NewGuid());
        javob.StatusCode.ShouldBe(HttpStatusCode.Created, await MatnAsync(javob));

        var karta = await NomzodAsync(id);
        karta.ReappliedAt.ShouldNotBeNull("anketa shu kartaga qo'shildi");
        karta.Phone!.Value.ShouldBe(anketaTelefoni, "anketadagi raqam yangiroq — kartaga o'tadi");
        karta.Notes.ShouldContain(
            note => note.Kind == CandidateNoteKind.OtherPhone && note.Text.Contains(botTelefoni, StringComparison.Ordinal),
            "botdagi avvalgi raqam lentada qoladi");

        (await IsmdagilarAsync(familiya, ism)).ShouldBe(1, "ikkinchi karta ochilmaydi");
    }

    [Fact]
    public async Task Tugilgan_sanasi_boshqa_bolsa_yangi_karta_ochiladi()
    {
        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        var vakansiya = await VakansiyaAsync(admin);
        var (familiya, ism) = Ism();
        var sana = Bugun.AddYears(-25);

        var id = await BotNomzodiAsync(admin, vakansiya, Telefon(), familiya, ism);
        await TugilganSanaQoyAsync(id, sana);

        using var mehmon = Mehmon();
        using var javob = await YuborAsync(
            mehmon,
            QuestionnaireSamples.Full(Bugun, Telefon()) with
            {
                LastName = familiya,
                FirstName = ism,
                MiddleName = null,
                SignatureName = familiya + " " + ism,
                BirthDate = sana.AddDays(1),
            },
            Guid.NewGuid());
        javob.StatusCode.ShouldBe(HttpStatusCode.Created, await MatnAsync(javob));

        (await NomzodAsync(id)).ReappliedAt.ShouldBeNull("adashlar bitta kartaga qo'shilmaydi");
        (await IsmdagilarAsync(familiya, ism)).ShouldBe(2);
    }

    /// <summary>Boshqa testlar bilan to'qnashmaydigan F.I. — faqat harflar (forma tekshiruvi shuni talab qiladi).</summary>
    private static (string Familiya, string Ism) Ism()
    {
        const string harflar = "abdefghiklmnoprstuvxyz";
        var tasodif = Guid.NewGuid().ToByteArray();
        var qism = new string([.. tasodif.Take(7).Select(bayt => harflar[bayt % harflar.Length])]);
        return ("Telefonov" + qism, "Ikkiraqam");
    }

    private async Task TugilganSanaQoyAsync(Guid id, DateOnly sana)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var nomzod = await db.Candidates.SingleAsync(candidate => candidate.Id == id, Ct);
        nomzod.Update(nomzod.FullName, sana, nomzod.Phone, nomzod.Address, nomzod.Speciality, nomzod.Note).IsSuccess.ShouldBeTrue();
        await db.SaveChangesAsync(Ct);
    }

    private async Task<int> IsmdagilarAsync(string familiya, string ism)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        // Bot kartasi familiyani katta harf bilan yozgan bo'lishi mumkin — registrsiz
        return await db.Candidates.CountAsync(
            candidate => EF.Functions.ILike(candidate.FullName.LastName, familiya) && candidate.FullName.FirstName == ism,
            Ct);
    }
}
