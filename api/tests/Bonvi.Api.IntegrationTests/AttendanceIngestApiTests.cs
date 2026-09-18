using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bonvi.Api.Endpoints;
using Bonvi.Api.Extensions;
using Bonvi.Application.Attendance.Identities;
using Bonvi.Application.Attendance.Ingest;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Events;
using Serilog.Parsing;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Qabul javobi (test uchun).</summary>
public sealed record IngestResponse(bool Ok, int Stored, int Duplicates);

/// <summary>
/// Qabul testlari uchun alohida xost — o'z vaqtinchalik baza va O'Z fayl papkasi bilan.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega umumiy <see cref="BonviApiFactory"/> yetmaydi.</b> Uning fayl
/// ildizi (<c>bonvi-test-uploads</c>) barcha test sinflari uchun bitta va
/// sinflar parallel ishlaydi. «Takror yuborishda disk'da AYNAN bitta fayl
/// qoladi» degan tekshiruv u yerda boshqa sinfning rasm yuklashi bilan
/// to'qnashib, goh o'tib, goh yiqilardi. Bu yerda papka faqat shu sinfniki,
/// sinf ichidagi testlar esa ketma-ket ishlaydi — sanoq aniq.
/// </para>
/// </remarks>
public sealed class AttendanceIngestHost : IAsyncLifetime
{
    private readonly BonviApiFactory _baza = new();

    /// <summary>Faqat shu sinfga tegishli fayl ildizi.</summary>
    public string StorageRoot { get; } = Path.Combine(
        Path.GetTempPath(),
        "bonvi-ingest-test-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..12]);

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    /// <summary>Soati qotirilgan xostning «hozir»i: 2026-09-16, Toshkent 17:00 (UTC 12:00).</summary>
    public static readonly DateTimeOffset QotirilganHozir = new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Xuddi shu baza va fayl ildizi, lekin <see cref="IClock"/> <see cref="QotirilganHozir"/> ga QOTIRILGAN.
    /// </summary>
    /// <remarks>
    /// Qabul hodisa vaqtini server soati bilan solishtiradi (±30 kun, <c>clock_skew_seconds</c>,
    /// <c>received_at</c>). Tizim soatiga tayangan test «10 daqiqa og'ish ≈ 600» deb taxmin bilan
    /// yozilardi va yarim tunda sana chegarasini kesib o'tishi mumkin edi. Bu xostda og'ish AYNAN
    /// 600, 30 kunlik chegara soniyasigacha aniq, sana esa test ishga tushgan kunga bog'liq emas.
    /// </remarks>
    public WebApplicationFactory<Program> SoatiQotirilgan { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _baza.InitializeAsync().ConfigureAwait(false);

        Factory = _baza.WithWebHostBuilder(builder => builder.UseSetting("Storage:RootPath", StorageRoot));

        SoatiQotirilgan = _baza.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Storage:RootPath", StorageRoot);
            builder.ConfigureTestServices(services => services.AddSingleton<IClock>(new QotirilganSoat(QotirilganHozir)));
        });
    }

    /// <summary>Qotirilgan soat — <c>AttendanceReadModelTests</c> dagi bilan bir xil shakl.</summary>
    private sealed class QotirilganSoat(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;

        public DateOnly Today => utcNow.TashkentDate();
    }

    public async ValueTask DisposeAsync()
    {
        // Hosil qilingan xost asosiy fabrika bilan birga yopiladi.
        await _baza.DisposeAsync().ConfigureAwait(false);

        if (Directory.Exists(StorageRoot))
        {
            Directory.Delete(StorageRoot, recursive: true);
        }
    }

    /// <summary>Ildiz ostidagi fayllar soni.</summary>
    public int FileCount()
        => Directory.Exists(StorageRoot)
            ? Directory.GetFiles(StorageRoot, "*", SearchOption.AllDirectories).Length
            : 0;
}

/// <summary>
/// Turniket/yuz terminallari hodisalarini qabul qilish — uchi-uchiga (SPEC §3, C3 nazorat nuqtasi).
/// </summary>
/// <remarks>
/// <para>
/// Qurilmani boshqarish endpointlari hali yo'q, shu sababli qurilma va
/// moslik to'g'ridan-to'g'ri bazaga yoziladi — token esa xuddi haqiqiy
/// ro'yxatga olishdagidek <see cref="AttendanceDeviceToken"/> orqali
/// yasaladi va xeshlanadi. Ya'ni «yozish tomoni bilan tekshirish tomoni bir
/// xil xeshlaydi» degan shart ham shu yerda mahkamlanadi.
/// </para>
/// </remarks>
public sealed class AttendanceIngestApiTests : IClassFixture<AttendanceIngestHost>
{
    private readonly AttendanceIngestHost _host;

    public AttendanceIngestApiTests(AttendanceIngestHost host) => _host = host;

    // ────────────────────────────────────────────────────────────
    //  C3 — to'rt asosiy xatti-harakat
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Haqiqiy shakldagi Hikvision multipart (JSON qismi + JPEG qismi) → 200,
    /// bitta qator, bitta surat fayli, maydonlar to'g'ri.
    /// </summary>
    [Fact]
    public async Task Hikvision_hodisasi_surati_bilan_yoziladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var (qurilmaId, token) = await QurilmaAsync();
        using var mijoz = _host.Factory.CreateClient();

        var vaqt = HozirToshkentda();
        var fayllar = _host.FileCount();

        using var javob = await mijoz.PostAsync(
            YolTokenBilan(token),
            HikvisionTanasi(HikvisionHodisasi(vaqt, "10427", serialNo: 88213), TestImages.JpegYasa(64, 64, 80)),
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        // Javobda faqat uchta maydon: kim o'tgani haqida hech narsa yo'q (SPEC §6.1).
        using (var hujjat = JsonDocument.Parse(await MatnAsync(javob)))
        {
            hujjat.RootElement.EnumerateObject().Select(maydon => maydon.Name)
                .ShouldBe(["ok", "stored", "duplicates"], ignoreOrder: true);
        }

        (await javob.Content.ReadFromJsonAsync<IngestResponse>(bekorQilish))
            .ShouldBe(new IngestResponse(Ok: true, Stored: 1, Duplicates: 0));

        var otish = (await OtishlarAsync(qurilmaId)).ShouldHaveSingleItem();

        otish.RawPersonId.ShouldBe("10427");
        otish.OccurredAt.ShouldBe(vaqt.ToUniversalTime());
        otish.DeviceOffsetMinutes.ShouldBe((short)300);
        otish.OffsetAssumed.ShouldBeFalse();
        otish.LocalDate.ShouldBe(vaqt.TashkentDate());
        otish.Direction.ShouldBe(PassDirection.In);
        otish.DirectionSource.ShouldBe(DirectionSource.Device);
        otish.Method.ShouldBe(PassMethod.Face);
        otish.DedupeKey.ShouldBe("s:88213");
        otish.RawEvent.ShouldNotBeNull().ShouldContain("AccessControllerEvent");
        otish.ProcessedAt.ShouldBeNull("yangi o'tish qayta hisoblash navbatida turishi kerak");

        // Moslik yo'q — o'tish TASHLANMAYDI, bog'lanmagan holda saqlanadi.
        otish.ResolvedEmployeeId.ShouldBeNull();

        otish.PhotoKey.ShouldNotBeNull();
        File.Exists(Path.Combine(_host.StorageRoot, otish.PhotoKey)).ShouldBeTrue();
        _host.FileCount().ShouldBe(fayllar + 1);

        var qurilma = await QurilmaniOqiAsync(qurilmaId);
        qurilma.LastEventAt.ShouldNotBeNull();
        qurilma.LastSeenAt.ShouldNotBeNull();
    }

    /// <summary>
    /// Aynan o'sha hodisa qayta yuborilsa → 200, <c>duplicates: 1</c>, baribir
    /// bitta qator va bitta fayl; takror <c>last_event_at</c> ni surmaydi.
    /// </summary>
    [Fact]
    public async Task Qayta_yuborilgan_hodisa_takror_sanaladi_va_hech_narsa_qoshilmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var (qurilmaId, token) = await QurilmaAsync();
        using var mijoz = _host.Factory.CreateClient();

        var hodisa = HikvisionHodisasi(HozirToshkentda(), "20511", serialNo: 5001);
        var rasm = TestImages.JpegYasa(64, 64, 80);

        using var birinchi = await mijoz.PostAsync(YolTokenBilan(token), HikvisionTanasi(hodisa, rasm), bekorQilish);
        (await birinchi.Content.ReadFromJsonAsync<IngestResponse>(bekorQilish))
            .ShouldBe(new IngestResponse(true, 1, 0));

        var fayllar = _host.FileCount();
        var oxirgiHodisa = (await QurilmaniOqiAsync(qurilmaId)).LastEventAt;

        using var ikkinchi = await mijoz.PostAsync(YolTokenBilan(token), HikvisionTanasi(hodisa, rasm), bekorQilish);

