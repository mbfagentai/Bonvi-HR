using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Catalog;
using Bonvi.Domain.Organization;

namespace Bonvi.Application.Employees.Common;

/// <summary>
/// Bo'lim / lavozim / yuridik shaxs / ishdan ketish sababi nomlarini
/// identifikator bo'yicha topadi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega kerak.</b> Mehnat munosabatida faqat identifikatorlar saqlanadi,
/// xodim kartasida esa «Sotuv bo'limi · Menejer · VIKOR MChJ» ko'rinishi kerak.
/// </para>
/// <para>
/// <b>Nega so'rov davomida eslab qolinadi.</b> Bitta xodimning uchta mehnat
/// munosabati va o'nlab hodisasi bo'lishi mumkin, ularning ko'pi bir xil
/// bo'lim va lavozimga ishora qiladi. Kesh bo'lmasa, bitta karta uchun
/// o'nlab bir xil so'rov ketardi. Kesh <b>so'rov doirasida</b> yashaydi
/// (Scoped), shu sababli eskirgan nom ko'rsatish xavfi yo'q.
/// </para>
/// </remarks>
public sealed class CatalogNameResolver
{
    private readonly IDepartmentRepository _departments;
    private readonly IPositionRepository _positions;
    private readonly IReferenceRepository<LegalEntity> _legalEntities;
    private readonly IReferenceRepository<TerminationReason> _terminationReasons;
    private readonly IReferenceRepository<AwardType> _awardTypes;
    private readonly IReferenceRepository<PenaltyType> _penaltyTypes;
    private readonly IReferenceRepository<DocumentType> _documentTypes;

    private readonly Dictionary<Guid, string?> _departmentNames = [];
    private readonly Dictionary<Guid, string?> _positionNames = [];
    private readonly Dictionary<Guid, string?> _legalEntityNames = [];
    private readonly Dictionary<Guid, string?> _terminationReasonNames = [];
    private readonly Dictionary<Guid, string?> _awardTypeNames = [];
    private readonly Dictionary<Guid, string?> _penaltyTypeNames = [];
    private readonly Dictionary<Guid, string?> _documentTypeNames = [];

    public CatalogNameResolver(
        IDepartmentRepository departments,
        IPositionRepository positions,
        IReferenceRepository<LegalEntity> legalEntities,
        IReferenceRepository<TerminationReason> terminationReasons,
        IReferenceRepository<AwardType> awardTypes,
        IReferenceRepository<PenaltyType> penaltyTypes,
        IReferenceRepository<DocumentType> documentTypes)
    {
        _departments = departments;
        _positions = positions;
        _legalEntities = legalEntities;
        _terminationReasons = terminationReasons;
        _awardTypes = awardTypes;
        _penaltyTypes = penaltyTypes;
        _documentTypes = documentTypes;
    }

    public Task<string?> DepartmentAsync(Guid? id, CancellationToken cancellationToken = default)
        => ResolveAsync(id, _departmentNames, _departments, cancellationToken);

    public Task<string?> PositionAsync(Guid? id, CancellationToken cancellationToken = default)
        => ResolveAsync(id, _positionNames, _positions, cancellationToken);

    public Task<string?> LegalEntityAsync(Guid? id, CancellationToken cancellationToken = default)
        => ResolveAsync(id, _legalEntityNames, _legalEntities, cancellationToken);

    public Task<string?> TerminationReasonAsync(Guid? id, CancellationToken cancellationToken = default)
        => ResolveAsync(id, _terminationReasonNames, _terminationReasons, cancellationToken);

    public Task<string?> AwardTypeAsync(Guid? id, CancellationToken cancellationToken = default)
        => ResolveAsync(id, _awardTypeNames, _awardTypes, cancellationToken);

    public Task<string?> PenaltyTypeAsync(Guid? id, CancellationToken cancellationToken = default)
        => ResolveAsync(id, _penaltyTypeNames, _penaltyTypes, cancellationToken);

    public Task<string?> DocumentTypeAsync(Guid? id, CancellationToken cancellationToken = default)
        => ResolveAsync(id, _documentTypeNames, _documentTypes, cancellationToken);

    private static async Task<string?> ResolveAsync<TEntity>(
        Guid? id,
        Dictionary<Guid, string?> cache,
        IRepository<TEntity> repository,
        CancellationToken cancellationToken)
        where TEntity : Domain.Common.ReferenceEntity
    {
        if (id is not { } key)
        {
            return null;
        }

        if (cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var entity = await repository.GetByIdAsync(key, cancellationToken).ConfigureAwait(false);
        var name = entity?.Name;

        cache[key] = name;
        return name;
    }
}
