/**
 * Time helpers shared by the home page and the edit dialog.
 *
 * The app works in whole quarter hours: every time it displays or submits is
 * snapped to :00, :15, :30 or :45 in the browser's local time zone.
 */

export type Meridiem = 'AM' | 'PM';

/** A time of day as shown by the three dropdowns (hour / minute / AM-PM). */
export interface ClockParts {
  hour: string;
  minute: string;
  tt: Meridiem;
}

export const HOUR_OPTIONS = ['01', '02', '03', '04', '05', '06', '07', '08', '09', '10', '11', '12'];
export const MINUTE_OPTIONS = ['00', '15', '30', '45'];
export const MERIDIEM_OPTIONS: Meridiem[] = ['AM', 'PM'];

const QUARTER_HOUR_MS = 15 * 60 * 1000;

const pad = (value: number): string => value.toString().padStart(2, '0');

/**
 * Snaps an instant to the nearest quarter hour.
 *
 * Rounding on the epoch is safe here because every real UTC offset is a whole
 * number of quarter hours, so local and UTC quarter boundaries coincide.
 */
export function roundToNearestQuarter(date: Date): Date {
  return new Date(Math.round(date.getTime() / QUARTER_HOUR_MS) * QUARTER_HOUR_MS);
}

/** Converts an instant into the quarter-hour dropdown values that represent it. */
export function toClockParts(date: Date): ClockParts {
  const rounded = roundToNearestQuarter(date);
  const hours24 = rounded.getHours();

  const hour12 = hours24 % 12 === 0 ? 12 : hours24 % 12;

  return {
    hour: pad(hour12),
    minute: pad(rounded.getMinutes()),
    tt: hours24 >= 12 ? 'PM' : 'AM',
  };
}

/** Rebuilds a Date from dropdown values, on the day of `base` (today by default). */
export function fromClockParts(parts: ClockParts, base: Date = new Date()): Date {
  let hours24 = parseInt(parts.hour, 10) % 12;
  if (parts.tt === 'PM') {
    hours24 += 12;
  }

  const date = new Date(base.getTime());
  date.setHours(hours24, parseInt(parts.minute, 10), 0, 0);
  return date;
}

/** Moves the displayed time by whole minutes, wrapping across midnight. */
export function shiftClockParts(parts: ClockParts, deltaMinutes: number): ClockParts {
  const shifted = new Date(fromClockParts(parts).getTime() + deltaMinutes * 60_000);
  return toClockParts(shifted);
}

/** Formats an API date-time string for display, e.g. "09:15 AM". */
export function formatTime(value: string | null | undefined): string {
  if (!value) {
    return '';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return '';
  }

  const { hour, minute, tt } = toClockParts(date);
  return `${hour}:${minute} ${tt}`;
}

/**
 * Formats a Date the way the API expects it: a local date-time with no zone
 * designator, e.g. "2024-05-06T09:15:00.000".
 */
export function toLocalISODateTimeString(date: Date): string {
  return (
    `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}` +
    `T${pad(date.getHours())}:${pad(date.getMinutes())}:00.000`
  );
}

/** Formats a Date as a local "yyyy-MM-dd" string, for `<input type="date">`. */
export function toLocalISODateString(date: Date): string {
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}
