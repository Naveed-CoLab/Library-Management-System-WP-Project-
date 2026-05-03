using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Models;

namespace System.Areas.User.Controllers;

public class FinesController : UserControllerBase
{
    public FinesController(AppDbContext db, UserManager<ApplicationUser> userManager)
        : base(db, userManager) { }

    public async Task<IActionResult> Index(string? filter)
    {
        var member = await GetCurrentMemberAsync();
        if (member == null || member.IsBlocked)
            return MemberAccessDenied();

        filter ??= "unpaid";
        var query = Db.Fines
            .Include(f => f.BorrowRecord).ThenInclude(br => br.BookItem).ThenInclude(i => i.Book)
            .Where(f => f.BorrowRecord.MemberId == member.MemberId)
            .AsQueryable();

        query = filter switch
        {
            "paid" => query.Where(f => f.PaidOn != null),
            "all" => query,
            _ => query.Where(f => f.PaidOn == null)
        };

        ViewBag.Filter = filter;
        return View(await query.OrderByDescending(f => f.IssuedOn).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Pay(int id)
    {
        var member = await GetCurrentMemberAsync();
        if (member == null || member.IsBlocked)
            return MemberAccessDenied();

        var fine = await Db.Fines
            .Include(f => f.BorrowRecord)
            .FirstOrDefaultAsync(f => f.FineId == id && f.BorrowRecord.MemberId == member.MemberId);
        if (fine == null)
            return NotFound();

        if (fine.PaidOn == null)
        {
            fine.PaidOn = DateOnly.FromDateTime(DateTime.Today);
            await Db.SaveChangesAsync();
            TempData["Success"] = "Fine marked paid.";
        }

        return RedirectToAction(nameof(Index), new { filter = "paid" });
    }
}
