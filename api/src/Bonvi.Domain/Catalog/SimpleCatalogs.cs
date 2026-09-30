using Bonvi.Domain.Common;

namespace Bonvi.Domain.Catalog;

/// <summary>
/// Ishdan ketish sababi.
/// </summary>
/// <remarks>
/// Excel'da 232 xil qiymat bor edi — «Ўз хохиши билан», «Ўз ихтиёри билан»,
/// «Иш ёқмади», «иш ёқмади» kabi. Tozalangandan keyin ~15 ta qoladi.
/// Bu eng muhim ma'lumotnoma: kadrlar oqimi hisoboti aynan shu bo'yicha quriladi.
/// </remarks>
public sealed class TerminationReason : ReferenceEntity
{
    private TerminationReason(Guid id, string name) : base(id, name) { }

    private TerminationReason() { }

    /// <summary>
    /// Xodimning tashabbusi bilanmi. Kadrlar oqimi hisobotida
    /// ixtiyoriy va majburiy ketishni ajratish uchun.
    /// </summary>
    public bool IsVoluntary { get; private set; } = true;

    /// <summary>
    /// Shu sabab bilan ketgan xodimni qayta ishga olish mumkinmi.
    /// «Хайдалди» — mumkin emas, «Ўқишга кетди» — mumkin.
    /// </summary>
    public bool IsRehireable { get; private set; } = true;

    public static Result<TerminationReason> Create(string? name)
    {
        var normalized = Normalize(name);
        return normalized is null
            ? Error.Validation("sabab.nom_bosh", "Sabab nomi kiritilishi shart.")
            : new TerminationReason(NewId(), normalized);
    }

    public void SetVoluntary(bool value) => IsVoluntary = value;

    public void SetRehireable(bool value) => IsRehireable = value;
}

/// <summary>Yo'qlik turi: «Ishxona ishi bilan», «Kasallik», «Shaxsiy sabab».</summary>
/// <remarks>
/// <para>
/// Davomat so'rovlari shu ro'yxatdan tanlanadi (SPEC-so'rovlar §B1). Ro'yxat
/// KODDA emas, ma'lumotnomada: yangi tur kerak bo'lganda HR uni o'zi
/// qo'shadi va dasturchi ham, deploy ham kutilmaydi (egasining qarori,
/// 2026-09-19).
/// </para>
/// <para>
/// «Ishlangan vaqtga kiradimi» degan bayroq ATAYLAB yo'q: tasdiqlangan
/// so'rov kunni «Sababli» qiladi, ishlangan daqiqa QO'SHMAYDI (§B3). Bayroq
/// qo'shilsa, tizim HR kiritmagan raqamni o'zi yasay boshlardi.
/// </para>
/// </remarks>
public sealed class AbsenceType : ReferenceEntity
{
    private AbsenceType(Guid id, string name) : base(id, name) { }

    private AbsenceType() { }

    public static Result<AbsenceType> Create(string? name)
    {
        var normalized = Normalize(name);
        return normalized is null
            ? Error.Validation("yoqlik.nom_bosh", "Yo'qlik turining nomi kiritilishi shart.")
            : new AbsenceType(NewId(), normalized);
    }
}

/// <summary>Rag'bat turi: tashakkurnoma, mukofot pul, sovg'a.</summary>
public sealed class AwardType : ReferenceEntity
{
    private AwardType(Guid id, string name) : base(id, name) { }

    private AwardType() { }

    /// <summary>Pul bilan bog'liqmi — shundan xarajat hisoboti chiqadi.</summary>
    public bool HasAmount { get; private set; }

    public static Result<AwardType> Create(string? name)
    {
        var normalized = Normalize(name);
        return normalized is null
            ? Error.Validation("ragbat.nom_bosh", "Rag'bat turi nomi kiritilishi shart.")
            : new AwardType(NewId(), normalized);
    }

    public void SetHasAmount(bool value) => HasAmount = value;
}

/// <summary>Jarima turi: ogohlantirish, hayfsan, jarima puli.</summary>
/// <remarks>
/// Excel'da bu bo'lim umuman yo'q edi — foydalanuvchi so'ragan yangi imkoniyat.
/// </remarks>
public sealed class PenaltyType : ReferenceEntity
{
    private PenaltyType(Guid id, string name) : base(id, name) { }

    private PenaltyType() { }

    /// <summary>Pul jarimasimi.</summary>
    public bool HasAmount { get; private set; }

    /// <summary>
    /// Og'irlik darajasi: 1 — ogohlantirish, 2 — hayfsan, 3 — oxirgi ogohlantirish.
    /// Takroriy buzilishlarni kuzatish uchun.
    /// </summary>
    public int Severity { get; private set; } = 1;

    public static Result<PenaltyType> Create(string? name)
    {
        var normalized = Normalize(name);
        return normalized is null
            ? Error.Validation("jarima.nom_bosh", "Jarima turi nomi kiritilishi shart.")
            : new PenaltyType(NewId(), normalized);
    }

    public void SetHasAmount(bool value) => HasAmount = value;

    public Result SetSeverity(int severity)
    {
        if (severity is < 1 or > 3)
        {
            return Error.Validation("jarima.oglirlik", "Og'irlik darajasi 1 dan 3 gacha bo'lishi kerak.");
        }

        Severity = severity;
        return Result.Success();
    }
}

/// <summary>Hujjat turi: mehnat shartnomasi, buyruq, anketa, pasport nusxasi.</summary>
public sealed class DocumentType : ReferenceEntity
{
    private DocumentType(Guid id, string name) : base(id, name) { }

    private DocumentType() { }

    /// <summary>Har bir xodimda bo'lishi shartmi — yetishmasa ogohlantiriladi.</summary>
    public bool IsRequired { get; private set; }

    public static Result<DocumentType> Create(string? name)
    {
        var normalized = Normalize(name);
        return normalized is null
            ? Error.Validation("hujjat.nom_bosh", "Hujjat turi nomi kiritilishi shart.")
            : new DocumentType(NewId(), normalized);
    }

    public void SetRequired(bool value) => IsRequired = value;
}
