using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Application.Data
{
  public class AppDbContext : IdentityDbContext<IdentityUser>
  {



    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
      optionsBuilder.UseSqlite("Data Source = C:\\Users\\doros\\Desktop\\Mrshoofer_org.db;");

    }
  }
}
