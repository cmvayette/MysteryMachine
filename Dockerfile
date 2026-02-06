# Stage 1: Build Frontend
FROM node:20 AS frontend
WORKDIR /app
COPY dashboard/package*.json ./
RUN npm ci
COPY dashboard/ ./
RUN npm run build

# Stage 2: Build Backend
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend
WORKDIR /src
COPY ["src/SystemCartographer.Api/SystemCartographer.Api.csproj", "SystemCartographer.Api/"]
COPY ["src/SystemCartographer.Core/SystemCartographer.Core.csproj", "SystemCartographer.Core/"]
COPY ["src/SystemCartographer.Federation/SystemCartographer.Federation.csproj", "SystemCartographer.Federation/"]
COPY ["src/SystemCartographer.Linker/SystemCartographer.Linker.csproj", "SystemCartographer.Linker/"]
COPY ["src/SystemCartographer.Risk/SystemCartographer.Risk.csproj", "SystemCartographer.Risk/"]
COPY ["src/SystemCartographer.Scanner.CSharp/SystemCartographer.Scanner.CSharp.csproj", "SystemCartographer.Scanner.CSharp/"]
COPY ["src/SystemCartographer.Scanner.Sql/SystemCartographer.Scanner.Sql.csproj", "SystemCartographer.Scanner.Sql/"]
COPY Directory.Build.props .

# Restore dependencies
RUN dotnet restore "SystemCartographer.Api/SystemCartographer.Api.csproj"

# Copy source code
COPY src/ ./

# Build and Publish
RUN dotnet publish "SystemCartographer.Api/SystemCartographer.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

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

ENTRYPOINT ["dotnet", "SystemCartographer.Api.dll"]
