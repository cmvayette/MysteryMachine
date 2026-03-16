import { useQuery } from '@apollo/client';
import { useState, useMemo, useCallback } from 'react';
import { useNavigationStore } from '../store/navigationStore';
import {
  FEDERATION_QUERY,
  PACKAGE_METRICS_QUERY,
  CENTRALITY_QUERY,
  ORPHANS_QUERY,
  API_SURFACE_QUERY,
  SNAPSHOT_COMPARISON_QUERY,
} from '../graphql/client';

// ─── Types ───────────────────────────────────────────────────────────────────

interface PackageMetric {
  namespace: string;
  totalTypes: number;
  abstractTypes: number;
  afferentCoupling: number;
  efferentCoupling: number;
  instability: number;
  abstractness: number;
  distanceFromMainSequence: number;
  zone: string;
}

interface CentralityNode {
  node: { name: string; type: string; namespace: string };
  inDegree: number;
  outDegree: number;
  totalDegree: number;
}

interface OrphanNode {
  id: string;
  name: string;
  type: string;
  namespace: string;
  outboundCount: number;
}

interface ApiSurface {
  totalResolvers: number;
  totalDomainServices: number;
  unbackedResolvers: string[];
  unexposedServices: string[];
}

interface FedRepo {
  id: string;
  name: string;
  atomCount: number;
  namespaces: string[];
}

type SortField = 'namespace' | 'totalTypes' | 'instability' | 'abstractness' | 'distanceFromMainSequence' | 'zone' | 'afferentCoupling' | 'efferentCoupling';

// ─── Zone Badge ──────────────────────────────────────────────────────────────

function ZoneBadge({ zone }: { zone: string }) {
  const colors: Record<string, string> = {
    Ideal: 'bg-emerald-900/50 text-emerald-300 border-emerald-700/50',
    Pain: 'bg-red-900/50 text-red-300 border-red-700/50',
    Uselessness: 'bg-amber-900/50 text-amber-300 border-amber-700/50',
  };
  return (
    <span className={`px-2 py-0.5 rounded text-xs font-medium border ${colors[zone] || 'bg-slate-800 text-slate-400 border-slate-700'}`}>
      {zone}
    </span>
  );
}

// ─── Health Grade Badge (Phase 1) ────────────────────────────────────────────

function HealthGrade({ grade, score }: { grade: string; score: number }) {
  const gradeColors: Record<string, { bg: string; text: string; border: string; glow: string }> = {
    A: { bg: 'bg-emerald-900/40', text: 'text-emerald-300', border: 'border-emerald-600/50', glow: 'shadow-emerald-500/20' },
    B: { bg: 'bg-blue-900/40', text: 'text-blue-300', border: 'border-blue-600/50', glow: 'shadow-blue-500/20' },
    C: { bg: 'bg-amber-900/40', text: 'text-amber-300', border: 'border-amber-600/50', glow: 'shadow-amber-500/20' },
    D: { bg: 'bg-orange-900/40', text: 'text-orange-300', border: 'border-orange-600/50', glow: 'shadow-orange-500/20' },
    F: { bg: 'bg-red-900/40', text: 'text-red-300', border: 'border-red-600/50', glow: 'shadow-red-500/20' },
  };
  const c = gradeColors[grade] || gradeColors.F;
  return (
    <div className={`${c.bg} ${c.border} border rounded-xl px-5 py-3 flex items-center gap-3 shadow-lg ${c.glow}`} title={`Health Score: ${score.toFixed(0)}/100`}>
      <span className={`text-4xl font-black ${c.text}`}>{grade}</span>
      <div>
        <div className="text-xs text-slate-400 uppercase tracking-wider">Health</div>
        <div className="text-sm text-slate-300 font-medium">{score.toFixed(0)}/100</div>
      </div>
    </div>
  );
}

// ─── Risk Badge (Phase 5) ────────────────────────────────────────────────────

function RiskBadge({ degree }: { degree: number }) {
  if (degree > 200) return (
    <span className="px-1.5 py-0.5 rounded text-[10px] font-bold bg-red-900/50 text-red-300 border border-red-700/50" title="God object — consider splitting into sub-modules">CRITICAL</span>
  );
  if (degree > 100) return (
    <span className="px-1.5 py-0.5 rounded text-[10px] font-bold bg-amber-900/50 text-amber-300 border border-amber-700/50" title="High coupling — monitor for growth">WARNING</span>
  );
  return (
    <span className="px-1.5 py-0.5 rounded text-[10px] font-medium bg-emerald-900/30 text-emerald-400 border border-emerald-700/30">OK</span>
  );
}

// ─── Insight Box (Phase 4) ───────────────────────────────────────────────────

