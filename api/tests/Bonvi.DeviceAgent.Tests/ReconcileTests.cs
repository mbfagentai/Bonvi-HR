using System.Net;
using Bonvi.DeviceAgent.Bonvi;
using Bonvi.DeviceAgent.Hikvision;

namespace Bonvi.DeviceAgent.Tests;

/// <summary>
/// Ro'yxat halqasi — solishtirish, o'chirish, hisobot va to'siqlar
/// (2026-09-27 audit topilmalari).
/// </summary>
/// <remarks>
/// Har bir test bitta topilmani qadaydi: tuzatish qaytarilsa, o'sha test
/// yiqilishi kerak.
/// </remarks>
public sealed class ReconcileTests
{
    private static readonly byte[] Face = [0xFF, 0xD8, 0xFF, 0xE0, 0, 1, 2, 3];

    /// <summary>
    /// Bitta xodimning rasmi yuklanmasa (server 503) — qolganlari yoziladi,
    /// o'chirish bajariladi va hisobot yetib boradi (topilma 1).
    /// </summary>
    [Fact]
    public async Task Bitta_rasm_yuklanmasa_qolganlari_yoziladi_va_ochirish_bajariladi()
    {
        using var stand = new AgentHarness();

        var entries = People(5);
        stand.Bonvi.PhotoFailures.Add(entries[2].EmployeeId);
        stand.Terminal.Persons.Add("900000999");

        Setup(stand, entries, remove: ["900000999"]);

        await stand.ReconcileAsync();

        stand.Terminal.Deletes.ShouldBe(["900000999"]);
        stand.Terminal.FaceWrites.Count.ShouldBe(4);

        var items = stand.Bonvi.ReportItems.ToList();
        items.Count.ShouldBe(6, "4 ta yozildi, 1 ta xato, 1 ta o'chirish");

        var failed = items.Single(item => item.GetProperty("personNo").GetString() == entries[2].PersonNo);
        failed.GetProperty("ok").GetBoolean().ShouldBeFalse();
        failed.GetProperty("rejected").GetBoolean().ShouldBeFalse("tarmoq xatosi vaqtinchalik — keyingi safar qayta urinadi");

        stand.Bonvi.Statuses.ShouldBe([(0, 0)]);
    }

    /// <summary>
    /// Server umuman javob bermay qolsa, aylanish to'xtaydi — lekin shu
    /// paytgacha bajarilgan ishning hisoboti baribir yuboriladi (topilma 1).
    /// </summary>
    [Fact]
    public async Task Ketma_ket_xatoda_aylanish_toxtaydi_lekin_bajarilgani_hisobotga_tushadi()
    {
        using var stand = new AgentHarness();

        var entries = People(10);
        foreach (var entry in entries.Skip(3))
        {
            stand.Bonvi.PhotoFailures.Add(entry.EmployeeId);
        }

        Setup(stand, entries);

        await Should.ThrowAsync<HttpRequestException>(stand.ReconcileAsync);

        stand.Bonvi.PhotoRequests.ShouldBe(6, "3 tasi yozildi, keyin 3 ta ketma-ket xato — to'xtadi");

        var items = stand.Bonvi.ReportItems.ToList();
        items.Count(item => item.GetProperty("ok").GetBoolean()).ShouldBe(3);
        items.Count(item => !item.GetProperty("ok").GetBoolean()).ShouldBe(3);
    }

    /// <summary>Katta ro'yxatda hisobot bo'lak-bo'lak ketadi — oxirini kutmaydi (topilma 1).</summary>
    [Fact]
    public async Task Hisobot_har_20_qatorda_yuboriladi()
    {
        using var stand = new AgentHarness();

        Setup(stand, People(45));

        await stand.ReconcileAsync();

        stand.Bonvi.Reports.Select(report => report.Count).ShouldBe([20, 20, 5]);
    }

