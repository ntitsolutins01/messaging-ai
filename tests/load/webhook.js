// Teste de carga do webhook com k6 (módulo 09).
// Uso:  BASE_URL=http://localhost:5080 WEBHOOK_SECRET=load-secret k6 run tests/load/webhook.js
// Dois cenários: eventos únicos (throughput) e tempestade de reenvios (mesmo evento repetido).
import http from 'k6/http';
import crypto from 'k6/crypto';
import { check } from 'k6';
import { Counter } from 'k6/metrics';

const BASE = __ENV.BASE_URL || 'http://localhost:5080';
const SECRET = __ENV.WEBHOOK_SECRET || 'load-secret';
const unexpectedStatus = new Counter('unexpected_status');

export const options = {
  scenarios: {
    unique_events: {
      executor: 'constant-arrival-rate',
      rate: 100,
      timeUnit: '1s',
      duration: '30s',
      preAllocatedVUs: 50,
      maxVUs: 200,
      exec: 'uniqueEvents',
    },
    retry_storm: {
      executor: 'constant-arrival-rate',
      rate: 50,
      timeUnit: '1s',
      duration: '30s',
      preAllocatedVUs: 20,
      maxVUs: 100,
      exec: 'retryStorm',
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<300'],
    unexpected_status: ['count==0'],
  },
};

function signature(body) {
  const ts = Math.floor(Date.now() / 1000);
  const mac = crypto.hmac('sha256', SECRET, `${ts}.${body}`, 'hex');
  return `t=${ts},v1=${mac}`;
}

function post(eventId, body) {
  const res = http.post(`${BASE}/webhooks/acme`, body, {
    headers: {
      'Content-Type': 'application/json',
      'X-Event-Id': eventId,
      'X-Signature': signature(body),
    },
  });
  const ok = check(res, { 'status 200': (r) => r.status === 200 });
  if (!ok) {
    unexpectedStatus.add(1);
  }
}

export function uniqueEvents() {
  const id = `evt_${__VU}_${__ITER}_${Date.now()}`;
  post(id, JSON.stringify({ id, amount: 10 }));
}

export function retryStorm() {
  // 50 reenvios do mesmo evento antes de passar para o próximo: duplicata deve sempre responder 200.
  const id = `evt_storm_${Math.floor(__ITER / 50)}`;
  post(id, JSON.stringify({ id, amount: 10 }));
}
