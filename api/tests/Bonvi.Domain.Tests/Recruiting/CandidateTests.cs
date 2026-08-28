using Bonvi.Domain.Recruiting;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Domain.Tests.Recruiting;

/// <summary>Nomzod va uning voronkadagi yo'li.</summary>
public sealed class CandidateTests
{
    private static readonly Guid VakansiyaId = Guid.CreateVersion7();
    private static readonly Guid AnketaId = Guid.CreateVersion7();
    private static readonly Guid KengashId = Guid.CreateVersion7();
    private static readonly DateOnly Sana = new(2026, 3, 1);

    private static Candidate Nomzod()
        => Candidate.Register(Ism(), VakansiyaId, CandidateSource.Manual).Value;

    private static FullName Ism(string familiya = "Каримов", string ism = "Азиз")
        => FullName.Create(familiya, ism, null).Value;

    // ────────────────────── Kiritish ──────────────────────

    [Fact]
    public void Vakansiyasiz_kiritilmaydi()
    {
        var natija = Candidate.Register(Ism(), Guid.Empty);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.vakansiya_yoq");
    }

    [Fact]
    public void Kelajakdagi_tugilgan_sana_qabul_qilinmaydi()
    {
        var ertaga = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        var natija = Candidate.Register(Ism(), VakansiyaId, birthDate: ertaga);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.tugilgan_sana");
    }

    [Fact]
    public void Yangi_nomzod_jarayonda_boladi()
    {
        var natija = Candidate.Register(
            Ism(),
            VakansiyaId,
            CandidateSource.TelegramBot,
            phone: PhoneNumber.Create("905088888").Value,
            speciality: "Иқтисодчи");

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.Status.ShouldBe(CandidateStatus.InProgress);
        natija.Value.IsActive.ShouldBeTrue();
        natija.Value.Source.ShouldBe(CandidateSource.TelegramBot, "manba botdan kelganini saqlaydi");
        natija.Value.CurrentStageId.ShouldBeNull("bosqichni use-case qo'yadi, domen emas");
        natija.Value.StageEntries.ShouldBeEmpty();
    }

    // ────────────────────── Voronka ──────────────────────

    [Fact]
    public void Bosqich_yozilganda_joriy_bosqich_yangilanadi()
    {
        var nomzod = Nomzod();

        var natija = nomzod.RecordStage(AnketaId, Sana);

        natija.IsSuccess.ShouldBeTrue();
        nomzod.CurrentStageId.ShouldBe(AnketaId);
        nomzod.StageEntries.Count.ShouldBe(1);
        nomzod.StageEntries[0].Outcome.ShouldBe(StageOutcome.Pending, "sukut — «kutilmoqda»");
    }

    [Fact]
    public void Ochiq_bosqichga_natija_yozilsa_AYNI_yozuv_toldiriladi()
    {
        // HR bosqichni oldindan ochadi («ertaga kengash»), natijani keyin
        // yozadi. Bu ikkita yozuv emas, BITTA yozuvning ikki holati.
        var nomzod = Nomzod();
        nomzod.RecordStage(KengashId, Sana);

        var natija = nomzod.RecordStage(
            KengashId, Sana.AddDays(1), StageOutcome.Passed, "Кенгаш хулосаси: маъқулланди");

        natija.IsSuccess.ShouldBeTrue();
        nomzod.StageEntries.Count.ShouldBe(1, "yangi yozuv qo'shilmaydi");
        nomzod.StageEntries[0].Outcome.ShouldBe(StageOutcome.Passed);
        nomzod.StageEntries[0].Date.ShouldBe(Sana.AddDays(1));
        nomzod.StageEntries[0].Conclusion.ShouldBe("Кенгаш хулосаси: маъқулланди");
    }

    [Fact]
    public void Boshqa_bosqich_yozilsa_yangi_yozuv_qoshiladi()
    {
        var nomzod = Nomzod();
        nomzod.RecordStage(AnketaId, Sana, StageOutcome.Passed);

        nomzod.RecordStage(KengashId, Sana.AddDays(2));

        nomzod.StageEntries.Count.ShouldBe(2);
        nomzod.CurrentStageId.ShouldBe(KengashId);
    }

    [Fact]
    public void Yakunlangan_bosqich_takrorlansa_ikkinchi_yozuv_qoshiladi()
    {
        // Qayta suhbat — haqiqiy hol va u tarixda alohida fakt bo'lishi kerak.
        var nomzod = Nomzod();
        nomzod.RecordStage(KengashId, Sana, StageOutcome.Failed, "Биринчи кенгаш: рад");

        nomzod.RecordStage(KengashId, Sana.AddDays(30), StageOutcome.Passed, "Иккинчи кенгаш: маъқул");

        nomzod.StageEntries.Count.ShouldBe(2);
        nomzod.StageEntries[0].Outcome.ShouldBe(StageOutcome.Failed, "eski xulosa qayta yozilmaydi");
        nomzod.StageEntries[1].Outcome.ShouldBe(StageOutcome.Passed);
    }