    /// <summary>
    /// O'chirib bo'lmasa — hisobotda «bajarilmadi», «bajarildi» EMAS (topilma 5).
    /// </summary>
    [Fact]
    public async Task Ochirib_bolmasa_hisobotda_muvaffaqiyatsiz()
    {
        using var stand = new AgentHarness();

        stand.Terminal.Persons.AddRange(["900000001", "900000002", "900000003", "900000004", "900000005", "900000006"]);
        stand.Terminal.RejectDeleteFor.Add("900000006");

        Setup(
            stand,
            [.. People(5).Select(entry => entry with { Force = false })],
            remove: ["900000006"]);

        await stand.ReconcileAsync();

        var removal = stand.Bonvi.ReportItems.Single(item => item.GetProperty("action").GetString() == "Remove");
        removal.GetProperty("personNo").GetString().ShouldBe("900000006");
        removal.GetProperty("ok").GetBoolean().ShouldBeFalse();
        removal.GetProperty("error").GetString().ShouldNotBeNullOrWhiteSpace();

        stand.Terminal.Persons.ShouldContain("900000006");
    }

    /// <summary>
    /// Rasm juda katta — terminalga UMUMAN tegilmaydi va xato qat'iy deb
    /// yuboriladi (topilma 6).
    /// </summary>
    [Fact]
    public async Task Rasm_juda_katta_bolsa_terminalga_tegilmaydi_va_xato_qatiy()
    {
        using var stand = new AgentHarness();

        var entry = People(1)[0];
        stand.Bonvi.Photos[entry.EmployeeId] = new byte[300 * 1024];
        Setup(stand, [entry]);

        await stand.ReconcileAsync();

        stand.Terminal.PersonWrites.ShouldBeEmpty("rasm tekshirilmasdan odam yozilsa, terminalda yuzsiz odam qolardi");

        var item = stand.Bonvi.ReportItems.Single();
        item.GetProperty("ok").GetBoolean().ShouldBeFalse();
        item.GetProperty("rejected").GetBoolean().ShouldBeTrue();
    }

    /// <summary>Terminal yuzni rad etsa — xato QAT'IY (keyingi safar ushlab turiladi).</summary>
    [Fact]
    public async Task Terminal_yuzni_rad_etsa_xato_qatiy()
    {
        using var stand = new AgentHarness();

        var entry = People(1)[0];
        stand.Terminal.RejectFaceFor.Add(entry.PersonNo);
        Setup(stand, [entry]);

        await stand.ReconcileAsync();

        var item = stand.Bonvi.ReportItems.Single();
        item.GetProperty("ok").GetBoolean().ShouldBeFalse();
        item.GetProperty("rejected").GetBoolean().ShouldBeTrue();
        item.GetProperty("error").GetString()!.ShouldContain("face quality");
    }

    /// <summary>
    /// «Ushlab tur» qatori — yozilmaydi (hatto terminalda yo'q bo'lsa ham) va
    /// o'chirilmaydi ham (topilma 6).
    /// </summary>
    [Fact]
    public async Task Ushlab_turilgan_qator_yozilmaydi_va_ochirilmaydi()
    {
        using var stand = new AgentHarness();

        var held = People(1)[0] with { Hold = true, Force = false };
        stand.Terminal.Persons.Add(held.PersonNo);
        stand.Terminal.WithoutFace.Add(held.PersonNo);

        Setup(stand, [held], remove: []);

        await stand.ReconcileAsync();

        stand.Bonvi.PhotoRequests.ShouldBe(0);
        stand.Terminal.PersonWrites.ShouldBeEmpty();
        stand.Terminal.Deletes.ShouldBeEmpty();
        stand.Bonvi.ReportItems.ShouldBeEmpty();
    }

    /// <summary>
    /// Bo'sh ro'yxat hech kimni o'chirmaydi — to'siq ushlab qoladi va HR ga
    /// soni aytiladi (topilma 8).
    /// </summary>
    [Fact]
    public async Task Bosh_royxat_hech_kimni_ochirmaydi()
    {
        using var stand = new AgentHarness();

        stand.Terminal.Persons.AddRange(["900000001", "900000002"]);
        Setup(stand, [], remove: ["900000001", "900000002"]);

        await stand.ReconcileAsync();

        stand.Terminal.Deletes.ShouldBeEmpty();
        stand.Bonvi.Statuses.ShouldBe([(2, 0)]);
    }

