namespace GMG.TimeReporting.WebApi.Models
{
    /// <summary>
    /// Everything the dashboard plots for one user over one date range.
    /// </summary>
    /// <remarks>
    /// The two breakdowns are served together because they come from a single scan of the
    /// same rows and are always shown against the same date range.
    /// <para>
    /// Hours here are always a number. A still-running entry contributes nothing rather than
    /// the null that <see cref="Core.TimeReportingData.TimeReportingContext.TimeSheetEntry"/>
    /// carries: the distinction matters on a timesheet, but a null cannot be plotted.
    /// </para>
    /// </remarks>
    public class RangeSummary
    {
        public decimal TotalHours { get; set; }

        /// <summary>Days in the range with at least one entry.</summary>
        public int DaysLogged { get; set; }

        /// <summary>
        /// Every day in the range, including days with no hours, so that a gap in the trend
        /// is drawn as a gap rather than closed over.
        /// </summary>
        public List<DailyTotal> DailyTotals { get; set; } = [];

        /// <summary>Task titles over the whole range, highest hours first.</summary>
        public List<TitleTotal> TitleTotals { get; set; } = [];
    }

    public class DailyTotal
    {
        public DateTime Date { get; set; }

        public decimal TotalHours { get; set; }
    }

    public class TitleTotal
    {
        public string Title { get; set; } = string.Empty;

        public decimal TotalHours { get; set; }
    }
}
