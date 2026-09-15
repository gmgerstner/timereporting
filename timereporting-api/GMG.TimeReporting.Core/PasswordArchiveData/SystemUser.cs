namespace GMG.TimeReporting.Core.PasswordArchiveData
{
    public partial class SystemUser
    {
        public int SystemUserId { get; set; }
        public string SystemUsername { get; set; } = null!;
        public string HashedSystemPassword { get; set; } = null!;
        public string? DefaultUsername { get; set; }
        public string? SystemPasswordHint { get; set; }

        public virtual ICollection<Password> Passwords { get; set; } = new HashSet<Password>();
    }
}
