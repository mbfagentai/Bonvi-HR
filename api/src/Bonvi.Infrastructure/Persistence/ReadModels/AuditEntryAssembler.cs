using System.Text.Json;
using Bonvi.Application.Audit;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Employees;
using Bonvi.Infrastructure.Persistence.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Bonvi.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Sahifadagi jurnal qatorlarini DTO'ga aylantiradi: maydonlarni o'qiydi, ruxsatga qarab
/// yashiradi va havolalarga nom qo'yadi (SPEC-rollar-va-audit §7.10, §8.3).
/// </summary>
/// <remarks>
/// <para>
/// <b>To'rt bosqich, so'rovlar to'plab:</b> (1) har bir qatorning maydonlari;
/// (2) buyruq TURI — qo'shimcha haqni okladdan ajratish uchun, tahrir qatorida u yo'q
/// bo'lsa bazadan, hamma qatorga bitta so'rov; (3) yashirish va havolalarni yig'ish;
/// (4) nomlar — har bir jadvalga BITTA so'rov, o'chirilganlari bilan. 200 qatorli
/// sahifa ham o'nta atrofida so'rov bilan quriladi, N+1 yo'q.
/// </para>
/// <para>
/// <b>Yashirilgan qiymat — ikki manbadan:</b> so'rovchida ruxsat yo'q (oklad, qo'shimcha
/// haq, shaxsiy maydon) yoki maydon yozish paytidayoq sir bo'lib yozilgan
/// (<see cref="AuditCatalog.MaskedValue"/>). Ikkalasida ham qiymat, nom va yorliq
/// <c>null</c>, maydon NOMI esa qoladi: «oklad shu kuni o'zgargan» — ko'rinadi, summa — yo'q.
/// </para>
/// </remarks>
internal sealed class AuditEntryAssembler
{
    private readonly AppDbContext _context;
    private readonly AuditViewer _viewer;
    private readonly AuditLabelLoader _labels;

    public AuditEntryAssembler(AppDbContext context, AuditViewer viewer)
    {
        _context = context;
        _viewer = viewer;
        _labels = new AuditLabelLoader(context);
    }

    public async Task<IReadOnlyList<AuditEntryDto>> BuildAsync(
        IReadOnlyList<AuditLog> rows,
        CancellationToken cancellationToken)
    {
        var drafts = rows.Select(Draft).ToList();

        await ResolveDiscriminatorsAsync(drafts, cancellationToken).ConfigureAwait(false);

        var changes = drafts.Select(Classify).ToList();

        // Kengaytirishdan oldingi qatorlarda bajaruvchi ismining nusxasi yo'q — foydalanuvchining
        // hozirgi ismi qo'yiladi. Rol nomi esa QO'YILMAYDI: u o'sha lahzadagi nusxa bo'lishi
        // kerak, hozirgisi tarixni qayta yozardi.
        foreach (var draft in drafts)
        {
            if (draft.Log.ActorName is null && draft.Log.UserId is { } userId)
            {
                _labels.Request(AuditRefTarget.User, userId);
            }
        }

        await _labels.LoadAsync(cancellationToken).ConfigureAwait(false);

        var result = new List<AuditEntryDto>(drafts.Count);

        for (var index = 0; index < drafts.Count; index++)
        {
            var draft = drafts[index];
            var log = draft.Log;

            result.Add(new AuditEntryDto(
                log.Id,
                log.OccurredAt,
                log.Action,
                log.Operation,
                AuditActionGroups.ModuleOfEvent(log.Action, log.EntityName) ?? draft.Rule?.Module,
                log.EntityName,
                log.EntityId,
                log.EntityLabel,
                log.SubjectEmployeeId,
                new AuditActorDto(
                    log.ActorKind,
                    log.UserId,
                    log.UserLogin,
                    log.ActorName ?? (log.UserId is { } userId ? _labels.NameOf(AuditRefTarget.User, userId) : null),
                    log.ActorRole),
                [.. changes[index].Select(Finish)],
                log.CorrelationId));
        }

        return result;
    }

    /// <summary>(1) Qatorning maydonlari va yozuv turi (bo'lsa).</summary>
    private RowDraft Draft(AuditLog log)
    {
        // Katalog qoidasi faqat ENTITY o'zgarishiga: kirish hodisasi ham `User` nomida
        // yoziladi, lekin uning tafsiloti (`reason`) foydalanuvchining maydoni emas.
        var rule = AuditActionGroups.Changes.Contains(log.Action) ? AuditCatalog.FindByName(log.EntityName) : null;
        var entityType = rule is null ? null : _context.Model.FindEntityType(rule.EntityType);

        var fields = AuditChangeReader.Read(entityType, log.Action, log.OldValues, log.NewValues, log.ChangedFields);

        string? discriminator = null;
        var needsLookup = false;

        if (rule?.ExtraPay is { } extra
            && fields.Any(field => string.Equals(field.Name, extra.Field, StringComparison.OrdinalIgnoreCase)))
        {
            discriminator = AuditChangeReader.TextOf(fields, extra.Discriminator);
            needsLookup = discriminator is null;
        }

        return new RowDraft(log, rule, entityType, fields) { Discriminator = discriminator, NeedsDiscriminator = needsLookup };
    }

