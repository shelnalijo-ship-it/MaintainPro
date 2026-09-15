using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using MaintainPro.Api.Development;
using MaintainPro.Api.Endpoints;
using MaintainPro.Api.Health;
using MaintainPro.Api.Middleware;
using MaintainPro.Api.Security;
using MaintainPro.Api.Scheduling;
using MaintainPro.Application.Abstractions;
using MaintainPro.Infrastructure;
using MaintainPro.Infrastructure.Files;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
// Console/debug logging works without Windows Event Log permissions, including EF tooling.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

if (args.Contains("--database-inspect"))
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException("Database inspection is restricted to Development.");
    Environment.ExitCode = await DatabaseInspector.RunAsync(builder.Configuration);
    return;
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddInfrastructure(builder.Configuration);
var fileStorage = builder.Configuration.GetSection(FileStorageOptions.SectionName).Get<FileStorageOptions>() ?? new();
if (fileStorage.MaxFileSizeBytes is < 1 or > 100 * 1024 * 1024)
    throw new InvalidOperationException("FileStorage:MaxFileSizeBytes must be between 1 and 104857600.");
// Leave room for multipart fields while storage independently counts and limits actual file bytes.
builder.WebHost.ConfigureKestrel(options =>
    options.Limits.MaxRequestBodySize = Math.Max(30_000_000, fileStorage.MaxFileSizeBytes + 1024 * 1024));
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = fileStorage.MaxFileSizeBytes + 1024 * 1024);
builder.Services.AddApplicationAuthentication();
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.RespectRequiredConstructorParameters = true;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
});
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
builder.Services.AddHostedService<IdentityInitializationService>();
builder.Services.Configure<MaintenanceGenerationOptions>(builder.Configuration.GetSection(MaintenanceGenerationOptions.SectionName));
builder.Services.AddHostedService<MaintenanceGenerationService>();
builder.Services.AddOptions<NotificationProcessingOptions>()
    .Bind(builder.Configuration.GetSection(NotificationProcessingOptions.SectionName))
    .Validate(options => options.IntervalMinutes is >= 1 and <= 1440,
        "NotificationProcessing:IntervalMinutes must be between 1 and 1440.")
    .ValidateOnStart();
builder.Services.AddHostedService<NotificationProcessingService>();
builder.Services.AddOptions<CalibrationProcessingOptions>()
    .Bind(builder.Configuration.GetSection(CalibrationProcessingOptions.SectionName))
    .Validate(x => x.IntervalHours is >= 1 and <= 168, "CalibrationProcessing:IntervalHours must be between 1 and 168.")
    .ValidateOnStart();
builder.Services.AddHostedService<CalibrationProcessingService>();
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
        }));
    options.OnRejected = (context, _) => new ValueTask(Results.Problem(statusCode: 429,
        title: "Too many requests", detail: "Wait before trying again.").ExecuteAsync(context.HttpContext));
});
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info.Title = "MaintainPro backend";
        document.Info.Version = "v1";
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
            Description = "Access token returned by the login or refresh endpoint."
        };
        foreach (var (pathName, path) in document.Paths)
        {
            if (pathName is "/health" or "/api/v1/auth/login" or "/api/v1/auth/refresh") continue;
            if (path.Operations is null) continue;
            foreach (var operation in path.Operations.Values)
                operation.Security = [new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", document)] = []
                }];
        }
        return Task.CompletedTask;
    });
});

var app = builder.Build();
app.UseMiddleware<ApiExceptionMiddleware>();
if (!app.Environment.IsEnvironment("Testing")) app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
if (app.Environment.IsDevelopment()) app.MapOpenApi().AllowAnonymous();
app.MapHealthChecks("/health", new HealthCheckOptions()).AllowAnonymous();
app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapMasterDataEndpoints();
app.MapMachineEndpoints();
app.MapPlanningEndpoints();
app.MapWorkOrderEndpoints();
app.MapExecutionEndpoints();
app.MapNotificationEndpoints();
app.MapBreakdownEndpoints();
app.MapCalibrationEndpoints();
app.MapExternalServiceEndpoints();
app.MapReportingEndpoints();

app.Run();

public partial class Program { }
