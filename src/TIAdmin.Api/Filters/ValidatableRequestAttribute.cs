namespace TIAdmin.Api.Filters;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TIAdmin.Application.Common.Models;

/// <summary>
/// Activa la validacion automatica de modelos antes de entrar al controller.
/// Evita duplicar la validacion en cada endpoint.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ValidatableRequestAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.ModelState.IsValid)
        {
            return;
        }

        var errors = context.ModelState
            .SelectMany(entry => entry.Value?.Errors.Select(error =>
                new ApiError("VALIDATION_ERROR", string.IsNullOrWhiteSpace(error.ErrorMessage)
                    ? "El valor proporcionado no es valido."
                    : error.ErrorMessage)) ?? [])
            .ToArray();

        context.Result = new BadRequestObjectResult(
            ApiResponse.Fail("Los datos proporcionados no son validos.", errors));
    }
}
