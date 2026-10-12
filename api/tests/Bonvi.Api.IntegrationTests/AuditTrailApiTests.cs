using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Organization;
using Bonvi.Domain.PublicApi;
using Bonvi.Domain.Settings;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// O'zgarishlar jurnalining uchi-uchiga testlari (SPEC-rollar-va-audit §12.1 «Integration — audit»).
/// </summary>
/// <remarks>
/// <para>
/// Haqiqiy PostgreSQL va haqiqiy interceptor ustida: yozuv bazaga tushadimi, eski
/// qiymat saqlanadimi, sir jurnalga sizmaydimi va jurnalni o'zgartirib bo'lmaydimi.
/// Mock bilan bularning hech biri tekshirilmaydi — trigger ham, unikal indeks ham,
/// <c>SaveChanges</c> ning haqiqiy tartibi ham faqat bazada bor.
/// </para>
/// <para>
/// Testlar bitta bazani baham ko'radi: har bir test o'z belgisi (<see cref="Belgi"/>)
/// bilan yozadi va jurnaldan faqat O'Z yozuvlarini qidiradi.
/// </para>
/// </remarks>
public sealed class AuditTrailApiTests : IClassFixture<BonviApiFactory>
{
    private const string AdminIsmi = "Test Administrator Testovich";

    private readonly BonviApiFactory _factory;

