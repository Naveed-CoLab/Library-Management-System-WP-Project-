using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Models;

namespace System.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;

    public AccountController(
        SignInManager<ApplicationUser> signInManager)
    {
        _signInManager = signInManager;
    }

    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        return RedirectToAction("Login", "UserAuth", new { returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    public IActionResult Login(LoginViewModel model)
    {
        return RedirectToAction("Login", "UserAuth", new { returnUrl = model.ReturnUrl });
    }

    [AllowAnonymous]
    public IActionResult Register() => RedirectToAction("Signup", "UserAuth");

    [HttpPost]
    [AllowAnonymous]
    public IActionResult Register(RegisterViewModel model)
    {
        return RedirectToAction("Signup", "UserAuth");
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Login", "UserAuth");
    }

    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return RedirectToAction("AccessDenied", "UserAuth");
    }
}
