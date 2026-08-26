using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Domain.Tests.Employees;

/// <summary>Rag'bat va jarima mantig'i (91.14 + HR so'ragan yangi jarima imkoniyati).</summary>
public sealed class IncentiveTests
{
    private static readonly Guid XodimId = Guid.CreateVersion7();
    private static readonly Guid TurId = Guid.CreateVersion7();
    private static readonly DateOnly Sana = new(2026, 3, 1);

    [Fact]
    public void Tursiz_yozuv_yaratilmaydi()
    {
        var natija = Incentive.Grant(XodimId, IncentiveKind.Award, Guid.Empty, Sana, typeHasAmount: false);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("ragbat_jarima.tur_bosh");
    }

    [Fact]
    public void Jarima_sababsiz_yaratilmaydi()
    {
        // Mehnat kodeksi 181-moddasi: intizomiy jazo sababi hujjatda
        // ko'rsatilishi shart — xuddi qora ro'yxat kabi.
        var natija = Incentive.Grant(XodimId, IncentiveKind.Penalty, TurId, Sana, typeHasAmount: false);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("ragbat_jarima.sabab_bosh");
    }

    [Fact]
    public void Ragbat_sababsiz_ham_yaratiladi()
    {
        Incentive.Grant(XodimId, IncentiveKind.Award, TurId, Sana, typeHasAmount: false)
            .IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Jarimada_sovga_bolishi_mumkin_emas()
    {
        var natija = Incentive.Grant(
            XodimId, IncentiveKind.Penalty, TurId, Sana, typeHasAmount: false,
            giftDescription: "Soat", reason: "Intizom buzilishi");

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("ragbat_jarima.sovga_jarimada");
    }

    [Fact]
    public void Pulli_tur_summasiz_yaratilmaydi()
    {
        // AwardType/PenaltyType.HasAmount — S2'da qo'shilgan, lekin
        // shu paytgacha ishlatilmagan metadata. Endi haqiqiy tekshiruv.
        var natija = Incentive.Grant(XodimId, IncentiveKind.Award, TurId, Sana, typeHasAmount: true);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("ragbat_jarima.summa_kerak");
    }

    [Fact]
    public void Pulsiz_tur_summa_bilan_yaratilmaydi()
    {
        var pul = Money.Create(500_000m).Value;

        var natija = Incentive.Grant(
            XodimId, IncentiveKind.Award, TurId, Sana, typeHasAmount: false, amount: pul);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("ragbat_jarima.summa_kerak_emas");
    }

    [Fact]
    public void Pulli_tur_summa_bilan_muvaffaqiyatli_yaratiladi()
    {
        var pul = Money.Create(1_500_000m).Value;

        var natija = Incentive.Grant(
            XodimId, IncentiveKind.Penalty, TurId, Sana, typeHasAmount: true,
            amount: pul, reason: "Intizom buzilishi", orderNo: "45-b", orderDate: Sana);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.Amount.ShouldBe(pul);
        natija.Value.OrderNo.ShouldBe("45-b");
    }

    [Fact]
    public void Update_turini_va_sanasini_ozgartiradi_lekin_turni_saqlaydi()
    {
        var incentive = Incentive
            .Grant(XodimId, IncentiveKind.Award, TurId, Sana, typeHasAmount: false)
            .Value;

        var yangiTur = Guid.CreateVersion7();
        var yangiSana = Sana.AddDays(10);

        var natija = incentive.Update(yangiTur, yangiSana, typeHasAmount: false, reason: "Tuzatildi");

        natija.IsSuccess.ShouldBeTrue();
        incentive.TypeId.ShouldBe(yangiTur);
        incentive.Date.ShouldBe(yangiSana);
        incentive.Reason.ShouldBe("Tuzatildi");
        incentive.Kind.ShouldBe(IncentiveKind.Award, "tur (rag'bat/jarima) Update orqali o'zgarmaydi");
    }

    [Fact]
    public void Update_jarimada_sababni_bosh_qoldirsa_rad_etiladi()
    {
        var incentive = Incentive
            .Grant(XodimId, IncentiveKind.Penalty, TurId, Sana, typeHasAmount: false, reason: "Sabab")
            .Value;

        var natija = incentive.Update(TurId, Sana, typeHasAmount: false, reason: null);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("ragbat_jarima.sabab_bosh");
        incentive.Reason.ShouldBe("Sabab", "muvaffaqiyatsiz tahrir eski qiymatni buzmasligi kerak");
    }
}
