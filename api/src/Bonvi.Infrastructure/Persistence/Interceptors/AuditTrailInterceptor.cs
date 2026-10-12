using Bonvi.Application.Abstractions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Infrastructure.Persistence.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Bonvi.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Har bir <c>SaveChanges</c> dagi entity o'zgarishlarini audit jurnaliga yozadi — kim, qachon,
/// qaysi amal, eski → yangi qiymat (SPEC-rollar-va-audit §7.1–§7.2).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega interceptor, use-case'larda qo'lda emas</b> — <see cref="AuditableInterceptor"/>
/// bilan aynan bir xil sabab: qo'lda yozilgan chaqiruv bitta joyda unutiladi va o'sha
/// joydagi o'zgarish izsiz qoladi. Bugungacha jurnalga faqat foydalanuvchi boshqaruvi
/// yozardi; ko'chirish, oklad, ishdan bo'shatish va «yozuvni tuzatish» ning eski
/// qiymati hech qayerda qolmasdi. Bu yerda chetlab o'tish yo'li yo'q: yoziladigan
/// jadvallarga yozishning hamma yo'li <c>SaveChanges</c> dan o'tadi. Kuzatuvni chetlab
/// o'tadigan yozuvlar (<c>ExecuteUpdate</c>/<c>ExecuteDelete</c>/xom SQL) faqat yozilmaydigan
/// jadvallarda (tokenlar, o'tishlar, MCP) va bo'lim daraxtining hisoblanadigan
/// <c>level</c>/<c>path</c> ida (SPEC §2.10).
/// </para>
/// <para>
/// <b>Tartib:</b> <see cref="AuditableInterceptor"/> dan KEYIN ro'yxatga olinadi — u
/// <c>Delete</c> ni yumshoq o'chirishga aylantirgan va <c>UpdatedAt</c> ni qo'ygan
/// bo'lishi kerak. Audit qatorlari o'zgarish bilan BITTA tranzaksiyada saqlanadi:
/// saqlash yiqilsa, ular kuzatuvdan chiqariladi — keyingi muvaffaqiyatli saqlash
/// «bo'lmagan» o'zgarishni yozib yubormasin.
/// </para>
/// <para>
/// <b>Jurnal faqat qo'shiladi.</b> Kuzatuvdagi <see cref="AuditLog"/> o'zgartirilsa yoki
/// o'chirilsa — istisno. Bazada ham trigger bor (<c>audit_logs_append_only</c>).
/// </para>
/// <para>
/// Qaysi entity yoziladi, qaysi maydon yashiriladi va qaysi biri tashlab ketiladi —
/// <see cref="AuditCatalog"/>. <c>IAuditLogger</c> navbatga qo'ygan hodisalar (kirish,
/// eksport, ruxsat rad etilishi) ham shu yerda, bir xil bajaruvchi va amal nomi bilan
/// qatorga aylanadi.
/// </para>
/// </remarks>
public sealed class AuditTrailInterceptor : SaveChangesInterceptor
{
    private readonly IRequestContext _request;
    private readonly IAuditActorSource _actors;
    private readonly IClock _clock;
    private readonly PendingAuditEvents _pending;
    private readonly AuditActorResolver _actorResolver = new();

    /// <summary>Joriy saqlash qo'shgan qatorlar — «saqlanyapti» va «saqlandi/yiqildi» orasida.</summary>
    private readonly List<AuditLog> _added = [];

