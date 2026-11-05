using Bonvi.Application.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Blanks;
using Bonvi.Domain.Processes;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bonvi.Infrastructure.Persistence;

/// <summary>
/// <see cref="IDatabaseFault"/> ning PostgreSQL uchun amalga oshirilishi.
/// </summary>
/// <remarks>
/// Drayver haqidagi bilim SHU YERDA to'xtaydi — yuqoridagi qatlamlar faqat
/// «nom takrorlandimi» degan javobni oladi. Sabab <see cref="IDatabaseFault"/>
/// izohida.
/// </remarks>
internal sealed class PostgresFault : IDatabaseFault
{
    /// <summary>
    /// Nom bo'yicha unikal indekslarning umumiy naqshi.
    /// </summary>
    /// <remarks>
    /// Indekslar migratsiyada aynan shu qoida bilan yaratiladi:
    /// <c>ux_&lt;jadval&gt;_name</c>. Nom o'zgarsa, bu yerdagi tekshiruv
    /// jimgina ishlamay qo'yadi — shu sababli ikkalasi bir vaqtda
    /// o'zgartiriladi.
    /// </remarks>
    private const string NameIndexPrefix = "ux_";

    /// <summary>The natural keys of the process tables, names from the <c>Processes</c> migration.</summary>
    /// <remarks>Renamed there and not here, a lost race answers 500 instead of 409, so both change together.</remarks>
    private static readonly HashSet<string> ProcessNaturalKeyIndexes = new(StringComparer.Ordinal)
    {
        "ix_process_cases_process_code_subject_ref",
        "ux_process_cases_process_code_employment_id",
        "ix_process_stage_bindings_binding_code",
        "ix_process_holidays_date",
    };
    private const string NameIndexSuffix = "_name";

    /// <summary>
    /// O'tishlar takrorini to'suvchi indeks — <c>AttendancePassConfiguration</c> dagi nom.
    /// </summary>
    /// <remarks>
    /// Nom o'zgarsa, takror 200 o'rniga 500 bo'lib qaytadi va qurilma uni
    /// cheksiz qayta yuboradi — shu sababli ikkalasi bir vaqtda o'zgartiriladi.
    /// </remarks>
    private const string PassDedupeIndex = "ux_attendance_passes_device_dedupe";

    /// <summary>
    /// Kun yozuvining (xodim, sana) unikalligi — <c>AttendanceDayConfiguration</c> dagi nom.
    /// </summary>
    /// <remarks>
    /// Nom o'zgarsa, parallel qayta hisoblashning birinchi yozuvi yana 500 bo'lib
    /// qaytadi — shu sababli ikkalasi bir vaqtda o'zgartiriladi.
    /// </remarks>
    private const string DayUniqueIndex = "ux_attendance_days_employee_date";

    /// <summary>
    /// Xodimga bitta Bonvi raqami — <c>AttendanceIdentityConfiguration</c> dagi nom.
    /// </summary>
    /// <remarks>
    /// Nom o'zgarsa, ikki agentning parallel so'rovi 409 o'rniga 500 bo'lib
    /// qaytadi — shu sababli ikkalasi bir vaqtda o'zgartiriladi.
    /// </remarks>
    public const string ManagedNumberIndex = "ux_attendance_identities_managed_employee";

    /// <summary>
    /// Xodimga bitta ilova hisobi — <c>EmployeeAccountConfiguration</c> dagi nom.
    /// </summary>
    /// <remarks>
    /// Nom o'zgarsa, avtomatik ochishdagi poyga 500 bo'lib qaytadi — shu
    /// sababli ikkalasi bir vaqtda o'zgartiriladi.
    /// </remarks>
    private const string EmployeeAccountIndex = "ux_employee_accounts_employee";

    /// <summary>
    /// Jadval so'rovida bir sana bitta kun — <c>ScheduleChangeDayConfiguration</c> dagi (konvensiya) nom.
    /// </summary>
    /// <remarks>
    /// Domen takror sanani rad etadi, ya'ni bu indeksga faqat bitta so'rovni IKKI tahrir bir vaqtda
    /// almashtirganda uriladi: ikkalasi eski kunni o'chirib, bir xil sanali yangi kun qo'shadi
    /// (SPEC-sorov-tahriri §3.3). Nom o'zgarsa, parallel tahrir 409 o'rniga 500 bo'ladi — shu
    /// sababli ikkalasi bir vaqtda o'zgartiriladi.
    /// </remarks>
    private const string ScheduleChangeDayIndex = "ix_schedule_change_request_days_request_id_date";

    /// <summary>Onlayn anketaning yuborish identifikatori — <c>CandidateFormConfiguration</c> dagi (konvensiya) nom.</summary>
    private const string FormSubmissionIndex = "ix_candidate_forms_client_submission_id";

    /// <summary>Blanka yaratish identifikatori — <c>BlankSubmissionConfiguration</c> dagi (konvensiya) nom.</summary>
    private const string BlankRequestIndex = "ix_blank_submissions_client_request_id";

    private const string BlankStepIndex = "ix_blank_submission_steps_submission_id_step_code";

