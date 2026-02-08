# Governance Rules Engine Implementation

The Governance Rules Engine in the Diagnostic Structural Lens (DSL) provides real-time architectural enforcement by evaluating dependencies against predefined constraints during graph hydration.

## 1. Core Logic: `CheckViolation`

Architectural rules are evaluated in the `DiagnosticStructuralLensDataService.CheckViolation` method. This method takes a source and target element (typed as objects or the `CodeAtom`/`SqlAtom` records from `DiagnosticStructuralLens.Core`) and the link type to determine if the relationship is "illegal".

**Type Mapping Note**: While internal logic uses generic `Atom` references for planning, the implementation strictly maps to `CodeAtom` and `SqlAtom` records. The `DiagnosticStructuralLensDataService` handles the polymorphic resolution.

```csharp
public bool CheckViolation(object source, object target, LinkType type)
{
    // Most rules apply to CodeAtoms (C# code)
    if (source is CodeAtom s && target is CodeAtom t)
    {
        // Rule 1: Layering - Domain cannot depend on Infrastructure or Web
        if (IsDomain(s) && (IsInfrastructure(t) || IsWeb(t)))
        {
            return true;
        }

        // Rule 2: Explicit Blocklist - Controllers cannot bypass Service layer to access Repositories
        if (s.Name.EndsWith("Controller") && target.Name.EndsWith("Repository"))
        {
            return true;
        }

        // Rule 3: Vertical Slicing
        if (GetRootModule(s) != GetRootModule(t) && type == LinkType.Calls)
        {
             // return false; // Disabled for V1
        }
    }

    return false;
}

private bool IsDomain(CodeAtom atom) => atom.Namespace?.Contains(".Domain") ?? false;
private bool IsInfrastructure(CodeAtom atom) => atom.Namespace?.Contains(".Infrastructure") ?? false;
private bool IsWeb(CodeAtom atom) => atom.Namespace?.Contains(".Web") ?? atom.Namespace?.Contains(".Api") ?? false;

private string GetRootModule(CodeAtom atom)
{
    var parts = atom.Namespace?.Split('.');
    if (parts?.Length >= 2) return parts[1];
    return "Common";
}
```

## 2. Rule Definitions (V1)

### 2.1 Layering Constraint

- **Goal**: Protect the purity of the Domain layer.
- **Logic**: If the source atom is in the `.Domain` namespace, it is prohibited from referencing atoms in the `.Infrastructure`, `.Web`, or `.Api` namespaces.
- **Violation Significance**: Indicates a breach of "Clean Architecture" or "Hexagonal Architecture" principles, where external concerns (DB/API) leak into core business logic.

### 2.2 Layer Bypass (Explicit Blocklist)

- **Goal**: Enforce the Service Layer pattern.
- **Logic**: Atoms with the suffix `Controller` are prohibited from directly linking to atoms with the suffix `Repository`.
- **Violation Significance**: Ensures that business logic remains centralized in Services rather than being bypassed for "convenience," which leads to fragmentation.

## 3. Data Integration

The `CheckViolation` logic is wired into the GraphQL `Query` resolvers. When links are constructed for the Federation (L1), Repository (L2), or Namespace (L3) views, the `IsViolation` flag is computed on-the-fly.

- **Types Updated**: `InternalLink`, `CrossRepoLink`, `AtomLinkView`.
- **Performance**: Rule evaluation is $O(1)$ per edge, relying on string suffix/prefix checks, making it suitable for large-scale graphs without significant latency.

## 4. Visual Feedback

In the dashboard, violations are rendered as **Red, Dashed, and Pulsing** edges when **Governance Mode** is toggled. This immediate visual feedback allows developers to identify structural regression during PR reviews or exploration.
