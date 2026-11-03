using Bonvi.Domain.Common;

namespace Bonvi.Domain.Blanks;

/// <summary>
/// Yuborishda ta'rifdan yaratiladigan qadam rejasi — <see cref="BlankSubmission.Submit"/> kiritmasi.
/// </summary>
/// <param name="StepCode">Ta'rifdagi qadam <c>id</c> si.</param>
/// <param name="OrderNo">Tartib; teng raqamlar — parallel guruh.</param>
/// <param name="Title">Sarlavha (nusxa).</param>
/// <param name="ActorRole">Bajaruvchi roli (<see cref="BlankActorRoles"/>).</param>
/// <param name="Mode">Qanday imzolanishi mumkin.</param>
/// <param name="Skipped">Shart (<c>when</c>) bajarilmadi — qadam o'tkazib yuboriladi.</param>
/// <param name="DueDate">Muddat (ta'rifdagi <c>due</c>).</param>
public sealed record BlankStepPlan(
    string StepCode,
    int OrderNo,
    string Title,
    string ActorRole,
    BlankStepMode Mode,
    bool Skipped = false,
    DateOnly? DueDate = null);

/// <summary>
/// Blankaning imzo qadami (<c>blank_submission_steps</c>), SPEC-blankalar §3.4.
/// </summary>
/// <remarks>
/// <para>
/// Blankasiz ma'noga ega emas — o'z repozitoriysi yo'q, faqat <see cref="BlankSubmission"/>
/// orqali yaratiladi va o'zgaradi (holat o'tishlari guruh qoidasiga bog'liq va u agregatning
/// invarianti). Shu sababli o'zgartiruvchi metodlar <c>internal</c>.
/// </para>
/// <para>
/// Qadamlar YUBORISHDA ta'rifdan yaratiladi va «qaytarish» da o'chiriladi (ularning amallari
/// audit jurnalida qoladi) — keyingi yuborishda ta'rif bo'yicha qaytadan yaratiladi.
/// </para>
/// </remarks>
public sealed class BlankSubmissionStep : Entity
{
    /// <summary>Qadam kodi uzunligi.</summary>
    public const int MaxStepCodeLength = 64;

    /// <summary>Sarlavha uzunligi.</summary>
    public const int MaxTitleLength = 200;

    /// <summary>Qaror qiymati uzunligi.</summary>
    public const int MaxDecisionLength = 64;

    /// <summary>Izoh uzunligi.</summary>
    public const int MaxCommentLength = 2000;

    /// <summary>Imzolovchi ismi uzunligi.</summary>
    public const int MaxSignerNameLength = 200;

    /// <summary>Fayl ombori kaliti uzunligi.</summary>
    public const int MaxStorageKeyLength = 500;

    private BlankSubmissionStep(Guid id, Guid submissionId, BlankStepPlan plan)
        : base(id)
    {
        SubmissionId = submissionId;
        StepCode = plan.StepCode;
        OrderNo = plan.OrderNo;
        Title = plan.Title;
        ActorRole = plan.ActorRole;
        Mode = plan.Mode;
        DueDate = plan.DueDate;
        State = plan.Skipped ? BlankStepState.Skipped : BlankStepState.Waiting;
    }

    private BlankSubmissionStep() { }

    public Guid SubmissionId { get; private set; }

    /// <summary>Ta'rifdagi qadam <c>id</c> si — blanka ichida unikal.</summary>
    public string StepCode { get; private set; } = string.Empty;

    /// <summary>Tartib; teng raqamlar — parallel guruh.</summary>
    public int OrderNo { get; private set; }

    /// <summary>Sarlavha — yuborish paytidagi nusxa.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Bajaruvchi roli (<see cref="BlankActorRoles"/>).</summary>
    public string ActorRole { get; private set; } = string.Empty;

    /// <summary>Qanday imzolanishi mumkin.</summary>
    public BlankStepMode Mode { get; private set; }

    public BlankStepState State { get; private set; }

    /// <summary>Tanlangan qaror (<c>agree</c>, <c>object</c>, …).</summary>
    public string? Decision { get; private set; }

    public string? Comment { get; private set; }

    /// <summary>Muddat.</summary>
    public DateOnly? DueDate { get; private set; }

    /// <summary>Amalda qanday imzolangan.</summary>
    public BlankSignMethod? Method { get; private set; }

