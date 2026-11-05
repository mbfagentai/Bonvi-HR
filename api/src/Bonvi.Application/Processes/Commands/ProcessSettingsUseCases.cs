using Bonvi.Application.Abstractions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Processes;

namespace Bonvi.Application.Processes.Commands;

/// <summary>One stage binding (§6.5); a code with no row reads as unbound.</summary>
/// <param name="BindingCode"><c>contacted</c>, <c>invited</c>, <c>council</c> or <c>practicalTest</c>.</param>
/// <param name="PipelineStageId">The bound stage; <c>null</c> — unbound, the step closes by its manual fallback only.</param>
/// <param name="PipelineStageName">The stage's name.</param>
public sealed record ProcessStageBindingDto(string BindingCode, Guid? PipelineStageId, string? PipelineStageName);

/// <summary>GET <c>/settings/stage-bindings</c> (§10 #16): every code, in settings page order.</summary>
public sealed class ListProcessStageBindingsQueryHandler
{
    private readonly IProcessStageBindingRepository _bindings;
    private readonly IPipelineStageRepository _stages;

    public ListProcessStageBindingsQueryHandler(IProcessStageBindingRepository bindings, IPipelineStageRepository stages)
    {
        _bindings = bindings;
        _stages = stages;
    }

    public async Task<IReadOnlyList<ProcessStageBindingDto>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _bindings.ListAsync(cancellationToken).ConfigureAwait(false);
        var byCode = rows.ToDictionary(row => row.BindingCode, row => row.PipelineStageId, StringComparer.Ordinal);

        var stageIds = rows.Select(row => row.PipelineStageId).OfType<Guid>().Distinct().ToList();
        var stages = stageIds.Count == 0 ? [] : await _stages.GetManyAsync(stageIds, cancellationToken).ConfigureAwait(false);
        var names = stages.ToDictionary(stage => stage.Id, stage => stage.Name);

        return
        [
            .. ProcessStageBinding.Codes.Select(code =>
            {
                var stageId = byCode.GetValueOrDefault(code);
                return new ProcessStageBindingDto(code, stageId, stageId is { } id ? names.GetValueOrDefault(id) : null);
            }),
        ];
    }
}

/// <summary>One change of PUT <c>/settings/stage-bindings</c>.</summary>
public sealed record SetProcessStageBindingItem(string? BindingCode, Guid? PipelineStageId);

/// <summary>PUT <c>/settings/stage-bindings</c> (§10 #16).</summary>
/// <param name="Bindings">The codes to change; a code left out keeps its binding.</param>
public sealed record SetProcessStageBindingsCommand(IReadOnlyList<SetProcessStageBindingItem>? Bindings);

/// <summary>
/// Binds or unbinds pipeline stages. All or nothing: one unknown code or stage refuses the whole request. The next
/// pass reads the new bindings; §7.4 reconciles what they change.
/// </summary>
public sealed class SetProcessStageBindingsCommandHandler
{
    private readonly IProcessStageBindingRepository _bindings;
    private readonly IPipelineStageRepository _stages;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDatabaseFault _fault;
    private readonly ListProcessStageBindingsQueryHandler _list;

    public SetProcessStageBindingsCommandHandler(
        IProcessStageBindingRepository bindings,
        IPipelineStageRepository stages,
        IUnitOfWork unitOfWork,
        IDatabaseFault fault,
        ListProcessStageBindingsQueryHandler list)
    {
        _bindings = bindings;
        _stages = stages;
        _unitOfWork = unitOfWork;
        _fault = fault;
        _list = list;
    }

