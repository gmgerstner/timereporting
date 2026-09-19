namespace GMG.TimeReporting.WebApi
{
    /// <summary>
    /// The From/To pair the dashboard and report endpoints take, with the defaults and limits
    /// applied once for both.
    /// </summary>
    /// <remarks>
    /// Both ends are inclusive work days with no time component. The application works in
    /// naive local time throughout, so nothing here converts to UTC.
    /// </remarks>
    public readonly record struct DateRange(DateTime From, DateTime To)
    {
        /// <summary>The range used when the caller supplies neither end.</summary>
        public const int DefaultDays = 30;

        /// <summary>
        /// The longest range these endpoints will scan. A request for more is a mistake
        /// rather than something worth reading every row for.
        /// </summary>
        public const int MaximumDays = 366;

        private static DateTime Naive(DateTime value) =>
            DateTime.SpecifyKind(value, DateTimeKind.Unspecified);

        public static bool TryResolve(
            DateTime? from,
            DateTime? to,
            out DateRange range,
            out string? error)
        {
            // DateTime.Today is Local, and a Local date serialises with the server's UTC
            // offset — which a browser in a different zone reads back as the day before.
            // Every other date this API returns is naive, so these are too.
            var last = Naive((to ?? DateTime.Today).Date);
            var first = Naive((from ?? last.AddDays(-(DefaultDays - 1))).Date);

            if (last < first)
            {
                range = default;
                error = "'To' must not be earlier than 'From'.";
                return false;
            }

            if ((last - first).TotalDays + 1 > MaximumDays)
            {
                range = default;
                error = $"A range may cover at most {MaximumDays} days.";
                return false;
            }

            range = new DateRange(first, last);
            error = null;
            return true;
        }

        /// <summary>
        /// Every day from <see cref="From"/> to <see cref="To"/> inclusive, so that days with
        /// no entries can be reported as zero instead of being missing.
        /// </summary>
        public IEnumerable<DateTime> EnumerateDays()
        {
            for (var day = From; day <= To; day = day.AddDays(1))
            {
                yield return day;
            }
        }
    }
}
