using Bonvi.Domain.Common;

namespace Bonvi.Domain.Employees;

/// <summary>Xodimga qo'yilgan belgi turi.</summary>
public enum EmployeeFlagType
{
    /// <summary>Rezerv — hozir olinmadi, lekin kerak bo'lsa chaqirsa bo'ladi (Excel: «91.9»).</summary>
    Reserve = 1,

    /// <summary>Qora ro'yxat — qayta olinmaydi (Excel: «91.10 Чёрный список»).</summary>
    Blacklist = 2,

    /// <summary>Nazoratda — bir muddat kuzatiladi (Excel: «Назоратга олинган ходимлар»).</summary>
    Monitoring = 3,

    /// <summary>Ko'proq yuklama ko'taradi (Excel: «91.12 Вазифа берса бўладиган»).</summary>
    Capable = 4,
}

/// <summary>Belgi xodim yo'lining QAYSI nuqtasida qo'yiladi.</summary>
/// <remarks>
/// <para>
/// Foydalanuvchining aniq qarori: belgilar ikki guruhga bo'linadi va
/// ular ARALASHMAYDI. Sabab — belgining o'zi savolga javob berishi
/// kerak, savol esa har bosqichda boshqacha:
/// </para>
/// <list type="bullet">
///   <item><b>Ishlab turganda</b> — «bu odam bilan HOZIR nima qilish
///   kerak?»: kuzatib turish (<see cref="EmployeeFlagType.Monitoring"/>)
///   yoki ko'proq yuklama berish
///   (<see cref="EmployeeFlagType.Capable"/>).</item>
///   <item><b>Ketayotganda</b> — «bu odamni KEYIN qaytarib olamizmi?»:
///   chaqirsa bo'ladi (<see cref="EmployeeFlagType.Reserve"/>) yoki
///   umuman olinmaydi (<see cref="EmployeeFlagType.Blacklist"/>).</item>
/// </list>
/// <para>
/// Aralashgan holat ma'nosiz: ishlab turgan odamni «rezerv» deb belgilash
/// — «u bizda ishlayapti, lekin kerak bo'lsa chaqiramiz» degani, ketgan
/// odamni «nazoratda» deb belgilash esa hech kim bajarmaydigan vazifa.
/// Excel'da ikkala hol ham bor edi va ro'yxatlar shu sababdan
/// ishlatilmay qolgan.
/// </para>
/// </remarks>
public enum EmployeeFlagStage
{
    /// <summary>Ishlab turgan xodimga qo'yiladi.</summary>
    Employed = 1,

    /// <summary>Ishdan ketishda tanlanadi.</summary>
    Departure = 2,
}

/// <summary>Belgi turining bosqichi — bitta manba.</summary>
/// <remarks>
/// <para>
/// Bu bilim DOMENDA turadi, use-case'da emas: u belgi turining o'z
/// xossasi va uchta joyda kerak bo'ladi — belgi qo'yishda
/// (<c>RaiseFlagCommandHandler</c>), ishdan bo'shatishda va ishga
/// qabulda. Uch joyda uchta <c>switch</c> yozilsa, yangi belgi turi
/// qo'shilganda biri unutilardi.
/// </para>
/// <para>
/// <c>switch</c> ATAYLAB <c>default</c> siz emas: yangi tur qo'shilganda
/// kompilyator ogohlantirmaydi, shu sababli har bir tur ochiq
/// sanab o'tilgan va noma'lum qiymat xato bilan tugaydi.
/// </para>
/// </remarks>
public static class EmployeeFlagStages
{
    public static EmployeeFlagStage Of(EmployeeFlagType type) => type switch
    {
        EmployeeFlagType.Monitoring => EmployeeFlagStage.Employed,
        EmployeeFlagType.Capable => EmployeeFlagStage.Employed,
        EmployeeFlagType.Reserve => EmployeeFlagStage.Departure,
        EmployeeFlagType.Blacklist => EmployeeFlagStage.Departure,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Noma'lum belgi turi."),
    };

    /// <summary>Bu belgi ishlab turgan xodimga qo'yiladimi.</summary>
    public static bool IsForEmployed(EmployeeFlagType type) => Of(type) == EmployeeFlagStage.Employed;

    /// <summary>Bu belgi ishdan ketishda tanlanadimi.</summary>
    public static bool IsForDeparture(EmployeeFlagType type) => Of(type) == EmployeeFlagStage.Departure;
}

