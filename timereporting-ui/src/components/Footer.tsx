import { Container } from 'react-bootstrap';

// Injected by Vite from package.json — see vite.config.ts `define`.
declare const __APP_VERSION__: string;

export function Footer() {
  const year = new Date().getFullYear();

  return (
    <div className="app-footer">
      <Container>
        <div className="py-2">
          © 2020-{year} GMG Digital Technologies. All rights reserved. Time Reporting v
          {__APP_VERSION__}
        </div>
      </Container>
    </div>
  );
}
