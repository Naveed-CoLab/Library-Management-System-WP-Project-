using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using System.Notifications;

namespace System.Filters;

public sealed class ExceptionSnackbarFilter : IExceptionFilter
{
    private readonly ILogger<ExceptionSnackbarFilter> _logger;
    private readonly ITempDataDictionaryFactory _tempDataFactory;

    public ExceptionSnackbarFilter(
        ILogger<ExceptionSnackbarFilter> logger,
        ITempDataDictionaryFactory tempDataFactory)
    {
        _logger = logger;
        _tempDataFactory = tempDataFactory;
    }

    public void OnException(ExceptionContext context)
    {
        if (context.ExceptionHandled)
            return;

        _logger.LogError(context.Exception, "Unhandled MVC exception at {Path}", context.HttpContext.Request.Path);

        _tempDataFactory.GetTempData(context.HttpContext)
            .NotifyError("Something went wrong while processing your request. Please try again.");

        context.ExceptionHandled = true;
        context.Result = new RedirectToActionResult("Index", "Error", new { area = "" });
    }
}
