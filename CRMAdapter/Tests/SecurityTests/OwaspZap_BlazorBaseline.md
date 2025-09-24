# OWASP ZAP Baseline Scan (placeholder for future Blazor UI)

- **Purpose:** Once the Blazor administrative UI ships, run the ZAP baseline scan to catch cross-site scripting and missing headers.
- **Command:**

```bash
zap-baseline.py \
  -t ${CRM_UI_BASE_URL:-http://localhost:5000} \
  -r Artifacts/ZapReports/blazor-baseline.html \
  -J Artifacts/ZapReports/blazor-baseline.json \
  -x Artifacts/ZapReports/blazor-baseline.xml
```

- Configure the CI nightly to execute the baseline scan with alert threshold `--fail-on-warning`.
- Document and triage all alerts ≥ Medium severity within 24 hours.
