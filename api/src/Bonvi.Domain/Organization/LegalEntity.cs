using Bonvi.Domain.Common;

namespace Bonvi.Domain.Organization;

/// <summary>
/// Yuridik shaxs — xodim rasman qaysi firmada ishlaydi.
/// </summary>
/// <remarks>
/// <para>
/// Excel'da bu ustun «Мехнат дафтарчаси» deb nomlangan va u aslida
/// yuridik shaxsni bildiradi: VIKOR, Rispetto, Velo union, Vicor metan
/// MChJ, Kokand Bicycles, BONVI plus, MBF, Next phone, Pattern.
/// </para>
/// <para>
/// Bonvi — holding, shu sababli bitta xodim bir vaqtda ikki firmada
/// ishlashi mumkin: Excel'da «VIKOR + Вело унион 0,5 ставка ўриндош»
/// kabi yozuvlar bor. Aynan shu sababli <c>Employee</c> va
/// <c>Employment</c> alohida.
/// </para>
/// </remarks>
public sealed class LegalEntity : ReferenceEntity
{
    private LegalEntity(Guid id, string name) : base(id, name) { }

    private LegalEntity() { }

    /// <summary>Soliq to'lovchi identifikatsion raqami (STIR).</summary>
    public string? TaxId { get; private set; }

    /// <summary>To'liq rasmiy nomi (shartnoma va buyruqlar uchun).</summary>
    public string? FullName { get; private set; }

    public static Result<LegalEntity> Create(string? name)
    {
        var normalized = Normalize(name);
        if (normalized is null)
        {
            return Error.Validation("yuridik_shaxs.nom_bosh", "Yuridik shaxs nomi kiritilishi shart.");
        }

        return new LegalEntity(NewId(), normalized);
    }

    public Result SetTaxId(string? taxId)
    {
        if (string.IsNullOrWhiteSpace(taxId))
        {
            TaxId = null;
            return Result.Success();
        }

        var digits = new string([.. taxId.Where(char.IsAsciiDigit)]);
        if (digits.Length != 9)
        {
            return Error.Validation("yuridik_shaxs.stir", "STIR 9 xonadan iborat bo'lishi kerak.");
        }

        TaxId = digits;
        return Result.Success();
    }

    public void SetFullName(string? fullName) => FullName = Normalize(fullName);
}
