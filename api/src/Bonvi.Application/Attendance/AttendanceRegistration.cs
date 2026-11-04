using Bonvi.Application.Attendance.Agent;
using Bonvi.Application.Attendance.Devices;
using Bonvi.Application.Attendance.Identities;
using Bonvi.Application.Attendance.Mobile;
using Bonvi.Application.Attendance.Ingest;
using Bonvi.Application.Attendance.Queries;
using Bonvi.Application.Attendance.Requests;
using Bonvi.Application.Attendance.Recompute;
using Bonvi.Application.Attendance.Retention;
using Bonvi.Application.Attendance.Schedules;
using Bonvi.Application.Attendance.WorkSites;
using Bonvi.Application.Catalog.Common;
using Bonvi.Domain.Catalog;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Application.Attendance;

/// <summary>Davomat (3-bosqich) use-case'larini ro'yxatga oladi — modulning YAGONA kirish nuqtasi.</summary>
/// <remarks>
/// <para>
/// <b>Qabul yo'li o'z faylida</b> (<see cref="IngestRegistration"/>) va shu
/// yerdan chaqiriladi (SPEC §10.2.1): u boshqaruv use-case'laridan oldin
/// qurilgan va endpointi paydo bo'lgan lahzadanoq ishlashi kerak edi.
/// <c>AddApplication</c> endi faqat shu metodni chaqiradi — modul ikki joydan
/// ro'yxatga olinmaydi.
/// </para>
/// <para>
/// Hisoblash yadrosi (<c>ScheduleResolver</c>, <c>AttendanceDayBuilder</c>)
/// bu yerda YO'Q va bu ataylab: ular servis emas, bitta to'plam ma'lumoti
/// ustida quriladi (B4). DI da faqat ularni YURGIZADIGAN
/// <see cref="AttendanceDayRecomputer"/> turadi.
/// </para>
/// <para>
/// ⚠️ Fon vazifalari (<see cref="AttendanceBackfillJob"/>,
/// <see cref="AttendanceRecomputeRangeJob"/>) oddiy scoped sinf: navbat
/// (sinxron yoki Hangfire) ularni DI dan oladi. Ro'yxatdan tushib qolsa,
/// moslik yaratish so'rovi «vazifa topilmadi» bilan yiqiladi.
/// </para>
/// </remarks>
internal static class AttendanceRegistration
{
    public static IServiceCollection AddAttendance(this IServiceCollection services)
    {
        services.AddAttendanceIngest();

        // Jurnal, taxta, tabel, hisobotlar va eksport (B5)
        services.AddScoped<SearchPassesQueryHandler>();
        services.AddScoped<GetPassQueryHandler>();
        services.AddScoped<GetPassPhotoQueryHandler>();
        services.AddScoped<GetBoardQueryHandler>();
        services.AddScoped<GetFormerEmployeeAlertsQueryHandler>();
        services.AddScoped<GetAttendanceDashboardQueryHandler>();
        services.AddScoped<GetAttendanceTrendQueryHandler>();
        services.AddScoped<GetUpcomingBirthdaysQueryHandler>();

        // Qurilmaga yozish — agent va holat (SPEC-agent §D)
        services.AddScoped<GetAgentRosterQueryHandler>();
        services.AddScoped<ReportAgentEnrollmentsCommandHandler>();
        services.AddScoped<ReportDeviceClockCommandHandler>();
        services.AddScoped<GetDeviceEnrollmentsQueryHandler>();
        services.AddScoped<GetEmployeeEnrollmentsQueryHandler>();
        services.AddScoped<ResyncEmployeeEnrollmentsCommandHandler>();
        services.AddScoped<ResyncDeviceEnrollmentsCommandHandler>();
        services.AddScoped<ReportAgentStatusCommandHandler>();
        services.AddScoped<ApproveDeviceRemovalsCommandHandler>();

        // Yillik to'r (SPEC-jadval-to'ri §C)
        services.AddScoped<GetScheduleYearQueryHandler>();
        services.AddScoped<SetScheduleDatesCommandHandler>();
        services.AddScoped<FillScheduleYearCommandHandler>();

        // Yo'qlik so'rovlari (SPEC-so'rovlar §B4)
        services.AddScoped<CreateAbsenceRequestCommandHandler>();
        services.AddScoped<UpdateAbsenceRequestCommandHandler>();
        services.AddScoped<DecideAbsenceRequestCommandHandler>();
        services.AddScoped<CancelAbsenceRequestCommandHandler>();

        // Jadval so'rovlari (SPEC-verifikatsiya-jadval-sorovi §C).
        services.AddScoped<EmployeePlanReader>();
        services.AddScoped<CreateScheduleChangeRequestCommandHandler>();
        services.AddScoped<UpdateScheduleChangeRequestCommandHandler>();
        services.AddScoped<DecideScheduleChangeRequestCommandHandler>();
        services.AddScoped<CancelScheduleChangeRequestCommandHandler>();
        services.AddScoped<SearchScheduleChangeRequestsQueryHandler>();
        services.AddScoped<GetScheduleChangeRequestQueryHandler>();

        // Jadval so'rovidagi tanaffus qoidasi (SPEC-sorov-tahriri §2.2) — sozlamadan, so'rov davomida.
        services.AddScoped<RequestBreakRuleReader>();
        services.AddScoped<GetRequestBreakRuleQueryHandler>();

        // Yo'qlik turining «ish vaqti» bayrog'i o'zgarsa — so'rov kunlari qayta hisoblanadi.
        services.AddScoped<IReferenceSavedHook<AbsenceType>, AbsenceTypeWorkTimeHook>();
        services.AddScoped<SearchAbsenceRequestsQueryHandler>();
        services.AddScoped<GetPendingAbsenceRequestCountQueryHandler>();
        services.AddScoped<GetTimesheetQueryHandler>();
        services.AddScoped<GetAttendanceExceptionsQueryHandler>();
        services.AddScoped<GetAttendanceSummaryQueryHandler>();
        services.AddScoped<ExportTimesheetQueryHandler>();
        services.AddScoped<ExportPassesQueryHandler>();
        services.AddScoped<ExportAttendanceExceptionsQueryHandler>();
        services.AddScoped<ExportAttendanceSummaryQueryHandler>();

        // Obyektlar — geo-to'siq ma'lumotnomasi (SPEC-xodim-ilovasi §D1).
        // Umumiy ma'lumotnoma shtampidan FOYDALANILMAYDI: yozish ruxsati
        // boshqa (`attendance.manage`) va uchta maydon davomatga xos
        // (sababi `SaveWorkSiteCommand` izohida).
        services.AddScoped<ListWorkSitesQueryHandler>();
        services.AddScoped<GetWorkSiteQueryHandler>();
        services.AddScoped<CreateWorkSiteCommandHandler>();
        services.AddScoped<UpdateWorkSiteCommandHandler>();
        services.AddScoped<SetWorkSiteActiveCommandHandler>();

        // Telefondan belgilash — bitta ishlovchi, butun oqim shunda
        // (SPEC-xodim-ilovasi §E).
        services.AddScoped<MobileCheckInCommandHandler>();
        services.AddScoped<Manual.AddManualPassCommandHandler>();
        services.AddScoped<Manual.DeleteManualPassCommandHandler>();
        services.AddScoped<Manual.GetDayPassesQueryHandler>();

        // Qurilmalar
        services.AddScoped<AttendanceDeviceRules>();
        services.AddScoped<AttendanceDeviceMapper>();
        services.AddScoped<ListDevicesQueryHandler>();
        services.AddScoped<GetDeviceQueryHandler>();
        services.AddScoped<RegisterDeviceCommandHandler>();
        services.AddScoped<UpdateDeviceCommandHandler>();
        services.AddScoped<RotateDeviceTokenCommandHandler>();
        services.AddScoped<SetDeviceActiveCommandHandler>();
        services.AddScoped<DeleteDeviceCommandHandler>();

        // Mosliklar va Verifix importi
        services.AddScoped<SearchIdentitiesQueryHandler>();
        services.AddScoped<ListUnmatchedQueryHandler>();
        services.AddScoped<CreateIdentityCommandHandler>();
        services.AddScoped<DeleteIdentityCommandHandler>();
        services.AddScoped<PreviewIdentityImportQueryHandler>();
        services.AddScoped<ConfirmIdentityImportCommandHandler>();

        // Jadvallar va biriktirmalar
        services.AddScoped<ListWorkSchedulesQueryHandler>();
        services.AddScoped<GetWorkScheduleQueryHandler>();
        services.AddScoped<CreateWorkScheduleCommandHandler>();
        services.AddScoped<UpdateWorkScheduleCommandHandler>();
        services.AddScoped<SetWorkScheduleActiveCommandHandler>();
        services.AddScoped<DeleteWorkScheduleCommandHandler>();
        services.AddScoped<ListScheduleAssignmentsQueryHandler>();
        services.AddScoped<CreateScheduleAssignmentCommandHandler>();
        services.AddScoped<DeleteScheduleAssignmentCommandHandler>();
        services.AddScoped<ResolveScheduleQueryHandler>();

        // Qayta hisoblash va uning fon vazifalari
        services.AddScoped<AttendanceDayRecomputer>();
        services.AddScoped<AttendanceBackfillJob>();
        services.AddScoped<AttendanceRecomputeRangeJob>();
        services.AddScoped<AttendanceScheduleDatesJob>();
        services.AddScoped<RecomputeAttendanceCommandHandler>();

        // Takroriy vazifalar (B7, SPEC §9.2) va saqlash muddati. Recurring
        // jadvali Api dagi `BackgroundJobs` da — bu yerda faqat sinflar.
        services.AddScoped<AttendanceRetention>();
        services.AddScoped<AttendanceDirtyRecomputeJob>();
        services.AddScoped<AttendanceNightlyRecomputeJob>();
        services.AddScoped<AttendanceStartedShiftsJob>();
        services.AddScoped<AttendanceEmploymentChangesJob>();
        services.AddScoped<AttendancePhotoPurgeJob>();
        services.AddScoped<AttendancePassPurgeJob>();

        return services;
    }
}
