using System.Buffers.Binary;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bonvi.Application.Recruiting.Forms;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Onlayn anketa — xavfsizlik: «tuzoq» maydon, mijoz IP'si bo'yicha va kunlik umumiy tezlik
/// chegarasi, poyga, hajm va rasm chegaralari, ism bo'yicha moslik, vakansiya tekshiruvi tartibi,
/// ruxsat etilmagan belgilar.
/// </summary>
public sealed partial class KioskQuestionnaireApiTests
{
    [Fact]
    public async Task Tuzoq_maydon_toldirilsa_400_va_hech_narsa_yozilmaydi()
    {
        using var mehmon = Mehmon();

        var telefon = Telefon();
        var yuborish = Guid.NewGuid();
        var javoblar = QuestionnaireSamples.Minimal(Bugun, telefon);

        using (var bot = await YuborAsync(mehmon, javoblar, yuborish, website: "https://spam.example"))
        {
            bot.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

            using var xato = JsonDocument.Parse(await MatnAsync(bot));
            xato.RootElement.GetProperty("title").GetString().ShouldBe("anketa.belgi");
            xato.RootElement.GetProperty("detail").GetString().ShouldBe("website");
        }

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

            (await db.CandidateForms.CountAsync(form => form.ClientSubmissionId == yuborish, Ct)).ShouldBe(0);
            (await db.Candidates.CountAsync(candidate => candidate.Phone == PhoneNumber.FromTrusted(telefon), Ct)).ShouldBe(0);
        }

