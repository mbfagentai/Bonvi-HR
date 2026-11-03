using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bonvi.Domain.Blanks;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// «Blankalar» — imzo qadamlari (SPEC-blankalar §5, §6.2, §7 #12–#16) uchi-uchiga.
/// </summary>
/// <remarks>
/// To'liq zanjirlar seeder qo'ygan haqiqiy shablonlarda (HR-F07, HR-F13); qoidalar (qaytarish, rad etish, rol
/// biriktirish, versiya) — test ichida yozilgan kichik ta'rifda. Rol biriktirgan test oxirida uni bo'shatadi.
/// </remarks>
public sealed class BlankStepApiTests : IClassFixture<BonviApiFactory>
{
    private const string Yol = "/api/blanks";

    private readonly BonviApiFactory _factory;

    public BlankStepApiTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ────────────────────────────────────────────────────────────
    //  Ruxsat: #12–#16 — 401 / 403 / 2xx
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Har_qadam_endpointi_tokensiz_401_ruxsatsiz_403()
    {
        var dunyo = await DunyoAsync();
        using var hr = await MijozAsync("blanks.view", "blanks.fill");
        var blanka = await YuborilganAsync(hr, dunyo);

        using var tokensiz = _factory.CreateClient();
        using var begona = await MijozAsync("employees.view");
        using var koruvchi = await MijozAsync("blanks.view");

        foreach (var (mijoz, kutilgan) in new[] { (tokensiz, HttpStatusCode.Unauthorized), (begona, HttpStatusCode.Forbidden) })
        {
            foreach (var (usul, yol) in Endpointlar(blanka.Id))
            {
                using var javob = await YuborAsync(mijoz, usul, yol, new { expectedVersion = blanka.Version, comment = "X" });
                javob.StatusCode.ShouldBe(kutilgan, $"{usul} {yol}");
            }
        }

        // «blanks.view» — o'qiydi, lekin qadam bajaruvchisi emas: #12/#14 handlerda, #13/#15 siyosatda yopiq.
        foreach (var (usul, yol) in Endpointlar(blanka.Id).Where(e => e.Usul != "GET"))
        {
            using var javob = await YuborAsync(koruvchi, usul, yol, new { expectedVersion = blanka.Version, comment = "X" });
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden, $"{usul} {yol}");
        }
    }

    [Fact]
    public async Task Har_qadam_endpointi_ruxsati_bilan_2xx()
    {
        var dunyo = await DunyoAsync();
        using var hr = await MijozAsync("blanks.view", "blanks.fill");
        using var tasdiqlovchi = await MijozAsync("blanks.view", "blanks.approve");
        using var koruvchi = await MijozAsync("blanks.view");

        // #12 — hr qadami (hr1, biriktirilmagan → har qanday «blanks.approve»)
        var blanka = await YuborilganAsync(hr, dunyo);
        blanka = await TasdiqlaAsync(tasdiqlovchi, blanka, "hr", new { decision = "ok", answers = new { hrNote = "Tekshirildi" } });
        Qadam(blanka, "hr").State.ShouldBe("Signed");
        Qadam(blanka, "employeeSign").State.ShouldBe("Open");

        // #13 — xodim qadami planshetda
        blanka = await PlanshetdaAsync(hr, blanka, "employeeSign", new { expectedVersion = blanka.Version });
        blanka.Status.ShouldBe("Approved");

        // #16 — imzo rasmi
        using (var javob = await koruvchi.GetAsync(Uri($"{Yol}/submissions/{blanka.Id}/steps/employeeSign/signature"), Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK);
            javob.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
        }

        // #14 — qaytarish
        var ikkinchi = await YuborilganAsync(hr, dunyo);
        using (var javob = await IzohliAsync(tasdiqlovchi, ikkinchi, "hr", "return", "Sababni aniqlashtiring"))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await javob.Content.ReadAsStringAsync(Ct));
        }

        // #15 — rad etish
        var uchinchi = await YuborilganAsync(hr, dunyo);
        using (var javob = await IzohliAsync(tasdiqlovchi, uchinchi, "hr", "reject", "Asossiz"))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await javob.Content.ReadAsStringAsync(Ct));
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Haqiqiy shablonlar
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task HR_F07_toliq_zanjir_parallel_guruh_shaxs_roli_planshet_imzosi_va_HR1_qarori_bilan_tasdiqlanadi()
    {
        var xodim = await XodimAsync("Sinovchi");
        var ustoz = await XodimAsync("Ustoz");
        var ustozHisobi = await TestAccounts.CreateAsync(_factory, "blanks.view");
        await XodimgaBoglaAsync(ustozHisobi.UserId, ustoz);

        using var hr = await MijozAsync("blanks.view", "blanks.fill");
        using var tasdiqlovchi = await MijozAsync("blanks.view", "blanks.approve");
        using var ustozMijoz = Mijoz(ustozHisobi.AccessToken);
        using var koruvchi = await MijozAsync("blanks.view");

        var blanka = await YaratAsync(hr, "HR-F07", xodim);
        blanka = await SaqlaAsync(hr, blanka, new { employee = blanka.Answers.GetProperty("employee"), mentor = new { id = ustoz } });
        blanka = await YuborishAsync(hr, blanka);

        // 1-guruh parallel: bo'lim rahbari va ustoz birga ochiq
        blanka.Status.ShouldBe("InApproval");
        Qadam(blanka, "signHead").State.ShouldBe("Open");
        Qadam(blanka, "signMentor").State.ShouldBe("Open");
        Qadam(blanka, "hr4Tests").State.ShouldBe("Waiting");

        // Begona «blanks.view» egasi ustoz qadamini imzolay olmaydi
        using (var javob = await koruvchi.PostAsJsonAsync(
                   Uri($"{Yol}/submissions/{blanka.Id}/steps/signMentor/approve"), new { expectedVersion = blanka.Version }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            (await KodAsync(javob)).ShouldBe("blanka.qadam_ruxsat_yoq");
        }

        // Ustoz — faqat «blanks.view», lekin users.employee_id = mentor → o'z qadamini elektron imzolaydi
        var ustozKorgani = await OqiAsync(ustozMijoz, blanka.Id);
        ustozKorgani.Can.ActionableSteps.ShouldContain(step => step.StepCode == "signMentor" && step.Electronic);
        blanka = await TasdiqlaAsync(ustozMijoz, blanka, "signMentor", new { });
        Qadam(blanka, "signMentor").State.ShouldBe("Signed");
        Qadam(blanka, "signMentor").Method.ShouldBe("Electronic");
        Qadam(blanka, "signMentor").SignerEmployeeId.ShouldBe(ustoz);
        Qadam(blanka, "hr4Tests").State.ShouldBe("Waiting", "guruh hali yopilmagan");

        // Bo'lim boshlig'i aniqlanmaydi → faqat planshetda; rahbar bahosi bo'limi majburiylari tekshiriladi
        var baho = new
        {
            criteria = Enumerable.Range(0, 7).Select(_ => new { score = 4 }).ToArray(),
            conclusion = "passed",
        };
        using (var javob = await PlanshetdaYuborAsync(hr, blanka.Id, "signHead", new { expectedVersion = blanka.Version }))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await KodAsync(javob)).ShouldBe("blanka.majburiy");
        }

        blanka = await PlanshetdaAsync(hr, blanka, "signHead", new { expectedVersion = blanka.Version, answers = baho, signerName = "Rahbar R." });
        Qadam(blanka, "signHead").State.ShouldBe("Signed");
        Qadam(blanka, "signHead").HasSignature.ShouldBeTrue();
        Qadam(blanka, "hr4Tests").State.ShouldBe("Open");
        blanka.Answers.GetProperty("conclusion").GetProperty("value").GetString().ShouldBe("passed");

        // HR-4 testlar
        blanka = await TasdiqlaAsync(tasdiqlovchi, blanka, "hr4Tests", new
        {
            answers = new { testResults = new[] { new { testPercent = 80 }, new { testPercent = 75 } } },
        });
        Qadam(blanka, "signEmployee").State.ShouldBe("Open");

        // Xodim planshetda: qaror majburiy; «e'tirozim bor» — qo'shimcha matn majburiy
        using (var javob = await PlanshetdaYuborAsync(hr, blanka.Id, "signEmployee", new { expectedVersion = blanka.Version }))
        {
            (await KodAsync(javob)).ShouldBe("blanka.qaror_kerak");
        }

        using (var javob = await PlanshetdaYuborAsync(hr, blanka.Id, "signEmployee", new { expectedVersion = blanka.Version, decision = "object" }))
        {
            (await KodAsync(javob)).ShouldBe("blanka.qaror_kerak");
        }

        blanka = await PlanshetdaAsync(hr, blanka, "signEmployee", new
        {
            expectedVersion = blanka.Version,
            decision = "object",
            decisionExtra = "Baho past qo'yilgan",
        });
        var xodimQadami = Qadam(blanka, "signEmployee");
        xodimQadami.State.ShouldBe("Signed");
        xodimQadami.Method.ShouldBe("InPerson");
        xodimQadami.Decision.ShouldBe("object");
        xodimQadami.SignerEmployeeId.ShouldBe(xodim);
        xodimQadami.SignerName.ShouldNotBeNullOrWhiteSpace();
        xodimQadami.AnswersPatch!.Value.GetProperty("$decisionExtra").GetString().ShouldBe("Baho past qo'yilgan");

        // HR-1 qarori — oxirgi guruh → Approved
        blanka = await TasdiqlaAsync(tasdiqlovchi, blanka, "hr1Decision", new { decision = "approve", answers = new { registered = true } });
        blanka.Status.ShouldBe("Approved");
        blanka.CompletedAt.ShouldNotBeNull();
        blanka.Steps.ShouldAllBe(step => step.State == "Signed");
        blanka.Missing.ShouldBeEmpty();

        using (var javob = await koruvchi.GetAsync(Uri($"{Yol}/submissions/{blanka.Id}/steps/signEmployee/signature"), Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK);
            var rasm = await javob.Content.ReadAsByteArrayAsync(Ct);
            using var bitmap = SKBitmap.Decode(rasm);
            bitmap.Width.ShouldBeLessThanOrEqualTo(1200);
        }

        // Elektron imzolangan qadamda rasm yo'q
        using (var javob = await koruvchi.GetAsync(Uri($"{Yol}/submissions/{blanka.Id}/steps/signMentor/signature"), Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        // Tasdiqlangandan keyin qadam amali — holat noto'g'ri
        using (var javob = await IzohliAsync(tasdiqlovchi, blanka, "hr1Decision", "reject", "Kech"))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            (await KodAsync(javob)).ShouldBe("blanka.holat_notogri");
        }
    }

    [Fact]
    public async Task HR_F13_guvoh_qadamlari_tushuntirish_yozilgan_bolsa_otkazib_yuboriladi()
    {
        using var hr = await MijozAsync("blanks.view", "blanks.fill");
        using var tasdiqlovchi = await MijozAsync("blanks.view", "blanks.approve");

        // Tushuntirish yozildi → 3-guruh (guvohlar) Skipped, HR-3 chorasi ochiladi
        var blanka = await F13XodimQadamigachaAsync(hr, tasdiqlovchi);
        blanka = await PlanshetdaAsync(hr, blanka, "signEmployee", new
        {
            expectedVersion = blanka.Version,
            decision = "received",
            answers = new { explanation = "Avtobus kechikdi." },
        });
        Qadam(blanka, "witness1").State.ShouldBe("Skipped");
        Qadam(blanka, "witness2").State.ShouldBe("Skipped");
        Qadam(blanka, "hr3Measure").State.ShouldBe("Open");

        // Tushuntirish yo'q → guvohlar ochiladi; guvohda ism majburiy
        var ikkinchi = await F13XodimQadamigachaAsync(hr, tasdiqlovchi);
        ikkinchi = await PlanshetdaAsync(hr, ikkinchi, "signEmployee", new { expectedVersion = ikkinchi.Version, decision = "refused" });
        Qadam(ikkinchi, "witness1").State.ShouldBe("Open");
        Qadam(ikkinchi, "witness2").State.ShouldBe("Open");
        Qadam(ikkinchi, "hr3Measure").State.ShouldBe("Waiting");

        using var javob = await PlanshetdaYuborAsync(hr, ikkinchi.Id, "witness1", new { expectedVersion = ikkinchi.Version });
        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await KodAsync(javob)).ShouldBe("blanka.imzochi_kerak");

        // Guvoh qadami faqat planshetda
        using var elektron = await tasdiqlovchi.PostAsJsonAsync(
            Uri($"{Yol}/submissions/{ikkinchi.Id}/steps/witness1/approve"), new { expectedVersion = ikkinchi.Version }, Ct);
        elektron.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ────────────────────────────────────────────────────────────
    //  Qoidalar
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Rol_biriktirilgan_bolsa_qadamni_faqat_biriktirilgan_foydalanuvchi_tasdiqlaydi()
    {
        var dunyo = await DunyoAsync();
        using var hr = await MijozAsync("blanks.view", "blanks.fill");
        using var boshqaruvchi = await MijozAsync("blanks.view", "blanks.manage");
        var biriktirilgan = await TestAccounts.CreateAsync(_factory, "blanks.view", "blanks.approve");
        using var biriktirilganMijoz = Mijoz(biriktirilgan.AccessToken);
        using var boshqaTasdiqlovchi = await MijozAsync("blanks.view", "blanks.approve");

        await RolAsync(boshqaruvchi, "hr1", biriktirilgan.UserId);
        try
        {
            var blanka = await YuborilganAsync(hr, dunyo);

            using (var javob = await boshqaTasdiqlovchi.PostAsJsonAsync(
                       Uri($"{Yol}/submissions/{blanka.Id}/steps/hr/approve"),
                       new { expectedVersion = blanka.Version, decision = "ok", answers = new { hrNote = "X" } },
                       Ct))
            {
                javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
                (await KodAsync(javob)).ShouldBe("blanka.qadam_ruxsat_yoq");
            }

            blanka = await TasdiqlaAsync(biriktirilganMijoz, blanka, "hr", new { decision = "ok", answers = new { hrNote = "X" } });
            Qadam(blanka, "hr").State.ShouldBe("Signed");
            Qadam(blanka, "hr").ActedByUserId.ShouldBe(biriktirilgan.UserId);
        }
        finally
        {
            await RolAsync(boshqaruvchi, "hr1");
        }
    }

    [Fact]
    public async Task Qaytarish_qoralamaga_otkazadi_raqamni_saqlaydi_izoh_majburiy()
    {
        var dunyo = await DunyoAsync();
        using var hr = await MijozAsync("blanks.view", "blanks.fill");
        using var tasdiqlovchi = await MijozAsync("blanks.view", "blanks.approve");

        var blanka = await YuborilganAsync(hr, dunyo);
        var raqam = blanka.Number;
        raqam.ShouldNotBeNull();

        using (var javob = await IzohliAsync(tasdiqlovchi, blanka, "hr", "return", "  "))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await KodAsync(javob)).ShouldBe("blanka.sabab_kerak");
        }

        // Hali kutayotgan qadamdan qaytarib bo'lmaydi
        using (var javob = await IzohliAsync(tasdiqlovchi, blanka, "employeeSign", "return", "X"))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            (await KodAsync(javob)).ShouldBe("blanka.qadam_ochiq_emas");
        }

        using (var javob = await IzohliAsync(tasdiqlovchi, blanka, "hr", "return", "Sababni aniqlashtiring"))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK);
            blanka = (await javob.Content.ReadFromJsonAsync<BlankaJavobi>(Ct))!;
        }

        blanka.Status.ShouldBe("Draft");
        blanka.Number.ShouldBe(raqam);
        blanka.ReturnComment.ShouldBe("Sababni aniqlashtiring");
        blanka.Steps.ShouldBeEmpty();

        blanka = await YuborishAsync(hr, blanka);
        blanka.Number.ShouldBe(raqam);
        blanka.ReturnComment.ShouldBeNull();
        Qadam(blanka, "hr").State.ShouldBe("Open");
    }

    [Fact]
    public async Task Rad_etish_yakuniy_izoh_majburiy_canReject_yoq_qadamda_409()
    {
        var dunyo = await DunyoAsync();
        using var hr = await MijozAsync("blanks.view", "blanks.fill");
        using var tasdiqlovchi = await MijozAsync("blanks.view", "blanks.approve");

        var blanka = await YuborilganAsync(hr, dunyo);

        using (var javob = await IzohliAsync(tasdiqlovchi, blanka, "hr", "reject", null))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await KodAsync(javob)).ShouldBe("blanka.sabab_kerak");
        }

        using (var javob = await IzohliAsync(tasdiqlovchi, blanka, "hr", "reject", "Asossiz"))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK);
            blanka = (await javob.Content.ReadFromJsonAsync<BlankaJavobi>(Ct))!;
        }

        blanka.Status.ShouldBe("Rejected");
        blanka.CompletedAt.ShouldNotBeNull();
        Qadam(blanka, "hr").State.ShouldBe("Rejected");
        Qadam(blanka, "hr").Comment.ShouldBe("Asossiz");

        // F13 ning birinchi qadamida «canReject» yo'q
        using var hrF13 = await MijozAsync("blanks.view", "blanks.fill");
        var f13 = await F13YuborilganAsync(hrF13);
        using var rad = await IzohliAsync(tasdiqlovchi, f13, "hr3Demand", "reject", "X");
        rad.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await KodAsync(rad)).ShouldBe("blanka.rad_mumkin_emas");
    }

    [Fact]
    public async Task Eskirgan_expectedVersion_409_ozgargan_qaror_varianti_notogri_400()
    {
        var dunyo = await DunyoAsync();
        using var hr = await MijozAsync("blanks.view", "blanks.fill");
        using var tasdiqlovchi = await MijozAsync("blanks.view", "blanks.approve");

        var blanka = await YuborilganAsync(hr, dunyo);

        foreach (var yol in new[] { "approve", "return", "reject" })
        {
            using var javob = await tasdiqlovchi.PostAsJsonAsync(
                Uri($"{Yol}/submissions/{blanka.Id}/steps/hr/{yol}"),
                new { expectedVersion = blanka.Version - 1, decision = "ok", comment = "X", answers = new { hrNote = "X" } },
                Ct);
            javob.StatusCode.ShouldBe(HttpStatusCode.Conflict, yol);
            (await KodAsync(javob)).ShouldBe("blanka.ozgargan");
        }

        using (var javob = await PlanshetdaYuborAsync(hr, blanka.Id, "employeeSign", new { expectedVersion = blanka.Version - 1 }))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            (await KodAsync(javob)).ShouldBe("blanka.ozgargan");
        }

        using (var javob = await tasdiqlovchi.PostAsJsonAsync(
                   Uri($"{Yol}/submissions/{blanka.Id}/steps/hr/approve"),
                   new { expectedVersion = blanka.Version, decision = "maybe", answers = new { hrNote = "X" } },
                   Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await KodAsync(javob)).ShouldBe("blanka.notogri");
        }

        // Boshqa bo'lim maydoni qadam bilan o'zgarmaydi
        using (var javob = await tasdiqlovchi.PostAsJsonAsync(
                   Uri($"{Yol}/submissions/{blanka.Id}/steps/hr/approve"),
                   new { expectedVersion = blanka.Version, decision = "ok", answers = new { hrNote = "X", reason = "Boshqa" } },
                   Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            (await KodAsync(javob)).ShouldBe("blanka.maydon_tahrirlanmaydi");
        }

        // Kutayotgan qadamga imzo — 409; ochilgach yaroqsiz rasm — 400
        using (var javob = await PlanshetdaYuborAsync(hr, blanka.Id, "employeeSign", new { expectedVersion = blanka.Version }))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            (await KodAsync(javob)).ShouldBe("blanka.qadam_ochiq_emas");
        }

        blanka = await TasdiqlaAsync(tasdiqlovchi, blanka, "hr", new { decision = "other", decisionExtra = "Qisman", answers = new { hrNote = "X" } });
        Qadam(blanka, "hr").Decision.ShouldBe("other");

        using var yaroqsiz = await PlanshetdaYuborAsync(hr, blanka.Id, "employeeSign", new { expectedVersion = blanka.Version }, [1, 2, 3, 4]);
        yaroqsiz.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await KodAsync(yaroqsiz)).ShouldBe("blanka.imzo_yaroqsiz");
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    private static readonly string[] Yoqlik = ["absent"];

    private sealed record Dunyo(string Kod, Guid Xodim);

    private sealed record QadamJavobi(
        string StepCode,
        string State,
        string? Decision,
        string? Comment,
        string? Method,
        Guid? ActedByUserId,
        Guid? SignerEmployeeId,
        string? SignerName,
        bool HasSignature,
        JsonElement? AnswersPatch);

    private sealed record ImkonQadam(string StepCode, bool Electronic, bool InPerson);

    private sealed record ImkonJavobi(List<ImkonQadam> ActionableSteps);

    private sealed record BlankaJavobi(
        Guid Id,
        string Status,
        int? Number,
        int Version,
        JsonElement Answers,
        List<string> Missing,
        string? ReturnComment,
        DateTimeOffset? CompletedAt,
        List<QadamJavobi> Steps,
        ImkonJavobi Can);

    private static Uri Uri(string yol) => new(yol, UriKind.Relative);

    private static QadamJavobi Qadam(BlankaJavobi blanka, string kod) => blanka.Steps.Single(step => step.StepCode == kod);

    private static IEnumerable<(string Usul, string Yol)> Endpointlar(Guid blanka) =>
    [
        ("POST", $"{Yol}/submissions/{blanka}/steps/hr/approve"),
        ("POST", $"{Yol}/submissions/{blanka}/steps/hr/sign-in-person"),
        ("POST", $"{Yol}/submissions/{blanka}/steps/hr/return"),
        ("POST", $"{Yol}/submissions/{blanka}/steps/hr/reject"),
        ("GET", $"{Yol}/submissions/{blanka}/steps/hr/signature"),
    ];

    private static Task<HttpResponseMessage> YuborAsync(HttpClient mijoz, string usul, string yol, object tana)
    {
        var sorov = new HttpRequestMessage(new HttpMethod(usul), Uri(yol));
        if (yol.EndsWith("/sign-in-person", StringComparison.Ordinal))
        {
            // Marshrut faqat multipart qabul qiladi — JSON bilan ruxsatgacha 415 bo'lardi.
            sorov.Content = new MultipartFormDataContent
            {
                { new StringContent(JsonSerializer.Serialize(tana)), "payload" },
                { new ByteArrayContent(ImzoPng()), "signature", "imzo.png" },
            };
        }
        else if (usul != "GET")
        {
            sorov.Content = JsonContent.Create(tana);
        }

        return mijoz.SendAsync(sorov, Ct);
    }

    private static async Task<string?> KodAsync(HttpResponseMessage javob)
        => (await javob.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("title").GetString();

    private static async Task<BlankaJavobi> OqiAsync(HttpClient mijoz, Guid id)
        => (await mijoz.GetFromJsonAsync<BlankaJavobi>(Uri($"{Yol}/submissions/{id}"), Ct))!;

    private static async Task<BlankaJavobi> YaratAsync(HttpClient mijoz, string kod, Guid xodim)
    {
        using var javob = await mijoz.PostAsJsonAsync(
            Uri(Yol + "/submissions"),
            new { templateCode = kod, employeeId = xodim, clientRequestId = Guid.NewGuid() },
            Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.Created, await javob.Content.ReadAsStringAsync(Ct));
        return (await javob.Content.ReadFromJsonAsync<BlankaJavobi>(Ct))!;
    }

    private static async Task<BlankaJavobi> SaqlaAsync(HttpClient mijoz, BlankaJavobi blanka, object javoblar)
    {
        using var javob = await mijoz.PutAsJsonAsync(
            Uri($"{Yol}/submissions/{blanka.Id}/answers"),
            new { expectedVersion = blanka.Version, answers = javoblar },
            Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await javob.Content.ReadAsStringAsync(Ct));
        return (await javob.Content.ReadFromJsonAsync<BlankaJavobi>(Ct))!;
    }

    private static async Task<BlankaJavobi> YuborishAsync(HttpClient mijoz, BlankaJavobi blanka)
    {
        using var javob = await mijoz.PostAsJsonAsync(
            Uri($"{Yol}/submissions/{blanka.Id}/submit"), new { expectedVersion = blanka.Version }, Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await javob.Content.ReadAsStringAsync(Ct));
        return (await javob.Content.ReadFromJsonAsync<BlankaJavobi>(Ct))!;
    }

    private static async Task<BlankaJavobi> YuborilganAsync(HttpClient mijoz, Dunyo dunyo)
    {
        var blanka = await YaratAsync(mijoz, dunyo.Kod, dunyo.Xodim);
        blanka = await SaqlaAsync(mijoz, blanka, new { employee = blanka.Answers.GetProperty("employee"), reason = "Kechikish" });
        return await YuborishAsync(mijoz, blanka);
    }

    private async Task<BlankaJavobi> F13YuborilganAsync(HttpClient hr)
    {
        var blanka = await YaratAsync(hr, "HR-F13", await XodimAsync("Intizom"));
        blanka = await SaqlaAsync(hr, blanka, new
        {
            employee = blanka.Answers.GetProperty("employee"),
            violationAt = "2026-11-02T09:30:00+05:00",
            violationTypes = Yoqlik,
            whatHappened = "Ishga kelmadi.",
        });
        return await YuborishAsync(hr, blanka);
    }

    private async Task<BlankaJavobi> F13XodimQadamigachaAsync(HttpClient hr, HttpClient tasdiqlovchi)
    {
        var blanka = await F13YuborilganAsync(hr);
        Qadam(blanka, "hr3Demand").State.ShouldBe("Open");
        blanka = await TasdiqlaAsync(tasdiqlovchi, blanka, "hr3Demand", new { });
        Qadam(blanka, "signEmployee").State.ShouldBe("Open");
        return blanka;
    }

    /// <summary>#12 — tanaga <c>expectedVersion</c> qo'shib yuboradi.</summary>
    private static async Task<BlankaJavobi> TasdiqlaAsync(HttpClient mijoz, BlankaJavobi blanka, string qadam, object tana)
    {
        var json = JsonSerializer.SerializeToNode(tana)!.AsObject();
        json["expectedVersion"] = blanka.Version;

        using var javob = await mijoz.PostAsJsonAsync(Uri($"{Yol}/submissions/{blanka.Id}/steps/{qadam}/approve"), json, Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await javob.Content.ReadAsStringAsync(Ct));
        return (await javob.Content.ReadFromJsonAsync<BlankaJavobi>(Ct))!;
    }

    private static Task<HttpResponseMessage> IzohliAsync(HttpClient mijoz, BlankaJavobi blanka, string qadam, string amal, string? izoh)
        => mijoz.PostAsJsonAsync(
            Uri($"{Yol}/submissions/{blanka.Id}/steps/{qadam}/{amal}"),
            new { expectedVersion = blanka.Version, comment = izoh },
            Ct);

    private static async Task<BlankaJavobi> PlanshetdaAsync(HttpClient mijoz, BlankaJavobi blanka, string qadam, object payload)
    {
        using var javob = await PlanshetdaYuborAsync(mijoz, blanka.Id, qadam, payload);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await javob.Content.ReadAsStringAsync(Ct));
        return (await javob.Content.ReadFromJsonAsync<BlankaJavobi>(Ct))!;
    }

    private static Task<HttpResponseMessage> PlanshetdaYuborAsync(
        HttpClient mijoz,
        Guid blanka,
        string qadam,
        object payload,
        byte[]? imzo = null)
    {
        var forma = new MultipartFormDataContent
        {
            { new StringContent(JsonSerializer.Serialize(payload)), "payload" },
        };
        var rasm = new ByteArrayContent(imzo ?? ImzoPng());
        rasm.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        forma.Add(rasm, "signature", "imzo.png");

        return mijoz.PostAsync(Uri($"{Yol}/submissions/{blanka}/steps/{qadam}/sign-in-person"), forma, Ct);
    }

    /// <summary>Shaffof fonda chiziq — server oq fonda qayta chizadi va kichraytiradi.</summary>
    private static byte[] ImzoPng()
    {
        using var surface = SKSurface.Create(new SKImageInfo(1000, 400, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.Transparent);
        using var qalam = new SKPaint { Color = SKColors.Black, StrokeWidth = 6, IsStroke = true };
        surface.Canvas.DrawLine(50, 350, 950, 50, qalam);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static async Task RolAsync(HttpClient boshqaruvchi, string rol, params Guid[] foydalanuvchilar)
    {
        using var javob = await boshqaruvchi.PutAsJsonAsync(Uri($"{Yol}/roles/{rol}"), new { userIds = foydalanuvchilar }, Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await javob.Content.ReadAsStringAsync(Ct));
    }

    /// <summary>Kichik ta'rif: qoralama (majburiy «reason»), hr1 qadami (o'z bo'limi, qaror, rad/qaytarish), xodim qadami.</summary>
    private static string Tarif(string kod) => $$"""
        { "schema": 1, "code": "{{kod}}", "version": "1.0", "subject": "employee", "title": "Qadam sinovi",
          "sections": [
            { "id": "hdr", "title": "Xodim", "fields": [
              { "id": "employee", "type": "ref", "refKind": "employee", "label": "Xodim", "prefill": "subject.fullName", "readOnly": true },
              { "id": "reason", "type": "text", "label": "Sabab", "required": true } ] },
            { "id": "hrPart", "title": "HR", "fill": { "at": "step", "step": "hr" }, "fields": [
              { "id": "hrNote", "type": "text", "label": "HR izohi", "required": true } ] } ],
          "steps": [
            { "id": "hr", "title": "HR", "order": 1, "actor": "hr1", "mode": "electronic", "canReject": true, "canReturn": true,
              "decision": { "required": true, "options": [
                { "value": "ok", "label": "Ha" },
                { "value": "other", "label": "Boshqa", "extra": { "type": "text", "label": "Nima", "required": true } } ] } },
            { "id": "employeeSign", "title": "Xodim tanishdi", "order": 2, "actor": "subjectEmployee", "mode": "either" } ] }
        """;

    private async Task<Dunyo> DunyoAsync()
    {
        var kod = "S" + string.Concat(Guid.NewGuid().ToString("N")[..8]
            .Select(belgi => (char)('A' + Convert.ToInt32(belgi.ToString(), 16))));

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var shablon = BlankTemplate.Create(kod, "Qadam sinovi " + kod, BlankSubjectKind.Employee).Value;
        shablon.Activate();
        var versiya = BlankTemplateVersion.Create(shablon.Id, "1.0", Tarif(kod), new string('b', 64), BlankTemplateSource.Seed).Value;
        versiya.Publish(null, DateTimeOffset.UtcNow).IsSuccess.ShouldBeTrue();
        var xodim = Employee.Create(FullName.Create("Xodim" + kod, "Ali", null).Value).Value;

        kontekst.AddRange(shablon, versiya, xodim);
        await kontekst.SaveChangesAsync(Ct);

        return new Dunyo(kod, xodim.Id);
    }

    private async Task<Guid> XodimAsync(string familiya)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
        var xodim = Employee.Create(FullName.Create(familiya, "Vali", null).Value).Value;
        kontekst.Add(xodim);
        await kontekst.SaveChangesAsync(Ct);
        return xodim.Id;
    }

    private async Task XodimgaBoglaAsync(Guid foydalanuvchi, Guid xodim)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await kontekst.Set<User>().SingleAsync(row => row.Id == foydalanuvchi, Ct);
        user.LinkToEmployee(xodim);
        await kontekst.SaveChangesAsync(Ct);
    }

    private HttpClient Mijoz(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<HttpClient> MijozAsync(params string[] ruxsatlar)
        => Mijoz(await TestAccounts.TokenAsync(_factory, ruxsatlar));
}