    /// <summary>
    /// Terminaldagi Bonvi odamlarining beshdan biridan ko'pi o'chishi kerak
    /// bo'lsa — ushlab qolinadi; HR tasdiqlasa — bajariladi (topilma 8).
    /// </summary>
    [Fact]
    public async Task Ommaviy_ochirish_ushlab_qolinadi_HR_tasdiqlasa_bajariladi()
    {
        using var stand = new AgentHarness();

        var keep = People(10).Select(entry => entry with { Force = false }).ToList();
        var gone = Enumerable.Range(100, 5).Select(index => $"9{index:D8}").ToList();

        stand.Terminal.Persons.AddRange(keep.Select(entry => entry.PersonNo));
        stand.Terminal.Persons.AddRange(gone);

        Setup(stand, keep, remove: gone);
        await stand.ReconcileAsync();

        stand.Terminal.Deletes.ShouldBeEmpty("15 tadan 5 tasi — beshdan biridan ko'p");
        stand.Bonvi.Statuses.Last().ShouldBe((5, 0));

        Setup(stand, keep, remove: gone, allowMassRemoval: true);
        await stand.ReconcileAsync();

        stand.Terminal.Deletes.Count.ShouldBe(5);
        stand.Bonvi.Statuses.Last().ShouldBe((0, 0));
    }

    /// <summary>
    /// Bonvi fazasidagi, lekin bu server ajratmagan odam — BEGONA: o'chirilmaydi,
    /// soni aytiladi (ikki server bitta terminalda, topilma 8).
    /// </summary>
    [Fact]
    public async Task Begona_Bonvi_raqamiga_tegilmaydi()
    {
        using var stand = new AgentHarness();

        var mine = People(5).Select(entry => entry with { Force = false }).ToList();
        stand.Terminal.Persons.AddRange(mine.Select(entry => entry.PersonNo));
        stand.Terminal.Persons.AddRange(["955555555", "900000777", "17"]);

        Setup(stand, mine, remove: ["900000777"]);

        await stand.ReconcileAsync();

        stand.Terminal.Deletes.ShouldBe(["900000777"]);
        stand.Terminal.Persons.ShouldContain("955555555");
        stand.Terminal.Persons.ShouldContain("17", "Verifix odami");
        stand.Bonvi.Statuses.ShouldBe([(0, 1)]);
    }

    /// <summary>Har so'rov terminal seriyasi bilan ketadi — server tokenni shunga bog'laydi (topilma 8).</summary>
    [Fact]
    public async Task Royxat_va_hisobot_terminal_seriyasi_bilan_ketadi()
    {
        using var stand = new AgentHarness();
        Setup(stand, People(1));

        await stand.ReconcileAsync();

        stand.Bonvi.Serials
            .Where(entry => entry.Path is "/api/attendance/agent/roster" or "/api/attendance/agent/report" or "/api/attendance/agent/status")
            .ShouldAllBe(entry => entry.Serial == stand.Terminal.Serial);
    }

    /// <summary>Terminal javob bermasa — hech narsa yozilmaydi va o'chirilmaydi, ro'yxat ham so'ralmaydi.</summary>
    [Fact]
    public async Task Terminal_javob_bermasa_aylanish_boshida_toxtaydi()
    {
        using var stand = new AgentHarness();
        stand.Terminal.Offline = true;

        await Should.ThrowAsync<HttpRequestException>(stand.ReconcileAsync);

        stand.Bonvi.Serials.ShouldBeEmpty();
    }

