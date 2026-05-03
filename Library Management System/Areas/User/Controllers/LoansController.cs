using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Models;

namespace System.Areas.User.Controllers;

public class LoansController : UserControllerBase
{
    public LoansController(AppDbContext db, UserManager<ApplicationUser> userManager)
        : base(db, userManager) { }

    public async Task<IActionResult> Index(string? filter)
    {
        var member = await GetCurrentMemberAsync();
        if (member == null || member.IsBlocked)
            return MemberAccessDenied();

        filter ??= "active";
        var today = DateOnly.FromDateTime(DateTime.Today);
        var query = Db.BorrowRecords
            .Include(br => br.BookItem).ThenInclude(i => i.Book).ThenInclude(b => b.Author)
            .Include(br => br.BookItem).ThenInclude(i => i.Branch)
            .Where(br => br.MemberId == member.MemberId)
            .AsQueryable();

        query = filter switch
        {
            "returned" => query.Where(br => br.ReturnDate != null),
            "overdue" => query.Where(br => br.ReturnDate == null && br.Status == LoanRules.StatusBorrowed && br.DueDate != null && br.DueDate < today),
            "all" => query,
            _ => query.Where(br => br.ReturnDate == null && br.Status == LoanRules.StatusBorrowed)
        };

        ViewBag.Filter = filter;
        ViewBag.Today = today;
        return View(await query.OrderByDescending(br => br.BorrowDate).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Return(int id)
    {
        var member = await GetCurrentMemberAsync();
        if (member == null || member.IsBlocked)
            return MemberAccessDenied();

        var record = await Db.BorrowRecords
            .FirstOrDefaultAsync(br => br.BorrowRecordId == id && br.MemberId == member.MemberId);
        if (record == null)
            return NotFound();

        if (record.ReturnDate != null)
        {
            TempData["Warning"] = "This book was already returned.";
            return RedirectToAction(nameof(Index));
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        record.ReturnDate = today;
        record.Status = LoanRules.StatusReturned;

        if (record.DueDate.HasValue && today > record.DueDate)
        {
            var settings = await GetSettingsAsync();
            var days = today.DayNumber - record.DueDate.Value.DayNumber;
            var amount = Math.Round(settings.DailyFineAmount * days, 2);
            if (amount > 0)
            {
                Db.Fines.Add(new Fine
                {
                    BorrowRecordId = record.BorrowRecordId,
                    Amount = amount,
                    Reason = $"Overdue return ({days} day(s))",
                    IssuedOn = today
                });
            }
        }

        await Db.SaveChangesAsync();
        TempData["Success"] = "Return recorded.";
        return RedirectToAction(nameof(Index), new { filter = "returned" });
    }
}
