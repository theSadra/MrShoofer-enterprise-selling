using System.Text;

namespace Application.Utilities
{
    public static class PersianRialWords
    {
        private static readonly string[] Ones =
        {
            "", "یک", "دو", "سه", "چهار", "پنج", "شش", "هفت", "هشت", "نه"
        };
        private static readonly string[] Tens =
        {
            "", "ده", "بیست", "سی", "چهل", "پنجاه", "شصت", "هفتاد", "هشتاد", "نود"
        };
        private static readonly string[] Teens =
        {
            "ده", "یازده", "دوازده", "سیزده", "چهارده", "پانزده", "شانزده", "هفده", "هجده", "نوزده"
        };
        private static readonly string[] Hundreds =
        {
            "", "صد", "دویست", "سیصد", "چهارصد", "پانصد", "ششصد", "هفتصد", "هشتصد", "نهصد"
        };
        private static readonly string[] Scales = { "", "هزار", "میلیون", "میلیارد", "تریلیون" };

        public static string ToRialWords(long amount)
        {
            if (amount < 0) amount = 0;
            if (amount == 0) return "صفر ریال";
            return Convert(amount) + " ریال";
        }

        private static string Convert(long n)
        {
            if (n == 0) return "صفر";
            var parts = new List<string>();
            var scale = 0;
            while (n > 0)
            {
                var chunk = (int)(n % 1000);
                if (chunk > 0)
                {
                    var words = ThreeDigits(chunk);
                    if (!string.IsNullOrEmpty(Scales[scale]))
                        words += " " + Scales[scale];
                    parts.Insert(0, words);
                }
                n /= 1000;
                scale++;
            }
            return string.Join(" و ", parts);
        }

        private static string ThreeDigits(int n)
        {
            var sb = new StringBuilder();
            var h = n / 100;
            var rem = n % 100;
            if (h > 0)
            {
                sb.Append(Hundreds[h]);
                if (rem > 0) sb.Append(" و ");
            }
            if (rem >= 10 && rem <= 19)
            {
                sb.Append(Teens[rem - 10]);
            }
            else
            {
                var t = rem / 10;
                var o = rem % 10;
                if (t > 0)
                {
                    sb.Append(Tens[t]);
                    if (o > 0) sb.Append(" و ");
                }
                if (o > 0) sb.Append(Ones[o]);
            }
            return sb.ToString();
        }
    }
}
