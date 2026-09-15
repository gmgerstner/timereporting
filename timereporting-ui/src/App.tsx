import { Route, Routes } from 'react-router-dom';
import { NavBar } from './components/NavBar';
import { Footer } from './components/Footer';
import { RequireAuth } from './auth/RequireAuth';
import { HomePage } from './pages/HomePage';
import { LoginPage } from './pages/LoginPage';

export function App() {
  return (
    <>
      <NavBar />
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route
          path="/"
          element={
            <RequireAuth>
              <HomePage />
            </RequireAuth>
          }
        />
      </Routes>
      <Footer />
    </>
  );
}
