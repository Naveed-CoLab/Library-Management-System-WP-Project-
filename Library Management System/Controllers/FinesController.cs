using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Models;

namespace System.Controllers;

[Area("Admin")]
[Authorize(Roles = IdentitySeeder.AdminRole)]
public class FinesController : Controller
{
    private readonly AppDbContext _context;

    public FinesController(AppDbContext context) => _context = context;

    public async Task<IActionResult> Index(string? filter)
    {
        filter ??= "unpaid";
        var q = _context.Fines
            .Include(f => f.BorrowRecord).ThenInclude(br => br.Member)
            .Include(f => f.BorrowRecord).ThenInclude(br => br.BookItem).ThenInclude(i => i.Book)
            .AsQueryable();

        q = filter switch
        {
            "paid" => q.Where(f => f.PaidOn != null),
            "all" => q,
            _ => q.Where(f => f.PaidOn == null)
        };

        ViewBag.Filter = filter;
        return View(await q.OrderByDescending(f => f.IssuedOn).ToListAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(int id)
    {
        var fine = await _context.Fines.FindAsync(id);
        if (fine == null) return NotFound();
        if (fine.PaidOn != null)
        {
            TempData["Warning"] = "Fine already marked paid.";
            return RedirectToAction(nameof(Index));
        }

        fine.PaidOn = DateOnly.FromDateTime(DateTime.Today);
        await _context.SaveChangesAsync();
        TempData["Success"] = "Payment recorded.";
        return RedirectToAction(nameof(Index), new { filter = "paid" });
    }
}
