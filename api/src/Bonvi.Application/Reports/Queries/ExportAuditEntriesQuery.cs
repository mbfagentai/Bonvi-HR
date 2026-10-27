using System.Text.Json;
using System.Text.Json.Serialization;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Audit;
using Bonvi.Application.Reports.Export;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Reports.Queries;

/// <summary>
/// «Audit jurnali»ni EKRANDAGI filtr bilan Excel faylga eksport qiladi
/// (<c>SPEC-excel-eksport.md</c> §2 #9).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Fayl ekrandan ko'proq narsa ko'rsatmaydi.</b> Filtr —
/// <see cref="ListAuditEntriesQueryHandler.BuildFilter"/>, so'rovchining ko'rinishi bilan
/// (<see cref="AuditVisibility.ViewerOf"/>): ko'rinmaydigan bo'lim qatorlari tushmaydi,
/// «Kirish va xavfsizlik» faqat <c>users.manage</c> bilan (ruxsatsiz — «o'zgarishlar»
/// bo'lib ishlaydi, ro'yxatdagidek), oklad va shaxsiy maydonlar ruxsatsiz qiymatsiz keladi.
/// </para>
/// <para>
/// Qatorlar tekis — ekrandagi guruh sarlavhalari (bitta harakatning bir nechta yozuvi) faylda
/// yo'q: har yozuv o'z qatorida, o'sha tartibda (eng yangisi birinchi). Matnlar —
/// <see cref="AuditEntryText"/> (ekrandagi qoidaning nusxasi).
/// </para>
/// <para>
/// IP manzil faylda YO'Q: u ro'yxat qatorida emas, faqat bitta yozuv tafsilotida
/// (<see cref="AuditEntryDetailsDto"/>) — fayl uchun qo'shimcha so'rov qilinmaydi (SPEC §1).
/// </para>
/// </remarks>
public sealed class ExportAuditEntriesQueryHandler
{
    private const string Slug = "audit";
    private const string SheetName = "Audit jurnali";

    private static readonly IReadOnlyList<ExcelColumn<AuditEntryDto>> Columns =
    [
        new("Vaqt", row => row.OccurredAt, ExcelCellKind.DateTime),
        new("Kim", row => AuditEntryText.ActorName(row.Actor)),
        new("Roli", row => AuditEntryText.ActorDetail(row.Actor)),
        new("Amal", AuditEntryText.Action),
        new("Nima", row => row.EntityLabel),
        new("Yozuv turi", row => AuditEntryText.EntityType(row.EntityName)),
        new("O'zgarishlar", row => AuditEntryText.Summary(row) is { Length: > 0 } summary ? summary : null, ExcelCellKind.LongText),
    ];

    private readonly IAuditReadModel _readModel;
    private readonly ICurrentUser _currentUser;
    private readonly IExcelTableWriter _writer;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ExportAuditEntriesQueryHandler(
        IAuditReadModel readModel,
        ICurrentUser currentUser,
        IExcelTableWriter writer,
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _readModel = readModel;
        _currentUser = currentUser;
        _writer = writer;
        _auditLogger = auditLogger;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<ExcelExportFile>> HandleAsync(
        ListAuditEntriesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // `MaxRows + 1`: chegaradan oshganini bilish uchun bittasi ortiqcha yetarli.
        var filter = ListAuditEntriesQueryHandler.BuildFilter(
            query,
            AuditVisibility.ViewerOf(_currentUser),
            _readModel,
            _clock.Today,
            page: 1,
            pageSize: ExcelExport.MaxRows + 1);
        if (filter.IsFailure)
        {
            return filter.Error;
        }

        var found = await _readModel.ListAsync(filter.Value, cancellationToken).ConfigureAwait(false);

        if (found.Total > ExcelExport.MaxRows)
        {
            return ExcelExport.TooMany(found.Total);
        }

        var rows = found.Items;
        var content = _writer.Write(SheetName, Columns, rows);

        // Eksportning o'z yozuvi faylga tushmaydi — u o'qishdan KEYIN saqlanadi.
        _auditLogger.Record(
            AuditAction.Export,
            nameof(AuditLog),
            entityId: null,
            newValues: JsonSerializer.Serialize(
                new ExportAuditDetail(ExportAuditFilters.Of(query), rows.Count),
                ExportAuditJson));

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new ExcelExportFile(content, ExcelExport.FileName(Slug, _clock.Today));
    }

    /// <summary>Bo'sh filtrlar yozilmaydi — tafsilot qisqa va o'qilarli bo'lsin.</summary>
    private static readonly JsonSerializerOptions ExportAuditJson = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Eksport hodisasining tafsiloti.</summary>
    /// <param name="Filters">Qo'llangan filtrlar.</param>
    /// <param name="Rows">Fayldagi qatorlar soni.</param>
    private sealed record ExportAuditDetail(ExportAuditFilters Filters, int Rows);

    /// <summary>
    /// Filtrlar — so'ralgani, matn bo'lib. Jurnalda erkin qidiruv yo'q: bajaruvchi — identifikator
    /// yoki <c>system</c>, ism emas, ya'ni shaxsiy ma'lumot yozilmaydi.
    /// </summary>
    private sealed record ExportAuditFilters(
        DateOnly? From,
        DateOnly? To,
        string? Actor,
        string? Module,
        string? Action,
        Guid? EmployeeId,
        string? Entity,
        string? EntityId,
        string? Kind)
    {
        public static ExportAuditFilters Of(ListAuditEntriesQuery query) => new(
            query.From,
            query.To,
            Blank(query.Actor),
            Blank(query.Module),
            Blank(query.Action),
            query.EmployeeId,
            Blank(query.Entity),
            Blank(query.EntityId),
            Blank(query.Kind));

        private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
