using Bonvi.Domain.Common;

namespace Bonvi.Domain.Recruiting;

/// <summary>Ochilgan ish o'rnining holati.</summary>
public enum VacancyStatus
{
    /// <summary>Ochiq — nomzod qabul qilinadi.</summary>
    Open = 1,

    /// <summary>
    /// To'xtatilgan — vaqtincha yopilgan, lekin bekor qilinmagan.
    /// </summary>
    /// <remarks>
    /// Nega <see cref="Closed"/> dan alohida: «byudjet kutilyapti» va
    /// «o'rin to'ldi» ikki xil fakt. Ular bir holatga yig'ilsa, HR nima
    /// uchun yopilganini faqat izohdan taxmin qilardi va «qайta ochish
    /// kerakmi?» degan savolga javob yo'qolardi.
    /// </remarks>
    Paused = 2,

    /// <summary>Yopilgan — ish o'rni yakunlangan (to'ldirilgan yoki bekor qilingan).</summary>
    Closed = 3,
}

/// <summary>
/// Vakansiya — ochilgan ish o'rni, ya'ni E'LON.
/// </summary>
/// <remarks>
/// <para>
/// <b>Voronkaning boshlanish nuqtasi.</b> Har bir nomzod aynan bitta
/// vakansiyaga tegishli: «bu odam nimaga kelgan» degan savolga javob
/// bo'lishi kerak. Vakansiyasiz nomzod — bu shunchaki telefon raqami
/// yozilgan qog'oz, u hech qanday hisobotga tushmaydi.
/// </para>
/// <para>
/// <b>⭐ Vakansiyada kompaniya tuzilmasidan HECH NARSA yo'q.</b> Lavozim
/// yo'q, bo'lim yo'q, yuridik shaxs ham yo'q — faqat
/// <see cref="VacancyTitleId"/>, ya'ni e'londa turadigan nom. Bu ataylab
/// va bu — foydalanuvchining aniq qarori («vakansiya nomi bilan kompaniya
/// lavozimi bir-biriga aralashmasin»).
/// </para>
/// <para>
/// <b>NEGA lavozim yo'q.</b> Batafsil sabab <see cref="VacancyTitle"/>
/// izohida: e'lon tashqariga qaraydi va nomzod tushunadigan tilda
/// yoziladi, shtat birligi esa ichkariga — buyruqqa va shartnomaga.
/// Ilgari vakansiya <c>Position</c> ma'lumotnomasidan tanlanardi va
/// natijada e'lon uchun ochilgan har bir nom shtat jadvaliga tushib
/// ketardi.
/// </para>
/// <para>
/// <b>NEGA bo'lim yo'q.</b> Bitta e'longa kelgan odamlar turli bo'limlarga
/// tarqaladi: «Сотувчи-консультант» — bugun do'konga, ertaga omborga.
/// Bo'limni e'longa yozib qo'yish HR ni har bo'lim uchun ALOHIDA vakansiya
/// ochishga majbur qilardi va bitta e'lonning nomzodlari o'nta ro'yxatga
/// bo'linib ketardi.
/// </para>
/// <para>
/// <b>NEGA yuridik shaxs ham yo'q.</b> Bu eng oxirgi olib tashlangani va
/// sabab yuqoridagilar bilan bir xil: vakansiya — E'LON, uni kim ko'radi
/// va qaysi firmaga ishga olinishi HAR DOIM ham bir xil emas. Bitta
/// «Сотувчи-консультант» e'loniga kelgan odam holdingning turli
/// firmalariga ishga olinishi mumkin. Firma, bo'lim va lavozim — uchalasi
/// ham ishga qabul paytidagi QAROR, e'lonning xossasi emas.
/// </para>
/// <para>
/// <b>Uchalasi QAYERDA so'raladi.</b> Aynan bitta nuqtada —
/// <c>AcceptJobOfferCommand</c> da, ya'ni taklif qabul qilinganda. O'sha
/// yerda HR firma, bo'lim va lavozimni MAVJUD ma'lumotnomalardan tanlaydi
/// va ular to'g'ridan-to'g'ri <c>HireEmployeeCommand</c> ga uzatiladi.
/// Ya'ni e'lon bilan shtat jadvali bitta joyda, bitta qarorda uchrashadi —
/// va o'sha qaror hujjat bo'lib qoladi.
/// </para>
/// <para>
/// <b>Nega o'rin to'lganda O'ZI yopilmaydi.</b> <see cref="Openings"/> —
/// reja, fakt emas: HR bitta vakansiyaga ikki kishi olishi ham, bittasini
/// olib qolganini keyingi oyga qoldirishi ham mumkin. Tizim o'zi yopib
/// qo'ysa, HR buni sezmasdan yangi nomzodni kirita olmay qolardi. O'rin
/// nechtasi to'lgani ro'yxatda ko'rsatiladi (<c>HiredCount</c>), yopish
/// qarori esa odamniki.
/// </para>
/// </remarks>
public sealed class Vacancy : AggregateRoot, IAuditable, ISoftDeletable
{
    /// <summary>Bitta vakansiyada eng ko'p nechta o'rin bo'lishi mumkin.</summary>
    /// <remarks>
    /// Chegara kiritish xatosiga qarshi: «5» o'rniga «55» yozilishi oson va
    /// bunday yozuv hisobotda «55 ta odam kerak» bo'lib ko'rinardi. Bonvi'da
    /// eng katta ommaviy qabul — zavod sexiga 20-30 kishi, shu sababli 100
    /// yetarlicha keng, lekin ma'nosiz qiymatni to'sadi.
    /// </remarks>
    public const int MaxOpenings = 100;

