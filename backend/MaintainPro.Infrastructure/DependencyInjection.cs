using MaintainPro.Infrastructure.Persistence;
using MaintainPro.Infrastructure.Identity;
using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Audit;
using MaintainPro.Application.Identity;
using MaintainPro.Application.Users;
using MaintainPro.Application.Machines;
using MaintainPro.Application.MasterData;
using MaintainPro.Application.Planning;
using MaintainPro.Application.WorkOrders;
using MaintainPro.Infrastructure.Planning;
using MaintainPro.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MaintainPro.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is not configured.");
        }

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddSingleton(TimeProvider.System);
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<MachineService>();
        services.AddScoped<MasterDataService>();
        services.AddScoped<IdentityInitializer>();
        services.AddSingleton<RecurrenceService>();
        services.AddScoped<MaintenanceTypeService>();
        services.AddScoped<MaintenancePlanService>();
        services.AddScoped<WorkOrderService>();
        services.AddScoped<WorkOrderNumberAllocator>();
        services.AddScoped<IGenerationConcurrency, GenerationConcurrency>();
        services.AddScoped<WorkOrderGenerationService>();

        return services;
    }
}
