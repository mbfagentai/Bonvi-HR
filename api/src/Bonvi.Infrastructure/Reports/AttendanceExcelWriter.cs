using System.Globalization;
using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using ClosedXML.Excel;

namespace Bonvi.Infrastructure.Reports;

/// <summary>
/// <see cref="IAttendanceExcelWriter"/> ning <c>ClosedXML</c> orqali amalga oshirilishi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Uslub — <c>EmployeeExcelWriter</c> bilan bir xil:</b> 1-qator qalin
/// sarlavha, kulrang fon, qotirilgan; sarlavhalar o'zbekcha (lotin) va shu
/// yerda qattiq yozilgan — <c>.xlsx</c> brauzerda ochilmaydi, uni frontend
/// tarjima qila olmaydi.
/// </para>
/// <para>
/// ⭐ <b>Sana — HAQIQIY Excel sanasi</b> (<c>dd.mm.yyyy</c>), lahza — haqiqiy
/// sana-vaqt, ikkalasi ham TOSHKENT vaqtida. Bazada UTC turadi, lekin
/// Excel'da siljish tushunchasi yo'q: UTC yozilsa, HR «08:03» o'rniga
/// «03:03» ni ko'rardi. Shu sababli o'girish aynan shu yerda, bir marta
/// (<see cref="BonviTime.ToTashkent"/>).
/// </para>
/// <para>
/// ⭐ <b>Davomiylik — IKKI ustun:</b> «(daq.)» — SON, yig'indilanadi;
/// «(soat:daq)» — MATN, o'qiladi (SPEC §6.7). «8:30» ni son qilib Excel
/// vaqtiga aylantirish ham mumkin edi, lekin 24 soatdan oshgan oylik
/// yig'indi «1:30» bo'lib ko'rinardi — HR buni xato deb o'ylaydi va haq
/// bo'ladi.
/// </para>
/// <para>
/// <b>Formatlar ustun ORALIG'IGA bir marta qo'yiladi</b>, har katakka
/// alohida emas: tabelning batafsil varag'i 16 ming qatorgacha boradi va
/// katak bo'yicha uslub faylni sezilarli sekinlashtirardi.
/// </para>
/// </remarks>
public sealed class AttendanceExcelWriter : IAttendanceExcelWriter
{
    private const string DateFormat = "dd.mm.yyyy";
    private const string DateTimeFormat = "dd.mm.yyyy hh:mm";
    private const string DateTimeSecondsFormat = "dd.mm.yyyy hh:mm:ss";
    private const string DayHeaderFormat = "dd.mm";
    private const string HoursFormat = "0.0";

    /// <summary>
    /// Ustun kengligi shuncha qatorga qarab hisoblanadi.
    /// </summary>
    /// <remarks>
    /// <c>AdjustToContents</c> har bir katakni o'lchaydi; 20 ming qatorda bu
    /// faylni bir necha soniya sekinlashtiradi, natija esa birinchi yuz
    /// qatordagidan deyarli farq qilmaydi.
    /// </remarks>
    private const int WidthSampleRows = 200;

    private static readonly XLColor HeaderFill = XLColor.FromHtml("#F2F2F2");

    // ════════════════════════════════════════════════════════════
    //  Tabel
    // ════════════════════════════════════════════════════════════

    private static readonly string[] TimesheetTotalsHeaders =
    [
        "Ishlangan (daq.)",
        "Ishlangan (soat:daq)",
        "Reja (daq.)",
        "Reja (soat:daq)",
        "Kelgan kunlar",
        "Kelmagan kunlar",
        "Kechikish (daq.)",
        "Kechikish (soat:daq)",
        "Erta ketish (daq.)",
        "Erta ketish (soat:daq)",
    ];

