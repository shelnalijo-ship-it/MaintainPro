using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MaintainPro.Application.Common;
using MaintainPro.Application.Execution;
using MaintainPro.Application.Identity;
using MaintainPro.Application.Reviews;
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

public sealed class ExecutionApiTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task HTTP_execution_upload_reject_correct_resubmit_approve_and_history_work_end_to_end()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture);
        using var factory = new ExecutionApiFactory(fixture);
        using var client = factory.CreateClient();
        var path = $"/api/v1/work-orders/{data.WorkOrder.Id}";
        await SignInAsync(client, data.Technician.Email, fixture.Password);
        (await client.PostAsync(path + "/start", null)).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync(path + "/checklist-results",
            new ChecklistResultsRequest([new ChecklistResultRequest(data.Item.Id, ConfirmationValue: true)]), Json)).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync(path + "/execution-comments", new ExecutionCommentsRequest("Original comment"), Json)).EnsureSuccessStatusCode();
        var partResponse = await client.PostAsJsonAsync(path + "/parts", new PartUsageRequest("Bearing", 1m), Json);
        partResponse.EnsureSuccessStatusCode();
        var part = (await partResponse.Content.ReadFromJsonAsync<PartUsageDto>(Json))!;
        var defectResponse = await client.PostAsJsonAsync(path + "/defects", new DefectRequest("Leak", "Seal inspection", "LOW", true), Json);
        defectResponse.EnsureSuccessStatusCode();
        using var multipart = PngForm(data.Item.Id);
        var evidenceResponse = await client.PostAsync(path + "/attachments", multipart);
        evidenceResponse.EnsureSuccessStatusCode();
        var attachment = (await evidenceResponse.Content.ReadFromJsonAsync<AttachmentDto>(Json))!;
        var downloaded = await client.GetAsync($"/api/v1/files/{attachment.FileId}");
        downloaded.EnsureSuccessStatusCode();
        Assert.Equal(ExecutionTestData.Png(), await downloaded.Content.ReadAsByteArrayAsync());
        Assert.Equal("image/png", downloaded.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", downloaded.Content.Headers.ContentDisposition?.DispositionType);
        Assert.True(downloaded.Headers.CacheControl?.NoStore);
        Assert.Equal("nosniff", Assert.Single(downloaded.Headers.GetValues("X-Content-Type-Options")));
        (await client.PostAsync(path + "/complete", null)).EnsureSuccessStatusCode();
        var firstResponse = await client.PostAsync(path + "/submit", null);
        firstResponse.EnsureSuccessStatusCode();
        var first = (await firstResponse.Content.ReadFromJsonAsync<WorkOrderSubmissionDto>(Json))!;
        Assert.Equal(1, first.Submission.VersionNumber);
        Assert.Single(first.Attachments);
        await AssertProblemAsync(await client.PutAsJsonAsync(path + "/execution-comments", new ExecutionCommentsRequest("Too late")), HttpStatusCode.Conflict);

        await SignInAsync(client, data.Supervisor.Email, fixture.Password);
        var pending = (await client.GetFromJsonAsync<PagedResult<PendingApprovalDto>>("/api/v1/work-orders/pending-approvals", Json))!;
        Assert.Equal(1, pending.Page);
        Assert.Equal(20, pending.PageSize);
        Assert.Equal(first.Submission.Id, Assert.Single(pending.Items).SubmissionId);
        await AssertProblemAsync(await client.PostAsJsonAsync(path + "/reject", new WorkOrderReviewRequest(first.Submission.Id), Json), HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync(path + "/reject", new WorkOrderReviewRequest(first.Submission.Id, "Clarify the observations"), Json)).EnsureSuccessStatusCode();
        await SignInAsync(client, data.Technician.Email, fixture.Password);
        (await client.PostAsync(path + "/resume", null)).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync(path + "/execution-comments", new ExecutionCommentsRequest("Corrected comment"), Json)).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync(path + $"/parts/{part.Id}", new PartUsageRequest("Bearing", 2m), Json)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(path + $"/attachments/{attachment.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/files/{attachment.FileId}")).StatusCode);
        (await client.PostAsync(path + "/complete", null)).EnsureSuccessStatusCode();
        var secondResponse = await client.PostAsync(path + "/submit", null);
        secondResponse.EnsureSuccessStatusCode();
        var second = (await secondResponse.Content.ReadFromJsonAsync<WorkOrderSubmissionDto>(Json))!;
        Assert.Equal(2, second.Submission.VersionNumber);
        Assert.Empty(second.Attachments);
        await SignInAsync(client, data.Supervisor.Email, fixture.Password);
        (await client.PostAsJsonAsync(path + "/approve", new WorkOrderReviewRequest(second.Submission.Id, "Accepted"), Json)).EnsureSuccessStatusCode();
        var submissions = (await client.GetFromJsonAsync<SubmissionSummaryDto[]>(path + "/submissions", Json))!;
        Assert.Equal(2, submissions.Length);
        var original = (await client.GetFromJsonAsync<WorkOrderSubmissionDto>(path + $"/submissions/{first.Submission.Id}", Json))!;
        Assert.Equal("Original comment", original.OverallComments);
        Assert.Equal(1m, Assert.Single(original.PartUsages).Quantity);
        Assert.Single(original.Attachments);
        var history = (await client.GetFromJsonAsync<WorkOrderHistoryDto[]>(path + "/history", Json))!;
        Assert.Equal("WorkOrder.Approved", history[^1].Action);
        var machine = (await client.GetFromJsonAsync<PagedResult<MachineMaintenanceHistoryDto>>(
            $"/api/v1/machines/{data.Machine.Id}/maintenance-history", Json))!;
        Assert.Equal(second.Submission.Id, Assert.Single(machine.Items).ApprovedSubmissionId);
        Assert.Equal(2, Assert.Single(machine.Items).ApprovedSubmissionVersion);
    }

    [Theory]
    [InlineData("PUT", "/checklist-results", "{}")]
    [InlineData("PUT", "/checklist-results", "{\"results\":null}")]
    [InlineData("PUT", "/checklist-results", "{\"results\":[null]}")]
    [InlineData("POST", "/parts", "{}")]
    [InlineData("POST", "/parts", "{\"partName\":null,\"quantity\":1}")]
    [InlineData("POST", "/defects", "{}")]
    public async Task Invalid_execution_JSON_returns_clear_validation_errors(string method, string suffix, string body)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture);
        using var factory = new ExecutionApiFactory(fixture);
        using var client = factory.CreateClient();
        await SignInAsync(client, data.Technician.Email, fixture.Password);
        (await client.PostAsync($"/api/v1/work-orders/{data.WorkOrder.Id}/start", null)).EnsureSuccessStatusCode();
        using var request = new HttpRequestMessage(new HttpMethod(method), $"/api/v1/work-orders/{data.WorkOrder.Id}{suffix}")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        await AssertProblemAsync(await client.SendAsync(request), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task File_and_execution_routes_reject_anonymous_and_unrelated_access_and_admin_execution()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture);
        await fixture.Execution.StartAsync(data.WorkOrder.Id);
        var attachment = await ExecutionEvidenceTests.UploadPngAsync(fixture, data.WorkOrder.Id);
        using var factory = new ExecutionApiFactory(fixture);
        using var client = factory.CreateClient();
        await AssertProblemAsync(await client.GetAsync($"/api/v1/files/{attachment.FileId}"), HttpStatusCode.Unauthorized);
        var unrelated = await fixture.SeedUserAsync("TECHNICIAN");
        await SignInAsync(client, unrelated.Email, fixture.Password);
        await AssertProblemAsync(await client.GetAsync($"/api/v1/files/{attachment.FileId}"), HttpStatusCode.NotFound);
        await AssertProblemAsync(await client.GetAsync($"/api/v1/work-orders/{data.WorkOrder.Id}/execution"), HttpStatusCode.NotFound);
        await AssertProblemAsync(await client.PostAsync($"/api/v1/work-orders/{data.WorkOrder.Id}/start", null), HttpStatusCode.NotFound);
        await SignInAsync(client, data.Administrator.Email, fixture.Password);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/files/{attachment.FileId}")).StatusCode);
        await AssertProblemAsync(await client.PostAsync($"/api/v1/work-orders/{data.WorkOrder.Id}/complete", null), HttpStatusCode.Forbidden);
        await AssertProblemAsync(await client.PostAsJsonAsync($"/api/v1/work-orders/{data.WorkOrder.Id}/approve",
            new WorkOrderReviewRequest(Guid.NewGuid()), Json), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Multipart_upload_rejects_invalid_format_and_missing_evidence_category()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture);
        using var factory = new ExecutionApiFactory(fixture);
        using var client = factory.CreateClient();
        await SignInAsync(client, data.Technician.Email, fixture.Password);
        var path = $"/api/v1/work-orders/{data.WorkOrder.Id}";
        (await client.PostAsync(path + "/start", null)).EnsureSuccessStatusCode();
        using var missingCategory = PngForm(null, includeEvidenceType: false);
        await AssertProblemAsync(await client.PostAsync(path + "/attachments", missingCategory), HttpStatusCode.BadRequest);
        using var fake = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(Encoding.UTF8.GetBytes("not an image"));
        bytes.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        fake.Add(bytes, "file", "fake.png");
        fake.Add(new StringContent("OTHER"), "evidenceType");
        await AssertProblemAsync(await client.PostAsync(path + "/attachments", fake), HttpStatusCode.BadRequest);
    }

    private static MultipartFormDataContent PngForm(Guid? itemId, bool includeEvidenceType = true)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(ExecutionTestData.Png());
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file, "file", "condition.png");
        if (includeEvidenceType) content.Add(new StringContent("AFTER_MAINTENANCE"), "evidenceType");
        if (itemId.HasValue) content.Add(new StringContent(itemId.ToString()!), "workOrderChecklistItemId");
        return content;
    }

    private static async Task SignInAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();
        var login = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)status, problem.RootElement.GetProperty("status").GetInt32());
    }

    private sealed class ExecutionApiFactory(ModuleFixture fixture) : WebApplicationFactory<Program>
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
