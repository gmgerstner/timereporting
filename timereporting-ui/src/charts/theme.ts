/**
 * Chart colours and shared mark specs.
 *
 * Every chart in the app plots a single series, so this is a one-hue job: the same
 * blue for every bar and every line. Shading bars darker-where-bigger would encode
 * length twice and say nothing the bar's own length does not.
 *
 * Light values only — the app has no dark mode. These are validated against the white
 * Bootstrap card the charts sit on.
 */
export const chartTheme = {
  /** The one series colour. */
  series: '#2a78d6',
  /** Hairline grid, one step off the surface. */
  grid: '#e1e0d9',
  /** Axis rules and the baseline. */
  axis: '#c3c2b7',
  /** Axis tick labels. */
  tick: '#898781',
  /** Body text inside tooltips and labels. */
  ink: '#52514e',
  /** The card behind the plot. Bars and dots use it as a separating gap, never a border. */
  surface: '#ffffff',
} as const;

/** Bars stay thin — the leftover band is deliberate air, not wasted space. */
export const BAR_SIZE = 20;

/** Rounded at the data end, square against the baseline. */
export const BAR_RADIUS: [number, number, number, number] = [0, 4, 4, 0];

/** Shared axis styling. Recharts defaults to a heavier axis than this. */
export const axisProps = {
  tick: { fill: chartTheme.tick, fontSize: 12 },
  tickLine: false,
  axisLine: { stroke: chartTheme.axis },
} as const;

/** Hours to one decimal place, trailing ".0" dropped. */
export function formatHours(hours: number): string {
  return `${Number(hours.toFixed(1))}`;
}
