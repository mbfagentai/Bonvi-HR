using Bonvi.Application.Abstractions;
using Bonvi.Application.Dashboard.Abstractions;
using Bonvi.Application.Dashboard.Queries;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Identity;
using NSubstitute;

namespace Bonvi.Application.Tests.Dashboard;

/// <summary>
/// Bosh sahifa sonlari: rekruting bloki RUXSATGA bog'langan.
/// </summary>
/// <remarks>
/// <para>
/// Bu qoida aynan shu yerda — handler'da — tekshiriladi, chunki u aynan
/// shu yerda yashaydi. Endpoint ruxsati <c>employees.view</c> bo'lib
/// qoladi (bosh sahifa hamma uchun ochilishi kerak), javob esa
/// <c>recruiting.view</c> ga qarab TORAYADI. Xuddi shu naqsh oklad
/// (<c>SalaryAccess</c>) va bildirishnomalarda (<c>NotificationCollector</c>)
/// ham qo'llangan.
/// </para>
/// <para>
/// Bu yerda baza yo'q: o'qish modeli almashtirilgan (mock). Sanoq
/// SQL'ining to'g'riligi boshqa savol va u uchi-uchiga testlarda
/// (<c>DashboardApiTests</c>) qadalgan.
/// </para>
/// </remarks>
public sealed class GetDashboardStatsQueryHandlerTests
{
    private readonly IDashboardReadModel _readModel = Substitute.For<IDashboardReadModel>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ICurrentUser _user = Substitute.For<ICurrentUser>();

    public GetDashboardStatsQueryHandlerTests()
    {
        _clock.Today.Returns(new DateOnly(2026, 8, 26));

        _readModel
            .GetCountsAsync(Arg.Any<DashboardCountsRange>(), Arg.Any<CancellationToken>())
            .Returns(new DashboardCounts(540, 12, 33, 4, 2, 6, 3, 1, 96, 74, 9, 5));

        _readModel
            .GetRecruitingCountsAsync(Arg.Any<DashboardCountsRange>(), Arg.Any<CancellationToken>())
            .Returns(new DashboardRecruitingCounts(
                OpenVacancies: 5,
                Openings: 12,
                ActiveCandidates: 22,
                NewCandidates: 7));
    }

    private GetDashboardStatsQueryHandler Handler => new(_readModel, _clock, _user);

    /// <summary>
    /// <c>recruiting.view</c> bor — blok to'liq qaytadi.
    /// </summary>
    /// <remarks>
    /// Sonlar ATAYLAB to'rttasi ham har xil (5 / 12 / 22 / 7): bir xil
    /// bo'lganida joyi almashtirilgan argument test'dan o'tib ketardi.
    /// Ayniqsa birinchi ikkitasi — vakansiya soni va o'rinlar yig'indisi
    /// bir-biriga o'xshash raqamlar va aynan ular almashib qolishi oson.
    /// </remarks>
    [Fact]
    public async Task Ruxsat_bor_bolsa_rekruting_sonlari_qaytadi()
    {
        _user.HasPermission(Permissions.RecruitingView).Returns(true);

        var natija = await Handler.HandleAsync(
            new GetDashboardStatsQuery("month"),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();

        var rekruting = natija.Value.Recruiting;
        rekruting.ShouldNotBeNull();
        rekruting.OpenVacancies.ShouldBe(5);
        rekruting.Openings.ShouldBe(12);
        rekruting.ActiveCandidates.ShouldBe(22);
        rekruting.NewCandidates.ShouldBe(7);
    }

    /// <summary>
    /// Rekruting sonlari xodimlar sonlari bilan AYNAN bir xil oraliqda
    /// so'raladi.
    /// </summary>
    /// <remarks>
    /// Oraliqni ikkinchi marta hisoblash yo'li ochiq edi va u jimgina
    /// xato berardi: bitta ekranda «oxirgi bir oy» ikki xil kundan
    /// boshlanardi va «7 ta yangi ariza» qaysi kunlarga tegishli ekani
    /// noaniq bo'lib qolardi. Shu sababli ikkala chaqiruvga bitta
    /// <c>DashboardCountsRange</c> uzatiladi va shu yerda qadalgan.
    /// </remarks>
    [Fact]
    public async Task Rekruting_va_xodimlar_bir_xil_oraliqda_sanaladi()
    {
        _user.HasPermission(Permissions.RecruitingView).Returns(true);

        var natija = await Handler.HandleAsync(
            new GetDashboardStatsQuery("week"),
            TestContext.Current.CancellationToken);

        // Javobda qaytgan chegaralar — ekranda «20.08 – 26.08» bo'lib
        // ko'rinadigan o'sha oraliqning o'zi.
        natija.Value.From.ShouldBe(new DateOnly(2026, 8, 20));
        natija.Value.To.ShouldBe(new DateOnly(2026, 8, 26));

        // Rekruting sanog'i AYNAN shu chegaralar bilan so'ralgan
        await _readModel
            .Received(1)
            .GetRecruitingCountsAsync(
                Arg.Is<DashboardCountsRange>(oraliq =>
                    oraliq.From == new DateOnly(2026, 8, 20)
                    && oraliq.To == new DateOnly(2026, 8, 26)),
                Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// <c>recruiting.view</c> yo'q — blok <c>null</c> va o'qish modeliga
    /// UMUMAN murojaat qilinmaydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ikkita alohida da'vo va ikkalasi ham kerak. Birinchisi — maxfiylik:
    /// nol emas, aynan <c>null</c> qaytishi kerak, chunki nol («ochiq
    /// vakansiya yo'q») haqiqiy javob va ruxsatsiz odam unga ishonib
    /// noto'g'ri xulosa chiqarardi. Ikkinchisi — narx: so'rov yuborilib,
    /// natijasi tashlab yuborilsa, ko'rsatilmaydigan raqam uchun baza
    /// har safar to'rtta <c>COUNT</c> bajarardi.
    /// </para>
    /// <para>
    /// Xodimlar sonlari esa JOYIDA qoladi: bosh sahifa
    /// <c>employees.view</c> bilan ochiladi va rekruting ruxsatining
    /// yo'qligi butun ekranni sindirmasligi kerak.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Ruxsat_yoq_bolsa_rekruting_soralmaydi_va_null_qaytadi()
    {
        _user.HasPermission(Permissions.RecruitingView).Returns(false);

        var natija = await Handler.HandleAsync(
            new GetDashboardStatsQuery("month"),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.Recruiting.ShouldBeNull();

        await _readModel
            .DidNotReceive()
            .GetRecruitingCountsAsync(Arg.Any<DashboardCountsRange>(), Arg.Any<CancellationToken>());

        // Ekranning qolgan qismi ishlashda davom etadi
        natija.Value.ActiveEmployees.ShouldBe(540);
    }
}
