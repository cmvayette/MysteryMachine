# Governance Rules

Diagnostic Structural Lens enforces architectural integrity through a flexible, YAML-based configuration engine. You can define custom rules for your repository by creating a `governance.yaml` file in the root directory.

## File Format: `governance.yaml`

The configuration file has two main sections:

1.  **Definitions**: Reusable aliases for sets of code (e.g., "All Controllers").
2.  **Rules**: Constraints applied to those definitions.

### Example

```yaml
version: 1.0

definitions:
  # Select by Namespace
  web_layer:
    namespace: "MyProject.Web.*"
  domain_layer:
    namespace: "MyProject.Core.*"
  data_layer:
    namespace: "MyProject.Data.*"

  # Select by Name Pattern (Regex)
  controllers:
    pattern: ".*Controller$"
  repositories:
    pattern: ".*Repository$"

rules:
  # Rule 1: Layering
  # Enforce Web -> Domain -> Data
  - type: layering
    mode: strict
    layers:
      - "@web_layer"
      - "@domain_layer"
      - "@data_layer"

  # Rule 2: Explicit Ban
  # Controllers should never talk to Repositories directly
  - type: forbidden
    source: "@controllers"
    target: "@repositories"
    message: "Architecture Violation: Controllers must use Services, not Repositories."
```

---

## 1. Definitions

Use the `definitions` block to create named selectors starting with `@`.

| Property    | Description                                    | Example                     |
| ----------- | ---------------------------------------------- | --------------------------- |
| `namespace` | Matches namespace prefix (supports wildcards). | `MyApp.Web.*`               |
| `pattern`   | Regex matching the **Class Name**.             | `.*Service$`                |
| `type`      | Matches the C# Code Atom type.                 | `Class`, `Interface`, `Dto` |

**Example:**

```yaml
definitions:
  dtos:
    type: Dto
  legacy_code:
    namespace: "MyApp.Legacy.*"
```

## 2. Rule Types

### A. Forbidden Dependency (`forbidden`)

Prevents **Source** from having any dependency (call, inheritance, usage) on **Target**.

| Property  | Description                                              |
| --------- | -------------------------------------------------------- |
| `source`  | Selector for the consumer (e.g., `@controllers`).        |
| `target`  | Selector for the dependee (e.g., `@repositories`).       |
| `message` | Custom error message displayed in the CLI and Dashboard. |

### B. Layering (`layering`)

Enforces a strict TOP-DOWN ordering.

- Layers are defined as a list.
- Layer `N` can depend on `N+1`.
- Layer `N` CANNOT depend on `N-1` (Upward violation).

| Property | Description                                               |
| -------- | --------------------------------------------------------- |
| `layers` | Ordered list of selectors (Top to Bottom).                |
| `mode`   | `strict` (only N -> N+1) or `relaxed` (N -> N+any below). |

### C. Visibility (`visibility`)

Enforces encapsulation using an **Allow List**.

- If a link's **Target** matches the rule, the **Source** MUST be in the `allowed_consumers` list.
- If `allowed_consumers` is empty, the Target is effectively **Private**.

| Property            | Description                                                |
| ------------------- | ---------------------------------------------------------- |
| `target`            | Selector for the restricted code (e.g., `@internal-core`). |
| `allowed_consumers` | List of selectors allowed to access the target.            |

**Example:**

```yaml
# Only 'Core' can access 'Internal'
- type: visibility
  target: "@internal"
  allowed_consumers:
    - "@core"
```

**Strict vs Relaxed:**

- **Strict**: Web -> Domain is OK. Web -> Data is BLOCKED (Skipping Domain).
- **Relaxed**: Web -> Data is OK.

## Troubleshooting

- **No Diagnostics?**: Ensure your `governance.yaml` is in the root folder where you run `dsl scan`.
- **Regex Issues**: The `pattern` is case-insensitive by default.
- **Unmatched Selectors**: If a definition doesn't match any atoms, the rule effectively does nothing for that selector.
