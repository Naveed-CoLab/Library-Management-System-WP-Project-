using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Diagnostics;
using System.Models;

namespace System.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;

    public HomeController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var totalCopies = await _db.BookItems.AsNoTracking().CountAsync();
        var activeLoanIds = await _db.BorrowRecords.AsNoTracking()
            .Where(br => br.ReturnDate == null && br.Status == LoanRules.StatusBorrowed)
            .Select(br => br.BookItemId)
            .ToListAsync();
        var busy = activeLoanIds.ToHashSet();
        var availableCopies = totalCopies - busy.Count;

        var overdueCount = await _db.BorrowRecords.AsNoTracking()
            .CountAsync(br => br.ReturnDate == null
                && br.Status == LoanRules.StatusBorrowed
                && br.DueDate != null
                && br.DueDate < today);

        var overdueItems = await _db.BorrowRecords.AsNoTracking()
            .Include(br => br.Member)
            .Include(br => br.BookItem).ThenInclude(i => i.Book)
            .Where(br => br.ReturnDate == null
                && br.Status == LoanRules.StatusBorrowed
                && br.DueDate != null
                && br.DueDate < today)
            .OrderBy(br => br.DueDate)
            .Take(12)
            .ToListAsync();

        var recent = await _db.BorrowRecords.AsNoTracking()
            .Include(br => br.Member)
            .Include(br => br.BookItem).ThenInclude(i => i.Book).ThenInclude(b => b.Author)
            .OrderByDescending(br => br.BorrowRecordId)
            .Take(8)
            .ToListAsync();

        var pendingRes = await _db.Reservations.AsNoTracking()
            .CountAsync(r => r.Status == ReservationRules.Waiting);

        var finesTotal = await _db.Fines.AsNoTracking()
            .Where(f => f.PaidOn == null)
            .SumAsync(f => (decimal?)f.Amount) ?? 0m;

        var vm = new DashboardViewModel
        {
            TotalBooks = await _db.Books.AsNoTracking().CountAsync(),
            TotalCopies = totalCopies,
            TotalAuthors = await _db.Authors.AsNoTracking().CountAsync(),
            TotalMembers = await _db.Members.AsNoTracking().CountAsync(),
            ActiveLoans = busy.Count,
            OverdueLoans = overdueCount,
            AvailableCopiesNow = availableCopies,
            PendingReservations = pendingRes,
            OutstandingFinesTotal = finesTotal,
            RecentActivity = recent,
            OverdueItems = overdueItems
        };

        return View(vm);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
