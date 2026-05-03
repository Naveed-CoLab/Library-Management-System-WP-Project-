using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Models;

namespace System.Controllers;

[Area("Admin")]
[Authorize(Roles = IdentitySeeder.AdminRole)]
public class AuthorsController : Controller
{
    private readonly AppDbContext _context;
    public AuthorsController(AppDbContext context) { _context = context; }

    public async Task<IActionResult> Index() =>
        View(await _context.Authors.Include(a => a.Books).ThenInclude(b => b.Category).ToListAsync());

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();
        var author = await _context.Authors.Include(a => a.Books).ThenInclude(b => b.Category).FirstOrDefaultAsync(m => m.AuthorId == id);
        if (author == null) return NotFound();
        return View(author);
    }

    public IActionResult Create() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("FullName,Nationality,DateOfBirth")] Author author)
    {
        NormalizeAuthor(author);
        await ValidateAuthorAsync(author);
        if (ModelState.IsValid) { _context.Add(author); await _context.SaveChangesAsync(); TempData["Success"] = "Author added."; return RedirectToAction(nameof(Index)); }
        return View(author);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var author = await _context.Authors.FindAsync(id);
        if (author == null) return NotFound();
        return View(author);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("AuthorId,FullName,Nationality,DateOfBirth")] Author author)
    {
        if (id != author.AuthorId) return NotFound();
        NormalizeAuthor(author);
        await ValidateAuthorAsync(author, id);
        if (ModelState.IsValid)
        {
            try { _context.Update(author); await _context.SaveChangesAsync(); TempData["Success"] = "Author updated."; }
            catch (DbUpdateConcurrencyException) { if (!_context.Authors.Any(e => e.AuthorId == id)) return NotFound(); throw; }
            return RedirectToAction(nameof(Index));
        }
        return View(author);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        var author = await _context.Authors.Include(a => a.Books).ThenInclude(b => b.Category).FirstOrDefaultAsync(m => m.AuthorId == id);
        if (author == null) return NotFound();
        return View(author);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var author = await _context.Authors
            .Include(a => a.Books).ThenInclude(b => b.Reservations)
            .Include(a => a.Books).ThenInclude(b => b.Items).ThenInclude(i => i.BorrowRecords)
            .FirstOrDefaultAsync(a => a.AuthorId == id);
        if (author == null)
            return RedirectToAction(nameof(Index));

        foreach (var book in author.Books)
        {
            var active = book.Items.SelectMany(i => i.BorrowRecords)
                .Any(br => br.ReturnDate == null && br.Status == LoanRules.StatusBorrowed);
            if (active)
            {
                TempData["Error"] = "Cannot delete this author while a linked copy is still on loan.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        foreach (var book in author.Books.ToList())
        {
            _context.Reservations.RemoveRange(book.Reservations);
            foreach (var item in book.Items.ToList())
            {
                _context.BorrowRecords.RemoveRange(item.BorrowRecords);
                _context.BookItems.Remove(item);
            }
            _context.Books.Remove(book);
        }

        _context.Authors.Remove(author);
        await _context.SaveChangesAsync();
        TempData["Success"] = "Author and linked catalogue records removed.";
        return RedirectToAction(nameof(Index));
    }

    private static void NormalizeAuthor(Author author)
    {
        author.FullName = author.FullName.Trim();
        author.Nationality = string.IsNullOrWhiteSpace(author.Nationality) ? null : author.Nationality.Trim();
    }

    private async Task ValidateAuthorAsync(Author author, int? currentAuthorId = null)
    {
        if (author.DateOfBirth.HasValue && author.DateOfBirth.Value > DateOnly.FromDateTime(DateTime.Today))
            ModelState.AddModelError(nameof(Author.DateOfBirth), "Date of birth cannot be in the future.");

        var duplicate = await _context.Authors.AnyAsync(a =>
            a.FullName == author.FullName
            && (!currentAuthorId.HasValue || a.AuthorId != currentAuthorId.Value));
        if (duplicate)
            ModelState.AddModelError(nameof(Author.FullName), "An author with this name already exists.");
    }
}
