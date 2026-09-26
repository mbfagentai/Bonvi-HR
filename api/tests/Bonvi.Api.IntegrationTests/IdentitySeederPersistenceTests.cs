using Bonvi.Domain.Identity;
using Bonvi.Infrastructure.Persistence;
using Bonvi.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// <see cref="IdentitySeeder"/> ning haqiqiy PostgreSQL ustidagi testlari.
/// </summary>
/// <remarks>
/// <para>
/// <b>2026-09-26 dan seeder ODDIY rollarga tegmaydi.</b> Ilgari u har
/// ishga tushishda barcha tizim rollarini koddagi ro'yxatga «to'g'rilardi»
/// va shu fayldagi testlar aynan shuni qadab turardi. Endi rollarni egasi
/// o'zi belgilaydi, shu sababli testlar TESKARI narsani isbotlaydi:
/// tahrirlangan «HR menejeri» va «Bosh rahbar» qayta ishga tushishda
/// tiklanmaydi, super administrator va bot roli esa hamon koddagi ta'rifga
/// tenglanadi.
/// </para>
/// <para>
/// Testlar bitta bazani baham ko'radi va ketma-ket bajariladi. Seeder
/// oddiy rolni endi o'zi tiklamagani uchun, uni o'zgartirgan test asl
/// ro'yxatni O'ZI qaytaradi (<see cref="TiklaAsync"/>) — aks holda keyingi
/// testlar tartibga bog'lanib qolardi. Rol jadvalini butunlay buzadigan
/// testlar esa o'z bazasida ishlaydi.
/// </para>
/// </remarks>
public sealed class IdentitySeederPersistenceTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public IdentitySeederPersistenceTests(BonviApiFactory factory) => _factory = factory;

    /// <summary>
    /// Egasi HR menejeridan olib tashlagan ruxsat qayta ishga tushishda
    /// QAYTIB KELMAYDI — seeder oddiy rolni «to'g'rilamaydi».
    /// </summary>
    [Fact]
    public async Task Tahrirlangan_hr_menejer_roli_qayta_ishga_tushishda_tiklanmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var rol = await kontekst.Roles.SingleAsync(r => r.Code == SystemRoles.HrManager, bekorQilish);
        var asli = rol.Permissions.ToArray();

        try
        {
            // Egasi muharrirda: oklad va bo'shatishni olib tashladi,
            // foydalanuvchi boshqaruvini esa qo'shdi.
            rol.ReplacePermissions(asli
                    .Where(p => p != Permissions.EmploymentSalary && p != Permissions.EmploymentTerminate)
                    .Append(Permissions.UsersManage))
                .IsSuccess.ShouldBeTrue();
            await kontekst.SaveChangesAsync(bekorQilish);

            var tahrirlangan = rol.Permissions.ToArray();

            // Ilova qayta ishga tushgandek — seeder yana chaqiriladi.
            await new IdentitySeeder(kontekst).SeedAsync(bekorQilish);

            kontekst.ChangeTracker.Clear();
            var keyin = await kontekst.Roles.AsNoTracking()
                .SingleAsync(r => r.Code == SystemRoles.HrManager, bekorQilish);

            keyin.Permissions.ShouldBe(tahrirlangan, ignoreOrder: true);
            keyin.Has(Permissions.EmploymentSalary).ShouldBeFalse("egasi olib tashlagan ruxsat qaytmasligi kerak");
            keyin.Has(Permissions.UsersManage).ShouldBeTrue("egasi bergan ruxsat olinmasligi kerak");
        }
        finally
        {
            await TiklaAsync(SystemRoles.HrManager, asli);
        }
    }

    /// <summary>Bosh rahbar ham endi oddiy rol — tahriri saqlanib qoladi.</summary>
    [Fact]
    public async Task Tahrirlangan_bosh_rahbar_roli_qayta_ishga_tushishda_tiklanmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var rol = await kontekst.Roles.SingleAsync(r => r.Code == SystemRoles.MainBoss, bekorQilish);
        var asli = rol.Permissions.ToArray();

        try
        {
            rol.ReplacePermissions([Permissions.EmployeesView, Permissions.UsersManage]).IsSuccess.ShouldBeTrue();
            rol.Rename("Direktor").IsSuccess.ShouldBeTrue();
            await kontekst.SaveChangesAsync(bekorQilish);

            await new IdentitySeeder(kontekst).SeedAsync(bekorQilish);

            kontekst.ChangeTracker.Clear();
            var keyin = await kontekst.Roles.AsNoTracking()
                .SingleAsync(r => r.Code == SystemRoles.MainBoss, bekorQilish);

            keyin.Permissions.ShouldBe([Permissions.EmployeesView, Permissions.UsersManage], ignoreOrder: true);
            keyin.Name.ShouldBe("Direktor");
        }
        finally
        {
            await TiklaAsync(SystemRoles.MainBoss, asli, SystemRoles.Defaults[SystemRoles.MainBoss].Name);
        }
    }

    /// <summary>
    /// Super administrator esa HAR DOIM <see cref="Permissions.All"/> ga
    /// tenglanadi — kodga qo'shilgan yangi ruxsat tizim egasiga o'zi yetadi.
    /// </summary>
    [Fact]
    public async Task Super_administrator_har_doim_barcha_ruxsatlarga_tenglanadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        // Domen qoidasi super administratordan ruxsat olib tashlashni
        // taqiqlaydi — «eski bazani» (yangi ruxsatlar hali yetib bormagan)
        // qayta hosil qilish uchun to'g'ridan-to'g'ri SQL bilan buzamiz.
        await kontekst.Database.ExecuteSqlRawAsync(
            "UPDATE roles SET permissions = ARRAY['employees.view']::text[] WHERE code = {0}",
            [SystemRoles.SuperAdmin],
            bekorQilish);

        kontekst.ChangeTracker.Clear();

        await new IdentitySeeder(kontekst).SeedAsync(bekorQilish);

        var yangilangan = await kontekst.Roles
            .AsNoTracking()
            .SingleAsync(r => r.Code == SystemRoles.SuperAdmin, bekorQilish);

        yangilangan.Permissions.ShouldBe(Permissions.All, ignoreOrder: true);
    }

    /// <summary>
    /// Bot roli ham koddagi QAT'IY ro'yxatiga tenglanadi: qo'lda qo'shilgan
    /// ortiqcha ruxsat keyingi ishga tushishda olib tashlanadi.
    /// </summary>
    [Fact]
    public async Task Bot_roli_har_doim_qatiy_royxatga_tenglanadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        await kontekst.Database.ExecuteSqlRawAsync(
            "UPDATE roles SET permissions = permissions || ARRAY['employees.delete']::text[] WHERE code = {0}",
            [SystemRoles.RecruitingBot],
            bekorQilish);

        kontekst.ChangeTracker.Clear();

        await new IdentitySeeder(kontekst).SeedAsync(bekorQilish);

        var bot = await kontekst.Roles
            .AsNoTracking()
            .SingleAsync(r => r.Code == SystemRoles.RecruitingBot, bekorQilish);

        bot.Permissions.ShouldBe(SystemRoles.Defaults[SystemRoles.RecruitingBot].Permissions, ignoreOrder: true);
    }

    /// <summary>
    /// Eski <c>employment.manage</c> — deploy'da HAR BIR rolda sakkizta amal
    /// kodiga almashadi: hech kim bironta huquqini yo'qotmaydi.
    /// </summary>
    /// <remarks>
    /// O'z bazasida: rollarning ruxsatlari «eski bazadagi» holatga
    /// qaytariladi va umumiy bazada bu boshqa testlarga ta'sir qilardi.
    /// </remarks>
    [Fact]
    public async Task Eski_mehnat_ruxsati_sakkizta_amalga_almashadi_va_takrorlansa_ozgarmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var yakka = new BonviApiFactory();
        await yakka.InitializeAsync();
        using (var _ = yakka.CreateClient()) { }

        await using var qamrov = yakka.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        // Egasi (hali eski modelda) yaratgan maxsus rol ham shu ko'chirishga tushishi kerak.
        var maxsus = Role.Create("custom_eski0001", "Kadrlar (eski)").Value;
        maxsus.ReplacePermissions([Permissions.EmployeesView, Permissions.EmploymentView]).IsSuccess.ShouldBeTrue();
        kontekst.Roles.Add(maxsus);
        await kontekst.SaveChangesAsync(bekorQilish);

        // «Eski baza»: yangi amal kodlari yo'q, o'rnida `employment.manage`.
        // To'g'ridan-to'g'ri SQL — domen eski kodni endi umuman qabul qilmaydi.
        await kontekst.Database.ExecuteSqlRawAsync(
            """
            UPDATE roles
               SET permissions = array_append(
                       array(SELECT p FROM unnest(permissions) AS p WHERE p NOT LIKE 'employment.%' OR p = 'employment.view'),
                       'employment.manage')
             WHERE code IN ('super_admin', 'main_boss', 'hr_manager', 'custom_eski0001');
            """,
            bekorQilish);

        kontekst.ChangeTracker.Clear();

        var oldin = await kontekst.Roles.AsNoTracking().ToDictionaryAsync(r => r.Code, r => r.Permissions.ToArray(), bekorQilish);
        oldin[SystemRoles.HrManager].ShouldContain(Permissions.LegacyEmploymentManage, "test sozlamasi noto'g'ri");
        oldin[SystemRoles.HrManager].ShouldNotContain(Permissions.EmploymentSalary, "test sozlamasi noto'g'ri");

        await new IdentitySeeder(kontekst).SeedAsync(bekorQilish);
        kontekst.ChangeTracker.Clear();

        var keyin = await kontekst.Roles.AsNoTracking().ToDictionaryAsync(r => r.Code, r => r.Permissions.ToArray(), bekorQilish);

        foreach (var kod in new[] { SystemRoles.SuperAdmin, SystemRoles.MainBoss, SystemRoles.HrManager, "custom_eski0001" })
        {
            keyin[kod].ShouldNotContain(Permissions.LegacyEmploymentManage, kod);

            foreach (var amal in Permissions.EmploymentActions)
            {
                keyin[kod].ShouldContain(amal, $"«{kod}» «{amal}» ni olishi kerak edi");
            }

            // Eski koddan boshqa hech narsa yo'qolmagan
            foreach (var ruxsat in oldin[kod].Where(p => p != Permissions.LegacyEmploymentManage))
            {
                keyin[kod].ShouldContain(ruxsat, $"«{kod}» «{ruxsat}» ni yo'qotmasligi kerak");
            }
        }

        // Bot rolida eski kod yo'q edi — unga amallar berilmaydi
        keyin[SystemRoles.RecruitingBot].ShouldBe(
            SystemRoles.Defaults[SystemRoles.RecruitingBot].Permissions, ignoreOrder: true);

        // Ikkinchi ishga tushish — hech narsa o'zgarmaydi
        await new IdentitySeeder(kontekst).SeedAsync(bekorQilish);
        kontekst.ChangeTracker.Clear();

        var ikkinchi = await kontekst.Roles.AsNoTracking().ToDictionaryAsync(r => r.Code, r => r.Permissions.ToArray(), bekorQilish);

        foreach (var (kod, ruxsatlar) in keyin)
        {
            ikkinchi[kod].ShouldBe(ruxsatlar, $"«{kod}» ikkinchi ishga tushishda o'zgarmasligi kerak");
        }
    }

    /// <summary>
    /// Egasi o'chirgan oddiy rol keyingi ishga tushishda QAYTA YARATILMAYDI.
    /// </summary>
    /// <remarks>
    /// Oddiy rollar faqat birinchi ishga tushishda (bazada super
    /// administrator hali yo'q) yaratiladi. «Yo'q bo'lsa yarat» har safar
    /// ishlasa, o'chirilgan rol har deploy'da qaytib kelardi. O'z bazasida:
    /// umumiy bazadan HR menejeri rolini o'chirish boshqa testlarni buzardi.
    /// </remarks>
    [Fact]
    public async Task Ochirilgan_oddiy_rol_qayta_yaratilmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var yakka = new BonviApiFactory();
        await yakka.InitializeAsync();
        using (var _ = yakka.CreateClient()) { }

        await using var qamrov = yakka.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        // Yangi bazada ikkala rol ham bor va ularni hech kim egallamagan —
        // egasi ularni muharrirdan o'chirgandek.
        await kontekst.Database.ExecuteSqlRawAsync(
            "DELETE FROM roles WHERE code IN ('hr_manager', 'main_boss');",
            bekorQilish);

        kontekst.ChangeTracker.Clear();

        await new IdentitySeeder(kontekst).SeedAsync(bekorQilish);

        var kodlar = await kontekst.Roles.AsNoTracking().Select(r => r.Code).ToListAsync(bekorQilish);

        kodlar.ShouldNotContain(SystemRoles.HrManager);
        kodlar.ShouldNotContain(SystemRoles.MainBoss);
        kodlar.ShouldContain(SystemRoles.SuperAdmin);
        kodlar.ShouldContain(SystemRoles.RecruitingBot);
    }

    /// <summary>
    /// Ikki rolli modeldan qolgan <c>admin</c> roli <c>super_admin</c> ga
    /// QAYTA NOMLANADI — foydalanuvchilardagi <c>role_ids</c> massiviga
    /// tegilmagan holda.
    /// </summary>
    /// <remarks>
    /// Bu test o'zining ALOHIDA bazasida ishlaydi: u rol jadvalini eski
    /// modelga qaytaradi va umumiy bazada bu boshqa testlarga ta'sir qilardi.
    /// </remarks>
    [Fact]
    public async Task Eski_admin_roli_super_admin_ga_kochiriladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var yakka = new BonviApiFactory();
        await yakka.InitializeAsync();

        // Fabrikani haqiqatan ko'taramiz — migratsiya va urug'lantirish o'tsin
        using (var _ = yakka.CreateClient()) { }

        await using var qamrov = yakka.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        // "Eski baza" ni qayta hosil qilamiz: `super_admin` yana `admin` bo'ladi.
        // To'g'ridan-to'g'ri SQL — `Code` domen darajasida faqat `Recode` orqali
        // o'zgaradi, bu yerda esa aynan ko'chirishning O'ZI sinovdan o'tmoqda.
        await kontekst.Database.ExecuteSqlRawAsync(
            """UPDATE roles SET code = 'admin', name = 'Asosiy rahbar / Administrator' WHERE code = 'super_admin';""",
            bekorQilish);

        // Yangi modeldagi ikkinchi rolni ham olib tashlaymiz — eski bazada u yo'q edi
        await kontekst.Database.ExecuteSqlRawAsync(
            """DELETE FROM roles WHERE code = 'main_boss';""",
            bekorQilish);

        kontekst.ChangeTracker.Clear();

        var eski = await kontekst.Roles.AsNoTracking().SingleAsync(r => r.Code == "admin", bekorQilish);
        var eskiId = eski.Id;

        // Boshlang'ich hisob hamon o'sha rolga bog'langan bo'lishi kerak
        var hisob = await kontekst.Users
            .AsNoTracking()
            .SingleAsync(u => u.RoleIds.Contains(eskiId), bekorQilish);

        await new IdentitySeeder(kontekst).SeedAsync(bekorQilish);

        kontekst.ChangeTracker.Clear();

        var kochirilgan = await kontekst.Roles
            .AsNoTracking()
            .SingleAsync(r => r.Code == SystemRoles.SuperAdmin, bekorQilish);

        kochirilgan.Id.ShouldBe(eskiId, "rol identifikatori o'zgarmasligi kerak");
        kochirilgan.Name.ShouldBe(SystemRoles.Defaults[SystemRoles.SuperAdmin].Name);
        kochirilgan.Permissions.Count.ShouldBe(Permissions.All.Count);

        // Eski kod umuman qolmasligi kerak
        (await kontekst.Roles.AsNoTracking().AnyAsync(r => r.Code == SystemRoles.LegacyAdmin, bekorQilish))
            .ShouldBeFalse();

        // Yetishmayotgan uchinchi rol ham shu yerda yaratiladi: bu «birinchi
        // ishga tushish» — bazada super administrator hali yo'q edi
        // (`Ochirilgan_oddiy_rol_qayta_yaratilmaydi` bilan farqi shunda).
        (await kontekst.Roles.AsNoTracking().AnyAsync(r => r.Code == SystemRoles.MainBoss, bekorQilish))
            .ShouldBeTrue();

        // Eng muhimi: hisob rolsiz qolmadi — u hamon o'sha identifikatorga bog'liq
        var kochirilganHisob = await kontekst.Users
            .AsNoTracking()
            .SingleAsync(u => u.Id == hisob.Id, bekorQilish);

        kochirilganHisob.RoleIds.ShouldContain(eskiId);
    }

    [Fact]
    public async Task Tizim_royxatida_bolmagan_rolga_seeder_tegmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        // Administrator kelajakda qo'lda yaratishi mumkin bo'lgan maxsus rol —
        // kodi SystemRoles.Defaults'da yo'q, shu sababli seeder uni bilmaydi
        // va ruxsatlarini o'zgartirmasligi kerak.
        var maxsusRol = Role.Create("qollab_quvvatlash", "Qo'llab-quvvatlash", isSystem: false).Value;
        maxsusRol.Grant(Permissions.ReportsView).IsSuccess.ShouldBeTrue();

        kontekst.Roles.Add(maxsusRol);
        await kontekst.SaveChangesAsync(bekorQilish);

        await new IdentitySeeder(kontekst).SeedAsync(bekorQilish);

        var yangilangan = await kontekst.Roles
            .AsNoTracking()
            .SingleAsync(r => r.Id == maxsusRol.Id, bekorQilish);

        yangilangan.Permissions.ShouldBe([Permissions.ReportsView]);
    }

    /// <summary>Oddiy rolning asl holatini qaytaradi — seeder buni endi o'zi qilmaydi.</summary>
    private async Task TiklaAsync(string kod, IReadOnlyList<string> ruxsatlar, string? nom = null)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var rol = await kontekst.Roles.SingleAsync(r => r.Code == kod, TestContext.Current.CancellationToken);
        rol.ReplacePermissions(ruxsatlar).IsSuccess.ShouldBeTrue();

        if (nom is not null)
        {
            rol.Rename(nom).IsSuccess.ShouldBeTrue();
        }

        await kontekst.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
