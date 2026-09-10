using Bonvi.Domain.Common;

namespace Bonvi.Domain.Recruiting;

/// <summary>Izohni QAYSI amal tug'dirgani.</summary>
/// <remarks>
/// <para>
/// <b>Nega erkin matn emas, sanoq.</b> Tarixda qatorning yonida uni
/// nomlaydigan yorliq turishi kerak («Boshqa vakansiyaga o'tkazildi») va u
/// to'rt tilda chiziladi. Yorliq matnning ICHIGA yozilgan bo'lsa, uni
/// tarjima qilib bo'lmasdi.
/// </para>
/// <para>
/// ⭐ <b>Ro'yxatda <c>Manual</c> yo'q va bu ATAYLAB.</b> Nomzod kartasida
/// «izoh qo'shish» degan mustaqil tugma yo'q: izoh DOIM biror amaldan
/// tug'iladi. Erkin izoh oqimi bosqich xulosasi bilan raqobatlashardi va
/// HR bitta gapni ikki joyda qidiradigan bo'lardi — bu esa aynan shu
/// o'zgarish tuzatayotgan muammo. Sanoqda qiymat yo'qligi shu qoidani
/// KODDA ushlab turadi.
/// </para>
/// <para>
/// Yangi qiymat faqat OXIRIGA qo'shiladi — loyihaning umumiy qoidasi
/// (<see cref="CandidateStatus"/> izohi): o'rtaga qo'yilsa bazadagi eski
/// qiymatlar jimgina siljib ketardi.
/// </para>
/// </remarks>
public enum CandidateNoteKind
{
    /// <summary>Boshqa vakansiyaga o'tkazildi.</summary>
    Reassign = 1,

    /// <summary>Rezervdan voronkaga qaytarildi.</summary>
    ReturnFromReserve = 2,

    /// <summary>Suhbat yangi vaqtga ko'chirildi (bosqich o'zgarmadi).</summary>
    InterviewRescheduled = 3,
}

/// <summary>
/// Nomzod bilan bo'lgan ishning izohi — bosqich yozuvi TEGISHLI
/// BO'LMAGAN amallar uchun.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega yangi jadval kerak bo'ldi.</b> Uchta amal — boshqa
/// vakansiyaga o'tkazish, rezervdan qaytarish va suhbatni ko'chirish —
/// bosqichni O'ZGARTIRMAYDI, lekin HR ular haqida gap yozishi kerak
/// («qo'ng'iroq qildim, 5-6 kunda keladi dedi»). Izohni qo'yish uchun
/// uchta mavjud joy ko'rildi va uchalasi ham rad etildi:
/// </para>
/// <list type="bullet">
///   <item><see cref="Candidate.Note"/> — bu ODAMNING uzoq muddatli
///   tavsifi («kim tavsiya qilgan, nimasi ajralib turadi») va u tahrirlash
///   formasidan ochiq: bu yerga yozilgan gap keyingi tahrirda jimgina
///   ustidan yozilardi. Bundan tashqari u BITTA maydon, tarix emas.
///   Chegara <see cref="Candidate.MoveToReserve"/> izohida allaqachon
///   chizilgan.</item>
///   <item><see cref="Candidate.ClosingNote"/> — rezervdan qaytarish uchun
///   umuman ishlamaydi: <see cref="Candidate.ReturnFromReserve"/> o'sha
///   tranzaksiyada uni TOZALAYDI, ya'ni izoh yozilgan zahoti o'chardi.</item>
///   <item><see cref="CandidateStageEntry"/> — yozuvda <c>StageId</c> ham,
///   <c>Outcome</c> ham bo'ladi va ikkalasi ham yolg'on bo'lardi: telefon
///   suhbatidan o'tgan odamga «Гаплашилди» bosqichida yangi «kutilmoqda»
///   yozuvi qo'yilsa, tarix telefon suhbati qaytadan o'tkazilayotganini
///   da'vo qilardi va qatorda «natijani yozing» tugmasi paydo bo'lardi —
///   natijasi bo'lmagan narsa uchun.</item>
/// </list>
/// <para>
/// To'rtinchi yo'l — <see cref="StageOutcome"/> ga <c>Note</c> qiymatini
/// qo'shish — ham rad etildi, sababi ta'mga emas faktga tayanadi: u
/// sanoqning IKKINCHI iste'molchisi bor. Xodim kartasidagi kengash arxivi
/// <c>GET /api/employees/{id}/stages</c> dan o'qiydi, ya'ni yangi qiymat
/// XODIMLAR bo'limida paydo bo'lardi — u yerda nomzod izohining ma'nosi
/// yo'q.
/// </para>
/// <para>
/// <b>Bu ham <see cref="Candidate"/> ning bolasi</b>, ildiz emas:
/// <see cref="CandidateStageEntry"/> bilan bir xil sabab — yozuv
/// nomzoddan ajralgan holda ma'nosiz va u DOIM nomzod bilan birga
/// o'qiladi. Yumshoq o'chirish ham xuddi shu sababdan yo'q.
/// </para>
/// </remarks>
public sealed class CandidateNote : Entity
{
    private CandidateNote(Guid id, Guid candidateId, CandidateNoteKind kind, string text)
        : base(id)
    {
        CandidateId = candidateId;
        Kind = kind;
        Text = text;
    }

