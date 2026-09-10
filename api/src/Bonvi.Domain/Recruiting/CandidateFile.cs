using Bonvi.Domain.Common;

namespace Bonvi.Domain.Recruiting;

/// <summary>
/// Nomzodga biriktirilgan fayl: rezyume, pasport sahifasi, diplom skani.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega to'rt ustun emas, alohida jadval.</b> Ilgari nomzodda BITTA
/// fayl bor edi (<c>candidates.resume_file_key</c> va uning uch qardoshi)
/// va amalda u yetmadi: pasport bilan diplom telefonda uch-to'rt rasm
/// bo'lib keladi, HR esa ularni bitta faylga birlashtira olmaydi. To'rt
/// ustunni «rezyume_2», «rezyume_3» qilib ko'paytirish yo'li yo'q — shu
/// sababli ular shu jadvalga KO'CHIRILDI va o'chirildi (migratsiya
/// <c>NomzodIzohVaFayllar</c>).
/// </para>
/// <para>
/// <b>Nega bu agregat ILDIZI emas, <see cref="Candidate"/> ning bolasi.</b>
/// <c>EmployeeDocument</c> — ildiz, bu esa ataylab bola.
/// Sabab: fayllar SONI (<see cref="Candidate.MaxFiles"/>) va umumiy
/// HAJMI (<see cref="Candidate.MaxFilesTotalBytes"/>) — nomzodning
/// invarianti, invariantni esa faqat uni egallagan agregat ichida ushlab
/// turish mumkin. Alohida ildiz bo'lganida sanoq har INSERT dan oldin
/// so'rov bilan olinardi: bu poyga (bir vaqtda kelgan ikki yuklash
/// ikkalasi ham to'qqizta faylni ko'radi) va biznes qoidasi domen
/// o'rniga ishlovchida qolardi. <c>EmployeeDocument</c> ni ildiz qilgan
/// ikkinchi sabab — har hujjatning TURI va shu tur bo'yicha
/// filtrlanadigan alohida tab — bu yerda umuman yo'q.
/// </para>
/// <para>
/// <b>Yumshoq o'chirish YO'Q</b> — <c>EmployeeDocument</c> dan ikkinchi
/// ataylab farq: yozuv butunlay o'chadi va fayl ham diskdan olib
/// tashlanadi. Ikki sabab. (1) Yumshoq o'chirilgan yozuv o'nta o'rindan
/// bittasini egallab turardi va uni filtrlashni bironta sanoq unutsa,
/// yuklash KO'RINMAS sababdan to'xtardi — bu loyihada eng yomon nosozlik
/// turi. (2) Eski yo'l ham AYNAN shunday ishlardi (o'chirilgan
/// <c>DeleteCandidateResumeCommandHandler</c>): «foydalanuvchi ataylab
/// olib tashlang dedi va faylga boshqa hech narsa ishora qilmaydi».
/// Bugun bu qoidani <c>DeleteCandidateFileCommandHandler</c> davom
/// ettiradi. Nomzodning fayli — uning O'ZINING
/// hujjati, korxonaning rasmiy hujjati emas
/// (<c>EmployeeDocument</c> izohidagi qarama-qarshi qaror shundan).
/// </para>
/// <para>
/// Fayl mazmuni bu yerda emas: <see cref="FileKey"/> —
/// <c>IFileStorage</c> dagi kalit. Haqiqiy turni MAZMUNGA qarab
/// tekshirish va rasmni kichraytirish Application qatlamida, diskka
/// yozishdan OLDIN bajariladi (<c>ScaledDocumentStore</c>) — bu yerga
/// faqat tasdiqlangan qiymatlar kiradi.
/// </para>
/// </remarks>
public sealed class CandidateFile : Entity
{
    private CandidateFile(
        Guid id,
        Guid candidateId,
        string fileKey,
        string fileName,
        string contentType,
        long sizeBytes)
        : base(id)
    {
        CandidateId = candidateId;
        FileKey = fileKey;
        FileName = fileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
    }

    /// <summary>EF Core uchun.</summary>
    private CandidateFile() { }

    public Guid CandidateId { get; private set; }

    /// <summary>Saqlash kaliti — <c>IFileStorage</c> ichidagi ichki manzil.</summary>
    public string FileKey { get; private set; } = string.Empty;

    /// <summary>
    /// Asl fayl nomi — faqat KO'RSATISH uchun (yuklab olishda
    /// <c>Content-Disposition</c>). Saqlash yo'lida ISHLATILMAYDI.
    /// </summary>
    /// <remarks>
    /// Mijoz yuborgan nom hech qachon diskdagi yo'lga tushmaydi
    /// (<c>LocalFileStorage</c> kalitni o'zi yasaydi) — aks holda
    /// <c>../../etc/passwd</c> shu yerdan kelardi. Sababi
    /// <c>PhotoLimits.StoredFileName</c> izohida to'liq yozilgan.
    /// </remarks>
    public string FileName { get; private set; } = string.Empty;

    /// <summary>
    /// Fayl mazmuniga qarab ANIQLANGAN haqiqiy MIME turi (mijoz yozgan
    /// sarlavha emas) — <c>application/pdf</c> yoki <c>image/jpeg</c>.
    /// </summary>
    public string ContentType { get; private set; } = string.Empty;

