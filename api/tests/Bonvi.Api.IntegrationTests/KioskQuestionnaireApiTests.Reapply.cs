using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bonvi.Application.Recruiting.Forms;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Ro'yxat qatori — qayta murojaat maydoni bilan.</summary>
public sealed record ReappliedCandidateRowResponse(Guid Id, DateTimeOffset CreatedAt, DateTimeOffset? ReappliedAt);

/// <summary>Sahifalangan ro'yxat — qayta murojaat maydoni bilan.</summary>
public sealed record ReappliedCandidatePageResponse(IReadOnlyList<ReappliedCandidateRowResponse> Items, int Total);

/// <summary>
/// Botdan kelgan nomzod keyin ofisda onlayn anketa to'ldiradi (egasi, 2026-10-30): karta qayta
/// murojaat bilan belgilanadi va ro'yxatda tepaga chiqadi, anketadagi vakansiya va surat
/// kartaga o'tadi, HR'ga bildirishnoma chiqadi.
/// </summary>
public sealed partial class KioskQuestionnaireApiTests
{
    [Fact]
    public async Task Botdan_kelgan_nomzod_anketa_toldirsa_vakansiya_va_surat_almashadi_qayta_murojaat_yoziladi()
    {
        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        var botVakansiyasi = await VakansiyaAsync(admin);
        var anketaVakansiyasi = await VakansiyaAsync(admin);
        var telefon = Telefon();

        var id = await BotNomzodiAsync(admin, botVakansiyasi, telefon);
        await SuratQoyAsync(admin, id);

        var oldin = await NomzodAsync(id);
        oldin.PhotoKey.ShouldNotBeNull();
        oldin.ReappliedAt.ShouldBeNull("karta ochilgandan beri qayta murojaat yo'q");

        using var mehmon = Mehmon();
        using var javob = await YuborAsync(mehmon, Javoblar(telefon) with { VacancyId = anketaVakansiyasi }, Guid.NewGuid());
        javob.StatusCode.ShouldBe(HttpStatusCode.Created, await MatnAsync(javob));
        var natija = await javob.Content.ReadFromJsonAsync<KioskSubmittedResponse>(Ct);

        var keyin = await NomzodAsync(id);

        keyin.VacancyId.ShouldBe(anketaVakansiyasi, "anketada tanlangan vakansiya — odamning hozirgi niyati");
        keyin.Notes.ShouldContain(
            note => note.Kind == CandidateNoteKind.Reassign && note.Text.Contains("Onlayn anketada", StringComparison.Ordinal),
            "vakansiya o'zgargani kartada ko'rinishi kerak");

        keyin.PhotoKey.ShouldNotBeNull();
        keyin.PhotoKey.ShouldNotBe(oldin.PhotoKey, "anketadagi (HR olgan) surat profilga ham o'tadi");

        var anketa = await AnketaAsync(natija!.FormNumber);
        anketa.PhotoKey.ShouldNotBe(keyin.PhotoKey, "anketada MUSTAQIL nusxa — profil surati almashsa ham saqlanadi");

        keyin.ReappliedAt.ShouldBe(natija.SubmittedAt);
        keyin.CreatedAt.ShouldBe(oldin.CreatedAt, "birinchi murojaat kuni tarixiy fakt — o'zgarmaydi");
        keyin.Source.ShouldBe(CandidateSource.TelegramBot);
    }

    [Fact]
    public async Task Anketada_vakansiya_tanlanmasa_kartaning_vakansiyasi_qoladi()
    {
        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        var botVakansiyasi = await VakansiyaAsync(admin);
        var telefon = Telefon();

        var id = await BotNomzodiAsync(admin, botVakansiyasi, telefon);

        using var mehmon = Mehmon();
        using var javob = await YuborAsync(mehmon, Javoblar(telefon), Guid.NewGuid());
        javob.StatusCode.ShouldBe(HttpStatusCode.Created, await MatnAsync(javob));

        var nomzod = await NomzodAsync(id);

        nomzod.VacancyId.ShouldBe(botVakansiyasi, "«Umumiy anketa» — tanlov emas, aniq vakansiyaning o'rnini olmaydi");
        nomzod.Notes.ShouldNotContain(note => note.Kind == CandidateNoteKind.Reassign);
        nomzod.ReappliedAt.ShouldNotBeNull();
        nomzod.PhotoKey.ShouldNotBeNull("surat yo'q edi — anketadagisi qo'yiladi");
    }

