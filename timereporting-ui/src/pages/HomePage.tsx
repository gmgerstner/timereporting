import { useCallback, useEffect, useState } from 'react';
import { Button, Card, Col, Container, Form, Row, Table } from 'react-bootstrap';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faPlay } from '@fortawesome/free-solid-svg-icons';
import { faPenToSquare, faTrashCan } from '@fortawesome/free-regular-svg-icons';
import { DateTime } from 'luxon';
import { api } from '../api/client';
import { config } from '../config';
import { QuarterHourPicker } from '../components/QuarterHourPicker';
import { TitleInput } from '../components/TitleInput';
import { ConfirmDeleteDialog } from '../dialogs/ConfirmDeleteDialog';
import { EditTimeEntryDialog } from '../dialogs/EditTimeEntryDialog';
import type { TimeEntry, TimeSheetEntry } from '../models';
import {
  formatTime,
  fromClockParts,
  roundToNearestQuarter,
  toClockParts,
  toLocalISODateString,
  type ClockParts,
} from '../utils/time';

/**
 * Fills in the implied end time of each entry: a running entry that is followed
 * by another one actually ended when the next entry started.
 */
function withImpliedEndTimes(entries: TimeEntry[]): TimeEntry[] {
  return entries.map((entry, index) => {
    const next = entries[index + 1];
    if (entry.endTime === null && next !== undefined) {
      return { ...entry, endTime: next.startTime };
    }
    return entry;
  });
}

