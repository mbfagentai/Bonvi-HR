using Bonvi.Application.Employees.Abstractions;

namespace Bonvi.Application.Reports.Abstractions;

/// <summary>
/// Rag'bat va jarima ro'yxatini Excel (<c>.xlsx</c>) faylga yozadigan port.
/// </summary>
/// <remarks>
/// <see cref="IEmployeeExcelWriter"/> bilan bir xil qaror: <c>ClosedXML</c> —
/// Infrastructure tafsiloti, use-case uni bilmaydi. Qatorlar o'qish modelidan
/// tayyor keladi; summa <c>incentives.view</c> ostida ochiq, ya'ni yozuvchi
/// hech narsani yashirmaydi (<c>ListIncentivesQueryHandler.Amount</c> izohi).
/// </remarks>
public interface IIncentiveExcelWriter
{
    /// <summary>Qatorlarni bitta <c>.xlsx</c> faylining baytlariga aylantiradi.</summary>
    byte[] Write(IReadOnlyList<IncentiveListRow> rows);
}
