using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Identity;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Rol (test uchun).</summary>
public sealed record RoleResponse(
    Guid Id,
    string Code,
    string Name,
    bool IsProtected,
    string[] Permissions,
    int UserCount);

/// <summary>
/// Rollar muharriri (<c>/api/roles</c>) va rol o'zgarishining «darhol kuchga
/// kirishi» — HTTP qatlamining uchi-uchiga testlari.
/// </summary>
/// <remarks>
/// <para>
/// Haqiqiy PostgreSQL ustida. Testlar bitta bazani baham ko'radi, shu
/// sababli har bir test o'z rollarini noyob nom bilan yaratadi va standart
/// rollarga (HR menejeri, bosh rahbar) TEGMAYDI — ularni tahrirlash boshqa
/// testlarning natijasini o'zgartirardi.
/// </para>
/// <para>
/// Xato kodlari shartnomadagi jadvalning AYNAN o'zi: frontend ularni to'rt
/// tilda tarjima qiladi va har biri alohida qadab qo'yilgan.
/// </para>
/// </remarks>
public sealed class RoleApiTests : IClassFixture<BonviApiFactory>
{
    private static readonly Uri RolesUri = new("/api/roles", UriKind.Relative);
    private static readonly Uri CatalogUri = new("/api/roles/catalog", UriKind.Relative);
    private static readonly Uri UsersUri = new("/api/users", UriKind.Relative);

    private static int _nomSanagichi;

    private readonly BonviApiFactory _factory;

    public RoleApiTests(BonviApiFactory factory) => _factory = factory;

