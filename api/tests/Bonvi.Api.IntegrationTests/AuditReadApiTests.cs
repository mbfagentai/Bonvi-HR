using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Audit jurnalini O'QISH — ruxsat, ko'rinish, yashirish va nomlar (SPEC-rollar-va-audit §7.10,
/// §8.3, §12.1 «A2»).
/// </summary>
/// <remarks>
/// <para>
/// Haqiqiy PostgreSQL, haqiqiy interceptor va haqiqiy HTTP: qator yozish tomonida qanday
/// yozilgan bo'lsa, o'qish tomonida xuddi shu qatordan o'qiladi. Ko'rinish SQL'da
/// qo'llanadi — bu faqat bazada tekshiriladi.
/// </para>
/// <para>
/// Testlar bitta bazani baham ko'radi: har biri o'z belgisi bilan xodim yoki yozuv yaratadi
/// va jurnaldan faqat O'Z qatorlarini so'raydi (xodim, yozuv yoki bajaruvchi bo'yicha).
/// </para>
/// </remarks>
public sealed class AuditReadApiTests : IClassFixture<BonviApiFactory>
{
    private const string AdminIsmi = "Test Administrator Testovich";

    private readonly BonviApiFactory _factory;

    public AuditReadApiTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Belgi() => Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..8].ToUpperInvariant();

    // ────────────────────────────────────────────────────────────
    //  Ruxsat (RBAC)
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// <c>audit.view</c> siz to'rtala marshrut ham siyosat bilan rad etiladi — boshqa ruxsatlar
    /// qancha bo'lsa ham (jurnal ochish alohida huquq).
    /// </summary>
    [Fact]
    public async Task Audit_view_siz_har_bir_marshrut_403()
    {
        using var client = await MijozAsync(
            Permissions.EmployeesView, Permissions.EmploymentView, Permissions.SalaryView,
            Permissions.UsersManage, Permissions.RolesManage);

        string[] yollar =
        [
            "/api/audit",
            "/api/audit/actors",
            $"/api/audit/{Guid.CreateVersion7()}",
            $"/api/employees/{Guid.CreateVersion7()}/history",
        ];

        foreach (var yol in yollar)
        {
            using var javob = await client.GetAsync(new Uri(yol, UriKind.Relative), Ct);

            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden, yol);
            (await MatnAsync(javob)).ShouldContain("auth.ruxsat_yoq", customMessage: yol);
        }
    }

    /// <summary>Xodim tarixi — <c>audit.view</c> VA <c>employees.view</c> ikkalasi ham kerak.</summary>
    [Fact]
    public async Task Xodim_tarixi_ikki_ruxsatni_talab_qiladi()
    {
        var urug = await UrugAsync(Belgi());
        var yol = new Uri($"/api/employees/{urug.XodimId}/history", UriKind.Relative);

        using (var faqatAudit = await MijozAsync(Permissions.AuditView))
        using (var javob = await faqatAudit.GetAsync(yol, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using (var faqatXodim = await MijozAsync(Permissions.EmployeesView))
        using (var javob = await faqatXodim.GetAsync(yol, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using var ikkalasi = await MijozAsync(Permissions.AuditView, Permissions.EmployeesView);
        var sahifa = await SahifaAsync(ikkalasi, yol.OriginalString);

        sahifa.Items.ShouldContain(qator => qator.EntityName == "Employee" && qator.Action == "Create");
        sahifa.Items.ShouldAllBe(qator => qator.SubjectEmployeeId == urug.XodimId);
    }

    /// <summary>Jurnal faqat o'qiladi: <c>/api/audit</c> ostida yozish marshruti yo'q.</summary>
    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task Jurnalni_ozgartiradigan_marshrut_yoq(string usul)
    {
        using var client = await AdminMijozAsync();
        using var sorov = new HttpRequestMessage(new HttpMethod(usul), new Uri($"/api/audit/{Guid.CreateVersion7()}", UriKind.Relative));

        using var javob = await client.SendAsync(sorov, Ct);

        javob.StatusCode.ShouldBeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
    }

    // ────────────────────────────────────────────────────────────
    //  Ko'rinish
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Ko'rinmaydigan bo'lim — xato emas, bo'sh sahifa (ko'rinish filtrdan oldin qo'llanadi);
    /// xuddi shu qatorlar bo'lim ruxsati bilan chiqadi.
    /// </summary>
    [Fact]
    public async Task Korinmaydigan_bolim_bosh_sahifa_beradi()
    {
        var urug = await UrugAsync(Belgi());

        using var torRuxsat = await MijozAsync(Permissions.AuditView, Permissions.EmployeesView);

        var rekruting = await SahifaAsync(torRuxsat, "/api/audit?module=recruiting");
        rekruting.Total.ShouldBe(0);
        rekruting.Items.ShouldBeEmpty();

        var mehnat = await SahifaAsync(torRuxsat, $"/api/audit?module=employment&employeeId={urug.XodimId}");
        mehnat.Total.ShouldBe(0, "employment.view yo'q — munosabat va buyruq qatorlari ko'rinmaydi");

        var hammasi = await SahifaAsync(torRuxsat, $"/api/audit?employeeId={urug.XodimId}");
        hammasi.Items.Select(qator => qator.EntityName).Distinct().ShouldBe(["Employee"]);

        using var mehnatRuxsati = await MijozAsync(Permissions.AuditView, Permissions.EmployeesView, Permissions.EmploymentView);

        var ochiq = await SahifaAsync(mehnatRuxsati, $"/api/audit?module=employment&employeeId={urug.XodimId}");
        ochiq.Items.Select(qator => qator.EntityName).ShouldContain("Employment");
        ochiq.Items.Select(qator => qator.EntityName).ShouldContain("EmploymentEvent");
        ochiq.Items.ShouldAllBe(qator => qator.Module == "employment");
    }

    /// <summary>
    /// Kirish hodisalari — faqat <c>users.manage</c> bilan; ruxsatsiz «security» xato emas,
    /// «changes» bo'lib ishlaydi va hodisa chiqmaydi.
    /// </summary>
    [Fact]
    public async Task Xavfsizlik_hodisalari_users_manage_siz_korinmaydi()
    {
        var hisob = await TestAccounts.CreateAsync(_factory);
        _ = await KirishAsync(hisob.Login, TestAccounts.Password);

        using var ruxsatsiz = await MijozAsync(Permissions.AuditView, Permissions.EmployeesView);

        var xavfsizlik = await SahifaAsync(ruxsatsiz, $"/api/audit?kind=security&actor={hisob.UserId}");
        xavfsizlik.Items.ShouldNotContain(qator => qator.Action == "Login");
        xavfsizlik.Total.ShouldBe(0);

        var hammasi = await SahifaAsync(ruxsatsiz, $"/api/audit?kind=all&actor={hisob.UserId}");
        hammasi.Items.ShouldNotContain(qator => qator.Action == "Login");

        using var boshqaruvchi = await MijozAsync(Permissions.AuditView, Permissions.UsersManage);

        var kirish = (await SahifaAsync(boshqaruvchi, $"/api/audit?kind=security&actor={hisob.UserId}"))
            .Items.ShouldHaveSingleItem();
        kirish.Action.ShouldBe("Login");
        kirish.Module.ShouldBe("users");
        kirish.Actor.UserId.ShouldBe(hisob.UserId);

        (await SahifaAsync(boshqaruvchi, $"/api/audit?actor={hisob.UserId}"))
            .Items.ShouldNotContain(qator => qator.Action == "Login", "sukut — faqat o'zgarishlar");
    }

    /// <summary>
    /// Qobiliyat, hujjat va tekshiruv materiali QATORI — shaxsiy ma'lumot ruxsatisiz ko'rinmaydi
    /// (bo'lim ochiq bo'lsa ham).
    /// </summary>
    [Fact]
    public async Task Shaxsiy_qatorlar_ruxsatsiz_royxatga_tushmaydi()
    {
        var urug = await UrugAsync(Belgi());

        await BazadaAsync(async db =>
        {
            db.Add(EmployeeCompetency.Create(urug.XodimId).Value);
            await db.SaveChangesAsync(Ct);
            return 0;
        });

        using var korish = await MijozAsync(Permissions.AuditView, Permissions.EmployeesView);
        (await SahifaAsync(korish, $"/api/employees/{urug.XodimId}/history"))
            .Items.ShouldNotContain(qator => qator.EntityName == "EmployeeCompetency");

        using var shaxsiy = await MijozAsync(Permissions.AuditView, Permissions.EmployeesView, Permissions.EmployeesPersonalView);
        (await SahifaAsync(shaxsiy, $"/api/employees/{urug.XodimId}/history"))
            .Items.ShouldContain(qator => qator.EntityName == "EmployeeCompetency");
    }

    /// <summary>
    /// Ko'rinmaydigan qatorning tafsiloti — 404 <c>audit.topilmadi</c>, mavjud bo'lmagan bilan
    /// bir xil javob; ko'radiganga — 200.
    /// </summary>
    [Fact]
    public async Task Korinmaydigan_qator_tafsiloti_topilmadi()
    {
        var yonalishId = await BazadaAsync(async db =>
        {
            var yonalish = Direction.Create("Кўринмас йўналиш " + Belgi()).Value;
            db.Directions.Add(yonalish);
            await db.SaveChangesAsync(Ct);
            return yonalish.Id;
        });

        using var admin = await AdminMijozAsync();
        var qator = (await SahifaAsync(admin, $"/api/audit?entity=Direction&entityId={yonalishId}")).Items.ShouldHaveSingleItem();

        using var tor = await MijozAsync(Permissions.AuditView, Permissions.EmployeesView);

        using (var javob = await tor.GetAsync(new Uri($"/api/audit/{qator.Id}", UriKind.Relative), Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await MatnAsync(javob)).ShouldContain("audit.topilmadi");
        }

        using (var javob = await tor.GetAsync(new Uri($"/api/audit/{Guid.CreateVersion7()}", UriKind.Relative), Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await MatnAsync(javob)).ShouldContain("audit.topilmadi");
        }

        var tafsilot = await TafsilotAsync(admin, qator.Id);
        tafsilot.Entry.Id.ShouldBe(qator.Id);
        tafsilot.Entry.Module.ShouldBe("catalog");
    }

    /// <summary>
    /// «Shu amal ichida» — bir saqlashda yozilgan qatorlar, faqat ko'rinadiganlari: mehnat
    /// ruxsati bilan xodim kartasi va ma'lumotnoma qatorlari tushib qoladi.
    /// </summary>
    [Fact]
    public async Task Bir_amal_qatorlari_korinish_boyicha_filtrlanadi()
    {
        var urug = await UrugAsync(Belgi());

        using var admin = await AdminMijozAsync();
        var munosabat = (await SahifaAsync(admin, $"/api/audit?employeeId={urug.XodimId}&entity=Employment&action=Create"))
            .Items.ShouldHaveSingleItem();

        var toliq = await TafsilotAsync(admin, munosabat.Id);
        toliq.Related.Select(qator => qator.EntityName).ShouldContain("Employee");
        toliq.Related.Select(qator => qator.EntityName).ShouldContain("Department");
        toliq.Related.ShouldAllBe(qator => qator.CorrelationId == munosabat.CorrelationId);
        toliq.Related.ShouldNotContain(qator => qator.Id == munosabat.Id, "qatorning o'zi «related» da takrorlanmaydi");

        using var mehnat = await MijozAsync(Permissions.AuditView, Permissions.EmploymentView);
        var tor = await TafsilotAsync(mehnat, munosabat.Id);

        string[] nomlar = [.. tor.Related.Select(qator => qator.EntityName).Distinct().Order(StringComparer.Ordinal)];
        nomlar.ShouldBe(["EmploymentEvent"], customMessage: string.Join(", ", nomlar));
    }

    // ────────────────────────────────────────────────────────────
    //  Yashirish
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Oklad — <c>salary.view</c> siz «masked», qiymatsiz (maydon NOMI ko'rinadi); ruxsat bilan —
    /// bitta pul qiymati <c>{ amount, currency }</c>, tahrirda eski → yangi.
    /// </summary>
    [Fact]
    public async Task Oklad_ruxsatsiz_yashirin_ruxsat_bilan_bitta_pul_qiymati()
    {
        var urug = await UrugAsync(Belgi(), oklad: 4_123_457m);

        await BazadaAsync(async db =>
        {
            var munosabat = await db.Employments.SingleAsync(row => row.Id == urug.MunosabatId, Ct);
            munosabat.SetSalary(Money.Create(4_765_433m).Value, new DateOnly(2025, 6, 1)).IsSuccess.ShouldBeTrue();
            await db.SaveChangesAsync(Ct);
            return 0;
        });

        var yol = $"/api/employees/{urug.XodimId}/history";

        using var okladsiz = await MijozAsync(Permissions.AuditView, Permissions.EmployeesView, Permissions.EmploymentView);
        var (yopiq, xom) = await SahifaVaMatnAsync(okladsiz, yol);

        var yaratish = yopiq.Items.Single(qator => qator.EntityName == "Employment" && qator.Action == "Create");
        var yashirin = yaratish.Changes.Single(ozgarish => ozgarish.Field == "Salary");
        yashirin.Masked.ShouldBeTrue();
        yashirin.ValueType.ShouldBe("money");
        yashirin.Old.ShouldBeNull();
        yashirin.New.ShouldBeNull();

        yopiq.Items.Single(qator => qator.EntityName == "Employment" && qator.Action == "Update")
            .Changes.Single(ozgarish => ozgarish.Field == "Salary").Masked.ShouldBeTrue();

        xom.ShouldNotContain("4123457");
        xom.ShouldNotContain("4765433");

        using var okladli = await MijozAsync(
            Permissions.AuditView, Permissions.EmployeesView, Permissions.EmploymentView, Permissions.SalaryView);
        var ochiq = await SahifaAsync(okladli, yol);

        var boshlangich = ochiq.Items.Single(qator => qator.EntityName == "Employment" && qator.Action == "Create")
            .Changes.Single(ozgarish => ozgarish.Field == "Salary");
        boshlangich.Masked.ShouldBeFalse();
        Summa(boshlangich.New).ShouldBe((4_123_457m, "UZS"));
        ochiq.Items.Single(qator => qator.EntityName == "Employment" && qator.Action == "Create")
            .Changes.Select(ozgarish => ozgarish.Field)
            .ShouldNotContain("Salary.Amount", "pul jufti bitta maydonga birlashadi");

        var tahrir = ochiq.Items.Single(qator => qator.EntityName == "Employment" && qator.Action == "Update")
            .Changes.Single(ozgarish => ozgarish.Field == "Salary");
        tahrir.ValueType.ShouldBe("money");
        tahrir.Old.ShouldNotBeNull().GetProperty("amount").GetDecimal().ShouldBe(4_123_457m);
        tahrir.New.ShouldNotBeNull().GetProperty("amount").GetDecimal().ShouldBe(4_765_433m);
    }

    /// <summary>
    /// Qo'shimcha vazifa haqi — <c>salary.view</c> ning o'zi yetmaydi, <c>salary.extra.view</c>
    /// ham kerak; tuzatish qatorida buyruq turi yo'q bo'lsa ham (u bazadan olinadi). Oddiy oklad
    /// buyrug'ining tuzatishi esa faqat <c>salary.view</c> bilan ochiq.
    /// </summary>
    [Fact]
    public async Task Qoshimcha_vazifa_haqi_alohida_ruxsat_bilan()
    {
        var belgi = Belgi();
        var urug = await UrugAsync(belgi, oklad: 3_000_000m);

        await BazadaAsync(async db =>
        {
            var munosabat = await db.Employments.Include(row => row.Events).SingleAsync(row => row.Id == urug.MunosabatId, Ct);
            munosabat.AddAdditionalDuty(new DateOnly(2025, 3, 1), "Навбатчилик " + belgi, Money.Create(1_111_119m).Value)
                .IsSuccess.ShouldBeTrue();
            await db.SaveChangesAsync(Ct);
            return 0;
        });

        // Ikkala buyruqning summasini tuzatish — tahrir qatorlarida `Type` yo'q
        await BazadaAsync(async db =>
        {
            var munosabat = await db.Employments.Include(row => row.Events).SingleAsync(row => row.Id == urug.MunosabatId, Ct);

            var vazifa = munosabat.Events.Single(item => item.Type == EmploymentEventType.AdditionalDuty);
            munosabat.EditEvent(vazifa.Id, vazifa.EffectiveDate, toSalary: Money.Create(1_222_223m).Value).IsSuccess.ShouldBeTrue();

            var oklad = munosabat.Events.Single(item => item.Type == EmploymentEventType.SalaryChange);
            munosabat.EditEvent(oklad.Id, oklad.EffectiveDate, toSalary: Money.Create(3_333_337m).Value).IsSuccess.ShouldBeTrue();

            await db.SaveChangesAsync(Ct);
            return 0;
        });

        var yol = $"/api/employees/{urug.XodimId}/history?module=employment";

        using var okladli = await MijozAsync(
            Permissions.AuditView, Permissions.EmployeesView, Permissions.EmploymentView, Permissions.SalaryView);
        var (sahifa, xom) = await SahifaVaMatnAsync(okladli, yol);

        var buyruqlar = sahifa.Items.Where(qator => qator.EntityName == "EmploymentEvent").ToList();
        var vazifaHaqi = buyruqlar
            .Where(qator => qator.Changes.Any(ozgarish => ozgarish.Field == "Note" && ozgarish.New?.GetString() == "Навбатчилик " + belgi))
            .ShouldHaveSingleItem()
            .Changes.Single(ozgarish => ozgarish.Field == "ToSalary");
        vazifaHaqi.Masked.ShouldBeTrue("qo'shimcha haq — salary.extra.view siz");

        xom.ShouldNotContain("1111119");
        xom.ShouldNotContain("1222223");
        xom.ShouldContain("3333337", Case.Sensitive, "oddiy oklad tuzatishi — salary.view bilan ochiq (tur bazadan olindi)");

        using var ikkalasi = await MijozAsync(
            Permissions.AuditView, Permissions.EmployeesView, Permissions.EmploymentView,
            Permissions.SalaryView, Permissions.SalaryExtraView);
        var (_, ochiqXom) = await SahifaVaMatnAsync(ikkalasi, yol);

        ochiqXom.ShouldContain("1111119");
        ochiqXom.ShouldContain("1222223");
    }

    /// <summary>
    /// Shaxsiy maydonlar (tug'ilgan sana, manzil) — ruxsatsiz «masked», ism esa ochiq;
    /// <c>employees.personal.view</c> yoki tahrirlash huquqi bilan — ochiq.
    /// </summary>
    [Fact]
    public async Task Shaxsiy_maydon_ruxsatsiz_yashirin()
    {
        var belgi = Belgi();
        var urug = await UrugAsync(belgi);
        var yol = $"/api/employees/{urug.XodimId}/history";

        using var korish = await MijozAsync(Permissions.AuditView, Permissions.EmployeesView);
        var (yopiq, xom) = await SahifaVaMatnAsync(korish, yol);

        var karta = yopiq.Items.Single(qator => qator.EntityName == "Employee" && qator.Action == "Create");
        karta.Changes.Single(ozgarish => ozgarish.Field == "Address").Masked.ShouldBeTrue();
        karta.Changes.Single(ozgarish => ozgarish.Field == "BirthDate").Masked.ShouldBeTrue();

        var familiya = karta.Changes.Single(ozgarish => ozgarish.Field == "FullName.LastName");
        familiya.Masked.ShouldBeFalse();
        familiya.New.ShouldNotBeNull().GetString().ShouldBe("Ўқишов");

        xom.ShouldNotContain("Чилонзор " + belgi);
        xom.ShouldNotContain("1990-05-01");

        foreach (var ruxsat in new[] { Permissions.EmployeesPersonalView, Permissions.EmployeesEdit })
        {
            using var ochiq = await MijozAsync(Permissions.AuditView, Permissions.EmployeesView, ruxsat);
            var manzil = (await SahifaAsync(ochiq, yol)).Items
                .Single(qator => qator.EntityName == "Employee" && qator.Action == "Create")
                .Changes.Single(ozgarish => ozgarish.Field == "Address");

            manzil.Masked.ShouldBeFalse(ruxsat);
            manzil.New.ShouldNotBeNull().GetString().ShouldBe("Тошкент, Чилонзор " + belgi);
        }
    }

    /// <summary>
    /// Yozishda yashirilgan sir (parol xeshi, <c>"***"</c>) o'qishda ham «masked» — literal
    /// hech qachon javobga chiqmaydi, eng keng ruxsat bilan ham.
    /// </summary>
    [Fact]
    public async Task Yozishda_yashirilgan_sir_oqishda_ham_masked()
    {
        var hisob = await TestAccounts.CreateAsync(_factory);

        using var admin = await AdminMijozAsync();
        using (var javob = await admin.PostAsJsonAsync(
                   new Uri($"/api/users/{hisob.UserId}/reset-password", UriKind.Relative),
                   new { newPassword = "Yangi_Oqish_Parol_7" },
                   Ct))
        {
            javob.IsSuccessStatusCode.ShouldBeTrue(await MatnAsync(javob));
        }

        var (sahifa, xom) = await SahifaVaMatnAsync(admin, $"/api/audit?entity=User&entityId={hisob.UserId}&action=Update");

        var parol = sahifa.Items.ShouldHaveSingleItem().Changes.Single(ozgarish => ozgarish.Field == "PasswordHash");
        parol.Masked.ShouldBeTrue();
        parol.Old.ShouldBeNull();
        parol.New.ShouldBeNull();

        xom.ShouldNotContain("***");
    }

    // ────────────────────────────────────────────────────────────
    //  Nomlar
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Havola maydoni — nomi bilan, o'chirilgan bo'lim va lavozimning nomi ham (o'qish paytida,
    /// filtrsiz).
    /// </summary>
    [Fact]
    public async Task Ochirilgan_bolim_va_lavozim_nomi_ham_chiqadi()
    {
        var urug = await UrugAsync(Belgi());

        await BazadaAsync(async db =>
        {
            var munosabat = await db.Employments.SingleAsync(row => row.Id == urug.MunosabatId, Ct);
            munosabat.Transfer(new DateOnly(2025, 9, 1), urug.YangiBolimId, newPositionId: null).IsSuccess.ShouldBeTrue();
            await db.SaveChangesAsync(Ct);
            return 0;
        });

        await BazadaAsync(async db =>
        {
            db.Departments.Remove(await db.Departments.SingleAsync(row => row.Id == urug.EskiBolimId, Ct));
            db.Positions.Remove(await db.Positions.SingleAsync(row => row.Id == urug.LavozimId, Ct));
            await db.SaveChangesAsync(Ct);
            return 0;
        });

        (await BazadaAsync(db => db.Departments.AnyAsync(row => row.Id == urug.EskiBolimId, Ct)))
            .ShouldBeFalse("bo'lim yumshoq o'chirildi — oddiy so'rovda ko'rinmaydi");

        using var admin = await AdminMijozAsync();
        var qatorlar = (await SahifaAsync(admin, $"/api/audit?employeeId={urug.XodimId}&entity=Employment")).Items;

        var kochirish = qatorlar.Single(qator => qator.Action == "Update").Changes.Single(ozgarish => ozgarish.Field == "DepartmentId");
        kochirish.ValueType.ShouldBe("ref");
        kochirish.Old.ShouldNotBeNull().GetString().ShouldBe(urug.EskiBolimId.ToString());
        kochirish.OldLabel.ShouldBe(urug.EskiBolimNomi);
        kochirish.NewLabel.ShouldBe(urug.YangiBolimNomi);

        var lavozim = qatorlar.Single(qator => qator.Action == "Create").Changes.Single(ozgarish => ozgarish.Field == "PositionId");
        lavozim.NewLabel.ShouldBe(urug.LavozimNomi);

        var firma = qatorlar.Single(qator => qator.Action == "Create").Changes.Single(ozgarish => ozgarish.Field == "LegalEntityId");
        firma.NewLabel.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>Buyruq muallifi mehnat yo'lida — ism bilan; tizim yozgani — muallifsiz (§7.9).</summary>
    [Fact]
    public async Task Mehnat_yolida_buyruq_muallifi_ismi_bilan()
    {
        var belgi = Belgi();
        var urug = await UrugAsync(belgi);

        using var admin = await AdminMijozAsync();
        using (var javob = await admin.PostAsJsonAsync(
                   new Uri($"/api/employments/{urug.MunosabatId}/transfer", UriKind.Relative),
                   new { effectiveDate = new DateOnly(2026, 3, 1), departmentId = urug.YangiBolimId, orderNo = belgi + "-K" },
                   Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        }

        using var lenta = await admin.GetAsync(new Uri($"/api/employees/{urug.XodimId}/timeline", UriKind.Relative), Ct);
        lenta.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(lenta));

        using var hujjat = JsonDocument.Parse(await MatnAsync(lenta));
        var hodisalar = hujjat.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("event"))
            .ToList();

        var kochirish = hodisalar.Single(hodisa => hodisa.GetProperty("orderNo").GetString() == belgi + "-K");
        kochirish.GetProperty("recordedByName").GetString().ShouldBe(AdminIsmi);

        var qabul = hodisalar.Single(hodisa => hodisa.GetProperty("orderNo").GetString() == belgi + "-1");
        qabul.GetProperty("recordedByName").ValueKind.ShouldBe(JsonValueKind.Null, "bazaga to'g'ridan-to'g'ri yozilgan — muallifsiz");
    }

    // ────────────────────────────────────────────────────────────
    //  Filtrlar va sahifalar
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Notogri_davr_va_notanish_filtr_400()
    {
        using var admin = await AdminMijozAsync();

        using (var javob = await admin.GetAsync(new Uri("/api/audit?from=2026-10-05&to=2026-10-01", UriKind.Relative), Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await MatnAsync(javob)).ShouldContain("audit.davr_notogri");
        }

        foreach (var sorov in new[] { "module=xodimlar", "action=Ochirish", "actor=admin", "kind=hammasi", "entity=Xodim" })
        {
            using var javob = await admin.GetAsync(new Uri("/api/audit?" + sorov, UriKind.Relative), Ct);

            javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest, sorov);
            (await MatnAsync(javob)).ShouldContain("audit.filtr_notogri", customMessage: sorov);
        }
    }

    /// <summary><c>actor=system</c> — HTTP'siz yozilgan qatorlar; foydalanuvchi filtri ularni olmaydi.</summary>
    [Fact]
    public async Task Tizim_bajaruvchisi_filtri()
    {
        var yonalishId = await BazadaAsync(async db =>
        {
            var yonalish = Direction.Create("Тизим йўналиши " + Belgi()).Value;
            db.Directions.Add(yonalish);
            await db.SaveChangesAsync(Ct);
            return yonalish.Id;
        });

        using var admin = await AdminMijozAsync();
        var adminId = await AdminIdAsync();

        var tizim = (await SahifaAsync(admin, $"/api/audit?actor=system&entity=Direction&entityId={yonalishId}"))
            .Items.ShouldHaveSingleItem();
        tizim.Actor.Kind.ShouldBe("System");
        tizim.Actor.UserId.ShouldBeNull();

        (await SahifaAsync(admin, $"/api/audit?actor={adminId}&entity=Direction&entityId={yonalishId}"))
            .Total.ShouldBe(0);
    }

    /// <summary>Sahifalar: «total» — hamma sahifalardagi qatorlar soni, sahifalar kesishmaydi.</summary>
    [Fact]
    public async Task Sahifalar_jamini_toliq_va_takrorsiz_qoplaydi()
    {
        var belgi = Belgi();
        var urug = await UrugAsync(belgi);

        for (var i = 1; i <= 3; i++)
        {
            var tartib = i;
            await BazadaAsync(async db =>
            {
                var xodim = await db.Employees.SingleAsync(row => row.Id == urug.XodimId, Ct);
                xodim.Rename(FullName.Create("Ўқишов", "Ходим", belgi + tartib.ToString(CultureInfo.InvariantCulture)).Value)
                    .IsSuccess.ShouldBeTrue();
                await db.SaveChangesAsync(Ct);
                return 0;
            });
        }

        using var admin = await AdminMijozAsync();
        var birinchi = await SahifaAsync(admin, $"/api/employees/{urug.XodimId}/history?pageSize=2");

        birinchi.Total.ShouldBeGreaterThanOrEqualTo(5, "xodim, munosabat, buyruq va uchta tahrir");
        birinchi.PageCount.ShouldBe((birinchi.Total + 1) / 2);

        var hammasi = new List<Qator>(birinchi.Items);
        for (var sahifa = 2; sahifa <= birinchi.PageCount; sahifa++)
        {
            hammasi.AddRange((await SahifaAsync(admin, $"/api/employees/{urug.XodimId}/history?pageSize=2&page={sahifa}")).Items);
        }

        hammasi.Count.ShouldBe(birinchi.Total);
        hammasi.Select(qator => qator.Id).ShouldBeUnique();
        hammasi.Select(qator => qator.OccurredAt).ShouldBeInOrder(SortDirection.Descending);

        (await SahifaAsync(admin, $"/api/employees/{urug.XodimId}/history?pageSize=2&page=999")).Items.ShouldBeEmpty();
    }

    /// <summary>«Kim» filtri — jurnalda uchragan foydalanuvchilar, ismi va faolligi bilan.</summary>
    [Fact]
    public async Task Aktorlar_royxatida_jurnaldagi_foydalanuvchi_bor()
    {
        using var admin = await AdminMijozAsync();
        var adminId = await AdminIdAsync();

        using var javob = await admin.GetAsync(new Uri("/api/audit/actors", UriKind.Relative), Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var aktorlar = (await javob.Content.ReadFromJsonAsync<List<Aktor>>(Ct)).ShouldNotBeNull();
        var men = aktorlar.Single(aktor => aktor.UserId == adminId);
        men.FullName.ShouldBe(AdminIsmi);
        men.IsActive.ShouldBeTrue();
        men.Login.ShouldStartWith("+998");
    }

    // ────────────────────────────────────────────────────────────
    //  Kengaytirishdan oldingi qatorlar
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Eski shakldagi qatorlar (amal nomi, bajaruvchi ismi va maydonlar ro'yxati yo'q, to'liq
    /// nusxa) xatosiz chiziladi: tahrirda farq qilgan kalit, ism — hozirgisi, rol nomi — yo'q.
    /// </summary>
    [Fact]
    public async Task Eski_shakldagi_qatorlar_chiziladi()
    {
        var adminId = await AdminIdAsync();
        var rolId = Guid.CreateVersion7();
        var hisobId = Guid.CreateVersion7();

        await BazadaAsync(async db =>
        {
            db.AuditLogs.Add(AuditLog.Record(
                DateTimeOffset.UtcNow,
                AuditAction.Update,
                "Role",
                rolId.ToString(),
                adminId,
                "+998905088888",
                oldValues: """{"code":"custom_eski","name":"Eski nom","permissions":["employees.view"]}""",
                newValues: """{"code":"custom_eski","name":"Yangi nom","permissions":["employees.view"]}"""));

            db.AuditLogs.Add(AuditLog.Record(
                DateTimeOffset.UtcNow,
                AuditAction.Create,
                "User",
                hisobId.ToString(),
                adminId,
                "+998905088888",
                newValues: """{"login":"+998900000000","fullName":"Eski Hisob","role":"hr_manager"}"""));

            await db.SaveChangesAsync(Ct);
            return 0;
        });

        using var admin = await AdminMijozAsync();

        var rol = (await SahifaAsync(admin, $"/api/audit?entity=Role&entityId={rolId}")).Items.ShouldHaveSingleItem();
        rol.Operation.ShouldBeNull();
        rol.Module.ShouldBe("roles");
        rol.Actor.Kind.ShouldBe("User");
        rol.Actor.Name.ShouldBe(AdminIsmi, "nusxa yo'q — hozirgi ism");
        rol.Actor.RoleName.ShouldBeNull("rol nomi o'sha lahzadagi nusxa bo'lishi kerak — hozirgisi qo'yilmaydi");

        var nom = rol.Changes.ShouldHaveSingleItem();
        nom.Field.ShouldBe("name");
        nom.ValueType.ShouldBe("text");
        nom.Old.ShouldNotBeNull().GetString().ShouldBe("Eski nom");
        nom.New.ShouldNotBeNull().GetString().ShouldBe("Yangi nom");

        var hisob = (await SahifaAsync(admin, $"/api/audit?entity=User&entityId={hisobId}")).Items.ShouldHaveSingleItem();
        hisob.Changes.Select(ozgarish => ozgarish.Field).ShouldBe(["login", "fullName", "role"]);
        hisob.Changes.Single(ozgarish => ozgarish.Field == "login").New.ShouldNotBeNull().GetString().ShouldBe("+998900000000");

        var tafsilot = await TafsilotAsync(admin, rol.Id);
        tafsilot.Related.ShouldBeEmpty("eski qatorda guruh yo'q");
        tafsilot.IpAddress.ShouldBeNull();
    }

    // ════════════════════════════════════════════════════════════
    //  Yordamchilar
    // ════════════════════════════════════════════════════════════

    private sealed record Urug(
        Guid XodimId,
        Guid MunosabatId,
        Guid EskiBolimId,
        string EskiBolimNomi,
        Guid YangiBolimId,
        string YangiBolimNomi,
        Guid LavozimId,
        string LavozimNomi);

    /// <summary>
    /// Firma, ikki bo'lim, lavozim, xodim (tug'ilgan sana va manzil bilan) va mehnat munosabati —
    /// BITTA saqlashda (bitta guruh), HTTP'siz.
    /// </summary>
    private Task<Urug> UrugAsync(string belgi, decimal? oklad = null)
        => BazadaAsync(async db =>
        {
            var firma = LegalEntity.Create("Ўқиш МЧЖ " + belgi).Value;
            var eski = Department.Create("Эски бўлим " + belgi).Value;
            var yangi = Department.Create("Янги бўлим " + belgi).Value;
            var lavozim = Position.Create("Ўқиш лавозими " + belgi).Value;

            var xodim = Employee.Create(FullName.Create("Ўқишов", "Ходим", belgi).Value, new DateOnly(1990, 5, 1)).Value;
            xodim.SetAddress("Тошкент, Чилонзор " + belgi);

            var munosabat = Employment.Hire(
                xodim.Id, firma.Id, new DateOnly(2025, 1, 15), eski.Id, lavozim.Id, orderNo: belgi + "-1").Value;

            if (oklad is { } summa)
            {
                munosabat.SetSalary(Money.Create(summa).Value, new DateOnly(2025, 1, 15)).IsSuccess.ShouldBeTrue();
            }

            db.AddRange(firma, eski, yangi, lavozim, xodim);
            db.Add(munosabat);
            await db.SaveChangesAsync(Ct);

            return new Urug(xodim.Id, munosabat.Id, eski.Id, eski.Name, yangi.Id, yangi.Name, lavozim.Id, lavozim.Name);
        });

    private async Task<T> BazadaAsync<T>(Func<AppDbContext, Task<T>> ish)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        return await ish(qamrov.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private async Task<Guid> AdminIdAsync()
        => (await KirishAsync(BonviApiFactory.TestAdminLogin, BonviApiFactory.TestAdminPassword)).User.Id;

    /// <summary>Faqat shu ruxsatlarga ega yangi hisobning mijozi.</summary>
    private async Task<HttpClient> MijozAsync(params string[] ruxsatlar)
    {
        var token = await TestAccounts.TokenAsync(_factory, ruxsatlar);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<HttpClient> AdminMijozAsync()
    {
        var auth = await KirishAsync(BonviApiFactory.TestAdminLogin, BonviApiFactory.TestAdminPassword);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private async Task<AuthResponse> KirishAsync(string login, string parol)
    {
        using var client = _factory.CreateClient();
        using var javob = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new { login, password = parol },
            Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        return (await javob.Content.ReadFromJsonAsync<AuthResponse>(Ct)).ShouldNotBeNull();
    }

    private static async Task<Sahifa> SahifaAsync(HttpClient client, string yol)
        => (await SahifaVaMatnAsync(client, yol)).Sahifa;

    /// <summary>Sahifa va javobning XOM matni — yashirilgan qiymat hech qayerda yo'qligini tekshirish uchun.</summary>
    private static async Task<(Sahifa Sahifa, string Xom)> SahifaVaMatnAsync(HttpClient client, string yol)
    {
        using var javob = await client.GetAsync(new Uri(yol, UriKind.Relative), Ct);
        var xom = await MatnAsync(javob);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, xom);
        return (JsonSerializer.Deserialize<Sahifa>(xom, Json).ShouldNotBeNull(), xom);
    }

    private static async Task<Tafsilot> TafsilotAsync(HttpClient client, Guid id)
    {
        using var javob = await client.GetAsync(new Uri($"/api/audit/{id}", UriKind.Relative), Ct);
        var xom = await MatnAsync(javob);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, xom);
        return JsonSerializer.Deserialize<Tafsilot>(xom, Json).ShouldNotBeNull();
    }

    private static (decimal Amount, string? Currency) Summa(JsonElement? qiymat)
    {
        var obyekt = qiymat.ShouldNotBeNull();
        return (obyekt.GetProperty("amount").GetDecimal(), obyekt.GetProperty("currency").GetString());
    }

    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(Ct);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record Sahifa(List<Qator> Items, int Page, int PageSize, int Total, int PageCount);

    private sealed record Qator(
        Guid Id,
        DateTimeOffset OccurredAt,
        string Action,
        string? Operation,
        string? Module,
        string EntityName,
        string? EntityId,
        string? EntityLabel,
        Guid? SubjectEmployeeId,
        Bajaruvchi Actor,
        List<Ozgarish> Changes,
        Guid? CorrelationId);

    private sealed record Bajaruvchi(string Kind, Guid? UserId, string? Login, string? Name, string? RoleName);

    private sealed record Ozgarish(
        string Field,
        string ValueType,
        JsonElement? Old,
        JsonElement? New,
        string? OldLabel,
        string? NewLabel,
        bool Masked);

    private sealed record Tafsilot(Qator Entry, string? IpAddress, string? UserAgent, List<Qator> Related);

    private sealed record Aktor(Guid UserId, string FullName, string Login, bool IsActive);
}
