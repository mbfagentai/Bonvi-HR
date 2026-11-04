using System.Text;
using Bonvi.Domain.Common;

namespace Bonvi.Domain.Blanks;

/// <summary>
/// Javoblarning hisoblangan holati — har o'zgarishda Application qayta hisoblab beradi.
/// </summary>
/// <param name="Answers">Javoblar (JSON obyekt).</param>
/// <param name="Computed">Server hisoblagan maydonlar <c>{yo'l: qiymat}</c> (JSON obyekt).</param>
/// <param name="Missing">Muddati kelgan bo'limlardagi bo'sh majburiy maydon yo'llari (JSON massiv).</param>
/// <param name="MissingCount"><paramref name="Missing"/> uzunligi.</param>
/// <param name="DueDate">Ta'rifdagi <c>dueDate</c> yoki eng yaqin ochiq qadam muddati.</param>
public sealed record BlankAnswersState(
    string Answers,
    string Computed,
    string Missing,
    int MissingCount,
    DateOnly? DueDate)
{
    /// <summary>Bo'sh blanka.</summary>
    public static BlankAnswersState Empty { get; } = new("{}", "{}", "[]", 0, null);
}

/// <summary>
/// To'ldirilgan blanka — agregat ildizi (qadamlar va fayllar bilan), SPEC-blankalar §3.3, §5.
/// </summary>
/// <remarks>
/// <para>
/// <b>Holat mashinasi</b> (§5): <c>Draft → InApproval → Approved</c>; ochiq qadamdan «qaytarish»
/// → <c>Draft</c> (qadamlar o'chadi, raqam qoladi), «rad etish» → <c>Rejected</c>; <c>Draft</c> va
/// <c>InApproval</c> dan «bekor qilish» → <c>Cancelled</c>. Oxirgi uchtasi yakuniy — undan
/// keyin hech narsa o'zgarmaydi. KIM qila olishi (ruxsat, rol, qadam bayroqlari
/// <c>canReturn</c>/<c>canReject</c>) va QAYSI maydon tahrirlanishi ta'rifga bog'liq va
/// Application'da tekshiriladi; bu yerda — faqat holat o'tishlari va guruh qoidasi.
/// </para>
/// <para>
/// <b><see cref="Version"/></b> — parallellik belgisi (<c>AbsenceRequest</c> naqshi): har bir
/// o'zgartiruvchi metod uni bittaga oshiradi, bazadagi <c>UPDATE</c> esa
/// <c>WHERE version = @yuklangan</c> bilan shartli. Mijoz ko'rgan qiymat
/// <see cref="CheckVersion"/> bilan solishtiriladi (409 <c>blanka.ozgargan</c>).
/// </para>
/// <para>
/// <b>Javoblar jurnalga tushmaydi</b> (intizom, ketish sabablari): huquqiy iz — har qadamning
/// <see cref="BlankSubmissionStep.AnswersPatch"/> i.
/// </para>
/// </remarks>
public sealed class BlankSubmission : AggregateRoot, IAuditable
{
    /// <summary>Javoblarning eng katta hajmi (UTF-8 baytlarda) — 512 KB.</summary>
    public const int MaxAnswersBytes = 512 * 1024;

    /// <summary>Bekor qilish sababi uzunligi.</summary>
    public const int MaxCancelReasonLength = 1000;

    /// <summary>Qaytarish izohi uzunligi.</summary>
    public const int MaxReturnCommentLength = 2000;

    private readonly List<BlankSubmissionStep> _steps = [];
    private readonly List<BlankAttachment> _attachments = [];

    private BlankSubmission(
        Guid id,
        BlankTemplate template,
        BlankTemplateVersion version,
        BlankSubject subject,
        BlankAnswersState state,
        Guid? clientRequestId)
        : base(id)
    {
        TemplateId = template.Id;
        TemplateVersionId = version.Id;
        TemplateCode = template.Code;
        SubjectKind = template.SubjectKind;
        EmployeeId = subject.EmployeeId;
        EmploymentId = subject.EmploymentId;
        CandidateId = subject.CandidateId;
        DepartmentId = subject.DepartmentId;
        ClientRequestId = clientRequestId;
        Status = BlankSubmissionStatus.Draft;
        Apply(state);
    }

