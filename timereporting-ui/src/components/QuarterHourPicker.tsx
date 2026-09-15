import { Button, Form } from 'react-bootstrap';
import {
  HOUR_OPTIONS,
  MERIDIEM_OPTIONS,
  MINUTE_OPTIONS,
  shiftClockParts,
  toClockParts,
  type ClockParts,
  type Meridiem,
} from '../utils/time';

interface QuarterHourPickerProps {
  idPrefix: string;
  value: ClockParts;
  onChange: (value: ClockParts) => void;
  disabled?: boolean;
}

/**
 * The hour / minute / AM-PM dropdown trio plus its Now, +15 and -15 buttons.
 * The Angular app repeated this markup three times; here it is one component.
 */
export function QuarterHourPicker({
  idPrefix,
  value,
  onChange,
  disabled = false,
}: QuarterHourPickerProps) {
  return (
    <div className="d-flex flex-wrap align-items-center gap-1">
      <Form.Select
        id={`${idPrefix}_hh`}
        className="time-dropdown"
        aria-label="Hour"
        value={value.hour}
        disabled={disabled}
        onChange={(event) => onChange({ ...value, hour: event.target.value })}
      >
        {HOUR_OPTIONS.map((hour) => (
          <option key={hour} value={hour}>
            {hour}
          </option>
        ))}
      </Form.Select>

      <Form.Select
        id={`${idPrefix}_mm`}
        className="time-dropdown"
        aria-label="Minute"
        value={value.minute}
        disabled={disabled}
        onChange={(event) => onChange({ ...value, minute: event.target.value })}
      >
        {MINUTE_OPTIONS.map((minute) => (
          <option key={minute} value={minute}>
            {minute}
          </option>
        ))}
      </Form.Select>

      <Form.Select
        id={`${idPrefix}_tt`}
        className="time-dropdown me-2"
        aria-label="AM or PM"
        value={value.tt}
        disabled={disabled}
        onChange={(event) => onChange({ ...value, tt: event.target.value as Meridiem })}
      >
        {MERIDIEM_OPTIONS.map((tt) => (
          <option key={tt} value={tt}>
            {tt}
          </option>
        ))}
      </Form.Select>

      <Button disabled={disabled} onClick={() => onChange(toClockParts(new Date()))}>
        Now
      </Button>
      <Button disabled={disabled} onClick={() => onChange(shiftClockParts(value, 15))}>
        +15
      </Button>
      <Button disabled={disabled} onClick={() => onChange(shiftClockParts(value, -15))}>
        -15
      </Button>
    </div>
  );
}
