using MaintainPro.Infrastructure.Identity;
using MaintainPro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Api.Security;

public sealed class IdentityInitializationService(IServiceScopeFactory scopes, IHostEnvironment environment)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (environment.IsEnvironment("Testing"))
            return;

        using var scope = scopes.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Apply any pending Entity Framework migrations before
        // attempting to seed roles/users.
        await db.Database.MigrateAsync(cancellationToken);

        await scope.ServiceProvider
            .GetRequiredService<IdentityInitializer>()
            .InitializeAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}