/// <summary>
/// Xodimga qo'yilgan belgi.
/// </summary>
/// <remarks>
/// <para>
/// Excel'da bular to'rt xil varaq edi, lekin mohiyati bir xil: xodimga
/// vaqtinchalik yoki doimiy yorliq qo'yiladi. Shu sababli bitta jadval.
/// </para>
/// <para>
/// <b>Eng muhim ishlatilishi</b> — HR aniq so'ragan qoida: ishdan
/// bo'shatayotganda yoki qayta ishga olayotganda tizim qora ro'yxat va
/// nazoratni tekshirib, ogohlantirishi kerak.
/// </para>
/// <para>
/// <b>Bir vaqtda ATIGI BITTA kuchdagi belgi.</b> Foydalanuvchi qarori,
/// istisnosiz: qora ro'yxatdagi odam ayni paytda rezervda ham bo'la
/// olmaydi — bu ikki yorliq bir-birini inkor qiladi va ikkalasi birga
/// turganda ro'yxat ham, kartadagi nishonlar ham ma'nosini yo'qotadi
/// («chaqirsa bo'ladigan, lekin qayta olinmaydigan odam» degan javob
/// hech qanday qarorga yaramaydi). Qoidaning O'ZI bu yerda emas,
/// use-case qatlamida (<c>RaiseFlagCommandHandler</c>): u BIR XODIMNING
/// boshqa belgilarini bilishni talab qiladi, agregat esa faqat o'zini
/// ko'radi — bunday qoidani konstruktordan tekshirib bo'lmaydi. Bu yerda
/// esa uning ikkita quroli turadi: <see cref="IsInForceAfter"/> va
/// <see cref="Supersede"/>.
/// </para>
/// </remarks>
public sealed class EmployeeFlag : AggregateRoot, IAuditable
{
    private EmployeeFlag(Guid id, Guid employeeId, EmployeeFlagType type, DateOnly startDate)
        : base(id)
    {
        EmployeeId = employeeId;
        Type = type;
        StartDate = startDate;
    }

    /// <summary>EF Core uchun.</summary>
    private EmployeeFlag() { }

    public Guid EmployeeId { get; private set; }

    public EmployeeFlagType Type { get; private set; }

    public DateOnly StartDate { get; private set; }

    /// <summary>Tugash sanasi. <c>null</c> — muddatsiz.</summary>
    public DateOnly? EndDate { get; private set; }

    /// <summary>Nima uchun qo'yilgan. Qora ro'yxat uchun majburiy.</summary>
    public string? Reason { get; private set; }