    private static readonly string[] TimesheetDayHeaders =
    [
        "F.I.O.",
        "Bo'lim",
        "Lavozim",
        "Sana",
        "Holat",
        "Birinchi kirish",
        "Oxirgi chiqish",
        "Ishlangan (daq.)",
        "Ishlangan (soat:daq)",
        "Kechikish (daq.)",
        "Kechikish (soat:daq)",
        "Erta ketish (daq.)",
        "Erta ketish (soat:daq)",
        "Tanaffus olingan (daq.)",
        "Tanaffus ayirilgan (daq.)",
    ];

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Ikki varaq. <b>«Tabel»</b> — ekrandagi katak: qator — xodim, ustun —
    /// kun (sarlavhasi haqiqiy sana), katakda ishlangan SOAT soni (son,
    /// <c>0.0</c> formatida, <c>daqiqa / 60</c> aniq qiymati bilan — ko'rinish
    /// yaxlitlanadi, yig'indi emas). Ishlanmagan kunda holatning qisqa belgisi
    /// turadi (K — kelmagan, D — dam olish...), izohi jadval ostida.
    /// <c>SUM</c> matnli kataklarni o'tkazib yuboradi, ya'ni belgi yig'indini
    /// buzmaydi.
    /// </para>
    /// <para>
    /// <b>«Kunlar»</b> — har bir xodim-kun alohida qator, barcha sonlar ikki
    /// ustunda. HR saralash, filtr va yig'ma jadval (pivot) uchun aynan shu
    /// varaqni ishlatadi.
    /// </para>
    /// </remarks>
    public byte[] WriteTimesheet(AttendanceTimesheetDto timesheet)
    {
        ArgumentNullException.ThrowIfNull(timesheet);

        using var workbook = new XLWorkbook();

        WriteTimesheetGrid(workbook.Worksheets.Add("Tabel"), timesheet);
        WriteTimesheetDays(workbook.Worksheets.Add("Kunlar"), timesheet);

        return Save(workbook);
    }

    private static void WriteTimesheetGrid(IXLWorksheet sheet, AttendanceTimesheetDto timesheet)
    {
        const int firstDayColumn = 4;
        var days = timesheet.Days;
        var totalsColumn = firstDayColumn + days.Count;
        var lastRow = timesheet.Items.Count + 1;

        sheet.Cell(1, 1).Value = "F.I.O.";
        sheet.Cell(1, 2).Value = "Bo'lim";
        sheet.Cell(1, 3).Value = "Lavozim";

        for (var i = 0; i < days.Count; i++)
        {
            var header = sheet.Cell(1, firstDayColumn + i);
            header.Value = days[i].ToDateTime(TimeOnly.MinValue);
            header.Style.DateFormat.Format = DayHeaderFormat;

            // Dam olish kunlari ustuni kulrang — hafta ritmi chiziqsiz ko'rinsin
            // (ekrandagi §8.6 qoidasi bilan bir xil).
            if (days[i].DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                sheet.Column(firstDayColumn + i).Style.Fill.BackgroundColor = HeaderFill;
            }
        }

        for (var i = 0; i < TimesheetTotalsHeaders.Length; i++)
        {
            sheet.Cell(1, totalsColumn + i).Value = TimesheetTotalsHeaders[i];
        }

        StyleHeader(sheet, totalsColumn + TimesheetTotalsHeaders.Length - 1);

        for (var r = 0; r < timesheet.Items.Count; r++)
        {
            var row = timesheet.Items[r];
            var excelRow = r + 2;

            sheet.Cell(excelRow, 1).Value = row.FullName;
            sheet.Cell(excelRow, 2).Value = row.Department ?? string.Empty;
            sheet.Cell(excelRow, 3).Value = row.Position ?? string.Empty;

            for (var i = 0; i < row.Cells.Count && i < days.Count; i++)
            {
                WriteGridCell(sheet.Cell(excelRow, firstDayColumn + i), row.Cells[i]);
            }

            var totals = row.Totals;
            var column = totalsColumn;

            SetDuration(sheet, excelRow, ref column, totals.WorkedMinutes);
            SetDuration(sheet, excelRow, ref column, totals.PlannedMinutes);
            sheet.Cell(excelRow, column++).Value = totals.PresentDays;
            sheet.Cell(excelRow, column++).Value = totals.AbsentDays;
            SetDuration(sheet, excelRow, ref column, totals.LateMinutes);
            SetDuration(sheet, excelRow, ref column, totals.EarlyLeaveMinutes);
        }

        if (timesheet.Items.Count > 0)
        {
            sheet.Range(2, firstDayColumn, lastRow, totalsColumn - 1).Style.NumberFormat.Format = HoursFormat;
            sheet.Range(2, firstDayColumn, lastRow, totalsColumn - 1).Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;
        }

        WriteLegend(sheet, lastRow + 2);

        // Ism ustuni ham qotiriladi: 31 kunni o'ngga aylantirganda kimning
        // qatori ekani ko'rinib tursin.
        sheet.SheetView.Freeze(1, 1);

        sheet.Columns(1, 3).AdjustToContents(1, WidthSampleRows);
        sheet.Columns(firstDayColumn, totalsColumn - 1).Width = 6;
        sheet.Columns(totalsColumn, totalsColumn + TimesheetTotalsHeaders.Length - 1).AdjustToContents(1, 1);
    }

