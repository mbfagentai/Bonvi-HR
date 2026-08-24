using Bonvi.Domain.Catalog;
using Bonvi.Domain.Organization;

namespace Bonvi.Domain.Tests.Organization;

/// <summary>
/// Ma'lumotnoma entity'larining biznes qoidalari.
/// </summary>
/// <remarks>
/// Asosiy e'tibor <b>nom normalizatsiyasiga</b>: Excel'dagi 228 lavozim va
/// 73 bo'limning katta qismi bir narsaning turli yozilishi edi. Agar
/// normalizatsiya ishlamasa, tizim ham xuddi Excel kabi tarqalib ketadi.
/// </remarks>
public sealed class CatalogTests
{
    // ────────────────────── Nom normalizatsiyasi ──────────────────────

    [Theory]
    [InlineData("  Метан завод  ", "Метан завод")]
    [InlineData("Маркетинг  бўлими", "Маркетинг бўлими")]
    [InlineData("Плазмада\tкесиш", "Плазмада кесиш")]
    [InlineData("Вело   унион", "Вело унион")]
    [InlineData("Метан\u00A0завод", "Метан завод")]   // uzilmaydigan bo'shliq — Excel eksportining tuzog'i
    public void Nomdagi_ortiqcha_boshliqlar_tozalanadi(string kirish, string kutilgan)
    {
        // Excel'da «Маркетинг бўлими» va «Маркетинг  бўлими» ikki xil qiymat edi
        var natija = Department.Create(kirish);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.Name.ShouldBe(kutilgan);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Bosh_nom_bilan_yozuv_yaratilmaydi(string? nom)
    {
        Department.Create(nom).IsFailure.ShouldBeTrue();
        Position.Create(nom).IsFailure.ShouldBeTrue();
        Direction.Create(nom).IsFailure.ShouldBeTrue();
        LegalEntity.Create(nom).IsFailure.ShouldBeTrue();
        TerminationReason.Create(nom).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Yangi_yozuv_faol_holatda_yaratiladi()
    {
        var direction = Direction.Create("Вело").Value;

        direction.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Faolsizlantirilganda_sabab_izohga_yoziladi()
    {
        // Yozuv o'chirilmaydi: eski buyruqlarda unga havola qolgan bo'lishi mumkin
        var position = Position.Create("Спица терувчи").Value;

        position.Deactivate("Bu lavozim 2026-yildan bekor qilindi");

        position.IsActive.ShouldBeFalse();
        position.Note.ShouldBe("Bu lavozim 2026-yildan bekor qilindi");
    }

    // ────────────────────── Yuridik shaxs ──────────────────────

    [Theory]
    [InlineData("123456789", "123456789")]
    [InlineData("123 456 789", "123456789")]
    [InlineData("123-456-789", "123456789")]
    public void STIR_raqamlari_ajratib_olinadi(string kirish, string kutilgan)
    {
        var entity = LegalEntity.Create("VIKOR").Value;

        entity.SetTaxId(kirish).IsSuccess.ShouldBeTrue();
        entity.TaxId.ShouldBe(kutilgan);
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("1234567890")]
    public void Notogri_uzunlikdagi_STIR_rad_etiladi(string stir)
    {
        var entity = LegalEntity.Create("VIKOR").Value;

        var natija = entity.SetTaxId(stir);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("yuridik_shaxs.stir");
    }

    [Fact]
    public void Bosh_STIR_qiymatni_tozalaydi()
    {
        var entity = LegalEntity.Create("VIKOR").Value;
        entity.SetTaxId("123456789");

        entity.SetTaxId("  ").IsSuccess.ShouldBeTrue();

        entity.TaxId.ShouldBeNull();
    }

    // ────────────────────── Yo'nalish rangi ──────────────────────

    [Theory]
    [InlineData("#007AFF", "#007AFF")]
    [InlineData("#34c759", "#34C759")]
    public void Togri_rang_qabul_qilinadi(string kirish, string kutilgan)
    {
        var direction = Direction.Create("Метан").Value;

        direction.SetColor(kirish).IsSuccess.ShouldBeTrue();
        direction.Color.ShouldBe(kutilgan);
    }

    [Theory]
    [InlineData("007AFF")]      // # yo'q
    [InlineData("#007AF")]      // qisqa
    [InlineData("#GGGGGG")]     // o'n oltilik emas
    [InlineData("blue")]
    public void Notogri_rang_rad_etiladi(string rang)
    {
        var direction = Direction.Create("Метан").Value;

        var natija = direction.SetColor(rang);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("yonalish.rang");
    }

    // ────────────────────── Bo'limlar daraxti ──────────────────────

    [Fact]
    public void Bolim_ozining_ota_bolimi_bola_olmaydi()
    {
        var department = Department.Create("Метан завод").Value;

        var natija = department.MoveTo(department.Id);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("bolim.ozi_ota");
    }

    [Fact]
    public void Bolim_eng_yuqori_darajaga_kochiriladi()
    {
        var parent = Department.Create("7-Департамент").Value;
        var child = Department.Create("Метан завод", parent.Id).Value;

        child.MoveTo(null).IsSuccess.ShouldBeTrue();

        child.ParentId.ShouldBeNull();
    }

    [Fact]
    public void Ierarxiya_maydonlari_tashqaridan_ornatiladi()
    {
        // Level va Path repozitoriyda hisoblanadi: buning uchun butun
        // daraxt kerak, entity esa faqat o'zini biladi
        var department = Department.Create("Плазмада кесиш бўлими").Value;

        department.SetHierarchy(2, "7-Департамент / Метан завод / Плазмада кесиш бўлими");

        department.Level.ShouldBe(2);
        department.Path.ShouldNotBeNull();
        department.Path.ShouldContain("Метан завод");
    }

    // ────────────────────── Lavozim ──────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Notogri_daraja_rad_etiladi(int daraja)
    {
        var position = Position.Create("Юк ташувчи").Value;

        var natija = position.SetGrade(daraja);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("lavozim.daraja");
    }

    [Fact]
    public void Sinov_muddati_180_kundan_oshmasligi_kerak()
    {
        // Mehnat kodeksi chegarasi
        var position = Position.Create("Юк ташувчи").Value;

        position.SetProbationDays(90).IsSuccess.ShouldBeTrue();
        position.SetProbationDays(181).IsFailure.ShouldBeTrue();
        position.SetProbationDays(null).IsSuccess.ShouldBeTrue();
    }

    // ────────────────────── Lavozim vazifalari ──────────────────────

    [Fact]
    public void Vazifalar_qoshilgan_tartibda_saqlanadi()
    {
        var position = Position.Create("Омборчи").Value;

        position.AddDuty("Товарни қабул қилиш");
        position.AddDuty("Ҳисоботни юритиш");
        position.AddDuty("Инвентаризация");

        position.Duties.Count.ShouldBe(3);
        position.Duties[0].Text.ShouldBe("Товарни қабул қилиш");
        position.Duties[2].SortOrder.ShouldBe(2);
    }

    [Fact]
    public void Bir_xil_vazifa_ikki_marta_qoshilmaydi()
    {
        var position = Position.Create("Омборчи").Value;
        position.AddDuty("Товарни қабул қилиш");

        // Registr farqi ham takror hisoblanadi
        var natija = position.AddDuty("товарни қабул қилиш");

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("lavozim.vazifa_takror");
        position.Duties.Count.ShouldBe(1);
    }

    [Fact]
    public void Vazifa_ochirilganda_tartib_raqamlari_qayta_hisoblanadi()
    {
        var position = Position.Create("Омборчи").Value;
        position.AddDuty("Биринчи");
        position.AddDuty("Иккинчи");
        position.AddDuty("Учинчи");

        position.RemoveDuty(position.Duties[0].Id);

        position.Duties.Count.ShouldBe(2);
        position.Duties[0].SortOrder.ShouldBe(0);
        position.Duties[1].SortOrder.ShouldBe(1);
    }

    [Fact]
    public void Vazifalar_tartibi_ozgartiriladi()
    {
        var position = Position.Create("Омборчи").Value;
        position.AddDuty("Биринчи");
        position.AddDuty("Иккинчи");
        position.AddDuty("Учинчи");

        var teskari = position.Duties.Select(d => d.Id).Reverse().ToList();
        position.ReorderDuties(teskari);

        position.Duties[0].Text.ShouldBe("Учинчи");
        position.Duties[2].Text.ShouldBe("Биринчи");
    }

    [Fact]
    public void Bosh_vazifa_qoshilmaydi()
    {
        var position = Position.Create("Омборчи").Value;

        var natija = position.AddDuty("   ");

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("lavozim.vazifa_bosh");
    }

    // ────────────────────── Ishdan ketish sababi ──────────────────────

    [Fact]
    public void Sabab_ixtiyoriylik_va_qayta_olish_belgilariga_ega()
    {
        // «Хайдалди» — majburiy va qayta olinmaydi
        var haydash = TerminationReason.Create("Ҳайдалди").Value;
        haydash.SetVoluntary(false);
        haydash.SetRehireable(false);

        haydash.IsVoluntary.ShouldBeFalse();
        haydash.IsRehireable.ShouldBeFalse();

        // «Ўқишга кетди» — ixtiyoriy va qayta olsa bo'ladi
        var oqish = TerminationReason.Create("Ўқишга кетди").Value;

        oqish.IsVoluntary.ShouldBeTrue();
        oqish.IsRehireable.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void Jarima_ogirlik_darajasi_chegaralangan(int daraja)
    {
        var penalty = PenaltyType.Create("Ҳайфсан").Value;

        penalty.SetSeverity(daraja).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Jarima_ogirlik_darajasi_1_dan_3_gacha()
    {
        var penalty = PenaltyType.Create("Ҳайфсан").Value;

        penalty.SetSeverity(2).IsSuccess.ShouldBeTrue();
        penalty.Severity.ShouldBe(2);
    }
}
