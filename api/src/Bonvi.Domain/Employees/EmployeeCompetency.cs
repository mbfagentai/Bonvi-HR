using Bonvi.Domain.Common;

namespace Bonvi.Domain.Employees;

/// <summary>
/// Xodimning qobiliyatlar profili (Excel: «91.15 Қобилятлар жадвали»).
/// </summary>
/// <remarks>
/// <para>
/// Bitta xodimda BITTA profil bo'ladi — Excel varag'ida ham har bir odam
/// bir marta uchraydi. Shu sababli bu <c>EmployeeFlag</c> yoki
/// <c>Incentive</c> kabi tarix emas, balki xodim kartasining bir bo'lagi:
/// yangi ma'lumot eskisini ALMASHTIRADI, tarix sifatida qo'shilmaydi.
/// </para>
/// <para>
/// Excel varag'ida 554 yozuvdan aksariyatida atigi 2-3 ustun to'ldirilgan
/// (til va mutaxassislik ko'p, «компьютер саводхонлиги» deyarli hech
/// qachon). Shu sababli hamma maydon ixtiyoriy — to'liqlik talab qilinsa,
/// mavjud ma'lumotning katta qismi ko'chmay qolardi.
/// </para>
/// </remarks>
public sealed class EmployeeCompetency : AggregateRoot, IAuditable
{
    /// <summary>Kompyuter savodxonligi foizining yuqori chegarasi.</summary>
    public const int MaxComputerLiteracyPercent = 100;

    private EmployeeCompetency(Guid id, Guid employeeId) : base(id) => EmployeeId = employeeId;

    /// <summary>EF Core uchun.</summary>
    private EmployeeCompetency() { }

    public Guid EmployeeId { get; private set; }

    /// <summary>Til bilish (Excel: «Тил билиш») — erkin matn: «Инглиз ва рус тили».</summary>
    public string? Languages { get; private set; }

    /// <summary>
    /// Kompyuter savodxonligi, 0–100%.
    /// </summary>
    /// <remarks>
    /// Excel'da bu ustun («Компьютер саводхонлиги») deyarli har doim bo'sh
    /// edi — ya'ni haqiqiy ma'lumot emas, HR endi tizimda to'ldiradigan
    /// YANGI maydon. Foiz sifatida saqlanadi, chunki reja aynan shuni so'radi.
    /// </remarks>
    public int? ComputerLiteracyPercent { get; private set; }

    /// <summary>Mutaxassisligi (Excel: «Мутахассислиги») — masalan «Ихтисодиёт».</summary>
    public string? Specialization { get; private set; }

    /// <summary>Boshqa qobiliyatlar (Excel: «Бошка қобилиятлар»).</summary>
    public string? OtherAbilities { get; private set; }

    /// <summary>Qiziqishi nimada (Excel: «Қизикиши нимада»).</summary>
    public string? Interest { get; private set; }

    /// <summary>Ta'lim olgan joyi (Excel: «Талим олган жойи») — masalan «ТДТУ».</summary>
    public string? EducationPlace { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static Result<EmployeeCompetency> Create(Guid employeeId)
        => employeeId == Guid.Empty
            ? Error.Validation("qobiliyat.xodim_bosh", "Xodim ko'rsatilishi shart.")
            : new EmployeeCompetency(NewId(), employeeId);

    public void SetLanguages(string? value) => Languages = Trim(value);

    public Result SetComputerLiteracy(int? percent)
    {
        if (percent is { } value && value is < 0 or > MaxComputerLiteracyPercent)
        {
            return Error.Validation(
                "qobiliyat.kompyuter_foiz",
                $"Kompyuter savodxonligi 0 dan {MaxComputerLiteracyPercent} gacha bo'lishi kerak.");
        }

        ComputerLiteracyPercent = percent;
        return Result.Success();
    }

    public void SetSpecialization(string? value) => Specialization = Trim(value);

    public void SetOtherAbilities(string? value) => OtherAbilities = Trim(value);

    public void SetInterest(string? value) => Interest = Trim(value);

    public void SetEducationPlace(string? value) => EducationPlace = Trim(value);

    private static string? Trim(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