    private Vacancy(Guid id, Guid vacancyTitleId, int openings, DateOnly openedAt)
        : base(id)
    {
        VacancyTitleId = vacancyTitleId;
        Openings = openings;
        OpenedAt = openedAt;
        Status = VacancyStatus.Open;
    }

    /// <summary>EF Core uchun.</summary>
    private Vacancy() { }

    /// <summary>
    /// E'londa turadigan nom — <c>vacancy_titles</c> ma'lumotnomasidan.
    /// </summary>
    /// <remarks>
    /// Vakansiyaning YAGONA mazmuniy bog'lanishi. Bu <b>lavozim emas</b>:
    /// shtat birligi ishga qabul paytida alohida tanlanadi (sinf izohiga
    /// qarang).
    /// </remarks>
    public Guid VacancyTitleId { get; private set; }

    /// <summary>Nechta odam kerak.</summary>
    public int Openings { get; private set; }

    /// <summary>Vakansiya ochilgan sana.</summary>
    public DateOnly OpenedAt { get; private set; }

    /// <summary>Yopilgan sana. <see cref="VacancyStatus.Closed"/> bo'lmaguncha bo'sh.</summary>
    public DateOnly? ClosedAt { get; private set; }

    public VacancyStatus Status { get; private set; }

    /// <summary>Vakansiya uchun mas'ul xodim (rekruter).</summary>
    /// <remarks>
    /// <para>
    /// Xodimga ishora, foydalanuvchi hisobiga emas — <c>Incident</c> va
    /// <c>EmployeeFlag</c> dagi bilan bir xil naqsh: mas'ul odam tizimga
    /// kirmasligi ham mumkin (xodimlarga login berilmaydi).
    /// </para>
    /// <para>
    /// Bu maydon xodimlar jadvaliga ishora qilsa ham, «kompaniya
    /// tuzilmasi» emas: u e'lonni KIM olib boryapti degan savolga javob
    /// beradi, nomzod QAYERGA olinadi degan savolga emas. Shu sababli
    /// lavozim/bo'lim/firma bilan birga olib tashlanmadi.
    /// </para>
    /// </remarks>
    public Guid? ResponsibleEmployeeId { get; private set; }

