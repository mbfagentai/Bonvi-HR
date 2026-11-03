using Bonvi.Domain.Blanks;

namespace Bonvi.Domain.Tests.Blanks;

/// <summary>To'ldirilgan blankaning holat mashinasi va versiyasi (docs/SPEC-blankalar.md §5).</summary>
public sealed class BlankSubmissionTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 3, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid Employee = Guid.NewGuid();
    private static readonly Guid Hr = Guid.NewGuid();
    private const string Sha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static (BlankTemplate Template, BlankTemplateVersion Version) Shablon(
        BlankSubjectKind kind = BlankSubjectKind.Employee)
    {
        var template = BlankTemplate.Create("HR-F07", "Sinov muddati xulosasi", kind).Value;
        var version = BlankTemplateVersion.Create(template.Id, "1.0", "{\"schema\":1}", Sha, BlankTemplateSource.Seed).Value;
        version.Publish(null, At).IsSuccess.ShouldBeTrue();
        return (template, version);
    }

    private static BlankSubmission Yangi()
    {
        var (template, version) = Shablon();
        return BlankSubmission.Create(template, version, new BlankSubject(Employee), BlankAnswersState.Empty).Value;
    }

    /// <summary>F07 shaklidagi reja: xodim (1) → boshliq va HR-1 parallel (2) → direktor (3, shart bajarilmagan).</summary>
    private static BlankStepPlan[] Reja() =>
    [
        new("employee", 1, "Xodim", BlankActorRoles.SubjectEmployee, BlankStepMode.InPerson),
        new("head", 2, "Bo'lim boshlig'i", BlankActorRoles.DepartmentHead, BlankStepMode.Either),
        new("hr1", 2, "HR-1", BlankActorRoles.Hr1, BlankStepMode.Electronic),
        new("director", 3, "Direktor", BlankActorRoles.Director, BlankStepMode.Electronic, Skipped: true),
    ];

    private static BlankSubmission Yuborilgan(params BlankStepPlan[] plan)
    {
        var blanka = Yangi();
        blanka.Submit(1, plan.Length == 0 ? Reja() : plan, BlankAnswersState.Empty, At).IsSuccess.ShouldBeTrue();
        return blanka;
    }

    private static BlankStepAct Elektron(string? decision = "agree") => new(BlankSignMethod.Electronic, Hr, Decision: decision);

    private static BlankStepAct Qolda() => new(BlankSignMethod.InPerson, Hr, Employee, "Aliyev Vali", "blanks/sig.png");

    private static BlankSubmissionStep Qadam(BlankSubmission blanka, string code) => blanka.Steps.Single(step => step.StepCode == code);

    // ---------- Yaratish ----------

    [Fact]
    public void Yangi_blanka_qoralama_raqamsiz_versiyasi_nol()
    {
        var blanka = Yangi();

        blanka.Status.ShouldBe(BlankSubmissionStatus.Draft);
        blanka.Number.ShouldBeNull("raqam faqat birinchi yuborishda — qoralama raqam yoqmaydi");
        blanka.Version.ShouldBe(0);
        blanka.TemplateCode.ShouldBe("HR-F07");
        blanka.SubjectKind.ShouldBe(BlankSubjectKind.Employee);
        blanka.Answers.ShouldBe("{}");
        blanka.Missing.ShouldBe("[]");
    }

    [Fact]
    public void Faol_bolmagan_shablonda_blanka_ochilmaydi()
    {
        var (template, version) = Shablon();
        template.Deactivate();

        BlankSubmission.Create(template, version, new BlankSubject(Employee), BlankAnswersState.Empty)
            .Error.ShouldBe(BlankErrors.TemplateInactive);
    }

    [Fact]
    public void Elon_qilinmagan_yoki_begona_versiya_bilan_ochilmaydi()
    {
        var (template, _) = Shablon();
        var draft = BlankTemplateVersion.Create(template.Id, "2.0", "{}", Sha, BlankTemplateSource.Seed).Value;
        var (_, begona) = Shablon();

        BlankSubmission.Create(template, draft, new BlankSubject(Employee), BlankAnswersState.Empty)
            .Error.ShouldBe(BlankErrors.TemplateNotFound);
        BlankSubmission.Create(template, begona, new BlankSubject(Employee), BlankAnswersState.Empty)
            .Error.ShouldBe(BlankErrors.TemplateNotFound);
    }

    [Theory]
    [InlineData(BlankSubjectKind.Employee, false, false)]
    [InlineData(BlankSubjectKind.Employee, true, true)]
    [InlineData(BlankSubjectKind.Candidate, true, false)]
    [InlineData(BlankSubjectKind.None, true, false)]
    [InlineData(BlankSubjectKind.None, false, true)]
    public void Subyekt_shablon_turiga_mos_bolmasa_rad(BlankSubjectKind kind, bool withEmployee, bool withCandidate)
    {
        var (template, version) = Shablon(kind);
        var subject = new BlankSubject(
            withEmployee ? Employee : null,
            CandidateId: withCandidate ? Guid.NewGuid() : null);

        BlankSubmission.Create(template, version, subject, BlankAnswersState.Empty)
            .Error.ShouldBe(BlankErrors.SubjectMismatch);
    }

    [Fact]
    public void Juda_katta_javoblar_rad()
    {
        var (template, version) = Shablon();
        var katta = BlankAnswersState.Empty with
        {
            Answers = "{\"x\":\"" + new string('a', BlankSubmission.MaxAnswersBytes) + "\"}",
        };

        BlankSubmission.Create(template, version, new BlankSubject(Employee), katta)
            .Error.ShouldBe(BlankErrors.TooLarge);
    }

    // ---------- Saqlash ----------

    [Fact]
    public void Saqlash_javoblarni_yozadi_va_versiyani_oshiradi()
    {
        var blanka = Yangi();
        var holat = new BlankAnswersState("{\"a\":1}", "{\"avg\":4.5}", "[\"b\"]", 1, new DateOnly(2026, 11, 10));

        blanka.SaveAnswers(holat).IsSuccess.ShouldBeTrue();
        blanka.SaveAnswers(holat).IsSuccess.ShouldBeTrue();

        blanka.Version.ShouldBe(2);
        blanka.Answers.ShouldBe("{\"a\":1}");
        blanka.MissingCount.ShouldBe(1);
        blanka.DueDate.ShouldBe(new DateOnly(2026, 11, 10));
    }

    [Fact]
    public void Versiya_tekshiruvi_eskisini_ozgargan_deydi()
    {
        var blanka = Yangi();
        blanka.SaveAnswers(BlankAnswersState.Empty);

        blanka.CheckVersion(1).IsSuccess.ShouldBeTrue();
        blanka.CheckVersion(0).Error.ShouldBe(BlankErrors.Stale);
    }

    // ---------- Yuborish ----------

    [Fact]
    public void Yuborish_raqam_beradi_qadamlarni_yaratadi_va_birinchi_guruhni_ochadi()
    {
        var blanka = Yangi();

        blanka.Submit(7, Reja(), BlankAnswersState.Empty, At).IsSuccess.ShouldBeTrue();

        blanka.Status.ShouldBe(BlankSubmissionStatus.InApproval);
        blanka.Number.ShouldBe(7);
        blanka.SubmittedAt.ShouldBe(At);
        blanka.Version.ShouldBe(1);
        Qadam(blanka, "employee").State.ShouldBe(BlankStepState.Open);
        Qadam(blanka, "head").State.ShouldBe(BlankStepState.Waiting);
        Qadam(blanka, "hr1").State.ShouldBe(BlankStepState.Waiting);
        Qadam(blanka, "director").State.ShouldBe(BlankStepState.Skipped);
    }

    [Fact]
    public void Birinchi_yuborishda_raqamsiz_rad()
        => Yangi().Submit(null, Reja(), BlankAnswersState.Empty, At).Error.ShouldBe(BlankErrors.Invalid);

    [Fact]
    public void Qadamsiz_yoki_hammasi_otkazilgan_blanka_darrov_tasdiqlanadi()
    {
        var bosh = Yangi();
        bosh.Submit(1, [], BlankAnswersState.Empty, At).IsSuccess.ShouldBeTrue();
        bosh.Status.ShouldBe(BlankSubmissionStatus.Approved);
        bosh.CompletedAt.ShouldBe(At);

        var otkazilgan = Yuborilgan(new BlankStepPlan("d", 1, "Direktor", BlankActorRoles.Director, BlankStepMode.Electronic, Skipped: true));
        otkazilgan.Status.ShouldBe(BlankSubmissionStatus.Approved);
    }

    [Fact]
    public void Takror_qadam_kodi_yoki_notanish_rol_rad()
    {
        Yangi().Submit(
                1,
                [
                    new("a", 1, "A", BlankActorRoles.Hr1, BlankStepMode.Electronic),
                    new("a", 2, "B", BlankActorRoles.Hr2, BlankStepMode.Electronic),
                ],
                BlankAnswersState.Empty,
                At)
            .Error.ShouldBe(BlankErrors.Invalid);

        Yangi().Submit(1, [new("a", 1, "A", "boss", BlankStepMode.Electronic)], BlankAnswersState.Empty, At)
            .Error.ShouldBe(BlankErrors.Invalid);
    }

    [Fact]
    public void Kelishuvdagi_blankani_qayta_yuborib_bolmaydi()
        => Yuborilgan().Submit(2, Reja(), BlankAnswersState.Empty, At).Error.ShouldBe(BlankErrors.InvalidStatus);

    // ---------- Imzolash ----------

    [Fact]
    public void Toliq_zanjir_parallel_guruh_bilan_tasdiqgacha()
    {
        var blanka = Yuborilgan();

        blanka.SignStep(Qadam(blanka, "employee").Id, Qolda(), null, At).IsSuccess.ShouldBeTrue();
        Qadam(blanka, "head").State.ShouldBe(BlankStepState.Open, "parallel guruh birga ochiladi");
        Qadam(blanka, "hr1").State.ShouldBe(BlankStepState.Open);

        blanka.SignStep(Qadam(blanka, "hr1").Id, Elektron(), null, At).IsSuccess.ShouldBeTrue();
        blanka.Status.ShouldBe(BlankSubmissionStatus.InApproval, "guruhda bitta qadam hali ochiq");

        blanka.SignStep(Qadam(blanka, "head").Id, Elektron(), null, At.AddHours(1)).IsSuccess.ShouldBeTrue();

        blanka.Status.ShouldBe(BlankSubmissionStatus.Approved, "direktor qadami o'tkazib yuborilgan");
        blanka.CompletedAt.ShouldBe(At.AddHours(1));
        blanka.Version.ShouldBe(4, "yuborish + uchta imzo");

        var xodim = Qadam(blanka, "employee");
        xodim.Method.ShouldBe(BlankSignMethod.InPerson);
        xodim.SignatureStorageKey.ShouldBe("blanks/sig.png");
        xodim.SignerName.ShouldBe("Aliyev Vali");
        xodim.ActedByUserId.ShouldBe(Hr);
        Qadam(blanka, "hr1").Decision.ShouldBe("agree");
    }

    [Fact]
    public void Imzo_bilan_kelgan_javoblar_yoziladi()
    {
        var blanka = Yuborilgan();
        var holat = BlankAnswersState.Empty with { Answers = "{\"conclusion\":\"passed\"}" };

        blanka.SignStep(Qadam(blanka, "employee").Id, Qolda() with { AnswersPatch = "{\"conclusion\":\"passed\"}" }, holat, At)
            .IsSuccess.ShouldBeTrue();

        blanka.Answers.ShouldBe("{\"conclusion\":\"passed\"}");
        Qadam(blanka, "employee").AnswersPatch.ShouldBe("{\"conclusion\":\"passed\"}");
    }

    [Fact]
    public void Kutayotgan_yoki_imzolangan_qadamni_imzolab_bolmaydi()
    {
        var blanka = Yuborilgan();

        blanka.SignStep(Qadam(blanka, "hr1").Id, Elektron(), null, At).Error.ShouldBe(BlankErrors.StepNotOpen);

        blanka.SignStep(Qadam(blanka, "employee").Id, Qolda(), null, At);
        blanka.SignStep(Qadam(blanka, "employee").Id, Qolda(), null, At).Error.ShouldBe(BlankErrors.StepNotOpen);
        blanka.SignStep(Qadam(blanka, "director").Id, Elektron(), null, At).Error.ShouldBe(BlankErrors.StepNotOpen);
    }

    [Fact]
    public void Notanish_qadam_topilmadi()
        => Yuborilgan().SignStep(Guid.NewGuid(), Elektron(), null, At).Error.ShouldBe(BlankErrors.StepNotFound);

    [Fact]
    public void Qoralamada_imzolab_bolmaydi()
        => Yangi().SignStep(Guid.NewGuid(), Elektron(), null, At).Error.ShouldBe(BlankErrors.InvalidStatus);

    [Fact]
    public void Usul_qadam_rejimiga_mos_bolishi_shart()
    {
        var blanka = Yuborilgan();

        blanka.SignStep(Qadam(blanka, "employee").Id, Elektron(), null, At)
            .Error.ShouldBe(BlankErrors.StepForbidden, "faqat qo'lda imzolanadigan qadam");

        blanka.SignStep(Qadam(blanka, "employee").Id, Qolda() with { SignatureStorageKey = null }, null, At)
            .Error.ShouldBe(BlankErrors.SignatureRequired);

        blanka.SignStep(Qadam(blanka, "employee").Id, Qolda(), null, At);
        blanka.SignStep(Qadam(blanka, "hr1").Id, Qolda(), null, At)
            .Error.ShouldBe(BlankErrors.StepForbidden, "faqat elektron qadam");
        blanka.SignStep(Qadam(blanka, "head").Id, Qolda(), null, At).IsSuccess.ShouldBeTrue("«either» — ikkalasi ham");
    }

    [Fact]
    public void Xato_amal_versiyani_oshirmaydi()
    {
        var blanka = Yuborilgan();

        blanka.SignStep(Qadam(blanka, "hr1").Id, Elektron(), null, At);
        blanka.Cancel(null, At);

        blanka.Version.ShouldBe(1);
    }

    // ---------- Qaytarish ----------

    [Fact]
    public void Qaytarish_qoralamaga_qadamlarni_ochiradi_raqamni_saqlaydi()
    {
        var blanka = Yuborilgan();
        blanka.SignStep(Qadam(blanka, "employee").Id, Qolda(), null, At);

        blanka.ReturnToDraft(Qadam(blanka, "hr1").Id, "  Ball noto'g'ri  ").IsSuccess.ShouldBeTrue();

        blanka.Status.ShouldBe(BlankSubmissionStatus.Draft);
        blanka.Steps.ShouldBeEmpty();
        blanka.Number.ShouldBe(1);
        blanka.ReturnComment.ShouldBe("Ball noto'g'ri");
        blanka.Version.ShouldBe(3);

        blanka.Submit(99, Reja(), BlankAnswersState.Empty, At).IsSuccess.ShouldBeTrue();
        blanka.Number.ShouldBe(1, "raqam birinchi yuborishdagisi qoladi");
        blanka.ReturnComment.ShouldBeNull("keyingi yuborish izohni tozalaydi");
        Qadam(blanka, "employee").State.ShouldBe(BlankStepState.Open, "qadamlar qaytadan yaratiladi");
    }

    [Fact]
    public void Qaytarish_izohsiz_va_yopiq_qadamdan_rad()
    {
        var blanka = Yuborilgan();

        blanka.ReturnToDraft(Qadam(blanka, "employee").Id, " ").Error.ShouldBe(BlankErrors.ReasonRequired);
        blanka.ReturnToDraft(Qadam(blanka, "hr1").Id, "Sabab").Error.ShouldBe(BlankErrors.StepNotOpen);
        Yangi().ReturnToDraft(Guid.NewGuid(), "Sabab").Error.ShouldBe(BlankErrors.InvalidStatus);
    }

    // ---------- Rad etish ----------

    [Fact]
    public void Rad_etish_yakuniy()
    {
        var blanka = Yuborilgan();
        var qadam = Qadam(blanka, "employee");

        blanka.Reject(qadam.Id, Hr, Employee, "Rozi emas", At).IsSuccess.ShouldBeTrue();

        blanka.Status.ShouldBe(BlankSubmissionStatus.Rejected);
        blanka.CompletedAt.ShouldBe(At);
        blanka.IsFinal.ShouldBeTrue();
        qadam.State.ShouldBe(BlankStepState.Rejected);
        qadam.Comment.ShouldBe("Rozi emas");
        blanka.Version.ShouldBe(2);

        blanka.SaveAnswers(BlankAnswersState.Empty).Error.ShouldBe(BlankErrors.InvalidStatus);
        blanka.Cancel("Sabab", At).Error.ShouldBe(BlankErrors.InvalidStatus);
    }

    [Fact]
    public void Rad_etish_sababsiz_rad()
    {
        var blanka = Yuborilgan();

        blanka.Reject(Qadam(blanka, "employee").Id, Hr, null, null, At).Error.ShouldBe(BlankErrors.ReasonRequired);
    }

    // ---------- Bekor qilish ----------

    [Fact]
    public void Qoralama_va_kelishuvdagi_blanka_bekor_qilinadi()
    {
        var qoralama = Yangi();
        qoralama.Cancel(" Xato ochilgan ", At).IsSuccess.ShouldBeTrue();
        qoralama.Status.ShouldBe(BlankSubmissionStatus.Cancelled);
        qoralama.CancelReason.ShouldBe("Xato ochilgan");
        qoralama.CancelledAt.ShouldBe(At);
        qoralama.Version.ShouldBe(1);

        var kelishuvda = Yuborilgan();
        kelishuvda.Cancel("Ishdan ketdi", At).IsSuccess.ShouldBeTrue();
        kelishuvda.Status.ShouldBe(BlankSubmissionStatus.Cancelled);
    }

    [Fact]
    public void Bekor_qilish_sababsiz_va_yakuniy_holatdan_rad()
    {
        Yangi().Cancel("", At).Error.ShouldBe(BlankErrors.ReasonRequired);

        var tasdiqlangan = Yangi();
        tasdiqlangan.Submit(1, [], BlankAnswersState.Empty, At);
        tasdiqlangan.Cancel("Sabab", At).Error.ShouldBe(BlankErrors.InvalidStatus);

        var bekor = Yangi();
        bekor.Cancel("Sabab", At);
        bekor.Cancel("Sabab", At).Error.ShouldBe(BlankErrors.InvalidStatus);
        bekor.Submit(1, [], BlankAnswersState.Empty, At).Error.ShouldBe(BlankErrors.InvalidStatus);
    }

    // ---------- Fayllar va PDF ----------

    private static BlankAttachmentFile Fayl(string path = "evidence")
        => new(path, BlankAttachmentKind.Pdf, "dalil.pdf", "application/pdf", 1024, Sha, "blanks/x.pdf");

    [Fact]
    public void Fayl_biriktirish_va_olib_tashlash_versiyani_oshiradi()
    {
        var blanka = Yangi();

        var fayl = blanka.AddAttachment(Fayl(), Hr, At).Value;
        blanka.Attachments.ShouldHaveSingleItem();
        blanka.Version.ShouldBe(1);

        blanka.RemoveAttachment(fayl.Id, Hr, At).IsSuccess.ShouldBeTrue();
        fayl.IsActive.ShouldBeFalse();
        fayl.RemovedBy.ShouldBe(Hr);
        blanka.Version.ShouldBe(2);

        blanka.RemoveAttachment(fayl.Id, Hr, At).Error.ShouldBe(BlankErrors.FileNotFound, "ikkinchi marta olib tashlanmaydi");
    }

    [Fact]
    public void Yakuniy_blankaga_fayl_biriktirilmaydi_va_bosh_fayl_rad()
    {
        Yangi().AddAttachment(Fayl() with { SizeBytes = 0 }, Hr, At).Error.ShouldBe(BlankErrors.FileEmpty);

        var bekor = Yangi();
        bekor.Cancel("Sabab", At);
        bekor.AddAttachment(Fayl(), Hr, At).Error.ShouldBe(BlankErrors.InvalidStatus);
    }

    [Fact]
    public void Yakuniy_pdf_faqat_tasdiqlanganda_va_versiyani_oshirmaydi()
    {
        var blanka = Yangi();
        blanka.SetFinalPdf("blanks/final.pdf").Error.ShouldBe(BlankErrors.InvalidStatus);

        blanka.Submit(1, [], BlankAnswersState.Empty, At);
        var version = blanka.Version;

        blanka.SetFinalPdf("blanks/final.pdf").IsSuccess.ShouldBeTrue();
        blanka.FinalPdfStorageKey.ShouldBe("blanks/final.pdf");
        blanka.Version.ShouldBe(version);
    }
}
