export interface LoginCredentials {
  username: string;
  password: string;
}

export interface User {
  username: string;
  /** Local ISO date-time string, as returned by the API. */
  expires: string;
  token: string;
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
