using Bonvi.Domain.Common;

namespace Bonvi.Domain.Recruiting;

/// <summary>
/// Ochilgan ish o'rnining holati — IKKITA, uchtasi emas.
/// </summary>
/// <remarks>
/// <para>
/// <b>NEGA «to'xtatilgan» (Paused) OLIB TASHLANDI.</b> Pauza va yopish
/// tizim uchun AYNI natijani berardi: ikkalasida ham vakansiya yangi
/// nomzod qabul qilmaydi (<c>Vacancy.AcceptsCandidates</c> ikkalasini ham
/// <c>false</c> deb hisoblardi), ikkalasidan ham qaytish mumkin edi.
/// Yagona farq HR ning NIYATIDA edi — «vaqtincha» yoki «tugadi». Niyat
/// esa tizim harakat qiladigan narsa emas: undan hech qanday tekshiruv,
/// hisobot yoki cheklov kelib chiqmasdi.
/// </para>
/// <para>
/// Ilgari bu yerda «byudjet kutilyapti» va «o'rin to'ldi» ikki xil fakt
/// degan izoh turardi. Bu to'g'ri, lekin bu farq HOLATNING emas,
/// SABABNING farqi — va sabab uchun <c>Vacancy.Note</c> bor, u yopishda
/// aynan shu maqsadda so'raladi. Bir xil natija beradigan ikkita tugma
/// esa HR ni har safar ma'nosiz tanlov qilishga majbur qilardi.
/// </para>
/// <para>
/// <b>NEGA <see cref="Closed"/> raqami 3 bo'lib qoldi va 2 bo'sh
/// turibdi.</b> Holat bazada BUTUN SON bo'lib yotadi. Yopilganini 2 ga
/// ko'chirish barcha mavjud qatorlarni qayta raqamlashni talab qilardi va
/// eski dump'lar («status = 3» — yopilgan) jimgina noto'g'ri o'qilardi.
/// Bo'sh raqam — arzon narx; qayta raqamlash esa ma'lumot yo'qotish
/// xavfi. 2 raqami ATAYLAB abadiy band deb hisoblanadi.
/// </para>
/// </remarks>
public enum VacancyStatus
{
    /// <summary>Ochiq — nomzod qabul qilinadi.</summary>
    Open = 1,

    /// <summary>
    /// Yopilgan — vakansiya yangi nomzod qabul qilmaydi.
    /// </summary>
    /// <remarks>
    /// Yopilish sabablari har xil bo'lishi mumkin («o'rin to'ldi»,
    /// «byudjet kutilyapti», «kerak emas ekan») va ular
    /// <c>Vacancy.Note</c> ga yoziladi. Holat ularni ajratmaydi: tizim
    /// uchun uchalasi ham bir narsani anglatadi — bu o'ringa endi nomzod
    /// kiritilmaydi. Fikrdan qaytish normal hol, shu sababli
    /// <c>Vacancy.Reopen</c> hech qanday shartsiz ishlaydi.
    /// </remarks>
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
    /// Holat ikkitaga qisqargach bu xossa deyarli tavtologiyaga aylandi,
    /// lekin ATAYLAB qoldirildi: chaqiruvchi joylar «ochiqmi» deb emas,
    /// «nomzod qabul qiladimi» deb so'rashi kerak. Ikkovi bugun bir xil,
    /// ertaga esa (masalan «o'rinlar to'ldi» avtomatik qoidasi qo'shilsa)
    /// farq qilishi mumkin — va o'shanda tuzatiladigan joy BITTA bo'ladi.
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
    /// (<see cref="Close"/> / <see cref="Reopen"/>).
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

    /// <summary>Yopilgan vakansiyani qayta ochadi.</summary>
    /// <remarks>
    /// <para>
    /// <b>Qayta ochish ATAYLAB mumkin va bu asosiy oqim, istisno emas.</b>
    /// O'rin to'ldi deb yopilgan vakansiyaga olingan odam bir haftada ketib
    /// qolishi Bonvi'da odatiy hol («91.8 Стажировкадан кетганлар» — 231
    /// yozuv). Bunda yangi vakansiya ochish butun nomzodlar tarixini ikkiga
    /// bo'lib yuborardi. Ilgari yopish oynasida «qayta ochish mumkin emas»
    /// degan yozuv turardi — u YOLG'ON edi: bu metod hech qachon hech
    /// qanday shart qo'ymagan.
    /// </para>
    /// <para>
    /// <b>NEGA «allaqachon ochiq» xatosi YO'Q</b> (<see cref="Close"/> da
    /// esa «allaqachon yopiq» bor). Ikkalasi bir xil emas: yopish IZOH va
    /// SANA yozadi, ya'ni ikkinchi yopish mavjud faktni jimgina qayta
    /// yozardi va buni to'sish kerak. Qayta ochish esa hech narsa
    /// yozmaydi — u faqat holatni tiklaydi. Ochiq vakansiyani qayta ochish
    /// hech narsani buzmaydi, ya'ni bu yerda 409 faqat ikki oynada ochiq
    /// turgan bir xil ro'yxat haqida xabar berardi.
    /// </para>
    /// <para>
    /// <see cref="ClosedAt"/> tozalanadi: vakansiya yana ochiq, ya'ni
    /// «qachon yopilgan» degan savolning javobi yo'q.
    /// </para>
    /// </remarks>
    public Result Reopen()
    {
        Status = VacancyStatus.Open;
        ClosedAt = null;

        return Result.Success();
    }

    /// <summary>Vakansiyani yopadi.</summary>
    /// <remarks>
    /// Yopish — QAYTARILADIGAN amal (<see cref="Reopen"/>). Nomzodlar,
    /// ularning bosqichlari va takliflari tegilmaydi: yopish ular haqida
    /// emas, faqat «bu o'ringa endi yangi odam kiritilmaydi» degani.
    /// </remarks>
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
