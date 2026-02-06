# System Validation Plan & Rubric

This document outlines the standard operating procedure for validating the **System Cartographer** against industry-standard architectures. Use this guide to verify correctness, performance, and user experience.

## 📋 Prerequisites

1.  **System Cartographer CLI**: Built and available on `PATH` (or run via `dotnet run`).
2.  **Git**: For cloning validation repositories.
3.  **Docker** (Optional): For testing containerized deployments.

---

## 🏗️ Validation Targets

| ID     | Repository          | Architecture     | Validation Focus                               |
| ------ | ------------------- | ---------------- | ---------------------------------------------- |
| **V1** | `dotnet/eShop`      | Microservices    | Federation, Service Links, Distributed Systems |
| **V2** | `nopCommerce`       | Monolith (Large) | Performance, Stress Testing, Time Travel       |
| **V3** | `CleanArchitecture` | Layered          | Governance Rules, Zero-False-Positives         |

---

## 📝 Execution Implementation Plan

### V1: Federation Validation (`eShop`)

_Goal: Ensure multiple microservices are correctly identified and linked._

1.  **Clone**: `git clone https://github.com/dotnet/eShop.git validation/eShop`
2.  **Scan**:
    ```bash
    cartographer scan --repo validation/eShop --output v1_eshop.json
    ```
3.  **Verify**:
    - Open `v1_eshop.json` in Dashboard.
    - Check for distinct clusters (Identity, Catalog, Ordering).
    - Verify `Http` links exist between services.

### V2: Stress & Time Travel (`nopCommerce`)

_Goal: Ensure the system handles large-scale data and temporal evolution._

1.  **Clone**: `git clone https://github.com/nopSolutions/nopCommerce.git validation/nopCommerce`
2.  **Baseline Scan (v4.50)**:
    ```bash
    cd validation/nopCommerce && git checkout release-4.50
    cartographer scan --repo . --output ../../v2_nop_4.50.json
    ```
3.  **Current Scan (v4.70)**:
    ```bash
    git checkout release-4.70
    cartographer scan --repo . --output ../../v2_nop_4.70.json
    ```
4.  **Verify**:
    - Load both files into Dashboard.
    - Use Timeline controls to play animation.
    - Toggle "Diff Mode" to see structural changes.

### V3: Governance Enforcement (`CleanArchitecture`)

_Goal: Prove that rules are correctly enforced on a compliant codebase._

1.  **Clone**: `git clone https://github.com/jasontaylordev/CleanArchitecture.git validation/CleanArchitecture`
2.  **Configure**: Create `governance.yaml` in root (content below).
    <details>
    <summary>Click to see governance.yaml</summary>

    ```yaml
    version: 1.0
    definitions:
      domain: { namespace: "CleanArchitecture.Domain.*" }
      application: { namespace: "CleanArchitecture.Application.*" }
      infrastructure: { namespace: "CleanArchitecture.Infrastructure.*" }
      api: { namespace: "CleanArchitecture.WebUI.*" }
    rules:
      - type: layering
        mode: strict
        layers: ["@api", "@infrastructure", "@application", "@domain"]
    ```

    </details>

3.  **Scan**: `cartographer scan --repo . --output v3_clean.json`
4.  **Verify**:
    - **Result should be 0 Violations.**
    - _sabotage_: Add `public CleanArchitecture.Infrastructure.MyClass BadProp { get; set; }` to a Domain entity.
    - **Result should be >0 Violations.**

---

## 🏆 Scoring Rubric

Use this rubric to grade the system's readiness.

### 1. Performance (nopCommerce)

| Grade       | Criteria                                                    |
| ----------- | ----------------------------------------------------------- |
| 🟢 **Pass** | Scan time < 60s. Dashboard loads < 2s. 60 FPS navigation.   |
| 🟡 **Warn** | Scan time < 120s. Dashboard loads < 5s. Occasional stutter. |
| 🔴 **Fail** | Scan time > 120s. Browser crash or significant lag.         |

### 2. Accuracy (CleanArchitecture)

| Grade       | Criteria                                                                    |
| ----------- | --------------------------------------------------------------------------- |
| 🟢 **Pass** | 0 False Positives on strict layering. 100% detection of sabotage violation. |
| 🟡 **Warn** | < 5 False Positives (fixable via config).                                   |
| 🔴 **Fail** | Missed violations or > 5 False Positives.                                   |

### 3. User Experience (Time Travel)

| Grade       | Criteria                                                                        |
| ----------- | ------------------------------------------------------------------------------- |
| 🟢 **Pass** | Smooth animation between snapshots. nodes perform "biological" division/growth. |
| 🟡 **Warn** | Animation works but nodes "teleport" or clutter.                                |
| 🔴 **Fail** | Timeline broken, diff mode incorrect, or crash.                                 |
