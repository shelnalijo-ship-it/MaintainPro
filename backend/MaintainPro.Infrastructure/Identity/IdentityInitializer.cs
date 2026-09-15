using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Application.Identity;
using MaintainPro.Application.Users;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MaintainPro.Infrastructure.Identity;

public sealed class IdentityInitializer(IApplicationDbContext db, IPasswordService passwords,
    IAuditWriter audit, IConfiguration configuration, ILogger<IdentityInitializer> logger, TimeProvider clock)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        var administratorCreated = false;
        var roles = await db.Roles.ToListAsync(cancellationToken);
        if (roles.Any(role => !RoleNames.All.Contains(role.Name)))
            throw new AppException(409, "Unexpected role definitions require administrator review.");
        foreach (var name in RoleNames.All)
        {
            if (roles.Any(role => role.Name == name)) continue;
            var role = new Role { Name = name };
            roles.Add(role);
            db.Roles.Add(role);
        }
        // Read only the five approved bootstrap settings, and never log their values.
        if (!await db.Users.AnyAsync(cancellationToken))
        {
            var employeeId = configuration["BootstrapAdmin:EmployeeId"];
            var firstName = configuration["BootstrapAdmin:FirstName"];
            var lastName = configuration["BootstrapAdmin:LastName"];
            var email = configuration["BootstrapAdmin:Email"];
            var password = configuration["BootstrapAdmin:Password"];
            if (new[] { employeeId, firstName, lastName, email, password }.Any(string.IsNullOrWhiteSpace))
                logger.LogInformation("Initial administrator bootstrap skipped because configuration is incomplete.");
            else
            {
                User? user = null;
                try
                {
                    IdentityValidation.Password(password);
                    var now = clock.GetUtcNow().UtcDateTime;
                    user = new User
                    {
                        EmployeeId = IdentityValidation.EmployeeId(employeeId),
                        FirstName = Guard.Required(firstName, "First name", 100),
                        LastName = Guard.Required(lastName, "Last name", 100),
                        Email = IdentityValidation.Email(email), PasswordHash = string.Empty,
                        IsActive = true, CreatedAt = now, UpdatedAt = now
                    };
                }
                catch (AppException)
                {
                    logger.LogWarning("Initial administrator bootstrap skipped because configured values are invalid.");
                }
                if (user is not null)
                {
                    user.PasswordHash = passwords.Hash(user, password!);
                    var admin = roles.Single(role => role.Name == RoleNames.Admin);
                    user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = admin.Id, User = user, Role = admin });
                    db.Users.Add(user);
                    audit.Record("user.created", nameof(User), user.Id, newValues: UserDto.FromEntity(user));
                    administratorCreated = true;
                }
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (administratorCreated)
            logger.LogInformation("Initial administrator bootstrap completed.");
    }
}
