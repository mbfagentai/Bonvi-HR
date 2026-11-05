using Bonvi.Domain.Identity;

namespace Bonvi.Domain.Abstractions;

/// <summary>Writes the departments assigned to a scoped attendance user.</summary>
public interface IUserAttendanceScopeRepository
{
    /// <summary>The user's rows, tracked so that they can be removed.</summary>
    Task<IReadOnlyList<UserAttendanceScope>> ListForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>How many of the given identifiers are live departments.</summary>
    /// <param name="departmentIds">Identifiers without duplicates.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<int> CountExistingDepartmentsAsync(
        IReadOnlyCollection<Guid> departmentIds,
        CancellationToken cancellationToken = default);

    void Add(UserAttendanceScope scope);

    void Remove(UserAttendanceScope scope);
}
