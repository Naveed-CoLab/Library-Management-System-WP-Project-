using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Models;
using System.Notifications;

namespace System.Controllers;

[Area("Admin")]
[Authorize(Roles = IdentitySeeder.AdminRole)]
public class MembersController : Controller
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public MembersController(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index() =>
        View(await _context.Members.ToListAsync());

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();
        var member = await _context.Members.Include(m => m.BorrowRecords).ThenInclude(br => br.BookItem).ThenInclude(i => i.Book).ThenInclude(b => b.Author).FirstOrDefaultAsync(m => m.MemberId == id);
        if (member == null) return NotFound();
        return View(member);
    }

    public IActionResult Create() => View(new AdminMemberCreateViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminMemberCreateViewModel model)
    {
        var member = new Member
        {
            FullName = model.FullName,
            Email = model.Email,
            Phone = model.Phone,
            MemberSince = model.MemberSince
        };
        NormalizeMember(member);
        await ValidateMemberAsync(member);
        await ValidateIdentityEmailAsync(member.Email);

        if (ModelState.IsValid)
        {
            await using var tx = await _context.Database.BeginTransactionAsync();
            _context.Add(member);
            await _context.SaveChangesAsync();

            var identityUser = new ApplicationUser
            {
                UserName = member.Email,
                Email = member.Email,
                EmailConfirmed = true,
                FullName = member.FullName,
                PhoneNumber = member.Phone,
                MemberId = member.MemberId
            };

            var createResult = await _userManager.CreateAsync(identityUser, model.Password);
            if (!createResult.Succeeded)
            {
                foreach (var error in createResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);

                await tx.RollbackAsync();
                return View(model);
            }

            var roleResult = await _userManager.AddToRoleAsync(identityUser, IdentitySeeder.UserRole);
            if (!roleResult.Succeeded)
            {
                foreach (var error in roleResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);

                await tx.RollbackAsync();
                return View(model);
            }

            await tx.CommitAsync();
            TempData.NotifySuccess("Member registered with user login credentials.");
            return RedirectToAction(nameof(Index));
        }
        return View(model);
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
        NormalizeMember(member);
        await ValidateMemberAsync(member, id);

        if (ModelState.IsValid)
        {
            try
            {
                var existing = await _context.Members.FindAsync(id);
                if (existing == null) return NotFound();
                existing.FullName = member.FullName;
                existing.Email = member.Email;
                existing.Phone = member.Phone;
                existing.MemberSince = member.MemberSince;
                await _context.SaveChangesAsync();
                await SyncLinkedUserAsync(existing);
                TempData.NotifySuccess("Member profile and login identity updated.");
            }
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

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Block(int id)
    {
        await SetBlockedAsync(id, blocked: true);
        TempData["Success"] = "Member account blocked.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Unblock(int id)
    {
        await SetBlockedAsync(id, blocked: false);
        TempData["Success"] = "Member account unblocked.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task SetBlockedAsync(int id, bool blocked)
    {
        var member = await _context.Members.FindAsync(id);
        if (member == null)
            return;

        member.IsBlocked = blocked;
        var users = await _userManager.Users.Where(u => u.MemberId == id).ToListAsync();
        foreach (var user in users)
        {
            user.LockoutEnabled = blocked;
            user.LockoutEnd = blocked ? DateTimeOffset.MaxValue : null;
            await _userManager.UpdateAsync(user);
        }

        await _context.SaveChangesAsync();
    }

    private async Task SyncLinkedUserAsync(Member member)
    {
        var linkedUser = await _userManager.Users.FirstOrDefaultAsync(u => u.MemberId == member.MemberId);
        if (linkedUser == null)
            return;

        linkedUser.UserName = member.Email;
        linkedUser.Email = member.Email;
        linkedUser.FullName = member.FullName;
        linkedUser.PhoneNumber = member.Phone;
        await _userManager.UpdateAsync(linkedUser);
    }

    private static void NormalizeMember(Member member)
    {
        member.FullName = member.FullName.Trim();
        member.Email = member.Email.Trim().ToLowerInvariant();
        member.Phone = string.IsNullOrWhiteSpace(member.Phone) ? null : member.Phone.Trim();
    }

    private async Task ValidateMemberAsync(Member member, int? currentMemberId = null)
    {
        if (member.MemberSince > DateOnly.FromDateTime(DateTime.Today))
            ModelState.AddModelError(nameof(Member.MemberSince), "Member since cannot be in the future.");

        var duplicateEmail = await _context.Members.AnyAsync(m =>
            m.Email == member.Email
            && (!currentMemberId.HasValue || m.MemberId != currentMemberId.Value));
        if (duplicateEmail)
            ModelState.AddModelError(nameof(Member.Email), "Another member already uses this email address.");
    }

    private async Task ValidateIdentityEmailAsync(string email)
    {
        var duplicateUser = await _userManager.FindByEmailAsync(email);
        if (duplicateUser != null)
            ModelState.AddModelError(nameof(Member.Email), "This email is already used by a login account.");
    }
}
