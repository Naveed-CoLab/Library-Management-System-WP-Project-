using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace System.Models;

public static class LoanRules
{
    public const int DefaultLoanDays = 14;
    public const int MaxConcurrentLoansPerMember = 5;
    public const string StatusBorrowed = "Borrowed";
    public const string StatusReturned = "Returned";
}

public static class ReservationRules
{
    public const string Waiting = "Waiting";
    public const string Ready = "Ready";
    public const string Fulfilled = "Fulfilled";
    public const string Cancelled = "Cancelled";
}

public class DashboardViewModel
{
    public int TotalBooks { get; set; }
    public int TotalCopies { get; set; }
    public int TotalAuthors { get; set; }
    public int TotalMembers { get; set; }
    public int ActiveLoans { get; set; }
    public int OverdueLoans { get; set; }
    public int AvailableCopiesNow { get; set; }
    public int PendingReservations { get; set; }
    public decimal OutstandingFinesTotal { get; set; }
    public List<BorrowRecord> RecentActivity { get; set; } = new();
    public List<BorrowRecord> OverdueItems { get; set; } = new();
}

public class Publisher
{
    public int PublisherId { get; set; }
    [Required, MaxLength(160)]
    public string Name { get; set; } = null!;
    [MaxLength(80)]
    public string? Country { get; set; }
    [MaxLength(256)]
    public string? Website { get; set; }
    public ICollection<Book> Books { get; set; } = new List<Book>();
}

public class Category
{
    public int CategoryId { get; set; }
    [Required, MaxLength(100)]
    public string Name { get; set; } = null!;
    [MaxLength(300)]
    public string? Description { get; set; }
    public ICollection<Book> Books { get; set; } = new List<Book>();
}

public class Branch
{
    public int BranchId { get; set; }
    [Required, MaxLength(120)]
    public string Name { get; set; } = null!;
    [MaxLength(200)]
    public string? AddressLine { get; set; }
    [MaxLength(40)]
    public string? Phone { get; set; }
    public ICollection<BookItem> Items { get; set; } = new List<BookItem>();
}

public class Author
{
    public int AuthorId { get; set; }
    [Required, MaxLength(100)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = null!;
    [MaxLength(100)]
    public string? Nationality { get; set; }
    [DataType(DataType.Date)]
    [Display(Name = "Date of Birth")]
    public DateOnly? DateOfBirth { get; set; }
    public ICollection<Book> Books { get; set; } = new List<Book>();
}

public class Book
{
    public int BookId { get; set; }
    [Required, MaxLength(200)]
    public string Title { get; set; } = null!;
    [MaxLength(20)]
    [RegularExpression(@"^[0-9Xx-]{10,20}$", ErrorMessage = "ISBN may only contain digits, X, and hyphens.")]
    public string? ISBN { get; set; }
    [Required, Range(1450, 2100)]
    [Display(Name = "Published Year")]
    public int PublishedYear { get; set; }
    [MaxLength(800)]
    public string? Summary { get; set; }

    [Required]
    [Display(Name = "Author")]
    public int AuthorId { get; set; }
    public Author Author { get; set; } = null!;

    [Display(Name = "Publisher")]
    public int? PublisherId { get; set; }
    public Publisher? Publisher { get; set; }

    [Required]
    [Display(Name = "Category")]
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public ICollection<BookItem> Items { get; set; } = new List<BookItem>();
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}

/// <summary>Physical copy / barcode tracked unit.</summary>
public class BookItem
{
    public int BookItemId { get; set; }

    [Required]
    public int BookId { get; set; }
    public Book Book { get; set; } = null!;

    [Required]
    [Display(Name = "Branch")]
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;

    [Required, MaxLength(48)]
    public string Barcode { get; set; } = null!;
    [MaxLength(80)]
    [Display(Name = "Shelf / location")]
    public string? ShelfLocation { get; set; }
    [MaxLength(200)]
    public string? ConditionNotes { get; set; }

    public ICollection<BorrowRecord> BorrowRecords { get; set; } = new List<BorrowRecord>();
}

public class Member
{
    public int MemberId { get; set; }
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
    public bool IsBlocked { get; set; }
    public ICollection<BorrowRecord> BorrowRecords { get; set; } = new List<BorrowRecord>();
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}

public class BorrowRecord
{
    public int BorrowRecordId { get; set; }

    [Required]
    [Display(Name = "Member")]
    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;

    [Required]
    [Display(Name = "Copy (barcode)")]
    public int BookItemId { get; set; }
    public BookItem BookItem { get; set; } = null!;

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Borrow Date")]
    public DateOnly BorrowDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    [DataType(DataType.Date)]
    [Display(Name = "Return Date")]
    public DateOnly? ReturnDate { get; set; }
    [DataType(DataType.Date)]
    [Display(Name = "Due date")]
    public DateOnly? DueDate { get; set; }
    [Required, MaxLength(50)]
    public string Status { get; set; } = LoanRules.StatusBorrowed;

    public ICollection<Fine> Fines { get; set; } = new List<Fine>();
}

public class Reservation
{
    public int ReservationId { get; set; }

    [Required]
    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;

    [Required]
    public int BookId { get; set; }
    public Book Book { get; set; } = null!;

    public DateTime ReservedAt { get; set; } = DateTime.UtcNow;
    [DataType(DataType.Date)]
    public DateOnly? ExpiresOn { get; set; }
    [Required, MaxLength(30)]
    public string Status { get; set; } = ReservationRules.Waiting;
}

public class Fine
{
    public int FineId { get; set; }

    [Required]
    public int BorrowRecordId { get; set; }
    public BorrowRecord BorrowRecord { get; set; } = null!;

    [Range(typeof(decimal), "0.01", "999999.99")]
    [Column(TypeName = "decimal(10,2)")]
    public decimal Amount { get; set; }

    [Required, MaxLength(300)]
    public string Reason { get; set; } = "Overdue";

    public DateOnly IssuedOn { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public DateOnly? PaidOn { get; set; }
    [MaxLength(400)]
    public string? Notes { get; set; }
}

public class ErrorViewModel
{
    public string? RequestId { get; set; }
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
