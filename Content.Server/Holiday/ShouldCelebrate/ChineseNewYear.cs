using System;
using System.Globalization;
using Content.Server.Holiday.Interfaces;

namespace Content.Server.Holiday.ShouldCelebrate
{
    public class ChineseNewYear : IHolidayShouldCelebrate
    {
        public bool ShouldCelebrate(DateTime date, HolidayPrototype holiday)
        {
            var chinese = new ChineseLunisolarCalendar();
            var gregorian = new GregorianCalendar();

            var chineseNewYear = chinese.ToDateTime(date.Year, 1, 1, 0, 0, 0, 0);

            return date.Day == chineseNewYear.Day && date.Month == chineseNewYear.Month;
        }
    }
}