    [Fact]
    public async Task Qayta_murojaat_qilgan_nomzod_royxatda_tepada_filtrda_va_bildirishnomada()
    {
        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        var vakansiya = await VakansiyaAsync(admin);
        var telefon = Telefon();

        var eski = await BotNomzodiAsync(admin, vakansiya, telefon);

        // Bot arizasi 10 kun oldin — karta ochilgan sana orqaga suriladi
        var onKunOldin = DateTimeOffset.UtcNow.AddDays(-10);
        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE candidates SET created_at = {onKunOldin} WHERE id = {eski}",
                Ct);
        }

        // Undan keyin kelgan, lekin anketa to'ldirmagan nomzod — vakansiyaning o'zida
        var yangi = await BotNomzodiAsync(admin, vakansiya, Telefon(), "Тошматов", "Ботир");

        using var mehmon = Mehmon();
        using (var javob = await YuborAsync(mehmon, Javoblar(telefon), Guid.NewGuid()))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Created, await MatnAsync(javob));
        }

        // Sukut tartib — oxirgi murojaat: bugun anketa to'ldirgan tepada
        var royxat = await RoyxatAsync(admin, $"vacancyId={vakansiya}");
        royxat.Items.Select(row => row.Id).ShouldBe([eski, yangi]);
        royxat.Items[0].ReappliedAt.ShouldNotBeNull();
        royxat.Items[1].ReappliedAt.ShouldBeNull();

        // «Anketa to'ldirganlar» filtri
        var filtr = await RoyxatAsync(admin, $"vacancyId={vakansiya}&reapplied=true");
        filtr.Items.Select(row => row.Id).ShouldBe([eski]);

        // Sana oralig'i: bugun — anketa kuni; o'n kun oldin — birinchi murojaat kuni. Ikkalasi ham topadi.
        var bugun = _factory.Services.GetRequiredService<IClock>().Today;
        var bugungi = await RoyxatAsync(admin, $"vacancyId={vakansiya}&from={bugun:yyyy-MM-dd}&to={bugun:yyyy-MM-dd}");
        bugungi.Items.Select(row => row.Id).ShouldBe([eski, yangi]);

        var birinchiKun = DateOnly.FromDateTime(onKunOldin.ToTashkent().DateTime);
        var eskiKun = await RoyxatAsync(admin, $"vacancyId={vakansiya}&from={birinchiKun:yyyy-MM-dd}&to={birinchiKun:yyyy-MM-dd}");
        eskiKun.Items.Select(row => row.Id).ShouldBe([eski]);

        // Bildirishnoma — kartaga olib boradi
        using var bildirishnomalar = await admin.GetAsync(new Uri("/api/notifications?limit=200", UriKind.Relative), Ct);
        bildirishnomalar.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(bildirishnomalar));
        var ruyxat = await bildirishnomalar.Content.ReadFromJsonAsync<NotificationListResponse>(Ct);

        var yozuv = ruyxat!.Items.SingleOrDefault(item => item.Type == "CandidateReapplied" && item.Link.Id == eski);
        yozuv.ShouldNotBeNull("anketa to'ldirgan mavjud nomzod uchun bildirishnoma chiqishi kerak");
        yozuv.Severity.ShouldBe("Info");
        yozuv.Params["source"].ShouldBe("TelegramBot");
        yozuv.Params["addedOn"].ShouldBe(birinchiKun.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Ikki_jarayondagi_kartadan_anketadagi_vakansiyadagisi_olinadi_boshqasi_kochmaydi()
    {
        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        var kassir = await VakansiyaAsync(admin);
        var omborchi = await VakansiyaAsync(admin);
        var telefon = Telefon();

        // Bitta odam — ikki vakansiyaga ikkita jarayondagi karta (HR ogohlantirishni ko'rib qoldirgan)
        var birinchi = await BotNomzodiAsync(admin, kassir, telefon);
        var ikkinchi = await BotNomzodiAsync(admin, omborchi, telefon);

        using var mehmon = Mehmon();
        using var javob = await YuborAsync(mehmon, Javoblar(telefon) with { VacancyId = omborchi }, Guid.NewGuid());
        javob.StatusCode.ShouldBe(HttpStatusCode.Created, await MatnAsync(javob));

        var kassirKarta = await NomzodAsync(birinchi);
        var omborchiKarta = await NomzodAsync(ikkinchi);

        omborchiKarta.ReappliedAt.ShouldNotBeNull("anketa aynan tanlangan vakansiyadagi kartaga qo'shiladi");
        kassirKarta.VacancyId.ShouldBe(kassir, "boshqa karta jimgina ko'chirilmaydi");
        kassirKarta.ReappliedAt.ShouldBeNull();
        kassirKarta.Notes.ShouldNotContain(note => note.Kind == CandidateNoteKind.Reassign);
    }

    private QuestionnaireFormAnswers Javoblar(string telefon)
        => QuestionnaireSamples.Full(Bugun, telefon) with
        {
            LastName = "Karimov",
            FirstName = "Aziz",
            MiddleName = null,
            SignatureName = "Karimov Aziz",
        };

    private static async Task<Guid> BotNomzodiAsync(
        HttpClient admin,
        Guid vakansiya,
        string telefon,
        string familiya = "Каримов",
        string ism = "Азиз")
    {
        using var javob = await admin.PostAsJsonAsync(
            new Uri("/api/recruiting/candidates", UriKind.Relative),
            new
            {
                vacancyId = vakansiya,
                lastName = familiya,
                firstName = ism,
                phone = telefon,
                source = "TelegramBot",
                referralChannelId = ReferralSeeds.Telegram,
            },
            Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        using var hujjat = JsonDocument.Parse(await MatnAsync(javob));
        return hujjat.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task SuratQoyAsync(HttpClient admin, Guid id)
    {
        using var forma = new MultipartFormDataContent();
        var rasm = new ByteArrayContent(QuestionnaireSamples.Photo());
        rasm.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        forma.Add(rasm, "file", "bot.jpg");

        using var javob = await admin.PostAsync(new Uri($"/api/recruiting/candidates/{id}/photo", UriKind.Relative), forma, Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
    }

    private async Task<Candidate> NomzodAsync(Guid id)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.Candidates.AsNoTracking()
            .Include(candidate => candidate.Notes)
            .SingleAsync(candidate => candidate.Id == id, Ct);
    }

    private async Task<CandidateForm> AnketaAsync(int raqam)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.CandidateForms.AsNoTracking().SingleAsync(form => form.Number == raqam, Ct);
    }

    private static async Task<ReappliedCandidatePageResponse> RoyxatAsync(HttpClient admin, string query)
    {
        using var javob = await admin.GetAsync(new Uri($"/api/recruiting/candidates?{query}&pageSize=100", UriKind.Relative), Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        return (await javob.Content.ReadFromJsonAsync<ReappliedCandidatePageResponse>(Ct))!;
    }
}
