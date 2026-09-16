using System.Text.Json.Serialization;

namespace GMG.TimeReporting.Core.TimeReportingData
{
    public partial class TimeEntry
    {
        public int TimeEntryId { get; set; }
        public int UserId { get; set; }
        public string Title { get; set; } = null!;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Navigation only. The API serves this entity straight out of its read
        /// endpoints, and the owner is already implied by the request's token.
        /// </summary>
        [JsonIgnore]
        public virtual User User { get; set; } = null!;
    }
}
