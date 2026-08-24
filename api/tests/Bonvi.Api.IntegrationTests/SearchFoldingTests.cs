using Bonvi.Application.Employees.Abstractions;
using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// «Ko'z bilan bir xil, kompyuter uchun boshqa» belgilar qidiruvni
/// buzmasligi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega bu testlar bor.</b> Topilmagan qidiruv — bu bo'sh ekran emas.
/// HR odamni topolmasa, uni bazada yo'q deb hisoblaydi va yangi karta
/// ochadi; loyihaning eng katta muammosi (Excel'da bitta odam uch marta)
/// aynan shundan tug'ilgan. Ya'ni bu yerdagi har bir yiqilish —
/// kelajakdagi dublikat.
/// </para>
/// <para>
/// Tekshiruv ATAYLAB haqiqiy PostgreSQL ustida: yig'ish
/// (<c>translate</c>) bazada bajariladi va uni mock bilan ko'rib
/// bo'lmaydi. Bundan tashqari, ifoda ustidagi indeks so'rovdagi ifoda
/// bilan aynan mos tushishi kerak — bu ham faqat haqiqiy bazada bilinadi.
/// </para>
/// <para>
/// Testlar bitta bazani baham ko'radi, shu sababli xodimlar
/// <see cref="Belgi"/> bilan ajratiladi va tekshiruvlar faqat shu
/// yozuvlarni sanaydi.
/// </para>
/// </remarks>
public sealed class SearchFoldingTests : IClassFixture<BonviApiFactory>
{
    /// <summary>Shu test yaratgan xodimlarni ajratuvchi ism.</summary>
    private const string Belgi = "Ққидирув";

    /// <summary>
    /// Familiyada to'rttala «qiyin» harf ham bor: қ, ғ, ҳ va ў.
    /// </summary>
    private const string Familiya = "Ғаниқўҳов";

    /// <summary>
    /// Pasport LOTIN harflari bilan saqlanadi — ETL shunday normallashtiradi.
    /// Test aynan shu holatdan boshlaydi va kirill bilan qidiradi.
    /// </summary>
    private const string Pasport = "AB7412589";

    /// <summary>Nazorat raqami to'g'ri JSHSHIR (1990-03-15, erkak).</summary>
    private const string Jshshir = "31503904234561";

    private readonly BonviApiFactory _factory;

    public SearchFoldingTests(BonviApiFactory factory) => _factory = factory;

    /// <summary>
    /// Rus klaviaturasida yozilgan ism ham topiladi.
    /// </summary>
    /// <remarks>
    /// қ, ғ, ҳ va ў — rus klaviaturasida YO'Q harflar. HR ularni eng yaqin
    /// harf bilan almashtiradi va «Ғаниқўҳов» «Ганикухов» bo'lib yoziladi.
    /// Registr ham har xil bo'ladi: HR ba'zan CAPS LOCK bilan yozadi.
    /// </remarks>
    [Theory]
    [InlineData("Ғаниқўҳов")]   // aynan saqlangani
    [InlineData("Ганикухов")]   // rus klaviaturasi — to'rttala harf ham almashgan
    [InlineData("ганикухов")]   // kichik registr
    [InlineData("ГАНИКУХОВ")]   // CAPS LOCK
    [InlineData("никух")]       // o'rtadan bo'lak
    [InlineData("Ғаникухов")]   // aralash: bir harf to'g'ri, qolgani almashgan
    public async Task Ism_qiyin_harflarsiz_yozilganda_ham_topiladi(string qidiruv)
    {
        await TayyorlaAsync();

        var topilgan = await QidirAsync(qidiruv);

        topilgan.ShouldBe(1, $"«{qidiruv}» bo'yicha xodim topilishi kerak edi");
    }

    /// <summary>
    /// Kirill klaviaturada yozilgan pasport ham topiladi.
    /// </summary>
    /// <remarks>
    /// Excel'da 607 ta pasportda kirill «А» (U+0410) turgan edi, lotin «A»
    /// (U+0041) emas. ETL ularni lotinga o'girdi — lekin HR baribir kirill
    /// klaviaturada yozadi. Ikkalasi ekranda AYNAN bir xil ko'rinadi, ya'ni
    /// HR nega topilmayotganini tushunolmasdi.
    /// </remarks>
    [Theory]
    [InlineData("AB7412589")]   // lotin — saqlangani
    [InlineData("АВ7412589")]   // kirill «А» va «В» — ko'zga bir xil
    [InlineData("ав7412589")]   // kichik kirill
    [InlineData("АВ741")]       // boshidan bo'lak
    public async Task Pasport_kirill_harflar_bilan_yozilganda_ham_topiladi(string qidiruv)
    {
        await TayyorlaAsync();

        var topilgan = await QidirAsync(qidiruv);

        topilgan.ShouldBe(1, $"«{qidiruv}» bo'yicha xodim topilishi kerak edi");
    }

