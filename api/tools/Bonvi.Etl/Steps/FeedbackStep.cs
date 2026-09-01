using Bonvi.Domain.Employees;
using Bonvi.Domain.Feedback;
using Bonvi.Etl.Excel;
using Bonvi.Etl.Reporting;
using Bonvi.Etl.Runtime;

namespace Bonvi.Etl.Steps;

/// <summary>
/// «91.16 Қайта алоқа» → <see cref="ExitInterview"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Varaq — Google Forms eksporti.</b> Birinchi ustun «Отметка
/// времени», ya'ni anketa TO'LDIRILGAN lahza (kasr sonli sana). Boshqa
/// sana varaqda umuman yo'q, shu sababli suhbat sanasi shundan olinadi —
/// bu taxmin emas: anketani xodimning o'zi ketayotganda to'ldirgan.
/// </para>
/// <para>
/// <b>Nega munosabatga bog'lanadi, xodimga emas.</b> <c>ExitInterview</c>
/// ning o'z qoidasi: bitta odam ikki marta ishga kirib, ikki marta ketishi
/// mumkin va har ketish alohida suhbat. Shu sababli qidiruv ikki qadam:
/// ism → xodim → uning TUGAGAN munosabatlaridan anketa sanasiga eng
/// yaqini.
/// </para>
/// <para>
/// <b>Nega eng oxirida yurgiziladi.</b> Qadam xodimni ham, uning ishdan
/// ketish yozuvini ham izlaydi — ikkalasi oldingi qadamlarda yaratiladi.
/// </para>
/// </remarks>
public sealed class FeedbackStep
{
    private const string Name = "feedback";

    /// <summary>
    /// Varaq nomi.
    /// </summary>
    /// <remarks>
    /// ⚠️ «91.16» dan keyin IKKITA bo'shliq bor — HR shunday nomlagan.
    /// Tuzatilmaydi: varaq Excel'da qanday atalgan bo'lsa, shundayligicha
    /// izlanadi (<c>ProfilesStep</c> dagi «Қобилятлар» imlo xatosi bilan
    /// bir xil qoida).
    /// </remarks>
    private const string SheetName = "91.16  Қайта алоқа";

    /// <summary>1-qator — ustun nomlari, ma'lumot 2-qatordan.</summary>
    private const int FirstDataRow = 2;

    private const int TimestampColumn = 1;
    private const int FullNameColumn = 2;
    private const int ReasonColumn = 6;
    private const int RecommendColumn = 7;
    private const int ImpressionsColumn = 8;
    private const int ChangesColumn = 9;

    /// <summary>Tavsiya javobining tanilgan shakllari.</summary>
    /// <remarks>
    /// Anketa ikki tilda: «Ҳа / Да», «Йук / Нет», «Билмайман / Не знаю».
    /// Salbiy shakl BIRINCHI sinaladi — «Йук / Нет» ichida «Да» yo'q,
    /// lekin qoida <c>ProbationsStep.Outcome</c> dagi bilan bir xil
    /// bo'lsin: matn o'zgarsa ham javob to'g'ri qolsin.
    /// </remarks>
    private static readonly string[] NoForms = ["йук", "йўқ", "нет", "yo'q", "yuq"];

    private static readonly string[] DontKnowForms = ["билмайман", "не знаю", "bilmayman"];

    private static readonly string[] YesForms = ["ха", "ҳа", "да", "ha"];

    private readonly EtlWorkspace _workspace;

    public FeedbackStep(EtlWorkspace workspace) => _workspace = workspace;

    public void Run()
    {
        var summary = _workspace.Report.Step(Name);

        var rows = _workspace.Workbook.Rows(SheetName);

        if (rows.Count == 0)
        {
            _workspace.Report.Add(Name, SheetName, 0, string.Empty,
                "varaq topilmadi", SheetName, EtlAction.RowSkipped);

            return;
        }

        var processed = 0;

        foreach (var row in rows)
        {
            if (row.Number < FirstDataRow || row[FullNameColumn].IsEmpty)
            {
                continue;
            }

            if (_workspace.Options.Limit is { } limit && processed >= limit)
            {
                break;
            }

            processed++;
            summary.Read++;

            ReadRow(row, summary);
        }
    }

