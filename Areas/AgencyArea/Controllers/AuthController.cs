using Application.Services.Auth;
using Application.ViewModels.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Policy;
using static System.Net.Mime.MediaTypeNames;

namespace Application.Areas.AgencyArea
{
  [Area("AgencyArea")]
  public class AuthController : Controller
  {

    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserManager<IdentityUser> _usermanager;
    private readonly IOtpLogin _otpLogin;

    public AuthController(SignInManager<IdentityUser> signInManager, IOtpLogin otplogin, UserManager<IdentityUser> usermanager)
    {
      _otpLogin = otplogin;
      _signInManager = signInManager;
      _usermanager = usermanager;
    }


    [HttpGet]
    public IActionResult Login()
    {
      if (_signInManager.IsSignedIn(User))
      {
        // Redirect to the main page or any desired page
        return RedirectToAction("Index", "Home");
      }

      return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel viewmodel, string? ReturnUrl)
    {
      if (ModelState.IsValid)
      {

        // Without password hash logic

        var user = await _usermanager.FindByNameAsync(viewmodel.Username);
        
        var result = await _signInManager.PasswordSignInAsync(user, viewmodel.Password, viewmodel.RemmemberMe, false);
        if (user != null && result.Succeeded)
        {
          // Redirect or take further action
          if (!string.IsNullOrEmpty(ReturnUrl))
            return LocalRedirect(ReturnUrl);

          // Else : redirect to IndexPage
          return RedirectToAction("Index", "Home");

        }
        else
        {
          ViewBag.errormessage = "نام کاربری یا رمز عبور اشتباه است";
          return View(viewmodel);
        }

        //  var result = await _signInManager.PasswordSignInAsync(viewmodel.NumberPhone, viewmodel.Password, viewmodel.RemmemberMe, lockoutOnFailure: false);

        //  if (!_signInManager.UserManager.Users.Any(u => u.UserName == viewmodel.NumberPhone) || result.IsNotAllowed)
        //  {
        //    ViewBag.errormessage = "شماره تلفن یا رمز عبور اشتباه است";
        //    return View(viewmodel);
        //  }

        //  if (result.IsLockedOut)
        //  {
        //    ViewBag.errormessage = "حساب کاربری شما مسدود شده است";
        //    return View(viewmodel);
        //  }

        //  if (result.Succeeded)
        //  {
        //    if (!string.IsNullOrEmpty(ReturnUrl))
        //      return LocalRedirect(ReturnUrl);

        //    // Else : redirect to IndexPage
        //    return RedirectToAction("Index", "Home");

        //  }
        //  else
        //  {

        //    return View(viewmodel);
        //  }
        //}

      }
      return View(viewmodel);
    }

    [HttpGet]
    public async Task<IActionResult> Loginotp()
    {
      return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Loginotp(string numberphone)
    {

      // Check if the user is already authenticated
      if (_signInManager.IsSignedIn(User))
      {
        // Redirect to the main page or any desired page
        return RedirectToAction("Index", "Home");
      }


      if (string.IsNullOrEmpty(numberphone))
      {
        return RedirectToAction("Loginotp");
      }

      var user = await _usermanager.FindByNameAsync(numberphone);

      // User with entered numberphone does not exists
      if (user == null)
      {
        ViewBag.errormessage = "شماره وارد شده در سیستم ثبت نشده است";
        return View();
      }

      // Account is banned
      if (!await _signInManager.CanSignInAsync(user))
      {
        ViewBag.errormessage = "اجازه ورود به این حساب را ندارید";
        return View();
      }

      string otpcode = await _otpLogin.SendCode(numberphone);


      // Storing the code and exp_time in session values
      TempData["otp_code"] = otpcode;
      TempData["otp_exptime"] = DateTime.Now.AddSeconds(90).ToString();





      return RedirectToAction("LoginotpSubmit", new { numberphone });
    }

    [HttpGet]
    public IActionResult LoginotpSubmit(string numberphone)
    {
      if (string.IsNullOrEmpty(numberphone))
      {
        return RedirectToAction("Loginotp");
      }


      ViewBag.numberphone = numberphone;
      return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoginotpSubmit(string code1, string code2, string code3, string code4, string code5, string numberphone)
    {
      if (_signInManager.IsSignedIn(User))
      {
        // Redirect to the main page or any desired page
        return RedirectToAction("Index", "Home");
      }

      string otpcode = code1 + code2 + code3 + code4 + code5;

      // Checking for the data existance
      if (TempData["otp_code"] == null || TempData["otp_exptime"] == null || numberphone is null)
      {
        return RedirectToAction("Loginotp");
      }

      string storedcode = TempData["otp_code"].ToString();
      DateTime exptime = Convert.ToDateTime(TempData["otp_exptime"].ToString());


      var user = await _usermanager.FindByNameAsync(numberphone);

      if (user == null)
      {
        ViewBag.errormessage = "حساب کاربر وجود ندارد";

        ViewBag.numberphone = numberphone;

        return View("LoginotpSubmit");

      }

      var canlogin = await _signInManager.CanSignInAsync(user);



      // If otp expierd
      if (DateTime.Now > exptime)
      {
        TempData.Remove("otp-code");
        TempData.Remove("otp-exptime");
        return RedirectToAction("Loginotp");
      }


      if (otpcode != storedcode)
      {
        ViewBag.errormessage = "کد وارد شده نادرست است";

        ViewBag.numberphone = numberphone;
        TempData.Keep();

        return View("LoginotpSubmit");
      }

      // Logging in the user
      await _signInManager.SignInAsync(user, true, "OTP");
      return RedirectToAction("Index", "Home");

    }

    public async Task<IActionResult> Logout()
    {
      await _signInManager.SignOutAsync();

      return RedirectToAction("Index", "Home");
    }
  }
}
