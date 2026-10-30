using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Bo'lim davomati kaliti — <c>/api/public/v1/team</c> (egasi, 2026-10-30: bo'lim rahbarining boti).
/// </summary>
/// <remarks>
/// Eng qimmat xatolar: kalit o'z bo'limidan tashqariga qarashi, boshqa ommaviy yo'llarga kirishi va
/// oqimning yo'nalishni yoki kursorni noto'g'ri berishi. Shu uchtasi shu yerda qadalgan.
/// </remarks>
public sealed class PublicTeamApiTests : IClassFixture<BonviApiFactory>
{
    private static readonly TimeSpan Tashkent = TimeSpan.FromHours(5);

    private readonly BonviApiFactory _factory;

    public PublicTeamApiTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Bolim_kaliti_faqat_oz_bolimini_korishi_va_telefon_beradi()
    {
        var dunyo = await DunyoAsync();
        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        var kalit = await KalitAsync(admin, dunyo.Zavod);
        using var bot = PublicApiHarness.KalitMijozi(_factory, kalit);

        // Qamrov — zavod va sex, qo'shni bo'lim yo'q
        var qamrov = await JsonAsync(bot, "/api/public/v1/team");
        var bolimlar = qamrov.GetProperty("departments").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToList();
        bolimlar.ShouldBe([dunyo.Zavod, dunyo.Sex], ignoreOrder: true);

        // Taxta — Azo telefoni bilan, Begona yo'q
        var taxta = await JsonAsync(bot, "/api/public/v1/team/board?pageSize=200");
        var qatorlar = taxta.GetProperty("items").EnumerateArray().ToList();
        var azo = qatorlar.ShouldHaveSingleItem("faqat qamrovdagi xodim");
        azo.GetProperty("employeeId").GetGuid().ShouldBe(dunyo.Azo.Id);
        azo.GetProperty("phone").GetString().ShouldNotBeNullOrEmpty("rahbarga telefon kerak (egasi)");
        azo.GetProperty("positionName").ValueKind.ShouldNotBe(JsonValueKind.Undefined);

        // Begonaning oyi — 404 («yo'q» va «sizniki emas» bir xil)
        var (yil, oy) = (BugunToshkentda().Year, BugunToshkentda().Month);
        using (var begona = await bot.GetAsync(Yol($"/api/public/v1/team/members/{dunyo.Begona.Id}/attendance?year={yil}&month={oy}"), Ct))
        {
            begona.StatusCode.ShouldBe(HttpStatusCode.NotFound, await PublicApiHarness.MatnAsync(begona));
        }

        var karta = await JsonAsync(bot, $"/api/public/v1/team/members/{dunyo.Azo.Id}/attendance?year={yil}&month={oy}");
        karta.GetProperty("employee").GetProperty("phone").GetString().ShouldNotBeNullOrEmpty();

        // Qo'shni bo'lim bilan toraytirish — 404
        using var torayish = await bot.GetAsync(Yol($"/api/public/v1/team/board?departmentId={dunyo.Qoshni}"), Ct);
        torayish.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Shaxsiy maydonlar umuman yo'q
        var matn = taxta.GetRawText() + karta.GetRawText();
        foreach (var taqiq in new[] { "pinfl", "passport", "salary", "photoKey", "birthDate", "address" })
        {
            matn.Contains($"\"{taqiq}\"", StringComparison.OrdinalIgnoreCase).ShouldBeFalse(taqiq);
        }
    }

    [Fact]
    public async Task Bolim_kaliti_boshqa_yollarga_kirmaydi_oddiy_kalit_teamga_kirmaydi()
    {
        var dunyo = await DunyoAsync();
        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);

