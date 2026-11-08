using Microsoft.AspNetCore.Mvc;
using ProDoc.Domain.Common;

namespace ProDoc.Api.Errors;

/// <summary>
/// Maps <see cref="AppResult"/> / <see cref="AppResult{T}"/> to HTTP responses without throwing.
/// </summary>
public static class AppResultExtensions
{
    public static ActionResult<T> ToActionResult<T>(this AppResult<T> result, ControllerBase controller)
    {
        if (result.IsSuccess)
        {
            return controller.Ok(result.Value);
        }

        return new ActionResult<T>(DomainProblemResults.FromAppError(result.Error!));
    }

    public static ActionResult ToActionResult(this AppResult result, ControllerBase controller)
    {
        if (result.IsSuccess)
        {
            return controller.NoContent();
        }

        return DomainProblemResults.FromAppError(result.Error!);
    }

    public static ActionResult<T> ToCreatedResult<T>(
        this AppResult<T> result,
        ControllerBase controller,
        string actionName,
        object routeValues)
    {
        if (result.IsSuccess)
        {
            return controller.CreatedAtAction(actionName, routeValues, result.Value);
        }

        return new ActionResult<T>(DomainProblemResults.FromAppError(result.Error!));
    }
}
