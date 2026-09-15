namespace GMG.TimeReporting.WebApi.Models
{
    public class User
    {
        public string Username { get; set; } = string.Empty;
        public DateTime Expires { get; set; }
        public string Token { get; set; } = string.Empty;
    }
}
