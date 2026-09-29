using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Bonvi.Application.Common.Documents;
using Bonvi.Application.Identity.Abstractions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Nomzodga biriktirilgan fayl — javobdagi qatori.</summary>
public sealed record CandidateFileResponse(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset UploadedAt);

/// <summary>Nomzod kartasi — fayllar testiga kerakli qismi.</summary>
public sealed record CandidateFileCardResponse(
    Guid Id,
    string Status,
    IReadOnlyList<CandidateFileResponse> Files);

/// <summary>Ro'yxat qatori — «qog'oz qisqichi» bayrog'i.</summary>
public sealed record CandidateFileRowResponse(Guid Id, bool HasResume);

/// <summary>Sahifalangan ro'yxat — fayllar testiga kerakli qismi.</summary>
public sealed record CandidateFilePageResponse(IReadOnlyList<CandidateFileRowResponse> Items, int Total);

/// <summary>
/// Nomzod fayllari — uchi-uchiga: yuklash, yuklab olish, o'chirish,
/// ruxsatlar va BEGONA faylning berilmasligi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega alohida sinf.</b> <see cref="RecruitingApiTests"/> voronkaning
/// yo'lini tekshiradi va u allaqachon eng katta test sinfi; fayllar esa
/// o'z quvuri, o'z chegaralari va o'z ruxsat matritsasi bilan keladi.
/// <c>EmployeeDocumentApiTests</c> bilan bir xil taqsimot.
/// </para>
/// <para>
/// <b>Mock yo'q — HAQIQIY baytlar.</b> Haqiqiy PNG va shovqinli JPEG
/// (<see cref="TestImages"/>) server orqali o'tadi, diskka tushadi va
/// qaytib o'qiladi. Kichraytirishning O'ZI birlik darajasida
/// (<c>ScaledDocumentStoreTests</c>) tekshirilgan — bu yerda quvurning
/// HTTP shartnomaga TO'G'RI ULANGANI qadaladi: bitta chaqiruv tushib
/// qolsa, fayllar baribir saqlanardi va muammo faqat disk to'lganda
/// ma'lum bo'lardi.
/// </para>
/// </remarks>
public sealed class CandidateFileApiTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public CandidateFileApiTests(BonviApiFactory factory) => _factory = factory;

    // ────────────────────────────────────────────────────────────
    //  To'liq hayot sikli
    // ────────────────────────────────────────────────────────────

    /// <summary>Rasm yuklanadi, KICHRAYADI, qaytib o'qiladi va o'chiriladi.</summary>
    /// <remarks>
    /// ⭐ Eng muhim shart — <c>sizeBytes</c>. Eski rezyume kodi mijoz e'lon
    /// qilgan hajmni yozardi; rasm serverda kichraytirilgach o'sha son
    /// YOLG'ONGA aylanadi (4 MB lik telefon surati diskda ~300 KB). Test
    /// javobdagi sonni DISKDAN qaytgan baytlar bilan solishtiradi — bu
    /// yagona tekshiruv, chunki «kichrayibdi» degan gap o'zi hech narsani
    /// isbotlamaydi.
    /// </remarks>
    [Fact]
    public async Task Rasmning_toliq_hayot_sikli_ishlaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var mijoz = await AdminMijozAsync();
        var nomzod = await NomzodAsync(mijoz);

        // A4 skanerdan chiqadigan o'lcham, sifati 100 — mijozning shikoyati
        // aynan shundan boshlangan.
        var manba = TestImages.JpegYasa(3000, 2250, sifat: 100);

        CandidateFileResponse yuklangan;

        using (var yuklash = await FaylYuborAsync(mijoz, nomzod, manba, "Паспорт 1-бет.jpg", "image/jpeg"))
        {
            yuklash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yuklash));

            yuklangan = (await yuklash.Content.ReadFromJsonAsync<CandidateFileResponse>(bekorQilish))!;
            yuklangan.FileName.ShouldBe("Паспорт 1-бет.jpg", "asl nom KO'RSATISH uchun saqlanadi");
            yuklangan.ContentType.ShouldBe("image/jpeg");
        }

        // ── Yuklab olish: aynan shu baytlar va ASL nom ──
        var yol = new Uri(
            $"/api/recruiting/candidates/{nomzod}/files/{yuklangan.Id}", UriKind.Relative);

        using (var olish = await mijoz.GetAsync(yol, bekorQilish))
        {
            olish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(olish));
            olish.Content.Headers.ContentType!.MediaType.ShouldBe("image/jpeg");

            olish.Content.Headers.ContentDisposition.ShouldNotBeNull();
            olish.Content.Headers.ContentDisposition!.FileNameStar.ShouldBe("Паспорт 1-бет.jpg");

            var baytlar = await olish.Content.ReadAsByteArrayAsync(bekorQilish);

            // ⭐ Bazadagi hajm — DISKDAGI hajm.
            yuklangan.SizeBytes.ShouldBe(
                baytlar.Length,
                "javobdagi «sizeBytes» diskka TUSHGAN hajm bo'lishi kerak");
            yuklangan.SizeBytes.ShouldNotBe(
                manba.Length,
                "yuklangan hajm yozilib qolsa, karta HR ga yolg'on raqam ko'rsatardi");
            yuklangan.SizeBytes.ShouldBeLessThan(manba.Length, "rasm HAQIQATAN kichraygan");

            var (eni, boyi) = TestImages.JpegOlchami(baytlar);
            eni.ShouldBe(2000, "uzun tomon aynan chegaraga tushadi");
            boyi.ShouldBe(1500, "nisbat saqlanadi — qirqish YO'Q");
        }

        // ── Kartada ko'rinadi ──
        var karta = await KartaAsync(mijoz, nomzod);
        karta.Files.Count.ShouldBe(1);
        karta.Files[0].Id.ShouldBe(yuklangan.Id);
        karta.Files[0].SizeBytes.ShouldBe(yuklangan.SizeBytes);

        // ── Ro'yxatda «qog'oz qisqichi» yonadi ──
        var qator = await QatorAsync(mijoz, nomzod);
        qator.HasResume.ShouldBeTrue("«hasResume» endi «kamida BITTA fayl bor» degani");

        // ── O'chirish: 204, keyin fayl UMUMAN yo'q ──
        using (var ochirish = await mijoz.DeleteAsync(yol, bekorQilish))
        {
            ochirish.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(ochirish));
        }

        using (var ochirilgandanKeyin = await mijoz.GetAsync(yol, bekorQilish))
        {
            ochirilgandanKeyin.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await XatoKodiAsync(ochirilgandanKeyin)).ShouldBe("nomzod.rezyume_topilmadi");
        }

        // Ikkinchi o'chirish IDEMPOTENT emas — 404 (suratdan ataylab farq).
        using (var takror = await mijoz.DeleteAsync(yol, bekorQilish))
        {
            takror.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await XatoKodiAsync(takror)).ShouldBe("nomzod.rezyume_topilmadi");
        }

        (await KartaAsync(mijoz, nomzod)).Files.ShouldBeEmpty();
        (await QatorAsync(mijoz, nomzod)).HasResume.ShouldBeFalse("fayl qolmadi — qisqich ham o'chadi");
    }

    /// <summary>PNG ham JPEG bo'lib saqlanadi — chiqish turi BITTA.</summary>
    [Fact]
    public async Task Png_JPEG_bolib_saqlanadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var mijoz = await AdminMijozAsync();
        var nomzod = await NomzodAsync(mijoz);

        using var yuklash = await FaylYuborAsync(
            mijoz, nomzod, TestImages.PngYasa(2400, 1000), "диплом.png", "image/png");

        yuklash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yuklash));

        var yuklangan = await yuklash.Content.ReadFromJsonAsync<CandidateFileResponse>(bekorQilish);
        yuklangan!.ContentType.ShouldBe("image/jpeg", "shaffoflik skanda ma'no tashimaydi — chiqish doim JPEG");
        yuklangan.FileName.ShouldBe("диплом.png", "asl nom o'zgarmaydi, faqat mazmun qayta kodlanadi");

        using var olish = await mijoz.GetAsync(
            new Uri($"/api/recruiting/candidates/{nomzod}/files/{yuklangan.Id}", UriKind.Relative),
            bekorQilish);

        var baytlar = await olish.Content.ReadAsByteArrayAsync(bekorQilish);
        var (eni, boyi) = TestImages.JpegOlchami(baytlar);

        eni.ShouldBe(2000);
        boyi.ShouldBe(833);
        yuklangan.SizeBytes.ShouldBe(baytlar.Length);
    }

    /// <summary>PDF BAYTMA-BAYT saqlanadi — hujjat qayta yozilmaydi.</summary>
    [Fact]
    public async Task Pdf_BAYTMA_BAYT_saqlanadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var mijoz = await AdminMijozAsync();
        var nomzod = await NomzodAsync(mijoz);

        using var yuklash = await FaylYuborAsync(mijoz, nomzod, Pdf(), "Резюме.pdf", "application/pdf");

        yuklash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yuklash));

        var yuklangan = await yuklash.Content.ReadFromJsonAsync<CandidateFileResponse>(bekorQilish);
        yuklangan!.ContentType.ShouldBe("application/pdf");
        yuklangan.SizeBytes.ShouldBe(Pdf().Length, "PDF da yuklangan va saqlangan hajm TENG");

        using var olish = await mijoz.GetAsync(
            new Uri($"/api/recruiting/candidates/{nomzod}/files/{yuklangan.Id}", UriKind.Relative),
            bekorQilish);

        olish.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");

        var baytlar = await olish.Content.ReadAsByteArrayAsync(bekorQilish);
        baytlar.ShouldBe(Pdf(), "serverda PDF kutubxonasi yo'q — hujjat o'zgarishsiz qaytishi kerak");
    }

    /// <summary>Fayllar kartada YUKLASH tartibida qaytadi.</summary>
    /// <remarks>
    /// Xodim hujjatlaridan ataylab farq (u yerda yangidan eskiga): pasport
    /// uch varaq bo'lib 1-2-3 tartibda keladi va teskari tartibda karta
    /// o'qilmaydigan bo'lib qoladi.
    /// </remarks>
    [Fact]
    public async Task Fayllar_kartada_YUKLASH_tartibida_qaytadi()
    {
        using var mijoz = await AdminMijozAsync();
        var nomzod = await NomzodAsync(mijoz);

        foreach (var nom in new[] { "1-бет.pdf", "2-бет.pdf", "3-бет.pdf" })
        {
            using var yuklash = await FaylYuborAsync(mijoz, nomzod, Pdf(), nom, "application/pdf");
            yuklash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yuklash));
        }

        var karta = await KartaAsync(mijoz, nomzod);

        karta.Files.Count.ShouldBe(3);
        karta.Files[0].FileName.ShouldBe("1-бет.pdf");
        karta.Files[1].FileName.ShouldBe("2-бет.pdf");
        karta.Files[2].FileName.ShouldBe("3-бет.pdf");
    }

    // ────────────────────────────────────────────────────────────
    //  Chegaralar
    // ────────────────────────────────────────────────────────────

    /// <summary>O'n birinchi fayl rad etiladi, o'rin bo'shasa esa o'tadi.</summary>
    [Fact]
    public async Task Ontadan_ortiq_fayl_qabul_qilinmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var mijoz = await AdminMijozAsync();
        var nomzod = await NomzodAsync(mijoz);

        var birinchiId = Guid.Empty;

        for (var i = 1; i <= 10; i++)
        {
            using var yuklash = await FaylYuborAsync(mijoz, nomzod, Pdf(), $"{i}-бет.pdf", "application/pdf");
            yuklash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yuklash));

            if (i == 1)
            {
                var qator = await yuklash.Content.ReadFromJsonAsync<CandidateFileResponse>(bekorQilish);
                birinchiId = qator!.Id;
            }
        }

        using (var ortiqcha = await FaylYuborAsync(mijoz, nomzod, Pdf(), "11-бет.pdf", "application/pdf"))
        {
            ortiqcha.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(ortiqcha));
            (await XatoKodiAsync(ortiqcha)).ShouldBe("nomzod.rezyume_kop");
        }

        (await KartaAsync(mijoz, nomzod)).Files.Count.ShouldBe(10, "rad etilgan fayl yozuvga tushmaydi");

        // O'rin bo'shatilsa — o'n birinchi fayl o'tadi.
        using (var ochirish = await mijoz.DeleteAsync(
            new Uri($"/api/recruiting/candidates/{nomzod}/files/{birinchiId}", UriKind.Relative),
            bekorQilish))
        {
            ochirish.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(ochirish));
        }

        using (var yana = await FaylYuborAsync(mijoz, nomzod, Pdf(), "11-бет.pdf", "application/pdf"))
        {
            yana.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yana));
        }

        (await KartaAsync(mijoz, nomzod)).Files.Count.ShouldBe(10);
    }

    /// <summary>Chegaradan katta fayl 400 bilan rad etiladi.</summary>
    /// <remarks>
    /// Hajm YUKLANGAN baytlarda o'lchanadi: so'rovni dekodlashdan OLDIN rad
    /// etish kerak. Shu sababli mazmuni ahamiyatsiz — tekshiruv unga
    /// yetib bormaydi.
    /// </remarks>
    [Fact]
    public async Task Chegaradan_katta_fayl_qabul_qilinmaydi()
    {
        using var mijoz = await AdminMijozAsync();
        var nomzod = await NomzodAsync(mijoz);

        var katta = new byte[DocumentLimits.MaxBytes + 1024];

        using var yuklash = await FaylYuborAsync(mijoz, nomzod, katta, "katta.pdf", "application/pdf");

        yuklash.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(yuklash));
        (await XatoKodiAsync(yuklash)).ShouldBe("nomzod.rezyume_katta");

        (await KartaAsync(mijoz, nomzod)).Files.ShouldBeEmpty();
    }

    /// <summary>«.jpg» deb nomlangan matn fayli rad etiladi.</summary>
    /// <remarks>
    /// Tur MAZMUNDAN aniqlanadi: kengaytmani ham, <c>Content-Type</c>
    /// sarlavhasini ham yuklovchining O'ZI yozadi va ikkalasi ham dalil
    /// emas.
    /// </remarks>
    [Fact]
    public async Task Soxta_kengaytmali_fayl_qabul_qilinmaydi()
    {
        using var mijoz = await AdminMijozAsync();
        var nomzod = await NomzodAsync(mijoz);

        var skript = Encoding.UTF8.GetBytes("#!/bin/sh\necho salom\n");

        using var yuklash = await FaylYuborAsync(mijoz, nomzod, skript, "surat.jpg", "image/jpeg");

        yuklash.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(yuklash));
        (await XatoKodiAsync(yuklash)).ShouldBe("nomzod.rezyume_yaroqsiz");

        (await KartaAsync(mijoz, nomzod)).Files.ShouldBeEmpty("diskda ham hech narsa qolmasligi kerak");
    }

    [Fact]
    public async Task Nomavjud_nomzodga_fayl_yuklanmaydi()
    {
        using var mijoz = await AdminMijozAsync();

        using var yuklash = await FaylYuborAsync(
            mijoz, Guid.CreateVersion7(), Pdf(), "rezyume.pdf", "application/pdf");

        yuklash.StatusCode.ShouldBe(HttpStatusCode.NotFound, await MatnAsync(yuklash));
        (await XatoKodiAsync(yuklash)).ShouldBe("nomzod.topilmadi");
    }

    [Fact]
    public async Task Nomavjud_fayl_404_qaytaradi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var mijoz = await AdminMijozAsync();
        var nomzod = await NomzodAsync(mijoz);

        using var olish = await mijoz.GetAsync(
            new Uri($"/api/recruiting/candidates/{nomzod}/files/{Guid.CreateVersion7()}", UriKind.Relative),
            bekorQilish);

        olish.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await XatoKodiAsync(olish)).ShouldBe("nomzod.rezyume_topilmadi");
    }

    // ────────────────────────────────────────────────────────────
    //  Begona fayl
    // ────────────────────────────────────────────────────────────

    /// <summary>BOSHQA nomzodning fayli berilmaydi ham, o'chirilmaydi ham.</summary>
    /// <remarks>
    /// ⭐ Marshrutdagi ikkala identifikator ham ISHLATILADI: fayl nomzodning
    /// agregati ichidan izlanadi. Bu <c>EmployeeDocumentEndpoints</c> dan
    /// ATAYLAB farq — u hujjatni faqat o'z identifikatori bo'yicha topadi va
    /// marshrutdagi xodimga qaramaydi. To'siq tushib qolsa, birovning
    /// pasport nusxasi istalgan nomzod manzili orqali ochilardi va javob
    /// 200 bo'lardi.
    /// </remarks>
    [Fact]
    public async Task Begona_nomzodning_fayli_BERILMAYDI()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var mijoz = await AdminMijozAsync();
        var egasi = await NomzodAsync(mijoz, "Эгаси");
        var begona = await NomzodAsync(mijoz, "Бегона");

        CandidateFileResponse fayl;

        using (var yuklash = await FaylYuborAsync(mijoz, egasi, Pdf(), "Паспорт.pdf", "application/pdf"))
        {
            yuklash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yuklash));
            fayl = (await yuklash.Content.ReadFromJsonAsync<CandidateFileResponse>(bekorQilish))!;
        }

        var chalkash = new Uri(
            $"/api/recruiting/candidates/{begona}/files/{fayl.Id}", UriKind.Relative);

        using (var olish = await mijoz.GetAsync(chalkash, bekorQilish))
        {
            olish.StatusCode.ShouldBe(
                HttpStatusCode.NotFound,
                "chalkash juftlik boshqa odamning hujjatini BERMASLIGI kerak");

            (await XatoKodiAsync(olish)).ShouldBe(
                "nomzod.rezyume_topilmadi",
                "xato kodi faylning BORLIGINI ham oshkor qilmaydi");
        }

        using (var ochirish = await mijoz.DeleteAsync(chalkash, bekorQilish))
        {
            ochirish.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await XatoKodiAsync(ochirish)).ShouldBe("nomzod.rezyume_topilmadi");
        }

        // Fayl EGASIDA joyida turibdi — begona so'rov unga tegmagan.
        var karta = await KartaAsync(mijoz, egasi);
        karta.Files.Count.ShouldBe(1);
        karta.Files[0].Id.ShouldBe(fayl.Id);

        using var togri = await mijoz.GetAsync(
            new Uri($"/api/recruiting/candidates/{egasi}/files/{fayl.Id}", UriKind.Relative),
            bekorQilish);

        togri.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            "to'g'ri juftlik esa OCHIQ — aks holda test faqat yopiq eshikni o'lchagan bo'lardi");
    }

    // ────────────────────────────────────────────────────────────
    //  Ruxsatlar
    // ────────────────────────────────────────────────────────────

    /// <summary>Uchala marshrut ham RUXSAT talab qiladi.</summary>
    /// <remarks>
    /// <para>
    /// Matritsa to'liq tekshiriladi: tokensiz — 401 («kim ekaningni
    /// bilmayman»), ruxsatsiz — 403, FAQAT <c>recruiting.view</c> —
    /// yozishlarda 403, yuklab olishda 200, administrator — hammasi ochiq.
    /// </para>
    /// <para>
    /// Oxirgi qadam MAJBURIY: usiz test yopiq eshikni tekshirib, eshikning
    /// umuman ochilmasligini (marshrut yo'qligini) payqamasdi —
    /// <c>Tekshiruv_FAQAT_bot_hisobiga_ochiq</c> bilan bir xil naqsh.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Fayl_marshrutlari_RUXSAT_talab_qiladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var admin = await AdminMijozAsync();
        var nomzod = await NomzodAsync(admin, "Ҳимояланган");

        CandidateFileResponse fayl;

        using (var yuklash = await FaylYuborAsync(admin, nomzod, Pdf(), "Резюме.pdf", "application/pdf"))
        {
            yuklash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yuklash));
            fayl = (await yuklash.Content.ReadFromJsonAsync<CandidateFileResponse>(bekorQilish))!;
        }

        var faylYoli = new Uri(
            $"/api/recruiting/candidates/{nomzod}/files/{fayl.Id}", UriKind.Relative);

        // ── 1. TOKENSIZ: uchalasi ham 401 ──
        using (var tokensiz = _factory.CreateClient())
        {
            using (var yuklash = await FaylYuborAsync(tokensiz, nomzod, Pdf(), "a.pdf", "application/pdf"))
            {
                yuklash.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            }

            using (var olish = await tokensiz.GetAsync(faylYoli, bekorQilish))
            {
                olish.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            }

            using (var ochirish = await tokensiz.DeleteAsync(faylYoli, bekorQilish))
            {
                ochirish.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            }
        }

        // ── 2. RUXSATSIZ token: uchalasi ham 403 ──
        //    Autentifikatsiya o'tadi, avtorizatsiya to'xtatadi — eng
        //    xavfli holat aynan shu.
        using (var ruxsatsiz = await MijozAsync(await RuxsatsizTokenAsync()))
        {
            using (var yuklash = await FaylYuborAsync(ruxsatsiz, nomzod, Pdf(), "a.pdf", "application/pdf"))
            {
                yuklash.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            }

            using (var olish = await ruxsatsiz.GetAsync(faylYoli, bekorQilish))
            {
                olish.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            }

            using (var ochirish = await ruxsatsiz.DeleteAsync(faylYoli, bekorQilish))
            {
                ochirish.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            }
        }

        // ── 3. FAQAT «recruiting.view»: o'qish ochiq, yozish yopiq ──
        using (var koruvchi = await MijozAsync(await KoruvchiTokenAsync()))
        {
            using (var olish = await koruvchi.GetAsync(faylYoli, bekorQilish))
            {
                olish.StatusCode.ShouldBe(
                    HttpStatusCode.OK,
                    "nomzodni ko'ra olgan odam uning faylini ham yuklab oladi");

                (await olish.Content.ReadAsByteArrayAsync(bekorQilish)).ShouldBe(Pdf());
            }

            using (var yuklash = await FaylYuborAsync(koruvchi, nomzod, Pdf(), "a.pdf", "application/pdf"))
            {
                yuklash.StatusCode.ShouldBe(
                    HttpStatusCode.Forbidden, "yuklash «recruiting.manage» talab qiladi");
            }

            using (var ochirish = await koruvchi.DeleteAsync(faylYoli, bekorQilish))
            {
                ochirish.StatusCode.ShouldBe(
                    HttpStatusCode.Forbidden, "o'chirish «recruiting.manage» talab qiladi");
            }
        }

        // ── 4. Ruxsatsiz urinishlar HECH NARSANI o'zgartirmadi ──
        var karta = await KartaAsync(admin, nomzod);
        karta.Files.Count.ShouldBe(1, "na yangi fayl qo'shildi, na mavjudi o'chdi");
        karta.Files[0].Id.ShouldBe(fayl.Id);

        // ── 5. Administrator uchun uchala marshrut ham OCHIQ ──
        //
        //    Bu qadam MAJBURIY: usiz test mavjud bo'lmagan marshrutda ham
        //    yashil bo'lib o'tib ketardi (yo'q marshrut 404 emas, 403 ham
        //    berishi mumkin — «Fallback» siyosati bor joyda).
        using (var olish = await admin.GetAsync(faylYoli, bekorQilish))
        {
            olish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(olish));
        }

        using (var ochirish = await admin.DeleteAsync(faylYoli, bekorQilish))
        {
            ochirish.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(ochirish));
        }

        (await KartaAsync(admin, nomzod)).Files.ShouldBeEmpty();
    }

    // ────────────────────────────────────────────────────────────
    //  Eski marshrutlar
    // ────────────────────────────────────────────────────────────

    /// <summary>Eski BITTA rezyume marshruti butunlay yo'q.</summary>
    /// <remarks>
    /// Uchala ishlovchi <c>candidates.resume_*</c> ustunlarini o'qirdi —
    /// ustunlar migratsiyada o'chirildi, ya'ni marshrutni «eskirgan
    /// taxallus» sifatida qoldirish ham mumkin emas edi. Test shu qarorni
    /// qadaydi: marshrut qaytib kelsa, u ishlamaydigan yo'l bo'lardi.
    /// </remarks>
    [Fact]
    public async Task Eski_rezyume_marshrutlari_YOQ()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var mijoz = await AdminMijozAsync();
        var nomzod = await NomzodAsync(mijoz);
        var yol = new Uri($"/api/recruiting/candidates/{nomzod}/resume", UriKind.Relative);

        using (var yuklash = await FaylYuborAsync(mijoz, nomzod, Pdf(), "a.pdf", "application/pdf", "resume"))
        {
            yuklash.StatusCode.ShouldBeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
        }

        using (var olish = await mijoz.GetAsync(yol, bekorQilish))
        {
            olish.StatusCode.ShouldBeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
        }

        using (var ochirish = await mijoz.DeleteAsync(yol, bekorQilish))
        {
            ochirish.StatusCode.ShouldBeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Muhit tayyorlash
    // ────────────────────────────────────────────────────────────

    /// <summary>Minimal, lekin HAQIQIY PDF imzosi bilan boshlanadigan fayl.</summary>
    /// <remarks>
    /// To'liq PDF tuzilmasi shart emas: server PDF'ni hech qachon
    /// parslamaydi — faqat <c>%PDF-</c> imzosini tekshiradi
    /// (<c>FileContentInspector</c> izohi). Har chaqiruvda YANGI massiv
    /// qaytadi, chunki baytlar solishtirishda ishlatiladi.
    /// </remarks>
    private static byte[] Pdf()
        => Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\n%%EOF");

    private static string Belgi() => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    /// <summary>Vakansiya va nomzod — bitta qadamda.</summary>
    private static async Task<Guid> NomzodAsync(HttpClient mijoz, string familiya = "Файлли")
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        Guid vakansiyaId;

        using (var vakansiya = await mijoz.PostAsJsonAsync(
            new Uri("/api/recruiting/vacancies", UriKind.Relative),
            new { title = "Сотувчи-консультант " + Belgi(), openings = 1, openedAt = new DateOnly(2026, 3, 1) },
            bekorQilish))
        {
            vakansiya.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(vakansiya));
            vakansiyaId = (await vakansiya.Content.ReadFromJsonAsync<VacancyResponse>(bekorQilish))!.Id;
        }

        using var javob = await mijoz.PostAsJsonAsync(
            new Uri("/api/recruiting/candidates", UriKind.Relative),
            new
            {
                vacancyId = vakansiyaId,
                lastName = familiya + " " + Belgi(),
                firstName = "Азиз",
                phone = "905088888",
            },
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var nomzod = await javob.Content.ReadFromJsonAsync<CandidateFileCardResponse>(bekorQilish);
        nomzod.ShouldNotBeNull();
        return nomzod.Id;
    }

    private static async Task<CandidateFileCardResponse> KartaAsync(HttpClient mijoz, Guid nomzodId)
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var javob = await mijoz.GetAsync(
            new Uri($"/api/recruiting/candidates/{nomzodId}", UriKind.Relative), bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var karta = await javob.Content.ReadFromJsonAsync<CandidateFileCardResponse>(bekorQilish);
        karta.ShouldNotBeNull();
        return karta;
    }

    /// <summary>Nomzodning ro'yxatdagi qatorini o'qiydi.</summary>
    private static async Task<CandidateFileRowResponse> QatorAsync(HttpClient mijoz, Guid nomzodId)
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var javob = await mijoz.GetAsync(
            new Uri("/api/recruiting/candidates?pageSize=200", UriKind.Relative), bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var sahifa = await javob.Content.ReadFromJsonAsync<CandidateFilePageResponse>(bekorQilish);
        sahifa.ShouldNotBeNull();

        var qator = sahifa.Items.FirstOrDefault(q => q.Id == nomzodId);
        qator.ShouldNotBeNull("nomzod ro'yxatda bo'lishi kerak");
        return qator;
    }

    /// <summary>Faylni «multipart/form-data» bo'lib yuboradi.</summary>
    /// <remarks>
    /// Maydon nomi <c>file</c> — BITTA so'rov, BITTA fayl. Mijozning
    /// <c>Content-Type</c> sarlavhasi ATAYLAB yuboriladi: server unga
    /// ishonmasligi kerak va soxta kengaytma testi aynan shuni tekshiradi.
    /// </remarks>
    private static async Task<HttpResponseMessage> FaylYuborAsync(
        HttpClient mijoz,
        Guid nomzodId,
        byte[] baytlar,
        string fileName,
        string tur,
        string bolim = "files")
    {
        using var forma = new MultipartFormDataContent();
        using var qism = new ByteArrayContent(baytlar);
        qism.Headers.ContentType = new MediaTypeHeaderValue(tur);

        forma.Add(qism, "file", fileName);

        return await mijoz.PostAsync(
            new Uri($"/api/recruiting/candidates/{nomzodId}/{bolim}", UriKind.Relative),
            forma,
            TestContext.Current.CancellationToken);
    }

    private async Task<HttpClient> AdminMijozAsync()
    {
        var client = _factory.CreateClient();

        using var javob = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new { login = BonviApiFactory.TestAdminLogin, password = BonviApiFactory.TestAdminPassword },
            TestContext.Current.CancellationToken);

        javob.EnsureSuccessStatusCode();

        var auth = await javob.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return client;
    }

    /// <summary>Haqiqiy, lekin RUXSATSIZ token.</summary>
    /// <remarks>
    /// <c>RecruitingApiTests</c> dagi bilan bir xil naqsh: foydalanuvchi va
    /// imzo haqiqiy, faqat ruxsatlar ro'yxati bo'sh. Aynan shu holat eng
    /// xavflisi — autentifikatsiya o'tadi, avtorizatsiya esa endpoint
    /// darajasida to'xtatishi kerak.
    /// </remarks>
    private Task<string> RuxsatsizTokenAsync() => TokenAsync(ruxsat: null);

    /// <summary>Haqiqiy token, ichida FAQAT <c>recruiting.view</c>.</summary>
    /// <remarks>
    /// ⚠️ <b>Bu hisobga login orqali kirish MUMKIN emas.</b>
    /// Urug'lantirilgan rollarning hammasida ikkala rekruting ruxsati
    /// BIRGA turadi (<c>Permissions.cs</c>), ya'ni «faqat ko'ruvchi» degan
    /// foydalanuvchi bazada YO'Q. Uni rol boshqaruvi orqali yasash butun
    /// bir ketma-ketlikni talab qilardi; tekshiriladigan narsa esa bitta —
    /// endpoint'dagi <c>RequireAuthorization(...)</c>. Shu sababli token
    /// to'g'ridan-to'g'ri yasaladi va u yuklab olish bilan yuklashning
    /// FARQINI ko'rsatadi.
    /// <para>
    /// 2026-09-26 dan token tekshiruvi ruxsatlarni BAZADAN oladi — shu
    /// sababli «token yasash» endi faqat shu ruxsatli haqiqiy hisob bilan
    /// ishlaydi (<see cref="TestAccounts"/>). Urug'lantirilgan rollarda
    /// bunday hisob hamon yo'q; u faqat test uchun yaratiladi.
    /// </para>
    /// </remarks>
    private Task<string> KoruvchiTokenAsync() => TokenAsync(Permissions.RecruitingView);

    private Task<string> TokenAsync(string? ruxsat)
        => ruxsat is null ? TestAccounts.TokenAsync(_factory) : TestAccounts.TokenAsync(_factory, ruxsat);

    private Task<HttpClient> MijozAsync(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return Task.FromResult(client);
    }

    private static async Task<string?> XatoKodiAsync(HttpResponseMessage javob)
    {
        var muammo = await javob.Content.ReadFromJsonAsync<ProblemResponse>(TestContext.Current.CancellationToken);
        return muammo?.Title;
    }

    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
