using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Models;

namespace System.Controllers;

public class BooksController : Controller
{
    private readonly AppDbContext _context;
    public BooksController(AppDbContext context) => _context = context;

    private async Task<IDictionary<int, int>> ActiveLoanCountByBookAsync()
    {
        var rows = await _context.BorrowRecords.AsNoTracking()
            .Where(br => br.ReturnDate == null && br.Status == LoanRules.StatusBorrowed)
            .Join(_context.BookItems.AsNoTracking(), br => br.BookItemId, i => i.BookItemId, (_, i) => i.BookId)
            .GroupBy(bookId => bookId)
            .Select(g => new { BookId = g.Key, Count = g.Count() })
            .ToListAsync();
        return rows.ToDictionary(x => x.BookId, x => x.Count);
    }

    public async Task<IActionResult> Index(string? q, int? categoryId)
    {
        var query = _context.Books
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
                || b.Author.FullName.Contains(term)
                || b.Items.Any(i => i.Barcode.Contains(term) || (i.ShelfLocation != null && i.ShelfLocation.Contains(term))));
        }

        if (categoryId is > 0)
            query = query.Where(b => b.CategoryId == categoryId);

        ViewBag.OutCounts = await ActiveLoanCountByBookAsync();
        var cats = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
        var catOpts = new List<SelectListItem>
        {
            new() { Value = "", Text = "All categories", Selected = categoryId is null || categoryId == 0 }
        };
        catOpts.AddRange(cats.Select(c => new SelectListItem
        {
            Value = c.CategoryId.ToString(),
            Text = c.Name,
            Selected = categoryId == c.CategoryId
        }));
        ViewBag.CategoryOptions = catOpts;
        ViewBag.Q = q;
        ViewBag.CategoryId = categoryId;

        return View(await query.OrderBy(b => b.Title).ToListAsync());
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();
        var busyIds = await _context.BorrowRecords.AsNoTracking()
            .Where(br => br.ReturnDate == null && br.Status == LoanRules.StatusBorrowed)
            .Select(br => br.BookItemId)
            .ToHashSetAsync();

        var book = await _context.Books
            .Include(b => b.Author)
            .Include(b => b.Category)
            .Include(b => b.Publisher)
            .Include(b => b.Items).ThenInclude(i => i.Branch)
            .Include(b => b.Items).ThenInclude(i => i.BorrowRecords).ThenInclude(br => br.Member)
            .FirstOrDefaultAsync(m => m.BookId == id);
        if (book == null) return NotFound();

        ViewBag.BusyItemIds = busyIds;
        ViewBag.ActiveReservationCount = await _context.Reservations.AsNoTracking()
            .CountAsync(r => r.BookId == book.BookId && r.Status == ReservationRules.Waiting);
        return View(book);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateBookLookupsAsync();
        ViewBag.InitialCopies = 1;
        ViewBag.BranchId = (await _context.Branches.OrderBy(b => b.BranchId).FirstAsync()).BranchId;
        ViewBag.ShelfPrefix = "";
        return View(new Book());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("Title,ISBN,PublishedYear,Summary,AuthorId,PublisherId,CategoryId")] Book book,
        int initialCopies = 1,
        int branchId = 1,
        string? shelfPrefix = null)
    {
        if (initialCopies < 1 || initialCopies > 999)
            ModelState.AddModelError(string.Empty, "Initial copies must be between 1 and 999.");
        if (!await _context.Branches.AnyAsync(b => b.BranchId == branchId))
            ModelState.AddModelError(string.Empty, "Choose a valid branch.");

        if (ModelState.IsValid)
        {
            book.ISBN = string.IsNullOrWhiteSpace(book.ISBN) ? null : book.ISBN.Trim();
            _context.Add(book);
            await _context.SaveChangesAsync();

            for (var i = 1; i <= initialCopies; i++)
            {
                var barcode = $"LIB-{book.BookId:D5}-{i:D2}";
                var shelf = string.IsNullOrWhiteSpace(shelfPrefix) ? null : $"{shelfPrefix.Trim()}-{i:D2}";
                _context.BookItems.Add(new BookItem
                {
                    BookId = book.BookId,
                    BranchId = branchId,
                    Barcode = barcode,
                    ShelfLocation = shelf
                });
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Catalogued with {initialCopies} copy/copies.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateBookLookupsAsync(book);
        ViewBag.InitialCopies = initialCopies;
        ViewBag.BranchId = branchId;
        ViewBag.ShelfPrefix = shelfPrefix;
        return View(book);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var book = await _context.Books.FindAsync(id);
        if (book == null) return NotFound();
        await PopulateBookLookupsAsync(book);
        return View(book);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("BookId,Title,ISBN,PublishedYear,Summary,AuthorId,PublisherId,CategoryId")] Book book)
    {
        if (id != book.BookId) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                book.ISBN = string.IsNullOrWhiteSpace(book.ISBN) ? null : book.ISBN.Trim();
                _context.Update(book);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Bibliographic record updated.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Books.Any(e => e.BookId == id)) return NotFound();
                throw;
            }
            return RedirectToAction(nameof(Index));
        }

        await PopulateBookLookupsAsync(book);
        return View(book);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        var book = await _context.Books
            .Include(b => b.Author)
            .Include(b => b.Items)
            .FirstOrDefaultAsync(m => m.BookId == id);
        if (book == null) return NotFound();
        return View(book);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var book = await _context.Books
            .Include(b => b.Reservations)
            .Include(b => b.Items).ThenInclude(i => i.BorrowRecords)
            .FirstOrDefaultAsync(b => b.BookId == id);
        if (book != null)
        {
            foreach (var item in book.Items)
            {
                if (item.BorrowRecords.Any(br => br.ReturnDate == null && br.Status == LoanRules.StatusBorrowed))
                {
                    TempData["Error"] = "Cannot delete while at least one copy is still on loan.";
                    return RedirectToAction(nameof(Details), new { id });
                }
            }

            foreach (var item in book.Items.ToList())
            {
                _context.BorrowRecords.RemoveRange(item.BorrowRecords);
                _context.BookItems.Remove(item);
            }

            _context.Reservations.RemoveRange(book.Reservations);
            _context.Books.Remove(book);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Title and all copies removed.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateBookLookupsAsync(Book? selected = null)
    {
        ViewData["AuthorId"] = new SelectList(await _context.Authors.OrderBy(a => a.FullName).ToListAsync(), "AuthorId", "FullName", selected?.AuthorId);
        ViewData["PublisherId"] = new SelectList(await _context.Publishers.OrderBy(p => p.Name).ToListAsync(), "PublisherId", "Name", selected?.PublisherId);
        ViewData["CategoryId"] = new SelectList(await _context.Categories.OrderBy(c => c.Name).ToListAsync(), "CategoryId", "Name", selected?.CategoryId);
        ViewData["BranchId"] = new SelectList(await _context.Branches.OrderBy(b => b.Name).ToListAsync(), "BranchId", "Name");
    }
}
