using Bonvi.Api.Extensions;
using Bonvi.Application.System.Queries.GetSystemInfo;

namespace Bonvi.Api.Endpoints;

/// <summary>
/// Tizim holati endpoint'lari.
/// </summary>
/// <remarks>
/// Endpoint'lar mavzu bo'yicha guruhlanadi. Har bir guruh
/// <c>Map...</c> kengaytma metodi orqali <c>Program.cs</c> ga ulanadi —
/// shunda kompozitsiya ildizi qisqa qoladi.
/// </remarks>
public static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/system").WithTags("System");

        group.MapGet("/info", async (
                GetSystemInfoQueryHandler handler,
                IHostEnvironment environment,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetSystemInfoQuery(environment.EnvironmentName), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .WithName("GetSystemInfo")
            .WithSummary("Tizim holati")
            .WithDescription("Servis versiyasi, muhit va ma'lumotlar bazasi ulanishi holati.")
            .Produces<SystemInfoDto>(StatusCodes.Status200OK);

        return app;
    }
}