    /// <summary>Mas'ul shaxs (xodim).</summary>
    public Guid? ResponsibleEmployeeId { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static Result<EmployeeFlag> Raise(
        Guid employeeId,
        EmployeeFlagType type,
        DateOnly startDate,
        string? reason = null,
        DateOnly? endDate = null,
        Guid? responsibleEmployeeId = null)
    {
        var text = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        // Qora ro'yxat — jiddiy qaror, sababsiz qo'yilmaydi.
        // Excel'da sababsiz yozuvlar bor edi va keyin hech kim nima
        // uchun ekanini bilmasdi.
        if (type == EmployeeFlagType.Blacklist && text is null)
        {
            return Error.Validation("belgi.sabab_bosh", "Qora ro'yxatga qo'yish sababi ko'rsatilishi shart.");
        }

        if (endDate is { } end && end < startDate)
        {
            return Error.Validation("belgi.sana", "Tugash sanasi boshlanish sanasidan oldin bo'lishi mumkin emas.");
        }

        return new EmployeeFlag(NewId(), employeeId, type, startDate)
        {
            Reason = text,
            EndDate = endDate,
            ResponsibleEmployeeId = responsibleEmployeeId,
        };
    }

    /// <summary>Belgi shu sanada kuchdami.</summary>
    public bool IsActiveOn(DateOnly date)
        => StartDate <= date && (EndDate is null || EndDate >= date);

    /// <summary>
    /// Belgi shu sanadan KEYIN ham kuchda qoladimi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// «Bitta kuchdagi belgi» qoidasining tekshiruvi aynan shu — va u
    /// <see cref="IsActiveOn"/> dan ATAYLAB farq qiladi. Sabab: tizimda
    /// belgini «bugun yopish» degani «bugun kun oxirigacha amal qiladi»
    /// (<see cref="Close"/> <c>EndDate</c> ni o'sha kunga qo'yadi).
    /// Demak bugun tugaydigan belgi ERTAGA yangisi bilan ziddiyatga
    /// tushmaydi va uni almashtirish uchun tasdiq so'rashning ma'nosi
    /// yo'q — u o'zi ketyapti.
    /// </para>
    /// <para>
    /// Muddatli belgilar (masalan uch oylik nazorat) ham shu tekshiruvga
    /// tushadi: ularning <c>EndDate</c> si bor, lekin u KELAJAKDA — ya'ni
    /// belgi hozir kuchda va yangisiga yo'l bo'shatishi kerak.
    /// </para>
    /// </remarks>
    public bool IsInForceAfter(DateOnly date)
        => StartDate <= date && (EndDate is null || EndDate > date);

    /// <summary>
    /// Belgini muddatidan OLDIN tugatadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Ikki chaqiruvchi, bitta mexanika.</b> Belgi ikki sababdan
    /// muddatidan oldin tugashi mumkin: yangisi bilan ALMASHTIRILGANDA
    /// (<c>RaiseFlagCommandHandler</c>) va xodim ISHDAN KETGANDA
    /// (<c>TerminateEmploymentCommandHandler</c> — ketgan odamni hech kim
    /// kuzatmaydi, ya'ni «nazoratda» belgisi ma'nosini yo'qotadi).
    /// Ikkalasida ham qilinadigan ish bir xil — muddatni qisqartirish va
    /// sababni yozib qo'yish; farq faqat <paramref name="note"/> matnida,
    /// shu sababli ikkita metod emas, bitta.
    /// </para>
    /// <para>
    /// <b>Nega <see cref="Close"/> yetmaydi.</b> <c>Close</c> muddatsiz
    /// belgini yopish uchun va u allaqachon <c>EndDate</c> si bor
    /// yozuvni <c>belgi.yopilgan</c> bilan rad etadi — bu to'g'ri qoida:
    /// yopilgan belgini ikkinchi marta yopish xato. Almashtirishda esa
    /// vaziyat boshqacha: muddati KELAJAKDA tugaydigan belgi ham yo'l
    /// bo'shatishi kerak, ya'ni uning muddati QISQARADI, u qaytadan
    /// yopilmaydi. Ikkalasini bitta metodga siqish <c>Close</c> ning
    /// himoyasini olib tashlashni talab qilardi.
    /// </para>
    /// <para>
    /// Yozuv O'CHIRILMAYDI — tarix qoladi: «bu odam falon sanadan falon
    /// sanagacha nazoratda edi» degan fakt yangi belgi qo'yilgani uchun
    /// yolg'onga aylanmaydi. Sabab matni eskisiga QO'SHILADI, ustiga
    /// yozilmaydi.
    /// </para>
    /// </remarks>
    /// <param name="at">Tugatish kuni — belgi shu kunning oxirida tugaydi.</param>
    /// <param name="note">Nima uchun muddatidan oldin tugatilgani.</param>
    public Result Supersede(DateOnly at, string? note = null)
    {
        if (at < StartDate)
        {
            return Error.Validation(
                "belgi.sana",
                "Almashtirish sanasi belgining boshlanish sanasidan oldin bo'lishi mumkin emas.");
        }

        // Muddat faqat QISQARADI. Uzaytirish almashtirishning vazifasi emas
        // va u eski belgini yangisidan uzoqroq yashab qolishiga olib kelardi.
        if (EndDate is null || EndDate > at)
        {
            EndDate = at;
        }

        if (!string.IsNullOrWhiteSpace(note))
        {
            Reason = string.IsNullOrWhiteSpace(Reason) ? note.Trim() : $"{Reason}; {note.Trim()}";
        }

        return Result.Success();
    }

    public Result Close(DateOnly at, string? note = null)
    {
        if (EndDate is not null)
        {
            return Error.Conflict("belgi.yopilgan", "Bu belgi allaqachon yopilgan.");
        }

        if (at < StartDate)
        {
            return Error.Validation("belgi.sana", "Yopish sanasi boshlanish sanasidan oldin bo'lishi mumkin emas.");
        }

        EndDate = at;

        if (!string.IsNullOrWhiteSpace(note))
        {
            Reason = string.IsNullOrWhiteSpace(Reason) ? note.Trim() : $"{Reason}; {note.Trim()}";
        }

        return Result.Success();
    }
}