        ikkinchi.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(ikkinchi));
        (await ikkinchi.Content.ReadFromJsonAsync<IngestResponse>(bekorQilish))
            .ShouldBe(new IngestResponse(true, 0, 1));

        (await OtishlarAsync(qurilmaId)).ShouldHaveSingleItem();
        _host.FileCount().ShouldBe(fayllar, "takrorning surati saqlangan joyida o'chirilishi kerak");
        (await QurilmaniOqiAsync(qurilmaId)).LastEventAt.ShouldBe(oxirgiHodisa);
    }

    /// <summary>
    /// Noto'g'ri token, token yo'q, noto'g'ri sarlavha tokeni — AYNAN bir xil
    /// 401, va bazaga ham, diskka ham hech narsa yozilmaydi.
    /// </summary>
    [Fact]
    public async Task Notogri_yoki_yoq_token_bir_xil_401_va_hech_narsa_yozilmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var (qurilmaId, token) = await QurilmaAsync();
        using var mijoz = _host.Factory.CreateClient();

        var hodisa = HikvisionHodisasi(HozirToshkentda(), "30777", serialNo: 7001);
        var rasm = TestImages.JpegYasa(64, 64, 80);

        var otishlar = await JamiOtishlarAsync();
        var fayllar = _host.FileCount();

        // Haqiqiy tokenning bitta harfi o'zgartirilgan — «deyarli to'g'ri» ham rad.
        var buzilgan = (token[0] == 'A' ? "B" : "A") + token[1..];

        using var yoldaNotogri = await mijoz.PostAsync(YolTokenBilan(buzilgan), HikvisionTanasi(hodisa, rasm), bekorQilish);
        using var tokenYoq = await mijoz.PostAsync(SarlavhaYoli, HikvisionTanasi(hodisa, rasm), bekorQilish);

        using var sarlavhaSorovi = new HttpRequestMessage(HttpMethod.Post, SarlavhaYoli)
        {
            Content = HikvisionTanasi(hodisa, rasm),
        };
        sarlavhaSorovi.Headers.Add(AttendanceIngestEndpoints.TokenHeader, buzilgan);
        using var sarlavhadaNotogri = await mijoz.SendAsync(sarlavhaSorovi, bekorQilish);

        var javoblar = new[] { yoldaNotogri, tokenYoq, sarlavhadaNotogri };
        var xatolar = new List<ProblemResponse>();

        foreach (var javob in javoblar)
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, await MatnAsync(javob));
            xatolar.Add((await javob.Content.ReadFromJsonAsync<ProblemResponse>(bekorQilish))!);
        }

        xatolar.ShouldAllBe(xato => xato.Title == "davomat.ingest_ruxsat_yoq");
        xatolar.Select(xato => xato.Detail).Distinct().ShouldHaveSingleItem(
            "tekshiruvchi qaysi bosqichda yiqilganini matndan ham bilmasligi kerak");

        (await JamiOtishlarAsync()).ShouldBe(otishlar);
        _host.FileCount().ShouldBe(fayllar);
        (await QurilmaniOqiAsync(qurilmaId)).LastSeenAt.ShouldBeNull("tasdiqlanmagan so'rov qurilmaga hech narsa yozmaydi");
    }

    /// <summary>
    /// Yo'ldagi token konsol logiga tushmaydi — na muvaffaqiyatli, na rad
    /// etilgan so'rovda.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Konsol chiqishi ushlab olinadi: xostning haqiqiy Serilog konveyeri
    /// (<c>Program.cs</c>) shu yerga yozadi. Tozalagichning o'zi va fayl
    /// formatteri <see cref="IngestTokenScrubberTests"/> da aniq tekshiriladi.
    /// </para>
    /// <para>
    /// ⚠️ Bu yerda FAQAT salbiy tomon («token yo'q») tekshiriladi, «yashirilgan
    /// yo'l bor» — yo'q. Sabab: Serilog xostlari statik <c>Log.Logger</c> ni
    /// bo'lishadi va parallel test sinfi o'z xostini yopganda log yozuvlari bir
    /// lahzaga yutilib ketishi mumkin. Ijobiy tekshiruv bu yerda goh o'tib, goh
    /// yiqilardi; alohida ishga tushirilganda u o'tadi (tekshirilgan), va
    /// yashirish natijasining o'zi birlik testida mahkamlangan.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Yoldagi_token_logga_tushmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var (_, token) = await QurilmaAsync();
        using var mijoz = _host.Factory.CreateClient();

        var begona = AttendanceDeviceToken.Generate();
        string yigilgan;

        using (var konsol = new KonsolTutqichi())
        {
            using var togri = await mijoz.PostAsync(
                YolTokenBilan(token),
                HikvisionTanasi(HikvisionHodisasi(HozirToshkentda(), "40404", serialNo: 9001), null),
                bekorQilish);
            togri.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(togri));

            using var notogri = await mijoz.PostAsync(
                YolTokenBilan(begona),
                HikvisionTanasi(HikvisionHodisasi(HozirToshkentda(), "40404", serialNo: 9002), null),
                bekorQilish);
            notogri.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

            // So'rov logi javob yuborilgach yoziladi — kichik kutish.
            await Task.Delay(TimeSpan.FromMilliseconds(500), bekorQilish);
            yigilgan = konsol.Yigilgan();
        }

        yigilgan.ShouldNotContain(token);
        yigilgan.ShouldNotContain(begona);
    }

    // ────────────────────────────────────────────────────────────
    //  Qo'shimcha: moslik, faollik, format, chegaralar
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Mosligi bor, lekin MEHNAT MUNOSABATI YO'Q odamning o'tishi ham xodimga
    /// bog'lanadi — jonli taxtadagi «ishdan ketgan xodim o'tdi»
    /// ogohlantirishi aynan shu bog'lanishdan hisoblanadi (SPEC §6.5, §8.7).
    /// </summary>
    [Fact]
    public async Task Moslik_bor_bolsa_mehnat_munosabatisiz_ham_xodimga_boglanadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var (qurilmaId, token) = await QurilmaAsync();
        using var mijoz = _host.Factory.CreateClient();

        var shaxs = "5" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..7];
        var xodimId = await MoslikAsync(shaxs);

        using var javob = await mijoz.PostAsync(
            YolTokenBilan(token),
            HikvisionTanasi(HikvisionHodisasi(HozirToshkentda(), shaxs, serialNo: 12), null),
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var otish = (await OtishlarAsync(qurilmaId)).ShouldHaveSingleItem();
        otish.ResolvedEmployeeId.ShouldBe(xodimId);
        otish.ResolvedAt.ShouldNotBeNull();
        otish.PhotoKey.ShouldBeNull("rasm qismi yo'q — o'tish baribir yoziladi");
    }

    /// <summary>Yaroqli token, faolsiz qurilma → 403 (401 dan farqli) va hech narsa yozilmaydi.</summary>
    [Fact]
    public async Task Faolsiz_qurilma_403_va_hech_narsa_yozilmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var (qurilmaId, token) = await QurilmaAsync(faol: false);
        using var mijoz = _host.Factory.CreateClient();

        using var javob = await mijoz.PostAsync(
            YolTokenBilan(token),
            HikvisionTanasi(HikvisionHodisasi(HozirToshkentda(), "60606", serialNo: 1), TestImages.JpegYasa(32, 32, 80)),
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await MatnAsync(javob));
        (await javob.Content.ReadFromJsonAsync<ProblemResponse>(bekorQilish))!.Title.ShouldBe("davomat.qurilma_faolsiz");

        (await OtishlarAsync(qurilmaId)).ShouldBeEmpty();
        (await QurilmaniOqiAsync(qurilmaId)).LastSeenAt.ShouldBeNull();
    }

    /// <summary>
    /// Tahlilchi TANAGA emas, QURILMA YOZUVIGA qarab tanlanadi: bir xil oddiy
    /// JSON Generic qurilmada yoziladi, Hikvision qurilmada — «o'qib bo'lmadi».
    /// </summary>
    [Fact]
    public async Task Tahlilchi_qurilma_turi_boyicha_tanlanadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var (genericId, genericToken) = await QurilmaAsync(AttendanceProvider.Generic);
        var (hikvisionId, hikvisionToken) = await QurilmaAsync(AttendanceProvider.Hikvision);
        using var mijoz = _host.Factory.CreateClient();

        // Birinchisi siljishsiz: Toshkent taxmin qilinadi va bayroq qo'yiladi.
        // Ikkinchisida shaxs raqami SON bo'lib keladi va yo'nalish yo'q.
        var mahalliy = HozirToshkentda().AddMinutes(-3).DateTime.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
        var siljishli = IsoVaqt(HozirToshkentda());
        var tana = $$"""
            { "events": [
                { "personId": "70001", "occurredAt": "{{mahalliy}}", "direction": "Out", "method": "Card", "eventId": "a-1" },
                { "personId": 70002, "occurredAt": "{{siljishli}}" }
            ] }
            """;

        using var generic = await mijoz.SendAsync(SarlavhaliJson(genericToken, tana), bekorQilish);
        generic.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(generic));
        (await generic.Content.ReadFromJsonAsync<IngestResponse>(bekorQilish)).ShouldBe(new IngestResponse(true, 2, 0));

        var otishlar = await OtishlarAsync(genericId);
        otishlar.Count.ShouldBe(2);

        var birinchi = otishlar.Single(otish => otish.RawPersonId == "70001");
        birinchi.OffsetAssumed.ShouldBeTrue();
        birinchi.DeviceOffsetMinutes.ShouldBe((short)300);
        birinchi.Direction.ShouldBe(PassDirection.Out);
        birinchi.Method.ShouldBe(PassMethod.Card);
        birinchi.DedupeKey.ShouldBe("s:a-1");

        var ikkinchi = otishlar.Single(otish => otish.RawPersonId == "70002");
        ikkinchi.Direction.ShouldBe(PassDirection.Unknown);
        ikkinchi.DedupeKey.ShouldStartWith("t:70002|");

        using var hikvision = await mijoz.SendAsync(SarlavhaliJson(hikvisionToken, tana), bekorQilish);
        hikvision.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(hikvision));
        (await hikvision.Content.ReadFromJsonAsync<ProblemResponse>(bekorQilish))!.Title.ShouldBe("davomat.hodisa_oqilmadi");
        (await OtishlarAsync(hikvisionId)).ShouldBeEmpty();
    }

    /// <summary>
    /// O'tish bo'lmagan Hikvision hodisasi (yurak urishi, rad etilgan yuz) —
    /// 200 va <c>stored: 0</c>, 400 emas: aks holda terminal uni cheksiz qayta yuborardi.
    /// </summary>
    [Fact]
    public async Task Otish_bolmagan_Hikvision_hodisasi_200_bilan_tashlanadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var (qurilmaId, token) = await QurilmaAsync();
        using var mijoz = _host.Factory.CreateClient();

        const string yurakUrishi = """{ "eventType": "heartBeat", "dateTime": "2026-09-17T08:00:00+05:00" }""";
        var radEtilganYuz = HikvisionHodisasi(HozirToshkentda(), "80808", serialNo: 3, subEventType: 76);

        foreach (var hodisa in new[] { yurakUrishi, radEtilganYuz })
        {
            using var javob = await mijoz.PostAsync(YolTokenBilan(token), HikvisionTanasi(hodisa, null), bekorQilish);

            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
            (await javob.Content.ReadFromJsonAsync<IngestResponse>(bekorQilish)).ShouldBe(new IngestResponse(true, 0, 0));
        }

        (await OtishlarAsync(qurilmaId)).ShouldBeEmpty();
        (await QurilmaniOqiAsync(qurilmaId)).LastSeenAt.ShouldNotBeNull("qurilma gapiryapti — bu ham diagnostika");
    }

    /// <summary>30 kundan uzoq soat og'ishi → 400 <c>davomat.vaqt_yaroqsiz</c>, hech narsa yozilmaydi.</summary>
    [Fact]
    public async Task Soati_buzuq_qurilma_hodisasi_rad_etiladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var (qurilmaId, token) = await QurilmaAsync();
        using var mijoz = _host.Factory.CreateClient();

        var fayllar = _host.FileCount();

        using var javob = await mijoz.PostAsync(
            YolTokenBilan(token),
            HikvisionTanasi(HikvisionHodisasi(HozirToshkentda().AddDays(-40), "90909", serialNo: 4), TestImages.JpegYasa(32, 32, 80)),
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(javob));
        (await javob.Content.ReadFromJsonAsync<ProblemResponse>(bekorQilish))!.Title.ShouldBe("davomat.vaqt_yaroqsiz");

        (await OtishlarAsync(qurilmaId)).ShouldBeEmpty();
        _host.FileCount().ShouldBe(fayllar, "rad etilgan hodisaning surati diskka tushmasligi kerak");
    }

    /// <summary>200 dan ko'p hodisa → 400 <c>davomat.hodisa_kop</c>; 2 MB dan katta tana → 413.</summary>
    [Fact]
    public async Task Hajm_chegaralari_ishlaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var (qurilmaId, token) = await QurilmaAsync(AttendanceProvider.Generic);
        using var mijoz = _host.Factory.CreateClient();

        var vaqt = IsoVaqt(HozirToshkentda());
        var hodisalar = string.Join(
            ',',
            Enumerable.Range(1, IngestLimits.MaxEventsPerRequest + 1)
                .Select(i => $$"""{ "personId": "{{i}}", "occurredAt": "{{vaqt}}" }"""));

        using var kop = await mijoz.SendAsync(SarlavhaliJson(token, $$"""{ "events": [ {{hodisalar}} ] }"""), bekorQilish);
        kop.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(kop));
        (await kop.Content.ReadFromJsonAsync<ProblemResponse>(bekorQilish))!.Title.ShouldBe("davomat.hodisa_kop");

        using var katta = await mijoz.SendAsync(
            SarlavhaliJson(token, "{\"pad\":\"" + new string('x', AttendanceRules.MaxIngestBodyBytes) + "\"}"),
            bekorQilish);
        katta.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);

        (await OtishlarAsync(qurilmaId)).ShouldBeEmpty();
    }

    // ────────────────────────────────────────────────────────────
    //  Imzo, IP ro'yxati, ikki marshrut — soat qotirilgan (§3.2, §11.1 #4–#7, #10)
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// #4, #5. Imzo rejimidagi qurilma: imzo yo'q, boshqa sir, boshqa tana, prefikssiz, boshqa versiya,
    /// qisqa — hammasi token YO'Qdagi bilan AYNAN bir xil 401 va hech narsa yozilmaydi; to'g'ri
    /// imzo — 200 va bitta qator.
    /// </summary>
    [Fact]
    public async Task Imzo_rejimida_notogri_imzo_401_togri_imzo_bitta_qator_yozadi()
    {
        var ct = TestContext.Current.CancellationToken;
        var belgi = Belgi();
        var sir = "imzo-siri-" + belgi;
        var (qurilmaId, token) = await QurilmaAsync(AttendanceProvider.Generic, sir: sir);
        using var mijoz = _host.SoatiQotirilgan.CreateClient();

        var tana = GenericJson(("h-" + belgi, IsoVaqt(Toshkentda(8, 55)), "In", "1"));
        var boshqaTana = GenericJson(("h-" + belgi, IsoVaqt(Toshkentda(8, 56)), "In", "1"));
        var togriImzo = Imzo(sir, tana);

        using var tokensiz = await mijoz.SendAsync(JsonBaytlar(null, tana), ct);
        var namuna = await XatoAsync(tokensiz, HttpStatusCode.Unauthorized);

        var yaroqsizlar = new (string Holat, string? Imzo)[]
        {
            ("imzo yo'q", null),
            ("boshqa sir", Imzo("boshqa-sir", tana)),
            ("boshqa tana uchun imzo", Imzo(sir, boshqaTana)),
            ("prefikssiz", togriImzo[AttendanceDeviceAuthenticator.SignaturePrefix.Length..]),
            ("boshqa versiya", "v2=" + togriImzo[AttendanceDeviceAuthenticator.SignaturePrefix.Length..]),
            ("qisqa", togriImzo[..^2]),
        };

        foreach (var (holat, imzo) in yaroqsizlar)
        {
            using var javob = await mijoz.SendAsync(JsonBaytlar(token, tana, imzo), ct);
            (await XatoAsync(javob, HttpStatusCode.Unauthorized, holat)).ShouldBe(namuna, holat);
        }

        (await OtishlarAsync(qurilmaId)).ShouldBeEmpty();
        (await QurilmaniOqiAsync(qurilmaId)).LastSeenAt.ShouldBeNull("imzosi tasdiqlanmagan so'rov qurilmaga hech narsa yozmaydi");

        using var togri = await mijoz.SendAsync(JsonBaytlar(token, tana, togriImzo), ct);
        togri.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(togri));
        (await togri.Content.ReadFromJsonAsync<IngestResponse>(ct)).ShouldBe(new IngestResponse(true, 1, 0));

        var otish = (await OtishlarAsync(qurilmaId)).ShouldHaveSingleItem();
        otish.RawPersonId.ShouldBe("h-" + belgi);
        otish.OccurredAt.ShouldBe(Toshkentda(8, 55));
        (await QurilmaniOqiAsync(qurilmaId)).LastEventAt.ShouldBe(Hozir);
    }

    /// <summary>
    /// Tartib token → imzo → IP → faollik (§3.2): imzosi yo'q so'rov qurilma O'CHIRILGANINI ham
    /// bilmasligi kerak — 401, 403 emas. Imzo to'g'ri bo'lsa — 403 <c>davomat.qurilma_faolsiz</c>.
    /// </summary>
    [Fact]
    public async Task Imzo_rejimidagi_faolsiz_qurilma_imzosiz_sorovga_401_imzolisiga_403_beradi()
    {
        var ct = TestContext.Current.CancellationToken;
        var belgi = Belgi();
        var sir = "faolsiz-sir-" + belgi;
        var (qurilmaId, token) = await QurilmaAsync(AttendanceProvider.Generic, faol: false, sir: sir);
        using var mijoz = _host.SoatiQotirilgan.CreateClient();

        var tana = GenericJson(("f-" + belgi, IsoVaqt(Toshkentda(9, 0)), "In", "1"));

        using var imzosiz = await mijoz.SendAsync(JsonBaytlar(token, tana), ct);
        (await XatoAsync(imzosiz, HttpStatusCode.Unauthorized)).Title.ShouldBe("davomat.ingest_ruxsat_yoq");

        using var imzoli = await mijoz.SendAsync(JsonBaytlar(token, tana, Imzo(sir, tana)), ct);
        (await XatoAsync(imzoli, HttpStatusCode.Forbidden)).Title.ShouldBe("davomat.qurilma_faolsiz");

        (await OtishlarAsync(qurilmaId)).ShouldBeEmpty();
    }

    /// <summary>
    /// #6. Yo'ldagi va sarlavhadagi token bir xil qator beradi: ikki bir xil sozlangan qurilmaga bitta
    /// hodisa ikki marshrutdan yuboriladi va qatorlar identifikatorlardan tashqari HAR BIR ustunda
    /// teng. So'ngra marshrutlar almashtirilsa — ikkalasi ham takror: qurilma ham, takror kaliti ham
    /// token qayerdan kelganiga bog'liq emas.
    /// </summary>
    [Fact]
    public async Task Yoldagi_va_sarlavhadagi_token_bir_xil_qator_yozadi()
    {
        var ct = TestContext.Current.CancellationToken;
        var belgi = Belgi();
        var (yolQurilma, yolToken) = await QurilmaAsync();
        var (sarlavhaQurilma, sarlavhaToken) = await QurilmaAsync();
        using var mijoz = _host.SoatiQotirilgan.CreateClient();

        var hodisa = HikvisionXom("2026-09-16T08:10:00+05:00", "p6-" + belgi, serialNo: 4401);

        using (var yol = await mijoz.PostAsync(YolTokenBilan(yolToken), HikvisionTanasi(hodisa, null), ct))
        {
            yol.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yol));
            (await yol.Content.ReadFromJsonAsync<IngestResponse>(ct)).ShouldBe(new IngestResponse(true, 1, 0));
        }

        using (var sarlavha = await mijoz.SendAsync(SarlavhaliMultipart(sarlavhaToken, hodisa), ct))
        {
            sarlavha.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(sarlavha));
            (await sarlavha.Content.ReadFromJsonAsync<IngestResponse>(ct)).ShouldBe(new IngestResponse(true, 1, 0));
        }

        var yoldan = (await QatorlarJsonAsync(yolQurilma)).ShouldHaveSingleItem();
        var sarlavhadan = (await QatorlarJsonAsync(sarlavhaQurilma)).ShouldHaveSingleItem();

        yoldan.ShouldContain("\"dedupe_key\": \"s:4401\"");
        yoldan.ShouldContain("\"raw_person_id\": \"p6-" + belgi + "\"");
        sarlavhadan.ShouldBe(yoldan);

        using (var almashgan = await mijoz.SendAsync(SarlavhaliMultipart(yolToken, hodisa), ct))
        {
            (await almashgan.Content.ReadFromJsonAsync<IngestResponse>(ct)).ShouldBe(new IngestResponse(true, 0, 1));
        }

        using (var almashgan = await mijoz.PostAsync(YolTokenBilan(sarlavhaToken), HikvisionTanasi(hodisa, null), ct))
        {
            (await almashgan.Content.ReadFromJsonAsync<IngestResponse>(ct)).ShouldBe(new IngestResponse(true, 0, 1));
        }

        (await OtishlarAsync(yolQurilma)).ShouldHaveSingleItem();
        (await OtishlarAsync(sarlavhaQurilma)).ShouldHaveSingleItem();
    }

    /// <summary>
    /// #7. IP ro'yxati bor qurilma: ro'yxatdan tashqaridagi manba (va tarmoqning bitta qo'shni
    /// manzili) — token yo'qdagi bilan bir xil 401, hech narsa yozilmaydi; CIDR ichidagi va yakka
    /// yozilgan manzil — o'tadi. Manba Caddy ortidagidek <c>X-Forwarded-For</c> dan olinadi.
    /// </summary>
    [Fact]
    public async Task IP_royxatidan_tashqaridagi_manba_401_ichidagisi_otadi()
    {
        var ct = TestContext.Current.CancellationToken;
        var belgi = Belgi();
        var (qurilmaId, token) = await QurilmaAsync(AttendanceProvider.Generic, ipRoyxati: "10.20.0.0/16, 192.0.2.7");
        using var mijoz = _host.SoatiQotirilgan.CreateClient();

        using var tokensiz = await mijoz.SendAsync(JsonBaytlar(null, GenericJson(("ip-" + belgi, IsoVaqt(Toshkentda(9, 0)), "In", "0"))), ct);
        var namuna = await XatoAsync(tokensiz, HttpStatusCode.Unauthorized);

        foreach (var begona in new[] { "203.0.113.9", "10.21.0.1", "192.0.2.8" })
        {
            var tana = GenericJson(("ip-" + belgi, IsoVaqt(Toshkentda(9, 0)), "In", "b-" + begona));
            using var javob = await mijoz.SendAsync(JsonBaytlar(token, tana, manba: begona), ct);
            (await XatoAsync(javob, HttpStatusCode.Unauthorized, begona)).ShouldBe(namuna, begona);
        }

        (await OtishlarAsync(qurilmaId)).ShouldBeEmpty();
        (await QurilmaniOqiAsync(qurilmaId)).LastSeenAt.ShouldBeNull();

        foreach (var ichki in new[] { "10.20.5.6", "192.0.2.7" })
        {
            var tana = GenericJson(("ip-" + belgi, IsoVaqt(Toshkentda(9, 0)), "In", "i-" + ichki));
            using var javob = await mijoz.SendAsync(JsonBaytlar(token, tana, manba: ichki), ct);
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, $"{ichki}: {await MatnAsync(javob)}");
            (await javob.Content.ReadFromJsonAsync<IngestResponse>(ct)).ShouldBe(new IngestResponse(true, 1, 0));
        }

        (await OtishlarAsync(qurilmaId)).Count.ShouldBe(2);
        (await QurilmaniOqiAsync(qurilmaId)).LastSeenIp.ShouldBe("192.0.2.7");
    }

    /// <summary>
    /// #10. Ataylab qilingan nosimmetriya: qabul JWT SIZ ochiq, o'tishlar jurnali esa yopiq — na
    /// tokensiz, na qurilma tokeni bilan (sarlavhada ham, «Bearer» sifatida ham) ochilmaydi.
    /// </summary>
    [Fact]
    public async Task Qabul_JWT_siz_ochiq_otishlar_jurnali_esa_qurilma_tokeni_bilan_ham_yopiq()
    {
        var ct = TestContext.Current.CancellationToken;
        var belgi = Belgi();
        var (qurilmaId, token) = await QurilmaAsync(AttendanceProvider.Generic);
        using var mijoz = _host.SoatiQotirilgan.CreateClient();

        using (var qabul = await mijoz.SendAsync(JsonBaytlar(token, GenericJson(("j-" + belgi, IsoVaqt(Toshkentda(9, 0)), "In", "1"))), ct))
        {
            qabul.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qabul));
        }

        (await OtishlarAsync(qurilmaId)).ShouldHaveSingleItem();

        var jurnal = new Uri("/api/attendance/passes", UriKind.Relative);

        using (var anonim = await mijoz.GetAsync(jurnal, ct))
        {
            anonim.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using (var sorov = new HttpRequestMessage(HttpMethod.Get, jurnal))
        {
            sorov.Headers.Add(AttendanceIngestEndpoints.TokenHeader, token);
            using var sarlavhada = await mijoz.SendAsync(sorov, ct);
            sarlavhada.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using (var sorov = new HttpRequestMessage(HttpMethod.Get, jurnal))
        {
            sorov.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var bearer = await mijoz.SendAsync(sorov, ct);
            bearer.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
    }

    /// <summary>
    /// Moslik keshi (§4.1, B3 → B6 shartnomasi): moslik yaratilgach keyingi o'tish DARHOL xodimga
    /// bog'lanadi, o'chirilgach esa DARHOL bog'lanmaydi — 60 soniyalik kesh eski xodimni eslab
    /// qolmaydi. O'tgan o'tishlar esa o'chirishdan keyin ham xodimda (§4.3, #27).
    /// </summary>
    /// <remarks>
    /// Soat qotirilgan xostda kesh yozuvi HECH QACHON eskirmaydi — ya'ni «o'chirishda kesh
    /// tozalanmadi» xatosi vaqtga qarab goh ko'rinib, goh yashirinmaydi: har safar yiqiladi.
    /// Moslik endpoint ishlovchilari orqali, qabul qilayotgan AYNAN o'sha xostda boshqariladi.
    /// </remarks>
    [Fact]
    public async Task Moslik_yaratilsa_ham_ochirilsa_ham_kesh_darhol_unutadi()
    {
        var ct = TestContext.Current.CancellationToken;
        var shaxs = "kesh-" + Belgi();
        var (qurilmaId, token) = await QurilmaAsync();
        using var mijoz = _host.SoatiQotirilgan.CreateClient();

        Guid xodimId;
        await using (var qamrov = _host.Factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
            var xodim = Employee.Create(FullName.Create("Кешов", "Ходим", shaxs[5..]).Value).Value;
            kontekst.Employees.Add(xodim);
            await kontekst.SaveChangesAsync(ct);
            xodimId = xodim.Id;
        }

        async Task<AttendancePass> YuborAsync(long seriya, string vaqt)
        {
            using var javob = await mijoz.PostAsync(YolTokenBilan(token), HikvisionTanasi(HikvisionXom(vaqt, shaxs, seriya), null), ct);
            (await javob.Content.ReadFromJsonAsync<IngestResponse>(ct)).ShouldBe(new IngestResponse(true, 1, 0), vaqt);
            return (await OtishlarAsync(qurilmaId)).Single(otish => otish.DedupeKey == "s:" + seriya.ToString(CultureInfo.InvariantCulture));
        }

        (await YuborAsync(2701, "2026-09-16T08:00:00+05:00")).ResolvedEmployeeId.ShouldBeNull("sinov sharti: moslik hali yo'q");

        Guid moslikId;
        await using (var qamrov = _host.SoatiQotirilgan.Services.CreateAsyncScope())
        {
            var yaratildi = await qamrov.ServiceProvider.GetRequiredService<CreateIdentityCommandHandler>()
                .HandleAsync(new CreateIdentityCommand(AttendanceIdentityProvider.Verifix, shaxs, xodimId), ct);
            yaratildi.IsSuccess.ShouldBeTrue(yaratildi.IsFailure ? yaratildi.Error.Code : null);
            moslikId = yaratildi.Value.Id;
        }

        (await YuborAsync(2702, "2026-09-16T09:00:00+05:00")).ResolvedEmployeeId.ShouldBe(xodimId);

        await using (var qamrov = _host.SoatiQotirilgan.Services.CreateAsyncScope())
        {
            var ochirildi = await qamrov.ServiceProvider.GetRequiredService<DeleteIdentityCommandHandler>()
                .HandleAsync(new DeleteIdentityCommand(moslikId), ct);
            ochirildi.IsSuccess.ShouldBeTrue(ochirildi.IsFailure ? ochirildi.Error.Code : null);
        }

        (await YuborAsync(2703, "2026-09-16T10:00:00+05:00")).ResolvedEmployeeId
            .ShouldBeNull("o'chirilgan moslik keshdan keyingi o'tishni eski xodimga yozmasligi kerak");

        var otishlar = await OtishlarAsync(qurilmaId);
        otishlar.Single(otish => otish.DedupeKey == "s:2701").ResolvedEmployeeId.ShouldBe(xodimId, "to'ldirish bog'lagan eski o'tish");
        otishlar.Single(otish => otish.DedupeKey == "s:2702").ResolvedEmployeeId.ShouldBe(xodimId, "o'chirish o'tganni bog'lanmagan qilmaydi");
    }

    // ────────────────────────────────────────────────────────────
    //  Takror kaliti — §3.6, §11.1 #12–#14
    // ────────────────────────────────────────────────────────────

    /// <summary>#12. Bir xil <c>serialNo</c> BOSHQA qurilmadan — alohida qator: kalit qurilma bo'yicha.</summary>
    [Fact]
    public async Task Bir_xil_serialNo_boshqa_qurilmadan_alohida_qator_yozadi()
    {
        var ct = TestContext.Current.CancellationToken;
        var belgi = Belgi();
        var (birinchi, birinchiToken) = await QurilmaAsync();
        var (ikkinchi, ikkinchiToken) = await QurilmaAsync();
        using var mijoz = _host.SoatiQotirilgan.CreateClient();

        // Ikkala turniket hisoblagichi 9100 da — har birida o'z shaxsi.
        foreach (var (token, shaxs) in new[] { (birinchiToken, "a12-" + belgi), (ikkinchiToken, "b12-" + belgi) })
        {
            using var javob = await mijoz.PostAsync(
                YolTokenBilan(token),
                HikvisionTanasi(HikvisionXom("2026-09-16T08:20:00+05:00", shaxs, serialNo: 9100), null),
                ct);
            (await javob.Content.ReadFromJsonAsync<IngestResponse>(ct)).ShouldBe(new IngestResponse(true, 1, 0));
        }

        var birinchiQator = (await OtishlarAsync(birinchi)).ShouldHaveSingleItem();
        var ikkinchiQator = (await OtishlarAsync(ikkinchi)).ShouldHaveSingleItem();
        birinchiQator.DedupeKey.ShouldBe("s:9100");
        ikkinchiQator.DedupeKey.ShouldBe("s:9100");
        birinchiQator.RawPersonId.ShouldBe("a12-" + belgi);
        ikkinchiQator.RawPersonId.ShouldBe("b12-" + belgi);
    }

    /// <summary>
    /// #13. <c>serialNo</c> siz proshivka: shaxs + UTC soniya + yo'nalish. Bir soniya ichidagi qayta
    /// yuborish (millisekund farqi bilan ham) — takror; bir soniya keyin — yangi qator; xuddi shu
    /// soniyada CHIQISH — ham yangi qator.
    /// </summary>
    [Fact]
    public async Task SerialNo_siz_hodisa_bir_soniya_ichida_takror_boshqa_soniyada_yangi_qator()
    {
        var ct = TestContext.Current.CancellationToken;
        var shaxs = "p13-" + Belgi();
        var (qurilmaId, token) = await QurilmaAsync();
        using var mijoz = _host.SoatiQotirilgan.CreateClient();

        var yuborishlar = new (string Vaqt, string Holat, IngestResponse Kutilgan)[]
        {
            ("2026-09-16T08:03:12.100+05:00", "checkIn", new(true, 1, 0)),
            ("2026-09-16T08:03:12.900+05:00", "checkIn", new(true, 0, 1)),
            ("2026-09-16T08:03:12+05:00", "checkIn", new(true, 0, 1)),
            ("2026-09-16T08:03:13+05:00", "checkIn", new(true, 1, 0)),
            ("2026-09-16T08:03:12+05:00", "checkOut", new(true, 1, 0)),
        };

        foreach (var (vaqt, holat, kutilgan) in yuborishlar)
        {
            using var javob = await mijoz.PostAsync(
                YolTokenBilan(token),
                HikvisionTanasi(HikvisionXom(vaqt, shaxs, serialNo: null, holat), null),
                ct);
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
            (await javob.Content.ReadFromJsonAsync<IngestResponse>(ct)).ShouldBe(kutilgan, $"{vaqt} {holat}");
        }

        (await OtishlarAsync(qurilmaId)).Select(otish => otish.DedupeKey).ShouldBe(
            [
                $"t:{shaxs}|20260916T030312|{(int)PassDirection.In}",
                $"t:{shaxs}|20260916T030313|{(int)PassDirection.In}",
                $"t:{shaxs}|20260916T030312|{(int)PassDirection.Out}",
            ],
            ignoreOrder: true);
    }

    /// <summary>
    /// #14. Takror allaqachon hisobga olingan o'tishning <c>processed_at</c> ini TOZALAMAYDI (kun qayta
    /// navbatga tushmaydi) va qurilmaning <c>last_event_at</c> / <c>last_skew_seconds</c> ini surmaydi;
    /// <c>last_seen_at</c> esa suriladi — qurilma gapirdi.
    /// </summary>
    [Fact]
    public async Task Takror_hisobga_olingan_otishning_processed_at_ini_va_last_event_at_ni_ozgartirmaydi()
    {
        var ct = TestContext.Current.CancellationToken;
        var (qurilmaId, token) = await QurilmaAsync();
        using var mijoz = _host.SoatiQotirilgan.CreateClient();

        var hodisa = HikvisionXom("2026-09-16T08:30:00+05:00", "p14-" + Belgi(), serialNo: 1401);

        using (var birinchi = await mijoz.PostAsync(YolTokenBilan(token), HikvisionTanasi(hodisa, null), ct))
        {
            (await birinchi.Content.ReadFromJsonAsync<IngestResponse>(ct)).ShouldBe(new IngestResponse(true, 1, 0));
        }

        // Dirty navbat o'tishni hisobga olgan; qurilmaning oxirgi hodisasi esa KECHAGI deb qo'yiladi —
        // qotirilgan soatda «surilmadi» ni faqat shunday farqlab bo'ladi.
        var hisobgaOlindi = Hozir.AddMinutes(-20);
        var kecha = Hozir.AddDays(-1);

        await using (var qamrov = _host.Factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
            (await kontekst.AttendancePasses.SingleAsync(otish => otish.DeviceId == qurilmaId, ct)).MarkProcessed(hisobgaOlindi);
            (await kontekst.AttendanceDevices.SingleAsync(qurilma => qurilma.Id == qurilmaId, ct)).NoteEvent(kecha, "10.0.0.42", 7);
            await kontekst.SaveChangesAsync(ct);
        }

        using (var takror = await mijoz.PostAsync(YolTokenBilan(token), HikvisionTanasi(hodisa, TestImages.JpegYasa(32, 32, 80)), ct))
        {
            (await takror.Content.ReadFromJsonAsync<IngestResponse>(ct)).ShouldBe(new IngestResponse(true, 0, 1));
        }

        (await OtishlarAsync(qurilmaId)).ShouldHaveSingleItem().ProcessedAt.ShouldBe(hisobgaOlindi);

        var qurilma = await QurilmaniOqiAsync(qurilmaId);
        qurilma.LastEventAt.ShouldBe(kecha);
        qurilma.LastSkewSeconds.ShouldBe(7);
        qurilma.LastSeenAt.ShouldBe(Hozir);
    }

    // ────────────────────────────────────────────────────────────
    //  Surat va vaqt — §3.8, §3.10, §11.1 #17, #18, #21, #23
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// #17. «Surat» qismi mazmunan rasm emas (matn, PDF) — e'lon qilingan <c>image/jpeg</c> ga
    /// ishonilmaydi: o'tish yoziladi, <c>photo_key</c> bo'sh, diskka hech narsa tushmaydi.
    /// </summary>
    [Fact]
    public async Task Rasm_bolmagan_surat_qismi_tashlanadi_otish_baribir_yoziladi()
    {
        var ct = TestContext.Current.CancellationToken;
        var shaxs = "p17-" + Belgi();
        var (qurilmaId, token) = await QurilmaAsync();
        using var mijoz = _host.SoatiQotirilgan.CreateClient();

        var fayllar = _host.FileCount();
        byte[][] soxtaRasmlar =
        [
            Encoding.UTF8.GetBytes("bu rasm emas, shunchaki matn"),
            [.. "%PDF-1.4\n"u8.ToArray(), .. new byte[256]],
        ];

        for (var i = 0; i < soxtaRasmlar.Length; i++)
        {
            using var javob = await mijoz.PostAsync(
                YolTokenBilan(token),
                HikvisionTanasi(HikvisionXom("2026-09-16T08:40:00+05:00", shaxs, serialNo: 1700 + i), soxtaRasmlar[i]),
                ct);
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
            (await javob.Content.ReadFromJsonAsync<IngestResponse>(ct)).ShouldBe(new IngestResponse(true, 1, 0));
        }

        var otishlar = await OtishlarAsync(qurilmaId);
        otishlar.Count.ShouldBe(2);
        otishlar.ShouldAllBe(otish => otish.PhotoKey == null);
        otishlar.ShouldAllBe(otish => otish.RawEvent != null);
        _host.FileCount().ShouldBe(fayllar, "rasm bo'lmagan bayt diskka yozilmasligi kerak");
    }

    /// <summary>
    /// #18. Siljishsiz Hikvision <c>dateTime</c> — Toshkent deb olinadi, bayroq qo'yiladi va lahza
    /// mahalliy − 5 soat; xuddi shu vaqt <c>+05:00</c> bilan kelsa — lahza bir xil, bayroq yo'q.
    /// </summary>
    [Fact]
    public async Task Siljishsiz_Hikvision_vaqti_Toshkent_deb_olinadi_va_bayroq_qoyiladi()
    {
        var ct = TestContext.Current.CancellationToken;
        var shaxs = "p18-" + Belgi();
        var (qurilmaId, token) = await QurilmaAsync();
        using var mijoz = _host.SoatiQotirilgan.CreateClient();

        foreach (var (vaqt, seriya) in new[] { ("2026-09-16T08:03:12", 1801L), ("2026-09-16T08:03:12+05:00", 1802L) })
        {
            using var javob = await mijoz.PostAsync(
                YolTokenBilan(token),
                HikvisionTanasi(HikvisionXom(vaqt, shaxs, seriya), null),
                ct);
            (await javob.Content.ReadFromJsonAsync<IngestResponse>(ct)).ShouldBe(new IngestResponse(true, 1, 0), vaqt);
        }

        var otishlar = await OtishlarAsync(qurilmaId);
        var taxminiy = otishlar.Single(otish => otish.DedupeKey == "s:1801");
        var aniq = otishlar.Single(otish => otish.DedupeKey == "s:1802");

        taxminiy.OffsetAssumed.ShouldBeTrue();
        taxminiy.OccurredAt.ShouldBe(new DateTimeOffset(2026, 9, 16, 3, 3, 12, TimeSpan.Zero));
        taxminiy.DeviceOffsetMinutes.ShouldBe((short)300);
        taxminiy.DeviceLocalTime.ShouldBe(new DateTime(2026, 9, 16, 8, 3, 12));
        taxminiy.LocalDate.ShouldBe(new DateOnly(2026, 9, 16));
        taxminiy.ClockSkewSeconds.ShouldBe((int)(Hozir - new DateTimeOffset(2026, 9, 16, 3, 3, 12, TimeSpan.Zero)).TotalSeconds);

        aniq.OffsetAssumed.ShouldBeFalse();
        aniq.OccurredAt.ShouldBe(taxminiy.OccurredAt);
        aniq.LocalDate.ShouldBe(taxminiy.LocalDate);
    }

    /// <summary>
    /// #21. Soati 10 daqiqa orqada qolgan qurilma — o'tish YOZILADI, <c>clock_skew_seconds</c> AYNAN 600
    /// va qurilmaning <c>last_skew_seconds</c> iga ko'chadi; oldinda bo'lsa −600.
    /// </summary>
    [Fact]
    public async Task Soati_oggan_qurilma_yoziladi_ogish_otishga_va_qurilmaga_yoziladi()
    {
        var ct = TestContext.Current.CancellationToken;
        var shaxs = "p21-" + Belgi();
        var (qurilmaId, token) = await QurilmaAsync(AttendanceProvider.Generic);
        using var mijoz = _host.SoatiQotirilgan.CreateClient();

        foreach (var (siljish, eventId, kutilgan) in new[] { (-10, "orqada", 600), (10, "oldinda", -600) })
        {
            var vaqt = IsoVaqt(Hozir.AddMinutes(siljish).ToOffset(BonviTime.TashkentOffset));
            using var javob = await mijoz.SendAsync(JsonBaytlar(token, GenericJson((shaxs, vaqt, "In", eventId))), ct);
            (await javob.Content.ReadFromJsonAsync<IngestResponse>(ct)).ShouldBe(new IngestResponse(true, 1, 0), eventId);

            (await OtishlarAsync(qurilmaId)).Single(otish => otish.DedupeKey == "s:" + eventId)
                .ClockSkewSeconds.ShouldBe(kutilgan, eventId);

            var qurilma = await QurilmaniOqiAsync(qurilmaId);
            qurilma.LastSkewSeconds.ShouldBe(kutilgan, eventId);
            qurilma.LastEventAt.ShouldBe(Hozir);
        }
    }

    /// <summary>
    /// 30 kunlik chegara soniyasigacha: AYNAN 30 kun — hali «og'gan soat», yoziladi (va 500 ham emas:
    /// handlerdagi oldindan tekshiruv bilan domen chegarasi bir xil); bir soniya ortiq — ikkala
    /// yo'nalishda ham 400 <c>davomat.vaqt_yaroqsiz</c>.
    /// </summary>
    [Fact]
    public async Task Soat_ogishi_aynan_30_kun_qabul_bir_soniya_ortigi_rad_etiladi()
    {
        var ct = TestContext.Current.CancellationToken;
        var shaxs = "p30-" + Belgi();
        var (qurilmaId, token) = await QurilmaAsync(AttendanceProvider.Generic);
        using var mijoz = _host.SoatiQotirilgan.CreateClient();

        var chegara = Hozir.AddDays(-AttendanceRules.MaxClockSkewDays).ToOffset(BonviTime.TashkentOffset);

        using (var chegarada = await mijoz.SendAsync(JsonBaytlar(token, GenericJson((shaxs, IsoVaqt(chegara), "In", "c-0"))), ct))
        {
            chegarada.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(chegarada));
            (await chegarada.Content.ReadFromJsonAsync<IngestResponse>(ct)).ShouldBe(new IngestResponse(true, 1, 0));
        }

        var kelajak = Hozir.AddDays(AttendanceRules.MaxClockSkewDays).AddSeconds(1).ToOffset(BonviTime.TashkentOffset);

        foreach (var (vaqt, eventId) in new[] { (chegara.AddSeconds(-1), "c-o'tgan"), (kelajak, "c-kelajak") })
        {
            using var javob = await mijoz.SendAsync(JsonBaytlar(token, GenericJson((shaxs, IsoVaqt(vaqt), "In", eventId))), ct);
            (await XatoAsync(javob, HttpStatusCode.BadRequest, eventId)).Title.ShouldBe("davomat.vaqt_yaroqsiz");
        }

        (await OtishlarAsync(qurilmaId)).ShouldHaveSingleItem().ClockSkewSeconds.ShouldBe(AttendanceRules.MaxClockSkewDays * 86_400);
    }

    /// <summary>
    /// #23. Toshkentda 23:30 dagi o'tish — o'sha kunga; 00:30 dagisi — Toshkent kuniga, UTC kuniga
    /// (bir kun oldin) EMAS; UTC «Z» bilan kelgan 23:30 ham o'sha kunga. Lahzalar bazadan qayta o'qib
    /// solishtiriladi: kimdir mahalliy vaqtni <c>timestamptz</c> ga yozsa, bu test yiqiladi.
    /// </summary>
    [Fact]
    public async Task Yarim_tun_atrofidagi_otish_Toshkent_kuniga_tushadi_UTC_kuniga_emas()
    {
        var ct = TestContext.Current.CancellationToken;
        var belgi = Belgi();
        var (qurilmaId, token) = await QurilmaAsync(AttendanceProvider.Generic);
        using var mijoz = _host.SoatiQotirilgan.CreateClient();

        var tana = GenericJson(
            ("kech-" + belgi, "2026-09-15T23:30:00+05:00", "Out", "23-kech"),
            ("tun-" + belgi, "2026-09-16T00:30:00+05:00", "In", "23-tun"),
            ("utc-" + belgi, "2026-09-15T18:30:00Z", "Out", "23-utc"));

        using (var javob = await mijoz.SendAsync(JsonBaytlar(token, tana), ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
            (await javob.Content.ReadFromJsonAsync<IngestResponse>(ct)).ShouldBe(new IngestResponse(true, 3, 0));
        }

        var otishlar = await OtishlarAsync(qurilmaId);

        var kech = otishlar.Single(otish => otish.RawPersonId == "kech-" + belgi);
        kech.OccurredAt.ShouldBe(new DateTimeOffset(2026, 9, 15, 18, 30, 0, TimeSpan.Zero));
        kech.LocalDate.ShouldBe(new DateOnly(2026, 9, 15));

        var tun = otishlar.Single(otish => otish.RawPersonId == "tun-" + belgi);
        tun.OccurredAt.ShouldBe(new DateTimeOffset(2026, 9, 15, 19, 30, 0, TimeSpan.Zero));
        tun.OccurredAt.UtcDateTime.Date.ShouldBe(new DateTime(2026, 9, 15), "sinov sharti: UTC kuni boshqa");
        tun.LocalDate.ShouldBe(new DateOnly(2026, 9, 16));

        var utc = otishlar.Single(otish => otish.RawPersonId == "utc-" + belgi);
        utc.OccurredAt.ShouldBe(kech.OccurredAt);
        utc.LocalDate.ShouldBe(new DateOnly(2026, 9, 15));
        utc.DeviceOffsetMinutes.ShouldBe((short)0);
        utc.OffsetAssumed.ShouldBeFalse();
    }

    // ────────────────────────────────────────────────────────────
    //  Muhit tayyorlash
    // ────────────────────────────────────────────────────────────

    private static readonly Uri SarlavhaYoli = new("/api/attendance/ingest", UriKind.Relative);

    /// <summary>Soati qotirilgan xostning «hozir»i (UTC).</summary>
    private static DateTimeOffset Hozir => AttendanceIngestHost.QotirilganHozir;

    /// <summary>Qotirilgan kun (2026-09-16) ning Toshkent soatidagi lahzasi.</summary>
    private static DateTimeOffset Toshkentda(int soat, int daqiqa)
        => new(2026, 9, 16, soat, daqiqa, 0, BonviTime.TashkentOffset);

    private static string Belgi() => Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..8];

    /// <summary>Hikvision hodisasi — vaqt XOM satr (siljishsiz ham bo'lishi mumkin), <c>serialNo</c> ixtiyoriy.</summary>
    private static string HikvisionXom(string dateTime, string shaxs, long? serialNo, string holat = "checkIn")
    {
        var seriya = serialNo is { } raqam
            ? string.Create(CultureInfo.InvariantCulture, $"\"serialNo\": {raqam},")
            : string.Empty;

        return $$"""
            {
              "dateTime": "{{dateTime}}",
              "eventType": "AccessControllerEvent",
              "AccessControllerEvent": {
                "majorEventType": 5,
                "subEventType": 75,
                "employeeNoString": "{{shaxs}}",
                {{seriya}}
                "currentVerifyMode": "face",
                "attendanceStatus": "{{holat}}"
              }
            }
            """;
    }

    /// <summary>Oddiy JSON (Generic) tanasi — aynan yuboriladigan BAYTLAR (imzo shular ustida).</summary>
    private static byte[] GenericJson(params (string Shaxs, string Vaqt, string Yonalish, string EventId)[] hodisalar)
        => JsonSerializer.SerializeToUtf8Bytes(new
        {
            events = hodisalar.Select(hodisa => new
            {
                personId = hodisa.Shaxs,
                occurredAt = hodisa.Vaqt,
                direction = hodisa.Yonalish,
                eventId = hodisa.EventId,
            }),
        });

    /// <summary><c>v1=&lt;hex HMAC-SHA256(tana, sir)&gt;</c> — SPEC §3.2 dagi shakl.</summary>
    private static string Imzo(string sir, byte[] tana)
        => AttendanceDeviceAuthenticator.SignaturePrefix
            + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(sir), tana));

    /// <summary>Sarlavhali marshrutga JSON baytlari — ixtiyoriy imzo va proksi yozgan manba bilan.</summary>
    private static HttpRequestMessage JsonBaytlar(string? token, byte[] tana, string? imzo = null, string? manba = null)
    {
        var mazmun = new ByteArrayContent(tana);
        mazmun.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var sorov = new HttpRequestMessage(HttpMethod.Post, SarlavhaYoli) { Content = mazmun };

        if (token is not null)
        {
            sorov.Headers.Add(AttendanceIngestEndpoints.TokenHeader, token);
        }

        if (imzo is not null)
        {
            sorov.Headers.Add(AttendanceIngestEndpoints.SignatureHeader, imzo);
        }

        if (manba is not null)
        {
            // Prod'da Caddy yozadi; `UseForwardedHeaders` uni haqiqiy manbaga aylantiradi.
            sorov.Headers.Add("X-Forwarded-For", manba);
        }

        return sorov;
    }

    /// <summary>Sarlavhali marshrutga Hikvision multipart (suratsiz).</summary>
    private static HttpRequestMessage SarlavhaliMultipart(string token, string hodisa)
    {
        var sorov = new HttpRequestMessage(HttpMethod.Post, SarlavhaYoli) { Content = HikvisionTanasi(hodisa, null) };
        sorov.Headers.Add(AttendanceIngestEndpoints.TokenHeader, token);
        return sorov;
    }

    /// <summary>Xato javobi — holat tekshiriladi, tana ProblemDetails sifatida qaytadi.</summary>
    private static async Task<ProblemResponse> XatoAsync(HttpResponseMessage javob, HttpStatusCode holat, string? izoh = null)
    {
        javob.StatusCode.ShouldBe(holat, $"{izoh}: {await MatnAsync(javob)}");

        return (await javob.Content.ReadFromJsonAsync<ProblemResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    /// <summary>
    /// Qurilmaning o'tish qatorlari JSON ko'rinishida — <c>id</c> va <c>device_id</c> dan boshqa HAR BIR
    /// ustun (keyin qo'shilgan ustun ham avtomatik kiradi).
    /// </summary>
    private async Task<List<string>> QatorlarJsonAsync(Guid qurilmaId)
    {
        await using var qamrov = _host.Factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        return await kontekst.Database
            .SqlQuery<string>($"SELECT (to_jsonb(p) - 'id' - 'device_id')::text AS \"Value\" FROM attendance_passes AS p WHERE p.device_id = {qurilmaId}")
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private static Uri YolTokenBilan(string token) => new($"/api/attendance/ingest/{token}", UriKind.Relative);

    private static DateTimeOffset HozirToshkentda()
    {
        var hozir = DateTimeOffset.UtcNow.ToOffset(BonviTime.TashkentOffset);

        // Soniyagacha: qurilma ham shunday yozadi va solishtirish aniq bo'ladi.
        return new DateTimeOffset(hozir.Year, hozir.Month, hozir.Day, hozir.Hour, hozir.Minute, hozir.Second, hozir.Offset);
    }

    private static string IsoVaqt(DateTimeOffset vaqt)
        => vaqt.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);

    private static string HikvisionHodisasi(DateTimeOffset vaqt, string shaxs, long serialNo, int subEventType = 75)
        => $$"""
            {
              "ipAddress": "10.0.0.42",
              "macAddress": "aa:bb:cc:dd:ee:ff",
              "dateTime": "{{IsoVaqt(vaqt)}}",
              "eventType": "AccessControllerEvent",
              "eventState": "active",
              "AccessControllerEvent": {
                "deviceName": "Kirish-1",
                "majorEventType": 5,
                "subEventType": {{subEventType}},
                "employeeNoString": "{{shaxs}}",
                "name": "Karimov Aziz",
                "serialNo": {{serialNo}},
                "currentVerifyMode": "cardOrFace",
                "attendanceStatus": "checkIn",
                "doorNo": 1,
                "picturesNumber": 1
              }
            }
            """;

    private static MultipartFormDataContent HikvisionTanasi(string hodisa, byte[]? jpeg)
    {
        var tana = new MultipartFormDataContent
        {
            { new StringContent(hodisa, Encoding.UTF8, "application/json"), "event_log" },
        };

        if (jpeg is not null)
        {
            var rasm = new ByteArrayContent(jpeg);
            rasm.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            tana.Add(rasm, "Picture", "snapshot.jpg");
        }

        return tana;
    }

    private static HttpRequestMessage SarlavhaliJson(string token, string json)
    {
        var sorov = new HttpRequestMessage(HttpMethod.Post, SarlavhaYoli)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

        sorov.Headers.Add(AttendanceIngestEndpoints.TokenHeader, token);
        return sorov;
    }

    /// <param name="turi">Qurilma turi — tahlilchini shu tanlaydi.</param>
    /// <param name="faol">Faolmi.</param>
    /// <param name="sir">
    /// Berilsa — qurilma <see cref="DeviceAuthMode.TokenAndHmac"/> rejimida, sir esa xuddi B6 dagidek
    /// <see cref="ISecretProtector"/> bilan shifrlanib saqlanadi.
    /// </param>
    /// <param name="ipRoyxati">Ruxsat etilgan manzillar (CIDR yoki yakka, vergul bilan).</param>
    private async Task<(Guid Id, string Token)> QurilmaAsync(
        AttendanceProvider turi = AttendanceProvider.Hikvision,
        bool faol = true,
        string? sir = null,
        string? ipRoyxati = null)
    {
        var token = AttendanceDeviceToken.Generate();

        await using var qamrov = _host.Factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var qurilma = AttendanceDevice.Register(
            "Sinov turniketi " + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..8],
            AttendanceDeviceToken.Hash(token),
            AttendanceDeviceToken.PrefixOf(token),
            DateTimeOffset.UtcNow,
            provider: turi,
            authMode: sir is null ? DeviceAuthMode.Token : DeviceAuthMode.TokenAndHmac,
            hmacSecret: sir is null ? null : qamrov.ServiceProvider.GetRequiredService<ISecretProtector>().Protect(sir),
            allowedIps: ipRoyxati).Value;

        if (!faol)
        {
            qurilma.Deactivate();
        }

        kontekst.AttendanceDevices.Add(qurilma);
        await kontekst.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (qurilma.Id, token);
    }

    private async Task<Guid> MoslikAsync(string shaxs)
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _host.Factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var belgi = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..6];
        var xodim = Employee.Create(FullName.Create("Ketgan", $"Xodim{belgi}", null).Value).Value;
        kontekst.Employees.Add(xodim);

        kontekst.AttendanceIdentities.Add(
            AttendanceIdentity.Link(AttendanceIdentityProvider.Verifix, shaxs, xodim.Id).Value);

        await kontekst.SaveChangesAsync(bekorQilish);

        return xodim.Id;
    }

    private async Task<List<AttendancePass>> OtishlarAsync(Guid qurilmaId)
    {
        await using var qamrov = _host.Factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        return await kontekst.AttendancePasses
            .AsNoTracking()
            .Where(otish => otish.DeviceId == qurilmaId)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<int> JamiOtishlarAsync()
    {
        await using var qamrov = _host.Factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        return await kontekst.AttendancePasses.CountAsync(TestContext.Current.CancellationToken);
    }

    private async Task<AttendanceDevice> QurilmaniOqiAsync(Guid qurilmaId)
    {
        await using var qamrov = _host.Factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        return await kontekst.AttendanceDevices
            .AsNoTracking()
            .SingleAsync(qurilma => qurilma.Id == qurilmaId, TestContext.Current.CancellationToken);
    }

    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

    /// <summary>
    /// Konsol chiqishini ushlab turadi va asl chiqishga ham uzatadi.
    /// </summary>
    /// <remarks>
    /// Asl chiqish YO'QOLMAYDI: parallel ishlayotgan boshqa test sinflarining
    /// logi ham shu oynada o'tadi va u odatdagidek ko'rinishi kerak.
    /// </remarks>
    private sealed class KonsolTutqichi : IDisposable
    {
        private readonly TextWriter _asl = Console.Out;
        private readonly StringWriter _nusxa = new(CultureInfo.InvariantCulture);

        public KonsolTutqichi() => Console.SetOut(new Ikkiga(_asl, _nusxa));

        public string Yigilgan()
        {
            lock (_nusxa)
            {
                return _nusxa.ToString();
            }
        }

        public void Dispose()
        {
            Console.SetOut(_asl);
            _nusxa.Dispose();
        }

        private sealed class Ikkiga(TextWriter asl, StringWriter nusxa) : TextWriter
        {
            public override Encoding Encoding => asl.Encoding;

            public override void Write(char value)
            {
                asl.Write(value);

                lock (nusxa)
                {
                    nusxa.Write(value);
                }
            }

            public override void Write(string? value)
            {
                asl.Write(value);

                lock (nusxa)
                {
                    nusxa.Write(value);
                }
            }

            public override void Write(char[] buffer, int index, int count)
            {
                asl.Write(buffer, index, count);

                lock (nusxa)
                {
                    nusxa.Write(buffer, index, count);
                }
            }

            public override void Flush() => asl.Flush();
        }
    }
}

