using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Models;

namespace System.Data;

public static class IdentitySeeder
{
    public const string AdminRole = "Admin";
    public const string UserRole = "User";

    private const string AdminEmail = "naveed@lumina.com";
    private const string LegacyAdminEmail = "admin@lumina.local";
    private const string AdminPassword = "12345678";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (var role in new[] { AdminRole, UserRole })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var admin = await userManager.FindByEmailAsync(AdminEmail);
        if (admin == null)
        {
            admin = new ApplicationUser
            {
                UserName = AdminEmail,
                Email = AdminEmail,
                EmailConfirmed = true,
                FullName = "Naveed (Admin)"
            };
            await userManager.CreateAsync(admin, AdminPassword);
        }
        await EnsurePasswordAsync(userManager, admin, AdminPassword);
        if (!await userManager.IsInRoleAsync(admin, AdminRole))
            await userManager.AddToRoleAsync(admin, AdminRole);
        if (!await userManager.IsInRoleAsync(admin, UserRole))
            await userManager.AddToRoleAsync(admin, UserRole);

        var legacyAdmin = await userManager.FindByEmailAsync(LegacyAdminEmail);
        if (legacyAdmin != null)
        {
            if (await userManager.IsInRoleAsync(legacyAdmin, AdminRole))
                await userManager.RemoveFromRoleAsync(legacyAdmin, AdminRole);
            await userManager.DeleteAsync(legacyAdmin);
        }

        var admins = await userManager.GetUsersInRoleAsync(AdminRole);
        foreach (var adminUser in admins.Where(x => !string.Equals(x.Email, AdminEmail, StringComparison.OrdinalIgnoreCase)))
        {
            await userManager.RemoveFromRoleAsync(adminUser, AdminRole);
        }

        var demoUser = await userManager.FindByEmailAsync("user@lumina.local");
        if (demoUser != null)
            await userManager.DeleteAsync(demoUser);

        var localUsers = userManager.Users
            .Where(u => u.Email != null
                        && EF.Functions.Like(u.Email, "%@lumina.local")
                        && u.Email != AdminEmail)
            .ToList();
        foreach (var user in localUsers)
            await userManager.DeleteAsync(user);
    }

    private static async Task EnsurePasswordAsync(UserManager<ApplicationUser> userManager, ApplicationUser user, string password)
    {
        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, resetToken, password);
        if (!result.Succeeded)
        {
            var remove = await userManager.RemovePasswordAsync(user);
            if (remove.Succeeded)
                await userManager.AddPasswordAsync(user, password);
        }
    }
}