    public AuditTrailApiTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Belgi() => Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..8].ToUpperInvariant();

    // ────────────────────────────────────────────────────────────
    //  Yaratish, tahrir, o'chirish
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Har bir amal — AYNAN bitta qator: to'g'ri tur, maydonlar ro'yxati, eski va yangi qiymat.
    /// HTTP'siz qamrov (urug', vazifa) — bajaruvchi «tizim».
    /// </summary>
    [Fact]
    public async Task Yaratish_tahrir_yumshoq_va_haqiqiy_ochirish_bittadan_qator_yozadi()
    {
        var belgi = Belgi();
        var eskiNom = "Аудит йўналиш " + belgi;
        var yangiNom = "Янги йўналиш " + belgi;

        var yonalishId = await BazadaAsync(async db =>
        {
            var yonalish = Direction.Create(eskiNom).Value;
            db.Directions.Add(yonalish);
            await db.SaveChangesAsync(Ct);
            return yonalish.Id;
        });

        await BazadaAsync(async db =>
        {
            var yonalish = await db.Directions.SingleAsync(row => row.Id == yonalishId, Ct);
            yonalish.Rename(yangiNom).IsSuccess.ShouldBeTrue();
            await db.SaveChangesAsync(Ct);
            return 0;
        });

        // `Direction` — yumshoq o'chiriladi (`AuditableInterceptor`)
        await BazadaAsync(async db =>
        {
            db.Directions.Remove(await db.Directions.SingleAsync(row => row.Id == yonalishId, Ct));
            await db.SaveChangesAsync(Ct);
            return 0;
        });

        var yozuvlar = await JurnalAsync(log => log.EntityName == "Direction" && log.EntityId == yonalishId.ToString());

        yozuvlar.Select(log => log.Action).ShouldBe([AuditAction.Create, AuditAction.Update, AuditAction.Delete]);
        yozuvlar.ShouldAllBe(log => log.ActorKind == AuditActorKind.System && log.UserId == null);

        var yaratish = yozuvlar[0];
        yaratish.OldValues.ShouldBeNull();
        Matn(yaratish.NewValues, "Name").ShouldBe(eskiNom);
        yaratish.ChangedFields.ShouldNotBeNull().ShouldContain("Name");
        yaratish.ChangedFields.ShouldNotContain("CreatedAt", "kim/qachon — audit qatorining o'zida");
        yaratish.ChangedFields.ShouldNotContain("Id", "kalit entity_id da");
        yaratish.EntityLabel.ShouldBe(eskiNom);

        var tahrir = yozuvlar[1];
        tahrir.ChangedFields.ShouldBe(["Name"], "faqat haqiqatan o'zgargan maydon — UpdatedAt emas");
        Matn(tahrir.OldValues, "Name").ShouldBe(eskiNom);
        Matn(tahrir.NewValues, "Name").ShouldBe(yangiNom);
        tahrir.EntityLabel.ShouldBe(yangiNom);

        var ochirish = yozuvlar[2];
        ochirish.ChangedFields.ShouldBeNull();
        ochirish.NewValues.ShouldBeNull();
        Matn(ochirish.OldValues, "Name").ShouldBe(yangiNom, "o'chirishda oxirgi holat saqlanadi");

        // Haqiqiy (yumshoq emas) o'chirish — `Role` da o'chirilganlik belgisi yo'q
        var rolId = await BazadaAsync(async db =>
        {
            var rol = Role.Create("audit_" + belgi, "Аудит роли " + belgi).Value;
            db.Roles.Add(rol);
            await db.SaveChangesAsync(Ct);
            return rol.Id;
        });

        await BazadaAsync(async db =>
        {
            db.Roles.Remove(await db.Roles.SingleAsync(row => row.Id == rolId, Ct));
            await db.SaveChangesAsync(Ct);
            return 0;
        });

        var rolYozuvlari = await JurnalAsync(log => log.EntityName == "Role" && log.EntityId == rolId.ToString());

        rolYozuvlari.Select(log => log.Action).ShouldBe([AuditAction.Create, AuditAction.Delete]);
        Matn(rolYozuvlari[1].OldValues, "Code").ShouldBe("audit_" + belgi.ToLowerInvariant());
    }

    /// <summary>
    /// Kirish faqat «oxirgi kirish» va xato urinishlar sanog'ini yangilaydi — ular tashlab
    /// ketiladi, ya'ni hisob uchun tahrir qatori YO'Q (o'rniga bitta «Login» hodisasi).
    /// </summary>
    [Fact]
    public async Task Faqat_tashlab_ketilgan_maydonlar_ozgarsa_qator_yozilmaydi()
    {
        var hisob = await TestAccounts.CreateAsync(_factory);
        var hisobId = hisob.UserId.ToString();

        var oldin = await JurnalAsync(log => log.EntityName == "User" && log.EntityId == hisobId);

        _ = await KirishAsync(hisob.Login, TestAccounts.Password);

        var keyin = await JurnalAsync(log => log.EntityName == "User" && log.EntityId == hisobId);
        var yangilari = keyin.Where(log => oldin.TrueForAll(old => old.Id != log.Id)).ToList();

        yangilari.Select(log => log.Action).ShouldBe([AuditAction.Login]);

        // To'g'ridan-to'g'ri ham: faqat tashlab ketilgan maydonlar
        await BazadaAsync(async db =>
        {
            var user = await db.Users.SingleAsync(row => row.Id == hisob.UserId, Ct);
            user.RecordFailedLogin(DateTimeOffset.UtcNow);
            user.RecordSuccessfulLogin(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(Ct);
            return 0;
        });

        (await JurnalAsync(log => log.EntityName == "User" && log.EntityId == hisobId))
            .Count.ShouldBe(keyin.Count);
    }

    // ────────────────────────────────────────────────────────────
    //  Mehnat munosabatlari: guruh, muallif, tuzatishning eski qiymati
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Ko'chirish — mehnat munosabati tahriri + yangi buyruq: ikkala qator bitta guruhda,
    /// endpoint nomi bilan, xodimga bog'langan, bajaruvchining ismi va roli bilan.
    /// Buyruqning muallifi — so'rov qilgan foydalanuvchi (§7.9).
    /// </summary>
    [Fact]
    public async Task Kochirish_ikki_qatori_bitta_guruhda_va_buyruq_muallifi_yoziladi()
    {
        var belgi = Belgi();
        var urug = await XodimUrugiAsync(belgi);
        var (client, admin) = await AdminMijozAsync();
        using var _ = client;

        using (var javob = await client.PostAsJsonAsync(
                   new Uri($"/api/employments/{urug.MunosabatId}/transfer", UriKind.Relative),
                   new { effectiveDate = new DateOnly(2026, 3, 1), departmentId = urug.YangiBolimId, orderNo = belgi + "-K" },
                   Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        }

        var buyruq = await BazadaAsync(db => db.EmploymentEvents.AsNoTracking()
            .SingleAsync(row => row.EmploymentId == urug.MunosabatId && row.OrderNo == belgi + "-K", Ct));

        buyruq.RecordedBy.ShouldBe(admin.User.Id, "buyruqni kim kiritgani endi ma'lum");

        var buyruqQatori = (await JurnalAsync(log => log.EntityName == "EmploymentEvent" && log.EntityId == buyruq.Id.ToString()))
            .ShouldHaveSingleItem();

        buyruqQatori.CorrelationId.ShouldNotBeNull();

        var guruh = await JurnalAsync(log => log.CorrelationId == buyruqQatori.CorrelationId);

        string[] guruhNomlari = [.. guruh.Select(log => log.EntityName).Order(StringComparer.Ordinal)];
        guruhNomlari.ShouldBe(
            ["Employment", "EmploymentEvent"],
            customMessage: string.Join(" | ", guruh.Select(log => $"{log.EntityName} {log.Action} {string.Join(',', log.ChangedFields ?? [])} {log.NewValues}")));
        guruh.ShouldAllBe(log => log.Operation == "TransferEmployee");
        guruh.ShouldAllBe(log => log.SubjectEmployeeId == urug.XodimId);
        guruh.ShouldAllBe(log => log.EntityLabel == urug.XodimIsmi);
        guruh.ShouldAllBe(log => log.ActorKind == AuditActorKind.User && log.UserId == admin.User.Id);
        guruh.ShouldAllBe(log => log.ActorName == AdminIsmi);
        guruh.ShouldAllBe(log => !string.IsNullOrEmpty(log.ActorRole));

        var munosabatQatori = guruh.Single(log => log.EntityName == "Employment");
        munosabatQatori.Action.ShouldBe(AuditAction.Update);
        munosabatQatori.ChangedFields.ShouldNotBeNull().ShouldContain("DepartmentId");
        Matn(munosabatQatori.OldValues, "DepartmentId").ShouldBe(urug.EskiBolimId.ToString());
        Matn(munosabatQatori.NewValues, "DepartmentId").ShouldBe(urug.YangiBolimId.ToString());
    }

    /// <summary>
    /// «Yozuvni tuzatish» buyruqni USTIDAN yozadi (egasining 2026-09-16 qarori) — endi eski
    /// raqam, sana va oklad audit qatorida qoladi (SPEC §2.8 teshigi yopiladi).
    /// </summary>
    [Fact]
    public async Task Yozuvni_tuzatish_eski_qiymatlarni_jurnalda_saqlaydi()
    {
        var belgi = Belgi();
        var urug = await XodimUrugiAsync(belgi);
        var (client, _) = await AdminMijozAsync();
        using var mijoz = client;

        using (var javob = await client.PostAsJsonAsync(
                   new Uri($"/api/employments/{urug.MunosabatId}/transfer", UriKind.Relative),
                   new
                   {
                       effectiveDate = new DateOnly(2026, 3, 1),
                       departmentId = urug.YangiBolimId,
                       salaryAmount = 5_000_000m,
                       salaryCurrency = "UZS",
                       orderNo = belgi + "-K",
                   },
                   Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        }

        var buyruqId = await BazadaAsync(db => db.EmploymentEvents.AsNoTracking()
            .Where(row => row.EmploymentId == urug.MunosabatId && row.OrderNo == belgi + "-K")
            .Select(row => row.Id)
            .SingleAsync(Ct));

        using (var javob = await client.PutAsJsonAsync(
                   new Uri($"/api/employments/{urug.MunosabatId}/events/{buyruqId}", UriKind.Relative),
                   new { effectiveDate = new DateOnly(2026, 3, 5), orderNo = belgi + "-K2", amount = 5_500_000m },
                   Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        }

        var tuzatish = (await JurnalAsync(log =>
                log.EntityName == "EmploymentEvent" && log.EntityId == buyruqId.ToString() && log.Action == AuditAction.Update))
            .ShouldHaveSingleItem();

        tuzatish.Operation.ShouldBe("EditEmploymentEvent");
        tuzatish.SubjectEmployeeId.ShouldBe(urug.XodimId);

        Matn(tuzatish.OldValues, "OrderNo").ShouldBe(belgi + "-K");
        Matn(tuzatish.NewValues, "OrderNo").ShouldBe(belgi + "-K2");
        Matn(tuzatish.OldValues, "EffectiveDate").ShouldBe("2026-03-01");
        Matn(tuzatish.NewValues, "EffectiveDate").ShouldBe("2026-03-05");
        Son(tuzatish.OldValues, "ToSalary.Amount").ShouldBe(5_000_000m);
        Son(tuzatish.NewValues, "ToSalary.Amount").ShouldBe(5_500_000m);
    }

    // ────────────────────────────────────────────────────────────
    //  Xodim va yorliq
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Xodimga tegishli yozuv xodimga bog'lanadi — to'g'ridan-to'g'ri maydon, mehnat munosabati
    /// yoki qobiliyat profili orqali; kuzatuvdagi obyektdan ham, bazadan ham. Yorliq — xodimning
    /// O'SHA LAHZADAGI ismi: keyin qayta nomlansa, eski qatorlar eski ismda qoladi.
    /// </summary>
    [Fact]
    public async Task Xodim_va_yorliq_har_bir_bogliq_yozuvda_togri_va_lahzadagi_ism_bilan()
    {
        var belgi = Belgi();
        var urug = await XodimUrugiAsync(belgi);

        // Bazadan qidiruv: munosabat va xodim bu kontekstda kuzatilmaydi
        var (sinovId, belgiId, profilId) = await BazadaAsync(async db =>
        {
            var sinov = Probation.Start(urug.MunosabatId, new DateOnly(2026, 2, 1)).Value;
            var flag = EmployeeFlag.Raise(urug.XodimId, EmployeeFlagType.Reserve, new DateOnly(2026, 2, 1), "Аудит").Value;

            var profil = EmployeeCompetency.Create(urug.XodimId).Value;
            profil.SetLanguageSkills([new LanguageSkill(LanguageCode.Russian, LanguageLevel.Advanced)]).IsSuccess.ShouldBeTrue();

            db.AddRange(sinov, flag, profil);
            await db.SaveChangesAsync(Ct);
            return (sinov.Id, flag.Id, profil.Id);
        });

        // Til qatori o'chiriladi, profilning o'zi esa bu kontekstda YO'Q —
        // xodim faqat bazadagi profil orqali topiladi
        var tilId = await BazadaAsync(async db =>
        {
            var til = await db.EmployeeCompetencyLanguages.SingleAsync(row => row.EmployeeCompetencyId == profilId, Ct);
            db.Remove(til);
            await db.SaveChangesAsync(Ct);
            return til.Id;
        });

        string[] bogliq = [sinovId.ToString(), belgiId.ToString(), profilId.ToString(), tilId.ToString()];
        var yozuvlar = await JurnalAsync(log => log.EntityId != null && bogliq.Contains(log.EntityId));

        yozuvlar.Select(log => log.EntityName).Distinct().Order(StringComparer.Ordinal)
            .ShouldBe(["EmployeeCompetency", "EmployeeCompetencyLanguage", "EmployeeFlag", "Probation"]);
        yozuvlar.ShouldAllBe(log => log.SubjectEmployeeId == urug.XodimId);
        yozuvlar.ShouldAllBe(log => log.EntityLabel == urug.XodimIsmi);
        yozuvlar.ShouldContain(log => log.EntityName == "EmployeeCompetencyLanguage" && log.Action == AuditAction.Delete);

        // Urug'ning o'zi: xodim (o'zi), munosabat va ishga qabul buyrug'i (kuzatuvdan)
        var urugQatorlari = await JurnalAsync(log => log.SubjectEmployeeId == urug.XodimId && log.Action == AuditAction.Create);
        urugQatorlari.Select(log => log.EntityName).ShouldContain("Employee");
        urugQatorlari.Select(log => log.EntityName).ShouldContain("Employment");
        urugQatorlari.Select(log => log.EntityName).ShouldContain("EmploymentEvent");

        // Qayta nomlash — keyingi qator yangi ismda, oldingilari o'zgarmaydi
        var yangiIsm = FullName.Create("Янгиев", "Ходим", belgi).Value;

        await BazadaAsync(async db =>
        {
            var xodim = await db.Employees.SingleAsync(row => row.Id == urug.XodimId, Ct);
            xodim.Rename(yangiIsm).IsSuccess.ShouldBeTrue();
            db.Add(EmployeeFlag.Raise(urug.XodimId, EmployeeFlagType.Monitoring, new DateOnly(2026, 2, 2), "Кейинги").Value);
            await db.SaveChangesAsync(Ct);
            return 0;
        });

        var keyingi = (await JurnalAsync(log =>
                log.EntityName == "EmployeeFlag" && log.SubjectEmployeeId == urug.XodimId && log.EntityId != belgiId.ToString()))
            .ShouldHaveSingleItem();

        keyingi.EntityLabel.ShouldBe(yangiIsm.Full);
        (await JurnalAsync(log => log.EntityId == belgiId.ToString())).ShouldAllBe(log => log.EntityLabel == urug.XodimIsmi);
    }

    // ────────────────────────────────────────────────────────────
    //  Yashirish: sir hech qachon jurnalga tushmaydi
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Hisob yaratilganda parol ham, uning xeshi ham qatorda yo'q (xom matn bo'yicha qidiriladi);
    /// <c>PasswordHash</c> nomi qoladi, qiymati «***». Ilgari bu
    /// <c>CreateUserCommandHandlerTests.Muvaffaqiyatli_yaratishda_audit_yozuvi_qoyiladi</c> edi.
    /// </summary>
    [Fact]
    public async Task Foydalanuvchi_yaratilishi_parolsiz_va_xesh_yashirilgan_holda_yoziladi()
    {
        const string parol = "Audit_Parol_2026";
        var (client, _) = await AdminMijozAsync();
        using var mijoz = client;

        using var javob = await client.PostAsJsonAsync(
            new Uri("/api/users", UriKind.Relative),
            new { login = Telefon(), lastName = "Аудитов", firstName = "Хисоб", password = parol, roleCode = SystemRoles.HrManager },
            Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        var yaratilgan = (await javob.Content.ReadFromJsonAsync<UserSummaryResponse>(Ct)).ShouldNotBeNull();

        var qator = (await JurnalAsync(log => log.EntityName == "User" && log.EntityId == yaratilgan.Id.ToString()))
            .ShouldHaveSingleItem();

        qator.Action.ShouldBe(AuditAction.Create);
        qator.Operation.ShouldBe("CreateUser");
        qator.ChangedFields.ShouldNotBeNull().ShouldContain("PasswordHash");
        Matn(qator.NewValues, "PasswordHash").ShouldBe("***");

        var xesh = (string)(await SqlAsync("SELECT password_hash FROM users WHERE id = $1;", yaratilgan.Id))!;
        var xom = await XomQatorlarAsync(yaratilgan.Id.ToString());

        xom.ShouldNotContain(parol);
        xom.ShouldNotContain(xesh);
    }

    /// <summary>
    /// Parol tiklash — «parol almashtirildi» fakti va amal nomi, qiymat emas. Ilgari bu
    /// <c>ResetUserPasswordCommandHandlerTests.Parol_audit_yozuviga_hech_qachon_yozilmaydi</c> edi.
    /// </summary>
    [Fact]
    public async Task Parol_tiklash_xeshni_yashiradi_va_amal_nomini_yozadi()
    {
        const string yangiParol = "Yangi_Audit_Parol_1";
        var hisob = await TestAccounts.CreateAsync(_factory);
        var eskiXesh = (string)(await SqlAsync("SELECT password_hash FROM users WHERE id = $1;", hisob.UserId))!;

        var (client, _) = await AdminMijozAsync();
        using var mijoz = client;

        using (var javob = await client.PostAsJsonAsync(
                   new Uri($"/api/users/{hisob.UserId}/reset-password", UriKind.Relative),
                   new { newPassword = yangiParol },
                   Ct))
        {
            javob.IsSuccessStatusCode.ShouldBeTrue(await MatnAsync(javob));
        }

        var qator = (await JurnalAsync(log =>
                log.EntityName == "User" && log.EntityId == hisob.UserId.ToString() && log.Action == AuditAction.Update))
            .ShouldHaveSingleItem();

        qator.Operation.ShouldBe("ResetUserPassword");
        qator.ChangedFields.ShouldNotBeNull().ShouldContain("PasswordHash");
        Matn(qator.OldValues, "PasswordHash").ShouldBe("***");
        Matn(qator.NewValues, "PasswordHash").ShouldBe("***");

        var yangiXesh = (string)(await SqlAsync("SELECT password_hash FROM users WHERE id = $1;", hisob.UserId))!;
        var xom = await XomQatorlarAsync(hisob.UserId.ToString());

        xom.ShouldNotContain(yangiParol);
        xom.ShouldNotContain(eskiXesh);
        xom.ShouldNotContain(yangiXesh);
    }

    /// <summary>Maxfiy sozlama (bot tokeni) — na ochiq matni, na shifrlangan ko'rinishi.</summary>
    [Fact]
    public async Task Maxfiy_sozlama_qiymati_jurnalga_tushmaydi()
    {
        var token = "12345678:" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        var (client, _) = await AdminMijozAsync();
        using var mijoz = client;

        using (var javob = await client.PutAsJsonAsync(
                   new Uri("/api/settings", UriKind.Relative),
                   new { items = new[] { new { key = SystemSettingKeys.BotToken, value = token } } },
                   Ct))
        {
            javob.IsSuccessStatusCode.ShouldBeTrue(await MatnAsync(javob));
        }

        var sozlamaId = (Guid)(await SqlAsync("SELECT id FROM system_settings WHERE key = $1;", SystemSettingKeys.BotToken))!;
        var saqlangan = (string)(await SqlAsync("SELECT value FROM system_settings WHERE key = $1;", SystemSettingKeys.BotToken))!;

        var qatorlar = await JurnalAsync(log => log.EntityName == "SystemSetting" && log.EntityId == sozlamaId.ToString());
        qatorlar.ShouldNotBeEmpty();
        qatorlar.ShouldAllBe(log => log.EntityLabel == SystemSettingKeys.BotToken);
        Matn(qatorlar[^1].NewValues, "Value").ShouldBe("***");

        var xom = await XomQatorlarAsync(sozlamaId.ToString());
        xom.ShouldNotContain(token);
        xom.ShouldNotContain(saqlangan);
    }

    /// <summary>Tashqi API kaliti — xeshi yashirin, ochiq kalit esa hech qayerda.</summary>
    [Fact]
    public async Task Api_kalit_xeshi_jurnalga_tushmaydi()
    {
        var sir = ApiKeyFormat.Generate();

        var kalitId = await BazadaAsync(async db =>
        {
            var kalit = ApiKey.Issue("Аудит калити " + Belgi(), sir).Value;
            db.Add(kalit);
            await db.SaveChangesAsync(Ct);
            return kalit.Id;
        });

        var qator = (await JurnalAsync(log => log.EntityName == "ApiKey" && log.EntityId == kalitId.ToString()))
            .ShouldHaveSingleItem();

        Matn(qator.NewValues, "KeyHash").ShouldBe("***");

        var xom = await XomQatorlarAsync(kalitId.ToString());
        xom.ShouldNotContain(sir.PlainKey);
        xom.ShouldNotContain(Convert.ToHexString(sir.Hash), Case.Insensitive);
        xom.ShouldNotContain(Convert.ToBase64String(sir.Hash));
    }

    // ────────────────────────────────────────────────────────────
    //  Muvaffaqiyatsiz saqlash
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Yiqilgan saqlash (unikal indeks) jurnalda iz qoldirmaydi va kuzatuvda ham audit qatori
    /// qolmaydi; keyingi muvaffaqiyatli saqlash faqat O'Z qatorini yozadi.
    /// </summary>
    [Fact]
    public async Task Yiqilgan_saqlash_iz_qoldirmaydi_keyingisi_faqat_ozini_yozadi()
    {
        var belgi = Belgi();
        var takrorNomi = "Такрор " + belgi;

        var yangiId = await BazadaAsync(async db =>
        {
            var takror = Role.Create(SystemRoles.SuperAdmin, takrorNomi).Value;
            db.Roles.Add(takror);

            await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));

            db.ChangeTracker.Entries<AuditLog>().ShouldBeEmpty("yiqilgan saqlashning qatorlari kuzatuvdan chiqishi kerak");

            db.Entry(takror).State = EntityState.Detached;

            var yangi = Direction.Create("Муваффақиятли " + belgi).Value;
            db.Directions.Add(yangi);
            await db.SaveChangesAsync(Ct);

            db.ChangeTracker.Entries<AuditLog>().ShouldBeEmpty("saqlangan qatorlar ham kuzatuvda qolmaydi");
            return yangi.Id;
        });

        (await JurnalAsync(log => log.EntityLabel == takrorNomi)).ShouldBeEmpty();

        var qator = (await JurnalAsync(log => log.EntityId == yangiId.ToString())).ShouldHaveSingleItem();
        (await JurnalAsync(log => log.CorrelationId == qator.CorrelationId))
            .ShouldHaveSingleItem("ikkinchi saqlash birinchisining qatorini olib ketmasligi kerak");
    }

    // ────────────────────────────────────────────────────────────
    //  Xavfsizlik hodisalari
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Kirish — bajaruvchining ismi, roli va amal nomi bilan. (IP ni <c>TestServer</c> bermaydi —
    /// u <c>HttpRequestContextTests</c> da tekshiriladi.)
    /// </summary>
    [Fact]
    public async Task Muvaffaqiyatli_kirish_Login_hodisasini_yozadi()
    {
        var hisob = await TestAccounts.CreateAsync(_factory);

        _ = await KirishAsync(hisob.Login, TestAccounts.Password);

        var qator = (await JurnalAsync(log => log.Action == AuditAction.Login && log.UserId == hisob.UserId))
            .ShouldHaveSingleItem();

        qator.ActorKind.ShouldBe(AuditActorKind.User);
        qator.EntityName.ShouldBe("User");
        qator.EntityId.ShouldBe(hisob.UserId.ToString());
        qator.UserLogin.ShouldBe("+998" + hisob.Login);
        qator.ActorName.ShouldNotBeNullOrWhiteSpace();
        qator.ActorRole.ShouldNotBeNullOrWhiteSpace();
        qator.Operation.ShouldBe("Login");
    }

    /// <summary>Xato parol — sababi bilan; kiritilgan parol qatorda yo'q.</summary>
    [Fact]
    public async Task Xato_parol_LoginFailed_va_parolsiz_yoziladi()
    {
        const string xatoParol = "Xato_Parol_Audit_99";
        var hisob = await TestAccounts.CreateAsync(_factory);

        using var client = _factory.CreateClient();
        using var javob = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new { login = hisob.Login, password = xatoParol },
            Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var qator = (await JurnalAsync(log => log.Action == AuditAction.LoginFailed && log.UserId == hisob.UserId))
            .ShouldHaveSingleItem();

        Matn(qator.NewValues, "reason").ShouldBe("credentials");
        qator.ActorKind.ShouldBe(AuditActorKind.User);

        (await XomQatorlarAsync(hisob.UserId.ToString())).ShouldNotContain(xatoParol);
    }

    /// <summary>Noma'lum login — bajaruvchi «anonim», login esa kiritilgani (normallashgan).</summary>
    /// <summary>
    /// Bir xil xato urinish (IP + login + sabab) 10 daqiqada bitta qator — jurnal parol tanlash
    /// bilan shishmasin; boshqa login esa alohida yoziladi.
    /// </summary>
    [Fact]
    public async Task Takroriy_xato_kirish_oynada_bir_marta_yoziladi()
    {
        var birinchi = "90" + Random.Shared.Next(1_000_000, 9_999_999).ToString(CultureInfo.InvariantCulture);
        var ikkinchi = "91" + Random.Shared.Next(1_000_000, 9_999_999).ToString(CultureInfo.InvariantCulture);

        using var client = _factory.CreateClient();

        foreach (var login in new[] { birinchi, birinchi, birinchi, ikkinchi })
        {
            using var javob = await client.PostAsJsonAsync(
                new Uri("/api/auth/login", UriKind.Relative),
                new { login, password = "Taxmin_123" },
                Ct);
            javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        (await JurnalAsync(log => log.Action == AuditAction.LoginFailed && log.UserLogin == "+998" + birinchi)).Count.ShouldBe(1);
        (await JurnalAsync(log => log.Action == AuditAction.LoginFailed && log.UserLogin == "+998" + ikkinchi)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Notanish_login_anonim_LoginFailed_yozadi()
    {
        var notanish = Telefon();

        using var client = _factory.CreateClient();
        using var javob = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new { login = notanish, password = "Har_qanday_1" },
            Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var qator = (await JurnalAsync(log => log.Action == AuditAction.LoginFailed && log.UserLogin == "+998" + notanish))
            .ShouldHaveSingleItem();

        qator.ActorKind.ShouldBe(AuditActorKind.Anonymous);
        qator.UserId.ShouldBeNull();
        qator.EntityId.ShouldBeNull();
    }

    /// <summary>Chiqish — tokenning egasi nomidan.</summary>
    [Fact]
    public async Task Chiqish_Logout_hodisasini_yozadi()
    {
        var hisob = await TestAccounts.CreateAsync(_factory);
        var auth = await KirishAsync(hisob.Login, TestAccounts.Password);

        using var client = _factory.CreateClient();
        using var javob = await client.PostAsJsonAsync(
            new Uri("/api/auth/logout", UriKind.Relative),
            new { refreshToken = auth.RefreshToken, allSessions = false },
            Ct);

        javob.IsSuccessStatusCode.ShouldBeTrue(await MatnAsync(javob));

        var qator = (await JurnalAsync(log => log.Action == AuditAction.Logout && log.UserId == hisob.UserId))
            .ShouldHaveSingleItem();

        Bayroq(qator.NewValues, "allSessions").ShouldBeFalse();
        qator.ActorKind.ShouldBe(AuditActorKind.User);
    }

    /// <summary>Bekor qilingan tokenni qayta ishlatish — o'g'irlik belgisi.</summary>
    [Fact]
    public async Task Tokenni_qayta_ishlatish_TokenReuse_yozadi()
    {
        var hisob = await TestAccounts.CreateAsync(_factory);
        var auth = await KirishAsync(hisob.Login, TestAccounts.Password);

        using var client = _factory.CreateClient();

        using (var birinchi = await client.PostAsJsonAsync(
                   new Uri("/api/auth/refresh", UriKind.Relative), new { refreshToken = auth.RefreshToken }, Ct))
        {
            birinchi.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(birinchi));
        }

        using (var ikkinchi = await client.PostAsJsonAsync(
                   new Uri("/api/auth/refresh", UriKind.Relative), new { refreshToken = auth.RefreshToken }, Ct))
        {
            ikkinchi.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        (await JurnalAsync(log => log.Action == AuditAction.TokenReuse && log.UserId == hisob.UserId))
            .ShouldHaveSingleItem()
            .ActorKind.ShouldBe(AuditActorKind.User);
    }

    /// <summary>
    /// Siyosat rad etgan so'rov — usul va marshrut NAQSHI; so'rov satri (qidiruv matni) yo'q.
    /// 403 javobining o'zi o'zgarmaydi.
    /// </summary>
    [Fact]
    public async Task Ruxsat_rad_etilishi_PermissionDenied_va_sorov_satrisiz_yoziladi()
    {
        var hisob = await TestAccounts.CreateAsync(_factory);

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hisob.AccessToken);

        using var javob = await client.GetAsync(new Uri("/api/users?search=MaxfiyQidiruv", UriKind.Relative), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await MatnAsync(javob)).ShouldContain("auth.ruxsat_yoq");

        var qator = (await JurnalAsync(log => log.Action == AuditAction.PermissionDenied && log.UserId == hisob.UserId))
            .ShouldHaveSingleItem();

        qator.EntityName.ShouldBe("Endpoint");
        Matn(qator.NewValues, "method").ShouldBe("GET");
        Matn(qator.NewValues, "path").ShouldBe("/api/users");
        qator.NewValues.ShouldNotBeNull().ShouldNotContain("MaxfiyQidiruv");
        qator.ActorKind.ShouldBe(AuditActorKind.User);
    }

    /// <summary>
    /// Bir xil rad etilish (hisob + usul + marshrut) qisqa oynada qayta yozilmaydi — yangilanadigan
    /// sahifa jurnalni shishirmasin; boshqa marshrut esa alohida yoziladi.
    /// </summary>
    [Fact]
    public async Task Takroriy_rad_etilish_oynada_bir_marta_yoziladi()
    {
        var hisob = await TestAccounts.CreateAsync(_factory);

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hisob.AccessToken);

        for (var i = 0; i < 3; i++)
        {
            using var takror = await client.GetAsync(new Uri("/api/users", UriKind.Relative), Ct);
            takror.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using var boshqa = await client.GetAsync(new Uri("/api/roles", UriKind.Relative), Ct);
        boshqa.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var qatorlar = await JurnalAsync(log => log.Action == AuditAction.PermissionDenied && log.UserId == hisob.UserId);

        qatorlar.Count.ShouldBe(2);
        qatorlar.ShouldContain(log => Matn(log.NewValues, "path") == "/api/users");
        qatorlar.ShouldContain(log => Matn(log.NewValues, "path") == "/api/roles");
    }

    /// <summary>Excel eksporti — filtr va qatorlar soni; qidiruv matni (ism) yozilmaydi.</summary>
    [Fact]
    public async Task Eksport_filtr_va_sanoq_bilan_shaxsiy_malumotsiz_yoziladi()
    {
        var (client, admin) = await AdminMijozAsync();
        using var mijoz = client;

        using var javob = await client.GetAsync(
            new Uri("/api/reports/employees/export?search=" + Uri.EscapeDataString("Шахсийисм") + "&status=active", UriKind.Relative),
            Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var qator = (await JurnalAsync(log => log.Action == AuditAction.Export && log.UserId == admin.User.Id && log.EntityName == "Employee"))
            .ShouldHaveSingleItem();

        qator.Operation.ShouldBe("ExportEmployeesToExcel");
        qator.NewValues.ShouldNotBeNull();
        qator.NewValues.ShouldNotContain("Шахсийисм");

        using var tafsilot = JsonDocument.Parse(qator.NewValues);
        tafsilot.RootElement.GetProperty("rows").GetInt32().ShouldBe(0);
        tafsilot.RootElement.GetProperty("filters").GetProperty("hasSearch").GetBoolean().ShouldBeTrue();
        tafsilot.RootElement.GetProperty("filters").GetProperty("status").GetString().ShouldBe("active");
    }

    // ────────────────────────────────────────────────────────────
    //  O'zgarmaslik
    // ────────────────────────────────────────────────────────────

    /// <summary>Ilova darajasida: kuzatuvdagi audit qatorini o'zgartirish yoki o'chirish — istisno.</summary>
    [Fact]
    public async Task Audit_qatorini_EF_orqali_ozgartirib_ham_ochirib_ham_bolmaydi()
    {
        var hisob = await TestAccounts.CreateAsync(_factory);
        _ = await KirishAsync(hisob.Login, TestAccounts.Password);

        await BazadaAsync(async db =>
        {
            var qator = await db.AuditLogs.FirstAsync(log => log.UserId == hisob.UserId, Ct);
            db.Entry(qator).Property(nameof(AuditLog.EntityName)).CurrentValue = "Soxta";

            var tahrir = await Should.ThrowAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
            tahrir.Message.ShouldContain("audit_logs");

            db.Entry(qator).State = EntityState.Unchanged;
            db.AuditLogs.Remove(qator);

            var ochirish = await Should.ThrowAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
            ochirish.Message.ShouldContain("audit_logs");
            return 0;
        });
    }

    /// <summary>Baza darajasida: xom <c>UPDATE</c>, <c>DELETE</c>, <c>TRUNCATE</c> — trigger xabari bilan rad.</summary>
    [Theory]
    [InlineData("UPDATE audit_logs SET entity_name = 'Soxta';")]
    [InlineData("DELETE FROM audit_logs;")]
    [InlineData("TRUNCATE audit_logs;")]
    public async Task Xom_SQL_bilan_jurnalni_ozgartirib_bolmaydi(string sql)
    {
        // Jadval bo'sh bo'lmasin — qator triggerlari faqat qator bo'lsa ishga tushadi
        var hisob = await TestAccounts.CreateAsync(_factory);
        _ = await KirishAsync(hisob.Login, TestAccounts.Password);

        var oldin = (long)(await SqlAsync("SELECT count(*) FROM audit_logs;"))!;

        var xato = await Should.ThrowAsync<PostgresException>(() => SqlAsync(sql));
        xato.MessageText.ShouldContain("audit_logs: yozuvlar o'zgartirilmaydi va o'chirilmaydi");

        ((long)(await SqlAsync("SELECT count(*) FROM audit_logs;"))!).ShouldBe(oldin);
    }

    // ════════════════════════════════════════════════════════════
    //  Yordamchilar
    // ════════════════════════════════════════════════════════════

    private sealed record XodimUrugi(
        Guid XodimId,
        string XodimIsmi,
        Guid MunosabatId,
        Guid EskiBolimId,
        Guid YangiBolimId);

    /// <summary>Xodim, ikki bo'lim va mehnat munosabati — HTTP'siz, to'g'ridan-to'g'ri bazaga.</summary>
    private async Task<XodimUrugi> XodimUrugiAsync(string belgi)
    {
        return await BazadaAsync(async db =>
        {
            var firma = LegalEntity.Create("Аудит МЧЖ " + belgi).Value;
            var eski = Department.Create("Эски бўлим " + belgi).Value;
            var yangi = Department.Create("Янги бўлим " + belgi).Value;
            var lavozim = Position.Create("Аудит лавозими " + belgi).Value;
            var ism = FullName.Create("Аудитов", "Ходим", belgi).Value;
            var xodim = Employee.Create(ism).Value;

            var munosabat = Employment.Hire(
                xodim.Id, firma.Id, new DateOnly(2025, 1, 15), eski.Id, lavozim.Id, orderNo: belgi + "-1").Value;

            db.AddRange(firma, eski, yangi, lavozim, xodim);
            db.Add(munosabat);
            await db.SaveChangesAsync(Ct);

            return new XodimUrugi(xodim.Id, ism.Full, munosabat.Id, eski.Id, yangi.Id);
        });
    }

    private async Task<T> BazadaAsync<T>(Func<AppDbContext, Task<T>> ish)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        return await ish(qamrov.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>Jurnal qatorlari — vaqt bo'yicha (Guid v7 — bir lahzadagilarni ham tartiblaydi).</summary>
    private Task<List<AuditLog>> JurnalAsync(System.Linq.Expressions.Expression<Func<AuditLog, bool>> shart)
        => BazadaAsync(db => db.AuditLogs.AsNoTracking()
            .Where(shart)
            .OrderBy(log => log.OccurredAt)
            .ThenBy(log => log.Id)
            .ToListAsync(Ct));

    /// <summary>Shu obyektning BARCHA jurnal qatorlari — xom matn (sir izlash uchun).</summary>
    private async Task<string> XomQatorlarAsync(string entityId)
        => (string?)await SqlAsync("SELECT coalesce(string_agg(a::text, ' '), '') FROM audit_logs a WHERE a.entity_id = $1;", entityId)
           ?? string.Empty;

    private async Task<object?> SqlAsync(string sql, params object[] qiymatlar)
    {
        await using var ulanish = new NpgsqlConnection(_factory.ConnectionString);
        await ulanish.OpenAsync(Ct);

        await using var buyruq = ulanish.CreateCommand();
        buyruq.CommandText = sql;

        foreach (var qiymat in qiymatlar)
        {
            _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = qiymat });
        }

        var natija = await buyruq.ExecuteScalarAsync(Ct);
        return natija is DBNull ? null : natija;
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

    private async Task<(HttpClient Client, AuthResponse Auth)> AdminMijozAsync()
    {
        var auth = await KirishAsync(BonviApiFactory.TestAdminLogin, BonviApiFactory.TestAdminPassword);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        return (client, auth);
    }

    /// <summary>Bazada bo'lmagan telefon — 93 bilan boshlanadi (sinov hisoblari boshqa prefiksda).</summary>
    private static string Telefon()
        => "93" + Random.Shared.Next(1_000_000, 9_999_999).ToString(CultureInfo.InvariantCulture);

    private static string? Matn(string? json, string maydon)
    {
        json.ShouldNotBeNull();
        using var hujjat = JsonDocument.Parse(json);
        return hujjat.RootElement.GetProperty(maydon).GetString();
    }

    private static decimal Son(string? json, string maydon)
    {
        json.ShouldNotBeNull();
        using var hujjat = JsonDocument.Parse(json);
        return hujjat.RootElement.GetProperty(maydon).GetDecimal();
    }

    private static bool Bayroq(string? json, string maydon)
    {
        json.ShouldNotBeNull();
        using var hujjat = JsonDocument.Parse(json);
        return hujjat.RootElement.GetProperty(maydon).GetBoolean();
    }

    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(Ct);
}
