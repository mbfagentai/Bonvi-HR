using System.Text;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Common.Documents;
using Bonvi.Application.Employees.Documents;
using Bonvi.Domain.Employees;
using NSubstitute;

namespace Bonvi.Application.Tests.Employees;

/// <summary>Hujjat biriktirish, tuzatish va o'chirish — ulanish qatlami.</summary>
public sealed class EmployeeDocumentHandlerTests
{
    private readonly EmployeeTestContext _context = new();

    private UploadEmployeeDocumentCommandHandler Yuklash => new(
        _context.Employees,
        _context.DocumentTypes,
        _context.Documents,
        _context.Files,
        _context.Mapper,
        _context.UnitOfWork);

    private UpdateEmployeeDocumentCommandHandler Tuzatish => new(
        _context.Documents,
        _context.DocumentTypes,
        _context.Mapper,
        _context.UnitOfWork);

    private DeleteEmployeeDocumentCommandHandler Ochirish => new(_context.Documents, _context.UnitOfWork);

    private static MemoryStream Oqim() => new(Encoding.UTF8.GetBytes("%PDF-1.4 sinov"));

    // ────────────────────── Yuklash ──────────────────────

    [Fact]
    public async Task Nomavjud_xodimga_hujjat_yuklanmaydi()
    {
        var turi = _context.ExistingDocumentType();

        var natija = await Yuklash.HandleAsync(
            new UploadEmployeeDocumentCommand(Guid.CreateVersion7(), turi.Id, Oqim(), "shartnoma.pdf", 1024),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("xodim.topilmadi");
    }

    [Fact]
    public async Task Nomavjud_tur_bilan_yuklanmaydi()
    {
        var employee = _context.ExistingEmployee();

        _context.DocumentTypes.ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        var natija = await Yuklash.HandleAsync(
            new UploadEmployeeDocumentCommand(employee.Id, Guid.CreateVersion7(), Oqim(), "shartnoma.pdf", 1024),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("hujjat.turi_topilmadi");
    }

    [Fact]
    public async Task Chegaradan_katta_fayl_rad_etiladi()
    {
        var employee = _context.ExistingEmployee();
        var turi = _context.ExistingDocumentType();

        var natija = await Yuklash.HandleAsync(
            new UploadEmployeeDocumentCommand(
                employee.Id, turi.Id, Oqim(), "shartnoma.pdf", DocumentLimits.MaxBytes + 1),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("hujjat.katta");
        await _context.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Tanilmagan_mazmun_rad_etiladi()
    {
        var employee = _context.ExistingEmployee();
        var turi = _context.ExistingDocumentType();

        _context.Inspector.Detect(Arg.Any<byte[]>()).Returns((DetectedFileKind?)null);

        var natija = await Yuklash.HandleAsync(
            new UploadEmployeeDocumentCommand(employee.Id, turi.Id, Oqim(), "shartnoma.exe", 14),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("hujjat.yaroqsiz");
        _context.Documents.DidNotReceive().Add(Arg.Any<EmployeeDocument>());
    }

    [Fact]
    public async Task Togri_malumot_bilan_hujjat_yuklanadi_va_tur_nomi_qaytadi()
    {
        var employee = _context.ExistingEmployee();
        var turi = _context.ExistingDocumentType("Pasport nusxasi");

        _context.Inspector.Detect(Arg.Any<byte[]>()).Returns(DetectedFileKind.Pdf);
        _context.Storage
            .SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new StoredFile("2026/03/kalit.pdf", "hujjat.pdf", "application/pdf", 14));

        var natija = await Yuklash.HandleAsync(
            new UploadEmployeeDocumentCommand(employee.Id, turi.Id, Oqim(), "Шартнома.pdf", 14, "Asl nusxa"),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.DocumentTypeName.ShouldBe(turi.Name);
        natija.Value.FileName.ShouldBe("Шартнома.pdf");
        natija.Value.ContentType.ShouldBe("application/pdf");
        natija.Value.Note.ShouldBe("Asl nusxa");

        _context.Documents.Received(1).Add(Arg.Any<EmployeeDocument>());
        await _context.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ────────────────────── Tuzatish ──────────────────────

    [Fact]
    public async Task Nomavjud_hujjat_tuzatilmaydi()
    {
        var turi = _context.ExistingDocumentType();

        var natija = await Tuzatish.HandleAsync(
            new UpdateEmployeeDocumentCommand(Guid.CreateVersion7(), turi.Id, "Izoh"),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("hujjat.topilmadi");
    }

    [Fact]
    public async Task Mavjud_hujjat_yangi_tur_va_izoh_bilan_tuzatiladi()
    {
        var eskiTur = _context.ExistingDocumentType();
        var document = EmployeeDocument
            .Attach(Guid.CreateVersion7(), eskiTur.Id, "2026/03/abc.pdf", "shartnoma.pdf", "application/pdf", 2048)
            .Value;

        _context.Documents.GetByIdAsync(document.Id, Arg.Any<CancellationToken>()).Returns(document);

        var yangiTur = _context.ExistingDocumentType("Buyruq nusxasi");

        var natija = await Tuzatish.HandleAsync(
            new UpdateEmployeeDocumentCommand(document.Id, yangiTur.Id, "Tuzatildi"),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.DocumentTypeId.ShouldBe(yangiTur.Id);
        natija.Value.Note.ShouldBe("Tuzatildi");
        natija.Value.FileName.ShouldBe("shartnoma.pdf", "Update fayl nomiga tegmasligi kerak");
    }

    // ────────────────────── O'chirish ──────────────────────

    [Fact]
    public async Task Nomavjud_hujjat_ochirilmaydi()
    {
        var natija = await Ochirish.HandleAsync(
            new DeleteEmployeeDocumentCommand(Guid.CreateVersion7()),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("hujjat.topilmadi");
    }

    [Fact]
    public async Task Mavjud_hujjat_ochiriladi_va_fayl_saqlanib_qoladi()
    {
        var turi = _context.ExistingDocumentType();
        var document = EmployeeDocument
            .Attach(Guid.CreateVersion7(), turi.Id, "2026/03/abc.pdf", "shartnoma.pdf", "application/pdf", 2048)
            .Value;

        _context.Documents.GetByIdAsync(document.Id, Arg.Any<CancellationToken>()).Returns(document);

        var natija = await Ochirish.HandleAsync(
            new DeleteEmployeeDocumentCommand(document.Id),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        _context.Documents.Received(1).Remove(document);
        await _context.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        // Yumshoq o'chirish falsafasi: fayl DISKDAN o'chirilmaydi (izoh
        // `DeleteEmployeeDocumentCommandHandler` da) — quvurga hech qachon
        // murojaat qilinmasligi shu bilan tekshiriladi.
        await _context.Storage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
