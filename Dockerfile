# Stage 1: Build + download Playwright Chromium
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/Awake.API/Awake.API.csproj",                      "src/Awake.API/"]
COPY ["src/Awake.Application/Awake.Application.csproj",       "src/Awake.Application/"]
COPY ["src/Awake.Domain/Awake.Domain.csproj",                 "src/Awake.Domain/"]
COPY ["src/Awake.Infrastructure/Awake.Infrastructure.csproj", "src/Awake.Infrastructure/"]

RUN dotnet restore "src/Awake.API/Awake.API.csproj"

COPY . .
RUN dotnet publish "src/Awake.API/Awake.API.csproj" -c Release -o /publish --no-restore

# playwright.sh is only in build output (not publish output) — run it from there
RUN src/Awake.API/bin/Release/net10.0/playwright.sh install --with-deps chromium

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Minimal Chromium runtime libs (Ubuntu Noble)
RUN apt-get update && apt-get install -y --no-install-recommends \
    libnss3 libnspr4 libatk1.0-0 libatk-bridge2.0-0 libcups2 libdrm2 \
    libdbus-1-3 libxkbcommon0 libx11-6 libxcomposite1 libxdamage1 \
    libxext6 libxfixes3 libxrandr2 libgbm1 libpango-1.0-0 libcairo2 \
    libasound2t64 \
    && rm -rf /var/lib/apt/lists/*

# Playwright browser cache from build stage (no external download at runtime)
COPY --from=build /root/.cache/ms-playwright /root/.cache/ms-playwright

COPY --from=build /publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "Awake.API.dll"]
