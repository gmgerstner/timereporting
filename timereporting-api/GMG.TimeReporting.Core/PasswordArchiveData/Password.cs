namespace GMG.TimeReporting.Core.PasswordArchiveData
{
    public partial class Password
    {
        public int PasswordId { get; set; }
        public int SystemUserId { get; set; }
        public string Title { get; set; } = null!;
        public string PasswordValue { get; set; } = null!;
        public string? Url { get; set; }
        public string? Username { get; set; }
        public string? Notes { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastModifiedDate { get; set; }

        public virtual SystemUser SystemUser { get; set; } = null!;
        public virtual ICollection<PasswordCategory> PasswordCategories { get; set; } = new HashSet<PasswordCategory>();
    }
}
