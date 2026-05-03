using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Models;

namespace System.Areas.User.Controllers;

public class ReservationsController : UserControllerBase
{
    public ReservationsController(AppDbContext db, UserManager<ApplicationUser> userManager)
        : base(db, userManager) { }

    public async Task<IActionResult> Index()
    {
        var member = await GetCurrentMemberAsync();
        if (member == null || member.IsBlocked)
            return MemberAccessDenied();

        var reservations = await Db.Reservations
            .Include(r => r.Book).ThenInclude(b => b.Author)
            .Where(r => r.MemberId == member.MemberId)
            .OrderByDescending(r => r.ReservedAt)
            .ToListAsync();

        return View(reservations);
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(int id)
    {
        var member = await GetCurrentMemberAsync();
        if (member == null || member.IsBlocked)
            return MemberAccessDenied();

        var reservation = await Db.Reservations
            .FirstOrDefaultAsync(r => r.ReservationId == id && r.MemberId == member.MemberId);
        if (reservation == null)
            return NotFound();

        if (reservation.Status == ReservationRules.Waiting)
        {
            reservation.Status = ReservationRules.Cancelled;
            await Db.SaveChangesAsync();
            TempData["Success"] = "Reservation cancelled.";
        }

        return RedirectToAction(nameof(Index));
    }
}
