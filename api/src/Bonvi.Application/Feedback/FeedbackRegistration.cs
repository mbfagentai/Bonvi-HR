using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Application.Feedback;

/// <summary>
/// «Qayta aloqa» (exit interview, M10) use-case'larini ro'yxatga oladi.
/// </summary>
/// <remarks>
/// <c>CatalogNameResolver</c> qayta ro'yxatga OLINMAYDI — u
/// <c>EmployeeRegistration.AddEmployees</c> orqali allaqachon Scoped
/// qilib qo'yilgan va <c>Program.cs</c> ikkalasini ham chaqiradi.
/// </remarks>
internal static class FeedbackRegistration
{
    public static IServiceCollection AddFeedback(this IServiceCollection services)
    {
        services.AddScoped<ExitInterviewMapper>();

        services.AddScoped<RecordExitInterviewCommandHandler>();
        services.AddScoped<UpdateExitInterviewCommandHandler>();
        services.AddScoped<DeleteExitInterviewCommandHandler>();

        services.AddScoped<ListExitInterviewsQueryHandler>();
        services.AddScoped<GetExitInterviewByEmploymentQueryHandler>();

        return services;
    }
}
