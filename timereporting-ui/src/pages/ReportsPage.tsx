import { useEffect, useState } from 'react';
import { Button, Card, Container, Table } from 'react-bootstrap';
import { useNavigate } from 'react-router-dom';
import { DateTime } from 'luxon';
import { api } from '../api/client';
import { formatHours } from '../charts/theme';
import { DateRangePicker } from '../components/DateRangePicker';
import { lastNDays, type DateRange } from '../utils/dateRange';
import { HeroFigure } from '../components/StatTiles';
import { HorizontalBarChart } from '../components/charts/HorizontalBarChart';
import type { UserHours } from '../models';

export function ReportsPage() {
  const navigate = useNavigate();

  const [range, setRange] = useState<DateRange>(() => lastNDays(30));
  const [rows, setRows] = useState<UserHours[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    let cancelled = false;

    const load = async () => {
      try {
        setError('');
        setLoading(true);
        const next = await api.getHoursByUser(range.from, range.to);
        if (!cancelled) setRows(next);
      } catch {
        if (!cancelled) setError('Could not load the report.');
      } finally {
        if (!cancelled) setLoading(false);
      }
    };

    void load();
    return () => {
      cancelled = true;
    };
  }, [range]);

  const totalHours = rows.reduce((sum, row) => sum + row.totalHours, 0);

  // Someone with no hours in the range is worth seeing in the table, but an empty bar
  // says nothing on the chart.
  const bars = rows
    .filter((row) => row.totalHours > 0)
    .map((row) => ({ label: row.username, hours: row.totalHours }));

  const plotStyle = { opacity: loading && rows.length > 0 ? 0.5 : 1 };

  return (
    <Container>
      {error && <div className="alert alert-danger mt-3">{error}</div>}

      <Card className="mt-3">
        <Card.Body>
          <DateRangePicker value={range} onChange={setRange} />
        </Card.Body>
      </Card>

      <Card className="mt-3">
        <Card.Body>
          <HeroFigure
            label={`Hours across everyone, ${DateTime.fromISO(range.from).toFormat('d LLL')} to ${DateTime.fromISO(range.to).toFormat('d LLL yyyy')}`}
            value={formatHours(totalHours)}
            unit="hours"
          />
        </Card.Body>
      </Card>

      <Card className="mt-3 mb-3">
        <Card.Body>
          <h3 className="h5">Hours per person</h3>
          {bars.length === 0 ? (
            <p className="text-muted">Nobody logged time in this range.</p>
          ) : (
            <div style={plotStyle}>
              <HorizontalBarChart data={bars} />
            </div>
          )}

          <Table striped size="sm" className="mt-3" style={{ fontVariantNumeric: 'tabular-nums' }}>
            <thead>
              <tr>
                <th>Person</th>
                <th>Total hours</th>
                <th>Days logged</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.userId}>
                  <td>{row.username}</td>
                  <td>{formatHours(row.totalHours)}</td>
                  <td>{row.daysLogged}</td>
                  <td className="text-end">
                    <Button
                      size="sm"
                      variant="link"
                      // The dashboard's admin picker already does per-person drill-down.
                      onClick={() => void navigate(`/?userId=${row.userId}`)}
                    >
                      View dashboard
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </Table>
        </Card.Body>
      </Card>
    </Container>
  );
}
