using System;

namespace GMG.TimeReporting.Core
{
    public static class Rules
    {
        public static DateTime RoundToNearestQuarter(this DateTime time)
        {
            var quarters = Math.Round((time - time.Date).TotalHours * 4.0);
            var newtime = time.Date.AddMinutes(quarters * 15);
            return newtime;
        }
    }
}
