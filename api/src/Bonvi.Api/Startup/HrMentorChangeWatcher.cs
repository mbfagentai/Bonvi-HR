using Bonvi.Application.Blanks;

namespace Bonvi.Api.Startup;

/// <summary>
/// HRMentor dagi o'zgarishni doim kutib turadigan halqa (SPEC-hujjat-manbai §7, HRMentor SPEC §21.4 R4):
/// HR hujjat, biriktirish yoki anketa matnini saqlashi bilan sinxronlash soniyalar ichida boshlanadi.
/// </summary>
/// <remarks>
/// <para>
/// Har qadam o'z <c>scope</c> ida (<see cref="HrMentorChangeWatch"/> — baza, sozlama, sinxronlash);
/// halqa faqat oxirgi reviziyani va ketma-ket xatolar sonini eslaydi. Oraliqlar —
/// <see cref="HrMentorWatchDelay"/>.
/// </para>
/// <para>
/// <b>Xavfsizlik to'ri:</b> besh daqiqalik <c>hrmentor:sync</c> joyida qoladi — halqa yiqilsa ham
/// HRMentor dagi o'zgarish ko'pi bilan besh daqiqada keladi. Jurnalga faqat natija va KOD tushadi;
/// halqa hech qachon to'xtamaydi, faqat ilova to'xtaganda.
/// </para>
/// </remarks>
public sealed partial class HrMentorChangeWatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<HrMentorChangeWatcher> _logger;

    public HrMentorChangeWatcher(IServiceScopeFactory scopes, ILogger<HrMentorChangeWatcher> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string? since = null;
        var inARow = 0;
        HrMentorWatchOutcome? last = null;
        HrMentorWatchOutcome? lastKind = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            HrMentorWatchStep step;
            try
            {
                var scope = _scopes.CreateAsyncScope();
                await using (scope.ConfigureAwait(false))
                {
                    var watch = scope.ServiceProvider.GetRequiredService<HrMentorChangeWatch>();
                    step = await watch.RunOnceAsync(since, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Faqat turi: xabar matnida manzil bo'lishi mumkin.
                LogStepCrashed(ex.GetType().Name);
                step = new HrMentorWatchStep(HrMentorWatchOutcome.Failed, since, "hrmentor.watch_crashed");
            }

            since = step.Since;

            // Xatolar bitta tur: sinxronlash yiqilishi va so'rov yiqilishi bir oraliqni o'stiradi;
            // doimiy xato qolgan sinxronlash — sinxronlash: ketma-ket kelsa birga sekinlashadi.
            var kind = step.Outcome switch
            {
                HrMentorWatchOutcome.SyncFailed => HrMentorWatchOutcome.Failed,
                HrMentorWatchOutcome.SyncedWithError => HrMentorWatchOutcome.Synced,
                _ => step.Outcome,
            };
            inARow = kind == lastKind ? inARow + 1 : 1;
            lastKind = kind;

            if (step.Outcome == HrMentorWatchOutcome.Synced)
            {
                LogSynced();
            }
            else if (step.Outcome == HrMentorWatchOutcome.SyncedWithError)
            {
                LogSyncedWithError(step.ErrorCode ?? "-");
            }
            else if (step.Outcome == HrMentorWatchOutcome.Unchanged)
            {
                if (last is not (null or HrMentorWatchOutcome.Synced or HrMentorWatchOutcome.SyncedWithError or HrMentorWatchOutcome.Unchanged))
                {
                    LogResumed();
                }
            }
            else if (step.Outcome != last)
            {
                // Faqat holat almashganda — besh daqiqada bir xil ogohlantirish jurnalni to'ldirmasin.
                LogState(step.Outcome, step.ErrorCode ?? "-");
            }

            last = step.Outcome;

            var delay = HrMentorWatchDelay.After(step.Outcome, inARow);
            if (delay <= TimeSpan.Zero)
            {
                continue;
            }

            try
            {
                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    [LoggerMessage(
        EventId = 4810,
        Level = LogLevel.Information,
        Message = "HRMentor: o'zgarish keldi — sinxronlandi")]
    private partial void LogSynced();

    [LoggerMessage(
        EventId = 4814,
        Level = LogLevel.Warning,
        Message = "HRMentor: o'zgarish keldi — sinxronlandi, lekin {ErrorCode} qoldi (HR saqlagan narsa qabul qilinmadi); uni besh daqiqalik yurish qayta tekshiradi")]
    private partial void LogSyncedWithError(string errorCode);

    [LoggerMessage(
        EventId = 4811,
        Level = LogLevel.Warning,
        Message = "HRMentor: o'zgarishni kutish holati {Outcome} ({ErrorCode}) — besh daqiqalik yurish ishlayveradi")]
    private partial void LogState(HrMentorWatchOutcome outcome, string errorCode);

    [LoggerMessage(
        EventId = 4813,
        Level = LogLevel.Information,
        Message = "HRMentor: o'zgarishni kutish yana ishlayapti")]
    private partial void LogResumed();

    [LoggerMessage(
        EventId = 4812,
        Level = LogLevel.Error,
        Message = "HRMentor: o'zgarishni kutish qadami kutilmagan xato bilan yiqildi ({ExceptionType}) — keyinroq qayta")]
    private partial void LogStepCrashed(string exceptionType);
}
