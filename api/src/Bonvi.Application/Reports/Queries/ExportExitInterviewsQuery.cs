using System.Text.Json;
using System.Text.Json.Serialization;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Feedback;
using Bonvi.Application.Feedback.Dtos;
using Bonvi.Application.Reports.Export;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Common;
using Bonvi.Domain.Feedback;

namespace Bonvi.Application.Reports.Queries;

/// <summary>
/// «Qayta aloqa» jurnalini EKRANDAGI filtr bilan Excel faylga eksport qiladi
/// (<c>SPEC-excel-eksport.md</c> §2 #10).
/// </summary>
/// <remarks>
/// <para>
/// Qatorlar ro'yxat handler'ining O'ZIDAN (<see cref="ListExitInterviewsQueryHandler"/>):
/// ro'yxat sahifalanmaydi, ya'ni ekran bilan fayl bitta chaqiruvning bitta natijasi.
/// Chegara baribir tekshiriladi — jurnal ishdan ketgan har bir odam bilan o'sadi.
/// </para>
/// <para>
/// ⚠️ Ro'yxat har qator uchun mehnat munosabati va nomlarni alohida o'qiydi
/// (<see cref="ExitInterviewMapper"/> izohi: 192 tarixiy yozuvga arzimaydi). Eksport buni
/// o'zgartirmaydi — fayl ekranning o'zi qilgan ishdan og'ir emas. Hajm o'sib, sezilarli bo'lsa,
/// o'sha mapper'ni proyeksiyaga almashtirish ikkalasini birga tezlatadi.
/// </para>
/// <para>
/// Ustun sarlavhalari ekrandagidek (SPEC jadvalidagi «Tavsiya qiladimi» emas, ekrandagi
/// «Tavsiya qilarmidi» — SPEC §1: sarlavha ekrandagi bilan bir xil). «Bo'limi» katagi ham
/// ekrandagidek: bo'lim bo'lmasa — firma.
/// </para>
/// </remarks>
public sealed class ExportExitInterviewsQueryHandler
{
    private const string Slug = "qayta_aloqa";
    private const string SheetName = "Qayta aloqa";

    private static readonly IReadOnlyList<ExcelColumn<ExitInterviewDto>> Columns =
    [
        new("F.I.Sh.", row => row.EmployeeName),
        new("Bo'limi", row => row.DepartmentName ?? row.LegalEntityName),
        new("Lavozimi", row => row.PositionName),
        new("Ketgan sana", row => row.TerminationDate, ExcelCellKind.Date),
        new("Suhbat sanasi", row => row.InterviewDate, ExcelCellKind.Date),
        new("Tavsiya qilarmidi", row => RecommendationLabel(row.WouldRecommend)),
        new("Ketish sababi", row => row.Reason, ExcelCellKind.LongText),
    ];

    private readonly ListExitInterviewsQueryHandler _list;
    private readonly IExcelTableWriter _writer;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ExportExitInterviewsQueryHandler(
        ListExitInterviewsQueryHandler list,
        IExcelTableWriter writer,
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _list = list;
        _writer = writer;
        _auditLogger = auditLogger;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<ExcelExportFile>> HandleAsync(
        ListExitInterviewsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var found = await _list.HandleAsync(query, cancellationToken).ConfigureAwait(false);

        if (found.IsFailure)
        {
            return found.Error;
        }

        var rows = found.Value;

        if (rows.Count > ExcelExport.MaxRows)
        {
            return ExcelExport.TooMany(rows.Count);
        }

        var content = _writer.Write(SheetName, Columns, rows);

        _auditLogger.Record(
            AuditAction.Export,
            nameof(ExitInterview),
            entityId: null,
            newValues: JsonSerializer.Serialize(
                new ExportAuditDetail(ExportAuditFilters.Of(query), rows.Count),
                ExportAuditJson));

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new ExcelExportFile(content, ExcelExport.FileName(Slug, _clock.Today));
    }

    /// <summary>Javob — ekrandagi belgi bilan bir xil; javob yo'q — bo'sh katak.</summary>
    private static string? RecommendationLabel(RecommendationAnswer? answer) => answer switch
    {
        null => null,
        RecommendationAnswer.Yes => "Ha",
        RecommendationAnswer.No => "Yo'q",
        RecommendationAnswer.DontKnow => "Bilmayman",
        _ => answer.ToString(),
    };

    /// <summary>Bo'sh filtrlar yozilmaydi — tafsilot qisqa va o'qilarli bo'lsin.</summary>
    private static readonly JsonSerializerOptions ExportAuditJson = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Eksport hodisasining tafsiloti.</summary>
    /// <param name="Filters">Qo'llangan filtrlar (qidiruv matnisiz).</param>
    /// <param name="Rows">Fayldagi qatorlar soni.</param>
    private sealed record ExportAuditDetail(ExportAuditFilters Filters, int Rows);

    /// <summary>Filtrlar — qidiruv matnining O'ZI emas, faqat «qidiruv bor edi».</summary>
    private sealed record ExportAuditFilters(bool? HasSearch, DateOnly? From, DateOnly? To)
    {
        public static ExportAuditFilters Of(ListExitInterviewsQuery query) => new(
            string.IsNullOrWhiteSpace(query.Search) ? null : true,
            query.From,
            query.To);
    }
}