    /// <summary>Erkin izoh: shoshilinchlik, talablar, yopish sababi.</summary>
    public string? Note { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>Nomzod qabul qilinadigan holatdami.</summary>
    /// <remarks>
    /// To'xtatilgan vakansiyaga ham nomzod kiritilmaydi: to'xtatishning
    /// butun ma'nosi — «hozircha kerak emas». Aks holda to'xtatish faqat
    /// ro'yxatdagi rangni o'zgartirardi.
    /// </remarks>
    public bool AcceptsCandidates => Status == VacancyStatus.Open;

    public static Result<Vacancy> Open(
        Guid vacancyTitleId,
        DateOnly openedAt,
        int openings = 1,
        Guid? responsibleEmployeeId = null,
        string? note = null)
    {
        if (vacancyTitleId == Guid.Empty)
        {
            return Error.Validation("vakansiya.nom_yoq", "Vakansiya nomi ko'rsatilishi shart.");
        }

        var checkedOpenings = ValidateOpenings(openings);
        if (checkedOpenings.IsFailure)
        {
            return checkedOpenings.Error;
        }

        return new Vacancy(NewId(), vacancyTitleId, openings, openedAt)
        {
            ResponsibleEmployeeId = responsibleEmployeeId,
            Note = Trim(note),
        };
    }

    /// <summary>
    /// Vakansiya tafsilotlarini tuzatadi.
    /// </summary>
    /// <remarks>
    /// <b>Vakansiya nomi BU YERDA ham o'zgaradi</b> — <c>Incident.Update</c>
    /// dagi qoidadan farqli. Sabab: baxtsiz hodisada xodim almashtirilsa,
    /// bu boshqa FAKT bo'lardi; vakansiya esa fakt emas, REJA — u
    /// to'ldirilishidan oldin qayta ko'rib chiqilishi odatiy hol
    /// («сотувчи эмас, катта сотувчи керак экан»). Holat esa bu yerdan
    /// o'zgarmaydi: uning o'z amallari bor
    /// (<see cref="Pause"/> / <see cref="Resume"/> / <see cref="Close"/>).
    /// </remarks>
    public Result Update(
        Guid vacancyTitleId,
        DateOnly openedAt,
        int openings = 1,
        Guid? responsibleEmployeeId = null,
        string? note = null)
    {
        if (vacancyTitleId == Guid.Empty)
        {
            return Error.Validation("vakansiya.nom_yoq", "Vakansiya nomi ko'rsatilishi shart.");
        }

        var checkedOpenings = ValidateOpenings(openings);
        if (checkedOpenings.IsFailure)
        {
            return checkedOpenings.Error;
        }

        VacancyTitleId = vacancyTitleId;
        Openings = openings;
        OpenedAt = openedAt;
        ResponsibleEmployeeId = responsibleEmployeeId;
        Note = Trim(note);

        return Result.Success();
    }

    /// <summary>Vaqtincha to'xtatadi.</summary>
    public Result Pause(string? reason = null)
    {
        if (Status == VacancyStatus.Closed)
        {
            return Error.Conflict("vakansiya.yopiq", "Yopilgan vakansiyani to'xtatib bo'lmaydi.");
        }

        Status = VacancyStatus.Paused;
        Note = Trim(reason) ?? Note;

        return Result.Success();
    }

    /// <summary>To'xtatilgan yoki yopilgan vakansiyani qayta ochadi.</summary>
    /// <remarks>
    /// Yopilgan vakansiyani qayta ochish ATAYLAB mumkin: o'rin to'ldi deb
    /// yopilgan vakansiyaga olingan odam bir haftada ketib qolishi Bonvi'da
    /// odatiy hol («91.8 Стажировкадан кетганлар» — 231 yozuv). Bunda yangi
    /// vakansiya ochish butun nomzodlar tarixini ikkiga bo'lib yuborardi.
    /// <see cref="ClosedAt"/> tozalanadi: vakansiya yana ochiq, ya'ni
    /// «qachon yopilgan» degan savolning javobi yo'q.
    /// </remarks>
    public Result Resume()
    {
        Status = VacancyStatus.Open;
        ClosedAt = null;

        return Result.Success();
    }

    /// <summary>Vakansiyani yopadi.</summary>
    public Result Close(DateOnly closedAt, string? reason = null)
    {
        if (Status == VacancyStatus.Closed)
        {
            return Error.Conflict("vakansiya.allaqachon_yopiq", "Bu vakansiya allaqachon yopilgan.");
        }

        if (closedAt < OpenedAt)
        {
            return Error.Validation(
                "vakansiya.yopish_sanasi",
                "Yopilish sanasi ochilish sanasidan oldin bo'lishi mumkin emas.");
        }

        Status = VacancyStatus.Closed;
        ClosedAt = closedAt;
        Note = Trim(reason) ?? Note;

        return Result.Success();
    }

    private static Result ValidateOpenings(int openings)
        => openings is < 1 or > MaxOpenings
            ? Error.Validation(
                "vakansiya.orin_soni",
                $"Kerakli odamlar soni 1 dan {MaxOpenings} gacha bo'lishi kerak.")
            : Result.Success();

    private static string? Trim(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
