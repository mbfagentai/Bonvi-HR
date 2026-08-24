using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Ism va ma'lumotnoma nomlari o'zbek alifbosi tartibida saralanishi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega bu test bor.</b> Saralash tartibi — bazaning sozlamasi, kodda
/// ko'rinmaydi. Kimdir migratsiyani qaytarsa yoki yangi ustunga kolatsiya
/// qo'yishni unutsa, xato faqat HR ekranda «Ўринов» ni «Абдуллаев» dan
/// oldin ko'rganda ma'lum bo'lardi. Bu yerda esa u build'ni yiqitadi.
/// </para>
/// <para>
/// Standart <c>en_US.utf8</c> kolatsiyasi kirillni bayt tartibida
/// solishtiradi va quyidagi tekshiruvlarning HAMMASI yiqiladi.
/// </para>
/// </remarks>
public sealed class CollationTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public CollationTests(BonviApiFactory factory) => _factory = factory;

    /// <summary>Bu testlar uchun yaratilgan xodimlarni ajratuvchi belgi.</summary>
    /// <remarks>
    /// Testlar bitta bazani baham ko'radi. Ism oldiga qo'yiladigan bu prefiks
    /// boshqa testlar yaratgan xodimlarni saralash natijasiga aralashtirmaydi.
    /// </remarks>
    private const string Belgi = "Ззсаралаш";

    [Fact]
    public async Task Xodim_familiyalari_ozbek_alifbosi_tartibida_saralanadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        // Ataylab teskari tartibda kiritiladi — natija kiritish tartibiga
        // emas, faqat kolatsiyaga bog'liq bo'lsin.
        string[] familiyalar = ["Ҳакимов", "Қосимов", "Ғаниев", "Юсупов", "Абдуллаев"];

        foreach (var familiya in familiyalar)
        {
            var ism = FullName.Create(familiya, Belgi, null).Value;
            _ = kontekst.Employees.Add(Employee.Create(ism).Value);
        }

        await kontekst.SaveChangesAsync(bekorQilish);

        var tartib = await kontekst.Employees
            .AsNoTracking()
            .Where(xodim => xodim.FullName.FirstName == Belgi)
            .OrderBy(xodim => xodim.FullName.LastName)
            .Select(xodim => xodim.FullName.LastName)
            .ToListAsync(bekorQilish);

        // Г → Ғ → К → Қ → Х → Ҳ tartibi: qo'shimcha harflar o'z asosidan
        // KEYIN turadi, alifboning oxirida emas.
        tartib.ShouldBe(["Абдуллаев", "Ғаниев", "Қосимов", "Ҳакимов", "Юсупов"]);
    }

    [Fact]
    public async Task Qoshimcha_harflar_asosiy_harfdan_keyin_turadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        // Har bir juftlik: oddiy harf va uning o'zbekcha qo'shimchasi.
        string[] juftliklar = ["Ҳакимов", "Хакимов", "Ғаниев", "Ганиев", "Қосимов", "Кудратов"];

        foreach (var familiya in juftliklar)
        {
            var ism = FullName.Create(familiya, Belgi + "Ж", null).Value;
            _ = kontekst.Employees.Add(Employee.Create(ism).Value);
        }

        await kontekst.SaveChangesAsync(bekorQilish);

        var tartib = await kontekst.Employees
            .AsNoTracking()
            .Where(xodim => xodim.FullName.FirstName == Belgi + "Ж")
            .OrderBy(xodim => xodim.FullName.LastName)
            .Select(xodim => xodim.FullName.LastName)
            .ToListAsync(bekorQilish);

        tartib.ShouldBe(["Ганиев", "Ғаниев", "Кудратов", "Қосимов", "Хакимов", "Ҳакимов"]);
    }

    [Fact]
    public async Task Bir_familiyaning_ikki_imlosi_royxatda_yonma_yon_tushadi()
    {
        // Bloknotda «Тошпулатов» va «Тўшпулатов» ikkalasi ham bor va ETL
        // ularni ataylab birlashtirmaydi — ular turli odam bo'lishi mumkin.
        // Kolatsiya «ў» ni «у» bilan bir darajada ko'radi, shu sababli ikki
        // imlo ro'yxatda yonma-yon tushadi va HR ikkalasini birga ko'radi.
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        string[] familiyalar = ["Шокиров", "Тўшпулатов", "Абдуллаев", "Тошпулатов"];

        foreach (var familiya in familiyalar)
        {
            var ism = FullName.Create(familiya, Belgi + "И", null).Value;
            _ = kontekst.Employees.Add(Employee.Create(ism).Value);
        }

        await kontekst.SaveChangesAsync(bekorQilish);

        var tartib = await kontekst.Employees
            .AsNoTracking()
            .Where(xodim => xodim.FullName.FirstName == Belgi + "И")
            .OrderBy(xodim => xodim.FullName.LastName)
            .Select(xodim => xodim.FullName.LastName)
            .ToListAsync(bekorQilish);

        tartib.ShouldBe(["Абдуллаев", "Тошпулатов", "Тўшпулатов", "Шокиров"]);
    }

    [Fact]
    public async Task Malumotnoma_nomlari_ham_ozbek_tartibida_saralanadi()
    {
        // Ma'lumotnomalar `ConfigureReference` orqali sozlanadi — sakkizta
        // jadval uchun bitta qoida. Bittasi tekshirilsa, hammasi tekshirilgan
        // bo'ladi.
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        string[] nomlar = ["Ззҳудуд бўлими", "Ззғазна бўлими", "Ззқоровул бўлими", "Ззавтошина бўлими"];

        foreach (var nom in nomlar)
        {
            _ = kontekst.Positions.Add(Position.Create(nom).Value);
        }

        await kontekst.SaveChangesAsync(bekorQilish);

        var tartib = await kontekst.Positions
            .AsNoTracking()
            .Where(lavozim => lavozim.Name.StartsWith("Зз"))
            .OrderBy(lavozim => lavozim.Name)
            .Select(lavozim => lavozim.Name)
            .ToListAsync(bekorQilish);

        tartib.ShouldBe(["Ззавтошина бўлими", "Ззғазна бўлими", "Ззқоровул бўлими", "Ззҳудуд бўлими"]);
    }
}
