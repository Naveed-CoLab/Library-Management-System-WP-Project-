using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Models;

namespace System.Areas.User.Controllers;

public class HomeController : UserControllerBase
{
    public HomeController(AppDbContext db, UserManager<ApplicationUser> userManager)
        : base(db, userManager) { }

    public async Task<IActionResult> Index()
    {
        var member = await GetCurrentMemberAsync();
        if (member == null || member.IsBlocked)
            return MemberAccessDenied();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var currentLoans = await Db.BorrowRecords
            .Include(br => br.BookItem).ThenInclude(i => i.Book).ThenInclude(b => b.Author)
            .Where(br => br.MemberId == member.MemberId && br.ReturnDate == null && br.Status == LoanRules.StatusBorrowed)
            .OrderBy(br => br.DueDate)
            .ToListAsync();

        var vm = new UserDashboardViewModel
        {
            Member = member,
            CurrentLoans = currentLoans,
            ActiveLoans = currentLoans.Count,
            OverdueLoans = currentLoans.Count(br => br.DueDate.HasValue && br.DueDate < today),
            Reservations = await Db.Reservations.CountAsync(r => r.MemberId == member.MemberId && r.Status == ReservationRules.Waiting),
            OutstandingFinesTotal = await Db.Fines
                .Where(f => f.PaidOn == null && f.BorrowRecord.MemberId == member.MemberId)
                .SumAsync(f => (decimal?)f.Amount) ?? 0m,
            RecentReservations = await Db.Reservations
                .Include(r => r.Book).ThenInclude(b => b.Author)
                .Where(r => r.MemberId == member.MemberId)
                .OrderByDescending(r => r.ReservedAt)
                .Take(5)
                .ToListAsync(),
            OpenFines = await Db.Fines
                .Include(f => f.BorrowRecord).ThenInclude(br => br.BookItem).ThenInclude(i => i.Book)
                .Where(f => f.PaidOn == null && f.BorrowRecord.MemberId == member.MemberId)
                .OrderByDescending(f => f.IssuedOn)
                .Take(5)
                .ToListAsync()
        };

        return View(vm);
    }
}