/// <summary>
/// Qurilma tokenini log yozuvidan yashiruvchi — konveyersiz, aniq tekshiruv.
/// </summary>
/// <remarks>
/// Uchi-uchiga test (<see cref="AttendanceIngestApiTests.Yoldagi_token_logga_tushmaydi"/>)
/// «token chiqmadi» deydi; bu yerda esa «yo'l <c>***</c> bilan CHIQDI» degan
/// ijobiy tomon ham tekshiriladi — yozuvni butunlay yutib yuboradigan
/// tozalagich ham birinchi testdan o'tib ketardi.
/// </remarks>
public sealed class IngestTokenScrubberTests
{
    private static readonly MessageTemplateParser Tahlilchi = new();

    private const string Token = "Nx9PVwbK5aQ2Lr7Tt0mZ3cV8yB1dE4fG6hJ9kL2nP5q";

    /// <summary>Serilog so'rov logi va ASP.NET «Request starting» shaklidagi yozuvlar.</summary>
    [Theory]
    [InlineData("HTTP {RequestMethod} {RequestPath} responded {StatusCode}", "RequestPath")]
    [InlineData("Request starting {Protocol} {Method} {Scheme}://{Host}{PathBase}{Path}{QueryString}", "Path")]
    [InlineData("Qandaydir yangi yozuv: {Url}", "Url")]
    public void Yoldagi_token_barcha_satr_maydonlaridan_yashiriladi(string shablon, string maydon)
    {
        var yozuv = new LogEvent(
            DateTimeOffset.UnixEpoch,
            LogEventLevel.Information,
            exception: null,
            Tahlilchi.Parse(shablon),
            [new LogEventProperty(maydon, new ScalarValue("/API/Attendance/Ingest/" + Token))]);

        new IngestTokenScrubber().Enrich(yozuv, propertyFactory: null!);

        var matn = yozuv.RenderMessage(CultureInfo.InvariantCulture);
        matn.ShouldNotContain(Token);
        matn.ShouldContain("/API/Attendance/Ingest/***");
    }

