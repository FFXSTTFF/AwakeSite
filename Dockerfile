# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/Awake.API/Awake.API.csproj",                      "src/Awake.API/"]
COPY ["src/Awake.Application/Awake.Application.csproj",       "src/Awake.Application/"]
COPY ["src/Awake.Domain/Awake.Domain.csproj",                 "src/Awake.Domain/"]
COPY ["src/Awake.Infrastructure/Awake.Infrastructure.csproj", "src/Awake.Infrastructure/"]

RUN dotnet restore "src/Awake.API/Awake.API.csproj"

COPY . .
RUN dotnet publish "src/Awake.API/Awake.API.csproj" -c Release -o /publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install system Chromium + dependencies needed by Playwright
RUN apt-get update && apt-get install -y --no-install-recommends \
    chromium \
    && rm -rf /var/lib/apt/lists/*

# Point Playwright to the system Chromium instead of its own bundled binary
ENV PLAYWRIGHT_CHROMIUM_PATH=/usr/bin/chromium

COPY --from=build /publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "Awake.API.dll"]
