using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Domain.Tests.Employees;

/// <summary>
/// Mehnat munosabati va hodisalar mantig'i.
/// </summary>
/// <remarks>
/// Bu tizimning yuragi: Excel'da bitta o'zgarish 8 ta varaqqa qo'lda
/// ko'chirilardi. Bu yerda faqat bitta hodisa yoziladi, qolgan ro'yxatlar
/// undan hosil qilinadi. Shu sababli hodisalarning to'g'ri yozilishi —
/// eng muhim tekshiruv.
/// </remarks>
public sealed class EmploymentTests
{
    private static readonly Guid XodimId = Guid.CreateVersion7();
    private static readonly Guid FirmaId = Guid.CreateVersion7();
    private static readonly Guid BolimId = Guid.CreateVersion7();
    private static readonly Guid LavozimId = Guid.CreateVersion7();
    private static readonly DateOnly Kirgan = new(2026, 1, 15);

    private static Employment YaratilganMunosabat()
        => Employment.Hire(XodimId, FirmaId, Kirgan, BolimId, LavozimId, "12-b", Kirgan).Value;

    // ────────────────────── Ishga qabul ──────────────────────

    [Fact]
    public void Ishga_qabulda_birinchi_hodisa_yoziladi()
    {
        var m = YaratilganMunosabat();

        m.Events.Count.ShouldBe(1);
        m.Events[0].Type.ShouldBe(EmploymentEventType.Hire);
        m.Events[0].EffectiveDate.ShouldBe(Kirgan);
        m.Events[0].OrderNo.ShouldBe("12-b");
        m.Events[0].ToPositionId.ShouldBe(LavozimId);
    }

    [Fact]
    public void Yangi_munosabat_sinov_muddatida_va_toliq_stavkada_boshlanadi()
    {
        var m = YaratilganMunosabat();

        m.Status.ShouldBe(EmploymentStatus.Probation);
        m.Rate.ShouldBe(1.0m);
        m.IsPrimary.ShouldBeTrue();
    }

    [Fact]
    public void Xodimsiz_yoki_firmasiz_munosabat_yaratilmaydi()
    {
        Employment.Hire(Guid.Empty, FirmaId, Kirgan).IsFailure.ShouldBeTrue();
        Employment.Hire(XodimId, Guid.Empty, Kirgan).IsFailure.ShouldBeTrue();
    }

