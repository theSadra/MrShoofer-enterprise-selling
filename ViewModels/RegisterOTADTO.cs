namespace Application.ViewModels
{
    public class RegisterOTADTO
    {
      public string Username { get; set; }
      public string Password { get; set; }
      public string EmailAdress { get; set; }
      public string CompanyName { get; set; }
      public string NumberPhone { get; set; }
      public string? BackupNumberPhone { get; set; }
      public decimal BaseCommission { get; set; }
      public string CompanyAddress { get; set; }
    }
}
