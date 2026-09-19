import { Button, Col, Form, Row } from 'react-bootstrap';
import { lastNDays, monthToDate, type DateRange } from '../utils/dateRange';

const presets: { label: string; range: () => DateRange }[] = [
  { label: 'Last 7 days', range: () => lastNDays(7) },
  { label: 'Last 30 days', range: () => lastNDays(30) },
  { label: 'Last 90 days', range: () => lastNDays(90) },
  { label: 'Month to date', range: monthToDate },
];

/**
 * The one filter row for a page. Every chart below it re-renders against the same
 * slice, which is why this sits above the cards rather than inside any of them.
 */
export function DateRangePicker({
  value,
  onChange,
}: {
  value: DateRange;
  onChange: (range: DateRange) => void;
}) {
  const matches = (range: DateRange) => range.from === value.from && range.to === value.to;

  return (
    <Row className="g-2 align-items-end">
      <Col xs="auto">
        <Form.Label htmlFor="range-from" className="mb-1 small text-muted">
          From
        </Form.Label>
        <Form.Control
          id="range-from"
          type="date"
          value={value.from}
          max={value.to}
          onChange={(event) => onChange({ ...value, from: event.target.value })}
        />
      </Col>
      <Col xs="auto">
        <Form.Label htmlFor="range-to" className="mb-1 small text-muted">
          To
        </Form.Label>
        <Form.Control
          id="range-to"
          type="date"
          value={value.to}
          min={value.from}
          onChange={(event) => onChange({ ...value, to: event.target.value })}
        />
      </Col>
      <Col xs={12} md="auto">
        <div className="d-flex flex-wrap gap-2">
          {presets.map((preset) => {
            const range = preset.range();
            return (
              <Button
                key={preset.label}
                size="sm"
                variant={matches(range) ? 'primary' : 'outline-secondary'}
                onClick={() => onChange(range)}
              >
                {preset.label}
              </Button>
            );
          })}
        </div>
      </Col>
    </Row>
  );
}
