using IomEvents.Domain;
using Microsoft.EntityFrameworkCore;

namespace IomEvents.Infrastructure;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Event> Events { get; set; }
}