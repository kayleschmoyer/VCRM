# SonarQube Static Analysis Configuration

- **Purpose:** Harden the CRM adapter by scanning every pull request for code smells, injection flaws, and insecure APIs.
- **Scope:** Entire solution, including test projects. Build with `dotnet build` before analysis to generate symbols.
- **Key Quality Gates:**
  - No new blocker or critical issues.
  - Code coverage on new code ≥ 85% (enforced via Coverlet + ReportGenerator output).
  - Security hotspots reviewed on every PR.

## Command Reference

```bash
sonar-scanner \
  -Dsonar.projectKey=VCRM-Adapters \
  -Dsonar.organization=enterprise-quality \
  -Dsonar.host.url=${SONAR_HOST_URL} \
  -Dsonar.login=${SONAR_TOKEN} \
  -Dsonar.cs.vstest.reportsPaths=Artifacts/TestResults/*.trx \
  -Dsonar.cs.vscoveragexml.reportsPaths=Artifacts/Coverage/Summary.xml \
  -Dsonar.coverage.exclusions=Tests/**
```

> The CI pipeline publishes the test and coverage artifacts under `Artifacts/` so SonarQube ingests them automatically.

## Remediation Workflow

1. Run static analysis locally with the same command to reproduce issues.
2. For each finding, document the root cause in the PR description (include file + line number).
3. Fix or justify with security review. No suppression without approval from the security guild.