    /// <summary>
    /// Katak: ishlagan bo'lsa — soat (son), aks holda holatning qisqa belgisi.
    /// </summary>
    /// <remarks>
    /// «Kelgan», «dam olish kunida ishlagan» holatlarida 0 daqiqa ham SON
    /// bo'lib yoziladi: odam kelgan, faqat hisoblanadigan oraliq yo'q —
    /// katakni «K» qilib qo'yish uni kelmaganga aylantirardi.
    /// </remarks>
    private static void WriteGridCell(IXLCell cell, TimesheetCellDto source)
    {
        var alwaysNumber = source.Status is AttendanceDayStatus.Present or AttendanceDayStatus.DayOffWorked;

        if (source.WorkedMinutes > 0 || alwaysNumber)
        {
            cell.Value = source.WorkedMinutes / 60d;
        }
        else
        {
            cell.Value = StatusCode(source.Status);
        }

        if (StatusFill(source.Status) is { } fill)
        {
            cell.Style.Fill.BackgroundColor = fill;
        }
    }

    private static void WriteLegend(IXLWorksheet sheet, int startRow)
    {
        (string Code, string Meaning)[] legend =
        [
            ("son", "Ishlangan soat (masalan 7.5 — 7 soat 30 daqiqa)"),
            (StatusCode(AttendanceDayStatus.Absent), StatusLabel(AttendanceDayStatus.Absent)),
            (StatusCode(AttendanceDayStatus.Excused), StatusLabel(AttendanceDayStatus.Excused)),
            (StatusCode(AttendanceDayStatus.DayOff), StatusLabel(AttendanceDayStatus.DayOff)),
            (StatusCode(AttendanceDayStatus.MissingExit), StatusLabel(AttendanceDayStatus.MissingExit)),
            (StatusCode(AttendanceDayStatus.MissingEntry), StatusLabel(AttendanceDayStatus.MissingEntry)),
            (StatusCode(AttendanceDayStatus.NoSchedule), StatusLabel(AttendanceDayStatus.NoSchedule)),
            (StatusCode(AttendanceDayStatus.NotEmployed), StatusLabel(AttendanceDayStatus.NotEmployed)),
        ];

        sheet.Cell(startRow, 1).Value = "Belgilar";
        sheet.Cell(startRow, 1).Style.Font.Bold = true;

        for (var i = 0; i < legend.Length; i++)
        {
            sheet.Cell(startRow + 1 + i, 1).Value = legend[i].Code;
            sheet.Cell(startRow + 1 + i, 2).Value = legend[i].Meaning;
        }
    }

    private static void WriteTimesheetDays(IXLWorksheet sheet, AttendanceTimesheetDto timesheet)
    {
        WriteHeaders(sheet, TimesheetDayHeaders);

        var excelRow = 2;

        foreach (var row in timesheet.Items)
        {
            foreach (var cell in row.Cells)
            {
                var column = 1;

                sheet.Cell(excelRow, column++).Value = row.FullName;
                sheet.Cell(excelRow, column++).Value = row.Department ?? string.Empty;
                sheet.Cell(excelRow, column++).Value = row.Position ?? string.Empty;
                sheet.Cell(excelRow, column++).Value = cell.Date.ToDateTime(TimeOnly.MinValue);
                sheet.Cell(excelRow, column++).Value = StatusLabel(cell.Status);
                SetInstant(sheet.Cell(excelRow, column++), cell.FirstInAt);
                SetInstant(sheet.Cell(excelRow, column++), cell.LastOutAt);
                SetDuration(sheet, excelRow, ref column, cell.WorkedMinutes);
                SetDuration(sheet, excelRow, ref column, cell.LateMinutes);
                SetDuration(sheet, excelRow, ref column, cell.EarlyLeaveMinutes);
                sheet.Cell(excelRow, column++).Value = cell.BreakTakenMinutes;
                sheet.Cell(excelRow, column).Value = cell.BreakDeductionMinutes;

                excelRow++;
            }
        }

        FormatColumn(sheet, 4, excelRow - 1, DateFormat);
        FormatColumn(sheet, 6, excelRow - 1, DateTimeFormat);
        FormatColumn(sheet, 7, excelRow - 1, DateTimeFormat);

        Finish(sheet);
    }

    // ════════════════════════════════════════════════════════════
    //  O'tishlar
    // ════════════════════════════════════════════════════════════

