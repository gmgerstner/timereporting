# Time Reporting User Interface

The Time Reporting User Interface is for recording time spent on individual tasks for use with any
timesheet system. This code is the client side (UI) logic.

This project is built with [React](https://react.dev/) 19, [TypeScript](https://www.typescriptlang.org/)
and [Vite](https://vite.dev/). It talks to the Time Reporting API
(see https://github.com/gmgerstner/timereporting-api).

> Previously an Angular 14 application. The conversion to React kept the same screens, the same API
> contract and the same `localStorage` keys, so existing sessions and deployments carry over.

## Requirements

- Node.js 20.19+ or 22.12+ (Vite 8 requirement)

## Development server

```cmd
npm install
npm run dev
```

Navigate to `http://localhost:4200/`. The app reloads automatically when you change a source file.

The dev server reads `VITE_API_URL` from `.env.development`, which points at the API running from
Visual Studio / `dotnet run` on `https://localhost:44352`. To point somewhere else without editing a
tracked file, create `.env.local` and set `VITE_API_URL` there.

## Configuration

Settings that used to live in `src/environments/environment.ts` are now Vite environment variables:

| Variable                     | Purpose                                                  |
| ---------------------------- | -------------------------------------------------------- |
| `VITE_API_URL`               | Base URL of the Time Reporting API                        |
| `VITE_TIMESHEET_PORTAL_URL`  | Link target for the timesheet portal on the home page     |
| `VITE_TIMESHEET_PORTAL_NAME` | Link text for that portal                                 |

They are resolved per mode from `.env`, `.env.development` and `.env.production`, and are read
through `src/config.ts`. Values are inlined at build time, so a production build must be rebuilt
after changing `.env.production`.

## Build

```cmd
npm run build
```

Build artifacts are written to `dist/TimeReporting`, the same folder the Angular build used, so the
existing copy/deploy scripts still work. `public/web.config` (the IIS SPA-fallback rewrite rules) is
copied into the output automatically.

Other scripts:

| Script            | Purpose                                             |
| ----------------- | --------------------------------------------------- |
| `npm run dev`     | Start the dev server                                 |
| `npm run build`   | Type-check and produce a production build            |
| `npm run preview` | Serve the production build locally                   |
| `npm run lint`    | Run ESLint                                           |
| `npm run copy`    | Copy `dist/TimeReporting` to the server share        |
| `npm run deploy`  | Build, then copy                                     |

## Project layout

```
src/
  api/          fetch-based API client and localStorage helpers
  auth/         auth context, provider and the RequireAuth route guard
  components/   NavBar, Footer and the shared title / quarter-hour pickers
  dialogs/      edit and delete modals
  models/       API response types
  pages/        HomePage and LoginPage
  utils/        quarter-hour time helpers
```

## Deploying to Server

```cmd
npm run deploy
```

## Initial Installation on Server

- Create a folder for the UI code.
- In IIS right-click Sites and select Add WebSite
- Enter the following:
  - Site Name
  - Physical path to the folder created above
  - Binding and Host name information as desired
- Make sure the Application Pool's .NET CLR Version is set to No Managed Code
- Install the URL Rewrite module for IIS, which `public/web.config` depends on for SPA routing.
- Open the package.json file and update the folder paths for copy and deploy to the physical path
  for the UI code.
- Open `.env.production` and adjust `VITE_API_URL` to point to the correct api path.
- In a command prompt (or powershell) run:
  ```
  npm install
  npm run deploy
  ```
- Install the API (see https://github.com/gmgerstner/timereporting-api)
