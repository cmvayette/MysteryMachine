# Packaging & Distribution Guide

This guide outlines how to package Diagnostic Structural Lens for distribution to end-users (Developers and Architects).

## 1. CLI Distribution (NuGet)

The CLI (`DiagnosticStructuralLens.Cli`) is configured as a .NET Tool. This allows users to install it globally using the `dotnet` CLI.

### Build & Pack

```bash
# Navigate to the solution root
dotnet pack src/DiagnosticStructuralLens.Cli -c Release -o ./artifacts
```

### Installation (Local/Private Feed)

```bash
# Install globally from local artifacts
dotnet tool install --global --add-source ./artifacts DiagnosticStructuralLens.Cli

# Update
dotnet tool update --global --add-source ./artifacts DiagnosticStructuralLens.Cli
```

### Usage

Once installed, the tool is available as `dsl`:

```bash
dsl scan --repo . --output my-snapshot.json
```

---

## 2. Full Stack Distribution (Docker)

The Docker image bundles the **Backend API** and the **Dashboard Frontend** into a single deployable unit. This is best for sharing the visualization interface.

### Build Image

```bash
docker build -t system-dsl:latest .
```

### Run (Server Mode)

This starts the dashboard on port 8080.

```bash
docker run -d -p 8080:8080 system-dsl:latest
```

Access: `http://localhost:8080`

### Run with Pre-loaded Snapshot

You can mount a snapshot file and load it automatically on startup:

```bash
docker run -p 8080:8080 \
  -v $(pwd)/samples/eshop_microservices.json:/app/snapshot.json \
  system-dsl:latest snapshot.json
```

## 3. Self-Contained Binaries (Manual)

For users without the .NET SDK installed, you can publish self-contained single-file executables.

### Release Command

```bash
# Windows x64
dotnet publish src/DiagnosticStructuralLens.Cli -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ./dist/win-x64

# macOS ARM64 (Apple Silicon)
dotnet publish src/DiagnosticStructuralLens.Cli -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true -o ./dist/osx-arm64

# Linux x64
dotnet publish src/DiagnosticStructuralLens.Cli -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o ./dist/linux-x64
```
