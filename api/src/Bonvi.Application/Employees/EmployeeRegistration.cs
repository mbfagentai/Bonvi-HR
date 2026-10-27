using Bonvi.Application.Employees.Checks;
using Bonvi.Application.Employees.Commands;
using Bonvi.Application.Employees.Common;
using Bonvi.Application.Employees.Competencies;
using Bonvi.Application.Employees.Documents;
using Bonvi.Application.Employees.Employments;
using Bonvi.Application.Employees.Flags;
using Bonvi.Application.Employees.Incentives;
using Bonvi.Application.Employees.Photos;
using Bonvi.Application.Employees.Probations;
using Bonvi.Application.Employees.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Application.Employees;

/// <summary>
/// Xodimlar va mehnat munosabatlari use-case'larini ro'yxatga oladi.
/// </summary>
/// <remarks>
/// <para>
/// Ro'yxat shu yerda, umumiy <c>DependencyInjection</c> da emas: bo'lim
/// o'z bog'liqliklarini o'zi olib boradi va yangi handler qo'shilganda
/// umumiy fayl o'zgarmaydi (ya'ni boshqa vazifalar bilan to'qnashuv bo'lmaydi).
/// </para>
/// <para>
/// <b>Mediator kutubxonasi yo'q.</b> Handler'lar oddiy sinf sifatida
/// ro'yxatga olinadi va endpoint'ga to'g'ridan-to'g'ri in'ektsiya qilinadi:
/// chaqiruv zanjiri stek izida ko'rinadi, refaktoring esa kompilyator
/// nazorati ostida bo'ladi.
/// </para>
/// </remarks>
internal static class EmployeeRegistration
{
    public static IServiceCollection AddEmployees(this IServiceCollection services)
    {
        // Umumiy yordamchilar — so'rov doirasida yashaydi (nom keshi shu sababli).
        services.AddScoped<CatalogNameResolver>();

        // Buyruqni kim kiritgani (SPEC-rollar-va-audit §7.9) — mehnat yo'lida.
        services.AddScoped<UserNameResolver>();

        services.AddScoped<EmployeeMapper>();
        services.AddScoped<EmployeeFlagGuard>();

        // Xodimlar
        services.AddScoped<SearchEmployeesQueryHandler>();
        services.AddScoped<GetEmployeeQueryHandler>();
        services.AddScoped<GetEmployeeTimelineQueryHandler>();
        services.AddScoped<CreateEmployeeCommandHandler>();
        services.AddScoped<UpdateEmployeeCommandHandler>();

        // Rasm
        services.AddScoped<GetEmployeePhotoQueryHandler>();
        services.AddScoped<UploadEmployeePhotoCommandHandler>();
        services.AddScoped<DeleteEmployeePhotoCommandHandler>();

        // Tekshiruv materiallari — hujjat quvurida (qirqilmaydi, kichraytirilmaydi):
        // sababi `EmployeeCheckFile` izohida.
        services.AddScoped<GetEmployeeCheckFilesQueryHandler>();
        services.AddScoped<GetEmployeeCheckFileContentQueryHandler>();
        services.AddScoped<UploadEmployeeCheckFileCommandHandler>();
        services.AddScoped<DeleteEmployeeCheckFileCommandHandler>();

        // Nomzodlik bosqichlari — FAQAT o'qish (sababi so'rovning izohida)
        services.AddScoped<GetEmployeeStagesQueryHandler>();

        // Mehnat munosabatlari
        services.AddScoped<HireEmployeeCommandHandler>();
        services.AddScoped<GetEmploymentQueryHandler>();
        services.AddScoped<ListEmploymentEventsQueryHandler>();
        services.AddScoped<TransferEmployeeCommandHandler>();
        services.AddScoped<AddAdditionalDutyCommandHandler>();
        services.AddScoped<EndAdditionalDutyCommandHandler>();
        services.AddScoped<ChangeSalaryCommandHandler>();
        services.AddScoped<EditEmploymentEventCommandHandler>();
        services.AddScoped<TerminateEmploymentCommandHandler>();

        // Sinov muddati
        services.AddScoped<ListProbationsQueryHandler>();
        services.AddScoped<GetProbationsEndingSoonQueryHandler>();
        services.AddScoped<ResolveProbationCommandHandler>();
        // Muddati tugagan sinov — avtomatik «o'tdi» (egasi 2026-10-22, Hangfire vazifasi)
        services.AddScoped<ProbationAutoPassJob>();
        services.AddScoped<ExtendProbationCommandHandler>();
        services.AddScoped<ListMissingProbationsQueryHandler>();
        services.AddScoped<ConfirmMissingProbationsCommandHandler>();

        // Belgilar
        services.AddScoped<ListFlaggedQueryHandler>();
        services.AddScoped<GetEmployeeFlagsQueryHandler>();
        services.AddScoped<RaiseFlagCommandHandler>();
        services.AddScoped<CloseFlagCommandHandler>();

        // Qobiliyatlar profili
        services.AddScoped<GetEmployeeCompetencyQueryHandler>();
        services.AddScoped<SaveEmployeeCompetencyCommandHandler>();

        // Rag'bat va jarima
        services.AddScoped<IncentiveTypeResolver>();
        services.AddScoped<GetEmployeeIncentivesQueryHandler>();

        // Umumiy ro'yxat («Rag'bat va jarima» sahifasi) — xodim kartasidagi
        // tabdan ALOHIDA ishlovchi: u repozitoriy bilan bitta odamning
        // yozuvlarini yuklaydi, bu esa o'qish modeli bilan butun tizimni
        // filtrlab, saralab va sahifalab beradi.
        services.AddScoped<ListIncentivesQueryHandler>();

        services.AddScoped<GrantIncentiveCommandHandler>();
        services.AddScoped<UpdateIncentiveCommandHandler>();
        services.AddScoped<DeleteIncentiveCommandHandler>();

        // Hujjatlar
        services.AddScoped<GetEmployeeDocumentsQueryHandler>();
        services.AddScoped<GetEmployeeDocumentFileQueryHandler>();
        services.AddScoped<UploadEmployeeDocumentCommandHandler>();
        services.AddScoped<UpdateEmployeeDocumentCommandHandler>();
        services.AddScoped<DeleteEmployeeDocumentCommandHandler>();

        return services;
    }
}
