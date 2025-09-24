# Mutation Testing Strategy

- **Tool:** [Stryker.NET](https://stryker-mutator.io/docs/stryker-net/Introduction/) targeting the `CRMAdapter.Tests` project.
- **Goal:** Mutation score ≥ 80% on adapters and domain logic.

## Quick Start

```bash
dotnet tool install -g dotnet-stryker
cd CRMAdapter/Tests
STRYKER_DASHBOARD_API_KEY=<token> dotnet stryker --project-file=CRMAdapter.Tests.csproj \
  --reporter html --reporter json --reporter dashboard \
  --coverage-analysis perTest --break-on 80
```

- HTML reports: `Artifacts/Mutation/stryker-report.html`
- Dashboard uploads gated by `STRYKER_DASHBOARD_API_KEY`.

## Recommendations

1. Run mutation tests nightly due to runtime cost.
2. Focus on mutants surviving in adapters or configuration validators.
3. Add unit tests whenever a mutant survives—no manual suppression without justification.
