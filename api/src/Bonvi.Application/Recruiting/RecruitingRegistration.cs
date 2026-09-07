using Bonvi.Application.Recruiting.Candidates;
using Bonvi.Application.Recruiting.Stages;
using Bonvi.Application.Recruiting.Vacancies;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Application.Recruiting;

/// <summary>
/// Rekruting (2-bosqich) use-case'larini ro'yxatga oladi.
/// </summary>
/// <remarks>
/// Alohida faylda — <c>DependencyInjection.cs</c> yuzlab qatorga
/// aylanmasligi uchun har bir mavzu o'z ro'yxatini o'zi olib boradi
/// (<c>CatalogRegistration</c> va <c>SafetyRegistration</c> bilan bir xil).
/// </remarks>
internal static class RecruitingRegistration
{
    public static IServiceCollection AddRecruiting(this IServiceCollection services)
    {
        services.AddScoped<RecruitingMapper>();

        // Voronka bosqichlari — ma'lumotnoma, lekin O'Z handlerlari bilan
        // (tartib va qayta tartiblash umumiy ma'lumotnoma mexanizmida yo'q).
        services.AddScoped<ListPipelineStagesQueryHandler>();
        services.AddScoped<CreatePipelineStageCommandHandler>();
        services.AddScoped<UpdatePipelineStageCommandHandler>();
        services.AddScoped<ReorderPipelineStagesCommandHandler>();
        services.AddScoped<SetPipelineStageActiveCommandHandler>();

        // Vakansiyalar
        services.AddScoped<VacancyReferenceGuard>();
        services.AddScoped<ListVacanciesQueryHandler>();
        services.AddScoped<GetVacancyQueryHandler>();
        services.AddScoped<OpenVacancyCommandHandler>();
        services.AddScoped<UpdateVacancyCommandHandler>();
        services.AddScoped<ChangeVacancyStateCommandHandler>();
        services.AddScoped<DeleteVacancyCommandHandler>();

        // Nomzodlar
        services.AddScoped<SearchCandidatesQueryHandler>();
        services.AddScoped<GetCandidateQueryHandler>();
        services.AddScoped<RegisterCandidateCommandHandler>();
        services.AddScoped<ListPendingRejectionNoticesQueryHandler>();
        services.AddScoped<MarkRejectionNoticeSentCommandHandler>();
        services.AddScoped<UpdateCandidateCommandHandler>();
        services.AddScoped<ReassignCandidateCommandHandler>();
        services.AddScoped<RecordCandidateStageCommandHandler>();
        services.AddScoped<CloseCandidateCommandHandler>();
        services.AddScoped<ReturnCandidateFromReserveCommandHandler>();

        // Nomzodni ishga olish — `HireEmployeeCommandHandler` ga bog'liq
        // (u `EmployeeRegistration` da ro'yxatga olingan): xodimni
        // 1-bosqichning oqimi yaratadi, rekruting o'z yo'lini yozmaydi.
        services.AddScoped<HireCandidateCommandHandler>();

        // Rezyume fayli — xodim hujjatlari bilan bir xil quvurdan o'tadi
        // (`DocumentFileStore` allaqachon `Common` da ro'yxatga olingan).
        services.AddScoped<UploadCandidateResumeCommandHandler>();
        services.AddScoped<GetCandidateResumeQueryHandler>();
        services.AddScoped<DeleteCandidateResumeCommandHandler>();

        // Nomzod surati — xodim rasmi bilan bir xil quvur
        // (`SquarePhotoStore` ham `Common` da ro'yxatga olingan).
        services.AddScoped<UploadCandidatePhotoCommandHandler>();
        services.AddScoped<GetCandidatePhotoQueryHandler>();
        services.AddScoped<DeleteCandidatePhotoCommandHandler>();

        return services;
    }
}
