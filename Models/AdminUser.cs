using Microsoft.AspNetCore.Identity;
using Org.BouncyCastle.Utilities.IO.Pem;
using System.Diagnostics.CodeAnalysis;

namespace Application.Models
{
  public class AdminUser
  {
    public int Id { get; set; }
    public string Name { get; set; }
    // IdentityUser navigation prop
    [NotNull]
    public IdentityUser Identity { get; set; }
  }
}
