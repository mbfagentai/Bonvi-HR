using Bonvi.Domain.Common;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Domain.Employees;

/// <summary>Rag'bat yoki jarima.</summary>
public enum IncentiveKind
{
    /// <summary>Rag'bat (Excel: «91.14 Тақдирланган ходимлар»).</summary>
    Award = 1,

    /// <summary>Jarima — Excel'da umuman yo'q, HR so'ragan yangi imkoniyat.</summary>
    Penalty = 2,
}

/// <summary>
/// Xodimga qo'llanilgan rag'bat yoki jarima — <b>bitta</b> jadval.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega bitta agregat, ikkita emas.</b> Rag'bat va jarima bir xil
/// shaklga ega: tur, sana, (ehtimol) summa, sabab, buyruq. Farq faqat
/// <see cref="Kind"/> da va qaysi ma'lumotnomadan (<c>AwardType</c> /
/// <c>PenaltyType</c>) tur olinishida — xuddi <c>EmployeeFlag</c>ning
/// to'rt turi bitta jadvalga yig'ilgani kabi.
/// </para>
/// <para>
/// <b>Nega xodimga, mehnat munosabatiga emas bog'langan.</b> Reja
/// (4-bo'lim) buni <c>Employee</c> ning bolasi deb belgilagan: rag'bat
/// yoki jarima SHAXSGA tegishli, u boshqa firmaga o'tsa yoki ishdan
/// ketib qaytib kelsa ham tarix yo'qolmasligi kerak.
/// </para>
/// <para>
/// <b>Jarima sababsiz bo'lmaydi.</b> Mehnat kodeksi 181-moddasi
/// intizomiy jazoning sababi hujjatda ko'rsatilishini talab qiladi —
/// bu <c>EmployeeFlag.Raise</c>dagi qora ro'yxat qoidasi bilan bir xil
/// mantiq.
/// </para>
/// <para>
/// <b>Summa — turga bog'liq.</b> <c>AwardType.HasAmount</c> /
/// <c>PenaltyType.HasAmount</c> allaqachon bor edi (S2 da qo'shilgan),
/// lekin hech qayerda ishlatilmagan. Shu yerda haqiqiy tekshiruvga
/// aylanadi: pul bilan bog'liq tur summasiz, pulsiz tur summa bilan
/// yaratilmaydi — aks holda «огохлантириш» kabi pulsiz turga tasodifan
/// summa yozilib qolishi yoki «пул мукофоти»da summa unutilib qolishi
/// mumkin edi.
/// </para>
/// </remarks>
public sealed class Incentive : AggregateRoot, IAuditable, ISoftDeletable
{
    private Incentive(Guid id, Guid employeeId, IncentiveKind kind, Guid typeId, DateOnly date)
        : base(id)
    {
        EmployeeId = employeeId;
        Kind = kind;
        TypeId = typeId;
        Date = date;
    }

    /// <summary>EF Core uchun.</summary>
    private Incentive() { }

    public Guid EmployeeId { get; private set; }

    public IncentiveKind Kind { get; private set; }

    /// <summary>
    /// Tur identifikatori — <see cref="Kind"/>ga qarab <c>AwardType</c>
    /// yoki <c>PenaltyType</c> yozuviga ishora qiladi.
    /// </summary>
    public Guid TypeId { get; private set; }

    /// <summary>Rag'bat/jarima sanasi.</summary>
    public DateOnly Date { get; private set; }

    /// <summary>Summa — faqat tur pul bilan bog'liq bo'lsa to'ldiriladi.</summary>
    public Money? Amount { get; private set; }

    /// <summary>Sovg'a tavsifi (faqat rag'bat uchun ma'noli).</summary>
    public string? GiftDescription { get; private set; }

    /// <summary>Sabab. Jarima uchun MAJBURIY.</summary>
    public string? Reason { get; private set; }

    /// <summary>Buyruq raqami.</summary>
    public string? OrderNo { get; private set; }