    /// <summary>EF Core uchun.</summary>
    private CandidateNote() { }

    public Guid CandidateId { get; private set; }

    /// <summary>Qaysi amal shu izohni tug'dirdi.</summary>
    public CandidateNoteKind Kind { get; private set; }

    /// <summary>
    /// Izohning o'zi — ICHKI qayd, nomzodga hech qachon yuborilmaydi.
    /// </summary>
    /// <remarks>
    /// Uzunlik faqat ustun bilan cheklangan (2000) —
    /// <see cref="Candidate.Note"/> va
    /// <see cref="CandidateStageEntry.Conclusion"/> dagi bilan bir xil
    /// mavjud qoida; chegarani interfeys <c>maxLength</c> bilan qo'yadi.
    /// </remarks>
    public string Text { get; private set; } = string.Empty;

    /// <summary>Yozilgan lahza.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Kim yozdi — TIZIM FOYDALANUVCHISI (<c>users.id</c>).
    /// </summary>
    /// <remarks>
    /// <see cref="CandidateFile.CreatedBy"/> va
    /// <see cref="CandidateStageEntry.RecordedBy"/> bilan bir xil qoida:
    /// audit fakti, faqat tizimga kirgan hisobdan chiqadi.
    /// </remarks>
    public Guid? CreatedBy { get; private set; }

    /// <summary>
    /// Izoh yozuvini yaratadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>BO'SH matn bilan chaqirilmaydi</b> — bu chaqiruvchining
    /// shartnomasi va buzilishi <see cref="ArgumentException"/> beradi.
    /// Sabab: yozuv FAQAT matn borligi uchun mavjud, ya'ni «izoh
    /// yozilmadi» degan holat yozuvsiz ko'rinadi — ishlovchi
    /// <c>string.IsNullOrWhiteSpace</c> ni tekshirib, bu metodni umuman
    /// chaqirmaydi (amallarning hammasida izoh IXTIYORIY).
    /// </para>
    /// <para>
    /// Nega <c>Result</c> emas: xato kodi foydalanuvchiga ko'rsatiladigan
    /// narsa va u to'rt tilga tarjima talab qiladi. Bu holat esa
    /// foydalanuvchiga umuman yetib bormaydi — u dasturchining xatosi, va
    /// tarjimalanmagan kod ekranda XOM ko'rinishi bundan yomonroq bo'lardi.
    /// </para>
    /// </remarks>
    internal static CandidateNote Create(
        Guid candidateId,
        CandidateNoteKind kind,
        string text,
        Guid? createdBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        return new CandidateNote(NewId(), candidateId, kind, text.Trim())
        {
            CreatedBy = createdBy,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }
}
