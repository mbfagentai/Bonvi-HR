using System.Text;
using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;
using Bonvi.Etl;
using Bonvi.Etl.Excel;
using Bonvi.Etl.Mapping;
using Bonvi.Etl.Reporting;
using Bonvi.Etl.Runtime;
using ClosedXML.Excel;

namespace Bonvi.Etl.Tests;

/// <summary>
/// Ish maydonining qaror qabul qilish qoidalari.
/// </summary>
/// <remarks>
/// <para>
/// Bu yerda eng nozik qoida sinaladi: <b>hodisa qaysi mehnat munosabatiga
/// yozilishi</b>. Bitta odamda bir nechta munosabat bo'lishi mumkin
/// (o'rindoshlik yoki ketib-qaytish), va Excel'dagi hodisa qatorida faqat
/// sana bor. Noto'g'ri tanlov hodisani boshqa ish joyiga yozib yuborardi.
/// </para>
/// <para>
/// Testlar haqiqiy <c>.xlsx</c> yaratadi (vaqtinchalik faylda), chunki
/// <see cref="EtlWorkspace"/> konstruktorida ish kitobi talab qilinadi.
/// Fayl bo'sh — bu testlarga varaq kerak emas.
/// </para>
/// </remarks>
public sealed class EtlWorkspaceTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"etl-{Guid.NewGuid():N}.xlsx");
    private readonly ExcelWorkbook _workbook;
    private readonly EtlReport _report = new();
    private readonly EtlWorkspace _workspace;

    public EtlWorkspaceTests()
    {
        using (var created = new XLWorkbook())
        {
            _ = created.AddWorksheet("bo'sh");
            created.SaveAs(_path);
        }

        _workbook = new ExcelWorkbook(_path);

        var options = new EtlOptions { Source = _path };
        var aliases = AliasTable.Parse(new MemoryStream(Encoding.UTF8.GetBytes("{}")));

        _workspace = new EtlWorkspace(
            options,
            _workbook,
            aliases,
            CatalogIndex.FromSeedFiles(),
            EtlDatabase.Offline(),
            _report);
    }

    private static readonly Guid Company = Guid.NewGuid();

    private Employee AddEmployee(string last = "Каримов", string first = "Азиз")
    {
        var employee = Employee.Create(FullName.FromTrusted(last, first, null), new DateOnly(1990, 5, 17)).Value;
        _workspace.Register(employee);
        return employee;
    }

    private Employment AddEmployment(Employee employee, DateOnly hire, DateOnly? termination = null)
    {
        var employment = Employment.Hire(employee.Id, Company, hire).Value;

        if (termination is { } date)
        {
            _ = employment.ImportTermination(date, reasonId: null);
        }

        _workspace.Register(employment);
        return employment;
    }

    // ────────────────────────────────────────────────────────────
    //  Hodisa qaysi munosabatga tushadi
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void Sanani_qamrab_olgan_munosabat_aniq_tanlanadi()
    {
        var employee = AddEmployee();
        var employment = AddEmployment(employee, new DateOnly(2024, 1, 10));

        var found = _workspace.EmploymentOn(employee.Id, new DateOnly(2024, 6, 1), out var exact);

        found.ShouldBeSameAs(employment);
        exact.ShouldBeTrue();
    }

    [Fact]
    public void Ketib_qaytgan_xodimda_kechroq_boshlangani_tanlanadi()
    {
        var employee = AddEmployee();
        _ = AddEmployment(employee, new DateOnly(2020, 1, 1), new DateOnly(2021, 1, 1));
        var second = AddEmployment(employee, new DateOnly(2023, 1, 1));

        var found = _workspace.EmploymentOn(employee.Id, new DateOnly(2024, 6, 1), out var exact);

        found.ShouldBeSameAs(second);
        exact.ShouldBeTrue();
    }

    [Fact]
    public void Hech_qaysisiga_tushmagan_sana_uchun_taxmin_belgilanadi()
    {
        // Hodisa ikki ish joyi orasidagi «bo'shliq» ga tushdi. Eng yaqin
        // boshlangani olinadi, lekin `exact = false` — chaqiruvchi buni
        // hisobotga yozadi va HR ko'radi.
        var employee = AddEmployee();
        var first = AddEmployment(employee, new DateOnly(2020, 1, 1), new DateOnly(2021, 1, 1));
        _ = AddEmployment(employee, new DateOnly(2023, 1, 1));

        var found = _workspace.EmploymentOn(employee.Id, new DateOnly(2022, 6, 1), out var exact);

        found.ShouldBeSameAs(first);
        exact.ShouldBeFalse();
    }

    [Fact]
    public void Munosabati_yoq_xodim_uchun_hech_narsa_qaytmaydi()
    {
        var employee = AddEmployee();

        _workspace.EmploymentOn(employee.Id, new DateOnly(2024, 1, 1), out _).ShouldBeNull();
    }

    [Fact]
    public void Ishga_kirish_sanasi_boyicha_takror_topiladi()
    {
        var employee = AddEmployee();
        var employment = AddEmployment(employee, new DateOnly(2024, 3, 5));

        _workspace.EmploymentStartedOn(employee.Id, new DateOnly(2024, 3, 5)).ShouldBeSameAs(employment);
        _workspace.EmploymentStartedOn(employee.Id, new DateOnly(2024, 3, 6)).ShouldBeNull();
    }

    // ────────────────────────────────────────────────────────────
    //  Idempotentlik
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void Qoshilgan_hodisa_ikkinchi_marta_yozilmaydi()
    {
        var employee = AddEmployee();
        var employment = AddEmployment(employee, new DateOnly(2024, 1, 10));

        var moved = EmploymentEvent.Import(
            employment.Id, EmploymentEventType.Promotion, new DateOnly(2024, 6, 1), orderNo: "25 P");

        _workspace.HasEvent(employment.Id, EmploymentEventType.Promotion, new DateOnly(2024, 6, 1), "25 P")
            .ShouldBeFalse();

        _workspace.AddEvent(employment, moved);

        _workspace.HasEvent(employment.Id, EmploymentEventType.Promotion, new DateOnly(2024, 6, 1), "25 P")
            .ShouldBeTrue();
    }

    [Fact]
    public void Bazadan_yuklangan_munosabatning_hodisalari_ham_royxatga_tushadi()
    {
        // Idempotentlik tekshiruvi bazadan kelgan yozuv uchun ham,
        // shu yugurishda yaratilgani uchun ham BIR XIL ishlashi kerak.
        var employee = AddEmployee();
        var employment = Employment.Hire(employee.Id, Company, new DateOnly(2024, 1, 10), orderNo: "12").Value;

        _workspace.Register(employment);

        _workspace.HasEvent(employment.Id, EmploymentEventType.Hire, new DateOnly(2024, 1, 10), "12")
            .ShouldBeTrue();
    }

    [Fact]
    public void Boshatish_kaliti_royxatga_tushadi()
    {
        var employee = AddEmployee();
        var employment = AddEmployment(employee, new DateOnly(2024, 1, 10));

        var result = _workspace.Terminate(employment, new DateOnly(2024, 9, 1), reasonId: null, orderNo: "88");

        result.IsSuccess.ShouldBeTrue();
        employment.Status.ShouldBe(EmploymentStatus.Terminated);
        EtlWorkspace.IsTerminated(employment).ShouldBeTrue();
    }

    [Fact]
    public void Ikki_marta_boshatishga_urinish_rad_etiladi()
    {
        var employee = AddEmployee();
        var employment = AddEmployment(employee, new DateOnly(2024, 1, 10));

        _ = _workspace.Terminate(employment, new DateOnly(2024, 9, 1), reasonId: null);

        _workspace.Terminate(employment, new DateOnly(2024, 9, 1), reasonId: null).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Ishga_kirishdan_oldingi_ketish_sanasi_rad_etiladi()
    {
        var employee = AddEmployee();
        var employment = AddEmployment(employee, new DateOnly(2024, 1, 10));

        _workspace.Terminate(employment, new DateOnly(2023, 1, 1), reasonId: null).IsFailure.ShouldBeTrue();
    }

    // ────────────────────────────────────────────────────────────
    //  Katakni qiymatga aylantirish
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void Oqilmagan_sana_hisobotga_tushadi()
    {
        var context = new RowContext("test", "varaq", 5, "Каримов Азиз");

        _workspace.Date(context, ExcelCell.FromText("noma'lum"), "tug'ilgan sana").ShouldBeNull();

        _report.Issues.Count.ShouldBe(1);
        _report.Issues[0].Problem.ShouldContain("tug'ilgan sana");
    }

    [Fact]
    public void Bosh_sana_katagi_hisobotga_tushmaydi()
    {
        var context = new RowContext("test", "varaq", 5, "Каримов Азиз");

        _workspace.Date(context, ExcelCell.Empty, "tug'ilgan sana").ShouldBeNull();

        _report.Issues.ShouldBeEmpty();
    }

    [Fact]
    public void Ishbay_oklad_hisobotga_tushmaydi()
    {
        // Bu xato emas, HR ataylab yozgan holat — hisobot shovqinga
        // to'lib ketmasligi kerak.
        var context = new RowContext("test", "varaq", 5, "Каримов Азиз");

        _workspace.Salary(context, ExcelCell.FromText("Ишбай"), "oklad")
            .Kind.ShouldBe(SalaryCellKind.PieceRate);

        _report.Issues.ShouldBeEmpty();
    }

    [Fact]
    public void Tushunarsiz_oklad_hisobotga_tushadi()
    {
        var context = new RowContext("test", "varaq", 5, "Каримов Азиз");

        _workspace.Salary(context, ExcelCell.FromText("4000000+КПИ"), "oklad")
            .Kind.ShouldBe(SalaryCellKind.Unknown);

        _report.Issues.Count.ShouldBe(1);
    }

    // ────────────────────────────────────────────────────────────
    //  Xodimni topish
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void Topilmagan_xodim_hisobotga_tushadi_va_qator_tashlanadi()
    {
        var context = new RowContext("events", "91.3", 12, string.Empty);

        var lookup = _workspace.FindEmployee(context, ExcelCell.FromText("Каримов Азиз Валиевич"));

        lookup.Found.ShouldBeFalse();
        lookup.Name.ShouldNotBeNull();
        _report.Issues[0].Action.ShouldBe(EtlAction.RowSkipped);
    }

    [Fact]
    public void Ism_ajratilmasa_qator_tashlanadi()
    {
        var context = new RowContext("events", "91.3", 12, string.Empty);

        var lookup = _workspace.FindEmployee(context, ExcelCell.FromText("—"));

        lookup.Found.ShouldBeFalse();
        _report.Issues[0].Problem.ShouldBe("ism ajratilmadi");
    }

    [Theory]
    [InlineData("Ф.И.О")]
    [InlineData("ФИО")]
    [InlineData("Ходимнинг Ф.И.Ш")]
    public void Takroriy_ustun_sarlavhasi_hisobotga_tushmaydi(string text)
    {
        // Bloknot yillar bo'yicha bloklarga bo'lingan va HR har bir blok
        // boshida sarlavhani qaytadan yozgan — varaq o'rtasida 79 ta
        // shunday qator bor. Bu xato emas, tuzilish.
        var context = new RowContext("events", "91.3", 120, string.Empty);

        var lookup = _workspace.FindEmployee(context, ExcelCell.FromText(text));

        lookup.Found.ShouldBeFalse();
        _report.Issues.ShouldBeEmpty();
    }

    [Fact]
    public void Haqiqiy_oqilmagan_ism_hisobotga_tushadi()
    {
        var context = new RowContext("events", "91.3", 120, string.Empty);

        _ = _workspace.FindEmployee(context, ExcelCell.FromText("5784"));

        _report.Issues.Count.ShouldBe(1);
    }

    [Fact]
    public void Tugilgan_sana_farq_qilsa_ism_boyicha_boglanadi_va_belgilanadi()
    {
        // Excel'da tug'ilgan sana tez-tez xato kiritilgan, ism esa deyarli
        // hech qachon. Yagona nomzod bo'lsa — bog'lanadi, lekin belgilanadi.
        var employee = AddEmployee("Каримов", "Азиз");
        var context = new RowContext("events", "91.6", 12, string.Empty);

        var lookup = _workspace.FindEmployee(
            context,
            ExcelCell.FromText("Каримов Азиз"),
            ExcelCell.FromDate(new DateOnly(1985, 1, 1)));

        lookup.Employee.ShouldBeSameAs(employee);
        _report.Issues[0].Action.ShouldBe(EtlAction.Flagged);
    }

    public void Dispose()
    {
        _workbook.Dispose();
        File.Delete(_path);
    }
}
