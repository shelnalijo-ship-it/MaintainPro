using MaintainPro.Infrastructure.Identity;

namespace MaintainPro.Api.Security;

public sealed class IdentityInitializationService(IServiceScopeFactory scopes, IHostEnvironment environment)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (environment.IsEnvironment("Testing")) return;
        using var scope = scopes.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IdentityInitializer>().InitializeAsync(cancellationToken);
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