    // ────────────────────────────────────────────────────────────
    //  Kim kira oladi
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Tokensiz_royxat_401_qaytaradi()
    {
        using var client = _factory.CreateClient();

        using var javob = await client.GetAsync(RolesUri, TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Ro'yxat ikki ekranga kerak: «Rollar» (<c>roles.manage</c>) va
    /// foydalanuvchi formasidagi rol tanlash (<c>users.manage</c>) —
    /// ikkalasidan BIRI yetarli, hech biri bo'lmasa 403.
    /// </summary>
    [Fact]
    public async Task Royxat_users_manage_yoki_roles_manage_bilan_ochiladi()
    {
        using (var begona = await MijozAsync(Permissions.EmployeesView, Permissions.AuditView))
        using (var javob = await begona.GetAsync(RolesUri, TestContext.Current.CancellationToken))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            (await XatoKodiAsync(javob)).ShouldBe("auth.ruxsat_yoq");
        }

        using (var foydalanuvchilar = await MijozAsync(Permissions.UsersManage))
        using (var javob = await foydalanuvchilar.GetAsync(RolesUri, TestContext.Current.CancellationToken))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        }

        using (var rollar = await MijozAsync(Permissions.RolesManage))
        using (var javob = await rollar.GetAsync(RolesUri, TestContext.Current.CancellationToken))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        }
    }

    /// <summary>
    /// <c>users.manage</c> ro'yxatni ochadi, lekin xaritani ham, rolni
    /// o'zgartirishni ham OCHMAYDI — ular faqat <c>roles.manage</c> bilan.
    /// </summary>
    [Fact]
    public async Task Xarita_va_yozish_amallari_faqat_roles_manage_bilan()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var admin = await AdminMijozAsync();
        var rol = await YaratAsync(admin, NoyobNom("Begona"), [Permissions.EmployeesView]);

        using var client = await MijozAsync(Permissions.UsersManage, Permissions.EmployeesView, Permissions.EmployeesDelete);

        using (var javob = await client.GetAsync(CatalogUri, bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using (var javob = await client.PostAsJsonAsync(RolesUri, RolTanasi("Ruxsatsiz", Permissions.EmployeesView), bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using (var javob = await client.PutAsJsonAsync(RolUri(rol.Id), RolTanasi(rol.Name, Permissions.EmployeesDelete), bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using (var javob = await client.DeleteAsync(RolUri(rol.Id), bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        // Hech narsa o'zgarmagan
        (await RolAsync(admin, rol.Id)).Permissions.ShouldBe([Permissions.EmployeesView]);
    }

    // ────────────────────────────────────────────────────────────
    //  Ro'yxat va xarita
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Botning texnik roli ro'yxatda YO'Q; himoyalangan rol birinchi,
    /// qolganlari nom bo'yicha.
    /// </summary>
    [Fact]
    public async Task Royxatda_bot_roli_yoq_himoyalangani_esa_birinchi()
    {
        using var admin = await AdminMijozAsync();

        var rollar = await RoyxatAsync(admin);

        rollar.ShouldNotContain(rol => rol.Code == SystemRoles.RecruitingBot);

        rollar[0].Code.ShouldBe(SystemRoles.SuperAdmin);
        rollar[0].IsProtected.ShouldBeTrue();
        rollar[0].UserCount.ShouldBeGreaterThanOrEqualTo(1, "boshlang'ich administrator shu rolda");
        rollar[0].Permissions.ShouldBe(Permissions.All, ignoreOrder: true);

        rollar.Skip(1).ShouldAllBe(rol => !rol.IsProtected);
        rollar.ShouldContain(rol => rol.Code == SystemRoles.MainBoss);
        rollar.ShouldContain(rol => rol.Code == SystemRoles.HrManager);

        var nomlar = rollar.Skip(1).Select(rol => rol.Name).ToList();
        nomlar.ShouldBe(nomlar.Order(StringComparer.CurrentCulture).ToList(), "himoyalanmaganlar nom bo'yicha");
    }

    /// <summary>
    /// Xarita shartnomadagi shaklda keladi: amali yo'q bo'limda «actions»
    /// maydoni UMUMAN yo'q (<c>null</c> emas) va har bir kod bir marta.
    /// </summary>
    [Fact]
    public async Task Xarita_shartnomadagi_shaklda_keladi()
    {
        using var client = await MijozAsync(Permissions.RolesManage);

        using var javob = await client.GetAsync(CatalogUri, TestContext.Current.CancellationToken);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        using var hujjat = JsonDocument.Parse(await MatnAsync(javob));
        var ildiz = hujjat.RootElement;

        var bolimlar = ildiz.GetProperty("modules").EnumerateArray().ToList();
        bolimlar.Select(bolim => bolim.GetProperty("key").GetString()).ShouldBe(
        [
            "employees", "employment", "lists", "incentives", "feedback", "safety",
            "recruiting", "attendance", "catalog", "reports", "users", "roles", "audit", "settings",
        ]);

        var davomat = bolimlar.Single(bolim => bolim.GetProperty("key").GetString() == "attendance");
        davomat.GetProperty("levels").EnumerateObject().Select(p => p.Name).ShouldBe(["view", "edit", "full"]);
        Kodlar(davomat.GetProperty("levels").GetProperty("full")).Count.ShouldBe(3);
        davomat.TryGetProperty("actions", out _).ShouldBeFalse("amali yo'q bo'limda maydon bo'lmasligi kerak");

        var xodimlar = bolimlar[0].GetProperty("levels");
        Kodlar(xodimlar.GetProperty("view")).ShouldBe([Permissions.EmployeesView]);
        Kodlar(xodimlar.GetProperty("full")).Count.ShouldBe(4);
        bolimlar[0].TryGetProperty("actions", out _).ShouldBeFalse("amali yo'q bo'limda maydon bo'lmasligi kerak");

        var mehnat = bolimlar[1];
        Kodlar(mehnat.GetProperty("actions")).ShouldBe(Permissions.EmploymentActions);
        mehnat.GetProperty("levels").EnumerateObject().Select(p => p.Name).ShouldBe(["view"]);

        Kodlar(ildiz.GetProperty("extras")).ShouldBe([Permissions.SalaryView, Permissions.SalaryExtraView]);

        // Serializatsiyada hech bir kod yo'qolmagan
        var hammasi = bolimlar
            .SelectMany(bolim => bolim.GetProperty("levels").EnumerateObject().SelectMany(daraja => Kodlar(daraja.Value))
                .Concat(bolim.TryGetProperty("actions", out var amallar) ? Kodlar(amallar) : []))
            .Concat(Kodlar(ildiz.GetProperty("extras")))
            .ToHashSet(StringComparer.Ordinal);

        hammasi.ShouldBe(Permissions.All, ignoreOrder: true);
    }

    // ────────────────────────────────────────────────────────────
    //  To'liq hayot sikli
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Rolning_toliq_hayot_sikli_ishlaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var nom = NoyobNom("Kadr bo'limi boshlig'i");

        // 1. Yaratish — 201, kod avtomatik, takrorlar olib tashlanadi
        RoleResponse yaratilgan;
        using (var javob = await admin.PostAsJsonAsync(
            RolesUri,
            RolTanasi("  " + nom + "  ", Permissions.EmploymentView, Permissions.EmployeesView, Permissions.EmploymentView),
            bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Created, await MatnAsync(javob));
            javob.Headers.Location.ShouldNotBeNull();

            yaratilgan = (await javob.Content.ReadFromJsonAsync<RoleResponse>(bekorQilish))!;
        }

        yaratilgan.Name.ShouldBe(nom, "chetdagi probellar olib tashlanadi");
        yaratilgan.Code.ShouldMatch("^custom_[0-9a-f]{8}$");
        yaratilgan.IsProtected.ShouldBeFalse();
        yaratilgan.UserCount.ShouldBe(0);
        yaratilgan.Permissions.ShouldBe([Permissions.EmployeesView, Permissions.EmploymentView]);

        (await RoyxatAsync(admin)).ShouldContain(rol => rol.Id == yaratilgan.Id);

        // 2. Tahrirlash — nom va ruxsatlar TO'LIQ almashadi
        var yangiNom = NoyobNom("Kadrlar boshlig'i");
        using (var javob = await admin.PutAsJsonAsync(
            RolUri(yaratilgan.Id),
            RolTanasi(yangiNom, Permissions.EmploymentView, Permissions.EmploymentSalary),
            bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

            var tahrirlangan = (await javob.Content.ReadFromJsonAsync<RoleResponse>(bekorQilish))!;
            tahrirlangan.Name.ShouldBe(yangiNom);
            tahrirlangan.Code.ShouldBe(yaratilgan.Code, "kod hech qachon o'zgarmaydi");
            tahrirlangan.Permissions.ShouldBe([Permissions.EmploymentSalary, Permissions.EmploymentView]);
        }

        // 3. Rolni hisobga berish — foydalanuvchi formasi kod bilan yuboradi
        var hisobId = await HisobYaratAsync(admin, yaratilgan.Code);
        (await RolAsync(admin, yaratilgan.Id)).UserCount.ShouldBe(1);

        // 4. Egasi bor rol o'chmaydi — hisob FAOLSIZ bo'lsa ham
        using (var javob = await admin.DeleteAsync(RolUri(yaratilgan.Id), bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Conflict, await MatnAsync(javob));
            (await XatoKodiAsync(javob)).ShouldBe("rol.foydalanuvchisi_bor");
        }

        using (var javob = await admin.PostAsync(new Uri($"/api/users/{hisobId}/deactivate", UriKind.Relative), null, bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(javob));
        }

        using (var javob = await admin.DeleteAsync(RolUri(yaratilgan.Id), bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Conflict, await MatnAsync(javob));
            (await XatoKodiAsync(javob)).ShouldBe("rol.foydalanuvchisi_bor");
        }

        // 5. Hisob boshqa rolga o'tkazilgach — o'chadi
        await RolAlmashtirAsync(admin, hisobId, SystemRoles.HrManager);

        using (var javob = await admin.DeleteAsync(RolUri(yaratilgan.Id), bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(javob));
        }

        (await RoyxatAsync(admin)).ShouldNotContain(rol => rol.Id == yaratilgan.Id);

        // 6. Audit: kim, qaysi rol, eski → yangi ruxsatlar
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var yozuvlar = await kontekst.AuditLogs
            .AsNoTracking()
            .Where(log => log.EntityName == "Role" && log.EntityId == yaratilgan.Id.ToString())
            .OrderBy(log => log.OccurredAt)
            .ToListAsync(bekorQilish);

        yozuvlar.Select(log => log.Action).ShouldBe([AuditAction.Create, AuditAction.Update, AuditAction.Delete]);
        yozuvlar.ShouldAllBe(log => log.UserLogin == "+998" + BonviApiFactory.TestAdminLogin);

        var tahrir = yozuvlar[1];
        tahrir.OldValues.ShouldNotBeNull();
        tahrir.OldValues.ShouldContain(Permissions.EmployeesView);
        tahrir.NewValues.ShouldNotBeNull();
        tahrir.NewValues.ShouldContain(Permissions.EmploymentSalary);
        tahrir.NewValues.ShouldNotContain(Permissions.EmployeesView);

        yozuvlar[2].OldValues.ShouldNotBeNull();
        yozuvlar[2].OldValues!.ShouldContain(yaratilgan.Code);
    }

    // ────────────────────────────────────────────────────────────
    //  Qo'riqlar — shartnomadagi jadval
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Notanish_rol_404_qaytaradi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var yoq = Guid.NewGuid();

        using (var javob = await admin.PutAsJsonAsync(RolUri(yoq), RolTanasi(NoyobNom("Yo'q"), Permissions.EmployeesView), bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await XatoKodiAsync(javob)).ShouldBe("rol.topilmadi");
        }

        using (var javob = await admin.DeleteAsync(RolUri(yoq), bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await XatoKodiAsync(javob)).ShouldBe("rol.topilmadi");
        }
    }

    [Theory]
    [InlineData(SystemRoles.SuperAdmin)]
    [InlineData(SystemRoles.RecruitingBot)]
    public async Task Himoyalangan_rolni_ozgartirib_ham_ochirib_ham_bolmaydi(string kod)
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var rolId = await RolIdAsync(kod);

        using (var javob = await admin.PutAsJsonAsync(RolUri(rolId), RolTanasi(NoyobNom("Buzuvchi"), Permissions.EmployeesView), bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            (await XatoKodiAsync(javob)).ShouldBe("rol.himoyalangan");
        }

        using (var javob = await admin.DeleteAsync(RolUri(rolId), bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            (await XatoKodiAsync(javob)).ShouldBe("rol.himoyalangan");
        }
    }

    /// <summary>
    /// <c>roles.manage</c> li odam ham O'Z rolini o'zgartira va o'chira olmaydi.
    /// </summary>
    [Fact]
    public async Task Oz_rolini_ozgartirib_ham_ochirib_ham_bolmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var hisob = await TestAccounts.CreateAsync(
            _factory, Permissions.RolesManage, Permissions.UsersManage, Permissions.EmployeesView);

        using var client = Mijoz(hisob.AccessToken);

        using (var javob = await client.PutAsJsonAsync(
            RolUri(hisob.RoleId),
            RolTanasi(NoyobNom("O'zimniki"), Permissions.RolesManage),
            bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Conflict, await MatnAsync(javob));
            (await XatoKodiAsync(javob)).ShouldBe("rol.oz_roli");
        }

        using (var javob = await client.DeleteAsync(RolUri(hisob.RoleId), bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Conflict, await MatnAsync(javob));
            (await XatoKodiAsync(javob)).ShouldBe("rol.oz_roli");
        }
    }

    /// <summary>
    /// Foydalanuvchilarni boshqaradigan TENG huquqli ikki rol egalari bir-biriga
    /// tega olmaydi (parol tiklash, faolsizlantirish) — hisobni egallab olish yo'li
    /// yopiq. Qat'iy pastdagi rol egasiga esa tega oladi.
    /// </summary>
    [Fact]
    public async Task Teng_huquqli_boshqaruvchilar_bir_biriga_tega_olmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var admin = await AdminMijozAsync();
        var teng = await YaratAsync(admin, NoyobNom("Teng"), [Permissions.UsersManage, Permissions.EmployeesView]);
        var past = await YaratAsync(admin, NoyobNom("Past"), [Permissions.EmployeesView]);

        var hisob = await TestAccounts.CreateAsync(_factory, Permissions.UsersManage, Permissions.EmployeesView);
        using var client = Mijoz(hisob.AccessToken);

        var hamkasb = await HisobYaratAsync(admin, teng.Code);
        var qoldagi = await HisobYaratAsync(admin, past.Code);

        using (var javob = await client.PostAsJsonAsync(
            new Uri($"/api/users/{hamkasb}/reset-password", UriKind.Relative),
            new { newPassword = "Yangi_Parol_123" },
            bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await MatnAsync(javob));
            (await XatoKodiAsync(javob)).ShouldBe("foydalanuvchi.rolga_haqqi_yoq");
        }

        using (var javob = await client.PostAsync(
            new Uri($"/api/users/{hamkasb}/deactivate", UriKind.Relative), content: null, bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await MatnAsync(javob));
        }

        using (var javob = await client.PostAsJsonAsync(
            new Uri($"/api/users/{qoldagi}/reset-password", UriKind.Relative),
            new { newPassword = "Yangi_Parol_123" },
            bekorQilish))
        {
            ((int)javob.StatusCode).ShouldBeLessThan(300, await MatnAsync(javob));
        }
    }

    /// <summary>
    /// Rollar tomonidagi «teng huquqli hamkasb» qoidasi: huquqlari aynan teng
    /// va rollarni boshqaradigan boshqa rolni tahrirlab ham, o'chirib ham
    /// bo'lmaydi — aks holda ikki teng boshqaruvchidan biri ikkinchisining
    /// rolidan <c>roles.manage</c> ni olib, uni qo'l ostiga tushirardi. Super
    /// administrator esa tega oladi.
    /// </summary>
    [Fact]
    public async Task Teng_huquqli_boshqaruvchi_rolni_tahrirlab_ham_ochirib_ham_bolmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var admin = await AdminMijozAsync();
        string[] ruxsatlar = [Permissions.RolesManage, Permissions.EmployeesView];
        var hamkasb = await YaratAsync(admin, NoyobNom("Hamkasb"), ruxsatlar);

        var hisob = await TestAccounts.CreateAsync(_factory, ruxsatlar);
        using var client = Mijoz(hisob.AccessToken);

        using (var javob = await client.PutAsJsonAsync(
            RolUri(hamkasb.Id),
            RolTanasi(hamkasb.Name, Permissions.EmployeesView),
            bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Conflict, await MatnAsync(javob));
            (await XatoKodiAsync(javob)).ShouldBe("rol.teng_huquqli");
        }

        using (var javob = await client.DeleteAsync(RolUri(hamkasb.Id), bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Conflict, await MatnAsync(javob));
            (await XatoKodiAsync(javob)).ShouldBe("rol.teng_huquqli");
        }

        (await RolAsync(admin, hamkasb.Id)).Permissions.ShouldBe(ruxsatlar, ignoreOrder: true);

        // Qat'iy pastdagi rolni (roles.manage siz) esa tahrirlay oladi
        var past = await YaratAsync(admin, NoyobNom("Past"), [Permissions.EmployeesView]);
        using (var javob = await client.PutAsJsonAsync(
            RolUri(past.Id),
            RolTanasi(past.Name, Permissions.EmployeesView),
            bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        }

        // Super administrator uchun «teng» tushunchasi yo'q — u tizim egasi
        using (var javob = await admin.PutAsJsonAsync(
            RolUri(hamkasb.Id),
            RolTanasi(hamkasb.Name, Permissions.EmployeesView),
            bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        }
    }

    /// <summary>
    /// Kodda endi yo'q eski ruxsat kodi (<c>employment.manage</c>) hech narsa
    /// bermaydi — u hisoblarni boshqarish qoidasida ham hisobga olinmaydi.
    /// Aks holda rolda qolib ketgan bitta eski kod bosh rahbarni o'sha rol
    /// egalaridan jimgina uzib qo'yardi.
    /// </summary>
    [Fact]
    public async Task Eski_ruxsat_kodi_hisobni_boshqarishga_xalaqit_bermaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var admin = await AdminMijozAsync();
        var past = await YaratAsync(admin, NoyobNom("Eski kodli"), [Permissions.EmployeesView]);

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
            await kontekst.Database.ExecuteSqlRawAsync(
                "UPDATE roles SET permissions = permissions || ARRAY['employment.manage']::text[] WHERE id = {0}",
                [past.Id],
                bekorQilish);
        }

        var hisob = await TestAccounts.CreateAsync(_factory, Permissions.UsersManage, Permissions.EmployeesView);
        using var client = Mijoz(hisob.AccessToken);

        var qoldagi = await HisobYaratAsync(admin, past.Code);

        using (var javob = await client.PostAsJsonAsync(
            new Uri($"/api/users/{qoldagi}/reset-password", UriKind.Relative),
            new { newPassword = "Yangi_Parol_123" },
            bekorQilish))
        {
            ((int)javob.StatusCode).ShouldBeLessThan(300, await MatnAsync(javob));
        }
    }

    /// <summary>
    /// O'zidan YUQORI rolni (JORIY ruxsatlari kengroq) tahrirlab ham, o'chirib
    /// ham bo'lmaydi — faqat yangi ro'yxatni tekshirish yetmaydi: aks holda tor
    /// rol muharriri kengroq rolni «qisqartirib» yoki o'chirib yuborardi.
    /// </summary>
    [Fact]
    public async Task Ozidan_yuqori_rolni_tahrirlab_ham_ochirib_ham_bolmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var admin = await AdminMijozAsync();
        var yuqori = await YaratAsync(
            admin, NoyobNom("Yuqori"), [Permissions.EmployeesView, Permissions.EmployeesDelete]);

        var hisob = await TestAccounts.CreateAsync(_factory, Permissions.RolesManage, Permissions.EmployeesView);
        using var client = Mijoz(hisob.AccessToken);

        // Yangi ro'yxat o'z ichida bo'lsa ham — rolning HOZIRGI holati yuqori
        using (var javob = await client.PutAsJsonAsync(
            RolUri(yuqori.Id),
            RolTanasi(yuqori.Name, Permissions.EmployeesView),
            bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Conflict, await MatnAsync(javob));
            (await XatoKodiAsync(javob)).ShouldBe("rol.ortiqcha_ruxsat");
        }

        using (var javob = await client.DeleteAsync(RolUri(yuqori.Id), bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Conflict, await MatnAsync(javob));
            (await XatoKodiAsync(javob)).ShouldBe("rol.ortiqcha_ruxsat");
        }

        var ozgarmagan = await RolAsync(admin, yuqori.Id);
        ozgarmagan.Permissions.ShouldBe(
            [Permissions.EmployeesView, Permissions.EmployeesDelete], ignoreOrder: true);
    }

    /// <summary>
    /// O'zida yo'q ruxsatni boshqa rolga berib bo'lmaydi — yaratishda ham,
    /// tahrirlashda ham. O'zida borini esa bera oladi.
    /// </summary>
    [Fact]
    public async Task Ozida_yoq_ruxsatni_boshqa_rolga_berib_bolmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var hisob = await TestAccounts.CreateAsync(_factory, Permissions.RolesManage, Permissions.EmployeesView);

        using var client = Mijoz(hisob.AccessToken);

        using (var javob = await client.PostAsJsonAsync(
            RolesUri,
            RolTanasi(NoyobNom("Ortiqcha"), Permissions.EmployeesView, Permissions.EmployeesDelete),
            bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Conflict, await MatnAsync(javob));
            (await XatoKodiAsync(javob)).ShouldBe("rol.ortiqcha_ruxsat");
        }

        RoleResponse ozidanKuchsiz;
        using (var javob = await client.PostAsJsonAsync(
            RolesUri,
            RolTanasi(NoyobNom("Kuzatuvchi"), Permissions.EmployeesView),
            bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Created, await MatnAsync(javob));
            ozidanKuchsiz = (await javob.Content.ReadFromJsonAsync<RoleResponse>(bekorQilish))!;
        }

        using (var javob = await client.PutAsJsonAsync(
            RolUri(ozidanKuchsiz.Id),
            RolTanasi(ozidanKuchsiz.Name, Permissions.EmployeesView, Permissions.SettingsManage),
            bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Conflict, await MatnAsync(javob));
            (await XatoKodiAsync(javob)).ShouldBe("rol.ortiqcha_ruxsat");
        }
    }

    [Theory]
    [InlineData("xodimlar.hammasini_ochirish")]
    [InlineData("employment.manage")]
    public async Task Notanish_ruxsat_400_qaytaradi(string kod)
    {
        using var admin = await AdminMijozAsync();

        using var javob = await admin.PostAsJsonAsync(
            RolesUri,
            RolTanasi(NoyobNom("Notanish"), Permissions.EmployeesView, kod),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(javob));
        (await XatoKodiAsync(javob)).ShouldBe("rol.notanish_ruxsat");
    }

    [Fact]
    public async Task Nom_bosh_yoki_uzun_bolsa_400_qaytaradi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();

        foreach (var bosh in new[] { string.Empty, "   " })
        {
            using var javob = await admin.PostAsJsonAsync(RolesUri, RolTanasi(bosh, Permissions.EmployeesView), bekorQilish);
            javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await XatoKodiAsync(javob)).ShouldBe("rol.nom_bosh");
        }

        var chegarada = NoyobNom("N").PadRight(Role.MaxNameLength, 'n');

        using (var javob = await admin.PostAsJsonAsync(RolesUri, RolTanasi(chegarada + "n", Permissions.EmployeesView), bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await XatoKodiAsync(javob)).ShouldBe("rol.nom_uzun");
        }

        using (var javob = await admin.PostAsJsonAsync(RolesUri, RolTanasi(chegarada, Permissions.EmployeesView), bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Created, await MatnAsync(javob));
        }
    }

    /// <summary>
    /// Nom band — katta-kichik harf va chetdagi probellar farq qilmaydi;
    /// o'z nomini saqlab tahrirlash esa «band» emas.
    /// </summary>
    [Fact]
    public async Task Band_nom_409_qaytaradi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();

        using (var javob = await admin.PostAsJsonAsync(
            RolesUri,
            RolTanasi("  hr MENEJERI ", Permissions.EmployeesView),
            bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Conflict, await MatnAsync(javob));
            (await XatoKodiAsync(javob)).ShouldBe("rol.nom_band");
        }

        var birinchi = await YaratAsync(admin, NoyobNom("Birinchi"), [Permissions.EmployeesView]);
        var ikkinchi = await YaratAsync(admin, NoyobNom("Ikkinchi"), [Permissions.EmployeesView]);

        using (var javob = await admin.PutAsJsonAsync(
            RolUri(ikkinchi.Id),
            RolTanasi(" " + birinchi.Name.ToUpperInvariant(), Permissions.EmployeesView),
            bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Conflict, await MatnAsync(javob));
            (await XatoKodiAsync(javob)).ShouldBe("rol.nom_band");
        }

        using (var javob = await admin.PutAsJsonAsync(
            RolUri(ikkinchi.Id),
            RolTanasi(ikkinchi.Name, Permissions.EmployeesView, Permissions.EmployeesEdit),
            bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        }
    }

    // ────────────────────────────────────────────────────────────
    //  O'zgarish KEYINGI so'rovdayoq kuchga kiradi
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Rol tahriri — AYNAN O'SHA access token bilan keyingi so'rovda:
    /// olib tashlangan ruxsat 403, berilgan ruxsat 200.
    /// </summary>
    /// <remarks>
    /// Token qayta olinmaydi (refresh ham, login ham yo'q): egasining
    /// talabi — o'zgarish deploy va qayta kirishsiz, darhol kuchga kirsin.
    /// </remarks>
    [Fact]
    public async Task Rol_tahriri_osha_tokenning_keyingi_sorovida_kuchga_kiradi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var hisob = await TestAccounts.CreateAsync(_factory, Permissions.UsersManage);

        using var client = Mijoz(hisob.AccessToken);

        using (var javob = await client.GetAsync(UsersUri, bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, "boshlang'ich holat");
        }

        using (var javob = await client.GetAsync(CatalogUri, bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "boshlang'ich holat");
        }

        // Egasi rolni tahrirlaydi: foydalanuvchilar — olindi, rollar — berildi
        var rol = await RolAsync(admin, hisob.RoleId);
        using (var javob = await admin.PutAsJsonAsync(RolUri(hisob.RoleId), RolTanasi(rol.Name, Permissions.RolesManage), bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        }

        using (var javob = await client.GetAsync(UsersUri, bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "olib tashlangan ruxsat darhol yopilishi kerak");
        }

        using (var javob = await client.GetAsync(CatalogUri, bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, "berilgan ruxsat darhol ochilishi kerak");
        }
    }

    /// <summary>Hisobning ROLI almashtirilsa ham — o'sha token yangi rol bilan ishlaydi.</summary>
    [Fact]
    public async Task Hisob_roli_almashtirilsa_osha_token_yangi_rol_bilan_ishlaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var hisob = await TestAccounts.CreateAsync(_factory, Permissions.UsersManage);

        using var client = Mijoz(hisob.AccessToken);

        using (var javob = await client.GetAsync(new Uri("/api/employees", UriKind.Relative), bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "boshlang'ich holat");
        }

        await RolAlmashtirAsync(admin, hisob.UserId, SystemRoles.HrManager, hisob.Login);

        using (var javob = await client.GetAsync(new Uri("/api/employees", UriKind.Relative), bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        }

        using (var javob = await client.GetAsync(UsersUri, bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "HR menejerida users.manage yo'q");
        }

        using (var men = await client.GetAsync(new Uri("/api/auth/me", UriKind.Relative), bekorQilish))
        {
            var joriy = await men.Content.ReadFromJsonAsync<UserResponse>(bekorQilish);
            joriy!.Roles.ShouldBe([SystemRoles.HrManager]);
        }
    }

    /// <summary>
    /// Faolsizlantirilgan hisobning ALLAQACHON berilgan tokeni keyingi
    /// so'rovda 401 oladi; qayta faollashtirilsa — yana ishlaydi.
    /// </summary>
    [Fact]
    public async Task Faolsizlantirilgan_hisobning_tokeni_401_oladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var hisob = await TestAccounts.CreateAsync(_factory, Permissions.EmployeesView);

        using var client = Mijoz(hisob.AccessToken);
        var men = new Uri("/api/auth/me", UriKind.Relative);

        using (var javob = await client.GetAsync(men, bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, "boshlang'ich holat");
        }

        using (var javob = await admin.PostAsync(new Uri($"/api/users/{hisob.UserId}/deactivate", UriKind.Relative), null, bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(javob));
        }

        using (var javob = await client.GetAsync(men, bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            (await XatoKodiAsync(javob)).ShouldBe("auth.token_yaroqsiz");
        }

        using (var javob = await client.GetAsync(new Uri("/api/employees", UriKind.Relative), bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using (var javob = await admin.PostAsync(new Uri($"/api/users/{hisob.UserId}/activate", UriKind.Relative), null, bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(javob));
        }

        using (var javob = await client.GetAsync(men, bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, "qayta faollashtirilgan hisob darhol ishlashi kerak");
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    private static Uri RolUri(Guid id) => new($"/api/roles/{id}", UriKind.Relative);

    private static object RolTanasi(string nom, params string[] ruxsatlar) => new { name = nom, permissions = ruxsatlar };

    /// <summary>Umumiy bazada takrorlanmaydigan rol nomi.</summary>
    private static string NoyobNom(string asos)
        => asos + " " + Interlocked.Increment(ref _nomSanagichi).ToString(CultureInfo.InvariantCulture)
           + "-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..6];

    private static IReadOnlyList<string> Kodlar(JsonElement massiv)
        => [.. massiv.EnumerateArray().Select(element => element.GetString()!)];

    private async Task<HttpClient> MijozAsync(params string[] ruxsatlar)
        => Mijoz(await TestAccounts.TokenAsync(_factory, ruxsatlar));

    private HttpClient Mijoz(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
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

    private static async Task<List<RoleResponse>> RoyxatAsync(HttpClient client)
    {
        using var javob = await client.GetAsync(RolesUri, TestContext.Current.CancellationToken);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        return (await javob.Content.ReadFromJsonAsync<List<RoleResponse>>(TestContext.Current.CancellationToken))!;
    }

    private static async Task<RoleResponse> RolAsync(HttpClient client, Guid id)
        => (await RoyxatAsync(client)).Single(rol => rol.Id == id);

    private static async Task<RoleResponse> YaratAsync(HttpClient client, string nom, string[] ruxsatlar)
    {
        using var javob = await client.PostAsJsonAsync(RolesUri, RolTanasi(nom, ruxsatlar), TestContext.Current.CancellationToken);
        javob.StatusCode.ShouldBe(HttpStatusCode.Created, await MatnAsync(javob));

        return (await javob.Content.ReadFromJsonAsync<RoleResponse>(TestContext.Current.CancellationToken))!;
    }

    private async Task<Guid> RolIdAsync(string kod)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        return await kontekst.Roles
            .AsNoTracking()
            .Where(rol => rol.Code == kod)
            .Select(rol => rol.Id)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Foydalanuvchilar bo'limi orqali shu kodli rolda yangi hisob ochadi.</summary>
    private static async Task<Guid> HisobYaratAsync(HttpClient admin, string rolKodi)
    {
        using var javob = await admin.PostAsJsonAsync(
            UsersUri,
            new
            {
                login = "96" + Random.Shared.Next(1_000_000, 9_999_999).ToString(CultureInfo.InvariantCulture),
                lastName = "Ролов",
                firstName = "Синов",
                password = "Rol_Sinov_Paroli1",
                roleCode = rolKodi,
            },
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        return (await javob.Content.ReadFromJsonAsync<UserSummaryResponse>(TestContext.Current.CancellationToken))!.Id;
    }

    /// <summary>Hisobning rolini foydalanuvchilar bo'limi orqali almashtiradi.</summary>
    private static async Task RolAlmashtirAsync(HttpClient admin, Guid hisobId, string rolKodi, string? login = null)
    {
        using var hammasi = await admin.GetAsync(UsersUri, TestContext.Current.CancellationToken);
        var hisob = (await hammasi.Content.ReadFromJsonAsync<List<UserSummaryResponse>>(TestContext.Current.CancellationToken))!
            .Single(user => user.Id == hisobId);

        using var javob = await admin.PutAsJsonAsync(
            new Uri($"/api/users/{hisobId}", UriKind.Relative),
            new
            {
                login = login ?? hisob.Login,
                lastName = hisob.FullName.Split(' ')[0],
                firstName = hisob.FullName.Split(' ')[1],
                roleCode = rolKodi,
            },
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
    }

    private static async Task<string?> XatoKodiAsync(HttpResponseMessage javob)
    {
        var muammo = await javob.Content.ReadFromJsonAsync<ProblemResponse>(TestContext.Current.CancellationToken);
        return muammo?.Title;
    }

    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
