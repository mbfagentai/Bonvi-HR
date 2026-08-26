using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;

namespace Bonvi.Domain.Feedback;

/// <summary>Kompaniyani boshqalarga tavsiya qilarmidi degan savolga javob.</summary>
public enum RecommendationAnswer
{
    Yes = 1,
    No = 2,
    DontKnow = 3,
}

/// <summary>
/// Ishdan ketuvchi xodim bilan suhbat — «qayta aloqa» (Excel: «91.16 Қайта алоқа»).
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega <c>Employment</c>ga, <c>Employee</c>ga emas bog'langan.</b> Reja
/// (4-bo'lim, ma'lumot modeli) buni aniq ko'rsatadi: <c>Employment ──&lt; ExitInterview</c>.
/// Bitta shaxsning bir nechta mehnat munosabati bo'lishi mumkin (turli
/// firmalarda yoki turli davrlarda) — suhbat aynan QAYSI ketishga tegishli
/// ekani muhim, shaxsning o'ziga emas. Shu sababli <see cref="EmploymentId"/>
/// FK, <c>EmployeeId</c> emas.
/// </para>
/// <para>
/// <b>Ish joyi/bo'lim/lavozim bu yerda QAYTARILMAYDI.</b> Excel'dagi Google
/// Form ularni matn sifatida so'ragan (xodim o'zi yozgan), lekin ular
/// allaqachon bog'langan <see cref="Employment"/> yozuvida bor — takrorlash
/// ikkita manba yaratardi (biri eskirsa, ikkinchisi bilan mos kelmay qoladi).
/// Ko'rsatishda ular <c>Employment</c> orqali olinadi.
/// </para>
/// <para>
/// <b>«Отметка времени» (Google Forms belgisi) o'rniga <see cref="InterviewDate"/>.</b>
/// Ichki formada bu HR suhbat NECHA sanada bo'lganini yozadi — bu ko'pincha
/// yozuv kiritilgan kundan (audit <c>CreatedAt</c>) farq qiladi, chunki
/// qog'ozdagi suhbat keyinroq tizimga kiritilishi mumkin.
/// </para>
/// <para>
/// <b>«Ишдан бўшашингиз сабаби» — rasmiy sababdan ALOHIDA.</b>
/// <c>Employment.TerminationReasonId</c> — buyruqdagi rasmiy sabab
/// (ma'lumotnomadan). <see cref="Reason"/> esa xodimning o'z so'zi bilan
/// aytgani — ular ko'pincha boshqa-boshqa: rasmiy hujjatda «o'z xohishi
/// bilan» yozilgan bo'lsa-da, suhbatda haqiqiy sabab (masalan rahbariyat
/// bilan ziddiyat) chiqishi mumkin. Aynan shu farq — exit interview'ning
/// butun qiymati, shu sababli ikkalasi ham saqlanadi va biri ikkinchisini
/// almashtirmaydi.
/// </para>
/// <para>
/// <b>Bittadan ortiq bo'lmaydi.</b> Bitta mehnat munosabati bir marta
/// tugaydi, demak unga bitta suhbat yozuvi yetarli — takroriy urinish
/// Application qatlamida rad etiladi (tekshiruv shu yerda emas, chunki
/// bu agregatga boshqa yozuvlar haqida ma'lumot kelmaydi).
/// </para>
/// </remarks>
public sealed class ExitInterview : AggregateRoot, IAuditable, ISoftDeletable
{
    private ExitInterview(Guid id, Guid employmentId, DateOnly interviewDate, string reason)
        : base(id)
    {
        EmploymentId = employmentId;
        InterviewDate = interviewDate;
        Reason = reason;
    }

    /// <summary>EF Core uchun.</summary>
    private ExitInterview() { }

    public Guid EmploymentId { get; private set; }

    /// <summary>Suhbat o'tkazilgan sana.</summary>
    public DateOnly InterviewDate { get; private set; }

    /// <summary>Ishdan ketish sababi — xodimning o'z so'zi bilan. Majburiy.</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>Kompaniyani boshqalarga tavsiya qilarmidi.</summary>
    public RecommendationAnswer? WouldRecommend { get; private set; }

    /// <summary>Kompaniya haqida umumiy taassurot.</summary>
    public string? Impressions { get; private set; }

    /// <summary>Qanday o'zgarish bo'lsa qayta ishlashga rozi bo'lardi.</summary>
    public string? ConditionsToReturn { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    public static Result<ExitInterview> Record(
        Guid employmentId,
        DateOnly interviewDate,
        string? reason,
        RecommendationAnswer? wouldRecommend = null,
        string? impressions = null,
        string? conditionsToReturn = null)
    {
        if (employmentId == Guid.Empty)
        {
            return Error.Validation("qayta_aloqa.mehnat_yoq", "Mehnat munosabati ko'rsatilishi shart.");
        }

        var trimmedReason = Trim(reason);
        if (trimmedReason is null)
        {
            return Error.Validation("qayta_aloqa.sabab_bosh", "Ishdan ketish sababi kiritilishi shart.");
        }

        return new ExitInterview(NewId(), employmentId, interviewDate, trimmedReason)
        {
            WouldRecommend = wouldRecommend,
            Impressions = Trim(impressions),
            ConditionsToReturn = Trim(conditionsToReturn),
        };
    }

    /// <summary>
    /// Kiritish xatosini tuzatadi: sana, sabab, tavsiya, taassurot, shart.
    /// </summary>
    /// <remarks>
    /// <see cref="EmploymentId"/> O'ZGARMAYDI — <c>Incentive.Update</c> dagi
    /// bilan bir xil qoida: agar suhbat noto'g'ri mehnat munosabatiga
    /// yozilgan bo'lsa, bu tuzatish emas, boshqa fakt.
    /// </remarks>
    public Result Update(
        DateOnly interviewDate,
        string? reason,
        RecommendationAnswer? wouldRecommend = null,
        string? impressions = null,
        string? conditionsToReturn = null)
    {
        var trimmedReason = Trim(reason);
        if (trimmedReason is null)
        {
            return Error.Validation("qayta_aloqa.sabab_bosh", "Ishdan ketish sababi kiritilishi shart.");
        }

        InterviewDate = interviewDate;
        Reason = trimmedReason;
        WouldRecommend = wouldRecommend;
        Impressions = Trim(impressions);
        ConditionsToReturn = Trim(conditionsToReturn);

        return Result.Success();
    }

    private static string? Trim(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
