using Bonvi.Domain.Auditing;

namespace Bonvi.Application.Audit;

/// <summary>
/// Audit jurnalini O'QISH porti (SPEC-rollar-va-audit §7.10, §8.3).
/// </summary>
/// <remarks>
/// <para>
/// <b>Ko'rinish SQL'da qo'llanadi</b>, xotirada emas: qator so'rovchining bo'limiga
/// tegishli bo'lmasa, u sahifaga ham, umumiy sanoqqa ham kirmaydi. Xotirada filtrlansa,
/// «jami 120 ta» deb, sahifada uchta qator ko'rsatilardi.
/// </para>
/// <para>
/// Amalga oshirish so'rovchini (<c>ICurrentUser</c>) BILMAYDI — ruxsatlar
/// <see cref="AuditViewer"/> bo'lib uzatiladi (SPEC §3.10: «Infrastructure never reads the
/// principal»). Qaysi entity qaysi bo'limga tegishli, qaysi maydon oklad yoki shaxsiy —
/// bu esa Infrastructure'dagi audit katalogi.
/// </para>
/// </remarks>
public interface IAuditReadModel
{
    /// <summary>
    /// Entity nomining kanonik ko'rinishi (registrsiz qidiruv); jurnal klassifikatsiyasida
    /// bunday tur bo'lmasa — <c>null</c>.
    /// </summary>
    /// <param name="entityName">So'rovdagi nom (<c>employmentevent</c>).</param>
    string? FindEntityName(string entityName);

    /// <summary>Filtrlangan, sahifalangan ro'yxat — eng yangisi birinchi.</summary>
    Task<AuditPage> ListAsync(AuditListFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Bitta qator va shu harakatning boshqa qatorlari; topilmasa yoki ko'rinmasa — <c>null</c>.</summary>
    Task<AuditEntryDetailsDto?> GetAsync(
        Guid id,
        AuditViewer viewer,
        CancellationToken cancellationToken = default);

    /// <summary>Jurnalda uchraydigan tizim foydalanuvchilari — «Kim» filtri uchun.</summary>
    Task<IReadOnlyList<AuditActorOptionDto>> ListActorsAsync(CancellationToken cancellationToken = default);
}

/// <summary>So'rovchi jurnaldan nimani ko'ra oladi.</summary>
/// <param name="Modules">
/// Ochiq bo'limlar — har birining KIRISH kodi (eng quyi darajasining kodlari) so'rovchida
/// bor (<see cref="AuditVisibility"/>).
/// </param>
/// <param name="PersonalData">Shaxsiy maydonlar va shaxsiy qatorlar (hujjat, qobiliyat, tekshiruv).</param>
/// <param name="Salary">Oklad maydonlari.</param>
/// <param name="ExtraPay">Qo'shimcha vazifa haqi.</param>
/// <param name="Security">Kirish va xavfsizlik hodisalari — <c>users.manage</c>.</param>
public sealed record AuditViewer(
    IReadOnlySet<string> Modules,
    bool PersonalData,
    bool Salary,
    bool ExtraPay,
    bool Security);

/// <summary>Ro'yxat filtri — tekshirilgan va normallashgan ko'rinishda.</summary>
/// <param name="Viewer">Ko'rinish.</param>
/// <param name="From">Quyi chegara (UTC, kiradi); <c>null</c> — chegarasiz.</param>
/// <param name="To">Yuqori chegara (UTC, KIRMAYDI); <c>null</c> — chegarasiz.</param>
/// <param name="Changes">O'zgarishlar (yaratish, tahrir, o'chirish) va Excel eksporti.</param>
/// <param name="Security">Kirish va xavfsizlik hodisalari (ko'rinishda ruxsat bo'lsagina).</param>
/// <param name="ActorUserId">Faqat shu foydalanuvchining qatorlari.</param>
/// <param name="ActorSystem">Faqat tizim yozgan qatorlar.</param>
/// <param name="Module">Faqat shu bo'lim.</param>
/// <param name="Action">Faqat shu amal.</param>
/// <param name="EmployeeId">Faqat shu xodimga tegishli qatorlar.</param>
/// <param name="Entity">Faqat shu entity turi (kanonik nom).</param>
/// <param name="EntityId">Faqat shu yozuv.</param>
/// <param name="Page">Sahifa, 1 dan.</param>
/// <param name="PageSize">Sahifa hajmi.</param>
public sealed record AuditListFilter(
    AuditViewer Viewer,
    DateTimeOffset? From,
    DateTimeOffset? To,
    bool Changes,
    bool Security,
    Guid? ActorUserId,
    bool ActorSystem,
    string? Module,
    AuditAction? Action,
    Guid? EmployeeId,
    string? Entity,
    string? EntityId,
    int Page,
    int PageSize);

/// <summary>Ro'yxat sahifasi va filtrga mos qatorlarning umumiy soni.</summary>
/// <param name="Items">Sahifadagi qatorlar.</param>
/// <param name="Total">Hamma sahifalardagi qatorlar soni.</param>
public sealed record AuditPage(IReadOnlyList<AuditEntryDto> Items, int Total);
