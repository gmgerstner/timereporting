import { useEffect, useState } from 'react';
import { Accordion, Card, Col, Container, Form, Row, Table } from 'react-bootstrap';
import { useSearchParams } from 'react-router-dom';
import { DateTime } from 'luxon';
import { api } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { formatHours } from '../charts/theme';
import { DateRangePicker } from '../components/DateRangePicker';
import { lastNDays, type DateRange } from '../utils/dateRange';
import { HeroFigure, StatTiles } from '../components/StatTiles';
import { HoursTrendChart } from '../components/charts/HoursTrendChart';
import { HorizontalBarChart, type BarDatum } from '../components/charts/HorizontalBarChart';
import type { RangeSummary, UserSummary } from '../models';

/** Past this many bars the chart stops being readable, so the tail becomes one bar. */
const TOP_TITLES = 10;

function toBars(summary: RangeSummary): BarDatum[] {
  const ranked = summary.titleTotals.map((total) => ({
    label: total.title,
    hours: total.totalHours,
  }));

  if (ranked.length <= TOP_TITLES) {
    return ranked;
  }

  const head = ranked.slice(0, TOP_TITLES);
  const tail = ranked.slice(TOP_TITLES);
  return [
    ...head,
    { label: `Other (${tail.length})`, hours: tail.reduce((sum, bar) => sum + bar.hours, 0) },
  ];
}

export function DashboardPage() {
  const { username, isAdmin } = useAuth();
  const [searchParams, setSearchParams] = useSearchParams();

  const [range, setRange] = useState<DateRange>(() => lastNDays(30));
  const [summary, setSummary] = useState<RangeSummary | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  /** Other users an admin can look at. Empty for everyone else. */
  const [users, setUsers] = useState<UserSummary[]>([]);
  const userIdParam = searchParams.get('userId');
  const viewedUserId = userIdParam === null ? null : Number(userIdParam);
  const isViewingOther = viewedUserId !== null;

  useEffect(() => {
    let cancelled = false;

    const load = async () => {
      try {
        setError('');
        setLoading(true);
        const next = await api.getRangeSummary(range.from, range.to, viewedUserId);
        if (!cancelled) setSummary(next);
      } catch {
        if (!cancelled) setError('Could not load the dashboard.');
      } finally {
        if (!cancelled) setLoading(false);
      }
    };

    void load();
    return () => {
      cancelled = true;
    };
  }, [range, viewedUserId]);

  // The user picker is an admin-only affordance, and the endpoint behind it is
  // admin-only too, so there is nothing to fetch for anyone else.
  useEffect(() => {
    if (!isAdmin) return;

    let cancelled = false;
    const loadUsers = async () => {
      try {
        const list = await api.getUsers();
        if (!cancelled) setUsers(list);
      } catch {
        if (!cancelled) setError('Could not load the list of users.');
      }
    };

    void loadUsers();
    return () => {
      cancelled = true;
    };
  }, [isAdmin]);

  const changeViewedUser = (value: string) => {
    setSearchParams(value === '' ? {} : { userId: value }, { replace: true });
  };

  const viewedUsername = users.find((user) => user.userId === viewedUserId)?.username ?? '';
  const otherUsers = users.filter((user) => user.username !== username);

  const bars = summary === null ? [] : toBars(summary);
  const averagePerDay =
    summary === null || summary.daysLogged === 0 ? 0 : summary.totalHours / summary.daysLogged;

  // Holding the previous render at reduced opacity avoids a skeleton flash and the
  // layout jump that comes with it.
  const plotStyle = { opacity: loading && summary !== null ? 0.5 : 1 };

  return (
    <Container>
      {error && <div className="alert alert-danger mt-3">{error}</div>}

      {isAdmin && (
        <Card className="mt-3">
          <Card.Body>
            <Row className="g-2 align-items-center">
              <Col xs="auto">
                <Form.Label htmlFor="viewed-user" className="mb-0">
                  Viewing
                </Form.Label>
              </Col>
              <Col xs={12} md>
                <Form.Select
                  id="viewed-user"
                  value={viewedUserId ?? ''}
                  onChange={(event) => changeViewedUser(event.target.value)}
                >
                  <option value="">My own time</option>
                  {otherUsers.map((user) => (
                    <option key={user.userId} value={user.userId}>
                      {user.username}
                    </option>
                  ))}
                </Form.Select>
              </Col>
            </Row>
            {isViewingOther && (
              <div className="text-muted mt-2">
                Read-only view of {viewedUsername || 'another user'}’s time.
              </div>
            )}
          </Card.Body>
        </Card>
      )}

      <Card className="mt-3">
        <Card.Body>
          <DateRangePicker value={range} onChange={setRange} />
        </Card.Body>
      </Card>

      <Card className="mt-3">
        <Card.Body>
          <HeroFigure
            label={`Total hours, ${DateTime.fromISO(range.from).toFormat('d LLL')} to ${DateTime.fromISO(range.to).toFormat('d LLL yyyy')}`}
            value={formatHours(summary?.totalHours ?? 0)}
            unit="hours"
          />
        </Card.Body>
      </Card>

      <div className="mt-3">
        <StatTiles
          stats={[
            { label: 'Days logged', value: String(summary?.daysLogged ?? 0) },
            { label: 'Average per logged day', value: `${formatHours(averagePerDay)} h` },
            { label: 'Distinct tasks', value: String(summary?.titleTotals.length ?? 0) },
          ]}
        />
      </div>

      <Card className="mt-3">
        <Card.Body>
          <h3 className="h5">Hours per day</h3>
          <div style={plotStyle}>
            <HoursTrendChart data={summary?.dailyTotals ?? []} />
          </div>
          <Accordion>
            <Accordion.Item eventKey="days">
              <Accordion.Header>Table view</Accordion.Header>
              <Accordion.Body>
                <Table striped size="sm" style={{ fontVariantNumeric: 'tabular-nums' }}>
                  <thead>
                    <tr>
                      <th>Date</th>
                      <th>Hours</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(summary?.dailyTotals ?? []).map((day) => (
                      <tr key={day.date}>
                        <td>{DateTime.fromISO(day.date).toFormat('ccc d LLL yyyy')}</td>
                        <td>{formatHours(day.totalHours)}</td>
                      </tr>
                    ))}
                  </tbody>
                </Table>
              </Accordion.Body>
            </Accordion.Item>
          </Accordion>
        </Card.Body>
      </Card>

      <Card className="mt-3 mb-3">
        <Card.Body>
          <h3 className="h5">Hours by task</h3>
          {bars.length === 0 ? (
            <p className="text-muted mb-0">No time logged in this range.</p>
          ) : (
            <>
              <div style={plotStyle}>
                <HorizontalBarChart data={bars} />
              </div>
              <Accordion>
                <Accordion.Item eventKey="titles">
                  <Accordion.Header>Table view</Accordion.Header>
                  <Accordion.Body>
                    <Table striped size="sm" style={{ fontVariantNumeric: 'tabular-nums' }}>
                      <thead>
                        <tr>
                          <th>Task</th>
                          <th>Hours</th>
                        </tr>
                      </thead>
                      <tbody>
                        {/* The full list, not the top ten the chart folds. */}
                        {(summary?.titleTotals ?? []).map((total) => (
                          <tr key={total.title}>
                            <td>{total.title}</td>
                            <td>{formatHours(total.totalHours)}</td>
                          </tr>
                        ))}
                      </tbody>
                    </Table>
                  </Accordion.Body>
                </Accordion.Item>
              </Accordion>
            </>
          )}
        </Card.Body>
      </Card>
    </Container>
  );
}
