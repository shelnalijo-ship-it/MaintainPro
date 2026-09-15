using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Application.ExternalServices;
using MaintainPro.Application.Identity;
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

public sealed class ExternalServiceApiTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Service_attachment_document_history_follow_up_and_authorization_routes_work()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExternalServiceTestData.CreateAsync(fixture);
        using var factory = new ExternalServiceApiFactory(fixture);
        using var client = factory.CreateClient();
        await SignInAsync(client, data.Manager.Email, fixture.Password);

        var request = ExternalServiceTestData.Request(data, followUp: data.Today.AddDays(5));
        var createResponse = await client.PostAsJsonAsync("/api/v1/external-services", request, Json);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var service = (await createResponse.Content.ReadFromJsonAsync<ExternalServiceDto>(Json))!;
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/external-services/{service.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/external-services/follow-ups")).StatusCode);
        var machineHistory = (await client.GetFromJsonAsync<PagedResult<ExternalServiceDto>>(
            $"/api/v1/machines/{data.Machine.Id}/external-services", Json))!;
        Assert.Equal(service.Id, Assert.Single(machineHistory.Items).Id);

        using var attachmentForm = FileForm("service-report.pdf", "application/pdf", ExecutionTestData.Pdf());
        attachmentForm.Add(new StringContent("SERVICE_REPORT"), "documentType");
        var attachmentResponse = await client.PostAsync($"/api/v1/external-services/{service.Id}/attachments", attachmentForm);
        attachmentResponse.EnsureSuccessStatusCode();
        var attachment = (await attachmentResponse.Content.ReadFromJsonAsync<ExternalServiceAttachmentDto>(Json))!;
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/files/{attachment.FileId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync($"/api/v1/external-services/{service.Id}/attachments")).StatusCode);

        using var documentForm = FileForm("manual.pdf", "application/pdf", ExecutionTestData.Pdf());
        documentForm.Add(new StringContent("MANUAL"), "documentType");
        documentForm.Add(new StringContent("Machine manual"), "title");
        documentForm.Add(new StringContent(data.Today.AddDays(-10).ToString("yyyy-MM-dd")), "documentDate");
        documentForm.Add(new StringContent(data.Today.AddDays(60).ToString("yyyy-MM-dd")), "expiryDate");
        var documentResponse = await client.PostAsync($"/api/v1/machines/{data.Machine.Id}/documents", documentForm);
        documentResponse.EnsureSuccessStatusCode();
        var document = (await documentResponse.Content.ReadFromJsonAsync<MachineDocumentDto>(Json))!;
        Assert.Equal(MachineDocumentExpiryStatus.EXPIRING_SOON, document.ExpiryStatus);
        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync($"/api/v1/machines/{data.Machine.Id}/documents/{document.Id}")).StatusCode);

        await SignInAsync(client, data.Technician.Email, fixture.Password);
        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync($"/api/v1/machines/{data.Machine.Id}/documents")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PutAsJsonAsync($"/api/v1/external-services/{service.Id}", request, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/v1/external-services/follow-ups")).StatusCode);
        using var forbiddenDocument = FileForm("other.pdf", "application/pdf", ExecutionTestData.Pdf());
        forbiddenDocument.Add(new StringContent("MANUAL"), "documentType");
        forbiddenDocument.Add(new StringContent("Other"), "title");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsync($"/api/v1/machines/{data.Machine.Id}/documents", forbiddenDocument)).StatusCode);
    }

    private static MultipartFormDataContent FileForm(string filename, string mime, byte[] bytes)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(mime);
        form.Add(file, "file", filename);
        return form;
    }

    private static async Task SignInAsync(HttpClient client, string email, string password)
    {
        client.DefaultRequestHeaders.Authorization = null;
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();
        var login = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
    }

    private sealed class ExternalServiceApiFactory(ModuleFixture fixture) : WebApplicationFactory<Program>
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
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseSqlite(fixture.Db.Database.GetDbConnection()));
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
