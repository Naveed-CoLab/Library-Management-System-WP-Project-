using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Models;

namespace System.Controllers;

[AllowAnonymous]
public class ErrorController : Controller
{
    public IActionResult Index()
    {
        Response.StatusCode = StatusCodes.Status500InternalServerError;
        return View("Error", new ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
    }

    public IActionResult Status(int id)
    {
        Response.StatusCode = id;
        ViewBag.StatusCode = id;
        return View("Status");
    }
}