    [Fact]
    public void Voronka_orqaga_qaytishga_ruxsat_beradi()
    {
        // Bosqichlar tartibi BOR, lekin qat'iy ketma-ketlik ATAYLAB
        // majburlanmaydi: amalda HR bosqichni o'tkazib yuboradi yoki
        // nomzodni orqaga qaytaradi.
        var nomzod = Nomzod();
        nomzod.RecordStage(KengashId, Sana, StageOutcome.Passed);

        var natija = nomzod.RecordStage(AnketaId, Sana.AddDays(1));

        natija.IsSuccess.ShouldBeTrue();
        nomzod.CurrentStageId.ShouldBe(AnketaId);
    }

    [Fact]
    public void Bosqichsiz_yozuv_qabul_qilinmaydi()
    {
        var nomzod = Nomzod();

        var natija = nomzod.RecordStage(Guid.Empty, Sana);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("bosqich_yozuvi.bosqich_yoq");
    }

    // ────────────────────── Voronkadan chiqish ──────────────────────

    [Fact]
    public void Rad_etilgan_nomzod_voronkada_faol_qolmaydi()
    {
        var nomzod = Nomzod();

        var natija = nomzod.Reject(Sana, "Тажрибаси етарли эмас");

        natija.IsSuccess.ShouldBeTrue();
        nomzod.Status.ShouldBe(CandidateStatus.Rejected);
        nomzod.IsActive.ShouldBeFalse();
        nomzod.ClosedAt.ShouldBe(Sana);
        nomzod.ClosingNote.ShouldBe("Тажрибаси етарли эмас");
    }

    [Fact]
    public void Oz_voz_kechish_rad_etishdan_ALOHIDA_holat()
    {
        var nomzod = Nomzod();

        nomzod.Withdraw(Sana);

        nomzod.Status.ShouldBe(
            CandidateStatus.Withdrawn,
            "«biz rad etdik» va «u ketib qoldi» hisobotda boshqa-boshqa ma'no beradi");
    }

    [Fact]
    public void Ishga_olingan_nomzod_voronkadan_chiqadi()
    {
        var nomzod = Nomzod();

        var natija = nomzod.MarkHired(Sana);

        natija.IsSuccess.ShouldBeTrue();
        nomzod.Status.ShouldBe(CandidateStatus.Hired);
        nomzod.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Ikki_marta_ishga_olinmaydi()
    {
        var nomzod = Nomzod();
        nomzod.MarkHired(Sana);

        var natija = nomzod.MarkHired(Sana.AddDays(1));

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.jarayonda_emas");
    }

    [Fact]
    public void Rad_etilgan_nomzod_ishga_olinmaydi()
    {
        var nomzod = Nomzod();
        nomzod.Reject(Sana);

        var natija = nomzod.MarkHired(Sana.AddDays(1));

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.jarayonda_emas");
        nomzod.Status.ShouldBe(CandidateStatus.Rejected, "holat o'zgarmaydi");
    }

    [Fact]
    public void Voronkadan_chiqqan_nomzodga_bosqich_yozilmaydi()
    {
        var nomzod = Nomzod();
        nomzod.Reject(Sana);

        var natija = nomzod.RecordStage(KengashId, Sana.AddDays(1));

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.jarayonda_emas");
    }

    [Fact]
    public void Voronkadan_chiqqan_nomzod_kochirilmaydi()
    {
        var nomzod = Nomzod();
        nomzod.MarkHired(Sana);

        var natija = nomzod.Reassign(Guid.CreateVersion7());

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.jarayonda_emas");
    }

    [Fact]
    public void Kochirishda_bosqichlar_tarixi_saqlanadi()
    {
        var nomzod = Nomzod();
        nomzod.RecordStage(AnketaId, Sana, StageOutcome.Passed);
        var yangiVakansiya = Guid.CreateVersion7();

        var natija = nomzod.Reassign(yangiVakansiya);

        natija.IsSuccess.ShouldBeTrue();
        nomzod.VacancyId.ShouldBe(yangiVakansiya);
        nomzod.StageEntries.Count.ShouldBe(1, "telefon suhbatini qaytadan o'tkazish ma'nosiz");
        nomzod.CurrentStageId.ShouldBe(AnketaId);
    }

    // ────────────────────── Rezyume ──────────────────────

    [Fact]
    public void Rezyume_biriktiriladi_va_olib_tashlanadi()
    {
        var nomzod = Nomzod();

        var natija = nomzod.AttachResume("kalit-1", "C:\\Users\\hr\\rezyume.pdf", "application/pdf", 1024);

        natija.IsSuccess.ShouldBeTrue();
        nomzod.ResumeFileKey.ShouldBe("kalit-1");
        nomzod.ResumeFileName.ShouldBe("rezyume.pdf", "mijoz yuborgan yo'l qismi tashlanadi");
        nomzod.ResumeSizeBytes.ShouldBe(1024);

        nomzod.RemoveResume();

        nomzod.ResumeFileKey.ShouldBeNull();
        nomzod.ResumeFileName.ShouldBeNull();
        nomzod.ResumeContentType.ShouldBeNull();
        nomzod.ResumeSizeBytes.ShouldBeNull();
    }

    [Fact]
    public void Bosh_kalit_bilan_rezyume_biriktirilmaydi()
    {
        var nomzod = Nomzod();

        var natija = nomzod.AttachResume("  ", "rezyume.pdf", "application/pdf", 1024);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.rezyume_bosh");
    }
}
