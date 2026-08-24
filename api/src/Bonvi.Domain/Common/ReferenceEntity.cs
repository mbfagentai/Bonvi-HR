namespace Bonvi.Domain.Common;

/// <summary>
/// Oddiy ma'lumotnoma yozuvi uchun asos: nom va faollik.
/// </summary>
/// <remarks>
/// <para>
/// Bonvi'ning Excel'ida bu qiymatlar qo'lda yozilgan va shu sababli
/// tarqalib ketgan: 228 ta lavozim, 73 ta bo'lim, 232 ta ishdan ketish
/// sababi — ularning ko'pi bir narsaning turli yozilishi.
/// </para>
/// <para>
/// Ma'lumotnomaga o'tkazilgach, foydalanuvchi ro'yxatdan tanlaydi va
/// yangi variant paydo bo'lmaydi.
/// </para>
/// <para>
/// Yozuv o'chirilmaydi, <see cref="Deactivate"/> bilan yashiriladi:
/// eski buyruqlarda unga havola qolgan bo'lishi mumkin.
/// </para>
/// </remarks>
public abstract class ReferenceEntity : AggregateRoot, IAuditable, ISoftDeletable
{
    protected ReferenceEntity(Guid id, string name)
        : base(id)
    {
        Name = name;
        IsActive = true;
    }

    /// <summary>EF Core uchun.</summary>
    protected ReferenceEntity() => Name = string.Empty;

    /// <summary>
    /// Foydalanuvchiga ko'rinadigan nom — <b>yagona kalit</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ilgari yonida qo'lda yoziladigan <c>Code</c> ham turardi va uni
    /// yangi yozuv yaratayotgan HR dan so'rardi. Bu ortiqcha ish va
    /// xavf edi: bitta nomni ikki xil kod bilan yaratish mumkin bo'lib,
    /// keyin bir joyda birinchisi, boshqa joyda ikkinchisi ishlatilardi.
    /// </para>
    /// <para>
    /// Yozuvning haqiqiy o'ziga xosligi <see cref="Entity.Id"/> da —
    /// u baza tomonidan beriladi, hech qachon takrorlanmaydi va barcha
    /// bog'lanishlar aynan unga qaraydi. Nom esa ma'lumotnoma bo'yicha
    /// unikal (bazada <c>lower(name)</c> ustidagi indeks) — ya'ni odam
    /// uchun kalit ham tayyor.
    /// </para>
    /// </remarks>
    public string Name { get; private set; }


    /// <summary>
    /// Faol emas — yangi yozuvlarda tanlab bo'lmaydi, lekin
    /// eskilarida ko'rinib turadi.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>Ixtiyoriy izoh (masalan nima uchun faol emas).</summary>
    public string? Note { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    public Result Rename(string? name)
    {
        var normalized = Normalize(name);
        if (normalized is null)
        {
            return Error.Validation("malumotnoma.nom_bosh", "Nom kiritilishi shart.");
        }

        Name = normalized;
        return Result.Success();
    }


    public void SetNote(string? note) => Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

    public void Deactivate(string? reason = null)
    {
        IsActive = false;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            Note = reason.Trim();
        }
    }

    public void Activate() => IsActive = true;

    /// <summary>
    /// Nomni tozalaydi: chetdagi bo'shliqlar va ichidagi takroriy
    /// bo'shliqlar olib tashlanadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Excel'da «Маркетинг бўлими» va «Маркетинг  бўлими» ikki xil
    /// qiymat bo'lib yurgan — aynan shu sababli.
    /// </para>
    /// <para>
    /// Ajratgich sifatida <b>barcha</b> bo'shliq belgilari hisobga olinadi,
    /// faqat oddiy probel emas: Excel eksportida tabulyatsiya (<c>\t</c>) va
    /// uzilmaydigan bo'shliq (U+00A0) tez-tez uchraydi. Ular ko'zga
    /// oddiy bo'shliqdek ko'rinadi, lekin boshqa belgi — natijada bir xil
    /// nom ikki xil yozuv bo'lib qoladi.
    /// </para>
    /// </remarks>
    protected static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length == 0 ? null : string.Join(' ', parts);
    }
}