function InsightBox({ icon, children }: { icon: string; children: React.ReactNode }) {
  return (
    <div className="bg-amber-950/20 border border-amber-800/30 rounded-lg px-4 py-3 flex items-start gap-3 mb-4">
      <span className="material-symbols-outlined text-amber-400 text-lg mt-0.5">{icon}</span>
      <div className="text-sm text-amber-200/80 leading-relaxed">{children}</div>
    </div>
  );
}

// ─── Delta Badge (Phase 6) ───────────────────────────────────────────────────

function DeltaBadge({ value, suffix, invertColor }: { value: number; suffix?: string; invertColor?: boolean }) {
  if (value === 0) return null;
  const isPositive = value > 0;
  // By default, positive = red (worse), negative = green (better)
  // invertColor flips this (positive = green is better for Ideal zone count)
  const color = invertColor
    ? (isPositive ? 'text-emerald-400' : 'text-red-400')
    : (isPositive ? 'text-red-400' : 'text-emerald-400');
  const arrow = isPositive ? '↑' : '↓';
  return (
    <span className={`text-xs font-semibold ${color} ml-1`} title={`Change since baseline`}>
      {arrow}{Math.abs(value)}{suffix || ''}
    </span>
  );
}

// ─── Stat Card ───────────────────────────────────────────────────────────────

function StatCard({ icon, label, value, detail, accent, delta, deltaInvert }: {
  icon: string;
  label: string;
  value: string | number;
  detail?: string;
  accent?: string;
  delta?: number;
  deltaInvert?: boolean;
}) {
  return (
    <div className="bg-[#1e2026] border border-slate-700/50 rounded-lg p-5 flex flex-col gap-1 hover:border-[#a09078]/40 transition-colors">
      <div className="flex items-center gap-2 text-slate-400 text-xs uppercase tracking-wider font-medium">
        <span className="material-symbols-outlined text-base" style={accent ? { color: accent } : undefined}>{icon}</span>
        {label}
      </div>
      <div className="text-2xl font-bold text-slate-100 mt-1 flex items-center">
        {value}
        {delta !== undefined && <DeltaBadge value={delta} invertColor={deltaInvert} />}
      </div>
      {detail && <div className="text-xs text-slate-500">{detail}</div>}
    </div>
  );
}

// ─── Section Header ──────────────────────────────────────────────────────────

function SectionHeader({ icon, title, subtitle }: { icon: string; title: string; subtitle: string }) {
  return (
    <div className="flex items-center gap-3 mb-4 mt-8 first:mt-0">
      <div className="w-9 h-9 rounded-lg bg-[#a09078]/20 flex items-center justify-center">
        <span className="material-symbols-outlined text-[#c4a882]">{icon}</span>
      </div>
      <div>
        <h2 className="text-lg font-semibold text-slate-100">{title}</h2>
        <p className="text-xs text-slate-500">{subtitle}</p>
      </div>
    </div>
  );
}

// ─── Main Component ──────────────────────────────────────────────────────────

