import { DateTime } from 'luxon';
import { toLocalISODateString } from './time';

/** An inclusive span of work days, as the reporting endpoints take it. */
export interface DateRange {
  /** Local "yyyy-MM-dd". */
  from: string;
  /** Local "yyyy-MM-dd", inclusive. */
  to: string;
}

/** The last `days` days ending today. 30 matches the API's default. */
export function lastNDays(days: number): DateRange {
  const today = new Date();
  return {
    from: DateTime.fromJSDate(today)
      .minus({ days: days - 1 })
      .toISODate()!,
    to: toLocalISODateString(today),
  };
}

export function monthToDate(): DateRange {
  const now = DateTime.now();
  return { from: now.startOf('month').toISODate()!, to: now.toISODate()! };
}