    /// <summary>
    /// Yig'ish qidiruvni «kengaytirib» yubormaydi.
    /// </summary>
    /// <remarks>
    /// Eng xavfli xato — teskarisi: yig'ish natijasida BEGONA yozuvlar
    /// chiqib ketishi. U holda HR ro'yxatdan noto'g'ri odamni tanlab,
    /// boshqa birovning kartasiga buyruq yozib qo'yardi. Shu sababli
    /// mos kelmasligi SHART bo'lgan so'rovlar ham tekshiriladi.
    ///
    /// <c>%</c> va <c>_</c> — <c>LIKE</c> ning joker belgilari. Ular
    /// ekranlanmasa, «%» deb qidirgan HR butun bazani olib kelardi.
    /// </remarks>
    [Theory]
    [InlineData("Ганиев")]      // boshqa familiya
    [InlineData("AB7412580")]   // pasportning oxirgi raqami boshqa
    [InlineData("%")]           // LIKE jokeri — ekranlanishi kerak
    [InlineData("_")]           // LIKE jokeri
    public async Task Yigish_begona_yozuvni_olib_kelmaydi(string qidiruv)
    {
        await TayyorlaAsync();

        var topilgan = await QidirAsync(qidiruv);

        topilgan.ShouldBe(0, $"«{qidiruv}» bo'yicha hech narsa topilmasligi kerak edi");
    }

    /// <summary>
    /// JSHSHIR va telefon bo'yicha qidiruv avvalgidek ishlaydi.
    /// </summary>
    /// <remarks>
    /// Yig'ish qo'shilganda kod ustunlari uchun alohida naqsh paydo bo'ldi.
    /// Bu test o'sha o'zgarish eski, ishlab turgan yo'lni buzmaganini
    /// tasdiqlaydi — HR uchun JSHSHIR eng ishonchli kalit.
    /// </remarks>
    [Theory]
    [InlineData(Jshshir)]
    [InlineData("905087412")]
    [InlineData("+998905087412")]
    public async Task Jshshir_va_telefon_bilan_qidiruv_buzilmadi(string qidiruv)
    {
        await TayyorlaAsync();

        var topilgan = await QidirAsync(qidiruv);

        topilgan.ShouldBe(1, $"«{qidiruv}» bo'yicha xodim topilishi kerak edi");
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Test xodimini yaratadi. Idempotent — u allaqachon bo'lsa hech
    /// narsa qilmaydi.
    /// </summary>
    /// <remarks>
    /// Har bir <c>[Theory]</c> qatori alohida test, lekin xodim bitta
    /// bo'lishi kerak: JSHSHIR unikal va ikkinchi marta qo'shish
    /// cheklovga urilardi. Testlar ketma-ket ham, parallel ham
    /// ishlashi mumkin, shu sababli tekshiruv har safar qayta bajariladi.
    /// </remarks>
    private async Task TayyorlaAsync()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var pinfl = Pinfl.Create(Jshshir).Value;

        var mavjud = await kontekst.Employees
            .AnyAsync(xodim => xodim.Pinfl == pinfl, bekorQilish)
            .ConfigureAwait(false);

        if (mavjud)
        {
            return;
        }

        var ism = FullName.Create(Familiya, Belgi, null).Value;
        var xodimYangi = Employee.Create(ism, new DateOnly(1990, 3, 15)).Value;

        _ = xodimYangi.SetPinfl(pinfl);
        xodimYangi.SetPassport(PassportSeries.Create(Pasport).Value);
        xodimYangi.SetPhones(PhoneNumber.Create("905087412").Value);

        _ = kontekst.Employees.Add(xodimYangi);

        try
        {
            await kontekst.SaveChangesAsync(bekorQilish).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // Parallel ishlagan boshqa test qatori ulgurib qo'ygan —
            // bizga xodimning BORLIGI kerak, kim yaratgani emas.
        }
    }

    /// <summary>Nechta test xodimi topilganini qaytaradi.</summary>
    /// <remarks>
    /// Sanoq faqat SHU test yaratgan yozuvlar bo'yicha: baza boshqa
    /// testlar bilan baham ko'riladi va umumiy <c>total</c> ular
    /// qo'shgan yozuvlardan ham o'zgarardi.
    /// </remarks>
    private async Task<int> QidirAsync(string qidiruv)
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var oqish = qamrov.ServiceProvider.GetRequiredService<IEmployeeReadModel>();

        var sahifa = await oqish
            .SearchEmployeesAsync(new EmployeeSearchFilter(qidiruv, PageSize: 200), bekorQilish)
            .ConfigureAwait(false);

        return sahifa.Items.Count(qator => qator.FirstName == Belgi);
    }
}
