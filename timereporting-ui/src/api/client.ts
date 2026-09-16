import { config } from '../config';
import type { LoginCredentials, TimeEntry, TimeSheetEntry, User, UserSummary } from '../models';
import { toLocalISODateTimeString } from '../utils/time';
import { getToken } from './storage';

export class ApiError extends Error {
  readonly status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
  }
}

type Query = Record<string, string | number | null | undefined>;

function buildUrl(path: string, query?: Query): string {
  const url = new URL(`${config.apiUrl}${path}`);
  for (const [key, value] of Object.entries(query ?? {})) {
    if (value !== null && value !== undefined) {
      url.searchParams.set(key, String(value));
    }
  }
  return url.toString();
}

async function request<T>(
  path: string,
  options: { method?: string; query?: Query; body?: unknown; anonymous?: boolean } = {},
): Promise<T> {
  const { method = 'GET', query, body, anonymous = false } = options;

  const headers: Record<string, string> = {};
  if (!anonymous) {
    headers.Authorization = `Bearer ${getToken()}`;
  }
  if (body !== undefined) {
    headers['Content-Type'] = 'application/json';
  }

  const response = await fetch(buildUrl(path, query), {
    method,
    headers,
    body: body === undefined ? null : JSON.stringify(body),
  });

  if (!response.ok) {
    throw new ApiError(`${method} ${path} failed with status ${response.status}.`, response.status);
  }

  // 204s and empty bodies are normal for the void-returning endpoints.
  const text = await response.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

export const api = {
  login(credentials: LoginCredentials): Promise<User> {
    return request<User>('/Authentication/Login', {
      method: 'POST',
      body: credentials,
      anonymous: true,
    });
  },

  getCommonTitles(): Promise<string[]> {
    return request<string[]>('/TimeEntries/GetCommonTitles');
  },

  getRecentTitles(): Promise<string[]> {
    return request<string[]>('/TimeEntries/GetRecentTitles');
  },

  /** Admins may pass another user's id; everyone else gets their own entries. */
  getSchedule(userId?: number | null): Promise<TimeEntry[]> {
    return request<TimeEntry[]>('/TimeEntries/GetSchedule', {
      query: { UserId: userId },
    });
  },

  getDailyTimeSheet(workDate: string | null, userId?: number | null): Promise<TimeSheetEntry[]> {
    return request<TimeSheetEntry[]>('/TimeEntries/GetDailyTimeSheet', {
      query: { WorkDate: workDate, UserId: userId },
    });
  },

  /** Admin-only: everyone who has logged in at least once. */
  getUsers(): Promise<UserSummary[]> {
    return request<UserSummary[]>('/Users/GetUsers');
  },

  getTimeEntry(id: number): Promise<TimeEntry> {
    return request<TimeEntry>('/TimeEntries/GetTimeEntry', { query: { id } });
  },

  startClock(title: string, time: Date): Promise<number> {
    return request<number>('/TimeEntries/StartClock', {
      method: 'POST',
      query: { Title: title, Time: toLocalISODateTimeString(time) },
    });
  },

  stopClock(time: Date): Promise<void> {
    return request<void>('/TimeEntries/StopClock', {
      method: 'POST',
      query: { Time: toLocalISODateTimeString(time) },
    });
  },

  editTimeEntry(entry: TimeEntry): Promise<void> {
    return request<void>('/TimeEntries/EditTimeEntry', {
      method: 'POST',
      query: {
        id: entry.timeEntryId,
        Title: entry.title,
        StartTime: entry.startTime,
        EndTime: entry.endTime,
      },
    });
  },

  deleteTimeEntry(id: number): Promise<void> {
    return request<void>('/TimeEntries/DeleteTimeEntry', {
      method: 'DELETE',
      query: { id },
    });
  },
};
