using Bonvi.Domain.Safety;

namespace Bonvi.Domain.Tests.Safety;

/// <summary>Baxtsiz hodisalar jurnali (M11) mantig'i.</summary>
public sealed class IncidentTests
{
    private static readonly Guid XodimId = Guid.CreateVersion7();
    private static readonly DateOnly Sana = new(2026, 3, 1);

    [Fact]
    public void Xodimsiz_yaratilmaydi()
    {
        var natija = Incident.Record(Guid.Empty, Sana, "Sex 1", "Jarohat", IncidentSeverity.Minor);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("baxtsiz_hodisa.xodim_yoq");
    }

    [Fact]
    public void Joysiz_yaratilmaydi()
    {
        var natija = Incident.Record(XodimId, Sana, "   ", "Jarohat", IncidentSeverity.Minor);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("baxtsiz_hodisa.joy_bosh");
    }

    [Fact]
    public void Tavsifsiz_yaratilmaydi()
    {
        var natija = Incident.Record(XodimId, Sana, "Sex 1", null, IncidentSeverity.Minor);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("baxtsiz_hodisa.tavsif_bosh");
    }

    [Fact]
    public void Togri_malumot_bilan_muvaffaqiyatli_yaratiladi()
    {
        var natija = Incident.Record(
            XodimId,
            Sana,
            "Metan zavodi",
            "Qo'l jarohati",
            IncidentSeverity.Moderate,
            shift: "Kunduzgi smena",
            cause: "Ehtiyotsizlik",
            correctiveAction: "Tibbiy yordam ko'rsatildi",
            medicalAidProvided: true,
            medicalAidNote: "Tez tibbiy yordam chaqirildi");

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.Severity.ShouldBe(IncidentSeverity.Moderate);
        natija.Value.MedicalAidProvided.ShouldBeTrue();
        natija.Value.CommissionConclusion.ShouldBeNull("xulosa yaratishda MAJBURIY emas");
    }

    [Fact]
    public void Update_xodimni_ozgartirmaydi()
    {
        var incident = Incident.Record(XodimId, Sana, "Sex 1", "Jarohat", IncidentSeverity.Minor).Value;

        var natija = incident.Update(
            Sana.AddDays(1), "Sex 2", "Tuzatilgan tavsif", IncidentSeverity.Severe);

        natija.IsSuccess.ShouldBeTrue();
        incident.EmployeeId.ShouldBe(XodimId, "xodim Update orqali o'zgarmaydi");
        incident.Location.ShouldBe("Sex 2");
        incident.Description.ShouldBe("Tuzatilgan tavsif");
        incident.Severity.ShouldBe(IncidentSeverity.Severe);
    }

    [Fact]
    public void Xulosasiz_yakunlanmaydi()
    {
        var incident = Incident.Record(XodimId, Sana, "Sex 1", "Jarohat", IncidentSeverity.Minor).Value;

        var natija = incident.Conclude(null, Sana.AddDays(10));

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("baxtsiz_hodisa.xulosa_bosh");
        incident.CommissionConclusion.ShouldBeNull();
    }

    [Fact]
    public void Xulosa_muvaffaqiyatli_yoziladi()
    {
        var incident = Incident.Record(XodimId, Sana, "Sex 1", "Jarohat", IncidentSeverity.Fatal).Value;

        var xulosaSanasi = Sana.AddDays(14);
        var natija = incident.Conclude("Xavfsizlik qoidalari buzilgan", xulosaSanasi);

        natija.IsSuccess.ShouldBeTrue();
        incident.CommissionConclusion.ShouldBe("Xavfsizlik qoidalari buzilgan");
        incident.ConcludedAt.ShouldBe(xulosaSanasi);
    }
}
