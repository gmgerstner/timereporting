import { useState, type FormEvent } from 'react';
import { Button, Card, Col, Container, Form, Row } from 'react-bootstrap';
import { Navigate, useNavigate } from 'react-router-dom';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faSpinner } from '@fortawesome/free-solid-svg-icons';
import { StorageKeys, hasValidSession } from '../api/storage';
import { useAuth } from '../auth/useAuth';

export function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();

  const [remember, setRemember] = useState(
    () => localStorage.getItem(StorageKeys.rememberLogin) === 'true',
  );
  const [username, setUsername] = useState(() =>
    localStorage.getItem(StorageKeys.rememberLogin) === 'true'
      ? (localStorage.getItem(StorageKeys.username) ?? '')
      : '',
  );
  const [password, setPassword] = useState(() =>
    localStorage.getItem(StorageKeys.rememberLogin) === 'true'
      ? (localStorage.getItem(StorageKeys.password) ?? '')
      : '',
  );
  const [loggingIn, setLoggingIn] = useState(false);
  const [error, setError] = useState('');

  if (hasValidSession()) {
    return <Navigate to="/" replace />;
  }

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setLoggingIn(true);
    setError('');

    try {
      await login({ username, password });

      localStorage.setItem(StorageKeys.rememberLogin, remember ? 'true' : 'false');
      localStorage.setItem(StorageKeys.password, remember ? password : '');

      navigate('/', { replace: true });
    } catch {
      setError('Invalid login, or the server could not be reached.');
      setLoggingIn(false);
    }
  };

  return (
    <Container>
      <Row>
        <Col md={6}>
          <Card className="mt-2">
            <Card.Body>
              <Card.Title as="h2">Login</Card.Title>
              <Form onSubmit={onSubmit}>
                <Form.Group className="mb-2" controlId="username">
                  <Form.Label>User Name</Form.Label>
                  <Form.Control
                    type="text"
                    placeholder="User Name"
                    autoComplete="username"
                    value={username}
                    onChange={(event) => setUsername(event.target.value)}
                  />
                </Form.Group>

                <Form.Group className="mb-2" controlId="password">
                  <Form.Label>Password</Form.Label>
                  <Form.Control
                    type="password"
                    placeholder="Password"
                    autoComplete="current-password"
                    value={password}
                    onChange={(event) => setPassword(event.target.value)}
                  />
                </Form.Group>

                <Form.Check
                  type="checkbox"
                  id="rememberLogin"
                  className="mb-2"
                  label="Remember Login"
                  checked={remember}
                  onChange={(event) => setRemember(event.target.checked)}
                />

                {error && <div className="alert alert-danger">{error}</div>}

                <Button type="submit" disabled={loggingIn}>
                  {loggingIn && <FontAwesomeIcon icon={faSpinner} spin className="me-1" />}
                  Login
                </Button>
              </Form>
            </Card.Body>
          </Card>
        </Col>
      </Row>
    </Container>
  );
}
