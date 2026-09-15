using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GMG.TimeReporting.Core
{
    /// <summary>
    /// Strips the <see cref="DateTimeKind"/> off values on their way to and from the
    /// database, keeping the wall-clock value untouched.
    /// </summary>
    /// <remarks>
    /// Both databases store naive local timestamps (SQL Server <c>datetime</c>, PostgreSQL
    /// <c>timestamp without time zone</c>) and the application works in local time
    /// throughout: the UI posts date-times with no zone designator and the API compares
    /// them against <see cref="DateTime.Today"/> and <see cref="DateTime.Now"/>.
    /// <para>
    /// SQL Server ignored the Kind and wrote whatever wall-clock value it was given.
    /// Npgsql does not: writing a <see cref="DateTimeKind.Utc"/> value to a
    /// <c>timestamp without time zone</c> column throws. Normalising to
    /// <see cref="DateTimeKind.Unspecified"/> keeps the old behaviour, so a client that
    /// sends a 'Z'-suffixed timestamp still round-trips instead of failing.
    /// </para>
    /// </remarks>
    public class UnspecifiedKindConverter : ValueConverter<DateTime, DateTime>
    {
        public UnspecifiedKindConverter()
            : base(
                value => DateTime.SpecifyKind(value, DateTimeKind.Unspecified),
                value => DateTime.SpecifyKind(value, DateTimeKind.Unspecified))
        {
        }
    }
}
