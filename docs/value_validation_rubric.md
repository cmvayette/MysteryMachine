# Value Validation Rubric

This rubric defines the "Immediate Value" criteria for Diagnostic Structural Lens. It answers the question: _"Does this tool give an Architect or Lead Developer actionable insights within 60 seconds?"_

## 1. The "First Glance" Test (Temporal Insight)

**Goal**: The user must immediately understand the _shape_ and _scale_ of the system without interacting.

- [ ] **Legibility**: Are repositories and high-level boundaries clearly distinguishable?
- [ ] **Gravity**: Does the force-directed graph naturally cluster related components (high cohesion)?
- [ ] **Noise**: Is the default view free of overwhelming "hairball" links?

## 2. The "Governance" Test (Actionability)

**Goal**: Violations must be obvious and annoying. They should demand attention.

- [ ] **Visual Alarm**: Do governance violations (red edges/nodes) stand out against the dark background?
- [ ] **Traceability**: Clicking a violation clearly shows _Who_ (Source) -> _Who_ (Target) -> _Why_ (Rule).
- [ ] **Isolation**: Can I isolate just the violations to create a "fix list"?

## 3. The "Federation" Test (System Boundaries)

**Goal**: Validates that microservices/repos are treated as first-class citizens.

- [ ] **Clustering**: Do distinct Repositories form distinct visual clusters?
- [ ] **Cross-Pollination**: Are cross-repo links visually distinct from internal links?
- [ ] **Drill-Down**: Can I go from "Federation View" (L1) to "Code View" (L4) smoothly?

## 4. The "Scale" Test (Performance)

**Goal**: The tool must feel responsive even with 10k+ atoms.

- [ ] **Liveliness**: Does the simulation settle quickly (< 5s)?
- [ ] **Responsiveness**: Do interactions (hover, click) feel < 100ms?
