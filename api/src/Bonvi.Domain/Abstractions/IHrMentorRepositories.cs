using Bonvi.Domain.Blanks;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Domain.Abstractions;

/// <summary>HRMentor blankalarining shu yerdagi nusxalari (SPEC-blankalar-hrmentor §3.3).</summary>
public interface IHrBlankRepository : IRepository<HrBlank>
{
    /// <summary>Hammasi, kuzatuv bilan — sinxronlash ularni yangilaydi yoki o'chiradi.</summary>
    Task<IReadOnlyList<HrBlank>> ListTrackedAsync(CancellationToken cancellationToken = default);

    /// <summary>Hammasi, kod bo'yicha — faqat o'qish (sahifa).</summary>
    Task<IReadOnlyList<HrBlank>> ListAsync(CancellationToken cancellationToken = default);
}

/// <summary>HR matnlari reviziyalari (SPEC-blankalar-hrmentor §3.2, §7).</summary>
public interface IFormTextSnapshotRepository : IRepository<FormTextSnapshot>
{
    /// <summary>Blankaning eng yangi reviziyasi; hali yo'q bo'lsa <c>null</c>.</summary>
    Task<FormTextSnapshot?> LatestAsync(string formCode, CancellationToken cancellationToken = default);

    /// <summary>Aniq reviziya; yo'q bo'lsa <c>null</c>.</summary>
    Task<FormTextSnapshot?> FindAsync(string formCode, int revision, CancellationToken cancellationToken = default);
}

/// <summary>HRMentor konstruktorida e'lon qilingan anketa versiyalari (SPEC-anketa-konstruktori §5).</summary>
public interface IFormDefinitionRepository : IRepository<FormDefinitionSnapshot>
{
    /// <summary>Eng katta versiya; hali yo'q bo'lsa <c>null</c>.</summary>
    Task<FormDefinitionSnapshot?> LatestAsync(string formCode, CancellationToken cancellationToken = default);

    /// <summary>Aniq versiya; yo'q bo'lsa <c>null</c>.</summary>
    Task<FormDefinitionSnapshot?> FindAsync(string formCode, int version, CancellationToken cancellationToken = default);
}