export function ArchitecturalReport({ onNavigateToExplorer }: { onNavigateToExplorer?: () => void }) {
  const navigateTo = useNavigationStore(s => s.navigateTo);

  const { data: fedData, loading: fedLoading } = useQuery(FEDERATION_QUERY);
  const { data: metricsData, loading: metricsLoading } = useQuery(PACKAGE_METRICS_QUERY);
  const { data: centralityData, loading: centralityLoading } = useQuery(CENTRALITY_QUERY);
  const { data: orphansData, loading: orphansLoading } = useQuery(ORPHANS_QUERY);
  const { data: apiData, loading: apiLoading } = useQuery(API_SURFACE_QUERY);
  const { data: compData } = useQuery(SNAPSHOT_COMPARISON_QUERY);

  const [sortField, setSortField] = useState<SortField>('distanceFromMainSequence');
  const [sortDir, setSortDir] = useState<'asc' | 'desc'>('desc');
  const [nsFilter, setNsFilter] = useState('');
  const [selectedScope, setSelectedScope] = useState<string>('__federation__');

  const loading = metricsLoading || centralityLoading || orphansLoading || apiLoading || fedLoading;
  const repos: FedRepo[] = useMemo(() => fedData?.federation?.repositories ?? [], [fedData]);
  const allMetrics: PackageMetric[] = useMemo(() => metricsData?.packageMetrics ?? [], [metricsData]);
  const allCentrality: CentralityNode[] = useMemo(() => centralityData?.centrality ?? [], [centralityData]);
  const allOrphans: OrphanNode[] = useMemo(() => orphansData?.orphans ?? [], [orphansData]);
  const api: ApiSurface = useMemo(() => apiData?.apiSurface ?? { totalResolvers: 0, totalDomainServices: 0, unbackedResolvers: [], unexposedServices: [] }, [apiData]);

  // Phase 3: Navigate to Explorer graph for a namespace (drill to project level)
  const goToNamespace = useCallback((namespace: string) => {
    const ownerRepo = repos.find(r => r.namespaces?.includes(namespace));
    if (ownerRepo) {
      navigateTo('project', ['Digital Backbone', ownerRepo.id, namespace]);
    } else {
      navigateTo('project', ['Digital Backbone', '', namespace]);
    }
    onNavigateToExplorer?.();
  }, [repos, navigateTo, onNavigateToExplorer]);

  // Phase 3: Navigate to Explorer graph for a specific atom (drill to component level)
  const goToNode = useCallback((namespace: string) => {
    const ownerRepo = repos.find(r => r.namespaces?.includes(namespace));
    if (ownerRepo) {
      navigateTo('component', ['Digital Backbone', ownerRepo.id, namespace]);
    } else {
      navigateTo('component', ['Digital Backbone', '', namespace]);
    }
    onNavigateToExplorer?.();
  }, [repos, navigateTo, onNavigateToExplorer]);

  // ── Sort repos: DSL first, then alpha ──

  const sortedRepos = useMemo(() => {
    return [...repos].sort((a, b) => {
      const aIsDsl = a.name.toLowerCase().includes('diagnostic');
      const bIsDsl = b.name.toLowerCase().includes('diagnostic');
      if (aIsDsl && !bIsDsl) return -1;
      if (!aIsDsl && bIsDsl) return 1;
      return a.name.localeCompare(b.name);
    });
  }, [repos]);

  // ── Build repo→namespaces lookup ──

  const repoNamespaces = useMemo(() => {
    const map = new Map<string, Set<string>>();
    repos.forEach(r => map.set(r.id, new Set(r.namespaces || [])));
    return map;
  }, [repos]);

  // ── Filtered data based on scope selection ──

  const metrics = useMemo(() => {
    if (selectedScope === '__federation__') return allMetrics;
    const nsSet = repoNamespaces.get(selectedScope);
    if (!nsSet) return allMetrics;
    return allMetrics.filter(m => nsSet.has(m.namespace));
  }, [allMetrics, selectedScope, repoNamespaces]);

  const centrality = useMemo(() => {
    if (selectedScope === '__federation__') return allCentrality;
    const nsSet = repoNamespaces.get(selectedScope);
    if (!nsSet) return allCentrality;
    return allCentrality.filter(c => nsSet.has(c.node.namespace || ''));
  }, [allCentrality, selectedScope, repoNamespaces]);

  const orphans = useMemo(() => {
    if (selectedScope === '__federation__') return allOrphans;
    const nsSet = repoNamespaces.get(selectedScope);
    if (!nsSet) return allOrphans;
    return allOrphans.filter(o => nsSet.has(o.namespace || ''));
  }, [allOrphans, selectedScope, repoNamespaces]);

  // ── Derived data ──

  const scopeLabel = useMemo(() => {
    if (selectedScope === '__federation__') return 'Digital Backbone';
    return repos.find(r => r.id === selectedScope)?.name || selectedScope;
  }, [selectedScope, repos]);

  const zoneCounts = useMemo(() => {
    const counts = { Ideal: 0, Pain: 0, Uselessness: 0 };
    metrics.forEach(m => { if (m.zone in counts) counts[m.zone as keyof typeof counts]++; });
    return counts;
  }, [metrics]);

  const top10Hubs = useMemo(() =>
    [...centrality].sort((a, b) => b.totalDegree - a.totalDegree).slice(0, 10),
    [centrality]
  );

  const maxDegree = top10Hubs[0]?.totalDegree || 1;

  // ── Phase 1: Health Score ──

  const healthScore = useMemo(() => {
    const totalNs = metrics.length || 1;
    const totalNodes = centrality.length || 1;
    const zoneHealth = (zoneCounts.Ideal / totalNs) * 100;
    const orphanHealth = (1 - orphans.length / totalNodes) * 100;
    const criticalHubs = centrality.filter(c => c.totalDegree > 100).length;
    const hubHealth = Math.max(0, 100 - criticalHubs * 15);
    const apiCoverage = api.totalResolvers > 0
      ? (1 - api.unbackedResolvers.length / api.totalResolvers) * 100
      : 100;
    return Math.round(zoneHealth * 0.4 + orphanHealth * 0.25 + hubHealth * 0.2 + apiCoverage * 0.15);
  }, [metrics, centrality, orphans, zoneCounts, api]);

  const healthGrade = useMemo(() => {
    if (healthScore >= 85) return 'A';
    if (healthScore >= 70) return 'B';
    if (healthScore >= 55) return 'C';
    if (healthScore >= 40) return 'D';
    return 'F';
  }, [healthScore]);

  // ── Phase 2: Refactoring Priorities ──

  const refactoringPriorities = useMemo(() => {
    return metrics
      .filter(m => m.zone === 'Pain' || m.distanceFromMainSequence > 0.5)
      .map(m => {
        const impactScore = m.distanceFromMainSequence * m.totalTypes * (1 + m.afferentCoupling / 10);
        let recommendation = '';
        if (m.instability > 0.8 && m.afferentCoupling > 10) {
          recommendation = 'Highly unstable with many dependents — stabilize by extracting abstractions';
        } else if (m.abstractness === 0 && m.totalTypes > 20) {
          recommendation = 'No abstractions — introduce interfaces to improve extensibility';
        } else if (m.distanceFromMainSequence > 0.7 && m.efferentCoupling > m.afferentCoupling) {
          recommendation = 'Far from main sequence — reduce outbound dependencies';
        } else if (m.distanceFromMainSequence > 0.7 && m.afferentCoupling > m.efferentCoupling) {
          recommendation = 'Far from main sequence — add abstractions to balance coupling';
        } else {
          recommendation = 'Review dependency balance — distance from ideal is significant';
        }
        return { ...m, impactScore, recommendation };
      })
      .sort((a, b) => b.impactScore - a.impactScore)
      .slice(0, 5);
  }, [metrics]);

  const maxImpact = refactoringPriorities[0]?.impactScore || 1;

  const filteredMetrics = useMemo(() => {
    const filtered = nsFilter ? metrics.filter(m => m.namespace.toLowerCase().includes(nsFilter.toLowerCase())) : metrics;
    return [...filtered].sort((a, b) => {
      const av = a[sortField];
      const bv = b[sortField];
      if (typeof av === 'string' && typeof bv === 'string') return sortDir === 'asc' ? av.localeCompare(bv) : bv.localeCompare(av);
      return sortDir === 'asc' ? (av as number) - (bv as number) : (bv as number) - (av as number);
    });
  }, [metrics, sortField, sortDir, nsFilter]);

  const handleSort = (field: SortField) => {
    if (sortField === field) setSortDir(d => d === 'asc' ? 'desc' : 'asc');
    else { setSortField(field); setSortDir('desc'); }
  };

  const sortIcon = (field: SortField) =>
    sortField === field ? (sortDir === 'asc' ? '↑' : '↓') : '';

  if (loading) {
    return (
      <div className="flex-1 flex items-center justify-center bg-[#13141a]">
        <div className="flex flex-col items-center gap-3">
          <div className="w-8 h-8 border-2 border-[#a09078] border-t-transparent rounded-full animate-spin" />
          <span className="text-slate-400 text-sm">Generating architectural report…</span>
        </div>
      </div>
    );
  }

  return (
    <div className="flex-1 overflow-y-auto bg-[#13141a] p-6">
      <div className="max-w-6xl mx-auto">

        {/* ── Header ── */}
        <div className="mb-6">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-5">
              <div>
                <h1 className="text-2xl font-bold text-slate-100 flex items-center gap-2">
                  <span className="material-symbols-outlined text-[#c4a882]">analytics</span>
                  {scopeLabel}
                </h1>
                <p className="text-sm text-slate-500 mt-1">
                  {metrics.length} namespaces · {centrality.length} nodes · {orphans.length} orphans
                </p>
              </div>
              {/* Phase 1: Health Grade */}
              <HealthGrade grade={healthGrade} score={healthScore} />
            </div>

            {/* Scope Dropdown */}
            <div className="flex items-center gap-2">
              <span className="material-symbols-outlined text-slate-500 text-base">account_tree</span>
              <select
                value={selectedScope}
                onChange={e => setSelectedScope(e.target.value)}
                className="bg-[#1e2026] border border-slate-700 rounded-md px-3 py-1.5 text-sm text-white focus:border-[#a09078] focus:ring-1 focus:ring-[#a09078] outline-none transition-colors appearance-none cursor-pointer pr-8"
                style={{ backgroundImage: `url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 12 12'%3E%3Cpath fill='%2394a3b8' d='M3 5l3 3 3-3'/%3E%3C/svg%3E")`, backgroundRepeat: 'no-repeat', backgroundPosition: 'right 8px center' }}
              >
                <option value="__federation__">Digital Backbone</option>
                {sortedRepos.map(r => (
                  <option key={r.id} value={r.id}>
                    {'  └ ' + r.name}
                  </option>
                ))}
              </select>
            </div>
          </div>
        </div>

        {/* Phase 4: Executive Insight */}
        <InsightBox icon="psychology">
          This codebase scores <strong>{healthScore}/100 ({healthGrade})</strong>.
          {zoneCounts.Pain > zoneCounts.Ideal
            ? ` ${Math.round(zoneCounts.Pain / (metrics.length || 1) * 100)}% of namespaces are in the Pain zone — structural coupling exceeds abstraction.`
            : ` ${Math.round(zoneCounts.Ideal / (metrics.length || 1) * 100)}% of namespaces are in the Ideal zone — well-balanced architecture.`}
          {orphans.length > centrality.length * 0.3 &&
            ` ${orphans.length} orphans (${Math.round(orphans.length / (centrality.length || 1) * 100)}% of nodes) suggest dead or unreferenced code.`}
        </InsightBox>

        {/* ── ① Executive Summary ── */}
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          <StatCard icon="hub" label="Total Nodes" value={centrality.length.toLocaleString()} detail={`across ${metrics.length} namespaces`} accent="#60a5fa" />
          <StatCard icon="warning" label="Orphans" value={orphans.length} detail="zero inbound edges" accent="#f97316" />
          <StatCard
            icon="speed"
            label="Zone Distribution"
            value={`${zoneCounts.Ideal} ideal`}
            detail={`${zoneCounts.Pain} pain · ${zoneCounts.Uselessness} useless`}
            accent="#10b981"
            delta={compData?.snapshotComparison?.painZoneDelta}
          />
          <StatCard icon="api" label="GQL Surface" value={api.totalResolvers} detail={`${api.unbackedResolvers.length} unbacked · ${api.totalDomainServices} services`} accent="#a78bfa" />
        </div>

        {/* ── Phase 6: Regression Detection ── */}
        {compData?.snapshotComparison && (() => {
          const comp = compData.snapshotComparison;
          const regressed = comp.namespaceDeltas.filter((d: { status: string }) => d.status === 'REGRESSED');
          const improved = comp.namespaceDeltas.filter((d: { status: string }) => d.status === 'IMPROVED');
          const newNs = comp.namespaceDeltas.filter((d: { status: string }) => d.status === 'NEW');
          if (regressed.length === 0 && improved.length === 0 && newNs.length === 0) return null;
          return (
            <>
              <SectionHeader icon="trending_down" title="Regression Detection" subtitle={`Comparing ${comp.baselineDate} → ${comp.currentDate}`} />
              {regressed.length > 0 && (
                <div className="bg-red-950/20 border border-red-800/30 rounded-lg p-4 mb-3">
                  <div className="flex items-center gap-2 mb-3">
                    <span className="material-symbols-outlined text-red-400">error</span>
                    <span className="text-sm font-semibold text-red-300">{regressed.length} Regressed Namespaces</span>
                  </div>
                  <div className="space-y-1.5">
                    {regressed.slice(0, 10).map((d: { namespace: string; oldZone: string; newZone: string; distanceDelta: number; typesDelta: number; couplingDelta: number }) => (
                      <div key={d.namespace} className="flex items-center gap-3 text-xs">
                        <button onClick={() => goToNamespace(d.namespace)} className="text-slate-200 hover:text-[#c4a882] hover:underline transition-colors cursor-pointer bg-transparent border-none p-0 text-left font-mono truncate max-w-[300px]" title={d.namespace}>{d.namespace}</button>
                        {d.oldZone !== d.newZone && (
                          <span className="text-slate-500">
                            <ZoneBadge zone={d.oldZone} /> → <ZoneBadge zone={d.newZone} />
                          </span>
                        )}
                        {d.typesDelta !== 0 && <span className="text-slate-500">types <DeltaBadge value={d.typesDelta} /></span>}
                        {d.couplingDelta !== 0 && <span className="text-slate-500">Ca <DeltaBadge value={d.couplingDelta} /></span>}
                      </div>
                    ))}
                  </div>
                </div>
              )}
              {improved.length > 0 && (
                <div className="bg-emerald-950/20 border border-emerald-800/30 rounded-lg p-4 mb-3">
                  <div className="flex items-center gap-2 mb-3">
                    <span className="material-symbols-outlined text-emerald-400">check_circle</span>
                    <span className="text-sm font-semibold text-emerald-300">{improved.length} Improved Namespaces</span>
                  </div>
                  <div className="space-y-1.5">
                    {improved.slice(0, 5).map((d: { namespace: string; oldZone: string; newZone: string }) => (
                      <div key={d.namespace} className="flex items-center gap-3 text-xs">
                        <span className="text-slate-300 font-mono truncate max-w-[300px]">{d.namespace}</span>
                        <span className="text-slate-500"><ZoneBadge zone={d.oldZone} /> → <ZoneBadge zone={d.newZone} /></span>
                      </div>
                    ))}
                  </div>
                </div>
              )}
              {newNs.length > 0 && (
                <div className="text-xs text-slate-500 mb-3">
                  <span className="text-blue-400 font-medium">{newNs.length} new namespaces</span> added since baseline
                </div>
              )}
            </>
          );
        })()}

        {/* ── Phase 2: Refactoring Priorities ── */}
        {refactoringPriorities.length > 0 && (
          <>
            <SectionHeader
              icon="construction"
              title="Top Refactoring Priorities"
              subtitle="Highest-impact namespaces ranked by distance × types × coupling"
            />
            <div className="space-y-2">
              {refactoringPriorities.map((p, i) => (
                <div key={p.namespace} className="bg-[#1e2026] border border-slate-700/50 rounded-lg p-4 flex items-start gap-4">
                  <div className="w-8 h-8 rounded-full bg-red-900/30 border border-red-700/40 flex items-center justify-center flex-shrink-0">
                    <span className="text-sm font-bold text-red-400">{i + 1}</span>
                  </div>
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 mb-1">
                      <button onClick={() => goToNamespace(p.namespace)} className="text-sm font-semibold text-slate-200 truncate hover:text-[#c4a882] hover:underline transition-colors cursor-pointer bg-transparent border-none p-0 text-left" title={`View ${p.namespace} in Explorer`}>{p.namespace}</button>
                      <ZoneBadge zone={p.zone} />
                      <span className="text-[10px] text-slate-500 ml-auto">Impact: {p.impactScore.toFixed(0)}</span>
                    </div>
                    <div className="h-1.5 bg-slate-800 rounded-full overflow-hidden mb-2">
                      <div className="h-full rounded-full bg-gradient-to-r from-red-500 to-red-400" style={{ width: `${(p.impactScore / maxImpact) * 100}%` }} />
                    </div>
                    <p className="text-xs text-slate-400">
                      <span className="material-symbols-outlined text-amber-400 text-xs align-middle mr-1">lightbulb</span>
                      {p.recommendation}
                    </p>
                    <div className="flex gap-4 mt-1 text-[11px] text-slate-500">
                      <span>Types: {p.totalTypes}</span>
                      <span>I: {p.instability.toFixed(2)}</span>
                      <span>A: {p.abstractness.toFixed(2)}</span>
                      <span>D: {p.distanceFromMainSequence.toFixed(2)}</span>
                      <span>Ca: {p.afferentCoupling}</span>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </>
        )}

        {/* ── ② Martin Metrics Table ── */}
        <SectionHeader
          icon="leaderboard"
          title="Package Health — Martin Metrics"
          subtitle="Instability (I), Abstractness (A), Distance from Main Sequence (D)"
        />

        <div className="flex items-center gap-3 mb-3">
          <div className="relative flex-1 max-w-xs">
            <span className="material-symbols-outlined absolute left-2.5 top-1/2 -translate-y-1/2 text-slate-500 text-sm">filter_alt</span>
            <input
              type="text"
              placeholder="Filter namespaces…"
              value={nsFilter}
              onChange={e => setNsFilter(e.target.value)}
              className="w-full bg-[#1e2026] border border-slate-700 rounded-md pl-9 pr-3 py-1.5 text-sm text-white placeholder-slate-500 focus:border-[#a09078] focus:ring-1 focus:ring-[#a09078] outline-none transition-colors"
            />
          </div>
          <span className="text-xs text-slate-500">{filteredMetrics.length} packages</span>
        </div>

        <div className="bg-[#1e2026] border border-slate-700/50 rounded-lg overflow-hidden">
          <div className="overflow-x-auto max-h-[420px] overflow-y-auto">
            <table className="w-full text-sm">
              <thead className="bg-[#18191f] sticky top-0 z-10">
                <tr className="text-left text-xs text-slate-400 uppercase tracking-wider">
                  {([
                    ['namespace', 'Namespace'],
                    ['totalTypes', 'Types'],
                    ['afferentCoupling', 'Ca'],
                    ['efferentCoupling', 'Ce'],
                    ['instability', 'I'],
                    ['abstractness', 'A'],
                    ['distanceFromMainSequence', 'D'],
                    ['zone', 'Zone'],
                  ] as [SortField, string][]).map(([field, label]) => (
                    <th
                      key={field}
                      className="px-3 py-2.5 cursor-pointer hover:text-[#c4a882] transition-colors select-none"
                      onClick={() => handleSort(field)}
                    >
                      {label} {sortIcon(field)}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-800">
                {filteredMetrics.map(m => (
                  <tr key={m.namespace} className="hover:bg-[#1a1b22] transition-colors">
                    <td className="px-3 py-2 text-slate-300 font-mono text-xs truncate max-w-[240px]" title={m.namespace}>{m.namespace}</td>
                    <td className="px-3 py-2 text-slate-400 text-center">{m.totalTypes}</td>
                    <td className="px-3 py-2 text-slate-400 text-center">{m.afferentCoupling}</td>
                    <td className="px-3 py-2 text-slate-400 text-center">{m.efferentCoupling}</td>
                    <td className="px-3 py-2 text-center">
                      <span className={m.instability > 0.7 ? 'text-red-400' : m.instability > 0.3 ? 'text-amber-400' : 'text-emerald-400'}>
                        {m.instability.toFixed(3)}
                      </span>
                    </td>
                    <td className="px-3 py-2 text-center">
                      <span className={m.abstractness > 0.8 ? 'text-amber-400' : 'text-slate-300'}>
                        {m.abstractness.toFixed(3)}
                      </span>
                    </td>
                    <td className="px-3 py-2 text-center">
                      <span className={m.distanceFromMainSequence > 0.5 ? 'text-red-400 font-semibold' : 'text-slate-300'}>
                        {m.distanceFromMainSequence.toFixed(3)}
                      </span>
                    </td>
                    <td className="px-3 py-2"><ZoneBadge zone={m.zone} /></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        {/* ── ③ Hub Analysis ── */}
        <SectionHeader
          icon="hub"
          title="Architectural Hubs — Top 10"
          subtitle="Most connected nodes by total degree (inbound + outbound)"
        />

        {/* Phase 4: Hub Insight */}
        {top10Hubs.length > 0 && top10Hubs[0].totalDegree > 100 && (
          <InsightBox icon="warning">
            <strong>{top10Hubs[0].node.name}</strong> has {top10Hubs[0].totalDegree} connections
            {top10Hubs[1] ? ` — ${(top10Hubs[0].totalDegree / top10Hubs[1].totalDegree).toFixed(1)}× the next largest hub` : ''}.
            {top10Hubs[0].totalDegree > 200
              ? ' This is a coupling bottleneck — consider splitting into sub-modules.'
              : ' Monitor this hub for further growth.'}
          </InsightBox>
        )}

        <div className="bg-[#1e2026] border border-slate-700/50 rounded-lg p-4">
          <div className="space-y-2">
            {top10Hubs.map((h, i) => (
              <div key={i} className="flex items-center gap-3">
                <span className="text-xs text-slate-500 w-5 text-right font-mono">{i + 1}</span>
                <div className="flex-1">
                  <div className="flex items-center justify-between mb-0.5">
                    <div className="flex items-center gap-2">
                      <button onClick={() => goToNode(h.node.namespace)} className="text-sm text-slate-200 font-medium truncate max-w-[200px] hover:text-[#c4a882] hover:underline transition-colors cursor-pointer bg-transparent border-none p-0 text-left" title={`View ${h.node.name} in Explorer`}>{h.node.name}</button>
                      {/* Phase 5: Risk Badge */}
                      <RiskBadge degree={h.totalDegree} />
                    </div>
                    <div className="flex items-center gap-2 text-xs">
                      <span className="text-blue-400">{h.inDegree}↓</span>
                      <span className="text-amber-400">{h.outDegree}↑</span>
                      <span className="text-slate-300 font-semibold">{h.totalDegree}</span>
                    </div>
                  </div>
                  <div className="h-1.5 bg-slate-800 rounded-full overflow-hidden">
                    <div
                      className="h-full rounded-full"
                      style={{
                        width: `${(h.totalDegree / maxDegree) * 100}%`,
                        background: `linear-gradient(90deg, #a09078, ${i === 0 ? '#e0c9a8' : '#7a6b5d'})`,
                      }}
                    />
                  </div>
                </div>
                <span className="text-[10px] text-slate-500 font-mono w-16 text-right">{h.node.type}</span>
              </div>
            ))}
          </div>
        </div>

        {/* ── ④ API Surface Gaps ── */}
        <SectionHeader
          icon="api"
          title="API Surface Analysis"
          subtitle={`${api.totalResolvers} resolvers · ${api.totalDomainServices} domain services`}
        />

        {/* Phase 4: API Insight */}
        {api.totalResolvers > 0 && api.unbackedResolvers.length > api.totalResolvers * 0.5 && (
          <InsightBox icon="info">
            {Math.round(api.unbackedResolvers.length / api.totalResolvers * 100)}% of resolvers lack backing services.
            {api.unbackedResolvers.length > 300
              ? ' This suggests auto-generated or scaffolded resolvers — review which ones need domain logic.'
              : ' Consider adding domain service implementations for high-priority resolvers.'}
          </InsightBox>
        )}

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {/* Unbacked Resolvers */}
          <div className="bg-[#1e2026] border border-slate-700/50 rounded-lg p-4">
            <div className="flex items-center gap-2 mb-3">
              <span className="material-symbols-outlined text-red-400 text-base">warning</span>
              <h3 className="text-sm font-semibold text-slate-200">Unbacked Resolvers</h3>
              <span className="text-xs text-slate-500 ml-auto">{api.unbackedResolvers.length}</span>
            </div>
            <div className="max-h-[200px] overflow-y-auto space-y-1">
              {api.unbackedResolvers.slice(0, 30).map(r => (
                <div key={r} className="text-xs font-mono text-slate-400 py-0.5 px-2 hover:bg-[#1a1b22] rounded">
                  {r}
                </div>
              ))}
              {api.unbackedResolvers.length > 30 && (
                <div className="text-xs text-slate-500 px-2">… and {api.unbackedResolvers.length - 30} more</div>
              )}
            </div>
          </div>

          {/* Unexposed Services */}
          <div className="bg-[#1e2026] border border-slate-700/50 rounded-lg p-4">
            <div className="flex items-center gap-2 mb-3">
              <span className="material-symbols-outlined text-amber-400 text-base">visibility_off</span>
              <h3 className="text-sm font-semibold text-slate-200">Unexposed Services</h3>
              <span className="text-xs text-slate-500 ml-auto">{api.unexposedServices.length}</span>
            </div>
            <div className="max-h-[200px] overflow-y-auto space-y-1">
              {api.unexposedServices.slice(0, 30).map(s => (
                <div key={s} className="text-xs font-mono text-slate-400 py-0.5 px-2 hover:bg-[#1a1b22] rounded">
                  {s}
                </div>
              ))}
              {api.unexposedServices.length > 30 && (
                <div className="text-xs text-slate-500 px-2">… and {api.unexposedServices.length - 30} more</div>
              )}
            </div>
          </div>
        </div>

        {/* ── ⑤ Orphan Analysis ── */}
        <SectionHeader
          icon="delete_sweep"
          title="Orphan Analysis"
          subtitle={`${orphans.length} nodes with zero inbound dependencies`}
        />

        {/* Phase 4: Orphan Insight */}
        {orphans.length > 0 && (() => {
          const highOutbound = orphans.filter(o => o.outboundCount > 5).length;
          return highOutbound > 0 ? (
            <InsightBox icon="lightbulb">
              {highOutbound} orphan{highOutbound > 1 ? 's' : ''} with 5+ outbound connections — likely utility code that should be promoted to shared libraries or explicitly depended upon.
            </InsightBox>
          ) : null;
        })()}

        <div className="bg-[#1e2026] border border-slate-700/50 rounded-lg overflow-hidden">
          <div className="overflow-x-auto max-h-[300px] overflow-y-auto">
            <table className="w-full text-sm">
              <thead className="bg-[#18191f] sticky top-0 z-10">
                <tr className="text-left text-xs text-slate-400 uppercase tracking-wider">
                  <th className="px-3 py-2.5">Name</th>
                  <th className="px-3 py-2.5">Type</th>
                  <th className="px-3 py-2.5">Namespace</th>
                  <th className="px-3 py-2.5 text-center">Outbound</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-800">
                {orphans.slice(0, 50).map(o => (
                  <tr key={o.id} className="hover:bg-[#1a1b22] transition-colors">
                    <td className="px-3 py-2 text-slate-300 font-mono text-xs">
                      <button onClick={() => goToNode(o.namespace)} className="hover:text-[#c4a882] hover:underline transition-colors cursor-pointer bg-transparent border-none p-0 text-left font-mono text-xs text-slate-300" title={`View ${o.name} in Explorer`}>{o.name}</button>
                    </td>
                    <td className="px-3 py-2 text-slate-400 text-xs">{o.type}</td>
                    <td className="px-3 py-2 text-slate-500 text-xs font-mono truncate max-w-[200px]" title={o.namespace}>{o.namespace}</td>
                    <td className="px-3 py-2 text-center text-slate-400 text-xs">{o.outboundCount}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {orphans.length > 50 && (
            <div className="px-3 py-2 text-xs text-slate-500 border-t border-slate-800 bg-[#18191f]">
              Showing first 50 of {orphans.length} orphans
            </div>
          )}
        </div>

        {/* Spacer */}
        <div className="h-12" />
      </div>
    </div>
  );
}
