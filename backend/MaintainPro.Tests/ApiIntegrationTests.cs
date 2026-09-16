using System.Net;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;
using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Audit;
using MaintainPro.Application.Common;
using MaintainPro.Application.Identity;
using MaintainPro.Application.Machines;
using MaintainPro.Application.Users;
using MaintainPro.Application.Planning;
using MaintainPro.Application.WorkOrders;
using MaintainPro.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace MaintainPro.Tests;

public sealed class ApiIntegrationTests
{
    private static readonly JsonSerializerOptions ApiJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Protected_endpoint_returns_a_problem_details_bearer_challenge()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        using var factory = new IsolatedApiFactory(fixture);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/auth/me");

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized);
        Assert.Contains(response.Headers.WwwAuthenticate, header => header.Scheme == "Bearer");
    }

    [Fact]
    public async Task Health_is_public_and_exposes_only_health_status()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        using var factory = new IsolatedApiFactory(fixture);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Machine_and_user_lists_accept_omitted_query_parameters_with_default_pagination()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var administrator = await fixture.AsAdminAsync();
        using var factory = new IsolatedApiFactory(fixture);
        using var client = factory.CreateClient();
        await SignInAsync(client, administrator.Email, fixture.Password);

        foreach (var path in new[] { "/api/v1/machines", "/api/v1/users", "/api/v1/maintenance-plans", "/api/v1/work-orders" })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(1, json.RootElement.GetProperty("page").GetInt32());
            Assert.Equal(20, json.RootElement.GetProperty("pageSize").GetInt32());
            Assert.Equal(JsonValueKind.Array, json.RootElement.GetProperty("items").ValueKind);
        }
    }

    [Theory]
    [InlineData("PATCH", "/api/v1/machines/{id}/status")]
    [InlineData("PATCH", "/api/v1/departments/{id}/status")]
    [InlineData("PATCH", "/api/v1/locations/{id}/status")]
    [InlineData("PATCH", "/api/v1/machine-categories/{id}/status")]
    [InlineData("PATCH", "/api/v1/users/{id}/status")]
    [InlineData("PATCH", "/api/v1/maintenance-plans/{id}/status")]
    [InlineData("PATCH", "/api/v1/maintenance-types/{id}/status")]
    [InlineData("POST", "/api/v1/machines/{id}/assign-owner")]
    public async Task Required_mutation_values_cannot_be_silently_defaulted_from_an_empty_body(string method, string path)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var administrator = await fixture.AsAdminAsync();
        using var factory = new IsolatedApiFactory(fixture);
        using var client = factory.CreateClient();
        await SignInAsync(client, administrator.Email, fixture.Password);
        using var request = new HttpRequestMessage(new HttpMethod(method), path.Replace("{id}", Guid.NewGuid().ToString()))
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };

        await AssertProblemAsync(await client.SendAsync(request), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Planning_checklist_and_generation_API_expose_versioned_historical_jobs_with_scoped_access()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await PlanningTestData.CreateAsync(fixture);
        using var factory = new IsolatedApiFactory(fixture);
        using var client = factory.CreateClient();
        await SignInAsync(client, data.Administrator.Email, fixture.Password);
        var createdResponse = await client.PostAsJsonAsync("/api/v1/maintenance-plans", data.Request, ApiJson);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var plan = (await createdResponse.Content.ReadFromJsonAsync<MaintenancePlanDto>(ApiJson))!;
        var checklistResponse = await client.PutAsJsonAsync($"/api/v1/maintenance-plans/{plan.Id}/checklist",
            PlanningTestData.Checklist(), ApiJson);
        Assert.Equal(HttpStatusCode.OK, checklistResponse.StatusCode);
        Assert.Equal(1, (await checklistResponse.Content.ReadFromJsonAsync<ChecklistTemplateDto>(ApiJson))!.Version);
        var generated = await client.PostAsJsonAsync("/api/v1/maintenance-plans/generate-due", new GenerationRequest(), ApiJson);
        Assert.Equal(HttpStatusCode.OK, generated.StatusCode);
        Assert.Equal(1, (await generated.Content.ReadFromJsonAsync<GenerationSummary>(ApiJson))!.WorkOrdersCreated);
        var repeated = await client.PostAsJsonAsync("/api/v1/maintenance-plans/generate-due", new GenerationRequest(), ApiJson);
        Assert.Equal(0, (await repeated.Content.ReadFromJsonAsync<GenerationSummary>(ApiJson))!.WorkOrdersCreated);
        var revision = await client.PutAsJsonAsync($"/api/v1/maintenance-plans/{plan.Id}/checklist",
            PlanningTestData.Checklist("New checklist"), ApiJson);
        Assert.Equal(2, (await revision.Content.ReadFromJsonAsync<ChecklistTemplateDto>(ApiJson))!.Version);
        Assert.Equal(1, (await client.GetFromJsonAsync<ChecklistTemplateDto>(
            $"/api/v1/maintenance-plans/{plan.Id}/checklist?version=1", ApiJson))!.Version);
        await AssertProblemAsync(await client.GetAsync("/api/v1/work-orders/calendar"), HttpStatusCode.BadRequest);
        var calendar = await client.GetFromJsonAsync<WorkOrderCalendarEventDto[]>(
            $"/api/v1/work-orders/calendar?from={plan.StartDate:yyyy-MM-dd}&to={plan.StartDate:yyyy-MM-dd}", ApiJson);
        var orderId = Assert.Single(calendar!).Id;
        var order = (await client.GetFromJsonAsync<WorkOrderDto>($"/api/v1/work-orders/{orderId}", ApiJson))!;
        Assert.Equal(1, order.Definition.ChecklistVersion);

        await SignInAsync(client, data.Technician.Email, fixture.Password);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/work-orders/{orderId}")).StatusCode);
        await AssertProblemAsync(await client.PostAsJsonAsync("/api/v1/maintenance-plans/generate-due", new GenerationRequest()),
            HttpStatusCode.Forbidden);
        var unrelated = await fixture.SeedUserAsync("TECHNICIAN");
        await SignInAsync(client, unrelated.Email, fixture.Password);
        await AssertProblemAsync(await client.GetAsync($"/api/v1/work-orders/{orderId}"), HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("signature")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("expired")]
    public async Task Bearer_validation_rejects_invalid_tokens_even_when_the_user_session_exists(string defect)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var user = await fixture.SeedUserAsync("TECHNICIAN");
        var login = await fixture.Auth.LoginAsync(new LoginRequest(user.Email, fixture.Password));
        using var factory = new IsolatedApiFactory(fixture);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        var original = new JwtSecurityTokenHandler().ReadJwtToken(login.AccessToken);
        var key = defect == "signature" ? Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
            : fixture.JwtSettings.SigningKey;
        var altered = new JwtSecurityToken(
            defect == "issuer" ? "Untrusted.Issuer" : fixture.JwtSettings.Issuer,
            defect == "audience" ? "Another.Application" : fixture.JwtSettings.Audience,
            original.Claims.Where(claim => claim.Type is not "iss" and not "aud" and not "nbf" and not "exp"),
            DateTime.UtcNow.AddMinutes(-30),
            defect == "expired" ? DateTime.UtcNow.AddMinutes(-2) : DateTime.UtcNow.AddMinutes(10),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        var invalid = defect == "malformed" ? "not-a-valid-jwt" : new JwtSecurityTokenHandler().WriteToken(altered);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", invalid);

        await AssertProblemAsync(await client.GetAsync("/api/v1/auth/me"), HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Real_login_and_bearer_authorization_enforce_admin_policy_and_logout()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var technician = await fixture.SeedUserAsync("TECHNICIAN");
        using var factory = new IsolatedApiFactory(fixture);
        using var client = factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(technician.Email, fixture.Password));
        loginResponse.EnsureSuccessStatusCode();
        var loginJson = await loginResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("PasswordHash", loginJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(technician.PasswordHash, loginJson);
        var login = (await loginResponse.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var me = await client.GetFromJsonAsync<UserDto>("/api/v1/auth/me");
        Assert.Equal(technician.Id, me!.Id);
        await AssertProblemAsync(await client.GetAsync("/api/v1/users"), HttpStatusCode.Forbidden);
        var logout = await client.PostAsJsonAsync("/api/v1/auth/logout", new RefreshRequest(login.RefreshToken));
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        await AssertProblemAsync(await client.GetAsync("/api/v1/auth/me"), HttpStatusCode.Unauthorized);
        await AssertProblemAsync(await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(login.RefreshToken)),
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Machine_api_returns_created_validation_not_found_and_conflict_contracts()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var administrator = await fixture.AsAdminAsync();
        using var factory = new IsolatedApiFactory(fixture);
        using var client = factory.CreateClient();
        await SignInAsync(client, administrator.Email, fixture.Password);

        await AssertProblemAsync(await client.PostAsJsonAsync("/api/v1/machines", new MachineWriteRequest(), ApiJson),
            HttpStatusCode.BadRequest);
        var request = new MachineWriteRequest { MachineCode = "API-001", Name = "API pump" };
        var created = await client.PostAsJsonAsync("/api/v1/machines", request, ApiJson);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);
        await AssertProblemAsync(await client.PostAsJsonAsync("/api/v1/machines", request, ApiJson), HttpStatusCode.Conflict);
        await AssertProblemAsync(await client.GetAsync($"/api/v1/machines/{Guid.NewGuid()}"), HttpStatusCode.NotFound);
        var machine = (await created.Content.ReadFromJsonAsync<MachineDto>(ApiJson))!;

        var deleted = await client.DeleteAsync($"/api/v1/machines/{machine.Id}");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/machines/{machine.Id}")).StatusCode);
    }

    [Fact]
    public async Task Unexpected_failure_returns_generic_problem_details_without_exception_or_secrets()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var administrator = await fixture.AsAdminAsync();
        var confidential = ModuleFixture.NewPassword();
        using var factory = new IsolatedApiFactory(fixture, new FailingAuditWriter(confidential));
        using var client = factory.CreateClient();
        await SignInAsync(client, administrator.Email, fixture.Password);

        var response = await client.PostAsJsonAsync("/api/v1/machines",
            new MachineWriteRequest { MachineCode = "API-FAIL", Name = "Failure pump" }, ApiJson);
        var json = await AssertProblemAsync(response, HttpStatusCode.InternalServerError);

        Assert.DoesNotContain(confidential, json);
        Assert.DoesNotContain("InvalidOperationException", json);
        Assert.DoesNotContain("stack", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connection", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Audit_log_api_is_read_only_and_allows_only_managers_and_administrators()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var administrator = await fixture.AsAdminAsync();
        fixture.Audit.Record("Machine.Updated", "Machine", Guid.NewGuid(),
            oldValues: new { Status = "Standby" }, newValues: new { Status = "Operational" });
        await fixture.Db.SaveChangesAsync();
        var manager = await fixture.SeedUserAsync("MANAGER");
        var supervisor = await fixture.SeedUserAsync("SUPERVISOR");
        using var factory = new IsolatedApiFactory(fixture);
        using var client = factory.CreateClient();

        foreach (var allowed in new[] { manager, administrator })
        {
            await SignInAsync(client, allowed.Email, fixture.Password);
            var response = await client.GetAsync("/api/v1/audit-logs?action=Machine.Updated&page=1&pageSize=20");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var page = (await response.Content.ReadFromJsonAsync<PagedResult<AuditLogDto>>(ApiJson))!;
            Assert.Contains(page.Items, item => item.Action == "Machine.Updated" && item.User is not null);
            var json = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("passwordHash", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("refreshToken", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("accessToken", json, StringComparison.OrdinalIgnoreCase);
        }

        await SignInAsync(client, supervisor.Email, fixture.Password);
        await AssertProblemAsync(await client.GetAsync("/api/v1/audit-logs"), HttpStatusCode.Forbidden);

        await SignInAsync(client, administrator.Email, fixture.Password);
        Assert.Equal(HttpStatusCode.MethodNotAllowed,
            (await client.PostAsJsonAsync("/api/v1/audit-logs", new { })).StatusCode);
    }

    private static async Task SignInAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();
        var login = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
    }

    private static async Task<string> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.Content.ReadAsStringAsync();
        using var problem = JsonDocument.Parse(json);
        Assert.Equal((int)expected, problem.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("title").GetString()));
        return json;
    }

    private sealed class IsolatedApiFactory(ModuleFixture fixture, IAuditWriter? audit = null) : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            // Minimal hosting reads DefaultConnection during Program execution, before the late
            // web-host configuration callback. Supply inert test settings through the early host.
            builder.ConfigureHostConfiguration(configuration => configuration.AddInMemoryCollection(Settings()));
            return base.CreateHost(builder);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Testing disables startup initialization and prevents loading Development user secrets.
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(Settings()));
            builder.ConfigureServices(services =>
            {
                // Windows Event Log is outside the test sandbox; the assertion runner reports failures.
                services.AddLogging(logging => logging.ClearProviders());
                services.RemoveAll<ApplicationDbContext>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(fixture.Db.Database.GetDbConnection()));
                if (audit is not null)
                {
                    services.RemoveAll<IAuditWriter>();
                    services.AddSingleton(audit);
                }
            });
        }

        private Dictionary<string, string?> Settings() => new()
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Port=1;Database=never_opened;Username=tests",
            ["Jwt:Issuer"] = fixture.JwtSettings.Issuer,
            ["Jwt:Audience"] = fixture.JwtSettings.Audience,
            ["Jwt:SigningKey"] = fixture.JwtSettings.SigningKey,
            ["Jwt:AccessTokenMinutes"] = "15", ["Jwt:RefreshTokenDays"] = "7"
        };
    }

    private sealed class FailingAuditWriter(string confidential) : IAuditWriter
    {
        public void Record(string action, string entityType, Guid? entityId,
            object? oldValues = null, object? newValues = null) => throw new InvalidOperationException(confidential);
    }
}
