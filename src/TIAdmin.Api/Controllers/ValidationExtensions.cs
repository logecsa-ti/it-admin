namespace TIAdmin.Api.Controllers;

using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using TIAdmin.Application.Common.Models;

internal static class ValidationExtensions
{
    /// <summary>
    /// Ejecuta el validador y devuelve un 400 con errores VALIDATION_ERROR, o null si es valido.
    /// Uso: <c>if (await this.ValidateAsync(new XValidator(), request, ct) is { } invalid) return invalid;</c>
    /// </summary>
    public static async Task<ActionResult?> ValidateAsync<T>(
        this ControllerBase controller,
        IValidator<T> validator,
        T request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        return validation.IsValid
            ? null
            : controller.BadRequest(ApiResponse.Fail("Los datos proporcionados no son validos.",
                validation.Errors.Select(e => new ApiError("VALIDATION_ERROR", e.ErrorMessage)).ToArray()));
    }
}