    /// <summary>Tugmani bosgan panel foydalanuvchisi (elektron) yoki planshetni boshqargan HR (qo'lda).</summary>
    public Guid? ActedByUserId { get; private set; }

    /// <summary>Qo'lda imzolagan yoki aniqlangan shaxs.</summary>
    public Guid? SignerEmployeeId { get; private set; }

    /// <summary>Imzolovchining ismi — nusxa (guvohda qo'lda yoziladi).</summary>
    public string? SignerName { get; private set; }

    /// <summary>Imzo rasmi (PNG) ombordagi kaliti — AI'ga va jurnalga hech qachon chiqmaydi.</summary>
    public string? SignatureStorageKey { get; private set; }

    /// <summary>Shu bajaruvchi imzo bilan birga yuborgan bo'lim qiymatlari (JSON) — huquqiy iz.</summary>
    public string? AnswersPatch { get; private set; }

    /// <summary>Amal lahzasi.</summary>
    public DateTimeOffset? ActedAt { get; private set; }

    /// <summary>Imzolangan yoki o'tkazib yuborilgan — guruh yopilishiga to'sqinlik qilmaydi.</summary>
    public bool IsDone => State is BlankStepState.Signed or BlankStepState.Skipped;

    /// <summary>Usul qadam rejimiga mosmi.</summary>
    public bool Allows(BlankSignMethod method) => method switch
    {
        BlankSignMethod.Electronic => Mode is BlankStepMode.Electronic or BlankStepMode.Either,
        BlankSignMethod.InPerson => Mode is BlankStepMode.InPerson or BlankStepMode.Either,
        _ => false,
    };

    internal static Result<BlankSubmissionStep> Create(Guid submissionId, BlankStepPlan plan)
    {
        if (string.IsNullOrWhiteSpace(plan.StepCode) || plan.StepCode.Length > MaxStepCodeLength
            || string.IsNullOrWhiteSpace(plan.Title) || plan.Title.Length > MaxTitleLength
            || !BlankActorRoles.IsKnown(plan.ActorRole)
            || !Enum.IsDefined(plan.Mode))
        {
            return BlankErrors.Invalid;
        }

        return new BlankSubmissionStep(NewId(), submissionId, plan);
    }

    internal void Open() => State = BlankStepState.Open;

    internal void Sign(BlankStepAct act, DateTimeOffset at)
    {
        State = BlankStepState.Signed;
        Method = act.Method;
        Decision = act.Decision;
        Comment = act.Comment;
        ActedByUserId = act.ActedByUserId;
        SignerEmployeeId = act.SignerEmployeeId;
        SignerName = act.SignerName;
        SignatureStorageKey = act.SignatureStorageKey;
        AnswersPatch = act.AnswersPatch;
        ActedAt = at;
    }

    internal void Reject(Guid? actedByUserId, Guid? signerEmployeeId, string comment, DateTimeOffset at)
    {
        State = BlankStepState.Rejected;
        Method = BlankSignMethod.Electronic;
        Comment = comment;
        ActedByUserId = actedByUserId;
        SignerEmployeeId = signerEmployeeId;
        ActedAt = at;
    }
}

/// <summary>Qadamni imzolash amali — <see cref="BlankSubmission.SignStep"/> kiritmasi.</summary>
/// <param name="Method">Elektron yoki qo'lda.</param>
/// <param name="ActedByUserId">Tugmani bosgan / planshetni boshqargan foydalanuvchi (ilova kanalida <c>null</c>).</param>
/// <param name="SignerEmployeeId">Imzolovchi shaxs (aniqlangan bo'lsa).</param>
/// <param name="SignerName">Imzolovchining ismi (guvohda majburiy — buni Application tekshiradi).</param>
/// <param name="SignatureStorageKey">Qo'lda imzoda — imzo rasmi kaliti (majburiy).</param>
/// <param name="Decision">Tanlangan qaror (majburiyligini ta'rif biladi — Application tekshiradi).</param>
/// <param name="Comment">Izoh.</param>
/// <param name="AnswersPatch">Bajaruvchi yuborgan bo'lim qiymatlari (JSON).</param>
public sealed record BlankStepAct(
    BlankSignMethod Method,
    Guid? ActedByUserId,
    Guid? SignerEmployeeId = null,
    string? SignerName = null,
    string? SignatureStorageKey = null,
    string? Decision = null,
    string? Comment = null,
    string? AnswersPatch = null);
