using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Settings;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests.Processes;

/// <summary>The default mode is <c>warn</c>; a test about what a gate blocks has to say <c>enforce</c>.</summary>
internal static class ProcessTestGates
{
    public static async Task EnforceAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();
        var row = await db.SystemSettings.SingleOrDefaultAsync(setting => setting.Key == SystemSettingKeys.ProcessesGatesMode, cancellationToken);

        if (row is null)
        {
            db.SystemSettings.Add(SystemSetting.Create(SystemSettingKeys.ProcessesGatesMode, SystemSettingKeys.ProcessesGatesEnforce, protector).Value);
        }
        else
        {
            row.SetValue(SystemSettingKeys.ProcessesGatesEnforce, protector).IsSuccess.ShouldBeTrue();
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