    private BlankSubmission() { }

    public Guid TemplateId { get; private set; }

    /// <summary>Tekshirish, ekran va PDF umrbod shu versiya ta'rifidan.</summary>
    public Guid TemplateVersionId { get; private set; }

    /// <summary>Shablon kodi — filtr va AI uchun nusxa.</summary>
    public string TemplateCode { get; private set; } = string.Empty;

    /// <summary>Shablon bo'yicha raqam — birinchi yuborishda beriladi, qoralamada <c>null</c>.</summary>
    public int? Number { get; private set; }

    public BlankSubmissionStatus Status { get; private set; }

    /// <summary>Shablondan nusxa.</summary>
    public BlankSubjectKind SubjectKind { get; private set; }

    public Guid? EmployeeId { get; private set; }

    /// <summary>Ochilgan paytdagi faol (bo'lmasa oxirgi) ish joyi.</summary>
    public Guid? EmploymentId { get; private set; }

    public Guid? CandidateId { get; private set; }

    /// <summary>Ochilgan paytdagi bo'lim (nusxa).</summary>
    public Guid? DepartmentId { get; private set; }

    /// <summary>Javoblar (JSON obyekt).</summary>
    public string Answers { get; private set; } = "{}";

    /// <summary>Server hisoblagan maydonlar (JSON obyekt).</summary>
    public string Computed { get; private set; } = "{}";

    /// <summary>Bo'sh majburiy maydon yo'llari (JSON massiv) — AI «to'liq emas» deb shuni o'qiydi.</summary>
    public string Missing { get; private set; } = "[]";

    public int MissingCount { get; private set; }

    public DateOnly? DueDate { get; private set; }

    /// <summary>
    /// Parallellik belgisi — har o'zgarishda +1, faqat shu agregat metodlari orqali.
    /// </summary>
    public int Version { get; private set; }

    /// <summary>Yaratish so'rovining identifikatori — takroriy yaratishdan himoya.</summary>
    public Guid? ClientRequestId { get; private set; }

    /// <summary>Oxirgi yuborilgan lahza.</summary>
    public DateTimeOffset? SubmittedAt { get; private set; }

    /// <summary>Tasdiqlangan yoki rad etilgan lahza.</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public string? CancelReason { get; private set; }

    /// <summary>Oxirgi «qaytarish» izohi — keyingi yuborish tozalaydi.</summary>
    public string? ReturnComment { get; private set; }

