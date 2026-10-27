using Bonvi.Application.Employees.Probations;
using Bonvi.Application.Employees.Queries;
using Bonvi.Application.Recruiting.Candidates;
using Bonvi.Application.Recruiting.Vacancies;
using Bonvi.Application.Reports.Export;

namespace Bonvi.Application.Tests.Reports;

/// <summary>
/// Ro'yxat eksportlari qatorlarni ro'yxat handler'ining O'ZIDAN oladi —
/// <see cref="ExcelExport.MaxRows"/><c> + 1</c> o'lchamli bitta sahifa bilan.
/// </summary>
/// <remarks>
/// ⚠️ Handler sahifani o'z <c>MaxPageSize</c> i bilan qisadi. U eksport
/// chegarasidan kichik bo'lib qolsa (masalan kimdir yana 200 ga tushirsa),
/// «Total» to'g'ri qoladi va <c>eksport.juda_kop</c> ishlamaydi, fayl esa
/// birinchi sahifa bilan JIMGINA qirqiladi — SPEC-excel-eksport §1 aynan shuni
/// taqiqlaydi. Bu test o'sha kuni yiqiladi.
/// </remarks>
public sealed class ListExportRowLimitTests
{
    public static TheoryData<string, int> SahifaChegaralari => new()
    {
        { nameof(SearchCandidatesQueryHandler), SearchCandidatesQueryHandler.MaxPageSize },
        { nameof(ListVacanciesQueryHandler), ListVacanciesQueryHandler.MaxPageSize },
        { nameof(ListProbationsQueryHandler), ListProbationsQueryHandler.MaxPageSize },

        // Buyruqlar ro'yxati xodimlar ro'yxatining chegarasini oladi
        // (`ListEmploymentEventsQueryHandler` izohi).
        { "ListEmploymentEventsQueryHandler", SearchEmployeesQueryHandler.MaxPageSize },
    };

    [Theory]
    [MemberData(nameof(SahifaChegaralari))]
    public void Royxat_sahifasi_eksport_chegarasini_sigdiradi(string royxat, int maxPageSize)
        => maxPageSize.ShouldBeGreaterThan(
            ExcelExport.MaxRows,
            $"«{royxat}» sahifani eksport chegarasidan kichik qilib qisadi — fayl jimgina qirqilardi");
}
