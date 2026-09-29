using document_service.Documents;
using Microsoft.EntityFrameworkCore;

namespace document_service.Database;

public class DocumentDbContext : DbContext
{
    public DocumentDbContext(DbContextOptions<DocumentDbContext> options) : base(options)
    {
    }
    
    public DbSet<Document> Documents { get; set; }
    
}