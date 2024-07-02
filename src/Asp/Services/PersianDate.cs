using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Globalization;

namespace Application.Services
{

  public class PersianDate
  {
   

    private readonly DateTime _datetime;
    readonly PersianCalendar pc;
    public PersianDate(DateTime dateTime)
    {
      this._datetime = dateTime;
      pc = new PersianCalendar();
    }

    public PersianDate(string PersianDateString)
    {
      string[] spliteddatestring = PersianDateString.Split('/');

      int year = Convert.ToInt32(spliteddatestring[0]);
      int month = Convert.ToInt32(spliteddatestring[1]);
      int day = Convert.ToInt32(spliteddatestring[2]);


      pc = new PersianCalendar();
      this._datetime = pc.ToDateTime(year, month, day, 0, 0, 0,0);
    }

    public DateTime ToDateTime()
    {
      return this._datetime;
    }

    public int Year
    {
      get
      {
        return pc.GetYear(this._datetime);
      }
    }

    public int Month
    {
      get
      {
        return pc.GetMonth(this._datetime);
      }
    }

    public int Day
    {
      get
      {
        return pc.GetDayOfMonth(this._datetime);
      }
    }

    public string DayOfWeek
    {
      get
      {
        switch ((int)_datetime.DayOfWeek)
        {
          case 6:
            return "شنبه";

          case 0:
            return "یک‌شنبه";

          case 1:
            return "دوشنبه";

          case 2:
            return "سه‌شنبه";
          case 3:
            return "چهارشنبه";

          case 4:
            return "پنج‌شنبه";

          case 5:
            return "جمعه";

          default:
            throw new NotImplementedException();
        }
      }
    }


    public string ToShortDateString()
    {
      return Year.ToString() + "/" + Month.ToString() + "/" + Day.ToString();
    }
  }


  public static class PersonDateExtentionMethodClass
  {
    // Extention method for normal datetimes
    public static PersianDate ToPersianDate(this DateTime dt)
    {
      return new PersianDate(dt);
    }
  }
}