    /// <summary>Tasdiqlanganda saqlangan PDF nusxasi kaliti.</summary>
    public string? FinalPdfStorageKey { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    /// <summary>Imzo qadamlari (tartib va kod bo'yicha).</summary>
    public IReadOnlyList<BlankSubmissionStep> Steps => _steps.AsReadOnly();

    /// <summary>Biriktirilgan fayllar (olib tashlanganlari ham).</summary>
    public IReadOnlyList<BlankAttachment> Attachments => _attachments.AsReadOnly();

    /// <summary>Yakuniy holatda — hech narsa o'zgarmaydi.</summary>
    public bool IsFinal => Status is BlankSubmissionStatus.Approved
        or BlankSubmissionStatus.Rejected
        or BlankSubmissionStatus.Cancelled;

    /// <summary>Yangi blanka — qoralama.</summary>
    /// <param name="template">Faol shablon.</param>
    /// <param name="version">Shablonning e'lon qilingan versiyasi.</param>
    /// <param name="subject">Kim haqida (shablon turiga mos).</param>
    /// <param name="state">Oldindan to'ldirilgan javoblar.</param>
    /// <param name="clientRequestId">Takroriy yaratishdan himoya.</param>
    public static Result<BlankSubmission> Create(
        BlankTemplate template,
        BlankTemplateVersion version,
        BlankSubject subject,
        BlankAnswersState state,
        Guid? clientRequestId = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(state);

        if (!template.IsActive)
        {
            return BlankErrors.TemplateInactive;
        }

        if (version.TemplateId != template.Id || version.Status != BlankTemplateVersionStatus.Published)
        {
            return BlankErrors.TemplateNotFound;
        }

        if (!subject.Matches(template.SubjectKind))
        {
            return BlankErrors.SubjectMismatch;
        }

        if (Validate(state) is { } invalid)
        {
            return invalid;
        }

        return new BlankSubmission(NewId(), template, version, subject, state, clientRequestId);
    }

    /// <summary>Mijoz ko'rgan versiya joriysiga tengmi.</summary>
    /// <param name="expected">Mijoz yuborgan <c>expectedVersion</c>.</param>
    public Result CheckVersion(int expected)
        => expected == Version ? Result.Success() : BlankErrors.Stale;

    /// <summary>
    /// Qadam amalidan keyin to'liqlik (<c>missing</c>) va muddatni yangilaydi — qadamlar grafi siljigach
    /// Application qayta hisoblaydi (§5.3). Javoblar o'zgarmaydi deb kutiladi; versiya oshmaydi (amal o'zi oshirgan).
    /// </summary>
    public Result RefreshCompleteness(BlankAnswersState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (Validate(state) is { } invalid)
        {
            return invalid;
        }

        Apply(state);
        return Result.Success();
    }

    /// <summary>Javoblarni saqlaydi — qoralamada yoki kelishuvda (qaysi bo'lim — Application biladi).</summary>
    /// <param name="state">Yangi holat.</param>
    public Result SaveAnswers(BlankAnswersState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (Status is not (BlankSubmissionStatus.Draft or BlankSubmissionStatus.InApproval))
        {
            return BlankErrors.InvalidStatus;
        }

        if (Validate(state) is { } invalid)
        {
            return invalid;
        }

        Apply(state);
        Version++;
        return Result.Success();
    }

    /// <summary>
    /// Qoralamani yuboradi: raqam (faqat birinchi yuborishda), qadamlar rejadan, eng kichik
    /// tartibli guruh ochiladi. Qadam qolmasa — darrov tasdiqlanadi.
    /// </summary>
    /// <param name="number">Hisoblagichdan olingan raqam — blankada raqam hali yo'q bo'lsa majburiy, bor bo'lsa e'tiborsiz.</param>
    /// <param name="plan">Ta'rifdagi qadamlar (shart bajarilmaganlari <see cref="BlankStepPlan.Skipped"/>).</param>
    /// <param name="state">Yuborish paytidagi javoblar holati.</param>
    /// <param name="at">Lahza.</param>
    public Result Submit(int? number, IReadOnlyList<BlankStepPlan> plan, BlankAnswersState state, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(state);

        if (Status != BlankSubmissionStatus.Draft)
        {
            return BlankErrors.InvalidStatus;
        }

        if (Number is null && number is not > 0)
        {
            return BlankErrors.Invalid;
        }

        if (Validate(state) is { } invalid)
        {
            return invalid;
        }

        if (plan.Select(step => step.StepCode).Distinct(StringComparer.Ordinal).Count() != plan.Count)
        {
            return BlankErrors.Invalid;
        }

        var steps = new List<BlankSubmissionStep>(plan.Count);
        foreach (var item in plan)
        {
            var step = BlankSubmissionStep.Create(Id, item);
            if (step.IsFailure)
            {
                return step.Error;
            }

            steps.Add(step.Value);
        }

        Number ??= number;
        _steps.Clear();
        _steps.AddRange(steps.OrderBy(step => step.OrderNo).ThenBy(step => step.StepCode, StringComparer.Ordinal));
        Apply(state);
        ReturnComment = null;
        SubmittedAt = at;
        Status = BlankSubmissionStatus.InApproval;
        Advance(at);
        Version++;
        return Result.Success();
    }

    /// <summary>
    /// Ochiq qadamni imzolaydi; guruh yopilsa keyingisi ochiladi, qolmasa — tasdiqlanadi.
    /// </summary>
    /// <param name="stepId">Qadam.</param>
    /// <param name="act">Imzo ma'lumoti.</param>
    /// <param name="state">Bajaruvchi bo'limi qo'shilgan javoblar holati (o'zgarmasa <c>null</c>).</param>
    /// <param name="at">Lahza.</param>
    /// <param name="isStepDue">
    /// Qadam sharti (<c>when</c>) — guruh navbati kelganda qayta baholanadi (SPEC §4.11 p.3): <c>false</c> bo'lgan
    /// qadam <see cref="BlankStepState.Skipped"/> bo'ladi. <c>null</c> — hamma qadam kerak.
    /// </param>
    public Result SignStep(
        Guid stepId,
        BlankStepAct act,
        BlankAnswersState? state,
        DateTimeOffset at,
        Func<string, bool>? isStepDue = null)
    {
        ArgumentNullException.ThrowIfNull(act);

        var found = FindOpenStep(stepId);
        if (found.IsFailure)
        {
            return found.Error;
        }

        var step = found.Value;
        if (!step.Allows(act.Method))
        {
            return BlankErrors.StepForbidden;
        }

        if (act.Method == BlankSignMethod.InPerson && string.IsNullOrWhiteSpace(act.SignatureStorageKey))
        {
            return BlankErrors.SignatureRequired;
        }

        if (act.Decision is { Length: > BlankSubmissionStep.MaxDecisionLength }
            || act.Comment is { Length: > BlankSubmissionStep.MaxCommentLength }
            || act.SignerName is { Length: > BlankSubmissionStep.MaxSignerNameLength }
            || act.SignatureStorageKey is { Length: > BlankSubmissionStep.MaxStorageKeyLength })
        {
            return BlankErrors.Invalid;
        }

        if (state is not null && Validate(state) is { } invalid)
        {
            return invalid;
        }

        step.Sign(act with { Comment = Clean(act.Comment), SignerName = Clean(act.SignerName) }, at);
        if (state is not null)
        {
            Apply(state);
        }

        Advance(at, isStepDue);
        Version++;
        return Result.Success();
    }

    /// <summary>
    /// Ochiq qadamdan qoralamaga qaytaradi: qadamlar o'chadi (amallari jurnalda qoladi), raqam
    /// qoladi, izoh to'ldirish sahifasida keyingi yuborishgacha ko'rinadi.
    /// </summary>
    /// <param name="stepId">Qaytarayotgan qadam.</param>
    /// <param name="comment">Izoh — majburiy.</param>
    public Result ReturnToDraft(Guid stepId, string? comment)
    {
        var found = FindOpenStep(stepId);
        if (found.IsFailure)
        {
            return found.Error;
        }

        var clean = Clean(comment);
        if (clean is null)
        {
            return BlankErrors.ReasonRequired;
        }

        if (clean.Length > MaxReturnCommentLength)
        {
            return BlankErrors.Invalid;
        }

        _steps.Clear();
        ReturnComment = clean;
        Status = BlankSubmissionStatus.Draft;
        Version++;
        return Result.Success();
    }

    /// <summary>Ochiq qadamda rad etadi — blanka yakuniy <see cref="BlankSubmissionStatus.Rejected"/>.</summary>
    /// <param name="stepId">Rad etayotgan qadam.</param>
    /// <param name="actedByUserId">Foydalanuvchi.</param>
    /// <param name="signerEmployeeId">Aniqlangan shaxs (bo'lsa).</param>
    /// <param name="comment">Sabab — majburiy.</param>
    /// <param name="at">Lahza.</param>
    public Result Reject(Guid stepId, Guid? actedByUserId, Guid? signerEmployeeId, string? comment, DateTimeOffset at)
    {
        var found = FindOpenStep(stepId);
        if (found.IsFailure)
        {
            return found.Error;
        }

        var clean = Clean(comment);
        if (clean is null)
        {
            return BlankErrors.ReasonRequired;
        }

        if (clean.Length > BlankSubmissionStep.MaxCommentLength)
        {
            return BlankErrors.Invalid;
        }

        found.Value.Reject(actedByUserId, signerEmployeeId, clean, at);
        Status = BlankSubmissionStatus.Rejected;
        CompletedAt = at;
        Version++;
        return Result.Success();
    }

    /// <summary>Bekor qiladi — qoralama yoki kelishuvdan (kimligini Application tekshiradi).</summary>
    /// <param name="reason">Sabab — majburiy.</param>
    /// <param name="at">Lahza.</param>
    public Result Cancel(string? reason, DateTimeOffset at)
    {
        if (Status is not (BlankSubmissionStatus.Draft or BlankSubmissionStatus.InApproval))
        {
            return BlankErrors.InvalidStatus;
        }

        var clean = Clean(reason);
        if (clean is null)
        {
            return BlankErrors.ReasonRequired;
        }

        if (clean.Length > MaxCancelReasonLength)
        {
            return BlankErrors.Invalid;
        }

        Status = BlankSubmissionStatus.Cancelled;
        CancelReason = clean;
        CancelledAt = at;
        Version++;
        return Result.Success();
    }

    /// <summary>Maydonga fayl biriktiradi — yakuniy holatda emas (maydon tahrirlanishini Application tekshiradi).</summary>
    /// <param name="file">Tekshirilgan va omborga yozilgan fayl.</param>
    /// <param name="uploadedBy">Yuklagan foydalanuvchi.</param>
    /// <param name="at">Lahza.</param>
    public Result<BlankAttachment> AddAttachment(BlankAttachmentFile file, Guid uploadedBy, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (IsFinal)
        {
            return BlankErrors.InvalidStatus;
        }

        var created = BlankAttachment.Create(Id, file, uploadedBy, at);
        if (created.IsFailure)
        {
            return created.Error;
        }

        _attachments.Add(created.Value);
        Version++;
        return created.Value;
    }

    /// <summary>
    /// Faylni olib tashlaydi (belgi bilan) — diskdagi nusxani chaqiruvchi saqlashdan keyin o'chiradi.
    /// </summary>
    /// <param name="attachmentId">Fayl.</param>
    /// <param name="removedBy">Foydalanuvchi.</param>
    /// <param name="at">Lahza.</param>
    public Result<BlankAttachment> RemoveAttachment(Guid attachmentId, Guid removedBy, DateTimeOffset at)
    {
        if (IsFinal)
        {
            return BlankErrors.InvalidStatus;
        }

        var attachment = _attachments.Find(item => item.Id == attachmentId && item.IsActive);
        if (attachment is null)
        {
            return BlankErrors.FileNotFound;
        }

        attachment.Remove(removedBy, at);
        Version++;
        return attachment;
    }

    /// <summary>
    /// Tasdiqlangan blankaning PDF nusxasini qayd etadi. <see cref="Version"/> OSHMAYDI: bu
    /// foydalanuvchi o'zgarishi emas, tasdiqdan keyingi texnik yozuv.
    /// </summary>
    /// <param name="storageKey">Ombor kaliti.</param>
    public Result SetFinalPdf(string? storageKey)
    {
        if (Status != BlankSubmissionStatus.Approved)
        {
            return BlankErrors.InvalidStatus;
        }

        if (string.IsNullOrWhiteSpace(storageKey) || storageKey.Length > BlankSubmissionStep.MaxStorageKeyLength)
        {
            return BlankErrors.Invalid;
        }

        FinalPdfStorageKey = storageKey;
        return Result.Success();
    }

    private Result<BlankSubmissionStep> FindOpenStep(Guid stepId)
    {
        if (Status != BlankSubmissionStatus.InApproval)
        {
            return BlankErrors.InvalidStatus;
        }

        var step = _steps.Find(item => item.Id == stepId);
        if (step is null)
        {
            return BlankErrors.StepNotFound;
        }

        return step.State == BlankStepState.Open ? step : BlankErrors.StepNotOpen;
    }

    /// <summary>
    /// Guruh qoidasi: ochiq qadam qolmagan bo'lsa, keyingi kutayotgan guruh (eng kichik tartib)
    /// ochiladi; kutayotgan ham qolmasa — blanka tasdiqlanadi.
    /// </summary>
    private void Advance(DateTimeOffset at, Func<string, bool>? isStepDue = null)
    {
        if (_steps.Exists(step => step.State == BlankStepState.Open))
        {
            return;
        }

        // Navbati kelgan guruhning sharti bajarilmagan qadamlari o'tkazib yuboriladi; guruh butunlay
        // o'tkazilsa — keyingi guruhga (§4.11 p.3: HR-F13 guvohlari, tushuntirish yozilgan bo'lsa).
        var waiting = _steps.Where(step => step.State == BlankStepState.Waiting).ToList();
        while (isStepDue is not null && waiting.Count > 0)
        {
            var due = waiting.Min(step => step.OrderNo);
            var group = waiting.Where(step => step.OrderNo == due).ToList();
            foreach (var step in group.Where(step => !isStepDue(step.StepCode)))
            {
                step.Skip();
            }

            if (group.Exists(step => step.State == BlankStepState.Waiting))
            {
                break;
            }

            waiting.RemoveAll(step => step.OrderNo == due);
        }

        if (waiting.Count == 0)
        {
            Status = BlankSubmissionStatus.Approved;
            CompletedAt = at;
            return;
        }

        var next = waiting.Min(step => step.OrderNo);
        foreach (var step in waiting.Where(step => step.OrderNo == next))
        {
            step.Open();
        }
    }

    private void Apply(BlankAnswersState state)
    {
        Answers = state.Answers;
        Computed = state.Computed;
        Missing = state.Missing;
        MissingCount = state.MissingCount;
        DueDate = state.DueDate;
    }

    private static Error? Validate(BlankAnswersState state)
    {
        if (string.IsNullOrWhiteSpace(state.Answers)
            || string.IsNullOrWhiteSpace(state.Computed)
            || string.IsNullOrWhiteSpace(state.Missing))
        {
            return BlankErrors.InvalidJson;
        }

        if (Encoding.UTF8.GetByteCount(state.Answers) > MaxAnswersBytes)
        {
            return BlankErrors.TooLarge;
        }

        return state.MissingCount < 0 ? BlankErrors.Invalid : null;
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Blanka kim haqida — ochilish paytidagi nusxa.</summary>
/// <param name="EmployeeId">Xodim (sub'yekt xodim bo'lsa).</param>
/// <param name="EmploymentId">Faol yoki oxirgi ish joyi.</param>
/// <param name="CandidateId">Nomzod (sub'yekt nomzod bo'lsa).</param>
/// <param name="DepartmentId">Bo'lim nusxasi.</param>
public sealed record BlankSubject(
    Guid? EmployeeId = null,
    Guid? EmploymentId = null,
    Guid? CandidateId = null,
    Guid? DepartmentId = null)
{
    /// <summary>Sub'yektsiz blanka.</summary>
    public static BlankSubject None { get; } = new();

    /// <summary>Shablon turiga mosmi (bazadagi CHECK bilan bir xil qoida).</summary>
    /// <param name="kind">Shablonning sub'yekt turi.</param>
    public bool Matches(BlankSubjectKind kind) => kind switch
    {
        BlankSubjectKind.Employee => EmployeeId is { } employee && employee != Guid.Empty && CandidateId is null,
        BlankSubjectKind.Candidate => CandidateId is { } candidate && candidate != Guid.Empty && EmployeeId is null && EmploymentId is null,
        BlankSubjectKind.None => EmployeeId is null && CandidateId is null && EmploymentId is null,
        _ => false,
    };
}
