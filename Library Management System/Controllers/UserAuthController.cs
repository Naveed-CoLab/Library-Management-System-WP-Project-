using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Models;
using System.Notifications;

namespace System.Controllers;

[Route("user")]
public class UserAuthController : Controller
{
    private readonly AppDbContext _db;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public UserAuthController(
        AppDbContext db,
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [AllowAnonymous]
    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true && User.IsInRole(IdentitySeeder.UserRole))
            return RedirectToAction("Index", "Home", new { area = "User" });

        return View(new UserLoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(UserLoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var email = model.Email.Trim().ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null || !await _userManager.IsInRoleAsync(user, IdentitySeeder.UserRole))
        {
            ModelState.AddModelError(string.Empty, "Invalid user credentials.");
            return View(model);
        }

        if (await _userManager.IsInRoleAsync(user, IdentitySeeder.AdminRole))
        {
            ModelState.AddModelError(string.Empty, "Use admin login for administrator accounts.");
            return View(model);
        }

        if (user.MemberId != null)
        {
            var blocked = await _db.Members.Where(m => m.MemberId == user.MemberId).Select(m => m.IsBlocked).FirstOrDefaultAsync();
            if (blocked)
            {
                ModelState.AddModelError(string.Empty, "This member account is blocked.");
                return View(model);
            }
        }

        var result = await _signInManager.PasswordSignInAsync(email, model.Password, model.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
            return RedirectToAction("Index", "Home", new { area = "User" });

        if (result.IsLockedOut)
        {
            TempData.NotifyWarning("Too many failed attempts. Try again in 15 minutes.");
            return View(model);
        }

        ModelState.AddModelError(string.Empty, "Invalid user credentials.");
        return View(model);
    }

    [AllowAnonymous]
    [HttpGet("signup")]
    public IActionResult Signup() => View(new UserSignupViewModel());

    [AllowAnonymous]
    [HttpPost("signup")]
    public async Task<IActionResult> Signup(UserSignupViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var email = model.Email.Trim().ToLowerInvariant();
        if (await _userManager.FindByEmailAsync(email) != null)
        {
            ModelState.AddModelError(nameof(model.Email), "An account already exists for this email.");
            return View(model);
        }

        var member = await _db.Members.FirstOrDefaultAsync(m => m.Email == email);
        if (member?.IsBlocked == true)
        {
            ModelState.AddModelError(nameof(model.Email), "This member is blocked. Contact admin.");
            return View(model);
        }

        if (member == null)
        {
            member = new Member
            {
                FullName = model.FullName.Trim(),
                Email = email,
                Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim(),
                MemberSince = DateOnly.FromDateTime(DateTime.Today)
            };
            _db.Members.Add(member);
            await _db.SaveChangesAsync();
        }

        var appUser = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = model.FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim(),
            MemberId = member.MemberId
        };

        var create = await _userManager.CreateAsync(appUser, model.Password);
        if (!create.Succeeded)
        {
            foreach (var error in create.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        await _userManager.AddToRoleAsync(appUser, IdentitySeeder.UserRole);
        await _signInManager.SignInAsync(appUser, isPersistent: false);
        return RedirectToAction("Index", "Home", new { area = "User" });
    }

    [AllowAnonymous]
    [HttpGet("access-denied")]
    public IActionResult AccessDenied()
    {
        TempData.NotifyWarning("You do not have permission to access this user page.");
        return View();
    }
}
