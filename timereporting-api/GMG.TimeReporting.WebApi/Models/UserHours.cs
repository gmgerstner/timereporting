namespace GMG.TimeReporting.WebApi.Models
{
    /// <summary>
    /// One person's total over a date range, for the admin report.
    /// </summary>
    public class UserHours
    {
        public int UserId { get; set; }

        public string Username { get; set; } = string.Empty;

        public decimal TotalHours { get; set; }

        /// <summary>Days in the range on which this person logged at least one entry.</summary>
        public int DaysLogged { get; set; }
    }
}
