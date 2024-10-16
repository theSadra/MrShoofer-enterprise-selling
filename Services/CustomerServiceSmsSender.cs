using Kavenegar;
using Kavenegar.Core.Models.Enums;

namespace Application.Services
{
  public class CustomerServiceSmsSender
  {
    private readonly KavenegarApi kavenegarApi;

    public CustomerServiceSmsSender(KavenegarApi kavenegarApi)
    {
      this.kavenegarApi = kavenegarApi;
    }



    public async Task SendCustomerTicket_issued(string firstname, string lastname, string reference, string link, string numberphone)
    {

      char spacechar = '\u0020';

      var result = await kavenegarApi.VerifyLookup(
        receptor: numberphone,
        token: firstname,
        token2: lastname,
        token3: link,
        token10: reference,
        "Mrshoofer-org-customer-ticket-issued",
        VerifyLookupType.Sms
        );
    }

  }
}
