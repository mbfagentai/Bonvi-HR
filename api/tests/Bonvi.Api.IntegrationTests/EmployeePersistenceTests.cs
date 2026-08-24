using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Xodimlar va mehnat munosabatlari qatlamining haqiqiy PostgreSQL
/// ustidagi testlari.
/// </summary>
/// <remarks>
/// <para>
/// Bu yerdagi tekshiruvlarning HECH BIRI mock bilan ko'rinmaydi: qisman
/// unikal indeks, <c>uuid[]</c> massivi ustidagi <c>Contains</c> tarjimasi,
/// <c>NULL</c> bo'la oladigan pul ustunlari va mavjud agregatga bola
/// qo'shish — hammasi bazaning va EF Core'ning haqiqiy xatti-harakati.
/// </para>
/// <para>
/// Testlar bitta bazani baham ko'radi, shu sababli har biri O'ZIGA XOS
/// JSHSHIR va ism ishlatadi — aks holda ular bir-birining unikallik
/// cheklovlariga urilardi.
/// </para>
/// </remarks>
public sealed class EmployeePersistenceTests : IClassFixture<BonviApiFactory>
{
    /// <summary>
    /// Nazorat raqami TO'G'RI JSHSHIR'lar (1990-03-15 da tug'ilgan erkak).
    /// </summary>
    /// <remarks>
    /// Ular qo'lda emas, <see cref="Pinfl"/> algoritmi bo'yicha hisoblangan:
    /// <c>Pinfl.Create</c> nazorat raqamini tekshiradi va tasodifiy raqam
    /// testni «validatsiya xatosi» bilan yiqitardi.
    /// </remarks>
    private const string BirinchiJshshir = "31503901234562";
    private const string IkkinchiJshshir = "31503902234565";
    private const string TakrorJshshir = "31503903234568";

    private static readonly DateOnly TugilganSana = new(1990, 3, 15);

    private readonly BonviApiFactory _factory;

    public EmployeePersistenceTests(BonviApiFactory factory) => _factory = factory;

    // ────────────────────────────────────────────────────────────
    //  Qiymat obyektlari
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Qiymat obyekti bazaga oddiy satr bo'lib tushadi va o'qishda
    /// <c>FromTrusted</c> orqali qayta yig'iladi.
    /// </summary>
    /// <remarks>
    /// Nega muhim: konvertor yoki taqqoslagich noto'g'ri bo'lsa, xato
    /// saqlashda EMAS, faqat qayta o'qishda ko'rinadi — masalan telefon
    /// «+998905088888» o'rniga bo'sh qaytadi.
    /// </remarks>
    [Fact]
    public async Task Xodim_qiymat_obyektlari_bilan_saqlanadi_va_qaytib_oqiladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        var xodimId = Guid.Empty;

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

            var ism = FullName.Create("Мамажонов", "Бобуржон", "Фазлитдин ўғли").Value;
            var xodim = Employee.Create(ism, TugilganSana).Value;

            xodim.SetPinfl(Pinfl.Create(BirinchiJshshir).Value).IsSuccess.ShouldBeTrue();
            xodim.SetPassport(PassportSeries.Create("AB5725936").Value);
            xodim.SetPhones(
                PhoneNumber.Create("905088888").Value,
                PhoneNumber.Create("+998 93 111 22 33").Value);

            xodimId = xodim.Id;