export function HomePage() {
  const [currentTitle, setCurrentTitle] = useState('');
  const [commonTitles, setCommonTitles] = useState<string[]>([]);
  const [recentTitles, setRecentTitles] = useState<string[]>([]);
  const [timeEntries, setTimeEntries] = useState<TimeEntry[]>([]);
  const [timeSheetEntries, setTimeSheetEntries] = useState<TimeSheetEntry[]>([]);
  const [clock, setClock] = useState<ClockParts>(() => toClockParts(new Date()));
  const [scheduleDate, setScheduleDate] = useState(() => toLocalISODateString(new Date()));
  const [error, setError] = useState('');

  const [entryToEdit, setEntryToEdit] = useState<TimeEntry | null>(null);
  const [entryToDelete, setEntryToDelete] = useState<TimeEntry | null>(null);

  const refreshRecentTitles = useCallback(async () => {
    setRecentTitles(await api.getRecentTitles());
  }, []);

  const refreshSchedule = useCallback(async () => {
    setTimeEntries(withImpliedEndTimes(await api.getSchedule()));
  }, []);

  const refreshTimeSheet = useCallback(async (workDate: string) => {
    setTimeSheetEntries(await api.getDailyTimeSheet(workDate));
  }, []);

  /** Reloads everything that a clock change can affect. */
  const refreshAll = useCallback(
    async (workDate: string) => {
      try {
        setError('');
        await Promise.all([refreshRecentTitles(), refreshSchedule(), refreshTimeSheet(workDate)]);
      } catch {
        setError('Could not load data from the server.');
      }
    },
    [refreshRecentTitles, refreshSchedule, refreshTimeSheet],
  );

  useEffect(() => {
    let cancelled = false;

    const load = async () => {
      try {
        setError('');
        const [common, recent, schedule, timesheet] = await Promise.all([
          api.getCommonTitles(),
          api.getRecentTitles(),
          api.getSchedule(),
          api.getDailyTimeSheet(toLocalISODateString(new Date())),
        ]);
        if (cancelled) return;

        setCommonTitles(common);
        setRecentTitles(recent);
        setTimeEntries(withImpliedEndTimes(schedule));
        setTimeSheetEntries(timesheet);
      } catch {
        if (!cancelled) setError('Could not load data from the server.');
      }
    };

    void load();
    return () => {
      cancelled = true;
    };
  }, []);

  const changeScheduleDate = async (value: string) => {
    setScheduleDate(value);
    try {
      setError('');
      await refreshTimeSheet(value);
    } catch {
      setError('Could not load the timesheet.');
    }
  };

  const shiftScheduleDate = (days: number) => {
    const shifted = DateTime.fromISO(scheduleDate).plus({ days }).toISODate();
    if (shifted !== null) {
      void changeScheduleDate(shifted);
    }
  };

  const startClock = async (title: string, time: Date) => {
    try {
      setError('');
      await api.startClock(title, time);
      await refreshAll(scheduleDate);
    } catch {
      setError('Could not start the clock.');
    }
  };

  const onStartClock = () => {
    if (currentTitle.trim() === '') return;
    void startClock(currentTitle, fromClockParts(clock));
  };

  const onStopClock = async () => {
    try {
      setError('');
      await api.stopClock(fromClockParts(clock));
      await refreshAll(scheduleDate);
    } catch {
      setError('Could not stop the clock.');
    }
  };

  const onResume = (title: string) => {
    void startClock(title, roundToNearestQuarter(new Date()));
  };

  const onConfirmDelete = async () => {
    if (entryToDelete === null) return;
    const id = entryToDelete.timeEntryId;
    setEntryToDelete(null);
    try {
      setError('');
      await api.deleteTimeEntry(id);
      await refreshAll(scheduleDate);
    } catch {
      setError('Could not delete the time entry.');
    }
  };

  const totalHours = timeSheetEntries.reduce((sum, entry) => sum + (entry.totalHours ?? 0), 0);

  return (
    <Container>
      {error && <div className="alert alert-danger mt-3">{error}</div>}

      <Row>
        <Col>
          <Card className="mt-3">
            <Card.Body>
              <h3>Time Entry</h3>

              <Form.Label htmlFor="startendtime_hh">Start/End Time</Form.Label>
              <div className="mb-3">
                <QuarterHourPicker idPrefix="startendtime" value={clock} onChange={setClock} />
              </div>

              <TitleInput
                value={currentTitle}
                onChange={setCurrentTitle}
                commonTitles={commonTitles}
                recentTitles={recentTitles}
              />

              <div className="d-flex gap-2">
                <Button onClick={onStartClock} disabled={currentTitle.trim() === ''}>
                  Start Clock
                </Button>
                <Button onClick={() => void onStopClock()} disabled={timeEntries.length === 0}>
                  Stop Clock
                </Button>
              </div>
            </Card.Body>
          </Card>
        </Col>
      </Row>

      <Row>
        <Col>
          <Card className="mt-1">
            <Card.Body>
              <h3>Schedule</h3>
              <Table striped size="sm">
                <thead>
                  <tr>
                    <th />
                    <th>Title</th>
                    <th>Start Time</th>
                    <th>End Time</th>
                  </tr>
                </thead>
                <tbody>
                  {timeEntries.map((entry) => (
                    <tr key={entry.timeEntryId}>
                      <td className="text-nowrap">
                        <Button
                          size="sm"
                          variant="link"
                          title="Resume this task"
                          onClick={() => onResume(entry.title)}
                        >
                          <FontAwesomeIcon icon={faPlay} />
                        </Button>
                        <Button
                          size="sm"
                          variant="link"
                          title="Edit this entry"
                          onClick={() => setEntryToEdit(entry)}
                        >
                          <FontAwesomeIcon icon={faPenToSquare} />
                        </Button>
                        <Button
                          size="sm"
                          variant="link"
                          title="Delete this entry"
                          onClick={() => setEntryToDelete(entry)}
                        >
                          <FontAwesomeIcon icon={faTrashCan} />
                        </Button>
                      </td>
                      <td>{entry.title}</td>
                      <td>{formatTime(entry.startTime)}</td>
                      <td>{formatTime(entry.endTime)}</td>
                    </tr>
                  ))}
                </tbody>
              </Table>
            </Card.Body>
          </Card>
        </Col>
      </Row>

      <Row className="mb-3">
        <Col>
          <Card className="mt-1 mb-3">
            <Card.Body>
              <h3>Timesheet</h3>
              <a href={config.timesheetPortalUrl} target="_blank" rel="noreferrer">
                {config.timesheetPortalName}
              </a>
              <hr />

              <Row className="g-2 align-items-center">
                <Col xs={12} md>
                  <Form.Control
                    type="date"
                    aria-label="Timesheet date"
                    value={scheduleDate}
                    onChange={(event) => void changeScheduleDate(event.target.value)}
                  />
                </Col>
                <Col xs="auto">
                  <Button
                    size="sm"
                    onClick={() => void changeScheduleDate(toLocalISODateString(new Date()))}
                  >
                    Today
                  </Button>
                </Col>
                <Col xs="auto">
                  <Button size="sm" onClick={() => shiftScheduleDate(-1)}>
                    Previous
                  </Button>
                </Col>
                <Col xs="auto">
                  <Button size="sm" onClick={() => shiftScheduleDate(1)}>
                    Next
                  </Button>
                </Col>
              </Row>

              <div className="mt-3">Total Hours: {totalHours}</div>

              <Table striped size="sm">
                <thead>
                  <tr>
                    <th />
                    <th>Title</th>
                    <th>Total Hours</th>
                  </tr>
                </thead>
                <tbody>
                  {timeSheetEntries.map((entry) => (
                    <tr key={entry.title}>
                      <td className="text-nowrap">
                        <Button
                          size="sm"
                          variant="link"
                          title="Resume this task"
                          onClick={() => onResume(entry.title)}
                        >
                          <FontAwesomeIcon icon={faPlay} />
                        </Button>
                      </td>
                      <td>{entry.title}</td>
                      <td>{entry.totalHours}</td>
                    </tr>
                  ))}
                </tbody>
              </Table>
            </Card.Body>
          </Card>
        </Col>
      </Row>

      {entryToEdit !== null && (
        <EditTimeEntryDialog
          entry={entryToEdit}
          commonTitles={commonTitles}
          recentTitles={recentTitles}
          onClose={() => setEntryToEdit(null)}
          onSaved={() => {
            setEntryToEdit(null);
            void refreshAll(scheduleDate);
          }}
        />
      )}

      {entryToDelete !== null && (
        <ConfirmDeleteDialog
          entry={entryToDelete}
          onClose={() => setEntryToDelete(null)}
          onConfirm={() => void onConfirmDelete()}
        />
      )}
    </Container>
  );
}
