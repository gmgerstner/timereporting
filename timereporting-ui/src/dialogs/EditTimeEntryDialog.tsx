import { useState } from 'react';
import { Button, Form, Modal } from 'react-bootstrap';
import { api } from '../api/client';
import { QuarterHourPicker } from '../components/QuarterHourPicker';
import { TitleInput } from '../components/TitleInput';
import type { TimeEntry } from '../models';
import {
  fromClockParts,
  toClockParts,
  toLocalISODateTimeString,
  type ClockParts,
} from '../utils/time';

interface EditTimeEntryDialogProps {
  entry: TimeEntry;
  commonTitles: string[];
  recentTitles: string[];
  onClose: () => void;
  onSaved: () => void;
}

export function EditTimeEntryDialog({
  entry,
  commonTitles,
  recentTitles,
  onClose,
  onSaved,
}: EditTimeEntryDialogProps) {
  const startDate = new Date(entry.startTime);

  const [title, setTitle] = useState(entry.title);
  const [start, setStart] = useState<ClockParts>(() => toClockParts(startDate));
  const [hasEnd, setHasEnd] = useState(entry.endTime !== null);
  const [end, setEnd] = useState<ClockParts>(() =>
    toClockParts(entry.endTime !== null ? new Date(entry.endTime) : new Date()),
  );
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  const onSave = async () => {
    setSaving(true);
    setError('');
    try {
      // Times are edited as a time-of-day, so they stay on the entry's own date.
      await api.editTimeEntry({
        ...entry,
        title,
        startTime: toLocalISODateTimeString(fromClockParts(start, startDate)),
        endTime: hasEnd ? toLocalISODateTimeString(fromClockParts(end, startDate)) : null,
      });
      onSaved();
    } catch {
      setError('Could not save the time entry.');
      setSaving(false);
    }
  };

  return (
    <Modal show onHide={onClose} centered>
      <Modal.Header closeButton>
        <Modal.Title>Edit Time Entry</Modal.Title>
      </Modal.Header>
      <Modal.Body>
        {error && <div className="alert alert-danger">{error}</div>}

        <TitleInput
          value={title}
          onChange={setTitle}
          commonTitles={commonTitles}
          recentTitles={recentTitles}
        />

        <Form.Label htmlFor="starttime_hh">Start Time</Form.Label>
        <div className="mb-3">
          <QuarterHourPicker idPrefix="starttime" value={start} onChange={setStart} />
        </div>

        <Form.Check
          type="checkbox"
          id="endTimeCheckbox"
          label="End Time"
          className="mb-1"
          checked={hasEnd}
          onChange={(event) => setHasEnd(event.target.checked)}
        />
        <QuarterHourPicker idPrefix="endtime" value={end} onChange={setEnd} disabled={!hasEnd} />
      </Modal.Body>
      <Modal.Footer>
        <Button variant="outline-dark" onClick={onSave} disabled={saving || title.trim() === ''}>
          Save
        </Button>
        <Button variant="outline-dark" onClick={onClose} disabled={saving}>
          Cancel
        </Button>
      </Modal.Footer>
    </Modal>
  );
}
