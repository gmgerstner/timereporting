namespace GMG.TimeReporting.Core.PasswordArchiveData
{
    public partial class PasswordCategory
    {
        public int PasswordId { get; set; }
        public int CategoryId { get; set; }

        public virtual Category Category { get; set; } = null!;
        public virtual Password Password { get; set; } = null!;
    }
}
