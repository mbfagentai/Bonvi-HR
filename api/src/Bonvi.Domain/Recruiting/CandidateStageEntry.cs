using Bonvi.Domain.Common;

namespace Bonvi.Domain.Recruiting;

/// <summary>Nomzodning bosqichdagi natijasi.</summary>
public enum StageOutcome
{
    /// <summary>Kutilmoqda — bosqich boshlandi, natija hali yo'q.</summary>
    Pending = 1,

    /// <summary>O'tdi.</summary>
    Passed = 2,

    /// <summary>O'tmadi.</summary>
    Failed = 3,
}

/// <summary>
/// Nomzodning bitta bosqichdan o'tishi: qachon, qanday natija, qanday xulosa.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bu yozuv butun voronkaning yagona «hodisasi».</b> Telefon suhbat
/// natijasi ham, kengash xulosasi ham, rahbar suhbati ham — hammasi shu
/// yozuv. Foydalanuvchi qarori shuni talab qildi: kengash ovoz bermaydi,
/// HR bitta xulosa yozadi (natija + izoh + sana). Ya'ni «Кенгаш» — bu
/// mexanizm emas, <see cref="PipelineStage"/> ning bir qiymati.
/// </para>
/// <para>
/// <b>Nega alohida <c>Interview</c> agregati YOZILMADI.</b> Suhbat ham
/// bosqich: uning «kim, qachon, qanday xulosa» dan boshqa maydoni yo'q.
/// Alohida agregat bo'lganida voronka ikki xil tarixdan yig'ilardi
/// (bosqichlar + suhbatlar) va «nomzod hozir qayerda» degan savolga javob
/// ikki jadvalni birlashtirib topilardi. Bitta oqim esa aynan
/// <c>EmploymentEvent</c> naqshi: bitta hodisa jurnali, qolgan hamma
/// ko'rinish undan hosil bo'ladi.
/// </para>
/// <para>
/// <b>Nega bu agregat ILDIZI emas, <see cref="Candidate"/> ning bolasi.</b>
/// Yozuv nomzoddan ajralgan holda ma'nosiz va u DOIM nomzod bilan birga
/// o'qiladi. Muhimrog'i — <c>Candidate.CurrentStageId</c> aynan shu
/// yozuvlardan kelib chiqadi: ikkisi bitta tranzaksiyada o'zgarishi shart,
/// aks holda nomzod «Кенгашда» turgan bo'lib ko'rinib, tarixida kengash
/// yozuvi bo'lmasdi.
/// </para>
/// <para>
/// <b>Yozuv o'zgarmas EMAS</b> — <c>EmploymentEvent</c> dan farqli.
/// Sabab: mehnat hodisasi buyruq bilan rasmiylashtiriladi va buyruq bekor
/// qilinmaydi; suhbat xulosasi esa ichki qayd — HR uni suhbat davomida
/// «kutilmoqda» deb ochib, tugagach natijasini yozadi
/// (<see cref="Resolve"/>). Ikki bosqichli yozuvni o'zgarmas qilish har
/// suhbat uchun ikkita yozuv qoldirardi va tarix ikki barobar shovqin
/// bo'lardi.
/// </para>
/// </remarks>
public sealed class CandidateStageEntry : Entity
{
    private CandidateStageEntry(
        Guid id,
        Guid candidateId,
        Guid stageId,
        DateOnly date,
        StageOutcome outcome)
        : base(id)
    {
        CandidateId = candidateId;
        StageId = stageId;
        Date = date;
        Outcome = outcome;
    }

    /// <summary>EF Core uchun.</summary>
    private CandidateStageEntry() { }

    public Guid CandidateId { get; private set; }

    /// <summary>Qaysi bosqich — <c>pipeline_stages</c> ma'lumotnomasidan.</summary>
    public Guid StageId { get; private set; }

    /// <summary>Bosqich o'tkazilgan sana (suhbat kuni, kengash kuni).</summary>
    public DateOnly Date { get; private set; }

    public StageOutcome Outcome { get; private set; }

    /// <summary>
    /// Xulosa matni — «Кенгаш хулосаси», «телефон суҳбат натижаси».
    /// </summary>
    /// <remarks>
    /// Erkin matn va ATAYLAB majburiy emas: «kutilmoqda» yozuvida hali
    /// aytadigan gap yo'q. Natija qo'yilganda ham xulosa talab qilinmaydi —
    /// HR ba'zan faqat «o'tdi» deb belgilaydi va uni majburlash yolg'on
    /// («ok») xulosalar yozilishiga olib kelardi.
    /// </remarks>
    public string? Conclusion { get; private set; }

    /// <summary>
    /// Kim yozdi — TIZIM FOYDALANUVCHISI (<c>users.id</c>).
    /// </summary>
    /// <remarks>
    /// Bu yerda xodimga emas, foydalanuvchiga ishora qilinadi —
    /// <c>Vacancy.ResponsibleEmployeeId</c> dan farqli. Sabab: «kim mas'ul»
    /// va «kim kiritdi» ikki xil savol. Birinchisi tashkiliy fakt (mas'ul
    /// odam tizimga umuman kirmasligi mumkin), ikkinchisi esa audit
    /// faktidir va u faqat tizimga kirgan hisobdan chiqadi.
    /// </remarks>
    public Guid? RecordedBy { get; private set; }

    public DateTimeOffset RecordedAt { get; private set; }

    internal static Result<CandidateStageEntry> Create(
        Guid candidateId,
        Guid stageId,
        DateOnly date,
        StageOutcome outcome,
        string? conclusion = null,
        Guid? recordedBy = null)
    {
        if (stageId == Guid.Empty)
        {
            return Error.Validation("bosqich_yozuvi.bosqich_yoq", "Bosqich ko'rsatilishi shart.");
        }

        return new CandidateStageEntry(NewId(), candidateId, stageId, date, outcome)
        {
            Conclusion = Trim(conclusion),
            RecordedBy = recordedBy,
            RecordedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>Natijani va xulosani yozadi (yoki tuzatadi).</summary>
    internal Result Resolve(StageOutcome outcome, DateOnly date, string? conclusion, Guid? recordedBy)
    {
        Outcome = outcome;
        Date = date;
        Conclusion = Trim(conclusion);
        RecordedBy = recordedBy ?? RecordedBy;
        RecordedAt = DateTimeOffset.UtcNow;

        return Result.Success();
    }

    private static string? Trim(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