    /// <summary>
    /// Diskka HAQIQATAN yozilgan bayt soni — yuklangan fayl hajmi emas.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Ikkita son, ikkita ish.</b> 20 MB lik fayl chegarasi
    /// YUKLANGAN baytlarda o'lchanadi: so'rovni dekodlashdan OLDIN rad
    /// etish kerak. Bu ustunda esa SAQLANGAN hajm turadi, chunki rasm
    /// serverda kichraytiriladi — 4 MB lik telefon surati diskda ~300 KB
    /// bo'ladi va kartada «4 MB» deb ko'rsatilsa, bu shunchaki yolg'on
    /// bo'lardi. Nomzodga tegishli umumiy hajm chegarasi
    /// (<see cref="Candidate.MaxFilesTotalBytes"/>) ham aynan shu son
    /// bo'yicha hisoblanadi: diskni haqiqatan unga tushgan narsa
    /// to'ldiradi.
    /// </para>
    /// <para>
    /// <c>0</c> — hajmi NOMA'LUM: eski <c>candidates.resume_size_bytes</c>
    /// bo'sh bo'lgan yozuvlar migratsiyada shu qiymat bilan ko'chdi
    /// (ustun majburiy, <c>NULL</c> qo'yib bo'lmaydi). Ekranda u chiziqcha
    /// bo'lib chiziladi.
    /// </para>
    /// </remarks>
    public long SizeBytes { get; private set; }

    /// <summary>Fayl biriktirilgan lahza.</summary>
    /// <remarks>
    /// Fayllar kartada aynan shu tartibda — YUKLASH tartibida —
    /// ko'rsatiladi (xodim hujjatlaridagidek yangidan eskiga EMAS):
    /// pasport uch varaq bo'lib 1-2-3 tartibda keladi va teskari
    /// tartibda karta o'qilmaydigan bo'lib qoladi.
    /// </remarks>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Kim yukladi — TIZIM FOYDALANUVCHISI (<c>users.id</c>).
    /// </summary>
    /// <remarks>
    /// <see cref="CandidateStageEntry.RecordedBy"/> bilan bir xil ma'no va
    /// bir xil sabab bilan majburiy emas: «kim kiritdi» — audit fakti va u
    /// faqat tizimga kirgan hisobdan chiqadi. Migratsiyada ko'chgan eski
    /// rezyumelarda u <c>null</c>: yuklovchi hech qachon yozilmagan edi.
    /// </remarks>
    public Guid? CreatedBy { get; private set; }

    /// <summary>
    /// Tekshirilgan faylni nomzodga biriktiradi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>internal</c> va ATAYLAB shunday: yozuv faqat
    /// <see cref="Candidate.AttachFile"/> orqali yaratiladi, chunki soni va
    /// umumiy hajmi nomzodning invarianti (izohi sinf tepasida). Bu yerda
    /// faqat FAYLNING o'z qoidasi tekshiriladi — kalit, turi va hajmi bor.
    /// </para>
    /// <para>
    /// Xato kodi eski <c>AttachResume</c> dan MEROS: u allaqachon to'rt
    /// tilga tarjimalangan va HR uchun ma'nosi o'zgarmadi.
    /// </para>
    /// </remarks>
    internal static Result<CandidateFile> Create(
        Guid candidateId,
        string fileKey,
        string? fileName,
        string contentType,
        long storedBytes,
        Guid? createdBy = null)
    {
        if (string.IsNullOrWhiteSpace(fileKey) || string.IsNullOrWhiteSpace(contentType) || storedBytes <= 0)
        {
            return Error.Validation("nomzod.rezyume_bosh", "Rezyume fayli biriktirilishi shart.");
        }

        return new CandidateFile(NewId(), candidateId, fileKey, NormalizeFileName(fileName), contentType, storedBytes)
        {
            CreatedBy = createdBy,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>Fayl nomini ko'rsatish uchun tozalaydi (yo'l qismi tashlanadi).</summary>
    /// <remarks>
    /// <see cref="Candidate"/> dan O'ZGARTIRILMAGAN holda ko'chirildi
    /// (u yerda rezyume ustunlari bilan birga o'chdi). Zaxira nom
    /// <c>«rezyume»</c> bo'lib qoldi, <c>«hujjat»</c> emas: migratsiyada
    /// ko'chgan qatorlar ham aynan shu nomni oldi va ikkalasi bir xil
    /// ko'rinishi kerak.
    /// </remarks>
    private static string NormalizeFileName(string? fileName)
    {
        var trimmed = string.IsNullOrWhiteSpace(fileName) ? null : fileName.Trim();
        if (trimmed is null)
        {
            return "rezyume";
        }

        // Mijoz to'liq yo'l yuborishi mumkin («C:\Users\...\rezyume.pdf») —
        // saqlashda u ishlatilmaydi, lekin ekranda ham ko'rinmasligi kerak.
        var name = Path.GetFileName(trimmed.Replace('\\', '/'));

        return string.IsNullOrWhiteSpace(name) ? "rezyume" : name;
    }
}
