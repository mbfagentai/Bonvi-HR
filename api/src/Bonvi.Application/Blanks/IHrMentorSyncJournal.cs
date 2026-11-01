namespace Bonvi.Application.Blanks;

/// <summary>Sinxronlash qadami (jurnal va <c>hrmentor.lastError</c> uchun).</summary>
public enum HrMentorSyncStep
{
    /// <summary>Katalogni yuborish.</summary>
    Catalog = 1,

    /// <summary>HR matnlarini olish.</summary>
    Texts = 2,

    /// <summary>Blankalarni olish.</summary>
    Blanks = 3,

    /// <summary>Profilni (bog'lanishlar + urug') yuborish — SPEC-anketa-konstruktori §3.</summary>
    Profile = 4,

    /// <summary>E'lon qilingan anketa ta'rifini olish — SPEC-anketa-konstruktori §5.2.</summary>
    Definition = 5,
}

/// <summary>Shu yurishda amaldagi anketa almashdi.</summary>
/// <param name="Version">Yangi amaldagi versiya; <c>null</c> — o'rnatilgan anketaga qaytildi.</param>
public sealed record HrMentorDefinitionSwitch(int? Version);

/// <summary>Bitta yurishning natijasi — jurnal uchun.</summary>
/// <param name="CatalogPushed">Katalog shu yurishda yuborildimi.</param>
/// <param name="TextsRevision">Shu yurishda saqlangan yangi reviziya; yo'q bo'lsa <c>null</c>.</param>
/// <param name="BlanksAdded">Yangi blankalar.</param>
/// <param name="BlanksUpdated">Yangi versiyasi olinganlar.</param>
/// <param name="BlanksRemoved">HRMentor ro'yxatidan chiqib, o'chirilganlar.</param>
/// <param name="ErrorCode">Birinchi yiqilgan qadamning kodi; hammasi o'tsa <c>null</c>.</param>
/// <param name="ProfilePushed">Profil shu yurishda yuborildimi.</param>
/// <param name="Definition">Amaldagi anketa almashgan bo'lsa — qaysi versiyaga; aks holda <c>null</c>.</param>
/// <param name="ErrorsLasting">
/// Yiqilgan qadamlarning hammasi doimiy xato bilan (<see cref="HrMentorErrors.IsLasting"/>) — xuddi shu
/// holatni qayta sinash foyda bermaydi. Xato bo'lmasa <c>false</c>.
/// </param>
public sealed record HrMentorSyncReport(
    bool CatalogPushed,
    int? TextsRevision,
    int BlanksAdded,
    int BlanksUpdated,
    int BlanksRemoved,
    string? ErrorCode,
    bool ProfilePushed = false,
    HrMentorDefinitionSwitch? Definition = null,
    bool ErrorsLasting = false)
{
    /// <summary>Shu yurishda biror narsa o'zgardimi.</summary>
    public bool Changed => CatalogPushed || ProfilePushed || TextsRevision is not null || Definition is not null
        || BlanksAdded + BlanksUpdated + BlanksRemoved > 0;
}

/// <summary>
/// Sinxronlash jurnali — Application qatlamida <c>ILogger</c> yo'q, yozuvlar Infrastructure da
/// (<c>HrMentorSyncJournal</c>). Hech bir yozuvda kalit yoki javob tanasi yo'q — faqat kodlar.
/// </summary>
public interface IHrMentorSyncJournal
{
    /// <summary>Qadam yiqildi: oxirgi yaxshi ma'lumot qoldi, qolgan qadamlar davom etadi.</summary>
    void StepFailed(HrMentorSyncStep step, string errorCode);

    /// <summary>Bitta blanka olinmadi (hajm, SHA-256, tarmoq) — eski nusxasi qoldi.</summary>
    void BlankSkipped(Guid documentId, string errorCode);

    /// <summary>Yurish tugadi va nimadir o'zgardi.</summary>
    void Finished(HrMentorSyncReport report);
}
