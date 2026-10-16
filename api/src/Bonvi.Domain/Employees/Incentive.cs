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
/// shaklga ega: tur, sana, (ehtimol) summa, sabab. Farq faqat
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
/// <b>Buyruq YO'Q.</b> Egasining qarori (2026-10-16): rag'bat ham, jarima
/// ham buyruq bilan rasmiylashtirilmaydi — buyruq raqami va sanasi
/// maydonlari olib tashlandi (bazadagi ustunlar ham, migratsiya
/// <c>RagbatJarimaBuyruqsiz</c>). Ilgari ular Excel «91.14» dan kelgan,
/// lekin prod'da birorta yozuvda to'ldirilmagan edi.
/// </para>
/// <para>
/// <b>Jarima — summa YOKI foiz.</b> Pulli jarima qat'iy summa
/// («300 000 so'm») yoki foiz («4 %») bilan beriladi — ikkalasi birga
/// emas. Foiz nimadan olinishi (oylik maosh, mukofot) TURNING o'zidan
/// anglashiladi («Жарима», «Мукофотдан маҳрум қилиш») va bu yerda
/// hisoblanmaydi: tizim ish haqini hisoblamaydi, faqat qarorni qayd etadi.
/// Rag'bat faqat summa bilan beriladi.
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

    /// <summary>Foizning eng katta qiymati.</summary>
    public const decimal MaxPercent = 100m;

    /// <summary>Foizdagi kasr xonalar soni — bazada <c>numeric(5,2)</c>.</summary>
    public const int PercentScale = 2;

    /// <summary>
    /// Summa — faqat tur pul bilan bog'liq bo'lsa to'ldiriladi. Jarimada
    /// uning o'rnida <see cref="Percent"/> bo'lishi mumkin, ikkalasi birga emas.
    /// </summary>
    public Money? Amount { get; private set; }

    /// <summary>
    /// Foiz (0 dan katta, ko'pi bilan 100, ikki kasr xonagacha) — faqat
    /// pulli JARIMADA, <see cref="Amount"/> o'rniga.
    /// </summary>
    public decimal? Percent { get; private set; }

    /// <summary>Sovg'a tavsifi (faqat rag'bat uchun ma'noli).</summary>
    public string? GiftDescription { get; private set; }

    /// <summary>Sabab. Jarima uchun MAJBURIY.</summary>
    public string? Reason { get; private set; }

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
        decimal? percent = null)
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

        var amountCheck = CheckAmount(kind, typeHasAmount, amount, percent);
        if (amountCheck.IsFailure)
        {
            return amountCheck.Error;
        }

        return new Incentive(NewId(), employeeId, kind, typeId, date)
        {
            Amount = amount,
            Percent = percent,
            GiftDescription = trimmedGift,
            Reason = trimmedReason,
        };
    }

    /// <summary>
    /// Kiritish xatosini tuzatadi: tur, sana, summa yoki foiz, sovg'a, sabab.
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
        decimal? percent = null)
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

        var amountCheck = CheckAmount(Kind, typeHasAmount, amount, percent);
        if (amountCheck.IsFailure)
        {
            return amountCheck.Error;
        }

        TypeId = typeId;
        Date = date;
        Amount = amount;
        Percent = percent;
        GiftDescription = trimmedGift;
        Reason = trimmedReason;

        return Result.Success();
    }

    /// <summary>
    /// Pul qismi: tur pulli bo'lsa summa YOKI foiz (foiz faqat jarimada),
    /// pulsiz bo'lsa ikkalasi ham yo'q.
    /// </summary>
    private static Result CheckAmount(IncentiveKind kind, bool typeHasAmount, Money? amount, decimal? percent)
    {
        if (percent is not null && kind != IncentiveKind.Penalty)
        {
            return Error.Validation(
                "ragbat_jarima.foiz_ragbatda",
                "Foiz faqat jarima uchun — rag'bat summa bilan beriladi.");
        }

        if (amount is not null && percent is not null)
        {
            return Error.Validation(
                "ragbat_jarima.summa_va_foiz",
                "Summa yoki foizdan faqat bittasi kiritiladi.");
        }

        if (percent is { } value && !IsValidPercent(value))
        {
            return Error.Validation(
                "ragbat_jarima.foiz_oraligi",
                "Foiz 0 dan katta va 100 dan oshmasligi kerak (ko'pi bilan ikki kasr xona).");
        }

        var hasValue = amount is not null || percent is not null;

        if (typeHasAmount && !hasValue)
        {
            return Error.Validation(
                "ragbat_jarima.summa_kerak",
                "Bu tur uchun summa ko'rsatilishi shart (jarimada — summa yoki foiz).");
        }

        if (!typeHasAmount && hasValue)
        {
            return Error.Validation(
                "ragbat_jarima.summa_kerak_emas",
                "Bu tur summasiz — summa yoki foiz kiritilmasligi kerak.");
        }

        return Result.Success();
    }

    // `decimal.Scale` «4.50» ni 2 deb sanaydi — ortiqcha nollar kasr xona emas.
    private static bool IsValidPercent(decimal value)
        => value > 0m
            && value <= MaxPercent
            && decimal.Round(value, PercentScale) == value;

    private static string? Trim(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
