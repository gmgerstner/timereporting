import { Button, Container, Nav, Navbar } from 'react-bootstrap';
import { Link, NavLink, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/useAuth';

export function NavBar() {
  const { username, isAuthenticated, isAdmin, logout } = useAuth();
  const navigate = useNavigate();

  const onLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <Navbar expand="lg" bg="dark" variant="dark" data-bs-theme="dark">
      <Container fluid>
        <Navbar.Brand as={Link} to="/">
          Time Reporting
        </Navbar.Brand>
        <Navbar.Toggle aria-controls="main-navbar" />
        <Navbar.Collapse id="main-navbar">
          <Nav className="me-auto mb-2 mb-lg-0">
            {isAuthenticated && (
              <>
                <Nav.Link as={NavLink} to="/" end>
                  Dashboard
                </Nav.Link>
                <Nav.Link as={NavLink} to="/timesheet">
                  Timesheet
                </Nav.Link>
                {isAdmin && (
                  <Nav.Link as={NavLink} to="/reports">
                    Reports
                  </Nav.Link>
                )}
              </>
            )}
          </Nav>
          <div className="d-flex align-items-center gap-2">
            <span className="navbar-text">{username.toUpperCase()}</span>
            {isAuthenticated && (
              <Button variant="outline-success" onClick={onLogout}>
                Logout
              </Button>
            )}
          </div>
        </Navbar.Collapse>
      </Container>
    </Navbar>
  );
}
