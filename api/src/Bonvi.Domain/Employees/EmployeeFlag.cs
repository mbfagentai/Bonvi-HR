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
