using System.Text.Json;
using System.Text.Json.Serialization;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Employees.Abstractions;
using Bonvi.Application.Employees.Incentives;
using Bonvi.Application.Reports.Abstractions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;

namespace Bonvi.Application.Reports.Queries;

/// <summary>Excel faylga aylantirilgan rag'bat va jarima ro'yxati.</summary>
/// <param name="Content"><c>.xlsx</c> faylining baytlari.</param>
/// <param name="FileName">Yuklab olinadigan fayl nomi.</param>
public sealed record IncentiveExportFile(byte[] Content, string FileName);

/// <summary>
/// «Rag'bat va jarima» ro'yxatini, EKRANDAGI bilan bir xil filtr va saralash
/// bilan, Excel faylga eksport qiladi.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ExportEmployeesQueryHandler"/> naqshi: kirish — AYNAN
/// <see cref="ListIncentivesQuery"/>, filtr —
/// <see cref="ListIncentivesQueryHandler.BuildFilter"/> orqali. Ro'yxatga
/// qo'shilgan har bir yangi filtr eksportga ham o'zi o'tadi.
/// </para>
/// <para>
/// Sahifalanmaydi: mos yozuvlarning HAMMASI bitta faylda. Chegaradan oshsa
/// fayl jimgina qirqilmaydi — <c>eksport.juda_kop</c> qaytadi.
/// </para>
/// <para>
/// Har bir eksport jurnalga yoziladi (<c>Export</c>): filtrlar va qatorlar
/// soni, qidiruv matnining O'ZI emas — unda xodimning ismi bo'ladi.
/// </para>
/// </remarks>
public sealed class ExportIncentivesQueryHandler
{
    /// <summary>Bitta eksportdagi eng ko'p qator — xodimlar eksporti bilan bir xil chegara.</summary>
    public const int MaxExportRows = ExportEmployeesQueryHandler.MaxExportRows;

    private readonly IIncentiveReadModel _readModel;
    private readonly IIncentiveExcelWriter _writer;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ExportIncentivesQueryHandler(
        IIncentiveReadModel readModel,
        IIncentiveExcelWriter writer,
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _readModel = readModel;
        _writer = writer;
        _auditLogger = auditLogger;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<IncentiveExportFile>> HandleAsync(
        ListIncentivesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // `MaxExportRows + 1`: chegaradan oshganini bilish uchun bittasi ortiqcha yetarli.
        var filter = ListIncentivesQueryHandler.BuildFilter(query, page: 1, pageSize: MaxExportRows + 1);

        var found = await _readModel.ListIncentivesAsync(filter, cancellationToken).ConfigureAwait(false);

        if (found.Total > MaxExportRows)
        {
            return Error.Validation(
                "eksport.juda_kop",
                $"Tanlangan filtrga {found.Total} yozuv mos keladi — eksport chegarasi " +
                $"{MaxExportRows}. Filtrni toraytirib (masalan sana yoki bo'lim bo'yicha) qayta urinib ko'ring.");
        }

        var content = _writer.Write(found.Items);
        var fileName = $"Bonvi_ragbat_jarima_{_clock.Today:yyyy-MM-dd}.xlsx";

        _auditLogger.Record(
            AuditAction.Export,
            nameof(Incentive),
            entityId: null,
            newValues: JsonSerializer.Serialize(
                new ExportAuditDetail(ExportAuditFilters.Of(query), found.Items.Count),
                ExportAuditJson));

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new IncentiveExportFile(content, fileName);
    }

    /// <summary>Bo'sh filtrlar yozilmaydi — tafsilot qisqa va o'qilarli bo'lsin.</summary>
    private static readonly JsonSerializerOptions ExportAuditJson = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Eksport hodisasining tafsiloti.</summary>
    /// <param name="Filters">Qo'llangan filtrlar (qidiruv matnisiz).</param>
    /// <param name="Rows">Fayldagi qatorlar soni.</param>
    private sealed record ExportAuditDetail(ExportAuditFilters Filters, int Rows);

    /// <summary>Filtrlar — shaxsiy ma'lumotsiz.</summary>
    private sealed record ExportAuditFilters(
        bool? HasSearch,
        IncentiveKind? Kind,
        DateOnly? From,
        DateOnly? To,
        Guid? DepartmentId,
        Guid? LegalEntityId,
        string? Sort)
    {
        public static ExportAuditFilters Of(ListIncentivesQuery query) => new(
            string.IsNullOrWhiteSpace(query.Search) ? null : true,
            query.Kind,
            query.From,
            query.To,
            query.DepartmentId,
            query.LegalEntityId,
            query.Sort);
    }
}
