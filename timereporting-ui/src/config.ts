/**
 * Runtime configuration, supplied by Vite at build time from the .env files.
 * Replaces the Angular `environment.ts` / `environment.prod.ts` file-replacement setup.
 */
export const config = {
  apiUrl: import.meta.env.VITE_API_URL,
  timesheetPortalUrl: import.meta.env.VITE_TIMESHEET_PORTAL_URL,
  timesheetPortalName: import.meta.env.VITE_TIMESHEET_PORTAL_NAME,
} as const;
