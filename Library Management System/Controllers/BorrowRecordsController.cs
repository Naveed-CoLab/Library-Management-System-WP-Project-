using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Models;

namespace System.Controllers;

public class BorrowRecordsController : Controller
{
    private readonly AppDbContext _context;
    public BorrowRecordsController(AppDbContext context) => _context = context;

    public async Task<IActionResult> Index(string? filter)
    {
        filter ??= "active";
        var today = DateOnly.FromDateTime(DateTime.Today);
        var q = _context.BorrowRecords
            .Include(br => br.Member)
            .Include(br => br.BookItem).ThenInclude(i => i.Book).ThenInclude(b => b.Author)
            .Include(br => br.BookItem).ThenInclude(i => i.Branch)
            .AsQueryable();

        q = filter switch
        {
            "returned" => q.Where(br => br.ReturnDate != null),
            "overdue" => q.Where(br =>
                br.ReturnDate == null
                && br.Status == LoanRules.StatusBorrowed
                && br.DueDate != null
                && br.DueDate < today),
            "active" => q.Where(br => br.ReturnDate == null && br.Status == LoanRules.StatusBorrowed),
            _ => q
        };

        ViewBag.Filter = filter;
        ViewBag.Today = today;
        return View(await q.OrderByDescending(br => br.BorrowDate).ToListAsync());
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();
        var record = await _context.BorrowRecords
            .Include(br => br.Member)
            .Include(br => br.BookItem).ThenInclude(i => i.Book).ThenInclude(b => b.Author)
            .Include(br => br.BookItem).ThenInclude(i => i.Branch)
            .Include(br => br.Fines)
            .FirstOrDefaultAsync(m => m.BorrowRecordId == id);
        if (record == null) return NotFound();
        ViewBag.Today = DateOnly.FromDateTime(DateTime.Today);
        return View(record);
    }

