using Bonvi.Application.Dashboard.Abstractions;
using Bonvi.Application.Dashboard.Queries;
using Bonvi.Domain.Abstractions;
using NSubstitute;

namespace Bonvi.Application.Tests.Dashboard;

/// <summary>
/// Bugungi tug'ilgan kunlar ro'yxati: «bugun» soatdan olinadi, yosh esa
/// faqat ISHONCHLI sanada ko'rsatiladi.
/// </summary>
/// <remarks>
/// <para>
/// Bu yerda baza yo'q: o'qish modeli almashtirilgan (mock). KIMNI
/// ro'yxatga qo'shish sharti — SQL savoli va u uchi-uchiga testlarda
/// (<c>DashboardApiTests</c>) qadalgan, ayniqsa eng muhim da'vo: ro'yxat
/// uzunligi bosh sahifadagi <c>birthdaysToday</c> soniga teng.
/// Handler'ning o'z hissasi esa ikkitagina qaror va ular shu yerda.
/// </para>
/// <para>
/// <c>GetDashboardStatsQueryHandlerTests</c> bilan bir xil uslub: soat
/// qotirilgan sanaga qo'yiladi, aks holda test yiliga bir marta —
/// yangi yil kechasi — o'z-o'zidan yiqilardi.
/// </para>
/// </remarks>
public sealed class GetBirthdaysTodayQueryHandlerTests
{
    private static readonly DateOnly Bugun = new(2026, 8, 29);

    private readonly IDashboardReadModel _readModel = Substitute.For<IDashboardReadModel>();
    private readonly IClock _clock = Substitute.For<IClock>();

    public GetBirthdaysTodayQueryHandlerTests() => _clock.Today.Returns(Bugun);

    private GetBirthdaysTodayQueryHandler Handler => new(_readModel, _clock);

    /// <summary>«Bugun» mijozdan emas, SOATDAN olinadi.</summary>
    /// <remarks>
    /// So'rovda sana parametri umuman yo'q va bo'lmasligi ham kerak:
    /// brauzerdagi kun foydalanuvchining qurilma sozlamasiga bog'liq va
    /// u bosh sahifadagi son bilan mos kelmay qolishi mumkin edi. Soat
    /// esa Toshkent kunini beradi — ikkala so'rov uchun bitta manba.
    /// </remarks>
    [Fact]
    public async Task Bugungi_sana_soatdan_olinadi()
    {
        _readModel
            .ListBirthdaysTodayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var natija = await Handler.HandleAsync(
            new GetBirthdaysTodayQuery(),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.ShouldBeEmpty();

        await _readModel
            .Received(1)
            .ListBirthdaysTodayAsync(Bugun, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Yosh hisoblanadi, lekin SHUBHALI sanada umuman berilmaydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Excel'dan ko'chirilgan yozuvlarda kelajakdagi sana ham, 1896-yil
    /// ham uchraydi. «136 yoshga to'ldi» degan tabrik butun ro'yxatga
    /// bo'lgan ishonchni yo'qotardi, shu sababli bunday holatda yosh
    /// <c>null</c> bo'lib qaytadi va ekranda hech narsa chizilmaydi.
    /// </para>
    /// <para>
    /// Chegara tug'ilgan kun BILDIRISHNOMASI bilan bir xil
    /// (<c>0 &lt; yosh &lt; 120</c>): bitta xodim qo'ng'iroqda va bu
    /// ro'yxatda ikki xil yoshda ko'rinmasligi kerak.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Shubhali_tugilgan_sanada_yosh_berilmaydi()
    {
        _readModel
            .ListBirthdaysTodayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns([
                Qator("Оддий", new DateOnly(1996, 8, 29)),
                Qator("Келажак", new DateOnly(2030, 8, 29)),
                Qator("Жудаэски", new DateOnly(1896, 8, 29)),
                Qator("Шуйилги", new DateOnly(2026, 8, 29)),
            ]);

        var natija = await Handler.HandleAsync(
            new GetBirthdaysTodayQuery(),
            TestContext.Current.CancellationToken);

        var royxat = natija.Value;
        royxat.Count.ShouldBe(4);

        royxat[0].Age.ShouldBe(30);

        // Kelajakdagi sana — manfiy yosh; 130 — chegaradan tashqarida;
        // shu yilgi sana — nol. Uchalasi ham ko'rsatilmaydi.
        royxat[1].Age.ShouldBeNull();
        royxat[2].Age.ShouldBeNull();
        royxat[3].Age.ShouldBeNull();

        // Shubhali sana YOZUVNI ro'yxatdan chiqarib tashlamaydi: odam
        // bugun ishga keladi va uni tabriklash kerak — faqat yoshi
        // aytilmaydi.
        royxat[1].FullName.ShouldBe("Келажак");
    }

    /// <summary>Maydonlar joyi almashib ketmaganini qadaydi.</summary>
    /// <remarks>
    /// Lavozim va bo'lim — ikkalasi ham <c>string?</c> va yonma-yon
    /// turadi; almashib qolsa kompilyator jim qolardi, ekranda esa
    /// «Sotuv bo'limi · Menejer» o'rniga teskarisi chiqardi.
    /// </remarks>
    [Fact]
    public async Task Qator_maydonlari_ozgarmasdan_otadi()
    {
        var xodimId = Guid.NewGuid();

        _readModel
            .ListBirthdaysTodayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns([
                new BirthdayEmployeeRow(
                    xodimId,
                    "Намунаев Дилшод",
                    "photo/1.jpg",
                    "Менежер",
                    "Сотув бўлими",
                    new DateOnly(1996, 8, 29)),
            ]);

        var natija = await Handler.HandleAsync(
            new GetBirthdaysTodayQuery(),
            TestContext.Current.CancellationToken);

        var qator = natija.Value.ShouldHaveSingleItem();
        qator.Id.ShouldBe(xodimId);
        qator.FullName.ShouldBe("Намунаев Дилшод");
        qator.PhotoKey.ShouldBe("photo/1.jpg");
        qator.PositionName.ShouldBe("Менежер");
        qator.DepartmentName.ShouldBe("Сотув бўлими");
        qator.BirthDate.ShouldBe(new DateOnly(1996, 8, 29));
    }

    /// <summary>Eng qisqa qator — testda faqat ism va sana muhim bo'lganda.</summary>
    private static BirthdayEmployeeRow Qator(string ism, DateOnly tugilgan)
        => new(Guid.NewGuid(), ism, null, null, null, tugilgan);
}
