using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Notifications;

namespace System.Filters;

public sealed class ValidationSnackbarFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        if (context.Controller is not Controller controller)
            return;

        if (context.HttpContext.Request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
            return;

        if (context.Result is not ViewResult)
            return;

        if (!controller.ViewData.ModelState.IsValid)
            controller.TempData.NotifyError("Please fix the highlighted validation errors before continuing.");
    }
}
