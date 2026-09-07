using System.Net;
using System.Text.Json;
using BuildingBlocks.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Middleware;

/// <summary>
/// Global exception handler middleware that catches all unhandled exceptions
/// and returns RFC 7807 ProblemDetails responses.
/// </summary>
public class GlobalExceptionHandlerMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title, detail, errors) = exception switch
        {
            ValidationException validationEx => (
                HttpStatusCode.BadRequest,
                "Validation Error",
                "One or more validation errors occurred.",
                (IReadOnlyDictionary<string, string[]?>?)validationEx.Errors),

            NotFoundException notFoundEx => (
                HttpStatusCode.NotFound,
                "Not Found",
                notFoundEx.Message,
                null),

            ConflictException conflictEx => (
                HttpStatusCode.Conflict,
                "Conflict",
                conflictEx.Message,
                null),

            UnauthorizedAccessException => (
                HttpStatusCode.Unauthorized,
                "Unauthorized",
                "Unauthorized access.",
                null),

            _ => (
                HttpStatusCode.InternalServerError,
                "Internal Server Error",
                "An unexpected error occurred.",
                null)
        };

        _logger.LogError(
            exception,
            "Unhandled exception: {ExceptionType} - {Message}",
            exception.GetType().Name,
            exception.Message);

        var problem = new Dictionary<string, object?>
        {
            ["type"] = "https://datatracker.ietf.org/doc/html/rfc7807",
            ["title"] = title,
            ["status"] = (int)statusCode,
            ["detail"] = detail
        };

        if (errors is not null)
        {
            problem["errors"] = errors;
        }

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }
}
