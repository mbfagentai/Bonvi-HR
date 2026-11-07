using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Common.Photos;
using Bonvi.Application.Employees.Common;
using Bonvi.Application.Employees.Photos;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Queries;

/// <summary>Employee search inside the attendance section.</summary>
/// <param name="Search">Part of the name.</param>
/// <param name="Limit">The most rows wanted; defaulted and capped by the handler.</param>
public sealed record LookupAttendanceEmployeesQuery(string? Search, int? Limit);

/// <summary>
/// The employee search of a user who cannot open <c>/api/employees</c>: a picker row and nothing else.
/// </summary>
public sealed class LookupAttendanceEmployeesQueryHandler
{
    /// <summary>Rows when the client does not ask for a number.</summary>
    public const int DefaultLimit = 20;

    /// <summary>A picker needs no more, and the route must not double as a staff export.</summary>
    public const int MaxLimit = 50;

    private readonly IAttendanceReadModel _readModel;

    public LookupAttendanceEmployeesQueryHandler(IAttendanceReadModel readModel) => _readModel = readModel;

    public async Task<Result<IReadOnlyList<AttendanceEmployeeLookupItemDto>>> HandleAsync(
        LookupAttendanceEmployeesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var limit = Math.Clamp(query.Limit ?? DefaultLimit, 1, MaxLimit);

        var items = await _readModel
            .LookupEmployeesAsync(query.Search?.Trim(), limit, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(items);
    }
}

/// <summary>The header of one employee's attendance page.</summary>
/// <param name="EmployeeId">Employee.</param>
public sealed record GetAttendanceEmployeeHeaderQuery(Guid EmployeeId);

/// <summary>
/// Name, department, position and whether a photo exists, for a user who cannot open
/// <c>/api/employees/{id}</c>. This is the only employee data the page adds to what the
/// attendance lists already show; passport, PINFL, phone and salary have no way in here.
/// </summary>
public sealed class GetAttendanceEmployeeHeaderQueryHandler
{
    private readonly IAttendanceScope _scope;
    private readonly IAttendanceReadModel _readModel;

    public GetAttendanceEmployeeHeaderQueryHandler(IAttendanceScope scope, IAttendanceReadModel readModel)
    {
        _scope = scope;
        _readModel = readModel;
    }

    public async Task<Result<AttendanceEmployeeLookupItemDto>> HandleAsync(
        GetAttendanceEmployeeHeaderQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Same error as an unknown id, so an id outside the scope does not confirm the person exists.
        if (!await _scope.CanAccessEmployeeAsync(query.EmployeeId, cancellationToken).ConfigureAwait(false))
        {
            return EmployeeErrors.NotFound;
        }

        var header = await _readModel
            .GetEmployeeHeaderAsync(query.EmployeeId, cancellationToken)
            .ConfigureAwait(false);

        return header is null ? EmployeeErrors.NotFound : header;
    }
}

/// <summary>An employee's photo for the attendance section.</summary>
/// <param name="EmployeeId">Employee.</param>
/// <param name="KnownVersion">Version of the client's copy (<c>If-None-Match</c>).</param>
public sealed record GetAttendanceEmployeePhotoQuery(Guid EmployeeId, string? KnownVersion = null);

/// <summary>
/// The employee photo behind the attendance scope. Finding the key and opening the file stay
/// in <see cref="GetEmployeePhotoQueryHandler"/>; this only decides who may ask.
/// </summary>
public sealed class GetAttendanceEmployeePhotoQueryHandler
{
    private readonly IAttendanceScope _scope;
    private readonly GetEmployeePhotoQueryHandler _photos;

    public GetAttendanceEmployeePhotoQueryHandler(IAttendanceScope scope, GetEmployeePhotoQueryHandler photos)
    {
        _scope = scope;
        _photos = photos;
    }

    public async Task<Result<PhotoContent>> HandleAsync(
        GetAttendanceEmployeePhotoQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Same error as an unknown id. «No photo» out of scope would confirm the person exists.
        if (!await _scope.CanAccessEmployeeAsync(query.EmployeeId, cancellationToken).ConfigureAwait(false))
        {
            return EmployeeErrors.NotFound;
        }

        return await _photos
            .HandleAsync(new GetEmployeePhotoQuery(query.EmployeeId, query.KnownVersion), cancellationToken)
            .ConfigureAwait(false);
    }
}
