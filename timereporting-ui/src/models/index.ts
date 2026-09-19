export interface LoginCredentials {
  username: string;
  password: string;
}

export interface User {
  username: string;
  /** True when this user may view other users' schedules and timesheets. */
  isAdmin: boolean;
  /** Local ISO date-time string, as returned by the API. */
  expires: string;
  token: string;
}

/** One entry in the admin's user picker. */
export interface UserSummary {
  userId: number;
  username: string;
  isAdmin: boolean;
}

export interface TimeEntry {
  timeEntryId: number;
  title: string;
  /** Local ISO date-time string, as returned by the API. */
  startTime: string;
  /** Local ISO date-time string, or null while the clock is still running. */
  endTime: string | null;
}

export interface TimeSheetEntry {
  title: string;
  totalHours: number | null;
}

/** One day's total. Present for every day in the range, including empty ones. */
export interface DailyTotal {
  /** Local ISO date-time string, as returned by the API. */
  date: string;
  totalHours: number;
}

export interface TitleTotal {
  title: string;
  totalHours: number;
}

/**
 * Everything the dashboard plots for one date range. Hours are always numbers here —
 * a still-running entry counts as nothing rather than the null a timesheet carries.
 */
export interface RangeSummary {
  totalHours: number;
  daysLogged: number;
  dailyTotals: DailyTotal[];
  titleTotals: TitleTotal[];
}

/** One person's totals in the admin report. */
export interface UserHours {
  userId: number;
  username: string;
  totalHours: number;
  daysLogged: number;
}
