using System;
using System.Collections.Generic;

namespace Server.Models;

public partial class Author
{
    public int AuthorId { get; set; }

    public string AuthorName { get; set; } = null!;

    public DateOnly? DateOfBirth { get; set; }

    public string? Nationality { get; set; }

    public virtual ICollection<Book> Books { get; set; } = new List<Book>();
}
