using Bonvi.Domain.Common;

namespace Bonvi.Domain.Employees;

/// <summary>
/// Tekshiruv materiali — «Tekshiruvlar» kartasidagi dalil fayli: mehnat.uz
/// skrinshoti, qarzdorlik yoki sudlanganlik ma'lumotnomasi va h.k.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega bitta skrinshot emas (2026-09-27).</b> Ilgari xodimda bitta
/// maydon bor edi (mehnat.uz skrinshoti) va yangisi eskisini almashtirardi.
/// Amalda HR bir nechta tekshiruv o'tkazadi — har birining o'z dalili bor
/// va bitta yuklama yetmay qoldi (foydalanuvchi talabi). Shu sababli
/// materiallar endi RO'YXAT: har biri alohida yozuv.
/// </para>
/// <para>
/// <b>Nega <see cref="EmployeeDocument"/> emas.</b> Hujjat — shartnoma,
/// buyruq, pasport: uning TURI majburiy va u «Hujjatlar» tabida yashaydi.
/// Tekshiruv materialining turi yo'q, u faqat «Tekshiruvlar» kartasiga
/// tegishli. Hujjatga maxsus tur bilan qo'shilsa, karta o'z fayllarini
/// tur NOMI bo'yicha qidirishi kerak bo'lardi — HR ma'lumotnomada nomni
/// o'zgartirsa, karta jimgina bo'shab qolardi.
/// </para>
/// <para>
/// <b>Fayl quvuri hujjatniki</b> (<c>DocumentFileStore</c>): mazmun
/// bo'yicha tur, 20 MB chegara, baytlar O'ZGARISHSIZ — skrinshotdagi
/// matn o'qilishi kerak (qirqilmaydi, kichraytirilmaydi).
/// </para>
/// <para>
/// <b>Yumshoq o'chiriladi va fayl diskda qoladi</b> —
/// <see cref="EmployeeDocument"/> bilan bir xil qoida: bu dalil, uning
/// tarixi yo'qolmasligi kerak.
/// </para>
/// </remarks>
public sealed class EmployeeCheckFile : AggregateRoot, IAuditable, ISoftDeletable
{
    private EmployeeCheckFile(
        Guid id,
        Guid employeeId,
        string fileKey,
        string fileName,
        string contentType,
        long? sizeBytes)
        : base(id)
    {
        EmployeeId = employeeId;
        FileKey = fileKey;
        FileName = fileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
    }

    /// <summary>EF Core uchun.</summary>
    private EmployeeCheckFile() { }

    public Guid EmployeeId { get; private set; }

    /// <summary>Saqlash kaliti — <c>IFileStorage</c> ichidagi ichki manzil.</summary>
    public string FileKey { get; private set; } = string.Empty;

    /// <summary>
    /// Asl fayl nomi — faqat KO'RSATISH uchun. Saqlash yo'lida ISHLATILMAYDI.
    /// </summary>
    public string FileName { get; private set; } = string.Empty;

    /// <summary>Fayl MAZMUNIGA qarab aniqlangan MIME turi (mijoz yozgani emas).</summary>
    public string ContentType { get; private set; } = string.Empty;

    /// <summary>
    /// Hajm. <c>null</c> — noma'lum: eski yagona skrinshotdan ko'chirilgan
    /// yozuvda (u hajmni saqlamagan va migratsiya diskni o'qimaydi).
    /// </summary>
    public long? SizeBytes { get; private set; }

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
    public static Result<EmployeeCheckFile> Attach(
        Guid employeeId,
        string fileKey,
        string? fileName,
        string contentType,
        long sizeBytes)
    {
        if (employeeId == Guid.Empty)
        {
            return Error.Validation("tekshiruv_fayli.xodim_yoq", "Xodim ko'rsatilishi shart.");
        }

        if (string.IsNullOrWhiteSpace(fileKey) || string.IsNullOrWhiteSpace(contentType) || sizeBytes <= 0)
        {
            return Error.Validation("hujjat.fayl_bosh", "Fayl biriktirilishi shart.");
        }

        return new EmployeeCheckFile(
            NewId(), employeeId, fileKey.Trim(), NormalizeFileName(fileName), contentType.Trim(), sizeBytes);
    }

    /// <summary>Ustun chegarasi — undan uzun nom saqlashda 500 bilan yiqilardi.</summary>
    public const int MaxFileNameLength = 255;

    /// <summary>Bundan uzun «kengaytma» — kengaytma emas, nomning bir qismi.</summary>
    private const int MaxExtensionLength = 16;

    /// <summary>
    /// Ko'rsatiladigan nom: yo'lsiz, ustunga sig'adigan, kengaytmasi bilan.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item>
    ///     Mijoz to'liq yo'l yuborishi mumkin («C:\Users\...\dalil.pdf») —
    ///     saqlashda u ishlatilmaydi, ekranda ham ko'rinmasligi kerak
    ///     (<c>CandidateFile</c> dagi bilan bir xil qoida).
    ///   </item>
    ///   <item>
    ///     Uzun nom qisqartirilganda KENGAYTMA saqlanadi — HR faylning
    ///     PDF yoki rasm ekanini nomidan ham ko'radi.
    ///   </item>
    ///   <item>
    ///     Kesish UTF-16 juftligini (emoji) yarmidan bo'lmaydi — yarim
    ///     juftlik yaroqsiz matn bo'lib, saqlashda yiqilardi.
    ///   </item>
    /// </list>
    /// </remarks>
    private static string NormalizeFileName(string? fileName)
    {
        var trimmed = string.IsNullOrWhiteSpace(fileName) ? null : fileName.Trim();
        var name = trimmed is null ? null : Path.GetFileName(trimmed.Replace('\\', '/')).Trim();

        if (string.IsNullOrEmpty(name))
        {
            return "material";
        }

        if (name.Length <= MaxFileNameLength)
        {
            return name;
        }

        var extension = Path.GetExtension(name);
        if (extension.Length > MaxExtensionLength)
        {
            extension = string.Empty;
        }

        var stem = name[..^extension.Length];
        var keep = MaxFileNameLength - extension.Length;

        if (char.IsHighSurrogate(stem[keep - 1]))
        {
            keep--;
        }

        return stem[..keep] + extension;
    }
}
