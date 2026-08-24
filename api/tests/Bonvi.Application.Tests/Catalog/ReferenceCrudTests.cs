using Bonvi.Application.Catalog.Commands;
using Bonvi.Application.Catalog.Common;
using Bonvi.Application.Catalog.Dtos;
using Bonvi.Application.Catalog.Queries;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Organization;
using NSubstitute;

namespace Bonvi.Application.Tests.Catalog;

/// <summary>
/// Generik ma'lumotnoma amallarining testlari.
/// </summary>
/// <remarks>
/// <para>
/// Testlar <c>LegalEntity</c> ustida yozilgan, lekin ular tekshirayotgan kod
/// SAKKIZTA ma'lumotnomaga xizmat qiladi: handler generik. Ya'ni bu yerdagi
/// har bir test aslida sakkizta ma'lumotnomani himoyalaydi.
/// </para>
/// <para>
/// Repozitoriy mock qilingan — bu qatlamda baza emas, <b>qarorlar tartibi</b>
/// tekshiriladi: avval format, keyin nom bandligi, keyin domen qoidalari.
/// </para>
/// </remarks>
public sealed class ReferenceCrudTests
{
    private readonly IReferenceRepository<LegalEntity> _repository =
        Substitute.For<IReferenceRepository<LegalEntity>>();

    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly SaveReferenceCommandValidator _validator = new();
    private readonly LegalEntityDefinition _definition = new();

    private CreateReferenceCommandHandler<LegalEntity, LegalEntityDto> Yaratuvchi
        => new(_repository, _definition, _validator, _unitOfWork);

    private UpdateReferenceCommandHandler<LegalEntity, LegalEntityDto> Tahrirlovchi
        => new(_repository, _definition, _validator, _unitOfWork);

    private ListReferenceQueryHandler<LegalEntity, LegalEntityDto> Royxat
        => new(_repository, _definition);

    // ────────── Nom takrorlanmasligi ──────────

    [Fact]
    public async Task Nom_band_bolsa_yaratish_rad_etiladi()
    {
        _repository
            .NameExistsAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var natija = await Yaratuvchi.HandleAsync(
            new SaveReferenceCommand("VIKOR"),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("malumotnoma.nom_band");
        natija.Error.Message.ShouldBe("Bunday nom allaqachon mavjud.");

        // Takror nom bilan hech narsa saqlanmasligi kerak
        _repository.DidNotReceive().Add(Arg.Any<LegalEntity>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Nom_bandligi_409_konflikt_sifatida_qaytadi()
    {
        // 400 emas: kiritilgan qiymatning O'ZI to'g'ri, u bazaning joriy
        // holati bilan to'qnashadi — mijoz uchun bu boshqa turdagi xato.
        _repository
            .NameExistsAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var natija = await Yaratuvchi.HandleAsync(
            new SaveReferenceCommand("VIKOR"),
            TestContext.Current.CancellationToken);

        natija.Error.Type.ShouldBe(ErrorType.Conflict);
    }

    [Fact]
    public async Task Tahrirlashda_yozuvning_ozi_takror_hisoblanmaydi()
    {
        var entity = LegalEntity.Create("VIKOR").Value;
        _repository.GetByIdAsync(entity.Id, Arg.Any<CancellationToken>()).Returns(entity);
        _repository
            .NameExistsAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var natija = await Tahrirlovchi.HandleAsync(
            entity.Id,
            new SaveReferenceCommand("VIKOR"),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Message : null);

        // Yozuvning O'Z identifikatori istisno sifatida uzatilishi shart —
        // aks holda faqat kodni o'zgartirish ham «nom band» bo'lib qolardi
        await _repository.Received(1).NameExistsAsync("VIKOR", entity.Id, Arg.Any<CancellationToken>());
    }

    // ────────── Validatsiya ──────────

    [Fact]
    public async Task Bosh_nom_bilan_yaratib_bolmaydi()
    {
        var natija = await Yaratuvchi.HandleAsync(
            new SaveReferenceCommand("   "),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("malumotnoma.nom_bosh");

        // Muhim tartib: bo'sh nom bilan bazaga UMUMAN murojaat qilinmaydi
        await _repository.DidNotReceive()
            .NameExistsAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Uzun_nom_rad_etiladi()
    {
        var uzunNom = new string('a', 256);

        var natija = await Yaratuvchi.HandleAsync(
            new SaveReferenceCommand(uzunNom),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("malumotnoma.nom_uzun");
    }

    [Fact]
    public async Task Notogri_stir_domen_kodi_bilan_rad_etiladi()
    {
        _repository
            .NameExistsAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var natija = await Yaratuvchi.HandleAsync(
            new SaveReferenceCommand("VIKOR", TaxId: "123"),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("yuridik_shaxs.stir");
        _repository.DidNotReceive().Add(Arg.Any<LegalEntity>());
    }

    // ────────── Muvaffaqiyatli oqim ──────────

    [Fact]
    public async Task Yaratilgan_yozuv_turga_xos_maydonlari_bilan_qaytadi()
    {
        _repository
            .NameExistsAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var natija = await Yaratuvchi.HandleAsync(
            new SaveReferenceCommand(
                "  VIKOR   MChJ ",
                TaxId: "123 456 789",
                FullName: "«VIKOR» mas'uliyati cheklangan jamiyati"),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Message : null);

        // Nom domen tomonidan tozalanadi: ortiqcha bo'shliqlar yo'qoladi
        natija.Value.Name.ShouldBe("VIKOR MChJ");
        natija.Value.TaxId.ShouldBe("123456789");
        natija.Value.IsActive.ShouldBeTrue();

        _repository.Received(1).Add(Arg.Any<LegalEntity>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Ro'yxat ALIFBO tartibida qaytadi.
    /// </summary>
    /// <remarks>
    /// Kiritish tartibi ataylab teskari: natija repozitoriy qaytargan
    /// ketma-ketlikka emas, faqat saralashga bog'liq bo'lsin.
    /// </remarks>
    [Fact]
    public async Task Royxat_alifbo_tartibida_saralanadi()
    {
        var zulu = LegalEntity.Create("Zulu").Value;
        var alfa = LegalEntity.Create("Alfa").Value;

        _repository
            .ListAsync(false, Arg.Any<CancellationToken>())
            .Returns(new List<LegalEntity> { zulu, alfa });

        var natija = await Royxat.HandleAsync(
            new ListReferenceQuery(),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();

        natija.Value[0].Name.ShouldBe("Alfa");
        natija.Value[1].Name.ShouldBe("Zulu");
    }

    [Fact]
    public async Task Mavjud_bolmagan_yozuvni_tahrirlab_bolmaydi()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((LegalEntity?)null);

        var natija = await Tahrirlovchi.HandleAsync(
            Guid.NewGuid(),
            new SaveReferenceCommand("VIKOR"),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("malumotnoma.topilmadi");
        natija.Error.Type.ShouldBe(ErrorType.NotFound);
    }
}
