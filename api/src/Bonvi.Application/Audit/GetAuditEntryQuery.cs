using Bonvi.Application.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Audit;

/// <summary>Bitta jurnal qatori — IP, qurilma va «shu amal ichida» qatorlari bilan.</summary>
/// <param name="Id">Qator identifikatori.</param>
public sealed record GetAuditEntryQuery(Guid Id);

public sealed class GetAuditEntryQueryHandler
{
    private readonly IAuditReadModel _readModel;
    private readonly ICurrentUser _currentUser;

    public GetAuditEntryQueryHandler(IAuditReadModel readModel, ICurrentUser currentUser)
    {
        _readModel = readModel;
        _currentUser = currentUser;
    }

    public async Task<Result<AuditEntryDetailsDto>> HandleAsync(
        GetAuditEntryQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var details = await _readModel
            .GetAsync(query.Id, AuditVisibility.ViewerOf(_currentUser), cancellationToken)
            .ConfigureAwait(false);

        return details is null ? AuditErrors.NotFound : details;
    }
}
