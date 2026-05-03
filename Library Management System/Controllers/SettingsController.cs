using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Models;

namespace System.Controllers;

[Area("Admin")]
[Authorize(Roles = IdentitySeeder.AdminRole)]
public class SettingsController : Controller
{
    private readonly AppDbContext _db;

    public SettingsController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        return View(await GetSettingsAsync());
    }

    public async Task<IActionResult> Edit()
    {
        return View(await GetSettingsAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Edit([Bind("LibrarySettingsId,DefaultLoanDays,MaxConcurrentLoansPerMember,DailyFineAmount")] LibrarySettings settings)
    {
        if (!ModelState.IsValid)
            return View(settings);

        settings.LibrarySettingsId = 1;
        _db.Update(settings);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Library settings updated.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<LibrarySettings> GetSettingsAsync()
    {
        var settings = await _db.LibrarySettings.FirstOrDefaultAsync(s => s.LibrarySettingsId == 1);
        if (settings != null)
            return settings;

        settings = new LibrarySettings();
        _db.LibrarySettings.Add(settings);
        await _db.SaveChangesAsync();
        return settings;
    }
}
