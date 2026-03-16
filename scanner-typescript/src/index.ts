#!/usr/bin/env node
import * as fs from 'node:fs';
import * as path from 'node:path';
import { execSync } from 'node:child_process';
import { scan, buildSnapshot } from './scanner.js';

async function main() {
  const args = process.argv.slice(2);

  let repoPath: string | undefined;
  let outputPath: string | undefined;
  let includeTests = false;
  let asSnapshot = false;
  let publish = false;
  let apiUrl = 'http://localhost:5170/load';

  for (let i = 0; i < args.length; i++) {
    switch (args[i]) {
      case '--repo':
        repoPath = args[++i];
        break;
      case '--output':
        outputPath = args[++i];
        break;
      case '--include-tests':
        includeTests = true;
        break;
      case '--snapshot':
        asSnapshot = true;
        break;
      case '--publish':
        publish = true;
        break;
      case '--url':
        apiUrl = args[++i];
        break;
      case '--help':
      case '-h':
        printHelp();
        process.exit(0);
    }
  }

  if (!repoPath) {
    console.error('Error: --repo is required');
    printHelp();
    process.exit(1);
  }

  // --publish implies --snapshot (API requires a full Snapshot)
  if (publish) {
    asSnapshot = true;
  }

  const fullRepoPath = path.resolve(repoPath);
  if (!fs.existsSync(fullRepoPath)) {
    console.error(`Error: Repository path not found: ${fullRepoPath}`);
    process.exit(1);
  }

  const result = await scan({
    repoPath: fullRepoPath,
    includeTests,
  });

  // Build output
  let output: unknown;
  if (asSnapshot) {
    const branch = getGitBranch(fullRepoPath);
    const commit = getGitCommit(fullRepoPath);
    output = buildSnapshot(result, fullRepoPath, branch, commit);
  } else {
    output = result;
  }

  const json = JSON.stringify(output, null, 2);

  // Save to file if requested
  if (outputPath) {
    fs.writeFileSync(outputPath, json);
    console.log(`\n✅ Output saved to: ${outputPath}`);
  }

  // Publish to DSL API if requested
  if (publish) {
    await publishSnapshot(json, apiUrl);
  }

  // Print to stdout only if neither --output nor --publish was specified
  if (!outputPath && !publish) {
    console.log(json);
  }
}

/**
 * Publishes a snapshot JSON payload to the DSL API's /load endpoint.
 */
async function publishSnapshot(json: string, url: string): Promise<void> {
  console.log(`\n🚀 Publishing snapshot to ${url}...`);

  const response = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: json,
  });

  if (response.ok) {
    const data = (await response.json()) as {
      repositories: number;
      totalCodeAtoms: number;
      totalLinks: number;
    };
    console.log('✅ Publish successful!');
    console.log(
      `   Repositories: ${data.repositories}, Atoms: ${data.totalCodeAtoms}, Links: ${data.totalLinks}`
    );
  } else {
    const errorText = await response.text();
    console.error(`❌ Publish failed (${response.status}): ${errorText}`);
    process.exit(1);
  }
}

function printHelp() {
  console.log(`
DSL TypeScript Scanner - Extract CodeAtoms from TypeScript projects

Usage: dsl-scanner-ts [options]

Options:
  --repo <path>       Path to repository root (required)
  --output <file>     Output file path (default: stdout)
  --include-tests     Include test files in scan
  --snapshot          Output as full Snapshot (with git metadata)
  --publish           Scan and publish snapshot directly to DSL API
  --url <endpoint>    API endpoint for publish (default: http://localhost:5170/load)
  --help, -h          Show this help

Examples:
  dsl-scanner-ts --repo ./my-project --publish
  dsl-scanner-ts --repo ./my-project --publish --url http://dsl-server:5170/load
  dsl-scanner-ts --repo ./my-project --publish --output snapshot.json
`);
}

function getGitBranch(repoPath: string): string | undefined {
  try {
    return execSync('git rev-parse --abbrev-ref HEAD', {
      cwd: repoPath,
      encoding: 'utf-8',
    }).trim();
  } catch {
    return undefined;
  }
}

function getGitCommit(repoPath: string): string | undefined {
  try {
    return execSync('git rev-parse --short HEAD', {
      cwd: repoPath,
      encoding: 'utf-8',
    }).trim();
  } catch {
    return undefined;
  }
}

main().catch((err) => {
  console.error('Fatal error:', err);
  process.exit(1);
});
