# Validation Plan

To verify the accuracy, performance, and governance capabilities of System Cartographer, we recommend testing against these open-source industry standards.

## 1. Microservices & Federation: [dotnet/eShop](https://github.com/dotnet/eShop)

**Scenario**: Validating the scanner's ability to handle multiple loosely coupled services and the Dashboard's ability to visualize distributed systems.

- **Setup**: Clone the repo.
- **Scan Goal**: Run a scan on the root directory to see if the "Federation" view correctly clusters the specialized microservices (Identity, Catalog, Basket, Ordering).
- **Key Check**: Verify that `Http` calls between services are detected as links (if using Refit/HttpClient patterns that the scanner supports).

## 2. Stress Test & Monolith: [nopCommerce](https://github.com/nopSolutions/nopCommerce)

**Scenario**: Performance benchmarking on a massive, real-world Codebase.

- **Stats**: 10+ years of history, thousands of classes, heavy plugin architecture.
- **Scan Goal**:
  - Measure `scan` time (Target: < 30 seconds for non-linked scan).
  - Stress test the D3.js dashboard with thousands of nodes.
- **Key Check**: Does the dashboard remain responsive? Do the "Clusters" correctly identify the plugin architecture boundaries?

## 3. Governance Compliance: [CleanArchitecture](https://github.com/jasontaylordev/CleanArchitecture)

**Scenario**: Verifying the `governance.yaml` enforcement engine. This repo adheres to strict architectural layers.

- **Setup**: Clone the repo and add the following `governance.yaml` to the root:

```yaml
version: 1.0
definitions:
  domain:
    namespace: "CleanArchitecture.Domain.*"
  application:
    namespace: "CleanArchitecture.Application.*"
  infrastructure:
    namespace: "CleanArchitecture.Infrastructure.*"
  api:
    namespace: "CleanArchitecture.WebUI.*"

rules:
  # Enforce the Dependency Rule (Points inward)
  # API -> Infrastructure -> Application -> Domain
  - type: layering
    mode: strict
    layers:
      - "@api"
      - "@infrastructure"
      - "@application"
      - "@domain"
```

- **Expected Result**: **0 Violations**. The scanner should confirm the architecture is perfect.
- **Test**: Intentionally add a reference from `Domain` to `Infrastructure` and verify the scanner screams at you.
