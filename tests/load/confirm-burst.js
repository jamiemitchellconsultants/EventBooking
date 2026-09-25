// tests/load/confirm-burst.js (complete)
import http from 'k6/http';
import { sleep } from 'k6';
import { Counter, Gauge } from 'k6/metrics';

const fixture = JSON.parse(open('./fixture.json'));
const target = __ENV.API_URL || 'http://localhost:5001';
const prefix = 'eventbooking_capacity_lock_hold_duration_seconds';
const created = new Counter('load_created');
const exhausted = new Counter('load_capacity_exhausted');
const unexpected = new Counter('load_unexpected');
const serverErrors = new Counter('load_server_errors');
const lockSamples = new Gauge('load_lock_samples');
const under50ms = new Gauge('load_lock_under_50ms');

if (!fixture.eventId || !Array.isArray(fixture.bookTokens)
    || fixture.bookTokens.length !== 500
    || new Set(fixture.bookTokens).size !== 500
    || new Set(fixture.bookTokens.map((token) => token.slice(0, 12))).size !== 500) {
  throw new Error('The load fixture must contain one event and 500 distinct token prefixes.');
}

export const options = {
  scenarios: {
    confirm: {
      executor: 'per-vu-iterations',
      vus: 500,
      iterations: 1,
      maxDuration: '5m',
    },
  },
  thresholds: {
    iterations: ['count==500'],
    load_created: ['count==100'],
    load_capacity_exhausted: ['count==400'],
    load_unexpected: ['count==0'],
    load_server_errors: ['count==0'],
    load_lock_samples: ['value==500'],
    load_lock_under_50ms: ['value>=475'],
  },
};

function sample(text, suffix) {
  const name = prefix + suffix;
  const row = text.split('\n').find((line) => line.startsWith(name + ' '));
  if (!row) return null;
  const value = Number(row.slice(name.length + 1).trim());
  return Number.isFinite(value) ? value : null;
}

function bucket(text) {
  const name = prefix + '_bucket{le="0.049999"}';
  const row = text.split('\n').find((line) => line.startsWith(name + ' '));
  if (!row) return null;
  const value = Number(row.slice(name.length + 1).trim());
  return Number.isFinite(value) ? value : null;
}

function scrape() {
  const response = http.get(target + '/metrics', { timeout: '10s' });
  if (response.status !== 200) return null;
  return { count: sample(response.body, '_count'), under: bucket(response.body) };
}

export function setup() {
  const before = scrape();
  if (!before) throw new Error('GET /metrics failed before the burst.');
  created.add(0);
  exhausted.add(0);
  unexpected.add(0);
  serverErrors.add(0);
  return { before, startAt: Date.now() + 10000 };
}

export default function (data) {
  const delay = (data.startAt - Date.now()) / 1000;
  if (delay > 0) sleep(delay);
  const token = fixture.bookTokens[__VU - 1];
  const response = http.post(
    target + '/api/booking/' + encodeURIComponent(token) + '/confirm',
    JSON.stringify({ eventId: fixture.eventId }),
    { headers: { 'Content-Type': 'application/json' }, timeout: '120s',
      tags: { name: 'POST /api/booking/{token}/confirm' } },
  );
  if (response.status === 201) {
    created.add(1);
    return;
  }
  if (response.status >= 500) serverErrors.add(1);
  if (response.status === 409) {
    try {
      const problem = JSON.parse(response.body);
      if (problem.type === 'capacity-exhausted') {
        exhausted.add(1);
        return;
      }
    } catch (_) {
      // A malformed error body is an unexpected outcome.
    }
  }
  unexpected.add(1);
}

export function teardown(data) {
  const after = scrape();
  const before = data.before;
  const count = after && after.count !== null
    ? after.count - (before.count === null ? 0 : before.count) : -1;
  const under = after && after.under !== null
    ? after.under - (before.under === null ? 0 : before.under) : -1;
  lockSamples.add(count);
  under50ms.add(under);
  if (count !== 500 || under < 475) {
    console.error('Capacity lock metric delta failed: count=' + count + ', under50ms=' + under);
  }
}
