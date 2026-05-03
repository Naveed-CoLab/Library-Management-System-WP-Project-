using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace System.Models;

public class ApplicationUser : IdentityUser
{
    [MaxLength(120)]
    public string? FullName { get; set; }

    public int? MemberId { get; set; }
    public Member? Member { get; set; }
}

public class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = null!;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = null!;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required, MaxLength(100)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = null!;

    [Required, EmailAddress, MaxLength(150)]
    public string Email { get; set; } = null!;

    [Phone, MaxLength(20)]
    public string? Phone { get; set; }

    [Required, DataType(DataType.Password)]
    [MinLength(6)]
    public string Password { get; set; } = null!;

    [Required, DataType(DataType.Password)]
    [Compare(nameof(Password))]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = null!;
}

public class AdminLoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = null!;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = null!;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public class UserLoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = null!;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = null!;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public class UserSignupViewModel
{
    [Required, MaxLength(100)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = null!;

    [Required, EmailAddress, MaxLength(150)]
    public string Email { get; set; } = null!;

    [Phone, MaxLength(20)]
    public string? Phone { get; set; }

    [Required, DataType(DataType.Password), MinLength(8)]
    public string Password { get; set; } = null!;

    [Required, DataType(DataType.Password)]
    [Compare(nameof(Password))]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = null!;
}

public class AdminMemberCreateViewModel
{
    [Required, MaxLength(100), MinLength(2)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = null!;

    [Required, EmailAddress, MaxLength(150)]
    public string Email { get; set; } = null!;

    [Phone, MaxLength(20)]
    public string? Phone { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Member Since")]
    public DateOnly MemberSince { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Required, DataType(DataType.Password), MinLength(6)]
    [Display(Name = "Login Password")]
    public string Password { get; set; } = null!;

    [Required, DataType(DataType.Password)]
    [Compare(nameof(Password))]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = null!;
}

public class LibrarySettings
{
    public int LibrarySettingsId { get; set; } = 1;

    [Range(1, 90)]
    [Display(Name = "Default loan days")]
    public int DefaultLoanDays { get; set; } = LoanRules.DefaultLoanDays;

    [Range(1, 50)]
    [Display(Name = "Max concurrent loans per member")]
    public int MaxConcurrentLoansPerMember { get; set; } = LoanRules.MaxConcurrentLoansPerMember;

    [Range(typeof(decimal), "0", "1000")]
    [Display(Name = "Daily overdue fine")]
    public decimal DailyFineAmount { get; set; } = 1.50m;
}

public class UserDashboardViewModel
{
    public Member Member { get; set; } = null!;
    public int ActiveLoans { get; set; }
    public int OverdueLoans { get; set; }
    public int Reservations { get; set; }
    public decimal OutstandingFinesTotal { get; set; }
    public List<BorrowRecord> CurrentLoans { get; set; } = new();
    public List<Reservation> RecentReservations { get; set; } = new();
    public List<Fine> OpenFines { get; set; } = new();
}

public class ReportsViewModel
{
    public List<BorrowRecord> ActiveLoans { get; set; } = new();
    public List<BorrowRecord> OverdueLoans { get; set; } = new();
    public List<PopularBookReportRow> PopularBooks { get; set; } = new();
    public decimal OutstandingFinesTotal { get; set; }
}

public class PopularBookReportRow
{
    public int BookId { get; set; }
    public string Title { get; set; } = null!;
    public string Author { get; set; } = null!;
    public int BorrowCount { get; set; }
}
