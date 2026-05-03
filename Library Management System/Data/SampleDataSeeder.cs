using Microsoft.EntityFrameworkCore;
using System.Models;

namespace System.Data;

public static class SampleDataSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        await CleanupDemoDataAsync(db);
        await EnsureAuthorsAndBooksAsync(db);
        await EnsureBookCopiesAsync(db);
    }

    private static async Task CleanupDemoDataAsync(AppDbContext db)
    {
        var demoMemberIds = await db.Members
            .Where(m =>
                EF.Functions.Like(m.Email, "%@lumina.local")
                || EF.Functions.Like(m.FullName, "%Demo%"))
            .Select(m => m.MemberId)
            .ToListAsync();

        if (demoMemberIds.Count == 0)
            return;

        var demoBorrowRecordIds = await db.BorrowRecords
            .Where(br => demoMemberIds.Contains(br.MemberId))
            .Select(br => br.BorrowRecordId)
            .ToListAsync();

        if (demoBorrowRecordIds.Count > 0)
        {
            var fines = await db.Fines.Where(f => demoBorrowRecordIds.Contains(f.BorrowRecordId)).ToListAsync();
            db.Fines.RemoveRange(fines);
        }

        var reservations = await db.Reservations.Where(r => demoMemberIds.Contains(r.MemberId)).ToListAsync();
        db.Reservations.RemoveRange(reservations);

        var borrowRecords = await db.BorrowRecords.Where(br => demoMemberIds.Contains(br.MemberId)).ToListAsync();
        db.BorrowRecords.RemoveRange(borrowRecords);

        var usersLinkedToDemoMembers = await db.Users
            .Where(u => u.MemberId != null && demoMemberIds.Contains(u.MemberId.Value))
            .ToListAsync();
        foreach (var user in usersLinkedToDemoMembers)
            user.MemberId = null;

        var members = await db.Members.Where(m => demoMemberIds.Contains(m.MemberId)).ToListAsync();
        db.Members.RemoveRange(members);

        await db.SaveChangesAsync();
    }

    private static async Task EnsureAuthorsAndBooksAsync(AppDbContext db)
    {
        var authorData = new[]
        {
            new Author { FullName = "Paulo Coelho", Nationality = "Brazilian", DateOfBirth = new DateOnly(1947, 8, 24) },
            new Author { FullName = "Yuval Noah Harari", Nationality = "Israeli", DateOfBirth = new DateOnly(1976, 2, 24) },
            new Author { FullName = "James Clear", Nationality = "American", DateOfBirth = new DateOnly(1986, 1, 1) },
            new Author { FullName = "Matt Haig", Nationality = "British", DateOfBirth = new DateOnly(1975, 7, 3) },
            new Author { FullName = "Delia Owens", Nationality = "American", DateOfBirth = new DateOnly(1949, 4, 4) },
            new Author { FullName = "Khaled Hosseini", Nationality = "Afghan-American", DateOfBirth = new DateOnly(1965, 3, 4) }
        };

        foreach (var author in authorData)
        {
            if (!await db.Authors.AnyAsync(a => a.FullName == author.FullName))
                db.Authors.Add(author);
        }
        await db.SaveChangesAsync();

        var fictionCategory = await db.Categories.OrderBy(c => c.CategoryId).FirstAsync();
        var sciFiCategory = await db.Categories.OrderByDescending(c => c.CategoryId).FirstAsync();
        var publisher = await db.Publishers.OrderBy(p => p.PublisherId).FirstAsync();

        async Task<int> AuthorIdAsync(string fullName) =>
            await db.Authors.Where(a => a.FullName == fullName).Select(a => a.AuthorId).FirstAsync();

        var books = new[]
        {
            new Book
            {
                Title = "The Alchemist",
                ISBN = "978-0061122415",
                PublishedYear = 1988,
                Summary = "A shepherd's journey to discover purpose, destiny, and personal legend.",
                AuthorId = await AuthorIdAsync("Paulo Coelho"),
                CategoryId = fictionCategory.CategoryId,
                PublisherId = publisher.PublisherId
            },
            new Book
            {
                Title = "Sapiens",
                ISBN = "978-0062316097",
                PublishedYear = 2011,
                Summary = "A sweeping account of humankind's history and societal evolution.",
                AuthorId = await AuthorIdAsync("Yuval Noah Harari"),
                CategoryId = fictionCategory.CategoryId,
                PublisherId = publisher.PublisherId
            },
            new Book
            {
                Title = "Atomic Habits",
                ISBN = "978-0735211292",
                PublishedYear = 2018,
                Summary = "Practical systems for behavior change through small, compounding habits.",
                AuthorId = await AuthorIdAsync("James Clear"),
                CategoryId = fictionCategory.CategoryId,
                PublisherId = publisher.PublisherId
            },
            new Book
            {
                Title = "The Midnight Library",
                ISBN = "978-0525559474",
                PublishedYear = 2020,
                Summary = "A novel about regret, possibility, and choosing a meaningful life.",
                AuthorId = await AuthorIdAsync("Matt Haig"),
                CategoryId = fictionCategory.CategoryId,
                PublisherId = publisher.PublisherId
            },
            new Book
            {
                Title = "Where the Crawdads Sing",
                ISBN = "978-0735219090",
                PublishedYear = 2018,
                Summary = "A coming-of-age mystery set in the marshes of North Carolina.",
                AuthorId = await AuthorIdAsync("Delia Owens"),
                CategoryId = fictionCategory.CategoryId,
                PublisherId = publisher.PublisherId
            },
            new Book
            {
                Title = "A Thousand Splendid Suns",
                ISBN = "978-1594489501",
                PublishedYear = 2007,
                Summary = "A story of resilience, friendship, and survival in Afghanistan.",
                AuthorId = await AuthorIdAsync("Khaled Hosseini"),
                CategoryId = sciFiCategory.CategoryId,
                PublisherId = publisher.PublisherId
            }
        };

        foreach (var book in books)
        {
            if (!await db.Books.AnyAsync(b => b.ISBN == book.ISBN))
                db.Books.Add(book);
        }
        await db.SaveChangesAsync();
    }

    private static async Task EnsureBookCopiesAsync(AppDbContext db)
    {
        var branches = await db.Branches.OrderBy(b => b.BranchId).ToListAsync();
        if (branches.Count == 0)
            return;

        var books = await db.Books.OrderBy(b => b.BookId).ToListAsync();
        foreach (var book in books)
        {
            var existingCount = await db.BookItems.CountAsync(i => i.BookId == book.BookId);
            var targetCount = existingCount < 2 ? 3 : existingCount < 4 ? 4 : existingCount;

            for (var i = existingCount + 1; i <= targetCount; i++)
            {
                var barcode = $"LIB-{book.BookId:D5}-{i:D2}";
                if (await db.BookItems.AnyAsync(x => x.Barcode == barcode))
                    continue;

                var branch = branches[(i - 1) % branches.Count];
                db.BookItems.Add(new BookItem
                {
                    BookId = book.BookId,
                    BranchId = branch.BranchId,
                    Barcode = barcode,
                    ShelfLocation = $"{(char)('A' + ((book.BookId + i) % 6))}-{(book.BookId + i):D2}"
                });
            }
        }

        await db.SaveChangesAsync();
    }

}
