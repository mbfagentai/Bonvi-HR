using Bonvi.Domain.Common;

namespace Bonvi.Domain.Employees;

/// <summary>
/// Xodimga biriktirilgan fayl: shartnoma, buyruq, anketa, pasport skani va h.k.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega alohida agregat, <c>Employee</c>ning ichiga emas.</b> Bitta
/// xodimda o'nlab hujjat bo'lishi mumkin (har yangi buyruq, har yangi
/// shartnoma — yangi yozuv) — bu <see cref="Incentive"/> kabi TARIX,
/// <see cref="EmployeeCompetency"/> kabi bitta almashtiriladigan profil
/// emas. Shu sababli o'z repozitoriysi va o'z hayot sikli bor.
/// </para>
/// <para>
/// <b>Fayl mazmuni bu yerda emas.</b> <see cref="FileKey"/> —
/// <c>IFileStorage</c> dagi kalit, xuddi xodim rasmidagi kabi: domen
/// faylning o'zini bilmaydi, faqat unga ishora qiladi. Haqiqiy fayl turini
/// tekshirish (kengaytmaga emas, MAZMUNGA qarab) Application qatlamida,
/// diskka yozishdan OLDIN bajariladi — bu yerga faqat tasdiqlangan
/// <see cref="ContentType"/> kiradi.
/// </para>
/// <para>
/// <b>Yumshoq o'chirilganda fayl o'chmaydi.</b> <c>ISoftDeletable</c>ning
/// butun ma'nosi — tarixni yo'qotmaslik ("HR ma'lumoti tarixiy hujjat",
/// <see cref="ISoftDeletable"/> izohiga qarang). Agar yozuv bazada qolib,
/// unga ishora qiluvchi fayl diskdan o'chirilsa, tarixiy yozuv «buzilgan»
/// bo'lib qolardi — bu aynan shu falsafaga zid. Shu sababli fayl faqat
/// yozuv o'zi TUZATILGANDA (masalan boshqa faylga almashtirilganda) emas,
/// balki yumshoq o'chirilganda ham diskdan olib tashlanmaydi (qaror va
/// asos <c>DeleteEmployeeDocumentCommandHandler</c> izohida ham bor).
/// </para>
/// </remarks>
public sealed class EmployeeDocument : AggregateRoot, IAuditable, ISoftDeletable
{
    private EmployeeDocument(
        Guid id,
        Guid employeeId,
        Guid documentTypeId,
        string fileKey,
        string fileName,
        string contentType,
        long sizeBytes)
        : base(id)
    {
        EmployeeId = employeeId;
        DocumentTypeId = documentTypeId;
        FileKey = fileKey;
        FileName = fileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
    }

    /// <summary>EF Core uchun.</summary>
    private EmployeeDocument() { }

    public Guid EmployeeId { get; private set; }

    /// <summary>Hujjat turi — <c>DocumentType</c> ma'lumotnomasiga ishora.</summary>
    public Guid DocumentTypeId { get; private set; }

    /// <summary>Saqlash kaliti — <c>IFileStorage</c> ichidagi ichki manzil.</summary>
    public string FileKey { get; private set; } = string.Empty;

    /// <summary>
    /// Asl fayl nomi — faqat KO'RSATISH uchun (yuklab olishda
    /// <c>Content-Disposition</c>). Saqlash yo'lida ISHLATILMAYDI.
    /// </summary>
    public string FileName { get; private set; } = string.Empty;

    /// <summary>
    /// Fayl mazmuniga qarab ANIQLANGAN haqiqiy MIME turi (mijoz yozgan
    /// sarlavha emas) — <c>application/pdf</c>, <c>image/jpeg</c> va h.k.
    /// </summary>
    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    /// <summary>Ixtiyoriy izoh.</summary>
    public string? Note { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>
    /// Tekshirilgan faylni xodimga biriktiradi.
    /// </summary>
    /// <remarks>
    /// Fayl mazmuni bu bosqichda ALLAQACHON tasdiqlangan bo'lishi kerak
    /// (hajm, haqiqiy tur) — bu metod faqat metama'lumotni yozadi.
    /// </remarks>
    public static Result<EmployeeDocument> Attach(
        Guid employeeId,
        Guid documentTypeId,
        string fileKey,
        string? fileName,
        string contentType,
        long sizeBytes,
        string? note = null)
    {
        if (documentTypeId == Guid.Empty)
        {
            return Error.Validation("hujjat.tur_bosh", "Hujjat turi ko'rsatilishi shart.");
        }

        if (string.IsNullOrWhiteSpace(fileKey) || string.IsNullOrWhiteSpace(contentType) || sizeBytes <= 0)
        {
            return Error.Validation("hujjat.fayl_bosh", "Fayl biriktirilishi shart.");
        }

        return new EmployeeDocument(
            NewId(), employeeId, documentTypeId, fileKey, NormalizeFileName(fileName), contentType, sizeBytes)
        {
            Note = Trim(note),
        };
    }

    /// <summary>
    /// Turi va izohini tuzatadi.
    /// </summary>
    /// <remarks>
    /// Fayl va xodim O'ZGARMAYDI — <see cref="Incentive.Update"/> dagi
    /// <c>Kind</c>/<c>EmployeeId</c> bilan bir xil qoida: agar noto'g'ri
    /// fayl yuklangan bo'lsa, bu tuzatish emas, boshqa fakt — yozuv
    /// o'chirilib, yangisi yuklanadi.
    /// </remarks>
    public Result Update(Guid documentTypeId, string? note)
    {
        if (documentTypeId == Guid.Empty)
        {
            return Error.Validation("hujjat.tur_bosh", "Hujjat turi ko'rsatilishi shart.");
        }

        DocumentTypeId = documentTypeId;
        Note = Trim(note);

        return Result.Success();
    }

    private static string NormalizeFileName(string? fileName)
        => Trim(fileName) ?? "hujjat";

    private static string? Trim(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
