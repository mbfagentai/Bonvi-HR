using Bonvi.Domain.Blanks;

namespace Bonvi.Domain.Abstractions;

/// <summary>Blanka shablonlari uchun saqlash porti (SPEC-blankalar §3.1).</summary>
public interface IBlankTemplateRepository : IRepository<BlankTemplate>
{
    /// <summary>Kod bo'yicha (<c>HR-F06</c>), aniq moslik; yo'q bo'lsa <c>null</c>.</summary>
    Task<BlankTemplate?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>Hamma shablonlar — <c>sort_order</c>, keyin kod bo'yicha.</summary>
    Task<IReadOnlyList<BlankTemplate>> ListAsync(CancellationToken cancellationToken = default);
}

/// <summary>Shablon versiyalari uchun saqlash porti (SPEC-blankalar §3.2).</summary>
public interface IBlankTemplateVersionRepository : IRepository<BlankTemplateVersion>
{
    /// <summary>Shablonning e'lon qilingan versiyasi; yo'q bo'lsa <c>null</c>.</summary>
    Task<BlankTemplateVersion?> GetPublishedAsync(Guid templateId, CancellationToken cancellationToken = default);

    /// <summary>Shablonning aniq versiyasi (<c>1.0</c>); yo'q bo'lsa <c>null</c>.</summary>
    Task<BlankTemplateVersion?> GetAsync(Guid templateId, string version, CancellationToken cancellationToken = default);

    /// <summary>Shablonning hamma versiyalari — yaratilish tartibida.</summary>
    Task<IReadOnlyList<BlankTemplateVersion>> ListByTemplateAsync(Guid templateId, CancellationToken cancellationToken = default);
}

/// <summary>To'ldirilgan blankalar uchun saqlash porti (SPEC-blankalar §3.3–§3.5).</summary>
public interface IBlankSubmissionRepository : IRepository<BlankSubmission>
{
    /// <summary>
    /// Blankani qadamlari va fayllari BILAN yuklaydi — o'zgartiruvchi har bir amal shu metoddan
    /// boshlanadi (guruh qoidasi va fayl soni agregatning invarianti).
    /// </summary>
    Task<BlankSubmission?> GetWithChildrenAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Yaratish so'rovi identifikatori bo'yicha (takroriy yaratish) — bolalarsiz.</summary>
    Task<BlankSubmission?> GetByClientRequestIdAsync(Guid clientRequestId, CancellationToken cancellationToken = default);
}

/// <summary>Lavozim rollarini biriktirish uchun saqlash porti (SPEC-blankalar §3.6).</summary>
public interface IBlankRoleAssignmentRepository : IRepository<BlankRoleAssignment>
{
    /// <summary>Hamma biriktirishlar — rol, keyin yaratilish tartibida.</summary>
    Task<IReadOnlyList<BlankRoleAssignment>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Rolga biriktirilgan foydalanuvchilar (bo'sh — rol hech kimga biriktirilmagan).</summary>
    Task<IReadOnlyList<Guid>> ListUserIdsAsync(string roleCode, CancellationToken cancellationToken = default);

    /// <summary>Rol-foydalanuvchi jufti; yo'q bo'lsa <c>null</c>.</summary>
    Task<BlankRoleAssignment?> GetAsync(string roleCode, Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>Blanka raqamlari hisoblagichi (SPEC-blankalar §3.7).</summary>
public interface IBlankNumberCounter
{
    /// <summary>
    /// Shablon bo'yicha keyingi raqam — bazaga DARROV yoziladi (<c>INSERT … ON CONFLICT … RETURNING</c>)
    /// va qatorni tranzaksiya oxirigacha qulflaydi.
    /// </summary>
    /// <remarks>
    /// ⚠️ Faqat ochiq tranzaksiya ichida chaqiriladi (yuborish amali): tranzaksiyasiz chaqiruv
    /// raqamni darhol «yoqadi» va keyingi xatoda bo'shliq qoladi.
    /// </remarks>
    Task<int> NextAsync(string templateCode, CancellationToken cancellationToken = default);
}
