using Bonvi.Domain.Safety;
using Bonvi.Etl.Excel;
using Bonvi.Etl.Reporting;
using Bonvi.Etl.Runtime;

namespace Bonvi.Etl.Steps;

/// <summary>
/// «Бахтсиз ходисалар руйхати» → <see cref="Incident"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Varaq kichik, lekin har qatori qimmatli.</b> Uchta hodisa bor va
/// ikkitasi O'LIM bilan tugagan. Bu mehnat xavfsizligi hujjati: uni
/// «atigi uchta qator» deb tashlab ketish mumkin emas — aynan shunday
/// yozuvlar tekshiruvda so'raladi.
/// </para>
/// <para>
/// <b>Varaqning ikkita o'ziga xosligi bor.</b> Birinchisi — YIL
/// sarlavhalari («2026 ЙИЛ», «2025 ЙИЛ»): ular 6-ustunda yotadi va
/// hodisa qatoriga o'xshaydi. Ikkinchisi — bo'sh yil uchun qo'yilgan
/// to'ldiruvchi qator: hamma katagida bitta «-» turadi. Ikkalasi ham
/// hodisa EMAS va hisobotga ham tushmasligi kerak, aks holda HR uchta
/// haqiqiy yozuv orasida ikkita soxta xatoni ko'rardi.
/// </para>
/// </remarks>
public sealed class SafetyStep
{
    private const string Name = "safety";
    private const string SheetName = "Бахтсиз ходисалар руйхати";

    /// <summary>1–3 qatorlar sarlavha, ma'lumot 4-qatordan.</summary>
    private const int FirstDataRow = 4;

    private const int NumberColumn = 1;
    private const int DateColumn = 2;
    private const int ShiftColumn = 3;
    private const int FullNameColumn = 4;
    private const int LocationColumn = 6;
    private const int DescriptionColumn = 7;
    private const int CauseColumn = 8;
    private const int ActionColumn = 9;
    private const int SeverityColumn = 10;
    private const int MedicalAidColumn = 11;
    private const int ConclusionColumn = 12;

    /// <summary>Jabrlanish darajasining varaqdagi shakllari.</summary>
    /// <remarks>
    /// Tartib MUHIM: «оғир» ham «Ўрта оғир» da, ham «Оғир» da bor.
    /// Aniqrog'i («ўрта оғир») birinchi sinaladi, aks holda o'rtacha
    /// og'ir hodisa «og'ir» bo'lib yozilardi.
    /// </remarks>
    private static readonly (string Form, IncidentSeverity Severity)[] SeverityForms =
    [
        ("улим", IncidentSeverity.Fatal),
        ("урта огир", IncidentSeverity.Moderate),
        ("огир", IncidentSeverity.Severe),
        ("енгил", IncidentSeverity.Minor),
    ];

    /// <summary>Tibbiy yordam ko'rsatilganini bildiruvchi shakllar.</summary>
    private static readonly string[] MedicalAidForms = ["курсатилган", "чакирилди", "олиб борилди", "ха"];

    private readonly EtlWorkspace _workspace;

    public SafetyStep(EtlWorkspace workspace) => _workspace = workspace;

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
            if (row.Number < FirstDataRow || IsNoise(row))
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

    /// <summary>
    /// Qator hodisa emasmi — yil sarlavhasi yoki to'ldiruvchi.
    /// </summary>
    /// <remarks>
    /// Yil sarlavhasida F.I.Sh. ustuni BO'SH, to'ldiruvchi qatorda esa
    /// u «-». Ikkinchi shart tekshirilmasa, «-» ismli «xodim» qidirilib,
    /// hisobotga soxta «xodim topilmadi» satri tushardi.
    /// </remarks>
    private static bool IsNoise(ExcelRow row)
    {
        var name = row.Text(FullNameColumn).Trim();

        return name.Length == 0 || name == "-" || row[NumberColumn].Text.Trim() == "-";
    }