    /// <summary>Boshqa yo'llar va sarlavha shaklidagi qabul (<c>/api/attendance/ingest</c>) tegilmaydi.</summary>
    [Theory]
    [InlineData("/api/attendance/ingest")]
    [InlineData("/api/employees/0199a1b2-0000-7000-8000-000000000001")]
    public void Boshqa_yollar_ozgarmaydi(string yol)
        => IngestTokenScrubber.Scrub(yol).ShouldBe(yol);

    /// <summary>Fayl jurnali: istisno matnidagi yo'l ham tozalanadi.</summary>
    [Fact]
    public void Fayl_jurnalida_istisno_matnidagi_token_ham_yashiriladi()
    {
        var yozuv = new LogEvent(
            DateTimeOffset.UnixEpoch,
            LogEventLevel.Error,
            new InvalidOperationException("So'rov yiqildi: POST /api/attendance/ingest/" + Token + " ?x=1"),
            Tahlilchi.Parse("Tutilmagan istisno: {Method} {Path}"),
            [
                new LogEventProperty("Method", new ScalarValue("POST")),
                new LogEventProperty("Path", new ScalarValue("/api/attendance/ingest/" + Token)),
            ]);

        using var chiqish = new StringWriter(CultureInfo.InvariantCulture);
        new ErrorLogFormatter().Format(yozuv, chiqish);

        var matn = chiqish.ToString();
        matn.ShouldNotContain(Token);
        matn.ShouldContain(IngestTokenScrubber.MaskedPath);
    }
}
