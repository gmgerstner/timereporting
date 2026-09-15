namespace GMG.TimeReporting.Core.PasswordArchiveData
{
    public partial class Category
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = null!;

        public virtual ICollection<PasswordCategory> PasswordCategories { get; set; } = new HashSet<PasswordCategory>();
    }
}
