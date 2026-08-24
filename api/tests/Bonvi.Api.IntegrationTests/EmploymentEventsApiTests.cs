using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Pul summasi (test uchun).</summary>
public sealed record MoneyResponse(decimal Amount, string Currency, string Formatted);

/// <summary>Buyruqlar ro'yxatining bitta qatori (test uchun).</summary>
/// <remarks>
/// <c>Type</c> ATAYLAB satr: sanoq qiymatlari JSON'da nom bo'lib chiqadi
/// (<c>JsonStringEnumConverter</c>). Agar u raqamga qaytsa, bu test
/// deserializatsiyada yiqiladi va o'zgarish e'tibordan chetda qolmaydi.
/// </remarks>
public sealed record EmploymentEventItemResponse(
    Guid Id,
    Guid EmploymentId,
    Guid EmployeeId,
    string EmployeeName,
    string? PhotoKey,
    string Type,
    DateOnly EffectiveDate,
    string? OrderNo,
    DateOnly? OrderDate,
    string? FromDepartment,
    string? ToDepartment,
    string? FromPosition,
    string? ToPosition,
    MoneyResponse? FromSalary,
    MoneyResponse? ToSalary,
    string? Note,
    bool IsImported);

/// <summary>Sahifalangan buyruqlar javobi (test uchun).</summary>
public sealed record EmploymentEventPageResponse(
    IReadOnlyList<EmploymentEventItemResponse> Items,
    int Page,
    int PageSize,
    int Total,
    int PageCount);

