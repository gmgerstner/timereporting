using System.Text.Json.Serialization;

namespace GMG.TimeReporting.Core.TimeReportingData
{
    /// <summary>
    /// A task title a user has pinned as a favourite. Each user keeps their own list.
    /// </summary>
    public partial class CommonTask
    {
        public int CommonTaskId { get; set; }
        public int UserId { get; set; }
        public string Title { get; set; } = null!;

        /// <summary>
        /// Navigation only. The API serves this entity straight out of its read
        /// endpoints, and the owner is already implied by the request's token.
        /// </summary>
        [JsonIgnore]
        public virtual User User { get; set; } = null!;
    }
}
