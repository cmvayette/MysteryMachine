# Stage 1: Build Frontend
FROM node:20 AS frontend
WORKDIR /app
COPY dashboard/package*.json ./
RUN npm ci --legacy-peer-deps
COPY dashboard/ ./
RUN npm run build

# Stage 2: Build Backend
# Stage 2: Build Backend
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend
WORKDIR /src
COPY ["src/DiagnosticStructuralLens.Api/DiagnosticStructuralLens.Api.csproj", "DiagnosticStructuralLens.Api/"]
COPY ["src/DiagnosticStructuralLens.Core/DiagnosticStructuralLens.Core.csproj", "DiagnosticStructuralLens.Core/"]
COPY ["src/DiagnosticStructuralLens.Federation/DiagnosticStructuralLens.Federation.csproj", "DiagnosticStructuralLens.Federation/"]
COPY ["src/DiagnosticStructuralLens.Linker/DiagnosticStructuralLens.Linker.csproj", "DiagnosticStructuralLens.Linker/"]
COPY ["src/DiagnosticStructuralLens.Risk/DiagnosticStructuralLens.Risk.csproj", "DiagnosticStructuralLens.Risk/"]
COPY ["src/DiagnosticStructuralLens.Graph/DiagnosticStructuralLens.Graph.csproj", "DiagnosticStructuralLens.Graph/"]
COPY ["src/DiagnosticStructuralLens.Scanner.CSharp/DiagnosticStructuralLens.Scanner.CSharp.csproj", "DiagnosticStructuralLens.Scanner.CSharp/"]
COPY ["src/DiagnosticStructuralLens.Scanner.Sql/DiagnosticStructuralLens.Scanner.Sql.csproj", "DiagnosticStructuralLens.Scanner.Sql/"]
COPY Directory.Build.props .
# Downgrade TFM to net8.0 for production Docker build
RUN sed -i 's/net10.0/net8.0/g' Directory.Build.props
# Downgrade package versions to 8.0.0 (since 10.0.0 doesn't exist publicly)
RUN sed -i 's/Version="10.[0-9].[0-9]"/Version="8.0.0"/g' DiagnosticStructuralLens.Api/DiagnosticStructuralLens.Api.csproj

# Restore dependencies
RUN dotnet restore "DiagnosticStructuralLens.Api/DiagnosticStructuralLens.Api.csproj"

# Copy source code
COPY src/ ./

# Re-apply downgrades because COPY overwrites modified files
RUN sed -i 's/net10.0/net8.0/g' Directory.Build.props
RUN sed -i 's/Version="10\.[^"]*"/Version="8.0.0"/g' DiagnosticStructuralLens.Api/DiagnosticStructuralLens.Api.csproj

# Build and Publish
RUN dotnet publish "DiagnosticStructuralLens.Api/DiagnosticStructuralLens.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Final Image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Copy Backend Build
COPY --from=backend /app/publish .

# Copy Frontend Build into wwwroot
# Ensure directory exists just in case
RUN mkdir -p wwwroot
COPY --from=frontend /app/dist ./wwwroot

ENTRYPOINT ["dotnet", "DiagnosticStructuralLens.Api.dll"]
