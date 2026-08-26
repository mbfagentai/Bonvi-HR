using Bonvi.Domain.Identity;
using Bonvi.Infrastructure.Persistence;
using Bonvi.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Tizim rollarining ruxsatlarini koddagi ta'rif bilan sinxronlashning
/// haqiqiy PostgreSQL ustidagi testlari.
/// </summary>
/// <remarks>
/// <para>
/// Sabab-oqibat: avvalgi <see cref="IdentitySeeder"/> faqat YO'Q rolni yaratardi —
/// bir marta yaratilgan rolga keyinchalik <see cref="Permissions"/> ga qo'shilgan
/// yangi ruxsat HECH QACHON yetib bormasdi (masalan <c>incentives.view</c> —
/// tab qurilgach ham bosh administratorda ochilmay qoldi). Bu yerdagi testlar
/// aynan shu holatni — DB va koddagi ta'rif bir-biridan uzoqlashib ketishini —
/// qayta hosil qiladi va tuzatilgan seeder buni yo'qotishini isbotlaydi.
/// </para>
/// <para>
/// Testlar bitta bazani baham ko'radi va ketma-ket bajariladi (<c>CatalogPersistenceTests</c>
/// bilan bir xil qoida): har bir test seederni qayta chaqirib, rolni YANA to'g'ri
/// (koddagi ta'rifga mos) holatga qaytaradi — shu sababli keyingi testga
/// ta'sir qilmaydi.
/// </para>
/// </remarks>
public sealed class IdentitySeederPersistenceTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public IdentitySeederPersistenceTests(BonviApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Kodga_yangi_ruxsat_qoshilsa_mavjud_tizim_roli_uni_oladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        // Factory ishga tushganda hr_manager allaqachon TO'LIQ ro'yxat bilan
        // yaratilgan. Kod bilan bazani "orqada qoldirish" uchun ikkita ruxsatni
        // qo'lda olib tashlaymiz — xuddi ular hozirgina Permissions'ga
        // qo'shilgandek (aynan «incentives.*» topilgan haqiqiy hodisa).
        var rol = await kontekst.Roles
            .SingleAsync(r => r.Code == SystemRoles.HrManager, bekorQilish);

        rol.Revoke(Permissions.IncentivesView).IsSuccess.ShouldBeTrue();
        rol.Revoke(Permissions.IncentivesManage).IsSuccess.ShouldBeTrue();
        await kontekst.SaveChangesAsync(bekorQilish);

        rol.Has(Permissions.IncentivesView).ShouldBeFalse("test sozlamasi noto'g'ri");

        // Ilova qayta ishga tushgandek — seeder yana chaqiriladi.
        await new IdentitySeeder(kontekst).SeedAsync(bekorQilish);

        var yangilangan = await kontekst.Roles
            .AsNoTracking()
            .SingleAsync(r => r.Code == SystemRoles.HrManager, bekorQilish);

        yangilangan.Has(Permissions.IncentivesView).ShouldBeTrue();
        yangilangan.Has(Permissions.IncentivesManage).ShouldBeTrue();
        yangilangan.Permissions.Count.ShouldBe(SystemRoles.Defaults[SystemRoles.HrManager].Permissions.Length);
    }

    [Fact]
    public async Task Koddan_olib_tashlangan_ruxsat_tizim_rolidan_ham_olinadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        // hr_manager'ga koddagi ta'rifda YO'Q ruxsatni qo'lda beramiz — xuddi
        // rol avvalroq (masalan boshqa administrator tomonidan) kengaytirilgandek,
        // keyin esa o'sha ruxsat SystemRoles.Defaults'dan olib tashlangandek.
        var rol = await kontekst.Roles
            .SingleAsync(r => r.Code == SystemRoles.HrManager, bekorQilish);

        SystemRoles.Defaults[SystemRoles.HrManager].Permissions
            .ShouldNotContain(Permissions.UsersManage, "test shu farqqa tayanadi");

        rol.Grant(Permissions.UsersManage).IsSuccess.ShouldBeTrue();
        await kontekst.SaveChangesAsync(bekorQilish);

        rol.Has(Permissions.UsersManage).ShouldBeTrue("test sozlamasi noto'g'ri");

        await new IdentitySeeder(kontekst).SeedAsync(bekorQilish);

        var yangilangan = await kontekst.Roles
            .AsNoTracking()
            .SingleAsync(r => r.Code == SystemRoles.HrManager, bekorQilish);

        yangilangan.Has(Permissions.UsersManage).ShouldBeFalse();
        yangilangan.Permissions.Count.ShouldBe(SystemRoles.Defaults[SystemRoles.HrManager].Permissions.Length);
    }

    [Fact]
    public async Task Administrator_roli_hech_qachon_toliqsiz_qolmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        // Domen qoidasi admin rolidan Grant/Revoke orqali ruxsat olib tashlashni
        // taqiqlaydi (Role.Revoke) — shu sababli haqiqiy hodisadagi "eski bazani"
        // qayta hosil qilish uchun to'g'ridan-to'g'ri SQL bilan buzamiz.
        await kontekst.Database.ExecuteSqlRawAsync(
            "UPDATE roles SET permissions = ARRAY['employees.view']::text[] WHERE code = 'admin'",
            bekorQilish);

        // EF keshi bilan bazani mos holatga keltirish uchun kuzatuvni tozalaymiz —
        // aks holda kuzatilayotgan eski nusxa keyingi so'rovga qaytib chiqadi.
        kontekst.ChangeTracker.Clear();

        await new IdentitySeeder(kontekst).SeedAsync(bekorQilish);

        var yangilangan = await kontekst.Roles
            .AsNoTracking()
            .SingleAsync(r => r.Code == SystemRoles.Admin, bekorQilish);

        yangilangan.Permissions.Count.ShouldBe(Permissions.All.Count);
        foreach (var ruxsat in Permissions.All)
        {
            yangilangan.Has(ruxsat).ShouldBeTrue($"admin «{ruxsat}» ruxsatiga ega bo'lishi kerak edi");
        }
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
}
