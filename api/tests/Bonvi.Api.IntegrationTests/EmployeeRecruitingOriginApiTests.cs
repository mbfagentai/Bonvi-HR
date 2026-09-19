using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Bonvi.Application.Identity.Abstractions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Organization;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Xodim kartasidagi nomzodlik fayli — javobdagi qatori.</summary>
public sealed record EmployeeCandidateFileResponse(
    Guid Id,
    Guid CandidateId,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset UploadedAt);

/// <summary>Xodim kelib chiqqan nomzod yozuvi — kartasiga havola.</summary>
public sealed record EmployeeOriginCandidateResponse(
    Guid Id,
    string Status,
    DateOnly? ClosedAt,
    string? Vacancy);

/// <summary>«Nomzodlik davri» javobining konverti.</summary>
public sealed record EmployeeRecruitingOriginResponse(
    IReadOnlyList<EmployeeOriginCandidateResponse> Candidates,
    IReadOnlyList<EmployeeCandidateFileResponse> Files);

/// <summary>
/// Nomzodlik davri xodim kartasida ko'rinadi — havola va fayllar, uchi-uchiga.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nima uchun bu test bor.</b> Foydalanuvchi aytgan holat
/// (2026-09-19): nomzodning anketa suratlari fayllar bo'limiga
/// yuklangan, odam sinov muddatiga ishga olingan va xodim kartasidagi
/// «Hujjatlar» bo'p-bo'sh chiqqan — HR hammasini QAYTADAN yuklashga
/// majbur bo'lgan. Bu yerda tekshiriladigan narsa bitta: ishga qabuldan
/// KEYIN ham o'sha fayllar xodim kartasidan ko'rinib turadimi.
/// </para>
/// <para>
/// <b>Nusxa YO'Q — va test buni ham qadaydi.</b> Fayllar
/// <c>employee_documents</c> ga ko'chirilmaydi (sababi
/// <c>GetEmployeeRecruitingOriginQuery</c> izohida), shu sababli
/// «hujjatlar ro'yxati bo'sh qoladi» sharti ATAYLAB tekshiriladi: uni
/// tushirib qoldirish kelajakdagi «bir oz ko'chirib qo'yaylik»
/// o'zgarishini jimgina o'tkazib yuborardi.
/// </para>
/// </remarks>
public sealed class EmployeeRecruitingOriginApiTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public EmployeeRecruitingOriginApiTests(BonviApiFactory factory) => _factory = factory;

    /// <summary>
    /// Nomzodga fayl yuklanadi, u xodimga aylanadi va fayl xodim
    /// kartasida QOLADI.
    /// </summary>
    [Fact]
    public async Task Nomzodlik_fayllari_ishga_qabuldan_keyin_xodim_kartasida_qoladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var nomzodId = await NomzodAsync(mijoz);

        // Ikkita fayl — tartib ham tekshiriladi (pasport varaqlari 1-2-3
        // bo'lib keladi va teskari tartibda o'qilmaydigan bo'lib qolardi).
        var birinchi = await FaylYuklaAsync(mijoz, nomzodId, "Анкета-1.pdf");
        var ikkinchi = await FaylYuklaAsync(mijoz, nomzodId, "Анкета-2.pdf");

        var xodimId = await IshgaOlAsync(mijoz, nomzodId);

        var fayllar = (await NomzodlikDavriAsync(mijoz, xodimId)).Files;

        fayllar.Count.ShouldBe(2, "ikkala fayl ham ishga qabuldan keyin ko'rinib turishi kerak");
        fayllar[0].Id.ShouldBe(birinchi.Id, "tartib — YUKLASH tartibida");
        fayllar[1].Id.ShouldBe(ikkinchi.Id);
        fayllar[0].FileName.ShouldBe("Анкета-1.pdf");
        fayllar[0].CandidateId.ShouldBe(nomzodId, "yuklab olish AYNAN shu identifikator bilan ketadi");
        fayllar[0].SizeBytes.ShouldBeGreaterThan(0);

        // ⭐ Javobdagi «candidateId» HAQIQATAN yuklab olishga yaraydi —
        // xodim bo'limida ikkinchi marshrut ochilmagani shuning uchun.
        using var baytlar = await mijoz.GetAsync(
            new Uri(
                $"/api/recruiting/candidates/{fayllar[0].CandidateId}/files/{fayllar[0].Id}",
                UriKind.Relative),
            bekorQilish);

        baytlar.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(baytlar));
        (await baytlar.Content.ReadAsByteArrayAsync(bekorQilish)).ShouldBe(Pdf());

        // ⚠️ Xodimning O'Z hujjatlari ro'yxati BO'SH qoladi: nusxa
        // olinmaydi va bu ataylab (nomzod faylida hujjat TURI yo'q).
        using var hujjatlar = await mijoz.GetAsync(
            new Uri($"/api/employees/{xodimId}/documents", UriKind.Relative), bekorQilish);

        hujjatlar.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(hujjatlar));

        var royxat = await hujjatlar.Content
            .ReadFromJsonAsync<IReadOnlyList<EmployeeDocumentResponse>>(bekorQilish);

        royxat.ShouldNotBeNull();
        royxat.Count.ShouldBe(0, "fayllar KO'CHIRILMAYDI — ular nomzod kartasida qoladi");
    }

    /// <summary>
    /// Nomzod kartasidan o'chirilgan fayl xodim kartasidan ham yo'qoladi.
    /// </summary>
    /// <remarks>
    /// Bu qarorning NARXI va u ataylab qadaladi: xodim kartasi faylni
    /// rekrutingdan O'QIYDI, ya'ni fayl o'chirilsa ro'yxat ham qisqaradi
    /// (xodimning o'z hujjati esa yumshoq o'chiriladi va diskdan
    /// ketmaydi). Test bu xulqni TASDIQLAYDI — kelajakda kimdir uni
    /// «xato» deb tuzatmoqchi bo'lsa, avval qarorni o'qishi kerak.
    /// </remarks>
    [Fact]
    public async Task Nomzod_faylini_ochirish_xodim_kartasidan_ham_olib_tashlaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var nomzodId = await NomzodAsync(mijoz);
        var fayl = await FaylYuklaAsync(mijoz, nomzodId, "Паспорт.pdf");
        var xodimId = await IshgaOlAsync(mijoz, nomzodId);

        (await NomzodlikDavriAsync(mijoz, xodimId)).Files.Count.ShouldBe(1);

        using (var ochirish = await mijoz.DeleteAsync(
            new Uri($"/api/recruiting/candidates/{nomzodId}/files/{fayl.Id}", UriKind.Relative),
            bekorQilish))
        {
            ochirish.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(ochirish));
        }

        var keyin = await NomzodlikDavriAsync(mijoz, xodimId);

        keyin.Files.Count.ShouldBe(
            0, "nomzod fayli QATTIQ o'chadi — xodim kartasi uni faqat ko'rsatib turgan edi");

        keyin.Candidates.Count.ShouldBe(
            1, "⭐ fayl ketsa ham nomzod kartasiga HAVOLA qoladi: «nomzodlik paytida qanday edi» degan savol o'z joyida");
    }

    /// <summary>
    /// ⭐ FAYLSIZ nomzoddan kelgan xodimda ham nomzod kartasiga HAVOLA bor.
    /// </summary>
    /// <remarks>
    /// Foydalanuvchi talabi (2026-09-19): «hujjat bo'lmasa ham nomzodlik
    /// paytida qanday edi degan savolga javob topilsin». Ya'ni havola
    /// fayllardan MUSTAQIL bo'lishi kerak — bu test aynan shuni qadaydi.
    /// Havola yozuvida e'lon nomi va yopilish sanasi ham bor: bitta
    /// xodimda ikkita nomzod yozuvi bo'lsa (ketib qaytib kelgan), HR
    /// ularni shu ikki belgidan ajratadi.
    /// </remarks>
    [Fact]
    public async Task Faylsiz_nomzoddan_kelgan_xodimda_ham_havola_bor()
    {
        using var mijoz = await AdminMijozAsync();

        var nomzodId = await NomzodAsync(mijoz);
        var xodimId = await IshgaOlAsync(mijoz, nomzodId);

        var natija = await NomzodlikDavriAsync(mijoz, xodimId);

        natija.Files.Count.ShouldBe(0, "fayl umuman yuklanmagan");

        natija.Candidates.Count.ShouldBe(1, "havola esa baribir bo'lishi SHART");
        natija.Candidates[0].Id.ShouldBe(nomzodId);
        natija.Candidates[0].Status.ShouldBe("Hired");
        natija.Candidates[0].ClosedAt.ShouldNotBeNull("voronkadan chiqqan sana havola yorlig'ida ko'rinadi");
        natija.Candidates[0].Vacancy.ShouldNotBeNullOrWhiteSpace("e'lon nomi ikki yozuvni ajratish uchun");
    }

    /// <summary>Rekrutingsiz xodimda — BO'SH ro'yxat, xato emas.</summary>
    /// <remarks>
    /// Xodimlarning ko'pchiligi Excel'dan ko'chirilgan va ularda
    /// nomzodlik yozuvi umuman yo'q. Bo'sh ro'yxatni 404 bilan
    /// aralashtirib yuborish interfeysda «tizimda nuqson bor» degan
    /// taassurot qoldirardi.
    /// </remarks>
    [Fact]
    public async Task Rekrutingsiz_xodimda_bosh_royxat_qaytadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var xodimId = await QoldaXodimAsync(mijoz);

        var bosh = await NomzodlikDavriAsync(mijoz, xodimId);
        bosh.Files.Count.ShouldBe(0);
        bosh.Candidates.Count.ShouldBe(0, "rekrutingsiz xodimda havola ham bo'lmaydi");

        // Noma'lum xodim esa 404 — ikkalasi farqlanishi SHART.
        using var yoq = await mijoz.GetAsync(
            new Uri($"/api/employees/{Guid.NewGuid()}/recruiting", UriKind.Relative),
            bekorQilish);

        yoq.StatusCode.ShouldBe(HttpStatusCode.NotFound, await MatnAsync(yoq));
    }

    /// <summary>Marshrut <c>recruiting.view</c> talab qiladi.</summary>
    /// <remarks>
    /// <para>
    /// ⭐ Eng muhim qatori — uchinchisi: FAQAT <c>employees.view</c> bo'lgan
    /// foydalanuvchi 403 oladi. Ruxsat ataylab <c>employees.view</c> dan
    /// MUSTAQIL: fayl rekrutingga tegishli va uni yuklab olish ham
    /// <c>recruiting.view</c> bilan ketadi — ro'yxatni xodim ruxsati bilan
    /// ochish «ko'rinadi, lekin ochilmaydi» degan holatni yaratardi.
    /// </para>
    /// <para>
    /// Oxirgi qadam MAJBURIY: usiz test faqat yopiq eshikni o'lchab,
    /// marshrutning umuman yo'qligini payqamasdi.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Nomzodlik_fayllari_RUXSAT_talab_qiladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();

        var nomzodId = await NomzodAsync(admin);
        await FaylYuklaAsync(admin, nomzodId, "Диплом.pdf");
        var xodimId = await IshgaOlAsync(admin, nomzodId);

        var yol = new Uri($"/api/employees/{xodimId}/recruiting", UriKind.Relative);

        // ── 1. TOKENSIZ — 401 ──
        using (var tokensiz = _factory.CreateClient())
        using (var javob = await tokensiz.GetAsync(yol, bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        // ── 2. RUXSATSIZ token — 403 (autentifikatsiya o'tadi) ──
        using (var ruxsatsiz = await MijozAsync(await TokenAsync(null)))
        using (var javob = await ruxsatsiz.GetAsync(yol, bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        // ── 3. FAQAT «employees.view» — baribir 403 ──
        using (var xodimchi = await MijozAsync(await TokenAsync(Permissions.EmployeesView)))
        using (var javob = await xodimchi.GetAsync(yol, bekorQilish))
        {
            javob.StatusCode.ShouldBe(
                HttpStatusCode.Forbidden,
                "xodimlar ro'yxatini ochgan har bir kishi nomzod hujjatlarini ko'rishi shart emas");
        }

        // ── 4. «recruiting.view» — OCHIQ ──
        using (var koruvchi = await MijozAsync(await TokenAsync(Permissions.RecruitingView)))
        using (var javob = await koruvchi.GetAsync(yol, bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

            var konvert = await javob.Content
                .ReadFromJsonAsync<EmployeeRecruitingOriginResponse>(bekorQilish);

            konvert.ShouldNotBeNull();
            konvert.Files.Count.ShouldBe(1, "eshik ochiq ekanini ham tekshirish SHART");
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    private static byte[] Pdf()
        => Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\n%%EOF");

    private static string Belgi() => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private static async Task<EmployeeRecruitingOriginResponse> NomzodlikDavriAsync(
        HttpClient mijoz,
        Guid xodimId)
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var javob = await mijoz.GetAsync(
            new Uri($"/api/employees/{xodimId}/recruiting", UriKind.Relative), bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var konvert = await javob.Content
            .ReadFromJsonAsync<EmployeeRecruitingOriginResponse>(bekorQilish);

        konvert.ShouldNotBeNull();
        return konvert;
    }

    /// <summary>Vakansiya nomi, vakansiya va nomzod — bitta qadamda.</summary>
    private static async Task<Guid> NomzodAsync(HttpClient mijoz)
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        Guid nomId;

        using (var nom = await mijoz.PostAsJsonAsync(
            new Uri("/api/catalog/vacancy-titles", UriKind.Relative),
            new { name = "Оператор " + Belgi() },
            bekorQilish))
        {
            nom.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(nom));
            nomId = (await nom.Content.ReadFromJsonAsync<VacancyTitleResponse>(bekorQilish))!.Id;
        }

        Guid vakansiyaId;

        using (var vakansiya = await mijoz.PostAsJsonAsync(
            new Uri("/api/recruiting/vacancies", UriKind.Relative),
            new { vacancyTitleId = nomId, openings = 1, openedAt = new DateOnly(2026, 3, 1) },
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
                lastName = "Анкетали " + Belgi(),
                firstName = "Азиз",
                phone = "905077777",
            },
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var nomzod = await javob.Content.ReadFromJsonAsync<CandidateResponse>(bekorQilish);
        nomzod.ShouldNotBeNull();
        return nomzod.Id;
    }

    private static async Task<CandidateFileResponse> FaylYuklaAsync(
        HttpClient mijoz,
        Guid nomzodId,
        string fileName)
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var forma = new MultipartFormDataContent();
        using var qism = new ByteArrayContent(Pdf());
        qism.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        forma.Add(qism, "file", fileName);

        using var javob = await mijoz.PostAsync(
            new Uri($"/api/recruiting/candidates/{nomzodId}/files", UriKind.Relative),
            forma,
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var fayl = await javob.Content.ReadFromJsonAsync<CandidateFileResponse>(bekorQilish);
        fayl.ShouldNotBeNull();
        return fayl;
    }

    /// <summary>Nomzodni xodimga aylantiradi va XODIM identifikatorini qaytaradi.</summary>
    private async Task<Guid> IshgaOlAsync(HttpClient mijoz, Guid nomzodId)
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var (firmaId, lavozimId) = await MalumotnomaAsync();

        using var javob = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{nomzodId}/hire", UriKind.Relative),
            new { legalEntityId = firmaId, positionId = lavozimId },
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var natija = await javob.Content.ReadFromJsonAsync<DirectHireResponse>(bekorQilish);
        natija.ShouldNotBeNull();
        return natija.Hire.Employment.EmployeeId;
    }

    /// <summary>Rekrutingsiz — QO'LDA yaratilgan xodim («Excel» holati).</summary>
    private static async Task<Guid> QoldaXodimAsync(HttpClient mijoz)
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var javob = await mijoz.PostAsJsonAsync(
            new Uri("/api/employees", UriKind.Relative),
            new { lastName = "Эскидан " + Belgi(), firstName = "Ботир" },
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var xodim = await javob.Content.ReadFromJsonAsync<HiredPersonResponse>(bekorQilish);
        xodim.ShouldNotBeNull();
        return xodim.Id;
    }

    private async Task<(Guid LegalEntityId, Guid PositionId)> MalumotnomaAsync()
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var belgi = Belgi();

        var firma = LegalEntity.Create("Анкета Тест МЧЖ " + belgi).Value;
        var lavozim = Position.Create("Оператор " + belgi).Value;

        kontekst.Add(firma);
        kontekst.Add(lavozim);

        await kontekst.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (firma.Id, lavozim.Id);
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

    /// <summary>
    /// Haqiqiy token, ichida FAQAT berilgan ruxsat (yoki umuman yo'q).
    /// </summary>
    /// <remarks>
    /// Urug'lantirilgan rollarda ruxsatlar guruh-guruh turadi, ya'ni
    /// «faqat employees.view» degan foydalanuvchi bazada YO'Q. Tekshirilishi
    /// kerak bo'lgan narsa esa bitta — endpoint'dagi
    /// <c>RequireAuthorization(...)</c> (<c>CandidateFileApiTests</c> dagi
    /// bilan bir xil naqsh va bir xil sabab).
    /// </remarks>
    private async Task<string> TokenAsync(string? ruxsat)
    {
        using var qamrov = _factory.Services.CreateScope();

        var users = qamrov.ServiceProvider.GetRequiredService<IUserRepository>();
        var tokens = qamrov.ServiceProvider.GetRequiredService<ITokenService>();

        var login = PhoneNumber.Create(BonviApiFactory.TestAdminLogin).Value;
        var user = await users.GetByLoginAsync(login, TestContext.Current.CancellationToken);
        user.ShouldNotBeNull();

        var toplam = new HashSet<string>(StringComparer.Ordinal);

        if (ruxsat is not null)
        {
            toplam.Add(ruxsat);
        }

        return tokens.CreateTokens(user, [], toplam).AccessToken;
    }

    private Task<HttpClient> MijozAsync(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return Task.FromResult(client);
    }

    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
