using Bonvi.Application.Recruiting.Candidates;
using Bonvi.Application.Recruiting.Offers;
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
        services.AddScoped<DeleteCandidateCommandHandler>();

        // Rezyume fayli — xodim hujjatlari bilan bir xil quvurdan o'tadi
        // (`DocumentFileStore` allaqachon `Common` da ro'yxatga olingan).
        services.AddScoped<UploadCandidateResumeCommandHandler>();
        services.AddScoped<GetCandidateResumeQueryHandler>();
        services.AddScoped<DeleteCandidateResumeCommandHandler>();

        // Ish takliflari
        services.AddScoped<ListCandidateOffersQueryHandler>();
        services.AddScoped<SendJobOfferCommandHandler>();
        services.AddScoped<ChangeJobOfferCommandHandler>();

        // Taklifni qabul qilish — 1-bosqichning ishga qabul oqimini
        // chaqiradi, shu sababli `HireEmployeeCommandHandler` ga bog'liq
        // (u `EmployeeRegistration` da ro'yxatga olingan).
        services.AddScoped<AcceptJobOfferCommandHandler>();

        return services;
    }
}
