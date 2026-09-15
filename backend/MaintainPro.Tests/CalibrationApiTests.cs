using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Calibrations;
using MaintainPro.Application.Common;
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

public sealed class CalibrationApiTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Certificate_read_file_summary_and_manual_processing_routes_are_wired_and_authorized()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await CalibrationTestData.CreateAsync(fixture);
        using var factory = new CalibrationApiFactory(fixture);
        using var client = factory.CreateClient();
        await SignInAsync(client, data.Manager.Email, fixture.Password);

        using var form = CertificateForm(data.Machine.Id, data.Today);
        var response = await client.PostAsync("/api/v1/calibrations", form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<CalibrationCertificateDto>(Json))!;
        Assert.NotNull(created.CertificateFileId);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/calibrations/{created.Id}")).StatusCode);
        var history = (await client.GetFromJsonAsync<PagedResult<CalibrationCertificateDto>>(
            $"/api/v1/machines/{data.Machine.Id}/calibrations", Json))!;
        Assert.Equal(created.Id, Assert.Single(history.Items).Id);
        var status = (await client.GetFromJsonAsync<MachineCalibrationStatusDto>(
            $"/api/v1/machines/{data.Machine.Id}/calibration-status", Json))!;
        Assert.Equal(created.Id, status.CurrentCertificateId);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/calibrations/summary")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/v1/calibrations/process-reminders", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/files/{created.CertificateFileId}")).StatusCode);

        await SignInAsync(client, data.Technician.Email, fixture.Password);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/machines/{data.Machine.Id}/calibrations")).StatusCode);
        using var forbiddenForm = CertificateForm(data.Machine.Id, data.Today, "CERT-API-2");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsync("/api/v1/calibrations", forbiddenForm)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsync("/api/v1/calibrations/process-reminders", null)).StatusCode);
    }

    private static MultipartFormDataContent CertificateForm(Guid machineId, DateOnly today,
        string number = "CERT-API-1")
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(machineId.ToString()), "machineId");
        content.Add(new StringContent(number), "certificateNumber");
        content.Add(new StringContent("Accredited Lab"), "calibrationProvider");
        content.Add(new StringContent(today.AddDays(-5).ToString("yyyy-MM-dd")), "calibrationDate");
        content.Add(new StringContent(today.AddDays(60).ToString("yyyy-MM-dd")), "expiryDate");
        content.Add(new StringContent("PASS"), "result");
        content.Add(new StringContent("false"), "recordForNonRequiredMachine");
        var file = new ByteArrayContent(ExecutionTestData.Pdf());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(file, "certificate", "certificate.pdf");
        return content;
    }

    private static async Task SignInAsync(HttpClient client, string email, string password)
    {
        client.DefaultRequestHeaders.Authorization = null;
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();
        var login = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
    }

    private sealed class CalibrationApiFactory(ModuleFixture fixture) : WebApplicationFactory<Program>
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
