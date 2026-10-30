using Bonvi.Domain.Recruiting;

namespace Bonvi.Domain.Tests.Recruiting;

/// <summary>Vakansiya so'rovining qoidalari (docs/SPEC-vakansiya-sorovi.md §1).</summary>
public sealed class VacancyRequestTests
{
    private static readonly Guid Department = Guid.NewGuid();
    private static readonly Guid Head = Guid.NewGuid();
    private static readonly DateTimeOffset At = new(2026, 10, 30, 8, 0, 0, TimeSpan.Zero);

    private static VacancyRequest Yangi(string title = "  Payvandchi   usta ", int openings = 2)
        => VacancyRequest.Create(Department, Head, title, openings, new DateOnly(2026, 11, 15), "  Yangi liniya  ").Value;

    [Fact]
    public void Yangi_sorov_kutmoqda_nomi_va_izohi_tozalangan()
    {
        var sorov = Yangi();

        sorov.Status.ShouldBe(VacancyRequestStatus.Pending);
        sorov.Title.ShouldBe("Payvandchi usta", "vakansiya nomidagi qoida — tasdiqlanganda forma aynan shu nom bilan ochiladi");
        sorov.Note.ShouldBe("Yangi liniya");
        sorov.Openings.ShouldBe(2);
    }

    [Theory]
    [InlineData("", 1, "vakansiya_sorovi.nom_kerak")]
    [InlineData("Omborchi", 0, "vakansiya_sorovi.soni_notogri")]
    [InlineData("Omborchi", 101, "vakansiya_sorovi.soni_notogri")]
    public void Notogri_maydon_rad_etiladi(string title, int openings, string code)
        => VacancyRequest.Create(Department, Head, title, openings, null, null).Error.Code.ShouldBe(code);

    [Fact]
    public void Bolimsiz_va_sorovchisiz_sorov_yoq()
    {
        VacancyRequest.Create(Guid.Empty, Head, "Omborchi", 1, null, null).Error.Code.ShouldBe("vakansiya_sorovi.bolim_kerak");
        VacancyRequest.Create(Department, Guid.Empty, "Omborchi", 1, null, null).Error.Code.ShouldBe("vakansiya_sorovi.boshliq_yoq");
    }

    [Fact]
    public void Tasdiqlash_vakansiyani_boglaydi_va_qaror_bir_marta()
    {
        var sorov = Yangi();
        var vakansiya = Guid.NewGuid();
        var hr = Guid.NewGuid();

        sorov.Approve(vakansiya, hr, At).IsSuccess.ShouldBeTrue();

        sorov.Status.ShouldBe(VacancyRequestStatus.Approved);
        sorov.VacancyId.ShouldBe(vakansiya);
        sorov.DecidedBy.ShouldBe(hr);
        sorov.DecidedAt.ShouldBe(At);

        // Qaror qilingan so'rov boshqa holatga o'tmaydi
        sorov.Reject("Kech", hr, At).Error.Code.ShouldBe("vakansiya_sorovi.qaror_qilingan");
        sorov.Cancel(At).Error.Code.ShouldBe("vakansiya_sorovi.qaror_qilingan");
    }

    [Fact]
    public void Rad_etish_uchun_sabab_majburiy()
    {
        var sorov = Yangi();

        sorov.Reject("   ", Guid.NewGuid(), At).Error.Code.ShouldBe("vakansiya_sorovi.sabab_kerak");
        sorov.Status.ShouldBe(VacancyRequestStatus.Pending);

        sorov.Reject(" Shtat yo'q ", Guid.NewGuid(), At).IsSuccess.ShouldBeTrue();
        sorov.Status.ShouldBe(VacancyRequestStatus.Rejected);
        sorov.DecisionNote.ShouldBe("Shtat yo'q");
    }

    [Fact]
    public void Bekor_qilingan_sorovga_vakansiya_ochilmaydi()
    {
        var sorov = Yangi();

        sorov.Cancel(At).IsSuccess.ShouldBeTrue();
        sorov.Status.ShouldBe(VacancyRequestStatus.Cancelled);
        sorov.CancelledAt.ShouldBe(At);

        sorov.Approve(Guid.NewGuid(), Guid.NewGuid(), At).Error.Code.ShouldBe("vakansiya_sorovi.qaror_qilingan");
    }
}
