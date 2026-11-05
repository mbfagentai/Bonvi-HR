using Bonvi.Application.Processes.Engine;
using Microsoft.Extensions.Logging;

namespace Bonvi.Infrastructure.Processes.Engine;

/// <summary><see cref="IProcessEngineJournal"/> as structured log lines; nothing is stored.</summary>
public sealed partial class ProcessEngineJournal : IProcessEngineJournal
{
    private readonly ILogger<ProcessEngineJournal> _logger;

    public ProcessEngineJournal(ILogger<ProcessEngineJournal> logger) => _logger = logger;

    /// <inheritdoc />
    /// <remarks>Every pass ends here, every 5 minutes: only a pass that did something is worth a line above Debug.</remarks>
    public void PassFinished(int started, int changedRows)
    {
        if (started > 0 || changedRows > 0)
        {
            LogPassChanged(started, changedRows);
        }
        else
        {
            LogPassIdle();
        }
    }

    public void PassFailed(Exception exception) => LogPassFailed(exception);

    public void CaseProblem(Guid caseId, string processCode, string problem) => LogCaseProblem(caseId, processCode, problem);

    public void TemplateMissing(string processCode) => LogTemplateMissing(processCode);

    public void TemplateUnreadable(Guid versionId, Exception exception) => LogTemplateUnreadable(exception, versionId);

    public void GateFailedOpen(string operation, Guid employeeId, Exception exception) => LogGateFailedOpen(exception, operation, employeeId);

    [LoggerMessage(
        EventId = 2507,
        Level = LogLevel.Information,
        Message = "Process pass: {Started} cases started, {ChangedRows} rows changed")]
    private partial void LogPassChanged(int started, int changedRows);

    [LoggerMessage(
        EventId = 2508,
        Level = LogLevel.Debug,
        Message = "Process pass: nothing changed")]
    private partial void LogPassIdle();

    [LoggerMessage(
        EventId = 2509,
        Level = LogLevel.Error,
        Message = "Process pass failed; the case state stays as the last pass left it")]
    private partial void LogPassFailed(Exception exception);

    [LoggerMessage(
        EventId = 2510,
        Level = LogLevel.Warning,
        Message = "Process case {CaseId} ({ProcessCode}): {Problem}")]
    private partial void LogCaseProblem(Guid caseId, string processCode, string problem);

    [LoggerMessage(
        EventId = 2511,
        Level = LogLevel.Warning,
        Message = "Process {ProcessCode} has no Published template version; nothing starts for it")]
    private partial void LogTemplateMissing(string processCode);

    [LoggerMessage(
        EventId = 2512,
        Level = LogLevel.Error,
        Message = "Process template version {VersionId} cannot be read; its cases wait")]
    private partial void LogTemplateUnreadable(Exception exception, Guid versionId);

    [LoggerMessage(
        EventId = 2513,
        Level = LogLevel.Error,
        Message = "Process gate failed in warn mode; {Operation} of employee {EmployeeId} went on without it")]
    private partial void LogGateFailedOpen(Exception exception, string operation, Guid employeeId);
}