    private void ReadRow(ExcelRow row, StepSummary summary)
    {
        var context = new RowContext(Name, SheetName, row.Number, row.Text(FullNameColumn));

        var lookup = _workspace.FindEmployee(context, row[FullNameColumn]);

        if (lookup.Employee is not { } employee)
        {
            // Sababni `FindEmployee` o'zi yozadi.
            summary.Skipped++;
            return;
        }

        if (_workspace.Date(context, row[DateColumn], "hodisa sanasi") is not { } incidentDate)
        {
            summary.Skipped++;
            return;
        }

        if (_workspace.HasIncident(employee.Id, incidentDate))
        {
            _workspace.Report.Add(Name, SheetName, row.Number, lookup.Label,
                "hodisa allaqachon yozilgan", null, EtlAction.AlreadyExists);
            summary.Updated++;
            return;
        }

        var recorded = Incident.Record(
            employee.Id,
            incidentDate,
            location: row.Text(LocationColumn),
            description: row.Text(DescriptionColumn),
            severity: Severity(row, context),
            shift: Trim(row.Text(ShiftColumn)),
            cause: Trim(row.Text(CauseColumn)),
            correctiveAction: Trim(row.Text(ActionColumn)),
            medicalAidProvided: MedicalAidGiven(row),
            medicalAidNote: Trim(row.Text(MedicalAidColumn)));

        if (recorded.IsFailure)
        {
            _workspace.Report.Add(Name, SheetName, row.Number, lookup.Label,
                "hodisa qabul qilinmadi", recorded.Error.Message, EtlAction.RowSkipped);
            summary.Skipped++;
            return;
        }

        var incident = recorded.Value;

        Conclude(incident, row, incidentDate, lookup.Label);

        _workspace.Register(incident);
        _workspace.Database.Add(incident);
        summary.Created++;
    }

    /// <summary>
    /// Komissiya xulosasini yozadi (varaqda bo'lsa).
    /// </summary>
    /// <remarks>
    /// Xulosa sanasi varaqda YO'Q, shu sababli hodisa sanasi olinadi.
    /// Bu taxmin, lekin zararsiz: <c>ConcludedAt</c> «tekshiruv
    /// yakunlangan» degan BELGI va uning aniq kuni hech qayerda
    /// ishlatilmaydi. Xulosasiz hodisa esa ekranda «tekshiruv davom
    /// etmoqda» bo'lib turadi — 2024-yilgi hodisa uchun bu yolg'on.
    /// </remarks>
    private void Conclude(Incident incident, ExcelRow row, DateOnly incidentDate, string employee)
    {
        var conclusion = row.Text(ConclusionColumn).Trim();

        if (conclusion.Length == 0)
        {
            return;
        }

        var concluded = incident.Conclude(conclusion, incidentDate);

        if (concluded.IsFailure)
        {
            _workspace.Report.Add(Name, SheetName, row.Number, employee,
                "komissiya xulosasi yozilmadi", concluded.Error.Message, EtlAction.LeftEmpty);
        }
    }

    /// <summary>
    /// Jabrlanish darajasi.
    /// </summary>
    /// <remarks>
    /// Tanilmasa <see cref="IncidentSeverity.Minor"/> EMAS, balki
    /// <see cref="IncidentSeverity.Moderate"/> olinadi va hisobotga
    /// tushadi. Sabab: daraja majburiy maydon va noma'lum holatda uni
    /// «yengil» deb yozish hodisani KICHRAYTIRIB ko'rsatardi — mehnat
    /// xavfsizligi jurnalida bu eng yomon xato turi.
    /// </remarks>
    private IncidentSeverity Severity(ExcelRow row, RowContext context)
    {
        var text = row.Text(SeverityColumn).Trim();
        var key = Fold(text);

        foreach (var (form, severity) in SeverityForms)
        {
            if (key.Contains(form, StringComparison.Ordinal))
            {
                return severity;
            }
        }

        _workspace.Report.Add(Name, SheetName, context.Row, context.Employee,
            "jabrlanish darajasi tanilmadi — «o'rtacha og'ir» qo'yildi, HR tekshirsin",
            text, EtlAction.Adjusted);

        return IncidentSeverity.Moderate;
    }

    /// <summary>Tibbiy yordam ko'rsatilganmi.</summary>
    private static bool MedicalAidGiven(ExcelRow row)
    {
        var key = Fold(row.Text(MedicalAidColumn));

        foreach (var form in MedicalAidForms)
        {
            if (key.Contains(form, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Taqqoslash uchun matnni soddalashtiradi.</summary>
    /// <remarks>
    /// Boshqa qadamlardagi bilan bir xil qoida, ataylab nusxalangan —
    /// sabab <c>FeedbackStep.Fold</c> izohida.
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
