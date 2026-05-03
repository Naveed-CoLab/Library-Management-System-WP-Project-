using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Models;

namespace System.Controllers;

[Area("Admin")]
[Authorize(Roles = IdentitySeeder.AdminRole)]
public class ReportsController : Controller
{
    private readonly AppDbContext _db;

    public ReportsController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var activeLoans = await _db.BorrowRecords
            .Include(br => br.Member)
            .Include(br => br.BookItem).ThenInclude(i => i.Book).ThenInclude(b => b.Author)
            .Where(br => br.ReturnDate == null && br.Status == LoanRules.StatusBorrowed)
            .OrderBy(br => br.DueDate)
            .ToListAsync();

        var overdue = activeLoans
            .Where(br => br.DueDate.HasValue && br.DueDate < today)
            .ToList();

        var popularBooks = await _db.BorrowRecords.AsNoTracking()
            .Include(br => br.BookItem).ThenInclude(i => i.Book).ThenInclude(b => b.Author)
            .GroupBy(br => new
            {
                br.BookItem.BookId,
                br.BookItem.Book.Title,
                Author = br.BookItem.Book.Author.FullName
            })
            .Select(g => new PopularBookReportRow
            {
                BookId = g.Key.BookId,
                Title = g.Key.Title,
                Author = g.Key.Author,
                BorrowCount = g.Count()
            })
            .OrderByDescending(r => r.BorrowCount)
            .Take(10)
            .ToListAsync();

        var vm = new ReportsViewModel
        {
            ActiveLoans = activeLoans,
            OverdueLoans = overdue,
            PopularBooks = popularBooks,
            OutstandingFinesTotal = await _db.Fines
                .Where(f => f.PaidOn == null)
                .SumAsync(f => (decimal?)f.Amount) ?? 0m
        };

        return View(vm);
    }
}
