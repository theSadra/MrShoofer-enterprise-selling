namespace Application.Services.Auth
{
  public interface IOtpLogin
  {
     Task<string> SendCode(string numberphone);
  }
}
