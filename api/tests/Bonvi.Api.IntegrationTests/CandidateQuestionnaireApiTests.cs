using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Bonvi.Api.Startup;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Identity.Abstractions;
using Bonvi.Application.Settings;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Settings;
using Bonvi.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Nomzod kartasidagi anketa bloki (test uchun — sanoqlar NOM bilan).</summary>
public sealed record QuestionnaireResponse(
    string Education,
    string? WorkExperience,
    string MaritalStatus,
    string RussianLevel,
    string? OtherLanguages,
    string? OfficeSkills,
    string? LastSalary,
    string? ExtraNote,
    bool HasDriverLicense,
    string? DriverLicenseCategories,
    string ExpectedSalary,
    string ReferralSource,

    // Rozilik vaqti — so'ralmagan bo'lsa `null` (izohi domenda).
    DateTimeOffset? OfferAcceptedAt);

/// <summary>Nomzod kartasi — anketa va surat testlariga kerakli qismi.</summary>
public sealed record QuestionnaireCandidateResponse(
    Guid Id,
    string FullName,
    Guid VacancyId,
    string? Vacancy,
    bool HasPhoto,
    QuestionnaireResponse? Questionnaire);

/// <summary>Ro'yxat qatori — bayroqlar tekshiriladi, anketa esa u yerda BO'LMASLIGI kerak.</summary>
public sealed record CandidateFlagsRowResponse(Guid Id, bool HasResume, bool HasPhoto);

/// <summary>Sahifalangan ro'yxat — bayroqlar bilan.</summary>
public sealed record CandidateFlagsPageResponse(IReadOnlyList<CandidateFlagsRowResponse> Items, int Total);

/// <summary>Yuklangan nomzod suratining saqlash kaliti (test uchun).</summary>
public sealed record CandidatePhotoResponse(string PhotoKey);

/// <summary>
/// Nomzod anketasi va surati — uchi-uchiga.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Eng muhim test — birinchisi.</b> Prod'da deploy tartibi «avval API,
/// keyin bot» va oraliqda ESKI bot yangi API bilan gaplashadi. Ya'ni
/// anketasiz, eski shakldagi so'rov AYNAN avvalgidek ishlashi shart. Bu
/// yerda u qadab qo'yiladi: yangi maydonlardan biri ADASHIB majburiy
/// qilinsa, test darhol yiqiladi.
/// </para>
/// <para>
/// Anketaning O'Z qoidalari (uzunlik chegarasi, sanoq tekshiruvi) domen
/// testlarida qoplangan. Bu yerda ULANISH tekshiriladi: JSON'dan domenga,
/// domendan bazaga va bazadan kartaga.
/// </para>
/// <para>
/// «Umumiy anketa» yo'li ham shu yerda: uning HAQIQIY urug'lantirilishi
/// (<c>DatabaseInitializer</c>) faqat to'liq ko'tarilgan API'da ishlaydi,
/// ya'ni birlik testi buni ko'rsata olmasdi.
/// </para>
/// </remarks>
public sealed class CandidateQuestionnaireApiTests : IClassFixture<BonviApiFactory>
{
    /// <summary>Serverdan kutiladigan surat tomoni.</summary>
    private const int KutilganTomon = 512;

    private readonly BonviApiFactory _factory;

    public CandidateQuestionnaireApiTests(BonviApiFactory factory) => _factory = factory;

    // ────────────────────────────────────────────────────────────
    //  Eski bot bilan moslik
    // ────────────────────────────────────────────────────────────

    /// <summary>Anketasiz, ESKI shakldagi so'rov o'zgarishsiz ishlaydi.</summary>
    [Fact]
    public async Task Anketasiz_eski_shakldagi_sorov_avvalgidek_ishlaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var mijoz = await AdminMijozAsync();
        var vakansiya = await VakansiyaAsync(mijoz);

        // Tanada «questionnaire» kaliti UMUMAN yo'q — eski bot aynan shunday yuboradi.
        using var yaratish = await mijoz.PostAsJsonAsync(
            new Uri("/api/recruiting/candidates", UriKind.Relative),
            new
            {
                vacancyId = vakansiya,
                lastName = "Каримов",
                firstName = "Азиз",
                phone = "905077777",
                source = "TelegramBot",
                telegramChatId = 123456789L,
                telegramLanguage = "uz-Latn",
            },
            bekorQilish);

