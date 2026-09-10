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
        _context.Settings,
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
        _context.Candidates,
        _context.Vacancies,
        _context.Mapper,
        _context.Employees.CurrentUser,
        _context.UnitOfWork);

    private CloseCandidateCommandHandler Yopish => new(
        _context.Candidates, _context.Mapper, _context.Clock, _context.UnitOfWork);

    private ReturnCandidateFromReserveCommandHandler Qaytarish => new(
        _context.Candidates,
        _context.Mapper,
        _context.Employees.CurrentUser,
        _context.UnitOfWork);

    private InviteCandidateToInterviewCommandHandler Suhbat => new(
        _context.Candidates,
        _context.Stages,
        _context.Mapper,
        _context.Employees.CurrentUser,
        _context.Clock,
        _context.UnitOfWork);

    // ────────────────────── Kiritish ──────────────────────

    [Fact]
    public async Task Nomavjud_vakansiyaga_kiritilmaydi()
    {
        var natija = await Kiritish.HandleAsync(
            new RegisterCandidateCommand("Каримов", "Азиз", Guid.CreateVersion7()),
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
            new RegisterCandidateCommand("Каримов", "Азиз", vakansiya.Id),
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
            new RegisterCandidateCommand("Каримов", "Азиз", vakansiya.Id, Phone: "905088888"),
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
            new RegisterCandidateCommand("Каримов", "Азиз", vakansiya.Id),
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
            new RegisterCandidateCommand("Каримов", "Азиз", vakansiya.Id, StageId: eski.Id),
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
            new RegisterCandidateCommand("Каримов", "Азиз", vakansiya.Id, Phone: "123"),
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
                "Каримов", "Азиз", vakansiya.Id, Source: CandidateSource.TelegramBot),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.Source.ShouldBe(
            CandidateSource.TelegramBot,
            "«bot qancha nomzod keltirdi» savolining javobi shu maydonda");
    }

    [Fact]
    public async Task Vakansiyasiz_nomzod_Umumiy_anketaga_tushadi()
    {
        // Ochiq ish o'rni bo'lmaganda ham anketa qabul qilinishi kerak —
        // butun funksiyaning maqsadi shu.
        var umumiy = _context.GeneralPoolVacancy();
        _context.Funnel(_context.ExistingStage());

        var natija = await Kiritish.HandleAsync(
            new RegisterCandidateCommand("Каримов", "Азиз"),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Code : null);
        natija.Value.VacancyId.ShouldBe(umumiy.Id);
    }

    [Fact]
    public async Task Umumiy_anketa_sozlanmagan_bolsa_ANIQ_xato_qaytadi()
    {
        // Bu holat faqat urug'lantirish ishlamaganda bo'ladi va uni
        // DARHOL ko'rish kerak: aks holda botdan kelgan barcha anketa
        // jimgina yo'qolardi.
        _context.Funnel(_context.ExistingStage());

        var natija = await Kiritish.HandleAsync(
            new RegisterCandidateCommand("Каримов", "Азиз"),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.umumiy_anketa_yoq");
    }

    [Fact]
    public async Task Yopilgan_Umumiy_anketa_ham_ANIQ_xato_beradi()
    {
        // HR xizmat vakansiyasini qo'lda yopishi mumkin. Bunda
        // «vakansiya.qabul_qilmaydi» chalg'itardi: mijoz hech qanday
        // vakansiya yubormagan va o'z so'rovida xato izlardi.
        _context.GeneralPoolVacancy(open: false);
        _context.Funnel(_context.ExistingStage());

        var natija = await Kiritish.HandleAsync(
            new RegisterCandidateCommand("Каримов", "Азиз"),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.umumiy_anketa_yoq");
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

    // ────────────────────── Rezerv ──────────────────────

    [Fact]
    public async Task Rezervga_otkazilgan_nomzod_saqlanadi()
    {
        var vakansiya = _context.ExistingVacancy();
        var anketa = _context.ExistingStage();
        _context.Funnel(anketa);
        var nomzod = _context.ExistingCandidate(vakansiya, anketa);

        var natija = await Yopish.HandleAsync(
            new ReserveCandidateCommand(nomzod.Id, Reason: "Ҳозир ўрин йўқ"),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);
        natija.Value.Status.ShouldBe(CandidateStatus.Reserve);
        natija.Value.ClosedAt.ShouldBe(RecruitingTestContext.Today, "sana bo'sh bo'lsa — bugun");
        natija.Value.ClosingNote.ShouldBe("Ҳозир ўрин йўқ");
        await _context.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Nomavjud_nomzod_rezervga_otkazilmaydi()
    {
        var natija = await Yopish.HandleAsync(
            new ReserveCandidateCommand(Guid.CreateVersion7()),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.topilmadi");
    }

    [Fact]
    public async Task Rad_etilgan_nomzod_rezervga_otkazilmaydi()
    {
        var vakansiya = _context.ExistingVacancy();
        var anketa = _context.ExistingStage();
        _context.Funnel(anketa);
        var nomzod = _context.ExistingCandidate(vakansiya, anketa);
        nomzod.Reject(RecruitingTestContext.Today);

        var natija = await Yopish.HandleAsync(
            new ReserveCandidateCommand(nomzod.Id), TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.jarayonda_emas");
    }

    [Fact]
    public async Task Rezervdan_qaytarilgan_nomzod_voronkaga_tushadi()
    {
        var vakansiya = _context.ExistingVacancy();
        var kengash = _context.ExistingStage("Кенгаш", 1);
        _context.Funnel(kengash);
        var nomzod = _context.ExistingCandidate(vakansiya, kengash);
        nomzod.MoveToReserve(RecruitingTestContext.Today, "Ҳозир ўрин йўқ");

        var natija = await Qaytarish.HandleAsync(
            new ReturnCandidateFromReserveCommand(nomzod.Id), TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);
        natija.Value.Status.ShouldBe(CandidateStatus.InProgress);
        natija.Value.ClosedAt.ShouldBeNull();
        natija.Value.ClosingNote.ShouldBeNull();
        natija.Value.CurrentStageId.ShouldBe(kengash.Id, "qayerda to'xtagan bo'lsa, o'sha yerdan davom etadi");
        await _context.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rezervda_bolmagan_nomzod_qaytarilmaydi()
    {
        var vakansiya = _context.ExistingVacancy();
        var anketa = _context.ExistingStage();
        _context.Funnel(anketa);
        var nomzod = _context.ExistingCandidate(vakansiya, anketa);
        nomzod.Reject(RecruitingTestContext.Today);

        var natija = await Qaytarish.HandleAsync(
            new ReturnCandidateFromReserveCommand(nomzod.Id), TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.rezervda_emas");
        await _context.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Nomavjud_nomzod_qaytarilmaydi()
    {
        var natija = await Qaytarish.HandleAsync(
            new ReturnCandidateFromReserveCommand(Guid.CreateVersion7()),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.topilmadi");
    }

    // ────────────────────── Keyingi aloqa sanasi ──────────────────────

    [Fact]
    public async Task Bosqich_izoh_va_ALOQA_sanasi_bilan_yoziladi()
    {
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
                "Кенгаш хулосаси: ишга олинсин",
                NextContactDate: RecruitingTestContext.Today.AddDays(5)),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Code : null);
        natija.Value.Stages[0].Conclusion.ShouldBe("Кенгаш хулосаси: ишга олинсин");
        natija.Value.NextContactDate.ShouldBe(
            RecruitingTestContext.Today.AddDays(5),
            "oynadagi ikkala maydon ham BITTA so'rovda saqlanadi");
        await _context.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Bayroq_berilsa_aloqa_sanasi_TOZALANADI()
    {
        var vakansiya = _context.ExistingVacancy();
        var anketa = _context.ExistingStage();
        _context.Funnel(anketa);
        var nomzod = _context.ExistingCandidate(vakansiya, anketa);
        nomzod.SetNextContactDate(RecruitingTestContext.Today.AddDays(3));

        // Ikkalasi birga berilgan (mos kelmaydigan) so'rovda TOZALASH ustun
        // keladi — xato qaytarilmaydi.
        var natija = await Bosqich.HandleAsync(
            new RecordCandidateStageCommand(
                nomzod.Id,
                anketa.Id,
                NextContactDate: RecruitingTestContext.Today.AddDays(9),
                ClearNextContact: true),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Code : null);
        natija.Value.NextContactDate.ShouldBeNull();
        nomzod.NextContactDate.ShouldBeNull();
    }

    /// <summary>Voronkada kartani SUDRAB o'tkazish sanani o'chirmasligi kerak.</summary>
    /// <remarks>
    /// ⭐ Eng jimgina yo'qoladigan xato: sudrash oynasiz ishlaydi va faqat
    /// bosqichni yuboradi. «null — tozalash» shartnomasida u HR besh daqiqa
    /// oldin qo'ygan qo'ng'iroq rejasini o'chirib tashlardi va buni hech kim
    /// payqamasdi.
    /// </remarks>
    [Fact]
    public async Task Ikkala_maydon_berilmasa_aloqa_sanasi_TEGILMAYDI()
    {
        var vakansiya = _context.ExistingVacancy();
        var anketa = _context.ExistingStage("Анкета", 0);
        var kengash = _context.ExistingStage("Кенгаш", 3);
        _context.Funnel(anketa, kengash);
        var nomzod = _context.ExistingCandidate(vakansiya, anketa);
        nomzod.SetNextContactDate(RecruitingTestContext.Today.AddDays(4));

        var natija = await Bosqich.HandleAsync(
            new RecordCandidateStageCommand(nomzod.Id, kengash.Id),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Code : null);
        natija.Value.CurrentStageId.ShouldBe(kengash.Id, "bosqich baribir o'zgaradi");
        natija.Value.NextContactDate.ShouldBe(
            RecruitingTestContext.Today.AddDays(4),
            "sudrash sanaga TEGMAYDI");
    }

    [Fact]
    public async Task Rad_etish_aloqa_sanasini_TOZALAYDI()
    {
        var vakansiya = _context.ExistingVacancy();
        var anketa = _context.ExistingStage();
        _context.Funnel(anketa);
        var nomzod = _context.ExistingCandidate(vakansiya, anketa);
        nomzod.SetNextContactDate(RecruitingTestContext.Today.AddDays(3));

        var natija = await Yopish.HandleAsync(
            new RejectCandidateCommand(nomzod.Id, Reason: "Тажрибаси етарли эмас"),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Code : null);
        natija.Value.NextContactDate.ShouldBeNull(
            "rad etilgan nomzod «bugun bog'lanish kerak» ro'yxatida qolmasligi kerak");
    }

    [Fact]
    public async Task Rezerv_aloqa_sanasini_SAQLAYDI()
    {
        var vakansiya = _context.ExistingVacancy();
        var anketa = _context.ExistingStage();
        _context.Funnel(anketa);
        var nomzod = _context.ExistingCandidate(vakansiya, anketa);
        nomzod.SetNextContactDate(RecruitingTestContext.Today.AddDays(3));

        var saqlangan = await Yopish.HandleAsync(
            new ReserveCandidateCommand(nomzod.Id, Reason: "Ҳозир ўрин йўқ"),
            TestContext.Current.CancellationToken);

        saqlangan.IsSuccess.ShouldBeTrue(saqlangan.IsFailure ? saqlangan.Error.Code : null);
        saqlangan.Value.NextContactDate.ShouldBe(
            RecruitingTestContext.Today.AddDays(3),
            "rezerv chiqish emas, KECHIKTIRISH — qo'ng'iroq rejasi o'rinli");

        // Yangi sana berilsa esa u qo'yiladi.
        nomzod.ReturnFromReserve();

        var yangilangan = await Yopish.HandleAsync(
            new ReserveCandidateCommand(
                nomzod.Id, NextContactDate: RecruitingTestContext.Today.AddDays(60)),
            TestContext.Current.CancellationToken);

        yangilangan.IsSuccess.ShouldBeTrue(yangilangan.IsFailure ? yangilangan.Error.Code : null);
        yangilangan.Value.NextContactDate.ShouldBe(RecruitingTestContext.Today.AddDays(60));
    }

    // ────────────────────── Suhbatga chaqirish ──────────────────────

    /// <summary>Suhbat vaqti — «bugun» dan keyin, ya'ni domen to'sig'idan o'tadi.</summary>
    private static readonly DateTimeOffset SuhbatVaqti =
        new(2026, 3, 12, 15, 0, 0, TimeSpan.FromHours(5));

    [Fact]
    public async Task Suhbatga_chaqirishda_bosqich_berilsa_izoh_XULOSAGA_tushadi()
    {
        var vakansiya = _context.ExistingVacancy();
        var kengash = _context.ExistingStage("Кенгаш", 3);
        _context.Funnel(kengash);
        var nomzod = _context.ExistingCandidate(vakansiya);

        var natija = await Suhbat.HandleAsync(
            new InviteCandidateToInterviewCommand(
                nomzod.Id, kengash.Id, SuhbatVaqti, "Кенгашга чақирилди"),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Code : null);
        natija.Value.Stages.Count.ShouldBe(1);
        natija.Value.Stages[0].StageId.ShouldBe(kengash.Id);
        natija.Value.Stages[0].Conclusion.ShouldBe("Кенгашга чақирилди");
        natija.Value.Notes.ShouldBeEmpty("bosqich yozuvi bor — izoh alohida jadvalga TUSHMAYDI");
    }

    /// <summary>Suhbat ko'chirilganda izoh yo'qolmasligi kerak.</summary>
    /// <remarks>
    /// Ilgari bu matn interfeysda umuman so'ralmasdi, so'rovda kelib qolsa
    /// ham JIMGINA tashlanardi — aynan shu nosozlik tuzatilyapti.
    /// </remarks>
    [Fact]
    public async Task Suhbat_kochirilsa_izoh_IZOHLAR_jadvaliga_tushadi()
    {
        var vakansiya = _context.ExistingVacancy();
        var kengash = _context.ExistingStage("Кенгаш", 3);
        _context.Funnel(kengash);
        var nomzod = _context.ExistingCandidate(vakansiya, kengash);
        var kim = Guid.CreateVersion7();
        _context.Employees.CurrentUser.UserId.Returns(kim);

        var natija = await Suhbat.HandleAsync(
            new InviteCandidateToInterviewCommand(
                nomzod.Id, StageId: null, SuhbatVaqti, "Номзод сўради, эртага кўчирилди"),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Code : null);
        natija.Value.Notes.Count.ShouldBe(1);
        natija.Value.Notes[0].Kind.ShouldBe(CandidateNoteKind.InterviewRescheduled);
        natija.Value.Notes[0].Text.ShouldBe("Номзод сўради, эртага кўчирилди");
        natija.Value.Notes[0].CreatedBy.ShouldBe(kim);
        natija.Value.Stages.Count.ShouldBe(1, "bosqich o'zgarmadi — yangi yozuv qo'shilmaydi");
        natija.Value.InterviewAt.ShouldBe(SuhbatVaqti.ToUniversalTime());
    }

    [Fact]
    public async Task Suhbat_kochirilganda_BOSH_izoh_yozuv_yaratmaydi()
    {
        var vakansiya = _context.ExistingVacancy();
        var anketa = _context.ExistingStage();
        _context.Funnel(anketa);
        var nomzod = _context.ExistingCandidate(vakansiya, anketa);

        var natija = await Suhbat.HandleAsync(
            new InviteCandidateToInterviewCommand(nomzod.Id, StageId: null, SuhbatVaqti, "   "),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Code : null);
        natija.Value.Notes.ShouldBeEmpty("«izoh yozilmadi» — yozuvsiz ko'rinadi");
    }

    [Fact]
    public async Task Suhbatga_chaqirishda_ham_aloqa_sanasi_uch_holatli()
    {
        var vakansiya = _context.ExistingVacancy();
        var anketa = _context.ExistingStage();
        _context.Funnel(anketa);
        var nomzod = _context.ExistingCandidate(vakansiya, anketa);
        nomzod.SetNextContactDate(RecruitingTestContext.Today.AddDays(4));

        // 1. Ikkala maydon ham berilmadi — sana JOYIDA qoladi.
        var tegilmagan = await Suhbat.HandleAsync(
            new InviteCandidateToInterviewCommand(nomzod.Id, StageId: null, SuhbatVaqti),
            TestContext.Current.CancellationToken);

        tegilmagan.IsSuccess.ShouldBeTrue(tegilmagan.IsFailure ? tegilmagan.Error.Code : null);
        tegilmagan.Value.NextContactDate.ShouldBe(RecruitingTestContext.Today.AddDays(4));

        // 2. Yangi sana berildi — qo'yiladi.
        var qoyilgan = await Suhbat.HandleAsync(
            new InviteCandidateToInterviewCommand(
                nomzod.Id,
                StageId: null,
                SuhbatVaqti,
                NextContactDate: RecruitingTestContext.Today.AddDays(7)),
            TestContext.Current.CancellationToken);

        qoyilgan.Value.NextContactDate.ShouldBe(RecruitingTestContext.Today.AddDays(7));

        // 3. Bayroq berildi — tozalanadi.
        var tozalangan = await Suhbat.HandleAsync(
            new InviteCandidateToInterviewCommand(
                nomzod.Id, StageId: null, SuhbatVaqti, ClearNextContact: true),
            TestContext.Current.CancellationToken);

        tozalangan.Value.NextContactDate.ShouldBeNull();
    }

    // ────────────────────── Amal izohlari ──────────────────────

    [Fact]
    public async Task Kochirishda_izoh_IZOHLAR_jadvaliga_yoziladi()
    {
        var vakansiya = _context.ExistingVacancy();
        var anketa = _context.ExistingStage();
        _context.Funnel(anketa);
        var nomzod = _context.ExistingCandidate(vakansiya, anketa);
        var boshqa = _context.ExistingVacancy();
        var kim = Guid.CreateVersion7();
        _context.Employees.CurrentUser.UserId.Returns(kim);

        var natija = await Kochirish.HandleAsync(
            new ReassignCandidateCommand(
                nomzod.Id,
                boshqa.Id,
                "Сотувчига мос эмас, кассирга кўчирилди",
                RecruitingTestContext.Today.AddDays(2)),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Code : null);
        natija.Value.VacancyId.ShouldBe(boshqa.Id);
        natija.Value.Notes.Count.ShouldBe(1);
        natija.Value.Notes[0].Kind.ShouldBe(CandidateNoteKind.Reassign);
        natija.Value.Notes[0].Text.ShouldBe("Сотувчига мос эмас, кассирга кўчирилди");
        natija.Value.Notes[0].CreatedBy.ShouldBe(kim);
        natija.Value.NextContactDate.ShouldBe(RecruitingTestContext.Today.AddDays(2));
        natija.Value.Note.ShouldBeNull("bu nomzodning uzoq muddatli TAVSIFI emas");
    }

    [Fact]
    public async Task Kochirishda_BOSH_izoh_yozuv_yaratmaydi()
    {
        // Domen `AddNote` bo'sh matnda ISTISNO tashlaydi, xato kodi
        // qaytarmaydi — ya'ni ishlovchi uni umuman chaqirmasligi kerak.
        // Tekshiruv tushib qolsa, bu test 500 bilan yiqiladi.
        var vakansiya = _context.ExistingVacancy();
        var anketa = _context.ExistingStage();
        _context.Funnel(anketa);
        var nomzod = _context.ExistingCandidate(vakansiya, anketa);
        var boshqa = _context.ExistingVacancy();

        var natija = await Kochirish.HandleAsync(
            new ReassignCandidateCommand(nomzod.Id, boshqa.Id, "   "),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Code : null);
        natija.Value.Notes.ShouldBeEmpty();
        nomzod.Notes.ShouldBeEmpty();
    }

    [Fact]
    public async Task Kochirishda_sana_berilmasa_aloqa_sanasi_TEGILMAYDI()
    {
        var vakansiya = _context.ExistingVacancy();
        var anketa = _context.ExistingStage();
        _context.Funnel(anketa);
        var nomzod = _context.ExistingCandidate(vakansiya, anketa);
        nomzod.SetNextContactDate(RecruitingTestContext.Today.AddDays(6));
        var boshqa = _context.ExistingVacancy();

        var natija = await Kochirish.HandleAsync(
            new ReassignCandidateCommand(nomzod.Id, boshqa.Id),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Code : null);
        natija.Value.NextContactDate.ShouldBe(
            RecruitingTestContext.Today.AddDays(6),
            "bu amalda «tozalash» yo'q");
    }

    [Fact]
    public async Task Rezervdan_qaytarishda_izoh_IZOHLAR_jadvaliga_yoziladi()
    {
        var vakansiya = _context.ExistingVacancy();
        var kengash = _context.ExistingStage("Кенгаш", 1);
        _context.Funnel(kengash);
        var nomzod = _context.ExistingCandidate(vakansiya, kengash);
        nomzod.MoveToReserve(RecruitingTestContext.Today, "Ҳозир ўрин йўқ");
        var kim = Guid.CreateVersion7();
        _context.Employees.CurrentUser.UserId.Returns(kim);

        var natija = await Qaytarish.HandleAsync(
            new ReturnCandidateFromReserveCommand(
                nomzod.Id, "Ўрин бўшади, қайта чақирдим", RecruitingTestContext.Today.AddDays(1)),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Code : null);
        natija.Value.Notes.Count.ShouldBe(1);
        natija.Value.Notes[0].Kind.ShouldBe(CandidateNoteKind.ReturnFromReserve);
        natija.Value.Notes[0].Text.ShouldBe("Ўрин бўшади, қайта чақирдим");
        natija.Value.Notes[0].CreatedBy.ShouldBe(kim);
        natija.Value.ClosingNote.ShouldBeNull(
            "izoh `ClosingNote` ga yozilsa, o'sha tranzaksiyada o'chib ketardi");
        natija.Value.NextContactDate.ShouldBe(RecruitingTestContext.Today.AddDays(1));
    }

    [Fact]
    public async Task Rezervdan_qaytarish_TANASIZ_ham_avvalgidek_ishlaydi()
    {
        // Endpoint tanasi IXTIYORIY bo'lib qoldi: tana yubormagan chaqiruvchi
        // (va eski interfeys) aynan avvalgidek ishlashi kerak.
        var vakansiya = _context.ExistingVacancy();
        var kengash = _context.ExistingStage("Кенгаш", 1);
        _context.Funnel(kengash);
        var nomzod = _context.ExistingCandidate(vakansiya, kengash);
        nomzod.MoveToReserve(RecruitingTestContext.Today, "Ҳозир ўрин йўқ", RecruitingTestContext.Today.AddDays(8));

        var natija = await Qaytarish.HandleAsync(
            new ReturnCandidateFromReserveCommand(nomzod.Id), TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Code : null);
        natija.Value.Status.ShouldBe(CandidateStatus.InProgress);
        natija.Value.ClosedAt.ShouldBeNull();
        natija.Value.ClosingNote.ShouldBeNull();
        natija.Value.Notes.ShouldBeEmpty("izoh yo'q — yozuv ham yo'q");
        natija.Value.NextContactDate.ShouldBe(
            RecruitingTestContext.Today.AddDays(8),
            "rezervda qo'yilgan qo'ng'iroq rejasi voronkada ham kuchda qoladi");
    }
}
