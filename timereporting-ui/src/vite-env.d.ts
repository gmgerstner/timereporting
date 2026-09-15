/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_URL: string;
  readonly VITE_TIMESHEET_PORTAL_URL: string;
  readonly VITE_TIMESHEET_PORTAL_NAME: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
