using Bonvi.Application.Abstractions;
using Bonvi.Application.Attendance.Abstractions;

namespace Bonvi.Api.Extensions;

public static partial class AuthenticationExtensions
{
    /// <summary>The only routes a scoped user may write to under <c>/api/attendance</c>.</summary>
    private const string ManualPassCreateRoute = "/api/attendance/employees/{employeeId:guid}/manual-passes";

    private const string ManualPassDeleteRoute = "/api/attendance/manual-passes/{passId:guid}";

    /// <summary>
    /// Refused for reads too: the list holds every site's name, address and coordinates, and
    /// a site belongs to no department, so there is nothing to narrow it by.
    /// </summary>
    private const string WorkSitesPath = "/api/attendance/work-sites";

    /// <summary>
    /// Closes everything outside attendance to a scoped user (<see cref="AttendanceScopeRule"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Deny by default, by path, ahead of authorization.</b> The fallback policy only
    /// requires authentication and many routes ask for no specific permission, so taking
    /// permissions away from the role would not close them. A new route is closed to a
    /// scoped user without anyone thinking of it; opening one is an edit here.
    /// </para>
    /// <para>
    /// <b>Writes are matched by route pattern, reads by method.</b> Absence and schedule
    /// requests stay read-only for a scoped user, and that is the reason their handlers need
    /// no scope check. A path that matches no route is refused too, instead of a 404.
    /// </para>
    /// <para>
    /// Device ingest and anonymous routes carry no marker and pass through untouched.
    /// </para>
    /// </remarks>
    /// <param name="app">The application builder.</param>
    public static IApplicationBuilder UseBonviAttendanceScopeGuard(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(async (context, next) =>
        {
            var user = context.RequestServices.GetRequiredService<ICurrentUser>();

            if (user.IsAttendanceScoped() && !ScopedUserMayPass(context))
            {
                await DenyAsync(context).ConfigureAwait(false);
                return;
            }

            await next(context).ConfigureAwait(false);
        });
    }

    private static bool ScopedUserMayPass(HttpContext context)
    {
        var request = context.Request;

        // `StartsWithSegments` compares whole segments, so `/api/authors` is not `/api/auth`.
        if (request.Path.StartsWithSegments("/api/auth", StringComparison.OrdinalIgnoreCase)
            || request.Path.StartsWithSegments("/api/notifications", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!request.Path.StartsWithSegments("/api/attendance", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (request.Path.StartsWithSegments(WorkSitesPath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
        {
            return true;
        }

        var route = RouteTemplate(context.GetEndpoint());

        return (HttpMethods.IsPost(request.Method) && route == ManualPassCreateRoute)
            || (HttpMethods.IsDelete(request.Method) && route == ManualPassDeleteRoute);
    }
}
