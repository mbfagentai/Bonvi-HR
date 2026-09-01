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

    private readonly List<EmployeeCompetencyLanguage> _languageSkills = [];

    private EmployeeCompetency(Guid id, Guid employeeId) : base(id) => EmployeeId = employeeId;

    /// <summary>EF Core uchun.</summary>
    private EmployeeCompetency() { }

    public Guid EmployeeId { get; private set; }

    /// <summary>
    /// Til bilish (Excel: «Тил билиш») — erkin matn: «Инглиз ва рус тили».
    /// </summary>
    /// <remarks>
    /// ⚠️ ESKI yozuv, yangi kiritishlar <see cref="LanguageSkills"/> ga
    /// boradi. Ustun ATAYLAB o'chirilmagan: Excel'dan ko'chgan yozuvlarda
    /// erkin matn bor va uni «Рус тили , Инглиз тили» kabi qiymatlardan
    /// avtomatik ajratish taxmin bo'lardi — noto'g'ri ajratilgan til
    /// yo'qolgan matndan yomonroq. Strukturaviy ro'yxat bo'sh bo'lsa,
    /// ekran shu matnni «eski yozuv» sifatida ko'rsatadi.
    /// </remarks>
    public string? Languages { get; private set; }

    /// <summary>Til bilish — strukturaviy ro'yxat: til + daraja.</summary>
    public IReadOnlyList<EmployeeCompetencyLanguage> LanguageSkills => _languageSkills.AsReadOnly();

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

    /// <summary>
    /// Til ro'yxatini BUTUNLIGICHA almashtiradi — profildagi boshqa
    /// maydonlar kabi (butun forma yuboriladi, qisman yangilash yo'q).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Bitta til — bir marta.</b> «Ingliz — Elementary» va «Ingliz —
    /// Advanced» birga tursa, «bu odam inglizchani qay darajada biladi?»
    /// savoli javobsiz qolardi. Takror kelsa butun amal rad etiladi,
    /// jimgina birlashtirish yo'q — HR qaysi darajani nazarda tutganini
    /// faqat o'zi biladi.
    /// </para>
    /// <para>
    /// <b>Sanoq qiymatlari ham tekshiriladi.</b> JSON'da sanoq NOM bilan
    /// yursa ham, raqam yuborilsa binder uni tekshirmasdan qabul qiladi —
    /// <c>(LanguageCode)99</c> bazaga tushib, keyin hech qaysi ro'yxatda
    /// ko'rinmay yotardi.
    /// </para>
    /// <para>
    /// <b>Mavjud yozuvlar saqlanadi.</b> Ro'yxatda qolgan tilning yozuvi
    /// o'chirilib qayta yaratilmaydi, faqat darajasi yangilanadi — aks
    /// holda har saqlashda barcha qatorlar yangi <c>Id</c> olib, bazada
    /// keraksiz DELETE+INSERT bo'lardi.
    /// </para>
    /// </remarks>
    public Result SetLanguageSkills(IReadOnlyList<LanguageSkill> skills)
    {
        ArgumentNullException.ThrowIfNull(skills);

        var seen = new HashSet<LanguageCode>();
        foreach (var skill in skills)
        {
            if (!Enum.IsDefined(skill.Language) || !Enum.IsDefined(skill.Level))
            {
                return Error.Validation(
                    "qobiliyat.til_notogri",
                    "Til yoki daraja ro'yxatdan tashqarida.");
            }

            if (!seen.Add(skill.Language))
            {
                return Error.Validation(
                    "qobiliyat.til_takror",
                    "Bitta til ro'yxatda faqat bir marta kelishi mumkin.");
            }
        }

        _languageSkills.RemoveAll(existing => skills.All(skill => skill.Language != existing.Language));

        foreach (var skill in skills)
        {
            var existing = _languageSkills.Find(entry => entry.Language == skill.Language);
            if (existing is null)
            {
                _languageSkills.Add(EmployeeCompetencyLanguage.Create(Id, skill.Language, skill.Level));
            }
            else
            {
                existing.SetLevel(skill.Level);
            }
        }

        return Result.Success();
    }

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