        // Identifikator ham band qilinmagan: o'sha anketa bo'sh tuzoq bilan — odatdagidek 201
        using var odam = await YuborAsync(mehmon, javoblar, yuborish);
        odam.StatusCode.ShouldBe(HttpStatusCode.Created, await MatnAsync(odam));
    }

    [Fact]
    public async Task Yuborish_bitta_mijoz_IPsiga_soatiga_20_ta_boshqa_IP_alohida_seans_alohida()
    {
        var ipA = NoyobIp();
        using var a = Mehmon(ipA);

        // Arzon so'rovlar — identifikatorsiz (400), lekin har biri oynani yeydi
        for (var urinish = 0; urinish < 20; urinish++)
        {
            using var javob = await YuborXomAsync(a, "{}", submissionId: null);
            javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest, $"{urinish + 1}-urinish");
        }

        using (var oshdi = await YuborXomAsync(a, "{}", submissionId: null))
        {
            oshdi.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

            using var xato = JsonDocument.Parse(await MatnAsync(oshdi));
            xato.RootElement.GetProperty("title").GetString().ShouldBe("rate_limit.oshib_ketdi");
        }

        // Haqiqiy anketa ham shu IP'dan o'tmaydi
        using (var haqiqiy = await YuborAsync(a, QuestionnaireSamples.Minimal(Bugun, Telefon()), Guid.NewGuid()))
        {
            haqiqiy.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        }

        // Seans — o'z oynasida, forma ishlashda davom etadi
        using (var seans = await a.GetAsync(new Uri(SessionPath, UriKind.Relative), Ct))
        {
            seans.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        // Boshqa IP — o'z oynasi: haqiqiy anketa qabul qilinadi
        using var b = Mehmon();
        using var boshqa = await YuborAsync(b, QuestionnaireSamples.Minimal(Bugun, Telefon()), Guid.NewGuid());
        boshqa.StatusCode.ShouldBe(HttpStatusCode.Created, await MatnAsync(boshqa));

        // Sarlavhasiz so'rov (ulanish manzili bo'yicha) ham A ning chegarasiga tushmaydi
        using var sarlavhasiz = _factory.CreateClient();
        using var sarlavhasizJavob = await YuborXomAsync(sarlavhasiz, "{}", submissionId: null);
        sarlavhasizJavob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Kunlik_umumiy_chegara_faqat_qabul_qilinganlarni_sanaydi_va_hamma_IPga_429()
    {
        await using var fabrika = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting("RateLimiting:QuestionnaireDaily:PermitLimit", "1"));

        using var a = Mehmon(fabrika, NoyobIp());
        using var b = Mehmon(fabrika, NoyobIp());

        // Rad etilgan yuborish sanalmaydi — aks holda arzon xato so'rovlar formani kunga yopardi
        using (var xato = await YuborXomAsync(a, "{}", submissionId: null))
        {
            xato.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        var birinchiYuborish = Guid.NewGuid();
        using (var birinchi = await YuborAsync(a, QuestionnaireSamples.Minimal(Bugun, Telefon()), birinchiYuborish))
        {
            birinchi.StatusCode.ShouldBe(HttpStatusCode.Created, await MatnAsync(birinchi));
        }

        // Chegara to'ldi — boshqa IP ham 429, hech narsa yozilmaydi
        var telefon = Telefon();
        var ikkinchiYuborish = Guid.NewGuid();
        using (var ikkinchi = await YuborAsync(b, QuestionnaireSamples.Minimal(Bugun, telefon), ikkinchiYuborish))
        {
            ikkinchi.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
            ikkinchi.Headers.RetryAfter.ShouldNotBeNull();

            using var xato = JsonDocument.Parse(await MatnAsync(ikkinchi));
            xato.RootElement.GetProperty("title").GetString().ShouldBe("rate_limit.oshib_ketdi");
        }

        await using var qamrov = fabrika.Services.CreateAsyncScope();
        var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        (await db.CandidateForms.CountAsync(form => form.ClientSubmissionId == ikkinchiYuborish, Ct)).ShouldBe(0);
        (await db.Candidates.CountAsync(candidate => candidate.Phone == PhoneNumber.FromTrusted(telefon), Ct)).ShouldBe(0);
        (await db.CandidateForms.CountAsync(form => form.ClientSubmissionId == birinchiYuborish, Ct)).ShouldBe(1);
    }

    [Fact]
    public async Task Bir_vaqtda_kelgan_bir_xil_yuborish_bitta_anketa_ikkalasi_2xx()
    {
        using var kiosk = Mehmon();

        var telefon = Telefon();
        var yuborish = Guid.NewGuid();
        var javoblar = QuestionnaireSamples.Minimal(Bugun, telefon);

        var javoblarJufti = await Task.WhenAll(YuborAsync(kiosk, javoblar, yuborish), YuborAsync(kiosk, javoblar, yuborish));

        try
        {
            javoblarJufti.Select(javob => javob.StatusCode).Order().ShouldBe(new[] { HttpStatusCode.OK, HttpStatusCode.Created });

            var raqamlar = await Task.WhenAll(javoblarJufti.Select(javob => javob.Content.ReadFromJsonAsync<KioskSubmittedResponse>(Ct)));
            raqamlar[0]!.FormNumber.ShouldBe(raqamlar[1]!.FormNumber, "ikkinchisi g'olibning raqamini oladi");
        }
        finally
        {
            foreach (var javob in javoblarJufti)
            {
                javob.Dispose();
            }
        }

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        (await db.CandidateForms.CountAsync(form => form.ClientSubmissionId == yuborish, Ct)).ShouldBe(1);
        (await db.Candidates.CountAsync(candidate => candidate.Phone == PhoneNumber.FromTrusted(telefon), Ct)).ShouldBe(1, "yutqazganning nomzodi ham yozilmadi");
    }

    [Fact]
    public async Task Katta_javoblar_JSON_oqilishidan_oldin_rad_etiladi_va_yashirin_belgi_ham()
    {
        using var kiosk = Mehmon();

        // Forma chegarasi (256 KB) ichida, belgi chegarasidan (256 000) katta — handler JSON'ni ochmaydi
        using (var katta = await YuborXomAsync(kiosk, new string('x', 257_000), Guid.NewGuid()))
        {
            katta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

            using var xato = JsonDocument.Parse(await MatnAsync(katta));
            xato.RootElement.GetProperty("title").GetString().ShouldBe("anketa.notogri", "«anketa.json» emas — JSON o'qilmagan");
            xato.RootElement.GetProperty("detail").GetString().ShouldBe("answers");
        }

        // Forma chegarasidan ham katta — freymvork maydonni o'qimaydi, 500 emas
        using (var juda = await YuborXomAsync(kiosk, new string('x', 300 * 1024), Guid.NewGuid()))
        {
            ((int)juda.StatusCode).ShouldBeOneOf(400, 413);
            (await MatnAsync(juda)).ShouldNotContain("anketa.json");
        }

        // Familiyada yo'nalishni almashtiruvchi belgi
        var yashirin = QuestionnaireSamples.Minimal(Bugun, Telefon()) with { LastName = "‮авомирак" };
        using var belgi = await YuborAsync(kiosk, yashirin, Guid.NewGuid());
        belgi.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using var belgiXato = JsonDocument.Parse(await MatnAsync(belgi));
        belgiXato.RootElement.GetProperty("title").GetString().ShouldBe("anketa.belgi");
        belgiXato.RootElement.GetProperty("detail").GetString().ShouldBe("lastName");
    }

    [Fact]
    public async Task Rasm_va_imzo_olchami_va_turi_dekodlashdan_oldin_tekshiriladi()
    {
        using var kiosk = Mehmon();
        var yuborishlar = new List<Guid>();

        var bomba = PngBomba(20_000, 20_000);
        bomba.Length.ShouldBeLessThan(300 * 1024, "kichik fayl, ulkan e'lon qilingan o'lcham");

        // 1. Imzo chegaradan keng (haqiqiy PNG)
        using (var keng = await YuborXomAsync(kiosk, Json(), Yangi(), signature: Png(1300, 200)))
        {
            keng.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await MatnAsync(keng)).ShouldContain("anketa.imzo_yaroqsiz");
        }

        // 2. Imzo — «bomba»: 20000×20000 e'lon qilingan, dekodlanmaydi
        using (var imzoBomba = await YuborXomAsync(kiosk, Json(), Yangi(), signature: bomba))
        {
            imzoBomba.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await MatnAsync(imzoBomba)).ShouldContain("anketa.imzo_yaroqsiz");
        }

        // 3. Surat — WebP (forma faqat JPEG/PNG yuboradi)
        using (var webp = await YuborXomAsync(kiosk, Json(), Yangi(), photo: Rasm(SKEncodedImageFormat.Webp), photoType: "image/webp"))
        {
            webp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await MatnAsync(webp)).ShouldContain("anketa.rasm_yaroqsiz");
        }

        // 4. Surat — «bomba» (400 MP > 80 MP chegara)
        using (var suratBomba = await YuborXomAsync(kiosk, Json(), Yangi(), photo: bomba, photoType: "image/png"))
        {
            suratBomba.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await MatnAsync(suratBomba)).ShouldContain("anketa.rasm_yaroqsiz");
        }

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.CandidateForms.CountAsync(form => yuborishlar.Contains(form.ClientSubmissionId), Ct)).ShouldBe(0);

        string Json() => QuestionnaireFormJson.Write(QuestionnaireSamples.Minimal(Bugun, Telefon()));

        Guid Yangi()
        {
            var id = Guid.NewGuid();
            yuborishlar.Add(id);

            return id;
        }
    }

    [Fact]
    public async Task Imzo_server_chizgan_PNG_bolib_saqlanadi_mijoz_baytlari_emas()
    {
        using var kiosk = Mehmon();

        var asl = QuestionnaireSamples.Signature();

        using var javob = await YuborAsync(kiosk, QuestionnaireSamples.Minimal(Bugun, Telefon()), Guid.NewGuid(), asl);
        javob.StatusCode.ShouldBe(HttpStatusCode.Created, await MatnAsync(javob));
        var natija = await javob.Content.ReadFromJsonAsync<KioskSubmittedResponse>(Ct);

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
        var anketa = await db.CandidateForms.AsNoTracking().SingleAsync(form => form.Number == natija!.FormNumber, Ct);

        var saqlash = qamrov.ServiceProvider.GetRequiredService<Application.Abstractions.IFileStorage>();
        var oqim = await saqlash.OpenAsync(anketa.SignatureKey, Ct);
        oqim.ShouldNotBeNull();

        using var bufer = new MemoryStream();
        await using (oqim)
        {
            await oqim!.CopyToAsync(bufer, Ct);
        }
        var saqlangan = bufer.ToArray();

        saqlangan.ShouldNotBe(asl, "shaffof PNG oq fonga qayta chizilgan");

        using var rasm = SKBitmap.Decode(saqlangan);
        rasm.Width.ShouldBe(600);
        rasm.Height.ShouldBe(200);
        rasm.GetPixel(0, 0).ShouldBe(SKColors.White, "shaffof burchak — oq");
    }

    [Fact]
    public async Task Bir_xil_telefon_boshqa_ism_yangi_nomzod_ochadi()
    {
        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        var vakansiya = await VakansiyaAsync(admin);
        var telefon = Telefon();

        var mavjud = await JarayondagiNomzodAsync(admin, vakansiya, telefon, "Каримов", "Азиз");

        using var kiosk = Mehmon();

        // Raqami bir xil, odami boshqa (G‘ulomov Sardor) — masalan bitta raqam adashgan
        using var javob = await YuborAsync(kiosk, QuestionnaireSamples.Full(Bugun, telefon) with { VacancyId = vakansiya }, Guid.NewGuid());
        javob.StatusCode.ShouldBe(HttpStatusCode.Created, await MatnAsync(javob));
        var natija = await javob.Content.ReadFromJsonAsync<KioskSubmittedResponse>(Ct);

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var anketa = await db.CandidateForms.AsNoTracking().SingleAsync(form => form.Number == natija!.FormNumber, Ct);
        anketa.CandidateId.ShouldNotBe(mavjud, "boshqa odamning kartasiga yozilmaydi");

        var yangi = await db.Candidates.AsNoTracking().SingleAsync(candidate => candidate.Id == anketa.CandidateId, Ct);
        yangi.Source.ShouldBe(CandidateSource.OnlineForm);

        var eski = await db.Candidates.AsNoTracking()
            .Include(candidate => candidate.Files)
            .Include(candidate => candidate.Questionnaire)
            .SingleAsync(candidate => candidate.Id == mavjud, Ct);

        eski.Files.ShouldBeEmpty();
        eski.Questionnaire.ShouldBeNull("begona javoblardan anketa yasalmaydi");
        eski.PhotoKey.ShouldBeNull();
        eski.BirthDate.ShouldBeNull();
    }

    [Fact]
    public async Task Vakansiya_xatosi_telefon_moslashganiga_bogliq_emas()
    {
        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        var vakansiya = await VakansiyaAsync(admin);
        var telefon = Telefon();

        // Shu odam (lotin/kirill farqsiz) — jarayonda
        var mavjud = await JarayondagiNomzodAsync(admin, vakansiya, telefon, "Ғуломов", "Сардор");

        using var kiosk = Mehmon();

        var yoq = Guid.NewGuid();

        using var mos = await YuborAsync(kiosk, QuestionnaireSamples.Full(Bugun, telefon) with { VacancyId = yoq }, Guid.NewGuid());
        using var begona = await YuborAsync(kiosk, QuestionnaireSamples.Full(Bugun, Telefon()) with { VacancyId = yoq }, Guid.NewGuid());

        mos.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        begona.StatusCode.ShouldBe(mos.StatusCode, "javob raqam voronkada bor-yo'qligini bildirmasin");

        using var mosXato = JsonDocument.Parse(await MatnAsync(mos));
        using var begonaXato = JsonDocument.Parse(await MatnAsync(begona));
        mosXato.RootElement.GetProperty("title").GetString().ShouldBe("vakansiya.topilmadi");
        begonaXato.RootElement.GetProperty("title").GetString().ShouldBe("vakansiya.topilmadi");

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.CandidateForms.CountAsync(form => form.CandidateId == mavjud, Ct)).ShouldBe(0);

        // To'g'ri vakansiya bilan — o'sha odamning kartasiga (ism kirillda, anketa lotinda)
        using var togri = await YuborAsync(kiosk, QuestionnaireSamples.Full(Bugun, telefon) with { VacancyId = vakansiya }, Guid.NewGuid());
        togri.StatusCode.ShouldBe(HttpStatusCode.Created, await MatnAsync(togri));
        (await db.CandidateForms.CountAsync(form => form.CandidateId == mavjud, Ct)).ShouldBe(1);
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    /// <summary>Xom so'rov: javoblar matni, surat va imzo istalgancha.</summary>
    private static async Task<HttpResponseMessage> YuborXomAsync(
        HttpClient client,
        string answers,
        Guid? submissionId,
        byte[]? photo = null,
        string photoType = "image/jpeg",
        byte[]? signature = null)
    {
        using var forma = new MultipartFormDataContent
        {
            { new StringContent(answers, Encoding.UTF8), "answers" },
            { new StringContent("uz-Latn"), "language" },
        };

        if (submissionId is { } id)
        {
            forma.Add(new StringContent(id.ToString()), "clientSubmissionId");
        }

        var rasm = new ByteArrayContent(photo ?? QuestionnaireSamples.Photo());
        rasm.Headers.ContentType = new MediaTypeHeaderValue(photoType);
        forma.Add(rasm, "photo", "selfi");

        var imzo = new ByteArrayContent(signature ?? QuestionnaireSamples.Signature());
        imzo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        forma.Add(imzo, "signature", "imzo.png");

        return await client.PostAsync(new Uri(SubmitPath, UriKind.Relative), forma, Ct);
    }

    /// <summary>HR kiritgan jarayondagi nomzod (anketasiz, suratsiz, tug'ilgan sanasiz).</summary>
    private static async Task<Guid> JarayondagiNomzodAsync(HttpClient admin, Guid vakansiya, string telefon, string familiya, string ism)
    {
        using var javob = await admin.PostAsJsonAsync(
            new Uri("/api/recruiting/candidates", UriKind.Relative),
            new { vacancyId = vakansiya, lastName = familiya, firstName = ism, phone = telefon, referralChannelId = ReferralSeeds.Telegram },
            Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        using var hujjat = JsonDocument.Parse(await MatnAsync(javob));
        return hujjat.RootElement.GetProperty("id").GetGuid();
    }

    private static byte[] Png(int width, int height)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.Transparent);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        return data.ToArray();
    }

    private static byte[] Rasm(SKEncodedImageFormat format)
    {
        using var surface = SKSurface.Create(new SKImageInfo(400, 400));
        surface.Canvas.Clear(SKColors.SteelBlue);

        using var image = surface.Snapshot();
        using var data = image.Encode(format, 80);

        return data.ToArray();
    }

    /// <summary>
    /// «Siqilgan bomba» PNG: 1 bitli kulrang, o'lchami ulkan, fayli kichik (nollar zlib'da
    /// ~1000 marta qisqaradi). To'liq dekodlansa RGBA'da gigabaytlar kerak bo'lardi.
    /// </summary>
    private static byte[] PngBomba(int width, int height)
    {
        var rowBytes = 1 + ((width + 7) / 8);

        using var idat = new MemoryStream();
        using (var zlib = new ZLibStream(idat, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            var row = new byte[rowBytes];
            for (var y = 0; y < height; y++)
            {
                zlib.Write(row);
            }
        }

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), height);
        header[8] = 1; // bit chuqurligi
        header[9] = 0; // kulrang

        using var png = new MemoryStream();
        png.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);
        Chunk(png, "IHDR", header);
        Chunk(png, "IDAT", idat.ToArray());
        Chunk(png, "IEND", []);

        return png.ToArray();

        static void Chunk(Stream output, string type, byte[] data)
        {
            Span<byte> length = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
            output.Write(length);

            var typeBytes = Encoding.ASCII.GetBytes(type);
            output.Write(typeBytes);
            output.Write(data);

            Span<byte> crc = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32([.. typeBytes, .. data]));
            output.Write(crc);
        }

        static uint Crc32(byte[] bytes)
        {
            var crc = 0xFFFFFFFFu;

            foreach (var value in bytes)
            {
                crc ^= value;
                for (var bit = 0; bit < 8; bit++)
                {
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
                }
            }

            return ~crc;
        }
    }
}
