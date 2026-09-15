import { useCallback, useEffect, useState } from 'react';
import { Button, Card, Col, Container, Form, Row, Table } from 'react-bootstrap';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faPlay } from '@fortawesome/free-solid-svg-icons';
import { faPenToSquare, faTrashCan } from '@fortawesome/free-regular-svg-icons';
import { DateTime } from 'luxon';
import { api } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { config } from '../config';
import { QuarterHourPicker } from '../components/QuarterHourPicker';
import { TitleInput } from '../components/TitleInput';
import { ConfirmDeleteDialog } from '../dialogs/ConfirmDeleteDialog';
import { EditTimeEntryDialog } from '../dialogs/EditTimeEntryDialog';
import type { TimeEntry, TimeSheetEntry, UserSummary } from '../models';
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
  const { isAdmin } = useAuth();

  const [currentTitle, setCurrentTitle] = useState('');
  const [commonTitles, setCommonTitles] = useState<string[]>([]);
  const [recentTitles, setRecentTitles] = useState<string[]>([]);
  const [timeEntries, setTimeEntries] = useState<TimeEntry[]>([]);
  const [timeSheetEntries, setTimeSheetEntries] = useState<TimeSheetEntry[]>([]);
  const [clock, setClock] = useState<ClockParts>(() => toClockParts(new Date()));
  const [scheduleDate, setScheduleDate] = useState(() => toLocalISODateString(new Date()));
  const [error, setError] = useState('');

  /** Other users an admin can look at. Empty for everyone else. */
  const [users, setUsers] = useState<UserSummary[]>([]);
  /** Whose data is on screen. null means the signed-in user's own. */
  const [viewedUserId, setViewedUserId] = useState<number | null>(null);
  const isViewingOther = viewedUserId !== null;

  const [entryToEdit, setEntryToEdit] = useState<TimeEntry | null>(null);
  const [entryToDelete, setEntryToDelete] = useState<TimeEntry | null>(null);

  const refreshRecentTitles = useCallback(async () => {
    setRecentTitles(await api.getRecentTitles());
  }, []);

  const refreshSchedule = useCallback(async (userId: number | null) => {
    setTimeEntries(withImpliedEndTimes(await api.getSchedule(userId)));
  }, []);

  const refreshTimeSheet = useCallback(async (workDate: string, userId: number | null) => {
    setTimeSheetEntries(await api.getDailyTimeSheet(workDate, userId));
  }, []);

  /** Reloads everything that a clock change can affect. */
  const refreshAll = useCallback(
    async (workDate: string, userId: number | null) => {
      try {
        setError('');
        await Promise.all([
          refreshRecentTitles(),
          refreshSchedule(userId),
          refreshTimeSheet(workDate, userId),
        ]);
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
          api.getSchedule(null),
          api.getDailyTimeSheet(toLocalISODateString(new Date()), null),
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

  // The user picker is an admin-only affordance, and the endpoint behind it is
  // admin-only too, so there is nothing to fetch for anyone else.
  useEffect(() => {
    if (!isAdmin) return;

    let cancelled = false;
    const load = async () => {
      try {
        const list = await api.getUsers();
        if (!cancelled) setUsers(list);
      } catch {
        if (!cancelled) setError('Could not load the list of users.');
      }
    };

    void load();
    return () => {
      cancelled = true;
    };
  }, [isAdmin]);

  const changeViewedUser = async (value: string) => {
    const userId = value === '' ? null : Number(value);
    setViewedUserId(userId);
    try {
      setError('');
      await Promise.all([refreshSchedule(userId), refreshTimeSheet(scheduleDate, userId)]);
    } catch {
      setError('Could not load that user’s data.');
    }
  };

  const changeScheduleDate = async (value: string) => {
    setScheduleDate(value);
    try {
      setError('');
      await refreshTimeSheet(value, viewedUserId);
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
      await refreshAll(scheduleDate, null);
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
      await refreshAll(scheduleDate, null);
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
      await refreshAll(scheduleDate, null);
    } catch {
      setError('Could not delete the time entry.');
    }
  };

  const totalHours = timeSheetEntries.reduce((sum, entry) => sum + (entry.totalHours ?? 0), 0);
  const viewedUsername = users.find((user) => user.userId === viewedUserId)?.username ?? '';

  return (
    <Container>
      {error && <div className="alert alert-danger mt-3">{error}</div>}

      {isAdmin && (
        <Card className="mt-3">
          <Card.Body>
            <Row className="g-2 align-items-center">
              <Col xs="auto">
                <Form.Label htmlFor="viewed-user" className="mb-0">
                  Viewing
                </Form.Label>
              </Col>
              <Col xs={12} md>
                <Form.Select
                  id="viewed-user"
                  value={viewedUserId ?? ''}
                  onChange={(event) => void changeViewedUser(event.target.value)}
                >
                  <option value="">My own time</option>
                  {users.map((user) => (
                    <option key={user.userId} value={user.userId}>
                      {user.username}
                    </option>
                  ))}
                </Form.Select>
              </Col>
            </Row>
            {isViewingOther && (
              <div className="text-muted mt-2">
                Read-only view of {viewedUsername}’s time. Switch back to “My own time” to make
                changes.
              </div>
            )}
          </Card.Body>
        </Card>
      )}

      {!isViewingOther && (
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
      )}

      <Row>
        <Col>
          <Card className="mt-1">
            <Card.Body>
              <h3>Schedule</h3>
              <Table striped size="sm">
                <thead>
                  <tr>
                    {!isViewingOther && <th />}
                    <th>Title</th>
                    <th>Start Time</th>
                    <th>End Time</th>
                  </tr>
                </thead>
                <tbody>
                  {timeEntries.map((entry) => (
                    <tr key={entry.timeEntryId}>
                      {!isViewingOther && (
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
                      )}
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
                    {!isViewingOther && <th />}
                    <th>Title</th>
                    <th>Total Hours</th>
                  </tr>
                </thead>
                <tbody>
                  {timeSheetEntries.map((entry) => (
                    <tr key={entry.title}>
                      {!isViewingOther && (
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
                      )}
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
            void refreshAll(scheduleDate, null);
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