    private static readonly string[] PassHeaders =
    [
        "Sana",
        "Vaqt",
        "F.I.O.",
        "Shaxs raqami",
        "Bo'lim",
        "Lavozim",
        "Qurilma",
        "Yo'nalish",
        "Usul",
        "Soat og'ishi (soniya)",
        "Soat adashgan",
        "Vaqt mintaqasi taxmin qilingan",
        "Surat",
    ];

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// «Vaqt» ustunida TO'LIQ sana-vaqt, soniyasi bilan — «Sana» ustuni
    /// yonida takror ko'rinsa ham. Faqat soat formati (<c>hh:mm:ss</c>)
    /// qo'yilsa, Excel va o'qiydigan kutubxonalar katakni sana emas, «vaqt
    /// oralig'i» deb taniydi va tungi smenada saralash sanadan ajralib qoladi.
    /// </para>
    /// <para>Moslanmagan o'tishda F.I.O. bo'sh, lekin shaxs raqami doim bor.</para>
    /// </remarks>
    public byte[] WritePasses(IReadOnlyList<AttendancePassDto> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("O'tishlar");

        WriteHeaders(sheet, PassHeaders);

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var excelRow = i + 2;
            var column = 1;

            sheet.Cell(excelRow, column++).Value = row.LocalDate.ToDateTime(TimeOnly.MinValue);
            SetInstant(sheet.Cell(excelRow, column++), row.OccurredAt);
            sheet.Cell(excelRow, column++).Value = row.EmployeeName ?? string.Empty;
            sheet.Cell(excelRow, column++).Value = row.RawPersonId;
            sheet.Cell(excelRow, column++).Value = row.Department ?? string.Empty;
            sheet.Cell(excelRow, column++).Value = row.Position ?? string.Empty;
            sheet.Cell(excelRow, column++).Value = row.DeviceName ?? string.Empty;
            sheet.Cell(excelRow, column++).Value = DirectionLabel(row.Direction);
            sheet.Cell(excelRow, column++).Value = MethodLabel(row.Method);
            sheet.Cell(excelRow, column++).Value = row.ClockSkewSeconds;
            sheet.Cell(excelRow, column++).Value = YesNo(row.SkewWarning);
            sheet.Cell(excelRow, column++).Value = YesNo(row.OffsetAssumed);
            sheet.Cell(excelRow, column).Value = PhotoLabel(row);
        }

        FormatColumn(sheet, 1, rows.Count + 1, DateFormat);
        FormatColumn(sheet, 2, rows.Count + 1, DateTimeSecondsFormat);

