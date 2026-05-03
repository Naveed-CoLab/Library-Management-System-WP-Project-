using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Models;

namespace System.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<LibrarySettings> LibrarySettings => Set<LibrarySettings>();
    public DbSet<Publisher> Publishers => Set<Publisher>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Author> Authors => Set<Author>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<BookItem> BookItems => Set<BookItem>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<BorrowRecord> BorrowRecords => Set<BorrowRecord>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<Fine> Fines => Set<Fine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(e =>
        {
            e.HasOne(u => u.Member)
                .WithMany()
                .HasForeignKey(u => u.MemberId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<LibrarySettings>().HasData(new LibrarySettings
        {
            LibrarySettingsId = 1,
            DefaultLoanDays = LoanRules.DefaultLoanDays,
            MaxConcurrentLoansPerMember = LoanRules.MaxConcurrentLoansPerMember,
            DailyFineAmount = 1.50m
        });

        modelBuilder.Entity<Member>(e =>
        {
            e.HasIndex(m => m.Email).IsUnique();
        });

        modelBuilder.Entity<Book>(e =>
        {
            e.HasOne(b => b.Author)
                .WithMany(a => a.Books)
                .HasForeignKey(b => b.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(b => b.Publisher)
                .WithMany(p => p.Books)
                .HasForeignKey(b => b.PublisherId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(b => b.Category)
                .WithMany(c => c.Books)
                .HasForeignKey(b => b.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(b => b.ISBN).IsUnique();
        });

        modelBuilder.Entity<BookItem>(e =>
        {
            e.HasIndex(i => i.Barcode).IsUnique();
            e.HasIndex(i => new { i.BookId, i.BranchId });

            e.HasOne(i => i.Book)
                .WithMany(b => b.Items)
                .HasForeignKey(i => i.BookId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(i => i.Branch)
                .WithMany(br => br.Items)
                .HasForeignKey(i => i.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BorrowRecord>(e =>
        {
            e.HasOne(br => br.Member)
                .WithMany(m => m.BorrowRecords)
                .HasForeignKey(br => br.MemberId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(br => br.BookItem)
                .WithMany(i => i.BorrowRecords)
                .HasForeignKey(br => br.BookItemId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(br => br.MemberId);
            e.HasIndex(br => br.BookItemId);
        });

        modelBuilder.Entity<Fine>(e =>
        {
            e.HasOne(f => f.BorrowRecord)
                .WithMany(br => br.Fines)
                .HasForeignKey(f => f.BorrowRecordId)
                .OnDelete(DeleteBehavior.Cascade);

            e.Property(f => f.Amount).HasPrecision(10, 2);
        });

        modelBuilder.Entity<Reservation>(e =>
        {
            e.HasOne(r => r.Member)
                .WithMany(m => m.Reservations)
                .HasForeignKey(r => r.MemberId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(r => r.Book)
                .WithMany(b => b.Reservations)
                .HasForeignKey(r => r.BookId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(r => new { r.BookId, r.Status });
            e.HasIndex(r => r.MemberId);
        });

        SeedReferenceData(modelBuilder);
    }

    private static void SeedReferenceData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Publisher>().HasData(
            new Publisher { PublisherId = 1, Name = "Secker & Warburg", Country = "United Kingdom" },
            new Publisher { PublisherId = 2, Name = "Chilton Books", Country = "United States" });

        modelBuilder.Entity<Category>().HasData(
            new Category { CategoryId = 1, Name = "Dystopian", Description = "Society under authoritarian control." },
            new Category { CategoryId = 2, Name = "Satire", Description = "Political or social critique." },
            new Category { CategoryId = 3, Name = "Science Fiction", Description = "Speculative futures and worlds." },
            new Category { CategoryId = 4, Name = "Classic Literature", Description = "Enduring literary fiction." });

        modelBuilder.Entity<Branch>().HasData(
            new Branch { BranchId = 1, Name = "Main Campus Library", AddressLine = "1 University Road", Phone = "0300-0000000" },
            new Branch { BranchId = 2, Name = "Downtown Reading Room", AddressLine = "22 Market Street", Phone = "0300-0000001" });

        modelBuilder.Entity<Author>().HasData(
            new Author { AuthorId = 1, FullName = "George Orwell", Nationality = "British", DateOfBirth = new DateOnly(1903, 6, 25) },
            new Author { AuthorId = 2, FullName = "Frank Herbert", Nationality = "American", DateOfBirth = new DateOnly(1920, 10, 8) },
            new Author { AuthorId = 3, FullName = "Fyodor Dostoevsky", Nationality = "Russian", DateOfBirth = new DateOnly(1821, 11, 11) });

        modelBuilder.Entity<Book>().HasData(
            new Book { BookId = 1, Title = "1984", ISBN = "978-0451524935", PublishedYear = 1949, AuthorId = 1, PublisherId = 1, CategoryId = 1, Summary = "A dystopian social science fiction novel and cautionary tale." },
            new Book { BookId = 2, Title = "Animal Farm", ISBN = "978-0451526342", PublishedYear = 1945, AuthorId = 1, PublisherId = 1, CategoryId = 2, Summary = "Allegorical novella using farm animals to critique totalitarianism." },
            new Book { BookId = 3, Title = "Dune", ISBN = "978-0441013593", PublishedYear = 1965, AuthorId = 2, PublisherId = 2, CategoryId = 3, Summary = "Epic science fiction saga set on the desert planet Arrakis." },
            new Book { BookId = 4, Title = "Crime and Punishment", ISBN = "978-0143107637", PublishedYear = 1866, AuthorId = 3, PublisherId = 1, CategoryId = 4, Summary = "Psychological drama exploring morality and redemption." });

        modelBuilder.Entity<BookItem>().HasData(
            new BookItem { BookItemId = 1, BookId = 1, BranchId = 1, Barcode = "LIB-B001-01", ShelfLocation = "A-12" },
            new BookItem { BookItemId = 2, BookId = 1, BranchId = 1, Barcode = "LIB-B001-02", ShelfLocation = "A-12" },
            new BookItem { BookItemId = 3, BookId = 1, BranchId = 1, Barcode = "LIB-B001-03", ShelfLocation = "A-12" },
            new BookItem { BookItemId = 4, BookId = 1, BranchId = 1, Barcode = "LIB-B001-04", ShelfLocation = "A-12" },
            new BookItem { BookItemId = 5, BookId = 2, BranchId = 1, Barcode = "LIB-B002-01", ShelfLocation = "A-13" },
            new BookItem { BookItemId = 6, BookId = 2, BranchId = 1, Barcode = "LIB-B002-02", ShelfLocation = "A-13" },
            new BookItem { BookItemId = 7, BookId = 2, BranchId = 1, Barcode = "LIB-B002-03", ShelfLocation = "A-13" },
            new BookItem { BookItemId = 8, BookId = 3, BranchId = 1, Barcode = "LIB-B003-01", ShelfLocation = "B-04" },
            new BookItem { BookItemId = 9, BookId = 3, BranchId = 1, Barcode = "LIB-B003-02", ShelfLocation = "B-04" },
            new BookItem { BookItemId = 10, BookId = 4, BranchId = 1, Barcode = "LIB-B004-01", ShelfLocation = "C-01" },
            new BookItem { BookItemId = 11, BookId = 4, BranchId = 1, Barcode = "LIB-B004-02", ShelfLocation = "C-01" });

        modelBuilder.Entity<Member>().HasData(
            new Member { MemberId = 1, FullName = "Zara Ahmed", Email = "zara@lib.com", Phone = "0301-1234567", MemberSince = new DateOnly(2023, 1, 15) },
            new Member { MemberId = 2, FullName = "Bilal Malik", Email = "bilal@lib.com", Phone = "0312-9876543", MemberSince = new DateOnly(2023, 6, 1) });

        modelBuilder.Entity<BorrowRecord>().HasData(
            new BorrowRecord { BorrowRecordId = 1, MemberId = 1, BookItemId = 1, BorrowDate = new DateOnly(2024, 1, 10), DueDate = new DateOnly(2024, 1, 24), ReturnDate = new DateOnly(2024, 1, 24), Status = LoanRules.StatusReturned },
            new BorrowRecord { BorrowRecordId = 2, MemberId = 1, BookItemId = 8, BorrowDate = new DateOnly(2024, 3, 5), DueDate = new DateOnly(2024, 3, 19), ReturnDate = null, Status = LoanRules.StatusBorrowed },
            new BorrowRecord { BorrowRecordId = 3, MemberId = 2, BookItemId = 5, BorrowDate = new DateOnly(2024, 2, 20), DueDate = new DateOnly(2024, 3, 5), ReturnDate = new DateOnly(2024, 3, 5), Status = LoanRules.StatusReturned });

        modelBuilder.Entity<Reservation>().HasData(
            new Reservation
            {
                ReservationId = 1,
                MemberId = 2,
                BookId = 4,
                ReservedAt = new DateTime(2024, 5, 1, 12, 0, 0, DateTimeKind.Utc),
                ExpiresOn = new DateOnly(2026, 12, 31),
                Status = ReservationRules.Waiting
            });
    }
}