/// <summary>
/// «Barcha buyruqlar» ro'yxatining uchi-uchiga testlari.
/// </summary>
/// <remarks>
/// <para>
/// Bu ro'yxat Excel'dagi to'rtta varaqning (91.2, 91.3, 91.4, 91.6) o'rnini
/// bosadi, shu sababli undagi xato darhol ko'rinmaydi: HR «avgustda 5 kishi
/// kelgan» degan noto'g'ri javobni to'g'ri deb qabul qiladi. Aynan shuning
/// uchun filtr va sahifalash testda qadalgan.
/// </para>
/// <para>
/// Testlar bitta bazani baham ko'radi va unda boshqa testlarning hodisalari
/// ham bo'lishi mumkin. Shu sababli har bir test O'ZIGA XOS buyruq raqami
/// belgisidan foydalanadi va ro'yxatni <c>search</c> orqali cheklaydi —
/// aks holda «total» qo'shni testning yozuviga bog'liq bo'lib qolardi.
/// </para>
/// </remarks>
public sealed class EmploymentEventsApiTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public EmploymentEventsApiTests(BonviApiFactory factory) => _factory = factory;

    /// <summary>Bitta testning barcha buyruqlarini ajratib turadigan belgi.</summary>
    private static string Belgi() => "BQ" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    // ────────────────────────────────────────────────────────────
    //  Ro'yxatning o'zi
    // ────────────────────────────────────────────────────────────

    /// <summary>Filtrsiz so'rov ishlaydi va sahifalangan shaklda qaytadi.</summary>
    [Fact]
    public async Task Filtrsiz_royxat_200_qaytaradi()
    {
        var belgi = Belgi();
        await UrugAsync(belgi);

        using var client = await AdminMijozAsync();

        using var javob = await client.GetAsync(
            new Uri("/api/employments/events", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var sahifa = await OqishAsync(javob);

        sahifa.Page.ShouldBe(1);
        sahifa.PageSize.ShouldBe(25);
        sahifa.Total.ShouldBeGreaterThanOrEqualTo(3);
        sahifa.Items.ShouldNotBeEmpty();

        // Xodim nomi va rasm kaliti ro'yxatning O'ZIDA bo'lishi kerak:
        // ular uchun qatordan xodim kartasiga qayta so'rov yuborilmaydi.
        sahifa.Items.ShouldAllBe(qator => !string.IsNullOrWhiteSpace(qator.EmployeeName));

        // Saralash: kuchga kirish sanasi bo'yicha kamayish tartibida
        var sanalar = sahifa.Items.Select(qator => qator.EffectiveDate).ToList();
        sanalar.ShouldBe(sanalar.OrderByDescending(sana => sana).ToList());
    }

    /// <summary>Buyruq raqami bo'yicha qidiruv — registrga sezgir emas.</summary>
    /// <remarks>
    /// HR qo'lida qog'oz buyruq bilan keladi va uni katta-kichik harfga
    /// e'tibor bermasdan yozadi. Shu sababli <c>ILIKE</c>.
    /// </remarks>
    [Fact]
    public async Task Buyruq_raqami_boyicha_qidiruv_registrga_sezgir_emas()
    {
        var belgi = Belgi();
        await UrugAsync(belgi);

        using var client = await AdminMijozAsync();

        var sahifa = await SoravAsync(client, $"?search={belgi.ToLowerInvariant()}");

        sahifa.Total.ShouldBe(3);
        sahifa.Items.ShouldAllBe(qator => qator.OrderNo!.StartsWith(belgi, StringComparison.Ordinal));
    }

    /// <summary>
    /// Qidiruv matnidagi «%» — oddiy belgi, joker EMAS.
    /// </summary>
    /// <remarks>
    /// Bu test bir marta yiqilgan holatni qadaydi: ikki argumentli
    /// <c>EF.Functions.ILike</c> Npgsql'da <c>ESCAPE ''</c> bo'lib tarjima
    /// bo'ladi va ekranlash butunlay o'chadi. O'shanda «%» bitta buyruqni
    /// emas, BUTUN jadvalni qaytarardi — va bu jimgina, xatosiz sodir
    /// bo'lardi.
    /// </remarks>
    [Fact]
    public async Task Qidiruvdagi_joker_belgi_oddiy_belgi_sifatida_qaraladi()
    {
        var belgi = Belgi();
        await UrugAsync(belgi);

        using var client = await AdminMijozAsync();

        var joker = await SoravAsync(client, "?search=%25");

        // Ma'lumotda «%» belgili buyruq raqami yo'q — demak natija bo'sh
        joker.Total.ShouldBe(0);
        joker.Items.ShouldBeEmpty();
    }

    // ────────────────────────────────────────────────────────────
    //  Filtrlar
    // ────────────────────────────────────────────────────────────

    /// <summary>Hodisa turi bo'yicha filtr — Excel'dagi «91.3» varag'ining o'rni.</summary>
    [Fact]
    public async Task Tur_boyicha_filtr_ishlaydi()
    {
        var belgi = Belgi();
        await UrugAsync(belgi);

        using var client = await AdminMijozAsync();

        var kochirish = await SoravAsync(client, $"?search={belgi}&type=Transfer");

        kochirish.Total.ShouldBe(1);
        kochirish.Items.Count.ShouldBe(1);
        kochirish.Items[0].Type.ShouldBe("Transfer");

        // Bo'lim va lavozim NOM bo'lib qaytadi, identifikator bo'lib emas
        kochirish.Items[0].FromDepartment.ShouldBe("Sotuv " + belgi);
        kochirish.Items[0].ToDepartment.ShouldBe("Ombor " + belgi);
        kochirish.Items[0].FromPosition.ShouldBe("Sotuvchi " + belgi);
        kochirish.Items[0].ToPosition.ShouldBe("Omborchi " + belgi);

        var qabul = await SoravAsync(client, $"?search={belgi}&type=Hire");

        qabul.Total.ShouldBe(1);
        qabul.Items[0].Type.ShouldBe("Hire");
    }

    /// <summary>Sana oralig'i ikki tomondan yopiq: chegaradagi kun ham kiradi.</summary>
    [Fact]
    public async Task Sana_oraligi_chegara_kunini_ham_oladi()
    {
        var belgi = Belgi();
        await UrugAsync(belgi);

        using var client = await AdminMijozAsync();

        // Ko'chirish aynan 2024-06-01 da
        var oraliq = await SoravAsync(client, $"?search={belgi}&from=2024-06-01&to=2024-06-01");

        oraliq.Total.ShouldBe(1);
        oraliq.Items[0].EffectiveDate.ShouldBe(new DateOnly(2024, 6, 1));
    }

    /// <summary>Bo'lim filtri hodisaning ESKI bo'limiga ham mos keladi.</summary>
    /// <remarks>
    /// «Sotuv bo'limida nima bo'ldi» degan savolga bo'limga kelganlar ham,
    /// undan ketganlar ham javob beradi. Faqat yangi bo'limni qarash
    /// tarixning yarmini yashirardi.
    /// </remarks>
    [Fact]
    public async Task Bolim_filtri_eski_bolimga_ham_mos_keladi()
    {
        var belgi = Belgi();
        var urug = await UrugAsync(belgi);

        using var client = await AdminMijozAsync();

        // Sotuv: qabul (yangi bo'lim) + ko'chirish (eski bo'lim) + oklad.
        // Oklad hodisasida bo'lim ustunlari NULL, shu sababli u
        // munosabatning joriy bo'limiga — Sotuvga — tushadi.
        var sotuv = await SoravAsync(client, $"?search={belgi}&departmentId={urug.SotuvId}");
        sotuv.Total.ShouldBe(3);

        // Ombor: faqat ko'chirish (yangi bo'lim). Oklad bu yerga TUSHMAYDI —
        // munosabatning bo'limi Sotuvligicha qolgan.
        var ombor = await SoravAsync(client, $"?search={belgi}&departmentId={urug.OmborId}");
        ombor.Total.ShouldBe(1);
        ombor.Items[0].Type.ShouldBe("Transfer");
    }

    /// <summary>Bo'lim filtri ICHKI bo'limlarni ham qamrab oladi.</summary>
    /// <remarks>
    /// <para>
    /// Xodimlar ro'yxatida bo'lim filtri butun shoxni oladi (rekursiv CTE).
    /// Bu yerda aniq moslik bo'lsa, bir xil parametr ikki ekranda ikki xil
    /// javob berardi: HR «7-Departament» ni tanlab, uning ichidagi
    /// bo'limlardagi buyruqlarni umuman ko'rmasdi va ro'yxat to'liq deb
    /// o'ylardi.
    /// </para>
    /// <para>
    /// Urug'da hodisa ICHKI bo'limga tegishli, filtr esa OTA bo'lim
    /// bo'yicha: aniq moslikda natija nol bo'lardi.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Bolim_filtri_ichki_bolimlarni_ham_qamraydi()
    {
        var belgi = Belgi();
        var urug = await BolimUrugiAsync(belgi);

        using var client = await AdminMijozAsync();

        var ota = await SoravAsync(client, $"?search={belgi}&departmentId={urug.BoshId}&type=Hire");

        ota.Total.ShouldBe(1);
        ota.Items[0].ToDepartment.ShouldBe("Ички " + belgi);

        // Shoxga kirmaydigan bo'lim — natija bo'sh. Filtr «hamma narsani
        // qaytaradigan» holatga aylanib qolmaganiga ishonch.
        var begona = await SoravAsync(client, $"?search={belgi}&departmentId={Guid.NewGuid()}");

        begona.Total.ShouldBe(0);
        begona.Items.ShouldBeEmpty();
    }

    /// <summary>
    /// Bo'limi ko'rsatilmagan hodisa munosabatning joriy bo'limiga tushadi.
    /// </summary>
    /// <remarks>
    /// <c>SalaryChange</c>, <c>Termination</c> va <c>AdditionalDuty</c> da
    /// <c>from/to_department_id</c> odatda <c>NULL</c>: ular bo'limni
    /// o'zgartirmaydi. Ular filtrga kirmasa, «shu bo'limda nima bo'ldi»
    /// degan savolga oklad o'zgarishlari va bo'shatishlar javob bermasdi —
    /// ya'ni ro'yxat jimgina yarim bo'lardi.
    /// </remarks>
    [Fact]
    public async Task Bolimi_korsatilmagan_hodisa_munosabatning_bolimiga_tushadi()
    {
        var belgi = Belgi();
        var urug = await BolimUrugiAsync(belgi);

        using var client = await AdminMijozAsync();

        var oz = await SoravAsync(client, $"?search={belgi}&departmentId={urug.IchkiId}&type=SalaryChange");

        oz.Total.ShouldBe(1);
        oz.Items[0].FromDepartment.ShouldBeNull("hodisaning o'zida bo'lim yo'q");
        oz.Items[0].ToDepartment.ShouldBeNull("hodisaning o'zida bo'lim yo'q");

        // Ota bo'lim orqali ham topiladi: zaxira shart shox bilan birga ishlaydi
        var ota = await SoravAsync(client, $"?search={belgi}&departmentId={urug.BoshId}&type=SalaryChange");

        ota.Total.ShouldBe(1);
    }

    /// <summary>Yuridik shaxs filtri munosabat orqali qo'llanadi.</summary>
    [Fact]
    public async Task Yuridik_shaxs_boyicha_filtr_ishlaydi()
    {
        var belgi = Belgi();
        var urug = await UrugAsync(belgi);

        using var client = await AdminMijozAsync();

        var oz = await SoravAsync(client, $"?search={belgi}&legalEntityId={urug.FirmaId}");
        oz.Total.ShouldBe(3);

        // Boshqa firma — hodisalar tegishli emas
        var begona = await SoravAsync(client, $"?search={belgi}&legalEntityId={Guid.NewGuid()}");
        begona.Total.ShouldBe(0);
        begona.Items.ShouldBeEmpty();
    }

    /// <summary>Noma'lum hodisa turi 500 emas, 400 qaytaradi.</summary>
    [Fact]
    public async Task Nomalum_tur_400_qaytaradi()
    {
        using var client = await AdminMijozAsync();

        using var javob = await client.GetAsync(
            new Uri("/api/employments/events?type=Boshqa", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ────────────────────────────────────────────────────────────
    //  Sahifalash
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Sahifalash bazada bajariladi va «total» BUTUN to'plamni sanaydi.
    /// </summary>
    /// <remarks>
    /// Eng ko'p uchraydigan xato: <c>Total</c> sahifadagi qatorlar sonini
    /// qaytaradi. Unda interfeys «1–2 / 2» deb yozadi va uchinchi buyruq
    /// HR uchun umuman mavjud bo'lmay qoladi.
    /// </remarks>
    [Fact]
    public async Task Sahifalash_total_ni_togri_qaytaradi()
    {
        var belgi = Belgi();
        await UrugAsync(belgi);

        using var client = await AdminMijozAsync();

        var birinchi = await SoravAsync(client, $"?search={belgi}&page=1&pageSize=2");

        birinchi.Total.ShouldBe(3);
        birinchi.PageCount.ShouldBe(2);
        birinchi.Page.ShouldBe(1);
        birinchi.PageSize.ShouldBe(2);
        birinchi.Items.Count.ShouldBe(2);

        var ikkinchi = await SoravAsync(client, $"?search={belgi}&page=2&pageSize=2");

        ikkinchi.Total.ShouldBe(3);
        ikkinchi.Items.Count.ShouldBe(1);

        // Sahifalar KESISHMASLIGI kerak: teng sanalarda ikkinchi saralash
        // mezoni bo'lmasa, bitta hodisa ikkala sahifada ham chiqib qolardi.
        var hammasi = birinchi.Items.Concat(ikkinchi.Items).Select(qator => qator.Id).ToList();
        hammasi.Distinct().Count().ShouldBe(3);
    }

    // ────────────────────────────────────────────────────────────
    //  Oklad
    // ────────────────────────────────────────────────────────────

    /// <summary>Oklad hodisasida eski va yangi summa yonma-yon qaytadi.</summary>
    /// <remarks>
    /// Administrator <c>salary.view</c> ga ega, shu sababli summalar
    /// to'ldiriladi. Bu bir vaqtning o'zida <c>Money</c> ustunlarining
    /// (summa + valyuta) proyeksiyadan to'g'ri yig'ilishini ham tekshiradi —
    /// u faqat haqiqiy bazada ko'rinadi.
    /// </remarks>
    [Fact]
    public async Task Oklad_hodisasida_eski_va_yangi_summa_qaytadi()
    {
        var belgi = Belgi();
        await UrugAsync(belgi);

        using var client = await AdminMijozAsync();

        var sahifa = await SoravAsync(client, $"?search={belgi}&type=SalaryChange");

        sahifa.Total.ShouldBe(1);

        var qator = sahifa.Items[0];

        qator.FromSalary.ShouldNotBeNull();
        qator.FromSalary.Amount.ShouldBe(4_500_000m);
        qator.FromSalary.Currency.ShouldBe("UZS");

        qator.ToSalary.ShouldNotBeNull();
        qator.ToSalary.Amount.ShouldBe(5_200_000m);
        qator.ToSalary.Formatted.ShouldNotBeNullOrWhiteSpace();

        qator.IsImported.ShouldBeTrue();
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    /// <summary>Test ma'lumotining kalitlari.</summary>
    private sealed record Urug(Guid FirmaId, Guid SotuvId, Guid OmborId, Guid XodimId, Guid MunosabatId);

    /// <summary>Bo'lim daraxti bo'yicha testlarning kalitlari.</summary>
    private sealed record BolimUrugi(Guid BoshId, Guid IchkiId, Guid MunosabatId);

    /// <summary>
    /// Ikki qavatli bo'lim daraxti va uning ichidagi bitta xodim.
    /// </summary>
    /// <remarks>
    /// Asosiy urug'dan ATAYLAB alohida: bu yerdagi hodisalar soni boshqa
    /// testlarning «total» kutilmalariga qo'shilib ketmasligi kerak.
    /// Ikkala hodisa ham bo'lim filtrining ikki xil yo'lini tekshiradi —
    /// biri ichki bo'limga tegishli, ikkinchisida bo'lim umuman yo'q.
    /// </remarks>
    private async Task<BolimUrugi> BolimUrugiAsync(string belgi)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var firma = LegalEntity.Create("Дарахт Тест МЧЖ " + belgi).Value;
        var bosh = Department.Create("Бош " + belgi).Value;
        var ichki = Department.Create("Ички " + belgi, bosh.Id).Value;
        var lavozim = Position.Create("Мутахассис " + belgi).Value;

        var xodim = Employee.Create(FullName.Create("Дарахтов", "Ходим", belgi).Value).Value;

        var munosabat = Employment.Hire(
            xodim.Id,
            firma.Id,
            new DateOnly(2024, 2, 1),
            ichki.Id,
            lavozim.Id,
            orderNo: belgi + "-1").Value;

        // Oklad hodisasida bo'lim ustunlari ATAYLAB bo'sh — aynan shunday
        // hodisalar eski filtrdan tushib qolardi.
        munosabat.ImportEvent(EmploymentEvent.Import(
            munosabat.Id,
            EmploymentEventType.SalaryChange,
            new DateOnly(2024, 7, 1),
            orderNo: belgi + "-2",
            fromSalary: Money.Create(3_000_000m).Value,
            toSalary: Money.Create(3_600_000m).Value));

        kontekst.AddRange(firma, bosh, ichki, lavozim, xodim);
        kontekst.Add(munosabat);

        await kontekst.SaveChangesAsync(TestContext.Current.CancellationToken);

        return new BolimUrugi(bosh.Id, ichki.Id, munosabat.Id);
    }

    /// <summary>
    /// Bitta xodim va uning uchta buyrug'ini yaratadi.
    /// </summary>
    /// <remarks>
    /// Hodisalar domen metodlari orqali emas, <c>ImportEvent</c> bilan
    /// qo'yiladi: testga aynan kerakli tur, sana va buyruq raqami kerak,
    /// domen metodlari esa ular ustidan o'z qoidalarini qo'llaydi
    /// (masalan ko'chirishda tur avtomatik «Promotion» ga aylanadi).
    /// </remarks>
    private async Task<Urug> UrugAsync(string belgi)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var firma = LegalEntity.Create("Buyruq Test MChJ " + belgi).Value;
        var sotuv = Department.Create("Sotuv " + belgi).Value;
        var ombor = Department.Create("Ombor " + belgi).Value;
        var sotuvchi = Position.Create("Sotuvchi " + belgi).Value;
        var omborchi = Position.Create("Omborchi " + belgi).Value;

        var xodim = Employee.Create(FullName.Create("Буйруқов", "Ходим", belgi).Value).Value;

        var munosabat = Employment.Hire(
            xodim.Id,
            firma.Id,
            new DateOnly(2024, 1, 15),
            sotuv.Id,
            sotuvchi.Id,
            orderNo: belgi + "-1").Value;

        munosabat.ImportEvent(EmploymentEvent.Import(
            munosabat.Id,
            EmploymentEventType.Transfer,
            new DateOnly(2024, 6, 1),
            orderNo: belgi + "-2",
            orderDate: new DateOnly(2024, 5, 28),
            fromDepartmentId: sotuv.Id,
            toDepartmentId: ombor.Id,
            fromPositionId: sotuvchi.Id,
            toPositionId: omborchi.Id,
            note: "Omborga o'tkazildi"));

        munosabat.ImportEvent(EmploymentEvent.Import(
            munosabat.Id,
            EmploymentEventType.SalaryChange,
            new DateOnly(2024, 9, 1),
            orderNo: belgi + "-3",
            fromSalary: Money.Create(4_500_000m).Value,
            toSalary: Money.Create(5_200_000m).Value));

        kontekst.AddRange(firma, sotuv, ombor, sotuvchi, omborchi, xodim);
        kontekst.Add(munosabat);

        await kontekst.SaveChangesAsync(TestContext.Current.CancellationToken);

        return new Urug(firma.Id, sotuv.Id, ombor.Id, xodim.Id, munosabat.Id);
    }

    private static async Task<EmploymentEventPageResponse> SoravAsync(HttpClient client, string sorovQatori)
    {
        using var javob = await client.GetAsync(
            new Uri("/api/employments/events" + sorovQatori, UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        return await OqishAsync(javob);
    }

    private static async Task<EmploymentEventPageResponse> OqishAsync(HttpResponseMessage javob)
    {
        var sahifa = await javob.Content.ReadFromJsonAsync<EmploymentEventPageResponse>(
            TestContext.Current.CancellationToken);

        sahifa.ShouldNotBeNull();
        return sahifa;
    }

    private async Task<HttpClient> AdminMijozAsync()
    {
        var client = _factory.CreateClient();

        using var javob = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new { login = BonviApiFactory.TestAdminLogin, password = BonviApiFactory.TestAdminPassword },
            TestContext.Current.CancellationToken);

        javob.EnsureSuccessStatusCode();

        var auth = await javob.Content.ReadFromJsonAsync<AuthResponse>(
            TestContext.Current.CancellationToken);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return client;
    }

    /// <summary>Xato javobini o'qish — tekshiruv yiqilganda sababi ko'rinishi uchun.</summary>
    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
