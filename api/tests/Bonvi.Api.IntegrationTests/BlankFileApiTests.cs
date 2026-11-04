using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Blanks.Pdf;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Blanks;
using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// «Blankalar» — fayllar va PDF (SPEC-blankalar §7 #17–#20, §8, §10) uchi-uchiga.
/// </summary>
/// <remarks>
/// Har test o'z shabloni (tasodifiy kod) bilan ishlaydi; ta'rif shu yerda yozilgan — seederga tayanmaydi.
/// </remarks>
public sealed class BlankFileApiTests : IClassFixture<BonviApiFactory>
{
    private const string Yol = "/api/blanks";

    private const string DocxMain = "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";
    private const string DocmMain = "application/vnd.ms-word.document.macroEnabled.main+xml";
    private const string DocxType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    /// <summary>1×1 PNG — qo'lda imzo o'rnida.</summary>
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private readonly BonviApiFactory _factory;

    public BlankFileApiTests(BonviApiFactory factory) => _factory = factory;

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
        var fayl = await YuklaOkAsync(hr, blanka.Id, "evidence", Pdf(), "a.pdf");

        using var tokensiz = _factory.CreateClient();
        using var begona = await MijozAsync("employees.view");

        foreach (var (mijoz, kutilgan) in new[] { (tokensiz, HttpStatusCode.Unauthorized), (begona, HttpStatusCode.Forbidden) })
        {
            using (var javob = await YuklaAsync(mijoz, blanka.Id, "evidence", Pdf(), "b.pdf"))
            {
                javob.StatusCode.ShouldBe(kutilgan, "POST attachments");
            }

            foreach (var (usul, yol) in Endpointlar(blanka.Id, fayl.Id))
            {
                using var javob = await mijoz.SendAsync(new HttpRequestMessage(new HttpMethod(usul), new Uri(yol, UriKind.Relative)), Ct);
                javob.StatusCode.ShouldBe(kutilgan, $"{usul} {yol}");
            }
        }
    }

    [Fact]
    public async Task Har_endpoint_ruxsati_bilan_2xx_koruvchi_ozgartira_olmaydi()
    {
        var dunyo = await DunyoAsync();
        using var hr = await MijozAsync("blanks.view", "blanks.fill");
        using var koruvchi = await MijozAsync("blanks.view");
        var blanka = await YaratAsync(hr, dunyo);

        // #17
        var fayl = await YuklaOkAsync(hr, blanka.Id, "evidence", Pdf(), "dalil.pdf");
        fayl.FieldPath.ShouldBe("evidence");
        fayl.Kind.ShouldBe("Pdf");
        fayl.FileName.ShouldBe("dalil.pdf");

        // «blanks.view» o'zi — o'zgartiruvchilar handlerda yopiq («blanka.ruxsat_yoq»).
        using (var javob = await YuklaAsync(koruvchi, blanka.Id, "evidence", Pdf(), "c.pdf"))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            (await KodAsync(javob)).ShouldBe("blanka.ruxsat_yoq");
        }

        var joriy = await OlAsync(koruvchi, blanka.Id);
        using (var javob = await OchirAsync(koruvchi, blanka.Id, fayl.Id, joriy.Version))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        // #19, #20 — «blanks.view» yetadi.
        foreach (var (usul, yol) in Endpointlar(blanka.Id, fayl.Id).Where(e => e.Usul == "GET"))
        {
            using var javob = await koruvchi.GetAsync(new Uri(yol, UriKind.Relative), Ct);
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, yol);
        }

        // #18
        using (var javob = await OchirAsync(hr, blanka.Id, fayl.Id, joriy.Version))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Yuklash: maydon, tur, hajm, soni (§8)
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Yuklash_maydon_chegaralari_boyicha()
    {
        var dunyo = await DunyoAsync();
        using var hr = await MijozAsync("blanks.view", "blanks.fill");
        var blanka = await YaratAsync(hr, dunyo);
        blanka.Missing.ShouldContain("evidence");

        // PDF maydoni: PDF qabul, Word — tur xato.
        var birinchi = await YuklaOkAsync(hr, blanka.Id, "evidence", Pdf(), "../../etc/pass‮wd.pdf");
        birinchi.FileName.ShouldBe("passwd.pdf");
        await RadAsync(hr, blanka.Id, "evidence", Docx(), "x.docx", "blanka.fayl_turi");

        // Javobga bog'landi, to'liqlik yangilandi.
        var keyin = await OlAsync(hr, blanka.Id);
        keyin.Answers.GetProperty("evidence").EnumerateArray().Select(id => id.GetGuid()).ShouldBe([birinchi.Id]);
        keyin.Missing.ShouldNotContain("evidence");
        keyin.Version.ShouldBeGreaterThan(blanka.Version);

        // Office maydoni: .docx qabul (nomi .pdf bo'lsa ham — kengaytma mazmundan), .docm/eski/exe rad.
        var word = await YuklaOkAsync(hr, blanka.Id, "docs", Docx(), "shartnoma.pdf");
        word.Kind.ShouldBe("Docx");
        word.FileName.ShouldBe("shartnoma.docx");
        await RadAsync(hr, blanka.Id, "docs", Docm(), "makro.docm", "blanka.fayl_makro");
        await RadAsync(hr, blanka.Id, "docs", [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, .. new byte[512]], "eski.doc", "blanka.fayl_eski_format");
        await RadAsync(hr, blanka.Id, "docs", [(byte)'M', (byte)'Z', .. new byte[512]], "virus.exe", "blanka.fayl_turi");
        await RadAsync(hr, blanka.Id, "docs", [], "bosh.docx", "blanka.fayl_bosh");

        // Hajm: maydon chegarasi 1 MB.
        await RadAsync(hr, blanka.Id, "evidence", [.. Pdf(), .. new byte[1024 * 1024]], "katta.pdf", "blanka.fayl_katta");

        // Soni: maxFiles 2.
        await YuklaOkAsync(hr, blanka.Id, "evidence", Pdf(), "ikkinchi.pdf");
        await RadAsync(hr, blanka.Id, "evidence", Pdf(), "uchinchi.pdf", "blanka.fayl_soni");

        // Maydon: yo'q, fayl emas, jadval qatori saqlanmagan → 400 «blanka.notogri».
        await RadAsync(hr, blanka.Id, "nomalum", Pdf(), "a.pdf", "blanka.notogri");
        await RadAsync(hr, blanka.Id, "reason", Pdf(), "a.pdf", "blanka.notogri");
        await RadAsync(hr, blanka.Id, "items[0].doc", Pdf(), "a.pdf", "blanka.notogri");

        // Jadval katagi: qator saqlangandan keyin.
        await SaqlaAsync(hr, await OlAsync(hr, blanka.Id), new
        {
            employee = keyin.Answers.GetProperty("employee"),
            evidence = (await OlAsync(hr, blanka.Id)).Answers.GetProperty("evidence"),
            docs = new[] { word.Id },
            items = new[] { new { name = "Noutbuk" } },
        });
        var katak = await YuklaOkAsync(hr, blanka.Id, "items[0].doc", Png, "rasm.png");
        katak.FieldPath.ShouldBe("items[0].doc");
        katak.Kind.ShouldBe("Png");
        (await OlAsync(hr, blanka.Id)).Answers.GetProperty("items")[0].GetProperty("doc")[0].GetGuid().ShouldBe(katak.Id);
    }

    [Fact]
    public async Task Tahrirlanmaydigan_bolim_409()
    {
        var dunyo = await DunyoAsync();
        using var hr = await MijozAsync("blanks.view", "blanks.fill", "blanks.approve");
        var blanka = await YaratAsync(hr, dunyo);

        // Qoralamada HR qadami bo'limi yopiq.
        using (var javob = await YuklaAsync(hr, blanka.Id, "hrFile", Pdf(), "a.pdf"))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            (await KodAsync(javob)).ShouldBe("blanka.maydon_tahrirlanmaydi");
        }

        // Kelishuvda: ochiq «hr» qadamining bo'limi ochiladi, qoralama bo'limi yopiladi.
        var fayl = await YuklaOkAsync(hr, blanka.Id, "evidence", Pdf(), "a.pdf");
        var yuborilgan = await YuborishAsync(hr, await OlAsync(hr, blanka.Id));
        yuborilgan.Status.ShouldBe("InApproval");

        using (var javob = await YuklaAsync(hr, blanka.Id, "evidence", Pdf(), "b.pdf"))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            (await KodAsync(javob)).ShouldBe("blanka.maydon_tahrirlanmaydi");
        }

        using (var javob = await OchirAsync(hr, blanka.Id, fayl.Id, yuborilgan.Version))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            (await KodAsync(javob)).ShouldBe("blanka.maydon_tahrirlanmaydi");
        }

        (await YuklaOkAsync(hr, blanka.Id, "hrFile", Pdf(), "hr.pdf")).FieldPath.ShouldBe("hrFile");

        // «either» qadamida «blanks.fill» egasi planshet operatori sifatida ijrochi (§6.2) — bo'lim unga ham ochiq.
        using var operatorMijoz = await MijozAsync("blanks.view", "blanks.fill");
        (await YuklaOkAsync(operatorMijoz, blanka.Id, "hrFile", Pdf(), "c.pdf")).FieldPath.ShouldBe("hrFile");
    }

    // ────────────────────────────────────────────────────────────
    //  Olib tashlash va yuklab olish
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Olib_tashlash_javobdan_chiqaradi_eski_versiya_409()
    {
        var dunyo = await DunyoAsync();
        using var hr = await MijozAsync("blanks.view", "blanks.fill");
        var blanka = await YaratAsync(hr, dunyo);
        var fayl = await YuklaOkAsync(hr, blanka.Id, "evidence", Pdf(), "a.pdf");
        var joriy = await OlAsync(hr, blanka.Id);

        using (var javob = await OchirAsync(hr, blanka.Id, fayl.Id, blanka.Version))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            (await KodAsync(javob)).ShouldBe("blanka.ozgargan");
        }

        using (var javob = await OchirAsync(hr, blanka.Id, fayl.Id, joriy.Version))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        var keyin = await OlAsync(hr, blanka.Id);
        keyin.Answers.TryGetProperty("evidence", out _).ShouldBeFalse();
        keyin.Missing.ShouldContain("evidence");
        keyin.Attachments.ShouldBeEmpty();

        using (var javob = await hr.GetAsync(new Uri($"{Yol}/submissions/{blanka.Id}/attachments/{fayl.Id}", UriKind.Relative), Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await KodAsync(javob)).ShouldBe("blanka.fayl_topilmadi");
        }

        using (var javob = await OchirAsync(hr, blanka.Id, fayl.Id, keyin.Version))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }
    }

    [Fact]
    public async Task Yuklab_olish_sarlavhalari()
    {
        var dunyo = await DunyoAsync();
        using var hr = await MijozAsync("blanks.view", "blanks.fill");
        var blanka = await YaratAsync(hr, dunyo);
        var pdf = await YuklaOkAsync(hr, blanka.Id, "evidence", Pdf(), "dalil.pdf");
        var word = await YuklaOkAsync(hr, blanka.Id, "docs", Docx(), "Шартнома.docx");

        using (var javob = await hr.GetAsync(new Uri($"{Yol}/submissions/{blanka.Id}/attachments/{pdf.Id}", UriKind.Relative), Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK);
            javob.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
            javob.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("inline");
            javob.Content.Headers.ContentDisposition.FileName!.Trim('"').ShouldBe("dalil.pdf");
            javob.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
            (await javob.Content.ReadAsByteArrayAsync(Ct)).ShouldBe(Pdf());
        }

        using (var javob = await hr.GetAsync(new Uri($"{Yol}/submissions/{blanka.Id}/attachments/{word.Id}", UriKind.Relative), Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK);
            javob.Content.Headers.ContentType!.MediaType.ShouldBe(DocxType);
            javob.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
            javob.Content.Headers.ContentDisposition.FileNameStar.ShouldBe("Шартнома.docx");
            javob.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        }
    }

    // ────────────────────────────────────────────────────────────
    //  PDF (§10)
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Pdf_har_qanday_holatda_chiziladi()
    {
        var dunyo = await DunyoAsync();
        using var hr = await MijozAsync("blanks.view", "blanks.fill");
        var blanka = await YaratAsync(hr, dunyo);
        await YuklaOkAsync(hr, blanka.Id, "evidence", Pdf(), "dalil.pdf");

        using var javob = await hr.GetAsync(new Uri($"{Yol}/submissions/{blanka.Id}/pdf", UriKind.Relative), Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK);
        javob.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        javob.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("inline");
        javob.Content.Headers.ContentDisposition.FileName!.Trim('"').ShouldBe($"{dunyo.Kod}-qoralama.pdf");
        Encoding.ASCII.GetString((await javob.Content.ReadAsByteArrayAsync(Ct))[..5]).ShouldBe("%PDF-");

        using var yoq = await hr.GetAsync(new Uri($"{Yol}/submissions/{Guid.NewGuid()}/pdf", UriKind.Relative), Ct);
        yoq.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Yakuniy_pdf_tasdiqlanganda_saqlanadi()
    {
        var dunyo = await DunyoAsync();
        using var hr = await MijozAsync("blanks.view", "blanks.fill");
        var blanka = await YaratAsync(hr, dunyo);
        await YuklaOkAsync(hr, blanka.Id, "evidence", Pdf(), "dalil.pdf");
        (await YuborishAsync(hr, await OlAsync(hr, blanka.Id))).Status.ShouldBe("InApproval");

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var xizmat = qamrov.ServiceProvider.GetRequiredService<BlankFinalPdfService>();
        var ombor = qamrov.ServiceProvider.GetRequiredService<IFileStorage>();

        // Tasdiqlanmagan — holat xatosi, kalit yozilmaydi.
        (await xizmat.StoreFinalPdfAsync(blanka.Id, Ct)).Error.Code.ShouldBe("blanka.holat_notogri");

        // Qadamni domen orqali planshetda imzolaymiz (qadamlar endpointi boshqa bo'lakda).
        var repo = qamrov.ServiceProvider.GetRequiredService<IBlankSubmissionRepository>();
        var imzo = await ombor.SaveAsync(new MemoryStream(Png), "imzo.png", "image/png", Ct);
        var aggregat = (await repo.GetWithChildrenAsync(blanka.Id, Ct))!;
        aggregat.SignStep(
                aggregat.Steps.Single().Id,
                new BlankStepAct(BlankSignMethod.InPerson, null, dunyo.Xodim, "Guvoh Ali", imzo.Key, Comment: "Ko'rildi"),
                null,
                DateTimeOffset.UtcNow)
            .IsSuccess.ShouldBeTrue();
        aggregat.Status.ShouldBe(BlankSubmissionStatus.Approved);
        await qamrov.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);

        var natija = await xizmat.StoreFinalPdfAsync(blanka.Id, Ct);
        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Code : null);

        await using (var tekshiruv = _factory.Services.CreateAsyncScope())
        {
            var kontekst = tekshiruv.ServiceProvider.GetRequiredService<AppDbContext>();
            var saqlangan = await kontekst.BlankSubmissions.AsNoTracking().SingleAsync(row => row.Id == blanka.Id, Ct);
            saqlangan.FinalPdfStorageKey.ShouldBe(natija.Value);
        }

        await using var fayl = (await ombor.OpenAsync(natija.Value, Ct))!;
        var bosh = new byte[5];
        await fayl.ReadExactlyAsync(bosh, Ct);
        Encoding.ASCII.GetString(bosh).ShouldBe("%PDF-");

        // Tasdiqlangandan keyin fayl qo'shib bo'lmaydi.
        using var javob = await YuklaAsync(hr, blanka.Id, "evidence", Pdf(), "kech.pdf");
        javob.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await KodAsync(javob)).ShouldBe("blanka.holat_notogri");
    }

    [Fact]
    public async Task Yakuniy_pdf_qadam_tasdigi_va_qadamsiz_yuborishda_yoziladi()
    {
        // Qadamlar API orqali: «hr» elektron tasdiqlanadi → Approved → hook yakuniy PDF yozadi.
        var dunyo = await DunyoAsync();
        using var hr = await MijozAsync("blanks.view", "blanks.fill", "blanks.approve");
        var blanka = await YaratAsync(hr, dunyo);
        await YuklaOkAsync(hr, blanka.Id, "evidence", Pdf(), "dalil.pdf");
        var yuborilgan = await YuborishAsync(hr, await OlAsync(hr, blanka.Id));

        using (var javob = await hr.PostAsJsonAsync(
                   new Uri($"{Yol}/submissions/{blanka.Id}/steps/hr/approve", UriKind.Relative),
                   new { expectedVersion = yuborilgan.Version },
                   Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await javob.Content.ReadAsStringAsync(Ct));
            (await javob.Content.ReadFromJsonAsync<BlankaJavobi>(Ct))!.Status.ShouldBe("Approved");
        }

        await YakuniyPdfBorAsync(blanka.Id);

        // Qadamsiz shablon: yuborish darhol Approved → o'sha hook.
        var qadamsiz = await DunyoAsync(qadamlar: false);
        var ikkinchi = await YaratAsync(hr, qadamsiz);
        await YuklaOkAsync(hr, ikkinchi.Id, "evidence", Pdf(), "dalil.pdf");
        (await YuborishAsync(hr, await OlAsync(hr, ikkinchi.Id))).Status.ShouldBe("Approved");

        await YakuniyPdfBorAsync(ikkinchi.Id);
    }

    private async Task YakuniyPdfBorAsync(Guid blanka)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
        var kalit = (await kontekst.BlankSubmissions.AsNoTracking().SingleAsync(row => row.Id == blanka, Ct)).FinalPdfStorageKey;
        kalit.ShouldNotBeNull();

        await using var fayl = (await qamrov.ServiceProvider.GetRequiredService<IFileStorage>().OpenAsync(kalit, Ct))!;
        var bosh = new byte[5];
        await fayl.ReadExactlyAsync(bosh, Ct);
        Encoding.ASCII.GetString(bosh).ShouldBe("%PDF-");
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    private sealed record Dunyo(string Kod, Guid Xodim);

    private sealed record FaylJavobi(Guid Id, string FieldPath, string Kind, string FileName, long SizeBytes);

    private sealed record BlankaJavobi(
        Guid Id,
        string Status,
        int Version,
        JsonElement Answers,
        List<string> Missing,
        List<FaylJavobi> Attachments);

    private static IEnumerable<(string Usul, string Yol)> Endpointlar(Guid blanka, Guid fayl) =>
    [
        ("DELETE", $"{Yol}/submissions/{blanka}/attachments/{fayl}?expectedVersion=0"),
        ("GET", $"{Yol}/submissions/{blanka}/attachments/{fayl}"),
        ("GET", $"{Yol}/submissions/{blanka}/pdf"),
    ];

    private static byte[] Pdf() => Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\n%%EOF");

    private static byte[] Docx() => Ooxml(DocxMain);

    private static byte[] Docm() => Ooxml(DocmMain);

    /// <summary>Eng kichik OOXML paketi: content types, rels va asosiy qism.</summary>
    private static byte[] Ooxml(string mainType)
    {
        var qismlar = new (string Nom, string Matn)[]
        {
            ("[Content_Types].xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>"
                + "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
                + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"
                + "<Default Extension=\"xml\" ContentType=\"application/xml\"/>"
                + $"<Override PartName=\"/word/document.xml\" ContentType=\"{mainType}\"/></Types>"),
            ("_rels/.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>"
                + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/>"
                + "</Relationships>"),
            ("word/document.xml", "<root/>"),
        };

        using var bufer = new MemoryStream();
        using (var arxiv = new ZipArchive(bufer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (nom, matn) in qismlar)
            {
                using var oqim = arxiv.CreateEntry(nom, CompressionLevel.Fastest).Open();
                oqim.Write(Encoding.UTF8.GetBytes(matn));
            }
        }

        return bufer.ToArray();
    }

    private static async Task<string?> KodAsync(HttpResponseMessage javob)
        => (await javob.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("title").GetString();

    private static async Task<HttpResponseMessage> YuklaAsync(HttpClient mijoz, Guid blanka, string maydon, byte[] baytlar, string nom)
    {
        using var forma = new MultipartFormDataContent();
        using var qism = new ByteArrayContent(baytlar);
        qism.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        forma.Add(new StringContent(maydon), "fieldPath");
        forma.Add(qism, "file", nom);

        return await mijoz.PostAsync(new Uri($"{Yol}/submissions/{blanka}/attachments", UriKind.Relative), forma, Ct);
    }

    private static async Task<FaylJavobi> YuklaOkAsync(HttpClient mijoz, Guid blanka, string maydon, byte[] baytlar, string nom)
    {
        using var javob = await YuklaAsync(mijoz, blanka, maydon, baytlar, nom);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await javob.Content.ReadAsStringAsync(Ct));
        return (await javob.Content.ReadFromJsonAsync<FaylJavobi>(Ct))!;
    }

    private static async Task RadAsync(HttpClient mijoz, Guid blanka, string maydon, byte[] baytlar, string nom, string kod)
    {
        using var javob = await YuklaAsync(mijoz, blanka, maydon, baytlar, nom);
        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest, $"{maydon} {nom}");
        (await KodAsync(javob)).ShouldBe(kod, $"{maydon} {nom}");
    }

    private static Task<HttpResponseMessage> OchirAsync(HttpClient mijoz, Guid blanka, Guid fayl, int versiya)
        => mijoz.DeleteAsync(new Uri($"{Yol}/submissions/{blanka}/attachments/{fayl}?expectedVersion={versiya}", UriKind.Relative), Ct);

    private static async Task<BlankaJavobi> OlAsync(HttpClient mijoz, Guid blanka)
        => (await mijoz.GetFromJsonAsync<BlankaJavobi>($"{Yol}/submissions/{blanka}", Ct))!;

    private static async Task<BlankaJavobi> YaratAsync(HttpClient mijoz, Dunyo dunyo)
    {
        using var javob = await mijoz.PostAsJsonAsync(
            new Uri(Yol + "/submissions", UriKind.Relative),
            new { templateCode = dunyo.Kod, employeeId = dunyo.Xodim, clientRequestId = Guid.NewGuid() },
            Ct);
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

    /// <summary>
    /// Qoralama bo'limi: majburiy PDF dalil (≤ 2 ta, ≤ 1 MB), Office hujjatlar, fayl ustunli jadval;
    /// HR qadami bo'limida yana bir fayl maydoni; bitta qadam (planshetda ham).
    /// </summary>
    private static string Tarif(string kod, bool qadamlar) => $$"""
        { "schema": 1, "code": "{{kod}}", "version": "1.0", "subject": "employee", "title": "Fayl sinovi",
          "sections": [
            { "id": "hdr", "title": "Xodim", "fields": [
              { "id": "employee", "type": "ref", "refKind": "employee", "label": "Xodim", "prefill": "subject.fullName", "readOnly": true },
              { "id": "reason", "type": "text", "label": "Sabab" },
              { "id": "evidence", "type": "file", "label": "Dalil", "accept": ["pdf"], "maxFiles": 2, "maxSizeMb": 1, "required": true },
              { "id": "docs", "type": "file", "label": "Hujjatlar", "accept": ["office"], "maxFiles": 3 },
              { "id": "items", "type": "table", "label": "Topshirilgan", "columns": [
                { "id": "name", "type": "text", "label": "Nomi" },
                { "id": "doc", "type": "file", "label": "Fayl", "accept": ["pdf", "image"], "maxFiles": 1 } ] } ] }{{(qadamlar ? HrBolimi : "")}} ],
          "steps": {{(qadamlar ? Qadam : "[]")}} }
        """;

    private const string HrBolimi = """
        ,
            { "id": "hrPart", "title": "HR", "fill": { "at": "step", "step": "hr" }, "fields": [
              { "id": "hrFile", "type": "file", "label": "HR fayli", "accept": ["pdf"] } ] }
        """;

    private const string Qadam = """[ { "id": "hr", "title": "HR", "order": 1, "actor": "hr5", "mode": "either" } ]""";

    private async Task<Dunyo> DunyoAsync(bool qadamlar = true)
    {
        var kod = "F" + string.Concat(Guid.NewGuid().ToString("N")[..8]
            .Select(belgi => (char)('A' + Convert.ToInt32(belgi.ToString(), 16))));

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var shablon = BlankTemplate.Create(kod, "Fayl sinovi " + kod, BlankSubjectKind.Employee).Value;
        shablon.Activate();
        var versiya = BlankTemplateVersion.Create(shablon.Id, "1.0", Tarif(kod, qadamlar), new string('a', 64), BlankTemplateSource.Seed).Value;
        versiya.Publish(null, DateTimeOffset.UtcNow).IsSuccess.ShouldBeTrue();
        var xodim = Employee.Create(FullName.Create("Xodim" + kod, "Ali", null).Value).Value;

        kontekst.AddRange(shablon, versiya, xodim);
        await kontekst.SaveChangesAsync(Ct);

        return new Dunyo(kod, xodim.Id);
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
