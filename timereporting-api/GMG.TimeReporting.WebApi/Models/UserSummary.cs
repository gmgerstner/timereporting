namespace GMG.TimeReporting.WebApi.Models
{
    /// <summary>
    /// One entry in the admin's user picker. Deliberately carries nothing sensitive —
    /// no credentials live in the Time Reporting database at all.
    /// </summary>
    public class UserSummary
    {
        public int UserId { get; set; }

        public string Username { get; set; } = string.Empty;

        public bool IsAdmin { get; set; }
    }
}
