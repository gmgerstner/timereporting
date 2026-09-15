namespace GMG.TimeReporting.Core.TimeReportingData
{
    /// <summary>
    /// A person who uses the Time Reporting app.
    /// </summary>
    /// <remarks>
    /// Credentials are not stored here — logging in is still checked against the
    /// PasswordArchive database owned by the security application. This table only maps
    /// an archive username onto a local <see cref="UserId"/> that the rest of the schema
    /// can key off, so time entries survive a password row being deleted and re-added.
    /// Rows are created on first successful login.
    /// </remarks>
    public partial class User
    {
        public int UserId { get; set; }

        /// <summary>
        /// The archive username, lower-cased. PostgreSQL compares text case-sensitively,
        /// so normalising on the way in is what stops "GMG" and "gmg" becoming two people.
        /// </summary>
        public string Username { get; set; } = null!;

        /// <summary>
        /// Admins may read any user's schedule and timesheet. They get no extra write
        /// access: nobody can clock in, edit or delete on someone else's behalf.
        /// </summary>
        public bool IsAdmin { get; set; }

        public DateTime CreatedDate { get; set; }

        public virtual ICollection<CommonTask> CommonTasks { get; set; } = new HashSet<CommonTask>();
        public virtual ICollection<TimeEntry> TimeEntries { get; set; } = new HashSet<TimeEntry>();
    }
}
