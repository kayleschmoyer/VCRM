# OWASP Dependency-Check Configuration

- **Purpose:** Detect vulnerable NuGet and npm/k6 dependencies in adapters and test harnesses.
- **Tool:** [OWASP Dependency-Check](https://owasp.org/www-project-dependency-check/)

## CLI Usage

```bash
dependency-check.sh \
  --project "VCRM Adapter" \
  --scan . \
  --out Artifacts/DependencyCheck \
  --format "HTML" --format "JSON" \
  --nvdApiKey ${NVD_API_KEY} \
  --failOnCVSS 7.0
```

- Persist HTML + JSON reports in `Artifacts/DependencyCheck` for CI publishing.
- CI gate: build fails if any dependency has CVSS ≥ 7.0.

## Gradle Wrapper Example (for reference services)

```gradle
plugins {
    id "org.owasp.dependencycheck" version "10.0.0"
}

dependencyCheck {
    failBuildOnCVSS = 7.0
    formats = ['HTML', 'JSON']
    outputDirectory = "Artifacts/DependencyCheck"
}
```

> Ensure local developers run the CLI before pushing new dependency updates to avoid blocking the pipeline.
