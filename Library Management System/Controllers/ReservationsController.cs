using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Models;

namespace System.Controllers;

[Area("Admin")]
[Authorize(Roles = IdentitySeeder.AdminRole)]
public class ReservationsController : Controller
{
    private readonly AppDbContext _context;

    public ReservationsController(AppDbContext context) => _context = context;

    public async Task<IActionResult> Index()
    {
        var rows = await _context.Reservations
            .Include(r => r.Member)
            .Include(r => r.Book).ThenInclude(b => b.Author)
            .OrderByDescending(r => r.ReservedAt)
            .ToListAsync();
        return View(rows);
    }

    public async Task<IActionResult> Create()
    {
        ViewData["MemberId"] = new SelectList(await _context.Members.OrderBy(m => m.FullName).ToListAsync(), "MemberId", "FullName");
        ViewData["BookId"] = new SelectList(
            await _context.Books.Include(b => b.Author).OrderBy(b => b.Title)
                .Select(b => new { b.BookId, Label = b.Title + " — " + b.Author.FullName }).ToListAsync(),
            "BookId", "Label");
        return View(new Reservation { ExpiresOn = DateOnly.FromDateTime(DateTime.Today.AddDays(7)) });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("MemberId,BookId,ExpiresOn")] Reservation reservation)
    {
        reservation.ReservedAt = DateTime.UtcNow;
        reservation.Status = ReservationRules.Waiting;
        await ValidateReservationAsync(reservation);

        var dup = await _context.Reservations.AnyAsync(r =>
            r.MemberId == reservation.MemberId
            && r.BookId == reservation.BookId
            && r.Status == ReservationRules.Waiting);
        if (dup)
            ModelState.AddModelError(string.Empty, "That member already has an active hold on this title.");

        if (ModelState.IsValid)
        {
            _context.Add(reservation);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Hold placed.";
            return RedirectToAction(nameof(Index));
        }

        ViewData["MemberId"] = new SelectList(await _context.Members.OrderBy(m => m.FullName).ToListAsync(), "MemberId", "FullName", reservation.MemberId);
        ViewData["BookId"] = new SelectList(
            await _context.Books.Include(b => b.Author).OrderBy(b => b.Title)
                .Select(b => new { b.BookId, Label = b.Title + " — " + b.Author.FullName }).ToListAsync(),
            "BookId", "Label", reservation.BookId);
        return View(reservation);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var res = await _context.Reservations.FindAsync(id);
        if (res == null) return NotFound();
        if (res.Status == ReservationRules.Waiting)
        {
            res.Status = ReservationRules.Cancelled;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Hold cancelled.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateReservationAsync(Reservation reservation)
    {
        var member = await _context.Members.AsNoTracking().FirstOrDefaultAsync(m => m.MemberId == reservation.MemberId);
        if (member == null)
            ModelState.AddModelError(nameof(Reservation.MemberId), "Choose a valid member.");
        else if (member.IsBlocked)
            ModelState.AddModelError(nameof(Reservation.MemberId), "Blocked members cannot place holds.");

        if (!await _context.Books.AnyAsync(b => b.BookId == reservation.BookId))
            ModelState.AddModelError(nameof(Reservation.BookId), "Choose a valid book.");

        if (reservation.ExpiresOn.HasValue && reservation.ExpiresOn.Value < DateOnly.FromDateTime(DateTime.Today))
            ModelState.AddModelError(nameof(Reservation.ExpiresOn), "Expiration date cannot be in the past.");
    }
}
