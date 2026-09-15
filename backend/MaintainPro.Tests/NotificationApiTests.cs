using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MaintainPro.Application.Common;
using MaintainPro.Application.Identity;
using MaintainPro.Application.Notifications;
using MaintainPro.Application.WorkOrders;
using MaintainPro.Domain.Enums;
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

namespace MaintainPro.Tests;

public sealed class NotificationApiTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Inbox_HTTP_defaults_filters_read_and_read_all_keep_personal_ownership()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await NotificationTestData.CreateAsync(fixture);
        using var factory = new NotificationApiFactory(fixture);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/notifications")).StatusCode);
        await SignInAsync(client, data.Technician.Email, fixture.Password);
        var page = (await client.GetFromJsonAsync<PagedResult<NotificationDto>>("/api/v1/notifications", Json))!;
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
        var notice = Assert.Single(page.Items);
        Assert.Equal(NotificationType.WORK_ORDER_ASSIGNED, notice.NotificationType);
        Assert.Equal(data.Technician.Id, notice.UserId);
        Assert.Equal(1, (await client.GetFromJsonAsync<NotificationUnreadCountDto>("/api/v1/notifications/unread-count", Json))!.UnreadCount);
        var readResponse = await client.PatchAsync($"/api/v1/notifications/{notice.Id}/read", null);
        readResponse.EnsureSuccessStatusCode();
        var read = (await readResponse.Content.ReadFromJsonAsync<NotificationDto>(Json))!;
        Assert.True(read.IsRead);
        Assert.NotNull(read.ReadAt);
        Assert.Empty((await client.GetFromJsonAsync<PagedResult<NotificationDto>>("/api/v1/notifications?isRead=false", Json))!.Items);
        var allResponse = await client.PostAsync("/api/v1/notifications/read-all", null);
        allResponse.EnsureSuccessStatusCode();
        Assert.Equal(0, (await allResponse.Content.ReadFromJsonAsync<NotificationReadAllDto>(Json))!.UpdatedCount);
        await SignInAsync(client, data.Administrator.Email, fixture.Password);
        Assert.Empty((await client.GetFromJsonAsync<PagedResult<NotificationDto>>("/api/v1/notifications", Json))!.Items);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PatchAsync($"/api/v1/notifications/{notice.Id}/read", null)).StatusCode);
    }

    [Fact]
    public async Task Processing_and_escalation_HTTP_routes_publish_derived_lateness_without_changing_lifecycle()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await NotificationTestData.CreateAsync(fixture, -5);
        await fixture.SeedUserAsync("MANAGER");
        using var factory = new NotificationApiFactory(fixture);
        using var client = factory.CreateClient();
        await SignInAsync(client, data.Administrator.Email, fixture.Password);
        var process = await client.PostAsync("/api/v1/notifications/process-due", null);
        process.EnsureSuccessStatusCode();
        Assert.Equal(0, (await process.Content.ReadFromJsonAsync<ReminderProcessingSummary>(Json))!.Errors);
        var job = (await client.GetFromJsonAsync<WorkOrderDto>($"/api/v1/work-orders/{data.WorkOrder.Id}", Json))!.WorkOrder;
        Assert.True(job.IsOverdue);
        Assert.Equal(5, job.DaysOverdue);
        Assert.Equal(3, job.EscalationLevel);
        Assert.NotNull(job.LastEscalatedAt);
        Assert.Equal(WorkOrderLifecycleStatus.ASSIGNED, job.LifecycleStatus);
        var escalationPage = (await client.GetFromJsonAsync<PagedResult<EscalationDto>>("/api/v1/escalations", Json))!;
        Assert.Equal(1, escalationPage.Page);
        Assert.Equal(20, escalationPage.PageSize);
        Assert.Equal(4, escalationPage.TotalCount);
        var escalation = escalationPage.Items[0];
        Assert.Equal(escalation.Id, (await client.GetFromJsonAsync<EscalationDto>($"/api/v1/escalations/{escalation.Id}", Json))!.Id);
        await SignInAsync(client, data.Supervisor.Email, fixture.Password);
        Assert.Equal(4, (await client.GetFromJsonAsync<PagedResult<EscalationDto>>("/api/v1/escalations", Json))!.TotalCount);
        await SignInAsync(client, data.Technician.Email, fixture.Password);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/escalations")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/v1/notifications/process-due", null)).StatusCode);
    }

    [Fact]
    public async Task Settings_HTTP_enforces_manager_policy_and_validates_required_payload()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var administrator = await fixture.AsAdminAsync();
        var technician = await fixture.SeedUserAsync("TECHNICIAN");
        using var factory = new NotificationApiFactory(fixture);
        using var client = factory.CreateClient();
        await SignInAsync(client, administrator.Email, fixture.Password);
        var defaults = (await client.GetFromJsonAsync<EscalationSettingsDto>("/api/v1/settings/escalation", Json))!;
        Assert.Equal((1, 1, 3, 5), (defaults.DueSoonDays, defaults.TechnicianOverdueDays, defaults.SupervisorEscalationDays, defaults.ManagerEscalationDays));
        var updated = await client.PutAsJsonAsync("/api/v1/settings/escalation", new EscalationSettingsRequest(2, 2, 4, 6), Json);
        updated.EnsureSuccessStatusCode();
        Assert.Equal(6, (await updated.Content.ReadFromJsonAsync<EscalationSettingsDto>(Json))!.ManagerEscalationDays);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/v1/settings/escalation", new EscalationSettingsRequest(1, 4, 3, 5), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsync("/api/v1/settings/escalation", new StringContent("{}", Encoding.UTF8, "application/json"))).StatusCode);
        await SignInAsync(client, technician.Email, fixture.Password);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/settings/escalation")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync("/api/v1/settings/escalation", new EscalationSettingsRequest(1, 1, 3, 5), Json)).StatusCode);
    }

    [Fact]
    public async Task HTTP_reassignment_changes_visible_current_technician_and_notifies_new_assignee()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await NotificationTestData.CreateAsync(fixture);
        var replacement = await fixture.SeedUserAsync("TECHNICIAN");
        using var factory = new NotificationApiFactory(fixture);
        using var client = factory.CreateClient();
        await SignInAsync(client, data.Administrator.Email, fixture.Password);
        var response = await client.PostAsJsonAsync($"/api/v1/work-orders/{data.WorkOrder.Id}/assignment", new WorkOrderAssignmentRequest(replacement.Id, "Shift cover"), Json);
        response.EnsureSuccessStatusCode();
        Assert.Equal(replacement.Id, (await response.Content.ReadFromJsonAsync<WorkOrderSummaryDto>(Json))!.AssignedTechnicianId);
        await SignInAsync(client, data.Technician.Email, fixture.Password);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/work-orders/{data.WorkOrder.Id}")).StatusCode);
        await SignInAsync(client, replacement.Email, fixture.Password);
        var notice = Assert.Single((await client.GetFromJsonAsync<PagedResult<NotificationDto>>("/api/v1/notifications", Json))!.Items);
        Assert.Equal(NotificationType.WORK_ORDER_REASSIGNED, notice.NotificationType);
        Assert.Equal(data.WorkOrder.Id, notice.EntityId);
    }

    [Theory]
    [InlineData("/api/v1/notifications?page=0")]
    [InlineData("/api/v1/notifications?pageSize=101")]
    [InlineData("/api/v1/notifications?type=UNDEFINED")]
    [InlineData("/api/v1/escalations?level=4")]
    public async Task Invalid_notification_queries_return_400(string path)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var administrator = await fixture.AsAdminAsync();
        using var factory = new NotificationApiFactory(fixture);
        using var client = factory.CreateClient();
        await SignInAsync(client, administrator.Email, fixture.Password);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path)).StatusCode);
    }

    private static async Task SignInAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();
        var login = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
    }

    private sealed class NotificationApiFactory(ModuleFixture fixture) : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(configuration => configuration.AddInMemoryCollection(Settings()));
            return base.CreateHost(builder);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(Settings()));
            builder.ConfigureServices(services =>
            {
                services.AddLogging(logging => logging.ClearProviders());
                services.RemoveAll<ApplicationDbContext>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(fixture.Db.Database.GetDbConnection()));
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(fixture.Clock);
            });
        }

        private Dictionary<string, string?> Settings() => new()
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Port=1;Database=never_opened;Username=tests",
            ["Jwt:Issuer"] = fixture.JwtSettings.Issuer,
            ["Jwt:Audience"] = fixture.JwtSettings.Audience,
            ["Jwt:SigningKey"] = fixture.JwtSettings.SigningKey,
            ["Jwt:AccessTokenMinutes"] = "15",
            ["Jwt:RefreshTokenDays"] = "7",
            ["FileStorage:RootDirectory"] = fixture.FileStorageDirectory
        };
    }
}