    /// <summary>Terminal paroli rad etilsa — istisno halqaga ketadi (uzoq kutish uchun), xodim xatosi emas.</summary>
    [Fact]
    public async Task Terminal_paroli_rad_etilsa_halqa_uzoq_kutadi()
    {
        using var stand = new AgentHarness();
        stand.Terminal.Unauthorized = true;

        var failure = await Should.ThrowAsync<HikvisionException>(stand.ReconcileAsync);

        AgentWorker.RetryDelay(TimeSpan.FromSeconds(60), 1, failure).ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMinutes(10));
    }

    // ────────────────────────────────────────────────────────────
    //  Kutish (topilma 7)
    // ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 8)]
    [InlineData(5, 32)]
    [InlineData(6, 60)]
    [InlineData(50, 60)]
    public void Xatodan_keyingi_kutish_osib_boradi_va_60_soniyada_toxtaydi(int failures, int seconds)
        => AgentWorker.RetryDelay(TimeSpan.FromSeconds(2), failures, new HttpRequestException("x"))
            .ShouldBe(TimeSpan.FromSeconds(seconds));

    [Fact]
    public void Tasdiqlash_xatolarida_kamida_10_daqiqa_kutadi()
    {
        var interval = TimeSpan.FromSeconds(2);

        AgentWorker.RetryDelay(interval, 1, new HikvisionException(401, "")).ShouldBe(AgentWorker.AuthBackoff);
        AgentWorker.RetryDelay(interval, 1, new HttpRequestException("x", null, HttpStatusCode.Unauthorized)).ShouldBe(AgentWorker.AuthBackoff);
        AgentWorker.RetryDelay(interval, 1, new BonviApiException(HttpStatusCode.Conflict, "davomat.terminal_mos_emas")).ShouldBe(AgentWorker.AuthBackoff);

        // Raqam ajratish poygasi — o'zi tuzaladi, tez qayta urinadi.
        AgentWorker.RetryDelay(interval, 1, new BonviApiException(HttpStatusCode.Conflict, "davomat.agent_royxat_band")).ShouldBe(interval);

        AgentWorker.AuthBackoff.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMinutes(10));
    }

    [Theory]
    [InlineData(0, 3, 10, true)]   // bo'sh ro'yxat
    [InlineData(5, 0, 10, false)]  // o'chiradigan narsa yo'q
    [InlineData(100, 20, 120, false)]
    [InlineData(100, 30, 120, true)]
    [InlineData(3, 3, 4, false)]   // kichik terminal — pastki chegara
    [InlineData(3, 4, 7, true)]
    public void Ommaviy_ochirish_tosigi(int roster, int removals, int managed, bool hold)
        => AgentWorker.ShouldHoldRemovals(roster, removals, managed).ShouldBe(hold);

    /// <summary>
    /// Terminalda xodimning o'z yozuvi (iVMS raqami) YUZI bilan turibdi — ikkinchi
    /// yuz yozilmaydi, hisobot «Covered» va o'sha raqam bilan (SPEC-agent §M3, §M4).
    /// </summary>
    [Fact]
    public async Task Terminalning_oz_yozuvi_bor_xodim_yozilmaydi_va_Covered_deb_aytiladi()
    {
        using var stand = new AgentHarness();

        var covered = new Entry(Guid.NewGuid(), "900000001", Aliases: ["3092"]);
        var plain = new Entry(Guid.NewGuid(), "900000002");
        stand.Terminal.Persons.Add("3092");

        Setup(stand, [covered, plain]);

        await stand.ReconcileAsync();

        stand.Terminal.PersonWrites.ShouldBe(["900000002"], "iVMS yozuvi bor odamga Bonvi raqami yozilmaydi");
        stand.Terminal.Persons.ShouldContain("3092", "boshqa dasturning yozuviga tegilmaydi");

        var item = stand.Bonvi.ReportItems.Single(row => row.GetProperty("employeeId").GetGuid() == covered.EmployeeId);
        item.GetProperty("action").GetString().ShouldBe("Covered");
        item.GetProperty("personNo").GetString().ShouldBe("3092");
        item.GetProperty("ok").GetBoolean().ShouldBeTrue();
        item.GetProperty("version").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>Server allaqachon biladi (belgi joriy) — har daqiqada qayta aytilmaydi (§M4).</summary>
    [Fact]
    public async Task Terminalning_oz_yozuvi_server_bilsa_hisobot_yuborilmaydi()
    {
        using var stand = new AgentHarness();

        stand.Terminal.Persons.Add("3092");
        Setup(stand, [new Entry(Guid.NewGuid(), "900000001", Force: false, Aliases: ["3092"])]);

        await stand.ReconcileAsync();

        stand.Terminal.PersonWrites.ShouldBeEmpty();
        stand.Bonvi.ReportItems.ShouldBeEmpty();
    }

    /// <summary>
    /// Ilgari Bonvi rasmi rad etilgan (<c>hold</c>) — iVMS yozuvi topilgach holat
    /// «Covered» ga o'tadi, «Xato» da qolib ketmaydi.
    /// </summary>
    [Fact]
    public async Task Rad_etilgan_xodimning_oz_yozuvi_topilsa_Covered_deb_aytiladi()
    {
        using var stand = new AgentHarness();

        stand.Terminal.Persons.Add("3092");
        Setup(stand, [new Entry(Guid.NewGuid(), "900000001", Force: false, Hold: true, Aliases: ["3092"])]);

        await stand.ReconcileAsync();

        stand.Bonvi.ReportItems.Single().GetProperty("action").GetString().ShouldBe("Covered");
    }

    /// <summary>
    /// O'z yozuvi YUZSIZ yoki bu terminalda yo'q — Bonvi o'z raqami va rasmi bilan
    /// yozadi (§M3, §M6): yuzsiz odam yuz bilan o'ta olmaydi, ikki yuz to'qnashuvi ham yo'q.
    /// </summary>
    [Fact]
    public async Task Oz_yozuvi_yuzsiz_yoki_yoq_bolsa_Bonvi_yozadi()
    {
        using var stand = new AgentHarness();

        var faceless = new Entry(Guid.NewGuid(), "900000001", Aliases: ["3092"]);
        var elsewhere = new Entry(Guid.NewGuid(), "900000002", Aliases: ["3093"]);
        stand.Terminal.Persons.Add("3092");
        stand.Terminal.WithoutFace.Add("3092");

        Setup(stand, [faceless, elsewhere]);

        await stand.ReconcileAsync();

        stand.Terminal.PersonWrites.ShouldBe(["900000001", "900000002"], ignoreOrder: true);
        stand.Bonvi.ReportItems.ShouldAllBe(row => row.GetProperty("action").GetString() == "Upsert");
    }

    [Fact]
    public void Oz_yozuvi_faqat_yuzi_bilan_hisoblanadi()
    {
        var entry = new RosterEntry(Guid.NewGuid(), "900000001", "Sinov", "p", "v", Force: true, AliasNumbers: ["3092", "3093"]);

        AgentWorker.CoveredBy(entry, new Dictionary<string, DevicePerson>()).ShouldBeNull();
        AgentWorker.CoveredBy(entry, new Dictionary<string, DevicePerson> { ["3092"] = new("3092", null, 0) }).ShouldBeNull();
        AgentWorker.CoveredBy(entry, new Dictionary<string, DevicePerson> { ["3093"] = new("3093", null, null) }).ShouldBe("3093");
        AgentWorker.CoveredBy(entry with { AliasNumbers = null }, new Dictionary<string, DevicePerson> { ["3092"] = new("3092", null, 1) })
            .ShouldBeNull("eski server raqam bermaydi — eski xatti-harakat");
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    private sealed record Entry(
        Guid EmployeeId,
        string PersonNo,
        bool Force = true,
        bool Hold = false,
        IReadOnlyList<string>? Aliases = null);

    private static List<Entry> People(int count)
        => [.. Enumerable.Range(1, count).Select(index => new Entry(Guid.NewGuid(), $"9{index:D8}"))];

    /// <summary>Ro'yxatni va (hali berilmagan) rasmlarni soxta serverga qo'yadi.</summary>
    private static void Setup(
        AgentHarness stand,
        IReadOnlyList<Entry> entries,
        IReadOnlyList<string>? remove = null,
        bool allowMassRemoval = false)
    {
        foreach (var entry in entries)
        {
            stand.Bonvi.Photos.TryAdd(entry.EmployeeId, Face);
        }

        stand.Bonvi.Roster = new Dictionary<string, object?>
        {
            ["deviceId"] = Guid.NewGuid(),
            ["deviceName"] = "sinov",
            ["generatedAt"] = DateTimeOffset.UnixEpoch,
            ["entries"] = entries.Select(entry => new Dictionary<string, object?>
            {
                ["employeeId"] = entry.EmployeeId,
                ["personNo"] = entry.PersonNo,
                ["fullName"] = "Sinov " + entry.PersonNo,
                ["photoVersion"] = "employees/x.jpg",
                ["version"] = "employees/x.jpg:0:abcd1234",
                ["force"] = entry.Force,
                ["hold"] = entry.Hold,
                ["aliasNumbers"] = entry.Aliases,
            }).ToList(),
            ["removeNumbers"] = remove,
            ["allowMassRemoval"] = allowMassRemoval,
        };
    }
}
