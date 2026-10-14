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

    /// <summary>LOTINDA saqlangan test xodimini ajratuvchi ism.</summary>
    private const string BelgiLotin = "Qidiruvbek";

    /// <summary>
    /// Lotinda saqlangan familiya — bot orqali kelganlar ismini shunday yozadi.
    /// </summary>
    /// <remarks>Kirillda «Жўрақулов»: ў, қ — ikkalasi ham lotinda boshqacha.</remarks>
    private const string LotinFamiliya = "Jo'raqulov";

    /// <summary>Lotin xodimning JSHSHIR'i — idempotentlik kaliti.</summary>
    private const string LotinJshshir = "31503905234564";

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

    /// <summary>
    /// Telefon formatlangan (bo'shliq, tire, «+») holda ko'chirib
    /// qidirilganda ham topiladi.
    /// </summary>
    /// <remarks>
    /// Bazada telefon kanonik shaklda saqlanadi (<c>PhoneNumber</c> qiymat
    /// obyekti), lekin HR ekranda <c>+998 90 508 74 12</c> ko'radi va aynan
    /// shu ko'rinishni ko'chirib qidiruvga qo'yadi. Ajratgichlar hisobga
    /// olinmasa, bu qidiruv jimgina bo'sh natija berardi.
    /// </remarks>
    [Theory]
    [InlineData("90 508 74 12")]        // bo'shliq bilan
    [InlineData("+998 90 508 74 12")]   // mamlakat kodi + bo'shliq
    [InlineData("90-508-74-12")]        // tire bilan
    [InlineData("(90) 508-74-12")]      // qavs bilan
    [InlineData("905087412")]           // ajratgichsiz — avvalgidek ishlashi kerak
    public async Task Telefon_ajratgichlar_bilan_yozilganda_ham_topiladi(string qidiruv)
    {
        await TayyorlaAsync();

        var topilgan = await QidirAsync(qidiruv);

        topilgan.ShouldBe(1, $"«{qidiruv}» bo'yicha xodim topilishi kerak edi");
    }

    /// <summary>
    /// Qidiruv matnida bironta ham raqam bo'lmasa, telefon sharti
    /// so'rovga qo'shilmaydi va ism bo'yicha qidiruv buzilmaydi.
    /// </summary>
    /// <remarks>
    /// Xavfli xato: telefon uchun raqamsiz naqsh <c>%%</c> ga aylanib,
    /// butun jadvalni olib kelishi mumkin edi. Bu test aynan shu holatdan
    /// himoyalanganini tasdiqlaydi — faqat SHU test yaratgan xodim
    /// topilishi kerak, «begona» yozuvlar emas.
    /// </remarks>
    [Fact]
    public async Task Raqamsiz_qidiruvda_telefon_sharti_qollanmaydi()
    {
        await TayyorlaAsync();

        var topilgan = await QidirAsync(Familiya);

        topilgan.ShouldBe(1, "ism bo'yicha qidiruv raqamsiz bo'lsa ham ishlashi kerak edi");
    }

    // ────────────────────────────────────────────────────────────
    //  Lotin / kirill (2026-10-14)
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Kirillda saqlangan ism LOTINDA yozilganda ham topiladi.
    /// </summary>
    /// <remarks>
    /// Ismlar bazada aralash: №91 va Jobster'dan kelganlar kirillda, bot
    /// orqali ariza berganlar lotinda. HR qaysi yozuvda yozishini oldindan
    /// bilmaydi, shu sababli ikkalasi ham bitta qidiruv kalitiga tushadi
    /// (<c>search_key()</c>, migratsiya <c>IsmQidiruvKaliti</c>). Lotinning
    /// o'zi ham har xil: rasmiy («o'», «g'», turli apostroflar), apostrofsiz
    /// va ruscha uslub («kh», «u»).
    /// </remarks>
    [Theory]
    [InlineData("Ganiqo'hov")]    // rasmiy lotin
    [InlineData("Gʻaniqoʻhov")]   // boshqa apostrof (U+02BB)
    [InlineData("Ganiqohov")]     // apostrofsiz
    [InlineData("Ganikuxov")]     // ruscha lotin uslubi
    [InlineData("GANIQOHOV")]     // CAPS LOCK
    [InlineData("niqoh")]         // o'rtadan bo'lak
    public async Task Kirillda_saqlangan_ism_lotinda_yozilganda_ham_topiladi(string qidiruv)
    {
        await TayyorlaAsync();

        var topilgan = await QidirAsync(qidiruv);

        topilgan.ShouldBe(1, $"«{qidiruv}» bo'yicha kirillda saqlangan xodim topilishi kerak edi");
    }

    /// <summary>
    /// Lotinda saqlangan ism KIRILLDA yozilganda ham topiladi — teskari yo'l.
    /// </summary>
    [Theory]
    [InlineData("Jo'raqulov")]    // aynan saqlangani
    [InlineData("Jorakulov")]     // apostrofsiz, q → k
    [InlineData("Jurakulov")]     // ў → u
    [InlineData("Жўрақулов")]     // kirill
    [InlineData("Журакулов")]     // kirill, rus klaviaturasi
    [InlineData("ЖЎРАҚУЛОВ")]     // kirill, CAPS LOCK
    public async Task Lotinda_saqlangan_ism_kirillda_yozilganda_ham_topiladi(string qidiruv)
    {
        await TayyorlaAsync();

        var topilgan = await QidirAsync(qidiruv, BelgiLotin);

        topilgan.ShouldBe(1, $"«{qidiruv}» bo'yicha lotinda saqlangan xodim topilishi kerak edi");
    }

    /// <summary>
    /// Yozuvni birlashtirish qidiruvni «kengaytirib» yubormaydi.
    /// </summary>
    /// <remarks>
    /// <see cref="Yigish_begona_yozuvni_olib_kelmaydi"/> ning lotin/kirill
    /// juftligi: o'xshash, lekin BOSHQA familiya ikkala xodimdan hech birini
    /// olib kelmasligi kerak.
    /// </remarks>
    [Theory]
    [InlineData("Ganiyev")]       // «Ганиев» lotinda
    [InlineData("Жўраев")]        // Jo'raqulov ga o'xshash, lekin boshqa
    [InlineData("Jo'rabek")]
    public async Task Lotin_kirill_birlashuvi_begona_yozuvni_olib_kelmaydi(string qidiruv)
    {
        await TayyorlaAsync();

        (await QidirAsync(qidiruv)).ShouldBe(0, $"«{qidiruv}» kirill xodimni topmasligi kerak edi");
        (await QidirAsync(qidiruv, BelgiLotin)).ShouldBe(0, $"«{qidiruv}» lotin xodimni topmasligi kerak edi");
    }

    /// <summary>
    /// Bitta ismning turli yozilishlari bitta kalitga tushadi.
    /// </summary>
    /// <remarks>
    /// Qoidaning o'zi — bazadagi funksiya — to'g'ridan-to'g'ri tekshiriladi.
    /// Juftliklar haqiqiy ma'lumotdan: dev bazadagi bir xil telefonli
    /// lotin/kirill yozuvlar (2026-10-14 tahlili, 113 juftdan 109 tasi
    /// shu qoida bilan topildi).
    /// </remarks>
    [Theory]
    [InlineData("Ёқубов", "Yoqubov")]
    [InlineData("Ёқубов", "Екубов")]       // eski «ё = е» qoidasi saqlangan
    [InlineData("Семёнов", "Семенов")]
    [InlineData("Тўхтаев", "To'xtayev")]
    [InlineData("Тўхтаев", "Toxtayev")]
    [InlineData("Тўхтаев", "Tuxtayev")]
    [InlineData("Ғофуров", "G‘ofurov")]
    [InlineData("Ҳасанов", "Xasanov")]
    [InlineData("Ҳасанов", "Khasanov")]
    [InlineData("Абдуллаев", "Abdullayev")]
    [InlineData("Шоҳруҳ", "Shohruh")]
    [InlineData("Шоҳруҳ", "Shoxrux")]
    [InlineData("Улуғбек", "Uluģbek")]
    [InlineData("Маъмуров", "Ma'murov")]
    [InlineData("Жасур", "Zhasur")]
    [InlineData("Исҳоқ", "Ishoq")]
    [InlineData("Мамаджанов", "Mamadjanov")]
    [InlineData("Эргашев", "Ергашев")]
    [InlineData("Aзимжон", "Азимжон")]     // birinchi «A» — LOTIN harf, kirill ism ichida
    public async Task Bir_ismning_yozilishlari_bitta_kalitga_tushadi(string birinchi, string ikkinchi)
    {
        var kalitlar = await KalitlarAsync(birinchi, ikkinchi);

        kalitlar.Birinchi.ShouldBe(kalitlar.Ikkinchi, $"«{birinchi}» va «{ikkinchi}» bitta odam");
    }

    /// <summary>
    /// Boshqa-boshqa ismlar bitta kalitga tushmaydi.
    /// </summary>
    /// <remarks>
    /// «a» va «o» ATAYLAB birlashtirilmaydi: Собиров / Сабиров, Олимов /
    /// Алимов — boshqa odamlar, birlashsa ro'yxat begona natijaga to'lardi.
    /// </remarks>
    [Theory]
    [InlineData("Собиров", "Сабиров")]
    [InlineData("Олимов", "Alimov")]
    [InlineData("Каримов", "Керимов")]
    [InlineData("Шерзод", "Serzod")]
    public async Task Boshqa_ismlar_bitta_kalitga_tushmaydi(string birinchi, string ikkinchi)
    {
        var kalitlar = await KalitlarAsync(birinchi, ikkinchi);

        kalitlar.Birinchi.ShouldNotBe(kalitlar.Ikkinchi, $"«{birinchi}» va «{ikkinchi}» boshqa ismlar");
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    /// <summary>Ikki matnning bazadagi qidiruv kalitlari.</summary>
    private async Task<(string Birinchi, string Ikkinchi)> KalitlarAsync(string birinchi, string ikkinchi)
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        // Bitta qatorda — ikki qatorli javobning tartibiga tayanmaslik uchun.
        var juft = await kontekst.Database
            .SqlQuery<string>($"SELECT search_key({birinchi}) || '|' || search_key({ikkinchi}) AS \"Value\"")
            .SingleAsync(bekorQilish)
            .ConfigureAwait(false);

        var qismlar = juft.Split('|');

        return (qismlar[0], qismlar[1]);
    }

    /// <summary>
    /// Ikki test xodimini yaratadi — kirillda va lotinda saqlangan.
    /// Idempotent — xodim allaqachon bo'lsa, unga tegmaydi.
    /// </summary>
    /// <remarks>
    /// Har bir <c>[Theory]</c> qatori alohida test, lekin xodim bitta
    /// bo'lishi kerak: JSHSHIR unikal va ikkinchi marta qo'shish
    /// cheklovga urilardi. Testlar ketma-ket ham, parallel ham
    /// ishlashi mumkin, shu sababli tekshiruv har safar qayta bajariladi.
    /// </remarks>
    private async Task TayyorlaAsync()
    {
        await QoyAsync(Jshshir, Familiya, Belgi, xodim =>
        {
            xodim.SetPassport(PassportSeries.Create(Pasport).Value);
            xodim.SetPhones(PhoneNumber.Create("905087412").Value);
        });

        // Lotin xodimda pasport va telefon ATAYLAB yo'q: u faqat ism
        // bo'yicha qidiruvni tekshiradi va kirill xodimning kod/telefon
        // testlariga «ikkinchi topilgan» bo'lib aralashmasligi kerak.
        await QoyAsync(LotinJshshir, LotinFamiliya, BelgiLotin, _ => { });
    }

    /// <summary>Bitta test xodimini JSHSHIR bo'yicha idempotent qo'yadi.</summary>
    private async Task QoyAsync(string jshshir, string familiya, string ism, Action<Employee> toldir)
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var pinfl = Pinfl.Create(jshshir).Value;

        var mavjud = await kontekst.Employees
            .AnyAsync(xodim => xodim.Pinfl == pinfl, bekorQilish)
            .ConfigureAwait(false);

        if (mavjud)
        {
            return;
        }

        var xodimYangi = Employee.Create(
            FullName.Create(familiya, ism, null).Value,
            new DateOnly(1990, 3, 15)).Value;

        _ = xodimYangi.SetPinfl(pinfl);
        toldir(xodimYangi);

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
    /// Sanoq faqat SHU test yaratgan yozuvlar bo'yicha (<paramref name="belgi"/>
    /// — xodimning ismi): baza boshqa testlar bilan baham ko'riladi va
    /// umumiy <c>total</c> ular qo'shgan yozuvlardan ham o'zgarardi.
    /// </remarks>
    private async Task<int> QidirAsync(string qidiruv, string belgi = Belgi)
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var oqish = qamrov.ServiceProvider.GetRequiredService<IEmployeeReadModel>();

        var sahifa = await oqish
            .SearchEmployeesAsync(new EmployeeSearchFilter(qidiruv, PageSize: 200), bekorQilish)
            .ConfigureAwait(false);

        return sahifa.Items.Count(qator => qator.FirstName == belgi);
    }
}