    /// <summary>
    /// (2) Tahrir qatorida yozuv turi yo'q (u o'zgarmagan) — bazadan, bitta so'rov bilan.
    /// </summary>
    /// <remarks>
    /// Hozir bunday maydon yagona — buyruqning <c>ToSalary</c> si (qo'shimcha vazifada — haq).
    /// Katalogga yangi <c>ExtraPayWhen</c> qo'shilsa-yu, bu yerda uning jadvali bo'lmasa, tur
    /// noma'lum qoladi va maydon qo'shimcha haq deb YASHIRILADI — xato tomonga emas, ehtiyot
    /// tomonga.
    /// </remarks>
    private async Task ResolveDiscriminatorsAsync(List<RowDraft> drafts, CancellationToken cancellationToken)
    {
        var eventIds = drafts
            .Where(draft => draft.NeedsDiscriminator && draft.Rule?.EntityType == typeof(EmploymentEvent))
            .Select(draft => Guid.TryParse(draft.Log.EntityId, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (eventIds.Count == 0)
        {
            return;
        }

        var types = await _context.EmploymentEvents
            .AsNoTracking()
            .Where(employmentEvent => eventIds.Contains(employmentEvent.Id))
            .Select(employmentEvent => new { employmentEvent.Id, employmentEvent.Type })
            .ToDictionaryAsync(row => row.Id, row => row.Type.ToString(), cancellationToken)
            .ConfigureAwait(false);

        foreach (var draft in drafts.Where(draft => draft.NeedsDiscriminator))
        {
            if (Guid.TryParse(draft.Log.EntityId, out var id) && types.TryGetValue(id, out var type))
            {
                draft.Discriminator = type;
            }
        }
    }

    /// <summary>(3) Har bir maydon: turi, ko'rinadimi va havolasi.</summary>
    private List<ChangeDraft> Classify(RowDraft draft)
    {
        var result = new List<ChangeDraft>(draft.Fields.Count);

        foreach (var field in draft.Fields)
        {
            var valueType = AuditChangeReader.ValueTypeOf(draft.EntityType, draft.Rule, field);
            var fieldClass = draft.Rule?.ClassOf(field.Name, draft.Discriminator) ?? AuditFieldClass.Open;

            if (!CanSee(fieldClass) || AuditChangeReader.IsWriteMasked(field))
            {
                result.Add(new ChangeDraft(field.Name, valueType, Old: null, New: null, Target: null, Masked: true));
                continue;
            }

            var target = draft.Rule?.RefOf(field.Name);
            if (target is { } refTarget)
            {
                _labels.Request(refTarget, field.Old);
                _labels.Request(refTarget, field.New);
            }

            result.Add(new ChangeDraft(field.Name, valueType, field.Old, field.New, target, Masked: false));
        }

        return result;
    }

    private bool CanSee(AuditFieldClass fieldClass) => fieldClass switch
    {
        AuditFieldClass.Personal => _viewer.PersonalData,
        AuditFieldClass.Salary => _viewer.Salary,
        AuditFieldClass.ExtraPay => _viewer.ExtraPay,
        _ => true,
    };

    /// <summary>(4) Havola nomlari bilan yakuniy DTO.</summary>
    private AuditChangeDto Finish(ChangeDraft change)
    {
        string? oldLabel = null;
        string? newLabel = null;

        if (change.Target is { } target)
        {
            oldLabel = _labels.LabelOf(target, change.Old);
            newLabel = _labels.LabelOf(target, change.New);
        }

        return new AuditChangeDto(change.Field, change.ValueType, change.Old, change.New, oldLabel, newLabel, change.Masked);
    }

    private sealed record RowDraft(
        AuditLog Log,
        AuditEntityRule? Rule,
        IReadOnlyEntityType? EntityType,
        IReadOnlyList<AuditFieldValue> Fields)
    {
        public string? Discriminator { get; set; }

        public bool NeedsDiscriminator { get; init; }
    }

    private sealed record ChangeDraft(
        string Field,
        string ValueType,
        JsonElement? Old,
        JsonElement? New,
        AuditRefTarget? Target,
        bool Masked);
}
