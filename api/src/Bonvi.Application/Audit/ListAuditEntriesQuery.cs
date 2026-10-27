using System.Globalization;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Common;
using Bonvi.Domain.Identity;

namespace Bonvi.Application.Audit;

/// <summary>
/// Audit jurnali — «kim, qachon, nimani o'zgartirdi» (SPEC-rollar-va-audit §8.3, §9.5).
/// </summary>
/// <param name="From">Davr boshi (Toshkent sanasi, kiradi).</param>
/// <param name="To">Davr oxiri (Toshkent sanasi, kiradi).</param>
/// <param name="Actor">Foydalanuvchi identifikatori yoki <c>system</c>.</param>
/// <param name="Module">Bo'lim kaliti (<c>employment</c>, <c>attendance</c>…).</param>
/// <param name="Action">Amal nomi (<c>Update</c>, <c>Login</c>…).</param>
/// <param name="EmployeeId">Xodim — uning kartasiga tegishli qatorlar.</param>
/// <param name="Entity">Entity turi (<c>EmploymentEvent</c>).</param>
/// <param name="EntityId">Bitta yozuv.</param>
/// <param name="Kind"><c>changes</c> (sukut) | <c>security</c> | <c>all</c>.</param>
/// <param name="Page">Sahifa, 1 dan.</param>
/// <param name="PageSize">Sahifa hajmi (sukut 50, eng ko'pi 200).</param>
public sealed record ListAuditEntriesQuery(
    DateOnly? From = null,
    DateOnly? To = null,
    string? Actor = null,
    string? Module = null,
    string? Action = null,
    Guid? EmployeeId = null,
    string? Entity = null,
    string? EntityId = null,
    string? Kind = null,
    int? Page = null,
    int? PageSize = null);

public sealed class ListAuditEntriesQueryHandler
{
    /// <summary>Davr berilmasa — oxirgi shuncha kun (bugun ham kiradi).</summary>
    public const int DefaultPeriodDays = 30;

    private readonly IAuditReadModel _readModel;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public ListAuditEntriesQueryHandler(IAuditReadModel readModel, ICurrentUser currentUser, IClock clock)
    {
        _readModel = readModel;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<PagedResult<AuditEntryDto>>> HandleAsync(
        ListAuditEntriesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var (page, pageSize) = AuditFilterRules.Paging(query.Page, query.PageSize);

        var filter = BuildFilter(query, AuditVisibility.ViewerOf(_currentUser), _readModel, _clock.Today, page, pageSize);
        if (filter.IsFailure)
        {
            return filter.Error;
        }

        var found = await _readModel.ListAsync(filter.Value, cancellationToken).ConfigureAwait(false);

        return AuditFilterRules.ToPaged(found, page, pageSize);
    }

    /// <summary>
    /// So'rovni tekshiradi va filtrga aylantiradi — jurnal ham, Excel eksporti ham AYNAN shu
    /// metodni chaqiradi.
    /// </summary>
    /// <remarks>
    /// <c>internal</c>: <see cref="Reports.Queries.ExportAuditEntriesQueryHandler"/> o'z nusxasini
    /// yozmaydi — ko'rinish qoidasi (xavfsizlik hodisalari faqat <c>users.manage</c> bilan) ham
    /// shu yerda, ya'ni fayl ekrandan ko'proq qator ololmaydi. Sahifa tashqaridan: ekran uni
    /// 200 bilan qisadi, eksport esa chegara + 1 so'raydi.
    /// </remarks>
    /// <param name="query">Ekrandagi so'rov.</param>
    /// <param name="viewer">So'rovchining ko'rinishi (<see cref="AuditVisibility.ViewerOf"/>).</param>
    /// <param name="readModel">Entity nomini kanonik ko'rinishga keltirish uchun.</param>
    /// <param name="today">Bugun (Toshkent) — davr berilmaganda.</param>
    /// <param name="page">Sahifa, 1 dan.</param>
    /// <param name="pageSize">Sahifa hajmi.</param>
    internal static Result<AuditListFilter> BuildFilter(
        ListAuditEntriesQuery query,
        AuditViewer viewer,
        IAuditReadModel readModel,
        DateOnly today,
        int page,
        int pageSize)
    {
        var kind = AuditFilterRules.ParseKind(query.Kind);
        if (kind.IsFailure)
        {
            return kind.Error;
        }

        var actor = AuditFilterRules.ParseActor(query.Actor);
        if (actor.IsFailure)
        {
            return actor.Error;
        }

        var module = AuditFilterRules.ParseModule(query.Module);
        if (module.IsFailure)
        {
            return module.Error;
        }

        var action = AuditFilterRules.ParseAction(query.Action);
        if (action.IsFailure)
        {
            return action.Error;
        }

        string? entity = null;
        if (!string.IsNullOrWhiteSpace(query.Entity))
        {
            entity = readModel.FindEntityName(query.Entity.Trim());
            if (entity is null)
            {
                return AuditErrors.InvalidFilter("entity");
            }
        }

        // Davr DOIM chegaralangan (SPEC §7.8 «o'qish narxi»): oxiri berilmasa — bugun,
        // boshi berilmasa — oxiridan 30 kun oldin. Faqat bittasi berilsa ham qolgani
        // shu qoida bilan to'ladi, ya'ni jurnal hech qachon boshidan oxirigacha
        // o'qilmaydi.
        //
        // Aql doirasidan tashqari sana (0001-01-01, 9999-12-31) — 400: `AddDays` va kun
        // chegarasini hisoblash DateOnly chegarasidan oshib, 500 berardi.
        if (!AuditFilterRules.IsSaneDate(query.From) || !AuditFilterRules.IsSaneDate(query.To))
        {
            return AuditErrors.InvalidPeriod;
        }

        var to = query.To ?? today;
        var from = query.From ?? to.AddDays(-(DefaultPeriodDays - 1));

        if (from > to)
        {
            return AuditErrors.InvalidPeriod;
        }

        // `security`/`all` — faqat `users.manage` bilan; ruxsatsiz so'rov xato emas,
        // `changes` bo'lib ishlaydi (SPEC §8.3): segment yashirin bo'lgan eski havola
        // ochilganda ham sahifa ochiladi.
        var (changes, security) = kind.Value;
        if (!viewer.Security)
        {
            (changes, security) = (true, false);
        }

        return new AuditListFilter(
            viewer,
            StartOfDay(from),
            StartOfDay(to.AddDays(1)),
            changes,
            security,
            actor.Value.UserId,
            actor.Value.System,
            module.Value,
            action.Value,
            query.EmployeeId,
            entity,
            string.IsNullOrWhiteSpace(query.EntityId) ? null : query.EntityId.Trim(),
            page,
            pageSize);
    }

    /// <summary>Toshkent kunining boshi — UTC lahza (Npgsql <c>timestamptz</c> ga faqat UTC yozadi).</summary>
    private static DateTimeOffset StartOfDay(DateOnly date)
        => new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), BonviTime.TashkentOffset).ToUniversalTime();
}

