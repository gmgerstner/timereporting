import {
  Area,
  AreaChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { DateTime } from 'luxon';
import { axisProps, chartTheme, formatHours } from '../../charts/theme';
import type { DailyTotal } from '../../models';

/** Plot height plus the x-axis band, so the card never grows a nested scrollbar. */
const HEIGHT = 280;

function shortDate(value: string): string {
  return DateTime.fromISO(value).toFormat('d LLL');
}

function TrendTooltip({ active, payload }: { active?: boolean; payload?: { payload: DailyTotal }[] }) {
  if (!active || !payload?.length) {
    return null;
  }

  const point = payload[0].payload;
  return (
    <div className="bg-white border rounded shadow-sm px-2 py-1 small" style={{ color: chartTheme.ink }}>
      <div>{DateTime.fromISO(point.date).toFormat('cccc d LLLL')}</div>
      <strong>{formatHours(point.totalHours)} h</strong>
    </div>
  );
}

/**
 * Hours logged per day. One series, so there is no legend — the card heading names it.
 */
export function HoursTrendChart({ data }: { data: DailyTotal[] }) {
  // A month of dots is noise; the crosshair tooltip carries the per-day values, and the
  // table view below the chart carries them for anyone not using a pointer.
  return (
    <ResponsiveContainer width="100%" height={HEIGHT}>
      <AreaChart data={data} margin={{ top: 8, right: 16, bottom: 0, left: 0 }}>
        <CartesianGrid stroke={chartTheme.grid} strokeDasharray="0" vertical={false} />
        <XAxis
          dataKey="date"
          tickFormatter={shortDate}
          minTickGap={24}
          {...axisProps}
        />
        <YAxis width={40} allowDecimals={false} {...axisProps} />
        <Tooltip
          content={<TrendTooltip />}
          cursor={{ stroke: chartTheme.axis, strokeWidth: 1 }}
        />
        <Area
          type="monotone"
          dataKey="totalHours"
          stroke={chartTheme.series}
          strokeWidth={2}
          strokeLinecap="round"
          strokeLinejoin="round"
          fill={chartTheme.series}
          fillOpacity={0.1}
          dot={false}
          activeDot={{ r: 4, strokeWidth: 2, stroke: chartTheme.surface }}
        />
      </AreaChart>
    </ResponsiveContainer>
  );
}
