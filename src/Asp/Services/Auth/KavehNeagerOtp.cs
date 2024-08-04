using Kavenegar;
using System.Security.Cryptography;
using System.Text;


namespace Application.Services.Auth
{
  public class KavehNeagerOtp : IOtpLogin
  {


    public async Task<string> SendCode(string numberphone)
    {
        Kavenegar.KavenegarApi api = new Kavenegar.KavenegarApi("6D66344943355774613850422F6D795441455972666D705A70796D3759773869414D6462786D4C703371513D");

        string otpcode = GenerateOtp();

        var result = api.VerifyLookup(numberphone, otpcode, "Mrshoofer-org", Kavenegar.Core.Models.Enums.VerifyLookupType.Sms);

        return otpcode;
    }



    public static string GenerateOtp(int length = 5)
    {
      if (length <= 0)
      {
        throw new ArgumentOutOfRangeException(nameof(length), "Length must be a positive integer.");
      }

      using (var random = RandomNumberGenerator.Create())
      {
        byte[] randomBytes = new byte[length];
        random.GetBytes(randomBytes);

        // Convert each byte to a digit (0-9)
        StringBuilder otp = new StringBuilder();
        for (int i = 0; i < length; i++)
        {
          otp.Append((randomBytes[i] & 0xF) % 10); // Mask out higher bits and modulo 10 for 0-9
        }

        return otp.ToString();
      }
    }
  }
}
