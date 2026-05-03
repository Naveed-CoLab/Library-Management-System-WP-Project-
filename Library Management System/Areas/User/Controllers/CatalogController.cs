using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Models;

namespace System.Areas.User.Controllers;

public class CatalogController : UserControllerBase
{
    public CatalogController(AppDbContext db, UserManager<ApplicationUser> userManager)
        : base(db, userManager) { }

    public async Task<IActionResult> Index(string? q, int? categoryId)
    {
        var query = Db.Books
            .Include(b => b.Author)
            .Include(b => b.Category)
            .Include(b => b.Publisher)
            .Include(b => b.Items)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(b =>
                b.Title.Contains(term)
                || (b.ISBN != null && b.ISBN.Contains(term))
                || b.Author.FullName.Contains(term));
        }

        if (categoryId is > 0)
            query = query.Where(b => b.CategoryId == categoryId);

        ViewBag.OutCounts = await ActiveLoanCountByBookAsync();
        ViewBag.CategoryOptions = await BuildCategoryOptionsAsync(categoryId);
        ViewBag.Q = q;
        ViewBag.CategoryId = categoryId;
        return View(await query.OrderBy(b => b.Title).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var book = await Db.Books
            .Include(b => b.Author)
            .Include(b => b.Category)
            .Include(b => b.Publisher)
            .Include(b => b.Items).ThenInclude(i => i.Branch)
            .FirstOrDefaultAsync(b => b.BookId == id);
        if (book == null)
            return NotFound();

        var borrowedIds = await Db.BorrowRecords
            .Where(br => br.ReturnDate == null && br.Status == LoanRules.StatusBorrowed)
            .Select(br => br.BookItemId)
            .ToListAsync();
        ViewBag.BorrowedCopyIds = borrowedIds.ToHashSet();
        ViewBag.ActiveReservationCount = await Db.Reservations.CountAsync(r => r.BookId == id && r.Status == ReservationRules.Waiting);
        return View(book);
    }

    [HttpPost]
    public async Task<IActionResult> Borrow(int id)
    {
        var member = await GetCurrentMemberAsync();
        if (member == null || member.IsBlocked)
            return MemberAccessDenied();

        var bookExists = await Db.Books.AnyAsync(b => b.BookId == id);
        if (!bookExists)
            return NotFound();

        var settings = await GetSettingsAsync();
        var activeCount = await Db.BorrowRecords.CountAsync(br =>
            br.MemberId == member.MemberId
            && br.ReturnDate == null
            && br.Status == LoanRules.StatusBorrowed);
        if (activeCount >= settings.MaxConcurrentLoansPerMember)
        {
            TempData["Error"] = $"You can borrow at most {settings.MaxConcurrentLoansPerMember} books at a time.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var alreadyBorrowedTitle = await Db.BorrowRecords
            .Include(br => br.BookItem)
            .AnyAsync(br =>
                br.MemberId == member.MemberId
                && br.BookItem.BookId == id
                && br.ReturnDate == null
                && br.Status == LoanRules.StatusBorrowed);
        if (alreadyBorrowedTitle)
        {
            TempData["Warning"] = "You already have this title on loan.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var borrowedIds = await Db.BorrowRecords
            .Where(br => br.ReturnDate == null && br.Status == LoanRules.StatusBorrowed)
            .Select(br => br.BookItemId)
            .ToListAsync();

        var copy = await Db.BookItems
            .Where(i => i.BookId == id && !borrowedIds.Contains(i.BookItemId))
            .OrderBy(i => i.BookItemId)
            .FirstOrDefaultAsync();
        if (copy == null)
        {
            TempData["Warning"] = "No copy is available right now. Place a reservation instead.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        Db.BorrowRecords.Add(new BorrowRecord
        {
            MemberId = member.MemberId,
            BookItemId = copy.BookItemId,
            BorrowDate = today,
            DueDate = today.AddDays(settings.DefaultLoanDays),
            Status = LoanRules.StatusBorrowed
        });

        var reservation = await Db.Reservations
            .Where(r => r.BookId == id && r.MemberId == member.MemberId && r.Status == ReservationRules.Waiting)
            .OrderBy(r => r.ReservedAt)
            .FirstOrDefaultAsync();
        if (reservation != null)
            reservation.Status = ReservationRules.Fulfilled;

        await Db.SaveChangesAsync();
        TempData["Success"] = "Book borrowed. Check your loan list for the due date.";
        return RedirectToAction("Index", "Loans", new { area = "User" });
    }

    [HttpPost]
    public async Task<IActionResult> Reserve(int id)
    {
        var member = await GetCurrentMemberAsync();
        if (member == null || member.IsBlocked)
            return MemberAccessDenied();

        var bookExists = await Db.Books.AnyAsync(b => b.BookId == id);
        if (!bookExists)
            return NotFound();

        var exists = await Db.Reservations.AnyAsync(r =>
            r.MemberId == member.MemberId
            && r.BookId == id
            && r.Status == ReservationRules.Waiting);
        if (exists)
        {
            TempData["Warning"] = "You already have an active reservation for this book.";
            return RedirectToAction(nameof(Details), new { id });
        }

        Db.Reservations.Add(new Reservation
        {
            MemberId = member.MemberId,
            BookId = id,
            ReservedAt = DateTime.UtcNow,
            ExpiresOn = DateOnly.FromDateTime(DateTime.Today.AddDays(7)),
            Status = ReservationRules.Waiting
        });
        await Db.SaveChangesAsync();
        TempData["Success"] = "Reservation placed.";
        return RedirectToAction("Index", "Reservations", new { area = "User" });
    }

    private async Task<IDictionary<int, int>> ActiveLoanCountByBookAsync()
    {
        var rows = await Db.BorrowRecords.AsNoTracking()
            .Where(br => br.ReturnDate == null && br.Status == LoanRules.StatusBorrowed)
            .Join(Db.BookItems.AsNoTracking(), br => br.BookItemId, i => i.BookItemId, (_, i) => i.BookId)
            .GroupBy(bookId => bookId)
            .Select(g => new { BookId = g.Key, Count = g.Count() })
            .ToListAsync();
        return rows.ToDictionary(x => x.BookId, x => x.Count);
    }

    private async Task<List<SelectListItem>> BuildCategoryOptionsAsync(int? selected)
    {
        var options = new List<SelectListItem>
        {
            new() { Value = "", Text = "All categories", Selected = selected is null or 0 }
        };
        options.AddRange((await Db.Categories.OrderBy(c => c.Name).ToListAsync()).Select(c => new SelectListItem
        {
            Value = c.CategoryId.ToString(),
            Text = c.Name,
            Selected = selected == c.CategoryId
        }));
        return options;
    }
}
