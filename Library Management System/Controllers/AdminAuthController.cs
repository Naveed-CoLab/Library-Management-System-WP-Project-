using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Models;
using System.Notifications;

namespace System.Controllers;

[Route("admin")]
public class AdminAuthController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminAuthController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [AllowAnonymous]
    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true && User.IsInRole(IdentitySeeder.AdminRole))
            return RedirectToAction("Index", "Home", new { area = "Admin" });

        return View(new AdminLoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(AdminLoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var email = model.Email.Trim().ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null || !await _userManager.IsInRoleAsync(user, IdentitySeeder.AdminRole))
        {
            ModelState.AddModelError(string.Empty, "Invalid admin credentials.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(email, model.Password, model.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
            return RedirectToAction("Index", "Home", new { area = "Admin" });

        if (result.IsLockedOut)
        {
            TempData.NotifyWarning("Too many failed attempts. Try again in 15 minutes.");
            return View(model);
        }

        ModelState.AddModelError(string.Empty, "Invalid admin credentials.");
        return View(model);
    }

    [AllowAnonymous]
    [HttpGet("access-denied")]
    public IActionResult AccessDenied()
    {
        TempData.NotifyWarning("Only administrators can access that page.");
        return View();
    }
}
