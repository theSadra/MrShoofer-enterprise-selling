using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Application.Data
{
  public class AppDbContext : IdentityDbContext<IdentityUser>
  {
    public DbSet<Agency> Agencies { get; set; }
    public DbSet<Ticket> Tickets { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {

      string desktop_address = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
      optionsBuilder.UseSqlite($"Data Source = {desktop_address}\\Mrshoofer_org.db;");

    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      base.OnModelCreating(modelBuilder);
    }
  }
}