    private async Task PopulateDropdownsAsync(BorrowRecord? record = null)
    {
        ViewData["MemberId"] = new SelectList(
            await _context.Members.OrderBy(m => m.FullName).ToListAsync(),
            "MemberId", "FullName", record?.MemberId);

        var checkedOutIds = await _context.BorrowRecords
            .Where(br => br.ReturnDate == null && br.Status == LoanRules.StatusBorrowed)
            .Select(br => br.BookItemId)
            .ToListAsync();

        var items = await _context.BookItems
            .Include(i => i.Book).ThenInclude(b => b.Author)
            .Include(i => i.Branch)
            .OrderBy(i => i.Book.Title).ThenBy(i => i.Barcode)
            .ToListAsync();

        var eligible = items.Where(i =>
            !checkedOutIds.Contains(i.BookItemId) || (record != null && i.BookItemId == record.BookItemId));

        ViewData["BookItemId"] = new SelectList(
            eligible.Select(i => new
            {
                i.BookItemId,
                Display = $"{i.Book.Title} — {i.Barcode} · {i.Branch.Name}"
            }),
            "BookItemId", "Display", record?.BookItemId);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateDropdownsAsync();
        ViewBag.DefaultLoanDays = LoanRules.DefaultLoanDays;
        return View(new BorrowRecord
        {
            BorrowDate = DateOnly.FromDateTime(DateTime.Today)
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("MemberId,BookItemId,BorrowDate,DueDate")] BorrowRecord record)
    {
        record.Status = LoanRules.StatusBorrowed;
        record.ReturnDate = null;

        if (!record.DueDate.HasValue)
            record.DueDate = record.BorrowDate.AddDays(LoanRules.DefaultLoanDays);

        var memberActive = await _context.BorrowRecords.CountAsync(br =>
            br.MemberId == record.MemberId
            && br.ReturnDate == null
            && br.Status == LoanRules.StatusBorrowed);
        if (memberActive >= LoanRules.MaxConcurrentLoansPerMember)
            ModelState.AddModelError(string.Empty,
                $"Members may borrow at most {LoanRules.MaxConcurrentLoansPerMember} items at a time.");

        var alreadyOut = await _context.BorrowRecords.AnyAsync(br =>
            br.BookItemId == record.BookItemId
            && br.ReturnDate == null
            && br.Status == LoanRules.StatusBorrowed);
        if (alreadyOut)
            ModelState.AddModelError(nameof(BorrowRecord.BookItemId), "That copy is already checked out.");

        if (record.DueDate < record.BorrowDate)
            ModelState.AddModelError(nameof(BorrowRecord.DueDate), "Due date cannot be before the borrow date.");

        if (ModelState.IsValid)
        {
            _context.Add(record);
            await _context.SaveChangesAsync();

            var bookId = await _context.BookItems.Where(i => i.BookItemId == record.BookItemId).Select(i => i.BookId).FirstAsync();
            await FulfillReservationIfPossibleAsync(bookId, record.MemberId);
            TempData["Success"] = "Checkout recorded. Due date: " + record.DueDate?.ToString("d");
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdownsAsync(record);
        ViewBag.DefaultLoanDays = LoanRules.DefaultLoanDays;
        return View(record);
    }

    /// <summary>If member had a waiting reservation for this title, mark it fulfilled.</summary>
    private async Task FulfillReservationIfPossibleAsync(int bookId, int memberId)
    {
        var res = await _context.Reservations
            .Where(r => r.BookId == bookId && r.MemberId == memberId && r.Status == ReservationRules.Waiting)
            .OrderBy(r => r.ReservedAt)
            .FirstOrDefaultAsync();
        if (res != null)
        {
            res.Status = ReservationRules.Fulfilled;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var record = await _context.BorrowRecords.FindAsync(id);
        if (record == null) return NotFound();
        await PopulateDropdownsAsync(record);
        return View(record);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("BorrowRecordId,MemberId,BookItemId,BorrowDate,DueDate,ReturnDate,Status")] BorrowRecord record)
    {
        if (id != record.BorrowRecordId) return NotFound();

        if (record.DueDate.HasValue && record.DueDate < record.BorrowDate)
            ModelState.AddModelError(nameof(BorrowRecord.DueDate), "Due date cannot be before the borrow date.");

        if (record.ReturnDate.HasValue && record.ReturnDate < record.BorrowDate)
            ModelState.AddModelError(nameof(BorrowRecord.ReturnDate), "Return date cannot be before borrow date.");

        var dupLoan = await _context.BorrowRecords.AnyAsync(br =>
            br.BookItemId == record.BookItemId
            && br.BorrowRecordId != record.BorrowRecordId
            && br.ReturnDate == null
            && br.Status == LoanRules.StatusBorrowed);
        if (dupLoan)
            ModelState.AddModelError(nameof(BorrowRecord.BookItemId), "Another active loan already uses that copy.");

        if (ModelState.IsValid)
        {
            try
            {
                if (record.ReturnDate != null)
                    record.Status = LoanRules.StatusReturned;
                else if (record.Status == LoanRules.StatusReturned)
                    record.Status = LoanRules.StatusBorrowed;

                _context.Update(record);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Loan record saved.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.BorrowRecords.Any(e => e.BorrowRecordId == id))
                    return NotFound();
                throw;
            }
            return RedirectToAction(nameof(Index), new { filter = "all" });
        }

        await PopulateDropdownsAsync(record);
        return View(record);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Return(int id)
    {
        var record = await _context.BorrowRecords
            .Include(br => br.BookItem)
            .FirstOrDefaultAsync(br => br.BorrowRecordId == id);
        if (record == null) return NotFound();
        if (record.ReturnDate != null)
        {
            TempData["Warning"] = "This copy was already returned.";
            return RedirectToAction(nameof(Index));
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        record.ReturnDate = today;
        record.Status = LoanRules.StatusReturned;

        if (record.DueDate.HasValue && today > record.DueDate)
        {
            var days = today.DayNumber - record.DueDate.Value.DayNumber;
            var amount = Math.Round(1.50m * days, 2);
            if (amount > 0)
                _context.Fines.Add(new Fine
                {
                    BorrowRecordId = record.BorrowRecordId,
                    Amount = amount,
                    Reason = $"Overdue return ({days} day(s))",
                    IssuedOn = today
                });
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = "Item checked in.";
        return RedirectToAction(nameof(Index), new { filter = "returned" });
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        var record = await _context.BorrowRecords
            .Include(br => br.Member)
            .Include(br => br.BookItem).ThenInclude(i => i.Book)
            .FirstOrDefaultAsync(m => m.BorrowRecordId == id);
        if (record == null) return NotFound();
        return View(record);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var record = await _context.BorrowRecords.FindAsync(id);
        if (record != null)
        {
            _context.BorrowRecords.Remove(record);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Loan record removed.";
        }
        return RedirectToAction(nameof(Index), new { filter = "all" });
    }
}
