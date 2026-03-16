#!/usr/bin/env bash
###############################################################################
# DSL Demo Script — Guided Walkthrough
#
# This script walks through every major DSL command using pre-cloned sample
# repositories. Run each step interactively or pass --all to run everything.
#
# Prerequisites:
#   - .NET 10 SDK installed
#   - DSL CLI built (dotnet build from repo root)
#   - Sample repos cloned into ../validation/ (or run Step 0)
#
# Usage:
#   ./run-demo.sh           # Interactive step-by-step
#   ./run-demo.sh --all     # Run all steps automatically
###############################################################################

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
DEMO_DIR="$SCRIPT_DIR"
VALIDATION_DIR="$REPO_ROOT/validation"
SNAPSHOTS_DIR="$DEMO_DIR/snapshots"
REPORTS_DIR="$DEMO_DIR/reports"
DSL="dotnet run --project $REPO_ROOT/src/DiagnosticStructuralLens.Cli --"

AUTO_MODE=false
[[ "${1:-}" == "--all" ]] && AUTO_MODE=true

# Colors
GREEN='\033[0;32m'
CYAN='\033[0;36m'
YELLOW='\033[1;33m'
BOLD='\033[1m'
NC='\033[0m'

step_count=0

step() {
    step_count=$((step_count + 1))
    echo ""
    echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${BOLD}Step $step_count: $1${NC}"
    echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${YELLOW}$2${NC}"
    echo ""
    if [[ "$AUTO_MODE" == false ]]; then
        read -rp "Press Enter to continue (or 'q' to quit)... " input
        [[ "$input" == "q" ]] && echo "Demo stopped." && exit 0
    fi
}

info() {
    echo -e "${GREEN}  > $1${NC}"
}

mkdir -p "$SNAPSHOTS_DIR" "$REPORTS_DIR"

###############################################################################
echo -e "${BOLD}"
echo "  ____  ____  _       ____                       "
echo " |  _ \\/ ___|| |     |  _ \\  ___ _ __ ___   ___  "
echo " | | | \\___ \\| |     | | | |/ _ \\ '_ \` _ \\ / _ \\ "
echo " | |_| |___) | |___  | |_| |  __/ | | | | | (_) |"
echo " |____/|____/|_____| |____/ \\___|_| |_| |_|\\___/ "
echo ""
echo " Diagnostic Structural Lens — Guided Walkthrough"
echo -e "${NC}"
echo " This demo uses 4 open-source .NET repos to show how DSL helps"
echo " teams visualize, analyze, and govern complex multi-system codebases."
echo ""
echo " Sample repos:"
echo "   1. CleanArchitecture  — Small, layered (224 components)"
echo "   2. eShop              — Microservices federation (1,301 components)"
echo "   3. nopCommerce        — Large monolith (18,745 components)"
echo "   4. bitwarden-server   — Mixed stack, DB-heavy (11,925 components + 2,101 DB objects)"
echo ""

###############################################################################
# STEP 0: Clone repos (if needed)
###############################################################################
step "Clone sample repositories" \
    "Skip this if repos already exist in validation/. Each is cloned with --depth 1."

REPOS=(
    "jasontaylordev/CleanArchitecture:CleanArchitecture"
    "dotnet/eShop:eShop"
    "nopSolutions/nopCommerce:nopCommerce"
    "bitwarden/server:bitwarden-server"
)

for entry in "${REPOS[@]}"; do
    IFS=':' read -r gh_repo dir_name <<< "$entry"
    if [[ -d "$VALIDATION_DIR/$dir_name" ]]; then
        info "$dir_name already cloned, skipping"
    else
        info "Cloning $gh_repo..."
        git clone --depth 1 "https://github.com/$gh_repo.git" "$VALIDATION_DIR/$dir_name"
    fi
done

###############################################################################
# STEP 1: Scan a single repo
###############################################################################
step "Scan a single repository (CleanArchitecture)" \
    "The 'scan' command parses all .cs and .sql files, extracts components
(classes, interfaces, DTOs, enums, stored procedures, tables, etc.),
and writes a snapshot JSON file — the foundation for all analysis."

$DSL scan --repo "$VALIDATION_DIR/CleanArchitecture" \
    --output "$SNAPSHOTS_DIR/clean-architecture.json" --ci

info "Snapshot saved: snapshots/clean-architecture.json"

###############################################################################
# STEP 2: Generate an architecture report
###############################################################################
step "Generate an architecture report (CleanArchitecture)" \
    "The 'report' command scans + analyzes in one step. It produces a markdown
report with vital signs, component taxonomy, namespace breakdown, central
hub analysis, risk scores, and architecture findings (God classes,
circular deps, complex controllers, etc.)."

$DSL report --repo "$VALIDATION_DIR/CleanArchitecture" \
    --output "$REPORTS_DIR/clean-architecture-report.md" --ci

info "Report saved: reports/clean-architecture-report.md"
info "Open the markdown file to see the full architecture breakdown."

###############################################################################
# STEP 3: Scan a large monolith
###############################################################################
step "Scan a large monolith (nopCommerce — 18k+ components)" \
    "This stress-tests DSL against a real-world large codebase.
nopCommerce has 2,300+ classes, 100+ plugins, and SQL stored procedures.
Watch for: scan duration, God class detection, DB object extraction."

$DSL report --repo "$VALIDATION_DIR/nopCommerce" \
    --output "$REPORTS_DIR/nopcommerce-report.md" --ci

info "Report saved: reports/nopcommerce-report.md"

###############################################################################
# STEP 4: Scan a microservices federation
###############################################################################
step "Scan a microservices repo (eShop)" \
    "eShop is Microsoft's reference microservices app. DSL detects multiple
