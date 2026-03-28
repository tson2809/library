using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;
using Server.Contracts.OData;
using Server.Models;

namespace Server.Controllers;

[Route("odata/books")]
public sealed class BooksODataController : ODataController
{
    private readonly LibraryContext _db;

    public BooksODataController(LibraryContext db)
    {
        _db = db;
    }

    [HttpGet]
    [EnableQuery(PageSize = 100, MaxTop = 200)]
    public IQueryable<BookODataDto> Get()
    {
        return _db.Books
            .AsNoTracking()
            .Where(b => b.IsActive)
            .Select(b => new BookODataDto
            {
                BookId = b.BookId,
                Isbn = b.Isbn,
                Title = b.Title,
                Publisher = b.Publisher != null ? b.Publisher.PublisherName : null,
                PublishedYear = b.PublishedYear,
                TotalCopies = b.BookCopies.Count,
                AvailableCopies = b.BookCopies.Count(c => c.CopyStatus == "Available"),
                BorrowCount = b.BookCopies.SelectMany(c => c.LoanItems).Count(),
                CanDeactivate = !b.BookCopies
                    .SelectMany(c => c.LoanItems)
                    .Any(li => li.Loan.Status == "Borrowing" || li.Loan.Status == "Overdue")
            });
    }
}
