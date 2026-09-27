using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Eski <c>employment.manage</c> sakkizta amal kodiga bo'lingani va «pul
/// qoidasi» — HTTP qatlamida.
/// </summary>
/// <remarks>
/// <para>
/// Egasining maqsadi: «Kadr bo'limi boshlig'i» rolini u o'zi tuzadi —
/// oklad, bo'shatish, sinov yakuni faqat unda; HR menejeri esa ko'chiradi,
/// qo'shimcha vazifa beradi, sinovni uzaytiradi. Bu testlar aynan shu
/// bo'linish HAQIQATAN ishlashini isbotlaydi: har bir endpoint o'z kodini
/// talab qiladi va amal ichidagi summa <c>employment.salary</c> siz o'tmaydi.
/// </para>
/// <para>
/// «Ruxsat bor» holatida so'rov mavjud bo'lmagan yozuvga yuboriladi va
/// 404/400 kutiladi: bu avtorizatsiyadan O'TGANINI ko'rsatadi, holatni esa
/// buzmaydi (bo'shatish yoki sinov yakuni boshqa testlarning xodimini
/// o'zgartirib yubormaydi).
/// </para>
/// </remarks>
public sealed class EmploymentPermissionApiTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public EmploymentPermissionApiTests(BonviApiFactory factory) => _factory = factory;

    /// <summary>
    /// Shartnoma jadvali: endpoint → uning YAGONA amal kodi. So'rov tanasi
    /// summasiz — pul qoidasi bu testga aralashmasin.
    /// </summary>
    public static TheoryData<string, string, string, string> Amallar => new()
    {
        { Permissions.EmploymentHire, "POST", "/api/employments/hire", "{}" },
        { Permissions.EmploymentTransfer, "POST", "/api/employments/{id}/transfer", "{}" },
        { Permissions.EmploymentDuty, "POST", "/api/employments/{id}/additional-duty", """{"duty":"Navbatchilik"}""" },
        { Permissions.EmploymentSalary, "POST", "/api/employments/{id}/salary", """{"amount":5000000}""" },
        { Permissions.EmploymentTerminate, "POST", "/api/employments/{id}/terminate", "{}" },
        { Permissions.EmploymentCorrect, "PUT", "/api/employments/{id}/events/{id}", """{"orderNo":"1-k"}""" },
        { Permissions.EmploymentProbationExtend, "POST", "/api/probations/{id}/extend", """{"newEndDate":"2030-01-01"}""" },
        { Permissions.EmploymentProbationComplete, "POST", "/api/probations/{id}/resolve", """{"status":"Passed"}""" },
        { Permissions.EmploymentProbationComplete, "POST", "/api/probations/missing/confirm", """{"employmentIds":[]}""" },
    };

    /// <summary>
    /// Har bir amal O'Z kodini talab qiladi: qolgan yettitasi bor, faqat shu
    /// yo'q — 403; faqat shu bor — avtorizatsiyadan o'tadi.
    /// </summary>
    [Theory]
    [MemberData(nameof(Amallar))]
    public async Task Har_bir_amal_oz_ruxsatini_talab_qiladi(string kod, string usul, string manzil, string tana)
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var yol = manzil.Replace("{id}", Guid.NewGuid().ToString(), StringComparison.Ordinal);

        var qolganlari = Permissions.EmploymentActions.Where(amal => amal != kod).Append(Permissions.EmploymentView).ToArray();

        using (var ruxsatsiz = await MijozAsync(qolganlari))
        using (var javob = await YuborAsync(ruxsatsiz, usul, yol, tana, bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden, $"«{kod}» siz: {await MatnAsync(javob)}");
            (await XatoKodiAsync(javob)).ShouldBe("auth.ruxsat_yoq");
        }

        using (var ruxsatli = await MijozAsync(kod, Permissions.EmploymentView))
        using (var javob = await YuborAsync(ruxsatli, usul, yol, tana, bekorQilish))
        {
            javob.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden, $"«{kod}» bilan: {await MatnAsync(javob)}");
            ((int)javob.StatusCode).ShouldBeLessThan(500, await MatnAsync(javob));
        }
    }

    /// <summary>
    /// Summasiz tuzatish ham okladni aylanib o'ta olmaydi: eski oklad
    /// yozuvining sanasi eng keyingisidan keyinga surilsa, JORIY oklad o'sha
    /// yozuvnikiga qaytardi. «employment.salary» siz bu rad etiladi va hech
    /// narsa saqlanmaydi; ruxsat bilan esa o'tadi.
    /// </summary>
    [Fact]
    public async Task Sanani_surib_okladni_aylanib_otib_bolmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var urug = await UrugAsync();

        using (var oklad = await MijozAsync(Permissions.EmploymentView, Permissions.EmploymentSalary))
        using (var javob = await oklad.PostAsJsonAsync(
            new Uri($"/api/employments/{urug.EmploymentId}/salary", UriKind.Relative),
            new { amount = 6_000_000m, effectiveDate = new DateOnly(2024, 6, 1) },
            bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        }

        var yol = $"/api/employments/{urug.EmploymentId}/events/{urug.OkladHodisasi}";
        var tana = """{"effectiveDate":"2024-07-01"}""";

        // Summasiz, sanasi o'zgarmagan tuzatish (buyruq raqami) oklad
        // ruxsatisiz ham o'tadi — «employment.correct» ning asosiy ishi
        using (var faqatTuzatish = await MijozAsync(Permissions.EmploymentView, Permissions.EmploymentCorrect))
        using (var javob = await YuborAsync(
            faqatTuzatish, "PUT", yol, """{"effectiveDate":"2024-03-01","orderNo":"T-9"}""", bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        }

        using (var faqatTuzatish = await MijozAsync(Permissions.EmploymentView, Permissions.EmploymentCorrect))
        using (var javob = await YuborAsync(faqatTuzatish, "PUT", yol, tana, bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await MatnAsync(javob));
            (await XatoKodiAsync(javob)).ShouldBe("auth.ruxsat_yoq");
        }

        (await HolatAsync(urug.EmploymentId)).Salary!.Amount.ShouldBe(6_000_000m, "rad etilgan tuzatish saqlanmagan");

        using (var ikkalasi = await MijozAsync(
            Permissions.EmploymentView, Permissions.EmploymentCorrect, Permissions.EmploymentSalary))
        using (var javob = await YuborAsync(ikkalasi, "PUT", yol, tana, bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        }

    }

    /// <summary>
    /// «person» bilan qabul YANGI xodim kartasini yaratadi — «employment.hire»
    /// ning o'zi yetmaydi, «employees.create» ham shart.
    /// </summary>
    [Fact]
    public async Task Yangi_karta_bilan_qabul_xodim_qoshish_ruxsatini_talab_qiladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        const string yol = "/api/employments/hire";
        const string tana = """{"person":{"lastName":"Рухсатов","firstName":"Янги"}}""";

        using (var faqatQabul = await MijozAsync(Permissions.EmploymentView, Permissions.EmploymentHire))
        using (var javob = await YuborAsync(faqatQabul, "POST", yol, tana, bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await MatnAsync(javob));
            (await XatoKodiAsync(javob)).ShouldBe("auth.ruxsat_yoq");
        }

        using (var ikkalasi = await MijozAsync(
            Permissions.EmploymentView, Permissions.EmploymentHire, Permissions.EmployeesCreate))
        using (var javob = await YuborAsync(ikkalasi, "POST", yol, tana, bekorQilish))
        {
            javob.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden, await MatnAsync(javob));
            ((int)javob.StatusCode).ShouldBeLessThan(500, await MatnAsync(javob));
        }
    }

    /// <summary>
    /// Nomzodni ishga olish ham «Ishga qabul qilish» ruxsatini talab qiladi:
    /// u xodim, mehnat munosabati va buyruq yaratadi. Faqat rekruting ruxsati
    /// bilan bu yo'l orqali qabul ruxsatini chetlab o'tib bo'lmasligi kerak.
    /// </summary>
    [Fact]
    public async Task Nomzodni_ishga_olish_qabul_ruxsatisiz_yopiq()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var yol = $"/api/recruiting/candidates/{Guid.NewGuid()}/hire";
        const string tana = "{}";

        using (var faqatRekruting = await MijozAsync(Permissions.RecruitingView, Permissions.RecruitingManage))
        using (var javob = await YuborAsync(faqatRekruting, "POST", yol, tana, bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await MatnAsync(javob));
            (await XatoKodiAsync(javob)).ShouldBe("auth.ruxsat_yoq");
        }

        using (var ikkalasi = await MijozAsync(
            Permissions.RecruitingView, Permissions.RecruitingManage, Permissions.EmploymentHire,
            Permissions.EmployeesCreate))
        using (var javob = await YuborAsync(ikkalasi, "POST", yol, tana, bekorQilish))
        {
            javob.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden, await MatnAsync(javob));
            ((int)javob.StatusCode).ShouldBeLessThan(500, await MatnAsync(javob));
        }
    }

    /// <summary>
    /// Shartnomadagi asosiy misol: ko'chirish ruxsati bor, oklad ruxsati yo'q —
    /// okladsiz ko'chiradi, ko'chirish ichida oklad yubora olmaydi, «/salary» ga
    /// esa umuman yetmaydi. Rad etilgan so'rov hech narsani o'zgartirmaydi.
    /// </summary>
    [Fact]
    public async Task Kochirish_ruxsati_okladsiz_ishlaydi_okladga_esa_yetmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var urug = await UrugAsync();

        using var client = await MijozAsync(Permissions.EmploymentView, Permissions.EmploymentTransfer);

        using (var javob = await client.PostAsJsonAsync(
            new Uri($"/api/employments/{urug.EmploymentId}/transfer", UriKind.Relative),
            new { departmentId = urug.IkkinchiBolim, orderNo = "K-1" },
            bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        }

        using (var javob = await client.PostAsJsonAsync(
            new Uri($"/api/employments/{urug.EmploymentId}/transfer", UriKind.Relative),
            new { departmentId = urug.BirinchiBolim, salaryAmount = 9_000_000m },
            bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await MatnAsync(javob));
            (await XatoKodiAsync(javob)).ShouldBe("auth.ruxsat_yoq");
        }

        using (var javob = await client.PostAsJsonAsync(
            new Uri($"/api/employments/{urug.EmploymentId}/salary", UriKind.Relative),
            new { amount = 9_000_000m },
            bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await MatnAsync(javob));
            (await XatoKodiAsync(javob)).ShouldBe("auth.ruxsat_yoq");
        }

        var holat = await HolatAsync(urug.EmploymentId);
        holat.DepartmentId.ShouldBe(urug.IkkinchiBolim, "rad etilgan ko'chirish bo'limni o'zgartirmasligi kerak");
        holat.Salary!.Amount.ShouldBe(4_500_000m, "rad etilgan so'rov okladni o'zgartirmasligi kerak");

        // Oklad ruxsati qo'shilsa — xuddi o'sha so'rov o'tadi
        using var okladli = await MijozAsync(
            Permissions.EmploymentView, Permissions.EmploymentTransfer, Permissions.EmploymentSalary);

        using (var javob = await okladli.PostAsJsonAsync(
            new Uri($"/api/employments/{urug.EmploymentId}/transfer", UriKind.Relative),
            new { departmentId = urug.BirinchiBolim, salaryAmount = 9_000_000m },
            bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        }

        (await HolatAsync(urug.EmploymentId)).Salary!.Amount.ShouldBe(9_000_000m);
    }

    /// <summary>
    /// Yozuvni tuzatish: summasiz — faqat <c>employment.correct</c>, summa
    /// bilan — qo'shimcha <c>employment.salary</c>.
    /// </summary>
    [Fact]
    public async Task Yozuvni_tuzatishda_summa_oklad_ruxsatini_talab_qiladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var urug = await UrugAsync();
        var manzil = new Uri($"/api/employments/{urug.EmploymentId}/events/{urug.OkladHodisasi}", UriKind.Relative);

        using var tuzatuvchi = await MijozAsync(Permissions.EmploymentView, Permissions.EmploymentCorrect);

        using (var javob = await tuzatuvchi.PutAsJsonAsync(
            manzil,
            new { effectiveDate = urug.OkladSanasi, orderNo = "T-2" },
            bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        }

        using (var javob = await tuzatuvchi.PutAsJsonAsync(
            manzil,
            new { effectiveDate = urug.OkladSanasi, orderNo = "T-2", amount = 7_000_000m },
            bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await MatnAsync(javob));
            (await XatoKodiAsync(javob)).ShouldBe("auth.ruxsat_yoq");
        }

        (await HolatAsync(urug.EmploymentId)).Salary!.Amount.ShouldBe(4_500_000m);

        using var okladli = await MijozAsync(
            Permissions.EmploymentView, Permissions.EmploymentCorrect, Permissions.EmploymentSalary);

        using (var javob = await okladli.PutAsJsonAsync(
            manzil,
            new { effectiveDate = urug.OkladSanasi, orderNo = "T-2", amount = 7_000_000m },
            bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        }

        (await HolatAsync(urug.EmploymentId)).Salary!.Amount.ShouldBe(7_000_000m);
    }

    /// <summary>
    /// Qo'shimcha vazifa to'lovi va sinov yakunidagi oklad ham pul qoidasiga
    /// tushadi — ular summasiz yuborilsa esa oklad ruxsati kerak emas.
    /// </summary>
    /// <remarks>
    /// Pul tekshiruvi har qanday o'qishdan OLDIN: yozuv mavjud bo'lmasa ham
    /// avval 403 qaytadi — ya'ni ruxsatsiz odam summa yuborib, yozuv bor-yo'qligini
    /// ham bilib ololmaydi.
    /// </remarks>
    [Fact]
    public async Task Vazifa_tolovi_va_sinov_yakunidagi_oklad_ham_oklad_ruxsatini_talab_qiladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var yoq = Guid.NewGuid();

        using var client = await MijozAsync(
            Permissions.EmploymentView, Permissions.EmploymentDuty, Permissions.EmploymentProbationComplete);

        using (var javob = await client.PostAsJsonAsync(
            new Uri($"/api/employments/{yoq}/additional-duty", UriKind.Relative),
            new { duty = "Navbatchilik", extraSalaryAmount = 500_000m },
            bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await MatnAsync(javob));
            (await XatoKodiAsync(javob)).ShouldBe("auth.ruxsat_yoq");
        }

        using (var javob = await client.PostAsJsonAsync(
            new Uri($"/api/employments/{yoq}/additional-duty", UriKind.Relative),
            new { duty = "Navbatchilik" },
            bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.NotFound, await MatnAsync(javob));
        }

        using (var javob = await client.PostAsJsonAsync(
            new Uri($"/api/probations/{yoq}/resolve", UriKind.Relative),
            new { status = "Passed", salaryAmount = 6_000_000m },
            bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await MatnAsync(javob));
            (await XatoKodiAsync(javob)).ShouldBe("auth.ruxsat_yoq");
        }

        using (var javob = await client.PostAsJsonAsync(
            new Uri($"/api/probations/{yoq}/resolve", UriKind.Relative),
            new { status = "Passed" },
            bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.NotFound, await MatnAsync(javob));
        }
    }

    /// <summary>
    /// Okladi o'zgarmagan ko'chirishning sanasini tuzatish — oddiy hujjat
    /// tuzatishi: summa unda faqat o'sha kungi okladning nusxasi. Pul qarori
    /// (qo'shimcha vazifa haqi) sanasi esa hamon oklad ruxsatini talab qiladi.
    /// </summary>
    [Fact]
    public async Task Kochirish_sanasi_oklad_ruxsatisiz_tuzatiladi_vazifa_haqi_sanasi_esa_yoq()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var urug = await UrugAsync();

        using (var ikkalasi = await MijozAsync(
            Permissions.EmploymentView, Permissions.EmploymentTransfer, Permissions.EmploymentDuty,
            Permissions.EmploymentSalary, Permissions.SalaryView, Permissions.SalaryExtraView))
        {
            using (var javob = await ikkalasi.PostAsJsonAsync(
                new Uri($"/api/employments/{urug.EmploymentId}/transfer", UriKind.Relative),
                new { departmentId = urug.IkkinchiBolim, effectiveDate = new DateOnly(2024, 5, 1) },
                bekorQilish))
            {
                javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
            }

            using (var javob = await ikkalasi.PostAsJsonAsync(
                new Uri($"/api/employments/{urug.EmploymentId}/additional-duty", UriKind.Relative),
                new { duty = "Navbatchilik", extraSalaryAmount = 1_000_000m, effectiveDate = new DateOnly(2024, 6, 1) },
                bekorQilish))
            {
                javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
            }
        }

        var hodisalar = (await HodisalarAsync(urug.EmploymentId)).ToDictionary(hodisa => hodisa.Type);
        var kochirish = hodisalar[EmploymentEventType.Transfer];
        var vazifa = hodisalar[EmploymentEventType.AdditionalDuty];

        using var faqatTuzatish = await MijozAsync(Permissions.EmploymentView, Permissions.EmploymentCorrect);

        // Lenta interfeysga qaysi yozuvning sanasi qulflanganini aytadi —
        // «salary.view» siz summa yashirin, klient buni o'zi bila olmaydi.
        var qulflar = await QulflarAsync(faqatTuzatish, urug.XodimId);
        qulflar[kochirish.Id].ShouldBeFalse("okladi o'zgarmagan ko'chirish — pul qarori emas");
        qulflar[vazifa.Id].ShouldBeTrue("vazifa haqi — pul qarori");
        qulflar[urug.OkladHodisasi].ShouldBeTrue();

        // Oklad ruxsati bor, qo'shimcha haqniki yo'q — vazifa haqi sanasi hamon yopiq
        using (var okladli = await MijozAsync(Permissions.EmploymentView, Permissions.EmploymentSalary))
        {
            var okladQulflari = await QulflarAsync(okladli, urug.XodimId);
            okladQulflari[urug.OkladHodisasi].ShouldBeFalse();
            okladQulflari[kochirish.Id].ShouldBeFalse();
            okladQulflari[vazifa.Id].ShouldBeTrue("qo'shimcha haq — alohida ruxsat (salary.extra.view)");
        }

        using (var toliqHuquq = await MijozAsync(
            Permissions.EmploymentView, Permissions.EmploymentSalary, Permissions.SalaryView,
            Permissions.SalaryExtraView))
        {
            (await QulflarAsync(toliqHuquq, urug.XodimId)).Values.ShouldAllBe(qulf => !qulf);
        }

        using (var javob = await YuborAsync(
            faqatTuzatish,
            "PUT",
            $"/api/employments/{urug.EmploymentId}/events/{kochirish.Id}",
            """{"effectiveDate":"2024-05-02"}""",
            bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        }

        using (var javob = await YuborAsync(
            faqatTuzatish,
            "PUT",
            $"/api/employments/{urug.EmploymentId}/events/{vazifa.Id}",
            """{"effectiveDate":"2024-06-02"}""",
            bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await MatnAsync(javob));
            (await XatoKodiAsync(javob)).ShouldBe("auth.ruxsat_yoq");
        }

        (await HolatAsync(urug.EmploymentId)).Salary!.Amount.ShouldBe(4_500_000m);
    }

    /// <summary>
    /// ⭐ Xato (2026-09-27): qo'shimcha vazifa yozuvining buyruq raqamini
    /// tuzatish xodimning okladini vazifa haqiga almashtirib yuborardi —
    /// xatosiz, jimgina. Oklad ruxsati bor foydalanuvchida ham bu yuz
    /// bermasligi kerak.
    /// </summary>
    [Fact]
    public async Task Qoshimcha_vazifa_yozuvini_tuzatish_okladni_almashtirmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var urug = await UrugAsync();

        using var toliq = await MijozAsync(
            Permissions.EmploymentView, Permissions.EmploymentDuty, Permissions.EmploymentCorrect,
            Permissions.EmploymentSalary, Permissions.SalaryView, Permissions.SalaryExtraView);

        using (var javob = await toliq.PostAsJsonAsync(
            new Uri($"/api/employments/{urug.EmploymentId}/additional-duty", UriKind.Relative),
            new { duty = "Omborga mas'ul", extraSalaryAmount = 1_000_000m, effectiveDate = new DateOnly(2024, 6, 1) },
            bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        }

        var vazifa = (await HodisalarAsync(urug.EmploymentId))
            .Single(hodisa => hodisa.Type == EmploymentEventType.AdditionalDuty);

        using (var javob = await YuborAsync(
            toliq,
            "PUT",
            $"/api/employments/{urug.EmploymentId}/events/{vazifa.Id}",
            """{"effectiveDate":"2024-06-01","orderNo":"V-7"}""",
            bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        }

        (await HolatAsync(urug.EmploymentId)).Salary!.Amount.ShouldBe(4_500_000m, "oklad vazifa haqiga almashmaydi");
    }

    /// <summary>
    /// Sinov yakunidagi yangi lavozim yoki bo'lim — bu ko'chirish: u
    /// «employment.transfer» ni talab qiladi, xuddi summa «employment.salary»
    /// ni talab qilgani kabi. Tekshiruv har qanday o'qishdan OLDIN.
    /// </summary>
    [Fact]
    public async Task Sinov_yakunidagi_yangi_lavozim_kochirish_ruxsatini_talab_qiladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var yol = $"/api/probations/{Guid.NewGuid()}/resolve";
        var tana = $$"""{"status":"Passed","positionId":"{{Guid.NewGuid()}}"}""";

        using (var faqatSinov = await MijozAsync(Permissions.EmploymentView, Permissions.EmploymentProbationComplete))
        using (var javob = await YuborAsync(faqatSinov, "POST", yol, tana, bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await MatnAsync(javob));
            (await XatoKodiAsync(javob)).ShouldBe("auth.ruxsat_yoq");
        }

        using (var ikkalasi = await MijozAsync(
            Permissions.EmploymentView, Permissions.EmploymentProbationComplete, Permissions.EmploymentTransfer))
        using (var javob = await YuborAsync(ikkalasi, "POST", yol, tana, bekorQilish))
        {
            javob.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden, await MatnAsync(javob));
            ((int)javob.StatusCode).ShouldBeLessThan(500, await MatnAsync(javob));
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    /// <summary>Xodim lentasidan: yozuv → sanasi qulflanganmi (so'rovchi uchun).</summary>
    private static async Task<Dictionary<Guid, bool>> QulflarAsync(HttpClient client, Guid xodimId)
    {
        using var javob = await client.GetAsync(
            new Uri($"/api/employees/{xodimId}/timeline", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        using var hujjat = await System.Text.Json.JsonDocument.ParseAsync(
            await javob.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);

        return hujjat.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("event"))
            .ToDictionary(
                hodisa => hodisa.GetProperty("id").GetGuid(),
                hodisa => hodisa.GetProperty("dateLocked").GetBoolean());
    }

    private async Task<IReadOnlyList<EmploymentEvent>> HodisalarAsync(Guid employmentId)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        return await kontekst.Set<EmploymentEvent>()
            .AsNoTracking()
            .Where(hodisa => hodisa.EmploymentId == employmentId)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private sealed record Urug(
        Guid EmploymentId,
        Guid BirinchiBolim,
        Guid IkkinchiBolim,
        Guid OkladHodisasi,
        DateOnly OkladSanasi,
        Guid XodimId);

    /// <summary>Bitta faol munosabat: ikki bo'lim, bitta lavozim, oklad 4,5 mln.</summary>
    private async Task<Urug> UrugAsync()
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var belgi = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..8];

        var firma = LegalEntity.Create("Ruxsat Test MChJ " + belgi).Value;
        var birinchi = Department.Create("Sotuv " + belgi).Value;
        var ikkinchi = Department.Create("Ombor " + belgi).Value;
        var lavozim = Position.Create("Sotuvchi " + belgi).Value;
        var xodim = Employee.Create(FullName.Create("Рухсатов", "Ходим", null).Value).Value;

        var munosabat = Employment.Hire(
            xodim.Id,
            firma.Id,
            new DateOnly(2024, 1, 15),
            birinchi.Id,
            lavozim.Id,
            orderNo: belgi + "-1").Value;

        var okladSanasi = new DateOnly(2024, 3, 1);
        munosabat.SetSalary(Money.Create(4_500_000m).Value, okladSanasi).IsSuccess.ShouldBeTrue();

        kontekst.AddRange(firma, birinchi, ikkinchi, lavozim, xodim);
        kontekst.Add(munosabat);

        await kontekst.SaveChangesAsync(TestContext.Current.CancellationToken);

        var okladHodisasi = munosabat.Events.Single(hodisa => hodisa.ToSalary is not null).Id;

        return new Urug(munosabat.Id, birinchi.Id, ikkinchi.Id, okladHodisasi, okladSanasi, xodim.Id);
    }

    private async Task<Employment> HolatAsync(Guid employmentId)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        return await kontekst.Set<Employment>()
            .AsNoTracking()
            .SingleAsync(munosabat => munosabat.Id == employmentId, TestContext.Current.CancellationToken);
    }

    private async Task<HttpClient> MijozAsync(params string[] ruxsatlar)
    {
        var token = await TestAccounts.TokenAsync(_factory, ruxsatlar);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    private static async Task<HttpResponseMessage> YuborAsync(
        HttpClient client,
        string usul,
        string yol,
        string tana,
        CancellationToken bekorQilish)
    {
        using var sorov = new HttpRequestMessage(new HttpMethod(usul), new Uri(yol, UriKind.Relative))
        {
            Content = new StringContent(tana, System.Text.Encoding.UTF8, "application/json"),
        };

        return await client.SendAsync(sorov, bekorQilish);
    }

    private static async Task<string?> XatoKodiAsync(HttpResponseMessage javob)
    {
        var muammo = await javob.Content.ReadFromJsonAsync<ProblemResponse>(TestContext.Current.CancellationToken);
        return muammo?.Title;
    }

    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
