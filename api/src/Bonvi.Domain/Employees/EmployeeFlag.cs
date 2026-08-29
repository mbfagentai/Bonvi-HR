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
    /// Belgini yangisi bilan almashtirilgani uchun tugatadi.
    /// </summary>
    /// <remarks>
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
    /// <param name="at">Almashtirish kuni — belgi shu kunning oxirida tugaydi.</param>
    /// <param name="note">Nima uchun almashtirilgani.</param>
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
