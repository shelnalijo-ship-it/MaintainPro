using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using ClosedXML.Excel;
using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Identity;
using MaintainPro.Application.Reporting;
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

public sealed class ReportExportTests
{
    [Fact]
    public async Task PDF_and_Excel_exports_are_valid_nonempty_and_audited()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ReportingTestData.CreateAsync(fixture);
        var query = new ExternalServiceReportQuery(new(2026, 9, 1), new(2026, 9, 30), data.Machine.Id);

        var pdf = await fixture.ReportExports.ExternalServicesAsync(query, ReportExportFormat.Pdf);
        var excel = await fixture.ReportExports.ExternalServicesAsync(query, ReportExportFormat.Excel);

        Assert.Equal("application/pdf", pdf.ContentType);
        Assert.True(pdf.Content.Length > 500);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(pdf.Content, 0, 5));
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excel.ContentType);
        Assert.True(excel.Content.Length > 1000);
        Assert.Equal((byte)'P', excel.Content[0]);
        Assert.Equal((byte)'K', excel.Content[1]);
        using var workbook = new XLWorkbook(new MemoryStream(excel.Content));
        var cost = workbook.Worksheet("Report").Cell(7, 9);
        Assert.Equal(XLDataType.Number, cost.DataType);
        Assert.Equal(500m, cost.GetValue<decimal>());
        var audits = fixture.Db.AuditLogs.Where(x => x.Action == "Report.Exported").ToArray();
        Assert.Equal(2, audits.Length);
        Assert.All(audits, x => Assert.DoesNotContain("StorageKey", x.NewValuesJson));
        var history = await fixture.ReportExportHistory.ListAsync(new());
        Assert.Equal(2, history.TotalCount);
        Assert.All(history.Items, x => Assert.Equal("external-services", x.ReportName));
    }

    [Fact]
    public async Task Supervisor_export_uses_the_same_scope_and_financial_redaction_as_screen_report()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ReportingTestData.CreateAsync(fixture);
        fixture.ActAs(data.Supervisor);

        var excel = await fixture.ReportExports.ExternalServicesAsync(
            new(new(2026, 9, 1), new(2026, 9, 30)), ReportExportFormat.Excel);

        using var workbook = new XLWorkbook(new MemoryStream(excel.Content));
        var sheet = workbook.Worksheet("Report");
        Assert.Equal(data.ExternalService.ServiceNumber, sheet.Cell(7, 1).GetString());
        Assert.True(sheet.Cell(7, 9).IsEmpty());
        Assert.True(sheet.Cell(8, 1).IsEmpty());
        Assert.Single((await fixture.ReportExportHistory.ListAsync(new())).Items);
    }

    [Fact]
    public async Task Reporting_HTTP_endpoints_enforce_roles_and_return_correct_export_content_types()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ReportingTestData.CreateAsync(fixture);
        // JWT validation uses the host clock; keep the API test token inside its real validation window.
        fixture.Clock.SetUtc(DateTimeOffset.UtcNow);
        using var factory = new ReportingApiFactory(fixture);
        using var client = factory.CreateClient();
        await SignInAsync(client, data.Manager.Email, fixture.Password);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(
            "/api/v1/dashboard/manager?from=2026-09-01&to=2026-09-30")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(
            "/api/v1/reports/preventive-maintenance?from=2026-09-01&to=2026-09-30")).StatusCode);
        var pdf = await client.GetAsync(
            "/api/v1/reports/preventive-maintenance/export/pdf?from=2026-09-01&to=2026-09-30");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(pdf.Content.Headers.ContentDisposition?.FileNameStar);
        var excel = await client.GetAsync(
            "/api/v1/reports/monthly-summary/export/excel?year=2026&month=9");
        Assert.Equal(HttpStatusCode.OK, excel.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            excel.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync("/api/v1/reports/export-history")).StatusCode);

        await SignInAsync(client, data.Technician.Email, fixture.Password);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/dashboard/technician")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/v1/reports/preventive-maintenance")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/v1/dashboard/manager")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync($"/api/v1/reports/machine-history/{data.Machine.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/v1/reports/machine-history/{data.OtherMachine.Id}")).StatusCode);
    }

    private static async Task SignInAsync(HttpClient client, string email, string password)
    {
        client.DefaultRequestHeaders.Authorization = null;
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();
        var login = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
    }

    private sealed class ReportingApiFactory(ModuleFixture fixture) : WebApplicationFactory<Program>
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
