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