/// <summary>
/// Jurnal so'rovlarining umumiy filtr qoidalari — ro'yxat va xodim tarixi BIR XIL
/// qoidada ishlashi uchun bitta joyda.
/// </summary>
internal static class AuditFilterRules
{
    /// <summary>Sukut bo'yicha sahifa hajmi.</summary>
    public const int DefaultPageSize = 50;

    /// <summary>Eng katta sahifa hajmi.</summary>
    public const int MaxPageSize = 200;

    /// <summary><c>kind</c> → (o'zgarishlar, xavfsizlik).</summary>
    public static Result<(bool Changes, bool Security)> ParseKind(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return (true, false);
        }

        return kind.Trim().ToLowerInvariant() switch
        {
            "changes" => (true, false),
            "security" => (false, true),
            "all" => (true, true),
            _ => AuditErrors.InvalidFilter("kind"),
        };
    }

    /// <summary><c>actor</c> → foydalanuvchi yoki tizim.</summary>
    public static Result<(Guid? UserId, bool System)> ParseActor(string? actor)
    {
        if (string.IsNullOrWhiteSpace(actor))
        {
            return ((Guid?)null, false);
        }

        var value = actor.Trim();

        if (string.Equals(value, "system", StringComparison.OrdinalIgnoreCase))
        {
            return ((Guid?)null, true);
        }

        return Guid.TryParse(value, CultureInfo.InvariantCulture, out var userId) && userId != Guid.Empty
            ? ((Guid?)userId, false)
            : AuditErrors.InvalidFilter("actor");
    }

    /// <summary><c>module</c> → bo'lim kaliti (kichik harf bilan).</summary>
    public static Result<string?> ParseModule(string? module)
    {
        if (string.IsNullOrWhiteSpace(module))
        {
            return Result.Success<string?>(null);
        }

        var key = module.Trim().ToLowerInvariant();

        return AccessModuleKeys.All.Contains(key)
            ? Result.Success<string?>(key)
            : AuditErrors.InvalidFilter("module");
    }

    /// <summary><c>action</c> → amal (faqat NOMI bilan, raqami bilan emas).</summary>
    /// <remarks>
    /// <c>Enum.TryParse</c> raqamni ham qabul qiladi (<c>"99"</c> → mavjud bo'lmagan a'zo);
    /// bazadagi raqam esa ichki tafsilot — shartnomada faqat nom.
    /// </remarks>
    public static Result<AuditAction?> ParseAction(string? action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return Result.Success<AuditAction?>(null);
        }

        var value = action.Trim();

        // Faqat harflar: raqam ichki tafsilot, «Create,Update» esa bayroq sifatida 1|2 = 3
        // (Delete) bo'lib o'qilardi — ikkalasi ham 400.
        return value.All(char.IsAsciiLetter)
               && Enum.TryParse<AuditAction>(value, ignoreCase: true, out var parsed)
               && Enum.IsDefined(parsed)
            ? Result.Success<AuditAction?>(parsed)
            : AuditErrors.InvalidFilter("action");
    }

    /// <summary>Sana aql doirasidami (2000–2100) — berilmagan sana ham to'g'ri.</summary>
    /// <param name="date">So'rovdagi sana.</param>
    public static bool IsSaneDate(DateOnly? date)
        => date is not { } value || value.Year is >= 2000 and <= 2100;

    /// <summary>Sahifa 1 dan, hajm 1…200 (sukut 50) — chegaradan tashqarisi jimgina qisiladi.</summary>
    /// <remarks>Buyruqlar ro'yxati (<c>ListEmploymentEventsQuery</c>) bilan bir xil xulq.</remarks>
    public static (int Page, int PageSize) Paging(int? page, int? pageSize)
        => (page is { } requested && requested > 0 ? requested : 1,
            Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize));

    public static Result<PagedResult<AuditEntryDto>> ToPaged(AuditPage found, int page, int pageSize)
    {
        var pageCount = (found.Total + pageSize - 1) / pageSize;

        return Result.Success(new PagedResult<AuditEntryDto>(found.Items, page, pageSize, found.Total, pageCount));
    }
}
