namespace TIAdmin.Api.Middleware;

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Exceptions;

/// <summary>
/// Manejo global de errores (SPECS.md seccion 43).
/// Nunca expone stack traces al usuario final: en su lugar devuelve un traceId
/// que permite localizar el error en los logs.
/// </summary>
public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // El cliente aborto la peticion: no es un error de la aplicacion.
            logger.LogInformation("Peticion cancelada por el cliente. {Method} {Path}",
                context.Request.Method, context.Request.Path);
        }
        catch (Exception exception)
        {
            await HandleAsync(context, exception);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var traceId = context.TraceIdentifier;
        var correlationId = context.GetCorrelationId();

        var (statusCode, errorCode, message) = exception switch
        {
            EntityNotFoundException notFound => (HttpStatusCode.NotFound, notFound.Code, notFound.Message),
            DomainValidationException validation => (HttpStatusCode.BadRequest, validation.Code, validation.Message),
            ConflictException conflict => (HttpStatusCode.Conflict, conflict.Code, conflict.Message),
            InvalidOperationException domainInvalid => (HttpStatusCode.BadRequest, domainInvalid.Code, domainInvalid.Message),
            DbUpdateConcurrencyException => (HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT",
                "El registro fue modificado por otro usuario. Recargue e intente nuevamente."),
            UnauthorizedAccessException => (HttpStatusCode.Forbidden, "FORBIDDEN",
                "No tiene permisos para realizar esta operacion."),
            OperationCanceledException => (HttpStatusCode.RequestTimeout, "REQUEST_TIMEOUT",
                "La solicitud excedio el tiempo de espera."),
            _ => (HttpStatusCode.InternalServerError, "INTERNAL_ERROR",
                "Ha ocurrido un error inesperado.")
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            logger.LogError(exception,
                "Error no controlado. TraceId={TraceId} CorrelationId={CorrelationId} {Method} {Path}",
                traceId, correlationId, context.Request.Method, context.Request.Path);
        }
        else
        {
            logger.LogWarning(exception,
                "Error de negocio. Code={Code} TraceId={TraceId} CorrelationId={CorrelationId} {Method} {Path}",
                errorCode, traceId, correlationId, context.Request.Method, context.Request.Path);
        }

        if (context.Response.HasStarted)
        {
            logger.LogWarning("No se puede escribir la respuesta de error: la respuesta ya comenzo.");
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        // traceId solo se expone en errores 5xx: permite encontrar el error en los logs
        // sin filtrar detalle interno en los 4xx de negocio.
        var payload = statusCode == HttpStatusCode.InternalServerError
            ? ErrorResponse.WithTraceId(message, errorCode, traceId)
            : ErrorResponse.Create(message, errorCode);

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(payload, JsonOptions), context.RequestAborted);
    }
}

/// <summary>
/// Respuesta de error normalizada (SPECS.md secciones 13 y 43).
/// </summary>
public sealed record ErrorResponse(
    bool Success,
    object? Data,
    string Message,
    IReadOnlyCollection<ApiError> Errors,
    string? TraceId)
{
    public static ErrorResponse Create(string message, string code) =>
        new(false, null, message, [new ApiError(code, message)], null);

    public static ErrorResponse WithTraceId(string message, string code, string traceId) =>
        new(false, null, message, [new ApiError(code, message)], traceId);
}
