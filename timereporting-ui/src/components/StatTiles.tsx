import { Card, Col, Row } from 'react-bootstrap';

/**
 * The number a page leads with. Proportional figures, not tabular — equal-width digits
 * make a value like 121 look loose at this size.
 */
export function HeroFigure({ label, value, unit }: { label: string; value: string; unit: string }) {
  return (
    <div>
      <div className="text-muted">{label}</div>
      <div className="d-flex align-items-baseline gap-2">
        <span style={{ fontSize: '3rem', fontWeight: 600, lineHeight: 1.1 }}>{value}</span>
        <span className="text-muted">{unit}</span>
      </div>
    </div>
  );
}

export interface Stat {
  label: string;
  value: string;
}

export function StatTiles({ stats }: { stats: Stat[] }) {
  return (
    <Row className="g-2">
      {stats.map((stat) => (
        <Col xs={6} md key={stat.label}>
          <Card className="h-100">
            <Card.Body className="py-2">
              <div className="text-muted small">{stat.label}</div>
              <div className="fs-4 fw-semibold">{stat.value}</div>
            </Card.Body>
          </Card>
        </Col>
      ))}
    </Row>
  );
}
