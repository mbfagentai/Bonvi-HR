using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bonvi.Domain.Blanks;
using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// «Blankalar» — shablonlar, rollar va blankalar (SPEC-blankalar §7 #1–#11) uchi-uchiga.
/// </summary>
/// <remarks>
/// Har test o'z shabloni (tasodifiy kod) bilan ishlaydi — raqam hisoblagichi shablon bo'yicha, shuning
/// uchun raqamlar 1 dan boshlanadi va boshqa testlarga bog'liq emas. Ta'rif ichida yozilgan — seederga
/// tayanmaydi.
/// </remarks>
public sealed class BlankSubmissionApiTests : IClassFixture<BonviApiFactory>
{
    private const string Yol = "/api/blanks";

    private readonly BonviApiFactory _factory;

    public BlankSubmissionApiTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ────────────────────────────────────────────────────────────
    //  Ruxsat: har endpoint — 401 / 403 / 2xx
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Har_endpoint_tokensiz_401_ruxsatsiz_403()
    {
        var dunyo = await DunyoAsync();
        using var hr = await MijozAsync("blanks.view", "blanks.fill");
        var blanka = await YaratAsync(hr, dunyo);

        using var tokensiz = _factory.CreateClient();
        using var begona = await MijozAsync("employees.view");
        using var koruvchi = await MijozAsync("blanks.view");

        foreach (var (mijoz, kutilgan) in new[] { (tokensiz, HttpStatusCode.Unauthorized), (begona, HttpStatusCode.Forbidden) })
        {
            foreach (var (usul, yol, tana) in Endpointlar(dunyo, blanka.Id))
            {
                using var javob = await YuborAsync(mijoz, usul, yol, tana);
                javob.StatusCode.ShouldBe(kutilgan, $"{usul} {yol}");
            }
        }

        // «blanks.view» bilan — o'zgartiruvchi endpointlar yopiq (#9 — handlerda: fill ham, qadam ham yo'q).
        foreach (var (usul, yol, tana) in Endpointlar(dunyo, blanka.Id).Where(e => e.Usul != "GET"))
        {
            using var javob = await YuborAsync(koruvchi, usul, yol, tana);
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden, $"{usul} {yol}");
        }
    }

    [Fact]
    public async Task Har_endpoint_ruxsati_bilan_2xx()
    {
        var dunyo = await DunyoAsync();
        var hisob = await TestAccounts.CreateAsync(_factory, "blanks.view");
        using var koruvchi = Mijoz(hisob.AccessToken);
        using var hr = await MijozAsync("blanks.view", "blanks.fill");
        using var boshqaruvchi = await MijozAsync("blanks.view", "blanks.manage");

        // #1, #2
        var shablonlar = await koruvchi.GetFromJsonAsync<List<ShablonJavobi>>(Yol + "/templates", Ct);
        var shablon = shablonlar!.Single(row => row.Code == dunyo.Kod);
        shablon.IsActive.ShouldBeTrue();
        shablon.Version.ShouldBe("1.0");
        using (var javob = await koruvchi.GetAsync(new Uri($"{Yol}/template-versions/{shablon.VersionId}", UriKind.Relative), Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK);
            var versiya = await javob.Content.ReadFromJsonAsync<JsonElement>(Ct);
            versiya.GetProperty("definition").GetProperty("code").GetString().ShouldBe(dunyo.Kod);
        }

        // #3
        using (var javob = await boshqaruvchi.PatchAsJsonAsync(new Uri($"{Yol}/templates/{dunyo.ShablonId}", UriKind.Relative), new { isActive = false }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await javob.Content.ReadFromJsonAsync<ShablonJavobi>(Ct))!.IsActive.ShouldBeFalse();
        }

        using (var javob = await hr.PostAsJsonAsync(new Uri(Yol + "/submissions", UriKind.Relative), YaratishTanasi(dunyo), Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            (await KodAsync(javob)).ShouldBe("blanka.shablon_faol_emas");
        }

        using (var javob = await boshqaruvchi.PatchAsJsonAsync(new Uri($"{Yol}/templates/{dunyo.ShablonId}", UriKind.Relative), new { isActive = true }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        // #4, #5
        using (var javob = await boshqaruvchi.PutAsJsonAsync(new Uri(Yol + "/roles/lawyer", UriKind.Relative), new { userIds = new[] { hisob.UserId } }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var rollar = await koruvchi.GetFromJsonAsync<List<RolJavobi>>(Yol + "/roles", Ct);
        rollar!.Select(rol => rol.RoleCode).ShouldContain("hr1");
        rollar!.Single(rol => rol.RoleCode == "lawyer").Users.Select(user => user.Id).ShouldContain(hisob.UserId);

        using (var javob = await boshqaruvchi.PutAsJsonAsync(new Uri(Yol + "/roles/subjectEmployee", UriKind.Relative), new { userIds = Array.Empty<Guid>() }, Ct))
        {
            (await KodAsync(javob)).ShouldBe("blanka.rol_notogri");
        }

        using (var javob = await boshqaruvchi.PutAsJsonAsync(new Uri(Yol + "/roles/lawyer", UriKind.Relative), new { userIds = Array.Empty<Guid>() }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        // #7 → #8 → #9 → #10, #6, #11
        var blanka = await YaratAsync(hr, dunyo);
        blanka.Status.ShouldBe("Draft");
        blanka.Answers.GetProperty("employee").GetProperty("id").GetGuid().ShouldBe(dunyo.Xodim);
        blanka.Can.Edit.ShouldBeTrue();
        blanka.Can.EditableSections.ShouldBe(["hdr", "notes"]);

        var olingan = await koruvchi.GetFromJsonAsync<BlankaJavobi>($"{Yol}/submissions/{blanka.Id}", Ct);
        olingan!.Can.Edit.ShouldBeFalse();
        olingan.Missing.ShouldContain("reason");

        var saqlangan = await SaqlaAsync(hr, blanka, new { employee = Ref(blanka), reason = "Kechikish" });
        saqlangan.Version.ShouldBe(blanka.Version + 1);
        saqlangan.Missing.ShouldNotContain("reason");

        var yuborilgan = await YuborishAsync(hr, saqlangan);
        yuborilgan.Status.ShouldBe("InApproval");
        yuborilgan.Number.ShouldBe(1);
        yuborilgan.Steps.Select(step => (step.StepCode, step.State)).ShouldBe([("hr", "Open"), ("employeeSign", "Waiting")]);

        var sahifa = await koruvchi.GetFromJsonAsync<SahifaJavobi>($"{Yol}/submissions?templateCode={dunyo.Kod}", Ct);
        sahifa!.Items.Single().CurrentSteps.Single().StepCode.ShouldBe("hr");

        var ikkinchi = await YaratAsync(hr, dunyo);
        using (var javob = await hr.PostAsJsonAsync(new Uri($"{Yol}/submissions/{ikkinchi.Id}/cancel", UriKind.Relative), new { expectedVersion = ikkinchi.Version, reason = "Xato ochildi" }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await javob.Content.ReadFromJsonAsync<BlankaJavobi>(Ct))!.Status.ShouldBe("Cancelled");
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Oqim
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Bir_xil_clientRequestId_bilan_ikkinchi_yaratish_osha_blankani_200_qaytaradi()
    {
        var dunyo = await DunyoAsync();
        using var hr = await MijozAsync("blanks.view", "blanks.fill");
        var tana = YaratishTanasi(dunyo);

        using var birinchi = await hr.PostAsJsonAsync(new Uri(Yol + "/submissions", UriKind.Relative), tana, Ct);
        using var ikkinchi = await hr.PostAsJsonAsync(new Uri(Yol + "/submissions", UriKind.Relative), tana, Ct);

        birinchi.StatusCode.ShouldBe(HttpStatusCode.Created);
        ikkinchi.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ikkinchi.Content.ReadFromJsonAsync<BlankaJavobi>(Ct))!.Id
            .ShouldBe((await birinchi.Content.ReadFromJsonAsync<BlankaJavobi>(Ct))!.Id);

        var sahifa = await hr.GetFromJsonAsync<SahifaJavobi>($"{Yol}/submissions?templateCode={dunyo.Kod}", Ct);
        sahifa!.Total.ShouldBe(1);
    }

    [Fact]
    public async Task Subyekt_shablon_turiga_mos_kelmasa_400_xodim_yoq_bolsa_404()
    {
        var dunyo = await DunyoAsync();
        using var hr = await MijozAsync("blanks.view", "blanks.fill");

        using (var javob = await hr.PostAsJsonAsync(new Uri(Yol + "/submissions", UriKind.Relative), new { templateCode = dunyo.Kod, clientRequestId = Guid.NewGuid() }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await KodAsync(javob)).ShouldBe("blanka.subyekt_notogri");
        }

        using (var javob = await hr.PostAsJsonAsync(new Uri(Yol + "/submissions", UriKind.Relative), new { templateCode = dunyo.Kod, employeeId = Guid.NewGuid(), clientRequestId = Guid.NewGuid() }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await KodAsync(javob)).ShouldBe("blanka.xodim_topilmadi");
        }

        using (var javob = await hr.PostAsJsonAsync(new Uri(Yol + "/submissions", UriKind.Relative), new { templateCode = "YOQ-KOD", employeeId = dunyo.Xodim, clientRequestId = Guid.NewGuid() }, Ct))
        {
            (await KodAsync(javob)).ShouldBe("blanka.shablon_topilmadi");
        }
    }

    [Fact]
    public async Task Eskirgan_expectedVersion_409_ozgargan()
    {
        var dunyo = await DunyoAsync();
        using var hr = await MijozAsync("blanks.view", "blanks.fill");
        var blanka = await YaratAsync(hr, dunyo);
        await SaqlaAsync(hr, blanka, new { employee = Ref(blanka), reason = "Birinchi" });

        foreach (var (usul, yol, tana) in new (string, string, object)[]
                 {
                     ("PUT", $"{Yol}/submissions/{blanka.Id}/answers", new { expectedVersion = blanka.Version, answers = new { reason = "Ikkinchi" } }),
                     ("POST", $"{Yol}/submissions/{blanka.Id}/submit", new { expectedVersion = blanka.Version }),
                     ("POST", $"{Yol}/submissions/{blanka.Id}/cancel", new { expectedVersion = blanka.Version, reason = "Sabab" }),
                 })
        {
            using var javob = await YuborAsync(hr, usul, yol, tana);
            javob.StatusCode.ShouldBe(HttpStatusCode.Conflict, yol);
            (await KodAsync(javob)).ShouldBe("blanka.ozgargan");
        }
    }

    [Fact]
    public async Task Yuborishda_majburiy_maydon_bosh_bolsa_400_saqlashda_tur_xatosi_400()
    {
        var dunyo = await DunyoAsync();
        using var hr = await MijozAsync("blanks.view", "blanks.fill");
        var blanka = await YaratAsync(hr, dunyo);

        using (var javob = await hr.PostAsJsonAsync(new Uri($"{Yol}/submissions/{blanka.Id}/submit", UriKind.Relative), new { expectedVersion = blanka.Version }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await KodAsync(javob)).ShouldBe("blanka.majburiy");
        }

        using (var javob = await hr.PutAsJsonAsync(new Uri($"{Yol}/submissions/{blanka.Id}/answers", UriKind.Relative), new { expectedVersion = blanka.Version, answers = new { reason = 42 } }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await KodAsync(javob)).ShouldBe("blanka.notogri");
        }
    }

    /// <summary>Kelishuvda qoralama bo'limi yopiq — o'zgartirish 409; «open» bo'lim esa tahrirlanadi.</summary>
    [Fact]
    public async Task Kelishuvda_qoralama_bolimi_tahrirlanmaydi_open_bolim_tahrirlanadi()
    {
        var dunyo = await DunyoAsync();
        using var hr = await MijozAsync("blanks.view", "blanks.fill");
        var blanka = await YuborilganAsync(hr, dunyo);

        using (var javob = await hr.PutAsJsonAsync(
                   new Uri($"{Yol}/submissions/{blanka.Id}/answers", UriKind.Relative),
                   new { expectedVersion = blanka.Version, answers = new { employee = Ref(blanka), reason = "Boshqa sabab" } },
                   Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            (await KodAsync(javob)).ShouldBe("blanka.maydon_tahrirlanmaydi");
        }

        // HR qadamining bo'limi — «blanks.approve» siz foydalanuvchi uchun ham yopiq.
        using (var javob = await hr.PutAsJsonAsync(
                   new Uri($"{Yol}/submissions/{blanka.Id}/answers", UriKind.Relative),
                   new { expectedVersion = blanka.Version, answers = new { hrNote = "Izoh" } },
                   Ct))
        {
            (await KodAsync(javob)).ShouldBe("blanka.maydon_tahrirlanmaydi");
        }

        // Butun javoblar qaytarib yuborilsa (o'zgarmagan qismi bilan) — faqat «open» bo'lim yoziladi.
        var saqlangan = await SaqlaAsync(hr, blanka, new { employee = Ref(blanka), reason = "Kechikish", note = "Kuzatuv" });
        saqlangan.Answers.GetProperty("note").GetString().ShouldBe("Kuzatuv");
        saqlangan.Answers.GetProperty("reason").GetString().ShouldBe("Kechikish");

        // Tasdiqlovchi (hr1 — biriktirilmagan, demak har qanday «blanks.approve») o'z qadami bo'limini to'ldiradi.
        using var tasdiqlovchi = await MijozAsync("blanks.view", "blanks.approve");
        var tasdiq = await SaqlaAsync(tasdiqlovchi, saqlangan, new { hrNote = "Tekshirildi" });
        tasdiq.Answers.GetProperty("hrNote").GetString().ShouldBe("Tekshirildi");
        tasdiq.Can.ActionableSteps.Single().StepCode.ShouldBe("hr");
    }

    [Fact]
    public async Task Ikki_parallel_yuborish_ketma_ket_va_takrorlanmas_raqam_oladi()
    {
        var dunyo = await DunyoAsync();
        using var hr = await MijozAsync("blanks.view", "blanks.fill");

        var qoralamalar = new List<BlankaJavobi>();
        for (var i = 0; i < 2; i++)
        {
            var blanka = await YaratAsync(hr, dunyo);
            qoralamalar.Add(await SaqlaAsync(hr, blanka, new { employee = Ref(blanka), reason = "Sabab " + i }));
        }

        var yuborilganlar = await Task.WhenAll(qoralamalar.Select(blanka => YuborishAsync(hr, blanka)));
        yuborilganlar.Select(blanka => blanka.Number!.Value).Order().ShouldBe([1, 2]);

        var uchinchi = await YuborilganAsync(hr, dunyo);
        uchinchi.Number.ShouldBe(3);

        // Bir blankani ikki marta parallel yuborish — biri o'tadi, ikkinchisi 409; raqam bittaga oshadi.
        var bitta = await YaratAsync(hr, dunyo);
        bitta = await SaqlaAsync(hr, bitta, new { employee = Ref(bitta), reason = "Parallel" });
        var javoblar = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            hr.PostAsJsonAsync(new Uri($"{Yol}/submissions/{bitta.Id}/submit", UriKind.Relative), new { expectedVersion = bitta.Version }, Ct)));
        javoblar.Count(javob => javob.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        javoblar.Count(javob => javob.StatusCode == HttpStatusCode.Conflict).ShouldBe(1);
        var natija = await hr.GetFromJsonAsync<BlankaJavobi>($"{Yol}/submissions/{bitta.Id}", Ct);
        natija!.Number.ShouldBe(4);
        foreach (var javob in javoblar)
        {
            javob.Dispose();
        }
    }

    [Fact]
    public async Task Bekor_qilish_qoidalari()
    {
        var dunyo = await DunyoAsync();
        using var hr = await MijozAsync("blanks.view", "blanks.fill");
        using var boshqaHr = await MijozAsync("blanks.view", "blanks.fill");
        using var boshqaruvchi = await MijozAsync("blanks.view", "blanks.fill", "blanks.manage");

        var qoralama = await YaratAsync(hr, dunyo);
        using (var javob = await BekorAsync(boshqaHr, qoralama, "Begona"))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            (await KodAsync(javob)).ShouldBe("blanka.ruxsat_yoq");
        }

        using (var javob = await BekorAsync(hr, qoralama, "  "))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await KodAsync(javob)).ShouldBe("blanka.sabab_kerak");
        }

        using (var javob = await BekorAsync(boshqaruvchi, qoralama, "Boshqaruvchi yopdi"))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var kelishuvda = await YuborilganAsync(hr, dunyo);
        using (var javob = await BekorAsync(hr, kelishuvda, "O'zim"))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        BlankaJavobi bekor;
        using (var javob = await BekorAsync(boshqaruvchi, kelishuvda, "Kerak emas"))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK);
            bekor = (await javob.Content.ReadFromJsonAsync<BlankaJavobi>(Ct))!;
            bekor.Status.ShouldBe("Cancelled");
            bekor.CancelReason.ShouldBe("Kerak emas");
            bekor.Can.Cancel.ShouldBeFalse();
        }

        using (var javob = await BekorAsync(boshqaruvchi, bekor, "Yana"))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            (await KodAsync(javob)).ShouldBe("blanka.holat_notogri");
        }
    }

    [Fact]
    public async Task Royxat_filtrlari()
    {
        var dunyo = await DunyoAsync();
        using var hr = await MijozAsync("blanks.view", "blanks.fill");
        using var tasdiqlovchi = await MijozAsync("blanks.view", "blanks.approve");

        var qoralama = await YaratAsync(hr, dunyo);
        var yuborilgan = await YuborilganAsync(hr, dunyo);
        var boshqaXodim = await XodimAsync("Boshqa" + dunyo.Kod);
        var boshqasi = await YaratAsync(hr, dunyo with { Xodim = boshqaXodim });

        async Task<List<Guid>> IdlarAsync(HttpClient mijoz, string sorov)
            => (await mijoz.GetFromJsonAsync<SahifaJavobi>($"{Yol}/submissions?templateCode={dunyo.Kod}{sorov}", Ct))!
                .Items.Select(item => item.Id).ToList();

        (await IdlarAsync(hr, "")).ShouldBe([boshqasi.Id, yuborilgan.Id, qoralama.Id]);
        (await IdlarAsync(hr, "&status=InApproval")).ShouldBe([yuborilgan.Id]);
        (await IdlarAsync(hr, "&status=draft")).ShouldBe([boshqasi.Id, qoralama.Id]);
        (await IdlarAsync(hr, $"&employeeId={boshqaXodim}")).ShouldBe([boshqasi.Id]);
        (await IdlarAsync(hr, "&incomplete=true")).ShouldBe([boshqasi.Id, qoralama.Id]);
        (await IdlarAsync(hr, $"&search={yuborilgan.Number}")).ShouldBe([yuborilgan.Id]);
        (await IdlarAsync(hr, $"&search=Boshqa{dunyo.Kod}")).ShouldBe([boshqasi.Id]);
        (await IdlarAsync(tasdiqlovchi, "&waitingForMe=true")).ShouldBe([yuborilgan.Id]);
        (await IdlarAsync(hr, "&waitingForMe=true")).ShouldBeEmpty();
        (await IdlarAsync(hr, "&pageSize=1&page=2")).ShouldBe([yuborilgan.Id]);

        using var notogri = await hr.GetAsync(new Uri($"{Yol}/submissions?status=Yoq", UriKind.Relative), Ct);
        notogri.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    private sealed record Dunyo(string Kod, Guid ShablonId, Guid Xodim);

    private sealed record ShablonJavobi(Guid Id, string Code, string Title, string SubjectKind, bool IsActive, string? Version, Guid? VersionId);

    private sealed record RolFoydalanuvchi(Guid Id, string FullName);

    private sealed record RolJavobi(string RoleCode, List<RolFoydalanuvchi> Users);

    private sealed record QadamJavobi(string StepCode, string State, string ActorRole);

    private sealed record ImkonQadam(string StepCode, bool Electronic, bool InPerson);

    private sealed record ImkonJavobi(bool Edit, bool Submit, bool Cancel, List<string> EditableSections, List<ImkonQadam> ActionableSteps);

    private sealed record BlankaJavobi(
        Guid Id,
        string Status,
        int? Number,
        int Version,
        JsonElement Answers,
        List<string> Missing,
        string? CancelReason,
        List<QadamJavobi> Steps,
        ImkonJavobi Can);

    private sealed record JoriyQadam(string StepCode);

    private sealed record RoyxatQatori(Guid Id, int? Number, string Status, List<JoriyQadam> CurrentSteps);

    private sealed record SahifaJavobi(List<RoyxatQatori> Items, int Total);

    private static IEnumerable<(string Usul, string Yol, object? Tana)> Endpointlar(Dunyo dunyo, Guid blanka) =>
    [
        ("GET", $"{Yol}/templates", null),
        ("GET", $"{Yol}/template-versions/{Guid.NewGuid()}", null),
        ("PATCH", $"{Yol}/templates/{dunyo.ShablonId}", new { isActive = true }),
        ("GET", $"{Yol}/roles", null),
        ("PUT", $"{Yol}/roles/hr1", new { userIds = Array.Empty<Guid>() }),
        ("GET", $"{Yol}/submissions", null),
        ("POST", $"{Yol}/submissions", YaratishTanasi(dunyo)),
        ("GET", $"{Yol}/submissions/{blanka}", null),
        ("PUT", $"{Yol}/submissions/{blanka}/answers", new { expectedVersion = 0, answers = new { reason = "X" } }),
        ("POST", $"{Yol}/submissions/{blanka}/submit", new { expectedVersion = 0 }),
        ("POST", $"{Yol}/submissions/{blanka}/cancel", new { expectedVersion = 0, reason = "X" }),
    ];

    private static Task<HttpResponseMessage> YuborAsync(HttpClient mijoz, string usul, string yol, object? tana)
    {
        var sorov = new HttpRequestMessage(new HttpMethod(usul), new Uri(yol, UriKind.Relative));
        if (tana is not null)
        {
            sorov.Content = JsonContent.Create(tana);
        }

        return mijoz.SendAsync(sorov, Ct);
    }

    private static object YaratishTanasi(Dunyo dunyo)
        => new { templateCode = dunyo.Kod, employeeId = dunyo.Xodim, clientRequestId = Guid.NewGuid() };

    private static JsonElement Ref(BlankaJavobi blanka) => blanka.Answers.GetProperty("employee");

    private static async Task<string?> KodAsync(HttpResponseMessage javob)
        => (await javob.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("title").GetString();

    private static async Task<BlankaJavobi> YaratAsync(HttpClient mijoz, Dunyo dunyo)
    {
        using var javob = await mijoz.PostAsJsonAsync(new Uri(Yol + "/submissions", UriKind.Relative), YaratishTanasi(dunyo), Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.Created, await javob.Content.ReadAsStringAsync(Ct));
        return (await javob.Content.ReadFromJsonAsync<BlankaJavobi>(Ct))!;
    }

    private static async Task<BlankaJavobi> SaqlaAsync(HttpClient mijoz, BlankaJavobi blanka, object javoblar)
    {
        using var javob = await mijoz.PutAsJsonAsync(
            new Uri($"{Yol}/submissions/{blanka.Id}/answers", UriKind.Relative),
            new { expectedVersion = blanka.Version, answers = javoblar },
            Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await javob.Content.ReadAsStringAsync(Ct));
        return (await javob.Content.ReadFromJsonAsync<BlankaJavobi>(Ct))!;
    }

    private static async Task<BlankaJavobi> YuborishAsync(HttpClient mijoz, BlankaJavobi blanka)
    {
        using var javob = await mijoz.PostAsJsonAsync(
            new Uri($"{Yol}/submissions/{blanka.Id}/submit", UriKind.Relative),
            new { expectedVersion = blanka.Version },
            Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await javob.Content.ReadAsStringAsync(Ct));
        return (await javob.Content.ReadFromJsonAsync<BlankaJavobi>(Ct))!;
    }

    private static async Task<BlankaJavobi> YuborilganAsync(HttpClient mijoz, Dunyo dunyo)
    {
        var blanka = await YaratAsync(mijoz, dunyo);
        blanka = await SaqlaAsync(mijoz, blanka, new { employee = Ref(blanka), reason = "Kechikish" });
        return await YuborishAsync(mijoz, blanka);
    }

    private static Task<HttpResponseMessage> BekorAsync(HttpClient mijoz, BlankaJavobi blanka, string sabab)
        => mijoz.PostAsJsonAsync(
            new Uri($"{Yol}/submissions/{blanka.Id}/cancel", UriKind.Relative),
            new { expectedVersion = blanka.Version, reason = sabab },
            Ct);

    /// <summary>Kichik ta'rif: qoralama bo'limi (majburiy «reason»), HR qadami bo'limi, «open» bo'lim; 2 guruh.</summary>
    private static string Tarif(string kod) => $$"""
        { "schema": 1, "code": "{{kod}}", "version": "1.0", "subject": "employee", "title": "Sinov blankasi",
          "sections": [
            { "id": "hdr", "title": "Xodim", "fields": [
              { "id": "employee", "type": "ref", "refKind": "employee", "label": "Xodim", "prefill": "subject.fullName", "readOnly": true },
              { "id": "reason", "type": "text", "label": "Sabab", "required": true } ] },
            { "id": "hrPart", "title": "HR", "fill": { "at": "step", "step": "hr" }, "fields": [
              { "id": "hrNote", "type": "text", "label": "HR izohi", "required": true } ] },
            { "id": "notes", "title": "Izohlar", "fill": { "at": "open" }, "fields": [
              { "id": "note", "type": "longtext", "label": "Izoh" } ] } ],
          "steps": [
            { "id": "hr", "title": "HR", "order": 1, "actor": "hr1", "mode": "electronic" },
            { "id": "employeeSign", "title": "Xodim tanishdi", "order": 2, "actor": "subjectEmployee", "mode": "either" } ] }
        """;

    private async Task<Dunyo> DunyoAsync()
    {
        var kod = "T" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var shablon = BlankTemplate.Create(kod, "Sinov blankasi " + kod, BlankSubjectKind.Employee).Value;
        shablon.Activate();
        var versiya = BlankTemplateVersion.Create(shablon.Id, "1.0", Tarif(kod), new string('a', 64), BlankTemplateSource.Seed).Value;
        versiya.Publish(null, DateTimeOffset.UtcNow).IsSuccess.ShouldBeTrue();
        var xodim = Employee.Create(FullName.Create("Xodim" + kod, "Ali", null).Value).Value;

        kontekst.AddRange(shablon, versiya, xodim);
        await kontekst.SaveChangesAsync(Ct);

        return new Dunyo(kod, shablon.Id, xodim.Id);
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

    private HttpClient Mijoz(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<HttpClient> MijozAsync(params string[] ruxsatlar)
        => Mijoz(await TestAccounts.TokenAsync(_factory, ruxsatlar));
}
