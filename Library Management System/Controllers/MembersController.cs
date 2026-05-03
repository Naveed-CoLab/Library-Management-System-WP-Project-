using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Models;

namespace System.Controllers;

public class MembersController : Controller
{
    private readonly AppDbContext _context;
    public MembersController(AppDbContext context) { _context = context; }

    public async Task<IActionResult> Index() =>
        View(await _context.Members.ToListAsync());

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();
        var member = await _context.Members.Include(m => m.BorrowRecords).ThenInclude(br => br.BookItem).ThenInclude(i => i.Book).ThenInclude(b => b.Author).FirstOrDefaultAsync(m => m.MemberId == id);
        if (member == null) return NotFound();
        return View(member);
    }

    public IActionResult Create() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("FullName,Email,Phone,MemberSince")] Member member)
    {
        if (ModelState.IsValid) { _context.Add(member); await _context.SaveChangesAsync(); return RedirectToAction(nameof(Index)); }
        return View(member);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var member = await _context.Members.FindAsync(id);
        if (member == null) return NotFound();
        return View(member);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("MemberId,FullName,Email,Phone,MemberSince")] Member member)
    {
        if (id != member.MemberId) return NotFound();
        if (ModelState.IsValid)
        {
            try { _context.Update(member); await _context.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { if (!_context.Members.Any(e => e.MemberId == id)) return NotFound(); throw; }
            return RedirectToAction(nameof(Index));
        }
        return View(member);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        var member = await _context.Members.FirstOrDefaultAsync(m => m.MemberId == id);
        if (member == null) return NotFound();
        return View(member);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var member = await _context.Members.Include(m => m.BorrowRecords).FirstOrDefaultAsync(m => m.MemberId == id);
        if (member != null)
        {
            var hasActive = member.BorrowRecords.Any(br =>
                br.ReturnDate == null && br.Status == LoanRules.StatusBorrowed);
            if (hasActive)
            {
                TempData["Error"] = "Cannot delete a member who still has books checked out.";
                return RedirectToAction(nameof(Details), new { id });
            }

            _context.BorrowRecords.RemoveRange(member.BorrowRecords);
            _context.Members.Remove(member);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Member removed.";
        }
        return RedirectToAction(nameof(Index));
    }
}