    // ────────────────────── Stavka ──────────────────────

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(1.5)]
    public void Togri_stavka_qabul_qilinadi(decimal stavka)
    {
        var m = YaratilganMunosabat();

        m.SetRate(stavka).IsSuccess.ShouldBeTrue();
        m.Rate.ShouldBe(stavka);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.5)]
    [InlineData(2.5)]
    public void Notogri_stavka_rad_etiladi(decimal stavka)
    {
        var natija = YaratilganMunosabat().SetRate(stavka);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("mehnat.stavka");
    }

    // ────────────────────── Ko'chirish ──────────────────────

    [Fact]
    public void Lavozim_ozgarganda_hodisa_Promotion_boladi()
    {
        var m = YaratilganMunosabat();
        var yangiLavozim = Guid.CreateVersion7();

        m.Transfer(new DateOnly(2026, 6, 1), null, yangiLavozim, orderNo: "45-b").IsSuccess.ShouldBeTrue();

        var hodisa = m.Events[^1];
        hodisa.Type.ShouldBe(EmploymentEventType.Promotion);
        hodisa.FromPositionId.ShouldBe(LavozimId);
        hodisa.ToPositionId.ShouldBe(yangiLavozim);
        m.PositionId.ShouldBe(yangiLavozim);
    }

    [Fact]
    public void Faqat_bolim_ozgarganda_hodisa_Transfer_boladi()
    {
        var m = YaratilganMunosabat();
        var yangiBolim = Guid.CreateVersion7();

        m.Transfer(new DateOnly(2026, 6, 1), yangiBolim, null).IsSuccess.ShouldBeTrue();

        var hodisa = m.Events[^1];
        hodisa.Type.ShouldBe(EmploymentEventType.Transfer);
        hodisa.FromDepartmentId.ShouldBe(BolimId);
        hodisa.ToDepartmentId.ShouldBe(yangiBolim);
        m.PositionId.ShouldBe(LavozimId, "lavozim o'zgarmasligi kerak");
    }

    [Fact]
    public void Kochirishda_eski_va_yangi_qiymat_hodisada_saqlanadi()
    {
        // Excel'da bu «91.3» varag'ida ikki ustun bo'lib yozilardi.
        // Endi ular hodisa ichida — ro'yxat esa hodisalardan hosil qilinadi.
        var m = YaratilganMunosabat();
        m.SetSalary(Money.Create(4_000_000m).Value, Kirgan);

        m.Transfer(
            new DateOnly(2026, 6, 1),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Money.Create(5_500_000m).Value);

        var hodisa = m.Events[^1];
        hodisa.FromSalary!.Amount.ShouldBe(4_000_000m);
        hodisa.ToSalary!.Amount.ShouldBe(5_500_000m);
    }

    // ────────────────────── Ishdan bo'shatish ──────────────────────

    [Fact]
    public void Kochirilgan_boshatish_tarixiy_deb_belgilanadi()
    {
        // ETL uchun. Farqi bitta, lekin muhim: ko'chirilgan yozuvda buyruq
        // raqami bo'lmasligi yoki sana taxminiy bo'lishi mumkin, va uni
        // tizimda kiritilgan aniq buyruqdan ajratib turish kerak.
        var m = YaratilganMunosabat();
        var ketgan = new DateOnly(2026, 8, 20);

        m.ImportTermination(ketgan, reasonId: null).IsSuccess.ShouldBeTrue();

        m.Status.ShouldBe(EmploymentStatus.Terminated);
        m.TerminationDate.ShouldBe(ketgan);
        m.Events[^1].Type.ShouldBe(EmploymentEventType.Termination);
        m.Events[^1].IsImported.ShouldBeTrue();
    }

    [Fact]
    public void Kochirilgan_boshatishda_ham_tekshiruvlar_saqlanadi()
    {
        // Excel'da ketish sanasi ishga kirish sanasidan oldin bo'lgan
        // qatorlar bor. Ular jimgina qabul qilinmasligi kerak —
        // ko'chirish hisobotiga tushishi shart.
        var m = YaratilganMunosabat();

        m.ImportTermination(Kirgan.AddDays(-1), reasonId: null).IsFailure.ShouldBeTrue();
        m.Status.ShouldNotBe(EmploymentStatus.Terminated);
    }

    [Fact]
    public void Kochirilgan_boshatish_ikki_marta_yozilmaydi()
    {
        var m = YaratilganMunosabat();
        var ketgan = new DateOnly(2026, 8, 20);

        m.ImportTermination(ketgan, reasonId: null);

        m.ImportTermination(ketgan, reasonId: null).IsFailure.ShouldBeTrue();
        m.Events.Count(hodisa => hodisa.Type == EmploymentEventType.Termination).ShouldBe(1);
    }

    [Fact]
    public void Ishdan_boshatilganda_holat_va_hodisa_yoziladi()
    {
        var m = YaratilganMunosabat();
        var sabab = Guid.CreateVersion7();
        var ketgan = new DateOnly(2026, 8, 20);

        m.Terminate(ketgan, sabab, "78-b", ketgan, "O'z xohishi bilan").IsSuccess.ShouldBeTrue();

        m.Status.ShouldBe(EmploymentStatus.Terminated);
        m.TerminationDate.ShouldBe(ketgan);
        m.TerminationReasonId.ShouldBe(sabab);
        m.Events[^1].Type.ShouldBe(EmploymentEventType.Termination);
    }

    [Fact]
    public void Ketish_sanasi_kirish_sanasidan_oldin_bola_olmaydi()
    {
        var natija = YaratilganMunosabat()
            .Terminate(Kirgan.AddDays(-1), null);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("mehnat.ketish_sanasi");
    }

    [Fact]
    public void Ikki_marta_boshatib_bolmaydi()
    {
        var m = YaratilganMunosabat();
        m.Terminate(new DateOnly(2026, 8, 20), null);

        var natija = m.Terminate(new DateOnly(2026, 9, 1), null);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("mehnat.allaqachon_tugagan");
    }

    [Fact]
    public void Ishdan_ketgandan_keyin_ozgartirish_taqiqlanadi()
    {
        var m = YaratilganMunosabat();
        m.Terminate(new DateOnly(2026, 8, 20), null);

        m.Transfer(new DateOnly(2026, 9, 1), Guid.CreateVersion7(), null)
            .Error.Code.ShouldBe("mehnat.tugagan");
        m.SetSalary(Money.Create(1000m).Value, new DateOnly(2026, 9, 1))
            .Error.Code.ShouldBe("mehnat.tugagan");
        m.AddAdditionalDuty(new DateOnly(2026, 9, 1), "Qo'shimcha ish")
            .Error.Code.ShouldBe("mehnat.tugagan");
    }

    // ────────────────────── Ishlagan kunlar ──────────────────────

    [Fact]
    public void Ishlagan_kunlar_ketish_sanasigacha_hisoblanadi()
    {
        var m = YaratilganMunosabat();
        m.Terminate(Kirgan.AddDays(100), null);

        m.WorkedDays(new DateOnly(2026, 12, 31)).ShouldBe(100, "ketgandan keyin hisob to'xtaydi");
    }

    [Fact]
    public void Hali_ishlayotgan_xodimda_kunlar_bugungacha_hisoblanadi()
    {
        var m = YaratilganMunosabat();

        m.WorkedDays(Kirgan.AddDays(45)).ShouldBe(45);
    }

    // ────────────────────── Sinov muddati ──────────────────────

    [Fact]
    public void Sinov_muddati_tugaganda_holat_Active_ga_otadi()
    {
        var m = YaratilganMunosabat();

        m.CompleteProbation(Kirgan.AddDays(90)).IsSuccess.ShouldBeTrue();

        m.Status.ShouldBe(EmploymentStatus.Active);
        m.Events[^1].Type.ShouldBe(EmploymentEventType.ProbationResult);
    }

    [Fact]
    public void Sinovda_bolmagan_xodimda_takroriy_yakunlash_rad_etiladi()
    {
        var m = YaratilganMunosabat();
        m.CompleteProbation(Kirgan.AddDays(90));

        m.CompleteProbation(Kirgan.AddDays(120)).Error.Code.ShouldBe("mehnat.sinovda_emas");
    }

    // ────────────────────── Qo'shimcha vazifa ──────────────────────

    [Fact]
    public void Qoshimcha_vazifa_hodisa_sifatida_yoziladi()
    {
        var m = YaratilganMunosabat();

        m.AddAdditionalDuty(
            new DateOnly(2026, 3, 1),
            "Kadrlar bo'limini yuritish",
            Money.Create(500_000m).Value,
            "23-b").IsSuccess.ShouldBeTrue();

        var hodisa = m.Events[^1];
        hodisa.Type.ShouldBe(EmploymentEventType.AdditionalDuty);
        hodisa.Note.ShouldBe("Kadrlar bo'limini yuritish");
        hodisa.ToSalary!.Amount.ShouldBe(500_000m);
    }

    [Fact]
    public void Bosh_vazifa_matni_rad_etiladi()
    {
        YaratilganMunosabat()
            .AddAdditionalDuty(new DateOnly(2026, 3, 1), "   ")
            .Error.Code.ShouldBe("mehnat.vazifa_bosh");
    }

    // ────────────────────── Tarixni ko'chirish ──────────────────────

    [Fact]
    public void Kochirilgan_hodisa_alohida_belgilanadi()
    {
        // Excel'dagi tarix tartibsiz: ishdan ketgandan keyin lavozim
        // o'zgarishi yozilgan bo'lishi mumkin. Shu sababli migratsiya
        // domen metodlarini emas, `ImportEvent` ni ishlatadi.
        var m = YaratilganMunosabat();

        m.ImportEvent(EmploymentEvent.Import(
            m.Id,
            EmploymentEventType.Promotion,
            new DateOnly(2024, 5, 10),
            orderNo: "eski-15"));

        m.Events[^1].IsImported.ShouldBeTrue();
        m.Events[0].IsImported.ShouldBeFalse("tizimda kiritilgan hodisa ko'chirilgan emas");
    }

    [Fact]
    public void Ishga_qabul_hodisasi_nomzodga_keyin_boglanadi()
    {
        // Excel'dan ko'chirishda tartib teskari: xodim allaqachon bor,
        // kengash xulosasi esa keyingi qadamda o'qiladi. Shu sababli
        // bog'lanish hodisa yaratilgandan KEYIN yoziladi.
        var m = YaratilganMunosabat();
        var nomzodId = Guid.CreateVersion7();

        m.Events[0].LinkCandidate(nomzodId).ShouldBeTrue();
        m.Events[0].CandidateId.ShouldBe(nomzodId);
    }

    [Fact]
    public void Mavjud_nomzod_boglanishi_ustidan_yozilmaydi()
    {
        // Tizimda kiritilgan haqiqiy bog'lanish ko'chirishdagi ism
        // bo'yicha topilgan taxminiy moslikdan ishonchliroq.
        var m = YaratilganMunosabat();
        var birinchi = Guid.CreateVersion7();

        _ = m.Events[0].LinkCandidate(birinchi);

        m.Events[0].LinkCandidate(Guid.CreateVersion7()).ShouldBeFalse();
        m.Events[0].CandidateId.ShouldBe(birinchi);
    }

    [Fact]
    public void Bosh_nomzod_identifikatori_boglanmaydi()
    {
        var m = YaratilganMunosabat();

        m.Events[0].LinkCandidate(Guid.Empty).ShouldBeFalse();
        m.Events[0].CandidateId.ShouldBeNull();
    }
}
