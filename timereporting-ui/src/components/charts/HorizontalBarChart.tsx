import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  LabelList,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { BAR_RADIUS, BAR_SIZE, axisProps, chartTheme, formatHours } from '../../charts/theme';

export interface BarDatum {
  label: string;
  hours: number;
}

/** Long task titles are common, so the category axis gets room and still truncates. */
const AXIS_WIDTH = 160;
const MAX_LABEL = 24;

function truncate(label: string): string {
  return label.length > MAX_LABEL ? `${label.slice(0, MAX_LABEL - 1)}…` : label;
}

function BarTooltip({ active, payload }: { active?: boolean; payload?: { payload: BarDatum }[] }) {
  if (!active || !payload?.length) {
    return null;
  }

  const datum = payload[0].payload;
  return (
    <div
      className="bg-white border rounded shadow-sm px-2 py-1 small"
      style={{ color: chartTheme.ink, maxWidth: 320 }}
    >
      {/* The axis truncates; the tooltip is where the full title is readable. */}
      <div>{datum.label}</div>
      <strong>{formatHours(datum.hours)} h</strong>
    </div>
  );
}

/**
 * Magnitude by category — task titles, or people. One series, one colour: these
 * categories have no natural order, so shading by value would only repeat the
 * bar's own length.
 */
export function HorizontalBarChart({ data }: { data: BarDatum[] }) {
  // Grow with the rows rather than scrolling inside a fixed box, and leave room for
  // the value axis underneath.
  const height = Math.max(200, data.length * 32 + 48);

  return (
    <ResponsiveContainer width="100%" height={height}>
      <BarChart data={data} layout="vertical" margin={{ top: 4, right: 48, bottom: 0, left: 0 }}>
        <CartesianGrid stroke={chartTheme.grid} strokeDasharray="0" horizontal={false} />
        <XAxis type="number" allowDecimals={false} {...axisProps} />
        <YAxis
          type="category"
          dataKey="label"
          width={AXIS_WIDTH}
          tickFormatter={truncate}
          {...axisProps}
        />
        <Tooltip content={<BarTooltip />} cursor={{ fill: chartTheme.grid, fillOpacity: 0.4 }} />
        <Bar dataKey="hours" barSize={BAR_SIZE} radius={BAR_RADIUS} fill={chartTheme.series}>
          {/* A 2px ring in the surface colour separates touching bars. Never a border. */}
          {data.map((datum) => (
            <Cell key={datum.label} stroke={chartTheme.surface} strokeWidth={2} />
          ))}
          <LabelList
            dataKey="hours"
            position="right"
            formatter={(value) => formatHours(Number(value ?? 0))}
            style={{ fill: chartTheme.ink, fontSize: 12 }}
          />
        </Bar>
      </BarChart>
    </ResponsiveContainer>
  );
}
