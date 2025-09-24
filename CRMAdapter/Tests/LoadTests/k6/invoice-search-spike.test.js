/**
 * Purpose: Spike test for invoice search endpoint.
 * Validates: System resilience during short high-RPS bursts and recovery to baseline.
 * Success: Error rate below 2% with P99 latency under 600ms; outputs JSON for CI reporting.
 */
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend, Rate } from 'k6/metrics';

const invoiceSearchDuration = new Trend('invoice_search_duration');
const invoiceSearchErrors = new Rate('invoice_search_errors');

export const options = {
  thresholds: {
    invoice_search_duration: ['p(99)<600'],
    invoice_search_errors: ['rate<0.02'],
  },
  scenarios: {
    spike: {
      executor: 'ramping-arrival-rate',
      startRate: 5,
      timeUnit: '1s',
      preAllocatedVUs: 10,
      stages: [
        { target: 50, duration: '30s' },
        { target: 75, duration: '30s' },
        { target: 5, duration: '1m' },
      ],
      gracefulStop: '45s',
    },
  },
};

const baseUrl = `${__ENV.CRM_BASE_URL || 'http://localhost:5000'}/api/invoices/search?query=INV`;

export default function () {
  const response = http.get(baseUrl);
  const success = check(response, {
    'status is 200': (res) => res.status === 200,
  });

  invoiceSearchDuration.add(response.timings.duration);
  invoiceSearchErrors.add(!success);

  sleep(0.5);
}