            kontekst.Add(xodim);
            await kontekst.SaveChangesAsync(bekorQilish);
        }

        // Yangi qamrov — xotiradagi nusxa emas, aynan bazadan o'qiladi
        await using var oqishQamrovi = _factory.Services.CreateAsyncScope();
        var xodimlar = oqishQamrovi.ServiceProvider.GetRequiredService<IEmployeeRepository>();

        var oqilgan = await xodimlar.GetByIdAsync(xodimId, bekorQilish);

        oqilgan.ShouldNotBeNull();
        oqilgan.FullName.LastName.ShouldBe("Мамажонов");
        oqilgan.FullName.FirstName.ShouldBe("Бобуржон");
        oqilgan.FullName.MiddleName.ShouldBe("Фазлитдин ўғли");
        oqilgan.Pinfl.ShouldNotBeNull();
        oqilgan.Pinfl.Value.ShouldBe(BirinchiJshshir);
        oqilgan.Passport.ShouldNotBeNull();
        oqilgan.Passport.Value.ShouldBe("AB5725936");
        oqilgan.Phone.ShouldNotBeNull();
        oqilgan.Phone.Value.ShouldBe("+998905088888");
        oqilgan.SecondPhone.ShouldNotBeNull();
        oqilgan.SecondPhone.Value.ShouldBe("+998931112233");

        // JSHSHIR va pasport bo'yicha qidiruv — qiymat obyektining O'ZI
        // solishtiriladi va bu SQL'ga tarjima bo'lishi SHART
        var jshshirBoyicha = await xodimlar.FindByPinflAsync(
            Pinfl.Create(BirinchiJshshir).Value,
            bekorQilish);
        jshshirBoyicha.ShouldNotBeNull();
        jshshirBoyicha.Id.ShouldBe(xodimId);

        var pasportBoyicha = await xodimlar.FindByPassportAsync(
            PassportSeries.Create("AB5725936").Value,
            bekorQilish);
        pasportBoyicha.ShouldNotBeNull();
        pasportBoyicha.Id.ShouldBe(xodimId);

        // Ism va sana bo'yicha zaxira yo'l
        var ismBoyicha = await xodimlar.FindByNameAndBirthDateAsync(
            FullName.Create("Мамажонов", "Бобуржон", null).Value,
            TugilganSana,
            bekorQilish);
        ismBoyicha.ShouldContain(element => element.Id == xodimId);

        (await xodimlar.PinflExistsAsync(Pinfl.Create(BirinchiJshshir).Value, cancellationToken: bekorQilish))
            .ShouldBeTrue();

        // Yozuvning O'ZI takror hisoblanmaydi — tahrirlashda shu kerak
        (await xodimlar.PinflExistsAsync(Pinfl.Create(BirinchiJshshir).Value, xodimId, bekorQilish))
            .ShouldBeFalse();

        // Notanish JSHSHIR — hech qanday yozuv topilmasligi kerak
        (await xodimlar.PinflExistsAsync(Pinfl.Create(IkkinchiJshshir).Value, cancellationToken: bekorQilish))
            .ShouldBeFalse();
        (await xodimlar.FindByPinflAsync(Pinfl.Create(IkkinchiJshshir).Value, bekorQilish))
            .ShouldBeNull();
    }

    // ────────────────────────────────────────────────────────────
    //  JSHSHIR unikalligi — qisman indeks
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// JSHSHIR'i bo'sh ikki xodim yonma-yon yashay oladi.
    /// </summary>
    /// <remarks>
    /// Excel'dagi 540 yozuvdan 3 tasida JSHSHIR umuman yo'q. Unikal indeks
    /// qisman bo'lmasa (<c>WHERE pinfl IS NOT NULL</c>), ikkinchi bo'sh
    /// yozuv rad etilib, ko'chirish to'xtab qolardi.
    /// </remarks>
    [Fact]
    public async Task JSHSHIRsiz_ikki_xodim_yonma_yon_saqlanadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var birinchi = Employee.Create(FullName.Create("Жшшрсиз", "Биринчи", null).Value).Value;
        var ikkinchi = Employee.Create(FullName.Create("Жшшрсиз", "Иккинчи", null).Value).Value;

        birinchi.Pinfl.ShouldBeNull();
        ikkinchi.Pinfl.ShouldBeNull();

        kontekst.AddRange(birinchi, ikkinchi);

        // Xato bo'lmasligi — testning butun mazmuni shu
        await kontekst.SaveChangesAsync(bekorQilish);

        (await kontekst.Employees.CountAsync(
            xodim => xodim.Pinfl == null && xodim.FullName.LastName == "Жшшрсиз",
            bekorQilish))
            .ShouldBe(2);
    }

    /// <summary>Bir xil JSHSHIR ikki xodimga berilmaydi.</summary>
    [Fact]
    public async Task Bir_xil_JSHSHIR_ikki_marta_saqlanmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

            var xodim = Employee.Create(FullName.Create("Такрор", "Биринчи", null).Value, TugilganSana).Value;
            xodim.SetPinfl(Pinfl.Create(TakrorJshshir).Value);

            kontekst.Add(xodim);
            await kontekst.SaveChangesAsync(bekorQilish);
        }

        // Yangi qamrov: birinchi saqlash muvaffaqiyatli tugagani aniq bo'lsin
        await using var ikkinchiQamrov = _factory.Services.CreateAsyncScope();
        var ikkinchiKontekst = ikkinchiQamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var takroriy = Employee.Create(FullName.Create("Такрор", "Иккинчи", null).Value, TugilganSana).Value;
        takroriy.SetPinfl(Pinfl.Create(TakrorJshshir).Value);
        ikkinchiKontekst.Add(takroriy);

        var xato = await Should.ThrowAsync<DbUpdateException>(
            async () => await ikkinchiKontekst.SaveChangesAsync(bekorQilish));

        xato.InnerException.ShouldNotBeNull();
        xato.InnerException.Message.ShouldContain("ix_employees_pinfl");
    }

    // ────────────────────────────────────────────────────────────
    //  Yo'nalishlar massivi
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// <c>uuid[]</c> massivi saqlanadi va <c>Contains</c> SQL'ga tarjima bo'ladi.
    /// </summary>
    /// <remarks>
    /// Tarjima ISBOTI — so'rovning bajarilishining o'zi: EF ifodani SQL'ga
    /// aylantira olmasa, u jimgina xotirada hisoblamaydi, balki
    /// <c>InvalidOperationException</c> tashlaydi. Qo'shimcha ravishda
    /// hosil bo'lgan SQL matni ham tekshiriladi.
    /// </remarks>
    [Fact]
    public async Task Yonalishlar_massivi_saqlanadi_va_Contains_SQLga_tarjima_boladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        var birinchiYonalish = Direction.Create("Йўналиш Тест Бир", "yonalish-test-1").Value;
        var ikkinchiYonalish = Direction.Create("Йўналиш Тест Икки", "yonalish-test-2").Value;
        var xodimId = Guid.Empty;

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

            var xodim = Employee.Create(FullName.Create("Йўналишли", "Ходим", null).Value).Value;
            xodim.AssignDirection(birinchiYonalish.Id);
            xodim.AssignDirection(ikkinchiYonalish.Id);
            xodimId = xodim.Id;

            kontekst.AddRange(birinchiYonalish, ikkinchiYonalish);
            kontekst.Add(xodim);
            await kontekst.SaveChangesAsync(bekorQilish);
        }

        await using var oqishQamrovi = _factory.Services.CreateAsyncScope();
        var oqishKonteksti = oqishQamrovi.ServiceProvider.GetRequiredService<AppDbContext>();

        var oqilgan = await oqishKonteksti.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(xodim => xodim.Id == xodimId, bekorQilish);

        oqilgan.ShouldNotBeNull();
        oqilgan.DirectionIds.Count.ShouldBe(2);
        oqilgan.DirectionIds.ShouldContain(birinchiYonalish.Id);
        oqilgan.DirectionIds.ShouldContain(ikkinchiYonalish.Id);

        var soragan = oqishKonteksti.Employees
            .AsNoTracking()
            .Where(xodim => xodim.DirectionIds.Contains(birinchiYonalish.Id));

        soragan.ToQueryString().ShouldContain("direction_ids");

        var topilganlar = await soragan.ToListAsync(bekorQilish);
        topilganlar.ShouldContain(element => element.Id == xodimId);
    }

    // ────────────────────────────────────────────────────────────
    //  Mehnat munosabati va uning hodisalari
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Munosabat hodisalari bilan saqlanadi va MAVJUD munosabatga yangi
    /// hodisa qo'shiladi.
    /// </summary>
    /// <remarks>
    /// ⚠️ Testning ikkinchi yarmi — <c>AppDbContext</c> dagi
    /// <c>ChangeTracker.Tracked</c> ishlovchisining sinovi. Identifikatorni
    /// domen bergani uchun EF yangi hodisani «mavjud yozuv» deb hisoblab
    /// INSERT o'rniga UPDATE yuborardi va
    /// «expected to affect 1 row(s), but actually affected 0» xatosi chiqardi.
    /// Bu faqat MAVJUD agregatga bola qo'shilganda yuzaga chiqadi — shu
    /// sababli birinchi saqlash bilan ikkinchisi ATAYLAB alohida qamrovda.
    /// </remarks>
    [Fact]
    public async Task Mavjud_munosabatga_yangi_hodisa_qoshiladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        var munosabatId = Guid.Empty;

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

            var firma = LegalEntity.Create("Ҳодиса Тест МЧЖ", "hodisa-firma").Value;
            var xodim = Employee.Create(FullName.Create("Ҳодисали", "Ходим", null).Value).Value;

            var munosabat = Employment.Hire(
                xodim.Id,
                firma.Id,
                new DateOnly(2024, 1, 15),
                orderNo: "12-б").Value;

            munosabatId = munosabat.Id;

            kontekst.AddRange(firma, xodim);
            kontekst.Add(munosabat);
            await kontekst.SaveChangesAsync(bekorQilish);
        }

        // Ishga qabul hodisasi saqlangan bo'lishi kerak
        await using (var tekshiruvQamrovi = _factory.Services.CreateAsyncScope())
        {
            var munosabatlar = tekshiruvQamrovi.ServiceProvider.GetRequiredService<IEmploymentRepository>();

            var oqilgan = await munosabatlar.GetWithEventsAsync(munosabatId, bekorQilish);

            oqilgan.ShouldNotBeNull();
            oqilgan.Events.Count.ShouldBe(1);
            oqilgan.Events[0].Type.ShouldBe(EmploymentEventType.Hire);
            oqilgan.Events[0].OrderNo.ShouldBe("12-б");

            // Oklad ko'rsatilmagan — pul ustunlari NULL
            oqilgan.Salary.ShouldBeNull();
        }

        // ⚠️ MAVJUD munosabatga yangi hodisa — asosiy tekshiruv
        await using (var ozgartirishQamrovi = _factory.Services.CreateAsyncScope())
        {
            var kontekst = ozgartirishQamrovi.ServiceProvider.GetRequiredService<AppDbContext>();
            var munosabatlar = ozgartirishQamrovi.ServiceProvider.GetRequiredService<IEmploymentRepository>();

            var munosabat = await munosabatlar.GetWithEventsAsync(munosabatId, bekorQilish);
            munosabat.ShouldNotBeNull();

            munosabat
                .SetSalary(Money.Create(4_500_000m).Value, new DateOnly(2024, 6, 1), "45-б")
                .IsSuccess.ShouldBeTrue();

            await kontekst.SaveChangesAsync(bekorQilish);
        }

        await using var oxirgiQamrov = _factory.Services.CreateAsyncScope();
        var oxirgiMunosabatlar = oxirgiQamrov.ServiceProvider.GetRequiredService<IEmploymentRepository>();

        var yakuniy = await oxirgiMunosabatlar.GetWithEventsAsync(munosabatId, bekorQilish);

        yakuniy.ShouldNotBeNull();
        yakuniy.Events.Count.ShouldBe(2);

        // Tartib SHART: hodisalar kuchga kirish sanasi bo'yicha o'qiladi
        yakuniy.Events[0].Type.ShouldBe(EmploymentEventType.Hire);
        yakuniy.Events[1].Type.ShouldBe(EmploymentEventType.SalaryChange);

        // Pul NULL dan qiymatga o'tdi — ikkala holat ham bitta hodisada ko'rinadi
        var okladOzgarishi = yakuniy.Events[1];
        okladOzgarishi.FromSalary.ShouldBeNull();
        okladOzgarishi.ToSalary.ShouldNotBeNull();
        okladOzgarishi.ToSalary.Amount.ShouldBe(4_500_000m);
        okladOzgarishi.ToSalary.Currency.ShouldBe(Money.Uzs);

        yakuniy.Salary.ShouldNotBeNull();
        yakuniy.Salary.Amount.ShouldBe(4_500_000m);
        yakuniy.Salary.Currency.ShouldBe(Money.Uzs);
    }

    // ────────────────────────────────────────────────────────────
    //  Stavkalar yig'indisi
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Ikkita 0.5 stavka jami 1.0 beradi.
    /// </summary>
    /// <remarks>
    /// Excel'dagi «VIKOR + Вело унион 0,5 ставка ўриндош» holati. Yig'indi
    /// bazada hisoblanadi, shu sababli test <c>SUM(rate)</c> so'rovining
    /// haqiqatan ishlashini tekshiradi — C# dagi <c>Sum()</c> ni emas.
    /// </remarks>
    [Fact]
    public async Task Faol_stavkalar_yigindisi_bazada_hisoblanadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        var xodimId = Guid.Empty;

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

            var birinchiFirma = LegalEntity.Create("Ставка Тест ВИКОР", "stavka-vikor").Value;
            var ikkinchiFirma = LegalEntity.Create("Ставка Тест Вело", "stavka-velo").Value;
            var xodim = Employee.Create(FullName.Create("Ўриндош", "Ходим", null).Value).Value;
            xodimId = xodim.Id;

            var asosiy = Employment.Hire(xodim.Id, birinchiFirma.Id, new DateOnly(2023, 5, 1)).Value;
            asosiy.SetRate(0.5m).IsSuccess.ShouldBeTrue();

            var orindosh = Employment.Hire(xodim.Id, ikkinchiFirma.Id, new DateOnly(2023, 9, 1)).Value;
            orindosh.SetRate(0.5m).IsSuccess.ShouldBeTrue();
            orindosh.SetPrimary(false);

            // Ishdan ketgan munosabat yig'indiga QO'SHILMASLIGI kerak
            var eski = Employment.Hire(xodim.Id, birinchiFirma.Id, new DateOnly(2020, 1, 1)).Value;
            eski.Terminate(new DateOnly(2022, 12, 31), reasonId: null).IsSuccess.ShouldBeTrue();

            kontekst.AddRange(birinchiFirma, ikkinchiFirma);
            kontekst.Add(xodim);
            kontekst.AddRange(asosiy, orindosh, eski);
            await kontekst.SaveChangesAsync(bekorQilish);
        }

        await using var oqishQamrovi = _factory.Services.CreateAsyncScope();
        var munosabatlar = oqishQamrovi.ServiceProvider.GetRequiredService<IEmploymentRepository>();

        (await munosabatlar.GetTotalActiveRateAsync(xodimId, bekorQilish)).ShouldBe(1.0m);

        var faollar = await munosabatlar.GetActiveByEmployeeAsync(xodimId, bekorQilish);
        faollar.Count.ShouldBe(2);

        // Asosiy ish joyi ro'yxatda birinchi turishi kerak
        faollar[0].IsPrimary.ShouldBeTrue();

        var hammasi = await munosabatlar.GetByEmployeeAsync(xodimId, bekorQilish);
        hammasi.Count.ShouldBe(3);

        // Xodimda hech qanday munosabat bo'lmasa 0 qaytadi, xato emas
        (await munosabatlar.GetTotalActiveRateAsync(Guid.CreateVersion7(), bekorQilish)).ShouldBe(0m);
    }

    // ────────────────────────────────────────────────────────────
    //  Sinov muddati va belgilar
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// «Muddati tugashiga oz qolganlar» so'rovi va bitta munosabatga
    /// bitta sinov muddati qoidasi.
    /// </summary>
    [Fact]
    public async Task Muddati_tugayotgan_sinovlar_topiladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        var bugun = new DateOnly(2026, 3, 1);
        var yaqinId = Guid.Empty;

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

            var firma = LegalEntity.Create("Синов Тест МЧЖ", "sinov-firma").Value;
            var xodim = Employee.Create(FullName.Create("Синовдаги", "Ходим", null).Value).Value;

            var yaqinMunosabat = Employment.Hire(xodim.Id, firma.Id, bugun.AddDays(-85)).Value;
            var uzoqMunosabat = Employment.Hire(xodim.Id, firma.Id, bugun).Value;

            // Tugashiga 5 kun qolgan — eslatma ro'yxatiga tushishi kerak
            var yaqin = Probation.Start(yaqinMunosabat.Id, bugun.AddDays(-85)).Value;
            yaqinId = yaqin.Id;

            // Endigina boshlangan — hali tushmasligi kerak
            var uzoq = Probation.Start(uzoqMunosabat.Id, bugun).Value;

            kontekst.Add(firma);
            kontekst.Add(xodim);
            kontekst.AddRange(yaqinMunosabat, uzoqMunosabat);
            kontekst.AddRange(yaqin, uzoq);
            await kontekst.SaveChangesAsync(bekorQilish);
        }

        await using var oqishQamrovi = _factory.Services.CreateAsyncScope();
        var sinovlar = oqishQamrovi.ServiceProvider.GetRequiredService<IProbationRepository>();

        var yaqinlar = await sinovlar.GetEndingSoonAsync(bugun, Probation.WarningDays, bekorQilish);

        yaqinlar.ShouldContain(element => element.Id == yaqinId);
        yaqinlar.Count.ShouldBe(1);

        var topilgan = await sinovlar.GetByIdAsync(yaqinId, bekorQilish);
        topilgan.ShouldNotBeNull();

        (await sinovlar.GetByEmploymentAsync(topilgan.EmploymentId, bekorQilish))
            .ShouldNotBeNull();
    }

    /// <summary>
    /// Qora ro'yxat tekshiruvi: belgi faqat o'z muddati ichida «faol».
    /// </summary>
    /// <remarks>
    /// HR aniq so'ragan qoida: qayta ishga olishda tizim ogohlantirishi kerak.
    /// Shu sababli test faollikning IKKALA chegarasini ham tekshiradi.
    /// </remarks>
    [Fact]
    public async Task Xodim_belgilari_faollik_muddati_boyicha_filtrlanadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        var xodimId = Guid.Empty;

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

            var xodim = Employee.Create(FullName.Create("Белгили", "Ходим", null).Value).Value;
            xodimId = xodim.Id;

            // Muddatsiz qora ro'yxat — sababi majburiy
            var qoraRoyxat = EmployeeFlag.Raise(
                xodim.Id,
                EmployeeFlagType.Blacklist,
                new DateOnly(2025, 1, 1),
                "Интизом бузилиши").Value;

            // Yopilgan nazorat — 2025-yil oxirida tugagan
            var nazorat = EmployeeFlag.Raise(
                xodim.Id,
                EmployeeFlagType.Monitoring,
                new DateOnly(2025, 1, 1),
                endDate: new DateOnly(2025, 12, 31)).Value;

            kontekst.Add(xodim);
            kontekst.AddRange(qoraRoyxat, nazorat);
            await kontekst.SaveChangesAsync(bekorQilish);
        }

        await using var oqishQamrovi = _factory.Services.CreateAsyncScope();
        var belgilar = oqishQamrovi.ServiceProvider.GetRequiredService<IEmployeeFlagRepository>();

        var kun = new DateOnly(2026, 6, 1);

        (await belgilar.HasActiveFlagAsync(xodimId, EmployeeFlagType.Blacklist, kun, bekorQilish))
            .ShouldBeTrue("muddatsiz belgi doim faol");

        (await belgilar.HasActiveFlagAsync(xodimId, EmployeeFlagType.Monitoring, kun, bekorQilish))
            .ShouldBeFalse("muddati tugagan belgi faol emas");

        (await belgilar.GetByEmployeeAsync(xodimId, onlyActive: false, bekorQilish))
            .Count.ShouldBe(2);

        var qoraRoyxatdagilar = await belgilar.ListByTypeAsync(
            EmployeeFlagType.Blacklist,
            onlyActive: true,
            kun,
            bekorQilish);

        qoraRoyxatdagilar.ShouldContain(element => element.EmployeeId == xodimId);
    }

    // ────────────────────────────────────────────────────────────
    //  Trigramma indeksi
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// <c>pg_trgm</c> kengaytmasi va ism bo'yicha YIG'ILGAN trigramma
    /// indeksi migratsiyada haqiqatan yaratilgan.
    /// </summary>
    /// <remarks>
    /// Nega alohida test: kengaytma migratsiyada <c>CREATE EXTENSION</c>
    /// bilan o'rnatiladi va bu bosqich yangi muhitda jimgina o'tkazib
    /// yuborilsa, qidiruv ishlashda davom etadi — faqat sekin. Bunday
    /// nosozlik hech qachon xato bo'lib ko'rinmaydi, shu sababli u
    /// ataylab test bilan mahkamlanadi.
    /// </remarks>
    [Fact]
    public async Task Ism_boyicha_trigramma_indeksi_yaratilgan()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var kengaytmalar = await kontekst.Database
            .SqlQuery<string>($"SELECT extname AS \"Value\" FROM pg_extension WHERE extname = 'pg_trgm'")
            .ToListAsync(bekorQilish);

        kengaytmalar.ShouldContain("pg_trgm");

        var indekslar = await kontekst.Database
            .SqlQuery<string>($"SELECT indexname AS \"Value\" FROM pg_indexes WHERE tablename = 'employees'")
            .ToListAsync(bekorQilish);

        // Indeks YIG'ILGAN ifoda ustida: `translate(lower(...), 'қғҳўё', 'кгхуе')`.
        // Sabab `SearchFolding` da — rus klaviaturasida yozilgan ism ham
        // topilishi kerak. Eski `ix_employees_full_name_trgm` yig'ilmagan
        // ifoda ustida edi va so'rov o'zgargach ishlatilmay qolgandi.
        indekslar.ShouldContain("ix_employees_full_name_fold_trgm");
        indekslar.ShouldNotContain("ix_employees_full_name_trgm");
        indekslar.ShouldContain("ix_employees_last_name_first_name");
        indekslar.ShouldContain("ix_employees_pinfl");
        indekslar.ShouldContain("ix_employees_direction_ids");
    }
}