        yaratish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yaratish));

        var nomzod = await yaratish.Content.ReadFromJsonAsync<QuestionnaireCandidateResponse>(bekorQilish);

        nomzod.ShouldNotBeNull();
        nomzod.Questionnaire.ShouldBeNull("anketasiz nomzod — odatiy hol, xato emas");
        nomzod.HasPhoto.ShouldBeFalse();

        var karta = await KartaAsync(mijoz, nomzod.Id);
        karta.Questionnaire.ShouldBeNull();
        karta.HasPhoto.ShouldBeFalse();
    }

    // ────────────────────────────────────────────────────────────
    //  Anketa: yozish va o'qish
    // ────────────────────────────────────────────────────────────

    /// <summary>Anketa saqlanadi va nomzod kartasida to'liq qaytadi.</summary>
    /// <remarks>
    /// Ro'yxat ham shu yerda tekshiriladi: unga anketa TUSHMASLIGI kerak
    /// (u og'ir va faqat kartada kerak), lekin «hasPhoto» bayrog'i
    /// bo'lishi shart — ro'yxat qatorida rasm ko'rsatiladi.
    /// </remarks>
    [Fact]
    public async Task Anketa_saqlanadi_va_nomzod_kartasida_qaytadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var mijoz = await AdminMijozAsync();
        var vakansiya = await VakansiyaAsync(mijoz);

        var rozilikVaqti = new DateTimeOffset(2026, 9, 6, 10, 30, 0, TimeSpan.FromHours(5));

        using var yaratish = await mijoz.PostAsJsonAsync(
            new Uri("/api/recruiting/candidates", UriKind.Relative),
            new
            {
                vacancyId = vakansiya,
                lastName = "Юсупова",
                firstName = "Нилуфар",
                phone = "905066666",
                source = "TelegramBot",
                questionnaire = new
                {
                    education = "HigherComplete",
                    maritalStatus = "Unmarried",
                    russianLevel = "UpperIntermediate",
                    expectedSalary = "From3To4Million",
                    referralSource = "Telegram",
                    offerAcceptedAt = rozilikVaqti,
                    workExperience = "MBF, sotuvchi, 2 yil",
                    otherLanguages = "Ingliz tili — o'rta",
                    officeSkills = "Word 80%, Excel 60%",
                    lastSalary = "3 mln",
                    extraNote = "Тезкор ўрганаман",
                    hasDriverLicense = true,
                    driverLicenseCategories = "BC",
                },
            },
            bekorQilish);

        yaratish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yaratish));

        var nomzod = await yaratish.Content.ReadFromJsonAsync<QuestionnaireCandidateResponse>(bekorQilish);
        nomzod.ShouldNotBeNull();

        // Yaratish javobida ham anketa darhol ko'rinadi: bot uni ikkinchi
        // so'rov bilan tekshirmasligi kerak.
        nomzod.Questionnaire.ShouldNotBeNull();

        var karta = await KartaAsync(mijoz, nomzod.Id);
        var anketa = karta.Questionnaire;

        anketa.ShouldNotBeNull("anketa nomzod bilan birga saqlanishi kerak");
        anketa.Education.ShouldBe("HigherComplete");
        anketa.MaritalStatus.ShouldBe("Unmarried");
        anketa.RussianLevel.ShouldBe("UpperIntermediate");
        anketa.ExpectedSalary.ShouldBe("From3To4Million");
        anketa.ReferralSource.ShouldBe("Telegram");
        anketa.OfferAcceptedAt.ShouldBe(rozilikVaqti);

        // Bot vaqtni Toshkent siljishi bilan yuboradi («+05:00»). Npgsql esa
        // `timestamptz` ga faqat NOL siljishli qiymat yozadi — normallashtirish
        // bo'lmasa butun so'rov 500 bo'lardi. Bu tekshiruv aynan shuni qadaydi.
        anketa.OfferAcceptedAt!.Value.Offset.ShouldBe(TimeSpan.Zero);
        anketa.WorkExperience.ShouldBe("MBF, sotuvchi, 2 yil");
        anketa.OtherLanguages.ShouldBe("Ingliz tili — o'rta");
        anketa.OfficeSkills.ShouldBe("Word 80%, Excel 60%");
        anketa.LastSalary.ShouldBe("3 mln");
        anketa.ExtraNote.ShouldBe("Тезкор ўрганаман");
        anketa.HasDriverLicense.ShouldBeTrue();
        anketa.DriverLicenseCategories.ShouldBe("BC");

        // Ro'yxat: bayroqlar bor, anketa YO'Q
        using var royxat = await mijoz.GetAsync(
            new Uri($"/api/recruiting/candidates?vacancyId={vakansiya}", UriKind.Relative),
            bekorQilish);

        royxat.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(royxat));

        var xomJson = await MatnAsync(royxat);
        xomJson.Contains("questionnaire", StringComparison.OrdinalIgnoreCase).ShouldBeFalse(
            "anketa ro'yxatga tushmasligi kerak — u og'ir va faqat kartada kerak");

        var sahifa = await royxat.Content.ReadFromJsonAsync<CandidateFlagsPageResponse>(bekorQilish);

        sahifa.ShouldNotBeNull();
        sahifa.Items.Count.ShouldBe(1);
        sahifa.Items[0].HasPhoto.ShouldBeFalse();
        sahifa.Items[0].HasResume.ShouldBeFalse();
    }

    /// <summary>Anketa rad etilsa, NOMZOD ham yaratilmaydi.</summary>
    /// <remarks>
    /// Yarim yozuv eng yomon natija bo'lardi: bazada javoblarsiz nomzod
    /// qolib, bot takroriy urinishda ikkinchisini yaratardi va HR bir
    /// odamni ikki qator bo'lib ko'rardi.
    /// </remarks>
    [Fact]
    public async Task Notogri_anketa_bilan_nomzod_umuman_yaratilmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var mijoz = await AdminMijozAsync();
        var vakansiya = await VakansiyaAsync(mijoz);

        // 1. Sanoqda bo'lmagan qiymat
        using (var notogriSanoq = await mijoz.PostAsJsonAsync(
            new Uri("/api/recruiting/candidates", UriKind.Relative),
            new
            {
                vacancyId = vakansiya,
                lastName = "Сафаров",
                firstName = "Жасур",
                questionnaire = new
                {
                    education = 99,
                    maritalStatus = "Married",
                    russianLevel = "Elementary",
                    expectedSalary = "From1To2Million",
                    referralSource = "Olx",
                    offerAcceptedAt = DateTimeOffset.UtcNow,
                },
            },
            bekorQilish))
        {
            notogriSanoq.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(notogriSanoq));
            (await XatoKodiAsync(notogriSanoq)).ShouldBe("anketa.malumot_notogri");
        }

        /*
          2. Manba sanog'ida bo'lmagan qiymat.

          ⚠️ Bu yerda ilgari «ommaviy oferta vaqti yo'q» holati turgan
          edi va u 400 kutardi. Endi u XATO EMAS: bo'sh qiymat «rozilik
          so'ralmagan» degan haqiqiy ma'noga ega (sababi
          `CandidateQuestionnaire.OfferAcceptedAt` izohida) va uning
          uchun alohida test bor — `Ofertasiz_anketa_qabul_qilinadi`.
          Testning MAQSADI o'zgarmadi: yaroqsiz anketa yozuvni yarim
          holatda qoldirmasligi kerak, shu sababli o'rniga boshqa
          yaroqsiz qiymat qo'yildi.
        */
        using (var notogriManba = await mijoz.PostAsJsonAsync(
            new Uri("/api/recruiting/candidates", UriKind.Relative),
            new
            {
                vacancyId = vakansiya,
                lastName = "Сафаров",
                firstName = "Жасур",
                questionnaire = new
                {
                    education = "SecondarySchool",
                    maritalStatus = "Married",
                    russianLevel = "Elementary",
                    expectedSalary = "From1To2Million",
                    referralSource = 42,
                    offerAcceptedAt = DateTimeOffset.UtcNow,
                },
            },
            bekorQilish))
        {
            notogriManba.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(notogriManba));
            (await XatoKodiAsync(notogriManba)).ShouldBe("anketa.manba_notogri");
        }

        // Vakansiya BU test uchun yangi yaratilgan — unda bironta yozuv
        // qolmagani «yarim nomzod yo'q» degan yagona ishonchli dalil.
        using var royxat = await mijoz.GetAsync(
            new Uri($"/api/recruiting/candidates?vacancyId={vakansiya}", UriKind.Relative),
            bekorQilish);

        var sahifa = await royxat.Content.ReadFromJsonAsync<CandidateFlagsPageResponse>(bekorQilish);

        sahifa.ShouldNotBeNull();
        sahifa.Total.ShouldBe(0, "anketa rad etilganda nomzod ham yozilmasligi kerak");
    }

    /// <summary>Ommaviy oferta vaqtisiz anketa QABUL QILINADI va <c>null</c> bo'lib qaytadi.</summary>
    /// <remarks>
    /// <para>
    /// ⭐ Bu — huquqiy faktning haqiqiyligi haqidagi test. Ommaviy oferta
    /// havolasi (<c>bot.offerUrl</c>) sozlanmagan bo'lsa, bot anketaning
    /// oferta qadamini UMUMAN chizmaydi va nomzod hech qanday oferta
    /// ko'rmaydi. Ilgari bunday holatda ham bazaga «rozilik shu lahzada
    /// berildi» deb yozilardi — bot yuborish vaqtini qo'yardi, chunki
    /// ustun majburiy edi.
    /// </para>
    /// <para>
    /// Endi <c>null</c> — «rozilik SO'RALMAGAN», qiymat esa «aynan o'sha
    /// lahzada berilgan». Test aynan shu farqni qo'riqlaydi: agar kimdir
    /// maydonni yana majburiy qilsa yoki bot soxta vaqt qo'ya boshlasa,
    /// shu yerda yiqiladi.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Ofertasiz_anketa_qabul_qilinadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var mijoz = await AdminMijozAsync();

        // Tanada «offerAcceptedAt» UMUMAN yo'q — oferta havolasi
        // sozlanmaganda bot aynan shunday yuboradi.
        using var yaratish = await mijoz.PostAsJsonAsync(
            new Uri("/api/recruiting/candidates", UriKind.Relative),
            new
            {
                lastName = "Офертасиз",
                firstName = "Номзод",
                source = "TelegramBot",
                questionnaire = new
                {
                    education = "SecondarySchool",
                    maritalStatus = "Unmarried",
                    russianLevel = "Elementary",
                    expectedSalary = "From1To2Million",
                    referralSource = "Telegram",
                },
            },
            bekorQilish);

        yaratish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yaratish));

        var nomzod = await yaratish.Content.ReadFromJsonAsync<QuestionnaireCandidateResponse>(bekorQilish);

        nomzod.ShouldNotBeNull();
        nomzod.Questionnaire.ShouldNotBeNull();
        nomzod.Questionnaire.OfferAcceptedAt.ShouldBeNull("rozilik so'ralmagan — soxta vaqt yozilmasin");

        // Karta ham shu javobni beradi: qiymat yo'qligi saqlangan holat,
        // javobni yig'ishdagi tasodif emas.
        var karta = await KartaAsync(mijoz, nomzod.Id);

        karta.Questionnaire.ShouldNotBeNull();
        karta.Questionnaire.OfferAcceptedAt.ShouldBeNull();
    }

    // ────────────────────────────────────────────────────────────
    //  Vakansiyasiz ariza — «Umumiy anketa»
    // ────────────────────────────────────────────────────────────

    /// <summary>«vacancyId» berilmasa nomzod «Umumiy anketa»ga tushadi.</summary>
    /// <remarks>
    /// Butun funksiyaning maqsadi shu: ochiq ish o'rni bo'lmasa ham nomzod
    /// anketa to'ldira olsin. Xizmat vakansiyasining identifikatorini bot
    /// BILMAYDI — u yashirin sozlamada turadi va uni server o'qiydi.
    /// </remarks>
    [Fact]
    public async Task Vakansiyasiz_ariza_Umumiy_anketaga_tushadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var mijoz = await AdminMijozAsync();

        // Tanada «vacancyId» UMUMAN yo'q — bot aynan shunday yuboradi.
        using var yaratish = await mijoz.PostAsJsonAsync(
            new Uri("/api/recruiting/candidates", UriKind.Relative),
            new
            {
                lastName = "Умумий",
                firstName = "Номзод",
                source = "TelegramBot",
                questionnaire = new
                {
                    education = "VocationalSchool",
                    maritalStatus = "Married",
                    russianLevel = "Intermediate",
                    expectedSalary = "From2To3Million",
                    referralSource = "Acquaintance",
                    offerAcceptedAt = DateTimeOffset.UtcNow,
                },
            },
            bekorQilish);

        yaratish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yaratish));

        var nomzod = await yaratish.Content.ReadFromJsonAsync<QuestionnaireCandidateResponse>(bekorQilish);

        nomzod.ShouldNotBeNull();
        nomzod.VacancyId.ShouldBe(await UmumiyAnketaIdAsync(), "server xizmat vakansiyasini o'zi tanlaydi");
        nomzod.Vacancy.ShouldBe(DatabaseInitializer.GeneralPoolTitle);
        nomzod.Questionnaire.ShouldNotBeNull();

        // Kartada ham o'sha vakansiya turadi — javob tasodifiy emas.
        (await KartaAsync(mijoz, nomzod.Id)).VacancyId.ShouldBe(nomzod.VacancyId);
    }

    /// <summary>«vacancyId» berilsa — AVVALGIDEK o'sha vakansiyaga.</summary>
    /// <remarks>
    /// Prod uchun kritik: hozirgi bot va HR formasi vakansiyani DOIM
    /// yuboradi. «Umumiy anketa» ni qo'shish ularning yo'lini hech
    /// qanday o'zgartirmasligi kerak.
    /// </remarks>
    [Fact]
    public async Task Vakansiya_korsatilsa_avvalgidek_oshanga_tushadi()
    {
        using var mijoz = await AdminMijozAsync();
        var vakansiya = await VakansiyaAsync(mijoz);

        var nomzod = await NomzodAsync(mijoz, vakansiya, "Вакансияли");

        nomzod.VacancyId.ShouldBe(vakansiya);
        nomzod.VacancyId.ShouldNotBe(await UmumiyAnketaIdAsync());
        (await KartaAsync(mijoz, nomzod.Id)).VacancyId.ShouldBe(vakansiya);
    }

    // ────────────────────────────────────────────────────────────
    //  Surat
    // ────────────────────────────────────────────────────────────

    /// <summary>Yuklash → berish → kesh → almashtirish → o'chirish.</summary>
    /// <remarks>
    /// Bosqichlar ataylab bitta testda: ular bir-birining natijasiga
    /// tayanadi (rasmsiz «o'chirish» ni tekshirib bo'lmaydi) va alohida
    /// testlar bir xil tayyorgarlikni takrorlagan bo'lardi — xodim
    /// rasmi testidagi bilan bir xil qaror.
    /// </remarks>
    [Fact]
    public async Task Nomzod_suratining_toliq_hayot_sikli_ishlaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var mijoz = await AdminMijozAsync();
        var vakansiya = await VakansiyaAsync(mijoz);
        var nomzod = await NomzodAsync(mijoz, vakansiya, "Суратли");
        var yol = new Uri($"/api/recruiting/candidates/{nomzod.Id}/photo", UriKind.Relative);

        // 1. Yuklash — 40×20 PNG, ya'ni ATAYLAB kvadrat emas
        string birinchiKalit;

        using (var yuklash = await RasmYuborAsync(mijoz, yol, TestImages.PngYasa(40, 20), "image/png"))
        {
            yuklash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yuklash));

            var yuklangan = await yuklash.Content.ReadFromJsonAsync<CandidatePhotoResponse>(bekorQilish);

            yuklangan.ShouldNotBeNull();
            birinchiKalit = yuklangan.PhotoKey;

            // Kalitni server yasaydi: foydalanuvchi bergan nom unda ko'rinmaydi.
            birinchiKalit.ShouldEndWith(".jpg");
            birinchiKalit.ShouldNotContain("surat");
        }

        // 2. Berish — mazmun haqiqatan JPEG va 512×512
        string belgi;

        using (var oqish = await mijoz.GetAsync(yol, bekorQilish))
        {
            oqish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(oqish));
            oqish.Content.Headers.ContentType.ShouldNotBeNull();
            oqish.Content.Headers.ContentType.MediaType.ShouldBe("image/jpeg");

            oqish.Headers.CacheControl.ShouldNotBeNull();
            oqish.Headers.CacheControl.Private.ShouldBeTrue("surat — shaxsiy ma'lumot");
            oqish.Headers.CacheControl.MaxAge.ShouldBe(TimeSpan.FromDays(1));

            oqish.Headers.ETag.ShouldNotBeNull();
            belgi = oqish.Headers.ETag.Tag;

            var baytlar = await oqish.Content.ReadAsByteArrayAsync(bekorQilish);

            baytlar.Length.ShouldBeGreaterThan(0);
            baytlar[0].ShouldBe((byte)0xFF);
            baytlar[1].ShouldBe((byte)0xD8);

            var (eni, boyi) = TestImages.JpegOlchami(baytlar);
            eni.ShouldBe(KutilganTomon);
            boyi.ShouldBe(KutilganTomon, "markazdan kvadrat qirqilishi kerak");
        }

        // 3. Kesh — o'sha belgi bilan qayta so'ralganda mazmun yuborilmaydi
        using (var sorov = new HttpRequestMessage(HttpMethod.Get, yol))
        {
            sorov.Headers.TryAddWithoutValidation("If-None-Match", belgi);

            using var javob = await mijoz.SendAsync(sorov, bekorQilish);

            javob.StatusCode.ShouldBe(HttpStatusCode.NotModified);
            javob.Headers.ETag.ShouldNotBeNull();
            javob.Headers.ETag.Tag.ShouldBe(belgi);
        }

        // 4. Kartada va ro'yxatda bayroq ko'tariladi
        (await KartaAsync(mijoz, nomzod.Id)).HasPhoto.ShouldBeTrue();

        using (var royxat = await mijoz.GetAsync(
            new Uri($"/api/recruiting/candidates?vacancyId={vakansiya}", UriKind.Relative),
            bekorQilish))
        {
            var sahifa = await royxat.Content.ReadFromJsonAsync<CandidateFlagsPageResponse>(bekorQilish);

            sahifa.ShouldNotBeNull();
            sahifa.Items.Count.ShouldBe(1);
            sahifa.Items[0].HasPhoto.ShouldBeTrue("ro'yxatda rasm ko'rsatiladi");
        }

        // 5. Almashtirish — ESKI fayl diskda qolmasligi kerak
        using (var qaytaYuklash = await RasmYuborAsync(mijoz, yol, TestImages.PngYasa(24, 24), "image/png"))
        {
            qaytaYuklash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qaytaYuklash));

            var yangi = await qaytaYuklash.Content.ReadFromJsonAsync<CandidatePhotoResponse>(bekorQilish);

            yangi.ShouldNotBeNull();
            yangi.PhotoKey.ShouldNotBe(birinchiKalit);
        }

        (await FaylBorAsync(birinchiKalit)).ShouldBeFalse("eski surat diskdan o'chirilishi kerak");

        // 6. O'chirish — ikkinchi marta ham 204
        using (var ochirish = await mijoz.DeleteAsync(yol, bekorQilish))
        {
            ochirish.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(ochirish));
        }

        using (var qaytaOchirish = await mijoz.DeleteAsync(yol, bekorQilish))
        {
            qaytaOchirish.StatusCode.ShouldBe(
                HttpStatusCode.NoContent,
                "amal idempotent: surat yo'q bo'lsa ham xato emas");
        }

        using (var ochirilgandanKeyin = await mijoz.GetAsync(yol, bekorQilish))
        {
            ochirilgandanKeyin.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await XatoKodiAsync(ochirilgandanKeyin)).ShouldBe("nomzod.rasm_yoq");
        }

        (await KartaAsync(mijoz, nomzod.Id)).HasPhoto.ShouldBeFalse();
    }

    /// <summary>Rasm bo'lmagan fayl ikki bosqichda ham to'xtatiladi.</summary>
    /// <remarks>
    /// Ikkinchi holat asosiysi: mijoz turni «image/png» deb E'LON QILADI,
    /// lekin ichida oddiy matn. Bot uchun bu real xavf — unga fayl
    /// Telegram'dan keladi va sarlavhaga ishonib bo'lmaydi.
    /// </remarks>
    [Fact]
    public async Task Rasm_bolmagan_fayl_qabul_qilinmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var mijoz = await AdminMijozAsync();
        var vakansiya = await VakansiyaAsync(mijoz);
        var nomzod = await NomzodAsync(mijoz, vakansiya, "Матнли");
        var yol = new Uri($"/api/recruiting/candidates/{nomzod.Id}/photo", UriKind.Relative);

        var matn = Encoding.UTF8.GetBytes("Bu rasm emas, oddiy matn.");

        using (var eLonQilinganTur = await RasmYuborAsync(mijoz, yol, matn, "text/plain"))
        {
            eLonQilinganTur.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await XatoKodiAsync(eLonQilinganTur)).ShouldBe("nomzod.rasm_turi");
        }

        using (var soxtaTur = await RasmYuborAsync(mijoz, yol, matn, "image/png"))
        {
            soxtaTur.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await XatoKodiAsync(soxtaTur)).ShouldBe("nomzod.rasm_yaroqsiz");
        }

        using var oqish = await mijoz.GetAsync(yol, bekorQilish);
        oqish.StatusCode.ShouldBe(HttpStatusCode.NotFound, "hech qanday fayl saqlanmagan bo'lishi kerak");
    }

    /// <summary>«Nomzod yo'q» va «surat yo'q» — ikki xil xato.</summary>
    [Fact]
    public async Task Nomzod_va_surat_topilmagan_holatlar_ajratiladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var mijoz = await AdminMijozAsync();

        var notanish = new Uri($"/api/recruiting/candidates/{Guid.CreateVersion7()}/photo", UriKind.Relative);

        using (var oqish = await mijoz.GetAsync(notanish, bekorQilish))
        {
            oqish.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await XatoKodiAsync(oqish)).ShouldBe("nomzod.topilmadi");
        }

        using (var ochirish = await mijoz.DeleteAsync(notanish, bekorQilish))
        {
            ochirish.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await XatoKodiAsync(ochirish)).ShouldBe("nomzod.topilmadi");
        }

        using (var yuklash = await RasmYuborAsync(mijoz, notanish, TestImages.PngYasa(8, 8), "image/png"))
        {
            yuklash.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await XatoKodiAsync(yuklash)).ShouldBe("nomzod.topilmadi");
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Ruxsatlar
    // ────────────────────────────────────────────────────────────

    /// <summary>Rekruting ruxsati yo'q foydalanuvchi hech qayerga kira olmaydi.</summary>
    /// <remarks>
    /// Token HAQIQIY va imzosi to'g'ri — faqat ichida «recruiting.*» yo'q.
    /// Aynan shu holat eng xavflisi: autentifikatsiya o'tadi, avtorizatsiya
    /// esa endpoint darajasida to'xtatishi kerak.
    /// </remarks>
    [Fact]
    public async Task Recruiting_ruxsati_yoq_foydalanuvchi_403_oladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        // Nomzod haqiqiy bo'lsin: 403 «yo'q» degani emas, «ruxsat yo'q» degani.
        using var admin = await AdminMijozAsync();
        var vakansiya = await VakansiyaAsync(admin);
        var nomzod = await NomzodAsync(admin, vakansiya, "Химояланган");

        using var mijoz = _factory.CreateClient();
        mijoz.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await RuxsatsizTokenAsync());

        var rasmYoli = new Uri($"/api/recruiting/candidates/{nomzod.Id}/photo", UriKind.Relative);

        using (var karta = await mijoz.GetAsync(
            new Uri($"/api/recruiting/candidates/{nomzod.Id}", UriKind.Relative),
            bekorQilish))
        {
            karta.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using (var anketaBilanYaratish = await mijoz.PostAsJsonAsync(
            new Uri("/api/recruiting/candidates", UriKind.Relative),
            new
            {
                vacancyId = vakansiya,
                lastName = "Рухсатсиз",
                firstName = "Номзод",
                questionnaire = new
                {
                    education = "SecondarySchool",
                    maritalStatus = "Married",
                    russianLevel = "Elementary",
                    expectedSalary = "From1To2Million",
                    referralSource = "Olx",
                    offerAcceptedAt = DateTimeOffset.UtcNow,
                },
            },
            bekorQilish))
        {
            anketaBilanYaratish.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using (var oqish = await mijoz.GetAsync(rasmYoli, bekorQilish))
        {
            oqish.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using (var yuklash = await RasmYuborAsync(mijoz, rasmYoli, TestImages.PngYasa(8, 8), "image/png"))
        {
            yuklash.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using (var ochirish = await mijoz.DeleteAsync(rasmYoli, bekorQilish))
        {
            ochirish.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    /// <summary>Vakansiya nomi + ochiq vakansiya yaratadi va uning identifikatorini beradi.</summary>
    /// <remarks>
    /// Har bir test O'Z vakansiyasini oladi: ro'yxat tekshiruvlari
    /// («nechta nomzod bor») shu izolyatsiyaga tayanadi va testlar
    /// parallel ishlaydi.
    /// </remarks>
    private static async Task<Guid> VakansiyaAsync(HttpClient mijoz)
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var nomJavobi = await mijoz.PostAsJsonAsync(
            new Uri("/api/catalog/vacancy-titles", UriKind.Relative),
            new { name = "Анкета синови " + Guid.NewGuid().ToString("N")[..8] },
            bekorQilish);

        nomJavobi.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(nomJavobi));

        var nom = await nomJavobi.Content.ReadFromJsonAsync<VacancyTitleResponse>(bekorQilish);
        nom.ShouldNotBeNull();

        using var vakansiyaJavobi = await mijoz.PostAsJsonAsync(
            new Uri("/api/recruiting/vacancies", UriKind.Relative),
            new
            {
                vacancyTitleId = nom.Id,
                openings = 1,
                openedAt = new DateOnly(2026, 9, 1),
            },
            bekorQilish);

        vakansiyaJavobi.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(vakansiyaJavobi));

        var vakansiya = await vakansiyaJavobi.Content.ReadFromJsonAsync<VacancyResponse>(bekorQilish);
        vakansiya.ShouldNotBeNull();

        return vakansiya.Id;
    }

    private static async Task<QuestionnaireCandidateResponse> NomzodAsync(
        HttpClient mijoz,
        Guid vakansiyaId,
        string familiya)
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var javob = await mijoz.PostAsJsonAsync(
            new Uri("/api/recruiting/candidates", UriKind.Relative),
            new
            {
                vacancyId = vakansiyaId,
                lastName = familiya,
                firstName = "Номзод",
            },
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var nomzod = await javob.Content.ReadFromJsonAsync<QuestionnaireCandidateResponse>(bekorQilish);

        nomzod.ShouldNotBeNull();
        return nomzod;
    }

    private static async Task<QuestionnaireCandidateResponse> KartaAsync(HttpClient mijoz, Guid nomzodId)
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var javob = await mijoz.GetAsync(
            new Uri($"/api/recruiting/candidates/{nomzodId}", UriKind.Relative),
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var karta = await javob.Content.ReadFromJsonAsync<QuestionnaireCandidateResponse>(bekorQilish);

        karta.ShouldNotBeNull();
        return karta;
    }

    /// <summary>Faylni «multipart/form-data» bo'lib, «file» maydonida yuboradi.</summary>
    private static async Task<HttpResponseMessage> RasmYuborAsync(
        HttpClient mijoz,
        Uri yol,
        byte[] baytlar,
        string tur)
    {
        using var forma = new MultipartFormDataContent();
        using var qism = new ByteArrayContent(baytlar);

        qism.Headers.ContentType = new MediaTypeHeaderValue(tur);

        // Fayl nomi ataylab beriladi: u javobdagi kalitga TUSHMASLIGI kerak.
        forma.Add(qism, "file", "surat.png");

        return await mijoz.PostAsync(yol, forma, TestContext.Current.CancellationToken);
    }

    /// <summary>«Umumiy anketa» vakansiyasining identifikatori.</summary>
    /// <remarks>
    /// Qiymat YASHIRIN sozlamada turadi va HECH BIR endpoint uni
    /// qaytarmaydi (bu ataylab — izohi <c>SystemSettingKeys</c> da), shu
    /// sababli test uni serverning o'z o'quvchisidan oladi. Bu ayni
    /// paytda urug'lantirish HAQIQATAN ishlaganini ham isbotlaydi:
    /// qiymat bo'sh bo'lsa <c>Guid.Parse</c> darhol yiqiladi.
    /// </remarks>
    private async Task<Guid> UmumiyAnketaIdAsync()
    {
        using var scope = _factory.Services.CreateScope();

        var reader = scope.ServiceProvider.GetRequiredService<SystemSettingsReader>();
        var settings = await reader.ReadAsync(TestContext.Current.CancellationToken);

        return Guid.Parse(settings.Hidden(SystemSettingKeys.GeneralPoolVacancyId));
    }

    /// <summary>Saqlash kaliti bo'yicha fayl hali diskda turibdimi.</summary>
    /// <remarks>
    /// HTTP orqali buni ko'rib bo'lmaydi: yozuv yangi kalitga ko'chgan va
    /// eskisiga hech kim ishora qilmaydi. Yetim fayl esa jimgina joy
    /// egallayveradi — shu sababli tekshiruv saqlash portidan o'tadi.
    /// </remarks>
    private async Task<bool> FaylBorAsync(string kalit)
    {
        using var scope = _factory.Services.CreateScope();

        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();

        await using var content = await storage.OpenAsync(kalit, TestContext.Current.CancellationToken);

        return content is not null;
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

    /// <summary>Haqiqiy, lekin ruxsatlarsiz access token yasaydi.</summary>
    /// <remarks>
    /// Bazada faqat administrator bor va unda barcha ruxsatlar mavjud.
    /// Shu sababli token to'g'ridan-to'g'ri token servisidan, BO'SH ruxsat
    /// to'plami bilan olinadi — bu «ruxsati kam foydalanuvchi» holatini
    /// yangi rol yaratmasdan aniq takrorlaydi (<c>CatalogApiTests</c>
    /// dagi bilan bir xil usul).
    /// <para>
    /// 2026-09-26 dan token tekshiruvi ruxsatlarni BAZADAN oladi — tokenga
    /// bo'sh ro'yxat yozish endi ishlamaydi. Shu sababli ruxsatsiz rolli
    /// haqiqiy hisob yaratiladi (<see cref="TestAccounts"/>).
    /// </para>
    /// </remarks>
    private Task<string> RuxsatsizTokenAsync() => TestAccounts.TokenAsync(_factory);

    /// <summary>Xato javobidan kodni oladi — tekshiruv aniq bo'lishi uchun.</summary>
    private static async Task<string?> XatoKodiAsync(HttpResponseMessage javob)
    {
        var muammo = await javob.Content.ReadFromJsonAsync<ProblemResponse>(
            TestContext.Current.CancellationToken);

        return muammo?.Title;
    }

    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
