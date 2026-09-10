using System.Net;
using System.Text.Json;
using BuildingBlocks.Domain.Exceptions;
using BuildingBlocks.Domain.Localization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Middleware;

/// <summary>
/// Global exception handler middleware that catches all unhandled exceptions
/// and returns RFC 7807 ProblemDetails responses. Titles, details, entity
/// names, and validation messages are localized at this boundary using the
/// request's <c>X-Language</c> (or <c>Accept-Language</c>) header and the
/// <see cref="ErrorCatalog"/>; coded exceptions carry their own arguments.
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

    /// <summary>
    /// Resolves the response language: the explicit <c>X-Language</c> header
    /// (sent by the frontend with the app's selected language) wins over the
    /// browser's automatic <c>Accept-Language</c>; default is English.
    /// </summary>
    private static string ResolveLanguage(HttpContext context)
    {
        var explicitHeader = context.Request.Headers["X-Language"].ToString();
        if (!string.IsNullOrWhiteSpace(explicitHeader))
        {
            return explicitHeader.Split(',')[0].Trim();
        }

        var header = context.Request.Headers.AcceptLanguage.ToString();
        if (string.IsNullOrWhiteSpace(header))
        {
            return ErrorCatalog.DefaultLanguage;
        }

        // Take the highest-q (first listed) language tag, e.g. "fa-IR,fa;q=0.9".
        var first = header.Split(',')[0].Trim();
        var tag = first.Split(';')[0].Trim();
        return string.IsNullOrEmpty(tag) ? ErrorCatalog.DefaultLanguage : tag;
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var lang = ResolveLanguage(context);

        var (statusCode, titleCode, detail, code, errors) = exception switch
        {
            ValidationException validationEx => (
                HttpStatusCode.BadRequest,
                ErrorCodes.Titles.Validation,
                ErrorCatalog.Localize(ErrorCodes.Common.ValidationFailed, Array.Empty<object?>(), lang),
                ErrorCodes.Common.ValidationFailed,
                (IReadOnlyDictionary<string, string[]>?)LocalizeValidationErrors(validationEx.Errors, lang)),

            ConflictException conflictEx => (
                HttpStatusCode.Conflict,
                ErrorCodes.Titles.Conflict,
                ErrorCatalog.Localize(conflictEx.Code, conflictEx.Args, lang),
                conflictEx.Code,
                null),

            NotFoundException notFoundEx => (
                HttpStatusCode.NotFound,
                ErrorCodes.Titles.NotFound,
                LocalizeNotFound(notFoundEx, lang),
                notFoundEx.Code,
                null),

            UnauthorizedException unauthorizedEx => (
                HttpStatusCode.Unauthorized,
                ErrorCodes.Titles.Unauthorized,
                ErrorCatalog.Localize(unauthorizedEx.Code, unauthorizedEx.Args, lang),
                unauthorizedEx.Code,
                null),

            UnauthorizedAccessException => (
                HttpStatusCode.Unauthorized,
                ErrorCodes.Titles.Unauthorized,
                ErrorCatalog.Localize(ErrorCodes.Common.UnauthorizedAccess, Array.Empty<object?>(), lang),
                ErrorCodes.Common.UnauthorizedAccess,
                null),

            _ => (
                HttpStatusCode.InternalServerError,
                ErrorCodes.Titles.InternalError,
                ErrorCatalog.Localize(ErrorCodes.Common.UnexpectedError, Array.Empty<object?>(), lang),
                ErrorCodes.Common.UnexpectedError,
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
            ["title"] = ErrorCatalog.Localize(titleCode, Array.Empty<object?>(), lang),
            ["status"] = (int)statusCode,
            ["detail"] = detail
        };

        if (code is not null)
        {
            problem["code"] = code;
        }

        if (errors is not null)
        {
            problem["errors"] = errors;
        }

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }

    /// <summary>
    /// Validation failure messages are error codes (validators emit codes, not
    /// English text); each is localized, with unknown/raw text passing through.
    /// </summary>
    private static IReadOnlyDictionary<string, string[]> LocalizeValidationErrors(
        IReadOnlyDictionary<string, string[]> errors,
        string lang)
    {
        return errors.ToDictionary(
            entry => entry.Key,
            entry => entry.Value
                .Select(message => ErrorCatalog.Localize(message, Array.Empty<object?>(), lang))
                .ToArray());
    }

    /// <summary>
    /// For entity/key not-found errors, localizes the entity name itself
    /// ("Room" → "اتاق") before filling the "common.notFound" template.
    /// </summary>
    private static string LocalizeNotFound(NotFoundException exception, string lang)
    {
        var args = exception.Args;

        if (exception.EntityName is not null)
        {
            var entityCode = $"entity.{exception.EntityName}";
            var entity = ErrorCatalog.Contains(entityCode)
                ? ErrorCatalog.Localize(entityCode, Array.Empty<object?>(), lang)
                : exception.EntityName;

            args = [entity, exception.Key!];
        }

        return ErrorCatalog.Localize(exception.Code, args, lang);
    }
}