        Finish(sheet);
        return Save(workbook);
    }

    // ════════════════════════════════════════════════════════════
    //  Chetlanishlar
    // ════════════════════════════════════════════════════════════

    private static readonly string[] ExceptionHeaders =
    [
        "Sana",
        "F.I.O.",
        "Bo'lim",
        "Lavozim",
        "Reja boshlanishi",
        "Reja tugashi",
        "Birinchi kirish",
        "Oxirgi chiqish",
        "Kechikish (daq.)",
        "Kechikish (soat:daq)",
        "Erta ketish (daq.)",
        "Erta ketish (soat:daq)",
        "Ishlangan (daq.)",
        "Ishlangan (soat:daq)",
        "Holat",
    ];

    /// <inheritdoc />
    public byte[] WriteExceptions(ExceptionKind kind, IReadOnlyList<AttendanceExceptionDto> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(ExceptionSheetName(kind));

        WriteHeaders(sheet, ExceptionHeaders);

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var excelRow = i + 2;
            var column = 1;

            sheet.Cell(excelRow, column++).Value = row.Date.ToDateTime(TimeOnly.MinValue);
            sheet.Cell(excelRow, column++).Value = row.EmployeeName;
            sheet.Cell(excelRow, column++).Value = row.Department ?? string.Empty;
            sheet.Cell(excelRow, column++).Value = row.Position ?? string.Empty;
            SetInstant(sheet.Cell(excelRow, column++), row.PlanStartAt);
            SetInstant(sheet.Cell(excelRow, column++), row.PlanEndAt);
            SetInstant(sheet.Cell(excelRow, column++), row.FirstInAt);
            SetInstant(sheet.Cell(excelRow, column++), row.LastOutAt);
            SetDuration(sheet, excelRow, ref column, row.LateMinutes);
            SetDuration(sheet, excelRow, ref column, row.EarlyLeaveMinutes);
            SetDuration(sheet, excelRow, ref column, row.WorkedMinutes);
            sheet.Cell(excelRow, column).Value = StatusLabel(row.Status);
        }

        FormatColumn(sheet, 1, rows.Count + 1, DateFormat);

        for (var column = 5; column <= 8; column++)
        {
            FormatColumn(sheet, column, rows.Count + 1, DateTimeFormat);
        }

        Finish(sheet);
        return Save(workbook);
    }

    // ════════════════════════════════════════════════════════════
    //  Xulosa
    // ════════════════════════════════════════════════════════════

    private static readonly string[] SummaryHeaders =
    [
        "F.I.O.",
        "Bo'lim",
        "Lavozim",
        "Reja bo'yicha ish kunlari",
        "Kelgan kunlar",
        "Kelmagan kunlar",
        "Dam olish kunida ishlagan",
        "Muammoli kunlar",
        "Ishlangan (daq.)",
        "Ishlangan (soat:daq)",
        "Reja (daq.)",
        "Reja (soat:daq)",
        "Kechikish (daq.)",
        "Kechikish (soat:daq)",
        "Erta ketish (daq.)",
        "Erta ketish (soat:daq)",
        "Ortiqcha ish (daq.)",
        "Ortiqcha ish (soat:daq)",
    ];

    /// <inheritdoc />
    public byte[] WriteSummary(IReadOnlyList<AttendanceSummaryDto> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Xulosa");

        WriteHeaders(sheet, SummaryHeaders);

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var excelRow = i + 2;
            var column = 1;

            sheet.Cell(excelRow, column++).Value = row.EmployeeName;
            sheet.Cell(excelRow, column++).Value = row.Department ?? string.Empty;
            sheet.Cell(excelRow, column++).Value = row.Position ?? string.Empty;
            sheet.Cell(excelRow, column++).Value = row.PlannedDays;
            sheet.Cell(excelRow, column++).Value = row.PresentDays;
            sheet.Cell(excelRow, column++).Value = row.AbsentDays;
            sheet.Cell(excelRow, column++).Value = row.DayOffWorkedDays;
            sheet.Cell(excelRow, column++).Value = row.ProblemDays;
            SetDuration(sheet, excelRow, ref column, row.WorkedMinutes);
            SetDuration(sheet, excelRow, ref column, row.PlannedMinutes);
            SetDuration(sheet, excelRow, ref column, row.LateMinutes);
            SetDuration(sheet, excelRow, ref column, row.EarlyLeaveMinutes);
            SetDuration(sheet, excelRow, ref column, row.OvertimeMinutes);
        }

        Finish(sheet);
        return Save(workbook);
    }

    // ════════════════════════════════════════════════════════════
    //  Yordamchilar
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Davomiylik — ikki qo'shni katak: daqiqa (son) va «soat:daq» (matn).
    /// </summary>
    /// <remarks>
    /// Soat 24 dan oshganda ham aylanmaydi: oylik 176:30 aynan shunday
    /// yoziladi (<c>TimeSpan</c> formati «7.08:30» qilib yuborardi).
    /// </remarks>
    private static void SetDuration(IXLWorksheet sheet, int row, ref int column, int minutes)
    {
        sheet.Cell(row, column++).Value = minutes;
        sheet.Cell(row, column++).Value = HoursLabel(minutes);
    }

    internal static string HoursLabel(int minutes)
        => string.Create(CultureInfo.InvariantCulture, $"{minutes / 60}:{minutes % 60:00}");

    /// <summary>Lahzani Toshkent vaqtidagi haqiqiy Excel sana-vaqtiga yozadi; bo'sh bo'lsa katak bo'sh qoladi.</summary>
    private static void SetInstant(IXLCell cell, DateTimeOffset? instant)
    {
        if (instant is { } value)
        {
            cell.Value = value.ToTashkent().DateTime;
        }
    }

    private static void WriteHeaders(IXLWorksheet sheet, string[] headers)
    {
        for (var column = 0; column < headers.Length; column++)
        {
            sheet.Cell(1, column + 1).Value = headers[column];
        }

        StyleHeader(sheet, headers.Length);
    }

    private static void StyleHeader(IXLWorksheet sheet, int lastColumn)
    {
        var header = sheet.Range(1, 1, 1, lastColumn);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = HeaderFill;
    }

    private static void FormatColumn(IXLWorksheet sheet, int column, int lastRow, string format)
    {
        if (lastRow < 2)
        {
            return;
        }

        sheet.Range(2, column, lastRow, column).Style.DateFormat.Format = format;
    }

    private static void Finish(IXLWorksheet sheet)
    {
        // Sarlavha doim ko'rinib tursin (`EmployeeExcelWriter` bilan bir xil).
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents(1, WidthSampleRows);
    }

    private static byte[] Save(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>Holat — o'zbekcha matn (ekrandagi <c>attendance.status*</c> bilan bir ma'noda).</summary>
    private static string StatusLabel(AttendanceDayStatus status) => status switch
    {
        AttendanceDayStatus.Present => "Kelgan",
        AttendanceDayStatus.Absent => "Kelmagan",
        AttendanceDayStatus.DayOff => "Dam olish kuni",
        AttendanceDayStatus.DayOffWorked => "Dam olish kunida ishlagan",
        AttendanceDayStatus.MissingExit => "Chiqish yozilmagan",
        AttendanceDayStatus.MissingEntry => "Kirish yozilmagan",
        AttendanceDayStatus.NoSchedule => "Jadval yo'q",
        AttendanceDayStatus.NotEmployed => "Ishlamagan",
        AttendanceDayStatus.Excused => "Sababli",
        // Kelajak kun — Excel'da ham bo'sh emas, atayin nomlangan:
        // eksportni ochgan odam bo'sh katakni «ma'lumot yo'qolgan» deb
        // o'ylamasligi kerak.
        AttendanceDayStatus.Planned => "Kun kelmagan",
        _ => status.ToString(),
    };

    /// <summary>Tabel katagidagi qisqa belgi — ishlanmagan kun uchun.</summary>
    private static string StatusCode(AttendanceDayStatus status) => status switch
    {
        AttendanceDayStatus.Absent => "K",
        AttendanceDayStatus.DayOff => "D",
        AttendanceDayStatus.MissingExit => "ChY",
        AttendanceDayStatus.MissingEntry => "KY",
        AttendanceDayStatus.NoSchedule => "J",
        AttendanceDayStatus.NotEmployed => "—",
        AttendanceDayStatus.Excused => "S",
        // Kelajak kunning belgisi YO'Q: tabel katagida u bo'sh turishi
        // kerak, aks holda oyning qolgan yarmi belgilar bilan to'lardi.
        _ => string.Empty,
    };

    /// <summary>
    /// Katak foni — faqat e'tibor talab qiladigan holatlarda.
    /// </summary>
    /// <remarks>
    /// Ekrandagi ranglar bilan bir xil ma'no (§8.6): kelmagan — qizg'ish,
    /// chiqish/kirish yo'q — to'q sariq, dam olishda ishlagan — binafsha.
    /// «Kelgan» oq qoladi — rang faqat ma'no uchun.
    /// </remarks>
    private static XLColor? StatusFill(AttendanceDayStatus status) => status switch
    {
        AttendanceDayStatus.Absent => XLColor.FromHtml("#FDE2E1"),
        AttendanceDayStatus.MissingExit or AttendanceDayStatus.MissingEntry => XLColor.FromHtml("#FFEBD6"),
        AttendanceDayStatus.DayOffWorked => XLColor.FromHtml("#EFE3FA"),
        AttendanceDayStatus.NoSchedule => XLColor.FromHtml("#FFF6CC"),
        _ => null,
    };

    private static string DirectionLabel(PassDirection direction) => direction switch
    {
        PassDirection.In => "Kirish",
        PassDirection.Out => "Chiqish",
        _ => "Noma'lum",
    };

    private static string MethodLabel(PassMethod method) => method switch
    {
        PassMethod.Face => "Yuz",
        PassMethod.Card => "Karta",
        PassMethod.Fingerprint => "Barmoq izi",
        PassMethod.Password => "Parol",
        PassMethod.Other => "Boshqa",
        _ => "Noma'lum",
    };

    private static string PhotoLabel(AttendancePassDto row)
        => row.PhotoKey is not null ? "Bor"
            : row.PhotoPurgedAt is not null ? "Muddati o'tib o'chirilgan"
            : "Yo'q";

    private static string ExceptionSheetName(ExceptionKind kind) => kind switch
    {
        ExceptionKind.Late => "Kechikkanlar",
        ExceptionKind.EarlyLeave => "Erta ketganlar",
        ExceptionKind.Absent => "Kelmaganlar",
        ExceptionKind.Missing => "Muammoli kunlar",
        _ => "Chetlanishlar",
    };

    private static string YesNo(bool value) => value ? "Ha" : "Yo'q";
}
