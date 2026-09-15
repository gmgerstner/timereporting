import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import { AuthProvider } from './auth/AuthProvider';
import { App } from './App';

import 'bootstrap/dist/css/bootstrap.min.css';
import './index.css';

const container = document.getElementById('root');
if (container === null) {
  throw new Error('Root container #root was not found in index.html.');
}

createRoot(container).render(
  <StrictMode>
    <BrowserRouter>
      <AuthProvider>
        <App />
      </AuthProvider>
    </BrowserRouter>
  </StrictMode>,
);
