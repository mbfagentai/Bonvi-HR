using Bonvi.Domain.Employees;

namespace Bonvi.Domain.Tests.Employees;

/// <summary>Xodimga biriktirilgan fayl mantig'i (S8/M9).</summary>
public sealed class EmployeeDocumentTests
{
    private static readonly Guid XodimId = Guid.CreateVersion7();
    private static readonly Guid TurId = Guid.CreateVersion7();

    [Fact]
    public void Tursiz_hujjat_biriktirilmaydi()
    {
        var natija = EmployeeDocument.Attach(
            XodimId, Guid.Empty, "2026/03/abc.pdf", "shartnoma.pdf", "application/pdf", 1024);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("hujjat.tur_bosh");
    }

    [Fact]
    public void Kalitsiz_hujjat_biriktirilmaydi()
    {
        var natija = EmployeeDocument.Attach(
            XodimId, TurId, fileKey: "  ", "shartnoma.pdf", "application/pdf", 1024);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("hujjat.fayl_bosh");
    }

    [Fact]
    public void Nolinchi_hajm_bilan_biriktirilmaydi()
    {
        var natija = EmployeeDocument.Attach(
            XodimId, TurId, "2026/03/abc.pdf", "shartnoma.pdf", "application/pdf", 0);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("hujjat.fayl_bosh");
    }

    [Fact]
    public void Togri_malumot_bilan_muvaffaqiyatli_biriktiriladi()
    {
        var natija = EmployeeDocument.Attach(
            XodimId, TurId, "2026/03/abc.pdf", "Шартнома 2026.pdf", "application/pdf", 2048, "Asl nusxa");

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.EmployeeId.ShouldBe(XodimId);
        natija.Value.DocumentTypeId.ShouldBe(TurId);
        natija.Value.FileName.ShouldBe("Шартнома 2026.pdf");
        natija.Value.ContentType.ShouldBe("application/pdf");
        natija.Value.SizeBytes.ShouldBe(2048);
        natija.Value.Note.ShouldBe("Asl nusxa");
    }

    [Fact]
    public void Fayl_nomi_bosh_bolsa_ortga_qaytariladi()
    {
        // Kalit va tur mavjud bo'lsa ham, nom bo'sh yoki faqat bo'shliq
        // bo'lishi mumkin — ko'rsatishda nimadir bo'lishi kerak.
        var natija = EmployeeDocument.Attach(XodimId, TurId, "2026/03/abc.pdf", "   ", "application/pdf", 10);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.FileName.ShouldBe("hujjat");
    }

    [Fact]
    public void Update_turini_va_izohini_ozgartiradi_lekin_faylni_saqlaydi()
    {
        var document = EmployeeDocument
            .Attach(XodimId, TurId, "2026/03/abc.pdf", "shartnoma.pdf", "application/pdf", 2048)
            .Value;

        var yangiTur = Guid.CreateVersion7();

        var natija = document.Update(yangiTur, "Tuzatildi");

        natija.IsSuccess.ShouldBeTrue();
        document.DocumentTypeId.ShouldBe(yangiTur);
        document.Note.ShouldBe("Tuzatildi");
        document.FileKey.ShouldBe("2026/03/abc.pdf", "Update fayl kalitiga tegmasligi kerak");
        document.FileName.ShouldBe("shartnoma.pdf", "Update fayl nomiga tegmasligi kerak");
    }

    [Fact]
    public void Update_tursiz_rad_etiladi()
    {
        var document = EmployeeDocument
            .Attach(XodimId, TurId, "2026/03/abc.pdf", "shartnoma.pdf", "application/pdf", 2048)
            .Value;

        var natija = document.Update(Guid.Empty, "Izoh");

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("hujjat.tur_bosh");
        document.DocumentTypeId.ShouldBe(TurId, "muvaffaqiyatsiz tahrir eski qiymatni buzmasligi kerak");
    }
}
