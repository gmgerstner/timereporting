import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

const packageJson = JSON.parse(
  readFileSync(fileURLToPath(new URL('./package.json', import.meta.url)), 'utf-8'),
) as { version: string };

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  define: {
    // Surfaced in the footer, the way the Angular app imported package.json.
    __APP_VERSION__: JSON.stringify(packageJson.version),
  },
  build: {
    // Keep the historical output folder so `npm run copy` / `npm run deploy`
    // continue to publish from dist/TimeReporting.
    outDir: 'dist/TimeReporting',
    emptyOutDir: true,
    sourcemap: true,
  },
  server: {
    port: 4200,
    open: true,
  },
  preview: {
    port: 4200,
  },
});
