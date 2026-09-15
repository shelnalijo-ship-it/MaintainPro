using MaintainPro.Application.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MaintainPro.Api.Middleware;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (context.Response.HasStarted) throw;
            var (status, title, detail) = exception switch
            {
                AppException app => (app.StatusCode, Title(app.StatusCode), app.Message),
                BadHttpRequestException { StatusCode: 413 } => (413, "Payload too large", "The request exceeds the upload size limit."),
                BadHttpRequestException => (400, "Invalid request", "The request contains missing or invalid values."),
                DbUpdateConcurrencyException => (409, "Conflict", "The record changed. Reload it and try again."),
                DbUpdateException { InnerException: PostgresException { SqlState: "23505" } } =>
                    (409, "Conflict", "A record with the same unique value already exists."),
                DbUpdateException { InnerException: PostgresException { SqlState: "23503" } } =>
                    (409, "Conflict", "A referenced record changed or is still in use."),
                PostgresException { SqlState: "40001" or "40P01" } =>
                    (409, "Conflict", "A concurrent operation changed the data. Retry the request."),
                DbUpdateException { InnerException: PostgresException { SqlState: "40001" or "40P01" } } =>
                    (409, "Conflict", "A concurrent operation changed the data. Retry the request."),
                _ => (500, "Server error", "The request could not be completed.")
            };
            logger.LogWarning("Request ended with status {Status} ({ExceptionType}); trace {TraceId}",
                status, exception.GetType().Name, context.TraceIdentifier);
            context.Response.Clear();
            if (status == 401) context.Response.Headers.WWWAuthenticate = "Bearer";
            await Results.Problem(statusCode: status, title: title, detail: detail,
                extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier }).ExecuteAsync(context);
        }
    }

    private static string Title(int status) => status switch
    {
        400 => "Validation failed", 401 => "Authentication required", 403 => "Forbidden",
        404 => "Not found", 409 => "Conflict", 413 => "Payload too large", 503 => "Service unavailable", _ => "Request failed"
    };
}
