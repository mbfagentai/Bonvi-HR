using Bonvi.Domain.Feedback;

namespace Bonvi.Domain.Tests.Feedback;

/// <summary>«Qayta aloqa» (exit interview, M10) mantig'i.</summary>
public sealed class ExitInterviewTests
{
    private static readonly Guid MunosabatId = Guid.CreateVersion7();
    private static readonly DateOnly Sana = new(2026, 3, 1);

    [Fact]
    public void Mehnat_munosabatisiz_yaratilmaydi()
    {
        var natija = ExitInterview.Record(Guid.Empty, Sana, "Rahbariyat bilan ziddiyat");

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("qayta_aloqa.mehnat_yoq");
    }

    [Fact]
    public void Sababsiz_yaratilmaydi()
    {
        var natija = ExitInterview.Record(MunosabatId, Sana, "   ");

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("qayta_aloqa.sabab_bosh");
    }

    [Fact]
    public void Togri_malumot_bilan_muvaffaqiyatli_yaratiladi()
    {
        var natija = ExitInterview.Record(
            MunosabatId,
            Sana,
            "Rahbariyat bilan ziddiyat",
            RecommendationAnswer.Yes,
            "Ajoyib jamoa",
            "Ish sharoitlarini yaxshilash");

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.EmploymentId.ShouldBe(MunosabatId);
        natija.Value.Reason.ShouldBe("Rahbariyat bilan ziddiyat");
        natija.Value.WouldRecommend.ShouldBe(RecommendationAnswer.Yes);
        natija.Value.Impressions.ShouldBe("Ajoyib jamoa");
        natija.Value.ConditionsToReturn.ShouldBe("Ish sharoitlarini yaxshilash");
    }

    [Fact]
    public void Ixtiyoriy_maydonlarsiz_ham_yaratiladi()
    {
        var natija = ExitInterview.Record(MunosabatId, Sana, "Oylik kam edi");

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.WouldRecommend.ShouldBeNull();
        natija.Value.Impressions.ShouldBeNull();
        natija.Value.ConditionsToReturn.ShouldBeNull();
    }

    [Fact]
    public void Update_sabab_va_javoblarni_ozgartiradi_lekin_mehnat_munosabatini_saqlaydi()
    {
        var interview = ExitInterview.Record(MunosabatId, Sana, "Boshlang'ich sabab").Value;

        var yangiSana = Sana.AddDays(5);
        var natija = interview.Update(yangiSana, "Tuzatilgan sabab", RecommendationAnswer.No, "Yomon", "Hech qanday");

        natija.IsSuccess.ShouldBeTrue();
        interview.EmploymentId.ShouldBe(MunosabatId, "mehnat munosabati Update orqali o'zgarmaydi");
        interview.InterviewDate.ShouldBe(yangiSana);
        interview.Reason.ShouldBe("Tuzatilgan sabab");
        interview.WouldRecommend.ShouldBe(RecommendationAnswer.No);
        interview.Impressions.ShouldBe("Yomon");
        interview.ConditionsToReturn.ShouldBe("Hech qanday");
    }

    [Fact]
    public void Update_sababni_bosh_qoldirsa_rad_etiladi_va_eski_qiymat_saqlanadi()
    {
        var interview = ExitInterview.Record(MunosabatId, Sana, "Boshlang'ich sabab").Value;

        var natija = interview.Update(Sana, null);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("qayta_aloqa.sabab_bosh");
        interview.Reason.ShouldBe("Boshlang'ich sabab", "muvaffaqiyatsiz tahrir eski qiymatni buzmasligi kerak");
    }
}