        using var bot = PublicApiHarness.KalitMijozi(_factory, await KalitAsync(admin, dunyo.Zavod));
        foreach (var yol in new[] { "/api/public/v1/employees", "/api/public/v1/ping", "/api/public/v1/reference/departments" })
        {
            using var javob = await bot.GetAsync(Yol(yol), Ct);
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden, yol);
        }

        var oddiy = await PublicApiHarness.KalitYaratAsync(admin, PublicApiHarness.NoyobNom("Oddiy"));
        using var holding = PublicApiHarness.KalitMijozi(_factory, oddiy.Key);
        using var team = await holding.GetAsync(Yol("/api/public/v1/team/board"), Ct);
        team.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "Basic kalit bo'lim davomatini ko'rmaydi");

        using var kalitsiz = _factory.CreateClient();
        using var anonim = await kalitsiz.GetAsync(Yol("/api/public/v1/team"), Ct);
        anonim.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Kalit_yaratish_bolim_va_ruxsat_talab_qiladi()
    {
        var dunyo = await DunyoAsync();
        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);

        using (var bolimsiz = await admin.PostAsJsonAsync(
                   Yol("/api/api-keys"),
                   new { name = PublicApiHarness.NoyobNom("Bot"), scope = "DepartmentAttendance" },
                   Ct))
        {
            bolimsiz.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await PublicApiHarness.MatnAsync(bolimsiz)).ShouldContain("tashqi.bolim_kerak");
        }

        using (var yoq = await admin.PostAsJsonAsync(
                   Yol("/api/api-keys"),
                   new { name = PublicApiHarness.NoyobNom("Bot"), scope = "DepartmentAttendance", departmentId = Guid.NewGuid() },
                   Ct))
        {
            yoq.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        // Faqat sozlamalar huquqi — davomat va xodimlarni ko'rmaydigan odam bunday kalit bera olmaydi
        using var sozlovchi = _factory.CreateClient();
        sozlovchi.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await TestAccounts.TokenAsync(_factory, "settings.manage"));

        using var ruxsatsiz = await sozlovchi.PostAsJsonAsync(
            Yol("/api/api-keys"),
            new { name = PublicApiHarness.NoyobNom("Bot"), scope = "DepartmentAttendance", departmentId = dunyo.Zavod },
            Ct);
        ruxsatsiz.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await PublicApiHarness.MatnAsync(ruxsatsiz)).ShouldContain("tashqi.bolim_ruxsat_yoq");

        // Ro'yxatda bo'lim nomi ko'rinadi
        await KalitAsync(admin, dunyo.Zavod);
        var royxat = await admin.GetFromJsonAsync<JsonElement>(Yol("/api/api-keys"), Ct);
        royxat.EnumerateArray().ShouldContain(item =>
            item.GetProperty("departmentId").ValueKind == JsonValueKind.String
            && item.GetProperty("departmentId").GetGuid() == dunyo.Zavod
            && item.GetProperty("departmentName").GetString() == dunyo.ZavodNomi);
    }

    [Fact]
    public async Task Otishlar_oqimi_faqat_qamrovdagilar_samarali_yonalish_va_kursor()
    {
        var dunyo = await DunyoAsync(otishlar: true);
        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        using var bot = PublicApiHarness.KalitMijozi(_factory, await KalitAsync(admin, dunyo.Zavod));

        // Birinchi so'rov — bugun boshidan; boshqa testlarning o'tishlari ham bo'lishi mumkin, shuning uchun
        // kursor oxirigacha yuriladi va faqat shu dunyoning xodimlari olinadi
        var mening = new List<JsonElement>();
        string? kursor = null;

        for (var aylanish = 0; aylanish < 50; aylanish++)
        {
            var sahifa = await JsonAsync(bot, "/api/public/v1/team/passes" + (kursor is null ? string.Empty : "?after=" + kursor));
            mening.AddRange(sahifa.GetProperty("items").EnumerateArray());
            kursor = sahifa.GetProperty("next").GetString();
            if (!sahifa.GetProperty("hasMore").GetBoolean())
            {
                break;
            }
        }

        mening.ShouldAllBe(item => item.GetProperty("employeeId").GetGuid() != dunyo.Begona.Id, "qo'shni bo'lim oqimga tushmaydi");

        var azo = mening.Where(item => item.GetProperty("employeeId").GetGuid() == dunyo.Azo.Id)
            .OrderBy(item => item.GetProperty("at").GetDateTimeOffset())
            .ToList();
        azo.Count.ShouldBe(3);
        azo.Select(item => item.GetProperty("direction").GetString()).ShouldBe(["In", "In", "Out"],
            "noma'lum terminal: 5 soniyalik takror birinchisining yo'nalishida, kechqurungisi chiqish");
        azo[0].GetProperty("fullName").GetString().ShouldBe(dunyo.Azo.FullName);
        azo[0].GetProperty("departmentName").GetString().ShouldBe(dunyo.SexNomi);

        // Kursor bilan qayta so'rash — yangi o'tish yo'q
        var keyingi = await JsonAsync(bot, "/api/public/v1/team/passes?after=" + kursor);
        keyingi.GetProperty("items").EnumerateArray()
            .ShouldNotContain(item => item.GetProperty("employeeId").GetGuid() == dunyo.Azo.Id);

        using var buzuq = await bot.GetAsync(Yol("/api/public/v1/team/passes?after=abc"), Ct);
        buzuq.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await PublicApiHarness.MatnAsync(buzuq)).ShouldContain("tashqi.kursor_notogri");
    }

    // ════════════════════════════════════════════════════════════
    //  Yordamchilar
    // ════════════════════════════════════════════════════════════

    private sealed record Odam(Guid Id, string FullName);

    /// <summary>Zavod → Sex (Azo); Qoshni (Begona). Zavodning o'zida xodim yo'q.</summary>
    private sealed record Dunyo(Guid Zavod, string ZavodNomi, Guid Sex, string SexNomi, Guid Qoshni, Odam Azo, Odam Begona);

    private async Task<Dunyo> DunyoAsync(bool otishlar = false)
    {
        var belgi = "BK" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var firma = LegalEntity.Create("Bot MCHJ " + belgi).Value;
        var zavod = Department.Create("Zavod " + belgi).Value;
        var sex = Department.Create("Sex " + belgi, zavod.Id).Value;
        var qoshni = Department.Create("Qoshni " + belgi).Value;
        kontekst.AddRange(firma, zavod, sex, qoshni);

        Odam Ishga(string familiya, Department bolim)
        {
            var ism = FullName.Create(familiya + belgi, "Bot", null).Value;
            var xodim = Employee.Create(ism).Value;
            xodim.SetPhones(PhoneNumber.Create("+99890" + RandomNumberGenerator.GetInt32(1_000_000, 9_999_999).ToString(CultureInfo.InvariantCulture)).Value);
            kontekst.AddRange(xodim, Employment.Hire(xodim.Id, firma.Id, new DateOnly(2025, 1, 1), bolim.Id).Value);
            return new Odam(xodim.Id, ism.Full);
        }

        var azo = Ishga("Azo", sex);
        var begona = Ishga("Begona", qoshni);

        if (otishlar)
        {
            var qurilma = AttendanceDevice.Register("Bot turniketi " + belgi, RandomNumberGenerator.GetBytes(32), "tst00000", DateTimeOffset.UtcNow).Value;
            var bugun = BugunToshkentda();
            var hozir = DateTimeOffset.UtcNow;

            AttendancePass Otish(Guid xodimId, TimeOnly vaqt)
            {
                var lahza = new DateTimeOffset(bugun.ToDateTime(vaqt), Tashkent);
                var otish = AttendancePass.Record(
                    qurilma.Id,
                    AttendanceIdentityProvider.Verifix,
                    $"{belgi}-{xodimId:N}",
                    lahza,
                    hozir,
                    Guid.NewGuid().ToString("N"),
                    PassDirection.Unknown,
                    DirectionSource.Unknown,
                    PassMethod.Face).Value;

                otish.Resolve(xodimId, hozir);
                return otish;
            }

            kontekst.AddRange(
                qurilma,
                Otish(azo.Id, new TimeOnly(8, 0, 0)),
                Otish(azo.Id, new TimeOnly(8, 0, 5)),
                Otish(azo.Id, new TimeOnly(17, 0, 0)),
                Otish(begona.Id, new TimeOnly(8, 10, 0)));
        }

        await kontekst.SaveChangesAsync(Ct);
        await qamrov.ServiceProvider.GetRequiredService<IDepartmentRepository>().RebuildHierarchyAsync(Ct);

        return new Dunyo(zavod.Id, zavod.Name, sex.Id, sex.Name, qoshni.Id, azo, begona);
    }

    private static async Task<string> KalitAsync(HttpClient admin, Guid bolim)
    {
        using var javob = await admin.PostAsJsonAsync(
            Yol("/api/api-keys"),
            new { name = PublicApiHarness.NoyobNom("Rahbar boti"), scope = "DepartmentAttendance", departmentId = bolim },
            Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await PublicApiHarness.MatnAsync(javob));

        var tana = await javob.Content.ReadFromJsonAsync<JsonElement>(Ct);
        tana.GetProperty("scope").GetString().ShouldBe("DepartmentAttendance");
        tana.GetProperty("departmentId").GetGuid().ShouldBe(bolim);

        return tana.GetProperty("key").GetString()!;
    }

    private static async Task<JsonElement> JsonAsync(HttpClient client, string yol)
    {
        using var javob = await client.GetAsync(Yol(yol), Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, $"{yol}: {await PublicApiHarness.MatnAsync(javob)}");
        return await javob.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static Uri Yol(string yol) => new(yol, UriKind.Relative);

    private static DateOnly BugunToshkentda() => DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(Tashkent).DateTime);
}
