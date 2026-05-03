using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Models;

namespace System.Areas.User.Controllers;

[Area("User")]
[Authorize(Roles = IdentitySeeder.UserRole)]
public abstract class UserControllerBase : Controller
{
    protected readonly AppDbContext Db;
    protected readonly UserManager<ApplicationUser> UserManager;

    protected UserControllerBase(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        Db = db;
        UserManager = userManager;
    }

    protected async Task<Member?> GetCurrentMemberAsync()
    {
        var appUser = await UserManager.GetUserAsync(User);
        if (appUser?.MemberId == null)
            return null;

        return await Db.Members.FirstOrDefaultAsync(m => m.MemberId == appUser.MemberId);
    }

    protected IActionResult MemberAccessDenied()
    {
        return RedirectToAction("AccessDenied", "UserAuth", new { area = "" });
    }

    protected async Task<LibrarySettings> GetSettingsAsync()
    {
        var settings = await Db.LibrarySettings.FirstOrDefaultAsync(s => s.LibrarySettingsId == 1);
        if (settings != null)
            return settings;

        settings = new LibrarySettings();
        Db.LibrarySettings.Add(settings);
        await Db.SaveChangesAsync();
        return settings;
    }

    protected async Task<bool> IsCopyBorrowedAsync(int bookItemId)
    {
        return await Db.BorrowRecords.AnyAsync(br =>
            br.BookItemId == bookItemId
            && br.ReturnDate == null
            && br.Status == LoanRules.StatusBorrowed);
    }
}
