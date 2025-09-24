/**
 * Purpose: Sustained load test for customer list endpoint.
 * Validates: Throughput, latency percentiles, and non-error responses when listing recent customers.
 * Success: P95 under 350ms with <1% error rate, results exported as JSON for CI dashboards.
 */
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend, Rate } from 'k6/metrics';

const customerListDuration = new Trend('customer_list_duration');
const customerListErrors = new Rate('customer_list_errors');

export const options = {
  thresholds: {
    customer_list_duration: ['p(95)<350', 'p(99)<500'],
    customer_list_errors: ['rate<0.01'],
  },
  scenarios: {
    steady_list: {
      executor: 'constant-arrival-rate',
      rate: 10,
      timeUnit: '1s',
      duration: '2m',
      preAllocatedVUs: 5,
      gracefulStop: '30s',
    },
  },
};

const baseUrl = `${__ENV.CRM_BASE_URL || 'http://localhost:5000'}/api/customers/recent`;

export default function () {
  const response = http.get(baseUrl);
  const success = check(response, {
    'status is 200': (res) => res.status === 200,
    'has payload': (res) => !!res.body,
  });

  customerListDuration.add(response.timings.duration);
  customerListErrors.add(!success);

  sleep(1);
}
