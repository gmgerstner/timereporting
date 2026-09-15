namespace GMG.TimeReporting.WebApi.Models
{
    public class User
    {
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// True when this user may read other users' schedules and timesheets. The UI uses
        /// it to decide whether to offer the user picker; the API enforces it from the token.
        /// </summary>
        public bool IsAdmin { get; set; }

        public string Token { get; set; } = string.Empty;

        public DateTime Expires { get; set; }
    }
}
