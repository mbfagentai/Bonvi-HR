using Bonvi.Application.Recruiting.Candidates;
using Bonvi.Domain.Recruiting;
using NSubstitute;

namespace Bonvi.Application.Tests.Recruiting;

/// <summary>Nomzodni kiritish va voronka bo'ylab yurgizish — ulanish qatlami.</summary>
public sealed class CandidateHandlerTests
{
    private readonly RecruitingTestContext _context = new();

    private RegisterCandidateCommandHandler Kiritish => new(
        _context.Candidates,
        _context.Vacancies,
        _context.Stages,
        _context.Mapper,
        _context.Clock,
        _context.UnitOfWork);

    private RecordCandidateStageCommandHandler Bosqich => new(
        _context.Candidates,
        _context.Stages,
        _context.Mapper,
        _context.Employees.CurrentUser,
        _context.Clock,
        _context.UnitOfWork);

    private ReassignCandidateCommandHandler Kochirish => new(
        _context.Candidates, _context.Vacancies, _context.Mapper, _context.UnitOfWork);

    private CloseCandidateCommandHandler Yopish => new(
        _context.Candidates, _context.Mapper, _context.Clock, _context.UnitOfWork);

    // ────────────────────── Kiritish ──────────────────────

    [Fact]
    public async Task Nomavjud_vakansiyaga_kiritilmaydi()
    {
        var natija = await Kiritish.HandleAsync(
            new RegisterCandidateCommand(Guid.CreateVersion7(), "Каримов", "Азиз"),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("vakansiya.topilmadi");
    }

    [Fact]
    public async Task Yopilgan_vakansiyaga_kiritilmaydi()
    {
        var vakansiya = _context.ExistingVacancy(open: false);
        _context.Funnel(_context.ExistingStage());

        var natija = await Kiritish.HandleAsync(
            new RegisterCandidateCommand(vakansiya.Id, "Каримов", "Азиз"),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("vakansiya.qabul_qilmaydi");
    }

    [Fact]
    public async Task Bosqich_korsatilmasa_voronkaning_BIRINCHI_faol_bosqichi_olinadi()
    {
        var vakansiya = _context.ExistingVacancy();
        var anketa = _context.ExistingStage("Анкета", 0);
        var telefon = _context.ExistingStage("Телефон суҳбат", 1);
        _context.Funnel(anketa, telefon);

        var natija = await Kiritish.HandleAsync(
            new RegisterCandidateCommand(vakansiya.Id, "Каримов", "Азиз", Phone: "905088888"),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.CurrentStageId.ShouldBe(anketa.Id);
        natija.Value.Status.ShouldBe(CandidateStatus.InProgress);
        natija.Value.Stages.Count.ShouldBe(1, "nomzod darhol voronkaning birinchi ustuniga tushadi");
        natija.Value.Stages[0].Outcome.ShouldBe(StageOutcome.Pending);
        _context.Candidates.Received(1).Add(Arg.Any<Candidate>());
    }

    [Fact]
    public async Task Faol_bosqich_bolmasa_nomzod_kiritilmaydi()
    {
        var vakansiya = _context.ExistingVacancy();
        _context.Funnel();

        var natija = await Kiritish.HandleAsync(
            new RegisterCandidateCommand(vakansiya.Id, "Каримов", "Азиз"),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("bosqich.faol_yoq");
    }

    [Fact]
    public async Task Faolsizlantirilgan_bosqichga_kiritilmaydi()
    {
        var vakansiya = _context.ExistingVacancy();
        var eski = _context.ExistingStage("Кенгаш", 3, active: false);
        _context.Funnel(eski);

        var natija = await Kiritish.HandleAsync(
            new RegisterCandidateCommand(vakansiya.Id, "Каримов", "Азиз", StageId: eski.Id),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("bosqich.faol_emas");
    }

    [Fact]
    public async Task Notogri_telefon_bilan_kiritilmaydi()
    {
        var vakansiya = _context.ExistingVacancy();
        _context.Funnel(_context.ExistingStage());

        var natija = await Kiritish.HandleAsync(
            new RegisterCandidateCommand(vakansiya.Id, "Каримов", "Азиз", Phone: "123"),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldStartWith("telefon.");
    }

    [Fact]
    public async Task Botdan_kelgan_nomzodning_manbasi_saqlanadi()
    {
        var vakansiya = _context.ExistingVacancy();
        _context.Funnel(_context.ExistingStage());

        var natija = await Kiritish.HandleAsync(
            new RegisterCandidateCommand(
                vakansiya.Id, "Каримов", "Азиз", Source: CandidateSource.TelegramBot),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.Source.ShouldBe(
            CandidateSource.TelegramBot,
            "«bot qancha nomzod keltirdi» savolining javobi shu maydonda");
    }

    // ────────────────────── Voronka ──────────────────────

    [Fact]
    public async Task Ochirilgan_bosqichga_nomzod_otkazilmaydi()
    {
        var vakansiya = _context.ExistingVacancy();
        var anketa = _context.ExistingStage("Анкета", 0);
        var eski = _context.ExistingStage("Кенгаш", 3, active: false);
        _context.Funnel(anketa, eski);
        var nomzod = _context.ExistingCandidate(vakansiya, anketa);

        var natija = await Bosqich.HandleAsync(
            new RecordCandidateStageCommand(nomzod.Id, eski.Id),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("bosqich.faol_emas");
        nomzod.CurrentStageId.ShouldBe(anketa.Id, "nomzod joyida qoladi");
    }

    [Fact]
    public async Task Ochirilgan_bosqichda_TURGAN_nomzod_yoqolmaydi()
    {
        // Bosqichni faolsizlantirish nomzodni tarixidan ajratmasligi kerak:
        // uning kartasida bosqich NOMI ko'rinib turishi shart.
        var vakansiya = _context.ExistingVacancy();
        var kengash = _context.ExistingStage("Кенгаш", 3);
        _context.Funnel(kengash);
        var nomzod = _context.ExistingCandidate(vakansiya, kengash);

        kengash.Deactivate("Кенгаш бекор қилинди");

        var karta = await new GetCandidateQueryHandler(_context.Candidates, _context.Mapper)
            .HandleAsync(new GetCandidateQuery(nomzod.Id), TestContext.Current.CancellationToken);

        karta.IsSuccess.ShouldBeTrue();
        karta.Value.CurrentStageId.ShouldBe(kengash.Id);
        karta.Value.CurrentStage.ShouldBe("Кенгаш", "faolsizlantirilgan bosqichning nomi ham qaytadi");
        karta.Value.Stages.Count.ShouldBe(1);
        karta.Value.Stages[0].Stage.ShouldBe("Кенгаш");
    }

    [Fact]
    public async Task Kengash_xulosasi_oddiy_bosqich_yozuvi_bolib_saqlanadi()
    {
        // Foydalanuvchi qarori: ovoz berish YO'Q — HR bitta xulosa yozadi.
        var vakansiya = _context.ExistingVacancy();
        var kengash = _context.ExistingStage("Кенгаш", 3);
        _context.Funnel(kengash);
        var nomzod = _context.ExistingCandidate(vakansiya, kengash);

        var natija = await Bosqich.HandleAsync(
            new RecordCandidateStageCommand(
                nomzod.Id,
                kengash.Id,
                StageOutcome.Passed,
                RecruitingTestContext.Today,
                "Кенгаш хулосаси: ишга олинсин"),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.Stages.Count.ShouldBe(1, "ochiq yozuv TO'LDIRILADI, yangisi qo'shilmaydi");
        natija.Value.Stages[0].Outcome.ShouldBe(StageOutcome.Passed);
        natija.Value.Stages[0].Conclusion.ShouldBe("Кенгаш хулосаси: ишга олинсин");
    }

    [Fact]
    public async Task Voronkadan_chiqqan_nomzodga_bosqich_yozilmaydi()
    {
        var vakansiya = _context.ExistingVacancy();
        var anketa = _context.ExistingStage("Анкета", 0);
        _context.Funnel(anketa);
        var nomzod = _context.ExistingCandidate(vakansiya, anketa);
        nomzod.Reject(RecruitingTestContext.Today);

        var natija = await Bosqich.HandleAsync(
            new RecordCandidateStageCommand(nomzod.Id, anketa.Id),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.jarayonda_emas");
    }

    // ────────────────────── Ko'chirish va yopish ──────────────────────

    [Fact]
    public async Task Yopilgan_vakansiyaga_kochirilmaydi()
    {
        var vakansiya = _context.ExistingVacancy();
        var anketa = _context.ExistingStage();
        _context.Funnel(anketa);
        var nomzod = _context.ExistingCandidate(vakansiya, anketa);
        var yopiq = _context.ExistingVacancy(open: false);

        var natija = await Kochirish.HandleAsync(
            new ReassignCandidateCommand(nomzod.Id, yopiq.Id),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("vakansiya.qabul_qilmaydi");
    }

    [Fact]
    public async Task Rad_etish_va_voz_kechish_ALOHIDA_holatlar()
    {
        var vakansiya = _context.ExistingVacancy();
        var anketa = _context.ExistingStage();
        _context.Funnel(anketa);

        var raddiya = _context.ExistingCandidate(vakansiya, anketa);
        var vozKechgan = _context.ExistingCandidate(vakansiya, anketa);

        var birinchi = await Yopish.HandleAsync(
            new RejectCandidateCommand(raddiya.Id, Reason: "Тажрибаси етарли эмас"),
            TestContext.Current.CancellationToken);

        var ikkinchi = await Yopish.HandleAsync(
            new WithdrawCandidateCommand(vozKechgan.Id, Reason: "Бошқа жойга ишга кирди"),
            TestContext.Current.CancellationToken);

        birinchi.Value.Status.ShouldBe(CandidateStatus.Rejected);
        birinchi.Value.ClosedAt.ShouldBe(RecruitingTestContext.Today, "sana bo'sh bo'lsa — bugun");
        ikkinchi.Value.Status.ShouldBe(CandidateStatus.Withdrawn);
    }

    [Fact]
    public async Task Ikki_marta_yopilmaydi()
    {
        var vakansiya = _context.ExistingVacancy();
        var anketa = _context.ExistingStage();
        _context.Funnel(anketa);
        var nomzod = _context.ExistingCandidate(vakansiya, anketa);

        await Yopish.HandleAsync(
            new RejectCandidateCommand(nomzod.Id), TestContext.Current.CancellationToken);

        var natija = await Yopish.HandleAsync(
            new WithdrawCandidateCommand(nomzod.Id), TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.jarayonda_emas");
    }
}
