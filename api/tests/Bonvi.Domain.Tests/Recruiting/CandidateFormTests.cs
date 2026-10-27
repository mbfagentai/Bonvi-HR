using Bonvi.Domain.Recruiting;

namespace Bonvi.Domain.Tests.Recruiting;

/// <summary>Onlayn anketa yozuvi (SPEC-planshet-anketa §6.1).</summary>
public sealed class CandidateFormTests
{
    private static readonly DateTimeOffset Vaqt = new(2026, 10, 24, 9, 41, 0, TimeSpan.Zero);

    [Fact]
    public void Toliq_yozuv_yaratiladi()
    {
        var nomzod = Guid.CreateVersion7();
        var yuborish = Guid.CreateVersion7();

        var anketa = CandidateForm.Submit(nomzod, 1024, "{\"lastName\":\"G‘ulomov\"}", "uz-Latn", "rasm.jpg", "imzo.png", yuborish, fileId: null, Vaqt);

        anketa.IsSuccess.ShouldBeTrue();
        anketa.Value.CandidateId.ShouldBe(nomzod);
        anketa.Value.Number.ShouldBe(1024);
        anketa.Value.FormCode.ShouldBe(CandidateForm.HrF02);
        anketa.Value.FormVersion.ShouldBe("3.0");
        anketa.Value.ClientSubmissionId.ShouldBe(yuborish);
        anketa.Value.Language.ShouldBe("uz-Latn");
        anketa.Value.FileId.ShouldBeNull();
        anketa.Value.SubmittedAt.ShouldBe(Vaqt);
    }

    [Theory]
    [InlineData(0, "{}", "uz-Latn", "r", "i")]
    [InlineData(1, " ", "uz-Latn", "r", "i")]
    [InlineData(1, "{}", "", "r", "i")]
    [InlineData(1, "{}", "uz-Latn-UZ1", "r", "i")]
    [InlineData(1, "{}", "uz-Latn", "", "i")]
    [InlineData(1, "{}", "uz-Latn", "r", " ")]
    public void Toliq_bolmagan_yozuv_rad_etiladi(int raqam, string javoblar, string til, string rasm, string imzo)
    {
        var anketa = CandidateForm.Submit(Guid.CreateVersion7(), raqam, javoblar, til, rasm, imzo, Guid.CreateVersion7(), null, Vaqt);

        anketa.IsFailure.ShouldBeTrue();
        anketa.Error.Code.ShouldBe("anketa.notogri");
    }

    [Fact]
    public void Nomzodsiz_va_yuborish_identifikatorisiz_rad_etiladi()
    {
        CandidateForm.Submit(Guid.Empty, 1, "{}", "ru", "r", "i", Guid.CreateVersion7(), null, Vaqt)
            .Error.Code.ShouldBe("anketa.nomzod_yoq");

        CandidateForm.Submit(Guid.CreateVersion7(), 1, "{}", "ru", "r", "i", Guid.Empty, null, Vaqt)
            .Error.Code.ShouldBe("anketa.notogri");
    }
}
