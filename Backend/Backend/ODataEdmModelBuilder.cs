using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;
using Server.Contracts.OData;

namespace Server;

public static class ODataEdmModelBuilder
{
    public static IEdmModel GetEdmModel()
    {
        var builder = new ODataConventionModelBuilder();
        var books = builder.EntitySet<BookODataDto>("books");
        books.EntityType.HasKey(book => book.BookId);
        return builder.GetEdmModel();
    }
}