bounded contexts (Catalog, Ordering, Identity) and cross-service links.
This is where the C4 federation view shines."

$DSL scan --repo "$VALIDATION_DIR/eShop" \
    --output "$SNAPSHOTS_DIR/eshop.json" --ci

info "Snapshot saved: snapshots/eshop.json"

###############################################################################
# STEP 5: Scan a DB-heavy mixed-stack repo
###############################################################################
step "Scan a DB-heavy repo (bitwarden-server — 2,100 DB objects)" \
    "Bitwarden's server has extensive T-SQL: 1,099 stored procedures,
744 columns, 163 views, 76 tables. This validates the SQL scanner's
ability to map shared data dependencies — the key pain point for
teams with 60+ systems sharing databases."

$DSL scan --repo "$VALIDATION_DIR/bitwarden-server" \
    --output "$SNAPSHOTS_DIR/bitwarden-server.json" --ci

info "Snapshot saved: snapshots/bitwarden-server.json"

###############################################################################
# STEP 6: Federate multiple snapshots into a global map
###############################################################################
step "Federate snapshots into a global map" \
    "The 'federate' command merges multiple snapshots into a single unified
graph. This is the killer feature for teams managing 60+ systems — it
reveals cross-system dependencies through shared database objects.

In your org, each team would scan their repo and publish snapshots.
The federation merges them into a single navigable map."

$DSL federate \
    --snapshots "$SNAPSHOTS_DIR/clean-architecture.json,$SNAPSHOTS_DIR/eshop.json,$SNAPSHOTS_DIR/nopcommerce.json,$SNAPSHOTS_DIR/bitwarden-server.json" \
    --output "$SNAPSHOTS_DIR/federated-global.json" --ci

info "Federated map saved: snapshots/federated-global.json"
info "Load this into the dashboard for the full C4 federation view."

###############################################################################
# STEP 7: Generate risk reports
###############################################################################
step "Generate risk reports" \
    "The 'risk' command scores every component based on complexity, coupling,
fan-in/fan-out, and maintainability. Use this to prioritize which systems
or components need attention first."

$DSL risk --snapshot "$SNAPSHOTS_DIR/nopcommerce.json" \
    --format text --output "$REPORTS_DIR/nopcommerce-risk.txt" --top 15 --ci
info "Text risk report: reports/nopcommerce-risk.txt"

$DSL risk --snapshot "$SNAPSHOTS_DIR/bitwarden-server.json" \
    --format html --output "$REPORTS_DIR/bitwarden-risk.html" --top 20 --ci
info "HTML risk report: reports/bitwarden-risk.html (open in browser)"

###############################################################################
# STEP 8: Blast radius analysis
###############################################################################
step "Calculate blast radius for a critical component" \
    "The 'blast' command shows what would be impacted if a specific component
changed. This directly answers the question: 'What breaks if I touch this?'"

# Find a high-connectivity atom from the bitwarden snapshot to demo blast
info "Calculating blast radius for a high-connectivity component..."
$DSL blast --snapshot "$SNAPSHOTS_DIR/bitwarden-server.json" \
    --atom "table:dbo.User" --depth 3 --ci || \
    info "(Note: atom ID format depends on scanner output — adjust to match your snapshot)"

###############################################################################
# STEP 9: Snapshot diffing
###############################################################################
step "Compare two snapshots (diff)" \
    "The 'diff' command compares two snapshots to detect what changed:
new components, removed components, modified relationships, and
breaking changes. Run this between releases to track architecture drift."

info "Comparing CleanArchitecture with eShop (cross-repo diff for demo)..."
$DSL diff \
    --baseline "$SNAPSHOTS_DIR/clean-architecture.json" \
    --snapshot "$SNAPSHOTS_DIR/eshop.json" \
    --format markdown --ci

###############################################################################
# STEP 10: Load into the dashboard
###############################################################################
step "Launch the interactive dashboard" \
    "The dashboard provides:
  - C4 model navigation (Federation > Repo > Namespace > Class > Method)
  - Interactive force-directed graph with ELK layout
  - Treemap view for hierarchical component breakdown
  - Governance mode (red pulsing edges for violations)
  - Blast radius visualization
  - Time travel between snapshots

No database required! Just two commands:

  # Terminal 1 — Start the API (loads snapshots from folder)
  DSL_SNAPSHOTS_DIR=\$(pwd)/snapshots dotnet run \\
      --project src/DiagnosticStructuralLens.Api --no-launch-profile

  # Terminal 2 — Start the dashboard
  cd dashboard && npm install && npm run dev

  # Open http://localhost:3000 in your browser

Optional: for persistence + time-travel, use docker compose up instead."

info "Demo complete!"

###############################################################################
echo ""
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BOLD}Demo Summary${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""
echo "  Artifacts generated:"
echo "    snapshots/clean-architecture.json       (224 components)"
echo "    snapshots/eshop.json                    (1,301 components)"
echo "    snapshots/nopcommerce.json              (18,745 components)"
echo "    snapshots/bitwarden-server.json         (11,925 components + 2,101 DB)"
echo "    snapshots/federated-global.json         (combined federation)"
echo ""
echo "    reports/clean-architecture-report.md    (architecture report)"
echo "    reports/nopcommerce-report.md           (architecture report)"
echo "    reports/nopcommerce-risk.txt            (risk report)"
echo "    reports/bitwarden-risk.html             (risk report — open in browser)"
echo ""
echo "  Next steps for your team:"
echo "    1. Replace the sample repos with your own 60+ systems"
echo "    2. Customize governance.yaml with your architectural rules"
echo "    3. Add 'dsl scan' to your CI/CD pipeline"
echo "    4. Use 'dsl federate' to build a global dependency map"
echo "    5. Use 'dsl diff' between releases to track architecture drift"
echo ""