    private void ReadRow(ExcelRow row, StepSummary summary)
    {
        var context = new RowContext(Name, SheetName, row.Number, row.Text(FullNameColumn));

        var lookup = _workspace.FindEmployee(context, row[FullNameColumn]);

        if (lookup.Employee is not { } employee)
        {
            // `FindEmployee` sababni O'ZI hisobotga yozadi (topilmadi,
            // bir xil ismli ikkita va h.k.) — bu yerda takrorlanmaydi.
            summary.Skipped++;
            return;
        }

        if (Date(row) is not { } interviewDate)
        {
            _workspace.Report.Add(Name, SheetName, row.Number, lookup.Label,
                "anketa to'ldirilgan sana o'qilmadi — suhbat yozilmadi",
                row.Text(TimestampColumn), EtlAction.RowSkipped);
            summary.Skipped++;
            return;
        }

        var reason = row.Text(ReasonColumn).Trim();

        if (reason.Length == 0)
        {
            // Sabab MAJBURIY (`ExitInterview.Record`) va bu to'g'ri:
            // sababsiz suhbat hech qanday savolga javob bermaydi.
            _workspace.Report.Add(Name, SheetName, row.Number, lookup.Label,
                "ishdan ketish sababi bo'sh — suhbat yozilmadi", null, EtlAction.RowSkipped);
            summary.Skipped++;
            return;
        }

        if (Employment(employee, interviewDate) is not { } employment)
        {
            _workspace.Report.Add(Name, SheetName, row.Number, lookup.Label,
                "mehnat munosabati topilmadi — suhbat yoziladigan joy yo'q",
                null, EtlAction.RowSkipped);
            summary.Skipped++;
            return;
        }

        if (_workspace.HasExitInterview(employment.Id))
        {
            // Bitta munosabatga bitta suhbat (bazada unikal indeks).
            // Ikkinchi anketa — odatda bir odam formani ikki marta
            // to'ldirgani yoki ikkita qatorning bitta munosabatga
            // tushgani. Birinchisi kuchda qoladi.
            _workspace.Report.Add(Name, SheetName, row.Number, lookup.Label,
                "bu mehnat munosabatiga suhbat allaqachon yozilgan",
                interviewDate.ToString("yyyy-MM-dd", null), EtlAction.AlreadyExists);
            summary.Updated++;
            return;
        }

        var recorded = ExitInterview.Record(
            employment.Id,
            interviewDate,
            reason,
            Recommendation(row, context),
            impressions: Trim(row.Text(ImpressionsColumn)),
            conditionsToReturn: Trim(row.Text(ChangesColumn)));

        if (recorded.IsFailure)
        {
            _workspace.Report.Add(Name, SheetName, row.Number, lookup.Label,
                "suhbat qabul qilinmadi", recorded.Error.Message, EtlAction.RowSkipped);
            summary.Skipped++;
            return;
        }

        var interview = recorded.Value;

        _workspace.Register(interview);
        _workspace.Database.Add(interview);
        summary.Created++;
    }

    /// <summary>
    /// Suhbat sanasi — anketa to'ldirilgan lahzadan.
    /// </summary>
    /// <remarks>
    /// Katak KASR son: <c>45611.45235178241</c> — kun va kun ichidagi
    /// vaqt. <c>ExcelDate.Read</c> butun qismini oladi, ya'ni soat
    /// tashlanadi. Bu to'g'ri: <c>ExitInterview.InterviewDate</c> —
    /// <c>DateOnly</c>, suhbat qaysi KUNI bo'lgani yetarli.
    /// </remarks>
    private static DateOnly? Date(ExcelRow row)
        => row[TimestampColumn].IsEmpty ? null : ExcelDate.Read(row[TimestampColumn]);

    /// <summary>
    /// Anketa sanasiga eng mos mehnat munosabati.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Avval o'sha kunda kuchda bo'lgani izlanadi. Topilmasa — xodimning
    /// eng OXIRGI munosabati olinadi: anketa ketgandan keyin, ba'zan bir
    /// necha kun o'tib to'ldirilgan va u paytda munosabat allaqachon
    /// yopilgan bo'ladi.
    /// </para>
    /// <para>
    /// Bu <b>taxmin</b> emas: odam bitta firmadan ketgan va anketa aynan
    /// o'sha ketish haqida. Bir nechta munosabat bo'lgan holatda esa
    /// oxirgisi — eng yaqin ketish.
    /// </para>
    /// </remarks>
    private Employment? Employment(Employee employee, DateOnly interviewDate)
    {
        if (_workspace.EmploymentOn(employee.Id, interviewDate, out _) is { } onDate)
        {
            return onDate;
        }

        Employment? latest = null;

        foreach (var employment in _workspace.EmploymentsOf(employee.Id))
        {
            if (latest is null || employment.HireDate > latest.HireDate)
            {
                latest = employment;
            }
        }

        return latest;
    }

    /// <summary>«Бошқаларга тавсия қиласизми» javobi.</summary>
    private RecommendationAnswer? Recommendation(ExcelRow row, RowContext context)
    {
        var text = row.Text(RecommendColumn).Trim();

        if (text.Length == 0)
        {
            return null;
        }

        var key = Fold(text);

        if (Matches(key, NoForms))
        {
            return RecommendationAnswer.No;
        }

        if (Matches(key, DontKnowForms))
        {
            return RecommendationAnswer.DontKnow;
        }

        if (Matches(key, YesForms))
        {
            return RecommendationAnswer.Yes;
        }

        // Tanilmagan javob TASHLANMAYDI — u taassurot bilan birga
        // ko'rinishi uchun hisobotga tushadi va HR uni ekranda qo'lda
        // qo'yadi. «Bilmayman» deb yozib qo'yish yolg'on bo'lardi.
        _workspace.Report.Add(Name, SheetName, context.Row, context.Employee,
            "tavsiya javobi tanilmadi — bo'sh qoldirildi", text, EtlAction.LeftEmpty);

        return null;
    }

    private static bool Matches(string key, string[] forms)
    {
        foreach (var form in forms)
        {
            if (key.Contains(form, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Taqqoslash uchun matnni soddalashtiradi.
    /// </summary>
    /// <remarks>
    /// <c>ProbationsStep.Fold</c> va <c>CouncilStep.Fold</c> bilan bir xil
    /// qoida va ataylab nusxalangan: har varaqning javob so'zlari mustaqil
    /// o'zgaradi, umumiy yordamchi esa uchalasini bir-biriga bog'lab
    /// qo'yardi.
    /// </remarks>
    private static string Fold(string value)
    {
        var folded = value.Trim().ToLowerInvariant();

        return folded
            .Replace('ў', 'у')
            .Replace('қ', 'к')
            .Replace('ғ', 'г')
            .Replace('ҳ', 'х')
            .Replace('ё', 'е');
    }

    private static string? Trim(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