    public AuditTrailInterceptor(
        IRequestContext request,
        IAuditActorSource actors,
        IClock clock,
        PendingAuditEvents pending)
    {
        _request = request;
        _actors = actors;
        _clock = clock;
        _pending = pending;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is not null)
        {
            // Sinxron yo'l: ichidagi so'rovlar ham sinxron (`async: false`), ya'ni
            // vazifa allaqachon tugagan holda qaytadi — bloklab kutish yo'q.
            CaptureAsync(eventData.Context, async: false, CancellationToken.None).GetAwaiter().GetResult();
        }

        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is not null)
        {
            await CaptureAsync(eventData.Context, async: true, cancellationToken).ConfigureAwait(false);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Release(eventData.Context);
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Release(eventData.Context);
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Release(eventData.Context);
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Release(eventData.Context);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    /// <summary>Saqlash BEKOR qilindi (bekor qilish belgisi) — yiqilish bilan bir xil: qatorlar chiqariladi.</summary>
    /// <remarks>
    /// EF bekor qilinganda <c>SaveChangesFailed</c> ni emas, shu hodisani chaqiradi. Usiz audit
    /// qatorlari kuzatuvda «qo'shilgan» bo'lib qolib, keyingi saqlashga bo'lmagan o'zgarish
    /// sifatida ilashib ketardi.
    /// </remarks>
    public override void SaveChangesCanceled(DbContextEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Release(eventData.Context);
        base.SaveChangesCanceled(eventData);
    }

    /// <inheritdoc cref="SaveChangesCanceled" />
    public override Task SaveChangesCanceledAsync(
        DbContextEventData eventData,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Release(eventData.Context);
        return base.SaveChangesCanceledAsync(eventData, cancellationToken);
    }

    /// <summary>
    /// Qo'shilgan qatorlarni kuzatuvdan chiqaradi — saqlangan bo'lsa ham, yiqilgan bo'lsa ham.
    /// </summary>
    /// <remarks>
    /// Yiqilganda — majburiy (SPEC §7.2): aks holda ular keyingi saqlashga ilashib
    /// ketardi. Saqlanganda — tejash: qator o'zgarmas va unga hech kim qaytmaydi,
    /// kuzatuvda qolsa esa uzun yashaydigan kontekstda (ETL — o'n minglab qator)
    /// har bir keyingi <c>DetectChanges</c> ni bekorga sekinlashtirardi.
    /// </remarks>
    private void Release(DbContext? context)
    {
        if (context is not null)
        {
            foreach (var log in _added)
            {
                context.Entry(log).State = EntityState.Detached;
            }
        }

        _added.Clear();
    }

    private async Task CaptureAsync(DbContext context, bool async, CancellationToken cancellationToken)
    {
        var tracker = context.ChangeTracker;
        tracker.DetectChanges();

        // Quyidagi `Entries()` va `Add` har safar o'zgarishlarni qayta qidirmasin —
        // yuqorida bir marta qidirildi.
        var autoDetect = tracker.AutoDetectChangesEnabled;
        tracker.AutoDetectChangesEnabled = false;

        try
        {
            var entries = tracker.Entries().ToList();
            var changes = new List<CapturedChange>();

            foreach (var entry in entries)
            {
                if (entry.Entity is AuditLog)
                {
                    if (entry.State is EntityState.Modified or EntityState.Deleted)
                    {
                        throw new InvalidOperationException(
                            "audit_logs: yozuvlar o'zgartirilmaydi va o'chirilmaydi — jurnal faqat qo'shiladi.");
                    }

                    continue;
                }

                if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                {
                    continue;
                }

                var rule = AuditCatalog.Find(entry.Metadata.ClrType);
                if (rule is null || rule.Skips(entry.Entity))
                {
                    continue;
                }

                if (AuditEntryReader.Read(entry, rule) is { } change)
                {
                    changes.Add(change);
                }
            }

            var events = _pending.Drain();
            if (changes.Count == 0 && events.Count == 0)
            {
                return;
            }

            var request = new RequestSnapshot(_request.Operation, _request.CorrelationId, _request.IpAddress, _request.UserAgent);
            var now = _clock.UtcNow;

            var subjects = await ResolveSubjectsAndLabelsAsync(context, entries, changes, async, cancellationToken)
                .ConfigureAwait(false);

            var actor = await _actorResolver
                .ResolveAsync(context, _actors.Current, async, cancellationToken)
                .ConfigureAwait(false);

            // Qatorlar HAMMA qidiruv tugagach qo'shiladi: o'rtada so'rov yiqilsa,
            // kuzatuvda yarim to'plam qolib ketmasin (saqlash boshlanmagan, ya'ni
            // `SaveChangesFailed` ham chaqirilmaydi).
            var rows = new List<AuditLog>(changes.Count + events.Count);

            for (var index = 0; index < changes.Count; index++)
            {
                var change = changes[index];
                var (subject, label) = subjects[index];

                rows.Add(Row(new AuditLogEntry(now, change.Action, change.EntityName)
                {
                    EntityId = AuditEntryReader.EntityIdOf(change.Entry),
                    SubjectEmployeeId = subject,
                    EntityLabel = label,
                    ChangedFields = change.ChangedFields,
                    OldValues = change.OldValues,
                    NewValues = change.NewValues,
                }, actor, request));
            }

            foreach (var auditEvent in events)
            {
                var eventActor = auditEvent.Actor is null
                    ? actor
                    : await _actorResolver.ResolveAsync(context, auditEvent.Actor, async, cancellationToken).ConfigureAwait(false);

                rows.Add(Row(new AuditLogEntry(auditEvent.OccurredAt, auditEvent.Action, auditEvent.EntityName)
                {
                    EntityId = auditEvent.EntityId,
                    OldValues = auditEvent.OldValues,
                    NewValues = auditEvent.NewValues,
                }, eventActor, request));
            }

            context.Set<AuditLog>().AddRange(rows);
            _added.AddRange(rows);
        }
        finally
        {
            tracker.AutoDetectChangesEnabled = autoDetect;
        }
    }

    /// <summary>Har bir o'zgarishning xodimi va yorlig'i — ikki bosqichda, to'plab.</summary>
    /// <remarks>
    /// Avval xodimning o'zi (buyruq → mehnat munosabati → xodim), keyin nomlar
    /// (xodim nomi uchun xodim allaqachon ma'lum bo'lishi kerak).
    /// </remarks>
    private static async Task<(Guid? Subject, string? Label)[]> ResolveSubjectsAndLabelsAsync(
        DbContext context,
        IReadOnlyList<EntityEntry> entries,
        List<CapturedChange> changes,
        bool async,
        CancellationToken cancellationToken)
    {
        var lookups = new AuditLookups(context, entries);

        foreach (var change in changes)
        {
            var rule = change.Rule;

            if (rule is { SubjectSource: AuditSubjectSource.Lookup, SubjectLookup: { } subjectLookup }
                && change.ReadGuid(rule.SubjectProperty) is { } subjectKey)
            {
                lookups.Request(subjectLookup, subjectKey);
            }

            if (rule.LabelLookup is { } labelLookup && change.ReadGuid(rule.LabelKeyProperty) is { } labelKey)
            {
                lookups.Request(labelLookup, labelKey);
            }
        }

        await lookups.ResolveAsync(async, cancellationToken).ConfigureAwait(false);

        var subjects = new Guid?[changes.Count];

        for (var index = 0; index < changes.Count; index++)
        {
            var change = changes[index];
            var rule = change.Rule;

            subjects[index] = rule.SubjectSource switch
            {
                AuditSubjectSource.Self => change.ReadGuid("Id"),
                AuditSubjectSource.Property => change.ReadGuid(rule.SubjectProperty),
                AuditSubjectSource.Lookup when change.ReadGuid(rule.SubjectProperty) is { } key && rule.SubjectLookup is { } lookup
                    => lookups.EmployeeOf(lookup, key),
                _ => null,
            };

            if (rule.UsesSubjectLabel && subjects[index] is { } employeeId && rule.OwnLabel(change.Entry.Entity) is null)
            {
                lookups.Request(AuditLookup.EmployeeName, employeeId);
            }
        }

        await lookups.ResolveAsync(async, cancellationToken).ConfigureAwait(false);

        var result = new (Guid? Subject, string? Label)[changes.Count];

        for (var index = 0; index < changes.Count; index++)
        {
            var change = changes[index];
            var rule = change.Rule;
            var subject = subjects[index];

            var label = rule.OwnLabel(change.Entry.Entity);

            if (label is null && rule.UsesSubjectLabel && subject is { } employeeId)
            {
                label = lookups.LabelOf(AuditLookup.EmployeeName, employeeId);
            }

            if (label is null && rule.LabelLookup is { } labelLookup && change.ReadGuid(rule.LabelKeyProperty) is { } labelKey)
            {
                label = lookups.LabelOf(labelLookup, labelKey);
            }

            result[index] = (subject, label);
        }

        return result;
    }

    private static AuditLog Row(AuditLogEntry entry, ResolvedActor actor, RequestSnapshot request)
        => AuditLog.RecordChange(entry with
        {
            ActorKind = actor.Kind,
            UserId = actor.UserId,
            UserLogin = actor.Login,
            ActorName = actor.Name,
            ActorRole = actor.RoleName,
            Operation = request.Operation,
            CorrelationId = request.CorrelationId,
            IpAddress = request.IpAddress,
            UserAgent = request.UserAgent,
        });

    /// <summary>So'rov konteksti — bitta saqlash uchun bir marta o'qiladi.</summary>
    private sealed record RequestSnapshot(string? Operation, Guid? CorrelationId, string? IpAddress, string? UserAgent);
}
