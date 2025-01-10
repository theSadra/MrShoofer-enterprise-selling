using IPE.SmsIrClient;
using IPE.SmsIrClient.Models.Requests;
using IPE.SmsIrClient.Models.Results;
using Kavenegar;
using Kavenegar.Core.Models.Enums;

namespace Application.Services
{
  public class CustomerServiceSmsSender
  {
    private readonly SmsIr smsIr;

    public CustomerServiceSmsSender(IConfiguration configuration)
    {
      this.smsIr = new SmsIr(configuration["smsirapikey"]);
    }


    public async Task SendCustomerTicket_issued(string firstname, string lastname, string reference, string link, string numberphone)
    {
      int templateId = 200000;
      VerifySendParameter[] verifySendParameters = {
           new VerifySendParameter("FIRSTNAME",firstname),
           new VerifySendParameter("LASTNAME", lastname),
           new VerifySendParameter("TRIP", link),
           new VerifySendParameter("REFERENCE", reference),

        };

      var response = await smsIr.VerifySendAsync(numberphone, 782252, verifySendParameters);

    }
  }
}
