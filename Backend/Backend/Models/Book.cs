using System;
using System.Collections.Generic;

namespace Server.Models;

public partial class Book
{
    public int BookId { get; set; }

    public string Isbn { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string? ImageUrl { get; set; }

    public int? PublisherId { get; set; }

    public int? PublishedYear { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<BookCopy> BookCopies { get; set; } = new List<BookCopy>();

    public virtual ICollection<BookReservation> BookReservations { get; set; } = new List<BookReservation>();

    public virtual Publisher? Publisher { get; set; }

    public virtual ICollection<Author> Authors { get; set; } = new List<Author>();

    public virtual ICollection<Category> Categories { get; set; } = new List<Category>();
}