    public bool IsDuplicateName(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var postgres = UniqueViolation(exception);

        return postgres?.ConstraintName is { } name
            && name.StartsWith(NameIndexPrefix, StringComparison.Ordinal)
            && name.EndsWith(NameIndexSuffix, StringComparison.Ordinal);
    }

    public bool IsDuplicatePass(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return string.Equals(UniqueViolation(exception)?.ConstraintName, PassDedupeIndex, StringComparison.Ordinal);
    }

    public bool IsDuplicateDevicePersonNo(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return string.Equals(UniqueViolation(exception)?.ConstraintName, ManagedNumberIndex, StringComparison.Ordinal);
    }

    public bool IsConcurrentDayWrite(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (string.Equals(UniqueViolation(exception)?.ConstraintName, DayUniqueIndex, StringComparison.Ordinal))
        {
            return true;
        }

        // Kun jadvalida parallellik belgisi (concurrency token) YO'Q: EF bu
        // istisnoni faqat UPDATE/DELETE kutilgan bitta qator o'rniga nolga
        // tekkanda tashlaydi — ya'ni qatorni boshqa yozuvchi o'chirib ulgurgan.
        // Faqat kun yozuvlari bo'lsa: boshqa jadvaldagi bunday holat bu
        // yerda «qayta o'qib yoz» bilan yashirilmasligi kerak.
        return exception is DbUpdateConcurrencyException concurrency
            && concurrency.Entries.Count > 0
            && concurrency.Entries.All(entry => entry.Entity is AttendanceDay);
    }

    public bool IsDuplicateEmployeeAccount(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return string.Equals(UniqueViolation(exception)?.ConstraintName, EmployeeAccountIndex, StringComparison.Ordinal);
    }

    public bool IsStaleRequestWrite(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        // Ikki tahrir bir so'rovning kunlarini birga almashtirdi — ikkinchisining yangi kuni
        // g'olibning o'sha sanali kuniga uriladi (EF buyruqlar tartibida u versiya tekshiruvidan oldin).
        if (string.Equals(UniqueViolation(exception)?.ConstraintName, ScheduleChangeDayIndex, StringComparison.Ordinal))
        {
            return true;
        }

        // Faqat so'rov agregatlari: boshqa jadvaldagi parallellik xatosi bu yerda 409 bilan
        // yashirilmasligi kerak (`IsConcurrentDayWrite` dagi bilan bir xil ehtiyot).
        return exception is DbUpdateConcurrencyException concurrency
            && concurrency.Entries.Count > 0
            && concurrency.Entries.All(entry =>
                entry.Entity is AbsenceRequest or ScheduleChangeRequest or ScheduleChangeDay);
    }

    public bool IsDuplicateFormSubmission(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return string.Equals(UniqueViolation(exception)?.ConstraintName, FormSubmissionIndex, StringComparison.Ordinal);
    }

    public bool IsDuplicateBlankRequest(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return string.Equals(UniqueViolation(exception)?.ConstraintName, BlankRequestIndex, StringComparison.Ordinal);
    }

    public bool IsStaleBlankWrite(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        // Parallel yuborish: ikkinchi tranzaksiya UPDATE ... WHERE version dan oldin qadamlarni
        // INSERT qiladi va birinchisi yozgan qadamlarga uriladi — natija bir xil: blanka eskirgan.
        if (string.Equals(UniqueViolation(exception)?.ConstraintName, BlankStepIndex, StringComparison.Ordinal))
        {
            return true;
        }

        return exception is DbUpdateConcurrencyException concurrency
            && concurrency.Entries.Count > 0
            && concurrency.Entries.All(entry =>
                entry.Entity is BlankSubmission or BlankSubmissionStep or BlankAttachment);
    }

    public bool IsConsumedGateOverrideWrite(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception is DbUpdateConcurrencyException concurrency
            && concurrency.Entries.Any(entry => entry.Entity is ProcessGateOverride);
    }

    public bool IsStaleProcessWrite(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (UniqueViolation(exception)?.ConstraintName is { } constraint && ProcessNaturalKeyIndexes.Contains(constraint))
        {
            return true;
        }

        return exception is DbUpdateConcurrencyException concurrency
            && concurrency.Entries.Count > 0
            && concurrency.Entries.All(entry => entry.Entity is ProcessCase or ProcessCaseStep or ProcessStageBinding or ProcessHoliday);
    }

    /// <summary>Unikallik buzilishi bo'lsa — drayver istisnosi, aks holda <c>null</c>.</summary>
    /// <remarks>
    /// Istisno EF ning <c>DbUpdateException</c> i ichida keladi, lekin
    /// to'g'ridan-to'g'ri ham berilishi mumkin — ikkalasi ham qaraladi.
    /// </remarks>
    private static PostgresException? UniqueViolation(Exception exception)
    {
        var postgres = exception as PostgresException
            ?? (exception as DbUpdateException)?.InnerException as PostgresException;

        return postgres is { SqlState: PostgresErrorCodes.UniqueViolation } ? postgres : null;
    }
}
