using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Catalog;
using Bonvi.Domain.Organization;
using Bonvi.Infrastructure.Persistence;
using Bonvi.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Ma'lumotnomalar qatlamining haqiqiy PostgreSQL ustidagi testlari.
/// </summary>
/// <remarks>
/// <para>
/// Bu yerda tekshirilayotgan narsalarning HECH BIRI mock bilan ko'rinmaydi:
/// <c>lower(name)</c> ustidagi unikal indeks, rekursiv CTE, kaskad o'chirish —
/// hammasi bazaning o'z imkoniyatlari.
/// </para>
/// <para>
/// Testlar bitta bazani baham ko'radi va ketma-ket bajariladi, shu sababli
/// har bir test O'ZIGA XOS nom va kod ishlatadi — aks holda ular
/// bir-birining unikallik cheklovlariga urilardi.
/// </para>
/// </remarks>
public sealed class CatalogPersistenceTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public CatalogPersistenceTests(BonviApiFactory factory) => _factory = factory;

    // ────────────────────────────────────────────────────────────
    //  Bo'limlar daraxti
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Bolim_daraxti_daraja_va_yolni_togri_hisoblaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
        var bolimlar = qamrov.ServiceProvider.GetRequiredService<IDepartmentRepository>();

        var ota = Department.Create("Daraxt Test Ota").Value;
        var bola = Department.Create("Daraxt Test Bola", ota.Id).Value;
        var nabira = Department.Create("Daraxt Test Nabira", bola.Id).Value;

        kontekst.AddRange(ota, bola, nabira);
        await kontekst.SaveChangesAsync(bekorQilish);

        await bolimlar.RebuildHierarchyAsync(bekorQilish);

        // Qayta hisoblash bazada bajariladi. Xotiradagi obyektlar ham yangilangan
        // bo'lishi SHART — aks holda keyingi saqlash eski qiymatni qaytarib yozardi.
        ota.Level.ShouldBe(0);
        ota.Path.ShouldBe("Daraxt Test Ota");

        bola.Level.ShouldBe(1);
        bola.Path.ShouldBe("Daraxt Test Ota / Daraxt Test Bola");

        nabira.Level.ShouldBe(2);
        nabira.Path.ShouldBe("Daraxt Test Ota / Daraxt Test Bola / Daraxt Test Nabira");
    }

    [Fact]
    public async Task Avlod_identifikatorlari_rekursiv_topiladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
        var bolimlar = qamrov.ServiceProvider.GetRequiredService<IDepartmentRepository>();

        var ota = Department.Create("Avlod Test Ota").Value;
        var bola = Department.Create("Avlod Test Bola", ota.Id).Value;
        var nabira = Department.Create("Avlod Test Nabira", bola.Id).Value;
        var begona = Department.Create("Avlod Test Begona").Value;

        kontekst.AddRange(ota, bola, nabira, begona);
        await kontekst.SaveChangesAsync(bekorQilish);

        var avlodlar = await bolimlar.GetDescendantIdsAsync(ota.Id, bekorQilish);

        // Ikkinchi darajadagi «nabira» ham topilishi kerak — aynan shu
        // rekursiv CTE ishlayotganini isbotlaydi
        avlodlar.ShouldContain(bola.Id);
        avlodlar.ShouldContain(nabira.Id);
        avlodlar.ShouldNotContain(begona.Id);
        avlodlar.ShouldNotContain(ota.Id);

        // Bevosita bolalar — faqat bitta daraja
        var bolalar = await bolimlar.GetChildrenAsync(ota.Id, bekorQilish);
        bolalar.Count.ShouldBe(1);
        bolalar[0].Id.ShouldBe(bola.Id);
    }

    // ────────────────────────────────────────────────────────────
    //  Nom bo'yicha unikallik — registrga sezgir emas
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Excel'dagi asosiy muammo: «Маркетинг бўлими» va «маркетинг бўлими»
    /// ikkita alohida bo'lim bo'lib yurgan.
    /// </summary>
    [Fact]
    public async Task Nom_boyicha_unikallik_registr_farqini_sezmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
            kontekst.Add(Department.Create("Маркетинг Тест Бўлими").Value);
            await kontekst.SaveChangesAsync(bekorQilish);
        }

        // Yangi qamrov: birinchi saqlash muvaffaqiyatli tugagani aniq bo'lsin
        await using var ikkinchiQamrov = _factory.Services.CreateAsyncScope();
        var ikkinchiKontekst = ikkinchiQamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        // Faqat REGISTRI bilan farq qiladi — kodi boshqa, ya'ni kod indeksi to'smaydi
        ikkinchiKontekst.Add(Department.Create("маркетинг тест бўлими").Value);

        var xato = await Should.ThrowAsync<DbUpdateException>(
            async () => await ikkinchiKontekst.SaveChangesAsync(bekorQilish));

        xato.InnerException.ShouldNotBeNull();
        xato.InnerException.Message.ShouldContain("ix_departments_name_lower");
    }

    [Fact]
    public async Task Malumotnoma_repozitoriysi_nomni_registr_va_boshliqqa_qaramay_topadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        // Ochiq generik ro'yxatga olish ishlayotganini ham shu yerda tekshiramiz:
        // `IReferenceRepository<TerminationReason>` uchun alohida sinf yo'q
        var sabablar = qamrov.ServiceProvider
            .GetRequiredService<IReferenceRepository<TerminationReason>>();

        var sabab = TerminationReason.Create("Қидирув Тест Сабаби").Value;
        kontekst.Add(sabab);
        await kontekst.SaveChangesAsync(bekorQilish);

        // Boshqa registr + ichida ortiqcha bo'shliq
        var topilgan = await sabablar.FindByNameAsync("қидирув  тест   сабаби", bekorQilish);
        topilgan.ShouldNotBeNull();
        topilgan.Id.ShouldBe(sabab.Id);

        (await sabablar.NameExistsAsync("ҚИДИРУВ ТЕСТ САБАБИ", cancellationToken: bekorQilish))
            .ShouldBeTrue();

        // Yozuvning O'ZI takror hisoblanmaydi — tahrirlashda shu kerak
        (await sabablar.NameExistsAsync("ҚИДИРУВ ТЕСТ САБАБИ", sabab.Id, bekorQilish))
            .ShouldBeFalse();

        var royxat = await sabablar.ListAsync(onlyActive: true, bekorQilish);
        royxat.ShouldContain(element => element.Id == sabab.Id);
    }

    // ────────────────────────────────────────────────────────────
    //  Lavozim va uning vazifalari
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Lavozim_vazifalari_bilan_saqlanadi_va_tartibda_oqiladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        Guid lavozimId;
        Guid bolimId;

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

            var bolim = Department.Create("Вазифа Тест Бўлими").Value;
            bolimId = bolim.Id;

            var lavozim = Position.Create("Вазифа Тест Юк ташувчи", bolim.Id).Value;
            lavozim.AddDuty("Юкни ортиш").IsSuccess.ShouldBeTrue();
            lavozim.AddDuty("Юкни ташиш").IsSuccess.ShouldBeTrue();
            lavozim.AddDuty("Юкни тушириш").IsSuccess.ShouldBeTrue();
            lavozimId = lavozim.Id;

            kontekst.AddRange(bolim, lavozim);
            await kontekst.SaveChangesAsync(bekorQilish);
        }

        // Yangi qamrov — xotiradagi nusxa emas, aynan bazadan o'qiladi
        await using var oqishQamrovi = _factory.Services.CreateAsyncScope();
        var lavozimlar = oqishQamrovi.ServiceProvider.GetRequiredService<IPositionRepository>();

        var oqilgan = await lavozimlar.GetWithDutiesAsync(lavozimId, bekorQilish);

        oqilgan.ShouldNotBeNull();
        oqilgan.DepartmentId.ShouldBe(bolimId);
        oqilgan.Duties.Count.ShouldBe(3);

        // Tartib SHART: vazifalar ro'yxati foydalanuvchi belgilagan ketma-ketlikda
        oqilgan.Duties[0].Text.ShouldBe("Юкни ортиш");
        oqilgan.Duties[1].Text.ShouldBe("Юкни ташиш");
        oqilgan.Duties[2].Text.ShouldBe("Юкни тушириш");

        var bolimLavozimlari = await lavozimlar.GetByDepartmentAsync(bolimId, bekorQilish);
        bolimLavozimlari.Count.ShouldBe(1);
        bolimLavozimlari[0].Id.ShouldBe(lavozimId);
    }

    // ────────────────────────────────────────────────────────────
    //  Urug'lantirish
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Urug'lantirish har ishga tushishda chaqiriladi, shu sababli u
    /// <b>idempotent</b> bo'lishi shart.
    /// </summary>
    /// <remarks>
    /// Test JSON fayllar mavjudligiga bog'liq EMAS: fayl bo'lmasa birinchi
    /// chaqiruv ham hech narsa yaratmaydi va baribir xato tashlamaydi —
    /// aynan shu talab tekshiriladi.
    /// </remarks>
    [Fact]
    public async Task Katalog_urugi_takroriy_chaqirilganda_yangi_yozuv_qoshmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        CatalogSeedResult birinchi;

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var urugchi = qamrov.ServiceProvider.GetRequiredService<CatalogSeeder>();
            birinchi = await urugchi.SeedAsync(bekorQilish);
        }

        // DIQQAT: bu yerda `Created > 0` ni kutib bo'lmaydi.
        // Urug'lantirish endi dastur ishga tushishida (`DatabaseInitializer`)
        // bajariladi, ya'ni test boshlanganda ma'lumotnomalar allaqachon to'la.
        // Buning o'rniga o'qish mantig'i ishlaganini BAZADAGI yozuvlar
        // borligi bilan isbotlaymiz — bu qaysi bosqichda to'lganidan qat'i nazar
        // to'g'ri tekshiruv.
        if (birinchi.MissingFiles < 8)
        {
            await using var tekshiruv = _factory.Services.CreateAsyncScope();
            var kontekst = tekshiruv.ServiceProvider.GetRequiredService<AppDbContext>();

            var yozuvlar = await kontekst.Directions.CountAsync(bekorQilish);
            yozuvlar.ShouldBeGreaterThan(
                0,
                "fayl topilgan bo'lsa, undan yozuv o'qilib bazaga tushishi kerak");
        }

        await using var ikkinchiQamrov = _factory.Services.CreateAsyncScope();
        var ikkinchiUrugchi = ikkinchiQamrov.ServiceProvider.GetRequiredService<CatalogSeeder>();

        var ikkinchi = await ikkinchiUrugchi.SeedAsync(bekorQilish);

        ikkinchi.Created.ShouldBe(0, "urug'lantirish mavjud yozuvlarga tegmasligi kerak");

        // Birinchi o'tishda yaratilganlarning HAMMASI ikkinchi o'tishda
        // "mavjud" deb tanilishi kerak — aks holda idempotentlik yolg'on
        ikkinchi.Skipped.ShouldBe(birinchi.Created + birinchi.Skipped);
        ikkinchi.MissingFiles.ShouldBe(birinchi.MissingFiles);
    }

    /// <summary>
    /// «Yo'qlik turlari» ham urug'lanadi — aks holda xodim ilovasida so'rov
    /// yuborib bo'lmaydi.
    /// </summary>
    /// <remarks>
    /// 2026-10-17 dagi xato: ma'lumotnoma urug'lanmasdi va prod'da bo'sh
    /// qoldi — ilovadagi «Yo'qlik turi» varag'i bo'sh ochilardi. Urug' baza
    /// bo'sh bo'lgandagina quyiladi va dastur ishga tushganda ishlaydi,
    /// shuning uchun bu yerda faqat yozuvlar BORLIGI tekshiriladi.
    /// 2026-10-20 dan urug' — Verifix'dagi aynan ikki tur (SPEC-erkin-jadval §6.2):
    /// eski «Kasallik» va «Shaxsiy sabab» urug'da yo'q. Aniq son tekshirilmaydi —
    /// boshqa testlar shu bazaga o'z turlarini qo'shadi.
    /// </remarks>
    [Fact]
    public async Task Yoqlik_turlari_urugdan_toldiriladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var turlar = await kontekst.AbsenceTypes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(tur => new { tur.Name, tur.CountsAsWorkTime })
            .ToListAsync(bekorQilish);
        var nomlar = turlar.Select(tur => tur.Name).ToList();

        turlar.ShouldContain(
            tur => tur.Name == "Ishxona ishi bilan" && tur.CountsAsWorkTime,
            "«Ishxona ishi bilan» — ish vaqti hisoblanadigan tur");
        nomlar.ShouldContain("Shaxsiy");
        nomlar.ShouldNotContain("Kasallik");
        nomlar.ShouldNotContain("Shaxsiy sabab");
    }

    /// <summary>
    /// Urug'lantirishdan keyin bo'limlar daraxti to'liq hisoblangan bo'lishi kerak.
    /// </summary>
    /// <remarks>
    /// Fayllar tarkibiga bog'lanmagan tekshiruv: <b>otasi bor har bir bo'lim</b>
    /// darajasi 1 dan kam bo'lmasligi va yo'lida ajratgich bo'lishi shart.
    /// Bu <c>RebuildHierarchyAsync</c> urug'lantirishdan keyin haqiqatan
    /// chaqirilganini isbotlaydi.
    /// </remarks>
    [Fact]
    public async Task Katalog_urugidan_keyin_bolimlar_daraxti_hisoblangan_boladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();

        var urugchi = qamrov.ServiceProvider.GetRequiredService<CatalogSeeder>();
        await urugchi.SeedAsync(bekorQilish);

        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var otasiBorlar = await kontekst.Departments
            .AsNoTracking()
            .Where(bolim => bolim.ParentId != null)
            .ToListAsync(bekorQilish);

        // ⚠️ Bu tekshiruv MAJBURIY va u ataylab aniq songa bog'lanmagan.
        //
        // Urug' fayllarida bo'lim otasini va yuridik shaxsini NOM orqali
        // ko'rsatadi (ilgari qo'lda yoziladigan kod orqali edi). Nom
        // o'zgarsa yoki xarita buzilsa, bog'lanish JIMGINA yo'qoladi:
        // barcha bo'limlar ildizga aylanadi, daraxt yassilanadi va hech
        // qanday xato chiqmaydi. Pastdagi sikl esa bo'sh ro'yxatda ham
        // muvaffaqiyatli o'tib ketardi — shu sababli avval bog'lanish
        // UMUMAN borligi tasdiqlanadi.
        otasiBorlar.Count.ShouldBeGreaterThan(
            10,
            "urug' fayllaridagi ota-bola bog'lanishi nom bo'yicha topilmayapti");

        (await kontekst.Departments
            .AsNoTracking()
            .CountAsync(bolim => bolim.LegalEntityId != null, bekorQilish))
            .ShouldBeGreaterThan(
                0,
                "urug' fayllaridagi yuridik shaxs bog'lanishi nom bo'yicha topilmayapti");

        foreach (var bolim in otasiBorlar)
        {
            bolim.Level.ShouldBeGreaterThanOrEqualTo(1, bolim.Name);
            bolim.Path.ShouldNotBeNull();
            bolim.Path.ShouldContain(" / ", Case.Sensitive, bolim.Name);
        }
    }
}