    public async Task<Result<IReadOnlyList<ProcessStageBindingDto>>> HandleAsync(SetProcessStageBindingsCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var items = command.Bindings;
        if (items is null
            || items.Any(item => item is null) // {"bindings":[null]} is valid JSON and binds to a null element.
            || items.Select(item => item.BindingCode).Distinct(StringComparer.Ordinal).Count() != items.Count)
        {
            return ProcessErrors.Invalid;
        }

        var stageIds = items.Select(item => item.PipelineStageId).OfType<Guid>().Distinct().ToList();
        var known = stageIds.Count == 0 ? [] : await _stages.GetManyAsync(stageIds, cancellationToken).ConfigureAwait(false);
        if (known.Count != stageIds.Count)
        {
            return ProcessErrors.Invalid;
        }

        var rows = await _bindings.ListAsync(cancellationToken).ConfigureAwait(false);
        var byCode = rows.ToDictionary(row => row.BindingCode, StringComparer.Ordinal);

        foreach (var item in items)
        {
            if (item.BindingCode is { } code && byCode.TryGetValue(code, out var row))
            {
                var bound = row.Bind(item.PipelineStageId);
                if (bound.IsFailure)
                {
                    return bound.Error;
                }

                continue;
            }

            var created = ProcessStageBinding.Create(item.BindingCode, item.PipelineStageId);
            if (created.IsFailure)
            {
                return created.Error;
            }

            _bindings.Add(created.Value);
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (_fault.IsStaleProcessWrite(exception))
        {
            return ProcessErrors.Stale;
        }

        return Result.Success(await _list.HandleAsync(cancellationToken).ConfigureAwait(false));
    }
}

/// <summary>One calendar exception (§6.10).</summary>
public sealed record ProcessHolidayDto(DateOnly Date, ProcessHolidayKind Kind, string Title)
{
    public static ProcessHolidayDto From(ProcessHoliday row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return new ProcessHolidayDto(row.Date, row.Kind, row.Title);
    }
}

/// <summary>GET <c>/holidays</c> (§10 #17).</summary>
/// <param name="Year">Only this year; empty — all of them.</param>
public sealed record ListProcessHolidaysQuery(int? Year);

/// <summary>The calendar in date order.</summary>
public sealed class ListProcessHolidaysQueryHandler
{
    private readonly IProcessHolidayRepository _holidays;

    public ListProcessHolidaysQueryHandler(IProcessHolidayRepository holidays) => _holidays = holidays;

    public async Task<Result<IReadOnlyList<ProcessHolidayDto>>> HandleAsync(ListProcessHolidaysQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Year is { } invalid && invalid is < 1 or > 9999)
        {
            return ProcessErrors.Invalid;
        }

        var rows = query.Year is { } year
            ? await _holidays.ListAsync(new DateOnly(year, 1, 1), new DateOnly(year, 12, 31), cancellationToken).ConfigureAwait(false)
            : await _holidays.ListAllAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success<IReadOnlyList<ProcessHolidayDto>>([.. rows.OrderBy(row => row.Date).Select(ProcessHolidayDto.From)]);
    }
}

/// <summary>POST <c>/holidays</c> (§10 #17).</summary>
public sealed record SaveProcessHolidayCommand(DateOnly? Date, ProcessHolidayKind? Kind, string? Title);

/// <summary>Adds the date's row or changes it: the date is its identity (§6.10). The next pass moves the due dates.</summary>
public sealed class SaveProcessHolidayCommandHandler
{
    private readonly IProcessHolidayRepository _holidays;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDatabaseFault _fault;

    public SaveProcessHolidayCommandHandler(IProcessHolidayRepository holidays, IUnitOfWork unitOfWork, IDatabaseFault fault)
    {
        _holidays = holidays;
        _unitOfWork = unitOfWork;
        _fault = fault;
    }

    public async Task<Result<ProcessHolidayDto>> HandleAsync(SaveProcessHolidayCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Date is not { } date || command.Kind is not { } kind || ProcessFreeText.HasNul(command.Title))
        {
            return ProcessErrors.Invalid;
        }

        var row = await _holidays.GetByDateAsync(date, cancellationToken).ConfigureAwait(false);

        if (row is null)
        {
            var created = ProcessHoliday.Create(date, kind, command.Title);
            if (created.IsFailure)
            {
                return created.Error;
            }

            row = created.Value;
            _holidays.Add(row);
        }
        else
        {
            var updated = row.Update(kind, command.Title);
            if (updated.IsFailure)
            {
                return updated.Error;
            }
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (_fault.IsStaleProcessWrite(exception))
        {
            return ProcessErrors.Stale;
        }

        return ProcessHolidayDto.From(row);
    }
}

/// <summary>DELETE <c>/holidays/{date}</c> (§10 #17): the date is an ordinary day again.</summary>
public sealed class DeleteProcessHolidayCommandHandler
{
    private readonly IProcessHolidayRepository _holidays;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDatabaseFault _fault;

    public DeleteProcessHolidayCommandHandler(IProcessHolidayRepository holidays, IUnitOfWork unitOfWork, IDatabaseFault fault)
    {
        _holidays = holidays;
        _unitOfWork = unitOfWork;
        _fault = fault;
    }

    public async Task<Result> HandleAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var row = await _holidays.GetByDateAsync(date, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return ProcessErrors.NotFound;
        }

        _holidays.Remove(row);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (_fault.IsStaleProcessWrite(exception))
        {
            return ProcessErrors.Stale;
        }

        return Result.Success();
    }
}