    public DateOnly? OrderDate { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    public static Result<Incentive> Grant(
        Guid employeeId,
        IncentiveKind kind,
        Guid typeId,
        DateOnly date,
        bool typeHasAmount,
        Money? amount = null,
        string? giftDescription = null,
        string? reason = null,
        string? orderNo = null,
        DateOnly? orderDate = null)
    {
        if (typeId == Guid.Empty)
        {
            return Error.Validation("ragbat_jarima.tur_bosh", "Tur ko'rsatilishi shart.");
        }

        var trimmedReason = Trim(reason);

        // Excel'da sababsiz jarima yozuvlari bo'lganida bir yildan keyin
        // hech kim nima uchun ekanini bilmasdi — xuddi qora ro'yxat kabi.
        if (kind == IncentiveKind.Penalty && trimmedReason is null)
        {
            return Error.Validation("ragbat_jarima.sabab_bosh", "Jarima sababi ko'rsatilishi shart.");
        }

        var trimmedGift = Trim(giftDescription);
        if (kind == IncentiveKind.Penalty && trimmedGift is not null)
        {
            return Error.Validation(
                "ragbat_jarima.sovga_jarimada",
                "Jarima yozuvida sovg'a maydoni bo'lishi mumkin emas.");
        }

        var amountCheck = CheckAmount(typeHasAmount, amount);
        if (amountCheck.IsFailure)
        {
            return amountCheck.Error;
        }

        return new Incentive(NewId(), employeeId, kind, typeId, date)
        {
            Amount = amount,
            GiftDescription = trimmedGift,
            Reason = trimmedReason,
            OrderNo = Trim(orderNo),
            OrderDate = orderDate,
        };
    }

    /// <summary>
    /// Kiritish xatosini tuzatadi: tur, sana, summa, sovg'a, sabab, buyruq.
    /// </summary>
    /// <remarks>
    /// <see cref="Kind"/> va <see cref="EmployeeId"/> o'zgarmaydi — agar
    /// rag'bat noto'g'ri xodimga yoki noto'g'ri turga (jarima o'rniga
    /// rag'bat) yozilgan bo'lsa, bu tuzatish emas, balki boshqa fakt: yozuv
    /// o'chirilib, yangisi kiritiladi.
    /// </remarks>
    public Result Update(
        Guid typeId,
        DateOnly date,
        bool typeHasAmount,
        Money? amount = null,
        string? giftDescription = null,
        string? reason = null,
        string? orderNo = null,
        DateOnly? orderDate = null)
    {
        if (typeId == Guid.Empty)
        {
            return Error.Validation("ragbat_jarima.tur_bosh", "Tur ko'rsatilishi shart.");
        }

        var trimmedReason = Trim(reason);
        if (Kind == IncentiveKind.Penalty && trimmedReason is null)
        {
            return Error.Validation("ragbat_jarima.sabab_bosh", "Jarima sababi ko'rsatilishi shart.");
        }

        var trimmedGift = Trim(giftDescription);
        if (Kind == IncentiveKind.Penalty && trimmedGift is not null)
        {
            return Error.Validation(
                "ragbat_jarima.sovga_jarimada",
                "Jarima yozuvida sovg'a maydoni bo'lishi mumkin emas.");
        }

        var amountCheck = CheckAmount(typeHasAmount, amount);
        if (amountCheck.IsFailure)
        {
            return amountCheck.Error;
        }

        TypeId = typeId;
        Date = date;
        Amount = amount;
        GiftDescription = trimmedGift;
        Reason = trimmedReason;
        OrderNo = Trim(orderNo);
        OrderDate = orderDate;

        return Result.Success();
    }

    private static Result CheckAmount(bool typeHasAmount, Money? amount)
    {
        if (typeHasAmount && amount is null)
        {
            return Error.Validation("ragbat_jarima.summa_kerak", "Bu tur uchun summa ko'rsatilishi shart.");
        }

        if (!typeHasAmount && amount is not null)
        {
            return Error.Validation(
                "ragbat_jarima.summa_kerak_emas",
                "Bu tur summasiz — summa kiritilmasligi kerak.");
        }

        return Result.Success();
    }

    private static string? Trim(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
